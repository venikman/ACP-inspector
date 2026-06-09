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

    let private mkInitializeResult (loadSession: bool) (sessionCapabilities: SessionCapabilities) : InitializeResult =
        { protocolVersion = ProtocolVersion.current
          agentCapabilities =
            { loadSession = loadSession
              mcpCapabilities = { http = false; sse = false }
              promptCapabilities =
                { audio = false
                  image = false
                  embeddedContext = false }
              sessionCapabilities = sessionCapabilities
              auth = AgentAuthCapabilities.empty }
          agentInfo = None
          authMethods = [] }

    let private mkNewSessionResult (sessionId: SessionId) : NewSessionResult =
        { sessionId = sessionId
          configOptions = None
          modes = None
          _meta = None }

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

                            return Ok(mkInitializeResult false SessionCapabilities.empty)
                        }
                  onNewSession = fun _ -> task { return Ok(mkNewSessionResult (SessionId "test-session")) }
                  onLoadSession = fun _ -> task { return Error "not implemented" }
                  onResumeSession = fun _ -> task { return Error "not implemented" }
                  onListSessions = fun _ -> task { return Error "not implemented" }
                  onCloseSession = fun _ -> task { return Error "not implemented" }
                  onDeleteSession = fun _ -> task { return Error "not implemented" }
                  onPrompt =
                    fun p ->
                        task {
                            return
                                Ok
                                    { sessionId = p.sessionId
                                      stopReason = StopReason.EndTurn
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
                        }
                  onSetConfigOption = fun _ -> task { return Error "not implemented" } }

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
                { onInitialize = fun _ -> task { return Ok(mkInitializeResult false SessionCapabilities.empty) }
                  onNewSession = fun _ -> task { return Ok(mkNewSessionResult (SessionId "new-session-123")) }
                  onLoadSession = fun _ -> task { return Error "not implemented" }
                  onResumeSession = fun _ -> task { return Error "not implemented" }
                  onListSessions = fun _ -> task { return Error "not implemented" }
                  onCloseSession = fun _ -> task { return Error "not implemented" }
                  onDeleteSession = fun _ -> task { return Error "not implemented" }
                  onPrompt = fun _ -> task { return Error "not implemented" }
                  onCancel = fun _ -> task { () }
                  onSetMode = fun _ -> task { return Error "not implemented" }
                  onSetConfigOption = fun _ -> task { return Error "not implemented" } }

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
            let! sessionResult =
                client.NewSessionAsync(
                    { cwd = "/tmp"
                      mcpServers = []
                      additionalDirectories = [] }
                )

            match sessionResult with
            | Ok r -> Assert.Equal("new-session-123", SessionId.value r.sessionId)
            | Error e -> failwithf "NewSession failed: %A" e

            do! agent.StopAsync()
        }

    [<Fact>]
    let ``Client can load session after initialization`` () =
        task {
            let (clientTransport, agentTransport) = Transport.DuplexTransport.CreatePair()

            let handlers: Connection.AgentHandlers =
                { onInitialize = fun _ -> task { return Ok(mkInitializeResult true SessionCapabilities.empty) }
                  onNewSession = fun _ -> task { return Error "not implemented" }
                  onLoadSession =
                    fun p ->
                        task {
                            return
                                Ok
                                    { sessionId = p.sessionId
                                      configOptions = None
                                      modes = None
                                      _meta = None }
                        }
                  onResumeSession = fun _ -> task { return Error "not implemented" }
                  onListSessions = fun _ -> task { return Error "not implemented" }
                  onCloseSession = fun _ -> task { return Error "not implemented" }
                  onDeleteSession = fun _ -> task { return Error "not implemented" }
                  onPrompt = fun _ -> task { return Error "not implemented" }
                  onCancel = fun _ -> task { () }
                  onSetMode = fun _ -> task { return Error "not implemented" }
                  onSetConfigOption = fun _ -> task { return Error "not implemented" } }

            let agent = Connection.AgentConnection(agentTransport, handlers)
            let client = Connection.ClientConnection(clientTransport)

            let _ = agent.StartListening()

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

            let! loaded =
                client.LoadSessionAsync(
                    { sessionId = SessionId "load-me"
                      cwd = "/tmp"
                      mcpServers = []
                      additionalDirectories = [] }
                )

            match loaded with
            | Ok r -> Assert.Equal("load-me", SessionId.value r.sessionId)
            | Error e -> failwithf "LoadSession failed: %A" e

            do! agent.StopAsync()
        }

    [<Fact>]
    let ``Client can list sessions after initialization`` () =
        task {
            let (clientTransport, agentTransport) = Transport.DuplexTransport.CreatePair()

            let handlers: Connection.AgentHandlers =
                { onInitialize =
                    fun _ ->
                        task {
                            return
                                Ok(
                                    mkInitializeResult
                                        true
                                        { list = Some { _meta = None }
                                          close = None
                                          delete = None
                                          resume = None
                                          additionalDirectories = None }
                                )
                        }
                  onNewSession = fun _ -> task { return Error "not implemented" }
                  onLoadSession = fun _ -> task { return Error "not implemented" }
                  onResumeSession = fun _ -> task { return Error "not implemented" }
                  onListSessions =
                    fun _ ->
                        task {
                            return
                                Ok
                                    { sessions =
                                        [ { sessionId = SessionId "sess-1"
                                            cwd = "/tmp"
                                            title = Some "Roadmap"
                                            updatedAt = Some "2026-03-19T12:00:00Z"
                                            additionalDirectories = []
                                            _meta = None } ]
                                      nextCursor = None
                                      _meta = None }
                        }
                  onCloseSession = fun _ -> task { return Error "not implemented" }
                  onDeleteSession = fun _ -> task { return Error "not implemented" }
                  onPrompt = fun _ -> task { return Error "not implemented" }
                  onCancel = fun _ -> task { () }
                  onSetMode = fun _ -> task { return Error "not implemented" }
                  onSetConfigOption = fun _ -> task { return Error "not implemented" } }

            let agent = Connection.AgentConnection(agentTransport, handlers)
            let client = Connection.ClientConnection(clientTransport)

            let _ = agent.StartListening()

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

            let! listed =
                client.ListSessionsAsync(
                    { cwd = Some "/tmp"
                      cursor = None
                      _meta = None }
                )

            match listed with
            | Ok r ->
                Assert.Single(r.sessions) |> ignore
                Assert.Equal("sess-1", SessionId.value r.sessions.[0].sessionId)
            | Error e -> failwithf "ListSessions failed: %A" e

            do! agent.StopAsync()
        }

    [<Fact>]
    let ``Client can set config option and receive updated config state`` () =
        task {
            let (clientTransport, agentTransport) = Transport.DuplexTransport.CreatePair()

            let handlers: Connection.AgentHandlers =
                { onInitialize = fun _ -> task { return Ok(mkInitializeResult true SessionCapabilities.empty) }
                  onNewSession = fun _ -> task { return Error "not implemented" }
                  onLoadSession = fun _ -> task { return Error "not implemented" }
                  onResumeSession = fun _ -> task { return Error "not implemented" }
                  onListSessions = fun _ -> task { return Error "not implemented" }
                  onCloseSession = fun _ -> task { return Error "not implemented" }
                  onDeleteSession = fun _ -> task { return Error "not implemented" }
                  onPrompt = fun _ -> task { return Error "not implemented" }
                  onCancel = fun _ -> task { () }
                  onSetMode = fun _ -> task { return Error "not implemented" }
                  onSetConfigOption =
                    fun _ ->
                        task {
                            return
                                Ok
                                    { sessionId = SessionId "sess-1"
                                      configOptions =
                                        [ { id = SessionConfigId "mode"
                                            name = "Session Mode"
                                            description = None
                                            category = Some SessionConfigOptionCategory.Mode
                                            ``type`` = "select"
                                            currentValue = SessionConfigValueId "code"
                                            options =
                                              SessionConfigSelectOptions.Ungrouped
                                                  [ { value = SessionConfigValueId "ask"
                                                      name = "Ask"
                                                      description = None
                                                      _meta = None }
                                                    { value = SessionConfigValueId "code"
                                                      name = "Code"
                                                      description = None
                                                      _meta = None } ]
                                            _meta = None } ]
                                      _meta = None }
                        } }

            let agent = Connection.AgentConnection(agentTransport, handlers)
            let client = Connection.ClientConnection(clientTransport)

            let _ = agent.StartListening()

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

            let! updated =
                client.SetConfigOptionAsync(
                    { sessionId = SessionId "sess-1"
                      configId = SessionConfigId "mode"
                      value = SessionConfigValueId "code"
                      _meta = None }
                )

            match updated with
            | Ok r ->
                Assert.Single(r.configOptions) |> ignore
                Assert.Equal("code", SessionConfigValueId.value r.configOptions.[0].currentValue)
            | Error e -> failwithf "SetConfigOption failed: %A" e

            do! agent.StopAsync()
        }

    [<Fact>]
    let ``Client can send prompt and receive response`` () =
        task {
            let (clientTransport, agentTransport) = Transport.DuplexTransport.CreatePair()

            let mutable receivedPrompt: SessionPromptParams option = None

            let handlers: Connection.AgentHandlers =
                { onInitialize = fun _ -> task { return Ok(mkInitializeResult false SessionCapabilities.empty) }
                  onNewSession = fun _ -> task { return Ok(mkNewSessionResult (SessionId "s1")) }
                  onLoadSession = fun _ -> task { return Error "not implemented" }
                  onResumeSession = fun _ -> task { return Error "not implemented" }
                  onListSessions = fun _ -> task { return Error "not implemented" }
                  onCloseSession = fun _ -> task { return Error "not implemented" }
                  onDeleteSession = fun _ -> task { return Error "not implemented" }
                  onPrompt =
                    fun p ->
                        task {
                            receivedPrompt <- Some p

                            return
                                Ok
                                    { sessionId = p.sessionId
                                      stopReason = StopReason.EndTurn
                                      _meta = None }
                        }
                  onCancel = fun _ -> task { () }
                  onSetMode = fun _ -> task { return Error "not implemented" }
                  onSetConfigOption = fun _ -> task { return Error "not implemented" } }

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
            let! _ =
                client.NewSessionAsync(
                    { cwd = "/tmp"
                      mcpServers = []
                      additionalDirectories = [] }
                )

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
                  onLoadSession = fun _ -> task { return Error "not called" }
                  onResumeSession = fun _ -> task { return Error "not implemented" }
                  onListSessions = fun _ -> task { return Error "not called" }
                  onCloseSession = fun _ -> task { return Error "not called" }
                  onDeleteSession = fun _ -> task { return Error "not called" }
                  onPrompt = fun _ -> task { return Error "not called" }
                  onCancel = fun _ -> task { () }
                  onSetMode = fun _ -> task { return Error "not called" }
                  onSetConfigOption = fun _ -> task { return Error "not called" } }

            let agent = Connection.AgentConnection(agentTransport, handlers)

            // Agent sends session update
            let update =
                SessionUpdate.AgentMessageChunk
                    { content =
                        ContentBlock.Text
                            { text = "Hello from agent!"
                              annotations = None }
                      messageId = None }

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
                { onInitialize = fun _ -> task { return Ok(mkInitializeResult false SessionCapabilities.empty) }
                  onNewSession = fun _ -> task { return Error "not called" }
                  onLoadSession = fun _ -> task { return Error "not called" }
                  onResumeSession = fun _ -> task { return Error "not implemented" }
                  onListSessions = fun _ -> task { return Error "not called" }
                  onCloseSession = fun _ -> task { return Error "not called" }
                  onDeleteSession = fun _ -> task { return Error "not called" }
                  onPrompt = fun _ -> task { return Error "not called" }
                  onCancel =
                    fun p ->
                        task {
                            cancelReceived <- true
                            Assert.Equal("s1", SessionId.value p.sessionId)
                        }
                  onSetMode = fun _ -> task { return Error "not called" }
                  onSetConfigOption = fun _ -> task { return Error "not called" } }

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

    [<Fact>]
    let ``Client can close session after initialization`` () =
        task {
            let (clientTransport, agentTransport) = Transport.DuplexTransport.CreatePair()

            let handlers: Connection.AgentHandlers =
                { onInitialize =
                    fun _ ->
                        task {
                            return
                                Ok(
                                    mkInitializeResult
                                        true
                                        { list = None
                                          close = Some { _meta = None }
                                          delete = None
                                          resume = None
                                          additionalDirectories = None }
                                )
                        }
                  onNewSession = fun _ -> task { return Error "not implemented" }
                  onLoadSession = fun _ -> task { return Error "not implemented" }
                  onResumeSession = fun _ -> task { return Error "not implemented" }
                  onListSessions = fun _ -> task { return Error "not implemented" }
                  onCloseSession = fun p -> task { return Ok { _meta = None } }
                  onDeleteSession = fun _ -> task { return Error "not implemented" }
                  onPrompt = fun _ -> task { return Error "not implemented" }
                  onCancel = fun _ -> task { () }
                  onSetMode = fun _ -> task { return Error "not implemented" }
                  onSetConfigOption = fun _ -> task { return Error "not implemented" } }

            let agent = Connection.AgentConnection(agentTransport, handlers)
            let client = Connection.ClientConnection(clientTransport)

            let _ = agent.StartListening()

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

            let! closed =
                client.CloseSessionAsync(
                    { sessionId = SessionId "sess-1"
                      _meta = None }
                )

            match closed with
            | Ok _ -> ()
            | Error e -> failwithf "CloseSession failed: %A" e

            do! agent.StopAsync()
        }

    [<Fact>]
    let ``Client can delete session after initialization`` () =
        task {
            let (clientTransport, agentTransport) = Transport.DuplexTransport.CreatePair()

            let handlers: Connection.AgentHandlers =
                { onInitialize =
                    fun _ ->
                        task {
                            return
                                Ok(
                                    mkInitializeResult
                                        true
                                        { list = None
                                          close = None
                                          delete = Some { _meta = None }
                                          resume = None
                                          additionalDirectories = None }
                                )
                        }
                  onNewSession = fun _ -> task { return Error "not implemented" }
                  onLoadSession = fun _ -> task { return Error "not implemented" }
                  onResumeSession = fun _ -> task { return Error "not implemented" }
                  onListSessions = fun _ -> task { return Error "not implemented" }
                  onCloseSession = fun _ -> task { return Error "not implemented" }
                  onDeleteSession = fun p -> task { return Ok { _meta = None } }
                  onPrompt = fun _ -> task { return Error "not implemented" }
                  onCancel = fun _ -> task { () }
                  onSetMode = fun _ -> task { return Error "not implemented" }
                  onSetConfigOption = fun _ -> task { return Error "not implemented" } }

            let agent = Connection.AgentConnection(agentTransport, handlers)
            let client = Connection.ClientConnection(clientTransport)

            let _ = agent.StartListening()

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

            let! deleted =
                client.DeleteSessionAsync(
                    { sessionId = SessionId "sess-1"
                      _meta = None }
                )

            match deleted with
            | Ok _ -> ()
            | Error e -> failwithf "DeleteSession failed: %A" e

            do! agent.StopAsync()
        }

    [<Fact>]
    let ``Client can resume session after initialization`` () =
        task {
            let (clientTransport, agentTransport) = Transport.DuplexTransport.CreatePair()

            let handlers: Connection.AgentHandlers =
                { onInitialize =
                    fun _ ->
                        task {
                            return
                                Ok(
                                    mkInitializeResult
                                        true
                                        { list = None
                                          close = None
                                          delete = None
                                          resume = Some { _meta = None }
                                          additionalDirectories = None }
                                )
                        }
                  onNewSession = fun _ -> task { return Error "not implemented" }
                  onLoadSession = fun _ -> task { return Error "not implemented" }
                  onResumeSession =
                    fun p ->
                        task {
                            return
                                Ok
                                    { sessionId = p.sessionId
                                      configOptions = None
                                      modes = None
                                      _meta = None }
                        }
                  onListSessions = fun _ -> task { return Error "not implemented" }
                  onCloseSession = fun _ -> task { return Error "not implemented" }
                  onDeleteSession = fun _ -> task { return Error "not implemented" }
                  onPrompt = fun _ -> task { return Error "not implemented" }
                  onCancel = fun _ -> task { () }
                  onSetMode = fun _ -> task { return Error "not implemented" }
                  onSetConfigOption = fun _ -> task { return Error "not implemented" } }

            let agent = Connection.AgentConnection(agentTransport, handlers)
            let client = Connection.ClientConnection(clientTransport)

            let _ = agent.StartListening()

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

            let! resumed =
                client.ResumeSessionAsync(
                    { sessionId = SessionId "resume-me"
                      cwd = "/tmp"
                      mcpServers = []
                      additionalDirectories = [ "/extra" ]
                      _meta = None }
                )

            match resumed with
            | Ok r -> Assert.Equal("resume-me", SessionId.value r.sessionId)
            | Error e -> failwithf "ResumeSession failed: %A" e

            do! agent.StopAsync()
        }
