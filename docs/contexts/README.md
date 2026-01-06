# Bounded Contexts (U.BoundedContext)

This directory contains BoundedContext specifications following FPF A.1.1 pattern.

## What is a BoundedContext?

Per FPF A.1.1: *"Meaning is local. A term is defined strictly within a U.BoundedContext. Cross-context communication happens only via explicit Bridges with declared translation loss."*

Each BoundedContext defines:
- **Context Identity**: URI, name, scope, owner
- **Vocabulary**: Local glossary of terms with FPF mappings
- **Kind Signatures**: Formal type definitions (intension + extension)
- **Invariants**: Rules that must hold within the context
- **Bridges**: Mappings to other contexts with Congruence Level (CL)

## Context Index

| ID | Context | Scope | Related DRR |
|----|---------|-------|-------------|
| [BC-001](BC-001-assurance.md) | Assurance | Trust reasoning over agent messages | DRR-001 |
| [BC-002](BC-002-semantic-alignment.md) | Semantic Alignment | Cross-agent meaning coordination | DRR-002 |
| [BC-003](BC-003-capability-verification.md) | Capability Verification | Agent capability claims & testing | DRR-003 |
| [BC-004](BC-004-protocol-evolution.md) | Protocol Evolution | Spec versioning & conformance | DRR-004 |

## Bridge Matrix

Congruence Levels between contexts (CL0=incompatible, CL4=equivalent):

```
              │ Assurance │ Semantic │ Capability │ Evolution │ Protocol
──────────────┼───────────┼──────────┼────────────┼───────────┼──────────
Assurance     │    -      │   CL4    │    CL4     │    CL4    │   CL3
Semantic      │   CL4     │    -     │    CL3     │    CL3    │   CL2
Capability    │   CL4     │   CL3    │     -      │     -     │   CL2
Evolution     │   CL4     │   CL3    │     -      │     -     │   CL2
Protocol      │   CL3     │   CL2    │    CL2     │    CL2    │    -
```

**Key observation**: All ACP-Sentinel contexts have high mutual alignment (CL3-CL4), but significant loss when bridging to the ACP Protocol core (CL2-CL3). This reflects the gap between what the protocol currently supports and what rigorous assurance requires.

## Context Hierarchy

```
                    ┌─────────────────────┐
                    │   ACP Protocol      │
                    │   (external, CL2)   │
                    └─────────┬───────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
        ▼                     ▼                     ▼
┌───────────────┐   ┌─────────────────┐   ┌─────────────────┐
│  BC-001       │   │  BC-002         │   │  BC-004         │
│  Assurance    │◄──│  Semantic       │   │  Evolution      │
│  (F-G-R)      │   │  (Bridges, CL)  │   │  (Editions)     │
└───────┬───────┘   └────────┬────────┘   └─────────────────┘
        │                    │
        │     CL4            │ CL3
        ▼                    ▼
┌───────────────────────────────────────┐
│           BC-003                      │
│           Capability Verification     │
│           (Claims, Tests, Envelopes)  │
└───────────────────────────────────────┘
```

## Shared Concepts Across Contexts

These terms have consistent meaning across all ACP-Sentinel contexts:

| Term | Universal Meaning | Origin |
|------|-------------------|--------|
| **AssuranceLevel** | L0/L1/L2 ordinal | BC-001 → all |
| **Evidence** | Grounded artifact supporting claims | BC-001 → all |
| **Invariant** | Rule that must hold | FPF A.1.1 |
| **KindSignature** | Type definition | BC-002 → all |
| **CongruenceLevel** | CL0..CL4 translation fidelity | BC-002 → all |

## Per-Context URIs

```
urn:acp-sentinel:context:assurance:v1
urn:acp-sentinel:context:semantic:v1
urn:acp-sentinel:context:capability:v1
urn:acp-sentinel:context:evolution:v1
urn:acp:protocol:v1  (external)
```

## FPF Pattern Coverage

| FPF Pattern | Used In |
|-------------|---------|
| A.1.1 U.BoundedContext | All (structure) |
| A.2.2 U.Capability | BC-003 |
| A.2.3 U.ServiceClause | BC-003 |
| A.2.6 USM (Scope) | BC-001, BC-002 |
| B.3 Trust Calculus | BC-001, BC-003 |
| B.3.3 Assurance Levels | BC-001, BC-003, BC-004 |
| B.4 Evolution Loop | BC-004 |
| C.2 KD-CAL | BC-001 |
| C.3.2 KindSignature | BC-002 |
| C.3.3 KindBridge | BC-002 |
| E.9 DRR | BC-004 |
| F.9 Bridges & CL | BC-002 (core), all (bridges) |
| G.6 EvidenceGraph | BC-001 |

## Contributing

When adding or modifying a BoundedContext:

1. **Define clear boundaries**: What's inside vs outside this context?
2. **Local vocabulary first**: Define terms locally before mapping to FPF
3. **Explicit bridges**: Every cross-context reference needs a bridge with CL
4. **Invariants are testable**: Each INV-* should be enforceable by Sentinel
5. **Usage examples**: Include JSON examples showing protocol extensions
6. **F# validation rules**: Provide sentinel implementation sketches
