namespace Acp

open Domain
open Domain.Messaging
open Validation
open Protocol

/// Transport-agnostic runtime adapter: fold inbound/outbound messages through validation
/// with profile-aware checks. Intended as the integration boundary for actual runtimes.
module RuntimeAdapter =

    /// Inbound frame with optional raw size hint (bytes) and decoded domain message.
    type InboundFrame =
        { rawByteLength : int option
          message       : Message }

    type InboundResult =
        { trace    : Validation.SessionTrace
          findings : Validation.ValidationFinding list
          phase    : Result<Protocol.Phase, Protocol.ProtocolError>
          message  : Message }

    /// Validate an inbound decoded message against profile and ACP spec.
    /// Pass rawByteLength if the transport knows the frame size for Transport-lane checks.
    let validateInbound
        (sessionId        : PrimitivesAndParties.SessionId)
        (profile          : Domain.Metadata.RuntimeProfile option)
        (frame            : InboundFrame)
        (stopOnFirstError : bool) : InboundResult =

        // Transport size check at the adapter boundary
        let sizeFindings =
            match frame.rawByteLength with
            | Some n ->
                Validation.Transport.validateSize profile Validation.Subject.Connection None n |> Option.toList
            | None -> []

        let specResult =
            Validation.runWithValidation sessionId Protocol.spec [ frame.message ] stopOnFirstError profile

        { trace    = specResult.trace
          findings = sizeFindings @ specResult.findings
          phase    = specResult.finalPhase
          message  = frame.message }

    /// Outbound validation helper (no size check by default; caller may supply size if desired).
    type OutboundFrame =
        { rawByteLength : int option
          message       : Message }

    type OutboundResult =
        { findings : Validation.ValidationFinding list
          phase    : Result<Protocol.Phase, Protocol.ProtocolError>
          message  : Message }

    let validateOutbound
        (sessionId        : PrimitivesAndParties.SessionId)
        (profile          : Domain.Metadata.RuntimeProfile option)
        (frame            : OutboundFrame)
        (stopOnFirstError : bool) : OutboundResult =

        let sizeFindings =
            match frame.rawByteLength with
            | Some n ->
                Validation.Transport.validateSize profile Validation.Subject.Connection None n |> Option.toList
            | None -> []

        let specResult =
            Validation.runWithValidation sessionId Protocol.spec [ frame.message ] stopOnFirstError profile

        { findings = sizeFindings @ specResult.findings
          phase    = specResult.finalPhase
          message  = frame.message }
