module Acp.Cli.Commands.ValidateCommand

open System
open Argu
open Acp
open Acp.Domain
open Acp.Domain.Messaging
open Acp.Domain.PrimitivesAndParties
open Acp.Cli.Common

[<RequireQualifiedAccess>]
type ValidateArgs =
    | [<AltCommandLine("-d"); Mandatory>] Direction of direction: string
    | [<AltCommandLine("-s")>] Stop_On_Error
    | Verbose

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Direction _ -> "Message direction: client, agent, c2a, a2c"
            | Stop_On_Error -> "Stop on first validation error"
            | Verbose -> "Print detailed validation output"

let private printFinding (f: Validation.ValidationFinding) =
    let sev =
        match f.severity with
        | Validation.Severity.Error -> Output.Colors.red
        | Validation.Severity.Warning -> Output.Colors.yellow
        | Validation.Severity.Info -> Output.Colors.cyan

    let sevStr = sprintf "%A" f.severity
    let laneStr = sprintf "%A" f.lane

    match f.failure with
    | Some failure ->
        Output.printColored sev $"[{laneStr}/{sevStr}] {failure.code}\n"
        eprintfn "  %s" failure.message
    | None ->
        let note = f.note |> Option.defaultValue ""
        Output.printColored sev $"[{laneStr}/{sevStr}] {note}\n"

/// Execute the validate command to check ACP messages from standard input.
///
/// Reads JSON-RPC messages from stdin, decodes them, accumulates the message history,
/// and validates the entire sequence against the ACP protocol specification.
///
/// Returns 0 if all messages are valid, 1 if there are decode or validation errors.
let run (args: ParseResults<ValidateArgs>) : int =
    let directionStr = args.GetResult(ValidateArgs.Direction)
    let stopOnError = args.Contains(ValidateArgs.Stop_On_Error)
    let verbose = args.Contains(ValidateArgs.Verbose)

    match Parsing.parseDirection directionStr with
    | None ->
        Output.printError $"Invalid direction '{directionStr}'"
        Output.printInfo "Valid directions: client, agent, c2a, a2c, fromclient, fromagent"
        1
    | Some direction ->
        if verbose then
            Output.printInfo "Reading JSON-RPC messages from stdin..."
            Output.printInfo $"Direction: {directionStr}"
            Output.printInfo "Press Ctrl+D (Unix) or Ctrl+Z (Windows) to finish"
            Console.WriteLine()

        let mutable state = Codec.CodecState.empty
        let messages = ResizeArray<Message>()
        let mutable lineNum = 0
        let mutable decodeErrors = 0
        let mutable validationErrors = 0

        try
            let mutable line = Console.In.ReadLine()
            let mutable shouldContinue = true

            while line <> null && shouldContinue do
                lineNum <- lineNum + 1

                if not (String.IsNullOrWhiteSpace(line)) then
                    match Codec.decode direction state line with
                    | Error e ->
                        Output.printError $"Line {lineNum}: Decode error"
                        eprintfn "  %A" e
                        decodeErrors <- decodeErrors + 1

                        if stopOnError then
                            Output.printError "Stopping on first decode error"
                            shouldContinue <- false
                    | Ok(newState, msg) ->
                        state <- newState
                        messages.Add(msg)

                        if verbose then
                            Output.printSuccess $"Line {lineNum}: Valid message"

                if shouldContinue then
                    line <- Console.In.ReadLine()

            // Validate all collected messages
            let connectionId = SessionId(Guid.NewGuid().ToString())
            let messageList = messages |> Seq.toList

            let spec =
                Validation.runWithValidation connectionId Protocol.spec messageList false None None

            let mutable stopValidation = false

            for f in spec.findings do
                if not stopValidation then
                    if f.severity = Validation.Severity.Error then
                        validationErrors <- validationErrors + 1

                    if verbose || f.severity = Validation.Severity.Error then
                        printFinding f

                        if stopOnError && f.severity = Validation.Severity.Error then
                            Output.printError "Stopping on first validation error"
                            stopValidation <- true

            // Summary
            if verbose then
                Console.WriteLine()
                Output.printHeading "Validation summary"
                Output.printKeyValue "Lines read" (string lineNum)
                Output.printKeyValue "Messages decoded" (string messages.Count)
                Output.printKeyValue "Decode errors" (string decodeErrors)
                Output.printKeyValue "Validation errors" (string validationErrors)

            if decodeErrors > 0 || validationErrors > 0 then
                1
            else
                if verbose then
                    Output.printSuccess "All messages valid!"

                0
        with ex ->
            Output.printError $"Validation failed: {ex.Message}"
            1
