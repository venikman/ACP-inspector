module Acp.Inspector

// Suppress FS3261: Nullness warnings for interop with OpenTelemetry APIs that expect nullable Uri
#nowarn "3261"

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Net.Http
open System.Net.WebSockets
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Text.Json.Nodes
open System.Threading
open System.Threading.Tasks

open OpenTelemetry
open OpenTelemetry.Metrics
open OpenTelemetry.Resources
open OpenTelemetry.Trace
open OpenTelemetry.Instrumentation.Runtime

open Acp
open Acp.Domain
open Acp.Domain.JsonRpc
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Prompting
open Acp.Domain.SessionSetup
open Acp.Domain.Messaging

module private Cli =

    [<RequireQualifiedAccess>]
    type ExitCode =
        | Ok = 0
        | Usage = 2
        | RuntimeError = 10

    [<RequireQualifiedAccess>]
    type Direction =
        | FromClient
        | FromAgent

    module Direction =
        let tryParse (raw: string) =
            match raw.Trim().ToLowerInvariant() with
            | "fromclient"
            | "client"
            | "c2a"
            | "c->a" -> Some Direction.FromClient
            | "fromagent"
            | "agent"
            | "a2c"
            | "a->c" -> Some Direction.FromAgent
            | _ -> None

        let toCodec =
            function
            | Direction.FromClient -> Codec.Direction.FromClient
            | Direction.FromAgent -> Codec.Direction.FromAgent

        let render =
            function
            | Direction.FromClient -> "C→A"
            | Direction.FromAgent -> "A→C"

    [<CLIMutable>]
    type TraceFrame =
        { ts: DateTimeOffset
          direction: string
          json: string }

    module TraceFrame =
        let private jsonOptions =
            JsonSerializerOptions(
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            )

        let encode (frame: TraceFrame) =
            JsonSerializer.Serialize(frame, jsonOptions)

        let tryDecode (line: string) =
            try
                use doc = JsonDocument.Parse(line)
                let root = doc.RootElement

                if root.ValueKind <> JsonValueKind.Object then
                    None
                else
                    let mutable tsEl = Unchecked.defaultof<JsonElement>
                    let mutable directionEl = Unchecked.defaultof<JsonElement>
                    let mutable jsonEl = Unchecked.defaultof<JsonElement>

                    if
                        root.TryGetProperty("ts", &tsEl)
                        && root.TryGetProperty("direction", &directionEl)
                        && root.TryGetProperty("json", &jsonEl)
                    then
                        let tsOpt =
                            match tsEl.ValueKind with
                            | JsonValueKind.String ->
                                try
                                    Some(tsEl.GetDateTimeOffset())
                                with _ ->
                                    None
                            | JsonValueKind.Number ->
                                try
                                    Some(DateTimeOffset.FromUnixTimeMilliseconds(tsEl.GetInt64()))
                                with _ ->
                                    None
                            | _ -> None

                        let directionOpt =
                            match directionEl.ValueKind with
                            | JsonValueKind.String -> directionEl.GetString() |> Option.ofObj
                            | _ -> None

                        let jsonOpt =
                            match jsonEl.ValueKind with
                            | JsonValueKind.String -> jsonEl.GetString() |> Option.ofObj
                            | _ -> None

                        match tsOpt, directionOpt, jsonOpt with
                        | Some ts, Some direction, Some json ->
                            Some
                                { ts = ts
                                  direction = direction
                                  json = json }
                        | _ -> None
                    else
                        None
            with _ ->
                None

    type InspectConfig =
        { connectionId: SessionId
          stopOnFirstError: bool
          recordPath: string option
          printRaw: bool
          unstable: bool }

    type InspectState =
        { codec: Codec.CodecState
          rawCount: int
          messages: Message list
          seenFindings: HashSet<string>
          telemetryHintPrinted: bool }

    module InspectState =
        let empty =
            { codec = Codec.CodecState.empty
              rawCount = 0
              messages = []
              seenFindings = HashSet(StringComparer.Ordinal)
              telemetryHintPrinted = false }

    let private findingKey (f: Validation.ValidationFinding) =
        let failureCode = f.failure |> Option.map _.code |> Option.defaultValue ""
        let failureMessage = f.failure |> Option.map _.message |> Option.defaultValue ""

        sprintf "%A|%A|%A|%s|%s|%A|%A" f.lane f.severity f.subject failureCode failureMessage f.traceIndex f.note

    let private printFinding (f: Validation.ValidationFinding) =
        let lane = sprintf "%A" f.lane
        let sev = sprintf "%A" f.severity

        let subject =
            match f.subject with
            | Validation.Subject.Connection -> "connection"
            | Validation.Subject.Session sid -> $"session:{SessionId.value sid}"
            | Validation.Subject.PromptTurn(sid, turn) -> $"turn:{SessionId.value sid}#{turn}"
            | Validation.Subject.MessageAt(i, _) -> $"msg:{i}"
            | Validation.Subject.ToolCall toolCallId -> $"tool:{toolCallId}"

        match f.failure with
        | Some failure -> Console.Error.WriteLine($"[{lane}/{sev}] {failure.code} ({subject}) {failure.message}")
        | None ->
            let note = f.note |> Option.defaultValue ""
            Console.Error.WriteLine($"[{lane}/{sev}] ({subject}) {note}")

    type MetaHighlights =
        { traceparent: string option
          tracestate: string option
          baggage: string option }

    let private tryGetNumberValue (node: JsonNode) =
        match node with
        | :? JsonValue as v ->
            let mutable i = 0
            let mutable l = 0L
            let mutable d = 0.0
            let mutable f = 0f

            if v.TryGetValue(&i) then Some(float i)
            elif v.TryGetValue(&l) then Some(float l)
            elif v.TryGetValue(&d) then Some d
            elif v.TryGetValue(&f) then Some(float f)
            else None
        | _ -> None

    let private tryGetNumberProperty (o: JsonObject) (name: string) =
        let mutable value: JsonNode | null = null

        if o.TryGetPropertyValue(name, &value) then
            match value with
            | null -> None
            | node -> tryGetNumberValue node
        else
            None

    let private tryGetStringProperty (o: JsonObject) (name: string) =
        let mutable value: JsonNode | null = null

        if o.TryGetPropertyValue(name, &value) then
            match value with
            | null -> None
            | :? JsonValue as v ->
                try
                    Some(v.GetValue<string>())
                with :? InvalidOperationException ->
                    None
            | _ -> None
        else
            None

    let private tryGetObjectProperty (o: JsonObject) (name: string) =
        let mutable value: JsonNode | null = null

        if o.TryGetPropertyValue(name, &value) then
            match value with
            | :? JsonObject as obj -> Some obj
            | _ -> None
        else
            None

    let private tryGetArrayProperty (o: JsonObject) (name: string) =
        let mutable value: JsonNode | null = null

        if o.TryGetPropertyValue(name, &value) then
            match value with
            | :? JsonArray as arr -> Some arr
            | _ -> None
        else
            None

    let private tryGetStringArrayProperty (o: JsonObject) (name: string) =
        match tryGetArrayProperty o name with
        | None -> []
        | Some arr ->
            arr
            |> Seq.choose (function
                | null -> None
                | :? JsonValue as v ->
                    try
                        Some(v.GetValue<string>())
                    with :? InvalidOperationException ->
                        None
                | _ -> None)
            |> Seq.toList

    let private tryFindNumberBy (o: JsonObject) (predicate: string -> bool) =
        o
        |> Seq.tryPick (fun kvp ->
            if predicate kvp.Key then
                match kvp.Value with
                | null -> None
                | node -> tryGetNumberValue node |> Option.map (fun value -> kvp.Key, value)
            else
                None)

    let private tryGetContextObject (payload: JsonObject) =
        if isNull payload then
            None
        else
            tryGetObjectProperty payload "context"
            |> Option.orElseWith (fun () -> tryGetObjectProperty payload "contextWindow")
            |> Option.orElseWith (fun () -> tryGetObjectProperty payload "contextStatus")
            |> Option.orElseWith (fun () -> tryGetObjectProperty payload "context_status")

    let private tryComputeHeadroom (context: JsonObject) =
        let remaining =
            tryFindNumberBy context (fun key -> key.Contains("remaining", StringComparison.OrdinalIgnoreCase))

        let limit =
            tryFindNumberBy context (fun key ->
                key.Contains("limit", StringComparison.OrdinalIgnoreCase)
                || key.Contains("max", StringComparison.OrdinalIgnoreCase)
                || key.Contains("total", StringComparison.OrdinalIgnoreCase)
                || key.Contains("capacity", StringComparison.OrdinalIgnoreCase))

        match remaining, limit with
        | Some(remKey, rem), Some(limitKey, lim) when lim > 0.0 -> Some(remKey, rem, limitKey, lim, rem / lim)
        | _ -> None

    let private renderUsageSummary (label: string) (usage: JsonObject) =
        let pairs =
            usage
            |> Seq.choose (fun kvp ->
                match kvp.Value with
                | null -> None
                | node -> tryGetNumberValue node |> Option.map (fun value -> $"{kvp.Key}={value}"))
            |> Seq.toList

        if not pairs.IsEmpty then
            let pairsText = String.Join(", ", pairs)
            Console.Out.WriteLine($"  [draft] {label} {pairsText}")

    let private lowContextHeadroomThreshold = 0.10

    let private renderHeadroomWarning (context: JsonObject) =
        match tryComputeHeadroom context with
        | None -> ()
        | Some(remKey, rem, limitKey, lim, ratio) ->
            let percent = ratio * 100.0
            Console.Out.WriteLine($"  [draft] context headroom {remKey}={rem} {limitKey}={lim} ({percent:F1}%%)")

            if ratio < lowContextHeadroomThreshold then
                Console.Out.WriteLine($"  [warning] low context headroom ({percent:F1}%%)")

    let private tryGetMetaObject (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Object then
            None
        else
            let mutable metaEl = Unchecked.defaultof<JsonElement>

            if
                element.TryGetProperty("_meta", &metaEl)
                && metaEl.ValueKind = JsonValueKind.Object
            then
                Some metaEl
            else
                None

    let private tryGetStringElement (element: JsonElement) (name: string) =
        let mutable value = Unchecked.defaultof<JsonElement>

        if element.TryGetProperty(name, &value) && value.ValueKind = JsonValueKind.String then
            value.GetString() |> Option.ofObj
        else
            None

    let private tryExtractMetaHighlights (rawJson: string) : MetaHighlights option =
        try
            use doc = JsonDocument.Parse(rawJson)
            let root = doc.RootElement

            let metaOpt =
                tryGetMetaObject root
                |> Option.orElseWith (fun () ->
                    let mutable paramsEl = Unchecked.defaultof<JsonElement>

                    if root.TryGetProperty("params", &paramsEl) then
                        tryGetMetaObject paramsEl
                    else
                        None)
                |> Option.orElseWith (fun () ->
                    let mutable resultEl = Unchecked.defaultof<JsonElement>

                    if root.TryGetProperty("result", &resultEl) then
                        tryGetMetaObject resultEl
                    else
                        None)
                |> Option.orElseWith (fun () ->
                    let mutable paramsEl = Unchecked.defaultof<JsonElement>

                    if root.TryGetProperty("params", &paramsEl) then
                        let mutable updateEl = Unchecked.defaultof<JsonElement>

                        if paramsEl.TryGetProperty("update", &updateEl) then
                            tryGetMetaObject updateEl
                        else
                            None
                    else
                        None)

            match metaOpt with
            | None -> None
            | Some meta ->
                let highlights =
                    { traceparent = tryGetStringElement meta "traceparent"
                      tracestate = tryGetStringElement meta "tracestate"
                      baggage = tryGetStringElement meta "baggage" }

                if
                    highlights.traceparent.IsNone
                    && highlights.tracestate.IsNone
                    && highlights.baggage.IsNone
                then
                    None
                else
                    Some highlights
        with :? JsonException ->
            None

    let private renderTelemetryGuidance () =
        Console.Out.WriteLine("  [draft] telemetry export (otlp)")
        Console.Out.WriteLine("  OTEL_EXPORTER_OTLP_ENDPOINT=http://localhost:4318")
        Console.Out.WriteLine("  OTEL_EXPORTER_OTLP_PROTOCOL=http/protobuf")
        Console.Out.WriteLine("  OTEL_SERVICE_NAME=agent-name")
        Console.Out.WriteLine("  trace context keys: _meta.traceparent, _meta.tracestate, _meta.baggage")

    let private renderMetaHighlights (meta: MetaHighlights) =
        meta.traceparent
        |> Option.iter (fun v -> Console.Out.WriteLine($"  _meta.traceparent={v}"))

        meta.tracestate
        |> Option.iter (fun v -> Console.Out.WriteLine($"  _meta.tracestate={v}"))

        meta.baggage
        |> Option.iter (fun v -> Console.Out.WriteLine($"  _meta.baggage={v}"))

    let private renderProxySuccessorSummary (label: string) (p: Proxying.ProxySuccessorParams) =
        let metaKeys =
            p.meta
            |> Option.map (fun m -> m |> Seq.map (fun kvp -> kvp.Key) |> Seq.toList)
            |> Option.defaultValue []

        let parts =
            [ Some $"method={p.method}"
              if p.parameters.IsSome then Some "params=present" else None
              if metaKeys.Length > 0 then
                  let keysText = String.Join(", ", metaKeys)
                  Some $"metaKeys=[{keysText}]"
              else
                  None ]
            |> List.choose id

        let details = if parts.IsEmpty then "" else " " + String.Join(" ", parts)
        Console.Out.WriteLine($"  [draft] proxy/successor {label}{details}")

    let private renderProxySuccessorResponse (label: string) (methodName: string) (resultNodeOpt: JsonNode option) =
        let resultHint =
            if resultNodeOpt.IsSome then
                "result=present"
            else
                "result=null"

        Console.Out.WriteLine($"  [draft] proxy/successor {label} method={methodName} {resultHint}")

    let private renderProxySuccessorError (methodName: string) (err: Error) =
        Console.Out.WriteLine(
            $"  [draft] proxy/successor error method={methodName} code={err.code} message=\"{err.message}\""
        )

    let private renderMcpAcpServers (servers: McpServer list) =
        servers
        |> List.choose (function
            | McpServer.Acp v -> Some v
            | _ -> None)
        |> List.iter (fun v -> Console.Out.WriteLine($"  [draft] mcp transport=acp name={v.name} uuid={v.uuid}"))

    let private renderDraftDetails (msg: Message) =
        match msg with
        | Message.FromClient(ClientToAgentMessage.SessionNew p) -> renderMcpAcpServers p.mcpServers
        | Message.FromClient(ClientToAgentMessage.SessionLoad p) -> renderMcpAcpServers p.mcpServers
        | Message.FromClient(ClientToAgentMessage.ProxyInitialize p) ->
            Console.Out.WriteLine($"  [draft] proxy/initialize pv={p.protocolVersion}")
        | Message.FromAgent(AgentToClientMessage.ProxyInitializeResult r) ->
            Console.Out.WriteLine($"  [draft] proxy/initialize (result) pv={r.protocolVersion}")
        | Message.FromAgent(AgentToClientMessage.ProxyInitializeError err) ->
            Console.Out.WriteLine($"  [draft] proxy/initialize (error) code={err.code} message=\"{err.message}\"")
        | Message.FromClient(ClientToAgentMessage.ProxySuccessorRequest p) -> renderProxySuccessorSummary "request" p
        | Message.FromClient(ClientToAgentMessage.ProxySuccessorNotification p) ->
            renderProxySuccessorSummary "notification" p
        | Message.FromAgent(AgentToClientMessage.ProxySuccessorRequest p) -> renderProxySuccessorSummary "request" p
        | Message.FromAgent(AgentToClientMessage.ProxySuccessorNotification p) ->
            renderProxySuccessorSummary "notification" p
        | Message.FromClient(ClientToAgentMessage.ProxySuccessorResponse(methodName, resultNodeOpt)) ->
            renderProxySuccessorResponse "response" methodName resultNodeOpt
        | Message.FromAgent(AgentToClientMessage.ProxySuccessorResponse(methodName, resultNodeOpt)) ->
            renderProxySuccessorResponse "response" methodName resultNodeOpt
        | Message.FromClient(ClientToAgentMessage.ProxySuccessorError(methodName, err))
        | Message.FromAgent(AgentToClientMessage.ProxySuccessorError(methodName, err)) ->
            renderProxySuccessorError methodName err
        | Message.FromAgent(AgentToClientMessage.SessionUpdate u) ->
            match u.update with
            | SessionUpdate.Ext(tag, payload) ->
                match tag with
                | "session_info_update" ->
                    let title = tryGetStringProperty payload "title"

                    let metaKeys =
                        tryGetObjectProperty payload "_meta"
                        |> Option.map (fun o -> o |> Seq.map (fun kvp -> kvp.Key) |> Seq.toList)

                    let parts =
                        [ title |> Option.map (fun t -> $"title=\"{t}\"")
                          metaKeys
                          |> Option.bind (fun keys ->
                              if keys.Length > 0 then
                                  let keysText = String.Join(", ", keys)
                                  Some $"metaKeys=[{keysText}]"
                              else
                                  None) ]
                        |> List.choose id

                    let details = if parts.IsEmpty then "" else " " + String.Join(" ", parts)

                    Console.Out.WriteLine($"  [draft] session_info_update{details}")
                | "usage_update" ->
                    let usageObject =
                        tryGetObjectProperty payload "delta"
                        |> Option.orElseWith (fun () -> tryGetObjectProperty payload "usage")
                        |> Option.orElseWith (fun () -> tryGetObjectProperty payload "usageDelta")

                    usageObject |> Option.iter (renderUsageSummary "usage_update")

                    let contextOpt =
                        tryGetContextObject payload
                        |> Option.orElseWith (fun () -> usageObject |> Option.bind tryGetContextObject)

                    contextOpt |> Option.iter renderHeadroomWarning
                | _ -> ()
            | _ -> ()
        | Message.FromAgent(AgentToClientMessage.SessionPromptResult r) ->
            match r.usage with
            | None -> ()
            | Some usage ->
                renderUsageSummary "prompt_result_usage" usage
                tryGetContextObject usage |> Option.iter renderHeadroomWarning
        | Message.FromClient(ClientToAgentMessage.ExtRequest(methodName, _))
        | Message.FromClient(ClientToAgentMessage.ExtNotification(methodName, _))
        | Message.FromAgent(AgentToClientMessage.ExtRequest(methodName, _))
        | Message.FromAgent(AgentToClientMessage.ExtNotification(methodName, _)) when
            methodName.StartsWith("proxy/", StringComparison.OrdinalIgnoreCase)
            ->
            Console.Out.WriteLine($"  [draft] proxy-chain method={methodName}")
        | _ -> ()

    let private ensureRecordWriter (pathOpt: string option) =
        match pathOpt with
        | None -> None
        | Some path ->
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) |> string)
            |> ignore

            let fs = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read)
            let sw = new StreamWriter(fs, UTF8Encoding(false))
            sw.AutoFlush <- true
            Some sw

    let private withRecordWriter (pathOpt: string option) (f: StreamWriter option -> 'a) : 'a =
        match ensureRecordWriter pathOpt with
        | None -> f None
        | Some w ->
            use w = w
            f (Some w)

    let private recordFrame (writerOpt: StreamWriter option) (direction: Direction) (json: string) =
        match writerOpt with
        | None -> ()
        | Some w ->
            let frame =
                { ts = DateTimeOffset.UtcNow
                  direction =
                    match direction with
                    | Direction.FromClient -> "fromClient"
                    | Direction.FromAgent -> "fromAgent"
                  json = json }

            w.WriteLine(TraceFrame.encode frame)

    let private processRawJson
        (cfg: InspectConfig)
        (recordWriterOpt: StreamWriter option)
        (direction: Direction)
        (rawJson: string)
        (state: InspectState)
        =
        recordFrame recordWriterOpt direction rawJson

        let state =
            { state with
                rawCount = state.rawCount + 1 }

        let decodeResult = Codec.decode (Direction.toCodec direction) state.codec rawJson

        match decodeResult with
        | Error e ->
            Console.Error.WriteLine($"[decode-error] {Direction.render direction} {e}")
            state
        | Ok(codec', msg) ->
            let messages = state.messages @ [ msg ]

            Console.Out.WriteLine($"[{messages.Length - 1}] {Direction.render direction} {MessageTag.render msg}")

            if cfg.printRaw then
                Console.Out.WriteLine(rawJson)

            let metaOpt =
                if cfg.unstable then
                    tryExtractMetaHighlights rawJson
                else
                    None

            let mutable telemetryHintPrinted = state.telemetryHintPrinted

            if cfg.unstable then
                renderDraftDetails msg

                if not telemetryHintPrinted then
                    renderTelemetryGuidance ()
                    telemetryHintPrinted <- true

                metaOpt |> Option.iter renderMetaHighlights

            let spec =
                Validation.runWithValidation cfg.connectionId Protocol.spec messages cfg.stopOnFirstError None None

            for f in spec.findings do
                let key = findingKey f

                if state.seenFindings.Add key then
                    printFinding f

            { state with
                codec = codec'
                messages = messages
                telemetryHintPrinted = telemetryHintPrinted }

    let usage () =
        Console.Error.WriteLine($"ACP Inspector (schema={Spec.Schema}, protocolVersion={ProtocolVersion.current})")

        Console.Error.WriteLine(
            """
Usage:
  dotnet run --project cli/apps/ACP.Inspector/ACP.Inspector.fsproj -- <command> [options]

Commands:
  report --trace <path>
  replay --trace <path> [--connection-id <id>]
  record --out <path> --direction <fromClient|fromAgent>
  tap-stdin --direction <fromClient|fromAgent> [--record <path>] [--connection-id <id>]
  ws --url <ws://...> [--record <path>] [--connection-id <id>] [--stdin-send]
  sse --url <https://...> [--record <path>] [--connection-id <id>]
  proxy-stdio --client-cmd <cmd> --agent-cmd <cmd> [--record <path>] [--connection-id <id>]
  registry --registry <path> [--query <text>] [--capability <c1,c2>] [--id <id>] [--limit <n>] [--registry-sha256 <hex>]
  agent-manifest --id <id> [--out <path>] [--name <name>] [--version <ver>] [--schema-version <ver>]
                 [--description <text>] [--homepage <url>] [--repository <url>] [--authors <a1,a2>]
                 [--license <id>] [--capabilities <c1,c2>] [--auth-json <path>] [--distribution-json <path>] [--force]

Common options:
  --stop-on-first-error
  --print-raw
  --acp-unstable              Enable Draft RFD parsing/rendering (required for registry commands)
  --otel / --otel-console       Enable OpenTelemetry (includes .NET runtime metrics)
  --otlp-endpoint <url>
  --service-name <name>
"""
        )

    let private tryGetArg (name: string) (args: string[]) =
        let idx = args |> Array.tryFindIndex (fun a -> a = name)

        match idx with
        | None -> None
        | Some i -> if i + 1 >= args.Length then None else Some args.[i + 1]

    let private hasFlag (name: string) (args: string[]) =
        args |> Array.exists (fun a -> a = name)

    let private hasFlagEnabled (name: string) (args: string[]) =
        let flag = hasFlag name args

        if flag then
            true
        else
            args
            |> Array.exists (fun a ->
                if a.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase) then
                    let value = a.Substring(name.Length + 1).Trim()

                    value.Equals("on", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                    || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
                    || value = "1"
                else
                    false)

    let private connectionIdFromArgs (args: string[]) =
        match tryGetArg "--connection-id" args with
        | Some v when not (String.IsNullOrWhiteSpace v) -> SessionId v
        | _ -> SessionId "connection"

    let private buildConfig (args: string[]) =
        { connectionId = connectionIdFromArgs args
          stopOnFirstError = hasFlag "--stop-on-first-error" args
          recordPath = tryGetArg "--record" args
          printRaw = hasFlag "--print-raw" args
          unstable = hasFlagEnabled "--acp-unstable" args }

    let private noopDisposable: IDisposable =
        { new IDisposable with
            member _.Dispose() = () }

    let private tryReadTraceFile (tracePath: string) : Result<string[], int> =
        if not (File.Exists tracePath) then
            Console.Error.WriteLine($"[error] Trace file not found: {tracePath}")
            Error(int ExitCode.RuntimeError)
        else
            try
                Ok(File.ReadAllLines tracePath)
            with ex ->
                Console.Error.WriteLine($"[error] Failed to read trace file: {ex.Message}")
                Error(int ExitCode.RuntimeError)

    let startTelemetryFromArgs (args: string[]) : IDisposable =
        let consoleExporter = hasFlag "--otel" args || hasFlag "--otel-console" args

        let otlpEndpoint = tryGetArg "--otlp-endpoint" args

        let otlpEndpointUri =
            otlpEndpoint
            |> Option.bind (fun s ->
                match Uri.TryCreate(s, UriKind.Absolute) with
                | true, uri -> Some uri
                | false, _ ->
                    Console.Error.WriteLine($"[warning] Invalid OTLP endpoint URI: {s}")
                    None)

        let serviceName =
            tryGetArg "--service-name" args
            |> Option.filter (fun v -> not (String.IsNullOrWhiteSpace v))
            |> Option.defaultValue "acp-inspector"

        if (not consoleExporter) && otlpEndpointUri.IsNone then
            noopDisposable
        else
            let resource = ResourceBuilder.CreateDefault().AddService(serviceName)

            let configureExporters addOtlp addConsole builder =
                let builder =
                    match otlpEndpointUri with
                    | None -> builder
                    | Some endpoint -> addOtlp endpoint builder

                if consoleExporter then addConsole builder else builder

            let tracerBuilder =
                Sdk
                    .CreateTracerProviderBuilder()
                    .SetResourceBuilder(resource)
                    .AddSource(Observability.ActivitySourceName)

            let tracerBuilder =
                tracerBuilder
                |> configureExporters
                    (fun endpoint builder -> builder.AddOtlpExporter(fun o -> o.Endpoint <- endpoint))
                    (fun builder -> builder.AddConsoleExporter())

            let tracerProvider = tracerBuilder.Build()

            let meterBuilder =
                Sdk
                    .CreateMeterProviderBuilder()
                    .SetResourceBuilder(resource)
                    .AddMeter(Observability.MeterName)
                    .AddRuntimeInstrumentation()

            let meterBuilder =
                meterBuilder
                |> configureExporters
                    (fun endpoint builder -> builder.AddOtlpExporter(fun o -> o.Endpoint <- endpoint))
                    (fun builder -> builder.AddConsoleExporter())

            let meterProvider = meterBuilder.Build()

            { new IDisposable with
                member _.Dispose() =
                    tracerProvider.Dispose()
                    meterProvider.Dispose() }

    // ---- Agent registry (draft) ----

    type AgentManifest =
        { id: string
          name: string option
          version: string option
          schemaVersion: string option
          description: string option
          homepage: string option
          repository: string option
          authors: string list
          license: string option
          capabilities: string list
          auth: JsonNode option
          distributionTargets: string list }

    let private parseCsv (value: string) =
        value.Split(',', StringSplitOptions.RemoveEmptyEntries)
        |> Array.map (fun s -> s.Trim())
        |> Array.filter (fun s -> not (String.IsNullOrWhiteSpace s))
        |> Array.toList

    let private tryGetNodeProperty (o: JsonObject) (name: string) =
        let mutable value: JsonNode | null = null

        if o.TryGetPropertyValue(name, &value) then
            match value with
            | null -> None
            | node -> Some node
        else
            None

    let private parseAgentManifest (node: JsonNode) : Result<AgentManifest, string> =
        match node with
        | null -> Error "null entry"
        | :? JsonObject as o ->
            match tryGetStringProperty o "id" with
            | None -> Error "missing id"
            | Some id ->
                let distributionTargets =
                    tryGetObjectProperty o "distribution"
                    |> Option.map (fun d -> d |> Seq.map (fun kvp -> kvp.Key) |> Seq.toList |> List.sort)
                    |> Option.defaultValue []

                Ok
                    { id = id
                      name = tryGetStringProperty o "name"
                      version = tryGetStringProperty o "version"
                      schemaVersion = tryGetStringProperty o "schema_version"
                      description = tryGetStringProperty o "description"
                      homepage = tryGetStringProperty o "homepage"
                      repository = tryGetStringProperty o "repository"
                      authors = tryGetStringArrayProperty o "authors"
                      license = tryGetStringProperty o "license"
                      capabilities = tryGetStringArrayProperty o "capabilities"
                      auth = tryGetNodeProperty o "auth" |> Option.map (fun n -> n.DeepClone())
                      distributionTargets = distributionTargets }
        | _ -> Error "expected object"

    let private loadRegistryJson (root: JsonNode) : Result<AgentManifest list * string list, string> =
        try
            if isNull root then
                Error "registry.json is empty"
            else
                let entriesOpt =
                    match root with
                    | :? JsonArray as arr -> Some(arr |> Seq.toList)
                    | :? JsonObject as obj -> tryGetArrayProperty obj "agents" |> Option.map Seq.toList
                    | _ -> None

                match entriesOpt with
                | None -> Error "registry.json must be an array or contain an 'agents' array"
                | Some entries ->
                    let manifests = ResizeArray<AgentManifest>()
                    let warnings = ResizeArray<string>()

                    for (idx, entry) in entries |> Seq.indexed do
                        match parseAgentManifest entry with
                        | Ok manifest -> manifests.Add manifest
                        | Error err -> warnings.Add($"entry[{idx}]: {err}")

                    Ok(List.ofSeq manifests, List.ofSeq warnings)
        with ex ->
            Error ex.Message

    let private computeSha256 (bytes: byte[]) =
        use sha = SHA256.Create()

        sha.ComputeHash(bytes)
        |> Array.map (fun b -> b.ToString("x2"))
        |> String.concat ""

    let private maxRegistryBytes = 10L * 1024L * 1024L

    let private truncate (maxLen: int) (value: string) =
        if value.Length <= maxLen then
            value
        else
            value.Substring(0, maxLen - 3) + "..."

    let private matchesQuery (queryOpt: string option) (manifest: AgentManifest) =
        match queryOpt |> Option.map (fun v -> v.Trim()) with
        | None -> true
        | Some q when String.IsNullOrWhiteSpace q -> true
        | Some q ->
            let candidates =
                [ Some manifest.id
                  manifest.name
                  manifest.version
                  manifest.description
                  manifest.homepage
                  manifest.repository
                  manifest.license ]
                @ (manifest.authors |> List.map Some)
                @ (manifest.capabilities |> List.map Some)

            candidates
            |> List.choose id
            |> List.exists (fun v -> v.Contains(q, StringComparison.OrdinalIgnoreCase))

    let private matchesCapabilities (caps: string list) (manifest: AgentManifest) =
        caps
        |> List.forall (fun cap ->
            manifest.capabilities
            |> List.exists (fun c -> c.Equals(cap, StringComparison.OrdinalIgnoreCase)))

    let private renderRegistryEntry (manifest: AgentManifest) =
        let displayName =
            match manifest.name with
            | None -> manifest.id
            | Some name when name = manifest.id -> manifest.id
            | Some name -> $"{manifest.id} ({name})"

        let version = manifest.version |> Option.defaultValue "?"
        Console.Out.WriteLine($"- {displayName} v{version}")

        let details = ResizeArray<string>()

        manifest.description
        |> Option.map (truncate 120)
        |> Option.iter (fun d -> details.Add($"desc={d}"))

        if manifest.capabilities.Length > 0 then
            let capsText = String.Join(", ", manifest.capabilities)
            details.Add($"caps={capsText}")

        if manifest.distributionTargets.Length > 0 then
            let targetsText = String.Join(", ", manifest.distributionTargets)
            details.Add($"targets={targetsText}")

        if details.Count > 0 then
            let detailsText = String.Join(" ", details)
            Console.Out.WriteLine($"  {detailsText}")

    let private tryReadJsonFile (path: string) : Result<JsonNode, string> =
        if not (File.Exists path) then
            Error $"File not found: {path}"
        else
            try
                let node = JsonNode.Parse(File.ReadAllText(path))
                if isNull node then Error $"Empty JSON: {path}" else Ok node
            with ex ->
                Error ex.Message

    let private tryReadJsonArray (path: string) : Result<JsonArray, string> =
        match tryReadJsonFile path with
        | Ok(:? JsonArray as arr) -> Ok arr
        | Ok _ -> Error "Expected JSON array"
        | Error err -> Error err

    let private tryReadJsonObject (path: string) : Result<JsonObject, string> =
        match tryReadJsonFile path with
        | Ok(:? JsonObject as obj) -> Ok obj
        | Ok _ -> Error "Expected JSON object"
        | Error err -> Error err

    let private tryValidateUrl (label: string) (value: string) : Result<string, string> =
        match Uri.TryCreate(value, UriKind.Absolute) with
        | true, uri when uri.Scheme = Uri.UriSchemeHttp || uri.Scheme = Uri.UriSchemeHttps -> Ok value
        | _ -> Error $"Invalid {label} URL: {value}"

    let private requireUnstable (args: string[]) =
        if hasFlagEnabled "--acp-unstable" args then
            Ok()
        else
            Console.Error.WriteLine("[error] This command requires --acp-unstable.")
            Error(int ExitCode.Usage)

    let registry (args: string[]) =
        match requireUnstable args with
        | Error code -> code
        | Ok() ->
            match tryGetArg "--registry" args with
            | None ->
                usage ()
                int ExitCode.Usage
            | Some path ->
                if not (File.Exists path) then
                    Console.Error.WriteLine($"[error] Registry file not found: {path}")
                    int ExitCode.RuntimeError
                else
                    let readResult =
                        try
                            use fs = File.OpenRead(path)

                            if fs.Length > maxRegistryBytes then
                                Console.Error.WriteLine(
                                    $"[error] Registry file too large: {fs.Length} bytes (max {maxRegistryBytes})"
                                )

                                Error(int ExitCode.RuntimeError)
                            else
                                use ms = new MemoryStream()
                                fs.CopyTo(ms)
                                Ok(ms.ToArray())
                        with ex ->
                            Console.Error.WriteLine($"[error] Failed to read registry: {ex.Message}")
                            Error(int ExitCode.RuntimeError)

                    match readResult with
                    | Error code -> code
                    | Ok bytes ->
                        let hashCheckResult =
                            match tryGetArg "--registry-sha256" args with
                            | None -> Ok()
                            | Some expected ->
                                let actual = computeSha256 bytes

                                if actual.Equals(expected, StringComparison.OrdinalIgnoreCase) then
                                    Ok()
                                else
                                    Console.Error.WriteLine(
                                        $"[error] Registry sha256 mismatch (expected {expected}, got {actual}). Re-download the registry or update --registry-sha256."
                                    )

                                    Error(int ExitCode.RuntimeError)

                        match hashCheckResult with
                        | Error code -> code
                        | Ok() ->
                            let parseResult =
                                try
                                    let jsonText = Encoding.UTF8.GetString(bytes)
                                    let root = JsonNode.Parse(jsonText)

                                    if isNull root then
                                        Error "registry.json is empty"
                                    else
                                        Ok root
                                with ex ->
                                    Error ex.Message

                            match parseResult with
                            | Error msg ->
                                Console.Error.WriteLine($"[error] Failed to parse registry: {msg}")
                                int ExitCode.RuntimeError
                            | Ok root ->
                                match loadRegistryJson root with
                                | Error msg ->
                                    Console.Error.WriteLine($"[error] Failed to parse registry: {msg}")
                                    int ExitCode.RuntimeError
                                | Ok(manifests, warnings) ->
                                    for warning in warnings do
                                        Console.Error.WriteLine($"[warning] {warning}")

                                    let idFilter = tryGetArg "--id" args
                                    let queryFilter = tryGetArg "--query" args

                                    let requiredCaps =
                                        tryGetArg "--capability" args |> Option.map parseCsv |> Option.defaultValue []

                                    let filtered =
                                        manifests
                                        |> List.filter (fun m ->
                                            let idOk =
                                                match idFilter with
                                                | None -> true
                                                | Some id -> m.id.Equals(id, StringComparison.OrdinalIgnoreCase)

                                            idOk && matchesQuery queryFilter m && matchesCapabilities requiredCaps m)

                                    let filtered =
                                        match tryGetArg "--limit" args with
                                        | None -> filtered
                                        | Some raw ->
                                            match Int32.TryParse(raw) with
                                            | true, limit when limit > 0 ->
                                                if filtered.Length > limit then
                                                    filtered |> List.take limit
                                                else
                                                    filtered
                                            | _ -> filtered

                                    Console.Out.WriteLine($"registry: {path}")
                                    Console.Out.WriteLine($"agents: {manifests.Length} (matched={filtered.Length})")

                                    if filtered.IsEmpty then
                                        Console.Out.WriteLine("no matching agents")
                                    else
                                        for manifest in filtered do
                                            renderRegistryEntry manifest

                                    int ExitCode.Ok

    let agentManifest (args: string[]) =
        match requireUnstable args with
        | Error code -> code
        | Ok() ->
            match tryGetArg "--id" args with
            | None ->
                usage ()
                int ExitCode.Usage
            | Some id ->
                let outPath =
                    tryGetArg "--out" args
                    |> Option.filter (fun p -> not (String.IsNullOrWhiteSpace p))
                    |> Option.defaultValue (Path.Combine(id, "agent.json"))

                let force = hasFlag "--force" args

                if File.Exists outPath && not force then
                    Console.Error.WriteLine($"[error] Output already exists: {outPath} (use --force to overwrite)")
                    int ExitCode.RuntimeError
                else
                    let name = tryGetArg "--name" args |> Option.defaultValue id
                    let version = tryGetArg "--version" args |> Option.defaultValue "0.1.1"

                    let schemaVersion = tryGetArg "--schema-version" args |> Option.defaultValue "1"

                    let description =
                        tryGetArg "--description" args
                        |> Option.defaultValue "TODO: describe the agent."

                    let homepage =
                        tryGetArg "--homepage" args |> Option.defaultValue "https://example.com"

                    let repository =
                        tryGetArg "--repository" args |> Option.defaultValue "https://example.com/repo"

                    let license = tryGetArg "--license" args |> Option.defaultValue "UNLICENSED"

                    let authors =
                        tryGetArg "--authors" args |> Option.map parseCsv |> Option.defaultValue []

                    let capabilities =
                        tryGetArg "--capabilities" args |> Option.map parseCsv |> Option.defaultValue []

                    let authNodeResult =
                        match tryGetArg "--auth-json" args with
                        | None -> Ok(JsonArray() :> JsonNode)
                        | Some path -> tryReadJsonArray path |> Result.map (fun arr -> arr.DeepClone())

                    let distributionResult =
                        match tryGetArg "--distribution-json" args with
                        | None ->
                            let dist = JsonObject()
                            let target = JsonObject()
                            target["archive"] <- JsonValue.Create("https://example.com/agent-darwin-arm64.zip")
                            target["cmd"] <- JsonValue.Create("./agent")
                            let argsArray = JsonArray()
                            argsArray.Add(JsonValue.Create("acp"))
                            target["args"] <- argsArray
                            dist["darwin-aarch64"] <- target
                            Ok(dist)
                        | Some path -> tryReadJsonObject path |> Result.map (fun obj -> obj.DeepClone() :?> JsonObject)

                    let homepageResult = tryValidateUrl "homepage" homepage
                    let repositoryResult = tryValidateUrl "repository" repository

                    match homepageResult, repositoryResult, authNodeResult, distributionResult with
                    | Error err, _, _, _
                    | _, Error err, _, _
                    | _, _, Error err, _
                    | _, _, _, Error err ->
                        Console.Error.WriteLine($"[error] {err}")
                        int ExitCode.RuntimeError
                    | Ok homepage, Ok repository, Ok authNode, Ok distribution ->
                        let manifest = JsonObject()
                        manifest["id"] <- JsonValue.Create(id)
                        manifest["name"] <- JsonValue.Create(name)
                        manifest["version"] <- JsonValue.Create(version)
                        manifest["schema_version"] <- JsonValue.Create(schemaVersion)
                        manifest["description"] <- JsonValue.Create(description)
                        manifest["homepage"] <- JsonValue.Create(homepage)
                        manifest["repository"] <- JsonValue.Create(repository)

                        let authorsArray = JsonArray()
                        authors |> List.iter (fun a -> authorsArray.Add(JsonValue.Create(a)))
                        manifest["authors"] <- authorsArray

                        manifest["license"] <- JsonValue.Create(license)

                        let capsArray = JsonArray()
                        capabilities |> List.iter (fun c -> capsArray.Add(JsonValue.Create(c)))
                        manifest["capabilities"] <- capsArray

                        manifest["auth"] <- authNode
                        manifest["distribution"] <- distribution

                        let dir = Path.GetDirectoryName(outPath)

                        if not (String.IsNullOrWhiteSpace dir) then
                            Directory.CreateDirectory(dir) |> ignore

                        let json = manifest.ToJsonString(JsonSerializerOptions(WriteIndented = true))

                        File.WriteAllText(outPath, json, UTF8Encoding(false))
                        Console.Out.WriteLine($"manifest written: {outPath}")
                        int ExitCode.Ok

    let report (args: string[]) =
        match tryGetArg "--trace" args with
        | None ->
            usage ()
            int ExitCode.Usage
        | Some tracePath ->
            match tryReadTraceFile tracePath with
            | Error code -> code
            | Ok lines ->

                let mutable totalFrames = 0
                let mutable parseErrors = 0
                let mutable fromClientFrames = 0
                let mutable fromAgentFrames = 0

                let mutable requestCount = 0
                let mutable notificationCount = 0
                let mutable responseCount = 0
                let mutable unmatchedResponses = 0

                let pending = Dictionary<string, DateTimeOffset * string>(StringComparer.Ordinal)

                let methodCounts = Dictionary<string, int>(StringComparer.Ordinal)

                let latenciesByMethod =
                    Dictionary<string, ResizeArray<double>>(StringComparer.Ordinal)

                let oppositeDirection =
                    function
                    | Direction.FromClient -> Direction.FromAgent
                    | Direction.FromAgent -> Direction.FromClient

                let directionKey =
                    function
                    | Direction.FromClient -> "fromClient"
                    | Direction.FromAgent -> "fromAgent"

                let pendingKey dir id = $"{directionKey dir}|{id}"

                let incMethodCount methodName =
                    match methodCounts.TryGetValue(methodName) with
                    | true, v -> methodCounts.[methodName] <- v + 1
                    | false, _ -> methodCounts.[methodName] <- 1

                let addLatency methodName ms =
                    let bucket =
                        match latenciesByMethod.TryGetValue(methodName) with
                        | true, v -> v
                        | false, _ ->
                            let v = ResizeArray()
                            latenciesByMethod.[methodName] <- v
                            v

                    bucket.Add(ms)

                let tryParseJsonRpc (raw: string) =
                    try
                        use doc = JsonDocument.Parse(raw)
                        let root = doc.RootElement

                        if root.ValueKind <> JsonValueKind.Object then
                            None
                        else
                            let mutable methodEl = Unchecked.defaultof<JsonElement>
                            let hasMethod = root.TryGetProperty("method", &methodEl)

                            let methodOpt =
                                if hasMethod && methodEl.ValueKind = JsonValueKind.String then
                                    methodEl.GetString() |> Option.ofObj
                                else
                                    None

                            let mutable idEl = Unchecked.defaultof<JsonElement>
                            let hasId = root.TryGetProperty("id", &idEl)

                            let idOpt =
                                if hasId then
                                    match idEl.ValueKind with
                                    | JsonValueKind.String -> idEl.GetString() |> Option.ofObj
                                    | JsonValueKind.Number -> Some(idEl.GetRawText())
                                    | JsonValueKind.Null -> Some "null"
                                    | _ -> None
                                else
                                    None

                            let mutable tmp = Unchecked.defaultof<JsonElement>

                            let isResponse =
                                root.TryGetProperty("result", &tmp) || root.TryGetProperty("error", &tmp)

                            Some(methodOpt, idOpt, isResponse)
                    with _ ->
                        None

                for line in lines do
                    match TraceFrame.tryDecode line with
                    | None -> parseErrors <- parseErrors + 1
                    | Some frame ->
                        totalFrames <- totalFrames + 1

                        match Direction.tryParse frame.direction with
                        | None -> parseErrors <- parseErrors + 1
                        | Some dir ->
                            match dir with
                            | Direction.FromClient -> fromClientFrames <- fromClientFrames + 1
                            | Direction.FromAgent -> fromAgentFrames <- fromAgentFrames + 1

                            match tryParseJsonRpc frame.json with
                            | None -> parseErrors <- parseErrors + 1
                            | Some(methodOpt, idOpt, isResponse) ->
                                match methodOpt, isResponse with
                                | Some methodName, false ->
                                    // Request or notification
                                    incMethodCount methodName

                                    match idOpt with
                                    | Some id ->
                                        requestCount <- requestCount + 1
                                        pending.[pendingKey dir id] <- (frame.ts, methodName)
                                    | None -> notificationCount <- notificationCount + 1

                                | None, true ->
                                    // Response
                                    responseCount <- responseCount + 1

                                    match idOpt with
                                    | None -> unmatchedResponses <- unmatchedResponses + 1
                                    | Some id ->
                                        let requestKey = pendingKey (oppositeDirection dir) id

                                        match pending.TryGetValue(requestKey) with
                                        | true, (startedAt, reqMethod) ->
                                            pending.Remove(requestKey) |> ignore
                                            addLatency reqMethod (frame.ts - startedAt).TotalMilliseconds
                                        | false, _ -> unmatchedResponses <- unmatchedResponses + 1
                                | _ -> ()

                Console.Out.WriteLine($"trace: {tracePath}")
                Console.Out.WriteLine($"frames: {totalFrames} (parseErrors={parseErrors})")
                Console.Out.WriteLine($"by direction: fromClient={fromClientFrames} fromAgent={fromAgentFrames}")

                Console.Out.WriteLine(
                    $"rpc: requests={requestCount} notifications={notificationCount} responses={responseCount}"
                )

                Console.Out.WriteLine($"unmatched: requests={pending.Count} responses={unmatchedResponses}")

                if latenciesByMethod.Count > 0 then
                    Console.Out.WriteLine("")
                    Console.Out.WriteLine("latency_ms by method (count p50 p95 max):")

                    let rows =
                        latenciesByMethod
                        |> Seq.map (fun (KeyValue(methodName, values)) ->
                            let arr = values.ToArray()
                            Array.sortInPlace arr

                            let count = arr.Length

                            let p50 =
                                if count % 2 = 1 then
                                    arr.[count / 2]
                                else
                                    (arr.[(count / 2) - 1] + arr.[count / 2]) / 2.0

                            let p95Idx =
                                if count = 1 then
                                    0
                                else
                                    int (Math.Ceiling(0.95 * float (count - 1)))

                            let p95 = arr.[p95Idx]
                            let max = arr.[count - 1]
                            (methodName, count, p50, p95, max))
                        |> Seq.sortByDescending (fun (_, count, _, _, _) -> count)
                        |> Seq.toList

                    for (methodName, count, p50, p95, max) in rows do
                        Console.Out.WriteLine($"{methodName}: {count} {p50:F1} {p95:F1} {max:F1}")

                int ExitCode.Ok

    let replay (args: string[]) =
        match tryGetArg "--trace" args with
        | None ->
            usage ()
            int ExitCode.Usage
        | Some tracePath ->
            match tryReadTraceFile tracePath with
            | Error code -> code
            | Ok lines ->

                let cfg = buildConfig args
                let mutable state = InspectState.empty
                let recordWriterOpt = None

                for line in lines do
                    match TraceFrame.tryDecode line with
                    | None -> Console.Error.WriteLine($"[trace-error] could not parse line: {line}")
                    | Some frame ->
                        match Direction.tryParse frame.direction with
                        | None -> Console.Error.WriteLine($"[trace-error] unknown direction: {frame.direction}")
                        | Some dir -> state <- processRawJson cfg recordWriterOpt dir frame.json state

                Console.Out.WriteLine($"done: messages={state.messages.Length} rawFrames={state.rawCount}")
                int ExitCode.Ok

    let record (args: string[]) =
        match tryGetArg "--out" args, tryGetArg "--direction" args with
        | Some outPath, Some dirRaw ->
            match Direction.tryParse dirRaw with
            | None ->
                Console.Error.WriteLine($"Unknown direction: {dirRaw}")
                int ExitCode.Usage
            | Some dir ->
                let cfg = buildConfig args

                use writer =
                    ensureRecordWriter (Some outPath)
                    |> Option.defaultWith (fun () -> failwith "failed to open record file")

                let mutable state = InspectState.empty
                let recordWriterOpt = Some writer

                let rec loop () =
                    task {
                        let! (line: string | null) = Console.In.ReadLineAsync()

                        match line with
                        | null -> return ()
                        | line ->
                            state <- processRawJson cfg recordWriterOpt dir line state
                            return! loop ()
                    }

                (loop ()).GetAwaiter().GetResult()
                int ExitCode.Ok
        | _ ->
            usage ()
            int ExitCode.Usage

    let tapStdin (args: string[]) =
        match tryGetArg "--direction" args with
        | None ->
            usage ()
            int ExitCode.Usage
        | Some dirRaw ->
            match Direction.tryParse dirRaw with
            | None ->
                Console.Error.WriteLine($"Unknown direction: {dirRaw}")
                int ExitCode.Usage
            | Some dir ->
                let cfg = buildConfig args

                withRecordWriter cfg.recordPath (fun recordWriterOpt ->
                    let mutable state = InspectState.empty

                    let rec loop () =
                        task {
                            let! (line: string | null) = Console.In.ReadLineAsync()

                            match line with
                            | null -> return ()
                            | line ->
                                state <- processRawJson cfg recordWriterOpt dir line state
                                return! loop ()
                        }

                    (loop ()).GetAwaiter().GetResult()
                    int ExitCode.Ok)

    let ws (args: string[]) =
        match tryGetArg "--url" args with
        | None ->
            usage ()
            int ExitCode.Usage
        | Some url ->
            let cfg = buildConfig args

            withRecordWriter cfg.recordPath (fun recordWriterOpt ->
                let cts = new CancellationTokenSource()
                let token = cts.Token

                let ws = new ClientWebSocket()

                let connectTask =
                    task {
                        do! ws.ConnectAsync(Uri url, token)
                        Console.Error.WriteLine($"[ws] connected {url}")
                    }

                connectTask.GetAwaiter().GetResult()

                let mutable state = InspectState.empty

                let recvLoop =
                    task {
                        let buffer = Array.zeroCreate<byte> (1024 * 1024)

                        while ws.State = WebSocketState.Open && not token.IsCancellationRequested do
                            let seg = ArraySegment buffer
                            let! res = ws.ReceiveAsync(seg, token)

                            if res.MessageType = WebSocketMessageType.Close then
                                do! ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", token)
                                cts.Cancel()
                            else
                                let mutable total = res.Count
                                let mutable endOfMessage = res.EndOfMessage

                                while not endOfMessage do
                                    let! more =
                                        ws.ReceiveAsync(ArraySegment(buffer, total, buffer.Length - total), token)

                                    total <- total + more.Count
                                    endOfMessage <- more.EndOfMessage

                                let text = Encoding.UTF8.GetString(buffer, 0, total)
                                state <- processRawJson cfg recordWriterOpt Direction.FromAgent text state
                    }

                let sendLoop =
                    task {
                        if hasFlag "--stdin-send" args then
                            while ws.State = WebSocketState.Open && not token.IsCancellationRequested do
                                let! (line: string | null) = Console.In.ReadLineAsync()

                                match line with
                                | null -> cts.Cancel()
                                | line ->
                                    let bytes = Encoding.UTF8.GetBytes line
                                    do! ws.SendAsync(ArraySegment bytes, WebSocketMessageType.Text, true, token)
                                    state <- processRawJson cfg recordWriterOpt Direction.FromClient line state
                    }

                Task.WhenAll([| recvLoop :> Task; sendLoop :> Task |]).GetAwaiter().GetResult()
                int ExitCode.Ok)

    let sse (args: string[]) =
        match tryGetArg "--url" args with
        | None ->
            usage ()
            int ExitCode.Usage
        | Some url ->
            let cfg = buildConfig args

            withRecordWriter cfg.recordPath (fun recordWriterOpt ->
                use http = new HttpClient()
                use req = new HttpRequestMessage(HttpMethod.Get, url)
                use res = http.Send(req, HttpCompletionOption.ResponseHeadersRead)
                res.EnsureSuccessStatusCode() |> ignore

                use stream = res.Content.ReadAsStream()
                use reader = new StreamReader(stream, Encoding.UTF8)

                let mutable state = InspectState.empty

                // Minimal SSE support: treat each "data:" line as a JSON message.
                while not reader.EndOfStream do
                    match reader.ReadLine() with
                    | null -> ()
                    | line when line.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ->
                        let payload = line.Substring(5).TrimStart()

                        if not (String.IsNullOrWhiteSpace payload) then
                            state <- processRawJson cfg recordWriterOpt Direction.FromAgent payload state
                    | _ -> ()

                int ExitCode.Ok)

    let proxyStdio (args: string[]) =
        match tryGetArg "--client-cmd" args, tryGetArg "--agent-cmd" args with
        | Some clientCmd, Some agentCmd ->
            let cfg = buildConfig args

            withRecordWriter cfg.recordPath (fun recordWriterOpt ->
                let isWindows = OperatingSystem.IsWindows()

                let startShell (cmd: string) =
                    let psi = ProcessStartInfo()
                    psi.UseShellExecute <- false
                    psi.RedirectStandardInput <- true
                    psi.RedirectStandardOutput <- true
                    psi.RedirectStandardError <- true

                    if isWindows then
                        psi.FileName <- "cmd.exe"
                        psi.Arguments <- $"/c {cmd}"
                    else
                        let escaped = cmd.Replace("\"", "\\\"")
                        psi.FileName <- "/bin/bash"
                        psi.Arguments <- $"-lc \"{escaped}\""

                    let p = new Process(StartInfo = psi)

                    if not (p.Start()) then
                        failwith $"Failed to start: {cmd}"

                    p

                use client = startShell clientCmd
                use agent = startShell agentCmd

                let cts = new CancellationTokenSource()
                let token = cts.Token

                let mutable state = InspectState.empty

                let pipe (src: StreamReader) (dst: StreamWriter) (dir: Direction) =
                    task {
                        while not token.IsCancellationRequested do
                            let! (line: string | null) = src.ReadLineAsync()

                            match line with
                            | null -> cts.Cancel()
                            | line ->
                                do! dst.WriteLineAsync(line)
                                do! dst.FlushAsync()
                                state <- processRawJson cfg recordWriterOpt dir line state
                    }

                let stderrPump (name: string) (src: StreamReader) =
                    task {
                        while not token.IsCancellationRequested do
                            let! (line: string | null) = src.ReadLineAsync()

                            match line with
                            | null -> return ()
                            | line -> Console.Error.WriteLine($"[{name}] {line}")
                    }

                Task
                    .WhenAll(
                        [| pipe client.StandardOutput agent.StandardInput Direction.FromClient :> Task
                           pipe agent.StandardOutput client.StandardInput Direction.FromAgent :> Task
                           stderrPump "client:stderr" client.StandardError :> Task
                           stderrPump "agent:stderr" agent.StandardError :> Task |]
                    )
                    .GetAwaiter()
                    .GetResult()

                int ExitCode.Ok)
        | _ ->
            usage ()
            int ExitCode.Usage

[<EntryPoint>]
let main argv =
    try
        match argv |> List.ofArray with
        | [] ->
            Cli.usage ()
            int Cli.ExitCode.Usage
        | "--help" :: _
        | "-h" :: _ ->
            Cli.usage ()
            int Cli.ExitCode.Usage
        | cmd :: rest ->
            let args = rest |> List.toArray
            use _telemetry = Cli.startTelemetryFromArgs args

            match cmd with
            | "report" -> Cli.report args
            | "replay" -> Cli.replay args
            | "record" -> Cli.record args
            | "tap-stdin" -> Cli.tapStdin args
            | "ws" -> Cli.ws args
            | "sse" -> Cli.sse args
            | "proxy-stdio" -> Cli.proxyStdio args
            | "registry" -> Cli.registry args
            | "agent-manifest" -> Cli.agentManifest args
            | _ ->
                Console.Error.WriteLine($"Unknown command: {cmd}")
                Cli.usage ()
                int Cli.ExitCode.Usage
    with ex ->
        Console.Error.WriteLine(ex.ToString())
        int Cli.ExitCode.RuntimeError
