# DRR-005: Agent Tool Coordination

**Status**: Proposed  
**Date**: 2026-01-06  
**Authors**: Human + Warp Agent  
**FPF Grounding**: C.24 (Agent-Tools-CAL), C.5 (Resrc-CAL), C.19 (E/E-LOG), B.3 (Trust Calculus)

## Context

ACP agents execute tool calls during prompt turns to interact with external systems (filesystem, terminal, MCP servers). Tool calls are coordinated through a request-response cycle where the agent plans which tools to invoke and in what sequence.

The current state:

- Tool calls are opaque to validation—no declared plan or budget
- Sequencing logic is internal to agent—no protocol-level coordination primitives
- No resource awareness—agent doesn't declare compute/time/cost constraints
- No exploration-exploitation policy—all tool choices are "exploit" (greedy)
- Tool call failures produce error messages but no structured retry/fallback strategy

## Problem Statement

**Deutsch Framing**: Current explanations for "why did the agent choose this tool sequence" are *easy to vary*. "The model thought it was best" or "that's what the prompt said" can account for any tool selection without predicting future behavior. These explanations have no *reach*—they don't constrain what tool sequences are rational given resource limits.

**FPF Diagnosis**: Tool execution lacks the discipline of FPF C.24 (Agent-Tools-CAL):

- **No Call-Planning**: Tool calls are emitted one-by-one without declared multi-step plan
- **No Budget Constraints**: No compute budget, time budget, or cost budget tracked
- **No Policy Integration**: No explore-exploit policy (C.19 E/E-LOG) governing tool choice
- **No Scale-Awareness**: No scaling-law lens (C.18.1 SLL) for tool selection under resource pressure
- **No BLP Compliance**: No bitter-lesson preference (C.19.1 BLP) favoring general methods over tricks

The current approach resembles "greedy local search" without the scaffolding that FPF C.24 provides for principled tool orchestration.

## Forces

- **Agent autonomy**: Agents need freedom to explore without micromanagement
- **Resource limits**: Compute, time, and cost are finite—unbounded tool use is unacceptable
- **Observability**: Clients/sentinels need visibility into tool choice rationale
- **Protocol overhead**: Adding planning metadata increases message size
- **Backward compatibility**: Existing agents don't emit tool plans
- **Policy diversity**: Different agents may have different E/E strategies

## Decision Drivers

1. Principled tool coordination requires *declared constraints* (budget, policy)
2. Tool sequences should be *auditable*—why this tool, why now?
3. Resource management should be *explicit*, not implicit
4. Exploration policy should be *configurable*, not hardcoded
5. Scale-aware tool selection should favor *general methods* under resource pressure (BLP)

## Proposed Direction

Introduce **Tool Call Planning** with **Resource Budgets** and **E/E Policy**:

### 1. ToolCallPlan Structure

```text
ToolCallPlan := {
  planId: UUID                          // Unique plan identifier
  planningContext: {
    goal: string                        // What agent is trying to accomplish
    constraints: ResourceBudget         // Compute/time/cost limits
    policy: ExploitExplorePolicy        // E/E-LOG strategy
  }
  plannedSequence: ToolCallIntent[]     // What agent intends to do
  executionState: Planned | InProgress | Complete | Failed
}

ResourceBudget := {
  maxToolCalls?: number                 // Call count limit
  maxLatency?: Duration                 // Time budget
  maxCost?: ResourceUnits               // Cost budget (arbitrary units)
  scaleFactor?: float                   // SLL scale probe (1.0 = baseline)
}

ExploitExplorePolicy := {
  strategy: Greedy | EpsilonGreedy | UCB | ThompsonSampling
  explorationRate?: float               // For epsilon-greedy
  temperature?: float                   // For softmax-based
  preferGeneralMethods: boolean         // BLP flag
}

ToolCallIntent := {
  toolName: string
  rationale: string                     // Why this tool now?
  expectedOutcome?: string              // What agent expects to learn
  fallbackOnFailure?: ToolCallIntent    // Contingency plan
}
```

### 2. Protocol Extension (Optional)

Agents MAY include `toolCallPlan` in `SessionPromptResult` or `SessionUpdate`:

```text
AgentToClient: SessionUpdate {
  ...existing fields...
  toolCallPlan?: ToolCallPlan           // Declared tool coordination plan
}
```

### 3. Sentinel Validation

Sentinel can validate:

- **Budget adherence**: Tool call count ≤ declared maxToolCalls
- **Plan-execution alignment**: Executed tools match declared sequence (or rationale for deviation)
- **Policy compliance**: Tool choices respect declared E/E strategy
- **BLP violations**: Flag domain-specific tricks when general methods available

Validation findings:

```text
ValidationFinding.BudgetExceeded(planId, exceeded: ResourceBudget)
ValidationFinding.PlanDeviation(planId, expected: ToolCallIntent, actual: ToolCall)
ValidationFinding.BLPViolation(planId, trick: string, generalAlternative: string)
```

## Consequences

**Positive**:

- Makes tool coordination *auditable*—plans are explicit, not opaque
- Enables *resource management*—budgets prevent runaway tool use
- Supports *policy experimentation*—agents can try different E/E strategies
- Aligns with *FPF C.24*—proper agentic tool orchestration
- Creates foundation for *multi-agent coordination*—agents can share plans

**Negative**:

- Protocol complexity increase (optional field, backward compatible)
- Agents must implement planning layer (or emit trivial plans)
- Risk of "planning theater"—plans that don't reflect actual decision-making
- Budget enforcement requires runtime tracking infrastructure

## Rationale

**Deutsch**: A good explanation is *hard to vary*. By requiring agents to declare a tool plan with resource budgets, we constrain what tool sequences are admissible. An agent cannot simply invoke arbitrary tools—it must justify each call within a declared budget and policy. The plan either predicts the observed tool calls or it doesn't—no room for easy variation.

**FPF**: This implements C.24 (Agent-Tools-CAL) properly:

- **Call-planning discipline**: ToolCallPlan with declared sequence
- **Budget-aware sequencing**: ResourceBudget constrains tool use
- **Policy integration**: ExploitExplorePolicy (E/E-LOG) governs choice
- **Scale-awareness**: SLL scale factor + BLP preferGeneralMethods
- **Resource tracking**: C.5 (Resrc-CAL) for cost accounting

The structure follows FPF's transdisciplinary approach: tool coordination isn't an ad-hoc protocol feature, it's a first-class architectural concern with proper formalization.

## Alternatives Considered

1. **No planning layer**: Current state. Tool calls are uncoordinated black boxes. Easy to vary, no reach.

2. **Hard resource limits only**: Set maxToolCalls=10 globally. Doesn't explain *why* 10, or *which* 10 tools. Parochial.

3. **Post-hoc budget tracking**: Count tool calls after the fact. Reactive, not preventive. Closes barn door after horse escapes.

4. **Client-side planning**: Client dictates tool sequence. Defeats agent autonomy. Micromanagement.

5. **Implicit E/E via temperature**: Use LLM sampling temperature as exploration knob. Domain-specific trick, not general method. BLP violation.

## Open Questions

- Should `ToolCallPlan` be mandatory or optional? (Proposal: optional for backward compat, encouraged for L2 assurance)
- How to represent fallback strategies compactly? (Proposal: tree structure, but keep simple for MVP)
- What ResourceBudget units make sense across heterogeneous agents? (Proposal: dimensionless "resource units", agent-defined)
- Should Sentinel enforce budgets or just report violations? (Proposal: report only, enforcement is agent responsibility)
- How to handle dynamic replanning mid-turn? (Proposal: emit new plan via SessionUpdate, link to original)

## Dependencies

- **Builds on**:
  - DRR-001 (Assurance Envelope provides framework for evidence)
  - DRR-003 (Capability verification ensures agents *can* execute tools)
  - ACP tool call protocol (existing SessionPrompt/SessionUpdate)

- **Enables**:
  - Multi-agent tool coordination (agents share plans)
  - Resource-aware scheduling (sentinel routes to agents with budget)
  - Policy experimentation (compare E/E strategies empirically)

- **FPF Patterns**: C.24, C.5, C.18.1, C.19, C.19.1, B.3, G.5, G.9

## References

- Deutsch, D. (2011). *The Beginning of Infinity*, Ch. 1-2 (Good Explanations, Reach)
- FPF Spec: C.24 Agent-Tools-CAL
- FPF Spec: C.19 E/E-LOG (Explore-Exploit Governor)
- FPF Spec: C.19.1 BLP (Bitter-Lesson Preference)
- FPF Spec: C.18.1 SLL (Scaling-Law Lens)
- FPF Spec: C.5 Resrc-CAL (Resource tracking)
- Sutton, R. (2019). "The Bitter Lesson" (general methods > domain tricks)
- ACP Spec: Tool Call Protocol

---

## Implementation Notes (Non-Normative)

If this DRR is accepted, implementation would proceed as:

1. **Phase 1**: Define `ToolCallPlan` types in `Acp.Domain.fs` (optional extension)
2. **Phase 2**: Add `ValidationFinding` variants for budget/policy violations
3. **Phase 3**: Implement sentinel validation in `Acp.Validation.fs`
4. **Phase 4**: Add examples to `examples/tool-planning/` demonstrating E/E policies

Estimated effort: Medium (3-5 days for full implementation + tests)

FPF alignment impact: Closes C.24 gap, improves overall alignment from ~92% → ~95%.
