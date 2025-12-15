namespace Acp.Tests

open System
open System.IO
open System.Text
open System.Threading.Tasks
open Xunit

open Acp

module TransportTests =

    // ============================================================
    // ITransport interface tests
    // ============================================================

    [<Fact>]
    let ``MemoryTransport send and receive round-trips message`` () =
        task {
            let transport = Transport.MemoryTransport()

            // Simulate other side sending
            transport.Enqueue("""{"jsonrpc":"2.0","id":1,"method":"initialize"}""")

            let! received = transport.ReceiveAsync()

            match received with
            | Some msg -> Assert.Contains("initialize", msg)
            | None -> failwith "expected message"
        }

    [<Fact>]
    let ``MemoryTransport receive returns None when closed`` () =
        task {
            let transport = Transport.MemoryTransport()
            do! transport.CloseAsync()

            let! received = transport.ReceiveAsync()

            Assert.True(received.IsNone)
        }

    [<Fact>]
    let ``MemoryTransport SendAsync stores message for retrieval`` () =
        task {
            let transport = Transport.MemoryTransport()

            do! transport.SendAsync("""{"jsonrpc":"2.0","id":1,"result":{}}""")

            let sent = transport.DequeueSent()
            Assert.True(sent.IsSome)
            Assert.Contains("result", sent.Value)
        }

    // ============================================================
    // StdioTransport tests (using MemoryStream for testing)
    // ============================================================

    [<Fact>]
    let ``StdioTransport reads newline-delimited JSON`` () =
        task {
            let input = """{"jsonrpc":"2.0","id":1,"method":"test"}""" + "\n"
            let inputStream = new MemoryStream(Encoding.UTF8.GetBytes(input))
            let outputStream = new MemoryStream()

            let transport = Transport.StdioTransport(inputStream, outputStream)

            let! received = transport.ReceiveAsync()

            match received with
            | Some msg ->
                Assert.Contains("test", msg)
                // Should not include newline
                Assert.DoesNotContain("\n", msg)
            | None -> failwith "expected message"
        }

    [<Fact>]
    let ``StdioTransport writes newline-delimited JSON`` () =
        task {
            let inputStream = new MemoryStream()
            let outputStream = new MemoryStream()

            let transport = Transport.StdioTransport(inputStream, outputStream)

            do! transport.SendAsync("""{"jsonrpc":"2.0","id":1,"result":{}}""")
            do! transport.FlushAsync()

            outputStream.Position <- 0L
            use reader = new StreamReader(outputStream)
            let written = reader.ReadToEnd()

            Assert.Contains("result", written)
            Assert.EndsWith("\n", written)
        }

    [<Fact>]
    let ``StdioTransport returns None at end of stream`` () =
        task {
            let inputStream = new MemoryStream([||]) // empty
            let outputStream = new MemoryStream()

            let transport = Transport.StdioTransport(inputStream, outputStream)

            let! received = transport.ReceiveAsync()

            Assert.True(received.IsNone)
        }

    [<Fact>]
    let ``StdioTransport handles multiple messages`` () =
        task {
            let lines =
                [ """{"jsonrpc":"2.0","id":1,"method":"first"}"""
                  """{"jsonrpc":"2.0","id":2,"method":"second"}"""
                  """{"jsonrpc":"2.0","id":3,"method":"third"}""" ]

            let input = String.concat "\n" lines + "\n"
            let inputStream = new MemoryStream(Encoding.UTF8.GetBytes(input))
            let outputStream = new MemoryStream()

            let transport = Transport.StdioTransport(inputStream, outputStream)

            let! msg1 = transport.ReceiveAsync()
            let! msg2 = transport.ReceiveAsync()
            let! msg3 = transport.ReceiveAsync()
            let! msg4 = transport.ReceiveAsync()

            Assert.True(msg1.IsSome)
            Assert.Contains("first", msg1.Value)

            Assert.True(msg2.IsSome)
            Assert.Contains("second", msg2.Value)

            Assert.True(msg3.IsSome)
            Assert.Contains("third", msg3.Value)

            Assert.True(msg4.IsNone) // EOF
        }

    // ============================================================
    // DuplexTransport tests (bidirectional in-memory pair)
    // ============================================================

    [<Fact>]
    let ``DuplexTransport creates connected pair`` () =
        task {
            let (clientSide, agentSide) = Transport.DuplexTransport.CreatePair()

            // Client sends, agent receives
            do! clientSide.SendAsync("""{"from":"client"}""")
            let! agentReceived = agentSide.ReceiveAsync()

            Assert.True(agentReceived.IsSome)
            Assert.Contains("client", agentReceived.Value)

            // Agent sends, client receives
            do! agentSide.SendAsync("""{"from":"agent"}""")
            let! clientReceived = clientSide.ReceiveAsync()

            Assert.True(clientReceived.IsSome)
            Assert.Contains("agent", clientReceived.Value)
        }

    [<Fact>]
    let ``DuplexTransport close propagates to peer`` () =
        task {
            let (clientSide, agentSide) = Transport.DuplexTransport.CreatePair()

            do! clientSide.CloseAsync()

            let! agentReceived = agentSide.ReceiveAsync()
            Assert.True(agentReceived.IsNone)
        }

    // ============================================================
    // Chaos/Fault Injection Transport Tests
    // ============================================================

    /// Wrapper transport that can inject faults for testing.
    /// Optionally accepts a Random instance for deterministic testing.
    type ChaosTransport(inner: Transport.ITransport, ?random: Random) =
        let mutable delayMs = 0
        let mutable dropRate = 0.0
        let mutable corruptRate = 0.0
        let mutable failOnSend = false
        let mutable failOnReceive = false
        let random = defaultArg random (Random())

        member _.SetDelay(ms: int) = delayMs <- ms
        member _.SetDropRate(rate: float) = dropRate <- rate
        member _.SetCorruptRate(rate: float) = corruptRate <- rate
        member _.SetFailOnSend(fail: bool) = failOnSend <- fail
        member _.SetFailOnReceive(fail: bool) = failOnReceive <- fail

        interface Transport.ITransport with
            member _.SendAsync(message: string) =
                task {
                    if failOnSend then
                        raise (IOException("Simulated send failure"))

                    if delayMs > 0 then
                        do! Task.Delay(delayMs)

                    if random.NextDouble() < dropRate then
                        // Drop the message silently
                        ()
                    else
                        do! inner.SendAsync(message)
                }

            member _.ReceiveAsync() =
                task {
                    if failOnReceive then
                        raise (IOException("Simulated receive failure"))

                    if delayMs > 0 then
                        do! Task.Delay(delayMs)

                    let! result = inner.ReceiveAsync()

                    match result with
                    | None -> return None
                    | Some msg ->
                        if random.NextDouble() < corruptRate then
                            // Corrupt the message by truncating
                            let corrupted =
                                if msg.Length > 10 then
                                    msg.Substring(0, msg.Length / 2)
                                else
                                    "{"

                            return Some corrupted
                        else
                            return Some msg
                }

            member _.CloseAsync() = inner.CloseAsync()

    [<Fact>]
    let ``ChaosTransport with delay still delivers messages`` () =
        task {
            let (clientSide, agentSide) = Transport.DuplexTransport.CreatePair()
            let chaosClient = ChaosTransport(clientSide)
            chaosClient.SetDelay(10)

            do! (chaosClient :> Transport.ITransport).SendAsync("""{"test":"delayed"}""")

            let! received = agentSide.ReceiveAsync()
            Assert.True(received.IsSome)
            Assert.Contains("delayed", received.Value)
        }

    [<Fact>]
    let ``ChaosTransport drop rate causes message loss`` () =
        task {
            let (clientSide, agentSide) = Transport.DuplexTransport.CreatePair()
            let chaosClient = ChaosTransport(clientSide)
            chaosClient.SetDropRate(1.0) // 100% drop rate

            do! (chaosClient :> Transport.ITransport).SendAsync("""{"test":"dropped"}""")

            // Message should not arrive
            let! received = agentSide.ReceiveAsync()
            Assert.True(received.IsNone)
        }

    [<Fact>]
    let ``ChaosTransport corruption produces invalid JSON`` () =
        task {
            let transport = Transport.MemoryTransport()
            let chaos = ChaosTransport(transport)
            chaos.SetCorruptRate(1.0) // 100% corruption

            transport.Enqueue("""{"jsonrpc":"2.0","id":1,"method":"test"}""")

            let! received = (chaos :> Transport.ITransport).ReceiveAsync()
            Assert.True(received.IsSome)

            // Corrupted message should fail to parse
            let isValidJson =
                try
                    System.Text.Json.JsonDocument.Parse(received.Value) |> ignore
                    true
                with _ ->
                    false

            Assert.False(isValidJson)
        }

    [<Fact>]
    let ``ChaosTransport send failure raises exception`` () =
        task {
            let transport = Transport.MemoryTransport()
            let chaos = ChaosTransport(transport)
            chaos.SetFailOnSend(true)

            let! ex =
                Assert.ThrowsAsync<IOException>(fun () -> (chaos :> Transport.ITransport).SendAsync("test") :> Task)

            Assert.Contains("Simulated send failure", ex.Message)
        }

    [<Fact>]
    let ``ChaosTransport receive failure raises exception`` () =
        task {
            let transport = Transport.MemoryTransport()
            let chaos = ChaosTransport(transport)
            chaos.SetFailOnReceive(true)

            let! ex = Assert.ThrowsAsync<IOException>(fun () -> (chaos :> Transport.ITransport).ReceiveAsync() :> Task)

            Assert.Contains("Simulated receive failure", ex.Message)
        }

    [<Fact>]
    let ``StdioTransport handles partial read gracefully`` () =
        task {
            // Simulate partial/incomplete JSON (no newline yet)
            let input = """{"incomplete":true"""
            let inputStream = new MemoryStream(Encoding.UTF8.GetBytes(input))
            let outputStream = new MemoryStream()

            let transport = Transport.StdioTransport(inputStream, outputStream)

            // Should return the partial content as-is (or None at EOF)
            let! received = transport.ReceiveAsync()

            // At EOF without newline, ReadLineAsync returns the remaining content
            Assert.True(received.IsSome)
            Assert.Contains("incomplete", received.Value)
        }

    [<Fact>]
    let ``StdioTransport handles empty lines`` () =
        task {
            let input = "\n\n{\"valid\":true}\n\n"
            let inputStream = new MemoryStream(Encoding.UTF8.GetBytes(input))
            let outputStream = new MemoryStream()

            let transport = Transport.StdioTransport(inputStream, outputStream)

            let! msg1 = transport.ReceiveAsync()
            let! msg2 = transport.ReceiveAsync()
            let! msg3 = transport.ReceiveAsync()

            // Empty lines are returned as empty strings
            Assert.True(msg1.IsSome)
            Assert.Equal("", msg1.Value)

            Assert.True(msg2.IsSome)
            Assert.Equal("", msg2.Value)

            Assert.True(msg3.IsSome)
            Assert.Contains("valid", msg3.Value)
        }

    [<Fact>]
    let ``StdioTransport handles very long messages`` () =
        task {
            let longText = String.replicate 100000 "x"
            let input = sprintf """{"data":"%s"}""" longText + "\n"
            let inputStream = new MemoryStream(Encoding.UTF8.GetBytes(input))
            let outputStream = new MemoryStream()

            let transport = Transport.StdioTransport(inputStream, outputStream)

            let! received = transport.ReceiveAsync()

            Assert.True(received.IsSome)
            Assert.True(received.Value.Length > 100000)
        }
