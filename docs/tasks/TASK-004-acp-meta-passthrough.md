# TASK-004: ACP _meta Passthrough & Domain Support

**Status**: Pending  
**Priority**: High  
**Assignee**: Team  
**Created**: 2026-01-06  
**Context**: Draft RFD meta propagation rules need first-class modeling and passthrough guarantees.

## Objective

Add `_meta` support to core domain types and guarantee passthrough when proxying/forwarding ACP messages.

## Scope

- [ ] Add `_meta: JsonObject option` to relevant domain messages (prompt/update/results as per Draft RFD)
- [ ] Preserve `_meta` on encode/decode paths (codec)
- [ ] Ensure proxy paths preserve `_meta` (no renamespacing)
- [ ] Add explicit handling for `traceparent`, `tracestate`, `baggage` keys
- [ ] Add tests for `_meta` passthrough (including W3C keys)

## Deliverables

1. Updated domain models and codec handling for `_meta`
2. Proxy passthrough tests (golden or unit)
3. Documentation notes in `docs/ACP-RFD-TRACKER.md`

## Commands Reference

```bash
dotnet test tests/ACP.Tests.fsproj
```

## Constraints

- Must remain forward-compatible: unknown `_meta` keys should be preserved
- Do not hard-fail on missing `_meta`

## Success Criteria

- [ ] `_meta` survives roundtrip encode/decode
- [ ] Proxy forwarding preserves `_meta` unchanged
- [ ] Tests cover `traceparent`, `tracestate`, `baggage`
