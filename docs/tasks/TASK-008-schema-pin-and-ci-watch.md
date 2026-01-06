# TASK-008: ACP Schema Pin + CI Watchers

**Status**: Pending  
**Priority**: High  
**Assignee**: Team  
**Created**: 2026-01-06  
**Context**: Need to track stable ACP releases and RFD updates to avoid drift.

## Objective

Pin the current ACP schema version explicitly and add CI jobs to detect upstream changes.

## Scope

- [ ] Confirm latest stable ACP schema version
- [ ] Update `Acp.Domain.Spec.Schema` to exact version
- [ ] Add CI job to alert on ACP release/tag changes
- [ ] Add CI job to alert on RFD updates

## Deliverables

1. Explicit schema pin in `src/Acp.Domain.fs`
2. CI workflow with ACP/RFD update checks

## Commands Reference

```bash
dotnet test tests/ACP.Tests.fsproj
```

## Constraints

- Do not auto-update schema without review
- CI should fail or notify on upstream changes

## Success Criteria

- [ ] Schema pin is explicit (no `0.10.x` wildcard)
- [ ] CI signals when upstream ACP/RFD content changes
