# Product Structure - Four Holon Architecture

**Status**: Design
**Version**: 2.0
**Last Updated**: 2026-01-13

This document defines the four-holon architecture for the ACP ecosystem with test harnesses for each.

---

## Four Holons

### 1. ACP.Protocol (Protocol Core)

**Purpose**: Typed ACP protocol meaning (pure types + state machine)

**Responsibilities**:

- ACP domain types (sessions, messages, capabilities)
- Protocol phase/state transitions
- Protocol error codes and invariants

**Core Files**:

- `protocol/src/Acp.Domain.fs`
- `protocol/src/Acp.Protocol.fs`

**Dependencies**:

- None (foundational layer)

**Test Harness**:

- Planned: `sentinel/tests/Protocol.Harness/` (split from `sentinel/tests/ACP.Tests.fsproj`)

---

### 2. ACP.Runtime (Runtime SDK)

**Purpose**: Transport, codec, and client/agent connection APIs

**Responsibilities**:

- Transport abstractions (stdio, memory, duplex)
- JSON-RPC codec (encode/decode)
- Client and agent connection APIs
- Contrib helpers (session state, tool calls, permissions)
- Observability/telemetry surface (shared tags, duplicated in Sentinel)

**Core Files**:

- `runtime/src/Acp.Transport.fs`
- `runtime/src/Acp.Connection.fs`
- `runtime/src/Acp.Codec.Types.fs`
- `runtime/src/Acp.Codec.Json.fs`
- `runtime/src/Acp.Codec.AcpJson.fs`
- `runtime/src/Acp.Codec.fs`
- `runtime/src/Acp.Observability.fs`
- `runtime/src/Acp.Contrib.SessionState.fs`
- `runtime/src/Acp.Contrib.ToolCalls.fs`
- `runtime/src/Acp.Contrib.Permissions.fs`

**Dependencies**:

- ACP.Protocol

**Test Harness**:

- Current: `sentinel/tests/SDK.Harness/`

---

### 3. ACP.Inspector (Validation + Assurance)

**Purpose**: Validation lanes, findings, and assurance tooling over ACP traffic

**Responsibilities**:

- Protocol validation and findings
- Runtime boundary adapter (validate inbound/outbound)
- Assurance, evidence graph, and eval helpers
- Validation taxonomy and pins

**Core Files**:

- `sentinel/src/Acp.Validation.fs`
- `sentinel/src/Acp.RuntimeAdapter.fs`
- `sentinel/src/Acp.ValidationFindingPins.fs`
- `sentinel/src/Acp.Pins.fs`
- `sentinel/src/Acp.Eval.fs`
- `sentinel/src/Acp.FSharpTokenizer.fs`
- `sentinel/src/Acp.Assurance.fs`
- `sentinel/src/Acp.EvidenceGraph.fs`
- `sentinel/src/Acp.Semantic.fs`
- `sentinel/src/Acp.Evolution.fs`
- `sentinel/src/Acp.Capability.fs`
- `sentinel/src/Acp.Observability.fs` (duplicated from Runtime to avoid a dependency edge)

**Dependencies**:

- ACP.Protocol (Observability tags duplicated locally)

**Test Harness**:

- Current: `sentinel/tests/Validation.Harness/` and `sentinel/tests/Epistemology.Harness/`

---

### 4. ACP.Inspector.Cli (Tooling)

**Purpose**: CLI workflows for inspect/validate/replay/analyze/benchmark

**Responsibilities**:

- Command parsing and IO
- Trace inspection and replay
- Benchmark harness
- Output formatting and telemetry wiring

**Core Files**:

- `cli/apps/ACP.Cli/`
- `cli/apps/ACP.Benchmark/`
- `cli/benchmarks/`
- `cli/examples/cli-demo/`
- `cli/src/Acp.MessageTag.fs`

**Dependencies**:

- ACP.Runtime
- ACP.Inspector

**Test Harness**:

- Planned: CLI integration smoke tests

---

## Holon Dependency Graph

```text
ACP.Protocol (foundation)
  -> ACP.Runtime
  -> ACP.Inspector
ACP.Inspector.Cli depends on ACP.Runtime + ACP.Inspector
```

**Principle**: Lower layers have no knowledge of upper layers. Dependencies flow upward only.
Observability tags are duplicated in ACP.Runtime and ACP.Inspector to avoid a runtime→inspector dependency.

---

## Test Harness Architecture

### 1. Protocol.Harness (planned)

**Tests**:

- Domain type invariants
- Protocol state machine transitions
- Property-based protocol checks

**Evidence**:

- Protocol conformance and invariants

---

### 2. Runtime.Harness (current: `sentinel/tests/SDK.Harness/`)

**Tests**:

- Transport layer reliability
- Codec encode/decode roundtrips
- Client/agent connection lifecycle
- Session state and contrib helpers
- Observability metrics and tags

**Evidence**:

- Connection failures and codec errors

---

### 3. Sentinel.Harness (current: `sentinel/tests/Validation.Harness/` + `sentinel/tests/Epistemology.Harness/`)

**Tests**:

- Validation lanes and finding taxonomy
- Eval profiles and tokenizer checks
- Assurance/evidence graph invariants
- Runtime adapter validation behavior

**Evidence**:

- Invalid message examples and finding traces

---

### 4. CLI.Smoke (planned)

**Tests**:

- `inspect`, `validate`, `replay`, `analyze`, `benchmark` command flows

**Evidence**:

- CLI exit codes and command outputs

---

## Migration Plan

### Phase 1: Define module interfaces and bridges

1. Document context cards for each holon
2. Declare explicit bridges (package boundaries and CL)
3. Enumerate public API surface per repo

### Phase 2: Extract Protocol repo

1. Move protocol core files into repo A
2. Publish ACP.Protocol package
3. Replace project references with PackageReference

### Phase 3: Extract Runtime and Sentinel repos

1. Move runtime SDK files into repo B
2. Move sentinel validation files into repo C
3. Publish ACP.Runtime and ACP.Inspector packages

### Phase 4: Extract CLI repo

1. Move CLI apps and benchmark harness into repo D
2. Wire CLI to ACP.Runtime and ACP.Inspector packages
3. Add CLI integration smoke tests

---

## Product Packaging

### NuGet Packages (target)

```text
ACP.Protocol
- Dependencies: None
- Contains: Domain model + protocol state machine

ACP.Runtime
- Dependencies: ACP.Protocol
- Contains: Codec, transport, connection, contrib, observability

ACP.Inspector
- Dependencies: ACP.Protocol
- Contains: Validation, assurance, eval, runtime adapter

ACP.Inspector.Cli
- Dependencies: ACP.Runtime, ACP.Inspector
- Contains: CLI tooling and benchmark harness
```

---

## Boundary Enforcement

### Compile-Time Boundaries

Cross-repo usage must be package-based only:

```xml
<ItemGroup>
  <PackageReference Include="ACP.Protocol" Version="x.y.z" />
</ItemGroup>

<ItemGroup>
  <PackageReference Include="ACP.Runtime" Version="x.y.z" />
  <PackageReference Include="ACP.Inspector" Version="x.y.z" />
</ItemGroup>
```

### Runtime Boundaries

Each holon has separate:

- Assembly/DLL
- Public API surface (documented in package README)
- CI pipeline and release cadence

---

## Benefits

1. Clear separation of concerns by holon
2. Independent releases and ownership
3. Explicit bridge contracts for cross-context use
4. Reduced change coupling between CLI and core libraries
5. Easier documentation and onboarding

---

## References

- `docs/fpf/contexts/`
- `docs/fpf/bridges/`
- `README.md`

---

**Next Steps**:

1. Review and approve holon context cards and bridges
2. Extract protocol repo and publish ACP.Protocol
3. Extract runtime and sentinel repos, then CLI repo
4. Update CI per repo and add CLI smoke tests
