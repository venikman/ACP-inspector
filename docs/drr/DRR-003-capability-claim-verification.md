# DRR-003: Capability Claim Verification

**Status**: Proposed  
**Date**: 2026-01-06  
**Authors**: Human + Warp Agent  
**FPF Grounding**: A.2.2 (U.Capability), B.3.3 (Assurance Subtypes), A.2.3 (U.ServiceClause)

## Context

ACP agents declare capabilities during initialization (e.g., `loadSession`, `mcpCapabilities.http`, `promptCapabilities.audio`). Clients use these declarations to decide what operations to attempt. However, declarations are *claims*, not *proofs*.

The current state:
- Capability declarations are boolean flags or simple enums
- No evidence that agent actually possesses declared capabilities
- No mechanism to test capabilities before relying on them
- Capability claims are taken at face value

## Problem Statement

**Deutsch Framing**: "Agent X has capability Y" is currently an *inexplicable rule*. The agent says so, and we believe it. This explanation is *parochial*—it works only because we trust the agent vendor. It has no *reach* to adversarial or unreliable agents.

**FPF Diagnosis**: Capability claims lack the three assurance lanes (B.3.3):
- **TA (Type Assurance)**: Is the capability claim well-formed?
- **VA (Verification Assurance)**: Does the agent's implementation match the claim?
- **LA (Logical Assurance)**: Is the capability logically consistent with other claims?

The U.Capability (A.2.2) pattern requires:
- Ability specification (what can be done)
- Performance envelope (under what conditions)
- Measures (how to verify)

Current ACP capabilities have only the first, partially.

## Forces

- **Trust models vary**: Internal agents may need less verification than external
- **Verification cost**: Testing every capability is expensive
- **Dynamic capabilities**: Agent capabilities may change during session
- **Capability granularity**: "Can read files" vs "can read files < 1MB in UTF-8"
- **Graceful degradation**: What happens when a claimed capability fails?

## Decision Drivers

1. Claims without evidence are *L0 (Unsubstantiated)* by FPF rules
2. Verification should be *proportional* to risk
3. Capability structure should support *progressive refinement*
4. Failed capability claims should trigger *error correction*, not silent failure

## Proposed Direction

Introduce **Structured Capability Claims** with **Verification Lanes**:

### 1. Capability Claim Structure

```
CapabilityClaim := {
  claimId: UUID                        // Unique identifier
  capabilityKind: CapabilityKind       // What type of capability
  ability: AbilitySpec                 // What can be done
  performanceEnvelope?: {              // Under what conditions
    maxLatency?: Duration
    maxPayloadSize?: Bytes
    rateLimit?: Rate
    constraints?: Constraint[]
  }
  verificationLevel: Declared | Tested | Certified  // Maps to L0/L1/L2
  evidence?: TestEvidence[]            // Verification test results
  claimedAt: Timestamp
  expiresAt?: Timestamp
}
```

### 2. Verification Protocol Extension

```
// Client can request capability verification
ClientToAgent: VerifyCapability {
  capabilityKind: CapabilityKind
  testParams?: TestParams
}

AgentToClient: VerifyCapabilityResult {
  capabilityKind: CapabilityKind
  verified: boolean
  evidence: EvidenceRecord
  newAssuranceLevel?: L0 | L1 | L2
}
```

### 3. Sentinel Enforcement

- Track claimed vs verified capabilities per session
- Flag operations that rely on unverified L0 capabilities
- Compute effective assurance level: `min(claimed, verified)`
- Record capability failures as negative evidence

## Consequences

**Positive**:
- Transforms capability claims into testable hypotheses
- Enables risk-proportional verification strategies
- Supports capability degradation and recovery
- Creates audit trail for capability-related failures

**Negative**:
- Protocol complexity increase
- Verification adds latency to session establishment
- Agents must implement self-test endpoints
- Risk of verification theater (tests that don't test real capabilities)

## Rationale

**Deutsch**: A good explanation makes *risky predictions*. By requiring capabilities to be verifiable, we transform "I can do X" from an uncheckable assertion into a falsifiable claim. The capability either passes verification or it doesn't—no room for easy variation.

**FPF**: This implements U.Capability (A.2.2) properly:
- Ability (what) + Performance envelope (how well) + Measures (evidence)
- Claims progress through TA → VA → LA lanes (B.3.3)
- ServiceClause (A.2.3) binds capability to acceptance criteria

## Alternatives Considered

1. **Trust all claims**: Current state. Parochial, no reach.
2. **External certification only**: Requires trusted third parties. Centralization risk.
3. **Runtime-only verification**: Discover failures in production. Expensive.
4. **Capability contracts**: Legal agreements. Doesn't help at runtime.

## Open Questions

- How to verify capabilities that are expensive to test (e.g., "can process 1M tokens")?
- Should capability verification be mandatory or opt-in?
- How to handle capabilities that degrade over time (model drift)?
- What's the right SelfTestSpec format?

## Dependencies

- **Builds on**: DRR-001 (Assurance Envelope for evidence structure)
- **Enables**: Trust-based routing (send tasks to verified-capable agents)
- **Related**: DRR-002 (Capability semantics need context alignment)
- **FPF Patterns**: A.2.2, A.2.3, B.3.3, F.12

## References

- Deutsch, D. (2011). *The Beginning of Infinity*, Ch. 1 (Reach of Explanations)
- FPF Spec: A.2.2 U.Capability
- FPF Spec: A.2.3 U.ServiceClause
- FPF Spec: B.3.3 Assurance Subtypes & Levels
- ACP Spec: Capabilities Schema
