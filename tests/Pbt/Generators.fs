namespace Acp.Tests.Pbt

open System
open FsCheck

open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Capabilities
open Acp.Domain.Initialization
open Acp.Domain.SessionSetup
open Acp.Domain.Prompting
open Acp.Domain.Messaging

/// Tiny generator library for ACP domain types.
module Generators =

    module G = FsCheck.FSharp.Gen
    module A = FsCheck.FSharp.Arb

    // -----------------
    // Basic primitives
    // -----------------

    let private genSmallString: Gen<string> =
        let chars = [ 'a' .. 'z' ] @ [ '0' .. '9' ] @ [ '-'; '_' ]

        G.sized (fun s ->
            let len = max 1 (min 12 (s + 1))
            G.arrayOfLength len (G.elements chars) |> G.map (fun arr -> String(arr)))

    let genSessionId: Gen<SessionId> = genSmallString |> G.map SessionId

    let arbSessionId = A.fromGen genSessionId

    let genStopReason: Gen<StopReason> =
        G.frequency
            [ 5, G.constant StopReason.EndTurn
              3, G.constant StopReason.Cancelled
              1, G.constant StopReason.MaxTokens
              1, G.constant StopReason.MaxTurnRequests
              1, G.constant StopReason.Refusal ]

    let arbStopReason = A.fromGen genStopReason

    // -----------------
    // Prompt content
    // -----------------

    let private genResourceLink: Gen<ResourceLink> =
        genSmallString
        |> G.map (fun s ->
            { name = s
              uri = "file:///" + s
              title = None
              description = None
              mimeType = None
              size = None
              annotations = None })

    let genContentBlock: Gen<ContentBlock> =
        let genEmbedded =
            genSmallString
            |> G.map (fun s ->
                ContentBlock.Resource
                    { resource = EmbeddedResourceResource.Text("file:///" + s, s, None)
                      annotations = None })

        G.frequency
            [ 7,
              genSmallString
              |> G.map (fun s -> ContentBlock.Text { text = s; annotations = None })
              2, genResourceLink |> G.map ContentBlock.ResourceLink
              1, genEmbedded ]

    let arbContentBlock = A.fromGen genContentBlock

    let private genContentBlocks: Gen<ContentBlock list> = G.listOf genContentBlock

    // -----------------
    // Fixed-ish capability payloads (reduce noise)
    // -----------------

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
          sessionCapabilities = SessionCapabilities.empty }

    let private clientInfo: ImplementationInfo =
        { name = "pbt-client"
          title = None
          version = "0.0.0-test" }

    let private agentInfo: ImplementationInfo =
        { name = "pbt-agent"
          title = None
          version = "0.0.0-test" }

    let genInitializeParams: Gen<InitializeParams> =
        G.constant
            { protocolVersion = ProtocolVersion.current
              clientCapabilities = clientCaps
              clientInfo = Some clientInfo }

    let genInitializeResult: Gen<InitializeResult> =
        G.constant
            { protocolVersion = ProtocolVersion.current
              agentCapabilities = agentCaps
              agentInfo = Some agentInfo
              authMethods = [] }

    let private genNewSessionParams: Gen<NewSessionParams> =
        G.constant { cwd = "."; mcpServers = [] }

    let private genLoadSessionParams (sid: SessionId) : Gen<LoadSessionParams> =
        G.constant
            { sessionId = sid
              cwd = "."
              mcpServers = [] }

    let private genSessionPromptParams (sid: SessionId) : Gen<SessionPromptParams> =
        genContentBlocks |> G.map (fun blocks -> { sessionId = sid; prompt = blocks })

    let private genSessionPromptResult (sid: SessionId) : Gen<SessionPromptResult> =
        genStopReason |> G.map (fun sr -> { sessionId = sid; stopReason = sr })

    let private genSessionUpdate (sid: SessionId) : Gen<SessionUpdateNotification> =
        let genChunk = genContentBlock |> G.map (fun cb -> ({ content = cb }: ContentChunk))

        let genUpdate =
            G.frequency
                [ 3, genChunk |> G.map SessionUpdate.UserMessageChunk
                  3, genChunk |> G.map SessionUpdate.AgentMessageChunk
                  1, genChunk |> G.map SessionUpdate.AgentThoughtChunk ]

        genUpdate |> G.map (fun u -> { sessionId = sid; update = u })

    let private genToolCallUpdate: Gen<ToolCallUpdate> =
        G.zip genSmallString genContentBlock
        |> G.map (fun (id, block) ->
            { toolCallId = id
              title = None
              kind = None
              status = Some ToolCallStatus.Pending
              content = Some [ ToolCallContent.Content({ content = block }: Content) ]
              locations = Some []
              rawInput = None
              rawOutput = None })

    let private genRequestPermissionParams (sid: SessionId) : Gen<RequestPermissionParams> =
        genToolCallUpdate
        |> G.map (fun tc ->
            { sessionId = sid
              toolCall = tc
              options = [] })

    // -----------------
    // Message generators
    // -----------------

    let private genClientMessageAny: Gen<ClientToAgentMessage> =
        G.frequency
            [ 2, genInitializeParams |> G.map ClientToAgentMessage.Initialize
              2, genNewSessionParams |> G.map ClientToAgentMessage.SessionNew
              2,
              genSessionId
              |> G.bind genLoadSessionParams
              |> G.map ClientToAgentMessage.SessionLoad
              3,
              genSessionId
              |> G.bind genSessionPromptParams
              |> G.map ClientToAgentMessage.SessionPrompt
              2,
              genSessionId
              |> G.map (fun sid -> ClientToAgentMessage.SessionCancel { sessionId = sid }) ]

    let private genAgentMessageAny: Gen<AgentToClientMessage> =
        G.frequency
            [ 2, genInitializeResult |> G.map AgentToClientMessage.InitializeResult
              2,
              genSessionId
              |> G.map (fun sid -> AgentToClientMessage.SessionNewResult { sessionId = sid; modes = None })
              2,
              genSessionId
              |> G.map (fun sid -> AgentToClientMessage.SessionLoadResult { sessionId = sid; modes = None })
              3,
              genSessionId
              |> G.bind genSessionPromptResult
              |> G.map AgentToClientMessage.SessionPromptResult
              2,
              genSessionId
              |> G.bind genSessionUpdate
              |> G.map AgentToClientMessage.SessionUpdate
              1,
              genSessionId
              |> G.bind genRequestPermissionParams
              |> G.map AgentToClientMessage.SessionRequestPermissionRequest ]

    /// Unconstrained message generator (used for "invalid injection").
    let genMessageAny: Gen<Message> =
        G.frequency
            [ 5, genClientMessageAny |> G.map Message.FromClient
              4, genAgentMessageAny |> G.map Message.FromAgent ]

    // -----------------
    // Near-valid trace generator (state-aware)
    // -----------------

    type private SimplePhase =
        | AwaitingInitialize
        | WaitingForInitializeResult
        | Ready of Map<SessionId, bool> // bool = promptInFlight

    let private genValidStep (phase: SimplePhase) : Gen<Message * SimplePhase> =
        match phase with
        | AwaitingInitialize ->
            genInitializeParams
            |> G.map (fun p -> Message.FromClient(ClientToAgentMessage.Initialize p), WaitingForInitializeResult)

        | WaitingForInitializeResult ->
            genInitializeResult
            |> G.map (fun r -> Message.FromAgent(AgentToClientMessage.InitializeResult r), Ready Map.empty)

        | Ready sessions ->
            let known = sessions |> Map.toList |> List.map fst

            let idle =
                sessions
                |> Map.toList
                |> List.choose (fun (sid, inflight) -> if inflight then None else Some sid)

            let inflight =
                sessions
                |> Map.toList
                |> List.choose (fun (sid, inflight) -> if inflight then Some sid else None)

            let genSessionNewReq =
                genNewSessionParams
                |> G.map (fun p -> Message.FromClient(ClientToAgentMessage.SessionNew p), Ready sessions)

            let genSessionNewResult =
                genSessionId
                |> G.where (fun sid -> not (sessions |> Map.containsKey sid))
                |> G.map (fun sid ->
                    Message.FromAgent(AgentToClientMessage.SessionNewResult { sessionId = sid; modes = None }),
                    Ready(sessions |> Map.add sid false))

            let genSessionLoadReq =
                genSessionId
                |> G.bind genLoadSessionParams
                |> G.map (fun p -> Message.FromClient(ClientToAgentMessage.SessionLoad p), Ready sessions)

            let genSessionLoadResult =
                genSessionId
                |> G.map (fun sid ->
                    let sessions' =
                        if sessions |> Map.containsKey sid then
                            sessions
                        else
                            sessions |> Map.add sid false

                    Message.FromAgent(AgentToClientMessage.SessionLoadResult { sessionId = sid; modes = None }),
                    Ready sessions')

            let genPromptReq =
                G.elements idle
                |> G.bind (fun sid ->
                    genSessionPromptParams sid
                    |> G.map (fun p ->
                        Message.FromClient(ClientToAgentMessage.SessionPrompt p), Ready(sessions |> Map.add sid true)))

            let genPromptResult =
                G.elements inflight
                |> G.bind (fun sid ->
                    genSessionPromptResult sid
                    |> G.map (fun r ->
                        Message.FromAgent(AgentToClientMessage.SessionPromptResult r),
                        Ready(sessions |> Map.add sid false)))

            let genCancelReq =
                G.elements inflight
                |> G.map (fun sid ->
                    Message.FromClient(ClientToAgentMessage.SessionCancel { sessionId = sid }), Ready sessions)

            let genUpdateNotif =
                G.elements known
                |> G.bind (fun sid ->
                    genSessionUpdate sid
                    |> G.map (fun u -> Message.FromAgent(AgentToClientMessage.SessionUpdate u), Ready sessions))

            let genRequestPermission =
                G.elements inflight
                |> G.bind (fun sid ->
                    genRequestPermissionParams sid
                    |> G.map (fun p ->
                        Message.FromAgent(AgentToClientMessage.SessionRequestPermissionRequest p), Ready sessions))

            let choices: (int * Gen<Message * SimplePhase>) list =
                [ 3, genSessionNewReq
                  3, genSessionNewResult
                  2, genSessionLoadReq
                  2, genSessionLoadResult
                  if idle.Length > 0 then
                      4, genPromptReq
                  else
                      0,
                      G.constant (
                          Message.FromClient(ClientToAgentMessage.SessionNew { cwd = "."; mcpServers = [] }),
                          Ready sessions
                      )
                  if inflight.Length > 0 then
                      4, genPromptResult
                  else
                      0,
                      G.constant (
                          Message.FromClient(ClientToAgentMessage.SessionNew { cwd = "."; mcpServers = [] }),
                          Ready sessions
                      )
                  if inflight.Length > 0 then
                      2, genCancelReq
                  else
                      0,
                      G.constant (
                          Message.FromClient(ClientToAgentMessage.SessionNew { cwd = "."; mcpServers = [] }),
                          Ready sessions
                      )
                  if known.Length > 0 then
                      2, genUpdateNotif
                  else
                      0,
                      G.constant (
                          Message.FromClient(ClientToAgentMessage.SessionNew { cwd = "."; mcpServers = [] }),
                          Ready sessions
                      )
                  if inflight.Length > 0 then
                      1, genRequestPermission
                  else
                      0,
                      G.constant (
                          Message.FromClient(ClientToAgentMessage.SessionNew { cwd = "."; mcpServers = [] }),
                          Ready sessions
                      ) ]
                |> List.filter (fun (w, _) -> w > 0)

            G.frequency choices

    /// Generate a biased "near-valid" ACP message trace with occasional invalid injections.
    let genTrace: Gen<Message list> =
        G.sized (fun size ->
            let len = max 0 (min 40 (size + 1))

            let rec loop n phase =
                if n <= 0 then
                    G.constant []
                else
                    G.frequency
                        [ 8,
                          genValidStep phase
                          |> G.bind (fun (msg, phase') -> loop (n - 1) phase' |> G.map (fun rest -> msg :: rest))
                          2,
                          genMessageAny
                          |> G.bind (fun msg -> loop (n - 1) phase |> G.map (fun rest -> msg :: rest)) ]

            loop len AwaitingInitialize)

    let arbTrace = A.fromGen genTrace

    /// Generate a fully valid ACP message trace (no invalid injections).
    let genValidTrace: Gen<Message list> =
        G.sized (fun size ->
            let len = max 0 (min 40 (size + 1))

            let rec loop n phase =
                if n <= 0 then
                    G.constant []
                else
                    genValidStep phase
                    |> G.bind (fun (msg, phase') -> loop (n - 1) phase' |> G.map (fun rest -> msg :: rest))

            loop len AwaitingInitialize)

    let arbValidTrace = A.fromGen genValidTrace

    // -----------------
    // FsCheck config helpers (env-driven)
    // -----------------

    module PbtConfig =

        let private tryGetInt (name: string) =
            match Environment.GetEnvironmentVariable name with
            | null -> None
            | s ->
                match Int32.TryParse s with
                | true, v -> Some v
                | _ -> None

        let maxTest = defaultArg (tryGetInt "ACP_PBT_MAX_TEST") 200
        let startSize = defaultArg (tryGetInt "ACP_PBT_START_SIZE") 1
        let endSize = defaultArg (tryGetInt "ACP_PBT_END_SIZE") 50

        /// Optional replay seed (single int). If set, PBT runs are reproducible.
        let replay =
            match tryGetInt "ACP_PBT_SEED" with
            | None -> None
            | Some seed ->
                try
                    Some
                        { Rnd = new Rnd(uint64 seed)
                          Size = None }
                with _ ->
                    None

        let config =
            Config.QuickThrowOnFailure
                .WithMaxTest(maxTest)
                .WithStartSize(startSize)
                .WithEndSize(endSize)
                .WithReplay(replay)
                .WithRunner(EvidenceRunner.PbtEvidenceRunner())
