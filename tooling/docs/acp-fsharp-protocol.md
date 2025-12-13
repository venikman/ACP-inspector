# ACP F# Protocol Model (spec parity)

- ArtifactId: ACP-FSHARP-PROTOCOL-001
- Family: ToolingReference
- Type: U.MethodDescription (Design-time protocol model)
- Scope: Pinned ACP spec (`v0.10.2`)
- Status: Slice‑01 (protocol completeness)

This document describes how the ACP-sentinel F# model maps to the **pinned ACP spec** and how to keep implementation, validation, and tests aligned.

Spec sources (normative):
- GitHub (source of truth): https://github.com/agentclientprotocol/agent-client-protocol
- Overview/intro: https://agentclientprotocol.com/overview/introduction

Local spec pin:
- `core/roadmap/sub-ACP` (git submodule) @ `v0.10.2` (`8e3919b7ab494cf2d19326e2e17c5e7aeb15e366`)

Completeness tracking:
- `core/roadmap/ACP-spec-parity-matrix.md`

## Scope

Slice‑01 targets **full protocol parity** with the pinned spec.

The current codebase already models and validates a core subset (initialize + sessions + prompting + updates + cancel + permission requests). Everything missing or partial is tracked explicitly in the parity matrix (no “intentionally not modeled” items for slice‑01).

## Validation harness (current)

- `Acp.Validation.runWithValidation` executes `Acp.Protocol.spec`, returning `finalPhase`, `SessionTrace`, and a `ValidationFinding list`; pass `?stopOnFirstError=false` to keep folding after an error.
- `Validation.FromProtocol` assigns subjects (`Session`, `MessageAt`, `Connection`) and trace indices so findings anchor to sessions and message positions.
- `Validation.PromptOutcome.classify` derives `PromptTurnOutcome` from the trace/final phase, distinguishing user cancel vs protocol/domain errors.

## Design

### 1) Domain layer (`Acp.Domain`)

- Primitives: `ProtocolVersion`, `SessionId`, `StopReason`.
- Capabilities: `ClientCapabilities`, `AgentCapabilities`, `McpCapabilities`, `PromptCapabilities`.
- Method payloads are modeled as records and discriminated unions under `Domain.*`.
- Messages are direction-tagged:
  - `ClientToAgentMessage`
  - `AgentToClientMessage`
  - `Message = FromClient | FromAgent`

JSON-RPC 2.0 framing and raw JSON live in a separate envelope layer; that layer converts decoded JSON-RPC messages into `Domain.Messaging.Message` values.

### 2) Protocol state machine (`Acp.Protocol`)

The protocol spec is encoded as one pure transition function plus an initial phase:

```fsharp
type Spec<'phase,'message,'error> =
  { initial : 'phase
    step    : 'phase -> 'message -> Result<'phase,'error> }
```

`Acp.Protocol.spec : Spec<Phase, Message, ProtocolError>` enforces ordering/invariants for the pinned spec (expanded over time as parity progresses).

### 3) Validation (`Acp.Validation` + `Acp.RuntimeAdapter`)

- `Acp.Validation` converts protocol errors and rule checks into stable `ValidationFinding`s (lane + severity + code + subject anchor).
- `Acp.RuntimeAdapter.validateInbound/validateOutbound` are the boundary where decoded/encoded messages are checked before dispatch or send.

## Slice‑01 implementation rules (protocol-first)

- Do not add new protocol surface without updating the parity matrix row(s).
- Every new protocol method/field must include at least one executable test (unit, golden transcript, or PBT property).
- Never invent ACP fields/methods: when unsure, mark `// TODO: needs spec verification (ACP v0.10.2, section …)` and link to the spec source.
