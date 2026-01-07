# TASK-006: Telemetry Export Panel

**Status**: Spec-Wait
**Priority**: Medium
**Assignee**: Team
**Created**: 2026-01-06
**Context**: Draft RFD recommends OpenTelemetry export guidance and trace context alignment.

## Deferral

This work is intentionally deferred until the telemetry RFD stabilizes. Revisit after stable-spec catch-up work is complete.

## Objective

Surface telemetry export guidance in the inspector and connect it to `_meta` trace context.

## Scope

- [ ] Add a "Telemetry" section in inspector output/UI
- [ ] Surface recommended OTLP endpoint + exporter options
- [ ] Link `_meta.traceparent`/`tracestate`/`baggage` to telemetry hints
- [ ] Document required knobs/flags for telemetry export

## Deliverables

1. Inspector telemetry panel/section
2. Updated docs with OTLP guidance

## Commands Reference

```bash
dotnet test tests/ACP.Tests.fsproj
```

## Constraints

- Guidance only; no automatic exporting unless explicitly enabled
- Must not leak or rewrite trace context

## Success Criteria

- [ ] Telemetry section appears when `--acp-unstable` is enabled
- [ ] Trace context keys displayed in telemetry guidance
- [ ] Docs updated with exporter configuration notes
