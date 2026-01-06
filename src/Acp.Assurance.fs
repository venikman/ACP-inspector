namespace Acp

open System

/// BC-001: Assurance Context
/// Trust & Assurance Calculus (F-G-R) for agent protocol messages.
/// FPF Grounding: B.3, C.2, G.6
module Assurance =

    // =====================
    // Core Types (from BC-001 Kind Signatures)
    // =====================

    /// Formality level (F0..F9) - degree of structural rigor in claim expression.
    /// F0 = informal prose, F9 = machine-verified proof.
    [<RequireQualifiedAccess>]
    type Formality =
        | F0 // Informal prose, no structure
        | F1 // Labeled prose
        | F2 // Template-based
        | F3 // Semi-structured (JSON schema, type hints)
        | F4 // Structured with constraints
        | F5 // Formally typed
        | F6 // Formally specified (contracts, invariants)
        | F7 // Proof sketches
        | F8 // Partial proofs
        | F9 // Machine-verified (proofs, model-checked)

    [<RequireQualifiedAccess>]
    module Formality =
        let toInt =
            function
            | Formality.F0 -> 0
            | Formality.F1 -> 1
            | Formality.F2 -> 2
            | Formality.F3 -> 3
            | Formality.F4 -> 4
            | Formality.F5 -> 5
            | Formality.F6 -> 6
            | Formality.F7 -> 7
            | Formality.F8 -> 8
            | Formality.F9 -> 9

        let fromInt =
            function
            | 0 -> Formality.F0
            | 1 -> Formality.F1
            | 2 -> Formality.F2
            | 3 -> Formality.F3
            | 4 -> Formality.F4
            | 5 -> Formality.F5
            | 6 -> Formality.F6
            | 7 -> Formality.F7
            | 8 -> Formality.F8
            | 9 -> Formality.F9
            | n -> failwithf "Invalid formality level: %d" n

        let tryFromInt n =
            if n >= 0 && n <= 9 then Some(fromInt n) else None

    /// Assurance level (L0..L2) - ordinal classification of epistemic support.
    [<RequireQualifiedAccess>]
    type AssuranceLevel =
        | L0 // Unsubstantiated - no evidence path exists
        | L1 // Circumstantial - evidence exists but incomplete or indirect
        | L2 // Evidenced - complete evidence path to grounding

    [<RequireQualifiedAccess>]
    module AssuranceLevel =
        let toInt =
            function
            | AssuranceLevel.L0 -> 0
            | AssuranceLevel.L1 -> 1
            | AssuranceLevel.L2 -> 2

        let fromInt =
            function
            | 0 -> AssuranceLevel.L0
            | 1 -> AssuranceLevel.L1
            | 2 -> AssuranceLevel.L2
            | n -> failwithf "Invalid assurance level: %d" n

        let min a b = if toInt a <= toInt b then a else b

        /// Parse from string (case-insensitive)
        let tryParse (s: string) =
            match s.ToUpperInvariant() with
            | "L0"
            | "UNSUBSTANTIATED" -> Some AssuranceLevel.L0
            | "L1"
            | "CIRCUMSTANTIAL" -> Some AssuranceLevel.L1
            | "L2"
            | "EVIDENCED" -> Some AssuranceLevel.L2
            | _ -> None

    /// Congruence level (CL0..CL4) - semantic translation fidelity.
    /// Used when claims cross context boundaries.
    [<RequireQualifiedAccess>]
    type CongruenceLevel =
        | CL0 // Incompatible - no meaningful translation
        | CL1 // Lossy - significant meaning loss
        | CL2 // Approximate - moderate meaning loss
        | CL3 // HighFidelity - minimal meaning loss
        | CL4 // Equivalent - no meaning loss

    [<RequireQualifiedAccess>]
    module CongruenceLevel =
        let toInt =
            function
            | CongruenceLevel.CL0 -> 0
            | CongruenceLevel.CL1 -> 1
            | CongruenceLevel.CL2 -> 2
            | CongruenceLevel.CL3 -> 3
            | CongruenceLevel.CL4 -> 4

        /// Penalty factor applied to Reliability when crossing bridge
        let penalty =
            function
            | CongruenceLevel.CL4 -> 1.0
            | CongruenceLevel.CL3 -> 0.9
            | CongruenceLevel.CL2 -> 0.7
            | CongruenceLevel.CL1 -> 0.4
            | CongruenceLevel.CL0 -> 0.0

        let min a b = if toInt a <= toInt b then a else b

    // =====================
    // Scope & Context
    // =====================

    /// Context slice identifier - where a claim is asserted to hold.
    [<Struct>]
    type ContextSlice = ContextSlice of string

    [<RequireQualifiedAccess>]
    module ContextSlice =
        let value (ContextSlice s) = s
        let create s = ContextSlice s

    /// Claim scope - set of context slices where claim holds.
    /// INV-ASR-01: scope ≠ ∅ (every claim must have at least one context)
    type ClaimScope =
        { slices: ContextSlice list }

        static member Empty = { slices = [] }

        static member Single(slice: ContextSlice) = { slices = [ slice ] }

        static member Of(slices: ContextSlice list) = { slices = slices }

        member this.IsEmpty = this.slices.IsEmpty

        member this.Contains(slice: ContextSlice) = this.slices |> List.contains slice

    // =====================
    // Evidence & Grounding
    // =====================

    /// Evidence path identifier - unique trace through evidence graph.
    [<Struct>]
    type PathId = PathId of string

    [<RequireQualifiedAccess>]
    module PathId =
        let value (PathId s) = s
        let create s = PathId s

    /// URI to grounding holon (physical system anchoring the claim).
    [<Struct>]
    type GroundingRef = GroundingRef of string

    [<RequireQualifiedAccess>]
    module GroundingRef =
        let value (GroundingRef s) = s
        let create s = GroundingRef s

    /// Reliability component of F-G-R triad.
    type Reliability =
        { level: AssuranceLevel
          pathId: PathId option // Reference to evidence graph
          decay: TimeSpan option // Freshness window
          timestamp: DateTimeOffset option } // When evidence was gathered

    [<RequireQualifiedAccess>]
    module Reliability =
        let unsubstantiated =
            { level = AssuranceLevel.L0
              pathId = None
              decay = None
              timestamp = None }

        let circumstantial pathId =
            { level = AssuranceLevel.L1
              pathId = Some pathId
              decay = None
              timestamp = Some DateTimeOffset.UtcNow }

        let evidenced pathId decay =
            { level = AssuranceLevel.L2
              pathId = Some pathId
              decay = Some decay
              timestamp = Some DateTimeOffset.UtcNow }

        /// Check if evidence is still fresh
        let isFresh (r: Reliability) =
            match r.decay, r.timestamp with
            | Some decay, Some ts -> DateTimeOffset.UtcNow - ts <= decay
            | None, _ -> true // No decay = always fresh
            | Some _, None -> false // Has decay but no timestamp = stale

        /// Apply CL penalty to reliability
        let applyPenalty (cl: CongruenceLevel) (r: Reliability) : float =
            let base' = float (AssuranceLevel.toInt r.level) / 2.0
            base' * CongruenceLevel.penalty cl

    // =====================
    // Assurance Envelope (F-G-R composite)
    // =====================

    /// Complete assurance metadata for a claim/message.
    /// Implements F-G-R triad from FPF B.3.
    type AssuranceEnvelope =
        { formality: Formality // F - How rigorous is the claim?
          scope: ClaimScope // G - Where does it apply?
          reliability: Reliability // R - How well supported?
          groundingRef: GroundingRef option } // Link to physical grounding

    [<RequireQualifiedAccess>]
    module AssuranceEnvelope =
        /// Default envelope for unassured claims (L0)
        let unassured =
            { formality = Formality.F0
              scope = ClaimScope.Empty
              reliability = Reliability.unsubstantiated
              groundingRef = None }

        /// Create envelope with specified assurance level
        let create formality scope reliability groundingRef =
            { formality = formality
              scope = scope
              reliability = reliability
              groundingRef = groundingRef }

        /// Check if envelope satisfies INV-ASR-01 (no universal claims)
        let hasScope (env: AssuranceEnvelope) = not env.scope.IsEmpty

        /// Check if envelope satisfies INV-ASR-05 (L2 requires grounding)
        let hasRequiredGrounding (env: AssuranceEnvelope) =
            match env.reliability.level with
            | AssuranceLevel.L2 -> env.groundingRef.IsSome
            | _ -> true

        /// Validate envelope invariants
        let validate (env: AssuranceEnvelope) : string list =
            [ if env.scope.IsEmpty then
                  yield "INV-ASR-01: Claim has no scope (unbounded)"
              if env.reliability.level = AssuranceLevel.L2 && env.groundingRef.IsNone then
                  yield "INV-ASR-05: L2 claim requires grounding reference"
              if not (Reliability.isFresh env.reliability) then
                  yield "INV-ASR-03: Evidence is stale" ]

    // =====================
    // Assurance Findings (Sentinel output)
    // =====================

    /// Assurance-specific validation findings.
    [<RequireQualifiedAccess>]
    type AssuranceFinding =
        | UnboundedScope of messageId: string
        | FormalityOverclaim of messageId: string * claimed: Formality * actual: Formality
        | UngroundedL2Claim of messageId: string
        | StaleEvidence of messageId: string * age: TimeSpan
        | ReliabilityInflation of messageId: string * claimed: AssuranceLevel * computed: AssuranceLevel
        | MissingEnvelope of messageId: string

    [<RequireQualifiedAccess>]
    module AssuranceFinding =
        let code =
            function
            | AssuranceFinding.UnboundedScope _ -> "ACP.ASSURANCE.UNBOUNDED_SCOPE"
            | AssuranceFinding.FormalityOverclaim _ -> "ACP.ASSURANCE.FORMALITY_OVERCLAIM"
            | AssuranceFinding.UngroundedL2Claim _ -> "ACP.ASSURANCE.UNGROUNDED_L2"
            | AssuranceFinding.StaleEvidence _ -> "ACP.ASSURANCE.STALE_EVIDENCE"
            | AssuranceFinding.ReliabilityInflation _ -> "ACP.ASSURANCE.RELIABILITY_INFLATION"
            | AssuranceFinding.MissingEnvelope _ -> "ACP.ASSURANCE.MISSING_ENVELOPE"

        let describe =
            function
            | AssuranceFinding.UnboundedScope mid -> sprintf "Message %s has no declared scope (INV-ASR-01)" mid
            | AssuranceFinding.FormalityOverclaim(mid, claimed, actual) ->
                sprintf
                    "Message %s claims F%d but content supports F%d (INV-ASR-04)"
                    mid
                    (Formality.toInt claimed)
                    (Formality.toInt actual)
            | AssuranceFinding.UngroundedL2Claim mid ->
                sprintf "Message %s claims L2 assurance without grounding (INV-ASR-05)" mid
            | AssuranceFinding.StaleEvidence(mid, age) ->
                sprintf "Message %s has stale evidence (age: %O) (INV-ASR-03)" mid age
            | AssuranceFinding.ReliabilityInflation(mid, claimed, computed) ->
                sprintf
                    "Message %s claims L%d but evidence only supports L%d (INV-ASR-02)"
                    mid
                    (AssuranceLevel.toInt claimed)
                    (AssuranceLevel.toInt computed)
            | AssuranceFinding.MissingEnvelope mid -> sprintf "Message %s has no assurance envelope (assumed L0)" mid

    // =====================
    // Trust Aggregation (Γ operators)
    // =====================

    /// Aggregate assurance levels using weakest-link rule (INV-ASR-02).
    let aggregateAssurance (levels: AssuranceLevel list) : AssuranceLevel =
        if levels.IsEmpty then
            AssuranceLevel.L0
        else
            levels |> List.reduce AssuranceLevel.min

    /// Aggregate reliability across evidence chain.
    let aggregateReliability (chain: Reliability list) : Reliability =
        if chain.IsEmpty then
            Reliability.unsubstantiated
        else
            let weakest = chain |> List.map (fun r -> r.level) |> aggregateAssurance

            { level = weakest
              pathId = chain |> List.tryPick (fun r -> r.pathId)
              decay = chain |> List.choose (fun r -> r.decay) |> List.tryHead
              timestamp = chain |> List.choose (fun r -> r.timestamp) |> List.tryHead }

    /// Apply congruence level penalty when crossing context bridge (INV-SEM-03).
    /// Reliability is degraded based on the CL penalty factor.
    let crossBridge (cl: CongruenceLevel) (envelope: AssuranceEnvelope) : AssuranceEnvelope =
        let penalizedLevel =
            match cl with
            | CongruenceLevel.CL0 -> AssuranceLevel.L0 // Complete degradation
            | CongruenceLevel.CL1 ->
                // 75% penalty - L2 becomes L1, L1 stays L1
                if envelope.reliability.level = AssuranceLevel.L2 then
                    AssuranceLevel.L1
                else
                    envelope.reliability.level
            | CongruenceLevel.CL2 ->
                // 50% penalty - L2 becomes L1
                if envelope.reliability.level = AssuranceLevel.L2 then
                    AssuranceLevel.L1
                else
                    envelope.reliability.level
            | CongruenceLevel.CL3 ->
                // 25% penalty - slight degradation for L2 only in critical contexts
                // For now, preserve level but mark for potential review
                envelope.reliability.level
            | CongruenceLevel.CL4 -> envelope.reliability.level // No penalty

        { envelope with
            reliability =
                { envelope.reliability with
                    level = penalizedLevel } }
