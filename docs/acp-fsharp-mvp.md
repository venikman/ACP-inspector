# ACP F# Domain Model (MVP)

This module provides a *design-time* F# domain model and a small state machine for the Agent Client Protocol (ACP).  
It focuses on initialization, session setup, prompt turns, updates, cancellation, and permission requests. :contentReference[oaicite:33]{index=33}

## Scope

Covered:

- `initialize` request/response, including client and agent capabilities :contentReference[oaicite:34]{index=34}  
- `session/new` and `session/load` (+ session IDs and basic MCP server wiring) :contentReference[oaicite:35]{index=35}  
- `session/prompt` and `stopReason` (`EndTurn`, `MaxTokens`, `MaxTurnRequests`, `Refusal`, `Cancelled`) :contentReference[oaicite:36]{index=36}  
- `session/update` as a typed union (plan entries, message chunks, tool call updates, status text) :contentReference[oaicite:37]{index=37}  
- `session/cancel` and the requirement to return `Cancelled` for cancelled turns :contentReference[oaicite:38]{index=38}  
- `session/request_permission` with typed permission options and tool call context :contentReference[oaicite:39]{index=39}  

Intentionally *not* modeled yet:

- Authentication (`authenticate`)
- Full content block surface (images, audio, embedded context, rich resources)
- Full MCP configuration surface
- Terminal / filesystem JSON-RPC methods
- Error codes and JSON-RPC request IDs
- `_meta` pass-through preservation and explicit opacity rules
- Transport framing defaults (UTF-8, LF, message size/timeout guardrails)

All of those can be added as more record/union cases that stay inside the same design.

### Near-term required adds

- ACP/JSON-RPC error code map (single source, surfaced in validation when unknown/unsupported). // TODO: needs spec verification (ACP version pin)
- `_meta` handling: preserve on encode/decode, never use for validation unless a rule opts in.
- Transport defaults: document stdio framing assumptions (UTF-8, LF) and max payload guidance for the runtime layer.
- Testing minima: every new type gets a round-trip test; every new state transition gets valid + invalid path coverage; every new sentinel rule gets an example-based test that asserts expected `ValidationFinding` output.

## Design

### 1. Domain layer (`Acp.Domain`)

- `ProtocolVersion`, `SessionId`, `StopReason` represent stable protocol primitives.
- Capabilities are explicit records: `ClientCapabilities`, `AgentCapabilities`, `McpCapabilities`, `PromptCapabilities`.
- Method payloads are modeled as records:
  - `InitializeParams` / `InitializeResult`
  - `NewSessionParams` / `NewSessionResult`
  - `LoadSessionParams` / `LoadSessionResult`
  - `SessionPromptParams` / `SessionPromptResult`
  - `SessionUpdateNotification`
  - `SessionCancelParams`
  - `RequestPermissionParams`
- Messages are direction-tagged:
  - `ClientToAgentMessage`
  - `AgentToClientMessage`
  - `Message = FromClient | FromAgent`

JSON-RPC 2.0 and raw JSON handling are intentionally out of scope; another layer converts raw messages into these types. :contentReference[oaicite:40]{index=40}

### 2. Spec / state machine (`Acp.Protocol`)

The `Spec<'phase,'message>` type is minimal:

```fsharp
type Spec<'phase,'message> =
  { initial : 'phase
    step    : 'phase -> 'message -> Result<'phase, ProtocolError> }
```
