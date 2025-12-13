# ACP-DEC-002 · Problem Details as ACP Error Surface

- ArtifactId: ACP-DEC-002
- Family: ConceptualCore
- Type: Decision (DRR extract; Interop surface profile)
- AssuranceLevel: L1 (adopted, implementation pending)
- LifecycleState: Shape → Implement

Scope note:

- This is an HTTP-facing error surface profile (RFC 9457) layered on top of ACP. It should not compete with slice‑01 protocol parity work.

## 1. Decision (normative for ACP HTTP-facing services)

- All ACP-facing HTTP APIs **MUST** return errors as `application/problem+json` per RFC 9457 (type, title, status, detail, instance, extensions). Other channels (gRPC/MQ/CLI) **SHOULD** reuse the same data model even if the media type differs.
- RFC 9457 is treated as an **InteropCard / publication surface**; semantics live in the ACP/FPF model (U.Work, PathId/PathSliceId, Gate decisions, policies). No new semantics are introduced on the surface.
- Problem type URIs are minted in an internal registry (`https://acp.example.org/problems/...`), each pinned to the corresponding guard/policy episteme and UTS row.

## 2. ACP/FPF extension members (stable set)

Top-level extension members carried alongside the base fields:

- `context` – FPF bounded context / ACP component (e.g., `ACP.Core.v1`).
- `path_id`, `path_slice_id` – PathId / PathSliceId anchoring the evidence and refresh triggers (G.11).
- `assurance_lane` – `{ "F" | "G" | "R" }` lane whose guard fired; optional `assurance_level` (L0–L3 snapshot).
- `policy_id` – CAL / gate policy identifier responsible for the verdict.
- `sentinel_id`, `gate_id` – sentinel/gate instance that produced the decision.
- `work_id` – U.Work instance for this call; `scr_id` – evidence graph anchor; `instance` already carries a URI for the occurrence.
- `trace_id` / `correlation_id` – distributed tracing / log stitching hook.
- `acp_state` – ACP state-machine state at failure (e.g., `Observe.GateRejected`).

These fields are **pins** into existing ACP/FPF artifacts; they MUST NOT introduce new semantics beyond the referenced artifacts (MVPK no-new-claims rule).

## 3. Conformance & telemetry

- Each Problem Details payload is mirrored verbatim into telemetry/logs with the same extension fields; PathId/PathSliceId are mandatory when present in the decision context.
- RSCR tests to add: (a) every 4xx/5xx HTTP response validates against the shared schema, (b) gate/sentinel failures include `path_id`, `path_slice_id`, `policy_id`, `sentinel_id`, `assurance_lane`, `acp_state`.
- UTS rows R20–R22 record the envelope, type URI registry, and extension pins for cross-holon vocabulary alignment.

## 4. Integration work items (tracked in repo backlog)

- Add shared Problem Details schema (JSON Schema + OpenAPI component) with the extension fields above.
- Implement adapter/middleware per runtime that maps internal `FpfError`/`GateFailure` into RFC 9457 payloads and emits matching telemetry.
- Populate the problem-type registry with initial entries (sentinel-blocked, policy-abstain, evidence-stale, protocol-violation, tool-failure).
- Update dashboards to group errors by `assurance_lane`, `policy_id`, `sentinel_id`, and PathSliceId.

Decision owner: ACP-sentinel maintainers. Review cadence: refresh alongside Path/Policy profiles or when RFC 9457 changes.
