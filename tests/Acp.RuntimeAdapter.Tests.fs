namespace Acp.Tests

open Xunit
open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Capabilities
open Acp.Domain.Initialization
open Acp.Domain.SessionSetup
open Acp.Domain.Prompting
open Acp.Domain.Messaging
open Acp.Protocol
open Acp.Validation
open Acp.Eval
open Acp.RuntimeAdapter

module RuntimeAdapterTests =

    let sid = SessionId "s-runtime"

    let fsCaps = { FileSystemCapabilities.readTextFile = true; writeTextFile = true }
    let clientCaps = { ClientCapabilities.fs = fsCaps; terminal = false }
    let mcpCaps = { McpCapabilities.http = false; sse = false }
    let promptCaps = { PromptCapabilities.audio = false; image = false; embeddedContext = false }
    let agentCaps = { AgentCapabilities.loadSession = true; mcpCapabilities = mcpCaps; promptCapabilities = promptCaps }
    let clientInfo = { ImplementationInfo.name = "test-client"; title = None; version = None }
    let agentInfo = { ImplementationInfo.name = "test-agent"; title = None; version = None }

    let initParams : InitializeParams =
        { protocolVersion = 1; clientCapabilities = clientCaps; clientInfo = Some clientInfo }
    let initResult : InitializeResult =
        { negotiatedVersion = 1; agentCapabilities = agentCaps; agentInfo = Some agentInfo }

    [<Fact>]
    let ``inbound oversized frame yields Transport finding`` () =
        let profile : Metadata.RuntimeProfile =
            { metadata = Metadata.MetadataPolicy.AllowOpaque
              transport = Some { lineSeparator = None; maxFrameBytes = None; maxMessageBytes = Some 5; metaEnvelope = None } }

        let frame : InboundFrame =
            { rawByteLength = Some 10
              message = Message.FromClient (ClientToAgentMessage.Initialize initParams) }

        let result = validateInboundWithEval sid (Some profile) None frame true

        let transportFinding =
            result.findings
            |> List.tryFind (fun f ->
                f.lane = Lane.Transport &&
                match f.failure with
                | Some failure -> failure.code = "ACP.TRANSPORT.MAX_MESSAGE_BYTES_EXCEEDED"
                | None -> false)

        Assert.True(transportFinding.IsSome)

    [<Fact>]
    let ``valid inbound passes spec and yields no findings`` () =
        let profile : Metadata.RuntimeProfile =
            { metadata = Metadata.MetadataPolicy.AllowOpaque
              transport = None }

        let frame : InboundFrame =
            { rawByteLength = Some 4
              message = Message.FromClient (ClientToAgentMessage.Initialize initParams) }

        let result = validateInboundWithEval sid (Some profile) None frame true

        Assert.True(result.findings.IsEmpty)
        match result.phase with
        | Ok (Phase.WaitingForInitializeResult _) -> ()
        | other -> failwithf "unexpected phase %A" other

    [<Fact>]
    let ``custom eval profile allows empty prompt`` () =
        let evalProfile =
            { defaultProfile with requireNonEmptyInstruction = false }

        let frame : InboundFrame =
            { rawByteLength = None
              message = Message.FromClient (ClientToAgentMessage.SessionPrompt { sessionId = sid; content = [] }) }

        let result = validateInboundWithEval sid None (Some evalProfile) frame true

        let evalFindings =
            result.findings
            |> List.filter (fun f -> f.lane = Lane.Eval)

        Assert.True(evalFindings.IsEmpty)
