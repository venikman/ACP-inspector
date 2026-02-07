/// ACP SDK Benchmark CLI
/// Used by cross-language benchmark harness (hyperfine)
module Acp.Benchmark.Program

open System
open System.Diagnostics
open Acp
open Acp.Codec

// Sample ACP messages for benchmarking
let initializeRequest =
    """{"jsonrpc":"2.0","method":"initialize","params":{"protocolVersion":1,"clientCapabilities":{"fs":{"readTextFile":true,"writeTextFile":true},"terminal":true},"clientInfo":{"name":"benchmark","version":"1.0.0"}},"id":1}"""

let sessionNewRequest =
    """{"jsonrpc":"2.0","method":"session/new","params":{"cwd":"/tmp","mcpServers":[]},"id":1}"""

let sessionUpdateNotification =
    """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"sess-001","update":{"type":"agentMessage","message":{"type":"text","text":"Hello, this is a test message for codec benchmarking."}}}}"""

let promptRequest =
    """{"jsonrpc":"2.0","method":"session/prompt","params":{"sessionId":"sess-001","prompt":[{"type":"text","text":"What is 2+2?"}]},"id":2}"""

// Simulated streaming token content (realistic LLM output)
let makeTokenUpdate (tokenCount: int) =
    // ~5 chars per token average (realistic for LLMs)
    let text = String.replicate tokenCount "word "
    // Use proper ACP schema: sessionUpdate field with agent_message_chunk type
    sprintf
        """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"sess-001","update":{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"%s"}}}}"""
        text

// Pre-generate token messages of various sizes
let tokenUpdate10 = makeTokenUpdate 10 // ~50 chars
let tokenUpdate100 = makeTokenUpdate 100 // ~500 chars
let tokenUpdate1000 = makeTokenUpdate 1000 // ~5000 chars

/// Cold start benchmark: measure time to decode first message
let runColdStart () =
    let sw = Stopwatch.StartNew()

    // Simulate cold start: decode an initialize request
    let result = Codec.decode Direction.FromClient CodecState.empty initializeRequest

    sw.Stop()

    match result with
    | Ok _ ->
        printfn """{"status":"ok","mode":"cold-start","elapsed_ms":%d}""" sw.ElapsedMilliseconds
        0
    | Error e ->
        eprintfn "Decode error: %A" e
        1

/// Roundtrip benchmark: decode + encode
let runRoundtrip () =
    let sw = Stopwatch.StartNew()

    // Decode
    let decodeResult =
        Codec.decode Direction.FromClient CodecState.empty sessionNewRequest

    match decodeResult with
    | Ok(state, msg) ->
        // Encode a response message
        let responseMsg =
            Domain.Messaging.AgentToClientMessage.SessionNewResult
                { sessionId = Domain.PrimitivesAndParties.SessionId "sess-benchmark"
                  modes = None }

        let encodeResult =
            Codec.encode (Some(Domain.JsonRpc.RequestId.Number 1L)) (Domain.Messaging.Message.FromAgent responseMsg)

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

/// Throughput benchmark: process N messages
let runThroughput (count: int) =
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
            // Reset state on error (e.g., duplicate id)
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

/// Codec benchmark: pure encode/decode performance
let runCodec (count: int) =
    let messages =
        [| initializeRequest, Direction.FromClient
           sessionNewRequest, Direction.FromClient
           sessionUpdateNotification, Direction.FromAgent
           promptRequest, Direction.FromClient |]

    let sw = Stopwatch.StartNew()
    let mutable ops = 0

    for i in 0 .. (count - 1) do
        let (msg, direction) = messages[i % messages.Length]

        // Decode
        match Codec.decode direction CodecState.empty msg with
        | Ok _ -> ops <- ops + 1
        | Error _ -> ()

        // Encode (session new response)
        let responseMsg =
            Domain.Messaging.AgentToClientMessage.SessionNewResult
                { sessionId = Domain.PrimitivesAndParties.SessionId "sess-bench"
                  modes = None }

        match
            Codec.encode
                (Some(Domain.JsonRpc.RequestId.Number(int64 i)))
                (Domain.Messaging.Message.FromAgent responseMsg)
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

/// Raw JSON benchmark: pure System.Text.Json parsing (no ACP validation)
/// This isolates .NET runtime overhead from codec logic
let runRawJson (count: int) =
    let messages =
        [| initializeRequest
           sessionNewRequest
           sessionUpdateNotification
           promptRequest |]

    let sw = Stopwatch.StartNew()
    let mutable ops = 0

    for i in 0 .. (count - 1) do
        let msg = messages[i % messages.Length]

        // Raw JSON parse only (like TS/Python/Rust benchmarks do)
        let doc = System.Text.Json.JsonDocument.Parse(msg)
        doc.Dispose()
        ops <- ops + 1

        // Raw JSON serialize
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

/// Token throughput benchmark: measure tokens/sec through SDK
/// Simulates streaming LLM output with content chunks
let runTokens (count: int) (tokensPerMessage: int) =
    // Pre-generate the message for this token size
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

/// Parse command line arguments
let parseArgs (args: string[]) =
    let mutable mode = "roundtrip"
    let mutable count = 100
    let mutable tokensPerMsg = 100

    let rec parse =
        function
        | "--mode" :: m :: rest ->
            mode <- m
            parse rest
        | "--count" :: c :: rest ->
            count <- Int32.Parse(c)
            parse rest
        | "--tokens" :: t :: rest ->
            tokensPerMsg <- Int32.Parse(t)
            parse rest
        | _ :: rest -> parse rest
        | [] -> ()

    parse (Array.toList args)
    (mode, count, tokensPerMsg)

[<EntryPoint>]
let main args =
    let (mode, count, tokensPerMsg) = parseArgs args

    match mode with
    | "cold-start" -> runColdStart ()
    | "roundtrip" -> runRoundtrip ()
    | "throughput" -> runThroughput count
    | "codec" -> runCodec count
    | "tokens" -> runTokens count tokensPerMsg
    | "raw-json" -> runRawJson count
    | other ->
        eprintfn "Unknown mode: %s" other

        eprintfn
            "Usage: ACP.Benchmark --mode <cold-start|roundtrip|throughput|codec|tokens|raw-json> [--count N] [--tokens T]"

        1
