---
id: ACP-MSPEC-001
title: "MeasurementSpec: Deep Research Agent (FINDER + DEFT)"
kind: MeasurementSpec
state: Exploration
assurance_level: L0
reference_plane: concept
context: ACP/DeepResearchAgent
created: 2025-12-10
updated: 2025-12-10
---

# MeasurementSpec · Deep Research Agent (FINDER + DEFT)

Purpose: define a Comparability Gauge Frame (CG-frame) for ACP deep-research agent runs, using FINDER tasks/checklists and DEFT failure taxonomy, with lawful Characteristics, Scales, and Scores usable in FPF F/G/R assurance.

Scope note:

- This MeasurementSpec is **not** part of slice‑01 (protocol spec parity). Treat as post‑slice‑01 evaluation/assurance work.

ObjectType

- AgentRun[DeepResearchTask] := single execution producing a report with tool calls and traces.

Characteristics (C)

- C1 checklist_coverage ∈ [0,1] ratio: satisfied / required FINDER checklist items.
- C2 structural_compliance ∈ [0,1] ratio: required sections/formatting present.
- C3 analytical_depth ∈ [0,1] ratio: heuristic depth score (multi-step reasoning, comparisons).
- C4 evidence_grounding ∈ [0,1] ratio: claims requiring citation that have correct citations.
- C5..C18 deft_failure_rate[f] ∈ ℝ≥0: failure count for DEFT category f per report (or normalized per 1 report). Categories (14):
  - LAD (lack of adequate detail)
  - SOD (shallow/one-dimensional)
  - UCF (unsupported claims/fabrication)
  - MDM (missing major dimension)
  - MIS (misaggregation / wrong synthesis)
  - CAS (confused argument structure)
  - PPL (poor planning / lost objective)
  - IER (incorrect evidence retrieval)
  - IRR (irrelevant retrieval)
  - COV (coverage gaps)
  - DUP (duplicate/verbose padding)
  - COH (coherence/flow issues)
  - STL (style/format violations)
  - SCR (spec non-compliance residuals)

Scales / Units

- C1–C4: ratio scale [0,1], operations: mean, min, max allowed; no ordinal mixing.
- C5–C18: count per report (ratio). Derived rate := count / 1 report. Operations: add/avg; no ordinal aggregation.

Score functions (S)

- Per-category taxonomy score (monotone, bounded):  
  S_f = exp( -λ_f \* deft_failure_rate[f] ), default λ_f = 1.0.  
  Properties: 0 < S_f ≤ 1; if failures increase, S_f decreases.
- Optional composite DeepResearchScore:  
  DRS = w1*C1 + w2*C2 + w3*C3 + w4*C4 + Σ_f (w_f \* S_f)  
  with Σ weights = 1, all weights declared in policy; disallowed to use hidden weights.
- All composite scores must be queryable back to primitive C/Scales; no implicit roll-ups.

Evidence & Harness Binding

- Coverage/structure: judged by FINDER checklists per task (human or LLM grader); store item-level judgments.
- Depth: rubric-based evaluator (LLM judge or human) with prompt version logged; heuristic only → CL penalties apply when reused cross-plane.
- Evidence grounding: citation checker (e.g., fact validation) against cited sources; record match/mismatch.
- DEFT: classifier labeling spans/claims with failure categories; record counts + spans.
- Each judgment is an evidence item with:
  - lane: R (results), F for definition provenance, G for task scope;
  - Γ_time; PathId/PathSliceId; judgment method/version; CL/CL^plane penalties where synthetic benchmarks are reused.

Claim Scope (G)

- G tied to FINDER task scope: domain, time cutoff, required report length/format.
- Reuse outside that scope requires Bridge with CL^plane penalty applied to R only.

Acceptance predicates (example, policy-tunable)

- Minimums: C1 ≥ 0.70, C2 ≥ 0.70, C4 ≥ 0.60.
- Failure tolerances: deft_failure_rate[f] ≤ 0.15 for all f (or S_f ≥ exp(-0.15)).
- These thresholds are defaults; policy must declare actual values and edition.

Output schema (for validator implementations)

- metrics:
  - checklist_coverage, structural_compliance, analytical_depth, evidence_grounding (floats [0,1])
  - deft_failure_rate: map<category,float>
  - taxonomy_score: map<category,float>
  - composite_scores: map<string,float> (if used; must include weight vectors)
- evidence_refs: list of judgment artifacts (per item/section/failure)
- meta: task_id, prompt_hash, model/tool versions, Γ_time, PathId/PathSliceId, edition.

Assurance notes

- F-lane: definitions of C/Scales/S live here; do not degrade via CL.
- G-lane: task scope; ensure no stealth widening—use SpanUnion only across independent support.
- R-lane: apply penalties Φ(CL), Ψ(CL^k), Φ_plane for cross-context/plane reuse; freshness/valid_until for empirical judgments.

State / Assurance

- State: Exploration (no inductive evidence yet in this repo).
- AssuranceLevel: L0 until:
  - VA: harness correctness and score monotonicity proved or tested,
  - LA: empirical validation on sample tasks,
  - TA: typing/conformance of schemas to ACP/UTS.
