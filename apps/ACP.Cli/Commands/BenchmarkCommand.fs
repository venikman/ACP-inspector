module Acp.Cli.Commands.BenchmarkCommand

open System
open System.Diagnostics
open Argu
open Acp
open Acp.Codec
open Acp.Domain
open Acp.Cli.Common

[<RequireQualifiedAccess>]
type BenchmarkMode =
    | ColdStart
    | Roundtrip
    | Throughput
    | Codec
    | Tokens
    | RawJson

module BenchmarkMode =
    let tryParse (s: string) =
        match s.ToLowerInvariant() with
        | "cold-start" -> Some BenchmarkMode.ColdStart
        | "roundtrip" -> Some BenchmarkMode.Roundtrip
        | "throughput" -> Some BenchmarkMode.Throughput
        | "codec" -> Some BenchmarkMode.Codec
        | "tokens" -> Some BenchmarkMode.Tokens
        | "raw-json" -> Some BenchmarkMode.RawJson
        | _ -> None

    let toString =
        function
        | BenchmarkMode.ColdStart -> "cold-start"
        | BenchmarkMode.Roundtrip -> "roundtrip"
        | BenchmarkMode.Throughput -> "throughput"
        | BenchmarkMode.Codec -> "codec"
        | BenchmarkMode.Tokens -> "tokens"
        | BenchmarkMode.RawJson -> "raw-json"

[<RequireQualifiedAccess>]
type BenchmarkArgs =
    | [<AltCommandLine("-m"); Mandatory>] Mode of mode: string
    | [<AltCommandLine("-c")>] Count of count: int
    | [<AltCommandLine("-t")>] Tokens of tokens: int

    interface IArgParserTemplate with
        member s.Usage =
            match s with
            | Mode _ -> "Benchmark mode: cold-start, roundtrip, throughput, codec, tokens, raw-json"
            | Count _ -> "Number of iterations (default: 100)"
            | Tokens _ -> "Tokens per message for token mode (default: 100)"

// Sample ACP messages for benchmarking
let private initializeRequest =
    """{"jsonrpc":"2.0","method":"initialize","params":{"protocolVersion":1,"clientCapabilities":{"fs":{"readTextFile":true,"writeTextFile":true},"terminal":true},"clientInfo":{"name":"benchmark","version":"1.0.0"}},"id":1}"""

let private sessionNewRequest =
    """{"jsonrpc":"2.0","method":"session/new","params":{"cwd":"/tmp","mcpServers":[]},"id":1}"""

let private sessionUpdateNotification =
    """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"sess-001","update":{"type":"agentMessage","message":{"type":"text","text":"Hello, this is a test message for codec benchmarking."}}}}"""

let private promptRequest =
    """{"jsonrpc":"2.0","method":"session/prompt","params":{"sessionId":"sess-001","prompt":[{"type":"text","text":"What is 2+2?"}]},"id":2}"""

let private makeTokenUpdate (tokenCount: int) =
    let text = String.replicate tokenCount "word "

    sprintf
        """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"sess-001","update":{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"%s"}}}}"""
        text

let private runColdStart () =
    let sw = Stopwatch.StartNew()
    let result = Codec.decode Direction.FromClient CodecState.empty initializeRequest
    sw.Stop()

    match result with
    | Ok _ ->
        printfn """{"status":"ok","mode":"cold-start","elapsed_ms":%d}""" sw.ElapsedMilliseconds
        0
    | Error e ->
        eprintfn "Decode error: %A" e
        1

let private runRoundtrip () =
    let sw = Stopwatch.StartNew()
    let decodeResult = Codec.decode Direction.FromClient CodecState.empty sessionNewRequest

    match decodeResult with
    | Ok(state, msg) ->
        let responseMsg =
            Messaging.AgentToClientMessage.SessionNewResult
                { sessionId = PrimitivesAndParties.SessionId "sess-benchmark"
                  modes = None }

        let encodeResult =
            Codec.encode (Some(JsonRpc.RequestId.Number 1L)) (Messaging.Message.FromAgent responseMsg)

        sw.Stop()

        match encodeResult with
        | Ok _ ->
            printfn """{"status":"ok","mode":"roundtrip","elapsed_ms":%d}""" sw.ElapsedMilliseconds
            0
        | Error e ->
            eprintfn "Encode error: %A" e
            1
    | Error e ->
        eprintfn "Decode error: %A" e
        1

let private runThroughput (count: int) =
    let messages =
        [| initializeRequest, Direction.FromClient
           sessionNewRequest, Direction.FromClient
           sessionUpdateNotification, Direction.FromAgent
           promptRequest, Direction.FromClient |]

    let sw = Stopwatch.StartNew()
    let mutable state = CodecState.empty
    let mutable decoded = 0

    for i in 0 .. (count - 1) do
        let (msg, direction) = messages[i % messages.Length]

        match Codec.decode direction state msg with
        | Ok(newState, _) ->
            state <- newState
            decoded <- decoded + 1
        | Error _ ->
            state <- CodecState.empty
            decoded <- decoded + 1

    sw.Stop()

    let msgsPerSec =
        if sw.ElapsedMilliseconds > 0L then
            float decoded / (float sw.ElapsedMilliseconds / 1000.0)
        else
            float decoded * 1000.0

    printfn
        """{"status":"ok","mode":"throughput","count":%d,"elapsed_ms":%d,"msgs_per_sec":%.0f}"""
        decoded
        sw.ElapsedMilliseconds
        msgsPerSec

    0

let private runCodec (count: int) =
    let messages =
        [| initializeRequest, Direction.FromClient
           sessionNewRequest, Direction.FromClient
           sessionUpdateNotification, Direction.FromAgent
           promptRequest, Direction.FromClient |]

    let sw = Stopwatch.StartNew()
    let mutable ops = 0

    for i in 0 .. (count - 1) do
        let (msg, direction) = messages[i % messages.Length]

        match Codec.decode direction CodecState.empty msg with
        | Ok _ -> ops <- ops + 1
        | Error _ -> ()

        let responseMsg =
            Messaging.AgentToClientMessage.SessionNewResult
                { sessionId = PrimitivesAndParties.SessionId "sess-bench"
                  modes = None }

        match
            Codec.encode
                (Some(JsonRpc.RequestId.Number(int64 i)))
                (Messaging.Message.FromAgent responseMsg)
        with
        | Ok _ -> ops <- ops + 1
        | Error _ -> ()

    sw.Stop()

    let opsPerSec =
        if sw.ElapsedMilliseconds > 0L then
            float ops / (float sw.ElapsedMilliseconds / 1000.0)
        else
            float ops * 1000.0

    printfn
        """{"status":"ok","mode":"codec","ops":%d,"elapsed_ms":%d,"ops_per_sec":%.0f}"""
        ops
        sw.ElapsedMilliseconds
        opsPerSec

    0

let private runRawJson (count: int) =
    let messages =
        [| initializeRequest
           sessionNewRequest
           sessionUpdateNotification
           promptRequest |]

    let sw = Stopwatch.StartNew()
    let mutable ops = 0

    for i in 0 .. (count - 1) do
        let msg = messages[i % messages.Length]

        let doc = System.Text.Json.JsonDocument.Parse(msg)
        doc.Dispose()
        ops <- ops + 1

        let response =
            System.Text.Json.JsonSerializer.Serialize(
                {| jsonrpc = "2.0"
                   result = {| sessionId = "sess-bench" |}
                   id = i |}
            )

        ops <- ops + 1

    sw.Stop()

    let opsPerSec =
        if sw.ElapsedMilliseconds > 0L then
            float ops / (float sw.ElapsedMilliseconds / 1000.0)
        else
            float ops * 1000.0

    printfn
        """{"status":"ok","mode":"raw-json","ops":%d,"elapsed_ms":%d,"ops_per_sec":%.0f}"""
        ops
        sw.ElapsedMilliseconds
        opsPerSec

    0

let private runTokens (count: int) (tokensPerMessage: int) =
    let message = makeTokenUpdate tokensPerMessage

    let sw = Stopwatch.StartNew()
    let mutable decoded = 0
    let mutable totalTokens = 0L

    for _ in 1..count do
        match Codec.decode Direction.FromAgent CodecState.empty message with
        | Ok _ ->
            decoded <- decoded + 1
            totalTokens <- totalTokens + int64 tokensPerMessage
        | Error _ -> ()

    sw.Stop()

    let elapsedSec = float sw.ElapsedMilliseconds / 1000.0

    let tokensPerSec =
        if elapsedSec > 0.0 then
            float totalTokens / elapsedSec
        else
            float totalTokens * 1000.0

    let msgsPerSec =
        if elapsedSec > 0.0 then
            float decoded / elapsedSec
        else
            float decoded * 1000.0

    printfn
        """{"status":"ok","mode":"tokens","messages":%d,"tokens_per_msg":%d,"total_tokens":%d,"elapsed_ms":%d,"tokens_per_sec":%.0f,"msgs_per_sec":%.0f}"""
        decoded
        tokensPerMessage
        totalTokens
        sw.ElapsedMilliseconds
        tokensPerSec
        msgsPerSec

    0

/// Run benchmark command
let run (args: ParseResults<BenchmarkArgs>) : int =
    let modeStr = args.GetResult(BenchmarkArgs.Mode)
    let count = args.GetResult(BenchmarkArgs.Count, defaultValue = 100)
    let tokens = args.GetResult(BenchmarkArgs.Tokens, defaultValue = 100)

    match BenchmarkMode.tryParse modeStr with
    | Some BenchmarkMode.ColdStart -> runColdStart ()
    | Some BenchmarkMode.Roundtrip -> runRoundtrip ()
    | Some BenchmarkMode.Throughput -> runThroughput count
    | Some BenchmarkMode.Codec -> runCodec count
    | Some BenchmarkMode.Tokens -> runTokens count tokens
    | Some BenchmarkMode.RawJson -> runRawJson count
    | None ->
        Output.printError $"Unknown benchmark mode: {modeStr}"
        Output.printInfo "Valid modes: cold-start, roundtrip, throughput, codec, tokens, raw-json"
        1
