# ACP-sentinel

ACP-sentinel is an F# implementation of the Agent Client Protocol (ACP) plus a validation / sentinel layer.

It aims to give you:

- **Protocol holon**: pure F# types and rules that model "what ACP means".
- **Runtime holon**: transports, clients, and agents (IO, processes, stdio wiring).
- **Sentinel holon**: stateful validation and assurance over ACP traffic.

Normative behavior follows the published ACP spec and schema. This repo adds a First Principles Framework (FPF) view on assurance and observability.

---

## FPF anchor

The repo uses FPF concepts and artifacts:

- `FPF-Spec.md` - vendored FPF conceptual spec (read-only).
- `core/UTS.md` - Unified Term Sheet (F.17) giving a single table that unifies ACP / JSON-RPC / project concepts.
- `core/episteme/ACP-HYP-001-sentinel-surface-inspectability.md` and `core/episteme/ACP-DED-001-sentinel-surface-inspectability-deduction.md` - U.Episteme + U.Deduction pair for "sentinel-surface inspectability".

We align with the E.4 **Artefact Architecture**:

- **Conceptual Core** - definitions, UTS, episteme/deduction.
- **Tooling Reference** - F# domain model, protocol state machine, agent/sentinel playbook.
- **Pedagogical Companion** - ACP explained, evaluation patterns, coding codex.

---

## Repository layout

**Holon map (top level)** — see also `tooling/docs/repo-map.md` for a printable map.

- Holon 1 · Conceptual Core → `vendor/` (FPF spec), `core/` (UTS, episteme).
- Holon 2 · Tooling Reference → `src/` (domain/protocol/runtime/validation), `tooling/` (implementation-facing docs/playbooks), `tests/` (executable evidence and golden cases), `core/roadmap/` (+ submodule `core/roadmap/sub-ACP`) as implementation slices.
- Holon 3 · Pedagogical Companion → `pedagogy/` (explainers, onboarding, eval patterns).

**Conceptual Core (Holon 1)**

- `vendor/FPF-Spec.md` — vendored FPF specification (terminology + patterns).
- `core/UTS.md` — Unified Term Sheet for ACP concepts (per F.17).
- `core/episteme/*` — hypotheses/deductions (e.g., sentinel-surface inspectability).
- `core/roadmap/*` — roadmap and slice planning (kept in-repo for auditability).

**Tooling Reference (Holon 2)**

- `src/Acp.Domain.fs` — F# domain model: protocol versions, sessions, capabilities, messages, tool calls, etc.
- `src/Acp.Protocol.fs` — protocol state machine (initialize → sessions → prompt turns → updates → cancel).
- `src/Acp.Validation.fs` — validation lanes/findings, protocol-error bridge, `runWithValidation` helper.
- `src/Acp.RuntimeAdapter.fs` — runtime boundary: `validateInbound`/`validateOutbound` with profile-aware checks.
- `tooling/docs/acp-fsharp-mvp.md` — MVP domain model and state machine narrative.
- `tooling/docs/runtime-integration.md` — adapter contract, lane semantics, JS/TS mirroring guidance.
- `tooling/docs/error-reporting.md` — Problem Details (RFC 9457) surface with ACP/FPF extensions and telemetry hooks.
- `tooling/docs/project-rules.md` — repo conventions: lanes/labels, branch naming, PR checklist, testing minima.
- `tooling/docs/AGENT_ACP_SENTINEL.md` — agent/sentinel playbook: holons, working style, validation rules.
- `tests/` — protocol/runtime/sentinel tests and `golden/` fixtures.

**Pedagogical Companion (Holon 3)**

- `pedagogy/ACP-Explained.md` — non-technical explanation of ACP with exec-ready summary.
- `pedagogy/docs/evals/Reusable Evaluation Patterns.md` — UTS-style evaluation patterns for AI coding agents.
- `pedagogy/CODING.md` — coding codex for assistants/contributors working in this repo.

---

## How to work in this repo

- **Treat the ACP spec as normative.**  
  If this repo and the published spec disagree, the spec wins; open an issue and tag the discrepancy.

- **Keep holons separate.**  
  Avoid mixing protocol types, runtime IO concerns, and sentinel rules in the same module.

- **Prefer idiomatic F#.**  
  Use discriminated unions and records, composition over inheritance, and Result-based error handling.

- **Document spec grey areas.**  
  When the ACP spec is ambiguous, document assumptions in comments and mark them for later verification.

For more detailed rules, see:

- `tooling/docs/AGENT_ACP_SENTINEL.md` - architectural playbook.
- `pedagogy/CODING.md` - assistant and contributor guardrails.
