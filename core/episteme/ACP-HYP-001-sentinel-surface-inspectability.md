# ACP-HYP-001 · Sentinel‑Surface Inspectability

- ArtifactId: ACP-HYP-001
- Family: ConceptualCore
- Type: U.Episteme (Hypothesis)
- AssuranceLevel: L0 (Unsubstantiated)
- LifecycleState: Explore

## 0. Artifact Header

- Parent Context: ACP‑Inspector / ACP governance stack
- Created via Pattern: B.5.2 Abductive Loop
- Primary Conformance Targets: CC-B5.2.1 · CC-B5.2.2 · CC-B5.2.3 · CC-B5.2.4

## 1. Anomaly Reference (CC‑B5.2.1)

- AnomalyId: ACP-ANOM-001
- AnomalyTitle: Missing FPF‑aligned assurance story for ACP behavior from telemetry
- AnomalyStatement:
  - In current ACP‑style agent stacks, we do not have a compositional, FPF‑aligned way to show that every agent/tool action has passed through the intended sentinel / gate checks, the ACP state machine has moved only through allowed transitions, and the resulting behavior is backed by auditable assurance signals (F/G/R lanes, AssuranceLevel) rather than opaque, implementation‑specific logs.
  - As a result, we cannot explain or defend the claimed safety/governance properties of a deployed ACP system purely from its observable behavior and telemetry.
- Placement in FPF: This anomaly arises in the Observe phase of the Canonical Evolution Loop, where runtime behavior is compared to the design‑time ACP / E.TGA model, and discrepancies or blind spots are surfaced.
- Normative link: This satisfies CC‑B5.2.1 (Anomaly Framing Mandate): abductive work begins from a documented anomaly that the current model cannot address.

## 2. Candidate Hypothesis Set (Abductive Exploration)

Informative; supports traceability, not all candidates need to be retained in detail.

- CandidateSet label: ACP-HYP-CAND-SET-001 (abductive exploration around ACP assurance from telemetry)
- Selected candidate: **H1 – Sentinel as primary assurance surface.** Short description: Treat the sentinel/gate layer as the primary assurance object for ACP, provided it emits FPF‑compatible telemetry for every external action and state transition. Assurance metrics (R/CL/F/G) are then computed over the sentinel surface, not opaque agent internals.
- If you had other discarded candidates (e.g., “agent‑centric assurance,” “environment‑centric assurance”), you can list them here with 1–2 sentence rationales for rejection to improve abductive traceability, but this is optional at L0.

## 3. Prime Hypothesis (Selected Candidate)

- PrimeHypothesisId: ACP-HYP-001 (this artifact)
- SelectionStatus: Prime (selected from CandidateSet ACP-HYP-CAND-SET-001)

**Formal Claim (technical wording):**

For any ACP implementation that routes all external actions of agents/clients through a sentinel‑mediated gate stack and emits FPF‑compatible telemetry for each PathSlice and GateCrossing (including GateDecision, ConstraintViolation status, sentinel triggers, and ACP state transitions), an ACP‑inspector‑style tool can reconstruct a slice‑local, E.TGA‑style model of the run and compute protocol conformance violations, deviations from the canonical reasoning state machine (Explore → Shape → Evidence → Operate), and per‑run assurance summaries (R, CL, and key gauges) sufficient to explain and, where necessary, challenge the claimed safety and governance properties of that ACP implementation using the FPF assurance calculus rather than ad hoc logging.

Normative link: This is a new U.Episteme artifact created by abduction and therefore starts at AssuranceLevel:L0, satisfying CC‑B5.2.3 (L0 Artifact Mandate).

## 4. Plausibility Filters (CC‑B5.2.2)

Evaluation of ACP-HYP-001 against required plausibility filters. At least two filters are mandatory.

| Filter | Assessment of ACP-HYP-001 | Notes |
| --- | --- | --- |
| Parsimony | Moderate / Acceptable. Hypothesis reuses existing FPF primitives (sentinel, PathSlice, E.TGA, DecisionLog, trust & assurance calculus) and adds only a telemetry schema + process for reconstruction. No new core theory is introduced; complexity is largely implementation detail. | Aligns with FPF guidance that abduction should introduce minimal new complexity consistent with solving the anomaly. |
| Explanatory Power | High. If true, the hypothesis explains how to derive a defensible assurance story purely from ACP telemetry, resolve uncertainty about gate/sentinel enforcement, and check ACP state machine conformance. It directly addresses ACP‑ANOM‑001 instead of only local symptoms. | Supports FPF intent that the selected hypothesis should actually resolve the framed anomaly, not just alleviate symptoms. |
| Consistency | High. Hypothesis is consistent with FPF’s Canonical Reasoning Cycle and Explore → Shape → Evidence → Operate state machine, which already assume artifacts progress through states driven by structured reasoning and assurance evidence. It locates assurance at the sentinel surface, which is already an FPF‑conformant control layer. |  |
| Falsifiability | High. Hypothesis implies concrete, testable predictions about reconstructability of E.TGA runs, detectability of injected protocol violations, and impact of gate/sentinel hardening on computed R/CL. These predictions can be evaluated by simulation and real ACP deployments. | Satisfies FPF’s requirement that selected hypotheses generate clear, testable predictions. |

Plausibility conclusion: On current information, ACP-HYP-001 is retained as the prime hypothesis for addressing ACP-ANOM-001. It balances parsimony and explanatory power while remaining consistent with the FPF assurance stack.

## 5. Testable Predictions (Deductive Hooks)

These are intentionally framed to feed the later Deduction and Induction phases of the Canonical Reasoning Cycle.

**Trace‑Replay Prediction**

- Claim: Given a recorded ACP run with complete sentinel/telemetry, ACP‑inspector will either construct a unique E.TGA‑compatible transduction graph with PathSlices and GateDecisions, or emit concrete, localized reconstruction failures (e.g., missing pins, illegal crossings, non‑conformant state transitions).
- Intended test type: Structural reconstruction tests over synthetic and real logs.

**Conformance‑Detection Prediction**

- Claim: For seeded fault‑injection runs (e.g., skipped sentinel refresh, illegal state jump in the ACP state machine, hidden or bypassed gate), ACP‑inspector will raise at least one specific conformance violation tied to the relevant TGA/FPF checklist item.
- Intended test type: Controlled ACP scenarios with known violations, evaluated for detection rate and precision.

**Assurance‑Delta Prediction**

- Claim: When the ACP implementation is hardened (e.g., additional sentinel rules, stricter gates) without changing prompts/models, ACP‑inspector’s computed assurance report for a fixed workload will show increased R and/or improved assurance gauges, reduced CL penalties, with the delta attributable to the gate/sentinel layer rather than agent internals.
- Intended test type: A/B comparisons of ACP configurations holding model and prompts fixed, varying only sentinel/gate policies.

These predictions are not yet proven or empirically validated; they exist to guide the next Shaping and Evidence phases that could promote this artifact beyond L0.

## 6. Traceability & Rationale (CC‑B5.2.4)

- Anomaly linkage: ACP-ANOM-001 → motivates ACP-HYP-001. The hypothesis exists explicitly to resolve the inability to derive ACP assurance from telemetry.
- Reasoning linkage: Generated via Abductive Loop (Pattern B.5.2) with explicit anomaly framing, candidate set consideration, plausibility filtering, and selection.
- Lifecycle linkage: Located in Explore state as a new hypothesis; promotion to Shape and Evidence will require a deductive artifact deriving detailed consequences and formal checks from this hypothesis, and inductive artifacts (experiments, simulations, real‑world ACP runs) testing the predictions.
- Summary rationale (human‑readable): ACP‑style stacks currently claim safety and governance properties based on their use of sentinels, gates, and protocol constraints, but these claims are difficult to audit from logs alone. By designating the sentinel layer as the primary assurance surface and requiring FPF‑compatible telemetry for every gate crossing and state transition, we conjecture that a tool like ACP‑inspector can reconstruct the effective transduction graph, detect protocol/state‑machine violations, and compute assurance metrics directly from runtime behavior. If this holds, ACP assurance becomes transparent, compositional, and auditable from telemetry, resolving the core anomaly.

This block satisfies CC‑B5.2.4 (Traceability Mandate): the artifact contains a rationale linking back to the anomaly and summarizing plausibility filtering and selection.
