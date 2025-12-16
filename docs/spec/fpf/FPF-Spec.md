# FPF Spec - First Principles Framework

**Status**: Normative
**Version**: 1.0
**Last Updated**: 2025-12-16

This specification documents the First Principles Framework (FPF) patterns and valuable objects used throughout the ACP-inspector codebase.

---

## Table of Contents

- [A. Kernel Architecture](#a-kernel-architecture)
  - [A.1 Holonic Foundation](#a1-holonic-foundation)
  - [A.2 Role Taxonomy & Assignment](#a2-role-taxonomy--assignment)
  - [A.10 Evidence Graph](#a10-evidence-graph)
- [E. Pattern Composition & Authoring](#e-pattern-composition--authoring)
  - [E.17 Multi-View Describing (MVPK)](#e17-multi-view-describing-mvpk)
- [Implementation References](#implementation-references)

---

## A. Kernel Architecture

### A.1 Holonic Foundation

**Pattern**: Entity → Holon transformation where complex domain concepts compose into coherent wholes while maintaining part relationships.

**Principle**: Every entity in the ACP protocol is a holon - it is simultaneously:

- A **whole** with its own identity and capabilities
- A **part** of larger compositions
- Composed of **parts** at lower levels

**Implementation in ACP-inspector**:

```fsharp
// Domain.fs - Protocol primitives as holons
type SessionId = SessionId of string        // Atomic holon
type ProtocolVersion = int                  // Atomic holon

// Composite holons
type ImplementationInfo = {
    name: string
    title: string option
    version: string
}

type InitializeParams = {
    protocolVersion: ProtocolVersion         // Part
    clientCapabilities: ClientCapabilities   // Part
    clientInfo: ImplementationInfo option    // Part
}

// Session as holon containing multiple protocol interactions
type SessionSnapshot = {
    sessionId: SessionId                     // Identity
    modes: SessionMode list                  // Parts
    currentModeId: SessionModeId option      // Current state
    result: PromptResult option              // Accumulated result
    permissions: PermissionInfo list         // Parts
    agentCapabilities: AgentCapabilities     // Context
}
```

**Key Properties**:

1. **Identity Preservation**: Each holon has unique identifier (SessionId, MessageId)
2. **Compositional**: Parts combine to form wholes (ContentBlock list → Prompt)
3. **Hierarchical**: Clear parent-child relationships (Session → Modes → ToolCalls)
4. **Transformable**: Holons can evolve state while maintaining identity

**Evidence**:

- `src/Acp.Domain.fs:30-150` - Protocol primitive holons
- `src/Acp.Contrib.SessionState.fs:10-80` - Session holon composition
- `tests/Pbt/Generators.fs` - Property-based holon generators

---

### A.2 Role Taxonomy & Assignment

**Pattern**: Explicit role assignment where entities declare their capabilities and responsibilities through typed roles.

**Principle**: Roles are first-class citizens that define:

- **Capabilities**: What operations an entity can perform
- **Responsibilities**: What invariants an entity maintains
- **Contracts**: What interfaces an entity implements

**Implementation in ACP-inspector**:

```fsharp
// Role: Protocol Validator
// Responsibility: Ensure messages conform to schema
module Validation =
    type ValidationResult = Valid | Invalid of ValidationError list

    let validateInitialize: InitializeParams -> ValidationResult
    let validateSessionPrompt: SessionPromptParams -> ValidationResult
    let validateToolResponse: ToolResponseParams -> ValidationResult

// Role: Session Accumulator
// Responsibility: Merge streaming notifications into coherent state
type SessionAccumulator() =
    let mutable snapshot: SessionSnapshot option = None

    member _.Apply(notification: SessionNotification) : SessionSnapshot
    member _.Current : SessionSnapshot option

// Role: Tool Call Tracker
// Responsibility: Track lifecycle and locations of tool invocations
type ToolCallTracker() =
    let calls = ResizeArray<TrackedToolCall>()

    member _.TrackCall(toolCall: ToolCall, locations: SourceLocation list) : unit
    member _.GetToolCalls() : TrackedToolCall list

// Role: Permission Broker
// Responsibility: Manage approval/denial of agent requests
type PermissionBroker() =
    let mutable decisions = Map.empty<PermissionRequestId, PermissionDecision>

    member _.RecordDecision(id, decision) : unit
    member _.GetDecision(id) : PermissionDecision option
```

**Role Assignment Rules**:

1. **Single Responsibility**: Each role has one clear purpose
2. **Explicit Boundaries**: Roles don't overlap in responsibilities
3. **Composable**: Roles combine to build larger systems (SmartClient = Connection + Accumulator + Tracker + Broker)
4. **Type-Safe**: F# types enforce role contracts at compile time

**Evidence**:

- `src/Acp.Connection.fs:50-200` - Connection role
- `src/Acp.Contrib.SessionState.fs:15-90` - Accumulator role
- `src/Acp.Contrib.ToolCallTracker.fs:10-60` - Tracker role
- `src/Acp.Contrib.Permissions.fs:10-50` - Broker role
- `examples/FullIntegration.fsx:30-120` - Role composition pattern

---

### A.10 Evidence Graph

**Pattern**: Persistent capture of validation failures, test counterexamples, and execution traces to support forensic analysis and continuous improvement.

**Principle**: Every significant event leaves evidence that can be:

- **Inspected**: Human-readable format
- **Replayed**: Deterministic reproduction
- **Analyzed**: Pattern extraction and root cause analysis
- **Referenced**: Unique identifiers for tracking

**Implementation in ACP-inspector**:

```fsharp
// Evidence Runner for Property-Based Tests
module EvidenceRunner =
    type FailureRecord = {
        propertyName: string
        timestampUtc: string
        seed: string              // Reproducibility
        shrinks: int              // Minimization steps
        args: string list         // Original input
        shrunkArgs: string list   // Minimized counterexample
        outcome: string           // Failure reason
    }

    type PbtEvidenceRunner() =
        interface IRunner with
            member _.OnFinished(name, result) =
                match result with
                | TestResult.Failed(data, args, shrunkArgs, outcome, seed, _, shrinks) ->
                    // Persist to core/evidence/pbt/ACP-EVD-PBT-latest-failure.json
                    let record = { ... }
                    File.WriteAllText(evidencePath, JsonSerializer.Serialize(record))
                | _ -> ()
```

**Evidence Artifacts**:

1. **Trace Evidence** (`*.jsonl`):

   ```jsonl
   {"ts": 1702000001.234, "direction": "c2a", "json": {...}}
   {"ts": 1702000002.456, "direction": "a2c", "json": {...}}
   ```

   - Full protocol interaction history
   - Timestamped for temporal analysis
   - Direction tagged for party attribution

2. **PBT Failure Evidence** (`core/evidence/pbt/*.json`):

   ```json
   {
     "propertyName": "SessionId_roundtrip",
     "timestampUtc": "2025-12-16T10:30:00Z",
     "seed": "(12345, 67890)",
     "shrinks": 42,
     "args": ["SessionId \"very-long-complex-string-...\""],
     "shrunkArgs": ["SessionId \"\""],
     "outcome": "Exception: ArgumentException"
   }
   ```

3. **Validation Evidence** (CLI output):
   ```
   [ERROR] Frame 15 (a2c): Invalid tool response
     - Missing required field: result.content
     - Location: tools/response line 42
   ```

**Evidence Graph Structure**:

```
core/evidence/
├── pbt/
│   └── ACP-EVD-PBT-latest-failure.json    # Latest PBT counterexample
├── traces/
│   └── demo-session.jsonl                  # Protocol execution traces
└── validation/
    └── (runtime generated by CLI)          # Validation failure reports
```

**Evidence Principles**:

1. **Persistence**: Evidence survives process termination
2. **Reproducibility**: Seed values allow deterministic replay
3. **Minimality**: Shrinking reduces counterexamples to essential elements
4. **Traceability**: Timestamps and IDs link evidence across artifacts
5. **Non-Invasive**: Evidence capture never fails the primary operation

**Evidence Usage Patterns**:

```bash
# Inspect evidence from traces
acp-cli inspect examples/cli-demo/demo-session.jsonl

# Analyze patterns in evidence
acp-cli analyze examples/cli-demo/demo-session.jsonl

# Replay evidence step-by-step
acp-cli replay --interactive examples/cli-demo/demo-session.jsonl

# PBT evidence inspection
cat core/evidence/pbt/ACP-EVD-PBT-latest-failure.json | jq .
```

**Evidence**:

- `tests/Pbt/EvidenceRunner.fs:1-50` - PBT evidence persistence
- `apps/ACP.Cli/Commands/Inspect.fs` - Trace evidence validation
- `apps/ACP.Cli/Commands/Replay.fs` - Evidence replay
- `examples/cli-demo/demo-session.jsonl` - Reference trace evidence

---

## E. Pattern Composition & Authoring

### E.17 Multi-View Describing (MVPK)

**Pattern**: Multiple coordinated views of the same entity to support different stakeholder perspectives and use cases.

**Principle**: Complex systems require multiple simultaneous perspectives:

- **M**ethod: How operations are performed
- **V**alue: What outcomes are produced
- **P**rocess: Sequential flow through states
- **K**nowledge: Conceptual understanding and relationships

**Implementation in ACP-inspector**:

The ACP protocol itself is a multi-view system:

```fsharp
// VIEW 1: METHOD - Protocol Operations
module Protocol =
    // How: Request-response pairs
    type InitializeRequest = { params: InitializeParams }
    type InitializeResponse = { result: InitializeResult }

    type SessionPromptRequest = { params: SessionPromptParams }
    type SessionPromptResponse = { result: PromptResult }

// VIEW 2: VALUE - Capabilities and Results
module Capabilities =
    // What: Declarations of what's possible
    type ClientCapabilities = {
        fs: FileSystemCapabilities
        terminal: bool
    }

    type AgentCapabilities = {
        mcpCapabilities: McpCapabilities
        promptCapabilities: PromptCapabilities
        sessionCapabilities: SessionCapabilities
    }

// VIEW 3: PROCESS - Session State Machine
module SessionState =
    // Process: State evolution over time
    type SessionSnapshot = {
        sessionId: SessionId
        modes: SessionMode list          // State history
        currentModeId: SessionModeId option
        result: PromptResult option      // Accumulated outcome
    }

    // State transitions
    let apply: SessionSnapshot option -> SessionNotification -> SessionSnapshot

// VIEW 4: KNOWLEDGE - Domain Model
module Domain =
    // Knowledge: Conceptual entities and relationships
    type ContentBlock =
        | Text of TextContent
        | Image of ImageContent
        | Audio of AudioContent
        | ResourceLink of ResourceLinkContent
        | Resource of ResourceContent

    // Relationships
    type PromptResult = {
        content: ContentBlock list        // Composition
        stopReason: StopReason option     // Causation
        toolCalls: ToolCall list          // Association
    }
```

**MVPK Coordination**:

Each view provides essential perspective:

1. **Method View** (Connection.fs):
   - Transport-level send/receive
   - Request-response correlation
   - Error handling

2. **Value View** (Domain.fs):
   - Type definitions
   - Validation rules
   - Serialization format

3. **Process View** (SessionState.fs):
   - State accumulation
   - Notification streaming
   - Lifecycle management

4. **Knowledge View** (Documentation):
   - Conceptual architecture
   - Design rationale
   - Usage patterns

**MVPK Navigation Rules**:

1. **Method → Value**: Operations produce/consume typed values
2. **Value → Process**: Values flow through state machine
3. **Process → Knowledge**: States reflect domain concepts
4. **Knowledge → Method**: Concepts inform API design

**Evidence**:

- `src/Acp.Domain.fs` - Value/Knowledge views
- `src/Acp.Connection.fs` - Method view
- `src/Acp.Contrib.SessionState.fs` - Process view
- `docs/spec/protocol.md` - Knowledge view documentation

---

## Implementation References

### Source Code Locations

| FPF Pattern            | Primary Implementation                        | Supporting Files                  |
| ---------------------- | --------------------------------------------- | --------------------------------- |
| A.1 Holonic Foundation | `src/Acp.Domain.fs:30-500`                    | `src/Acp.Contrib.SessionState.fs` |
| A.2 Role Taxonomy      | `src/Acp.Connection.fs:1-300`                 | `src/Acp.Contrib.*.fs`            |
| A.10 Evidence Graph    | `tests/Pbt/EvidenceRunner.fs:1-50`            | `apps/ACP.Cli/Commands/*.fs`      |
| E.17 Multi-View (MVPK) | `src/Acp.Domain.fs` + `src/Acp.Connection.fs` | `docs/spec/protocol.md`           |

### Testing Evidence

| Pattern              | Test Location                   | Coverage                                 |
| -------------------- | ------------------------------- | ---------------------------------------- |
| Holonic Composition  | `tests/Pbt/Generators.fs`       | Property-based generators for all holons |
| Role Contracts       | `tests/Unit/*.fs`               | Unit tests for each role implementation  |
| Evidence Persistence | `tests/Pbt/EvidenceRunner.fs`   | Automatic failure capture                |
| MVPK Consistency     | `tests/Unit/ValidationTests.fs` | Cross-view validation                    |

### Examples Demonstrating FPF

| Pattern            | Example File                           | Key Demonstration                 |
| ------------------ | -------------------------------------- | --------------------------------- |
| Holonic Foundation | `examples/BasicClientAgent.fsx:20-60`  | Session holon creation and usage  |
| Role Composition   | `examples/FullIntegration.fsx:30-120`  | SmartClient with 4 roles          |
| Evidence Replay    | `examples/cli-demo/demo-session.jsonl` | 31-frame evidence trace           |
| Multi-View         | `examples/SessionStateTracking.fsx`    | Process view of session evolution |

---

## Validation & Assurance Principles

### Validation Strategy

The FPF patterns support multiple validation levels:

1. **Compile-Time** (Type Safety):
   - F# type system enforces holon structure
   - Role interfaces checked at compilation
   - Invalid states unrepresentable

2. **Runtime** (Protocol Validation):
   - Schema conformance checking
   - State transition validation
   - Capability negotiation

3. **Property-Based** (Invariant Testing):
   - Holon composition properties
   - Round-trip encoding/decoding
   - State machine properties

4. **Evidence-Based** (Forensic Analysis):
   - Trace replay for bug reproduction
   - Counterexample minimization
   - Pattern extraction from failures

### Assurance Calculus

**Confidence Levels**:

- **High**: Type-checked at compile time (Domain model, Role contracts)
- **Medium**: Runtime validation with evidence (Protocol messages)
- **Low**: Property-based testing with shrinking (Edge cases)

**Evidence Requirements**:

- All validation failures must produce evidence artifact
- All PBT failures must persist seed and counterexample
- All protocol violations must include frame number and location

---

## References

1. **ACP Protocol Specification**: `docs/spec/protocol.md`
2. **Domain Model**: `src/Acp.Domain.fs`
3. **Evidence Directory**: `core/evidence/`
4. **Test Suite**: `tests/` (140 passing tests)
5. **Examples**: `examples/` (6 working examples)

---

**Document Status**: This specification is normative and reflects the actual implementation patterns in ACP-inspector v1.0.

**Maintenance**: Update this document when adding new FPF patterns or significantly refactoring existing implementations.
