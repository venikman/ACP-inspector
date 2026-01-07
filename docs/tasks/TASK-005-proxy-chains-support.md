# TASK-005: Proxy Chains Draft Support

**Status**: Done
**Priority**: Medium
**Assignee**: Team
**Created**: 2026-01-06
**Context**: Draft RFD introduces proxy chains and proxy/initialize + proxy/successor methods.

## Deferral

Completed with draft support behind `--acp-unstable`.

## Objective

Parse and render proxy chain methods as first-class events in the inspector (behind `--acp-unstable`).

## Scope

- [x] Add explicit domain types for `proxy/initialize` and `proxy/successor`
- [x] Decode/encode proxy chain methods in codec
- [x] Render proxy chain events in Inspector output
- [x] Recognize MCP-over-ACP `acp` transport as experimental
- [x] Add golden transcript tests for proxy chain messages

## Deliverables

1. Domain + codec support for proxy chain methods
2. Inspector render paths for proxy events
3. Tests proving no crash + correct display

## Commands Reference

```bash
dotnet test tests/ACP.Tests.fsproj
```

## Constraints

- Must stay tolerant to schema drift (unknown fields ignored, payload preserved)
- Keep behavior parse-only; no automatic proxy behaviors

## Success Criteria

- [x] Proxy chain messages decode without errors
- [x] Inspector renders proxy chain events with Draft labels
- [x] Tests cover at least one proxy initialize and successor example
