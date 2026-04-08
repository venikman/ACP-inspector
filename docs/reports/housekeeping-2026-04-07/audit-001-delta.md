# Audit-001 Delta

- Prior audit: `docs/reports/audit-001-cleanup.md` dated 2026-01-06
- Current baseline: `docs/reports/housekeeping-2026-04-07/baseline.md`

## Summary

The January cleanup audit is directionally still useful, but several concrete assumptions are now stale. The current repository is healthier on build and test volume, while formatting drift has moved to newer ACP 0.11.3-era files and markdown debt is concentrated in planning/spec documents.

## Delta Table

| Area | Audit-001 (2026-01-06) | Current baseline (2026-04-07) | Delta |
| --- | --- | --- | --- |
| Build health | Clean build, 0 warnings | Clean build, 0 warnings | No regression |
| Test suite | 261 passing tests | 396 passing tests | +135 passing tests |
| Fantomas drift | 6 files | 8 files | Drift moved to newer protocol/runtime/test files |
| Link hygiene | Not measured | `lychee` reports 0 errors | Newly measured, currently clean |
| Markdown lint | Not measured | 25 issues across 3 planning/spec docs | Newly measured debt |
| Tool availability | Assumed | `dotnet tool restore` required before Fantomas in this environment | Added explicit prerequisite |

## Formatting Delta

Audit-001 flagged these files:

- `sentinel/src/Acp.Assurance.fs`
- `sentinel/src/Acp.Capability.fs`
- `sentinel/src/Acp.Semantic.fs`
- `sentinel/tests/Acp.AssuranceTests.fs`
- `sentinel/tests/Acp.CapabilityTests.fs`
- `sentinel/tests/Acp.SemanticTests.fs`

The current baseline instead flags:

- `protocol/src/Acp.Protocol.fs`
- `protocol/src/Acp.Domain.fs`
- `runtime/src/Acp.Codec.AcpJson.fs`
- `runtime/src/Acp.Contrib.SessionState.fs`
- `sentinel/tests/Acp.Codec.Tests.fs`
- `sentinel/tests/Acp.SessionState.Tests.fs`
- `sentinel/tests/Acp.Connection.Tests.fs`
- `sentinel/tests/Pbt/Generators.fs`

Interpretation:

- The original bounded-context additions appear to have been normalized since January.
- Current formatting drift clusters around the ACP 0.11.3 protocol/runtime work and the expanded test matrix.

## Codec Refactor Status

Audit-001 identified `runtime/src/Acp.Codec.fs` as a major split candidate. That recommendation has partially landed:

- `runtime/src/Acp.Codec.AcpJson.fs` exists.
- `runtime/src/Acp.Codec.Json.fs` exists.
- `runtime/src/Acp.Codec.Types.fs` exists.
- `runtime/src/Acp.Codec.fs` still exists, so the split is incomplete rather than obsolete.

This housekeeping pass does not continue the refactor because the current request is limited to low-risk cleanup.

## Test and Coverage Notes

- The current measured test count is materially higher than in January, which reduces the urgency of broad structural cleanup.
- This pass did not recalculate doc coverage or large-file counts because neither metric blocks the approved housekeeping scope.

## Operational Conclusion

- Keep the old audit as historical context.
- Prefer the 2026-04-07 baseline for any new cleanup or refactor decisions.
- Treat markdown/spec debt and the remaining codec split as separate follow-up work, not part of this housekeeping PR.
