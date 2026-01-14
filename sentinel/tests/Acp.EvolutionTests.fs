module Acp.Tests.EvolutionTests

open System
open Xunit
open Acp.Assurance
open Acp.Evolution

/// BC-004 Tests: Protocol Evolution Context

module ``Edition Identity`` =

    [<Fact>]
    let ``EditionId create generates unique ids`` () =
        let id1 = EditionId.create ()
        let id2 = EditionId.create ()
        Assert.NotEqual(id1, id2)

    [<Fact>]
    let ``EditionId tryParse parses valid GUID`` () =
        let guid = Guid.NewGuid()
        let parsed = EditionId.tryParse (guid.ToString())
        Assert.Equal(Some(EditionId.fromGuid guid), parsed)

    [<Fact>]
    let ``EditionId tryParse returns None for invalid`` () =
        Assert.Equal(None, EditionId.tryParse "not-a-guid")

    [<Fact>]
    let ``DrrId roundtrip`` () =
        let id = DrrId.create "DRR-001"
        Assert.Equal("DRR-001", DrrId.value id)

    [<Fact>]
    let ``SchemaHash roundtrip`` () =
        let hash = SchemaHash.create "sha256:abc123"
        Assert.Equal("sha256:abc123", SchemaHash.value hash)

module ``Change Classification`` =

    [<Fact>]
    let ``ChangeKind.isBreaking returns true only for Major`` () =
        Assert.True(ChangeKind.isBreaking ChangeKind.Major)
        Assert.False(ChangeKind.isBreaking ChangeKind.Minor)
        Assert.False(ChangeKind.isBreaking ChangeKind.Patch)

    [<Fact>]
    let ``ChangeKind tryParse handles valid inputs`` () =
        Assert.Equal(Some ChangeKind.Patch, ChangeKind.tryParse "patch")
        Assert.Equal(Some ChangeKind.Minor, ChangeKind.tryParse "MINOR")
        Assert.Equal(Some ChangeKind.Major, ChangeKind.tryParse "Major")
        Assert.Equal(None, ChangeKind.tryParse "invalid")

    [<Fact>]
    let ``ChangeKind toString roundtrips`` () =
        for kind in [ ChangeKind.Patch; ChangeKind.Minor; ChangeKind.Major ] do
            let str = ChangeKind.toString kind
            Assert.Equal(Some kind, ChangeKind.tryParse str)

module ``Breaking Changes`` =

    [<Fact>]
    let ``BreakingChange.path extracts path`` () =
        let change = BreakingChange.FieldRemoved("$.foo.bar", "Removed deprecated field")
        Assert.Equal(Some "$.foo.bar", BreakingChange.path change)

    [<Fact>]
    let ``BehaviorChange has no path`` () =
        let change = BreakingChange.BehaviorChange "Changed timeout behavior"
        Assert.Equal(None, BreakingChange.path change)

    [<Fact>]
    let ``BreakingChange.describe produces readable message`` () =
        let change =
            BreakingChange.RequiredFieldAdded("$.response.data", "Added required data field")

        let desc = BreakingChange.describe change
        Assert.Contains("Required field added", desc)
        Assert.Contains("$.response.data", desc)

module ``Non-Breaking Changes`` =

    [<Fact>]
    let ``NonBreakingChange.path extracts path`` () =
        let change =
            NonBreakingChange.OptionalFieldAdded("$.meta.tags", "Added optional tags")

        Assert.Equal("$.meta.tags", NonBreakingChange.path change)

module ``DRR Reference`` =

    [<Fact>]
    let ``DrrReference.create sets defaults`` () =
        let ref = DrrReference.create (DrrId.create "DRR-001") "Added assurance envelopes"
        Assert.Equal(DrrId.create "DRR-001", ref.drrId)
        Assert.Equal("Added assurance envelopes", ref.summary)
        Assert.Equal(None, ref.uri)

    [<Fact>]
    let ``DrrReference.withUri adds URI`` () =
        let ref =
            DrrReference.create (DrrId.create "DRR-001") "Summary"
            |> DrrReference.withUri "https://github.com/acp/drr/DRR-001.md"

        Assert.Equal(Some "https://github.com/acp/drr/DRR-001.md", ref.uri)

module ``Compatibility Matrix`` =

    let sampleEditionId () = EditionId.create ()

    [<Fact>]
    let ``empty matrix has no relationships`` () =
        let eid = sampleEditionId ()
        let matrix = CompatibilityMatrix.empty eid
        Assert.True(matrix.backwardCompatibleWith.IsEmpty)
        Assert.True(matrix.forwardCompatibleWith.IsEmpty)
        Assert.True(matrix.breakingFrom.IsEmpty)

    [<Fact>]
    let ``addBackwardCompatible adds to set`` () =
        let eid = sampleEditionId ()
        let other = sampleEditionId ()

        let matrix =
            CompatibilityMatrix.empty eid |> CompatibilityMatrix.addBackwardCompatible other

        Assert.True(matrix.backwardCompatibleWith |> Set.contains other)

    [<Fact>]
    let ``validate catches self-reference`` () =
        let eid = sampleEditionId ()

        let matrix =
            { CompatibilityMatrix.empty eid with
                backwardCompatibleWith = Set.singleton eid }

        let errors = CompatibilityMatrix.validate matrix
        Assert.True(errors |> List.exists (fun e -> e.Contains("itself")))

    [<Fact>]
    let ``validate catches breaking and backward overlap`` () =
        let eid = sampleEditionId ()
        let other = sampleEditionId ()

        let matrix =
            { CompatibilityMatrix.empty eid with
                backwardCompatibleWith = Set.singleton other
                breakingFrom = Set.singleton other }

        let errors = CompatibilityMatrix.validate matrix
        Assert.True(errors |> List.exists (fun e -> e.Contains("breaking and backward")))

module ``Semantic Version`` =

    [<Fact>]
    let ``SemVer.create makes version`` () =
        let ver = SemVer.create 1 2 3
        Assert.Equal(1, ver.major)
        Assert.Equal(2, ver.minor)
        Assert.Equal(3, ver.patch)
        Assert.Equal(None, ver.prerelease)

    [<Fact>]
    let ``SemVer.toString formats correctly`` () =
        Assert.Equal("1.2.3", SemVer.toString (SemVer.create 1 2 3))
        Assert.Equal("1.0.0-alpha", SemVer.toString (SemVer.create 1 0 0 |> SemVer.withPrerelease "alpha"))

    [<Fact>]
    let ``SemVer.tryParse parses valid versions`` () =
        match SemVer.tryParse "1.2.3" with
        | Some v ->
            Assert.Equal(1, v.major)
            Assert.Equal(2, v.minor)
            Assert.Equal(3, v.patch)
        | None -> Assert.Fail("Should parse")

    [<Fact>]
    let ``SemVer.tryParse handles prerelease`` () =
        match SemVer.tryParse "1.0.0-beta.1" with
        | Some v -> Assert.Equal(Some "beta.1", v.prerelease)
        | None -> Assert.Fail("Should parse")

    [<Fact>]
    let ``SemVer.tryParse returns None for invalid`` () =
        Assert.Equal(None, SemVer.tryParse "not-a-version")
        Assert.Equal(None, SemVer.tryParse "1.2")

module ``Edition`` =

    let sampleEdition () =
        Edition.create (EditionId.create ()) (SemVer.create 1 0 0) (SchemaHash.create "sha256:genesis")

    [<Fact>]
    let ``create makes genesis edition`` () =
        let edition = sampleEdition ()
        Assert.Equal(None, edition.parentEdition)
        Assert.Equal(ChangeKind.Major, edition.changeKind)
        Assert.Empty(edition.drrRefs)

    [<Fact>]
    let ``withParent sets parent and change kind`` () =
        let parent = sampleEdition ()
        let child = sampleEdition () |> Edition.withParent parent.editionId ChangeKind.Minor
        Assert.Equal(Some parent.editionId, child.parentEdition)
        Assert.Equal(ChangeKind.Minor, child.changeKind)

    [<Fact>]
    let ``addDrr adds reference`` () =
        let drr = DrrReference.create (DrrId.create "DRR-001") "Test"
        let edition = sampleEdition () |> Edition.addDrr drr
        Assert.Single(edition.drrRefs) |> ignore

    [<Fact>]
    let ``deprecate sets deprecation date`` () =
        let now = DateTimeOffset.UtcNow
        let edition = sampleEdition () |> Edition.deprecate now
        Assert.True(Edition.isDeprecated edition)
        Assert.Equal(Some now, edition.deprecatedAt)

    [<Fact>]
    let ``validate catches missing rationale for non-genesis`` () =
        let parent = sampleEdition ()
        let child = sampleEdition () |> Edition.withParent parent.editionId ChangeKind.Minor
        let errors = Edition.validate child
        Assert.True(errors |> List.exists (fun e -> e.Contains("INV-EVO-01")))

    [<Fact>]
    let ``validate catches sunset without deprecation`` () =
        let edition =
            { sampleEdition () with
                sunsetAt = Some DateTimeOffset.UtcNow }

        let errors = Edition.validate edition
        Assert.True(errors |> List.exists (fun e -> e.Contains("INV-EVO-05")))

    [<Fact>]
    let ``validate catches non-Major genesis`` () =
        let edition =
            { sampleEdition () with
                changeKind = ChangeKind.Minor }

        let errors = Edition.validate edition
        Assert.True(errors |> List.exists (fun e -> e.Contains("Genesis")))

    [<Fact>]
    let ``valid genesis passes validation`` () =
        let edition = sampleEdition ()
        let errors = Edition.validate edition
        Assert.Empty(errors)

module ``Conformance Level`` =

    [<Fact>]
    let ``toAssuranceLevel maps correctly`` () =
        Assert.Equal(AssuranceLevel.L0, ConformanceLevel.toAssuranceLevel ConformanceLevel.Declared)
        Assert.Equal(AssuranceLevel.L1, ConformanceLevel.toAssuranceLevel ConformanceLevel.Tested)
        Assert.Equal(AssuranceLevel.L2, ConformanceLevel.toAssuranceLevel ConformanceLevel.Certified)

    [<Fact>]
    let ``fromAssuranceLevel maps correctly`` () =
        Assert.Equal(ConformanceLevel.Declared, ConformanceLevel.fromAssuranceLevel AssuranceLevel.L0)
        Assert.Equal(ConformanceLevel.Tested, ConformanceLevel.fromAssuranceLevel AssuranceLevel.L1)
        Assert.Equal(ConformanceLevel.Certified, ConformanceLevel.fromAssuranceLevel AssuranceLevel.L2)

    [<Fact>]
    let ``tryParse handles valid inputs`` () =
        Assert.Equal(Some ConformanceLevel.Declared, ConformanceLevel.tryParse "declared")
        Assert.Equal(Some ConformanceLevel.Tested, ConformanceLevel.tryParse "TESTED")
        Assert.Equal(None, ConformanceLevel.tryParse "invalid")

module ``Deviation`` =

    [<Fact>]
    let ``minor creates minor deviation`` () =
        let dev = Deviation.minor "$.foo" "string" "number"
        Assert.Equal("minor", dev.severity)
        Assert.Equal("$.foo", dev.path)

    [<Fact>]
    let ``major creates major deviation`` () =
        let dev = Deviation.major "$.bar" "required" "missing"
        Assert.Equal("major", dev.severity)

module ``Edition Claim`` =

    [<Fact>]
    let ``declared has no test results`` () =
        let eid = EditionId.create ()
        let claim = EditionClaim.declared eid
        Assert.Equal(ConformanceLevel.Declared, claim.conformanceLevel)
        Assert.Equal(None, claim.testResultsRef)

    [<Fact>]
    let ``tested has test results`` () =
        let eid = EditionId.create ()
        let claim = EditionClaim.tested eid "https://ci.example.com/run/123"
        Assert.Equal(ConformanceLevel.Tested, claim.conformanceLevel)
        Assert.Equal(Some "https://ci.example.com/run/123", claim.testResultsRef)

module ``Conformance Report`` =

    [<Fact>]
    let ``create makes report`` () =
        let eid = EditionId.create ()
        let report = ConformanceReport.create "impl-1" eid ConformanceLevel.Declared
        Assert.Equal("impl-1", report.implementationId)
        Assert.Equal(eid, report.claimedEdition)
        Assert.Empty(report.deviations)

    [<Fact>]
    let ``validate catches Tested without evidence`` () =
        let eid = EditionId.create ()
        let report = ConformanceReport.create "impl-1" eid ConformanceLevel.Tested
        let errors = ConformanceReport.validate report
        Assert.True(errors |> List.exists (fun e -> e.Contains("INV-EVO-06")))

    [<Fact>]
    let ``validate passes for Tested with evidence`` () =
        let eid = EditionId.create ()

        let report =
            ConformanceReport.create "impl-1" eid ConformanceLevel.Tested
            |> ConformanceReport.withTestResults "https://ci.example.com/run/123"

        let errors = ConformanceReport.validate report
        Assert.Empty(errors)

module ``Edition Registry`` =

    let makeEdition version =
        Edition.create
            (EditionId.create ())
            (SemVer.create version 0 0)
            (SchemaHash.create (sprintf "sha256:v%d" version))

    [<Fact>]
    let ``empty registry has no editions`` () =
        let reg = EditionRegistry.empty
        Assert.True(reg.editions.IsEmpty)
        Assert.Equal(None, reg.current)

    [<Fact>]
    let ``register adds edition`` () =
        let edition = makeEdition 1
        let reg = EditionRegistry.empty |> EditionRegistry.register edition
        Assert.True(reg.editions.ContainsKey(edition.editionId))

    [<Fact>]
    let ``setCurrent sets current edition`` () =
        let edition = makeEdition 1

        let reg =
            EditionRegistry.empty
            |> EditionRegistry.register edition
            |> EditionRegistry.setCurrent edition.editionId

        Assert.Equal(Some edition.editionId, reg.current)

    [<Fact>]
    let ``tryFindCurrent returns current edition`` () =
        let edition = makeEdition 1

        let reg =
            EditionRegistry.empty
            |> EditionRegistry.register edition
            |> EditionRegistry.setCurrent edition.editionId

        match EditionRegistry.tryFindCurrent reg with
        | Some e -> Assert.Equal(edition.editionId, e.editionId)
        | None -> Assert.Fail("Should find current")

    [<Fact>]
    let ``lineage returns ancestry chain`` () =
        let v1 = makeEdition 1

        let v2 =
            makeEdition 2
            |> Edition.withParent v1.editionId ChangeKind.Minor
            |> Edition.addDrr (DrrReference.create (DrrId.create "DRR-1") "Minor update")

        let v3 =
            makeEdition 3
            |> Edition.withParent v2.editionId ChangeKind.Major
            |> Edition.addDrr (DrrReference.create (DrrId.create "DRR-2") "Major update")

        let reg =
            EditionRegistry.empty
            |> EditionRegistry.register v1
            |> EditionRegistry.register v2
            |> EditionRegistry.register v3

        let lineage = EditionRegistry.lineage v3.editionId reg
        Assert.Equal(3, lineage.Length)
        Assert.Equal(v1.editionId, lineage.[0].editionId)
        Assert.Equal(v2.editionId, lineage.[1].editionId)
        Assert.Equal(v3.editionId, lineage.[2].editionId)

module ``Evolution Findings`` =

    [<Fact>]
    let ``finding codes are unique`` () =
        let eid = EditionId.create ()

        let findings =
            [ EvolutionFinding.MissingRationale eid
              EvolutionFinding.ChangeKindMismatch(eid, ChangeKind.Patch, ChangeKind.Major)
              EvolutionFinding.IntransitiveCompatibility(eid, eid, eid)
              EvolutionFinding.SchemaHashMismatch(eid, SchemaHash.create "a", SchemaHash.create "b")
              EvolutionFinding.SunsetWithoutDeprecation eid
              EvolutionFinding.ConformanceWithoutEvidence("impl", ConformanceLevel.Tested)
              EvolutionFinding.OrphanedEdition(eid, eid) ]

        let codes = findings |> List.map EvolutionFinding.code
        Assert.Equal(codes.Length, codes |> List.distinct |> List.length)

    [<Fact>]
    let ``describe produces human-readable messages`` () =
        let eid = EditionId.create ()
        let finding = EvolutionFinding.MissingRationale eid
        let desc = EvolutionFinding.describe finding
        Assert.Contains("INV-EVO-01", desc)

module ``Schema Change Classification`` =

    [<Fact>]
    let ``classifyChanges returns Major for breaking changes`` () =
        let changes = [ Breaking(BreakingChange.FieldRemoved("$.foo", "removed")) ]
        Assert.Equal(ChangeKind.Major, classifyChanges changes)

    [<Fact>]
    let ``classifyChanges returns Minor for additive changes`` () =
        let changes =
            [ NonBreaking(NonBreakingChange.OptionalFieldAdded("$.bar", "added")) ]

        Assert.Equal(ChangeKind.Minor, classifyChanges changes)

    [<Fact>]
    let ``classifyChanges returns Patch for doc-only changes`` () =
        let changes = [ NonBreaking(NonBreakingChange.DocumentationUpdated "$.baz") ]
        Assert.Equal(ChangeKind.Patch, classifyChanges changes)

    [<Fact>]
    let ``classifyChanges returns Major when mixed`` () =
        let changes =
            [ Breaking(BreakingChange.FieldRemoved("$.foo", "removed"))
              NonBreaking(NonBreakingChange.OptionalFieldAdded("$.bar", "added")) ]

        Assert.Equal(ChangeKind.Major, classifyChanges changes)

    [<Fact>]
    let ``classifyChanges returns Patch for empty`` () =
        Assert.Equal(ChangeKind.Patch, classifyChanges [])
