# BC-004: Protocol Evolution Context

**FPF Pattern**: A.1.1 U.BoundedContext  
**Related DRR**: [DRR-004](../drr/DRR-004-protocol-evolution-stability.md)  
**Status**: Draft  
**Version**: 0.1.0

## Context Identity

```yaml
contextId: "urn:acp-sentinel:context:evolution:v1"
contextName: "ACP Protocol Evolution Context"
scope: "Structured evolution of protocol specifications and implementations"
owner: "ACP-Sentinel Project"
```

## Purpose

This bounded context defines the semantic frame for reasoning about **protocol evolution**. It provides vocabulary and rules for:
- Tracking protocol editions with immutable identifiers
- Classifying changes as breaking or non-breaking
- Recording rationale for changes (DRR discipline)
- Verifying implementation conformance to editions
- Managing deprecation and sunset of old editions

## Vocabulary (Local Glossary)

### Core Terms

| Term | Local Definition | FPF Mapping | Notes |
|------|------------------|-------------|-------|
| **Edition** | An immutable snapshot of the protocol specification | (local term) | Beyond simple version |
| **EditionId** | Content-addressable identifier for an edition | (local term) | UUID or hash |
| **ChangeKind** | Classification of change impact: Patch/Minor/Major | SemVer-aligned | Determines compatibility |
| **BreakingChange** | Change that violates backward compatibility | (local term) | Enumerated |
| **DRR** | Design-Rationale Record documenting change justification | E.9 DRR | Required for all changes |
| **ConformanceLevel** | Degree of implementation fidelity: Declared/Tested/Certified | Maps to L0/L1/L2 | Progressive assurance |
| **CompatibilityMatrix** | Declared compatibility relationships between editions | (local term) | Protocol artifact |
| **SchemaHash** | Content-addressable hash of edition schema | (local term) | SHA256 |
| **SemanticDelta** | Meaning change without schema change | (local term) | Hardest to detect |

### Derived Terms

| Term | Definition | Derivation |
|------|------------|------------|
| **EditionLineage** | Directed graph of edition → parentEdition | Tree of editions |
| **BreakingPath** | Sequence of editions where at least one is breaking | Path with Major change |
| **ConformanceGap** | Difference between claimed and observed conformance | Evidence of drift |
| **EvolutionVelocity** | Rate of edition change over time | Editions per time unit |

## Kind Signatures

### Edition

```
Kind: Edition
Intension: Immutable snapshot of protocol specification at a point in time
Slots:
  - editionId: UUID (unique, immutable)
  - version: SemVer (human-readable)
  - parentEdition: EditionId? (what this evolved from)
  - schemaHash: SHA256 (content-addressable)
  - changeKind: ChangeKind (from parent)
  - drrRefs: List<DRR-ID> (rationale records)
  - compatibilityMatrix: CompatibilityMatrix
  - publishedAt: Timestamp
  - deprecatedAt: Timestamp?
  - sunsetAt: Timestamp?
  
Invariant: editionId is globally unique and immutable
Invariant: parentEdition = None ⟹ changeKind = Major (genesis)
Invariant: drrRefs.nonEmpty (no change without rationale)
```

### ChangeKind

```
Kind: ChangeKind
Intension: Classification of change impact on compatibility
Extension: { Patch, Minor, Major }
SubkindOf: EnumeratedKind

Patch := Bug fix, no API change, backward compatible
Minor := Additive change, backward compatible
Major := Breaking change, not backward compatible
```

### BreakingChange

```
Kind: BreakingChange
Intension: Enumeration of changes that break backward compatibility
Extension: {
  RequiredFieldAdded,
  FieldRemoved,
  TypeNarrowed,
  EnumValueRemoved,
  SemanticChange,
  BehaviorChange
}
SubkindOf: EnumeratedKind

Each has slots:
  - path: JsonPath (location in schema)
  - description: String
  - migrationPath: MigrationSpec? (how to adapt)
```

### NonBreakingChange

```
Kind: NonBreakingChange
Intension: Enumeration of backward-compatible changes
Extension: {
  OptionalFieldAdded,
  TypeWidened,
  EnumValueAdded,
  DeprecationMarked,
  DocumentationUpdated
}
SubkindOf: EnumeratedKind
```

### CompatibilityMatrix

```
Kind: CompatibilityMatrix
Intension: Declared compatibility relationships between editions
Slots:
  - editionId: EditionId (subject edition)
  - backwardCompatibleWith: Set<EditionId>
  - forwardCompatibleWith: Set<EditionId>
  - breakingFrom: Set<EditionId>
  
Invariant: editionId ∉ backwardCompatibleWith ∪ forwardCompatibleWith ∪ breakingFrom
Invariant: breakingFrom ∩ backwardCompatibleWith = ∅
```

### ConformanceReport

```
Kind: ConformanceReport
Intension: Record of implementation conformance to an edition
Slots:
  - implementationId: String (SDK/agent identifier)
  - claimedEdition: EditionId
  - observedConformance: ConformanceLevel
  - deviations: List<Deviation>
  - testResults: URI?
  - reportedAt: Timestamp
  
Invariant: observedConformance ≤ claimedConformance OR deviations.nonEmpty
```

### DRR-Reference

```
Kind: DRR-Reference
Intension: Link to a Design-Rationale Record
Slots:
  - drrId: String (e.g., "DRR-004")
  - uri: URI (location of DRR document)
  - summary: String (one-line)
  
Invariant: uri resolves to valid DRR document
```

## Invariants

### INV-EVO-01: No Rationale-Free Changes
```
∀ edition E where E.parentEdition ≠ None:
  E.drrRefs.nonEmpty
  
Rationale: Every change must have documented justification (FPF E.9)
Violation: EvolutionFinding.MissingRationale
```

### INV-EVO-02: ChangeKind Accuracy
```
∀ edition E:
  E.changeKind = Major ⟺ 
    ∃ bc ∈ BreakingChange: bc applies between E.parentEdition and E
    
Rationale: Major versions must have breaking changes (and vice versa)
Violation: EvolutionFinding.ChangeKindMismatch
```

### INV-EVO-03: Compatibility Matrix Transitivity
```
∀ editions A, B, C:
  A.backwardCompatibleWith(B) ∧ B.backwardCompatibleWith(C)
  ⟹ A.backwardCompatibleWith(C)
  
Rationale: Compatibility is transitive
Violation: EvolutionFinding.IntransitiveCompatibility
```

### INV-EVO-04: Schema Hash Integrity
```
∀ edition E:
  SHA256(E.schema) = E.schemaHash
  
Rationale: Content-addressability requires hash accuracy
Violation: EvolutionFinding.SchemaHashMismatch
```

### INV-EVO-05: Deprecation Precedes Sunset
```
∀ edition E:
  E.sunsetAt ≠ None ⟹ E.deprecatedAt ≠ None ∧ E.deprecatedAt < E.sunsetAt
  
Rationale: Cannot sunset without deprecation warning
Violation: EvolutionFinding.SunsetWithoutDeprecation
```

### INV-EVO-06: Conformance Evidence for Tested+
```
∀ report R:
  R.observedConformance ≥ Tested ⟹ R.testResults ≠ None
  
Rationale: Tested conformance requires test evidence
Violation: EvolutionFinding.ConformanceWithoutEvidence
```

## Bridges to Other Contexts

### Bridge: Evolution ↔ Assurance (BC-001)

```yaml
bridgeId: "bridge:evolution-assurance"
sourceContext: "urn:acp-sentinel:context:evolution:v1"
targetContext: "urn:acp-sentinel:context:assurance:v1"
congruenceLevel: CL4

mappings:
  - source: ConformanceLevel.Declared
    target: AssuranceLevel.L0
    
  - source: ConformanceLevel.Tested
    target: AssuranceLevel.L1
    
  - source: ConformanceLevel.Certified
    target: AssuranceLevel.L2
    
  - source: DRR
    target: Evidence (rationale as evidence)
```

### Bridge: Evolution ↔ Protocol (ACP Core)

```yaml
bridgeId: "bridge:evolution-protocol"
sourceContext: "urn:acp-sentinel:context:evolution:v1"
targetContext: "urn:acp:protocol:v1"
congruenceLevel: CL2

mappings:
  - source: Edition
    target: protocolVersion (partial)
    lossNotes: "Protocol has single integer version; edition has rich metadata"
    
  - source: CompatibilityMatrix
    target: (no mapping)
    lossNotes: "Protocol has no compatibility negotiation"
    
  - source: ConformanceClaim
    target: (extension required)
    lossNotes: "Must add editionClaim to Initialize"
```

### Bridge: Evolution ↔ Semantic (BC-002)

```yaml
bridgeId: "bridge:evolution-semantic"
sourceContext: "urn:acp-sentinel:context:evolution:v1"
targetContext: "urn:acp-sentinel:context:semantic:v1"
congruenceLevel: CL3

mappings:
  - source: SemanticDelta
    target: SemanticDrift
    lossNotes: "Intentional semantic change vs unintentional drift"
    
  - source: Edition.schemaHash
    target: SemanticFingerprint (partial)
    lossNotes: "Schema hash doesn't capture semantic-only changes"
```

## Evolution Workflow

### The Canonical Evolution Loop (FPF B.4)

```
┌─────────────────────────────────────────────────────────────┐
│                    EVOLUTION LOOP                           │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│   ┌──────────┐    ┌──────────┐    ┌──────────┐    ┌──────────┐
│   │   RUN    │───▶│ OBSERVE  │───▶│  REFINE  │───▶│  DEPLOY  │
│   │          │    │          │    │          │    │          │
│   │ Current  │    │ Collect  │    │ Propose  │    │ Release  │
│   │ edition  │    │ findings │    │ via DRR  │    │ new      │
│   │ deployed │    │ bugs,    │    │ classify │    │ edition  │
│   │          │    │ gaps,    │    │ change   │    │          │
│   └──────────┘    │ drift    │    │ kind     │    └──────────┘
│        ▲          └──────────┘    └──────────┘         │
│        │                                               │
│        └───────────────────────────────────────────────┘
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### Change Classification Algorithm

```fsharp
let classifyChange (oldSchema: Schema) (newSchema: Schema) : ChangeKind =
    let diff = computeSchemaDiff oldSchema newSchema
    
    let hasBreaking = 
        diff.changes |> List.exists (fun c ->
            match c with
            | RequiredFieldAdded _ -> true
            | FieldRemoved _ -> true
            | TypeNarrowed _ -> true
            | EnumValueRemoved _ -> true
            | _ -> false)
    
    let hasAdditive =
        diff.changes |> List.exists (fun c ->
            match c with
            | OptionalFieldAdded _ -> true
            | TypeWidened _ -> true
            | EnumValueAdded _ -> true
            | _ -> false)
    
    match hasBreaking, hasAdditive with
    | true, _ -> Major
    | false, true -> Minor
    | false, false -> Patch
```

## Protocol Extensions

### Edition Claim in Initialize

```typescript
interface InitializeParams {
  protocolVersion: number;  // Existing
  editionClaim?: EditionClaim;  // New
  // ...
}

interface EditionClaim {
  editionId: string;  // UUID
  conformanceLevel: "declared" | "tested" | "certified";
  testResultsRef?: string;  // URI
}
```

### Edition Registry Query

```typescript
// Query available editions
interface QueryEditionsRequest {
  type: "query_editions";
  params: {
    compatibleWith?: string;  // EditionId
    since?: string;  // ISO8601
    includeDeprecated?: boolean;
  };
}

interface QueryEditionsResponse {
  type: "query_editions_result";
  result: {
    editions: EditionSummary[];
  };
}

interface EditionSummary {
  editionId: string;
  version: string;
  changeKind: "patch" | "minor" | "major";
  publishedAt: string;
  deprecatedAt?: string;
  compatibilityMatrix: {
    backwardCompatibleWith: string[];
    breakingFrom: string[];
  };
}
```

## Sentinel Validation Rules

```fsharp
module EvolutionValidation =
    
    /// Validate edition has rationale
    let validateRationale (edition: Edition) : EvolutionFinding list =
        match edition.parentEdition, edition.drrRefs with
        | Some _, [] -> [ MissingRationale edition.editionId ]
        | _ -> []
    
    /// Validate change kind accuracy
    let validateChangeKind 
        (parent: Edition) 
        (child: Edition) 
        : EvolutionFinding list =
        
        let actualKind = classifyChange parent.schema child.schema
        if actualKind <> child.changeKind then
            [ ChangeKindMismatch (child.editionId, child.changeKind, actualKind) ]
        else []
    
    /// Validate conformance claim against observed behavior
    let validateConformance 
        (claim: EditionClaim) 
        (trace: MessageTrace) 
        (edition: Edition) 
        : ConformanceReport =
        
        let deviations = findSchemaDeviations trace edition.schema
        let observedLevel =
            if deviations.IsEmpty then claim.conformanceLevel
            else Declared
        
        { implementationId = trace.agentId
          claimedEdition = claim.editionId
          observedConformance = observedLevel
          deviations = deviations
          testResults = claim.testResultsRef
          reportedAt = DateTime.UtcNow }
    
    /// Compute edition lineage
    let lineage (edition: Edition) (registry: EditionRegistry) : Edition list =
        let rec walk e acc =
            match e.parentEdition with
            | None -> e :: acc
            | Some parentId ->
                match registry.tryFind parentId with
                | None -> e :: acc  // Orphaned edition
                | Some parent -> walk parent (e :: acc)
        walk edition []
```

## Usage Examples

### Example 1: Edition Registration

```json
{
  "type": "register_edition",
  "params": {
    "editionId": "550e8400-e29b-41d4-a716-446655440000",
    "version": "0.11.0",
    "parentEdition": "440e8400-e29b-41d4-a716-446655440000",
    "schemaHash": "sha256:abc123...",
    "changeKind": "minor",
    "drrRefs": [
      {
        "drrId": "DRR-005",
        "uri": "https://github.com/acp/spec/drr/DRR-005.md",
        "summary": "Add optional assurance envelope to messages"
      }
    ],
    "compatibilityMatrix": {
      "backwardCompatibleWith": [
        "440e8400-e29b-41d4-a716-446655440000",
        "330e8400-e29b-41d4-a716-446655440000"
      ],
      "forwardCompatibleWith": [],
      "breakingFrom": []
    }
  }
}
```

### Example 2: Conformance Claim

```json
{
  "type": "initialize",
  "params": {
    "protocolVersion": 1,
    "editionClaim": {
      "editionId": "550e8400-e29b-41d4-a716-446655440000",
      "conformanceLevel": "tested",
      "testResultsRef": "https://ci.example.com/acp-conformance/run/12345"
    },
    "clientCapabilities": { ... }
  }
}
```

### Example 3: Conformance Deviation Report

```json
{
  "type": "conformance_report",
  "result": {
    "implementationId": "acp-sentinel-fsharp",
    "claimedEdition": "550e8400-e29b-41d4-a716-446655440000",
    "observedConformance": "declared",
    "deviations": [
      {
        "path": "$.agentCapabilities.mcpCapabilities",
        "expected": "object",
        "observed": "missing",
        "severity": "minor"
      }
    ],
    "reportedAt": "2026-01-06T01:50:00Z"
  }
}
```

## References

- FPF A.4: Temporal Duality & Open-Ended Evolution
- FPF B.4: Canonical Evolution Loop
- FPF E.9: Design-Rationale Record (DRR) Method
- FPF B.1.4: Contextual & Temporal Aggregation (Γ_time)
- FPF F.13: Lexical Continuity & Deprecation
- Semantic Versioning 2.0.0: https://semver.org/
- DRR-004: Protocol Evolution Stability
