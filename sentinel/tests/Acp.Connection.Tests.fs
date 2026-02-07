namespace Acp.Tests

open System
open System.Threading.Tasks
open Xunit

open Acp
open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Capabilities
open Acp.Domain.Initialization
open Acp.Domain.SessionSetup
open Acp.Domain.Prompting
open Acp.Domain.Messaging
open Acp.Domain.SessionModes

module ConnectionTests =

    // ============================================================
    // Integration test using duplex transport with both sides
    // ============================================================

    [<Fact>]
    let ``Full handshake between client and agent connections`` () =
        task {
            let (clientTransport, agentTransport) = Transport.DuplexTransport.CreatePair()

            // Set up agent handlers
            let mutable receivedInit: InitializeParams option = None

            let handlers: Connection.AgentHandlers =
                { onInitialize =
                    fun p ->
                        task {
                            receivedInit <- Some p

                            return
                                Ok
                                    { protocolVersion = ProtocolVersion.current
                                      agentCapabilities =
                                        { loadSession = false
                                          mcpCapabilities = { http = false; sse = false }
                                          promptCapabilities =
                                            { audio = false
                                              image = false
                                              embeddedContext = false }
                                          sessionCapabilities = SessionCapabilities.empty }
                                      agentInfo = None
                                      authMethods = [] }
                        }
                  onNewSession =
                    fun p ->
                        task {
                            return
                                Ok
                                    { sessionId = SessionId "test-session"
                                      modes = None }
                        }
                  onPrompt =
                    fun p ->
                        task {
                            return
                                Ok
                                    { sessionId = p.sessionId
                                      stopReason = StopReason.EndTurn
                                      usage = None
                                      _meta = None }
                        }
                  onCancel = fun _ -> task { () }
                  onSetMode =
                    fun p ->
                        task {
                            return
                                Ok
                                    { sessionId = p.sessionId
                                      modeId = p.modeId }
                        } }

            let agent = Connection.AgentConnection(agentTransport, handlers)
            let client = Connection.ClientConnection(clientTransport)

            // Start agent listening in background
            let agentListenTask = agent.StartListening()

            // Client sends initialize
            let initParams: InitializeParams =
                { protocolVersion = ProtocolVersion.current
                  clientCapabilities =
                    { fs =
                        { readTextFile = true
                          writeTextFile = true }
                      terminal = true }
                  clientInfo = None }

            let! initResult = client.InitializeAsync(initParams)

            // Verify
            Assert.True(receivedInit.IsSome)

            match initResult with
            | Ok r -> Assert.Equal(ProtocolVersion.current, r.protocolVersion)
            | Error e -> failwithf "Initialize failed: %A" e

            // Clean up
            do! agent.StopAsync()
        }

    [<Fact>]
    let ``Client can create session after initialization`` () =
        task {
            let (clientTransport, agentTransport) = Transport.DuplexTransport.CreatePair()

            let handlers: Connection.AgentHandlers =
                { onInitialize =
                    fun _ ->
                        task {
                            return
                                Ok
                                    { protocolVersion = ProtocolVersion.current
                                      agentCapabilities =
                                        { loadSession = false
                                          mcpCapabilities = { http = false; sse = false }
                                          promptCapabilities =
                                            { audio = false
                                              image = false
                                              embeddedContext = false }
                                          sessionCapabilities = SessionCapabilities.empty }
                                      agentInfo = None
                                      authMethods = [] }
                        }
                  onNewSession =
                    fun _ ->
                        task {
                            return
                                Ok
                                    { sessionId = SessionId "new-session-123"
                                      modes = None }
                        }
                  onPrompt = fun _ -> task { return Error "not implemented" }
                  onCancel = fun _ -> task { () }
                  onSetMode = fun _ -> task { return Error "not implemented" } }

            let agent = Connection.AgentConnection(agentTransport, handlers)
            let client = Connection.ClientConnection(clientTransport)

            let _ = agent.StartListening()

            // Initialize first
            let! _ =
                client.InitializeAsync(
                    { protocolVersion = ProtocolVersion.current
                      clientCapabilities =
                        { fs =
                            { readTextFile = true
                              writeTextFile = true }
                          terminal = true }
                      clientInfo = None }
                )

            // Create session
            let! sessionResult = client.NewSessionAsync({ cwd = "/tmp"; mcpServers = [] })

            match sessionResult with
            | Ok r -> Assert.Equal("new-session-123", SessionId.value r.sessionId)
            | Error e -> failwithf "NewSession failed: %A" e

            do! agent.StopAsync()
        }

    [<Fact>]
    let ``Client can send prompt and receive response`` () =
        task {
            let (clientTransport, agentTransport) = Transport.DuplexTransport.CreatePair()

            let mutable receivedPrompt: SessionPromptParams option = None

            let handlers: Connection.AgentHandlers =
                { onInitialize =
                    fun _ ->
                        task {
                            return
                                Ok
                                    { protocolVersion = ProtocolVersion.current
                                      agentCapabilities =
                                        { loadSession = false
                                          mcpCapabilities = { http = false; sse = false }
                                          promptCapabilities =
                                            { audio = false
                                              image = false
                                              embeddedContext = false }
                                          sessionCapabilities = SessionCapabilities.empty }
                                      agentInfo = None
                                      authMethods = [] }
                        }
                  onNewSession =
                    fun _ ->
                        task {
                            return
                                Ok
                                    { sessionId = SessionId "s1"
                                      modes = None }
                        }
                  onPrompt =
                    fun p ->
                        task {
                            receivedPrompt <- Some p

                            return
                                Ok
                                    { sessionId = p.sessionId
                                      stopReason = StopReason.EndTurn
                                      usage = None
                                      _meta = None }
                        }
                  onCancel = fun _ -> task { () }
                  onSetMode = fun _ -> task { return Error "not implemented" } }

            let agent = Connection.AgentConnection(agentTransport, handlers)
            let client = Connection.ClientConnection(clientTransport)

            let _ = agent.StartListening()

            // Initialize
            let! _ =
                client.InitializeAsync(
                    { protocolVersion = ProtocolVersion.current
                      clientCapabilities =
                        { fs =
                            { readTextFile = true
                              writeTextFile = true }
                          terminal = true }
                      clientInfo = None }
                )

            // Create session
            let! _ = client.NewSessionAsync({ cwd = "/tmp"; mcpServers = [] })

            // Send prompt
            let! promptResult =
                client.PromptAsync(
                    { sessionId = SessionId "s1"
                      prompt =
                        [ ContentBlock.Text
                              { text = "Hello agent!"
                                annotations = None } ]
                      _meta = None }
                )

            // Verify
            Assert.True(receivedPrompt.IsSome)

            match promptResult with
            | Ok r -> Assert.Equal(StopReason.EndTurn, r.stopReason)
            | Error e -> failwithf "Prompt failed: %A" e

            do! agent.StopAsync()
        }

    [<Fact>]
    let ``Agent can send session update to client`` () =
        task {
            let (clientTransport, agentTransport) = Transport.DuplexTransport.CreatePair()

            let handlers: Connection.AgentHandlers =
                { onInitialize = fun _ -> task { return Error "not called" }
                  onNewSession = fun _ -> task { return Error "not called" }
                  onPrompt = fun _ -> task { return Error "not called" }
                  onCancel = fun _ -> task { () }
                  onSetMode = fun _ -> task { return Error "not called" } }

            let agent = Connection.AgentConnection(agentTransport, handlers)

            // Agent sends session update
            let update =
                SessionUpdate.AgentMessageChunk
                    { content =
                        ContentBlock.Text
                            { text = "Hello from agent!"
                              annotations = None } }

            do! agent.SessionUpdateAsync(SessionId "s1", update)

            // Client receives it
            let! received = clientTransport.ReceiveAsync()

            Assert.True(received.IsSome)
            Assert.Contains("session/update", received.Value)
            Assert.Contains("Hello from agent!", received.Value)
        }

    [<Fact>]
    let ``Cancel notification is sent correctly`` () =
        task {
            let (clientTransport, agentTransport) = Transport.DuplexTransport.CreatePair()

            let mutable cancelReceived = false

            let handlers: Connection.AgentHandlers =
                { onInitialize =
                    fun _ ->
                        task {
                            return
                                Ok
                                    { protocolVersion = ProtocolVersion.current
                                      agentCapabilities =
                                        { loadSession = false
                                          mcpCapabilities = { http = false; sse = false }
                                          promptCapabilities =
                                            { audio = false
                                              image = false
                                              embeddedContext = false }
                                          sessionCapabilities = SessionCapabilities.empty }
                                      agentInfo = None
                                      authMethods = [] }
                        }
                  onNewSession = fun _ -> task { return Error "not called" }
                  onPrompt = fun _ -> task { return Error "not called" }
                  onCancel =
                    fun p ->
                        task {
                            cancelReceived <- true
                            Assert.Equal("s1", SessionId.value p.sessionId)
                        }
                  onSetMode = fun _ -> task { return Error "not called" } }

            let agent = Connection.AgentConnection(agentTransport, handlers)
            let client = Connection.ClientConnection(clientTransport)

            let _ = agent.StartListening()

            // Initialize first (required for protocol)
            let! _ =
                client.InitializeAsync(
                    { protocolVersion = ProtocolVersion.current
                      clientCapabilities =
                        { fs =
                            { readTextFile = true
                              writeTextFile = true }
                          terminal = true }
                      clientInfo = None }
                )

            // Send cancel
            let! cancelResult = client.CancelAsync(SessionId "s1")
            Assert.True(Result.isOk cancelResult)

            // Give agent time to process
            do! Task.Delay(50)

            Assert.True(cancelReceived)

            do! agent.StopAsync()
        }
