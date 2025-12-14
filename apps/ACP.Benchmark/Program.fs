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

/// Parse command line arguments
let parseArgs (args: string[]) =
    let mutable mode = "roundtrip"
    let mutable count = 100

    let rec parse =
        function
        | "--mode" :: m :: rest ->
            mode <- m
            parse rest
        | "--count" :: c :: rest ->
            count <- Int32.Parse(c)
            parse rest
        | _ :: rest -> parse rest
        | [] -> ()

    parse (Array.toList args)
    (mode, count)

[<EntryPoint>]
let main args =
    let (mode, count) = parseArgs args

    match mode with
    | "cold-start" -> runColdStart ()
    | "roundtrip" -> runRoundtrip ()
    | "throughput" -> runThroughput count
    | "codec" -> runCodec count
    | other ->
        eprintfn "Unknown mode: %s" other
        eprintfn "Usage: ACP.Benchmark --mode <cold-start|roundtrip|throughput|codec> [--count N]"
        1
