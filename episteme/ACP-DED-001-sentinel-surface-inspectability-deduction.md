# ACP-DED-001 · Sentinel-Surface Inspectability · Deductive Consequences

## Artifact Header

- ArtifactId: ACP-DED-001
- Title: Sentinel-Surface Inspectability · Deductive Consequences
- Type: U.Deduction (Deduction)
- AssuranceLevel: L0 (Analytic, unvalidated)
- Lifecycle State: Shape (Deductive consequences, pre-evidence)
- Parent Episteme: ACP-HYP-001 · Sentinel-Surface Inspectability
- Parent Context: ACP-Inspector / ACP governance stack
- Created via Pattern: B.5.3 Deductive Loop (from U.Episteme to concrete predictions)
- Primary Conformance Targets:
  - CC-B5.3.1 · Normalized Premise Set
  - CC-B5.3.2 · Explicit Derivation Steps
  - CC-B5.3.3 · Testable Consequence Catalog
  - CC-B5.3.4 · Traceability to Predictions

## 1. Hypothesis Reference & Scope

Reference hypothesis: ACP-HYP-001 (Sentinel-Surface Inspectability).

If an ACP implementation routes all external actions of agents/clients through a sentinel-mediated gate stack and emits FPF-compatible telemetry for each PathSlice and GateCrossing (including GateDecision, ConstraintViolation status, sentinel triggers, and ACP state transitions), then an ACP-inspector-style tool can reconstruct a slice-local, E.TGA-style model of the run and compute protocol conformance violations, deviations from the canonical reasoning state machine, and per-run assurance summaries (R, CL, and key gauges) sufficient to explain and, where necessary, challenge the claimed safety and governance properties of that ACP implementation using the FPF assurance calculus.

Deduction goal: derive, as explicit theorems, the three prediction families stated in ACP-HYP-001 (Trace-Replay, Conformance-Detection, Assurance-Delta) and expose the premises each family depends on.

## 2. Normalized Premise Set

We normalize the hypothesis and minimal surrounding assumptions into explicit premises P1–P7.

### 2.1 Structural premises

- **P1 (Sentinel totality on external actions).** For every external action a of the ACP implementation in a run ρ, there exists exactly one GateCrossing event g(a) in the sentinel/gate stack telemetry.
- **P2 (PathSlice coverage).** For every time-ordered pair of GateCrossings g_i, g_{i+1} in a run ρ, there exists a unique PathSlice record ps_i covering the internal behavior between them, including ACP state transitions.
- **P3 (FPF-compatible schema).** Every PathSlice and GateCrossing conforms to an FPF schema that includes at least ActorId, PathSliceId, RunId; GateId, GateDecision, ConstraintViolation; ACPStateBefore, ACPStateAfter; and DecisionLog/pins sufficient to embed in an E.TGA transduction graph.
- **P4 (Deterministic reconstruction).** Given a complete, time-ordered set of PathSlices and GateCrossings for a run ρ, an ACP-inspector algorithm I deterministically constructs an E.TGA-compatible transduction graph T(ρ) or returns a localized reconstruction failure on a finite subset of events.

### 2.2 Conformance & assurance premises

- **P5 (Protocol & state conformance encoding).** There exists a set of FPF/ACP conformance rules C such that violations of protocol constraints and ACP reasoning state machine transitions (Explore → Shape → Evidence → Operate, plus allowed sub-transitions) are decidable over T(ρ) and/or the underlying telemetry.
- **P6 (Assurance functional).** There exists an assurance functional A such that, for any reconstructed run graph T(ρ), A(T(ρ)) yields a reliability score R(ρ), a conformance load CL(ρ), and a vector of assurance gauges G(ρ) (e.g., coverage of sentinels, constraint satisfaction).
- **P7 (Monotonicity of gate-aligned assurance).** Holding models and prompts fixed, if a configuration κ₂ strengthens sentinel/gate policies relative to κ₁ such that all additional constraints are FPF-approved (i.e., they cannot increase the set of violations in C), then for any fixed workload W: R_{κ₂}(ρ) ≥ R_{κ₁}(ρ) and/or some components of G_{κ₂}(ρ) strictly improve, and CL_{κ₂}(ρ) ≤ CL_{κ₁}(ρ) for runs ρ induced by W under κ₁ and κ₂.

Note: P7 is an explicit unpacking of “assurance computed over the sentinel surface using the FPF assurance calculus” into a monotonicity property that the Assurance-Delta prediction implicitly relies on.

## 3. Derived Prediction Family I · Trace-Replay

### 3.1 Target prediction (from ACP-HYP-001)

Given a recorded ACP run with complete sentinel/telemetry, ACP-inspector will either construct a unique E.TGA-compatible transduction graph with PathSlices and GateDecisions, or emit concrete, localized reconstruction failures (e.g., missing pins, illegal crossings, non-conformant state transitions).

### 3.2 Theorem TR-1 (Trace-Replay Totality)

**Statement.** Under P1–P4, for any completed run ρ with complete telemetry, running I on the telemetry of ρ yields exactly one of:

- A unique E.TGA-compatible transduction graph T(ρ), or
- A finite set of localized reconstruction failures, each attached to one or more specific PathSlice and/or GateCrossing events.

**Sketch of proof.** By P1 and P2, for run ρ there exists a total, time-ordered sequence of PathSlices {ps_i} and GateCrossings {g_j} that covers all external actions and inter-gate internal behavior. By P3, the telemetry for {ps_i} and {g_j} is FPF-schema-compatible and thus admits a canonical embedding into a transduction graph representation (nodes for states/slices, edges for GateCrossings/transitions). By P4, the inspector I is a deterministic algorithm over such telemetry; for any complete input it returns either a graph T(ρ) or a reconstruction failure with localized evidence. Determinism (P4) plus fixed input telemetry implies uniqueness of the output. Localized failures follow from P4’s requirement that failures are attached to specific events or subsequences. Therefore TR-1 holds.

### 3.3 Corollary TR-2 (Canonical Trace-Replay Test)

**Statement.** Under P1–P4, a “trace-replay” test suite that generates synthetic or real runs ρₖ with known sentinel and state-transition behavior, records full telemetry for each ρₖ, and runs I to obtain either T(ρₖ) or reconstruction failures is sufficient to test whether ACP-HYP-001’s reconstructability claim holds for those implementations and workloads.

**Justification.** If ACP-HYP-001 is correct, then P1–P4 hold and TR-1 applies. Deviations in the trace-replay tests (e.g., non-localized or spurious failures, non-unique graphs) directly falsify some part of the premise set or the hypothesis implementation.

**Traceability.** This formalizes the Trace-Replay Prediction family as a direct consequence of ACP-HYP-001 plus P1–P4.

## 4. Derived Prediction Family II · Conformance-Detection

### 4.1 Target prediction (from ACP-HYP-001)

For seeded fault-injection runs (e.g., skipped sentinel refresh, illegal state jump in the ACP state machine, hidden or bypassed gate), ACP-inspector will raise at least one specific conformance violation tied to the relevant TGA/FPF checklist item.

### 4.2 Theorem CD-1 (Sound detection of protocol & state violations)

**Statement.** Under P1–P5, for any run ρ of an ACP implementation where the implementation respects P1–P3 (i.e., no hidden external actions beyond sentinel coverage) and the run includes at least one violation of a rule in C (protocol or state-machine), the inspector I, when applied to the telemetry of ρ, will emit at least one conformance violation associated with the violated rule(s) in C.

**Sketch of proof.** By P1–P4, I either reconstructs T(ρ) or localizes reconstruction failure. By assumption, the implementation and run respect P1–P3, so reconstruction succeeds and yields T(ρ). By P5, violations of C are decidable over T(ρ)/telemetry. The run ρ contains at least one such violation. Therefore, the conformance checker within I must identify at least one violating event or path and report it as a violation tied to a specific rule in C. Therefore CD-1 holds.

### 4.3 Theorem CD-2 (Detection of sentinel/gate bypass)

Now consider the case where the seeded fault breaks P1 (e.g., a hidden or bypassed gate) rather than introducing a violation within a P1-P3-respecting implementation.

**Statement.** Under P2–P5, for any run ρ’ that differs from a P1-satisfying run only by the presence of external actions with no corresponding GateCrossing telemetry, either I fails to reconstruct T(ρ’) and emits localized reconstruction failures at the point of bypass, or I reconstructs T(ρ’) and emits at least one violation of a rule in C that encodes required sentinel coverage.

**Sketch of proof.** Hidden/bypassed actions violate P1, so the telemetry set is inconsistent with the assumed coverage model. Inconsistency manifests as missing GateCrossing events for observed state changes and/or external effects, or mismatches between expected and observed ACP state transitions. By P2–P4, the reconstruction algorithm I will either fail locally (e.g., cannot reconcile state changes with available GateCrossings) or produce a graph with “holes” that must be checked against conformance rules. By P5, C can include rules of the form “every external action must map to a GateCrossing,” so the absence of such events in the reconstructed structure is a rule violation. Thus, even when P1 is violated by construction, I produces either reconstruction failures or C-linked violations at the bypass locus.

### 4.4 Corollary CD-3 (Canonical fault-injection test suite)

**Statement.** Under P1–P5, a fault-injection test suite that injects known protocol and state-machine violations within a P1-respecting implementation, injects violations that break P1 (skipped sentinel refresh, hidden gate, etc.), and records outputs from I is sufficient to test ACP-HYP-001’s claim that conformance violations and sentinel assumptions are inspectable from telemetry.

**Justification.** Case (1) is covered by CD-1. Case (2) is covered by CD-2. Failure to detect either class of seeded fault falsifies some combination of P1–P5 and thus the operative content of ACP-HYP-001.

**Traceability.** This formalizes the Conformance-Detection Prediction family as a consequence of ACP-HYP-001 plus P1–P5.

## 5. Derived Prediction Family III · Assurance-Delta

### 5.1 Target prediction (from ACP-HYP-001)

When the ACP implementation is hardened (e.g., additional sentinel rules, stricter gates) without changing prompts/models, ACP-inspector’s computed assurance report for a fixed workload will show increased R and/or improved assurance gauges, reduced CL penalties, with the delta attributable to the gate/sentinel layer rather than agent internals.

### 5.2 Theorem AD-1 (Assurance functional over sentinel surface)

**Statement.** Under P1–P3 and P6, for any run ρ, the assurance triple (R(ρ), CL(ρ), G(ρ)) computed by A is a function of the reconstructed transduction graph T(ρ) (per ACP-HYP-001). Therefore, any change to sentinel/gate policies that changes only the set of allowed GateDecisions and ConstraintViolations (holding models/prompts fixed) must manifest solely through changes in T(ρ) at the sentinel surface and hence in (R, CL, G).

**Sketch of proof.** ACP-HYP-001 states that assurance summaries are computed over reconstructed runs using FPF calculus, not agent internals. P6 formalizes this as a functional A(T(ρ)). Strengthening sentinel policies while holding models/prompts fixed affects which external actions are allowed/blocked and which ConstraintViolations are raised. These changes appear as differences in GateDecisions and violation annotations in telemetry, hence in T(ρ). Since A is defined on T(ρ), any change in assurance arises from changes at the sentinel surface, not opaque agent internals. Therefore AD-1 holds.

### 5.3 Theorem AD-2 (Monotone Assurance-Delta under hardened gates)

**Statement.** Under P1–P3, P6, and P7, for two ACP configurations κ₁ and κ₂ where models and prompts are identical, sentinel/gate policies in κ₂ are strictly stronger than in κ₁ and FPF-approved, and for a fixed workload W, each configuration induces runs {ρᵢ^{(1)}} and {ρᵢ^{(2)}} over the same tasks, the following holds for each matched task/run pair (ρ^{(1)}, ρ^{(2)}): R(ρ^{(2)}) ≥ R(ρ^{(1)}) and/or some gauge components in G(ρ^{(2)}) improve, and CL(ρ^{(2)}) ≤ CL(ρ^{(1)}).

**Sketch of proof.** By AD-1 and P6, assurance metrics are a function of T(ρ) and therefore of sentinel decisions and constraint satisfaction. By P7, the assurance calculus is defined so that strengthening FPF-approved gate constraints cannot worsen the relevant assurance metrics, and typically improves them when it eliminates violations or increases effective coverage. Holding models/prompts fixed eliminates confounding effects from agent internals. For each task and workload element, the difference between κ₁ and κ₂ is solely encoded in the sentinel surface behavior and thus in T(ρ^{(1)}) vs T(ρ^{(2)}). Applying P7, we obtain the stated inequalities for R, CL, and G. Therefore AD-2 holds.

### 5.4 Corollary AD-3 (Canonical A/B assurance test)

**Statement.** Under P1–P3, P6, and P7, an A/B test that fixes models/prompts and workload W, compares an ACP configuration with baseline sentinel/gate policies κ₁ against a hardened configuration κ₂, runs I on all logs to reconstruct T(ρ), and then applies A must, if ACP-HYP-001 and P7 are correct, exhibit non-negative assurance deltas attributable to the sentinel/gate layer in the sense of AD-2.

**Traceability.** This formalizes the Assurance-Delta Prediction family as a consequence of ACP-HYP-001 plus P1–P3, P6, and P7.

## 6. Prediction Catalog (For Evidence & Experiment Design)

For use by later Evidence/Induction artifacts, we summarize the derived predictions with their premise dependencies.

| Id  | Family               | Informal description                                                             | Premises          |
| --- | -------------------- | ------------------------------------------------------------------------------- | ----------------- |
| TR-1 | Trace-Replay         | Inspector reconstructs a unique E.TGA graph or localized failure for any fully logged run. | P1–P4             |
| TR-2 | Trace-Replay         | Trace-replay tests over logs suffice to evaluate reconstructability.           | P1–P4             |
| CD-1 | Conformance-Detection | Violations of protocol/state rules in C are detected as violations over reconstructed runs. | P1–P5             |
| CD-2 | Conformance-Detection | Sentinel/gate bypass produces either reconstruction failure or explicit violation of coverage rules. | P2–P5 (+¬P1)      |
| CD-3 | Conformance-Detection | Fault-injection suites (within P1 and breaking P1) suffice to test conformance-detection claims. | P1–P5             |
| AD-1 | Assurance-Delta      | Assurance metrics depend only on the reconstructed run surface, not internal agent logs. | P1–P3, P6         |
| AD-2 | Assurance-Delta      | Hardened FPF-approved gates produce monotone non-negative assurance deltas for fixed workloads. | P1–P3, P6–P7      |
| AD-3 | Assurance-Delta      | A/B tests over gate policies and fixed workloads are sufficient to measure assurance deltas. | P1–P3, P6–P7      |

These entries are the “deductive hooks” that experiment and implementation artifacts should reference when designing tests.

## 7. Traceability & Storage

Upstream linkage:

- ACP-ANOM-001 (Anomaly)
- ACP-HYP-001 (Prime hypothesis)

Downstream linkage (intended):

- Test design artifacts instantiating TR-family, CD-family, AD-family predictions.
- ACP-inspector implementation notes, referencing specific premises (P1–P7) they rely on.

Recommended file placement:

- Human-readable: episteme/ACP-DED-001-sentinel-surface-inspectability-deduction.md
- Optional machine-readable mirror (JSON/YAML) containing: id, type, assurance_level, state, hypothesis_ref, premises[], theorems[], predictions[], created_at, updated_at.

**Deduction summary (compact)**

- Assumptions: Sentinel coverage & FPF telemetry (P1–P3), deterministic reconstruction (P4), decidable conformance rules (P5), assurance functional & monotonicity (P6–P7).
- Model: ACP runs as E.TGA graphs reconstructed from sentinel-surface telemetry; inspector I + assurance functional A operate only on this surface.
- Options: (i) Treat predictions as empirical only; (ii) Ground them as explicit theorems from P1–P7 (chosen).
- Pick: Explicit theoremization (TR, CD, AD families) to make test design and falsification straightforward.
- Tests: Trace-replay suites, protocol/state fault-injection, sentinel-bypass scenarios, A/B gate-hardening experiments.
- Risks: Hidden channels violating P1, mis-specified rule set C, assurance calculus that violates P7; these would break the deductions.
- Next: Author Evidence/Experiment artifacts keyed to TR-, CD-, AD-* and implement telemetry & inspector features that make P1–P7 true (or explicitly relax them).
