# BC-002: Semantic Alignment Context

**FPF Pattern**: A.1.1 U.BoundedContext  
**Related DRR**: [DRR-002](../drr/DRR-002-cross-agent-semantic-alignment.md)  
**Status**: Draft  
**Version**: 0.1.0

## Context Identity

```yaml
contextId: "urn:acp-sentinel:context:semantic:v1"
contextName: "ACP Semantic Alignment Context"
scope: "Cross-agent meaning coordination and translation"
owner: "ACP-Sentinel Project"
```

## Purpose

This bounded context defines the semantic frame for reasoning about **meaning alignment** between agents. It provides vocabulary and rules for:
- Declaring an agent's semantic commitments
- Building bridges between agent contexts
- Computing translation loss (Congruence Level)
- Detecting and flagging semantic drift

## Vocabulary (Local Glossary)

### Core Terms

| Term | Local Definition | FPF Mapping | Notes |
|------|------------------|-------------|-------|
| **AgentContext** | The implicit semantic frame created by an agent's training, prompts, and tools | U.BoundedContext instance | Every agent has one, even if undeclared |
| **SemanticDeclaration** | Explicit statement of an agent's vocabulary and meaning commitments | A.1.1 glossary | Protocol artifact |
| **KindSignature** | Formal definition of a type's intension (meaning) and extension (members) | C.3.2 | Core of semantic alignment |
| **KindBridge** | Mapping between kinds across different contexts | C.3.3 | Carries CL penalty |
| **CongruenceLevel** | Ordinal measure of translation fidelity: CL0..CL4 | F.9 CL | CL4=perfect, CL0=incompatible |
| **TranslationLoss** | Information or meaning lost when crossing context boundary | F.9 lossNotes | Always > 0 for CL < CL4 |
| **SemanticDrift** | Unintended divergence of meaning over time or interactions | (local term) | Error to detect |
| **AlignmentBridge** | Declared mapping between two AgentContexts | F.9 Bridge | Protocol artifact |
| **VocabularyRef** | URI to external vocabulary/ontology | (local term) | e.g., schema.org, custom |

### Derived Terms

| Term | Definition | Derivation |
|------|------------|------------|
| **EffectiveReliability** | R × CL_penalty for cross-context claims | Assurance R degraded by bridge |
| **SemanticFingerprint** | Hash of vocabulary + invariants for quick comparison | Composite of KindSignatures |
| **AlignmentMatrix** | N×N matrix of CL values for N agents | Pairwise bridge CLs |
| **TermCollision** | Same surface term, different meanings across contexts | KindBridge with CL < CL3 |

## Kind Signatures

### AgentContext

```
Kind: AgentContext
Intension: Semantic frame within which an agent's terms have meaning
Slots:
  - contextId: URI (unique identifier)
  - agentId: AgentIdentifier
  - vocabularyRef: URI? (external vocab link)
  - kindSignatures: List<KindSignature>
  - invariants: List<Invariant>
  - parentContext: AgentContext? (inheritance)
  
Invariant: contextId is globally unique
Invariant: kindSignatures covers all terms used in agent outputs
```

### KindSignature

```
Kind: KindSignature
Intension: Formal definition of a term's meaning within a context
Slots:
  - kindId: URI (context-local identifier)
  - kindName: String (human-readable)
  - intension: IntensionSpec (what it means)
  - extension: ExtensionSpec (what instances exist)
  - formality: Formality (F0..F9)
  - constraints: List<Constraint>
  
Invariant: intension ≠ ∅ (every kind has meaning)
```

### KindBridge

```
Kind: KindBridge
Intension: Mapping between kinds in different contexts
Slots:
  - sourceKind: (ContextId, KindId)
  - targetKind: (ContextId, KindId)
  - mappingType: Equivalent | Subkind | Superkind | Overlapping | Disjoint
  - congruenceLevel: CL0..CL4
  - lossNotes: List<String>
  - bidirectional: Boolean
  
Invariant: CL4 ⟹ mappingType = Equivalent
Invariant: Disjoint ⟹ CL0
```

### CongruenceLevel

```
Kind: CongruenceLevel
Intension: Ordinal measure of semantic translation fidelity
Extension: { CL0, CL1, CL2, CL3, CL4 }
SubkindOf: OrdinalCharacteristic

CL0 := "Incompatible" — no meaningful translation possible
CL1 := "Lossy" — significant meaning loss
CL2 := "Approximate" — moderate meaning loss
CL3 := "High-fidelity" — minimal meaning loss
CL4 := "Equivalent" — no meaning loss (rare)
```

### AlignmentBridge

```
Kind: AlignmentBridge
Intension: Complete mapping between two agent contexts
Slots:
  - bridgeId: URI
  - sourceContext: AgentContext
  - targetContext: AgentContext
  - kindBridges: List<KindBridge>
  - aggregateCL: CongruenceLevel (weakest of kindBridges)
  - validFrom: Timestamp
  - validUntil: Timestamp?
  
Invariant: aggregateCL = min(kb.congruenceLevel for kb in kindBridges)
```

## Invariants

### INV-SEM-01: Context Declaration Required
```
∀ agent ∈ Session:
  agent.semanticDeclaration ≠ ∅
  OR agent.assumedContext = DefaultAgentContext
  
Rationale: Every agent operates within a context (FPF A.1.1)
Violation: ValidationFinding.UndeclaredContext (warning, not error)
```

### INV-SEM-02: Bridge Required for Cross-Context Claims
```
∀ message from agent_A referencing term_T:
  term_T ∈ agent_B.context ∧ agent_A ≠ agent_B
  ⟹ ∃ bridge: agent_A.context ↔ agent_B.context
  
Rationale: Cross-context meaning requires explicit mapping (FPF F.9)
Violation: ValidationFinding.UnbridgedCrossReference
```

### INV-SEM-03: CL Penalty Applied
```
∀ claim crossing bridge B:
  claim.effectiveReliability = claim.reliability × CLPenalty(B.aggregateCL)
  
where CLPenalty(CL4) = 1.0
      CLPenalty(CL3) = 0.9
      CLPenalty(CL2) = 0.7
      CLPenalty(CL1) = 0.4
      CLPenalty(CL0) = 0.0
      
Rationale: Translation degrades trust (FPF C.2.2)
```

### INV-SEM-04: No Transitive CL Inflation
```
∀ path A → B → C:
  CL(A,C) ≤ min(CL(A,B), CL(B,C))
  
Rationale: Chained translations cannot improve fidelity
Violation: ValidationFinding.CLInflation
```

### INV-SEM-05: Drift Detection
```
∀ agent_A at time t1, t2 where t2 > t1:
  diff(agent_A.kindSignatures[t1], agent_A.kindSignatures[t2]) > threshold
  ⟹ SemanticDriftAlert
  
Rationale: Undeclared meaning changes are errors
```

## Bridges to Other Contexts

### Bridge: Semantic ↔ Assurance (BC-001)

```yaml
bridgeId: "bridge:semantic-assurance"
sourceContext: "urn:acp-sentinel:context:semantic:v1"
targetContext: "urn:acp-sentinel:context:assurance:v1"
congruenceLevel: CL4

mappings:
  - source: AgentContext
    target: ClaimScope (as context slice)
    lossNotes: "Direct correspondence"
    
  - source: CongruenceLevel
    target: ReliabilityPenalty
    lossNotes: "CL maps to R degradation factor"
```

### Bridge: Semantic ↔ Protocol (ACP Core)

```yaml
bridgeId: "bridge:semantic-protocol"
sourceContext: "urn:acp-sentinel:context:semantic:v1"
targetContext: "urn:acp:protocol:v1"
congruenceLevel: CL2  # Significant gap

mappings:
  - source: AgentContext
    target: agentInfo (partial)
    lossNotes: "Protocol has minimal semantic metadata"
    
  - source: KindSignature
    target: (no mapping)
    lossNotes: "Protocol has no type semantics beyond JSON schema"
    
  - source: AlignmentBridge
    target: (extension required)
    lossNotes: "Must extend protocol to carry bridges"
```

### Bridge: Semantic ↔ Capability (BC-003)

```yaml
bridgeId: "bridge:semantic-capability"
sourceContext: "urn:acp-sentinel:context:semantic:v1"
targetContext: "urn:acp-sentinel:context:capability:v1"
congruenceLevel: CL3

mappings:
  - source: KindSignature (for capability terms)
    target: CapabilityKind
    lossNotes: "Capability semantics are a subset of general semantics"
    
  - source: AgentContext
    target: CapabilityContext
    lossNotes: "Capabilities interpreted within agent's semantic frame"
```

## Protocol Extensions

### SemanticDeclaration Message

```typescript
interface SemanticDeclaration {
  contextId: string;              // URI
  vocabularyRef?: string;         // URI to external vocabulary
  kindSignatures?: KindSignature[];
  invariants?: string[];          // Human-readable rules
  parentContext?: string;         // URI of inherited context
}

// Sent during Initialize or as separate declaration
interface InitializeParams {
  // ... existing fields ...
  semanticContext?: SemanticDeclaration;
}
```

### AlignmentBridge Declaration

```typescript
interface AlignmentBridgeDeclaration {
  bridgeId: string;
  sourceContextId: string;
  targetContextId: string;
  mappings: KindMapping[];
  aggregateCL: "CL0" | "CL1" | "CL2" | "CL3" | "CL4";
  lossNotes?: string[];
}

interface KindMapping {
  sourceKindId: string;
  targetKindId: string;
  mappingType: "equivalent" | "subkind" | "superkind" | "overlapping" | "disjoint";
  cl: "CL0" | "CL1" | "CL2" | "CL3" | "CL4";
}
```

## Sentinel Validation Rules

```fsharp
module SemanticValidation =
    
    /// Check for undeclared cross-context references
    let validateCrossReference 
        (msg: Message) 
        (sourceCtx: AgentContext) 
        (bridges: AlignmentBridge list) 
        : ValidationFinding list =
        
        let referencedContexts = extractContextRefs msg
        let unbridged = 
            referencedContexts 
            |> List.filter (fun ctx -> 
                not (bridges |> List.exists (fun b -> 
                    b.sourceContext.contextId = sourceCtx.contextId &&
                    b.targetContext.contextId = ctx)))
        
        unbridged |> List.map (fun ctx -> UnbridgedCrossReference (msg.id, ctx))
    
    /// Compute effective reliability with CL penalty
    let applyClPenalty (reliability: float) (cl: CongruenceLevel) : float =
        let penalty = 
            match cl with
            | CL4 -> 1.0
            | CL3 -> 0.9
            | CL2 -> 0.7
            | CL1 -> 0.4
            | CL0 -> 0.0
        reliability * penalty
    
    /// Detect semantic drift between sessions
    let detectDrift 
        (oldSigs: KindSignature list) 
        (newSigs: KindSignature list) 
        (threshold: float)
        : SemanticDriftAlert option =
        
        let diff = computeSignatureDiff oldSigs newSigs
        if diff > threshold then Some (SemanticDriftAlert diff) else None
```

## Usage Examples

### Example 1: Agent with Semantic Declaration

```json
{
  "type": "initialize",
  "params": {
    "protocolVersion": 1,
    "clientCapabilities": { ... },
    "semanticContext": {
      "contextId": "urn:agent:code-assistant:v2",
      "vocabularyRef": "https://example.com/vocab/code-assistant.json",
      "kindSignatures": [
        {
          "kindId": "file",
          "kindName": "File",
          "intension": "A named byte sequence in a filesystem",
          "formality": "F3"
        },
        {
          "kindId": "error",
          "kindName": "Error", 
          "intension": "A deviation from expected behavior requiring attention",
          "formality": "F3"
        }
      ],
      "invariants": [
        "file.path is always absolute",
        "error.severity ∈ {info, warning, error, critical}"
      ]
    }
  }
}
```

### Example 2: Cross-Agent Bridge Declaration

```json
{
  "type": "declare_bridge",
  "params": {
    "bridgeId": "bridge:code-assistant-to-security-scanner",
    "sourceContextId": "urn:agent:code-assistant:v2",
    "targetContextId": "urn:agent:security-scanner:v1",
    "mappings": [
      {
        "sourceKindId": "error",
        "targetKindId": "vulnerability",
        "mappingType": "overlapping",
        "cl": "CL2"
      },
      {
        "sourceKindId": "file",
        "targetKindId": "file",
        "mappingType": "equivalent",
        "cl": "CL4"
      }
    ],
    "aggregateCL": "CL2",
    "lossNotes": [
      "code-assistant 'error' includes non-security issues",
      "security-scanner 'vulnerability' has severity levels not in code-assistant"
    ]
  }
}
```

## References

- FPF A.1.1: U.BoundedContext — The Semantic Frame
- FPF F.9: Alignment & Bridge across Contexts
- FPF C.3.3: KindBridge & CL^k
- FPF F.0.1: Contextual Lexicon Principles
- Evans, E. (2003). Domain-Driven Design, Ch. 14
- DRR-002: Cross-Agent Semantic Alignment
