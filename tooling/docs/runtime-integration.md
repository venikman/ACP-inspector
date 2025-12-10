# Runtime Integration Guide (ACP RuntimeAdapter)

## Purpose
Provide a transport-agnostic boundary for runtimes (HTTP/WebSocket/stdio, JS/TS/.NET) to apply ACP profile-aware validation:
- Inbound: after decoding JSON-RPC → `Domain.Message`.
- Outbound: before encoding `Domain.Message` → JSON-RPC.

## API surface (F#)
Module: `Acp.RuntimeAdapter`

- `validateInbound : SessionId -> RuntimeProfile option -> InboundFrame -> bool -> InboundResult`
  - `InboundFrame = { rawByteLength : int option; message : Domain.Messaging.Message }`
  - `stopOnFirstError` (bool) passes through to `Validation.runWithValidation`.
  - Returns `InboundResult = { trace; findings; phase; message }`.

- `validateOutbound : SessionId -> RuntimeProfile option -> OutboundFrame -> bool -> OutboundResult`
  - `OutboundFrame = { rawByteLength : int option; message : Domain.Messaging.Message }`
  - Returns `OutboundResult = { findings; phase; message }`.

## When to call
1) **Inbound**: immediately after decoding a raw frame into `Domain.Message`, before dispatch.
```
let frame = { rawByteLength = Some rawBytes; message = decodedMsg }
let res = RuntimeAdapter.validateInbound sessionId profile frame stopOnFirstError=true
if res.findings |> List.exists (fun f -> f.severity = Severity.Error && f.lane <> Lane.Implementation) then
    // drop / respond with error using findings
else dispatch res.message
```

2) **Outbound**: immediately before encoding a `Domain.Message` to JSON-RPC and sending.
```
let frame = { rawByteLength = None; message = msgToSend }
let res = RuntimeAdapter.validateOutbound sessionId profile frame stopOnFirstError=true
if res.findings |> List.exists (fun f -> f.severity = Severity.Error && f.lane <> Lane.Implementation) then
    // don’t send; surface error
else encodeAndSend res.message
```

## Lane semantics (base profile)
- Protocol: mandatory, gating on `Error`.
- Session: mandatory, gating on `Error`.
- Transport: mandatory, gating on `Error` (size, framing).
- ToolSurface: opt-in, advisory by default.
- Implementation: advisory only.

## Profile fields used today
- `RuntimeProfile.metadata : MetadataPolicy`
  - `Disallow` / `AllowOpaque` / `AllowKinds` – evaluated on `ContentBlock.Other` kinds.
- `RuntimeProfile.transport.maxMessageBytes`
  - Checked at adapter boundary if `rawByteLength` is provided.

## What this does NOT do
- Actual network IO or JSON encoding/decoding.
- Turn grouping or routing; it only validates the provided `Message` sequence.

## Error surface (HTTP runtimes)
- HTTP runtimes MUST surface failures as RFC 9457 `application/problem+json` with the ACP extensions in `tooling/docs/error-reporting.md` (path_id, path_slice_id, assurance_lane, policy_id, sentinel_id, acp_state, etc.).
- The same Problem Details object should be mirrored into telemetry (logs/spans) so PathSlice‑keyed refresh (G.11) can act on gate failures.

## How to mirror in JS/TS runtime
- After JSON decode, map to the same `Domain.Message` shape (or a TS equivalent) and apply identical rules:
  - Size check → emit Transport-lane finding `ACP.TRANSPORT.MAX_MESSAGE_BYTES_EXCEEDED`.
  - Run Protocol + Session rules (single prompt in-flight, cancel mismatch, result-without-prompt, multiple prompts) using the same codes.
- Preserve lane/severity codes so cross-runtime reporting stays aligned.

## References
- `src/Acp.RuntimeAdapter.fs` – adapter implementation.
- `tests/Acp.RuntimeAdapter.Tests.fs` – runtime-style tests (oversize inbound, valid inbound).
- `src/Acp.Validation.fs` – lane rules and profile-aware helpers.
- `core/UTS.md` – lane profile table (R18) and codes.
