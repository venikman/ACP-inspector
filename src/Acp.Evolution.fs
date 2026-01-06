namespace Acp

open System

/// BC-004: Protocol Evolution Context
/// Structured evolution of protocol specifications and implementations.
/// FPF Grounding: A.4, B.4, E.9, F.13
module Evolution =

    open Assurance

    // =====================
    // Identity Types
    // =====================

    /// Unique identifier for a protocol edition (content-addressable)
    [<Struct>]
    type EditionId = EditionId of Guid

    [<RequireQualifiedAccess>]
    module EditionId =
        let value (EditionId g) = g
        let create () = EditionId(Guid.NewGuid())
        let fromGuid g = EditionId g

        let tryParse (s: string) =
            match Guid.TryParse(s) with
            | true, g -> Some(EditionId g)
            | false, _ -> None

        let toString (EditionId g) = g.ToString()

    /// DRR (Design-Rationale Record) identifier
    [<Struct>]
    type DrrId = DrrId of string

    [<RequireQualifiedAccess>]
    module DrrId =
        let value (DrrId s) = s
        let create s = DrrId s

    /// Schema content hash (SHA256)
    [<Struct>]
    type SchemaHash = SchemaHash of string

    [<RequireQualifiedAccess>]
    module SchemaHash =
        let value (SchemaHash s) = s
        let create s = SchemaHash s

    // =====================
    // Change Classification
    // =====================

    /// Change impact classification (SemVer-aligned)
    [<RequireQualifiedAccess>]
    type ChangeKind =
        | Patch // Bug fix, no API change, backward compatible
        | Minor // Additive change, backward compatible
        | Major // Breaking change, not backward compatible

    [<RequireQualifiedAccess>]
    module ChangeKind =
        let isBreaking =
            function
            | ChangeKind.Major -> true
            | _ -> false

        let tryParse (s: string) =
            match s.ToLowerInvariant() with
            | "patch" -> Some ChangeKind.Patch
            | "minor" -> Some ChangeKind.Minor
            | "major" -> Some ChangeKind.Major
            | _ -> None

        let toString =
            function
            | ChangeKind.Patch -> "patch"
            | ChangeKind.Minor -> "minor"
            | ChangeKind.Major -> "major"

    /// Enumeration of breaking changes
    [<RequireQualifiedAccess>]
    type BreakingChange =
        | RequiredFieldAdded of path: string * description: string
        | FieldRemoved of path: string * description: string
        | TypeNarrowed of path: string * oldType: string * newType: string
        | EnumValueRemoved of path: string * removedValue: string
        | SemanticChange of path: string * description: string
        | BehaviorChange of description: string

    [<RequireQualifiedAccess>]
    module BreakingChange =
        let path =
            function
            | BreakingChange.RequiredFieldAdded(p, _) -> Some p
            | BreakingChange.FieldRemoved(p, _) -> Some p
            | BreakingChange.TypeNarrowed(p, _, _) -> Some p
            | BreakingChange.EnumValueRemoved(p, _) -> Some p
            | BreakingChange.SemanticChange(p, _) -> Some p
            | BreakingChange.BehaviorChange _ -> None

        let describe =
            function
            | BreakingChange.RequiredFieldAdded(p, d) -> sprintf "Required field added at %s: %s" p d
            | BreakingChange.FieldRemoved(p, d) -> sprintf "Field removed at %s: %s" p d
            | BreakingChange.TypeNarrowed(p, o, n) -> sprintf "Type narrowed at %s: %s → %s" p o n
            | BreakingChange.EnumValueRemoved(p, v) -> sprintf "Enum value '%s' removed at %s" v p
            | BreakingChange.SemanticChange(p, d) -> sprintf "Semantic change at %s: %s" p d
            | BreakingChange.BehaviorChange d -> sprintf "Behavior change: %s" d

    /// Enumeration of non-breaking changes
    [<RequireQualifiedAccess>]
    type NonBreakingChange =
        | OptionalFieldAdded of path: string * description: string
        | TypeWidened of path: string * oldType: string * newType: string
        | EnumValueAdded of path: string * addedValue: string
        | DeprecationMarked of path: string * reason: string
        | DocumentationUpdated of path: string

    [<RequireQualifiedAccess>]
    module NonBreakingChange =
        let path =
            function
            | NonBreakingChange.OptionalFieldAdded(p, _) -> p
            | NonBreakingChange.TypeWidened(p, _, _) -> p
            | NonBreakingChange.EnumValueAdded(p, _) -> p
            | NonBreakingChange.DeprecationMarked(p, _) -> p
            | NonBreakingChange.DocumentationUpdated p -> p

    /// Union of all change types
    type SchemaChange =
        | Breaking of BreakingChange
        | NonBreaking of NonBreakingChange

    // =====================
    // DRR Reference
    // =====================

    /// Reference to a Design-Rationale Record
    type DrrReference =
        { drrId: DrrId
          uri: string option
          summary: string }

    [<RequireQualifiedAccess>]
    module DrrReference =
        let create drrId summary =
            { drrId = drrId
              uri = None
              summary = summary }

        let withUri uri ref = { ref with uri = Some uri }

    // =====================
    // Compatibility Matrix
    // =====================

    /// Declared compatibility relationships between editions
    type CompatibilityMatrix =
        { editionId: EditionId
          backwardCompatibleWith: Set<EditionId>
          forwardCompatibleWith: Set<EditionId>
          breakingFrom: Set<EditionId> }

    [<RequireQualifiedAccess>]
    module CompatibilityMatrix =
        let empty editionId =
            { editionId = editionId
              backwardCompatibleWith = Set.empty
              forwardCompatibleWith = Set.empty
              breakingFrom = Set.empty }

        let addBackwardCompatible eid matrix =
            { matrix with
                backwardCompatibleWith = matrix.backwardCompatibleWith |> Set.add eid }

        let addForwardCompatible eid matrix =
            { matrix with
                forwardCompatibleWith = matrix.forwardCompatibleWith |> Set.add eid }

        let addBreakingFrom eid matrix =
            { matrix with
                breakingFrom = matrix.breakingFrom |> Set.add eid }

        /// INV-EVO-03: Check if compatibility is consistent (no overlaps)
        let validate (matrix: CompatibilityMatrix) : string list =
            let selfRef =
                if
                    matrix.backwardCompatibleWith |> Set.contains matrix.editionId
                    || matrix.forwardCompatibleWith |> Set.contains matrix.editionId
                    || matrix.breakingFrom |> Set.contains matrix.editionId
                then
                    [ "Edition cannot reference itself in compatibility matrix" ]
                else
                    []

            let overlap =
                let intersection = Set.intersect matrix.breakingFrom matrix.backwardCompatibleWith

                if not (Set.isEmpty intersection) then
                    [ sprintf
                          "Edition cannot be both breaking and backward compatible: %A"
                          (intersection |> Set.map EditionId.toString) ]
                else
                    []

            selfRef @ overlap

    // =====================
    // Semantic Version
    // =====================

    /// Semantic version (major.minor.patch)
    type SemVer =
        { major: int
          minor: int
          patch: int
          prerelease: string option }

    [<RequireQualifiedAccess>]
    module SemVer =
        let create major minor patch =
            { major = major
              minor = minor
              patch = patch
              prerelease = None }

        let withPrerelease pre ver = { ver with prerelease = Some pre }

        let toString ver =
            match ver.prerelease with
            | None -> sprintf "%d.%d.%d" ver.major ver.minor ver.patch
            | Some pre -> sprintf "%d.%d.%d-%s" ver.major ver.minor ver.patch pre

        let tryParse (s: string) =
            let parts = s.Split([| '-' |], 2)
            let versionPart = parts.[0]
            let prerelease = if parts.Length > 1 then Some parts.[1] else None

            match versionPart.Split('.') |> Array.map Int32.TryParse with
            | [| (true, maj); (true, min); (true, pat) |] ->
                Some
                    { major = maj
                      minor = min
                      patch = pat
                      prerelease = prerelease }
            | _ -> None

    // =====================
    // Edition
    // =====================

    /// Immutable snapshot of protocol specification
    type Edition =
        { editionId: EditionId
          version: SemVer
          parentEdition: EditionId option
          schemaHash: SchemaHash
          changeKind: ChangeKind
          drrRefs: DrrReference list
          compatibilityMatrix: CompatibilityMatrix
          publishedAt: DateTimeOffset
          deprecatedAt: DateTimeOffset option
          sunsetAt: DateTimeOffset option }

    [<RequireQualifiedAccess>]
    module Edition =
        let create editionId version schemaHash =
            { editionId = editionId
              version = version
              parentEdition = None
              schemaHash = schemaHash
              changeKind = ChangeKind.Major // Genesis is always Major
              drrRefs = []
              compatibilityMatrix = CompatibilityMatrix.empty editionId
              publishedAt = DateTimeOffset.UtcNow
              deprecatedAt = None
              sunsetAt = None }

        let withParent parentId changeKind edition =
            { edition with
                parentEdition = Some parentId
                changeKind = changeKind }

        let addDrr drr edition =
            { edition with
                drrRefs = edition.drrRefs @ [ drr ] }

        let withCompatibility matrix edition =
            { edition with
                compatibilityMatrix = matrix }

        let deprecate (at: DateTimeOffset) edition = { edition with deprecatedAt = Some at }

        let sunset (at: DateTimeOffset) edition = { edition with sunsetAt = Some at }

        let isDeprecated edition = edition.deprecatedAt.IsSome

        let isSunset edition = edition.sunsetAt.IsSome

        /// INV-EVO-01: Validate edition has rationale (unless genesis)
        /// INV-EVO-05: Deprecation must precede sunset
        let validate (edition: Edition) : string list =
            let rationaleCheck =
                match edition.parentEdition, edition.drrRefs with
                | Some _, [] -> [ "INV-EVO-01: Edition has parent but no DRR references (missing rationale)" ]
                | _ -> []

            let sunsetCheck =
                match edition.deprecatedAt, edition.sunsetAt with
                | None, Some _ -> [ "INV-EVO-05: Edition has sunset without deprecation" ]
                | Some dep, Some sun when sun <= dep -> [ "INV-EVO-05: Sunset must be after deprecation" ]
                | _ -> []

            let genesisCheck =
                match edition.parentEdition, edition.changeKind with
                | None, ChangeKind.Major -> []
                | None, _ -> [ "Genesis edition must have Major change kind" ]
                | _ -> []

            rationaleCheck
            @ sunsetCheck
            @ genesisCheck
            @ CompatibilityMatrix.validate edition.compatibilityMatrix

    // =====================
    // Conformance
    // =====================

    /// Conformance level - degree of implementation fidelity
    [<RequireQualifiedAccess>]
    type ConformanceLevel =
        | Declared // L0 - Just claims conformance
        | Tested // L1 - Has test evidence
        | Certified // L2 - Third-party verified

    [<RequireQualifiedAccess>]
    module ConformanceLevel =
        let toAssuranceLevel =
            function
            | ConformanceLevel.Declared -> AssuranceLevel.L0
            | ConformanceLevel.Tested -> AssuranceLevel.L1
            | ConformanceLevel.Certified -> AssuranceLevel.L2

        let fromAssuranceLevel =
            function
            | AssuranceLevel.L0 -> ConformanceLevel.Declared
            | AssuranceLevel.L1 -> ConformanceLevel.Tested
            | AssuranceLevel.L2 -> ConformanceLevel.Certified

        let toInt =
            function
            | ConformanceLevel.Declared -> 0
            | ConformanceLevel.Tested -> 1
            | ConformanceLevel.Certified -> 2

        let tryParse (s: string) =
            match s.ToLowerInvariant() with
            | "declared" -> Some ConformanceLevel.Declared
            | "tested" -> Some ConformanceLevel.Tested
            | "certified" -> Some ConformanceLevel.Certified
            | _ -> None

    /// Schema deviation found during conformance check
    type Deviation =
        { path: string
          expected: string
          observed: string
          severity: string }

    [<RequireQualifiedAccess>]
    module Deviation =
        let create path expected observed severity =
            { path = path
              expected = expected
              observed = observed
              severity = severity }

        let minor path expected observed = create path expected observed "minor"

        let major path expected observed = create path expected observed "major"

    /// Edition claim made by implementation
    type EditionClaim =
        { editionId: EditionId
          conformanceLevel: ConformanceLevel
          testResultsRef: string option }

    [<RequireQualifiedAccess>]
    module EditionClaim =
        let declared editionId =
            { editionId = editionId
              conformanceLevel = ConformanceLevel.Declared
              testResultsRef = None }

        let tested editionId testResultsRef =
            { editionId = editionId
              conformanceLevel = ConformanceLevel.Tested
              testResultsRef = Some testResultsRef }

        let certified editionId testResultsRef =
            { editionId = editionId
              conformanceLevel = ConformanceLevel.Certified
              testResultsRef = Some testResultsRef }

    /// Conformance report - result of validating implementation against edition
    type ConformanceReport =
        { implementationId: string
          claimedEdition: EditionId
          observedConformance: ConformanceLevel
          deviations: Deviation list
          testResults: string option
          reportedAt: DateTimeOffset }

    [<RequireQualifiedAccess>]
    module ConformanceReport =
        let create implId claimedEdition observedConformance =
            { implementationId = implId
              claimedEdition = claimedEdition
              observedConformance = observedConformance
              deviations = []
              testResults = None
              reportedAt = DateTimeOffset.UtcNow }

        let withDeviations deviations report = { report with deviations = deviations }

        let withTestResults uri report = { report with testResults = Some uri }

        /// INV-EVO-06: Tested+ conformance requires test evidence
        let validate (report: ConformanceReport) : string list =
            match report.observedConformance, report.testResults with
            | ConformanceLevel.Tested, None
            | ConformanceLevel.Certified, None -> [ "INV-EVO-06: Tested/Certified conformance requires test evidence" ]
            | _ -> []

    // =====================
    // Edition Registry
    // =====================

    /// Registry of protocol editions
    type EditionRegistry =
        { editions: Map<EditionId, Edition>
          current: EditionId option }

    [<RequireQualifiedAccess>]
    module EditionRegistry =
        let empty = { editions = Map.empty; current = None }

        let register (edition: Edition) registry =
            { registry with
                editions = registry.editions |> Map.add edition.editionId edition }

        let setCurrent editionId registry =
            { registry with
                current = Some editionId }

        let tryFind editionId registry =
            registry.editions |> Map.tryFind editionId

        let tryFindCurrent registry =
            registry.current |> Option.bind (fun id -> tryFind id registry)

        /// Compute edition lineage (ancestry chain)
        let lineage (editionId: EditionId) (registry: EditionRegistry) : Edition list =
            let rec walk eid acc =
                match tryFind eid registry with
                | None -> acc
                | Some edition ->
                    match edition.parentEdition with
                    | None -> edition :: acc
                    | Some parentId -> walk parentId (edition :: acc)

            walk editionId []

        /// Find all editions compatible with given edition
        let compatibleWith editionId registry =
            registry.editions
            |> Map.toList
            |> List.map snd
            |> List.filter (fun e -> e.compatibilityMatrix.backwardCompatibleWith |> Set.contains editionId)

        /// Check if path between two editions contains breaking changes
        let hasBreakingPath (fromId: EditionId) (toId: EditionId) (registry: EditionRegistry) : bool =
            let path = lineage toId registry

            path
            |> List.pairwise
            |> List.exists (fun (older, newer) -> older.editionId = fromId || newer.changeKind = ChangeKind.Major)

    // =====================
    // Evolution Findings
    // =====================

    /// Evolution-specific validation findings
    [<RequireQualifiedAccess>]
    type EvolutionFinding =
        | MissingRationale of editionId: EditionId
        | ChangeKindMismatch of editionId: EditionId * declared: ChangeKind * computed: ChangeKind
        | IntransitiveCompatibility of a: EditionId * b: EditionId * c: EditionId
        | SchemaHashMismatch of editionId: EditionId * declared: SchemaHash * computed: SchemaHash
        | SunsetWithoutDeprecation of editionId: EditionId
        | ConformanceWithoutEvidence of implId: string * level: ConformanceLevel
        | OrphanedEdition of editionId: EditionId * missingParent: EditionId

    [<RequireQualifiedAccess>]
    module EvolutionFinding =
        let code =
            function
            | EvolutionFinding.MissingRationale _ -> "ACP.EVOLUTION.MISSING_RATIONALE"
            | EvolutionFinding.ChangeKindMismatch _ -> "ACP.EVOLUTION.CHANGE_KIND_MISMATCH"
            | EvolutionFinding.IntransitiveCompatibility _ -> "ACP.EVOLUTION.INTRANSITIVE_COMPAT"
            | EvolutionFinding.SchemaHashMismatch _ -> "ACP.EVOLUTION.SCHEMA_HASH_MISMATCH"
            | EvolutionFinding.SunsetWithoutDeprecation _ -> "ACP.EVOLUTION.SUNSET_NO_DEPRECATION"
            | EvolutionFinding.ConformanceWithoutEvidence _ -> "ACP.EVOLUTION.CONFORMANCE_NO_EVIDENCE"
            | EvolutionFinding.OrphanedEdition _ -> "ACP.EVOLUTION.ORPHANED_EDITION"

        let describe =
            function
            | EvolutionFinding.MissingRationale eid ->
                sprintf "Edition %s has no DRR references (INV-EVO-01)" (EditionId.toString eid)
            | EvolutionFinding.ChangeKindMismatch(eid, declared, computed) ->
                sprintf
                    "Edition %s declares %s but computed %s (INV-EVO-02)"
                    (EditionId.toString eid)
                    (ChangeKind.toString declared)
                    (ChangeKind.toString computed)
            | EvolutionFinding.IntransitiveCompatibility(a, b, c) ->
                sprintf
                    "Intransitive compatibility: %s → %s → %s (INV-EVO-03)"
                    (EditionId.toString a)
                    (EditionId.toString b)
                    (EditionId.toString c)
            | EvolutionFinding.SchemaHashMismatch(eid, declared, computed) ->
                sprintf
                    "Edition %s schema hash mismatch: declared %s, computed %s (INV-EVO-04)"
                    (EditionId.toString eid)
                    (SchemaHash.value declared)
                    (SchemaHash.value computed)
            | EvolutionFinding.SunsetWithoutDeprecation eid ->
                sprintf "Edition %s has sunset without deprecation (INV-EVO-05)" (EditionId.toString eid)
            | EvolutionFinding.ConformanceWithoutEvidence(implId, level) ->
                sprintf "Implementation %s claims %A without test evidence (INV-EVO-06)" implId level
            | EvolutionFinding.OrphanedEdition(eid, parentId) ->
                sprintf "Edition %s references missing parent %s" (EditionId.toString eid) (EditionId.toString parentId)

    // =====================
    // Change Classification
    // =====================

    /// Result of computing schema diff
    type SchemaDiff =
        { changes: SchemaChange list
          computedKind: ChangeKind }

    /// Classify changes between two editions
    let classifyChanges (changes: SchemaChange list) : ChangeKind =
        let hasBreaking =
            changes
            |> List.exists (function
                | Breaking _ -> true
                | _ -> false)

        let hasAdditive =
            changes
            |> List.exists (function
                | NonBreaking(NonBreakingChange.OptionalFieldAdded _)
                | NonBreaking(NonBreakingChange.TypeWidened _)
                | NonBreaking(NonBreakingChange.EnumValueAdded _) -> true
                | _ -> false)

        match hasBreaking, hasAdditive with
        | true, _ -> ChangeKind.Major
        | false, true -> ChangeKind.Minor
        | false, false -> ChangeKind.Patch
