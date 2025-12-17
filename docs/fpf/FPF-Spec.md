# FPF Spec - First Principles Framework

**Status**: Normative
**Version**: 1.0
**Last Updated**: 2025-12-17

This specification documents the First Principles Framework (FPF) patterns and valuable objects used throughout the ACP-inspector codebase.

---

## Table of Contents

- [A. Kernel Architecture](#a-kernel-architecture)
  - [A.1 Holonic Foundation](#a1-holonic-foundation)
  - [A.2 Role Taxonomy & Assignment](#a2-role-taxonomy--assignment)
  - [A.10 Evidence Graph](#a10-evidence-graph)
- [E. Pattern Composition & Authoring](#e-pattern-composition--authoring)
  - [E.8 Authoring Conventions](#e8-authoring-conventions)
  - [E.9 Decision Records (DRR)](#e9-decision-records-drr)
  - [E.17 Multi-View Describing (MVPK)](#e17-multi-view-describing-mvpk)
  - [E.TGA GateCrossing](#etga-gatecrossing)
- [G. Publication Profiles & Orchestration (SPF)](#g-publication-profiles--orchestration-spf)
  - [G.2 SoTA Packs & SoTA-Echoing](#g2-sota-packs--sota-echoing)
  - [G.3 CHR (Characterization)](#g3-chr-characterization)
  - [G.4 CAL (Calibration)](#g4-cal-calibration)
  - [G.10 Hook Surfaces (ATS / TGA)](#g10-hook-surfaces-ats--tga)
  - [G.14 SPF Orchestrator](#g14-spf-orchestrator)
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

### E.8 Authoring Conventions

**Pattern**: Standardized coding practices that ensure consistency, maintainability, and alignment with FPF architectural patterns.

**Principle**: Code authoring follows explicit conventions that:

- Enforce structural patterns through tooling
- Make architectural intent visible in code
- Support automated validation and formatting
- Align with holonic composition and role taxonomy

**Implementation in ACP-inspector**:

The codebase enforces authoring conventions through multiple layers:

**1. Style Guide Enforcement**:

```bash
# Fantomas formatter (deterministic, automated)
dotnet tool restore && dotnet fantomas src tests apps

# CI validation
dotnet fantomas src tests apps --check
```

**2. Type-Driven Design**:

```fsharp
// Single-case DUs enforce semantic type safety (A.1 Holonic Foundation)
type SessionId = SessionId of string
type MessageId = MessageId of string

// Record types align with holon composition
type InitializeParams = {
    protocolVersion: ProtocolVersion      // Part
    clientCapabilities: ClientCapabilities // Part
    clientInfo: ImplementationInfo option  // Part
}
```

**3. Module Organization by Role** (A.2 Role Taxonomy):

```fsharp
// Role: Domain Model
module Domain =
    type SessionId = SessionId of string

// Role: Codec
module Codec =
    let encode: SessionId -> JsonNode
    let decode: JsonNode -> SessionId

// Role: Validator
module Validation =
    let validate: JsonNode -> ValidationResult
```

**4. Test Naming Convention** (A.10 Evidence):

```fsharp
// Pattern: Method_Condition_Expected
[<TestMethod>]
member _.SessionId_RoundTrip_PreservesValue() = ...

// Evidence: Property-based tests
[<Property>]
let ``SessionId roundtrip preserves value`` (id: SessionId) = ...
```

**5. Documentation Standards**:

```fsharp
/// Initializes a new ACP session with the agent.
/// Returns the session ID and agent capabilities.
member _.InitializeAsync(params: InitializeParams) : Task<InitializeResult>
```

**Authoring Workflow**:

```
Write Code → Format (Fantomas) → Test (140+ tests) → Validate (CI) → Commit
    ↓             ↓                   ↓                  ↓              ↓
  Type-safe   Consistent         Evidence          Enforced      Traceable
  (compile)   (automated)        (PBT/unit)        (pipeline)    (git log)
```

**Enforcement Layers**:

| Layer        | Mechanism          | When            |
| ------------ | ------------------ | --------------- |
| Compile-time | F# type system     | Every build     |
| Pre-commit   | Fantomas format    | Developer local |
| CI           | Fantomas --check   | Every PR        |
| Runtime      | Validation layer   | Production      |
| Post-mortem  | Evidence artifacts | Test failures   |

**Key Conventions**:

1. **Naming**:
   - Modules: PascalCase
   - Types: PascalCase
   - Functions: camelCase
   - Test methods: `Method_Condition_Expected`

2. **Type Design**:
   - Single-case DUs for semantic types
   - Records for structured data
   - Discriminated unions for variants
   - Options for optional values (not null)

3. **Async Patterns**:
   - Task computation expressions (.NET 10)
   - Async suffix on async methods
   - CancellationToken support where applicable

4. **Error Handling**:
   - Result types for expected failures
   - Exceptions for unexpected failures
   - Evidence capture on all failures

**Detailed Reference**: See `docs/tooling/coding-standards.md` for complete coding standards including:

- F# style guide reference
- Formatting commands
- Module organization patterns
- Testing standards
- Documentation guidelines
- File organization structure

**Evidence**:

- `docs/tooling/coding-standards.md` - Complete coding standards
- `.editorconfig` - Editor configuration
- `dotnet-tools.json` - Fantomas version lock
- CI pipeline enforces --check

---

### E.9 Decision Records (DRR)

**Pattern**: Lean decision records that pin a small set of architectural choices that affect specs, implementation, and migrations.

**Principle**: A DRR is short, explicit, and stable:

- Capture only what must not drift: context, decision, consequences, alternatives.
- Link to specs/patterns instead of duplicating prose.
- Preserve history via git; avoid rewriting old DRRs.

**Authoring (aligns with E.8)**:

- Location: `docs/decisions/DRR-####-<slug>.md`
- Size: 1–2 pages
- Required sections: Context, Decision, Consequences, Rejected alternatives, Follow-ups

**Evidence**:

- `docs/decisions/DRR-0001-spf-g14.md`

---

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
- `docs/protocol.md` - Knowledge view documentation

---

### E.TGA GateCrossing

**Pattern**: Author enforceable controls as **GateCrossings** (E.8-conformant pattern docs) that bind observables (UTS), evaluation logic (Bridge), penalties, and an effective rating (`R_eff`).

**Principle**: Controls are enforceable when they are:

- **Named + versioned** (stable identity and change history)
- **Observable** (defined in terms of UTS signals)
- **Actionable** (produce allow/deny/degrade outcomes)
- **Composable** (referenced by orchestrators like G.14 and by realizations)

**Core objects**:

- **GateCrossing**: the crossing point and its policy (what is being gated).
- **Bridge**: mapping from UTS signals → crossing decision (how you evaluate).
- **UTS**: universal test signals (what must be observable/measurable).
- **Penalty model**: how violations reduce confidence/score.
- **`R_eff`**: effective rating after penalties.

**Artifact shape (E.8 template)**:

- File: `E-TGA-<nn>.md`
- Includes: identity, inputs (UTS), decision rule (Bridge), penalties, outputs (`R_eff`), conformance checklist.

---

## G. Publication Profiles & Orchestration (SPF)

**SPF** is a **publication profile** of `U.AppliedDiscipline` (it is not a new root entity).

SPF is published as a **discipline pack** under `docs/spf/packs/<discipline>/`, with strictness controlled by profile:

| Profile      | Intent                           | Minimum output                                                        |
| ------------ | -------------------------------- | --------------------------------------------------------------------- |
| **SPF-Min**  | Smallest usable vertical slice   | CHR + CAL + ≥1 `E.TGA` crossing + ≥1 realization case                 |
| **SPF-Lite** | Practical pack with SoTA echoing | SPF-Min + SoTA-Echoing sections (projection over sources / SoTA data) |
| **SPF-Full** | Strongest publication profile    | SPF-Lite + full conformance + broader crossings/realizations          |

### G.2 SoTA Packs & SoTA-Echoing

**Pattern**: Bind discipline packs to a traceable State-of-the-Art (SoTA) source base without duplicating SoTA content.

**Principle**:

- **SoTA-Pack** is the curated bundle of sources plus explicit adopt/adapt/reject decisions.
- **SoTA-Echoing** is a **projection/face** over SoTA data and cited sources, embedded into SPF artifacts.
- SoTA-Echoing **must not** fork SoTA: it references sources and the SoTA decisions it projects from.

**Publication rule**:

- SPF-Lite/Full may include SoTA-Echoing sections in CHR/CAL/E.TGA/Realization docs.
- Those sections must cite sources and record adopt/adapt/reject decisions (directly or by reference to the SoTA-Pack).

### G.3 CHR (Characterization)

**Pattern**: CHR records the discipline’s characterization in two coordinated views:

- **CHR-Sys**: system characterization (scope, boundaries, operational environment)
- **CHR-Epi**: epistemic characterization (claims, evidence expectations, uncertainty)

**Principle**: CHR makes the discipline pack falsifiable: it states what is being claimed and what evidence would change the claim.

**Minimum artifact set**:

- `CHR-Sys.md`
- `CHR-Epi.md`

**Minimum sections** (SPF-Min):

- Scope + boundaries
- Assumptions + constraints
- Claims/hypotheses + what would falsify them
- Evidence expectations (link to E.TGA crossings and UTS)
- Conformance checklist (E.8)

### G.4 CAL (Calibration)

**Pattern**: CAL defines calibration targets, thresholds, and scoring rules used by crossings and realizations.

**Principle**: Calibration is explicit and testable: thresholds and tradeoffs (false positives/negatives, costs) are documented and referenced by crossings.

**Minimum artifact**:

- `CAL.md`

**Minimum sections** (SPF-Min):

- Metrics/signals being calibrated (UTS)
- Thresholds + rationale
- Failure modes + penalties (ties into `R_eff`)
- Conformance checklist (E.8)

### G.10 Hook Surfaces (ATS / TGA)

**Pattern**: A standardized hook surface for attaching enforcement and evidence capture to a runtime pipeline.

**Principle**: Hooks separate **where you can intervene** from **what policy you apply** (policy lives in crossings like E.TGA).

**Normative rule**:

- A compliant system **MUST expose crossing hooks** via either:
  - **ATS hooks** (AH‑1..AH‑4) — legacy compatibility route, or
  - **TGA crossing hooks** (TH‑1..TH‑4) — preferred route aligned to `E.TGA` (GateCrossing + Bridge + UTS + penalties → `R_eff`).

**ATS hooks (legacy route)**:

- **AH‑1 Intake**: capture inputs and required context
- **AH‑2 Evaluate**: compute required signals/UTS
- **AH‑3 Gate**: apply allow/deny/degrade decisions
- **AH‑4 Record**: persist evidence and outcomes

**TGA crossing hooks (preferred route)**:

- **TH‑1 Observe**: emit required UTS signals for the crossing
- **TH‑2 Bridge**: evaluate the crossing Bridge (UTS → decision inputs)
- **TH‑3 Gate**: apply GateCrossing decision (allow/deny/degrade) and penalties → `R_eff`
- **TH‑4 Record**: persist evidence (inputs, UTS, decision, `R_eff`) for auditability

**Deprecation policy**:

- ATS is **deprecated**. New systems **SHOULD** implement TGA crossing hooks.
- ATS remains compliant for **1–2 editions** of this spec; after that, ATS **MAY** be removed from compliance language.
- Migration path: keep ATS behavior, add a compatibility layer that maps AH‑1..AH‑4 into TH‑1..TH‑4 per crossing.

### G.14 SPF Orchestrator

**Pattern**: A “one-button” orchestrator that generates an SPF pack as a **pattern-set output**, using E.8 templates per pattern (not one monolithic document).

**Status**: Documented (spec); tooling implementation is not yet present in ACP-inspector (planned Stage 3+).

**Principle**:

- Input: `U.AppliedDiscipline` + profile + SoTA sources/decisions.
- Output: a stable set of authored pattern documents (CHR/CAL/E.TGA/Realization), plus pack metadata.

**Output contract (SPF-Min)**:

```
docs/spf/packs/<discipline>/
  Passport.md
  Signature.md
  CHR-Sys.md
  CHR-Epi.md
  CAL.md
  E-TGA-01.md
  Realization-Case-01.md
```

#### G.14.1 Passport & Signature (skeleton)

**Status**: Documented (template + requirements; implementation pending).

**Passport** is the pack’s identity and provenance (minimum):

- Discipline id + title
- Profile (Min/Lite/Full)
- Pack version + date
- Source links (SoTA-Pack and/or cited sources)
- Generator identity (tool + commit hash)

**Signature** is the pack’s integrity envelope (minimum):

- Hashes of generated files (manifest)
- Optional signature mechanism (out of scope in this increment)

#### G.14.2 Vertical slice + E.TGA

**Status**: Documented (requirement; implementation pending).

SPF-Min must contain at least one **vertical slice**: a crossing (`E.TGA`) that is:

- Referenced by CHR claims (what is enforced / tested)
- Parameterized by CAL thresholds (what “good enough” means)
- Usable in a realization case (how it behaves in context)

#### G.14.3 CHR profile hook (reuses G.3)

**Status**: Documented (hook contract; implementation pending).

The orchestrator reuses **G.3** and applies profile strictness:

- **SPF-Min**: generate CHR-Sys + CHR-Epi with minimum sections.
- **SPF-Lite/Full**: add SoTA-Echoing sections and stricter conformance checks.

#### G.14.4 CAL profile hook (reuses G.4)

**Status**: Documented (hook contract; implementation pending).

The orchestrator reuses **G.4** and applies profile strictness:

- **SPF-Min**: generate CAL with explicit thresholds and penalty links.
- **SPF-Lite/Full**: expand rationale, include SoTA-Echoing, add broader calibration coverage.

#### G.14.5 Realization minimum

**Status**: Documented (minimum contract; implementation pending).

SPF-Min must include at least one **realization case** that references:

- CHR-Sys + CHR-Epi (what is being claimed)
- CAL (what thresholds apply)
- ≥1 E.TGA crossing (what is enforced/measured)

#### G.14.6 SoTA-Echo binding

**Status**: Documented (binding rule; implementation pending).

SoTA-Echoing is a **projection**, not duplicated SoTA:

- Echoing sections cite sources and record adopt/adapt/reject decisions.
- If a SoTA-Pack exists, echoing sections must reference it instead of re-stating it.

---

## Implementation References

### Source Code Locations

| FPF Pattern            | Primary Implementation                        | Supporting Files                  |
| ---------------------- | --------------------------------------------- | --------------------------------- |
| A.1 Holonic Foundation | `src/Acp.Domain.fs:30-500`                    | `src/Acp.Contrib.SessionState.fs` |
| A.2 Role Taxonomy      | `src/Acp.Connection.fs:1-300`                 | `src/Acp.Contrib.*.fs`            |
| A.10 Evidence Graph    | `tests/Pbt/EvidenceRunner.fs:1-50`            | `apps/ACP.Cli/Commands/*.fs`      |
| E.17 Multi-View (MVPK) | `src/Acp.Domain.fs` + `src/Acp.Connection.fs` | `docs/protocol.md`                |

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

1. **ACP Protocol Specification**: `docs/protocol.md`
2. **Domain Model**: `src/Acp.Domain.fs`
3. **Evidence Directory**: `core/evidence/`
4. **Test Suite**: `tests/` (140 passing tests)
5. **Examples**: `examples/` (6 working examples)

---

**Document Status**: This specification is normative and reflects the actual implementation patterns in ACP-inspector v1.0.

**Maintenance**: Update this document when adding new FPF patterns or significantly refactoring existing implementations.
