# TASK-007: ACP Agent Registry Support

**Status**: Spec-Wait
**Priority**: Medium
**Assignee**: Team
**Created**: 2026-01-06
**Context**: Draft RFD proposes registry.json and agent manifest schema for discovery.

## Deferral

This work is intentionally deferred until the registry RFD stabilizes. Revisit after stable-spec catch-up work is complete.

## Objective

Add optional registry ingestion (client side) and manifest generation (agent side) behind unstable flag.

## Scope

- [ ] Define draft registry/manifest models
- [ ] Implement optional ingestion of `registry.json`
- [ ] Render searchable agent list in inspector
- [ ] (If agent) generate `<id>/agent.json` manifest
- [ ] Add security controls (pinning, signatures if available, user consent)

## Deliverables

1. Draft registry models and parsing
2. Inspector registry view/search output
3. Security safeguards documented

## Commands Reference

```bash
dotnet test tests/ACP.Tests.fsproj
```

## Constraints

- Must be gated behind `--acp-unstable`
- No automatic download/exec without explicit user consent

## Success Criteria

- [ ] Registry ingestion is optional and safe by default
- [ ] Agent list renders without breaking existing flows
- [ ] Security checks documented
