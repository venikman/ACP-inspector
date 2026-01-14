# TASK-003: FPF Advanced Features (Phase 3)

**Status**: 🔵 Not Started (Future Work)  
**Priority**: Low  
**Dependencies**: TASK-002 (Phases 1 & 2 must be complete)  
**Started**: TBD  
**Based On**: [FPF Alignment Evaluation Report](../reports/fpf-alignment-evaluation-20260106.md)

## Objective

Implement advanced FPF patterns to achieve >95% alignment and enable sophisticated epistemic reasoning, protocol evolution tracking, and characteristic-based measurement frameworks.

## Context

TASK-002 (Phases 1 & 2) established foundational FPF infrastructure:

- Evidence decay & freshness tracking (INV-ASR-03)
- CL penalty computation across bridges
- Evidence graph construction (G.6)
- Context boundary enforcement (A.1.1)
- DRR-005 documenting C.24 adoption

Current FPF alignment: **~92%**

Phase 3 addresses remaining advanced patterns for research/production-grade epistemic infrastructure.

---

## Phase 3 Items

### Item 1: U.Episteme Runtime Type

**Priority**: Medium  
**FPF Pattern**: C.2.1 (U.Episteme slot graph)  
**Estimated Effort**: 5-7 days  
**Prerequisites**: Evidence Graph (TASK-002 Item 4) ✅ Complete

#### Current State

BC-001 documents episteme slot structure conceptually:

- **DescribedEntitySlot**: What the episteme describes
- **GroundingHolonSlot**: Physical system anchoring claims
- **ClaimGraphSlot**: Evidence structure
- **ViewpointSlot**: Perspective/context

These exist as documentation patterns but not as runtime-queryable F# types.

#### Proposed Implementation

**Files to Create**:

- `sentinel/src/Acp.Episteme.fs` (new module, ~250 lines)
- `sentinel/tests/Acp.EpistemeTests.fs` (new tests, ~200 lines)

**Type Structure**:

```fsharp
module Episteme =
    open Assurance
    open EvidenceGraph
    open Semantic

    /// Described entity - what this episteme is about
    type DescribedEntity =
        | ConcreteEntity of uri: string
        | AbstractConcept of name: string
        | TypeSignature of KindSignature

    /// Grounding holon - physical anchoring
    type GroundingHolon =
        | FileSystem of path: string
        | Database of connectionString: string * query: string
        | Network of url: string
        | Sensor of deviceId: string
        | NoGrounding // For pure abstract epistemes

    /// Viewpoint - perspective/context of claims
    type Viewpoint =
        { contextId: ContextId
          observer: string option  // Who made the observation
          timestamp: DateTimeOffset
          scope: ClaimScope }

    /// Episteme - complete knowledge artifact (C.2.1)
    type Episteme =
        { epistemeId: string
          describedEntity: DescribedEntity
          groundingHolon: GroundingHolon
          claimGraph: EvidenceGraph.EvidenceGraph
          viewpoint: Viewpoint
          assurance: AssuranceEnvelope
          metadata: Map<string, string> }

    module Episteme =
        val create : string -> DescribedEntity -> GroundingHolon -> Viewpoint -> Episteme
        val withClaim : ClaimId -> string -> Episteme -> Episteme
        val withEvidence : EvidenceId -> GroundingRef -> Episteme -> Episteme
        val computeAssurance : Episteme -> AssuranceEnvelope
        val validate : Episteme -> Result<unit, string list>
```

#### Implementation Steps

1. **Define Core Types** (2 days)
   - DescribedEntity, GroundingHolon, Viewpoint, Episteme
   - Builder functions for construction
   - Validation rules

2. **Integrate with Existing Modules** (2 days)
   - Link Episteme to EvidenceGraph
   - Link Episteme to AssuranceEnvelope
   - Link Episteme to Semantic contexts

3. **Query/Composition Functions** (1 day)
   - Find epistemes by described entity
   - Compose multiple epistemes
   - Merge claim graphs

4. **Testing** (2 days)
   - Construction tests
   - Validation tests
   - Integration tests with Evidence Graph

#### Acceptance Criteria

- [ ] Episteme type compiles and passes validation
- [ ] Can construct epistemes programmatically
- [ ] Epistemes integrate with existing Evidence Graph
- [ ] Assurance computed from claim graph structure
- [ ] All tests passing (target: 25+ tests)

#### References

- FPF Spec: C.2.1 U.Episteme
- BC-001: Assurance Context (episteme slot documentation)
- Evidence Graph implementation (TASK-002 Item 4)

---

### Item 2: DRR-005 Implementation (C.24)

**Priority**: Medium  
**FPF Pattern**: C.24 (Agent-Tools-CAL)  
**Estimated Effort**: 3-5 days  
**Prerequisites**: DRR-005 documented ✅ Complete

#### Current State

DRR-005 is complete and proposes:

- `ToolCallPlan` structure with resource budgets
- `ExploitExplorePolicy` for E/E-LOG integration
- Sentinel validation of budget adherence
- BLP (Bitter-Lesson Preference) support

This is currently a design document, not implemented.

#### Proposed Implementation

**Files to Create/Edit**:

- `sentinel/src/Acp.ToolPlanning.fs` (new module, ~300 lines)
- `sentinel/src/Acp.Validation.fs` (add ValidationFinding variants)
- `sentinel/tests/Acp.ToolPlanningTests.fs` (new tests, ~250 lines)

**Type Structure** (from DRR-005):

```fsharp
module ToolPlanning =
    type ResourceBudget =
        { maxToolCalls: int option
          maxLatency: TimeSpan option
          maxCost: float option
          scaleFactor: float }

    type ExploitExplorePolicy =
        { strategy: EEStrategy
          explorationRate: float option
          temperature: float option
          preferGeneralMethods: bool }
    
    and EEStrategy =
        | Greedy
        | EpsilonGreedy
        | UCB
        | ThompsonSampling

    type ToolCallIntent =
        { toolName: string
          rationale: string
          expectedOutcome: string option
          fallbackOnFailure: ToolCallIntent option }

    type ToolCallPlan =
        { planId: string
          goal: string
          constraints: ResourceBudget
          policy: ExploitExplorePolicy
          plannedSequence: ToolCallIntent list
          executionState: PlanState }
    
    and PlanState =
        | Planned
        | InProgress of completed: int
        | Complete
        | Failed of reason: string
```

#### Implementation Steps

1. **Define Planning Types** (1 day)
   - ResourceBudget, ExploitExplorePolicy, ToolCallIntent, ToolCallPlan
   - Builder functions

2. **Add Validation Findings** (1 day)
   - `ValidationFinding.BudgetExceeded`
   - `ValidationFinding.PlanDeviation`
   - `ValidationFinding.BLPViolation`

3. **Implement Sentinel Validation** (2 days)
   - Budget adherence checker
   - Plan-execution alignment validator
   - E/E policy compliance checker

4. **Testing & Examples** (1 day)
   - Unit tests for planning types
   - Validation tests
   - Example plans in `sentinel/examples/tool-planning/`

#### Acceptance Criteria

- [ ] ToolCallPlan types compile and validate
- [ ] Sentinel can validate budget adherence
- [ ] Plan deviation detection works
- [ ] BLP violations flagged correctly
- [ ] All tests passing (target: 20+ tests)
- [ ] At least 2 example plans documented

#### References

- DRR-005: Agent Tool Coordination
- FPF Spec: C.24 Agent-Tools-CAL
- FPF Spec: C.19 E/E-LOG, C.19.1 BLP, C.18.1 SLL

---

### Item 3: Evolution State Tracking (BC-004)

**Priority**: Low  
**FPF Pattern**: B.4 (Evolution Loop), BC-004 (Protocol Evolution)  
**Estimated Effort**: 4-6 days  
**Prerequisites**: Stable protocol adapter, Temporal aggregation (B.1.4)

#### Current State

BC-004 documents protocol evolution context but no runtime tracking of:

- Protocol version history
- Deprecation schedules
- Migration paths
- Compatibility matrices

This is purely aspirational - protocol evolution is currently manual.

#### Proposed Implementation

**Files to Create**:

- `sentinel/src/Acp.Evolution.fs` (expand existing stub, ~300 lines)
- `sentinel/tests/Acp.EvolutionTests.fs` (expand existing stub, ~200 lines)

**Type Structure**:

```fsharp
module Evolution =
    type ProtocolEdition =
        { editionId: string
          version: ProtocolVersion
          introduced: DateTimeOffset
          deprecated: DateTimeOffset option
          removed: DateTimeOffset option
          changeLog: string list }

    type MigrationPath =
        { fromEdition: string
          toEdition: string
          automaticMigration: bool
          migrationScript: string option
          breakingChanges: string list }

    type CompatibilityMatrix =
        { clientEdition: string
          agentEdition: string
          compatible: bool
          degradedFeatures: string list }

    type EvolutionState =
        { editions: Map<string, ProtocolEdition>
          migrations: MigrationPath list
          compatibility: CompatibilityMatrix list
          currentEdition: string }

    module EvolutionState =
        val register : ProtocolEdition -> EvolutionState -> EvolutionState
        val deprecate : string -> DateTimeOffset -> EvolutionState -> EvolutionState
        val findMigrationPath : string -> string -> EvolutionState -> MigrationPath option
        val isCompatible : string -> string -> EvolutionState -> bool
```

#### Implementation Steps

1. **Define Evolution Types** (2 days)
   - ProtocolEdition, MigrationPath, CompatibilityMatrix
   - Version management functions

2. **Implement Γ_time Aggregation** (1 day)
   - Temporal aggregation (FPF B.1.4)
   - Time-ordered edition history

3. **Migration Path Resolution** (2 days)
   - Pathfinding between editions
   - Breaking change detection
   - Compatibility checking

4. **Testing** (1 day)
   - Edition lifecycle tests
   - Migration path tests
   - Compatibility matrix tests

#### Acceptance Criteria

- [ ] Protocol editions tracked over time
- [ ] Deprecation schedules enforced
- [ ] Migration paths computable
- [ ] Compatibility matrix queryable
- [ ] All tests passing (target: 15+ tests)

#### References

- BC-004: Protocol Evolution Context
- FPF Spec: B.4 Evolution Loop
- FPF Spec: B.1.4 Temporal Aggregation
- DRR-004: Protocol Evolution Stability

---

### Item 4: CHR-CAL Framework

**Priority**: Low  
**FPF Pattern**: C.7 (CHR-CAL), C.16 (MM-CHR)  
**Estimated Effort**: 6-8 days  
**Prerequisites**: None (standalone)

#### Current State

Two CHRs (Characteristics) are implemented ad-hoc:

- `Formality` (F0..F9)
- `AssuranceLevel` (L0..L2)

These work but don't follow a general CHR-CAL pattern. No framework for defining new characteristics.

#### Proposed Implementation

**Files to Create**:

- `sentinel/src/Acp.Characteristic.fs` (new framework, ~400 lines)
- `sentinel/tests/Acp.CharacteristicTests.fs` (new tests, ~300 lines)

**Type Structure**:

```fsharp
module Characteristic =
    /// CSLC = Characteristic-Scale-Level-Coordinate (FPF A.18)
    type Scale =
        | Nominal of categories: string list
        | Ordinal of levels: string list
        | Interval of min: float * max: float * unit: string
        | Ratio of min: float * max: float * unit: string

    type Coordinate<'T> =
        { value: 'T
          scale: Scale
          timestamp: DateTimeOffset option
          uncertainty: float option }

    type Characteristic<'T> =
        { name: string
          definition: string
          scale: Scale
          measure: 'T -> Coordinate<'T>
          compare: Coordinate<'T> -> Coordinate<'T> -> int }

    module Characteristic =
        /// Define a new characteristic
        val define : string -> string -> Scale -> ('T -> Coordinate<'T>) -> Characteristic<'T>
        
        /// Compare two measurements
        val compare : Characteristic<'T> -> 'T -> 'T -> int
        
        /// Aggregate measurements
        val aggregate : Characteristic<'T> -> 'T list -> Coordinate<'T>

    /// Standard CHRs (migrate existing)
    module StandardCHRs =
        val Formality : Characteristic<Formality>
        val AssuranceLevel : Characteristic<AssuranceLevel>
        val CongruenceLevel : Characteristic<CongruenceLevel>
```

#### Implementation Steps

1. **CHR Framework Core** (3 days)
   - CSLC types (Characteristic-Scale-Level-Coordinate)
   - Generic Characteristic<'T> type
   - Measurement and comparison functions

2. **Migrate Existing CHRs** (2 days)
   - Reimplement Formality using framework
   - Reimplement AssuranceLevel using framework
   - Add CongruenceLevel as CHR

3. **Standard Operations** (2 days)
   - Aggregation functions
   - Uncertainty propagation
   - Temporal decay

4. **Testing & Documentation** (1 day)
   - Framework tests
   - Migration tests
   - Examples of defining custom CHRs

#### Acceptance Criteria

- [ ] CHR-CAL framework compiles
- [ ] Existing CHRs migrate without breaking changes
- [ ] Can define new characteristics using framework
- [ ] Aggregation works for all scale types
- [ ] All tests passing (target: 30+ tests)

#### References

- FPF Spec: C.7 CHR-CAL
- FPF Spec: C.16 MM-CHR
- FPF Spec: A.17 Characteristic rename
- FPF Spec: A.18 CSLC-KERNEL

---

## Progress Tracking

| Item | Priority | Effort | Status | Started | Completed |
| ---- | -------- | ------ | ------ | ------- | --------- |
| U.Episteme Runtime | Medium | 5-7 days | 🔲 Not Started | - | - |
| DRR-005 Implementation | Medium | 3-5 days | 🔲 Not Started | - | - |
| Evolution Tracking | Low | 4-6 days | 🔲 Not Started | - | - |
| CHR-CAL Framework | Low | 6-8 days | 🔲 Not Started | - | - |

**Total Estimated Effort**: 18-26 days

---

## Success Criteria

### Phase 3 Complete When

- [ ] U.Episteme type is runtime-queryable
- [ ] ToolCallPlan validation integrated into sentinel
- [ ] Protocol editions tracked with migration paths
- [ ] CHR-CAL framework supports custom characteristics
- [ ] FPF alignment score reaches **95%+**
- [ ] All Phase 3 tests passing (target: 90+ new tests)
- [ ] Documentation updated with new patterns

---

## Recommended Implementation Order

1. **DRR-005 Implementation** (Item 2) - Medium priority, clear spec, immediate value
2. **U.Episteme Runtime** (Item 1) - Builds on Evidence Graph, enables advanced queries
3. **CHR-CAL Framework** (Item 4) - Standalone, improves measurement discipline
4. **Evolution Tracking** (Item 3) - Lowest priority, requires broader protocol stability

---

## Dependencies & Risks

### Dependencies

- All items depend on TASK-002 completion ✅
- Item 1 (U.Episteme) requires Evidence Graph ✅
- Item 2 (DRR-005) requires DRR-005 document ✅
- Item 3 (Evolution) requires stable protocol adapter (in progress)
- Item 4 (CHR-CAL) has no blockers

### Risks

- **Scope creep**: Each item could expand significantly if not carefully scoped
- **Integration complexity**: U.Episteme touches many modules
- **Protocol churn**: Evolution tracking may need redesign if protocol changes
- **Framework over-engineering**: CHR-CAL could become too abstract

### Mitigation

- Start with MVPs for each item
- Incremental implementation with tests at each step
- Regular alignment checks against FPF spec
- User feedback before expanding scope

---

## FPF Alignment Impact

**Current** (after TASK-002): ~92%  
**After Phase 3**: ~95%+

### Pattern Coverage After Phase 3

**Fully Implemented**:

- C.2.1 U.Episteme ✅
- C.24 Agent-Tools-CAL ✅
- B.4 Evolution Loop ✅
- C.7 CHR-CAL ✅
- C.16 MM-CHR ✅
- Plus all patterns from Phases 1 & 2

**Remaining Gaps**:

- Part D (Ethics & Conflict) - Out of scope
- Advanced LOG-CAL - Future research
- Multi-agent coordination - Protocol extension needed

---

## Notes

- **Not Blocking**: Phase 3 is enhancement, not foundation
- **Research-Grade**: These patterns enable epistemic research
- **Production-Optional**: Core functionality works without Phase 3
- **FPF Reference**: Continue using `/tmp/FPF-YYYYMMDD` daily fetch

---

## References

- [TASK-002: FPF Alignment Improvements](TASK-002-fpf-alignment-improvements.md) (Phases 1 & 2)
- [FPF Alignment Evaluation Report](../reports/fpf-alignment-evaluation-20260106.md)
- [DRR-005: Agent Tool Coordination](../drr/DRR-005-agent-tool-coordination.md)
- [BC-004: Protocol Evolution Context](../contexts/BC-004-protocol-evolution.md)
- [FPF Spec (external)](https://github.com/ailev/FPF)
- [Tech Debt Backlog](BACKLOG-tech-debt.md)

---

**Status**: Phase 3 items are well-specified and ready for implementation when needed. Not blocking current development.

**Next Action**: Archive TASK-002 as complete. Phase 3 remains as future work documented here.
