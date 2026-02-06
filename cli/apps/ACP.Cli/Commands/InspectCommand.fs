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

/// Execute the inspect command to validate and analyze ACP protocol trace files.
///
/// Reads a JSONL trace file, decodes each message, validates protocol compliance,
/// and reports any validation findings. Optionally records validated traces to output.
///
/// Returns 0 on success, 1 on error or validation failures.
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

            // Initialize optional recording writer.
            let withRecordWriter (fn: StreamWriter option -> int) : int =
                match recordPath with
                | None -> fn None
                | Some path ->
                    match Security.validateOutputPath path with
                    | Error err ->
                        Output.printError err
                        1
                    | Ok outPath ->
                        Output.printKeyValue "  Record to" outPath

                        try
                            use writer = new StreamWriter(outPath, false)
                            fn (Some writer)
                        with ex ->
                            Output.printError $"Failed to open record file: {ex.Message}"
                            1

            // Process trace frames
            withRecordWriter (fun recordWriter ->
                let writeRecordFrame (frame: TraceFrame) (direction: Codec.Direction) =
                    match recordWriter with
                    | None -> ()
                    | Some w ->
                        let directionStr =
                            match direction with
                            | Codec.Direction.FromClient -> "fromClient"
                            | Codec.Direction.FromAgent -> "fromAgent"

                        // Canonical JSONL shape: { ts, direction, json }.
                        let line =
                            JsonSerializer.Serialize(
                                {| ts = frame.ts
                                   direction = directionStr
                                   json = frame.json |}
                            )

                        w.WriteLine(line)

                let mutable state = Codec.CodecState.empty
                let messages = ResizeArray<Message>()
                let mutable frameCount = 0
                let mutable invalidFrames = 0
                let mutable directionErrors = 0
                let mutable decodeErrors = 0
                let mutable shouldContinue = true
                let seenFindings = HashSet<string>(StringComparer.Ordinal)

                Console.WriteLine()
                Output.printHeading "Processing messages"

                for line in lines do
                    if shouldContinue then
                        frameCount <- frameCount + 1

                        match TraceFrame.tryDecode line with
                        | None ->
                            invalidFrames <- invalidFrames + 1
                            Output.printWarning $"Frame {frameCount}: invalid trace frame JSON"

                            if stopOnError then
                                Output.printError "Stopping on first frame error (--stop-on-error)"
                                shouldContinue <- false
                        | Some frame ->
                            match Parsing.parseDirection frame.direction with
                            | None ->
                                directionErrors <- directionErrors + 1
                                Output.printWarning $"Frame {frameCount}: unknown direction '{frame.direction}'"

                                if stopOnError then
                                    Output.printError "Stopping on first direction error (--stop-on-error)"
                                    shouldContinue <- false
                            | Some direction ->
                                match Codec.decode direction state frame.json with
                                | Error e ->
                                    Output.printError $"[{frameCount}] Decode error: {e}"
                                    decodeErrors <- decodeErrors + 1

                                    if stopOnError then
                                        Output.printError "Stopping on first decode error (--stop-on-error)"
                                        shouldContinue <- false
                                | Ok(newState, msg) ->
                                    state <- newState
                                    messages.Add(msg)
                                    writeRecordFrame frame direction

                                    let dirStr =
                                        match direction with
                                        | Codec.Direction.FromClient -> "C→A"
                                        | Codec.Direction.FromAgent -> "A→C"

                                    Console.WriteLine($"[{messages.Count}] {dirStr} {MessageTag.render msg}")

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
                Output.printKeyValue "Invalid frames" (string invalidFrames)
                Output.printKeyValue "Direction errors" (string directionErrors)
                Output.printKeyValue "Decode errors" (string decodeErrors)
                Output.printKeyValue "Validation findings" (string spec.findings.Length)
                Output.printKeyValue "Unique issues" (string seenFindings.Count)

                if
                    invalidFrames > 0
                    || directionErrors > 0
                    || decodeErrors > 0
                    || spec.findings.Length > 0
                then
                    Console.WriteLine()
                    Output.printWarning "Issues detected - review findings above"
                    1
                else
                    Console.WriteLine()
                    Output.printSuccess "All messages valid!"
                    0)

        with ex ->
            Output.printError $"Failed to process trace: {ex.Message}"
            eprintfn "Stack trace: %s" ex.StackTrace
            1
