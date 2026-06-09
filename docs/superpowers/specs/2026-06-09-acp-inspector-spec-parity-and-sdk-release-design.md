---
title: ACP Inspector — Spec Parity (0.13.6) & SDK Release (Program Phase 1)
date: 2026-06-09
status: draft (revised after PR #42 review)
revised: 2026-06-09 — corrected A3 session/resume shape, A6 usage_update shape (used/size/cost), §5.4 schema path (schema/schema.json, not v1), §8 drift-issue reopen — all re-verified against schema/schema.json @ tag v0.13.6
author: brainstorming session
target-branch: feat/acp-0.13.6-parity-and-sdk-release
target-pr: "feat(acp): 0.13.6 stable parity + SDK release readiness"
relates:
  - docs/ACP-RFD-TRACKER.md
  - docs/SDK-COMPARISON.md
  - docs/architecture/product-structure.md
  - .github/workflows/acp-upstream-watch.yml
grounded-by: docs/superpowers (parity audit workflow acp-parity-audit, 2026-06-09, 20 agents)
---

## 1. Context

ACP Inspector pins the ACP schema at `0.11.3` (`protocol/src/Acp.Domain.fs:17`,
`ProtocolVersion.current = 1`). Upstream stable ACP is now **`0.13.6`**
(2026-06-05) — roughly four minor releases / three months ahead. The wire
`protocolVersion` is still `1`, so the implementation still interoperates;
the gap is **feature/schema surface**, not a breaking protocol-version jump.

The drift was not visible because of a CI defect (§8): the
`acp-upstream-watch` workflow filed a single drift issue (#38, "v0.11.4")
and then suppressed every later alert, so no issue was ever filed for the
0.12.x or 0.13.x bumps. The repo therefore *felt* current while falling
~4 releases behind.

A grounded parity audit (20 agents, each verdict adversarially re-verified
against the **v0.13.6 tagged `schema.json`**) established the exact gap. Its
findings are the basis for this spec.

This is **Phase 1** of a larger "leverage" program (§3). The user selected
three product faces — canonical F#/.NET SDK, conformance/assurance harness,
and a standalone Inspector product — all of which sit on the same
foundation. Phase 1 builds that foundation and ships the **SDK** as the
first external face (the user's chosen lead).

## 2. Goal & Success Criteria

**Goal:** take the repo from *pinned-at-0.11.3, stale docs, unpublished* to
**spec-current (0.13.6 stable parity) + trustworthy + released as the
canonical typed F#/.NET ACP SDK.**

**Done when all hold:**

1. Every upstream-**stable** feature added between 0.11.3 and 0.13.6 is
   modelled across all holons (domain → codec → protocol → sentinel) with
   tests; the parity audit shows zero stable gaps vs 0.13.6.
2. `Spec.Schema` pin bumped `0.11.3` → `0.13.6`; `ProtocolVersion.current`
   stays `1` (no wire-version change in this range).
3. Full test suite green on `net10.0`.
4. `SDK-COMPARISON.md` and `ACP-RFD-TRACKER.md` rewritten against 0.13.6 and
   reviewed (no `0.10.x`/`0.11.x` residue, correct npm scope).
5. `dotnet pack` produces all four NuGet packages with per-package README +
   metadata; a publish **dry-run** succeeds. (Actual push to nuget.org is a
   separate, explicitly user-gated action — see §7.)
6. CI drift defect fixed; issues #38 and #40 resolved; PR #41 merged.

## 3. Program Roadmap (context, not all in scope)

```
        ┌─ Canonical F#/.NET SDK ──┐   ← Phase 1 lead face (THIS SPEC)
FOUNDATION ─┼─ Conformance harness ────┤   ← later phase
        └─ Standalone Inspector ───┘   ← later phase

FOUNDATION (THIS SPEC) = spec parity (0.11.3 → 0.13.6)
                       + refreshed trust artifacts
                       + green build/tests + CI drift fix
                       + realized holon→package boundaries
```

The conformance harness and Inspector product share a second capability — a
**live proxy** that captures client↔agent traffic — which is deferred to
their phases. This spec deliberately does not build it.

## 4. Non-Goals (named to prevent creep)

- **Unstable/experimental features as stable types.** `providers`,
  MCP-over-ACP, plan operations (incl. `plan_update` v2), and `session/fork`
  remain **gated/opaque** (§5.2). Not modelled as first-class stable types.
- **The Inspector-product UX / live proxy / web UI.** Later phase.
- **The conformance harness against external agents.** Later phase.
- **Upstream contribution** of the validation suite. Explicitly dropped by
  the user for now.
- **New transports** (HTTP/SSE/WebSocket). Still unstable upstream.
- **The holon repo *split*** into separate git repos
  (`product-structure.md` Phases 2–4 multi-repo extraction). Phase 1
  realizes the **package boundaries within this repo**, not a repo split.
- **Wire-version bump.** `protocolVersion` stays `1`.

## 5. Workstream A — Spec Parity Catch-up (0.11.3 → 0.13.6)

Each feature is implemented as a vertical slice across the holons in this
order: **domain type → codec encode/decode + method routing → protocol
state machine → sentinel validation lane → tests (codec roundtrip +
protocol transition + validation + golden/PBT)**. Capability fields are
negotiated at `initialize`, so capability-gated features touch the
handshake.

### 5.1 In-scope stable features (7)

Audit-confirmed upstream-stable and missing/partial locally. Ordered by
ascending effort so the smallest lands first as a pattern-setter.
Effort legend: **S** ≈ <0.5 dev-day, **M** ≈ 1–2 dev-days (total A ≈ 9–10
dev-days, indicative — `writing-plans` does the real sizing).

| # | Feature | Status | Effort | Upstream since |
|---|---------|--------|--------|----------------|
| A1 | Optional message IDs | missing | **S** | 0.13.6 (#1372) |
| A2 | `session/close` | missing | M | 0.12.2 (#1062) |
| A3 | `session/resume` | missing | M | 0.12.2 |
| A4 | `session/delete` | missing | M | 0.13.6 (#1370) |
| A5 | `logout` | missing | M | 0.13.3 (#1273) |
| A6 | session usage updates | partial | M | 0.13.6 (#1371) |
| A7 | `additionalDirectories` | missing | M | 0.13.5 (#1327) |

**A1 — Optional message IDs.** Add optional `messageId: string option` to
`ContentChunk` (`Acp.Domain.fs:518`). Codec: decode tolerant of absence
(`AcpJson.fs:1608`), encode omit-if-`None` (`:1616`). No method-routing or
phase change — informational chunk-grouping metadata. Distinct from the
already-modelled JSON-RPC envelope `RequestId` (`Domain.fs:402-405`) — do
not conflate. (The unstable-v2 "require message IDs" PR #1352 is **out of
scope**.) Tests: chunk roundtrip with and without `messageId`.

**A2 — `session/close`.** `CloseSessionRequest { sessionId; _meta? }` →
`CloseSessionResponse { _meta? }`; client→agent (`x-side: agent`).
Gated by `sessionCapabilities.close`, a **nullable marker object**
(`CloseSessionCapabilities | null`) — model like the existing
`SessionListCapabilities option`, **not a bool** (see §5.3.2). Protocol:
new terminal **Closed** phase; any in-flight prompt turn is treated as
cancelled. Sentinel: reject `session/close` when capability not advertised,
and reject post-close traffic on a closed session. Remove `session/close`
from the codec Ext catch-all (`AcpJson.fs:2877`).

**A3 — `session/resume`.** Verified against `schema/schema.json` @ tag
`v0.13.6` (raw `jq`). `ResumeSessionRequest` = `{ sessionId (req), cwd
(req), mcpServers?, additionalDirectories?, _meta? }` — i.e. the **same
shape as `session/load`** minus message replay; it is **not**
`sessionId`-only. `ResumeSessionResponse` = `{ modes?: SessionModeState,
configOptions?: SessionConfigOption[], _meta? }` — it **does** carry the
stable `modes` and `configOptions` (both already modelled in our domain, so
reuse them; both flagged `x-deserialize-default-on-error`). Semantics:
resumes an existing session **without replaying previous messages**
(contrast `session/load`). Gated by `sessionCapabilities.resume` (marker
object). Reuse the existing `cwd`/`mcpServers` codec paths from
`session/load`; coordinate `additionalDirectories` with A7.
*(Correction: an earlier draft asserted a `sessionId`-only request / `_meta`-only
response and blamed a stale docs render — that was a misread; the tagged
`schema/schema.json` is authoritative and shows the load-like shape above.)*

**A4 — `session/delete`.** `DeleteSessionRequest { sessionId; _meta? }` →
`DeleteSessionResponse { _meta? }`; mirrors `session/list` mechanically
across domain/codec/protocol. Gated by `sessionCapabilities.delete` (marker
object `SessionDeleteCapabilities { _meta? }`). Protocol: `Ready`-phase
transition; define semantics for deleting unknown/active/in-flight sessions
(treat anomalies as sentinel findings, never a crash). Sentinel: gate lane +
optional post-delete-use-of-deleted-`sessionId` finding.

**A5 — `logout`.** Method `"logout"` (**no** `session/` prefix),
client→agent, empty `{ _meta? }` request and response. Gated by
`agentCapabilities.auth.logout` — add `AgentAuthCapabilities` (default `{}`,
not required) carrying `logout: LogoutCapabilities option`. Mirror the
existing `authenticate` codec (`AcpJson.fs:373-388`) and connection
method-name maps (`Connection.fs:35,74-75`). Encode `auth.logout` such that
absent ⇒ unsupported, so existing 0.11.3 `initialize` roundtrips
(`Acp.Codec.Tests.fs:69`) still pass. Protocol: allow only from `Ready`.

**A6 — session usage updates.** Verified against `schema/schema.json` @ tag
`v0.13.6` (raw `jq`). The `session/update` `"usage_update"` variant carries
`UsageUpdate` = `{ used: uint64 (req), size: uint64 (req), cost?: Cost,
_meta? }`, where **`used`** = tokens currently in context and **`size`** =
total context-window size — **not** `inputTokens`/`outputTokens`/cache
fields. Add `type Cost = { amount: float; currency: string }` (both
required) and `type Usage = { used: int64; size: int64; cost: Cost option;
_meta: JsonObject option }`, plus a `SessionUpdate.UsageUpdate of Usage`
case **before** the Ext catch-all (`Domain.fs:~700`). Codec: `"usage_update"`
decode arm (required `used`/`size`, optional `cost`, tolerate extras) +
encode arm; remove the opaque usage passthrough (`AcpJson.fs:~1710`,
`:3426-3434`). Protocol: non-state-changing notification valid in the
streaming/Prompting phase. Sentinel: usage lane (`used`/`size` present &
non-negative; when `cost` is set, require `cost.amount` + `cost.currency`).
**Couples with §5.3.1** (drop the spec-drift `usage` field). Flip
`ACP-RFD-TRACKER.md` usage row Unstable→Stable.
*(Correction: an earlier draft used an `inputTokens`/`outputTokens`/cache
record — that shape is not in the v0.13.6 schema.)*

**A7 — `additionalDirectories`.** Optional `string list` (absolute paths,
empty == omitted) on `NewSessionParams`, `LoadSessionParams`,
`ResumeSessionParams` (A3), and `SessionInfo`; plus capability
`SessionAdditionalDirectoriesCapabilities { _meta? }` on
`SessionCapabilities`. Codec: write as a `JsonArray` only when non-empty,
decode tolerantly. Keep the **absolute-path** check in the **sentinel**
lane, not the codec (don't reject otherwise-valid frames). Optional finding
when supplied without the capability advertised.

### 5.2 Gated / opaque — explicitly NOT modelled as stable

| Feature | Upstream stability | Handling |
|---------|--------------------|----------|
| `providers` (`providers/list\|set\|disable`) | **unstable** (in `schema.unstable.json` only; absent from stable `schema.json`) | Stays opaque via Ext. No first-class types in Phase 1. |
| MCP-over-ACP message types | experimental (0.13.0) | Opaque via Ext. |
| Plan operations / `plan_update` v2 | unstable (0.13.4 / 0.13.6) | Opaque via `SessionUpdate.Ext`. |
| `session/fork` | draft (0.10.0) | Opaque via Ext. |

The existing forward-compat gate already prevents codec/validator/protocol
crashes for all of these (unknown request → `ExtRequest`; unknown
`sessionUpdate` discriminant → `SessionUpdate.Ext` with deep-cloned
payload; any-phase proxy/successor + `Ready` catch-all). **No stable-parity
work required.** See §5.3.3 for the one defensive improvement.

### 5.3 Latent fixes surfaced by the audit (in scope — they serve the SDK-trust goal)

**5.3.1 Spec-drift bug — drop `SessionPromptResult.usage`.** This field
(`Domain.fs:536`, codec `AcpJson.fs:3426-3434`) exists in **no** upstream
version (not 0.11.3, not 0.13.6 — `PromptResponse` defines only
`stopReason` + `_meta`), yet our codec round-trips it and tests assert it
(`Acp.Codec.Tests.fs:180-208`). Remove the field; usage flows **only**
through the `usage_update` notification (A6). Update the asserting tests and
`usage = None` construction sites (`Acp.Validation.Tests.fs:73`,
`Acp.Connection.Tests.fs:68,361`, `Pbt/Generators.fs:158`,
`Pbt/ProtocolStateMachine.fs:174`).

**5.3.2 Capability-shape correctness.** In 0.13.6 the `SessionCapabilities`
sub-fields (`close`, `delete`, `resume`, `additionalDirectories`, `list`)
are **nullable marker objects** (`anyOf[$ref, null]`), not booleans — one
audit agent's "boolean" reading was traced to a bad WebFetch summary and
overruled by the raw schema. Our existing `list` is already a marker object;
follow that pattern. **Implementation rule: verify each capability's exact
shape against the v0.13.6 *tagged* `schema.json` before modelling** (§5.4).

**5.3.3 Ext under-reporting finding.** Unknown `session/update` variants
(`plan_update` v2, MCP-over-ACP) fall into `SessionUpdate.Ext` and
`session/fork` creates a session id invisible to `ctx.sessions` — today
both are **silently dropped** (no finding), so the sentinel goes quiet on
exactly the unstable traffic it should surface. Add an **informational**
(not error) sentinel finding when Ext/unknown methods or unknown
`sessionUpdate` discriminants are observed. No crash exists today; this is
visibility only.

### 5.4 Schema-source discipline

Resolve every shape against the **version-tag** schema, which at `v0.13.6`
is reachable at:
`https://raw.githubusercontent.com/agentclientprotocol/agent-client-protocol/v0.13.6/schema/schema.json`
(verified **HTTP 200**, ~172 KB). **Both** the `main`-branch raw
`schema.json` *and* the tagged `schema/v1/schema.json` return **404**
(verified) — do not reference either; `schema/schema.json` at the tag is the
canonical stable surface. The unstable surface is `schema.unstable.json`
(used only to confirm §5.2 items are *not* stable). Treat the rendered docs
site as **secondary and sometimes stale**, and prefer raw `jq` over a
summarizing fetch for exact `required`/field lists — a summarizing fetch
misread `session/resume` and `usage_update` during the initial audit; the
A3/A6 corrections came from raw extraction.

### 5.5 Pin bump

After A1–A7 are green, bump `Spec.Schema` `0.11.3` → `0.13.6`
(`Acp.Domain.fs:17`, and the doc comment at `:6`). Add golden traces for
the new shapes. This is the last step of Workstream A.

## 6. Workstream B — Trust-Artifact Refresh

These are the artifacts a prospective SDK adopter reads first; shipping them
stale undercuts the "trustworthy typed core" pitch.

- **`SDK-COMPARISON.md`** — currently wrong: claims `0.10.x` and an
  outdated `@anthropic/acp-sdk` npm scope. Rewrite against ACP 0.13.6;
  re-derive the metrics (file/test/LOC counts) from the current tree; fix
  the npm/PyPI package identifiers; keep the (genuine) validation-layer
  differentiator framing.
- **`ACP-RFD-TRACKER.md`** — update header to target `0.13.6`; refresh the
  Stable Snapshot to include A2–A7; move usage updates Unstable→Stable; list
  `providers` + unstable bundle as gated/opaque with current status; record
  the CI fix (§8).
- Sanity-scan `README.md` and `docs/` for `0.10.x`/`0.11.x` version residue
  introduced by the bump.

## 7. Workstream C — Packaging & SDK Release (SDK-first)

Realize the **package boundaries** already designed in
`product-structure.md` (within this repo; not the multi-repo split):

- Produce four NuGet packages: **`ACP.Protocol`** (deps: none),
  **`ACP.Runtime`** (deps: `ACP.Protocol`), **`ACP.Inspector`** (deps:
  `ACP.Protocol`), **`ACP.Inspector.Cli`** (deps: `ACP.Runtime`,
  `ACP.Inspector`).
- Per-package README + package metadata (description, license, repo URL,
  symbols/sourcelink); independent semver, with the **ACP schema target
  (`0.13.6`) recorded in package metadata** (e.g. a tag/property), not baked
  into the package version.
- `dotnet pack -c Release` produces all four; a **publish dry-run**
  (`--dry-run` / local feed push) succeeds in CI.
- **Gated publish:** actual push to nuget.org is **outward-facing and
  effectively irreversible** — it is a separate action requiring explicit
  user go. This spec produces release-ready, signed packages and a dry-run;
  it does **not** push.

Resolve the historical `ACP.Inspector` slnx name ambiguity if it resurfaces
during packaging (the `cli/apps/ACP.Inspector` project vs the
`AssemblyName=ACP.Inspector` from `sentinel/src`) — see the 2026-04-07
scope-reduction spec for background.

## 8. Workstream D — CI / Drift Hygiene & Issue/PR Disposition

**CI drift defect (closes #38, blocks recurrence).** `acp-upstream-watch.yml`
lines 77-82: the "create issue on drift" step dedupes on a version-agnostic
title (`"ACP spec update available: in:title"`) and **early-exits** with no
update branch, so the first issue (#38, v0.11.4) suppressed all later alerts
and was never refreshed. **Fix:** capture the existing issue number across
**both open and closed** states (`--state all --jq '.[0].number // empty'`);
if present, **`gh issue reopen "$NUM"` when it is closed** (an
edited-but-closed issue stays invisible — flagged in review), then `gh issue
edit "$NUM" --title "ACP spec update available: v$LATEST" --body-file
"$BODY_FILE"` (and optionally comment); else fall through to `gh issue
create`. One canonical drift issue, always reopened and refreshed to the
newest upstream version.

**Issue disposition.**

- **#38** ("v0.11.4") — superseded by this parity work + CI fix; close on
  merge (the bump to 0.13.6 + edit-on-drift logic make it accurate/obsolete).
- **#40** (RFD hash changed) — closed by the §6 RFD-tracker refresh.

**PR disposition.**

- **#41** (Dependabot OpenTelemetry `1.14.0` → `1.15.2`) — **merge**. Low
  risk; only `ACP.Cli` consumes it (`ACP.Cli.fsproj:49,51`, used by
  `Common/Telemetry.fs` via long-stable builder APIs); 1.15.x loads on
  net10.0; mixed pinning is safe (NuGet unifies the shared core upward).
  **Follow-up (non-blocking):** a second bump to bring
  `OpenTelemetry.Exporter.Console` and `OpenTelemetry.Instrumentation.Runtime`
  (`:50,:52`, still 1.14.0) to 1.15.2 for lockstep.

**Green-build gate.** Confirm `dotnet build` + full test suite pass on
`net10.0` as the entry baseline and again before the pin bump.

## 9. Architecture Impact

No new holons. All changes land in the existing three
(`protocol`/`runtime`/`sentinel`) plus packaging metadata and CI. The one
structural move — realizing the package boundaries — executes the already
**approved** `product-structure.md` design rather than inventing one.
Observability tags stay duplicated across Runtime/Inspector (existing
decision) to avoid a runtime→inspector dependency edge.

## 10. Testing & Verification

Per feature (A1–A7): codec roundtrip (present/absent/empty + extra-field
tolerance + missing-required rejection), protocol state-machine transition,
sentinel validation finding, and golden-trace/PBT coverage where the suite
already has it. Capability features additionally test the `initialize`
negotiation roundtrip and the capability-gating finding.

**Phase exit gates:** (1) parity audit re-run shows zero stable gaps vs
0.13.6; (2) full suite green on net10.0; (3) `dotnet pack` emits all four
packages + publish dry-run succeeds; (4) refreshed docs reviewed; (5) CI
drift fix verified (manual `workflow_dispatch` produces/updates a single
canonical issue).

## 11. Risks & Mitigations

| Risk | Mitigation |
|------|------------|
| Capability shape mis-modelled as bool vs marker object | §5.4 rule: model against the v0.13.6 *tagged* schema; mirror existing `list` marker. |
| Single-source shape error (docs site, or one agent's summarizing fetch) | Resolve against the reachable tagged `schema/schema.json` via raw `jq`; the A3 `resume` and A6 `usage_update` corrections came from exactly this after review caught the audit's misreads. |
| Dropping `SessionPromptResult.usage` breaks asserting tests | Tracked explicitly in §5.3.1 with the exact test/construction sites to update. |
| Parity workstream large enough to overrun one plan | §12 splits execution at the green-tests checkpoint; A is independent of C. |
| Public NuGet publish is irreversible | §7 gates the actual push behind explicit user approval; dry-run only by default. |
| Unstable features later graduate to stable | CI drift fix (§8) restores reliable alerts; gated/opaque handling already forward-compatible. |
| Pin bump destabilizes golden traces | Bump is the **last** step of A (§5.5), after per-feature tests are green. |

## 12. Sequencing / Phasing

One spec, executed as two sequenced plans with a hard checkpoint:

1. **Plan 1 — Parity (Workstream A + D-green-gate + B).** A1 (pattern-setter)
   → A2–A7 → §5.3 latent fixes → pin bump (§5.5) → trust-artifact refresh.
   Merge PR #41 and land the CI drift fix alongside.
   **Checkpoint: full suite green on net10.0, audit shows zero stable gaps.**
2. **Plan 2 — SDK release readiness (Workstream C).** Package boundaries,
   metadata, `pack`, publish dry-run. Stops short of the gated nuget.org push.

`writing-plans` will turn this spec into those plans.

## 13. References

- ACP spec (source of truth): <https://github.com/agentclientprotocol/agent-client-protocol>
- ACP changelog: <https://raw.githubusercontent.com/agentclientprotocol/agent-client-protocol/main/CHANGELOG.md>
- ACP 0.13.6 schema (tagged): <https://raw.githubusercontent.com/agentclientprotocol/agent-client-protocol/v0.13.6/schema/schema.json>
- `docs/ACP-RFD-TRACKER.md`, `docs/SDK-COMPARISON.md`
- `docs/architecture/product-structure.md` (holon→package design)
- `.github/workflows/acp-upstream-watch.yml` (CI drift defect, §8)
- Parity audit: workflow `acp-parity-audit`, 2026-06-09 (20 agents, adversarially verified)
