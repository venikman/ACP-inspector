# ACP Error Reporting Profile (RFC 9457 + FPF extensions)

## Decision
- All HTTP-facing ACP APIs return errors as `application/problem+json` (RFC 9457). No ad-hoc error shapes.
- Non-HTTP channels reuse the same Problem Details data model; media type may differ.
- Problem type URIs come from the ACP registry (`https://acp.example.org/problems/...`) and map to guard/policy epistemes.

## Field set (base + ACP/FPF extensions)
- `type` (URI), `title`, `status`, `detail`, `instance` — RFC 9457 base.
- Extensions (top-level members):
  - `context` — bounded context/component id.
  - `path_id`, `path_slice_id` — PathId & PathSliceId for G.11 refresh / evidence anchoring.
  - `assurance_lane` (`F|G|R`), optional `assurance_level` (L0–L3 snapshot).
  - `policy_id`, `sentinel_id`, `gate_id` — which guard/policy produced the verdict.
  - `work_id`, `scr_id` — Work instance + evidence node.
  - `acp_state` — ACP state-machine state at failure.
  - `trace_id` / `correlation_id` — tracing/log stitching hook.

### Example payload
```json
{
  "type": "https://acp.example.org/problems/sentinel-policy-violation",
  "status": 403,
  "title": "Gate rejected tool invocation",
  "detail": "Tool call blocked by Sentinel S42: 'no external HTTP in red-lane'.",
  "instance": "urn:acp:work:123e4567-e89b-12d3-a456-426614174000",
  "context": "ACP.Core.v1",
  "path_id": "ACP-Path-7",
  "path_slice_id": "ACP-Path-7:step-3",
  "sentinel_id": "Sentinel:S42",
  "gate_id": "Gate:ToolHTTPOutbound",
  "assurance_lane": "R",
  "scr_id": "SCR:errors/2025-12-10/abc",
  "work_id": "Work:tool-call-123",
  "policy_id": "CAL.Policy:OutboundHTTP.v3"
}
```

## Implementation checklist (per runtime)
- Add a shared Problem Details schema (JSON Schema/OpenAPI component) including the extension members above.
- Centralise error mapping: internal `FpfError`/`GateFailure` → Problem Details payload + telemetry record (same fields).
- Default content type for 4xx/5xx: `application/problem+json; charset=utf-8`.
- Gate/sentinel failures MUST populate: `path_id`, `path_slice_id`, `policy_id`, `sentinel_id`, `assurance_lane`, `acp_state`, `type`.
- Mirror the same object into OpenTelemetry logs/spans as structured attributes (`acp.path_id`, `acp.policy_id`, etc.).

## Test hooks (RSCR candidates)
- “Every 4xx/5xx response validates against the Problem Details schema.”
- “Gate/sentinel failures carry PathId/PathSliceId + policy/sentinel ids.”
- “Problem type URIs come from the registry; unknown URIs fail validation.”
