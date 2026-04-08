# FPF Alignment Evaluation Report

**Date**: 2026-03-19  
**Evaluator**: Codex + local repo review  
**FPF Source**: March 2026 local `FPF-Spec.md` provided during this review  
**ACP Inspector Version**: working tree after ACP stable parity refresh  
**Evaluation Scope**: boundary discipline, multi-view publication, language-state handling, and agent-tool coordination

## Executive Summary

ACP Inspector remains strongly aligned with the core FPF architectural stance: bounded contexts are real, bridges are explicit, and the protocol/runtime/validation split is coherent. The main remaining gaps are no longer about whether the repo uses FPF language at all; they are about whether the repo routes claims, publications, and evolution states as rigorously as the March 2026 FPF now expects.

**Current assessment**: strong architectural fit, partial methodological fit.

## Method Update

This refresh supersedes the January 2026 snapshot. The current evaluation is grounded against the locally provided March 2026 FPF source and focuses on the patterns that materially change the repo roadmap:

- `A.6` Signature Stack & Boundary Discipline
- `E.17` Multi-view / publication discipline
- `A.16` Language-state transduction coordination
- `C.24` Agent-tool coordination

The repo is now also evaluated against the current ACP stable contract rather than the January ACP snapshot.

## Gap Matrix

| Pattern | State | Current repo position | Main gap |
| ------- | ----- | --------------------- | -------- |
| `A.6` | partially modeled | Bounded contexts, invariants, and bridges exist; some boundary claims are already separated in protocol and assurance docs | Boundary claims still mix laws, admissibility gates, commitments, and evidence statements in the same prose blocks |
| `A.6.B` | partially modeled | Assurance invariants and protocol state-machine rules already behave like law/gate surfaces | The repo does not yet route all protocol-facing claims through a visible Boundary Norm Square |
| `A.6.C` | missing | Contract-like language exists around assurance, capabilities, and evolution | Promise content, commitments, utterances, and evidence are not unpacked explicitly |
| `A.6.P` | documented only | The repo warns about semantic drift and context-local meaning | It does not yet carry a disciplined precision-restoration workflow for overloaded boundary language |
| `E.17` | partially modeled | CLI output, reports, parity trackers, and trace analyses already act as multiple publication faces | The repo does not state clearly enough that these are views over the same protocol artifacts and may not add new semantics |
| `A.16` | documented only | The repo already distinguishes stable, unstable, and draft ACP surfaces in practice | It lacks an explicit language-state ladder and lawful move rules for draft/stable/deprecated/retired publications |
| `C.24` | partially modeled | Tool calls, plan updates, slash-command surfaces, and trace validation already exist in ACP | `DRR-005` still proposes a shadow planning protocol instead of treating ACP-native `plan` and slash-command surfaces as canonical |

## Boundary / Publication Findings

### 1. Boundary claims are still mixed

Across the assurance and evolution docs, the repo often combines:

- laws / invariants
- admissibility gates
- commitments / duties
- evidence / work-effects

in a single sentence or table row. This is workable for human reading, but it is not yet `A.6.B` clean.

### 2. Publication faces are present but under-described

The repo already publishes the same underlying protocol artifacts through:

- trace files
- CLI inspect / replay / analyze views
- parity and tracker reports
- validation findings

That is already an `E.17` style multi-view setup, but the docs do not yet say the crucial part: these views are projections and must not mint new normative meaning.

### 3. Draft ACP support needs a language-state policy

The repo now distinguishes ACP stable and unstable features in practice, but the discipline is still narrative rather than explicit. Without an `A.16` ladder, unstable support can leak into parity claims and make the repo look more stable than it really is.

### 4. Agent-tool coordination should not become a shadow protocol

ACP already has first-class surfaces for:

- `plan`
- slash commands / available commands
- tool-call traces

The old January DRR for tool coordination proposed a custom `ToolCallPlan` object as the primary mechanism. Under the current FPF reading, that overreaches the boundary and risks violating both `A.6` and `E.17`.

## Recommended Roadmap

### `A.6`: route boundary claims explicitly

- Rewrite protocol-facing docs so each major claim is classified as one of:
  - law / invariant
  - admissibility gate
  - commitment / deontic
  - evidence / work-effect
- Treat assurance envelopes, findings, and reports as publication surfaces rather than the underlying work/evidence objects themselves.

### `E.17`: define view families explicitly

- State that CLI output, replay summaries, parity reports, and trace analyses are views over the same protocol artifacts.
- Add a standing rule that no view may add new protocol semantics.

### `A.16`: publish a stable language-state ladder

- Add `draft/unstable`, `stable`, `deprecated`, and `retired` as explicit publication states for ACP features and local docs.
- Require cited lineage and rationale for every move between those states.

### `C.24`: align coordination to ACP-native surfaces

- Rework `DRR-005` so typed plan updates, available slash commands, and trace/tool-call consistency are the canonical coordination surfaces.
- Keep heuristic scheduling, budget, or exploration policies as optional policy metadata or profile-level guidance, not as a parallel protocol contract.

## Net Effect On The Repo

After this refresh, the repo should be described as:

- **implemented in code** for ACP runtime and validation surfaces
- **partially modeled** for boundary routing and multi-view publication
- **documented only** for language-state governance
- **missing** only where the repo still invents a custom surface instead of using ACP-native protocol structure

That framing matches the current roadmap better than the old “85-90% alignment” summary, which overstated methodological completion.
