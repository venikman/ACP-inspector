# BC-001: Assurance Context

**FPF Pattern**: A.1.1 U.BoundedContext  
**Related DRR**: [DRR-001](../drr/DRR-001-agent-output-trustworthiness.md)  
**Status**: Draft  
**Version**: 0.1.0

## Context Identity

```yaml
contextId: "urn:acp-inspector:context:assurance:v1"
contextName: "ACP Assurance Context"
scope: "Trust reasoning over agent protocol messages"
owner: "ACP Inspector Project"
```

## Purpose

This bounded context defines the semantic frame for reasoning about **trust and assurance** in agent communications. It provides vocabulary and rules for:

- Classifying the reliability of agent outputs
- Structuring evidence that supports claims
- Computing trust across protocol boundaries

## Vocabulary (Local Glossary)

### Core Terms

| Term | Local Definition | FPF Mapping | Notes |
| ---- | ---------------- | ----------- | ----- |
| **Claim** | An assertion embedded in an agent message that purports to describe reality | U.Episteme (describedEntity populated) | Claims are not "true/false" but "supported/unsupported" |
| **Evidence** | A grounded artifact that supports or refutes a Claim | EvidenceRole (A.2.4) | Evidence must have provenance |
| **AssuranceLevel** | Ordinal measure of claim support: L0/L1/L2 | B.3.3 levels | L0=Unsubstantiated, L1=Circumstantial, L2=Evidenced |
| **Formality** | Degree of rigor in claim expression: F0..F9 | C.2.3 F-scale | F0=informal prose, F9=machine-verified proof |
| **ClaimScope** | The set of contexts where a claim is asserted to hold | A.2.6 USM | Set-valued, not universal |
| **Reliability** | Computed trust value based on evidence paths | C.2.2 R | Always weakest-link across evidence chain |
| **EvidencePath** | Directed graph from claim to grounding evidence | G.6 EvidenceGraph | PathId identifies a specific trace |
| **Decay** | Time-bounded validity window for evidence | B.3.4 | Evidence freshness degrades |
| **GroundingHolon** | Physical system that anchors epistemic claims | C.2.1 slot | Without grounding, claims float |

### Derived Terms

| Term | Definition | Derivation |
| ---- | ---------- | ---------- |
| **AssuranceEnvelope** | Protocol container for F-G-R metadata | Composite of Formality + ClaimScope + Reliability |
| **TrustScore** | Computed tuple ⟨F, G, R⟩ for a message | Aggregation of per-claim assurance |
| **EvidenceAnchor** | URI pointing to grounding artifact | Reference into EvidenceGraph |
| **FreshnessWindow** | ISO8601 duration for evidence validity | Decay + timestamp |

## Kind Signatures

### AssuranceLevel

```text
Kind: AssuranceLevel
Intension: Ordinal classification of epistemic support
Extension: { L0, L1, L2 }
SubkindOf: OrdinalCharacteristic

L0 := "Unsubstantiated" — no evidence path exists
L1 := "Circumstantial" — evidence exists but incomplete or indirect
L2 := "Evidenced" — complete evidence path to grounding
```

### Formality

```text
Kind: Formality
Intension: Degree of structural rigor in claim expression
Extension: { F0, F1, F2, F3, F4, F5, F6, F7, F8, F9 }
SubkindOf: OrdinalCharacteristic

F0 := Informal prose, no structure
F3 := Semi-structured (JSON schema, type hints)
F6 := Formally specified (contracts, invariants)
F9 := Machine-verified (proofs, model-checked)
```

### ClaimScope

```text
Kind: ClaimScope
Intension: Set of context slices where claim holds
Extension: PowerSet(ContextSlice)
SubkindOf: SetValuedScope

Invariant: scope ≠ ∅ (every claim has at least one context)
Invariant: scope is declared, not inferred
```

### EvidencePath

```text
Kind: EvidencePath
Intension: Directed acyclic graph from claim to grounding
Slots:
  - pathId: UUID (unique identifier)
  - source: Claim
  - target: GroundingHolon | Evidence
  - edges: List<EvidenceEdge>
  - weakestLink: AssuranceLevel (computed)
  
Invariant: acyclic(edges)
Invariant: weakestLink = min(edge.level for edge in edges)
```

## Invariants

### INV-ASR-01: No Universal Claims

```text
∀ claim ∈ Message:
  claim.scope ≠ UNIVERSAL
  
Rationale: Claims must declare bounded applicability (FPF A.1.1)
Violation: ValidationFinding.UnboundedScope
```

### INV-ASR-02: Weakest-Link Propagation

```text
∀ path ∈ EvidenceGraph:
  path.reliability = min(edge.reliability for edge in path.edges)
  
Rationale: Trust cannot exceed its weakest support (FPF B.1 WLNK)
Violation: ValidationFinding.ReliabilityInflation
```

### INV-ASR-03: Evidence Freshness

```text
∀ evidence ∈ EvidenceGraph:
  now() - evidence.timestamp ≤ evidence.freshnessWindow
  OR evidence.status = Stale
  
Rationale: Evidence decays (FPF B.3.4)
Violation: ValidationFinding.StaleEvidence
```

### INV-ASR-04: Formality Consistency

```text
∀ claim ∈ Message:
  claim.declaredFormality ≤ actualFormality(claim.content)
  
Rationale: Cannot claim higher formality than content supports
Violation: ValidationFinding.FormalityOverclaim
```

### INV-ASR-05: Grounding Requirement for L2

```text
∀ claim ∈ Message:
  claim.assuranceLevel = L2 ⟹ ∃ path: claim → GroundingHolon
  
Rationale: L2 requires physical grounding
Violation: ValidationFinding.UngroundedL2Claim
```

## Bridges to Other Contexts

### Bridge: Assurance ↔ Protocol (ACP Core)

```yaml
bridgeId: "bridge:assurance-protocol"
sourceContext: "urn:acp-inspector:context:assurance:v1"
targetContext: "urn:acp:protocol:v1"
congruenceLevel: CL3  # High alignment, some loss

mappings:
  - source: Claim
    target: MessageContent
    lossNotes: "Protocol has no native claim structure; claims extracted heuristically"
    
  - source: AssuranceEnvelope
    target: MessageMetadata (extension)
    lossNotes: "Optional field; absent = L0 assumed"
    
  - source: EvidencePath
    target: (no direct mapping)
    lossNotes: "Evidence graphs external to protocol; referenced by URI"
```

### Bridge: Assurance ↔ Semantic Alignment (BC-002)

```yaml
bridgeId: "bridge:assurance-semantic"
sourceContext: "urn:acp-inspector:context:assurance:v1"
targetContext: "urn:acp-inspector:context:semantic:v1"
congruenceLevel: CL4  # Very high alignment

mappings:
  - source: ClaimScope
    target: SemanticContext
    lossNotes: "Scope slices map to context boundaries"
    
  - source: Reliability (R)
    target: (degraded by CL penalty)
    lossNotes: "R_effective = R × CL_penalty when crossing contexts"
```

### Bridge: Assurance ↔ Capability Verification (BC-003)

```yaml
bridgeId: "bridge:assurance-capability"
sourceContext: "urn:acp-inspector:context:assurance:v1"
targetContext: "urn:acp-inspector:context:capability:v1"
congruenceLevel: CL4

mappings:
  - source: AssuranceLevel
    target: CapabilityVerificationLevel
    lossNotes: "Direct correspondence: L0→Declared, L1→Tested, L2→Certified"
    
  - source: EvidencePath
    target: VerificationEvidence
    lossNotes: "Capability test results are evidence artifacts"
```

## Sentinel Validation Rules

```fsharp
module AssuranceValidation =
    
    /// Validate assurance envelope completeness
    let validateEnvelope (msg: Message) : ValidationFinding list =
        match msg.assurance with
        | None -> [ UnboundedScope msg.id ]
        | Some env ->
            [ if env.scope.IsEmpty then yield UnboundedScope msg.id
              if env.formality > actualFormality msg.content then 
                  yield FormalityOverclaim (msg.id, env.formality)
              if env.reliability.level = L2 && env.reliability.pathId.IsNone then
                  yield UngroundedL2Claim msg.id ]
    
    /// Check evidence freshness
    let validateFreshness (evidence: Evidence) : ValidationFinding option =
        if DateTime.UtcNow - evidence.timestamp > evidence.freshnessWindow then
            Some (StaleEvidence evidence.id)
        else None
```

## Usage Examples

### Example 1: Agent Output with Assurance

```json
{
  "type": "agent_message",
  "content": "The file config.yaml contains 3 syntax errors.",
  "assurance": {
    "formality": "F3",
    "scope": ["urn:context:user-workspace:abc123"],
    "reliability": {
      "level": "L2",
      "pathId": "evidence:yaml-lint-run:def456",
      "decay": "PT1H"
    },
    "groundingRef": "file:///workspace/config.yaml"
  }
}
```

### Example 2: Unassured Output (L0 Default)

```json
{
  "type": "agent_message", 
  "content": "I think you should refactor that function."
}
// Sentinel interprets as: AssuranceLevel = L0, Scope = unbounded (warning)
```

## References

- FPF A.1.1: U.BoundedContext — The Semantic Frame
- FPF B.3: Trust & Assurance Calculus (F-G-R)
- FPF C.2: KD-CAL — Knowledge-specific aggregation
- FPF G.6: Evidence Graph & Provenance Ledger
- DRR-001: Agent Output Trustworthiness
