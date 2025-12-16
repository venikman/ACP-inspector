module Acp.Cli.Commands.ReplayCommand

open System
open System.IO
open Argu
open Acp.Cli.Common
open Acp.Cli.Commands.InspectCommand

[<RequireQualifiedAccess>]
type ReplayArgs =
    | [<MainCommand; ExactlyOnce>] Trace of path: string
    | [<AltCommandLine("-i")>] Interactive
    | [<AltCommandLine("-s")>] Stop_At of index: int
    | Verbose

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Trace _ -> "Path to trace file (JSONL format)"
            | Interactive -> "Pause after each message (press Enter to continue)"
            | Stop_At _ -> "Stop replay at message index"
            | Verbose -> "Show detailed message information"

/// Run replay command
let run (args: ParseResults<ReplayArgs>) : int =
    let tracePath = args.GetResult(ReplayArgs.Trace)
    let interactive = args.Contains(ReplayArgs.Interactive)
    let stopAt = args.TryGetResult(ReplayArgs.Stop_At)
    let verbose = args.Contains(ReplayArgs.Verbose)

    Output.printHeading $"Replaying trace: {tracePath}"

    if not (File.Exists(tracePath)) then
        Output.printError $"Trace file not found: {tracePath}"
        1
    else
        try
            let lines = File.ReadAllLines(tracePath)
            Output.printSuccess $"Loaded {lines.Length} frames"

            if interactive then
                Output.printInfo "Interactive mode: Press Enter to advance, 'q' to quit"

            Console.WriteLine()

            let mutable frameNum = 0
            let mutable shouldQuit = false

            for line in lines do
                if not shouldQuit then
                    frameNum <- frameNum + 1

                    match TraceFrame.tryDecode line with
                    | None -> Output.printWarning $"Frame {frameNum}: Invalid format"
                    | Some frame ->
                        // Display frame info
                        Output.printColored Output.Colors.cyan $"\n─── Frame {frameNum} "
                        Output.printColored Output.Colors.gray (String.replicate 60 "─")
                        Console.WriteLine()

                        Output.printKeyValue "Timestamp" (frame.ts.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                        Output.printKeyValue "Direction" frame.direction

                        if verbose then
                            Console.WriteLine()
                            Output.printColored Output.Colors.gray frame.json
                            Console.WriteLine()

                        // Check if we should stop
                        match stopAt with
                        | Some idx when frameNum >= idx ->
                            Console.WriteLine()
                            Output.printInfo $"Stopped at frame {frameNum} (--stop-at {idx})"
                            shouldQuit <- true
                        | _ -> ()

                        // Interactive pause
                        if interactive && not shouldQuit then
                            Console.Write("\n[Enter=next, q=quit] ")
                            let input = Console.ReadLine()

                            if not (isNull input) && input.Trim().ToLower() = "q" then
                                shouldQuit <- true
                                Output.printInfo "Replay stopped by user"

            Console.WriteLine()
            Output.printSuccess $"Replayed {frameNum} frames"
            0

        with ex ->
            Output.printError $"Replay failed: {ex.Message}"
            1
