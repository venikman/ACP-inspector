# DRR-004: Protocol Evolution Stability

**Status**: Proposed  
**Date**: 2026-01-06  
**Authors**: Human + Warp Agent  
**FPF Grounding**: A.4 (Temporal Duality), B.4 (Canonical Evolution Loop), E.9 (DRR Method)

## Context

ACP is a living protocol. The spec evolves (currently 0.10.x), implementations must track changes, and agents/clients negotiate protocol versions. However, evolution introduces risk: breaking changes, semantic drift, and version fragmentation.

The current state:
- Protocol version is a single integer negotiated at initialization
- No formal model of what constitutes a breaking change
- Semantic drift can occur without version bump
- Implementations may diverge from spec between releases
- No structured rationale for protocol changes

## Problem Statement

**Deutsch Framing**: The current error-correction mechanism for protocol evolution is *broken*. Errors (breaking changes, semantic drift) can accumulate without detection because there's no systematic way to:
1. Detect that a change is breaking
2. Trace why a change was made
3. Verify implementations match evolved spec
4. Roll back problematic changes

This is a *gap in error correction*—the evolutionary process lacks the feedback loops that good science requires.

**FPF Diagnosis**: Missing temporal architecture (A.4, B.4):
- No **design-time / run-time split**: spec changes and implementation changes conflated
- No **Canonical Evolution Loop**: changes aren't Run-Observe-Refine-Deploy disciplined
- No **DRR discipline**: rationale for changes isn't recorded or queryable
- No **Γ_time laws**: temporal aggregation rules are undefined

The protocol evolves, but not *architecturally*—it drifts.

## Forces

- **Innovation pressure**: New agent capabilities require protocol extensions
- **Backward compatibility**: Breaking existing implementations is costly
- **Spec authority**: Who decides what's normative?
- **Implementation diversity**: Multiple SDKs in multiple languages
- **Versioning complexity**: Semantic versioning may not capture semantic changes

## Decision Drivers

1. Evolution is *inevitable*—design for it, don't fight it (FPF P-10)
2. Changes must be *auditable*—every protocol delta needs rationale
3. Breaking changes must be *detectable*—not discovered in production
4. Implementations should *self-verify* against spec editions

## Proposed Direction

Introduce **Structured Protocol Evolution** with **Edition Tracking**:

### 1. Edition Model (beyond simple version)

```
ProtocolEdition := {
  version: SemVer                     // 0.10.3
  editionId: UUID                     // Immutable edition identifier
  parentEdition?: UUID                // What this evolved from
  changeKind: Patch | Minor | Major   // Semantic versioning class
  drrRefs: DRR-ID[]                   // Rationale records
  schemaHash: SHA256                  // Content-addressable schema
  compatibilityMatrix: {
    backwardCompatibleWith: UUID[]
    forwardCompatibleWith: UUID[]
    breakingFrom: UUID[]
  }
}
```

### 2. Change Classification Rules

```
BreakingChange := 
  | RequiredFieldAdded
  | FieldRemoved
  | TypeNarrowed
  | EnumValueRemoved
  | SemanticChange { field: Path, oldMeaning: URI, newMeaning: URI }

NonBreakingChange :=
  | OptionalFieldAdded
  | TypeWidened
  | EnumValueAdded
  | DeprecationMarked
```

### 3. Implementation Conformance Protocol

```
// Agent/Client can declare what edition they implement
Initialize.editionClaim: {
  editionId: UUID
  conformanceLevel: Declared | Tested | Certified
  testResultsRef?: URI
}

// Sentinel validates conformance
Sentinel.validateConformance(
  claimedEdition: UUID,
  observedBehavior: MessageTrace
) -> ConformanceReport
```

### 4. DRR Integration

Every protocol change requires a DRR with:
- Context: Why is change needed?
- Problem: What's broken or missing?
- Decision: What's the minimal change?
- Consequences: What breaks, what improves?
- Alternatives: What else was considered?

## Consequences

**Positive**:
- Protocol evolution becomes auditable and traceable
- Breaking changes are classified, not discovered
- Implementations can self-verify conformance
- Rationale is preserved for future maintainers
- Supports multiple coexisting editions

**Negative**:
- Overhead for spec maintainers (DRR discipline)
- Edition model adds complexity to handshake
- Conformance testing requires test infrastructure
- May slow down protocol iteration

## Rationale

**Deutsch**: Knowledge grows through *conjecture and criticism*. Protocol evolution should follow this pattern: propose changes (conjecture), test them (criticism), adopt what survives. The DRR discipline ensures criticism is structured; the edition model ensures conjectures are traceable.

**FPF**: This implements the Canonical Evolution Loop (B.4):
1. **Run**: Current edition is deployed
2. **Observe**: Collect findings (bugs, missing features, drift)
3. **Refine**: Propose changes via DRR, classify breaking/non-breaking
4. **Deploy**: Release new edition with compatibility matrix

The Γ_time laws (B.1.4) ensure temporal aggregation is lawful—you can't average across editions, and freshness windows are explicit.

## Alternatives Considered

1. **Ad-hoc versioning**: Current state. Error correction broken.
2. **Strict backward compatibility**: Never break. Accumulates cruft, stifles innovation.
3. **Ecosystem fragmentation**: Let implementations diverge. Defeats protocol purpose.
4. **Central authority**: Single entity controls all changes. Bottleneck, political.

## Open Questions

- How to handle semantic changes that don't change schema?
- What's the governance model for edition approval?
- How to deprecate and sunset old editions?
- Should DRRs be part of the spec itself or separate?

## Dependencies

- **Builds on**: DRR-001 (Assurance metadata can track edition conformance)
- **Enables**: Long-term protocol health, ecosystem coherence
- **Related**: DRR-002 (Semantic alignment includes spec semantics)
- **FPF Patterns**: A.4, B.4, E.9, B.1.4, F.13

## References

- Deutsch, D. (2011). *The Beginning of Infinity*, Ch. 9 (Optimism)
- FPF Spec: A.4 Temporal Duality & Open-Ended Evolution
- FPF Spec: B.4 Canonical Evolution Loop
- FPF Spec: E.9 Design-Rationale Record (DRR) Method
- Semantic Versioning: https://semver.org/
