# TASK-006: Telemetry Export Panel

**Status**: Done
**Priority**: Medium
**Assignee**: Team
**Created**: 2026-01-06
**Context**: Draft RFD recommends OpenTelemetry export guidance and trace context alignment.

## Deferral

Completed with draft guidance behind `--acp-unstable`.

## Objective

Surface telemetry export guidance in the inspector and connect it to `_meta` trace context.

## Scope

- [x] Add a "Telemetry" section in inspector output/UI
- [x] Surface recommended OTLP endpoint + exporter options
- [x] Link `_meta.traceparent`/`tracestate`/`baggage` to telemetry hints
- [x] Document required knobs/flags for telemetry export

## Deliverables

1. Inspector telemetry panel/section
2. Updated docs with OTLP guidance

## Commands Reference

```bash
dotnet test sentinel/tests/ACP.Tests.fsproj
```

## Constraints

- Guidance only; no automatic exporting unless explicitly enabled
- Must not leak or rewrite trace context

## Success Criteria

- [x] Telemetry section appears when `--acp-unstable` is enabled
- [x] Trace context keys displayed in telemetry guidance
- [x] Docs updated with exporter configuration notes
