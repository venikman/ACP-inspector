module Acp.Cli.Commands.InspectCommand

open System
open System.Collections.Generic
open System.IO
open System.Text.Json
open Argu
open Acp
open Acp.Domain
open Acp.Domain.Messaging
open Acp.Domain.PrimitivesAndParties
open Acp.Cli.Common

[<RequireQualifiedAccess>]
type InspectArgs =
    | [<MainCommand; ExactlyOnce>] Trace of path: string
    | [<AltCommandLine("-s")>] Stop_On_Error
    | [<AltCommandLine("-r")>] Record of path: string
    | Raw

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Trace _ -> "Path to trace file (JSONL format)"
            | Stop_On_Error -> "Stop processing on first validation error"
            | Record _ -> "Record validated trace to file"
            | Raw -> "Print raw JSON messages"

[<CLIMutable>]
type TraceFrame =
    { ts: DateTimeOffset
      direction: string
      json: string }

module TraceFrame =
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

let private parseDirection (dirStr: string) =
    match dirStr.Trim().ToLowerInvariant() with
    | "fromclient"
    | "client"
    | "c2a"
    | "c->a" -> Some Codec.Direction.FromClient
    | "fromagent"
    | "agent"
    | "a2c"
    | "a->c" -> Some Codec.Direction.FromAgent
    | _ -> None

let private methodTag (msg: Message) =
    match msg with
    | Message.FromClient c ->
        match c with
        | ClientToAgentMessage.Initialize p -> $"initialize pv={p.protocolVersion}"
        | ClientToAgentMessage.SessionNew _ -> "session/new"
        | ClientToAgentMessage.SessionPrompt _ -> "session/prompt"
        | ClientToAgentMessage.SessionCancel _ -> "session/cancel"
        | _ -> "other-client-msg"
    | Message.FromAgent a ->
        match a with
        | AgentToClientMessage.InitializeResult r -> $"initialize (result) pv={r.protocolVersion}"
        | AgentToClientMessage.SessionNewResult _ -> "session/new (result)"
        | AgentToClientMessage.SessionPromptResult _ -> "session/prompt (result)"
        | AgentToClientMessage.SessionUpdate _ -> "session/update"
        | _ -> "other-agent-msg"

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
    | Some failure ->
        Output.printError $"[{lane}/{sev}] {failure.code} ({subject})"
        eprintfn "  %s" failure.message
    | None ->
        let note = f.note |> Option.defaultValue ""
        Output.printWarning $"[{lane}/{sev}] ({subject}) {note}"

/// Run inspect command
let run (args: ParseResults<InspectArgs>) : int =
    let tracePath = args.GetResult(InspectArgs.Trace)
    let stopOnError = args.Contains(InspectArgs.Stop_On_Error)
    let recordPath = args.TryGetResult(InspectArgs.Record)
    let printRaw = args.Contains(InspectArgs.Raw)

    Output.printHeading $"Inspecting trace: {tracePath}"

    match Security.validateInputPath tracePath with
    | Error err ->
        Output.printError err
        1
    | Ok validatedPath ->
        Output.printInfo "Loading trace file..."

        try
            let lines = File.ReadAllLines(validatedPath)
            Output.printSuccess $"Loaded {lines.Length} frames"

            Output.printInfo "Configuration:"
            Output.printKeyValue "  Stop on error" (if stopOnError then "yes" else "no")
            Output.printKeyValue "  Print raw" (if printRaw then "yes" else "no")

            match recordPath with
            | Some path -> Output.printKeyValue "  Record to" path
            | None -> ()

            // Process trace frames
            let mutable state = Codec.CodecState.empty
            let messages = ResizeArray<Message>()
            let mutable frameCount = 0
            let mutable decodeErrors = 0
            let seenFindings = HashSet<string>(StringComparer.Ordinal)

            Console.WriteLine()
            Output.printHeading "Processing messages"

            for line in lines do
                match TraceFrame.tryDecode line with
                | None ->
                    Output.printWarning $"Skipped invalid frame at line {frameCount + 1}"
                    frameCount <- frameCount + 1
                | Some frame ->
                    frameCount <- frameCount + 1

                    match parseDirection frame.direction with
                    | None -> Output.printWarning $"Unknown direction '{frame.direction}' at frame {frameCount}"
                    | Some direction ->
                        match Codec.decode direction state frame.json with
                        | Error e ->
                            Output.printError $"[{frameCount}] Decode error: {e}"
                            decodeErrors <- decodeErrors + 1

                            if stopOnError then
                                Output.printError "Stopping on first error"
                                ()
                        | Ok(newState, msg) ->
                            state <- newState
                            messages.Add(msg)

                            let dirStr =
                                match direction with
                                | Codec.Direction.FromClient -> "C→A"
                                | Codec.Direction.FromAgent -> "A→C"

                            Console.WriteLine($"[{messages.Count}] {dirStr} {methodTag msg}")

                            if printRaw then
                                Output.printColored Output.Colors.gray frame.json
                                Console.WriteLine()

            // Run validation
            Console.WriteLine()
            Output.printHeading "Validation results"

            let connectionId = SessionId(Guid.NewGuid().ToString())
            let messageList = messages |> Seq.toList

            let spec =
                Validation.runWithValidation connectionId Protocol.spec messageList stopOnError None None

            for f in spec.findings do
                let key =
                    sprintf "%A|%A|%A" f.lane f.severity (f.failure |> Option.map _.code |> Option.defaultValue "")

                if seenFindings.Add(key) then
                    printFinding f

            // Summary
            Console.WriteLine()
            Output.printHeading "Summary"
            Output.printKeyValue "Total frames" (string frameCount)
            Output.printKeyValue "Messages decoded" (string messages.Count)
            Output.printKeyValue "Decode errors" (string decodeErrors)
            Output.printKeyValue "Validation findings" (string spec.findings.Length)
            Output.printKeyValue "Unique issues" (string seenFindings.Count)

            if decodeErrors > 0 || spec.findings.Length > 0 then
                Console.WriteLine()
                Output.printWarning "Issues detected - review findings above"
                1
            else
                Console.WriteLine()
                Output.printSuccess "All messages valid!"
                0

        with ex ->
            Output.printError $"Failed to process trace: {ex.Message}"
            eprintfn "Stack trace: %s" ex.StackTrace
            1
