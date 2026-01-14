module Acp.Cli.Program

open System
open System.Reflection
open Argu
open Acp.Cli.Common
open Acp.Cli.Commands

/// CLI version - loaded from assembly if available
let private getVersion () =
    let assembly = Assembly.GetExecutingAssembly()
    let version = assembly.GetName().Version

    if version <> null then
        $"{version.Major}.{version.Minor}.{version.Build}"
    else
        "0.1.1"

[<RequireQualifiedAccess>]
type Command =
    | [<CliPrefix(CliPrefix.None)>] Inspect of ParseResults<InspectCommand.InspectArgs>
    | [<CliPrefix(CliPrefix.None)>] Validate of ParseResults<ValidateCommand.ValidateArgs>
    | [<CliPrefix(CliPrefix.None)>] Replay of ParseResults<ReplayCommand.ReplayArgs>
    | [<CliPrefix(CliPrefix.None)>] Analyze of ParseResults<AnalyzeCommand.AnalyzeArgs>
    | [<CliPrefix(CliPrefix.None)>] Benchmark of ParseResults<BenchmarkCommand.BenchmarkArgs>

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Inspect _ -> "Inspect and validate ACP protocol traces"
            | Validate _ -> "Validate ACP messages from stdin"
            | Replay _ -> "Replay trace file with optional interactivity"
            | Analyze _ -> "Analyze trace file statistics"
            | Benchmark _ -> "Benchmark codec and protocol performance"

let private printBanner () =
    let version = getVersion ()

    if Output.supportsColor () then
        Output.printColored Output.Colors.cyan "acp-inspector"
        Console.Write($" v{version}")
        Output.printColored Output.Colors.gray " (ACP Inspector)\n"
    else
        Console.WriteLine($"acp-inspector v{version} (ACP Inspector)")

let private printVersion () =
    printBanner ()
    Console.WriteLine()
    Console.WriteLine("ACP Inspector CLI for ACP protocol inspection and benchmarking")
    Console.WriteLine()
    Console.WriteLine("Components:")
    Output.printKeyValue "  SDK" "ACP.Inspector (F# .NET 10)"
    Output.printKeyValue "  Protocol" $"ACP {Acp.Domain.Spec.Schema}"
    Output.printKeyValue "  License" "MIT"
    Console.WriteLine()

[<EntryPoint>]
let main argv =
    // Handle version flag manually before Argu parsing
    if argv.Length > 0 && (argv[0] = "--version" || argv[0] = "-v") then
        printVersion ()
        0
    else
        let parser =
            ArgumentParser.Create<Command>(
                programName = "acp-inspector",
                helpTextMessage = "ACP Inspector CLI for ACP protocol inspection and benchmarking",
                errorHandler = ProcessExiter()
            )

        try
            if argv.Length = 0 then
                printBanner ()
                Console.WriteLine()
                parser.PrintUsage() |> Console.WriteLine
                0
            else
                let results = parser.Parse(argv)

                let exitCode =
                    try
                        match results.GetSubCommand() with
                        | Command.Inspect args ->
                            Telemetry.withSpan "command.inspect" (fun () -> InspectCommand.run args)
                        | Command.Validate args ->
                            Telemetry.withSpan "command.validate" (fun () -> ValidateCommand.run args)
                        | Command.Replay args -> Telemetry.withSpan "command.replay" (fun () -> ReplayCommand.run args)
                        | Command.Analyze args ->
                            Telemetry.withSpan "command.analyze" (fun () -> AnalyzeCommand.run args)
                        | Command.Benchmark args ->
                            Telemetry.withSpan "command.benchmark" (fun () -> BenchmarkCommand.run args)
                    with ex ->
                        Output.printError $"Command failed: {ex.Message}"
                        eprintfn "Stack trace: %s" ex.StackTrace
                        1

                exitCode
        with
        | :? ArguParseException as ex ->
            Output.printError ex.Message
            1
        | ex ->
            Output.printError $"Unexpected error: {ex.Message}"
            1
