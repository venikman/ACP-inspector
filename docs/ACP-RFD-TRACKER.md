# ACP RFD Tracker

**Last Updated**: 2026-06-09
**Current ACP Schema Target**: `0.11.3` (pinned in code)
**Protocol Version**: `1`
**Current Upstream Stable ACP Release**: `0.13.6` (2026-06-05)
**Latest Upstream Review**: Issues #38 and #40 reviewed on 2026-06-09

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

## Claim Scope

- ACP Inspector currently promises typed stable parity for ACP `0.11.3`.
- Upstream stable ACP has advanced to `0.13.6`.
- The schema pin remains `0.11.3` until the `0.13.x` stable gaps below are implemented and tested.
- Items listed as stable gaps are not customer-facing typed support yet; where possible, they are preserved as opaque extension payloads.

## Stable ACP Snapshot

| Feature | Status | Notes |
| ------- | ------ | ----- |
| Initialize handshake | ✅ Implemented | `clientInfo` / `agentInfo`, `protocolVersion = 1` |
| Session lifecycle | ✅ Implemented | `session/new`, `session/list`, `session/load`, `session/cancel` |
| Prompt turns | ✅ Implemented | `session/prompt`, streaming `session/update`, prompt-result usage payload |
| File system and terminal tools | ✅ Implemented | Request/response flow in runtime + codec |
| Permission requests | ✅ Implemented | `session/request_permission` |
| Agent plans | ✅ Implemented | `plan` session update modeled as first-class type |
| Slash commands | ✅ Implemented | `available_commands_update` modeled as first-class type |
| Session config options | ✅ Implemented | Typed `configOptions`, `session/set_config_option`, `config_option_update` |
| Session info updates | ✅ Implemented | Typed `session_info_update` through codec, protocol, runtime, and session snapshots |
| Session modes | ✅ Implemented | Supported for backward compatibility alongside config options |
| Session close/resume/delete | Gap | Stabilized upstream after `0.11.3`; not yet modeled as first-class requests/responses |
| Logout | Gap | Stabilized upstream after `0.11.3`; not yet modeled as an auth capability/method |
| Additional session directories | Gap | Stabilized upstream after `0.11.3`; not yet modeled on lifecycle requests/session info |
| Session usage update | Gap | `usage_update` is preserved as an opaque update; typed `UsageUpdate` / `Cost` support is pending |
| Optional message IDs | Gap | Stabilized upstream after `0.11.3`; `MessageId` is not yet modeled in content/update shapes |
| Protocol state machine | ✅ Implemented | Full `Phase` tracking in `Acp.Protocol.fs` |

## Current Stable Parity Gaps

No known stable parity gaps are open against ACP `0.11.3` in the currently implemented surface.

Against current upstream stable ACP `0.13.6`, the active gaps are:

- `session/close` and `session/resume` stabilized in `0.12.2`.
- `logout` stabilized in `0.13.3`.
- `additionalDirectories` for session lifecycle/session info stabilized in `0.13.5`.
- Optional `MessageId`, typed session `UsageUpdate` / `Cost`, and `session/delete` stabilized in `0.13.6`.

These gaps need one coordinated implementation pass across `Acp.Domain`, codec mappings, runtime/session state, protocol-state handling, and tests before `Acp.Domain.Spec.Schema` can honestly move to `0.13.6`.

## Selected Unstable Support

| Feature | Status | Current handling |
| ------- | ------ | ---------------- |
| Proxy chains | Unstable | Implemented as first-class protocol extensions |
| Telemetry export guidance | Unstable | Documented and surfaced in inspector output |
| Registry support | RFD completed upstream, integration remains product-specific | Optional tooling/docs support |
| Streamable HTTP transport | Still unstable upstream | Not implemented |
| MCP-over-ACP | Unstable upstream | Not implemented |
| Plan operations and v2 protocol experiments | Unstable upstream | Not implemented |
| Elicitation | Unstable upstream | Not implemented |

## Changelog Notes That Matter for This Repo

- **`0.10.8`** stabilized Session Config Options.
- **`0.11.1`** stabilized `session/list` and `session_info_update`.
- **`0.11.3`** is the current ACP Inspector schema pin and implemented stable contract.
- **`0.11.4` - `0.11.7`** added unstable work such as additional directories, elicitation, and providers; no pin-only stable upgrade was safe.
- **`0.12.2`** stabilized `session/close` and `session/resume`.
- **`0.13.3`** stabilized `logout`.
- **`0.13.5`** stabilized session `additionalDirectories`.
- **`0.13.6`** stabilized optional message IDs, session usage updates, and `session/delete`.

## Upstream Review Evidence

- Issue #38 is still valid, but stale in scope: the repo is pinned at `0.11.3`, while upstream latest is `0.13.6`.
- Issue #40 is still valid: the rendered `https://agentclientprotocol.com/rfds` page hash is now `78134d4e1e76bcbc3ece7611ad0ab7b916e7e27a5f50f93c62821c2c46e6ea54`, different from the issue's recorded `31140fdd5bbb07913f0b692e46bb558a2da4baca7fe278a750a30916748d6471`.
- Upstream RFD inventory changed since `0.11.3`, including additional directories, custom LLM endpoint, model config category, plan operations, streamable HTTP/WebSocket transport, updates, and v2 RFD material.

## Near-Term Implementation Order

1. Maintain the schema pin at `0.11.3` until the stable `0.13.x` gaps are implemented.
2. Execute [TASK-009](tasks/TASK-009-acp-0.13-stable-parity.md) as a coordinated domain/codec/runtime/test pass.
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
- [x] Review issues #38 and #40 against current upstream ACP (`0.13.6`)
- [ ] Implement the `0.13.x` stable parity gaps before bumping the schema pin

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
