# ACP-sentinel

ACP-sentinel is an F# implementation of the Agent Client Protocol (ACP) plus a validation / sentinel layer.

It aims to give you:

- **Protocol holon**: pure F# types and rules that model "what ACP means".
- **Runtime holon**: transports, clients, and agents (IO, processes, stdio wiring).
- **Sentinel holon**: stateful validation and assurance over ACP traffic.

Normative behavior follows the published ACP spec and schema:

- Spec source of truth (GitHub): https://github.com/agentclientprotocol/agent-client-protocol
- Overview/intro (website): https://agentclientprotocol.com/overview/introduction

Implementation targets:

- ACP schema: `Acp.Domain.Spec.Schema` (currently `0.10.x`)
- Negotiated major `protocolVersion`: `Acp.Domain.PrimitivesAndParties.ProtocolVersion.current` (currently `1`)

This repo adds a First Principles Framework (FPF) view on assurance and observability.

## Who benefits (and how)

- **Agent platform teams** — drop-in ACP reference that keeps protocol drift low while you ship fast; the sentinel layer catches malformed turns before they hit production.
- **Runtime integrators** — `Acp.RuntimeAdapter` bridges ACP to your process/stdio boundary;
- **Risk, SRE, and governance** — validation lanes plus golden tests (`tests/`) give repeatable evidence for change control, regressions, and incident postmortems.
- **Enterprise engineering & compliance** — typed protocol core + auditable validation findings reduce vendor risk, ease security reviews, and support regulated change windows.
- **Applied AI researchers & prototypers** — a fully typed F# core and UTS let you explore ACP variants with safety rails and auditable deductions.

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
                    { protocolVersion = ProtocolVersion.current
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
                    { protocolVersion = ProtocolVersion.current
                      agentCapabilities =
                        { loadSession = true
                          mcpCapabilities = { http = false; sse = false }
                          promptCapabilities = { audio = false; image = false; embeddedContext = false } }
                      agentInfo = None
                      authMethods = [] }) }
        false

printfn "Inbound findings: %A" inbound.findings
printfn "Outbound findings: %A" outbound.findings
```

3. Want to wire it into a runtime?

**Tooling Reference (Holon 2)**

- `src/Acp.Domain.fs` — F# domain model: protocol versions, sessions, capabilities, messages, tool calls, etc.
- `src/Acp.Protocol.fs` — protocol state machine (initialize → sessions → prompt turns → updates → cancel).
- `src/Acp.Validation.fs` — validation lanes/findings, protocol-error bridge, `runWithValidation` helper.
- `src/Acp.RuntimeAdapter.fs` — runtime boundary: `validateInbound`/`validateOutbound` with profile-aware checks.
- `tests/` — protocol/runtime/sentinel tests

## Inspector CLI (apps/ACP.Inspector)

This repo includes a small CLI to decode JSON-RPC ACP traffic, correlate responses, and run the sentinel validation over the full trace.

- Build: `dotnet build apps/ACP.Inspector/ACP.Inspector.fsproj -c Release`
- Run: `dotnet run --project apps/ACP.Inspector/ACP.Inspector.fsproj -- <command> [options]`

Common commands:

- Replay a recorded trace: `replay --trace trace.jsonl`
- Inspect newline-delimited JSON-RPC from stdin: `tap-stdin --direction fromClient --record trace.jsonl`
- WebSocket tap (optional stdin send): `ws --url ws://host/path --stdin-send --record trace.jsonl`
- SSE tap (reads `data:` lines): `sse --url https://host/sse --record trace.jsonl`
- Stdio proxy between two commands: `proxy-stdio --client-cmd "<cmd>" --agent-cmd "<cmd>" --record trace.jsonl`

## Running tests

- All tests: `dotnet test tests/ACP.Tests.fsproj -c Release`
- Optional: only trace replay tests: `dotnet test tests/ACP.Tests.fsproj -c Release --filter FullyQualifiedName~TraceReplay`

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

- **Follow the F# style guide.**
  See `STYLEGUIDE.md` for the canonical conventions used in this repo.

- **Format with Fantomas.**
  This repo uses Fantomas as the formatter (and as a formatting “linter” in `--check` mode):
  - Format: `dotnet tool restore && dotnet fantomas src tests apps`
  - Check only: `dotnet tool restore && dotnet fantomas src tests apps --check`
  - Note: `tests/golden/` is ignored via `.fantomasignore` (it contains intentionally-invalid F# samples).

- **Optional: enable pre-commit auto-formatting.**
  This repo includes a `pre-commit` hook that auto-runs Fantomas and re-stages changes:
  - One-time setup: `git config core.hooksPath .githooks`

- **No Python in this repo.**
  Do not add Python source/config/scripts; this project is .NET-only. (Enforced by tests.)

- **Document spec grey areas.**
  When the ACP spec is ambiguous, document assumptions in comments and mark them for later verification.
