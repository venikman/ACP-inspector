# ACP slice‑01 MVP roadmap

We keep roadmap tracking in-repo (no external issue trackers). This file is the current source of truth for slice‑01.

## Goal of MVP
Deliver a thin, end‑to‑end ACP runtime slice that:
- runs via a minimal CLI client
- executes a basic Sentinel check over a request
- is grounded in an explicit ACP × FPF ontology and F/G/R trust framing
- is instrumented with a minimal eval + CI/CD loop
- is demoable to external stakeholders

## Milestones

### Milestone 0 – Foundations & trust framing
Scope:
- HAR‑17 · Define ACP holons and bounded contexts (ACP × FPF skeleton)
- HAR‑21 · Define FGR trust thresholds for ACP releases
- HAR‑6  · Map eval patterns into Sentinel holon
Outcome:
- Clear ACP holons and boundaries with FPF concepts
- F/G/R trust levels defined for slice‑01 (what “safe enough to ship” means)
- Eval patterns mapped into the Sentinel holon
Emphasis: Problem / prove‑out; Ontology / epistemology.

### Milestone 1 – Thin runtime slice
Scope:
- HAR‑22 · Design first thin ACP runtime slice (end‑to‑end demoable)
- HAR‑24 · slice‑01: Implement minimal ACP Protocol support
- HAR‑25 · slice‑01: Implement core ACP Runtime logic
Outcome:
- Designed “happy‑path” runtime slice with clear boundaries and assumptions
- Minimal protocol/runtime that runs a single ACP flow in-process
- Ready for Sentinel integration and CLI wiring
Emphasis: Problem / prove‑out; Implementation.

### Milestone 2 – Sentinel + eval integration
Scope:
- HAR‑26 · slice‑01: Implement basic Sentinel check
- HAR‑4  · Add Acp.Eval module with domain types for eval framework
- HAR‑2  · Design LLM‑as‑judge EvalJudgeKind & integration hooks
- HAR‑3  · Implement code‑based EvalJudge: rule for non‑empty instruction
- HAR‑5  · Extend UTS.md: Add R20‑R25 for eval profiles, golden cases, runs, metrics, error cycles
- HAR‑1  · Track CI/CD and monitoring eval runs
Outcome:
- Sentinel check running in the thin slice
- Minimal eval surface (Acp.Eval types + one code‑based judge + one LLM‑as‑judge kind)
- Eval profiles and golden cases documented
- CI/CD + monitoring run evals on change
Emphasis: Testing / eval; Implementation.

### Milestone 3 – CLI + public demo
Scope:
- HAR‑27 · slice‑01: Implement minimal CLI client
- Wire CLI → runtime → Sentinel → eval reporting
Outcome:
- Scripted CLI flow that:
  1) Accepts a request
  2) Runs through ACP runtime + protocol
  3) Executes a Sentinel check with F/G/R thresholds
  4) Presents a human‑readable result for demo
Emphasis: Demo; Implementation.

### Post‑MVP – Agentization & hygiene (next wave)
- HAR‑16 · Agent: doc‑code consistency sweeps
- HAR‑15 · Agent: propose explore/exploit experiments (NQD)
- HAR‑14 · Agent: draft F,G,R trust stubs for major features
- HAR‑13 · Agent: check contextual meaning & bridges
- HAR‑12 · Agent: enforce Role / Method / Work tagging
- HAR‑11 · Agent: decompose high‑level feature requests into FPF‑typed issues

## Issue → workstream matrix

| ID    | Title                                                              | Problem | Ontology | Impl | Eval | Demo | MVP scope |
|-------|--------------------------------------------------------------------|---------|----------|------|------|------|-----------|
| HAR‑27| slice‑01: Implement minimal CLI client                             |         |          | ✅   |      | ✅   | Core      |
| HAR‑26| slice‑01: Implement basic Sentinel check                           |         |          | ✅   | ✅   | ✅   | Core      |
| HAR‑25| slice‑01: Implement core ACP Runtime logic                         |         |          | ✅   |      |      | Core      |
| HAR‑24| slice‑01: Implement minimal ACP Protocol support                   |         |          | ✅   |      |      | Core      |
| HAR‑22| Design first thin ACP runtime slice (end‑to‑end demoable)          | ✅       |          | ✅   |      | ✅   | Core      |
| HAR‑21| Define FGR trust thresholds for ACP releases                       |         | ✅        |      | ✅   |      | Core      |
| HAR‑17| Define ACP holons and bounded contexts (ACP × FPF skeleton)        | ✅       | ✅        |      |      |      | Core      |
| HAR‑16| Agent: doc‑code consistency sweeps                                 |         |          | ✅   | ✅   |      | Stretch   |
| HAR‑15| Agent: propose explore/exploit experiments (NQD)                   | ✅       |          |      |      |      | Post‑MVP  |
| HAR‑14| Agent: draft F,G,R trust stubs for major features                  | ✅       | ✅        |      |      |      | Stretch   |
| HAR‑13| Agent: check contextual meaning & bridges                          |         | ✅        | ✅   |      |      | Post‑MVP  |
| HAR‑12| Agent: enforce Role / Method / Work tagging                        |         | ✅        | ✅   |      |      | Post‑MVP  |
| HAR‑11| Agent: decompose high‑level feature requests into FPF‑typed issues | ✅       | ✅        |      |      |      | Post‑MVP  |
| HAR‑6 | Map eval patterns into Sentinel holon                              | ✅       | ✅        |      | ✅   |      | Core      |
| HAR‑5 | Extend UTS.md: Add R20‑R25 for eval profiles, golden cases, runs…  |         |          |      | ✅   |      | Stretch   |
| HAR‑4 | Add Acp.Eval module with domain types for eval framework           |         |          | ✅   | ✅   |      | Core      |
| HAR‑3 | Implement code‑based EvalJudge: rule for non‑empty instruction     |         |          | ✅   | ✅   |      | Core      |
| HAR‑2 | Design LLM‑as‑judge EvalJudgeKind & integration hooks              |         | ✅        | ✅   | ✅   |      | Core      |
| HAR‑1 | Track CI/CD and monitoring eval runs                               |         |          | ✅   | ✅   |      | Core      |

Public preface (optional):
> We organize work across five streams: clarifying the problem, defining the ACP/FPF ontology, building the runtime and protocol, wiring testing & eval, and delivering a clean demo surface. The table shows how each issue contributes to the slice‑01 MVP.
