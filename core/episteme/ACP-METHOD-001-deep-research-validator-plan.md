---
id: ACP-METHOD-001
title: "Validator Plan: Deep Research Agent (FINDER/DEFT + Evals harness)"
kind: MethodDescription
state: Exploration
assurance_level: L0
reference_plane: concept
context: ACP/DeepResearchAgent
created: 2025-12-10
updated: 2025-12-10
---

# Validator Plan · Deep Research Agent (FINDER/DEFT + OpenAI-Evals-style harness)

Purpose: outline how to implement a validator that executes FINDER tasks and DEFT classification via an Evals-style runner, emitting FPF-legal metrics for ACP-MSPEC-001.

## Roles & components
- Runner: Evals-style harness (scripted evals, multi-turn/tool capable).
- Judge(s): LLM/VLM graders for checklist items, structure, depth, evidence; DEFT classifier (LLM with taxonomy prompt, or model fine-tuned on DEFT).
- Adapter: ACP integration layer that feeds AgentRun context to the harness and records PathId/PathSliceId.
- Store: evidence/metrics sink (FPF EvidenceGraph slots with lanes).

## Pipeline (per AgentRun)
1) **Task load**: select FINDER task; materialize checklist + structure spec; capture task scope (G).
2) **Execute**: run agent under Evals harness; collect:
   - final report,
   - tool traces,
   - timing/metadata.
3) **Checklist/structure grading (C1,C2)**:
   - For each checklist item: LLM grader returns pass/fail + rationale; store item-level evidence.
   - Structure: LLM grader checks sections/format.
4) **Depth grading (C3)**:
   - Rubric prompt for multi-step reasoning, comparisons, trade-offs; returns score [0,1]; mark heuristic → CL penalty when reused cross-plane.
5) **Evidence grounding (C4)**:
   - Citation checker: verify cited sources for claims; return fraction correct.
6) **DEFT classification (C5..C18)**:
   - Run DEFT classifier over report (and optionally reasoning trace); count failures per category; store spans.
7) **Scores**:
   - Compute taxonomy S_f = exp(-λ_f * failure_rate[f]).
   - Optionally compute composite DRS with declared weights.
8) **Emit metrics**:
   - Metrics object per ACP-MSPEC-001 schema.
   - Evidence refs per judgment; include model/prompt versions, Γ_time, PathId/PathSliceId, lane tags, CL/CL^plane as needed.

## Minimum schemas (wire format hints)
- metrics.json:
  - checklist_coverage, structural_compliance, analytical_depth, evidence_grounding: float
  - deft_failure_rate: map<string,float>
  - taxonomy_score: map<string,float>
  - composite_scores?: map<string,{value:float,weights:map<string,float>}>
  - meta: task_id, prompt_hash, model_ids, tool_versions, path_id, path_slice_id, gamma_time, edition
- evidence records:
  - type: checklist|structure|depth|evidence|deft
  - item_id / span / rationale
  - judge_model / prompt_version
  - lane: R (results); F for definitions; G for scope links
  - cl, cl_plane (if heuristic or cross-plane)

## Acceptance gates (policy-tunable defaults)
- Coverage C1 ≥ 0.70; Structure C2 ≥ 0.70; Evidence C4 ≥ 0.60.
- For all DEFT categories: failure_rate[f] ≤ 0.15 (⇔ S_f ≥ exp(-0.15)).
- Optional composite gate: DRS ≥ policy_threshold.

## Assurance hooks
- F-lane: metric/score definitions (from ACP-MSPEC-001).
- G-lane: FINDER task scope; ensure reuse uses Bridges + CL^plane penalties.
- R-lane: all judgments; apply Φ(CL), Ψ(CL^k), Φ_plane for heuristic graders and cross-context reuse; include valid_until for empirical graders.

-## Test plan (v0.1)
- Golden set: 3 FINDER tasks (see `tests/golden/deep-research-sample.yaml`) with hand labels for checklist/structure and DEFT categories; verify metric outputs and monotonicity (more failures → lower S_f). Expand to 5–10 tasks in v0.2.
- Regression: lock prompt/model versions; hash prompts; compare metrics across runs.
- Path coverage: ensure ≥80% of tool calls in the run have PathId/PathSliceId metadata in emitted records (align with MCP telemetry when present).

## Roadmap slice
- State: Exploration → Shaping once schemas + golden tests exist.
- Deliverable: runnable validator script + metric/evidence emitter wired to ACP logging/EvidenceGraph.
