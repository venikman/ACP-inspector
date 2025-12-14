namespace Acp

open System

module Protocol =

    open Domain
    open Domain.PrimitivesAndParties
    open Domain.Initialization
    open Domain.SessionSetup
    open Domain.SessionModes
    open Domain.Prompting
    open Domain.Messaging
    open Domain.SessionContext

    // -------------
    // State & errors
    // -------------

    /// Per-session prompt-turn lifecycle on a single ACP connection.
    [<RequireQualifiedAccess>]
    type TurnState =
        | Idle of lastStopReason: StopReason option
        | PromptInFlight of cancelled: bool

    /// Invariant:
    /// - At most one prompt is in-flight per session.
    /// - `PromptInFlight cancelled = true` means a cancel has been requested;
    ///   the agent must still surface the final `CancelledTurnOutcome`.

    type SessionState = SessionContext.SessionState<TurnState>

    /// Initialization + session table once the connection is ready.
    type InitializedContext = SessionContext.InitializedContext<TurnState>

    /// High-level protocol phase for a single JSON-RPC stream.
    [<RequireQualifiedAccess>]
    type Phase =
        | AwaitingInitialize
        | WaitingForInitializeResult of clientInit: InitializeParams
        | Ready of InitializedContext

    type ProtocolError =
        | UnexpectedMessage of phase: Phase * message: Message
        | DuplicateInitialize
        | InitializeResultWithoutRequest
        | UnknownSession of SessionId
        | SessionAlreadyExists of SessionId
        | PromptAlreadyInFlight of SessionId
        | NoPromptInFlight of SessionId
    // TODO: add ACP/JSON-RPC error code mapping once spec version is pinned; emit ValidationFinding when unknown codes encountered.

    module ProtocolError =
        let code =
            function
            | ProtocolError.UnexpectedMessage _ -> "ACP.PROTOCOL.UNEXPECTED_MESSAGE"
            | ProtocolError.DuplicateInitialize -> "ACP.PROTOCOL.DUPLICATE_INITIALIZE"
            | ProtocolError.InitializeResultWithoutRequest -> "ACP.PROTOCOL.INIT_RESULT_WITHOUT_REQUEST"
            | ProtocolError.UnknownSession _ -> "ACP.PROTOCOL.UNKNOWN_SESSION"
            | ProtocolError.SessionAlreadyExists _ -> "ACP.PROTOCOL.SESSION_ALREADY_EXISTS"
            | ProtocolError.PromptAlreadyInFlight _ -> "ACP.PROTOCOL.PROMPT_ALREADY_IN_FLIGHT"
            | ProtocolError.NoPromptInFlight _ -> "ACP.PROTOCOL.NO_PROMPT_IN_FLIGHT"

        let describe =
            function
            | ProtocolError.UnexpectedMessage(phase, message) ->
                sprintf "Unexpected message %A in phase %A" message phase
            | ProtocolError.DuplicateInitialize -> "initialize was called more than once"
            | ProtocolError.InitializeResultWithoutRequest ->
                "initialize result observed without a pending initialize request"
            | ProtocolError.UnknownSession sid -> sprintf "Unknown session %s" (SessionId.value sid)
            | ProtocolError.SessionAlreadyExists sid -> sprintf "Session %s already exists" (SessionId.value sid)
            | ProtocolError.PromptAlreadyInFlight sid ->
                sprintf "Prompt already in flight for session %s" (SessionId.value sid)
            | ProtocolError.NoPromptInFlight sid -> sprintf "No prompt in flight for session %s" (SessionId.value sid)

    /// A tiny internal DSL: one pure transition function plus an initial phase.
    type Spec<'phase, 'message, 'error> =
        { initial: 'phase
          step: 'phase -> 'message -> Result<'phase, 'error> }

    module Spec =

        /// Fold a message trace through a Spec.
        let run (spec: Spec<'p, 'm, 'e>) (messages: seq<'m>) =
            ((Ok spec.initial), messages)
            ||> Seq.fold (fun acc msg -> acc |> Result.bind (fun phase -> spec.step phase msg))

    // -------------
    // Concrete ACP Spec<Phase,Message>
    // -------------

    let private updateSession sid f (ctx: InitializedContext) =
        match ctx.sessions |> Map.tryFind sid with
        | None -> Error(ProtocolError.UnknownSession sid)
        | Some s ->
            let s' = f s
            let sessions' = ctx.sessions |> Map.add sid s'
            Ok { ctx with sessions = sessions' }

    /// MVP Spec<Phase,Message> for ACP v0.9.x "core slice".
    /// Rules encoded:
    ///   - initialize must be first
    ///   - exactly one initialize result
    ///   - sessions are created by session/new or session/load results
    ///   - at most one prompt in flight per session
    ///   - cancel only allowed while a prompt is in flight
    ///   - request_permission only allowed while a prompt is in flight
    let spec: Spec<Phase, Message, ProtocolError> =
        let step phase message =
            match phase, message with

            // --- Initialization handshake ---

            | Phase.AwaitingInitialize, Message.FromClient(ClientToAgentMessage.Initialize init) ->
                Ok(Phase.WaitingForInitializeResult init)

            | Phase.AwaitingInitialize, _ -> Error(ProtocolError.UnexpectedMessage(phase, message))

            | Phase.WaitingForInitializeResult _, Message.FromClient(ClientToAgentMessage.Initialize _) ->
                Error ProtocolError.DuplicateInitialize

            | Phase.WaitingForInitializeResult clientInit,
              Message.FromAgent(AgentToClientMessage.InitializeResult agentInit) ->
                let ctx =
                    { clientInit = clientInit
                      agentInit = agentInit
                      sessions = Map.empty }

                Ok(Phase.Ready ctx)

            | Phase.WaitingForInitializeResult _, Message.FromAgent(AgentToClientMessage.SessionNewResult _)
            | Phase.WaitingForInitializeResult _, Message.FromAgent(AgentToClientMessage.SessionLoadResult _)
            | Phase.WaitingForInitializeResult _, Message.FromAgent(AgentToClientMessage.SessionPromptResult _)
            | Phase.WaitingForInitializeResult _, Message.FromAgent(AgentToClientMessage.SessionUpdate _)
            | Phase.WaitingForInitializeResult _, Message.FromAgent(AgentToClientMessage.RequestPermission _) ->
                Error ProtocolError.InitializeResultWithoutRequest

            | Phase.WaitingForInitializeResult _, Message.FromClient _
            | Phase.WaitingForInitializeResult _, Message.FromAgent _ ->
                Error(ProtocolError.UnexpectedMessage(phase, message))

            // --- Ready: sessions and turns ---

            // Ignore duplicate initialize messages after Ready; treat as protocol error.
            | Phase.Ready _, Message.FromClient(ClientToAgentMessage.Initialize _)
            | Phase.Ready _, Message.FromAgent(AgentToClientMessage.InitializeResult _) ->
                Error ProtocolError.DuplicateInitialize

            // session/new request does not change state; result creates the session.
            | Phase.Ready ctx, Message.FromClient(ClientToAgentMessage.SessionNew _) -> Ok(Phase.Ready ctx)

            | Phase.Ready ctx,
              Message.FromAgent(AgentToClientMessage.SessionNewResult { sessionId = sid; modes = modes }) ->
                if ctx.sessions |> Map.containsKey sid then
                    Error(ProtocolError.SessionAlreadyExists sid)
                else
                    let s =
                        { sessionId = sid
                          modeState = modes
                          turnState = TurnState.Idle None }

                    let sessions' = ctx.sessions |> Map.add sid s
                    Ok(Phase.Ready { ctx with sessions = sessions' })

            // session/load request: state unchanged; result ensures the session is tracked.
            | Phase.Ready ctx, Message.FromClient(ClientToAgentMessage.SessionLoad _) -> Ok(Phase.Ready ctx)

            | Phase.Ready ctx,
              Message.FromAgent(AgentToClientMessage.SessionLoadResult { sessionId = sid; modes = modes }) ->
                let sessions' =
                    if ctx.sessions |> Map.containsKey sid then
                        let s = ctx.sessions.[sid]

                        let s' =
                            match modes with
                            | None -> s
                            | Some _ -> { s with modeState = modes }

                        ctx.sessions |> Map.add sid s'
                    else
                        let s =
                            { sessionId = sid
                              modeState = modes
                              turnState = TurnState.Idle None }

                        ctx.sessions |> Map.add sid s

                Ok(Phase.Ready { ctx with sessions = sessions' })

            // session/set_mode: allowed whenever the session exists (idle or prompt in flight).
            | Phase.Ready ctx, Message.FromClient(ClientToAgentMessage.SessionSetMode ssm) ->
                match ctx.sessions |> Map.tryFind ssm.sessionId with
                | None -> Error(ProtocolError.UnknownSession ssm.sessionId)
                | Some _ -> Ok(Phase.Ready ctx)

            | Phase.Ready ctx, Message.FromAgent(AgentToClientMessage.SessionSetModeResult r) ->
                match ctx.sessions |> Map.tryFind r.sessionId with
                | None -> Error(ProtocolError.UnknownSession r.sessionId)
                | Some s ->
                    let s' =
                        match s.modeState with
                        | None -> s
                        | Some ms ->
                            { s with
                                modeState = Some { ms with currentModeId = r.modeId } }

                    let sessions' = ctx.sessions |> Map.add s.sessionId s'
                    Ok(Phase.Ready { ctx with sessions = sessions' })

            // session/prompt: ensure known session and no prompt in flight.
            | Phase.Ready ctx, Message.FromClient(ClientToAgentMessage.SessionPrompt p) ->
                match ctx.sessions |> Map.tryFind p.sessionId with
                | None -> Error(ProtocolError.UnknownSession p.sessionId)
                | Some s ->
                    match s.turnState with
                    | TurnState.Idle _ ->
                        let s' =
                            { s with
                                turnState = TurnState.PromptInFlight false }

                        let sessions' = ctx.sessions |> Map.add s.sessionId s'
                        Ok(Phase.Ready { ctx with sessions = sessions' })
                    | TurnState.PromptInFlight _ -> Error(ProtocolError.PromptAlreadyInFlight s.sessionId)

            // session/prompt result: close the turn.
            | Phase.Ready ctx, Message.FromAgent(AgentToClientMessage.SessionPromptResult r) ->
                match ctx.sessions |> Map.tryFind r.sessionId with
                | None -> Error(ProtocolError.UnknownSession r.sessionId)
                | Some s ->
                    match s.turnState with
                    | TurnState.PromptInFlight _ ->
                        let s' =
                            { s with
                                turnState = TurnState.Idle(Some r.stopReason) }

                        let sessions' = ctx.sessions |> Map.add s.sessionId s'
                        Ok(Phase.Ready { ctx with sessions = sessions' })
                    | TurnState.Idle _ -> Error(ProtocolError.NoPromptInFlight s.sessionId)

            // session/cancel: only valid while a prompt is in flight.
            | Phase.Ready ctx, Message.FromClient(ClientToAgentMessage.SessionCancel c) ->
                match ctx.sessions |> Map.tryFind c.sessionId with
                | None -> Error(ProtocolError.UnknownSession c.sessionId)
                | Some s ->
                    match s.turnState with
                    | TurnState.PromptInFlight _ ->
                        let s' =
                            { s with
                                turnState = TurnState.PromptInFlight true }

                        let sessions' = ctx.sessions |> Map.add s.sessionId s'
                        Ok(Phase.Ready { ctx with sessions = sessions' })
                    | TurnState.Idle _ -> Error(ProtocolError.NoPromptInFlight s.sessionId)

            // session/update: allowed whenever the session exists.
            // This covers both prompt streaming and session/load replay.
            | Phase.Ready ctx, Message.FromAgent(AgentToClientMessage.SessionUpdate u) ->
                match ctx.sessions |> Map.tryFind u.sessionId with
                | None -> Error(ProtocolError.UnknownSession u.sessionId)
                | Some s ->
                    match u.update with
                    | SessionUpdate.CurrentModeUpdate currentModeId ->
                        let s' =
                            match s.modeState with
                            | None -> s
                            | Some ms ->
                                { s with
                                    modeState =
                                        Some
                                            { ms with
                                                currentModeId = currentModeId } }

                        let sessions' = ctx.sessions |> Map.add s.sessionId s'
                        Ok(Phase.Ready { ctx with sessions = sessions' })
                    | _ -> Ok(Phase.Ready ctx)

            // session/request_permission: must be inside a prompt turn.
            | Phase.Ready ctx, Message.FromAgent(AgentToClientMessage.RequestPermission p) ->
                match ctx.sessions |> Map.tryFind p.sessionId with
                | None -> Error(ProtocolError.UnknownSession p.sessionId)
                | Some s ->
                    match s.turnState with
                    | TurnState.PromptInFlight _ -> Ok(Phase.Ready ctx)
                    | TurnState.Idle _ -> Error(ProtocolError.NoPromptInFlight s.sessionId)

        { initial = Phase.AwaitingInitialize
          step = step }
