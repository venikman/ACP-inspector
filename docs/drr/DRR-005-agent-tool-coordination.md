# DRR-005: Agent Tool Coordination

**Status**: Proposed  
**Date**: 2026-03-19  
**Authors**: ACP Inspector maintainers  
**FPF Grounding**: `C.24`, `A.6`, `E.17`, `A.16`

## Context

ACP agents already coordinate tool use through protocol-native surfaces:

- `plan` updates
- slash commands / available commands
- tool-call traces and tool-call updates

The January draft of this DRR proposed a custom `ToolCallPlan` object as a new primary coordination structure. That direction now conflicts with the stronger March 2026 FPF reading of boundary discipline and publication discipline.

## Problem

We need agent-tool coordination that is:

- inspectable
- auditable
- compatible with ACP stable behavior
- extensible for policy metadata

without inventing a second planning protocol that competes with ACP itself.

## Decision

ACP-native coordination surfaces are canonical. ACP Inspector will model and validate tool coordination around:

- typed `plan` updates
- slash-command availability and updates
- tool-call events and tool-call updates
- trace consistency between declared plans and observed work-effects

Optional policy metadata such as budget hints, retry strategy, exploration heuristics, or ranking policy may exist as local overlays, but they are secondary and must not replace ACP-native coordination semantics.

## A.6.B Boundary Routing

### Laws / invariants

- protocol/runtime ordering of tool-call and plan events must be preserved
- trace/state validation must not infer impossible tool histories
- a view must not invent tool outcomes that are absent from the trace

### Admissibility

- a tool call is admissible only when the protocol/session state allows it
- a plan update is admissible only in the states where ACP permits it
- slash-command availability is admissible only when exposed by the agent surface

### Commitments / deontics

- the agent must expose the coordination state through ACP-native surfaces when it claims to support them
- the client must not treat local overlays as protocol law
- the sentinel must report mismatches as findings rather than rewriting the protocol contract

### Evidence / work-effects

- tool-call requests and updates
- retries, failures, and cancellations
- plan updates
- slash-command availability updates
- trace records and validation findings

## E.17 Constraint

CLI summaries, replay output, dashboards, and reports are projections over the same canonical trace and protocol artifacts. They may explain coordination, but they may not define a second planning semantics.

## A.16 Note

Experimental planning policies, budget schemes, exploration heuristics, and fallback strategies are draft-language-state material unless and until ACP or the repo promotes them explicitly. Draft policy overlays may be published, but they must remain clearly marked as draft or profile-scoped.

## Consequences

### Positive

- Aligns the repo with ACP stable surfaces already in use
- Avoids a shadow planning protocol
- Makes validation and CLI output traceable back to protocol evidence
- Leaves room for policy experimentation without hard-coding it into the contract

### Negative

- Some richer planning ideas move out of the protocol core and into optional overlays
- Budget/exploration metadata becomes advisory unless a higher-level profile defines stronger rules
- Existing January wording that centered `ToolCallPlan` is no longer authoritative

## Alternatives Considered

### 1. Keep the custom `ToolCallPlan` as primary

Rejected. It duplicates ACP-native coordination and turns repo-local documentation into a shadow protocol.

### 2. Ignore coordination entirely and validate only raw tool calls

Rejected. It leaves plan/slash-command surfaces underused and weakens auditability.

### 3. Make policy metadata mandatory at the protocol boundary

Rejected. ACP stable does not require that today, and forcing it locally would overstep the boundary.

## Implementation Notes

If accepted, implementation work should prioritize:

1. typed plan updates in protocol/runtime/validation
2. slash-command availability and rendering
3. trace validation for plan/tool-call consistency
4. optional policy metadata layered on top as repo-local profiles, not as a second protocol

Heuristics such as epsilon-greedy, UCB, Thompson sampling, or bespoke budget models may still be useful, but they belong in policy profiles and experiments, not in the canonical protocol contract.
