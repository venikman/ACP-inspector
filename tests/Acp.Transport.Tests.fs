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
