namespace Acp

open Domain
open Domain.Messaging
open Validation
open Protocol

/// Transport-agnostic runtime adapter: fold inbound/outbound messages through validation
/// with profile-aware checks. Intended as the integration boundary for actual runtimes.
module RuntimeAdapter =

    /// Combined validation configuration for runtime use.
    type ValidationConfig =
        { runtimeProfile: Domain.Metadata.RuntimeProfile option
          evalProfile: Eval.EvalProfile option }

    let defaultValidationConfig: ValidationConfig =
        { runtimeProfile = None
          evalProfile = None }

    /// Inbound frame with optional raw size hint (bytes) and decoded domain message.
    type InboundFrame =
        { rawByteLength: int option
          message: Message }

    type InboundResult =
        { trace: Validation.SessionTrace
          findings: Validation.ValidationFinding list
          phase: Result<Protocol.Phase, Protocol.ProtocolError>
          message: Message }

    /// Internal helper with explicit eval profile.
    let validateInboundWithEval
        (sessionId: PrimitivesAndParties.SessionId)
        (profile: Domain.Metadata.RuntimeProfile option)
        (evalProfile: Eval.EvalProfile option)
        (frame: InboundFrame)
        (stopOnFirstError: bool)
        : InboundResult =

        // Transport size check at the adapter boundary
        let sizeFindings =
            match frame.rawByteLength with
            | Some n ->
                Validation.Transport.validateSize profile Validation.Subject.Connection None n
                |> Option.toList
            | None -> []

        let specResult =
            Validation.runWithValidation sessionId Protocol.spec [ frame.message ] stopOnFirstError profile evalProfile

        { trace = specResult.trace
          findings = sizeFindings @ specResult.findings
          phase = specResult.finalPhase
          message = frame.message }

    /// Validate an inbound decoded message against profile and ACP spec.
    /// Pass rawByteLength if the transport knows the frame size for Transport-lane checks.
    /// Uses the default eval profile unless provided via the helper above.
    let validateInbound
        (sessionId: PrimitivesAndParties.SessionId)
        (profile: Domain.Metadata.RuntimeProfile option)
        (frame: InboundFrame)
        (stopOnFirstError: bool)
        : InboundResult =
        validateInboundWithEval sessionId profile None frame stopOnFirstError

    /// Validate inbound using a combined ValidationConfig (runtime + eval).
    let validateInboundWithConfig
        (sessionId: PrimitivesAndParties.SessionId)
        (config: ValidationConfig)
        (frame: InboundFrame)
        (stopOnFirstError: bool)
        : InboundResult =
        validateInboundWithEval sessionId config.runtimeProfile config.evalProfile frame stopOnFirstError

    /// Outbound validation helper (no size check by default; caller may supply size if desired).
    type OutboundFrame =
        { rawByteLength: int option
          message: Message }

    type OutboundResult =
        { findings: Validation.ValidationFinding list
          phase: Result<Protocol.Phase, Protocol.ProtocolError>
          message: Message }

    let validateOutboundWithEval
        (sessionId: PrimitivesAndParties.SessionId)
        (profile: Domain.Metadata.RuntimeProfile option)
        (evalProfile: Eval.EvalProfile option)
        (frame: OutboundFrame)
        (stopOnFirstError: bool)
        : OutboundResult =

        let sizeFindings =
            match frame.rawByteLength with
            | Some n ->
                Validation.Transport.validateSize profile Validation.Subject.Connection None n
                |> Option.toList
            | None -> []

        let specResult =
            Validation.runWithValidation sessionId Protocol.spec [ frame.message ] stopOnFirstError profile evalProfile

        { findings = sizeFindings @ specResult.findings
          phase = specResult.finalPhase
          message = frame.message }

    /// Outbound validation helper (no size check by default; caller may supply size if desired).
    /// Uses the default eval profile unless provided via the helper above.
    let validateOutbound
        (sessionId: PrimitivesAndParties.SessionId)
        (profile: Domain.Metadata.RuntimeProfile option)
        (frame: OutboundFrame)
        (stopOnFirstError: bool)
        : OutboundResult =
        validateOutboundWithEval sessionId profile None frame stopOnFirstError

    /// Validate outbound using a combined ValidationConfig (runtime + eval).
    let validateOutboundWithConfig
        (sessionId: PrimitivesAndParties.SessionId)
        (config: ValidationConfig)
        (frame: OutboundFrame)
        (stopOnFirstError: bool)
        : OutboundResult =
        validateOutboundWithEval sessionId config.runtimeProfile config.evalProfile frame stopOnFirstError
