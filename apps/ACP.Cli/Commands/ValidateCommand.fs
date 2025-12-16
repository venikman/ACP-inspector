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

let private parseDirection (dirStr: string) =
    match dirStr.Trim().ToLowerInvariant() with
    | "client"
    | "fromclient"
    | "c2a"
    | "c->a" -> Some Codec.Direction.FromClient
    | "agent"
    | "fromagent"
    | "a2c"
    | "a->c" -> Some Codec.Direction.FromAgent
    | _ -> None

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

/// Run validate command (read from stdin)
let run (args: ParseResults<ValidateArgs>) : int =
    let directionStr = args.GetResult(ValidateArgs.Direction)
    let stopOnError = args.Contains(ValidateArgs.Stop_On_Error)
    let verbose = args.Contains(ValidateArgs.Verbose)

    match parseDirection directionStr with
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
        let mutable messages: Message list = []
        let mutable lineNum = 0
        let mutable decodeErrors = 0
        let mutable validationErrors = 0

        try
            let mutable line = Console.In.ReadLine()

            while line <> null do
                lineNum <- lineNum + 1

                if not (String.IsNullOrWhiteSpace(line)) then
                    match Codec.decode direction state line with
                    | Error e ->
                        Output.printError $"Line {lineNum}: Decode error"
                        eprintfn "  %A" e
                        decodeErrors <- decodeErrors + 1

                        if stopOnError then () else line <- Console.In.ReadLine()
                    | Ok(newState, msg) ->
                        state <- newState
                        messages <- messages @ [ msg ]

                        if verbose then
                            Output.printSuccess $"Line {lineNum}: Valid message"

                        // Validate accumulated messages
                        let connectionId = SessionId(Guid.NewGuid().ToString())

                        let spec =
                            Validation.runWithValidation connectionId Protocol.spec messages false None None

                        if spec.findings.Length > 0 then
                            for f in spec.findings do
                                if f.severity = Validation.Severity.Error then
                                    validationErrors <- validationErrors + 1

                                if verbose || f.severity = Validation.Severity.Error then
                                    printFinding f

                            if stopOnError && validationErrors > 0 then
                                ()
                            else
                                line <- Console.In.ReadLine()
                        else
                            line <- Console.In.ReadLine()
                else
                    line <- Console.In.ReadLine()

            // Summary
            if verbose then
                Console.WriteLine()
                Output.printHeading "Validation summary"
                Output.printKeyValue "Lines read" (string lineNum)
                Output.printKeyValue "Messages decoded" (string messages.Length)
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
