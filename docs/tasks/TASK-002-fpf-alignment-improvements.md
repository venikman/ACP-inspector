# TASK-002: FPF Alignment Improvements

**Status**: 🟡 In Progress  
**Priority**: High  
**Assignee**: Team  
**Started**: 2026-01-06  
**Based On**: [FPF Alignment Evaluation Report](../reports/fpf-alignment-evaluation-20260106.md)

## Objective

Implement critical gaps identified in the FPF alignment evaluation to strengthen runtime assurance and context enforcement.

## Context

The FPF alignment evaluation (2026-01-06) found **85-90% alignment** with strong specification quality but identified runtime implementation gaps. This task addresses the highest-priority improvements.

## Implementation Plan

### Phase 1: Foundation (Immediate)

#### ✅ Item 1: Document FPF Fetch Workflow

**Status**: Complete  
**Files**: `README.md`, `docs/reports/fpf-alignment-evaluation-20260106.md`  
**Completed**: 2026-01-06

- [x] Update README.md with FPF daily fetch workflow
- [x] Create evaluation report
- [x] Link report in documentation section

---

#### ✅ Item 2: Runtime Evidence Decay (INV-ASR-03)

**Status**: Complete  
**Completed**: 2026-01-06  
**Priority**: High  
**FPF Pattern**: B.3.4 (Evidence Decay & Epistemic Debt)  
**Files**: `src/Acp.Assurance.fs`, `tests/Acp.AssuranceTests.fs`

**Specification** (from BC-001-assurance.md:129-138):

```text
INV-ASR-03: Evidence Freshness
∀ evidence ∈ EvidenceGraph:
  now() - evidence.timestamp ≤ evidence.freshnessWindow
  OR evidence.status = Stale
```

**Implementation Steps**:

1. Add `EvidenceStatus` discriminated union: `Fresh | Stale`
2. Extend `Reliability` type with computed `status` field
3. Add `isStale :: Reliability -> DateTimeOffset -> bool` function
4. Implement decay check in validation pipeline
5. Add tests for freshness window boundaries

**Files to Edit**:

- `src/Acp.Assurance.fs` - Add status type and decay logic
- `tests/Acp.AssuranceTests.fs` - Add decay tests

**Acceptance Criteria**:

- Evidence older than `freshnessWindow` marked as Stale
- Validation findings generated for stale evidence
- Tests cover edge cases (no window, expired, fresh)

---

#### ✅ Item 3: CL Penalty Computation

**Status**: Complete  
**Completed**: 2026-01-06  
**Priority**: High  
**FPF Pattern**: C.2.2 (Reliability R with CL penalties), F.9 (Bridges & CL)  
**Files**: `src/Acp.Assurance.fs`, `src/Acp.Semantic.fs`

**Specification** (from BC-001-assurance.md:196-200):

```yaml
- source: Reliability (R)
  target: (degraded by CL penalty)
  lossNotes: "R_effective = R × CL_penalty when crossing contexts"
```

**Implementation Steps**:

1. Add `applyClPenalty :: Reliability -> CongruenceLevel -> Reliability`
2. Implement penalty multiplication (CL4=1.0, CL3=0.9, CL2=0.7, CL1=0.4, CL0=0.0)
3. Add to `KindBridge` module as bridge operation
4. Document penalty rationale in code comments
5. Add tests for each CL level

**Files to Edit**:

- `src/Acp.Assurance.fs` - Add penalty function
- `src/Acp.Semantic.fs` - Integrate with KindBridge
- `tests/Acp.SemanticTests.fs` - Add penalty tests

**Acceptance Criteria**:

- Reliability degraded when crossing bridges
- CL penalties match FPF spec (from CongruenceLevel.penalty)
- Round-trip through CL4 bridge preserves reliability
- CL0 bridge zeros out reliability

---

### Phase 2: Core Infrastructure (Near-Term)

#### ✅ Item 4: Evidence Graph Builder

**Status**: Complete  
**Completed**: 2026-01-06  
**Priority**: High  
**FPF Pattern**: G.6 (EvidenceGraph), A.10 (Evidence Graph Referring)  
**Files**: `src/Acp.Assurance.fs`, new `src/Acp.EvidenceGraph.fs`

**Current State**: Only PathId references exist; no graph structure.

**Implementation Steps**:

1. Define `EvidenceGraph` type with adjacency list
2. Implement `addEdge :: Claim -> Evidence -> EvidenceGraph -> EvidenceGraph`
3. Implement `computeWeakestLink :: ClaimId -> EvidenceGraph -> AssuranceLevel option`
4. Add DAG validation (acyclic check)
5. Implement graph traversal for path resolution
6. Add serialization/deserialization (JSON)

**Files to Create/Edit**:

- `src/Acp.EvidenceGraph.fs` (new) - Graph structure and operations
- `src/Acp.Assurance.fs` - Integrate with Reliability type
- `tests/Acp.EvidenceGraphTests.fs` (new) - Graph tests

**Acceptance Criteria**:

- Evidence paths can be constructed programmatically
- Weakest-link computation matches INV-ASR-02
- DAG validation catches cycles
- PathId resolution returns full evidence chain

---

#### ✅ Item 5: Context Boundary Enforcement

**Status**: Complete  
**Completed**: 2026-01-06  
**Priority**: Medium  
**FPF Pattern**: A.1.1 (U.BoundedContext), F.9 (Bridges)  
**Files**: `src/Acp.Semantic.fs`, `src/Acp.Validation.fs`

**Current State**: Contexts documented, boundaries not enforced at runtime.

**Implementation Steps**:

1. Add `validateContextBoundary :: ContextId -> KindId -> Result<unit, string>`
2. Implement cross-context reference checker
3. Add bridge requirement validation
4. Integrate into validation pipeline
5. Generate `ValidationFinding.UnbridgedReference` for violations

**Files to Edit**:

- `src/Acp.Semantic.fs` - Add boundary checks
- `src/Acp.Validation.fs` - Integrate into validation lanes
- `tests/Acp.SemanticTests.fs` - Add boundary tests

**Acceptance Criteria**:

- Cross-context references without bridges rejected
- Bridge availability checked before reference
- Validation findings include missing bridge info
- Test cases for valid/invalid cross-context references

---

#### ✅ Item 6: DRR-005 - Adopt C.24 Agent-Tools-CAL

**Status**: Complete (DRR Documented)  
**Completed**: 2026-01-06  
**Priority**: Medium  
**FPF Pattern**: C.24 (Agent-Tools-CAL)  
**Files**: New `docs/drr/DRR-005-agent-tool-coordination.md`

**Rationale** (from evaluation report):
> ACP is fundamentally about tool-use coordination. C.24 provides:
>
> - Call-planning discipline
> - Budget-aware sequencing  
> - Policy integration (BLP, SLL)

**Implementation Steps**:

1. Create DRR-005 following E.9 structure
2. Analyze current tool call validation gaps
3. Propose C.24 integration approach
4. Define ToolCallPlan type
5. Document budget/policy hooks
6. Get team review/approval before implementation

**Files to Create**:

- `docs/drr/DRR-005-agent-tool-coordination.md` (new)

**Acceptance Criteria**:

- DRR follows FPF E.9 structure
- Deutsch framing identifies "easy to vary" explanations
- FPF diagnosis maps to C.24 patterns
- Concrete proposal with consequences
- Alternatives considered

---

### Phase 3: Advanced (Long-Term)

#### 🔲 Item 7: U.Episteme Runtime Type

**Priority**: Low  
**FPF Pattern**: C.2.1 (U.Episteme slot graph)  
**Files**: New `src/Acp.Episteme.fs`

**Current State**: Episteme slot structure documented in BC-001, not implemented as F# type.

**Implementation Steps**:

1. Define `Episteme` record type with slots
2. Implement `DescribedEntitySlot`, `GroundingHolonSlot`, `ClaimGraphSlot`, `ViewpointSlot`
3. Add episteme construction/query functions
4. Integrate with Assurance types
5. Enable runtime episteme composition

**Deferred**: Requires Phase 2 (Evidence Graph) completion first.

---

#### 🔲 Item 8: Evolution State Tracking (BC-004)

**Priority**: Low  
**FPF Pattern**: B.4 (Evolution Loop), BC-004 (Protocol Evolution)

**Deferred**: Requires stable protocol adapter implementation first.

---

#### 🔲 Item 9: CHR-CAL Framework

**Priority**: Low  
**FPF Pattern**: C.7 (CHR-CAL), C.16 (MM-CHR)

**Deferred**: Current ad-hoc CHRs (Formality, AssuranceLevel) sufficient for now.

---

## Progress Tracking

| Phase | Item | Status | Started | Completed |
| ----- | ---- | ------ | ------- | --------- |
| 1 | FPF Workflow Docs | ✅ Complete | 2026-01-06 | 2026-01-06 |
| 1 | Evidence Decay | ✅ Complete | 2026-01-06 | 2026-01-06 |
| 1 | CL Penalty | ✅ Complete | 2026-01-06 | 2026-01-06 |
| 2 | Evidence Graph | ✅ Complete | 2026-01-06 | 2026-01-06 |
| 2 | Context Boundaries | ✅ Complete | 2026-01-06 | 2026-01-06 |
| 2 | DRR-005 (C.24) | ✅ Complete | 2026-01-06 | 2026-01-06 |
| 3 | U.Episteme Type | 🔲 Deferred | - | - |
| 3 | Evolution Tracking | 🔲 Deferred | - | - |
| 3 | CHR-CAL | 🔲 Deferred | - | - |

---

## Success Criteria

### Phase 1 Complete When

- [ ] Evidence decay validated in runtime
- [ ] CL penalties computed when crossing bridges
- [ ] All Phase 1 tests passing
- [ ] No new compiler warnings

### Phase 2 Complete When

- [ ] Evidence graphs constructible programmatically
- [ ] Context boundaries enforced at runtime
- [ ] DRR-005 approved and documented
- [ ] Validation findings include boundary/bridge violations

### Overall Success

- [ ] FPF alignment score increases from 85% → 92%+
- [ ] All critical gaps (🔴) addressed
- [ ] Runtime enforcement matches specification rigor

---

## Notes

- **Dependencies**: Items 2-3 must complete before Item 4
- **FPF Reference**: Use `/tmp/FPF-YYYYMMDD` daily fetch
- **Testing**: Each item requires passing tests before marking complete
- **DRR First**: For Item 6, DRR must be approved before implementation

---

## References

- [FPF Alignment Evaluation Report](../reports/fpf-alignment-evaluation-20260106.md)
- [BC-001: Assurance Context](../contexts/BC-001-assurance.md)
- [BC-002: Semantic Alignment](../contexts/BC-002-semantic-alignment.md)
- [FPF Spec (external)](https://github.com/ailev/FPF)
- [Tech Debt Backlog](BACKLOG-tech-debt.md)

---

**Next Action**: Start with Item 2 (Evidence Decay) - high priority, clear specification, minimal dependencies.
