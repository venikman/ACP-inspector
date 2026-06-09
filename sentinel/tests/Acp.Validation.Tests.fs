namespace Acp.Tests

open Xunit

open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Capabilities
open Acp.Domain.Initialization
open Acp.Domain.SessionSetup
open Acp.Domain.SessionModes
open Acp.Domain.Prompting
open Acp.Domain.Messaging
open Acp.Protocol
open Acp.Validation
open Acp.Validation.PromptOutcome
open Acp.Validation.Transport
open Acp.Validation.MetadataProfile
open Acp.Domain.Metadata

module ValidationTests =

    let private fsCaps: FileSystemCapabilities =
        { readTextFile = true
          writeTextFile = true }

    let private clientCaps: ClientCapabilities = { fs = fsCaps; terminal = false }

    let private mcpCaps: McpCapabilities = { http = false; sse = false }

    let private promptCaps: PromptCapabilities =
        { audio = false
          image = false
          embeddedContext = false }

    let private agentCaps: AgentCapabilities =
        { loadSession = true
          mcpCapabilities = mcpCaps
          promptCapabilities = promptCaps
          sessionCapabilities = SessionCapabilities.empty
          auth = AgentAuthCapabilities.empty }

    let private clientInfo: ImplementationInfo =
        { name = "test-client"
          title = None
          version = "0.0.0-test" }

    let private agentInfo: ImplementationInfo =
        { name = "test-agent"
          title = None
          version = "0.0.0-test" }

    let private initParams: InitializeParams =
        { protocolVersion = ProtocolVersion.current
          clientCapabilities = clientCaps
          clientInfo = Some clientInfo }

    let private initResult: InitializeResult =
        { protocolVersion = ProtocolVersion.current
          agentCapabilities = agentCaps
          agentInfo = Some agentInfo
          authMethods = [] }

    let private textBlock (text: string) : ContentBlock =
        ContentBlock.Text { text = text; annotations = None }

    let private mkPromptParams (sid: SessionId) (blocks: ContentBlock list) : SessionPromptParams =
        { sessionId = sid
          prompt = blocks
          _meta = None }

    let private mkPromptResult (sid: SessionId) (stopReason: StopReason) : SessionPromptResult =
        { sessionId = sid
          stopReason = stopReason
          _meta = None }

    let private mkNewSessionResult (sid: SessionId) (modes: SessionModeState option) : NewSessionResult =
        { sessionId = sid
          configOptions = None
          modes = modes
          _meta = None }

    let private mkSessionUpdate (sid: SessionId) (update: SessionUpdate) : SessionUpdateNotification =
        { sessionId = sid
          update = update
          _meta = None }

    let private mkModeState () : SessionModeState =
        let ask: SessionMode =
            { id = SessionModeId "ask"
              name = "Ask"
              description = None }

        let code: SessionMode =
            { id = SessionModeId "code"
              name = "Code"
              description = None }

        { currentModeId = ask.id
          availableModes = [ ask; code ] }

    let mkHappyTrace (sid: SessionId) : Message list =
        [ Message.FromClient(ClientToAgentMessage.Initialize initParams)
          Message.FromAgent(AgentToClientMessage.InitializeResult initResult)
          Message.FromClient(
              ClientToAgentMessage.SessionNew
                  { cwd = "."
                    mcpServers = []
                    additionalDirectories = [] }
          )
          Message.FromAgent(AgentToClientMessage.SessionNewResult(mkNewSessionResult sid None))
          Message.FromClient(ClientToAgentMessage.SessionPrompt(mkPromptParams sid [ textBlock "hi" ]))
          Message.FromAgent(AgentToClientMessage.SessionPromptResult(mkPromptResult sid StopReason.EndTurn)) ]

    let mkCancelledTraceGood (sid: SessionId) : Message list =
        [ Message.FromClient(ClientToAgentMessage.Initialize initParams)
          Message.FromAgent(AgentToClientMessage.InitializeResult initResult)
          Message.FromClient(
              ClientToAgentMessage.SessionNew
                  { cwd = "."
                    mcpServers = []
                    additionalDirectories = [] }
          )
          Message.FromAgent(AgentToClientMessage.SessionNewResult(mkNewSessionResult sid None))
          Message.FromClient(ClientToAgentMessage.SessionPrompt(mkPromptParams sid [ textBlock "hi" ]))
          Message.FromClient(ClientToAgentMessage.SessionCancel { sessionId = sid })
          Message.FromAgent(AgentToClientMessage.SessionPromptResult(mkPromptResult sid StopReason.Cancelled)) ]

    let mkCancelledTraceBad (sid: SessionId) : Message list =
        [ Message.FromClient(ClientToAgentMessage.Initialize initParams)
          Message.FromAgent(AgentToClientMessage.InitializeResult initResult)
          Message.FromClient(
              ClientToAgentMessage.SessionNew
                  { cwd = "."
                    mcpServers = []
                    additionalDirectories = [] }
          )
          Message.FromAgent(AgentToClientMessage.SessionNewResult(mkNewSessionResult sid None))
          Message.FromClient(ClientToAgentMessage.SessionPrompt(mkPromptParams sid [ textBlock "hi" ]))
          Message.FromClient(ClientToAgentMessage.SessionCancel { sessionId = sid })
          Message.FromAgent(AgentToClientMessage.SessionPromptResult(mkPromptResult sid StopReason.EndTurn)) ]

    let mkSequentialPromptsTraceGood (sid: SessionId) : Message list =
        [ Message.FromClient(ClientToAgentMessage.Initialize initParams)
          Message.FromAgent(AgentToClientMessage.InitializeResult initResult)
          Message.FromClient(
              ClientToAgentMessage.SessionNew
                  { cwd = "."
                    mcpServers = []
                    additionalDirectories = [] }
          )
          Message.FromAgent(AgentToClientMessage.SessionNewResult(mkNewSessionResult sid None))
          Message.FromClient(ClientToAgentMessage.SessionPrompt(mkPromptParams sid [ textBlock "p1" ]))
          Message.FromAgent(AgentToClientMessage.SessionPromptResult(mkPromptResult sid StopReason.EndTurn))
          Message.FromClient(ClientToAgentMessage.SessionPrompt(mkPromptParams sid [ textBlock "p2" ]))
          Message.FromAgent(AgentToClientMessage.SessionPromptResult(mkPromptResult sid StopReason.EndTurn)) ]

    let mkConcurrentPromptsTraceBad (sid: SessionId) : Message list =
        [ Message.FromClient(ClientToAgentMessage.Initialize initParams)
          Message.FromAgent(AgentToClientMessage.InitializeResult initResult)
          Message.FromClient(
              ClientToAgentMessage.SessionNew
                  { cwd = "."
                    mcpServers = []
                    additionalDirectories = [] }
          )
          Message.FromAgent(AgentToClientMessage.SessionNewResult(mkNewSessionResult sid None))
          Message.FromClient(ClientToAgentMessage.SessionPrompt(mkPromptParams sid [ textBlock "p1" ]))
          Message.FromClient(ClientToAgentMessage.SessionPrompt(mkPromptParams sid [ textBlock "p2" ]))
          Message.FromAgent(AgentToClientMessage.SessionPromptResult(mkPromptResult sid StopReason.EndTurn)) ]

    let mkResultWithoutPromptTraceBad (sid: SessionId) : Message list =
        [ Message.FromClient(ClientToAgentMessage.Initialize initParams)
          Message.FromAgent(AgentToClientMessage.InitializeResult initResult)
          Message.FromClient(
              ClientToAgentMessage.SessionNew
                  { cwd = "."
                    mcpServers = []
                    additionalDirectories = [] }
          )
          Message.FromAgent(AgentToClientMessage.SessionNewResult(mkNewSessionResult sid None))
          Message.FromAgent(AgentToClientMessage.SessionPromptResult(mkPromptResult sid StopReason.EndTurn)) ]

    [<Fact>]
    let ``happy path produces Ready phase and no findings`` () =
        let sid = SessionId "s-1"
        let result = runWithValidation sid spec (mkHappyTrace sid) true None None
        Assert.True(result.findings.IsEmpty)

        match result.finalPhase with
        | Ok(Phase.Ready ctx) ->
            let s = ctx.sessions.[sid]
            Assert.Equal<TurnState>(TurnState.Idle(Some StopReason.EndTurn), s.turnState)
        | other -> failwithf "expected Ready phase, got %A" other

    [<Fact>]
    let ``stopOnFirstError=false keeps trace and findings`` () =
        let sid = SessionId "s-err"

        let badThenInit: Message list =
            [ Message.FromClient(ClientToAgentMessage.SessionPrompt(mkPromptParams sid [ textBlock "oops" ]))
              Message.FromClient(ClientToAgentMessage.Initialize initParams) ]

        let result = runWithValidation sid spec badThenInit false None None
        Assert.Equal(2, result.trace.messages.Length)
        Assert.Equal(1, result.findings.Length)

        match result.finalPhase with
        | Error(ProtocolError.UnexpectedMessage(Phase.AwaitingInitialize, _)) -> ()
        | other -> failwithf "expected UnexpectedMessage error, got %A" other

    [<Fact>]
    let ``cancelled prompt yields CancelledByUser outcome`` () =
        let sid = SessionId "s-cancel"

        let trace =
            [ Message.FromClient(ClientToAgentMessage.Initialize initParams)
              Message.FromAgent(AgentToClientMessage.InitializeResult initResult)
              Message.FromClient(
                  ClientToAgentMessage.SessionNew
                      { cwd = "."
                        mcpServers = []
                        additionalDirectories = [] }
              )
              Message.FromAgent(AgentToClientMessage.SessionNewResult(mkNewSessionResult sid None))
              Message.FromClient(ClientToAgentMessage.SessionPrompt(mkPromptParams sid [ textBlock "hi" ]))
              Message.FromClient(ClientToAgentMessage.SessionCancel { sessionId = sid })
              Message.FromAgent(AgentToClientMessage.SessionPromptResult(mkPromptResult sid StopReason.Cancelled)) ]

        let result = runWithValidation sid spec trace true None None
        let outcome = classify sid result.trace result.finalPhase result.findings
        Assert.Equal(PromptTurnOutcome.CancelledByUser, outcome)

    [<Fact>]
    let ``cancelled turn with Cancelled stopReason yields no Session-lane errors`` () =
        let sid = SessionId "s-cancel-ok"

        let result = runWithValidation sid spec (mkCancelledTraceGood sid) true None None

        match result.finalPhase with
        | Ok(Phase.Ready ctx) ->
            let s = ctx.sessions.[sid]
            Assert.Equal<TurnState>(TurnState.Idle(Some StopReason.Cancelled), s.turnState)
        | other -> failwithf "expected Ready phase, got %A" other

        let sessionFindings =
            result.findings |> List.filter (fun f -> f.lane = Lane.Session)

        Assert.True(sessionFindings.IsEmpty)

    [<Fact>]
    let ``cancelled turn with non-cancel stopReason yields Session-lane error`` () =
        let sid = SessionId "s-cancel-bad"

        let result = runWithValidation sid spec (mkCancelledTraceBad sid) true None None

        let sessionFindings =
            result.findings |> List.filter (fun f -> f.lane = Lane.Session)

        match sessionFindings with
        | [ f ] ->
            Assert.Equal(Severity.Error, f.severity)

            match f.failure with
            | Some failure -> Assert.Equal("ACP.SESSION.CANCEL_MISMATCH", failure.code)
            | None -> failwith "expected ValidationFailure"
        | many -> failwithf "expected exactly one Session-lane finding, got %d" many.Length

    [<Fact>]
    let ``sequential prompts yield no Session-lane concurrency errors`` () =
        let sid = SessionId "s-seq"

        let result =
            runWithValidation sid spec (mkSequentialPromptsTraceGood sid) true None None

        let sessionFindings =
            result.findings
            |> List.filter (fun f ->
                f.lane = Lane.Session
                && match f.failure with
                   | Some failure ->
                       failure.code = "ACP.SESSION.MULTIPLE_PROMPTS_IN_FLIGHT"
                       || failure.code = "ACP.SESSION.RESULT_WITHOUT_PROMPT"
                   | None -> false)

        Assert.True(sessionFindings.IsEmpty)

    [<Fact>]
    let ``concurrent prompts yield MULTIPLE_PROMPTS_IN_FLIGHT Session-lane error`` () =
        let sid = SessionId "s-concurrent"

        let result =
            runWithValidation sid spec (mkConcurrentPromptsTraceBad sid) true None None

        let sessionFindings =
            result.findings |> List.filter (fun f -> f.lane = Lane.Session)

        let concurrencyFindingOpt =
            sessionFindings
            |> List.tryFind (fun f ->
                match f.failure with
                | Some failure -> failure.code = "ACP.SESSION.MULTIPLE_PROMPTS_IN_FLIGHT"
                | None -> false)

        match concurrencyFindingOpt with
        | Some f -> Assert.Equal(Severity.Error, f.severity)
        | None -> failwith "expected Session-lane MULTIPLE_PROMPTS_IN_FLIGHT finding"

    [<Fact>]
    let ``result without prompt yields RESULT_WITHOUT_PROMPT Session-lane error`` () =
        let sid = SessionId "s-result-no-prompt"

        let result =
            runWithValidation sid spec (mkResultWithoutPromptTraceBad sid) true None None

        let sessionFindings =
            result.findings |> List.filter (fun f -> f.lane = Lane.Session)

        let findingOpt =
            sessionFindings
            |> List.tryFind (fun f ->
                match f.failure with
                | Some failure -> failure.code = "ACP.SESSION.RESULT_WITHOUT_PROMPT"
                | None -> false)

        match findingOpt with
        | Some f -> Assert.Equal(Severity.Error, f.severity)
        | None -> failwith "expected Session-lane RESULT_WITHOUT_PROMPT finding"

    [<Fact>]
    let ``protocol error yields DomainError ProtocolViolation outcome`` () =
        let sid = SessionId "s-protoerr"

        let badMessages: Message list =
            [ Message.FromClient(ClientToAgentMessage.SessionPrompt(mkPromptParams sid [ textBlock "hi before init" ])) ]

        let result = runWithValidation sid spec badMessages true None None
        let outcome = classify sid result.trace result.finalPhase result.findings

        match outcome with
        | PromptTurnOutcome.DomainError(DomainErrorOutcome.ProtocolViolation(code, _)) ->
            Assert.Equal("ACP.PROTOCOL.UNEXPECTED_MESSAGE", code)
        | other -> failwithf "expected protocol violation outcome, got %A" other

    [<Fact>]
    let ``transport size violation produces Transport lane finding`` () =
        let profile: RuntimeProfile =
            { metadata = MetadataPolicy.AllowOpaque
              transport =
                Some
                    { lineSeparator = None
                      maxFrameBytes = None
                      maxMessageBytes = Some 10
                      metaEnvelope = None } }

        let findingOpt = validateSize (Some profile) Subject.Connection (Some 0) 20

        match findingOpt with
        | Some f ->
            Assert.Equal(Lane.Transport, f.lane)
            Assert.Equal(Severity.Error, f.severity)
            Assert.Equal(Some "ACP.TRANSPORT.MAX_MESSAGE_BYTES_EXCEEDED", f.failure |> Option.map (fun x -> x.code))
        | None -> failwith "expected a Transport finding"

    [<Fact>]
    let ``metadata disallow policy flags Other kind`` () =
        let findingOpt =
            validate MetadataPolicy.Disallow "image/png" Subject.Connection (Some 1)

        match findingOpt with
        | Some f ->
            Assert.Equal(Lane.Transport, f.lane)
            Assert.Equal(Severity.Error, f.severity)
            Assert.Equal(Some "ACP.METADATA.DISALLOWED", f.failure |> Option.map (fun x -> x.code))
        | None -> failwith "expected metadata finding"

    [<Fact>]
    let ``session/new result modes populate session modeState`` () =
        let sid = SessionId "s-modes-new"
        let modes = mkModeState ()

        let trace: Message list =
            [ Message.FromClient(ClientToAgentMessage.Initialize initParams)
              Message.FromAgent(AgentToClientMessage.InitializeResult initResult)
              Message.FromClient(
                  ClientToAgentMessage.SessionNew
                      { cwd = "."
                        mcpServers = []
                        additionalDirectories = [] }
              )
              Message.FromAgent(AgentToClientMessage.SessionNewResult(mkNewSessionResult sid (Some modes))) ]

        let result = runWithValidation sid spec trace true None None

        match result.finalPhase with
        | Ok(Phase.Ready ctx) ->
            let s = ctx.sessions.[sid]
            Assert.Equal(Some modes, s.modeState)
        | other -> failwithf "expected Ready phase, got %A" other

    [<Fact>]
    let ``current_mode_update updates currentModeId`` () =
        let sid = SessionId "s-modes-update"
        let modes = mkModeState ()
        let next = SessionModeId "code"

        let trace: Message list =
            [ Message.FromClient(ClientToAgentMessage.Initialize initParams)
              Message.FromAgent(AgentToClientMessage.InitializeResult initResult)
              Message.FromClient(
                  ClientToAgentMessage.SessionNew
                      { cwd = "."
                        mcpServers = []
                        additionalDirectories = [] }
              )
              Message.FromAgent(AgentToClientMessage.SessionNewResult(mkNewSessionResult sid (Some modes)))
              Message.FromAgent(
                  AgentToClientMessage.SessionUpdate(
                      mkSessionUpdate sid (SessionUpdate.CurrentModeUpdate { currentModeId = next })
                  )
              ) ]

        let result = runWithValidation sid spec trace true None None

        match result.finalPhase with
        | Ok(Phase.Ready ctx) ->
            let s = ctx.sessions.[sid]

            match s.modeState with
            | Some ms -> Assert.Equal(next, ms.currentModeId)
            | None -> failwith "expected modeState"
        | other -> failwithf "expected Ready phase, got %A" other

    [<Fact>]
    let ``session/set_mode with invalid modeId yields Session-lane error`` () =
        let sid = SessionId "s-modes-invalid"
        let modes = mkModeState ()
        let badModeId = SessionModeId "not-a-real-mode"

        let trace: Message list =
            [ Message.FromClient(ClientToAgentMessage.Initialize initParams)
              Message.FromAgent(AgentToClientMessage.InitializeResult initResult)
              Message.FromClient(
                  ClientToAgentMessage.SessionNew
                      { cwd = "."
                        mcpServers = []
                        additionalDirectories = [] }
              )
              Message.FromAgent(AgentToClientMessage.SessionNewResult(mkNewSessionResult sid (Some modes)))
              Message.FromClient(ClientToAgentMessage.SessionSetMode { sessionId = sid; modeId = badModeId }) ]

        let result = runWithValidation sid spec trace true None None

        let sessionFindings =
            result.findings |> List.filter (fun f -> f.lane = Lane.Session)

        let invalidModeFinding =
            sessionFindings
            |> List.tryFind (fun f ->
                match f.failure with
                | Some failure -> failure.code = "ACP.SESSION.INVALID_MODE_ID"
                | None -> false)

        match invalidModeFinding with
        | Some f -> Assert.Equal(Severity.Error, f.severity)
        | None -> failwith "expected ACP.SESSION.INVALID_MODE_ID finding"

    [<Fact>]
    let ``close trace stays Ready and frees the closed session`` () =
        let sid = SessionId "s-close-1"

        let trace: Message list =
            [ Message.FromClient(ClientToAgentMessage.Initialize initParams)
              Message.FromAgent(AgentToClientMessage.InitializeResult initResult)
              Message.FromClient(
                  ClientToAgentMessage.SessionNew
                      { cwd = "."
                        mcpServers = []
                        additionalDirectories = [] }
              )
              Message.FromAgent(AgentToClientMessage.SessionNewResult(mkNewSessionResult sid None))
              Message.FromClient(ClientToAgentMessage.SessionClose { sessionId = sid; _meta = None })
              Message.FromAgent(AgentToClientMessage.SessionCloseResult { _meta = None }) ]

        let result = runWithValidation sid spec trace true None None
        Assert.True(result.findings.IsEmpty)

        match result.finalPhase with
        | Ok(Phase.Ready ctx) ->
            // Session freed on close request
            Assert.False(ctx.sessions |> Map.containsKey sid)
        | other -> failwithf "expected Phase.Ready, got %A" other

    [<Fact>]
    let ``closing one session keeps the connection ready for others`` () =
        let sid1 = SessionId "s-multi-1"
        let sid2 = SessionId "s-multi-2"
        let sid3 = SessionId "s-multi-3"

        let trace: Message list =
            [ Message.FromClient(ClientToAgentMessage.Initialize initParams)
              Message.FromAgent(AgentToClientMessage.InitializeResult initResult)
              // create s1
              Message.FromClient(
                  ClientToAgentMessage.SessionNew
                      { cwd = "."
                        mcpServers = []
                        additionalDirectories = [] }
              )
              Message.FromAgent(AgentToClientMessage.SessionNewResult(mkNewSessionResult sid1 None))
              // create s2
              Message.FromClient(
                  ClientToAgentMessage.SessionNew
                      { cwd = "."
                        mcpServers = []
                        additionalDirectories = [] }
              )
              Message.FromAgent(AgentToClientMessage.SessionNewResult(mkNewSessionResult sid2 None))
              // close s1
              Message.FromClient(ClientToAgentMessage.SessionClose { sessionId = sid1; _meta = None })
              Message.FromAgent(AgentToClientMessage.SessionCloseResult { _meta = None })
              // create s3 after s1 was closed — connection must still be Ready
              Message.FromClient(
                  ClientToAgentMessage.SessionNew
                      { cwd = "."
                        mcpServers = []
                        additionalDirectories = [] }
              )
              Message.FromAgent(AgentToClientMessage.SessionNewResult(mkNewSessionResult sid3 None)) ]

        let result = runWithValidation sid1 spec trace true None None
        Assert.True(result.findings.IsEmpty)

        match result.finalPhase with
        | Ok(Phase.Ready ctx) ->
            Assert.False(ctx.sessions |> Map.containsKey sid1)
            Assert.True(ctx.sessions |> Map.containsKey sid2)
            Assert.True(ctx.sessions |> Map.containsKey sid3)
        | other -> failwithf "expected Phase.Ready, got %A" other

    [<Fact>]
    let ``delete trace stays in Phase.Ready and removes session`` () =
        let sid = SessionId "s-delete-1"

        let trace: Message list =
            [ Message.FromClient(ClientToAgentMessage.Initialize initParams)
              Message.FromAgent(AgentToClientMessage.InitializeResult initResult)
              Message.FromClient(
                  ClientToAgentMessage.SessionNew
                      { cwd = "."
                        mcpServers = []
                        additionalDirectories = [] }
              )
              Message.FromAgent(AgentToClientMessage.SessionNewResult(mkNewSessionResult sid None))
              Message.FromClient(ClientToAgentMessage.SessionDelete { sessionId = sid; _meta = None })
              Message.FromAgent(AgentToClientMessage.SessionDeleteResult { _meta = None }) ]

        let result = runWithValidation sid spec trace true None None
        Assert.True(result.findings.IsEmpty)

        match result.finalPhase with
        | Ok(Phase.Ready ctx) ->
            // Session removed on request
            Assert.False(ctx.sessions |> Map.containsKey sid)
        | other -> failwithf "expected Phase.Ready, got %A" other

    [<Fact>]
    let ``capability roundtrip preserves close and delete markers`` () =
        let state0 = Acp.Codec.CodecState.empty

        // First decode an initialize request so we have a pending entry
        let initReq =
            """{"jsonrpc":"2.0","id":10,"method":"initialize","params":{"protocolVersion":1}}"""

        let state1, _ =
            match Acp.Codec.decode Acp.Codec.Direction.FromClient state0 initReq with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        let initRes =
            """{"jsonrpc":"2.0","id":10,"result":{"protocolVersion":1,"agentCapabilities":{"loadSession":true,"mcpCapabilities":{"http":false,"sse":false},"promptCapabilities":{"audio":false,"image":false,"embeddedContext":false},"sessionCapabilities":{"close":{},"delete":{}}},"authMethods":[]}}"""

        let _, msg =
            match Acp.Codec.decode Acp.Codec.Direction.FromAgent state1 initRes with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg with
        | Message.FromAgent(AgentToClientMessage.InitializeResult r) ->
            Assert.True(r.agentCapabilities.sessionCapabilities.close.IsSome)
            Assert.True(r.agentCapabilities.sessionCapabilities.delete.IsSome)
        | other -> failwithf "unexpected message %A" other

    [<Fact>]
    let ``ext request produces exactly one Protocol-lane Info finding with ACP.PROTOCOL.EXT_UNKNOWN code`` () =
        let sid = SessionId "s-ext-req"

        let trace: Message list =
            [ Message.FromClient(ClientToAgentMessage.Initialize initParams)
              Message.FromAgent(AgentToClientMessage.InitializeResult initResult)
              Message.FromClient(ClientToAgentMessage.ExtRequest("session/fork", None)) ]

        let result = runWithValidation sid spec trace false None None

        let extFindings =
            result.findings
            |> List.filter (fun f ->
                f.lane = Lane.Protocol
                && match f.failure with
                   | Some failure -> failure.code = "ACP.PROTOCOL.EXT_UNKNOWN"
                   | None -> false)

        Assert.Equal(1, extFindings.Length)
        Assert.Equal(Severity.Info, extFindings.[0].severity)

        let errorFindings =
            result.findings |> List.filter (fun f -> f.severity = Severity.Error)

        Assert.True(errorFindings.IsEmpty)

    [<Fact>]
    let ``ext notification from agent produces Protocol-lane Info finding with method name`` () =
        let sid = SessionId "s-ext-notif"

        let trace: Message list =
            [ Message.FromClient(ClientToAgentMessage.Initialize initParams)
              Message.FromAgent(AgentToClientMessage.InitializeResult initResult)
              Message.FromAgent(AgentToClientMessage.ExtNotification("plan_update", None)) ]

        let result = runWithValidation sid spec trace false None None

        let extFindings =
            result.findings
            |> List.filter (fun f ->
                f.lane = Lane.Protocol
                && match f.failure with
                   | Some failure -> failure.code = "ACP.PROTOCOL.EXT_UNKNOWN"
                   | None -> false)

        Assert.Equal(1, extFindings.Length)
        Assert.Equal(Severity.Info, extFindings.[0].severity)

        match extFindings.[0].failure with
        | Some failure -> Assert.Contains("plan_update", failure.message)
        | None -> failwith "expected failure record"

    [<Fact>]
    let ``session update with Ext tag produces Protocol-lane Info finding`` () =
        let sid = SessionId "s-ext-update"

        let trace: Message list =
            [ Message.FromClient(ClientToAgentMessage.Initialize initParams)
              Message.FromAgent(AgentToClientMessage.InitializeResult initResult)
              Message.FromClient(
                  ClientToAgentMessage.SessionNew
                      { cwd = "."
                        mcpServers = []
                        additionalDirectories = [] }
              )
              Message.FromAgent(AgentToClientMessage.SessionNewResult(mkNewSessionResult sid None))
              Message.FromAgent(
                  AgentToClientMessage.SessionUpdate(
                      mkSessionUpdate sid (SessionUpdate.Ext("plan_update", System.Text.Json.Nodes.JsonObject()))
                  )
              ) ]

        let result = runWithValidation sid spec trace false None None

        let extFindings =
            result.findings
            |> List.filter (fun f ->
                f.lane = Lane.Protocol
                && match f.failure with
                   | Some failure -> failure.code = "ACP.PROTOCOL.EXT_UNKNOWN"
                   | None -> false)

        Assert.Equal(1, extFindings.Length)
        Assert.Equal(Severity.Info, extFindings.[0].severity)

        let errorFindings =
            result.findings |> List.filter (fun f -> f.severity = Severity.Error)

        Assert.True(errorFindings.IsEmpty)

    [<Fact>]
    let ``happy path with no Ext produces no ACP.PROTOCOL.EXT_UNKNOWN finding`` () =
        let sid = SessionId "s-no-ext"
        let result = runWithValidation sid spec (mkHappyTrace sid) false None None

        let extFindings =
            result.findings
            |> List.filter (fun f ->
                match f.failure with
                | Some failure -> failure.code = "ACP.PROTOCOL.EXT_UNKNOWN"
                | None -> false)

        Assert.True(extFindings.IsEmpty)

    [<Fact>]
    let ``agent-side ExtRequest produces exactly one Protocol-lane Info ACP.PROTOCOL.EXT_UNKNOWN finding`` () =
        let sid = SessionId "s-ext-agent-req"

        let trace: Message list =
            [ Message.FromClient(ClientToAgentMessage.Initialize initParams)
              Message.FromAgent(AgentToClientMessage.InitializeResult initResult)
              Message.FromAgent(AgentToClientMessage.ExtRequest("session/fork", None)) ]

        let result = runWithValidation sid spec trace false None None

        let extFindings =
            result.findings
            |> List.filter (fun f ->
                f.lane = Lane.Protocol
                && match f.failure with
                   | Some failure -> failure.code = "ACP.PROTOCOL.EXT_UNKNOWN"
                   | None -> false)

        Assert.Equal(1, extFindings.Length)
        Assert.Equal(Severity.Info, extFindings.[0].severity)

        let errorFindings =
            result.findings |> List.filter (fun f -> f.severity = Severity.Error)

        Assert.True(errorFindings.IsEmpty)

    [<Fact>]
    let ``client-side ExtNotification produces exactly one Protocol-lane Info ACP.PROTOCOL.EXT_UNKNOWN finding`` () =
        let sid = SessionId "s-ext-client-notif"

        let trace: Message list =
            [ Message.FromClient(ClientToAgentMessage.Initialize initParams)
              Message.FromAgent(AgentToClientMessage.InitializeResult initResult)
              Message.FromClient(ClientToAgentMessage.ExtNotification("plan_update", None)) ]

        let result = runWithValidation sid spec trace false None None

        let extFindings =
            result.findings
            |> List.filter (fun f ->
                f.lane = Lane.Protocol
                && match f.failure with
                   | Some failure -> failure.code = "ACP.PROTOCOL.EXT_UNKNOWN"
                   | None -> false)

        Assert.Equal(1, extFindings.Length)
        Assert.Equal(Severity.Info, extFindings.[0].severity)

        let errorFindings =
            result.findings |> List.filter (fun f -> f.severity = Severity.Error)

        Assert.True(errorFindings.IsEmpty)
