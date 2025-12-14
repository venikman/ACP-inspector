# Project Rules & Conventions (ACP-sentinel)

This repository is the reference domain/protocol/validation layer. Keep it lean and consistent.

## Issue / ticket hygiene

- **Labels (Linear/issue trackers):**
  - `lane:protocol`, `lane:session`, `lane:transport`, `lane:tool`, `lane:impl` to mirror Validation lanes.
  - `area:validation`, `area:runtime-adapter`, `area:docs`, `area:tests` as needed.
  - `impact:gating` (breaks spec/validation) vs `impact:advisory` (non-gating).
- **Branch naming:** `<initials>/<ticket-id>-<slug>` e.g. `sn/har-28-runtime-adapter`.
- **PR checklist:**
  - Tests run: `dotnet test tests/ACP.Tests.fsproj`.
  - Commit hygiene: one “slice step” (parity-matrix row / ACP-P work item) per commit; don’t mix unrelated fixes.
  - Add/adjust docs if behavior or APIs change.
  - Note lane impact in the PR description (Protocol/Session/Transport/ToolSurface/Implementation).

## Validation rules ownership

- **Gating lanes:** Protocol, Session, Transport (Error severity blocks).
- **Advisory lanes:** Implementation (always), ToolSurface (unless explicitly marked gating in profile).
- New rules must specify lane + severity + error code; add tests that assert the code.

## Runtime integration contract

- Use `Acp.RuntimeAdapter.validateInbound/validateOutbound` at decode/encode edges; pass `RuntimeProfile` and frame size when available.
- Keep transport/IO out of this repo; adapters are the integration boundary.

## Docs to update when behavior changes

- `tooling/docs/runtime-integration.md`
- `core/UTS.md` (lane/profile tables)
- `tooling/docs/acp-fsharp-protocol.md` (spec narrative)

## Testing minima

- Every new validation rule: at least one positive and one negative test.
- Keep tests fast (`dotnet test` must stay sub-second per suite where possible).

## Coding style

- F#: prefer records/DUs, avoid partial matches; keep modules small and pure.
- Keep RequireQualifiedAccess off modules unless necessary to avoid noisy opens.
