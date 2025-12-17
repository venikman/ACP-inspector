module Acp.Cli.Commands.AnalyzeCommand

open System
open System.Collections.Generic
open System.IO
open Argu
open Acp
open Acp.Domain
open Acp.Domain.Messaging
open Acp.Cli.Common
open Acp.Cli.Commands.InspectCommand

[<RequireQualifiedAccess>]
type AnalyzeArgs =
    | [<MainCommand; ExactlyOnce>] Trace of path: string
    | [<AltCommandLine("-m")>] Show_Methods
    | [<AltCommandLine("-t")>] Show_Timing
    | [<AltCommandLine("-s")>] Show_Sessions

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Trace _ -> "Path to trace file (JSONL format)"
            | Show_Methods -> "Show method call statistics"
            | Show_Timing -> "Show timing analysis"
            | Show_Sessions -> "Show session information"

let private methodName (msg: Message) =
    match msg with
    | Message.FromClient c ->
        match c with
        | ClientToAgentMessage.Initialize _ -> "initialize"
        | ClientToAgentMessage.Authenticate _ -> "authenticate"
        | ClientToAgentMessage.SessionNew _ -> "session/new"
        | ClientToAgentMessage.SessionLoad _ -> "session/load"
        | ClientToAgentMessage.SessionPrompt _ -> "session/prompt"
        | ClientToAgentMessage.SessionSetMode _ -> "session/set_mode"
        | ClientToAgentMessage.SessionCancel _ -> "session/cancel"
        | ClientToAgentMessage.ExtRequest(name, _) -> name
        | ClientToAgentMessage.ExtNotification(name, _) -> name
        | ClientToAgentMessage.ExtResponse(name, _) -> name
        | ClientToAgentMessage.ExtError(name, _) -> name
        | _ -> "client-other"
    | Message.FromAgent a ->
        match a with
        | AgentToClientMessage.InitializeResult _ -> "initialize-result"
        | AgentToClientMessage.AuthenticateResult _ -> "authenticate-result"
        | AgentToClientMessage.SessionNewResult _ -> "session/new-result"
        | AgentToClientMessage.SessionLoadResult _ -> "session/load-result"
        | AgentToClientMessage.SessionPromptResult _ -> "session/prompt-result"
        | AgentToClientMessage.SessionSetModeResult _ -> "session/set_mode-result"
        | AgentToClientMessage.SessionUpdate _ -> "session/update"
        | AgentToClientMessage.ExtRequest(name, _) -> name
        | AgentToClientMessage.ExtNotification(name, _) -> name
        | AgentToClientMessage.ExtResponse(name, _) -> name
        | AgentToClientMessage.ExtError(name, _) -> name
        | _ -> "agent-other"

/// Run analyze command
let run (args: ParseResults<AnalyzeArgs>) : int =
    let tracePath = args.GetResult(AnalyzeArgs.Trace)
    let showMethods = args.Contains(AnalyzeArgs.Show_Methods)
    let showTiming = args.Contains(AnalyzeArgs.Show_Timing)
    let showSessions = args.Contains(AnalyzeArgs.Show_Sessions)

    // Default: show all if none specified
    let showAll = not showMethods && not showTiming && not showSessions

    Output.printHeading $"Analyzing trace: {tracePath}"

    match Security.validateInputPath tracePath with
    | Error err ->
        Output.printError err
        1
    | Ok validatedPath ->
        try
            let lines = File.ReadAllLines(validatedPath)
            Output.printSuccess $"Loaded {lines.Length} frames"

            // Parse all frames
            let frames = lines |> Array.choose TraceFrame.tryDecode |> Array.indexed

            if frames.Length = 0 then
                Output.printWarning "No valid frames found"
                1
            else
                // Decode messages
                let mutable state = Codec.CodecState.empty
                let messages = ResizeArray<Message>()
                let timestamps = ResizeArray<DateTimeOffset>()

                for (idx, frame) in frames do
                    let parseDirection (dirStr: string) =
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

                    match parseDirection frame.direction with
                    | Some direction ->
                        match Codec.decode direction state frame.json with
                        | Ok(newState, msg) ->
                            state <- newState
                            messages.Add(msg)
                            timestamps.Add(frame.ts)
                        | Error _ -> ()
                    | None -> ()

                Console.WriteLine()

                // Method statistics
                if showMethods || showAll then
                    Output.printHeading "Method Statistics"

                    let methodCounts = Dictionary<string, int>()

                    for msg in messages do
                        let method = methodName msg

                        let count =
                            if methodCounts.ContainsKey(method) then
                                methodCounts[method]
                            else
                                0

                        methodCounts[method] <- count + 1

                    let sorted =
                        methodCounts
                        |> Seq.sortByDescending (fun kvp -> kvp.Value)
                        |> Seq.take (min 10 methodCounts.Count)

                    for kvp in sorted do
                        Output.printKeyValue $"  {kvp.Key}" (string kvp.Value)

                    Console.WriteLine()

                // Timing analysis
                if showTiming || showAll then
                    Output.printHeading "Timing Analysis"

                    if timestamps.Count > 1 then
                        let first = timestamps[0]
                        let last = timestamps[timestamps.Count - 1]
                        let duration = last - first

                        Output.printKeyValue "First message" (first.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                        Output.printKeyValue "Last message" (last.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                        Output.printKeyValue "Total duration" (duration.ToString())

                        Output.printKeyValue
                            "Messages per second"
                            (sprintf "%.2f" (float messages.Count / duration.TotalSeconds))
                    else
                        Output.printInfo "Not enough messages for timing analysis"

                    Console.WriteLine()

                // Session information
                if showSessions || showAll then
                    Output.printHeading "Session Information"

                    let mutable sessionCount = 0
                    let mutable promptCount = 0

                    for msg in messages do
                        match msg with
                        | Message.FromClient(ClientToAgentMessage.SessionNew _) -> sessionCount <- sessionCount + 1
                        | Message.FromClient(ClientToAgentMessage.SessionPrompt _) -> promptCount <- promptCount + 1
                        | _ -> ()

                    Output.printKeyValue "Sessions created" (string sessionCount)
                    Output.printKeyValue "Prompts sent" (string promptCount)

                    if sessionCount > 0 then
                        Output.printKeyValue
                            "Avg prompts/session"
                            (sprintf "%.1f" (float promptCount / float sessionCount))

                    Console.WriteLine()

                // Summary
                Output.printHeading "Summary"
                Output.printKeyValue "Total frames" (string frames.Length)
                Output.printKeyValue "Valid messages" (string messages.Count)

                Output.printKeyValue
                    "Decode success rate"
                    (sprintf "%.1f%%" (100.0 * float messages.Count / float frames.Length))

                0

        with ex ->
            Output.printError $"Analysis failed: {ex.Message}"
            1
