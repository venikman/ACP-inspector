# ACP RFD Tracker

**Last Updated**: 2026-01-06
**Current ACP Schema Target**: `0.10.x`
**Protocol Version**: `1`

## Overview

This document tracks ACP (Agent Client Protocol) specification versions, Draft RFDs (Requests for Dialog), and their implementation status in ACP-inspector.

For the upstream ACP specification:

- Spec source of truth (GitHub): <https://github.com/agentclientprotocol/agent-client-protocol>
- Overview/intro (website): <https://agentclientprotocol.com/overview/introduction>
- RFDs (website): <https://agentclientprotocol.com/rfds>

## Implementation Strategy

We follow **Option 2: Stable + Parse Draft** — accept and display Draft messages without relying on them. This is optimal for an inspector tool:

1. **Stable ACP** is the contract (mandatory support)
2. **Draft RFDs** are parsed and displayed behind an unstable feature gate
3. **Unknown variants** render as raw JSON (forward-compatible, no crashes)

## Current Stable ACP Support

| Feature | Status | Notes |
| --- | --- | --- |
| Initialize handshake | ✅ Implemented | `clientInfo` in params, `agentInfo` in result |
| Session lifecycle | ✅ Implemented | `session/new`, `session/load`, `session/cancel` |
| Session prompts | ✅ Implemented | Streaming `session/update`, `session/prompt` result |
| Session modes | ✅ Implemented | `session/set_mode`, mode state tracking |
| File system tools | ✅ Implemented | `fs/read_text_file`, `fs/write_text_file` |
| Terminal tools | ✅ Implemented | `terminal/create`, `terminal/output`, etc. |
| Permission requests | ✅ Implemented | `session/request_permission` |
| Protocol state machine | ✅ Implemented | Full `Phase` tracking in `Acp.Protocol.fs` |

### Recent Schema Notes (v0.10.x)

- **v0.10.1**: Restored `title` field, added unstable `$/cancel_request` draft
- **v0.10.2**: Added unstable `session/resume` and `session/fork` fields

## Draft RFD Inventory

These are Draft-stage RFDs from <https://agentclientprotocol.com/rfds> as of 2026-01-06:

| RFD | Status | Priority | Inspector Support |
| --- | --- | --- | --- |
| Session List | Draft | P2 | ❌ Not started |
| Session Config Options | Draft | P2 | ❌ Not started |
| Forking of existing sessions | Draft | P2 | ❌ Not started |
| Request Cancellation Mechanism | Draft | P1 | ❌ Not started |
| Resuming of existing sessions | Draft | P2 | ❌ Not started |
| Meta Field Propagation Conventions | Draft | P1 | ❌ Not started |
| Session Info Update | Draft | P1 | ❌ Not started |
| Agent Telemetry Export | Draft | P2 | ❌ Not started |
| Proxy Chains: Composable Agent Architectures | Draft | P2 | ❌ Not started |
| Session Usage and Context Status | Draft | P1 | ❌ Not started |
| ACP Agent Registry | Draft | P2 | ❌ Not started |

### Priority Legend

- **P0**: Rebase/catch-up on stable ACP schema
- **P1**: High priority Draft features (inspector-relevant, safe to ship)
- **P2**: Lower priority Draft features (behavior-changing, require unstable gate)

## Planned Implementation Phases

### P0: Rebase on Current Stable ACP

- [ ] Verify `clientInfo` in `initialize` params
- [ ] Verify `agentInfo` in `initialize` result
- [ ] Confirm `session/cancel` support
- [ ] Confirm streaming `session/update` support
- [ ] Check schema pin for v0.10.1/v0.10.2 unstable fields

### P1: Unstable Feature Gate

- [x] Add `--acp-unstable=on` CLI flag (Inspector)
- [x] Add runtime config option for unstable features (Inspector config)
- [x] Unknown `sessionUpdate` variants → render as "Unknown/Ext" JSON
- [x] Unknown methods → render as "ExtRequest/ExtNotification"

### P2: Session Info Update (`session_info_update`)

- [x] Parse `session/update` where `sessionUpdate == "session_info_update"`
- [x] Update session title and metadata in session snapshot
- [x] Implement merge semantics for `_meta` field
- [x] Mark as "Draft" in inspector UI/output

### P3: Meta Field Propagation (`_meta`)

- [ ] Add `_meta` field to domain types as `JsonNode option`
- [ ] Reserve root keys: `traceparent`, `tracestate`, `baggage` (W3C Trace Context)
- [x] Display these 3 keys prominently in inspector output
- [ ] Passthrough preservation when proxying/forwarding

### P4: Session Usage Parsing

- [x] Add `usage_update` variant to `SessionUpdate` (parsed as draft Ext)
- [x] Add `usage` field to `SessionPromptResult`
- [x] Display per-turn usage deltas in inspector
- [x] Show context headroom (remaining context window)
- [x] Add warning threshold for low context (<10% remaining)
- [x] Schema-drift tolerance: don't hard-fail on extra fields

### P5: Proxy Chains Support (Parse-Only)

- [ ] Parse `proxy/successor` method
- [ ] Parse `proxy/initialize` method
- [ ] Display proxy chain events in inspector
- [ ] Recognize MCP-over-ACP "acp" transport (experimental)

### P6: Telemetry Export Alignment

- [ ] Add "Telemetry" panel/section to inspector output
- [ ] Surface recommended OpenTelemetry export configuration
- [ ] Connect trace context from `_meta` (P3)
- [ ] Document knobs for OTLP export

### P7: Agent Registry Readiness

- [ ] (If inspector as agent) Generate `<id>/agent.json` manifest
- [ ] (If inspector as client) Optional ingestion of `registry.json`
- [ ] Render searchable agent list from registry
- [ ] Security: pinning, signature verification, user consent

## Testing Strategy

### Golden Transcript Tests

1. **Session Info Update**: Feed recorded JSON-RPC log with `session_info_update` → assert title changed
2. **Meta Passthrough**: `_meta.traceparent` unchanged across forwarding
3. **Usage Update**: Accept even with extra fields (schema drift tolerance)
4. **Proxy Chain**: Methods appear as first-class events, not "unknown method"

### Negative Tests

- Unknown `sessionUpdate` strings → render as raw JSON, no error
- Unknown methods → graceful handling
- Malformed `_meta` → warning, not crash

## Risks & Mitigations

| Risk | Mitigation |
| --- | --- |
| Draft RFDs can change | Treat as non-exhaustive; ship behind "unstable" toggle |
| Registry ingestion is supply-chain vector | Pinning (hash/tag), signature verification, explicit user consent |
| Schema drift in usage fields | Accept extra fields, don't hard-fail on missing expected fields |

## CI/Automation

- [ ] Add CI job that alerts on ACP release/tag changes
- [ ] Add CI job that alerts on RFD updates (scrape agentclientprotocol.com/rfds)
- [ ] Pin schema version in `src/Acp.Domain.fs`

## References

- ACP Spec: <https://github.com/agentclientprotocol/agent-client-protocol>
- ACP Website: <https://agentclientprotocol.com>
- ACP RFDs: <https://agentclientprotocol.com/rfds>
- W3C Trace Context: <https://www.w3.org/TR/trace-context/>
- OpenTelemetry: <https://opentelemetry.io/>
