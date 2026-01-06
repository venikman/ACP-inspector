# DRR-001: Agent Output Trustworthiness

**Status**: Proposed  
**Date**: 2026-01-06  
**Authors**: Human + Warp Agent  
**FPF Grounding**: B.3 (Trust & Assurance Calculus), B.5.2 (Abductive Loop), C.2 (KD-CAL)

## Context

Agent Client Protocol (ACP) enables communication between clients and AI agents. Currently, agent outputs (messages, tool call results, reasoning traces) are transmitted as opaque payloads. Consumers must decide whether to trust these outputs without formal assurance infrastructure.

The current state:
- Agent outputs carry no provenance metadata
- No formal distinction between high-confidence and speculative claims
- Hallucinations and factual errors are indistinguishable from valid outputs at protocol level
- Post-hoc validation is the only defense (reactive, not preventive)

## Problem Statement

**Deutsch Framing**: Current explanations for "why trust this agent output" are *easy to vary*. "The model is good" or "it passed my spot check" can account for any observation without constraining what outputs are trustworthy. These explanations have no *reach*—they don't predict which future outputs will be reliable.

**FPF Diagnosis**: Agent outputs are *epistemes* (U.Episteme, C.2.1) without proper slot population:
- **DescribedEntitySlot**: Undefined—what real-world object does this claim describe?
- **GroundingHolonSlot**: Empty—no physical system anchors the claim
- **ClaimGraphSlot**: Missing—no evidence structure
- **ViewpointSlot**: Implicit—no declared perspective or scope

The F-G-R triad (B.3) is maximally deficient:
- **F (Formality)**: F≈0 (natural language, no formal structure)
- **G (ClaimScope)**: Unbounded (claims apply... somewhere?)
- **R (Reliability)**: Unknown (no evidence path, no PathId)

## Forces

- **Latency**: Adding assurance metadata increases message size and processing time
- **Compatibility**: Existing ACP implementations expect current message shapes
- **Complexity**: Full F-G-R calculus may be overkill for many use cases
- **Adoption**: Agents must emit assurance data; clients must consume it
- **Auditability**: Regulated domains (finance, healthcare) need evidence trails

## Decision Drivers

1. Error-correction requires *structure*—you can't criticize what you can't parse
2. Trust must be *computed*, not felt (B.3 principle)
3. Assurance should be *layered*—minimal baseline, optional depth
4. Protocol changes should be *backward compatible*

## Proposed Direction

Introduce optional **Assurance Envelope** in ACP messages:

```
AssuranceEnvelope := {
  formality: F0..F9           // How rigorous is the claim?
  scope: ContextSlice[]       // Where does it apply?
  reliability: {
    level: L0..L2             // Unsubstantiated / Circumstantial / Evidenced
    pathId?: string           // Reference to evidence graph
    decay?: ISO8601Duration   // Freshness window
  }
  groundingRef?: URI          // Link to grounding holon
}
```

Sentinel layer validates:
- Envelope completeness per declared AssuranceLevel
- Consistency between claimed F and actual formality markers
- Scope containment (claims don't exceed declared G)

## Consequences

**Positive**:
- Enables formal trust reasoning at protocol level
- Supports layered assurance (L0 → L2 progression)
- Creates foundation for cross-agent evidence sharing
- Aligns with FPF's B.3 calculus

**Negative**:
- Protocol complexity increase
- Agents must implement assurance emission
- Risk of "assurance theater" (claiming high F/R without substance)

## Rationale

**Deutsch**: A good explanation is *hard to vary*. By requiring agents to declare scope (G) and evidence paths (R), we constrain what counts as a valid claim. An agent cannot simply assert trustworthiness—it must provide structure that can be criticized.

**FPF**: This implements KD-CAL's (C.2) episteme slot graph at the protocol level. The EvidenceGraph (G.6) becomes a first-class protocol artifact, not an afterthought.

## Alternatives Considered

1. **Post-hoc validation only**: Current state. Easy to vary, no reach.
2. **Binary trust flag**: Too coarse. "Trusted=true" explains nothing.
3. **Full formal proofs**: F9 is impractical for most agent outputs.
4. **Separate assurance channel**: Complicates implementation, loses atomicity.

## Open Questions

- How to handle streaming outputs where assurance may change mid-stream?
- What's the minimal viable AssuranceEnvelope for L0 claims?
- How do CL (Congruence Level) penalties apply when agents bridge contexts?

## Dependencies

- **Builds on**: ACP message schema, Sentinel validation infrastructure
- **Enables**: DRR-002 (Cross-Agent Semantic Alignment), DRR-003 (Capability Verification)
- **FPF Patterns**: B.3, C.2, C.2.1, C.2.2, G.6

## References

- Deutsch, D. (2011). *The Beginning of Infinity*, Ch. 1-2 (Good Explanations)
- FPF Spec: B.3 Trust & Assurance Calculus
- FPF Spec: C.2 KD-CAL
- ACP Spec: Message Schema
