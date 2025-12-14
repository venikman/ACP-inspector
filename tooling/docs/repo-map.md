# Repository map (holons → folders)

Purpose: give newcomers a one-glance map of where things live, why, and when to touch them. Mirrors the holon split used throughout ACP-sentinel.

## Holon 1 — Conceptual Core

- `vendor/FPF-Spec.md` — vendored upstream FPF spec; source of terminology/patterns (read-only).
- `core/UTS.md` — Unified Term Sheet (F.17) for ACP concepts.
- `core/episteme/` — hypotheses, deductions, decision chains (e.g., sentinel-surface inspectability).

Touch when: updating ontology, adding rows to UTS, or recording hypotheses/decisions.

## Holon 2 — Tooling Reference

- `src/` — executable F# code: domain, protocol state machine, validation, runtime adapter.
- `tooling/docs/` — guidance and playbooks (runtime integration, error surface, project rules, protocol notes, agent/sentinel playbook, explainers, eval patterns).
- `tests/` — executable evidence: protocol/runtime/sentinel tests and `golden/` fixtures.
- `core/roadmap/` (+ spec submodule `core/roadmap/sub-ACP` and `core/roadmap/ACP-slice-01.md`) — roadmap and spec parity tracking; treated as implementation slices.

Touch when: implementing protocol/runtime/validation changes, adjusting contributor rules, adding tests/goldens, updating roadmap slices, or updating explainers/eval playbooks.

## Quick rules of thumb

- Keep holons separate: don’t mix runtime code with ontology files.
- Add docs next to the holon they serve (implementation docs in `tooling/docs`, ontology/terms in `core/`).
- Roadmap stays in `core/roadmap` so planning is reviewable like code.
