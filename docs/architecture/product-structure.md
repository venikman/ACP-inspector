# Product Structure - Three Product Architecture

**Status**: Design
**Version**: 1.0
**Last Updated**: 2025-12-16

This document defines the three-product architecture for the ACP ecosystem with test harnesses for each.

---

## Three Products

### 1. ACP.Epistemology

**Purpose**: Knowledge representation and domain modeling

**Responsibilities**:

- Architecture pattern implementations (Holonic Foundation, Role Taxonomy, Evidence Graph, MVPK)
- Domain model (Domain.fs types)
- Validation finding pins and error taxonomy
- Epistemic reasoning and knowledge composition

**Core Files**:

- `src/Acp.Domain.fs` - Protocol domain model
- `src/Acp.Pins.fs` - Finding pins
- `src/Acp.ValidationFindingPins.fs` - Validation taxonomy
- `src/Acp.Eval.fs` - Evaluation and reasoning

**Dependencies**:

- None (foundational layer)

**Test Harness**: `tests/Epistemology.Harness/`

---

### 2. ACP.Validation

**Purpose**: Protocol validation, codec, and compliance checking

**Responsibilities**:

- JSON-RPC codec (encode/decode)
- Protocol validation against schema
- Message validation
- Trace analysis and replay
- Compliance reporting

**Core Files**:

- `src/Acp.Validation.fs` - Validation logic
- `src/Acp.Codec.fs` - JSON encoding/decoding
- `src/Acp.Protocol.fs` - Protocol layer
- `apps/ACP.Cli/` - CLI tools (inspect, validate, replay, analyze)

**Dependencies**:

- ACP.Epistemology (for domain model and validation pins)

**Test Harness**: `tests/Validation.Harness/`

---

### 3. ACP.SDK

**Purpose**: Client and agent connection layer for building ACP applications

**Responsibilities**:

- Transport abstractions (stdio, HTTP, duplex)
- Client connection API
- Agent connection API
- Session state management
- Tool call tracking
- Permission handling
- Observability/telemetry

**Core Files**:

- `src/Acp.Connection.fs` - Client/Agent connection
- `src/Acp.Transport.fs` - Transport layer
- `src/Acp.Contrib.SessionState.fs` - Session accumulator
- `src/Acp.Contrib.ToolCalls.fs` - Tool call tracker
- `src/Acp.Contrib.Permissions.fs` - Permission broker
- `src/Acp.Observability.fs` - OpenTelemetry
- `src/Acp.RuntimeAdapter.fs` - Runtime integration

**Dependencies**:

- ACP.Epistemology (for domain model)
- ACP.Validation (for codec and protocol)

**Test Harness**: `tests/SDK.Harness/`

---

## Product Dependency Graph

```text
ACP.Epistemology (foundation)
    ↑
    |
ACP.Validation
    ↑
    |
ACP.SDK (application layer)
```

**Principle**: Lower layers have no knowledge of upper layers. Dependencies flow upward only.

---

## Test Harness Architecture

Each product has a dedicated test harness that:

1. **Exercises the product boundary** - Tests public API surface
2. **Validates invariants** - Ensures core properties hold
3. **Provides examples** - Demonstrates typical usage patterns
4. **Generates evidence** - Captures failures for analysis

### Harness Structure

```text
tests/
├── Epistemology.Harness/
│   ├── Epistemology.Harness.fsproj
│   ├── DomainModelTests.fs           # Holon composition tests
│   ├── PatternTests.fs               # Pattern validation
│   ├── ValidationTaxonomyTests.fs    # Finding pins tests
│   └── Examples/
│       ├── HolonicComposition.fsx
│       └── RoleAssignment.fsx
│
├── Validation.Harness/
│   ├── Validation.Harness.fsproj
│   ├── CodecTests.fs                 # Encode/decode roundtrip
│   ├── ProtocolValidationTests.fs    # Schema conformance
│   ├── TraceAnalysisTests.fs         # Trace replay and analysis
│   └── Examples/
│       ├── ValidatingMessages.fsx
│       └── ReplayingTraces.fsx
│
└── SDK.Harness/
    ├── SDK.Harness.fsproj
    ├── ConnectionTests.fs            # Client/Agent API tests
    ├── TransportTests.fs             # Transport layer tests
    ├── SessionStateTests.fs          # State accumulation tests
    ├── ToolCallTests.fs              # Tool tracking tests
    ├── PermissionTests.fs            # Permission broker tests
    └── Examples/
        ├── BasicClient.fsx           (existing BasicClientAgent.fsx)
        ├── FullIntegration.fsx       (existing)
        └── SessionTracking.fsx       (existing SessionStateTracking.fsx)
```

---

## Test Harness Responsibilities

### 1. Epistemology.Harness

**Tests**:

- Holonic composition (Entity → Holon patterns)
- Role taxonomy and assignment
- Pattern validation
- Domain model invariants
- Type safety and constraints

**Property-Based Tests**:

- Holon composition is associative
- Role assignment is type-safe
- Domain types roundtrip correctly

**Evidence**:

- Domain model constraint violations
- Type system boundary errors

---

### 2. Validation.Harness

**Tests**:

- Codec roundtrip (encode → decode → encode)
- Protocol message validation
- Schema conformance checking
- Trace file parsing and replay
- Validation finding generation

**Property-Based Tests**:

- All valid messages pass validation
- Codec preserves structure (roundtrip)
- Validation is deterministic

**Evidence**:

- Invalid message examples
- Codec serialization failures
- Schema violations with locations

---

### 3. SDK.Harness

**Tests**:

- Client connection lifecycle
- Agent connection lifecycle
- Transport layer reliability
- Session state accumulation
- Tool call tracking accuracy
- Permission broker correctness

**Integration Tests**:

- Full client-agent interaction
- Multi-session scenarios
- Tool call with permission flow
- Error handling and recovery

**Evidence**:

- Connection failures
- State corruption scenarios
- Permission denial traces

---

## Migration Plan

### Phase 1: Create Harness Projects

1. Create `tests/Epistemology.Harness/` project
2. Create `tests/Validation.Harness/` project
3. Create `tests/SDK.Harness/` project
4. Set up project references (following dependency graph)

### Phase 2: Move Existing Tests

1. Move domain/pattern tests → Epistemology.Harness
2. Move codec/validation tests → Validation.Harness
3. Move connection/transport tests → SDK.Harness
4. Keep existing `tests/` for shared utilities

### Phase 3: Add Examples

1. Move existing examples to appropriate harnesses
2. Create new examples demonstrating each product
3. Ensure all examples run successfully

### Phase 4: Update CI/CD

1. Update `.github/workflows/ci.yml` to run all harnesses
2. Generate separate test reports per product
3. Track coverage per product

---

## Product Packaging

### NuGet Packages (Future)

```text
ACP.Epistemology
├── Version: 1.0.0
├── Dependencies: None
└── Contains: Domain model, patterns/markers

ACP.Validation
├── Version: 1.0.0
├── Dependencies: ACP.Epistemology
└── Contains: Codec, validation, protocol

ACP.SDK
├── Version: 1.0.0
├── Dependencies: ACP.Epistemology, ACP.Validation
└── Contains: Connection, transport, contrib modules
```

---

## Boundary Enforcement

### Compile-Time Boundaries

Use F# project references to enforce dependencies:

```xml
<!-- Epistemology.Harness.fsproj -->
<ItemGroup>
  <ProjectReference Include="../../src/Acp.Epistemology.fsproj" />
  <!-- No references to Validation or SDK -->
</ItemGroup>

<!-- Validation.Harness.fsproj -->
<ItemGroup>
  <ProjectReference Include="../../src/Acp.Epistemology.fsproj" />
  <ProjectReference Include="../../src/Acp.Validation.fsproj" />
  <!-- No reference to SDK -->
</ItemGroup>

<!-- SDK.Harness.fsproj -->
<ItemGroup>
  <ProjectReference Include="../../src/Acp.Epistemology.fsproj" />
  <ProjectReference Include="../../src/Acp.Validation.fsproj" />
  <ProjectReference Include="../../src/Acp.SDK.fsproj" />
</ItemGroup>
```

### Runtime Boundaries

Each product has separate:

- Namespace prefix (`Acp.Epistemology.*`, `Acp.Validation.*`, `Acp.SDK.*`)
- Assembly/DLL
- Documentation folder (`docs/products/{epistemology,validation,sdk}/`)

---

## Benefits

1. **Clear Separation of Concerns**: Each product has focused responsibility
2. **Independent Testing**: Harnesses test product boundaries in isolation
3. **Dependency Control**: Compile-time enforcement prevents circular dependencies
4. **Incremental Adoption**: Users can consume just what they need
5. **Easier Maintenance**: Changes isolated to specific products
6. **Better Documentation**: Each product has targeted docs and examples

---

## References

- **Protocol pointers**: `docs/protocol.md`
- **Current Tests**: `tests/` (to be migrated)
- **Examples**: `examples/` (to be migrated to harnesses)

---

**Next Steps**:

1. Review and approve this architecture
2. Create harness projects (Phase 1)
3. Migrate existing tests (Phase 2)
4. Document product APIs
