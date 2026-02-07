# TASK-008: ACP Schema Pin + CI Watchers

**Status**: Complete
**Priority**: High
**Assignee**: Team
**Created**: 2026-01-06
**Completed**: 2026-01-07
**Context**: Need to track stable ACP releases and RFD updates to avoid drift.

## Objective

Pin the current ACP schema version explicitly and add CI jobs to detect upstream changes.

## Scope

- [x] Confirm latest stable ACP schema version
- [x] Update `Acp.Domain.Spec.Schema` to exact version
- [x] Add CI job to alert on ACP release/tag changes
- [x] Add CI job to alert on RFD updates

## Implementation Summary

### Schema Pin (`protocol/src/Acp.Domain.fs`)

Updated from wildcard `0.10.x` to explicit `0.10.5`:

```fsharp
[<Literal>]
let Schema = "0.10.5"
```

### CI Workflow (`.github/workflows/acp-upstream-watch.yml`)

Two jobs run daily at 06:00 UTC:

1. **check-acp-release**: Compares pinned version against latest GitHub release
   - Creates issue with label `acp-upgrade` if drift detected
   - Skips if issue already exists

2. **check-rfd-updates**: Monitors RFD page for content changes
   - Uses SHA256 hash of page content
   - Stores last-run hash to detect changes between runs
   - Creates issue with label `acp-upgrade` when changes are detected

### Documentation Updates

- Updated `docs/ACP-RFD-TRACKER.md` to show pinned version `0.10.5`

## Deliverables

1. Explicit schema pin in `protocol/src/Acp.Domain.fs` - Complete
2. CI workflow with ACP/RFD update checks - Complete

## Commands Reference

```bash
# Run tests
dotnet test sentinel/tests/ACP.Tests.fsproj

# Manually trigger upstream watch
gh workflow run acp-upstream-watch.yml
```

## Constraints

- Do not auto-update schema without review - Met (creates issue for human review)
- CI should fail or notify on upstream changes - Met (creates issue + warning annotation)

## Success Criteria

- [x] Schema pin is explicit (no `0.10.x` wildcard)
- [x] CI signals when upstream ACP/RFD content changes
