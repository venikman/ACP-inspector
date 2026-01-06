# Design-Rationale Records (DRRs)

This directory contains DRRs following FPF's E.9 pattern, grounded in David Deutsch's epistemology.

## Problem Selection Criteria

Per Deutsch (*The Beginning of Infinity*):

- **Easy-to-vary explanations** → weak understanding, need better theory
- **Lack of reach** → ad-hoc solutions that don't generalize
- **Inexplicable rules** → parochial constraints without justification
- **Error-correction gaps** → broken feedback loops

Per FPF:

- **F-G-R deficiency** → claims lack Formality, bounded Scope, or Reliability
- **Missing BoundedContext** → meaning assumed universal when it's local
- **No EvidenceGraph** → claims unanchored to verifiable evidence
- **Temporal disorder** → evolution without DRR discipline

## DRR Index

| ID | Problem | Deutsch Signal | FPF Diagnosis | Status |
| ---- | ------- | -------------- | ------------- | ------ |
| [DRR-001](DRR-001-agent-output-trustworthiness.md) | Agent Output Trustworthiness | Easy-to-vary ("model is good") | F-G-R maximal deficiency | Proposed |
| [DRR-002](DRR-002-cross-agent-semantic-alignment.md) | Cross-Agent Semantic Alignment | No reach (protocol-specific) | Missing KindBridge, CL ignored | Proposed |
| [DRR-003](DRR-003-capability-claim-verification.md) | Capability Claim Verification | Inexplicable rules ("trust manifest") | No TA/VA/LA lanes | Proposed |
| [DRR-004](DRR-004-protocol-evolution-stability.md) | Protocol Evolution Stability | Error-correction gap | No Γ_time, no DRR discipline | Proposed |

## Dependency Graph

```text
DRR-001 (Trustworthiness)
    ↓
    ├──→ DRR-002 (Semantic Alignment) ──→ DRR-003 (Capability Verification)
    │
    └──→ DRR-004 (Evolution Stability)
```

## FPF Pattern Coverage

| FPF Pattern | Used In |
| ----------- | ------- |
| A.1.1 U.BoundedContext | DRR-002 |
| A.2.2 U.Capability | DRR-003 |
| A.2.3 U.ServiceClause | DRR-003 |
| A.4 Temporal Duality | DRR-004 |
| B.3 Trust & Assurance | DRR-001, DRR-003 |
| B.4 Evolution Loop | DRR-004 |
| B.5.2 Abductive Loop | DRR-001 |
| C.2 KD-CAL | DRR-001 |
| C.3.3 KindBridge | DRR-002 |
| E.9 DRR Method | DRR-004 |
| F.9 Bridges & CL | DRR-002 |
| G.6 EvidenceGraph | DRR-001 |

## Status Legend

- **Proposed**: Initial draft, open for criticism
- **Accepted**: Team consensus, ready for implementation
- **Implemented**: Code exists
- **Superseded**: Replaced by newer DRR

## Contributing

When adding a DRR:

1. Use template from FPF E.9
2. Ground in Deutsch epistemology (what makes current explanation "easy to vary"?)
3. Diagnose via FPF (which patterns are missing or deficient?)
4. Propose direction with consequences and alternatives
5. Link dependencies to existing DRRs
