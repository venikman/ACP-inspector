namespace Acp

open System

/// BC-002: Semantic Alignment Context
/// Cross-agent meaning coordination via bounded contexts, kind signatures, and bridges.
/// FPF Grounding: A.1.1, F.9, C.3.3
module Semantic =

    open Assurance

    // =====================
    // Context Identity
    // =====================

    /// Unique identifier for a bounded context (agent's semantic frame)
    [<Struct>]
    type ContextId = ContextId of string

    [<RequireQualifiedAccess>]
    module ContextId =
        let value (ContextId s) = s
        let create s = ContextId s

        /// Well-known context for ACP protocol itself
        let acpProtocol = ContextId "urn:acp:protocol:v1"

        /// Context for ACP Inspector assurance layer
        let acpInspectorAssurance = ContextId "urn:acp-inspector:context:assurance:v1"

    /// Unique identifier for a kind within a context
    [<Struct>]
    type KindId = KindId of string

    [<RequireQualifiedAccess>]
    module KindId =
        let value (KindId s) = s
        let create s = KindId s

    // =====================
    // Kind Signature (C.3.2)
    // =====================

    /// Intension specification - what a kind means
    type IntensionSpec =
        { definition: string // Human-readable definition
          formalDefinition: string option // Optional formal definition
          formality: Formality } // How rigorous is the definition

    [<RequireQualifiedAccess>]
    module IntensionSpec =
        let informal definition =
            { definition = definition
              formalDefinition = None
              formality = Formality.F0 }

        let semiStructured definition =
            { definition = definition
              formalDefinition = None
              formality = Formality.F3 }

        let formal definition formalDef =
            { definition = definition
              formalDefinition = Some formalDef
              formality = Formality.F6 }

    /// Extension specification - what instances exist
    type ExtensionSpec =
        | Enumerated of values: string list
        | Described of description: string
        | SchemaRef of schemaUri: string
        | Open // Extension is not bounded

    /// Kind signature - formal definition of a term's meaning within a context
    type KindSignature =
        { kindId: KindId
          kindName: string // Human-readable name
          contextId: ContextId // Owning context
          intension: IntensionSpec // What it means
          extension: ExtensionSpec // What instances exist
          constraints: string list // Additional constraints
          superKinds: KindId list } // Inheritance (subKindOf)

    [<RequireQualifiedAccess>]
    module KindSignature =
        let create kindId kindName contextId intension =
            { kindId = kindId
              kindName = kindName
              contextId = contextId
              intension = intension
              extension = ExtensionSpec.Open
              constraints = []
              superKinds = [] }

        let withExtension extension sig' = { sig' with extension = extension }

        let withConstraint constraint' sig' =
            { sig' with
                constraints = sig'.constraints @ [ constraint' ] }

        let withSuperKind superKind sig' =
            { sig' with
                superKinds = sig'.superKinds @ [ superKind ] }

        /// Check if signature has sufficient formality
        let hasMinFormality minLevel (sig': KindSignature) =
            Formality.toInt sig'.intension.formality >= Formality.toInt minLevel

    // =====================
    // Kind Bridge (C.3.3)
    // =====================

    /// Mapping type between kinds in different contexts
    [<RequireQualifiedAccess>]
    type MappingType =
        | Equivalent // Kinds are semantically identical
        | Subkind // Source is more specific than target
        | Superkind // Source is more general than target
        | Overlapping // Partial overlap in meaning
        | Disjoint // No overlap (CL0 implied)

    /// Kind bridge - mapping between kinds in different contexts
    type KindBridge =
        { sourceKind: ContextId * KindId
          targetKind: ContextId * KindId
          mappingType: MappingType
          congruenceLevel: CongruenceLevel
          lossNotes: string list // What's lost in translation
          bidirectional: bool }

    [<RequireQualifiedAccess>]
    module KindBridge =
        let create sourceCtx sourceKind targetCtx targetKind mappingType cl =
            { sourceKind = (sourceCtx, sourceKind)
              targetKind = (targetCtx, targetKind)
              mappingType = mappingType
              congruenceLevel = cl
              lossNotes = []
              bidirectional = mappingType = MappingType.Equivalent }

        let withLossNote note bridge =
            { bridge with
                lossNotes = bridge.lossNotes @ [ note ] }

        let makeBidirectional bridge = { bridge with bidirectional = true }

        /// INV-SEM: CL4 implies Equivalent
        let validate (bridge: KindBridge) : string list =
            [ if
                  bridge.congruenceLevel = CongruenceLevel.CL4
                  && bridge.mappingType <> MappingType.Equivalent
              then
                  "CL4 requires Equivalent mapping type"
              if
                  bridge.mappingType = MappingType.Disjoint
                  && bridge.congruenceLevel <> CongruenceLevel.CL0
              then
                  "Disjoint mapping requires CL0" ]

        /// Reverse bridge direction (for bidirectional bridges)
        let reverse (bridge: KindBridge) : KindBridge =
            let reversedType =
                match bridge.mappingType with
                | MappingType.Subkind -> MappingType.Superkind
                | MappingType.Superkind -> MappingType.Subkind
                | t -> t

            { sourceKind = bridge.targetKind
              targetKind = bridge.sourceKind
              mappingType = reversedType
              congruenceLevel = bridge.congruenceLevel
              lossNotes = bridge.lossNotes
              bidirectional = bridge.bidirectional }

        /// Apply reliability penalty when crossing this bridge (C.2.2, F.9)
        /// R_effective = R × CL_penalty
        let applyReliabilityPenalty (bridge: KindBridge) (r: Assurance.Reliability) : float =
            Assurance.Reliability.applyClPenalty bridge.congruenceLevel r

    // =====================
    // Context Boundary Enforcement (A.1.1, F.9)
    // =====================

    /// Registry of bridges between contexts
    type BridgeRegistry =
        { bridges: Map<ContextId * ContextId, KindBridge list> }

    [<RequireQualifiedAccess>]
    module BridgeRegistry =
        /// Create an empty bridge registry
        let empty = { bridges = Map.empty }

        /// Register a bridge between two contexts
        /// If the bridge is bidirectional, also registers the reversed bridge entry.
        let addBridge (bridge: KindBridge) (registry: BridgeRegistry) : BridgeRegistry =
            let addUnder (key: ContextId * ContextId) (b: KindBridge) (registry: BridgeRegistry) : BridgeRegistry =
                let existing = registry.bridges |> Map.tryFind key |> Option.defaultValue []

                { registry with
                    bridges = registry.bridges |> Map.add key (b :: existing) }

            let (sourceCtx, _) = bridge.sourceKind
            let (targetCtx, _) = bridge.targetKind

            let registry' = addUnder (sourceCtx, targetCtx) bridge registry

            if bridge.bidirectional && sourceCtx <> targetCtx then
                registry' |> addUnder (targetCtx, sourceCtx) (KindBridge.reverse bridge)
            else
                registry'

        /// Find bridges between two contexts
        let findBridges (sourceCtx: ContextId) (targetCtx: ContextId) (registry: BridgeRegistry) : KindBridge list =
            registry.bridges |> Map.tryFind (sourceCtx, targetCtx) |> Option.defaultValue []

        /// Check if a bridge exists for a specific kind pair
        let hasBridgeForKinds
            (sourceCtx: ContextId)
            (sourceKind: KindId)
            (targetCtx: ContextId)
            (targetKind: KindId)
            (registry: BridgeRegistry)
            : bool =
            let bridges = findBridges sourceCtx targetCtx registry

            bridges
            |> List.exists (fun b ->
                let (_, sKind) = b.sourceKind
                let (_, tKind) = b.targetKind
                sKind = sourceKind && tKind = targetKind)

    /// Context boundary violation
    [<RequireQualifiedAccess>]
    type BoundaryViolation =
        | UnbridgedReference of
            sourceContext: ContextId *
            sourceKind: KindId *
            targetContext: ContextId *
            targetKind: KindId
        | MissingBridge of sourceContext: ContextId * targetContext: ContextId

    [<RequireQualifiedAccess>]
    module BoundaryViolation =
        let describe =
            function
            | BoundaryViolation.UnbridgedReference(sourceCtx, sourceKind, targetCtx, targetKind) ->
                sprintf
                    "Unbridged reference from %s:%s to %s:%s (no bridge found)"
                    (ContextId.value sourceCtx)
                    (KindId.value sourceKind)
                    (ContextId.value targetCtx)
                    (KindId.value targetKind)
            | BoundaryViolation.MissingBridge(sourceCtx, targetCtx) ->
                sprintf "No bridge found from context %s to %s" (ContextId.value sourceCtx) (ContextId.value targetCtx)

    /// Validate a cross-context reference
    let validateContextBoundary
        (sourceCtx: ContextId)
        (sourceKind: KindId)
        (targetCtx: ContextId)
        (targetKind: KindId)
        (registry: BridgeRegistry)
        : Result<KindBridge, BoundaryViolation> =
        // Same context - no bridge needed
        if sourceCtx = targetCtx then
            // Create a trivial CL4 bridge for same-context references
            Ok(KindBridge.create sourceCtx sourceKind targetCtx targetKind MappingType.Equivalent CongruenceLevel.CL4)
        else
            // Cross-context - bridge required
            let bridges = BridgeRegistry.findBridges sourceCtx targetCtx registry

            if bridges.IsEmpty then
                Error(BoundaryViolation.MissingBridge(sourceCtx, targetCtx))
            else
                // Find a bridge for this specific kind pair
                let bridgeOpt =
                    bridges
                    |> List.tryFind (fun b ->
                        let (_, sKind) = b.sourceKind
                        let (_, tKind) = b.targetKind
                        sKind = sourceKind && tKind = targetKind)

                match bridgeOpt with
                | Some bridge -> Ok bridge
                | None -> Error(BoundaryViolation.UnbridgedReference(sourceCtx, sourceKind, targetCtx, targetKind))

    /// Check if a cross-context reference is valid
    let isValidCrossContextReference
        (sourceCtx: ContextId)
        (sourceKind: KindId)
        (targetCtx: ContextId)
        (targetKind: KindId)
        (registry: BridgeRegistry)
        : bool =
        match validateContextBoundary sourceCtx sourceKind targetCtx targetKind registry with
        | Ok _ -> true
        | Error _ -> false

    // =====================
    // Agent Context (Bounded Context)
    // =====================

    /// Invariant specification for a context
    type Invariant =
        { id: string
          description: string
          formal: string option }

    /// Agent context - semantic frame within which an agent's terms have meaning
    type AgentContext =
        { contextId: ContextId
          agentId: string option // Associated agent
          vocabularyRef: string option // URI to external vocabulary
          kindSignatures: Map<KindId, KindSignature>
          invariants: Invariant list
          parentContext: ContextId option } // Inheritance

    [<RequireQualifiedAccess>]
    module AgentContext =
        let create contextId =
            { contextId = contextId
              agentId = None
              vocabularyRef = None
              kindSignatures = Map.empty
              invariants = []
              parentContext = None }

        let withAgentId agentId ctx = { ctx with agentId = Some agentId }

        let withVocabulary vocabRef ctx =
            { ctx with
                vocabularyRef = Some vocabRef }

        let addKind (sig': KindSignature) ctx =
            { ctx with
                kindSignatures = ctx.kindSignatures |> Map.add sig'.kindId sig' }

        let addInvariant invariant ctx =
            { ctx with
                invariants = ctx.invariants @ [ invariant ] }

        let withParent parentId ctx =
            { ctx with
                parentContext = Some parentId }

        let tryGetKind kindId ctx =
            ctx.kindSignatures |> Map.tryFind kindId

        let hasKind kindId ctx =
            ctx.kindSignatures |> Map.containsKey kindId

        /// Compute semantic fingerprint (hash of signatures for comparison)
        let fingerprint (ctx: AgentContext) : string =
            let kinds =
                ctx.kindSignatures
                |> Map.toList
                |> List.map (fun (KindId k, s) -> sprintf "%s:%s" k s.kindName)
                |> List.sort // Ensure deterministic order
                |> String.concat ","

            let bytes = System.Text.Encoding.UTF8.GetBytes(kinds)
            let hashBytes = System.Security.Cryptography.SHA256.HashData(bytes)
            let hash = System.Convert.ToHexString(hashBytes).ToLowerInvariant().Substring(0, 16)
            sprintf "%s:%s" (ContextId.value ctx.contextId) hash

    // =====================
    // Alignment Bridge (F.9)
    // =====================

    /// Complete alignment between two agent contexts
    type AlignmentBridge =
        { bridgeId: string
          sourceContext: ContextId
          targetContext: ContextId
          kindBridges: KindBridge list
          aggregateCL: CongruenceLevel // Weakest of kind bridges
          validFrom: DateTimeOffset
          validUntil: DateTimeOffset option }

    [<RequireQualifiedAccess>]
    module AlignmentBridge =
        let create bridgeId sourceCtx targetCtx (kindBridges: KindBridge list) =
            let aggregateCL =
                if kindBridges.IsEmpty then
                    CongruenceLevel.CL0
                else
                    kindBridges
                    |> List.map (fun kb -> kb.congruenceLevel)
                    |> List.reduce CongruenceLevel.min

            { bridgeId = bridgeId
              sourceContext = sourceCtx
              targetContext = targetCtx
              kindBridges = kindBridges
              aggregateCL = aggregateCL
              validFrom = DateTimeOffset.UtcNow
              validUntil = None }

        let withValidity validUntil bridge =
            { bridge with
                validUntil = Some validUntil }

        let addKindBridge (kindBridge: KindBridge) (bridge: AlignmentBridge) =
            let newBridges = bridge.kindBridges @ [ kindBridge ]

            let newAggregateCL =
                newBridges
                |> List.map (fun kb -> kb.congruenceLevel)
                |> List.reduce CongruenceLevel.min

            { bridge with
                kindBridges = newBridges
                aggregateCL = newAggregateCL }

        /// Check if bridge is still valid
        let isValid (now: DateTimeOffset) (bridge: AlignmentBridge) =
            bridge.validFrom <= now
            && match bridge.validUntil with
               | None -> true
               | Some until -> now <= until

        /// Find kind bridge for a specific kind
        let tryFindKindBridge sourceKind bridge =
            bridge.kindBridges |> List.tryFind (fun kb -> snd kb.sourceKind = sourceKind)

        /// INV-SEM-04: Validate aggregate CL matches weakest link
        let validate (bridge: AlignmentBridge) : string list =
            let computedCL =
                if bridge.kindBridges.IsEmpty then
                    CongruenceLevel.CL0
                else
                    bridge.kindBridges
                    |> List.map (fun kb -> kb.congruenceLevel)
                    |> List.reduce CongruenceLevel.min

            [ if computedCL <> bridge.aggregateCL then
                  sprintf "Aggregate CL mismatch: declared %A, computed %A" bridge.aggregateCL computedCL
              yield! bridge.kindBridges |> List.collect KindBridge.validate ]

    // =====================
    // Semantic Registry
    // =====================

    /// Registry of contexts and bridges
    type SemanticRegistry =
        { contexts: Map<ContextId, AgentContext>
          bridges: Map<string, AlignmentBridge> }

    [<RequireQualifiedAccess>]
    module SemanticRegistry =
        let empty =
            { contexts = Map.empty
              bridges = Map.empty }

        let registerContext ctx registry =
            { registry with
                contexts = registry.contexts |> Map.add ctx.contextId ctx }

        let registerBridge (bridge: AlignmentBridge) (registry: SemanticRegistry) : SemanticRegistry =
            { registry with
                bridges = registry.bridges |> Map.add bridge.bridgeId bridge }

        let tryGetContext contextId registry =
            registry.contexts |> Map.tryFind contextId

        let tryGetBridge bridgeId registry =
            registry.bridges |> Map.tryFind bridgeId

        /// Find bridge between two contexts
        let tryFindBridge sourceCtx targetCtx registry =
            registry.bridges
            |> Map.toList
            |> List.map snd
            |> List.tryFind (fun b -> b.sourceContext = sourceCtx && b.targetContext = targetCtx)

        /// Get all bridges from a context
        let bridgesFrom contextId registry =
            registry.bridges
            |> Map.toList
            |> List.map snd
            |> List.filter (fun b -> b.sourceContext = contextId)

    // =====================
    // Cross-Context Operations
    // =====================

    /// Result of translating a term across contexts
    type TranslationResult =
        | Translated of targetKind: KindId * cl: CongruenceLevel * lossNotes: string list
        | NoBridge of sourceCtx: ContextId * targetCtx: ContextId
        | NoKindMapping of sourceKind: KindId
        | Incompatible of reason: string

    /// Translate a kind from one context to another
    let translateKind
        (registry: SemanticRegistry)
        (sourceCtx: ContextId)
        (targetCtx: ContextId)
        (sourceKind: KindId)
        : TranslationResult =

        match SemanticRegistry.tryFindBridge sourceCtx targetCtx registry with
        | None -> NoBridge(sourceCtx, targetCtx)
        | Some bridge ->
            match AlignmentBridge.tryFindKindBridge sourceKind bridge with
            | None -> NoKindMapping sourceKind
            | Some kindBridge ->
                if kindBridge.mappingType = MappingType.Disjoint then
                    Incompatible "Kinds are disjoint"
                else
                    Translated(snd kindBridge.targetKind, kindBridge.congruenceLevel, kindBridge.lossNotes)

    /// Apply CL penalty to assurance when crossing bridge
    let applyBridgePenalty (bridge: AlignmentBridge) (envelope: AssuranceEnvelope) : AssuranceEnvelope =
        crossBridge bridge.aggregateCL envelope

    // =====================
    // Semantic Findings
    // =====================

    /// Semantic-specific validation findings
    [<RequireQualifiedAccess>]
    type SemanticFinding =
        | UndeclaredContext of agentId: string
        | UnbridgedCrossReference of sourceCtx: ContextId * targetCtx: ContextId * term: string
        | CLInflation of bridgeId: string * declared: CongruenceLevel * computed: CongruenceLevel
        | SemanticDrift of contextId: ContextId * changeScore: float
        | TermCollision of term: string * ctx1: ContextId * ctx2: ContextId
        | InvalidKindBridge of bridgeId: string * reason: string

    [<RequireQualifiedAccess>]
    module SemanticFinding =
        let code =
            function
            | SemanticFinding.UndeclaredContext _ -> "ACP.SEMANTIC.UNDECLARED_CONTEXT"
            | SemanticFinding.UnbridgedCrossReference _ -> "ACP.SEMANTIC.UNBRIDGED_REFERENCE"
            | SemanticFinding.CLInflation _ -> "ACP.SEMANTIC.CL_INFLATION"
            | SemanticFinding.SemanticDrift _ -> "ACP.SEMANTIC.DRIFT"
            | SemanticFinding.TermCollision _ -> "ACP.SEMANTIC.TERM_COLLISION"
            | SemanticFinding.InvalidKindBridge _ -> "ACP.SEMANTIC.INVALID_BRIDGE"

        let describe =
            function
            | SemanticFinding.UndeclaredContext agentId ->
                sprintf "Agent %s has no declared semantic context (INV-SEM-01)" agentId
            | SemanticFinding.UnbridgedCrossReference(src, tgt, term) ->
                sprintf
                    "Term '%s' crosses from %s to %s without bridge (INV-SEM-02)"
                    term
                    (ContextId.value src)
                    (ContextId.value tgt)
            | SemanticFinding.CLInflation(bid, declared, computed) ->
                sprintf
                    "Bridge %s declares CL%d but computes to CL%d (INV-SEM-04)"
                    bid
                    (CongruenceLevel.toInt declared)
                    (CongruenceLevel.toInt computed)
            | SemanticFinding.SemanticDrift(ctx, score) ->
                sprintf "Context %s has semantic drift (score: %.2f) (INV-SEM-05)" (ContextId.value ctx) score
            | SemanticFinding.TermCollision(term, ctx1, ctx2) ->
                sprintf
                    "Term '%s' has different meanings in %s and %s"
                    term
                    (ContextId.value ctx1)
                    (ContextId.value ctx2)
            | SemanticFinding.InvalidKindBridge(bid, reason) -> sprintf "Kind bridge %s is invalid: %s" bid reason

    // =====================
    // Semantic Drift Detection
    // =====================

    /// Compute difference between two context versions
    let computeDrift (old: AgentContext) (new': AgentContext) : float =
        let oldKinds = old.kindSignatures |> Map.toList |> List.map fst |> Set.ofList
        let newKinds = new'.kindSignatures |> Map.toList |> List.map fst |> Set.ofList

        let added = Set.difference newKinds oldKinds |> Set.count
        let removed = Set.difference oldKinds newKinds |> Set.count
        let common = Set.intersect oldKinds newKinds

        // Count definition changes in common kinds
        let changed =
            common
            |> Set.toList
            |> List.filter (fun kindId ->
                let oldSig = old.kindSignatures |> Map.find kindId
                let newSig = new'.kindSignatures |> Map.find kindId
                oldSig.intension.definition <> newSig.intension.definition)
            |> List.length

        let totalOld = max 1 (Set.count oldKinds)
        float (added + removed + changed) / float totalOld

    /// Check if drift exceeds threshold
    let detectDrift (threshold: float) (old: AgentContext) (new': AgentContext) : SemanticFinding option =
        let drift = computeDrift old new'

        if drift > threshold then
            Some(SemanticFinding.SemanticDrift(new'.contextId, drift))
        else
            None
