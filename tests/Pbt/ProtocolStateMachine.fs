namespace Acp.Tests.Pbt

open Xunit
open FsCheck
open FsCheck.Experimental
open FsCheck.FSharp

open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Capabilities
open Acp.Domain.Initialization
open Acp.Domain.SessionSetup
open Acp.Domain.Prompting
open Acp.Domain.Messaging
open Acp.Protocol

open Acp.Tests.Pbt.Generators

/// Stateful PBT over Protocol.spec using FsCheck Experimental StateMachine.
module ProtocolStateMachine =

    module G = FsCheck.FSharp.Gen

    type Model =
        | AwaitingInitialize
        | WaitingForInitializeResult
        | Ready of Map<SessionId, bool> // promptInFlight per session

    let private projectPhase (phase: Phase) : Model =
        match phase with
        | Phase.AwaitingInitialize -> AwaitingInitialize
        | Phase.WaitingForInitializeResult _ -> WaitingForInitializeResult
        | Phase.Ready ctx ->
            let sessions =
                ctx.sessions
                |> Map.map (fun _ s ->
                    match s.turnState with
                    | TurnState.PromptInFlight _ -> true
                    | _ -> false)

            Ready sessions

    let private apply (actual: Phase ref) (msg: Message) : bool =
        match spec.step actual.Value msg with
        | Ok phase' ->
            actual.Value <- phase'
            true
        | Error _ -> false

    // ---- Operation builders ----

    let private opInitialize (p: InitializeParams) =
        StateMachine.operation "Initialize" (fun _ -> WaitingForInitializeResult) (fun (actual, model) ->
            let msg = Message.FromClient(ClientToAgentMessage.Initialize p)
            let ok = apply actual msg
            ok && (projectPhase actual.Value = WaitingForInitializeResult))

    let private opInitializeResult (r: InitializeResult) =
        StateMachine.operation "InitializeResult" (fun _ -> Ready Map.empty) (fun (actual, model) ->
            let msg = Message.FromAgent(AgentToClientMessage.InitializeResult r)
            let ok = apply actual msg
            ok && (projectPhase actual.Value = Ready Map.empty))

    let private opSessionNewReq =
        StateMachine.operation "SessionNew" (fun m -> m) (fun (actual, model) ->
            let msg =
                Message.FromClient(ClientToAgentMessage.SessionNew { cwd = "."; mcpServers = [] })

            apply actual msg && (projectPhase actual.Value = model))

    let private opSessionNewResult (sid: SessionId) =
        StateMachine.operation
            "SessionNewResult"
            (function
            | Ready sessions -> Ready(sessions |> Map.add sid false)
            | m -> m)
            (fun (actual, model) ->
                let msg =
                    Message.FromAgent(AgentToClientMessage.SessionNewResult { sessionId = sid; modes = None })

                let ok = apply actual msg

                ok
                && match model with
                   | Ready sessions -> projectPhase actual.Value = Ready(sessions |> Map.add sid false)
                   | _ -> false)

    let private opSessionLoadReq (sid: SessionId) =
        StateMachine.operation "SessionLoad" (fun m -> m) (fun (actual, model) ->
            let msg =
                Message.FromClient(
                    ClientToAgentMessage.SessionLoad
                        { sessionId = sid
                          cwd = "."
                          mcpServers = [] }
                )

            apply actual msg && (projectPhase actual.Value = model))

    let private opSessionLoadResult (sid: SessionId) =
        StateMachine.operation
            "SessionLoadResult"
            (function
            | Ready sessions ->
                if sessions |> Map.containsKey sid then
                    Ready sessions
                else
                    Ready(sessions |> Map.add sid false)
            | m -> m)
            (fun (actual, model) ->
                let msg =
                    Message.FromAgent(AgentToClientMessage.SessionLoadResult { sessionId = sid; modes = None })

                let ok = apply actual msg

                ok
                && match model with
                   | Ready sessions ->
                       let sessions' =
                           if sessions |> Map.containsKey sid then
                               sessions
                           else
                               sessions |> Map.add sid false

                       projectPhase actual.Value = Ready sessions'
                   | _ -> false)

    let private opPromptReq (sid: SessionId) =
        StateMachine.operation
            "SessionPrompt"
            (function
            | Ready sessions -> Ready(sessions |> Map.add sid true)
            | m -> m)
            (fun (actual, model) ->
                let msg =
                    Message.FromClient(
                        ClientToAgentMessage.SessionPrompt
                            { sessionId = sid
                              prompt = []
                              _meta = None }
                    )

                let ok = apply actual msg

                ok
                && match model with
                   | Ready sessions -> projectPhase actual.Value = Ready(sessions |> Map.add sid true)
                   | _ -> false)

    let private opPromptResult (sid: SessionId) (sr: StopReason) =
        StateMachine.operation
            "SessionPromptResult"
            (function
            | Ready sessions -> Ready(sessions |> Map.add sid false)
            | m -> m)
            (fun (actual, model) ->
                let msg =
                    Message.FromAgent(
                        AgentToClientMessage.SessionPromptResult
                            { sessionId = sid
                              stopReason = sr
                              usage = None
                              _meta = None }
                    )

                let ok = apply actual msg

                ok
                && match model with
                   | Ready sessions -> projectPhase actual.Value = Ready(sessions |> Map.add sid false)
                   | _ -> false)

    let private opCancelReq (sid: SessionId) =
        StateMachine.operation "SessionCancel" (fun m -> m) (fun (actual, model) ->
            let msg = Message.FromClient(ClientToAgentMessage.SessionCancel { sessionId = sid })
            apply actual msg && (projectPhase actual.Value = model))

    let private opSessionUpdate (sid: SessionId) =
        StateMachine.operation "SessionUpdate" (fun m -> m) (fun (actual, model) ->
            let msg =
                Message.FromAgent(
                    AgentToClientMessage.SessionUpdate
                        { sessionId = sid
                          update =
                            SessionUpdate.AgentMessageChunk(
                                ({ content = ContentBlock.Text { text = "ok"; annotations = None } }: ContentChunk)
                            )
                          _meta = None }
                )

            apply actual msg && (projectPhase actual.Value = model))

    let private opRequestPermission (sid: SessionId) =
        let tc: ToolCallUpdate =
            { toolCallId = "tc-1"
              title = None
              kind = None
              status = Some ToolCallStatus.Pending
              content = Some []
              locations = Some []
              rawInput = None
              rawOutput = None }

        StateMachine.operation "RequestPermission" (fun m -> m) (fun (actual, model) ->
            let msg =
                Message.FromAgent(
                    AgentToClientMessage.SessionRequestPermissionRequest
                        { sessionId = sid
                          toolCall = tc
                          options = [] }
                )

            apply actual msg && (projectPhase actual.Value = model))

    // ---- Machine ----

    let private setup =
        StateMachine.setup (fun () -> ref Phase.AwaitingInitialize) (fun () -> AwaitingInitialize)

    let private next (model: Model) : Gen<Operation<Phase ref, Model>> =
        match model with
        | AwaitingInitialize -> Generators.genInitializeParams |> G.map opInitialize

        | WaitingForInitializeResult -> Generators.genInitializeResult |> G.map opInitializeResult

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

            let genNewResult =
                Generators.genSessionId
                |> G.where (fun sid -> not (sessions |> Map.containsKey sid))
                |> G.map opSessionNewResult

            let genLoadReq = Generators.genSessionId |> G.map opSessionLoadReq

            let genLoadResult = Generators.genSessionId |> G.map opSessionLoadResult

            let genPromptReq = G.elements idle |> G.map opPromptReq

            let genPromptResult =
                G.elements inflight
                |> G.bind (fun sid -> Generators.genStopReason |> G.map (fun sr -> opPromptResult sid sr))

            let genCancelReq = G.elements inflight |> G.map opCancelReq

            let genUpdate = G.elements known |> G.map opSessionUpdate

            let genReqPerm = G.elements inflight |> G.map opRequestPermission

            let choices: (int * Gen<Operation<Phase ref, Model>>) list =
                [ 2, G.constant opSessionNewReq
                  2, genNewResult
                  1, genLoadReq
                  1, genLoadResult
                  if idle.Length > 0 then
                      3, genPromptReq
                  else
                      0, G.constant opSessionNewReq
                  if inflight.Length > 0 then
                      3, genPromptResult
                  else
                      0, G.constant opSessionNewReq
                  if inflight.Length > 0 then
                      1, genCancelReq
                  else
                      0, G.constant opSessionNewReq
                  if known.Length > 0 then
                      1, genUpdate
                  else
                      0, G.constant opSessionNewReq
                  if inflight.Length > 0 then
                      1, genReqPerm
                  else
                      0, G.constant opSessionNewReq ]
                |> List.filter (fun (w, _) -> w > 0)

            G.frequency choices

    let private machine =
        { new Machine<Phase ref, Model>() with
            override _.Setup = Arb.fromGen (G.constant setup)
            override _.Next m = next m }

    [<Fact>]
    let ``Protocol.spec state machine holds over generated sequences`` () =
        let prop = StateMachine.generate machine |> Arb.fromGen |> StateMachine.forAll

        Check.One(Generators.PbtConfig.config, prop)
