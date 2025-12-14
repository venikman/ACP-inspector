# Repository map (holons → folders)

Purpose: give newcomers a one-glance map of where things live, why, and when to touch them. Mirrors the three-holon split used throughout ACP-sentinel.

## Holon 1 — Conceptual Core

- `vendor/FPF-Spec.md` — vendored upstream FPF spec; source of terminology/patterns (read-only).
- `core/UTS.md` — Unified Term Sheet (F.17) for ACP concepts.
- `core/episteme/` — hypotheses, deductions, decision chains (e.g., sentinel-surface inspectability).

Touch when: updating ontology, adding rows to UTS, or recording hypotheses/decisions.

## Holon 2 — Tooling Reference

- `src/` — executable F# code: domain, protocol state machine, validation, runtime adapter.
- `tooling/docs/` — implementation-facing guidance (runtime integration, error surface, project rules, protocol notes, agent/sentinel playbook).
- `tests/` — executable evidence: protocol/runtime/sentinel tests and `golden/` fixtures.
- `core/roadmap/` (+ spec submodule `core/roadmap/sub-ACP` and `core/roadmap/ACP-slice-01.md`) — roadmap and spec parity tracking; treated as implementation slices.

Touch when: implementing protocol/runtime/validation changes, adjusting contributor rules, adding tests/goldens, or updating roadmap slices.

## Holon 3 — Pedagogical Companion

- `pedagogy/` — explainers, onboarding guides, eval patterns, contributor codex.

Touch when: improving teaching material, onboarding notes, or evaluation playbooks.

## Quick rules of thumb

- Keep holons separate: don’t mix runtime code with pedagogy or ontology files.
- Add docs next to the holon they serve (runtime docs in `tooling/docs`, not `pedagogy/`).
- Roadmap stays in `core/roadmap` so planning is reviewable like code.
