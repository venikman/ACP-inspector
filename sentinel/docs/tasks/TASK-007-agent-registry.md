# TASK-007: ACP Agent Registry Support

**Status**: Done
**Priority**: Medium
**Assignee**: Team
**Created**: 2026-01-06
**Context**: Draft RFD proposes registry.json and agent manifest schema for discovery.

## Deferral

Completed with draft registry ingestion and manifest generation behind `--acp-unstable`.

## Objective

Add optional registry ingestion (client side) and manifest generation (agent side) behind unstable flag.

## Scope

- [x] Define draft registry/manifest models
- [x] Implement optional ingestion of `registry.json`
- [x] Render searchable agent list in inspector
- [x] (If agent) generate `<id>/agent.json` manifest
- [x] Add security controls (pinning, signatures if available, user consent)

## Deliverables

1. Draft registry models and parsing
2. Inspector registry view/search output
3. Security safeguards documented

## Commands Reference

```bash
dotnet test sentinel/tests/ACP.Tests.fsproj
```

## Constraints

- Must be gated behind `--acp-unstable`
- No automatic download/exec without explicit user consent

## Success Criteria

- [x] Registry ingestion is optional and safe by default
- [x] Agent list renders without breaking existing flows
- [x] Security checks documented
