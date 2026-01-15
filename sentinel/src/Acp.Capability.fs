namespace Acp

open System

/// BC-003: Capability Verification Context
/// Structured capability claims with verification levels and performance envelopes.
/// FPF Grounding: A.2.2, A.2.3, B.3.3
module Capability =

    open Assurance

    // =====================
    // Core Types (from BC-003 Kind Signatures)
    // =====================

    /// Capability kind enumeration - categories of agent capabilities.
    [<RequireQualifiedAccess>]
    type CapabilityKind =
        | FileRead
        | FileWrite
        | TerminalAccess
        | HttpRequest
        | SseConnection
        | AudioProcessing
        | ImageProcessing
        | SessionPersistence
        | PromptEmbeddedContext
        | McpHttp
        | McpSse
        | Custom of string

    [<RequireQualifiedAccess>]
    module CapabilityKind =
        let toString =
            function
            | CapabilityKind.FileRead -> "FileRead"
            | CapabilityKind.FileWrite -> "FileWrite"
            | CapabilityKind.TerminalAccess -> "TerminalAccess"
            | CapabilityKind.HttpRequest -> "HttpRequest"
            | CapabilityKind.SseConnection -> "SseConnection"
            | CapabilityKind.AudioProcessing -> "AudioProcessing"
            | CapabilityKind.ImageProcessing -> "ImageProcessing"
            | CapabilityKind.SessionPersistence -> "SessionPersistence"
            | CapabilityKind.PromptEmbeddedContext -> "PromptEmbeddedContext"
            | CapabilityKind.McpHttp -> "McpHttp"
            | CapabilityKind.McpSse -> "McpSse"
            | CapabilityKind.Custom s -> sprintf "Custom(%s)" s

        let tryParse (s: string) =
            match s.ToLowerInvariant() with
            | "fileread" -> Some CapabilityKind.FileRead
            | "filewrite" -> Some CapabilityKind.FileWrite
            | "terminalaccess" -> Some CapabilityKind.TerminalAccess
            | "httprequest" -> Some CapabilityKind.HttpRequest
            | "sseconnection" -> Some CapabilityKind.SseConnection
            | "audioprocessing" -> Some CapabilityKind.AudioProcessing
            | "imageprocessing" -> Some CapabilityKind.ImageProcessing
            | "sessionpersistence" -> Some CapabilityKind.SessionPersistence
            | "promptembeddedcontext" -> Some CapabilityKind.PromptEmbeddedContext
            | "mcphttp" -> Some CapabilityKind.McpHttp
            | "mcpsse" -> Some CapabilityKind.McpSse
            | _ when s.StartsWith("custom:", StringComparison.OrdinalIgnoreCase) ->
                Some(CapabilityKind.Custom(s.Substring(7)))
            | _ -> None

    /// Verification level - ordinal measure of capability claim support.
    /// Maps to AssuranceLevel: Declared=L0, Tested=L1, Certified=L2
    [<RequireQualifiedAccess>]
    type VerificationLevel =
        | Declared // Agent asserts capability, no verification performed
        | Tested // Capability passed in-protocol self-test
        | Certified // External attestation or comprehensive test suite

    [<RequireQualifiedAccess>]
    module VerificationLevel =
        let toAssuranceLevel =
            function
            | VerificationLevel.Declared -> AssuranceLevel.L0
            | VerificationLevel.Tested -> AssuranceLevel.L1
            | VerificationLevel.Certified -> AssuranceLevel.L2

        let fromAssuranceLevel =
            function
            | AssuranceLevel.L0 -> VerificationLevel.Declared
            | AssuranceLevel.L1 -> VerificationLevel.Tested
            | AssuranceLevel.L2 -> VerificationLevel.Certified

        let toInt =
            function
            | VerificationLevel.Declared -> 0
            | VerificationLevel.Tested -> 1
            | VerificationLevel.Certified -> 2

        let min a b = if toInt a <= toInt b then a else b

        let tryParse (s: string) =
            match s.ToLowerInvariant() with
            | "declared" -> Some VerificationLevel.Declared
            | "tested" -> Some VerificationLevel.Tested
            | "certified" -> Some VerificationLevel.Certified
            | _ -> None

    // =====================
    // Performance Envelope
    // =====================

    /// Rate specification (calls per time unit)
    type Rate = { count: int; perInterval: TimeSpan }

    [<RequireQualifiedAccess>]
    module Rate =
        let perMinute count =
            { count = count
              perInterval = TimeSpan.FromMinutes(1.0) }

        let perSecond count =
            { count = count
              perInterval = TimeSpan.FromSeconds(1.0) }

        let perHour count =
            { count = count
              perInterval = TimeSpan.FromHours(1.0) }

        let callsPerMinute (r: Rate) =
            float r.count * (60.0 / r.perInterval.TotalSeconds)

    /// Performance envelope - conditions bounding capability validity.
    type PerformanceEnvelope =
        { maxLatency: TimeSpan option // Response time bound
          maxPayloadBytes: int64 option // Input/output size limit
          rateLimit: Rate option // Calls per time unit
          resourceBudget: Map<string, float> option // Compute, memory, etc.
          constraints: string list } // Additional text constraints

    [<RequireQualifiedAccess>]
    module PerformanceEnvelope =
        let empty =
            { maxLatency = None
              maxPayloadBytes = None
              rateLimit = None
              resourceBudget = None
              constraints = [] }

        let withLatency latency env = { env with maxLatency = Some latency }

        let withPayloadLimit bytes env =
            { env with
                maxPayloadBytes = Some bytes }

        let withRateLimit rate env = { env with rateLimit = Some rate }

        let withConstraint constraint' env =
            { env with
                constraints = env.constraints @ [ constraint' ] }

        /// Check if envelope has any bounds defined
        let hasBounds (env: PerformanceEnvelope) =
            env.maxLatency.IsSome
            || env.maxPayloadBytes.IsSome
            || env.rateLimit.IsSome
            || env.resourceBudget.IsSome
            || not env.constraints.IsEmpty

    // =====================
    // Test Evidence
    // =====================

    /// Test result enumeration
    [<RequireQualifiedAccess>]
    type TestResult =
        | Pass
        | Fail
        | Inconclusive

    /// Test evidence - record of capability verification attempt.
    type TestEvidence =
        { evidenceId: string
          testId: string
          result: TestResult
          timestamp: DateTimeOffset
          duration: TimeSpan
          artifacts: string list // URIs to logs, outputs
          reproducible: bool }

    [<RequireQualifiedAccess>]
    module TestEvidence =
        let create testId result duration =
            { evidenceId = Guid.NewGuid().ToString()
              testId = testId
              result = result
              timestamp = DateTimeOffset.UtcNow
              duration = duration
              artifacts = []
              reproducible = true }

        let withArtifacts artifacts evidence = { evidence with artifacts = artifacts }

        let isPassing (e: TestEvidence) = e.result = TestResult.Pass

    // =====================
    // Capability Claim
    // =====================

    /// Ability specification - what can be done
    type AbilitySpec =
        { description: string
          inputSchema: string option // JSON Schema for inputs
          outputSchema: string option } // JSON Schema for outputs

    [<RequireQualifiedAccess>]
    module AbilitySpec =
        let simple description =
            { description = description
              inputSchema = None
              outputSchema = None }

    /// Capability claim - agent assertion of possessing a capability.
    type CapabilityClaim =
        { claimId: string
          capabilityKind: CapabilityKind
          ability: AbilitySpec
          performanceEnvelope: PerformanceEnvelope option
          verificationLevel: VerificationLevel
          evidence: TestEvidence list
          claimedAt: DateTimeOffset
          expiresAt: DateTimeOffset option }

    [<RequireQualifiedAccess>]
    module CapabilityClaim =
        let create kind ability =
            { claimId = Guid.NewGuid().ToString()
              capabilityKind = kind
              ability = ability
              performanceEnvelope = None
              verificationLevel = VerificationLevel.Declared
              evidence = []
              claimedAt = DateTimeOffset.UtcNow
              expiresAt = None }

        let withEnvelope envelope claim =
            { claim with
                performanceEnvelope = Some envelope }

        let withVerification level evidence claim =
            { claim with
                verificationLevel = level
                evidence = claim.evidence @ evidence }

        let upgrade level evidence claim =
            if VerificationLevel.toInt level > VerificationLevel.toInt claim.verificationLevel then
                { claim with
                    verificationLevel = level
                    evidence = claim.evidence @ evidence }
            else
                claim

        let degrade reason claim =
            { claim with
                verificationLevel = VerificationLevel.Declared
                evidence = claim.evidence } // Keep evidence but reset level

        /// Convert to assurance envelope
        let toAssuranceEnvelope (claim: CapabilityClaim) : AssuranceEnvelope =
            let pathId =
                claim.evidence
                |> List.tryHead
                |> Option.map (fun e -> PathId.create e.evidenceId)

            let reliability =
                match claim.verificationLevel, pathId with
                | VerificationLevel.Declared, _ -> Reliability.unsubstantiated
                | VerificationLevel.Tested, Some pid -> Reliability.circumstantial pid
                | VerificationLevel.Tested, None -> Reliability.unsubstantiated
                | VerificationLevel.Certified, Some pid -> Reliability.evidenced pid (TimeSpan.FromDays(30.0))
                | VerificationLevel.Certified, None -> Reliability.unsubstantiated

            { formality = Formality.F3 // Claims are semi-structured
              scope = ClaimScope.Single(ContextSlice.create "agent-capabilities")
              reliability = reliability
              groundingRef =
                claim.evidence
                |> List.tryHead
                |> Option.bind (fun e -> e.artifacts |> List.tryHead)
                |> Option.map GroundingRef.create }

    // =====================
    // Verification & Validation
    // =====================

    /// INV-CAP-01: Verification level must match evidence
    let validateVerificationLevel (claim: CapabilityClaim) : string list =
        match claim.verificationLevel, claim.evidence with
        | VerificationLevel.Declared, _ -> []
        | VerificationLevel.Tested, evidence when evidence |> List.exists TestEvidence.isPassing -> []
        | VerificationLevel.Tested, _ -> [ "INV-CAP-01: Tested requires passing evidence" ]
        | VerificationLevel.Certified, evidence when evidence |> List.exists (fun e -> e.result = TestResult.Pass) -> [] // Simplified: in full impl, check for external attestation
        | VerificationLevel.Certified, _ -> [ "INV-CAP-01: Certified requires passing evidence" ]

    /// INV-CAP-02: Effective capability is min(claimed, verified)
    let effectiveLevel (claimed: CapabilityClaim) (verified: CapabilityClaim option) : VerificationLevel =
        match verified with
        | None -> VerificationLevel.Declared // Unverified = Declared
        | Some v -> VerificationLevel.min claimed.verificationLevel v.verificationLevel

    /// INV-CAP-04: Check operation parameters against envelope
    type EnvelopeViolation =
        | PayloadTooLarge of actual: int64 * max: int64
        | RateLimitExceeded of rate: float * max: float
        | LatencyExceeded of actual: TimeSpan * max: TimeSpan

    let checkEnvelope
        (envelope: PerformanceEnvelope)
        (payloadSize: int64 option)
        (latency: TimeSpan option)
        (currentRate: float option)
        : EnvelopeViolation list =
        [ match envelope.maxPayloadBytes, payloadSize with
          | Some max, Some actual when actual > max -> yield PayloadTooLarge(actual, max)
          | _ -> ()

          match envelope.maxLatency, latency with
          | Some max, Some actual when actual > max -> yield LatencyExceeded(actual, max)
          | _ -> ()

          match envelope.rateLimit, currentRate with
          | Some limit, Some rate ->
              let maxRate = Rate.callsPerMinute limit

              if rate > maxRate then
                  yield RateLimitExceeded(rate, maxRate)
          | _ -> () ]

    // =====================
    // Capability Findings (Inspector output)
    // =====================

    /// Capability-specific validation findings.
    [<RequireQualifiedAccess>]
    type CapabilityFinding =
        | VerificationLevelMismatch of claimId: string * reason: string
        | EnvelopeViolation of claimId: string * violations: EnvelopeViolation list
        | CapabilityDegraded of kind: CapabilityKind * reason: string
        | UnfalsifiableTest of testId: string
        | StaleVerification of claimId: string * age: TimeSpan

    [<RequireQualifiedAccess>]
    module CapabilityFinding =
        let code =
            function
            | CapabilityFinding.VerificationLevelMismatch _ -> "ACP.CAPABILITY.VERIFICATION_MISMATCH"
            | CapabilityFinding.EnvelopeViolation _ -> "ACP.CAPABILITY.ENVELOPE_VIOLATION"
            | CapabilityFinding.CapabilityDegraded _ -> "ACP.CAPABILITY.DEGRADED"
            | CapabilityFinding.UnfalsifiableTest _ -> "ACP.CAPABILITY.UNFALSIFIABLE_TEST"
            | CapabilityFinding.StaleVerification _ -> "ACP.CAPABILITY.STALE_VERIFICATION"

        let describe =
            function
            | CapabilityFinding.VerificationLevelMismatch(cid, reason) -> sprintf "Claim %s: %s (INV-CAP-01)" cid reason
            | CapabilityFinding.EnvelopeViolation(cid, violations) ->
                sprintf "Claim %s: %d envelope violation(s) (INV-CAP-04)" cid (List.length violations)
            | CapabilityFinding.CapabilityDegraded(kind, reason) ->
                sprintf "Capability %s degraded: %s (INV-CAP-03)" (CapabilityKind.toString kind) reason
            | CapabilityFinding.UnfalsifiableTest tid -> sprintf "Test %s cannot fail (INV-CAP-05)" tid
            | CapabilityFinding.StaleVerification(cid, age) ->
                sprintf "Claim %s verification is stale (age: %O)" cid age

    // =====================
    // Capability Set Operations
    // =====================

    /// Set of capability claims for an agent
    type CapabilitySet =
        { claims: Map<CapabilityKind, CapabilityClaim> }

    [<RequireQualifiedAccess>]
    module CapabilitySet =
        let empty = { claims = Map.empty }

        let add claim set =
            { claims = set.claims |> Map.add claim.capabilityKind claim }

        let tryGet kind set = set.claims |> Map.tryFind kind

        let has kind set = set.claims |> Map.containsKey kind

        let remove kind set =
            { claims = set.claims |> Map.remove kind }

        let toList set =
            set.claims |> Map.toList |> List.map snd

        /// Get verification debt (capabilities claimed but not verified)
        let verificationDebt set =
            set.claims
            |> Map.filter (fun _ c -> c.verificationLevel = VerificationLevel.Declared)
            |> Map.toList
            |> List.map snd

        /// Compute effective capabilities (intersection of claimed and verified)
        let effective (claimed: CapabilitySet) (verified: CapabilitySet) : CapabilitySet =
            let effectiveClaims =
                claimed.claims
                |> Map.map (fun kind claim ->
                    let verifiedLevel =
                        verified.claims
                        |> Map.tryFind kind
                        |> Option.map (fun v -> v.verificationLevel)
                        |> Option.defaultValue VerificationLevel.Declared

                    { claim with
                        verificationLevel = VerificationLevel.min claim.verificationLevel verifiedLevel })

            { claims = effectiveClaims }

        /// Validate all claims in set
        let validate set : (CapabilityKind * string list) list =
            set.claims
            |> Map.toList
            |> List.map (fun (kind, claim) -> kind, validateVerificationLevel claim)
            |> List.filter (fun (_, errors) -> not errors.IsEmpty)
