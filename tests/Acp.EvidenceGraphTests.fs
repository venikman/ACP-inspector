module Acp.Tests.EvidenceGraphTests

open System
open Xunit
open Acp.Assurance
open Acp.EvidenceGraph

/// BC-001 Tests: Evidence Graph (G.6, A.10)

module ``Evidence Node`` =

    [<Fact>]
    let ``ClaimId roundtrip`` () =
        let id = ClaimId.create "claim-1"
        Assert.Equal("claim-1", ClaimId.value id)

    [<Fact>]
    let ``EvidenceId roundtrip`` () =
        let id = EvidenceId.create "evidence-1"
        Assert.Equal("evidence-1", EvidenceId.value id)

    [<Fact>]
    let ``node id returns correct string for Claim`` () =
        let node = EvidenceNode.Claim(ClaimId.create "c1", "test claim")
        Assert.Equal("c1", EvidenceNode.id node)

    [<Fact>]
    let ``node id returns correct string for Evidence`` () =
        let node =
            EvidenceNode.Evidence(EvidenceId.create "e1", GroundingRef.create "file:///test.txt")

        Assert.Equal("e1", EvidenceNode.id node)

    [<Fact>]
    let ``node id returns correct string for GroundingHolon`` () =
        let node = EvidenceNode.GroundingHolon(GroundingRef.create "file:///data.json")
        Assert.Equal("file:///data.json", EvidenceNode.id node)

module ``Evidence Edge`` =

    [<Fact>]
    let ``create edge without label`` () =
        let edge = EvidenceEdge.create "c1" "e1" AssuranceLevel.L2
        Assert.Equal("c1", edge.source)
        Assert.Equal("e1", edge.target)
        Assert.Equal(AssuranceLevel.L2, edge.level)
        Assert.Equal(None, edge.label)

    [<Fact>]
    let ``withLabel adds label to edge`` () =
        let edge =
            EvidenceEdge.create "c1" "e1" AssuranceLevel.L1
            |> EvidenceEdge.withLabel "supports"

        Assert.Equal(Some "supports", edge.label)

module ``Evidence Graph Construction`` =

    [<Fact>]
    let ``empty graph has no nodes or edges`` () =
        let graph = EvidenceGraph.empty
        Assert.Empty(EvidenceGraph.nodes graph)
        Assert.Empty(EvidenceGraph.edges graph)

    [<Fact>]
    let ``addNode adds node to graph`` () =
        let graph =
            EvidenceGraph.empty
            |> EvidenceGraph.addNode (EvidenceNode.Claim(ClaimId.create "c1", "claim"))

        Assert.Single(EvidenceGraph.nodes graph) |> ignore
        Assert.True(EvidenceGraph.hasNode "c1" graph)

    [<Fact>]
    let ``addEdge adds edge to graph`` () =
        let edge = EvidenceEdge.create "c1" "e1" AssuranceLevel.L2

        let graph = EvidenceGraph.empty |> EvidenceGraph.addEdge edge

        Assert.Single(EvidenceGraph.edges graph) |> ignore
        Assert.Single(EvidenceGraph.outgoingEdges "c1" graph) |> ignore

    [<Fact>]
    let ``multiple edges from same node`` () =
        let edge1 = EvidenceEdge.create "c1" "e1" AssuranceLevel.L2
        let edge2 = EvidenceEdge.create "c1" "e2" AssuranceLevel.L1

        let graph =
            EvidenceGraph.empty
            |> EvidenceGraph.addEdge edge1
            |> EvidenceGraph.addEdge edge2

        let outgoing = EvidenceGraph.outgoingEdges "c1" graph
        Assert.Equal(2, outgoing.Length)

    [<Fact>]
    let ``successors returns target nodes`` () =
        let graph =
            EvidenceGraph.empty
            |> EvidenceGraph.addEdge (EvidenceEdge.create "c1" "e1" AssuranceLevel.L2)
            |> EvidenceGraph.addEdge (EvidenceEdge.create "c1" "e2" AssuranceLevel.L1)

        let succ = EvidenceGraph.successors "c1" graph
        Assert.Equal(2, succ.Length)
        Assert.Contains("e1", succ)
        Assert.Contains("e2", succ)

    [<Fact>]
    let ``tryGetNode returns Some when node exists`` () =
        let node = EvidenceNode.Claim(ClaimId.create "c1", "test")

        let graph = EvidenceGraph.empty |> EvidenceGraph.addNode node

        let result = EvidenceGraph.tryGetNode "c1" graph
        Assert.True(result.IsSome)

    [<Fact>]
    let ``tryGetNode returns None when node missing`` () =
        let result = EvidenceGraph.tryGetNode "c999" EvidenceGraph.empty
        Assert.True(result.IsNone)

module ``DAG Validation`` =

    [<Fact>]
    let ``isAcyclic returns true for acyclic graph`` () =
        let graph =
            EvidenceGraph.empty
            |> EvidenceGraph.addNode (EvidenceNode.Claim(ClaimId.create "c1", "claim"))
            |> EvidenceGraph.addNode (EvidenceNode.Evidence(EvidenceId.create "e1", GroundingRef.create "file:///a"))
            |> EvidenceGraph.addNode (EvidenceNode.GroundingHolon(GroundingRef.create "file:///g"))
            |> EvidenceGraph.addEdge (EvidenceEdge.create "c1" "e1" AssuranceLevel.L2)
            |> EvidenceGraph.addEdge (EvidenceEdge.create "e1" "file:///g" AssuranceLevel.L2)

        Assert.True(EvidenceGraph.isAcyclic graph)

    [<Fact>]
    let ``isAcyclic returns false for cyclic graph`` () =
        let graph =
            EvidenceGraph.empty
            |> EvidenceGraph.addNode (EvidenceNode.Claim(ClaimId.create "c1", "claim"))
            |> EvidenceGraph.addNode (EvidenceNode.Claim(ClaimId.create "c2", "claim2"))
            |> EvidenceGraph.addEdge (EvidenceEdge.create "c1" "c2" AssuranceLevel.L1)
            |> EvidenceGraph.addEdge (EvidenceEdge.create "c2" "c1" AssuranceLevel.L1) // Cycle!

        Assert.False(EvidenceGraph.isAcyclic graph)

    [<Fact>]
    let ``validate returns Ok for valid graph`` () =
        let graph =
            EvidenceGraph.empty
            |> EvidenceGraph.addNode (EvidenceNode.Claim(ClaimId.create "c1", "claim"))
            |> EvidenceGraph.addNode (EvidenceNode.GroundingHolon(GroundingRef.create "file:///g"))
            |> EvidenceGraph.addEdge (EvidenceEdge.create "c1" "file:///g" AssuranceLevel.L2)

        match EvidenceGraph.validate graph with
        | Ok() -> Assert.True(true)
        | Error msg -> Assert.Fail(sprintf "Unexpected error: %s" msg)

    [<Fact>]
    let ``validate returns Error for cyclic graph`` () =
        let graph =
            EvidenceGraph.empty
            |> EvidenceGraph.addNode (EvidenceNode.Claim(ClaimId.create "c1", "claim"))
            |> EvidenceGraph.addNode (EvidenceNode.Claim(ClaimId.create "c2", "claim2"))
            |> EvidenceGraph.addEdge (EvidenceEdge.create "c1" "c2" AssuranceLevel.L1)
            |> EvidenceGraph.addEdge (EvidenceEdge.create "c2" "c1" AssuranceLevel.L1)

        match EvidenceGraph.validate graph with
        | Ok() -> Assert.Fail("Expected cycle detection error")
        | Error msg -> Assert.Contains("Cycle detected", msg)

    [<Fact>]
    let ``validate returns Error for missing node`` () =
        let graph =
            EvidenceGraph.empty
            |> EvidenceGraph.addNode (EvidenceNode.Claim(ClaimId.create "c1", "claim"))
            |> EvidenceGraph.addEdge (EvidenceEdge.create "c1" "missing" AssuranceLevel.L2)

        match EvidenceGraph.validate graph with
        | Ok() -> Assert.Fail("Expected missing node error")
        | Error msg -> Assert.Contains("Missing nodes", msg)

module ``Weakest-Link Computation`` =

    [<Fact>]
    let ``computeWeakestLink returns None for claim without path`` () =
        let graph =
            EvidenceGraph.empty
            |> EvidenceGraph.addNode (EvidenceNode.Claim(ClaimId.create "c1", "isolated claim"))

        let result = EvidenceGraph.computeWeakestLink (ClaimId.create "c1") graph

        Assert.True(result.IsNone)

    [<Fact>]
    let ``computeWeakestLink returns L2 for direct path to grounding`` () =
        let graph =
            EvidenceGraph.empty
            |> EvidenceGraph.addNode (EvidenceNode.Claim(ClaimId.create "c1", "claim"))
            |> EvidenceGraph.addNode (EvidenceNode.GroundingHolon(GroundingRef.create "file:///g"))
            |> EvidenceGraph.addEdge (EvidenceEdge.create "c1" "file:///g" AssuranceLevel.L2)

        let result = EvidenceGraph.computeWeakestLink (ClaimId.create "c1") graph

        Assert.Equal(Some AssuranceLevel.L2, result)

    [<Fact>]
    let ``computeWeakestLink returns weakest level in path`` () =
        // c1 --L2--> e1 --L1--> grounding
        // Weakest link: L1
        let graph =
            EvidenceGraph.empty
            |> EvidenceGraph.addNode (EvidenceNode.Claim(ClaimId.create "c1", "claim"))
            |> EvidenceGraph.addNode (EvidenceNode.Evidence(EvidenceId.create "e1", GroundingRef.create "file:///a"))
            |> EvidenceGraph.addNode (EvidenceNode.GroundingHolon(GroundingRef.create "file:///g"))
            |> EvidenceGraph.addEdge (EvidenceEdge.create "c1" "e1" AssuranceLevel.L2)
            |> EvidenceGraph.addEdge (EvidenceEdge.create "e1" "file:///g" AssuranceLevel.L1)

        let result = EvidenceGraph.computeWeakestLink (ClaimId.create "c1") graph

        Assert.Equal(Some AssuranceLevel.L1, result)

    [<Fact>]
    let ``computeWeakestLink chooses best path among multiple`` () =
        // c1 has two paths:
        //   c1 --L0--> e1 --L2--> g  (weakest: L0)
        //   c1 --L1--> e2 --L2--> g  (weakest: L1)
        // Should return L1 (best of the two paths)
        let graph =
            EvidenceGraph.empty
            |> EvidenceGraph.addNode (EvidenceNode.Claim(ClaimId.create "c1", "claim"))
            |> EvidenceGraph.addNode (EvidenceNode.Evidence(EvidenceId.create "e1", GroundingRef.create "file:///a"))
            |> EvidenceGraph.addNode (EvidenceNode.Evidence(EvidenceId.create "e2", GroundingRef.create "file:///b"))
            |> EvidenceGraph.addNode (EvidenceNode.GroundingHolon(GroundingRef.create "file:///g"))
            |> EvidenceGraph.addEdge (EvidenceEdge.create "c1" "e1" AssuranceLevel.L0)
            |> EvidenceGraph.addEdge (EvidenceEdge.create "c1" "e2" AssuranceLevel.L1)
            |> EvidenceGraph.addEdge (EvidenceEdge.create "e1" "file:///g" AssuranceLevel.L2)
            |> EvidenceGraph.addEdge (EvidenceEdge.create "e2" "file:///g" AssuranceLevel.L2)

        let result = EvidenceGraph.computeWeakestLink (ClaimId.create "c1") graph

        Assert.Equal(Some AssuranceLevel.L1, result)

    [<Fact>]
    let ``getEvidencePaths returns all paths`` () =
        let graph =
            EvidenceGraph.empty
            |> EvidenceGraph.addNode (EvidenceNode.Claim(ClaimId.create "c1", "claim"))
            |> EvidenceGraph.addNode (EvidenceNode.Evidence(EvidenceId.create "e1", GroundingRef.create "file:///a"))
            |> EvidenceGraph.addNode (EvidenceNode.Evidence(EvidenceId.create "e2", GroundingRef.create "file:///b"))
            |> EvidenceGraph.addNode (EvidenceNode.GroundingHolon(GroundingRef.create "file:///g"))
            |> EvidenceGraph.addEdge (EvidenceEdge.create "c1" "e1" AssuranceLevel.L2)
            |> EvidenceGraph.addEdge (EvidenceEdge.create "c1" "e2" AssuranceLevel.L1)
            |> EvidenceGraph.addEdge (EvidenceEdge.create "e1" "file:///g" AssuranceLevel.L2)
            |> EvidenceGraph.addEdge (EvidenceEdge.create "e2" "file:///g" AssuranceLevel.L2)

        let paths = EvidenceGraph.getEvidencePaths (ClaimId.create "c1") graph
        Assert.Equal(2, paths.Length)

    [<Fact>]
    let ``hasGrounding returns true when path exists`` () =
        let graph =
            EvidenceGraph.empty
            |> EvidenceGraph.addNode (EvidenceNode.Claim(ClaimId.create "c1", "claim"))
            |> EvidenceGraph.addNode (EvidenceNode.GroundingHolon(GroundingRef.create "file:///g"))
            |> EvidenceGraph.addEdge (EvidenceEdge.create "c1" "file:///g" AssuranceLevel.L2)

        Assert.True(EvidenceGraph.hasGrounding (ClaimId.create "c1") graph)

    [<Fact>]
    let ``hasGrounding returns false when no path exists`` () =
        let graph =
            EvidenceGraph.empty
            |> EvidenceGraph.addNode (EvidenceNode.Claim(ClaimId.create "c1", "isolated"))

        Assert.False(EvidenceGraph.hasGrounding (ClaimId.create "c1") graph)

module ``Builder API`` =

    [<Fact>]
    let ``builder creates valid graph`` () =
        let result =
            EvidenceGraphBuilder()
                .AddClaim(ClaimId.create "c1", "test claim")
                .AddGrounding(GroundingRef.create "file:///g")
                .AddEdge("c1", "file:///g", AssuranceLevel.L2)
                .Build()

        match result with
        | Ok graph ->
            Assert.True(EvidenceGraph.hasNode "c1" graph)
            Assert.Single(EvidenceGraph.edges graph) |> ignore
        | Error msg -> Assert.Fail(sprintf "Build failed: %s" msg)

    [<Fact>]
    let ``builder detects cycles`` () =
        let result =
            EvidenceGraphBuilder()
                .AddClaim(ClaimId.create "c1", "claim1")
                .AddClaim(ClaimId.create "c2", "claim2")
                .AddEdge("c1", "c2", AssuranceLevel.L1)
                .AddEdge("c2", "c1", AssuranceLevel.L1)
                .Build()

        match result with
        | Ok _ -> Assert.Fail("Expected cycle detection")
        | Error msg -> Assert.Contains("Cycle detected", msg)

    [<Fact>]
    let ``builder detects missing nodes`` () =
        let result =
            EvidenceGraphBuilder()
                .AddClaim(ClaimId.create "c1", "claim")
                .AddEdge("c1", "missing", AssuranceLevel.L2)
                .Build()

        match result with
        | Ok _ -> Assert.Fail("Expected missing node error")
        | Error msg -> Assert.Contains("Missing nodes", msg)

    [<Fact>]
    let ``builder fluent API chains calls`` () =
        let result =
            EvidenceGraphBuilder()
                .AddClaim(ClaimId.create "c1", "claim")
                .AddEvidence(EvidenceId.create "e1", GroundingRef.create "file:///a")
                .AddGrounding(GroundingRef.create "file:///g")
                .AddEdge("c1", "e1", AssuranceLevel.L2)
                .AddEdge("e1", "file:///g", AssuranceLevel.L2, "supports")
                .Build()

        match result with
        | Ok graph ->
            Assert.Equal(3, (EvidenceGraph.nodes graph).Length)
            Assert.Equal(2, (EvidenceGraph.edges graph).Length)
        | Error msg -> Assert.Fail(sprintf "Build failed: %s" msg)

module ``JSON Serialization`` =

    [<Fact>]
    let ``toJson serializes empty graph`` () =
        let json = EvidenceGraph.toJson EvidenceGraph.empty
        Assert.Contains("\"nodes\":[]", json)
        Assert.Contains("\"edges\":[]", json)

    [<Fact>]
    let ``toJson serializes graph with nodes`` () =
        let graph =
            EvidenceGraph.empty
            |> EvidenceGraph.addNode (EvidenceNode.Claim(ClaimId.create "c1", "test"))
            |> EvidenceGraph.addNode (EvidenceNode.GroundingHolon(GroundingRef.create "file:///g"))

        let json = EvidenceGraph.toJson graph
        Assert.Contains("\"type\":\"claim\"", json)
        Assert.Contains("\"type\":\"grounding\"", json)

    [<Fact>]
    let ``toJson serializes graph with edges`` () =
        let graph =
            EvidenceGraph.empty
            |> EvidenceGraph.addNode (EvidenceNode.Claim(ClaimId.create "c1", "test"))
            |> EvidenceGraph.addNode (EvidenceNode.GroundingHolon(GroundingRef.create "file:///g"))
            |> EvidenceGraph.addEdge (EvidenceEdge.create "c1" "file:///g" AssuranceLevel.L2)

        let json = EvidenceGraph.toJson graph
        Assert.Contains("\"source\":\"c1\"", json)
        Assert.Contains("\"target\":\"file:///g\"", json)
        Assert.Contains("\"level\":\"L2\"", json)
