# ACP spec parity matrix (slice‑01)

Purpose: track **spec-completeness** for ACP-sentinel against the pinned ACP spec.

Spec sources (normative):
- GitHub (source of truth): https://github.com/agentclientprotocol/agent-client-protocol
- Overview/intro: https://agentclientprotocol.com/overview/introduction

Pinned spec (fill/update when changing target):
- Tag: `v0.10.2`
- Commit: `8e3919b7ab494cf2d19326e2e17c5e7aeb15e366`
- Local pin: `core/roadmap/sub-ACP` (git submodule)

Legend:
- `Done` = implemented + tested
- `Partial` = implemented but missing codec/state/validation/tests
- `Todo` = not implemented
- `N/A` = intentionally out of scope for slice‑01 (avoid if possible; slice‑01 goal is full parity)

## 1) Methods and notifications

Fill this table from the pinned spec. Keep it exhaustive and review-gated.

| Method / Notification | Kind | Direction | Domain types | Codec | Protocol state | Validation | Tests | Notes |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `initialize` | request/response | client → agent | Partial | Todo | Partial | Partial | Partial | Existing core slice; expand to full spec fields. |
| `session/new` | request/response | client → agent | Partial | Todo | Partial | Partial | Partial | Existing core slice; confirm full param/result surface. |
| `session/load` | request/response | client → agent | Partial | Todo | Partial | Partial | Partial | Existing core slice; confirm replay semantics. |
| `session/prompt` | request/response | client → agent | Partial | Todo | Partial | Partial | Partial | Existing core slice; confirm full content blocks + streaming contract. |
| `session/update` | notification | agent → client | Partial | Todo | Partial | Partial | Partial | Existing core slice; expand update kinds (tool calls, chunks, plan, etc.). |
| `session/cancel` | request/response | client → agent | Partial | Todo | Partial | Partial | Partial | Existing core slice; enforce “Cancelled” outcome semantics. |
| `session/request_permission` | request/response | agent → client | Partial | Todo | Partial | Partial | Partial | Existing core slice; add client response side if in spec. |
| `authenticate` | request/response | client → agent | Todo | Todo | Todo | Todo | Todo | Fill from spec (auth shape, error codes, ordering). |
| `fs/*` | request/response | client ↔ agent | Todo | Todo | Todo | Todo | Todo | Fill each concrete method from spec (no wildcards in final). |
| `terminal/*` | request/response | client ↔ agent | Todo | Todo | Todo | Todo | Todo | Fill each concrete method from spec (no wildcards in final). |
| `mcp/*` | request/response | client ↔ agent | Todo | Todo | Todo | Todo | Todo | Fill if ACP spec defines MCP methods (avoid guessing). |

## 2) Content blocks and data types

Track the full content surface (beyond `Text` + `Resource`) and `_meta` handling.

| Type surface | Status | Where | Tests | Notes |
| --- | --- | --- | --- | --- |
| Content blocks (text/resource/image/audio/embedded context/…) | Partial | `src/Acp.Domain.fs` | Partial | Replace `Other` escape hatch with typed cases as spec stabilizes. |
| `_meta` preservation | Partial | `src/Acp.Domain.fs` (`MetaEnvelope`) | Todo | Preserve on decode/encode; do not validate unless profile opts in. |
| Capability flags | Partial | `src/Acp.Domain.fs` | Partial | Ensure parity with spec capability naming and defaults. |

## 3) Protocol invariants (state machine)

| Invariant / rule | Status | Where | Tests | Notes |
| --- | --- | --- | --- | --- |
| `initialize` must be first | Done | `src/Acp.Protocol.fs` | Done | Existing. |
| exactly one `initialize` result | Done | `src/Acp.Protocol.fs` | Done | Existing. |
| session must exist before prompt/cancel/update | Partial | `src/Acp.Protocol.fs` | Partial | Ensure this matches spec for replay/update streams. |
| at most one prompt in flight per session | Done | `src/Acp.Protocol.fs` | Done | Existing. |
| request_permission only during in-flight prompt | Done | `src/Acp.Protocol.fs` | Done | Existing; verify spec nuances. |
| cancellation semantics (Cancelled outcome required) | Partial | `src/Acp.Protocol.fs` + validation | Partial | Enforce “Cancelled” stop reason when cancel is requested (spec-defined). |

## 4) Errors and mapping

Goal: one pinned mapping for ACP errors ↔ JSON-RPC errors ↔ `ValidationFinding` codes.

| Error / code | Status | Where | Tests | Notes |
| --- | --- | --- | --- | --- |
| JSON-RPC standard errors (parse/invalid request/method not found/…) | Todo | codec/validation layer | Todo | Implement once the JSON-RPC envelope layer is in place. |
| ACP-specific error codes (auth_required, resource_not_found, …) | Todo | codec/validation layer | Todo | Populate from pinned spec; avoid inventing codes. |
| Protocol violations → `ACP.PROTOCOL.*` internal codes | Partial | `src/Acp.Protocol.fs`, `src/Acp.Validation.fs` | Partial | Decide which codes remain internal vs surfaced. |

## 5) Evidence requirements (slice‑01 gate)

- Every row marked `Done` must have at least one executable test (unit, golden transcript, or PBT property).
- A release candidate requires: no `Todo` rows in section (1), and no `Todo` rows in sections (3)–(4) that block conformance.
