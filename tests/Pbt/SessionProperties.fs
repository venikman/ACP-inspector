namespace Acp.Tests.Pbt

open Xunit
open FsCheck

module P = FsCheck.FSharp.Prop

open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Capabilities
open Acp.Domain.Initialization
open Acp.Domain.SessionSetup
open Acp.Domain.Prompting
open Acp.Domain.Messaging
open Acp.Protocol
open Acp.Validation

open Acp.Tests.Pbt.Generators

module SessionProperties =

    let private config = Generators.PbtConfig.config

    let private clientCaps: ClientCapabilities =
        { fs =
            { readTextFile = true
              writeTextFile = true }
          terminal = false }

    let private agentCaps: AgentCapabilities =
        { loadSession = true
          mcpCapabilities = { http = false; sse = false }
          promptCapabilities =
            { audio = false
              image = false
              embeddedContext = false }
          sessionCapabilities = SessionCapabilities.empty }

    let private initParams: InitializeParams =
        { protocolVersion = ProtocolVersion.current
          clientCapabilities = clientCaps
          clientInfo = None }

    let private initResult: InitializeResult =
        { protocolVersion = ProtocolVersion.current
          agentCapabilities = agentCaps
          agentInfo = None
          authMethods = [] }

    let private handshake: Message list =
        [ Message.FromClient(ClientToAgentMessage.Initialize initParams)
          Message.FromAgent(AgentToClientMessage.InitializeResult initResult) ]

    let private hasSessionFailure code (findings: ValidationFinding list) =
        findings
        |> List.exists (fun f ->
            f.lane = Lane.Session
            && match f.failure with
               | Some failure -> failure.code = code
               | None -> false)

    [<Fact>]
    let ``SessionPromptResult without prompt yields RESULT_WITHOUT_PROMPT`` () =
        let prop =
            P.forAll Generators.arbSessionId (fun sid ->
                let msgs =
                    handshake
                    @ [ Message.FromAgent(
                            AgentToClientMessage.SessionPromptResult
                                { sessionId = sid
                                  stopReason = StopReason.EndTurn
                                  usage = None }
                        ) ]

                let r = runWithValidation sid spec msgs false None None
                hasSessionFailure "ACP.SESSION.RESULT_WITHOUT_PROMPT" r.findings)

        Check.One(config, prop)

    [<Fact>]
    let ``Two prompts without result yields MULTIPLE_PROMPTS_IN_FLIGHT`` () =
        let prop =
            P.forAll Generators.arbSessionId (fun sid ->
                let msgs =
                    handshake
                    @ [ Message.FromAgent(AgentToClientMessage.SessionNewResult { sessionId = sid; modes = None })
                        Message.FromClient(ClientToAgentMessage.SessionPrompt { sessionId = sid; prompt = [] })
                        Message.FromClient(ClientToAgentMessage.SessionPrompt { sessionId = sid; prompt = [] }) ]

                let r = runWithValidation sid spec msgs false None None
                hasSessionFailure "ACP.SESSION.MULTIPLE_PROMPTS_IN_FLIGHT" r.findings)

        Check.One(config, prop)

    [<Fact>]
    let ``Cancel between prompt and non-cancel result yields CANCEL_MISMATCH`` () =
        let genNonCancelled =
            Generators.genStopReason
            |> FsCheck.FSharp.Gen.where (fun sr -> sr <> StopReason.Cancelled)

        let prop =
            P.forAll
                (FsCheck.FSharp.Arb.fromGen (FsCheck.FSharp.Gen.zip Generators.genSessionId genNonCancelled))
                (fun (sid, sr) ->
                    let msgs =
                        handshake
                        @ [ Message.FromAgent(AgentToClientMessage.SessionNewResult { sessionId = sid; modes = None })
                            Message.FromClient(ClientToAgentMessage.SessionPrompt { sessionId = sid; prompt = [] })
                            Message.FromClient(ClientToAgentMessage.SessionCancel { sessionId = sid })
                            Message.FromAgent(
                                AgentToClientMessage.SessionPromptResult
                                    { sessionId = sid
                                      stopReason = sr
                                      usage = None }
                            ) ]

                    let r = runWithValidation sid spec msgs false None None
                    hasSessionFailure "ACP.SESSION.CANCEL_MISMATCH" r.findings)

        Check.One(config, prop)
