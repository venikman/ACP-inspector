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

**Conceptual Core**

- `vendor/FPF-Spec.md`  
  Vendored FPF specification (source of FPF terminology and patterns).

- `core/UTS.md`  
  Unified Term Sheet for ACP concepts (per F.17).

- `core/episteme/ACP-HYP-001-sentinel-surface-inspectability.md`  
  Hypothesis about deriving assurance from a "sentinel surface".

- `core/episteme/ACP-DED-001-sentinel-surface-inspectability-deduction.md`  
  Deductive consequences and testable theorems derived from ACP-HYP-001.

**Tooling Reference**

- `src/Acp.Domain.fs`  
  F# domain model: protocol versions, sessions, capabilities, messages, tool calls, etc.

- `src/Acp.Protocol.fs`  
  F# state machine describing per-connection lifecycle (initialize -> sessions -> prompt turns -> updates -> cancel).

- `src/Acp.Validation.fs`  
  Sentinel surface: validation lanes/findings, protocol-error bridge, and `runWithValidation` helper that yields trace + findings (optionally continuing after errors).

- `src/Acp.RuntimeAdapter.fs`  
  Integration boundary for runtimes: `validateInbound` / `validateOutbound` apply profile-aware validation at decode/encode edges.

- `tooling/docs/acp-fsharp-mvp.md`  
  Narrative description of the MVP domain model and protocol state machine.

- `tooling/docs/runtime-integration.md`  
  Adapter contract for runtimes: where to call validation, lane semantics, JS/TS mirroring guidance.
- `tooling/docs/error-reporting.md`  
  Problem Details (RFC 9457) error surface with ACP/FPF extensions and telemetry hooks.

- `tooling/docs/project-rules.md`  
  Repo conventions: lanes/labels, branch naming, PR checklist, testing minima.

- `tooling/docs/AGENT_ACP_SENTINEL.md`  
  Agent / sentinel playbook: three-holon structure, working style, F# constraints, validation rules.

**Pedagogical Companion**

- `pedagogy/ACP-Explained.md`  
  Non-technical explanation of ACP, with an exec-ready summary.

- `pedagogy/docs/evals/Reusable Evaluation Patterns.md`  
  User-Trace-Spec evaluation patterns for AI coding agents and ACP-based systems.

- `pedagogy/CODING.md`  
  Coding codex for AI assistants and contributors working in this repo.

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
