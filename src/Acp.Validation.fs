namespace Acp

open System
open System.Diagnostics

module Validation =

    open Domain
    open Domain.PrimitivesAndParties
    open Domain.Metadata
    open Domain.Messaging
    open Domain.SessionModes
    open Domain.Tracing
    open Protocol

    /// Assurance lanes (R18) – where in the assurance stack this finding lives.
    [<RequireQualifiedAccess>]
    type Lane =
        | Protocol // ACP/JSON-RPC shape & sequencing
        | Session // Session/turn invariants (R3, R4, R16)
        | ToolSurface // Tool call execution & resources (R13–R15)
        | Transport // Stdio framing, timeouts, truncation (R19)
        | Eval // Eval judges (LLM/code) running alongside sentinel
        | Implementation // Agent/client local checks (logging, perf, etc.)

    [<RequireQualifiedAccess>]
    type Severity =
        | Info
        | Warning
        | Error

    /// Location / subject of a validation observation.
    [<RequireQualifiedAccess>]
    type Subject =
        | Connection
        | Session of SessionId
        | PromptTurn of SessionId * turnOrdinal: int
        | MessageAt of index: int * message: Message
        | ToolCall of toolCallId: string

    /// R12 – validator-detected violation.
    type ValidationFailure =
        { code: string // stable ID, e.g., "ACP.PROTOCOL.UNEXPECTED_MESSAGE"
          message: string // human-readable
          subject: Subject }

    /// R18 – structured validation event (can be a failure or a positive/neutral note).
    type ValidationFinding =
        { lane: Lane
          severity: Severity
          subject: Subject
          failure: ValidationFailure option
          // Optional anchors into a SessionTrace:
          sessionId: SessionId option
          traceIndex: int option
          // Optional free-form note for humans:
          note: string option }

    /// Reuse the domain-level session trace abstraction.
    type SessionTrace = Tracing.SessionTrace

    module SessionTrace =

        let empty sessionId =
            { sessionId = sessionId; messages = [] }

        let append (msg: Message) (trace: SessionTrace) =
            { trace with
                messages = trace.messages @ [ msg ] }

        let length (trace: SessionTrace) = trace.messages.Length

    module FromProtocol =

        let private sessionFromMessage =
            function
            | Message.FromClient(ClientToAgentMessage.SessionPrompt p) -> Some p.sessionId
            | Message.FromClient(ClientToAgentMessage.SessionCancel p) -> Some p.sessionId
            | Message.FromClient(ClientToAgentMessage.SessionLoad p) -> Some p.sessionId
            | Message.FromClient(ClientToAgentMessage.SessionSetMode p) -> Some p.sessionId
            | Message.FromClient(ClientToAgentMessage.FsReadTextFileError(req, _)) -> Some req.sessionId
            | Message.FromClient(ClientToAgentMessage.FsWriteTextFileError(req, _)) -> Some req.sessionId
            | Message.FromClient(ClientToAgentMessage.SessionRequestPermissionError(req, _)) -> Some req.sessionId
            | Message.FromClient(ClientToAgentMessage.TerminalCreateError(req, _)) -> Some req.sessionId
            | Message.FromClient(ClientToAgentMessage.TerminalOutputError(req, _)) -> Some req.sessionId
            | Message.FromClient(ClientToAgentMessage.TerminalWaitForExitError(req, _)) -> Some req.sessionId
            | Message.FromClient(ClientToAgentMessage.TerminalKillError(req, _)) -> Some req.sessionId
            | Message.FromClient(ClientToAgentMessage.TerminalReleaseError(req, _)) -> Some req.sessionId
            | Message.FromAgent(AgentToClientMessage.SessionNewResult r) -> Some r.sessionId
            | Message.FromAgent(AgentToClientMessage.SessionPromptResult r) -> Some r.sessionId
            | Message.FromAgent(AgentToClientMessage.SessionPromptError(req, _)) -> Some req.sessionId
            | Message.FromAgent(AgentToClientMessage.SessionLoadResult r) -> Some r.sessionId
            | Message.FromAgent(AgentToClientMessage.SessionLoadError(req, _)) -> Some req.sessionId
            | Message.FromAgent(AgentToClientMessage.SessionSetModeResult r) -> Some r.sessionId
            | Message.FromAgent(AgentToClientMessage.SessionSetModeError(req, _)) -> Some req.sessionId
            | Message.FromAgent(AgentToClientMessage.SessionUpdate u) -> Some u.sessionId
            | Message.FromAgent(AgentToClientMessage.SessionRequestPermissionRequest r) -> Some r.sessionId
            | _ -> None

        let private subjectOfError
            (ctx: InitializedContext option)
            (msg: Message)
            (error: ProtocolError)
            (traceIndex: int option)
            : Subject * SessionId option * int option =

            let sidFromMsg = sessionFromMessage msg

            let subjectFromError =
                match error with
                | ProtocolError.UnknownSession sid
                | ProtocolError.SessionAlreadyExists sid
                | ProtocolError.PromptAlreadyInFlight sid
                | ProtocolError.NoPromptInFlight sid -> Subject.Session sid
                | ProtocolError.UnexpectedMessage _ ->
                    match traceIndex with
                    | Some i -> Subject.MessageAt(i, msg)
                    | None -> Subject.Connection
                | ProtocolError.DuplicateInitialize
                | ProtocolError.InitializeResultWithoutRequest -> Subject.Connection

            let subject =
                match subjectFromError, sidFromMsg with
                | Subject.Connection, Some sid -> Subject.Session sid
                | s, _ -> s

            let sidForFinding =
                match subjectFromError, sidFromMsg with
                | Subject.Session sid, _ -> Some sid
                | _, Some sid -> Some sid
                | _ ->
                    match ctx with
                    | Some c when c.sessions |> Map.count = 1 ->
                        c.sessions |> Seq.head |> (fun kvp -> Some kvp.Value.sessionId)
                    | _ -> None

            subject, sidForFinding, traceIndex

        let ofProtocolError
            (ctx: InitializedContext option)
            (msg: Message)
            (error: ProtocolError)
            (traceIndex: int option)
            : ValidationFinding =
            let code = ProtocolError.code error
            let human = ProtocolError.describe error

            let subject, sidOpt, idxOpt = subjectOfError ctx msg error traceIndex

            let failure =
                { code = code
                  message = human
                  subject = subject }

            { lane = Lane.Protocol
              severity = Severity.Error
              subject = subject
              failure = Some failure
              sessionId = sidOpt
              traceIndex = idxOpt
              note = None }

    // -----------------
    // Session-lane invariants
    // -----------------

    /// Try to find the first (prompt, optional cancel, result) window for a given session.
    let private tryFindPromptWindow (sid: SessionId) (trace: SessionTrace) =
        let indexed = trace.messages |> List.mapi (fun i m -> i, m)

        let promptOpt =
            indexed
            |> List.tryFind (fun (_, msg) ->
                match msg with
                | Message.FromClient(ClientToAgentMessage.SessionPrompt p) -> p.sessionId = sid
                | _ -> false)

        match promptOpt with
        | None -> None
        | Some(promptIdx, _) ->
            let afterPrompt = indexed |> List.skip (promptIdx + 1)

            let resultOpt =
                afterPrompt
                |> List.tryFind (fun (_, msg) ->
                    match msg with
                    | Message.FromAgent(AgentToClientMessage.SessionPromptResult r) -> r.sessionId = sid
                    | Message.FromAgent(AgentToClientMessage.SessionPromptError(req, _)) -> req.sessionId = sid
                    | _ -> false)

            match resultOpt with
            | None -> None
            | Some(resultIdx, Message.FromAgent(AgentToClientMessage.SessionPromptResult r)) ->
                let between = indexed |> List.filter (fun (i, _) -> i > promptIdx && i < resultIdx)

                let cancelOpt =
                    between
                    |> List.tryFind (fun (_, msg) ->
                        match msg with
                        | Message.FromClient(ClientToAgentMessage.SessionCancel c) -> c.sessionId = sid
                        | _ -> false)

                let stopReason = Some r.stopReason

                let promptOrdinal =
                    indexed
                    |> List.take promptIdx
                    |> List.filter (fun (_, msg) ->
                        match msg with
                        | Message.FromClient(ClientToAgentMessage.SessionPrompt p) when p.sessionId = sid -> true
                        | _ -> false)
                    |> List.length

                Some(promptIdx, cancelOpt |> Option.map fst, resultIdx, stopReason, promptOrdinal)

            | Some(resultIdx, Message.FromAgent(AgentToClientMessage.SessionPromptError _)) ->
                let between = indexed |> List.filter (fun (i, _) -> i > promptIdx && i < resultIdx)

                let cancelOpt =
                    between
                    |> List.tryFind (fun (_, msg) ->
                        match msg with
                        | Message.FromClient(ClientToAgentMessage.SessionCancel c) -> c.sessionId = sid
                        | _ -> false)

                let stopReason = None

                let promptOrdinal =
                    indexed
                    |> List.take promptIdx
                    |> List.filter (fun (_, msg) ->
                        match msg with
                        | Message.FromClient(ClientToAgentMessage.SessionPrompt p) when p.sessionId = sid -> true
                        | _ -> false)
                    |> List.length

                Some(promptIdx, cancelOpt |> Option.map fst, resultIdx, stopReason, promptOrdinal)

            | Some _ -> None

    /// Session-lane invariant: if a cancel occurs between prompt and result, stopReason must be Cancelled.
    let private checkSessionCancelInvariant (trace: SessionTrace) : ValidationFinding list =
        match tryFindPromptWindow trace.sessionId trace with
        | None -> []
        | Some(promptIdx, cancelIdxOpt, resultIdx, stopReasonOpt, promptOrdinal) ->
            match cancelIdxOpt, stopReasonOpt with
            | None, _ -> []
            | Some _, None -> []
            | Some cancelIdx, Some stopReason ->
                match stopReason with
                | StopReason.Cancelled -> []
                | _ ->
                    let subject = Subject.PromptTurn(trace.sessionId, promptOrdinal)

                    let failure =
                        { code = "ACP.SESSION.CANCEL_MISMATCH"
                          message = "SessionCancel was sent, but the prompt result stopReason is not Cancelled."
                          subject = subject }

                    [ { lane = Lane.Session
                        severity = Severity.Error
                        subject = subject
                        failure = Some failure
                        sessionId = Some trace.sessionId
                        traceIndex = Some resultIdx
                        note = Some(sprintf "promptIdx=%d; cancelIdx=%d; resultIdx=%d" promptIdx cancelIdx resultIdx) } ]

    /// Session-lane invariants:
    /// - At most one prompt in flight per session.
    /// - A SessionPromptResult must not appear before any SessionPrompt.
    let private checkSessionPromptConcurrency (trace: SessionTrace) : ValidationFinding list =
        let mutable openPrompts = 0
        let mutable promptOrdinal = 0
        let mutable findings: ValidationFinding list = []

        trace.messages
        |> List.iteri (fun idx msg ->
            match msg with
            | Message.FromClient(ClientToAgentMessage.SessionPrompt p) when p.sessionId = trace.sessionId ->
                let currentOrdinal = promptOrdinal

                if openPrompts > 0 then
                    let subject = Subject.PromptTurn(trace.sessionId, currentOrdinal)

                    let failure =
                        { code = "ACP.SESSION.MULTIPLE_PROMPTS_IN_FLIGHT"
                          message = "A new SessionPrompt was sent while a previous prompt is still in flight."
                          subject = subject }

                    findings <-
                        findings
                        @ [ { lane = Lane.Session
                              severity = Severity.Error
                              subject = subject
                              failure = Some failure
                              sessionId = Some trace.sessionId
                              traceIndex = Some idx
                              note = Some(sprintf "index=%d; openPrompts=%d" idx openPrompts) } ]

                openPrompts <- openPrompts + 1
                promptOrdinal <- promptOrdinal + 1

            | Message.FromAgent(AgentToClientMessage.SessionPromptResult r) when r.sessionId = trace.sessionId ->
                if openPrompts = 0 then
                    let subject = Subject.PromptTurn(trace.sessionId, 0)

                    let failure =
                        { code = "ACP.SESSION.RESULT_WITHOUT_PROMPT"
                          message = "SessionPromptResult was sent without a prior in-flight SessionPrompt."
                          subject = subject }

                    findings <-
                        findings
                        @ [ { lane = Lane.Session
                              severity = Severity.Error
                              subject = subject
                              failure = Some failure
                              sessionId = Some trace.sessionId
                              traceIndex = Some idx
                              note = Some(sprintf "index=%d; openPrompts=%d" idx openPrompts) } ]
                else
                    openPrompts <- max 0 (openPrompts - 1)

            | Message.FromAgent(AgentToClientMessage.SessionPromptError(req, _)) when req.sessionId = trace.sessionId ->
                if openPrompts = 0 then
                    let subject = Subject.PromptTurn(trace.sessionId, 0)

                    let failure =
                        { code = "ACP.SESSION.ERROR_WITHOUT_PROMPT"
                          message = "SessionPrompt error response was sent without a prior in-flight SessionPrompt."
                          subject = subject }

                    findings <-
                        findings
                        @ [ { lane = Lane.Session
                              severity = Severity.Error
                              subject = subject
                              failure = Some failure
                              sessionId = Some trace.sessionId
                              traceIndex = Some idx
                              note = Some(sprintf "index=%d; openPrompts=%d" idx openPrompts) } ]
                else
                    openPrompts <- max 0 (openPrompts - 1)

            | Message.FromClient(ClientToAgentMessage.SessionCancel p) when p.sessionId = trace.sessionId ->
                // Do not close the prompt here; ACP still requires a SessionPromptResult (typically Cancelled).
                ()

            | _ -> ())

        findings

    /// Session-lane invariants for session modes:
    /// - If a mode state is known, set_mode requests must target a mode in availableModes.
    /// - If a mode state is known, current_mode_update must report a mode in availableModes.
    let private checkSessionModes (trace: SessionTrace) : ValidationFinding list =
        let sid = trace.sessionId
        let subject = Subject.Session sid

        let mutable modeStateOpt: SessionModeState option = None
        let mutable findings: ValidationFinding list = []

        let isKnownMode (modeId: SessionModeId) =
            match modeStateOpt with
            | None -> false
            | Some ms -> ms.availableModes |> List.exists (fun m -> m.id = modeId)

        let addFinding traceIndex severity code message note =
            let failure =
                { code = code
                  message = message
                  subject = subject }

            findings <-
                findings
                @ [ { lane = Lane.Session
                      severity = severity
                      subject = subject
                      failure = Some failure
                      sessionId = Some sid
                      traceIndex = Some traceIndex
                      note = note } ]

        trace.messages
        |> List.iteri (fun idx msg ->
            match msg with
            | Message.FromAgent(AgentToClientMessage.SessionNewResult r) when r.sessionId = sid ->
                modeStateOpt <- r.modes
            | Message.FromAgent(AgentToClientMessage.SessionLoadResult r) when r.sessionId = sid ->
                modeStateOpt <- r.modes

            | Message.FromClient(ClientToAgentMessage.SessionSetMode p) when p.sessionId = sid ->
                match modeStateOpt with
                | None ->
                    addFinding
                        idx
                        Severity.Warning
                        "ACP.SESSION.SET_MODE_WITHOUT_MODES"
                        "session/set_mode was sent, but the agent did not advertise any session modes."
                        None
                | Some _ ->
                    if not (isKnownMode p.modeId) then
                        addFinding
                            idx
                            Severity.Error
                            "ACP.SESSION.INVALID_MODE_ID"
                            "session/set_mode requested a modeId that is not present in availableModes."
                            (Some(sprintf "modeId=%s" (SessionModeId.value p.modeId)))

            | Message.FromAgent(AgentToClientMessage.SessionSetModeResult r) when r.sessionId = sid ->
                match modeStateOpt with
                | None -> ()
                | Some ms -> modeStateOpt <- Some { ms with currentModeId = r.modeId }

            | Message.FromAgent(AgentToClientMessage.SessionUpdate u) when u.sessionId = sid ->
                match u.update with
                | Prompting.SessionUpdate.CurrentModeUpdate { currentModeId = currentModeId } ->
                    match modeStateOpt with
                    | None -> ()
                    | Some ms ->
                        if not (isKnownMode currentModeId) then
                            addFinding
                                idx
                                Severity.Error
                                "ACP.SESSION.CURRENT_MODE_NOT_IN_AVAILABLE_MODES"
                                "current_mode_update reported a currentModeId that is not present in availableModes."
                                (Some(sprintf "currentModeId=%s" (SessionModeId.value currentModeId)))

                        modeStateOpt <-
                            Some
                                { ms with
                                    currentModeId = currentModeId }
                | _ -> ()

            | _ -> ())

        findings

    // -----------------
    // Transport + metadata helpers (profile-aware, optional to call from runtime)
    // -----------------

    module Transport =

        let validateSize
            (profile: RuntimeProfile option)
            (subject: Subject)
            (traceIndex: int option)
            (actualBytes: int)
            : ValidationFinding option =

            match profile with
            | None -> None
            | Some p ->
                match p.transport with
                | None -> None
                | Some t ->
                    match t.maxMessageBytes with
                    | None -> None
                    | Some limit when actualBytes > limit ->
                        let failure =
                            { code = "ACP.TRANSPORT.MAX_MESSAGE_BYTES_EXCEEDED"
                              message = sprintf "Message size %d bytes exceeds limit %d" actualBytes limit
                              subject = subject }

                        Some
                            { lane = Lane.Transport
                              severity = Severity.Error
                              subject = subject
                              failure = Some failure
                              sessionId = None
                              traceIndex = traceIndex
                              note = None }
                    | Some _ -> None

    module MetadataProfile =

        let validate
            (policy: MetadataPolicy)
            (kind: string)
            (subject: Subject)
            (traceIndex: int option)
            : ValidationFinding option =

            match policy with
            | MetadataPolicy.Disallow ->
                let failure =
                    { code = "ACP.METADATA.DISALLOWED"
                      message = sprintf "Metadata/content kind '%s' is not allowed" kind
                      subject = subject }

                Some
                    { lane = Lane.Transport
                      severity = Severity.Error
                      subject = subject
                      failure = Some failure
                      sessionId = None
                      traceIndex = traceIndex
                      note = None }
            | MetadataPolicy.AllowOpaque -> None
            | MetadataPolicy.AllowKinds kinds ->
                if kinds |> List.contains kind then
                    None
                else
                    let failure =
                        { code = "ACP.METADATA.DISALLOWED_KIND"
                          message = sprintf "Metadata/content kind '%s' not in allowlist" kind
                          subject = subject }

                    Some
                        { lane = Lane.Transport
                          severity = Severity.Error
                          subject = subject
                          failure = Some failure
                          sessionId = None
                          traceIndex = traceIndex
                          note = None }

    type SpecRunResult<'phase> =
        { finalPhase: Result<'phase, ProtocolError>
          trace: SessionTrace
          findings: ValidationFinding list }

    let runWithValidation
        (sessionId: SessionId)
        (spec: Spec<Phase, Message, ProtocolError>)
        (messages: Message list)
        (stopOnFirstError: bool)
        (profile: RuntimeProfile option)
        (evalProfile: Eval.EvalProfile option)
        : SpecRunResult<Phase> =

        let sessionIdText = SessionId.value sessionId

        use activity =
            Observability.startActivity
                "acp.validation.run"
                ActivityKind.Internal
                [ Observability.SessionIdTag, sessionIdText
                  "acp.message.count", messages.Length
                  "acp.stop_on_first_error", stopOnFirstError ]

        let sw = Stopwatch.StartNew()

        let mutable trace = SessionTrace.empty sessionId
        let mutable findings: ValidationFinding list = []
        let mutable phaseResult: Result<Phase, ProtocolError> = Ok spec.initial
        let mutable continueProcessing = true

        let evalProfile = defaultArg evalProfile Eval.defaultProfile

        let validateMessageExtensibility traceIndex (msg: Message) =
            match profile with
            | None -> ()
            | Some rp ->
                let policy = rp.metadata

                let validate kind =
                    match
                        MetadataProfile.validate policy kind (Subject.MessageAt(traceIndex, msg)) (Some traceIndex)
                    with
                    | Some f -> findings <- findings @ [ f ]
                    | None -> ()

                match msg with
                | Message.FromClient(ClientToAgentMessage.ExtNotification(methodName, _)) -> validate methodName
                | Message.FromClient(ClientToAgentMessage.ExtRequest(methodName, _)) -> validate methodName
                | Message.FromClient(ClientToAgentMessage.ExtResponse(methodName, _)) -> validate methodName
                | Message.FromClient(ClientToAgentMessage.ExtError(methodName, _)) -> validate methodName
                | Message.FromAgent(AgentToClientMessage.ExtNotification(methodName, _)) -> validate methodName
                | Message.FromAgent(AgentToClientMessage.ExtRequest(methodName, _)) -> validate methodName
                | Message.FromAgent(AgentToClientMessage.ExtResponse(methodName, _)) -> validate methodName
                | Message.FromAgent(AgentToClientMessage.ExtError(methodName, _)) -> validate methodName
                | _ -> ()

        let addEvalFindings (msg: Message) =
            // Eval findings are advisory; map to Eval lane.
            let evalFindings = Eval.runPromptChecks evalProfile msg

            let mapped =
                evalFindings
                |> List.map (fun ef ->
                    { lane = Lane.Eval
                      severity =
                        match ef.severity with
                        | Eval.EvalSeverity.Info -> Severity.Info
                        | Eval.EvalSeverity.Warning -> Severity.Warning
                        | Eval.EvalSeverity.Error -> Severity.Error
                      subject = Subject.Connection
                      failure =
                        Some
                            { code = ef.code
                              message = ef.message
                              subject = Subject.Connection }
                      sessionId = None
                      traceIndex = None
                      note = Some(sprintf "judge=%A" ef.judge) })

            findings <- findings @ mapped

        for (idx, msg) in messages |> List.indexed do
            trace <- SessionTrace.append msg trace
            validateMessageExtensibility idx msg
            addEvalFindings msg

            if continueProcessing then
                match phaseResult with
                | Ok phase ->
                    match spec.step phase msg with
                    | Ok phase' -> phaseResult <- Ok phase'
                    | Error e ->
                        let ctx =
                            match phase with
                            | Phase.Ready ctx -> Some ctx
                            | _ -> None

                        let vf = FromProtocol.ofProtocolError ctx msg e (Some idx)
                        findings <- findings @ [ vf ]
                        phaseResult <- Error e

                        if stopOnFirstError then
                            continueProcessing <- false
                | Error _ -> continueProcessing <- false

        let sessionCancelFindings = checkSessionCancelInvariant trace
        let sessionConcurrencyFindings = checkSessionPromptConcurrency trace
        let sessionModeFindings = checkSessionModes trace

        let combinedFindings =
            findings
            @ sessionCancelFindings
            @ sessionConcurrencyFindings
            @ sessionModeFindings

        sw.Stop()
        Observability.recordValidationRun sessionIdText sw.Elapsed.TotalMilliseconds

        for f in combinedFindings do
            let lane = sprintf "%A" f.lane
            let severity = sprintf "%A" f.severity
            Observability.recordValidationFinding sessionIdText lane severity

        { finalPhase = phaseResult
          trace = trace
          findings = combinedFindings }

    module PromptOutcome =

        open Domain.Prompting

        let private tryLastStopReason sessionId (trace: SessionTrace) =
            trace.messages
            |> List.choose (function
                | Message.FromAgent(AgentToClientMessage.SessionPromptResult r) when r.sessionId = sessionId ->
                    Some r.stopReason
                | _ -> None)
            |> List.tryLast

        let private wasCancelledByUser sessionId (trace: SessionTrace) =
            trace.messages
            |> List.exists (function
                | Message.FromClient(ClientToAgentMessage.SessionCancel c) when c.sessionId = sessionId -> true
                | _ -> false)

        /// Derive a PromptTurnOutcome from the trace and final phase/error.
        let classify
            (sessionId: SessionId)
            (trace: SessionTrace)
            (finalPhase: Result<Phase, ProtocolError>)
            (findings: ValidationFinding list)
            : PromptTurnOutcome =

            let cancelled = wasCancelledByUser sessionId trace

            match finalPhase with
            | Error e ->
                let details = ProtocolError.describe e
                PromptTurnOutcome.DomainError(DomainErrorOutcome.ProtocolViolation(ProtocolError.code e, details))
            | Ok _ ->
                match tryLastStopReason sessionId trace with
                | Some stopReason when cancelled -> PromptTurnOutcome.CancelledByUser
                | Some stopReason -> PromptTurnOutcome.Completed stopReason
                | None ->
                    let protocolFinding =
                        findings
                        |> List.tryFind (fun f ->
                            match f.subject with
                            | Subject.Session sid when sid = sessionId -> true
                            | _ -> false)

                    match protocolFinding with
                    | Some f ->
                        let msg =
                            f.failure
                            |> Option.map (fun v -> v.message)
                            |> Option.defaultValue "prompt turn failed without stop reason"

                        PromptTurnOutcome.DomainError(DomainErrorOutcome.ProtocolViolation(f.failure.Value.code, msg))
                    | None ->
                        PromptTurnOutcome.DomainError(
                            DomainErrorOutcome.AgentInternalFailure(
                                sprintf "No SessionPromptResult observed for session %s" (SessionId.value sessionId)
                            )
                        )
