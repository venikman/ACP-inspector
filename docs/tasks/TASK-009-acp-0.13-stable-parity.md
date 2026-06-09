# TASK-009: ACP 0.13 Stable Parity

**Status**: Pending
**Priority**: High
**Assignee**: Team
**Created**: 2026-06-09
**Context**: GitHub issues #38 and #40 showed the upstream ACP contract and RFD inventory moved beyond the local `0.11.3` pin. Upstream latest is `0.13.6`, but the repo should not bump `Acp.Domain.Spec.Schema` until the newly stabilized features are first-class and tested.

## Objective

Implement ACP stable `0.13.6` parity across the typed domain, codec, runtime/session state, protocol validation, and customer-facing docs, then bump the schema pin.

## Scope

- [ ] Add domain types/capabilities for `session/close`, `session/resume`, `session/delete`, `logout`, session `additionalDirectories`, optional `MessageId`, `UsageUpdate`, and `Cost`.
- [ ] Update JSON codec decode/encode paths for the new stable methods, response shapes, capabilities, and fields.
- [ ] Update protocol-state handling for the new stable session lifecycle methods.
- [ ] Update runtime/session snapshots for typed additional directories and typed session usage updates.
- [ ] Add regression tests for each newly stabilized method/field and for forward-compatible unknown payload handling.
- [ ] Update `Acp.Domain.Spec.Schema` from `0.11.3` to `0.13.6` only after the implementation and tests pass.
- [ ] Refresh `docs/ACP-RFD-TRACKER.md` after the pin bump.

## Deliverables

1. Typed ACP `0.13.6` stable support in protocol/runtime/sentinel surfaces.
2. Passing test evidence for the new stable methods and fields.
3. Updated schema pin and RFD tracker.

## Commands Reference

```bash
dotnet test sentinel/tests/ACP.Tests.fsproj -c Release
dotnet build cli/apps/ACP.Cli/ACP.Cli.fsproj -c Release
git diff --check
```

## Constraints

- Do not bump `Acp.Domain.Spec.Schema` before typed support and tests are in place.
- Keep unstable v2 and MCP-over-ACP work out of the stable parity pass unless upstream stabilizes them.
- Preserve forward-compatible handling for unknown variants and extension payloads.

## Success Criteria

- [ ] `Acp.Domain.Spec.Schema` is `0.13.6`.
- [ ] New stable methods and fields roundtrip through the codec.
- [ ] Protocol/runtime/session-state behavior is covered by tests.
- [ ] `docs/ACP-RFD-TRACKER.md` accurately separates implemented support, stable gaps, and unstable upstream work.
