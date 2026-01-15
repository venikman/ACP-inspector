# BC-003: Capability Verification Context

**FPF Pattern**: A.1.1 U.BoundedContext  
**Related DRR**: [DRR-003](../drr/DRR-003-capability-claim-verification.md)  
**Status**: Draft  
**Version**: 0.1.0

## Context Identity

```yaml
contextId: "urn:acp-inspector:context:capability:v1"
contextName: "ACP Capability Verification Context"
scope: "Verification and assurance of agent capability claims"
owner: "ACP Inspector Project"
```

## Purpose

This bounded context defines the semantic frame for reasoning about **capability claims and their verification**. It provides vocabulary and rules for:

- Structuring capability claims with performance envelopes
- Verifying claims through evidence (tests, attestations)
- Tracking capability status across sessions
- Handling capability degradation and recovery

## Vocabulary (Local Glossary)

### Core Terms

| Term | Local Definition | FPF Mapping | Notes |
| ---- | ---------------- | ----------- | ----- |
| **Capability** | A claimed ability of an agent to perform a class of actions | U.Capability (A.2.2) | Dispositional property |
| **CapabilityClaim** | An assertion by an agent that it possesses a Capability | U.Episteme about capability | Subject to verification |
| **PerformanceEnvelope** | Conditions under which a capability is claimed to hold | A.2.2 measures | Bounds, not guarantees |
| **VerificationLevel** | Ordinal measure of claim support: Declared/Tested/Certified | Maps to L0/L1/L2 | Progressive assurance |
| **SelfTest** | In-protocol verification procedure for a capability | (local term) | Agent-provided |
| **TestEvidence** | Artifact recording verification attempt and result | EvidenceRole | Timestamped, reproducible |
| **CapabilityDegradation** | Reduction in capability due to failure or changed conditions | (local term) | Triggers re-verification |
| **AcceptanceCriteria** | Conditions that must hold for capability to be accepted | A.2.3 ServiceClause | Falsifiable |

### Derived Terms

| Term | Definition | Derivation |
| ---- | ---------- | ---------- |
| **EffectiveCapability** | Intersection of claimed and verified capabilities | min(claimed, verified) |
| **CapabilityGap** | Difference between claimed and verified | claimed - verified |
| **VerificationDebt** | Capabilities claimed but not yet verified | Set of unverified claims |
| **CapabilityFingerprint** | Hash of capability set for quick comparison | Composite |

## Kind Signatures

### Capability

```text
Kind: Capability
Intension: Dispositional property — ability to perform a class of actions
Slots:
  - capabilityKind: CapabilityKind (enumerated type)
  - abilitySpec: AbilitySpec (what can be done)
  - performanceEnvelope: PerformanceEnvelope? (under what conditions)
  - constraints: List<Constraint> (limitations)
  
SubkindOf: U.Capability (A.2.2)
```

### CapabilityKind

```text
Kind: CapabilityKind
Intension: Enumeration of capability categories in ACP
Extension: {
  FileRead, FileWrite,
  TerminalAccess,
  HttpRequest, SseConnection,
  AudioProcessing, ImageProcessing,
  SessionPersistence,
  PromptEmbeddedContext,
  ... (extensible)
}
SubkindOf: EnumeratedKind
```

### CapabilityClaim

```text
Kind: CapabilityClaim
Intension: Agent assertion of possessing a capability
Slots:
  - claimId: UUID
  - agentId: AgentIdentifier
  - capability: Capability
  - verificationLevel: VerificationLevel
  - evidence: List<TestEvidence>?
  - claimedAt: Timestamp
  - expiresAt: Timestamp?
  
Invariant: verificationLevel ≥ Declared
Invariant: verificationLevel = Certified ⟹ evidence.nonEmpty
```

### VerificationLevel

```text
Kind: VerificationLevel
Intension: Ordinal measure of capability claim support
Extension: { Declared, Tested, Certified }
SubkindOf: OrdinalCharacteristic

Declared := Agent asserts capability, no verification performed
Tested := Capability passed in-protocol self-test
Certified := External attestation or comprehensive test suite
```

### PerformanceEnvelope

```text
Kind: PerformanceEnvelope
Intension: Conditions bounding capability validity
Slots:
  - maxLatency: Duration? (response time bound)
  - maxPayloadSize: Bytes? (input/output size limit)
  - rateLimit: Rate? (calls per time unit)
  - resourceBudget: ResourceSpec? (compute, memory, etc.)
  - environmentConstraints: List<Constraint>
  
Invariant: at least one slot populated
```

### TestEvidence

```text
Kind: TestEvidence
Intension: Record of capability verification attempt
Slots:
  - evidenceId: UUID
  - testSpec: SelfTestSpec
  - result: Pass | Fail | Inconclusive
  - timestamp: Timestamp
  - duration: Duration
  - artifacts: List<URI> (logs, outputs)
  - reproducible: Boolean
  
Invariant: timestamp ≤ now()
```

### SelfTestSpec

```text
Kind: SelfTestSpec
Intension: Specification for in-protocol capability verification
Slots:
  - testId: String
  - capabilityKind: CapabilityKind
  - inputSpec: Schema (test input format)
  - expectedOutputSpec: Schema (expected result format)
  - acceptanceCriteria: List<Criterion>
  - timeout: Duration
  
Invariant: acceptanceCriteria.nonEmpty (must be falsifiable)
```

## Invariants

### INV-CAP-01: Verification Level Consistency

```text
∀ claim ∈ CapabilityClaim:
  claim.verificationLevel = Tested ⟹ 
    ∃ e ∈ claim.evidence: e.result = Pass
  
  claim.verificationLevel = Certified ⟹
    ∃ e ∈ claim.evidence: e.result = Pass ∧ hasExternalAttestation(e)
    
Rationale: Verification level must match evidence (FPF B.3.3)
Violation: ValidationFinding.VerificationLevelMismatch
```

### INV-CAP-02: Effective Capability Bound

```text
∀ agent, capabilityKind:
  effectiveCapability(agent, capabilityKind) = 
    min(agent.claimed[capabilityKind].verificationLevel,
        agent.verified[capabilityKind].verificationLevel)
        
Rationale: Cannot rely on unverified claims
```

### INV-CAP-03: Degradation Triggers Re-verification

```text
∀ capability C of agent A:
  failureObserved(A, C) ⟹ 
    C.verificationLevel := Declared
    AND scheduleReverification(A, C)
    
Rationale: Failed capabilities lose trust
Violation: ValidationFinding.StaleVerification
```

### INV-CAP-04: Envelope Enforcement

```text
∀ operation O invoking capability C:
  O.parameters ⊆ C.performanceEnvelope
  OR ValidationFinding.EnvelopeViolation
  
Rationale: Operations must stay within claimed bounds
```

### INV-CAP-05: Self-Test Falsifiability

```text
∀ selfTest ∈ SelfTestSpec:
  selfTest.acceptanceCriteria.nonEmpty
  AND ∃ input: selfTest.acceptanceCriteria(input) = Fail
  
Rationale: Tests must be able to fail (Deutsch: falsifiability)
Violation: ValidationFinding.UnfalsifiableTest
```

## Bridges to Other Contexts

### Bridge: Capability ↔ Assurance (BC-001)

```yaml
bridgeId: "bridge:capability-assurance"
sourceContext: "urn:acp-inspector:context:capability:v1"
targetContext: "urn:acp-inspector:context:assurance:v1"
congruenceLevel: CL4

mappings:
  - source: VerificationLevel.Declared
    target: AssuranceLevel.L0
    
  - source: VerificationLevel.Tested
    target: AssuranceLevel.L1
    
  - source: VerificationLevel.Certified
    target: AssuranceLevel.L2
    
  - source: TestEvidence
    target: Evidence
    lossNotes: "Direct correspondence"
```

### Bridge: Capability ↔ Protocol (ACP Core)

```yaml
bridgeId: "bridge:capability-protocol"
sourceContext: "urn:acp-inspector:context:capability:v1"
targetContext: "urn:acp:protocol:v1"
congruenceLevel: CL2

mappings:
  - source: CapabilityClaim
    target: agentCapabilities / clientCapabilities
    lossNotes: "Protocol uses flat boolean flags; no performance envelope"
    
  - source: VerificationLevel
    target: (no mapping)
    lossNotes: "Protocol assumes Declared level"
    
  - source: SelfTestSpec
    target: (extension required)
    lossNotes: "Must add VerifyCapability message type"
```

### Bridge: Capability ↔ Semantic (BC-002)

```yaml
bridgeId: "bridge:capability-semantic"
sourceContext: "urn:acp-inspector:context:capability:v1"
targetContext: "urn:acp-inspector:context:semantic:v1"
congruenceLevel: CL3

mappings:
  - source: CapabilityKind
    target: KindSignature (for capability terms)
    lossNotes: "Capability kinds are specialized semantic kinds"
    
  - source: AbilitySpec
    target: (requires semantic interpretation)
    lossNotes: "Ability meanings depend on agent context"
```

## Protocol Extensions

### Structured Capability Declaration

```typescript
interface StructuredCapabilities {
  claims: CapabilityClaim[];
}

interface CapabilityClaim {
  capabilityKind: CapabilityKind;
  ability: AbilitySpec;
  performanceEnvelope?: PerformanceEnvelope;
  verificationLevel: "declared" | "tested" | "certified";
  evidence?: EvidenceRef[];
}

interface PerformanceEnvelope {
  maxLatencyMs?: number;
  maxPayloadBytes?: number;
  rateLimitPerMinute?: number;
  constraints?: string[];
}
```

### Capability Verification Protocol

```typescript
// Client requests verification
interface VerifyCapabilityRequest {
  type: "verify_capability";
  capabilityKind: CapabilityKind;
  testParams?: Record<string, unknown>;
}

// Agent provides self-test
interface VerifyCapabilityResponse {
  type: "verify_capability_result";
  capabilityKind: CapabilityKind;
  result: "pass" | "fail" | "inconclusive";
  evidence: TestEvidence;
  newVerificationLevel: "declared" | "tested" | "certified";
}

interface TestEvidence {
  testId: string;
  timestamp: string;  // ISO8601
  durationMs: number;
  artifacts?: string[];  // URIs
}
```

## Sentinel Validation Rules

```fsharp
module CapabilityValidation =
    
    /// Validate verification level matches evidence
    let validateVerificationLevel (claim: CapabilityClaim) : ValidationFinding list =
        match claim.verificationLevel, claim.evidence with
        | Declared, _ -> []  // Always valid
        | Tested, None -> [ VerificationLevelMismatch (claim.claimId, "Tested requires evidence") ]
        | Tested, Some evidence when evidence |> List.exists (fun e -> e.result = Pass) -> []
        | Tested, _ -> [ VerificationLevelMismatch (claim.claimId, "No passing test") ]
        | Certified, Some evidence when 
            evidence |> List.exists (fun e -> e.result = Pass) -> []  // In full impl, check for external attestation
        | Certified, _ -> [ VerificationLevelMismatch (claim.claimId, "Certified requires passing evidence") ]
    
    /// Check operation against performance envelope
    let validateEnvelope 
        (operation: Operation) 
        (envelope: PerformanceEnvelope option) 
        : ValidationFinding option =
        
        match envelope with
        | None -> None  // No envelope = no constraints
        | Some env ->
            let violations = [
                match env.maxPayloadBytes, operation.payloadSize with
                | Some max, Some actual when actual > max -> yield "Payload exceeds limit"
                | _ -> ()
                match env.rateLimitPerMinute, operation.rate with
                | Some max, Some actual when actual > max -> yield "Rate limit exceeded"
                | _ -> ()
            ]
            if violations.IsEmpty then None
            else Some (EnvelopeViolation (operation.id, violations))
    
    /// Compute effective capability set
    let effectiveCapabilities 
        (claimed: CapabilityClaim list) 
        (verified: CapabilityClaim list) 
        : CapabilityClaim list =
        
        claimed |> List.map (fun c ->
            let verifiedLevel = 
                verified 
                |> List.tryFind (fun v -> v.capabilityKind = c.capabilityKind)
                |> Option.map (fun v -> v.verificationLevel)
                |> Option.defaultValue Declared
            { c with verificationLevel = min c.verificationLevel verifiedLevel })
    
    /// Track verification debt
    let verificationDebt (claimed: CapabilityClaim list) : CapabilityClaim list =
        claimed |> List.filter (fun c -> c.verificationLevel = Declared)
```

## Usage Examples

### Example 1: Agent with Structured Capabilities

```json
{
  "type": "initialize_result",
  "result": {
    "protocolVersion": 1,
    "agentCapabilities": {
      "claims": [
        {
          "capabilityKind": "FileRead",
          "ability": { "description": "Read text files from workspace" },
          "performanceEnvelope": {
            "maxPayloadBytes": 10485760,
            "constraints": ["UTF-8 encoding only", "workspace-scoped paths"]
          },
          "verificationLevel": "tested",
          "evidence": [{
            "testId": "file-read-smoke",
            "timestamp": "2026-01-06T01:30:00Z",
            "result": "pass"
          }]
        },
        {
          "capabilityKind": "HttpRequest",
          "ability": { "description": "Make outbound HTTP requests" },
          "performanceEnvelope": {
            "rateLimitPerMinute": 60,
            "maxLatencyMs": 30000
          },
          "verificationLevel": "declared"
        }
      ]
    }
  }
}
```

### Example 2: Capability Verification Request

```json
{
  "type": "verify_capability",
  "params": {
    "capabilityKind": "FileRead",
    "testParams": {
      "testFile": "__test__/sample.txt",
      "expectedContent": "Hello, World!"
    }
  }
}

// Response
{
  "type": "verify_capability_result",
  "result": {
    "capabilityKind": "FileRead",
    "result": "pass",
    "evidence": {
      "testId": "file-read-verification-001",
      "timestamp": "2026-01-06T01:35:00Z",
      "durationMs": 42,
      "artifacts": ["log:///verification/file-read-001.log"]
    },
    "newVerificationLevel": "tested"
  }
}
```

### Example 3: Capability Degradation Event

```json
{
  "type": "capability_degradation",
  "params": {
    "capabilityKind": "HttpRequest",
    "previousLevel": "tested",
    "newLevel": "declared",
    "reason": "Connection timeout observed",
    "timestamp": "2026-01-06T01:40:00Z",
    "recommendedAction": "Re-verify when network stable"
  }
}
```

## References

- FPF A.2.2: U.Capability — System Ability
- FPF A.2.3: U.ServiceClause — The Service Promise
- FPF B.3.3: Assurance Subtypes & Levels
- FPF F.12: Service Acceptance Binding
- Deutsch, D. (2011). The Beginning of Infinity, Ch. 1 (Falsifiability)
- DRR-003: Capability Claim Verification
