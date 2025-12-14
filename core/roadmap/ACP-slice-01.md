# ACP slice‑01 — Protocol implementation roadmap

We keep roadmap tracking in-repo (no external issue trackers). This file is the current source of truth for slice‑01.

## Focus (updated)

Slice‑01 is about **full ACP protocol implementation** (spec parity), not about how broadly ACP-sentinel can be used.

Non-goals for this slice:

- runtime/transport integration breadth (multiple adapters, polyglot SDKs, demo UX)
- sentinel heuristics beyond protocol correctness
- marketing/positioning work (audiences, “typical scenarios”, etc.)

## Definition of done (slice‑01)

A slice‑01 release is “done” when:

- the ACP spec version is pinned and referenced as the single source of truth
- every method/message in that spec is modeled in `Acp.Domain` (and its JSON-RPC envelope layer, where applicable)
- `Acp.Protocol.spec` enforces the spec’s ordering + invariants (not just the MVP subset)
- validation maps protocol violations to stable error codes and deterministic `ValidationFinding`s
- conformance evidence exists (goldens + PBT) for every method and key edge cases

## Milestones

### Milestone 0 — Spec pin + parity matrix

Scope:

- ACP‑P01 · Pin ACP spec version (commit/tag) in `core/roadmap/sub-ACP` (or equivalent) and record the version in-repo
  - Current pin: `v0.10.2` (`8e3919b7ab494cf2d19326e2e17c5e7aeb15e366`)
- Spec sources:
  - GitHub (source of truth): https://github.com/agentclientprotocol/agent-client-protocol
  - Overview/intro: https://agentclientprotocol.com/overview/introduction
- ACP‑P02 · Maintain a “spec parity matrix” (methods, params/results, notifications, invariants, errors) in this file (see “Spec parity matrix (slice‑01)”).

Outcome:

- A checklist that drives implementation and prevents drift

### Milestone 1 — Complete protocol surface (types)

Scope:

- ACP‑P03 · Model remaining ACP methods from the parity matrix (e.g., `authenticate`, `session/set_mode`, `session/request_permission` outcomes, and tool-surface calls: `fs/read_text_file`, `fs/write_text_file`, `terminal/create|output|wait_for_exit|kill|release`)
- ACP‑P04 · Expand content blocks to full spec surface (image/audio/embedded context) + `_meta` preservation rules
- ACP‑P05 · Introduce typed request IDs + error codes in the JSON-RPC envelope layer (single source of truth for mapping)

Outcome:

- `Acp.Domain` represents the full protocol surface for the pinned spec

### Milestone 2 — Full protocol state machine

Scope:

- ACP‑P06 · Extend phases/invariants to cover new methods and legal orderings
- ACP‑P07 · Encode concurrency rules (what is allowed during `PromptInFlight`, session replay, etc.)
- ACP‑P08 · Encode cancellation semantics so “Cancelled” outcomes are enforced when required

Outcome:

- `Acp.Protocol.spec` matches the spec’s state machine, not just the core slice

### Milestone 3 — Validation + error mapping

Scope:

- ACP‑P09 · ACP ↔ JSON-RPC error map exposed via deterministic validation findings (subjects + trace anchors)
- ACP‑P10 · Profile-driven strictness (tolerant decode vs strict conformance) with clear defaults

Outcome:

- Protocol violations are deterministic, debuggable, and testable

### Milestone 4 — Conformance evidence (tests + goldens)

Scope:

- ACP‑P11 · Golden transcripts: happy path + edge cases per method
- ACP‑P12 · Property-based coverage for ordering/invariants across the full message surface
- ACP‑P13 · Schema/codec round-trip tests (when the JSON codec layer exists)

Outcome:

- Executable evidence that the implementation is spec-complete

### Milestone 5 — Release readiness

Scope:

- ACP‑P14 · Document supported ACP spec version + compatibility/deprecation policy
- ACP‑P15 · CI gates: parity matrix completion, golden coverage, PBT pass, build/lint

Outcome:

- Shipping slice‑01 doesn’t regress protocol compliance

## Workstream matrix

| ID      | Title                                                     | Spec | Types | State | Validation | Tests | Docs |
| ------- | --------------------------------------------------------- | ---- | ----- | ----- | ---------- | ----- | ---- |
| ACP‑P01 | Pin ACP spec version (commit/tag)                         | ✅   |       |       |            |       | ✅   |
| ACP‑P02 | Spec parity matrix (methods + invariants + errors)        | ✅   |       | ✅    | ✅         | ✅    | ✅   |
| ACP‑P03 | Model remaining ACP methods (auth, modes, fs, terminals…) |      | ✅    | ✅    |            | ✅    |      |
| ACP‑P04 | Full content blocks + `_meta` preservation rules          |      | ✅    |       | ✅         | ✅    | ✅   |
| ACP‑P05 | Request IDs + error codes (JSON-RPC mapping layer)        | ✅   | ✅    |       | ✅         | ✅    | ✅   |
| ACP‑P06 | Extend phases/orderings to full spec                      |      |       | ✅    | ✅         | ✅    |      |
| ACP‑P07 | Concurrency rules for in-flight turns                     |      |       | ✅    | ✅         | ✅    |      |
| ACP‑P08 | Cancellation semantics enforced                           |      |       | ✅    | ✅         | ✅    |      |
| ACP‑P09 | Deterministic validation findings + subject anchors       |      |       |       | ✅         | ✅    |      |
| ACP‑P10 | Strict/tolerant profiles (defaults + knobs)               |      |       |       | ✅         | ✅    | ✅   |
| ACP‑P11 | Golden transcripts per method                             |      |       |       |            | ✅    |      |
| ACP‑P12 | PBT coverage across full surface                          |      |       |       |            | ✅    |      |
| ACP‑P13 | Schema/codec round-trip tests                             |      | ✅    |       |            | ✅    |      |
| ACP‑P14 | Supported spec version + compatibility policy             | ✅   |       |       |            |       | ✅   |
| ACP‑P15 | CI gates for protocol completeness                        |      |       |       | ✅         | ✅    | ✅   |

Public preface (optional):

> Slice‑01 is a protocol-completeness effort: pin the ACP spec, implement the entire message/state surface, and ship with executable conformance evidence. Runtime breadth and demo polish are explicitly deferred until after spec parity is achieved.

## Spec parity matrix (slice‑01)

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

### 1) Methods and notifications

Fill this table from the pinned spec. Keep it exhaustive and review-gated.

| Method / Notification        | Kind             | Direction      | Domain types | Codec | Protocol state | Validation | Tests   | Notes                                                                                       |
| ---------------------------- | ---------------- | -------------- | ------------ | ----- | -------------- | ---------- | ------- | ------------------------------------------------------------------------------------------- |
| `initialize`                 | request/response | client → agent | Partial      | Todo  | Partial        | Partial    | Partial | Existing core slice; expand to full spec fields.                                            |
| `authenticate`               | request/response | client → agent | Todo         | Todo  | Todo           | Todo       | Todo    | Fill from spec (auth shape, error codes, ordering).                                         |
| `session/new`                | request/response | client → agent | Partial      | Todo  | Partial        | Partial    | Partial | Existing core slice; confirm full param/result surface.                                     |
| `session/load`               | request/response | client → agent | Partial      | Todo  | Partial        | Partial    | Partial | Existing core slice; confirm replay semantics.                                              |
| `session/prompt`             | request/response | client → agent | Partial      | Todo  | Partial        | Partial    | Partial | Existing core slice; confirm full content blocks + streaming contract.                      |
| `session/set_mode`           | request/response | client → agent | Done         | Todo  | Done           | Done       | Partial | Session modes state + `current_mode_update` modeled; still missing JSON codec coverage.     |
| `session/update`             | notification     | agent → client | Partial      | Todo  | Partial        | Partial    | Partial | Existing core slice; expand update kinds (tool calls, chunks, plan, modes, commands, etc.). |
| `session/cancel`             | notification     | client → agent | Partial      | Todo  | Partial        | Partial    | Partial | Existing core slice; enforce “Cancelled” outcome semantics.                                 |
| `session/request_permission` | request/response | agent → client | Partial      | Todo  | Partial        | Partial    | Partial | Existing core slice; model outcomes + cancel propagation.                                   |
| `fs/read_text_file`          | request/response | agent → client | Todo         | Todo  | Todo           | Todo       | Todo    | Tool-surface: read file text (client capability-gated).                                     |
| `fs/write_text_file`         | request/response | agent → client | Todo         | Todo  | Todo           | Todo       | Todo    | Tool-surface: write file text (client capability-gated).                                    |
| `terminal/create`            | request/response | agent → client | Todo         | Todo  | Todo           | Todo       | Todo    | Tool-surface: create/exec terminal (client capability-gated).                               |
| `terminal/output`            | request/response | agent → client | Todo         | Todo  | Todo           | Todo       | Todo    | Tool-surface: fetch terminal output/status.                                                 |
| `terminal/wait_for_exit`     | request/response | agent → client | Todo         | Todo  | Todo           | Todo       | Todo    | Tool-surface: wait for terminal completion.                                                 |
| `terminal/kill`              | request/response | agent → client | Todo         | Todo  | Todo           | Todo       | Todo    | Tool-surface: kill terminal process.                                                        |
| `terminal/release`           | request/response | agent → client | Todo         | Todo  | Todo           | Todo       | Todo    | Tool-surface: release terminal resources.                                                   |

### 2) Content blocks and data types

Track the full content surface (beyond `Text` + `Resource`) and `_meta` handling.

| Type surface                                                  | Status  | Where                                | Tests   | Notes                                                              |
| ------------------------------------------------------------- | ------- | ------------------------------------ | ------- | ------------------------------------------------------------------ |
| Content blocks (text/resource/image/audio/embedded context/…) | Partial | `src/Acp.Domain.fs`                  | Partial | Replace `Other` escape hatch with typed cases as spec stabilizes.  |
| `_meta` preservation                                          | Partial | `src/Acp.Domain.fs` (`MetaEnvelope`) | Todo    | Preserve on decode/encode; do not validate unless profile opts in. |
| Capability flags                                              | Partial | `src/Acp.Domain.fs`                  | Partial | Ensure parity with spec capability naming and defaults.            |

### 3) Protocol invariants (state machine)

| Invariant / rule                                    | Status  | Where                              | Tests   | Notes                                                                    |
| --------------------------------------------------- | ------- | ---------------------------------- | ------- | ------------------------------------------------------------------------ |
| `initialize` must be first                          | Done    | `src/Acp.Protocol.fs`              | Done    | Existing.                                                                |
| exactly one `initialize` result                     | Done    | `src/Acp.Protocol.fs`              | Done    | Existing.                                                                |
| session must exist before prompt/cancel/update      | Partial | `src/Acp.Protocol.fs`              | Partial | Ensure this matches spec for replay/update streams.                      |
| at most one prompt in flight per session            | Done    | `src/Acp.Protocol.fs`              | Done    | Existing.                                                                |
| request_permission only during in-flight prompt     | Done    | `src/Acp.Protocol.fs`              | Done    | Existing; verify spec nuances.                                           |
| cancellation semantics (Cancelled outcome required) | Partial | `src/Acp.Protocol.fs` + validation | Partial | Enforce “Cancelled” stop reason when cancel is requested (spec-defined). |

### 4) Errors and mapping

Goal: one pinned mapping for ACP errors ↔ JSON-RPC errors ↔ `ValidationFinding` codes.

| Error / code                                                        | Status  | Where                                          | Tests   | Notes                                                   |
| ------------------------------------------------------------------- | ------- | ---------------------------------------------- | ------- | ------------------------------------------------------- |
| JSON-RPC standard errors (parse/invalid request/method not found/…) | Todo    | codec/validation layer                         | Todo    | Implement once the JSON-RPC envelope layer is in place. |
| ACP-specific error codes (auth_required, resource_not_found, …)     | Todo    | codec/validation layer                         | Todo    | Populate from pinned spec; avoid inventing codes.       |
| Protocol violations → `ACP.PROTOCOL.*` internal codes               | Partial | `src/Acp.Protocol.fs`, `src/Acp.Validation.fs` | Partial | Decide which codes remain internal vs surfaced.         |

### 5) Evidence requirements (slice‑01 gate)

- Every row marked `Done` must have at least one executable test (unit, golden transcript, or PBT property).
- A release candidate requires: no `Todo` rows in section (1), and no `Todo` rows in sections (3)–(4) that block conformance.
