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
- ACP‑P02 · Maintain a “spec parity matrix” (methods, params/results, notifications, invariants, errors) in `core/roadmap/ACP-spec-parity-matrix.md`

Outcome:

- A checklist that drives implementation and prevents drift

### Milestone 1 — Complete protocol surface (types)

Scope:

- ACP‑P03 · Model remaining client→agent methods (e.g., `authenticate`, `fs/*`, `terminal/*`, permission responses)
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

| ID      | Title                                                      | Spec | Types | State | Validation | Tests | Docs |
| ------- | ---------------------------------------------------------- | ---- | ----- | ----- | ---------- | ----- | ---- |
| ACP‑P01 | Pin ACP spec version (commit/tag)                          | ✅   |       |       |            |       | ✅   |
| ACP‑P02 | Spec parity matrix (methods + invariants + errors)         | ✅   |       | ✅    | ✅         | ✅    | ✅   |
| ACP‑P03 | Model remaining protocol methods (authenticate, fs, term…) |      | ✅    | ✅    |            | ✅    |      |
| ACP‑P04 | Full content blocks + `_meta` preservation rules           |      | ✅    |       | ✅         | ✅    | ✅   |
| ACP‑P05 | Request IDs + error codes (JSON-RPC mapping layer)         | ✅   | ✅    |       | ✅         | ✅    | ✅   |
| ACP‑P06 | Extend phases/orderings to full spec                       |      |       | ✅    | ✅         | ✅    |      |
| ACP‑P07 | Concurrency rules for in-flight turns                      |      |       | ✅    | ✅         | ✅    |      |
| ACP‑P08 | Cancellation semantics enforced                            |      |       | ✅    | ✅         | ✅    |      |
| ACP‑P09 | Deterministic validation findings + subject anchors        |      |       |       | ✅         | ✅    |      |
| ACP‑P10 | Strict/tolerant profiles (defaults + knobs)                |      |       |       | ✅         | ✅    | ✅   |
| ACP‑P11 | Golden transcripts per method                              |      |       |       |            | ✅    |      |
| ACP‑P12 | PBT coverage across full surface                           |      |       |       |            | ✅    |      |
| ACP‑P13 | Schema/codec round-trip tests                              |      | ✅    |       |            | ✅    |      |
| ACP‑P14 | Supported spec version + compatibility policy              | ✅   |       |       |            |       | ✅   |
| ACP‑P15 | CI gates for protocol completeness                         |      |       |       | ✅         | ✅    | ✅   |

Public preface (optional):

> Slice‑01 is a protocol-completeness effort: pin the ACP spec, implement the entire message/state surface, and ship with executable conformance evidence. Runtime breadth and demo polish are explicitly deferred until after spec parity is achieved.
