# ACP RFD Tracker

**Last Updated**: 2026-06-09
**Current ACP Schema Target**: `0.13.6` (pinned in code)
**Protocol Version**: `1`
**Current Upstream Stable ACP Release**: `0.13.6` (2026-06-05)

## Overview

This document tracks ACP (Agent Client Protocol) stable releases, active parity gaps, and selected unstable features in ACP Inspector.

For the upstream ACP specification:

- Spec source of truth (GitHub): <https://github.com/agentclientprotocol/agent-client-protocol>
- Overview/intro (website): <https://agentclientprotocol.com/get-started/introduction>
- RFD process (website): <https://agentclientprotocol.com/rfds/about>
- Stable schema/docs: <https://agentclientprotocol.com/protocol/schema>
- Upstream changelog: <https://raw.githubusercontent.com/agentclientprotocol/agent-client-protocol/main/CHANGELOG.md>

## Implementation Strategy

ACP Inspector follows **stable-first + gated unstable**:

1. **Stable ACP** is the contract (mandatory support)
2. **Unstable ACP** is accepted only when it is behind a clear compatibility gate or preserved as an opaque extension payload
3. **Unknown variants** render as raw JSON (forward-compatible, no crashes)

## Stable ACP Snapshot

| Feature | Status | Notes |
| ------- | ------ | ----- |
| Initialize handshake | ✅ Implemented | `clientInfo` / `agentInfo`, `protocolVersion = 1` |
| Session lifecycle | ✅ Implemented | `session/new`, `session/list`, `session/load`, `session/cancel` |
| Session close | ✅ Implemented | `session/close` — drives terminal `Phase.Closed`; stabilized in 0.12.2 |
| Session resume | ✅ Implemented | `session/resume` — reattaches existing session; stabilized in 0.12.2 |
| Session delete | ✅ Implemented | `session/delete` — removes session on request; stabilized in 0.13.6 |
| Logout | ✅ Implemented | `logout` method + `AgentCapabilities.auth.logout` marker; stabilized in 0.13.3 |
| Optional message IDs | ✅ Implemented | `ContentChunk.messageId` optional field; stabilized in 0.13.6 |
| Typed session usage updates | ✅ Implemented | `usage_update` with `used`/`size`/`cost`; stabilized in 0.13.6 |
| Additional directories | ✅ Implemented | `additionalDirectories` on new/load/resume params and `SessionInfo`; stabilized in 0.13.5 |
| Prompt turns | ✅ Implemented | `session/prompt`, streaming `session/update` |
| File system and terminal tools | ✅ Implemented | Request/response flow in runtime + codec |
| Permission requests | ✅ Implemented | `session/request_permission` |
| Agent plans | ✅ Implemented | `plan` session update modeled as first-class type |
| Slash commands | ✅ Implemented | `available_commands_update` modeled as first-class type |
| Session config options | ✅ Implemented | Typed `configOptions`, `session/set_config_option`, `config_option_update` |
| Session info updates | ✅ Implemented | Typed `session_info_update` through codec, protocol, runtime, and session snapshots |
| Session modes | ✅ Implemented | Supported for backward compatibility alongside config options |
| Protocol state machine | ✅ Implemented | Full `Phase` tracking in `Acp.Protocol.fs` |

## Current Stable Parity Gaps

No known stable parity gaps are open against ACP `0.13.6` in the currently implemented surface.

Areas to keep watching:

- If upstream adds new stable session metadata fields, extend `Acp.Domain`, codec mappings, and session snapshots together.
- Keep `modes` compatibility tests alongside `configOptions` tests until ACP formally removes legacy interoperability expectations.

## Selected Unstable / Gated Support

| Feature | Status | Current handling |
| ------- | ------ | ---------------- |
| `providers` | Unstable | Not implemented as stable types; preserved as opaque extension payload |
| MCP-over-ACP | Unstable | Not implemented; unknown variants surface as `Ext` informational findings |
| Plan operations (`plan_update` v2) | Unstable | Not implemented as stable; opaque via `SessionUpdate.Ext` |
| `session/fork` | Unstable | Not implemented; opaque via `Ext` |
| Proxy chains | Unstable | Implemented as first-class protocol extensions |
| Telemetry export guidance | Unstable | Documented and surfaced in inspector output |
| Registry support | RFD completed upstream, integration remains product-specific | Optional tooling/docs support |
| Streamable HTTP transport | Still unstable upstream | Not implemented |
| Elicitation shapes | Unstable upstream | Not implemented |

## Changelog Notes That Matter for This Repo

- **`0.10.8`** stabilized Session Config Options.
- **`0.11.1`** stabilized `session/list` and `session_info_update`.
- **`0.11.3`** pinned schema baseline for this repo as of 2026-03-19.
- **`0.12.2`** stabilized `session/close` and `session/resume`.
- **`0.13.3`** stabilized `logout` + `AgentCapabilities.auth.logout` capability marker.
- **`0.13.5`** stabilized `additionalDirectories` on session new/load/resume params and `SessionInfo`.
- **`0.13.6`** stabilized `session/delete`, optional `ContentChunk.messageId`, and typed `usage_update` (used/size/cost). Current upstream stable release as of 2026-06-05.

## Near-Term Implementation Order

1. Maintain the schema pin at the current upstream stable release.
2. Keep typed support for `session/list`, session config options, and typed session info updates covered by regression tests.
3. Preserve `modes` backward compatibility while preferring `configOptions` in docs and examples.
4. Re-check upstream stable ACP on each release and update this tracker when the contract changes.

## Testing Focus

1. Codec roundtrips for newly stabilized ACP request/result/update shapes.
2. Connection-layer request handling for list/config-option flows.
3. Session-state accumulation for typed session info and config-option updates.
4. Compatibility tests proving legacy `modes` still work alongside `configOptions`.

## Risks & Mitigations

| Risk                                      | Mitigation                                                        |
| ----------------------------------------- | ----------------------------------------------------------------- |
| Upstream stable ACP continues to move     | Pin explicitly and track the changelog in CI                      |
| Unstable ACP features can still change    | Keep them gated or opaque unless promoted to stable               |
| Legacy `modes` and `configOptions` diverge | Keep both in sync when both are emitted and test coexistence      |
| Registry ingestion is supply-chain vector | Pinning (hash/tag), signature verification, explicit user consent |
| Schema drift in usage fields              | Accept extra fields, don't hard-fail on missing expected fields   |

## CI/Automation

- [x] Add CI job that alerts on ACP release/tag changes
- [x] Add CI job that alerts on RFD updates (scrape agentclientprotocol.com/rfds)
- [x] Pin schema version in `protocol/src/Acp.Domain.fs`
- [x] Upgrade the pinned schema from `0.10.5` to current stable ACP (`0.11.3`)
- [x] Upgrade the pinned schema from `0.11.3` to current stable ACP (`0.13.6`)
- [x] Fix CI drift-dedupe bug: drift workflow now reopens and edits the canonical issue instead of early-exiting on an existing (possibly closed) issue (fixes #38)

## References

- ACP Spec: <https://github.com/agentclientprotocol/agent-client-protocol>
- ACP Intro: <https://agentclientprotocol.com/get-started/introduction>
- ACP Schema: <https://agentclientprotocol.com/protocol/schema>
- ACP Session List: <https://agentclientprotocol.com/protocol/session-list>
- ACP Session Config Options: <https://agentclientprotocol.com/protocol/session-config-options>
- ACP Slash Commands: <https://agentclientprotocol.com/protocol/slash-commands>
- ACP RFD process: <https://agentclientprotocol.com/rfds/about>
- ACP Changelog: <https://raw.githubusercontent.com/agentclientprotocol/agent-client-protocol/main/CHANGELOG.md>
- W3C Trace Context: <https://www.w3.org/TR/trace-context/>
- OpenTelemetry: <https://opentelemetry.io/>
