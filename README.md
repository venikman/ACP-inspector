# ACP-sentinel

ACP-sentinel is an F# implementation of the Agent Client Protocol (ACP) plus a validation / sentinel layer.

It aims to give you:

- **Protocol holon**: pure F# types and rules that model "what ACP means".
- **Runtime holon**: transports, clients, and agents (IO, processes, stdio wiring).
- **Sentinel holon**: stateful validation and assurance over ACP traffic.

Normative behavior follows the published ACP spec and schema:

- Spec source of truth (GitHub): https://github.com/agentclientprotocol/agent-client-protocol
- Overview/intro (website): https://agentclientprotocol.com/overview/introduction
- Local spec pin (submodule): `core/roadmap/sub-ACP` @ `v0.10.2` (`8e3919b7ab494cf2d19326e2e17c5e7aeb15e366`)

This repo adds a First Principles Framework (FPF) view on assurance and observability.

## Who benefits (and how)

- **Agent platform teams** — drop-in ACP reference that keeps protocol drift low while you ship fast; the sentinel layer catches malformed turns before they hit production.
- **Runtime integrators** — `Acp.RuntimeAdapter` bridges ACP to your process/stdio boundary; JS/TS mirroring guidance lives in `tooling/docs/runtime-integration.md` so you can keep polyglot stacks aligned.
- **Risk, SRE, and governance** — validation lanes plus golden tests (`tests/`) give repeatable evidence for change control, regressions, and incident postmortems.
- **Enterprise engineering & compliance** — typed protocol core + auditable validation findings reduce vendor risk, ease security reviews, and support regulated change windows.
- **Applied AI researchers & prototypers** — a fully typed F# core and UTS let you explore ACP variants with safety rails and auditable deductions.
- **Educators & onboarding leads** — exec-friendly explainers and evaluation patterns live in `tooling/docs/` (see `tooling/docs/acp-explained.md` and `tooling/docs/evals/reusable-evaluation-patterns.md`).

### Typical scenarios

- You need to enforce ACP correctness at the IO boundary of an LLM agent runner and want ready-made `validateInbound` / `validateOutbound` gates.
- You’re adding a new tool/capability to an ACP agent and want protocol-safe fixtures plus validation findings instead of hand-rolled checks.
- You’re mirroring ACP into another language/runtime and need a canonical model + tests to prevent semantic drift.
- You’re onboarding teammates or stakeholders and need short, high-signal explainers that match the implementation.

## 60-second getting started

1. Prereqs: .NET 9 SDK. From repo root: `dotnet build src/ACP.fsproj` (restores + builds).
2. Quick probe via F# Interactive (from `src/` after build):

```fsharp
#r "bin/Debug/net9.0/ACP.dll"
open Acp
open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Capabilities
open Acp.Domain.Messaging
open Acp.RuntimeAdapter

let session = SessionId "demo-001"

let inbound =
    RuntimeAdapter.validateInbound session None
        { rawByteLength = None
          message =
            Message.FromClient(
                ClientToAgentMessage.Initialize
                    { protocolVersion = 1
                      clientCapabilities =
                        { fs = { readTextFile = true; writeTextFile = false }
                          terminal = false }
                      clientInfo = None }) }
        false

let outbound =
    RuntimeAdapter.validateOutbound session None
        { rawByteLength = None
          message =
            Message.FromAgent(
                AgentToClientMessage.InitializeResult
                    { negotiatedVersion = 1
                      agentCapabilities =
                        { loadSession = true
                          mcpCapabilities = { http = false; sse = false }
                          promptCapabilities = { audio = false; image = false; embeddedContext = false } }
                      agentInfo = None }) }
        false

printfn "Inbound findings: %A" inbound.findings
printfn "Outbound findings: %A" outbound.findings
```

3. Want to wire it into a runtime? See [`tooling/docs/runtime-integration.md`](tooling/docs/runtime-integration.md). Need an exec-friendly explainer for stakeholders? See [`tooling/docs/acp-explained.md`](tooling/docs/acp-explained.md).

### Running tests with a TRX report

```bash
# from repo root
tooling/scripts/run-tests.sh
# writes tests/TestResults.trx and prints the console summary
```

If `DOTNET_BIN` is unset, scripts use `dotnet` on PATH. If `DOTNET_BIN` is set but can't run the target framework(s), the script falls back to `dotnet` on PATH and prints a warning.

---

## FPF anchor

The repo uses FPF concepts and artifacts:

- `FPF-Spec.md` - vendored FPF conceptual spec (read-only).
- `core/UTS.md` - Unified Term Sheet (F.17) giving a single table that unifies ACP / JSON-RPC / project concepts.
- `core/episteme/ACP-HYP-001-sentinel-surface-inspectability.md` and `core/episteme/ACP-DED-001-sentinel-surface-inspectability-deduction.md` - U.Episteme + U.Deduction pair for "sentinel-surface inspectability".

We align with the E.4 **Artefact Architecture**:

- **Conceptual Core** - definitions, UTS, episteme/deduction.
- **Tooling Reference** - F# domain model, protocol state machine, agent/sentinel playbook, explainers and eval patterns.

---

## Repository layout

**Holon map (top level)** — see also `tooling/docs/repo-map.md` for a printable map.

- Holon 1 · Conceptual Core → `vendor/` (FPF spec), `core/` (UTS, episteme).
- Holon 2 · Tooling Reference → `src/` (domain/protocol/runtime/validation), `tooling/` (implementation-facing docs/playbooks), `tests/` (executable evidence and golden cases), `core/roadmap/` (+ submodule `core/roadmap/sub-ACP`) as implementation slices.

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
- `tooling/docs/acp-fsharp-protocol.md` — protocol model and state machine narrative (spec parity).
- `tooling/docs/runtime-integration.md` — adapter contract, lane semantics, JS/TS mirroring guidance.
- `tooling/docs/error-reporting.md` — Problem Details (RFC 9457) surface with ACP/FPF extensions and telemetry hooks.
- `tooling/docs/project-rules.md` — repo conventions: lanes/labels, branch naming, PR checklist, testing minima.
- `tooling/docs/AGENT_ACP_SENTINEL.md` — agent/sentinel playbook: holons, working style, validation rules.
- `tooling/docs/acp-explained.md` — non-technical explanation of ACP with exec-ready summary.
- `tooling/docs/evals/reusable-evaluation-patterns.md` — UTS-style evaluation patterns for AI coding agents.
- `tooling/docs/coding-codex.md` — coding codex for assistants/contributors working in this repo.
- `tests/` — protocol/runtime/sentinel tests and `golden/` fixtures.

---

## How to work in this repo

- **Treat the ACP spec as normative.**  
  If this repo and the published spec disagree, the spec wins; open an issue and tag the discrepancy.
  - Spec source of truth (GitHub): https://github.com/agentclientprotocol/agent-client-protocol
  - Overview/intro (website): https://agentclientprotocol.com/overview/introduction

- **Keep holons separate.**  
  Avoid mixing protocol types, runtime IO concerns, and sentinel rules in the same module.

- **Prefer idiomatic F#.**  
  Use discriminated unions and records, composition over inheritance, and Result-based error handling.

- **Document spec grey areas.**  
  When the ACP spec is ambiguous, document assumptions in comments and mark them for later verification.

For more detailed rules, see:

- `tooling/docs/AGENT_ACP_SENTINEL.md` - architectural playbook.
- `tooling/docs/coding-codex.md` - assistant and contributor guardrails.
