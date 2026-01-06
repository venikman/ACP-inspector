# DRR-002: Cross-Agent Semantic Alignment

**Status**: Proposed  
**Date**: 2026-01-06  
**Authors**: Human + Warp Agent  
**FPF Grounding**: A.1.1 (U.BoundedContext), F.9 (Bridges & CL), C.3.3 (KindBridge)

## Context

As agent ecosystems grow, agents increasingly communicate with each other—directly or via orchestration layers. Each agent operates within its own semantic context: its training data, fine-tuning, system prompts, and tool definitions create a *de facto* bounded context.

The current state:

- No formal mechanism to declare an agent's semantic context
- Term meanings assumed to be universal (but they aren't)
- "Tool call" means different things to different agents
- Cross-agent communication relies on implicit shared understanding

## Problem Statement

**Deutsch Framing**: The assumption "we both mean the same thing by X" is *infinitely easy to vary*. When Agent A says "file" and Agent B interprets "file," nothing in the protocol constrains whether they share meaning. This explanation has no *reach*—it cannot predict when miscommunication will occur.

**FPF Diagnosis**: Missing U.BoundedContext (A.1.1) declarations and Bridges (F.9):

- Each agent is an implicit BoundedContext with undefined boundaries
- No explicit mapping between agent-local terms and shared vocabulary
- Congruence Level (CL) is never computed—we don't know translation loss
- KindBridge (C.3.3) is absent—type mappings are assumed, not declared

The problem is structural: without declared contexts and bridges, semantic alignment is *hoped for*, not *engineered*.

## Forces

- **Agent diversity**: Agents are built by different teams with different ontologies
- **Implicit context**: System prompts and training create meaning, but aren't exposed
- **Protocol neutrality**: ACP aims to be agent-implementation-agnostic
- **Performance**: Full semantic negotiation is expensive
- **Practicality**: Most agents "just work" in common cases

## Decision Drivers

1. Meaning is *local* (FPF A.1.1)—universal semantics is a fiction
2. Cross-context communication requires *explicit bridges* with *declared loss*
3. The protocol should support *progressive formalization* (start informal, add rigor)
4. Agents should be able to *discover* each other's semantic commitments

## Proposed Direction

Introduce **Semantic Context Declaration** and **Alignment Bridges**:

### 1. Context Declaration (in Initialize handshake)

```text
SemanticContext := {
  contextId: URI                    // Unique context identifier
  vocabularyRef?: URI               // Link to term definitions
  kindSignatures?: KindSignature[]  // Declared types with intensions
  invariants?: Invariant[]          // Context-local rules
}
```

### 2. Bridge Declaration (on cross-agent calls)

```text
AlignmentBridge := {
  sourceContext: URI
  targetContext: URI
  mappings: KindMapping[]
  congruenceLevel: CL0..CL4        // Declared translation fidelity
  lossNotes?: string[]             // What's lost in translation
}
```

### 3. Sentinel Validation

- Flag cross-agent messages without bridge declarations
- Compute CL penalties on reliability (R) for bridged claims
- Warn when term is used across contexts without mapping

## Consequences

**Positive**:

- Makes semantic assumptions explicit and criticizable
- Enables principled cross-agent trust reasoning
- Supports heterogeneous agent ecosystems
- Creates foundation for semantic interoperability standards

**Negative**:

- Significant protocol addition
- Agents must introspect and declare their semantics (hard for LLMs)
- Risk of over-formalization for simple use cases
- CL computation may be subjective

## Rationale

**Deutsch**: Good explanations are *hard to vary* because they're tightly connected to reality. By requiring explicit context declarations and bridges, we force agents to commit to specific semantic claims that can be tested. "We mean the same thing" becomes a *falsifiable hypothesis*, not an assumption.

**FPF**: This implements the core F.0.1 principle: "Meaning is local. A term is defined strictly within a U.BoundedContext. Cross-context communication happens only via explicit Bridges with declared translation loss."

## Alternatives Considered

1. **Assume shared semantics**: Current state. Infinitely easy to vary.
2. **Universal ontology**: Force all agents to use same terms. Impractical, stifles innovation.
3. **Runtime negotiation**: Agents negotiate meanings per-call. Too expensive.
4. **Embedding-space alignment**: Use vector similarity. Opaque, not auditable.

## Open Questions

- How do LLM-based agents introspect their semantic commitments?
- What's the right granularity for KindSignatures?
- How to handle emergent meanings not in declared vocabulary?
- Should CL be self-declared or computed by sentinel?

## Dependencies

- **Builds on**: DRR-001 (Assurance Envelope provides F-G-R for bridged claims)
- **Enables**: DRR-003 (Capability claims need semantic grounding)
- **FPF Patterns**: A.1.1, F.0.1, F.9, C.3.3, C.2.2

## References

- Deutsch, D. (2011). *The Beginning of Infinity*, Ch. 10 (The Multiverse)
- Evans, E. (2003). *Domain-Driven Design*, Ch. 14 (Bounded Contexts)
- FPF Spec: A.1.1 U.BoundedContext
- FPF Spec: F.9 Alignment & Bridge across Contexts
- FPF Spec: C.3.3 KindBridge & CL^k
