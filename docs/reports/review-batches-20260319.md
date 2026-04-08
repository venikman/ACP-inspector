# Review Batches - 2026-03-19

This report packages the current ACP Inspector working state into four reviewable batches. It is written for a mixed audience: engineering reviewers, maintainers, and roadmap stakeholders.

## Executive Summary

### What changed

- The repo now has one CLI and one canonical docs tree.
- ACP stable parity was advanced to schema `0.11.3` with typed support for stable session listing, session config options, config updates, and typed session info updates.
- Validation, runtime state accumulation, CLI message rendering, and benchmarks were updated to reflect the new stable ACP surface.
- The FPF layer was refreshed around boundary routing, publication/view discipline, language-state handling, and ACP-native agent/tool coordination.

### Why it matters

- The product boundary is simpler to review and maintain.
- The runtime and codec now match more of current ACP stable instead of relying on generic extension payloads.
- The verification story is concrete: solution build, CLI build, full test suite, and inspect smoke run all pass.
- The methodology docs now describe the codebase in terms that are closer to the current FPF and ACP contract rather than the January snapshot.

### Stability framing

- **Stable now**: `session/list`, session config options, `session/set_config_option`, typed `session_info_update`, typed `config_option_update`, typed plan and available-command rendering, schema pin `0.11.3`.
- **Compatibility-only**: session modes remain supported and documented as legacy compatibility surface.
- **Draft / local**: unstable ACP support, local evolution/publication artifacts, and FPF methodology overlays remain draft or repo-local rather than protocol contract.

## Batch 1 - Product Boundary Cleanup

**Objective**: make the repo present one product surface with one CLI, one docs tree, and canonical external references.

**Impact**:

- review and onboarding no longer split across duplicate doc trees
- the repo no longer presents a deprecated standalone inspector executable
- ACP links and package metadata now point at the current canonical surfaces

**Key subsystems touched**:

- solution and product structure
- root docs and tracker docs
- package metadata

**Representative files**:

- `ACP-inspector.slnx`
- `README.md`
- `docs/ACP-RFD-TRACKER.md`

**Verification**:

- `dotnet build ACP-inspector.slnx -c Release`
- Outcome: passed after removal of legacy CLI and duplicate docs references

## Batch 2 - ACP Stable Parity Upgrade

**Objective**: move the protocol/runtime surface to ACP stable `0.11.3` and make stable session administration first-class.

**Impact**:

- stable ACP features are modeled explicitly instead of leaking through generic extension payloads
- runtime callers can list sessions, load sessions, and set config options through typed APIs
- session state tracks config options and typed session info updates as first-class data

**Key subsystems touched**:

- protocol domain and protocol state machine
- runtime codec and connection APIs
- session-state accumulation

**Representative files**:

- `protocol/src/Acp.Domain.fs`
- `protocol/src/Acp.Protocol.fs`
- `runtime/src/Acp.Codec.AcpJson.fs`

**Verification**:

- `dotnet test sentinel/tests/ACP.Tests.fsproj -c Release`
- Outcome: full suite passed with 396/396 tests, including new coverage for list/config-option/session-info flows

## Batch 3 - Validation, CLI, and Benchmark Hardening

**Objective**: prove the new stable ACP surface through validation, tests, CLI rendering, and benchmark fixtures.

**Impact**:

- typed stable updates and requests are covered in codec roundtrips and connection behavior
- CLI output renders the new stable message families cleanly
- benchmark fixtures now emit the updated session result and session update shapes

**Key subsystems touched**:

- sentinel tests and PBT helpers
- CLI message-tag rendering and benchmark command
- benchmark app fixtures

**Representative files**:

- `sentinel/tests/Acp.Codec.Tests.fs`
- `cli/src/Acp.MessageTag.fs`
- `cli/apps/ACP.Benchmark/Program.fs`

**Verification**:

- `dotnet build cli/apps/ACP.Cli/ACP.Cli.fsproj -c Release`
- `dotnet build ACP-inspector.slnx -c Release`
- `dotnet run --project cli/apps/ACP.Cli -c Release -- inspect cli/examples/cli-demo/demo-session.jsonl`
- Outcome: CLI build passed, solution build passed, inspect smoke run completed with 31 decoded frames, 0 decode errors, 0 validation findings

## Batch 4 - FPF Refresh and Diagram Handoff

**Objective**: package the methodology refresh as a separate review unit and attach diagrams as communication support, not as architecture source of truth.

**Impact**:

- assurance docs now route claims through boundary categories
- protocol evolution docs now own language-state discipline
- `DRR-005` now aligns tool coordination to ACP-native plan and slash-command surfaces
- the generated diagrams provide a quick architecture/module/flow entrypoint for reviews and roadmap discussion

**Key subsystems touched**:

- FPF alignment report
- assurance and evolution bounded contexts
- protocol-to-sentinel bridge and DRR-005
- architecture diagram set

**Representative files**:

- `docs/reports/fpf-alignment-evaluation-20260106.md`
- `docs/contexts/BC-004-protocol-evolution.md`
- `docs/architecture/diagram-set-20260319.md`

**Verification**:

- docs consistency checked against the implemented repo structure and current ACP parity state
- diagram assets generated and reviewed locally for readability

## Technical Appendix

### ACP sources

- ACP intro: <https://agentclientprotocol.com/get-started/introduction>
- ACP schema: <https://agentclientprotocol.com/protocol/schema>
- ACP changelog: <https://raw.githubusercontent.com/agentclientprotocol/agent-client-protocol/main/CHANGELOG.md>

### FPF sources

- Local review source: `/Users/stas-studio/Downloads/FPF-Spec.md`
- Upstream repo: <https://github.com/ailev/FPF>

### Diagram references

- [Architecture set index](../architecture/diagram-set-20260319.md)
- [Four Holon Architecture](../assets/generated-diagrams/acp-architecture-overview.png)
- [Module Map](../assets/generated-diagrams/acp-module-map.png)
- [Runtime + Validation Flow](../assets/generated-diagrams/acp-runtime-flow.png)

### Verification evidence

| Command | Outcome |
| ------- | ------- |
| `dotnet test sentinel/tests/ACP.Tests.fsproj -c Release` | Passed, 396/396 |
| `dotnet build cli/apps/ACP.Cli/ACP.Cli.fsproj -c Release` | Passed |
| `dotnet build ACP-inspector.slnx -c Release` | Passed |
| `dotnet run --project cli/apps/ACP.Cli -c Release -- inspect cli/examples/cli-demo/demo-session.jsonl` | Passed, 31 frames, 0 decode errors, 0 validation findings |
