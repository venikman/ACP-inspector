# TASK-004: ACP \_meta Passthrough & Domain Support

**Status**: Complete
**Priority**: High
**Assignee**: Team
**Created**: 2026-01-06
**Completed**: 2026-01-06
**Context**: Draft RFD meta propagation rules need first-class modeling and passthrough guarantees.

## Objective

Add `_meta` support to core domain types and guarantee passthrough when proxying/forwarding ACP messages.

## Scope

- [x] Add `_meta: JsonObject option` to relevant domain messages (prompt/update/results as per Draft RFD)
- [x] Preserve `_meta` on encode/decode paths (codec)
- [x] Ensure proxy paths preserve `_meta` (no renamespacing)
- [x] Add explicit handling for `traceparent`, `tracestate`, `baggage` keys
- [x] Add tests for `_meta` passthrough (including W3C keys)

## Implementation Summary

### Domain Changes (`src/Acp.Domain.fs`)

Added `_meta: JsonObject option` to three types:

- `SessionPromptParams` - for client-to-agent prompt requests
- `SessionPromptResult` - for agent-to-client prompt responses
- `SessionUpdateNotification` - for agent-to-client streaming updates

### Codec Changes (`src/Acp.Codec.AcpJson.fs`)

Updated encode/decode functions to preserve `_meta`:

- `decodeSessionPromptParams` / `encodeSessionPromptParams`
- `decodeSessionPromptResponse` (returns tuple with `_meta`)
- `decodeSessionUpdateNotification` / `encodeSessionUpdateNotification`

### Connection Changes (`src/Acp.Connection.fs`)

Updated `SessionUpdateAsync` method to accept optional `meta` parameter.

### Tests Added (`tests/Acp.Codec.Tests.fs`)

New tests for \_meta passthrough (W3C Trace Context):

- `decode session prompt request preserves _meta payload`
- `decode session update notification preserves _meta payload`
- `decode session prompt response preserves _meta payload`
- `decode session prompt without _meta yields None`

## Deliverables

1. Updated domain models and codec handling for `_meta` - Complete
2. Proxy passthrough tests (golden or unit) - Complete (4 new codec tests)
3. Documentation notes in `docs/ACP-RFD-TRACKER.md` - Complete

## Commands Reference

```bash
dotnet test tests/ACP.Tests.fsproj
```

## Constraints

- Must remain forward-compatible: unknown `_meta` keys should be preserved - Met
- Do not hard-fail on missing `_meta` - Met (optional field, defaults to None)

## Success Criteria

- [x] `_meta` survives roundtrip encode/decode
- [x] Proxy forwarding preserves `_meta` unchanged
- [x] Tests cover `traceparent`, `tracestate`, `baggage`

## Test Results

All 400 tests pass (384 ACP.Tests + 4 Validation.Harness + 7 SDK.Harness + 5 Epistemology.Harness)
