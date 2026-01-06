module Acp.Tests.SemanticTests

open System
open Xunit
open Acp.Assurance
open Acp.Semantic

/// BC-002 Tests: Semantic Alignment Context

module ``Context Identity`` =

    [<Fact>]
    let ``ContextId roundtrip`` () =
        let ctx = ContextId.create "my-context"
        Assert.Equal("my-context", ContextId.value ctx)

    [<Fact>]
    let ``well-known ACP protocol context`` () =
        Assert.Equal("urn:acp:protocol:v1", ContextId.value ContextId.acpProtocol)

    [<Fact>]
    let ``KindId roundtrip`` () =
        let kind = KindId.create "task"
        Assert.Equal("task", KindId.value kind)

module ``Intension Spec`` =

    [<Fact>]
    let ``informal has F0 formality`` () =
        let spec = IntensionSpec.informal "A task is work to be done"
        Assert.Equal(Formality.F0, spec.formality)
        Assert.Equal(None, spec.formalDefinition)

    [<Fact>]
    let ``semiStructured has F3 formality`` () =
        let spec = IntensionSpec.semiStructured "A structured task"
        Assert.Equal(Formality.F3, spec.formality)

    [<Fact>]
    let ``formal has F6 formality with definition`` () =
        let spec = IntensionSpec.formal "A formal task" "Task ::= (id, status, owner)"
        Assert.Equal(Formality.F6, spec.formality)
        Assert.Equal(Some "Task ::= (id, status, owner)", spec.formalDefinition)

module ``Kind Signature`` =

    let sampleSignature () =
        KindSignature.create
            (KindId.create "task")
            "Task"
            (ContextId.create "my-ctx")
            (IntensionSpec.informal "A work item")

    [<Fact>]
    let ``create signature with defaults`` () =
        let sig' = sampleSignature ()
        Assert.Equal(KindId.create "task", sig'.kindId)
        Assert.Equal("Task", sig'.kindName)
        Assert.Equal(ExtensionSpec.Open, sig'.extension)
        Assert.Empty(sig'.constraints)
        Assert.Empty(sig'.superKinds)

    [<Fact>]
    let ``withExtension sets extension`` () =
        let sig' =
            sampleSignature ()
            |> KindSignature.withExtension (ExtensionSpec.Enumerated [ "open"; "closed"; "in-progress" ])

        match sig'.extension with
        | ExtensionSpec.Enumerated values -> Assert.Equal(3, values.Length)
        | _ -> Assert.Fail("Expected Enumerated")

    [<Fact>]
    let ``withConstraint adds constraint`` () =
        let sig' =
            sampleSignature ()
            |> KindSignature.withConstraint "id must be unique"
            |> KindSignature.withConstraint "status is required"

        Assert.Equal(2, sig'.constraints.Length)

    [<Fact>]
    let ``withSuperKind adds inheritance`` () =
        let sig' =
            sampleSignature () |> KindSignature.withSuperKind (KindId.create "work-item")

        Assert.Single(sig'.superKinds) |> ignore
        Assert.Contains(KindId.create "work-item", sig'.superKinds)

    [<Fact>]
    let ``hasMinFormality returns true when met`` () =
        let sig' = sampleSignature ()
        Assert.True(KindSignature.hasMinFormality Formality.F0 sig')

    [<Fact>]
    let ``hasMinFormality returns false when not met`` () =
        let sig' = sampleSignature ()
        Assert.False(KindSignature.hasMinFormality Formality.F3 sig')

module ``Kind Bridge`` =

    let sampleBridge () =
        KindBridge.create
            (ContextId.create "ctx-a")
            (KindId.create "task-a")
            (ContextId.create "ctx-b")
            (KindId.create "task-b")
            MappingType.Equivalent
            CongruenceLevel.CL4

    [<Fact>]
    let ``create bridge with equivalent mapping`` () =
        let bridge = sampleBridge ()
        Assert.Equal(MappingType.Equivalent, bridge.mappingType)
        Assert.Equal(CongruenceLevel.CL4, bridge.congruenceLevel)
        Assert.True(bridge.bidirectional)
        Assert.Empty(bridge.lossNotes)

    [<Fact>]
    let ``create bridge with subkind is not bidirectional`` () =
        let bridge =
            KindBridge.create
                (ContextId.create "ctx-a")
                (KindId.create "sub")
                (ContextId.create "ctx-b")
                (KindId.create "super")
                MappingType.Subkind
                CongruenceLevel.CL3

        Assert.False(bridge.bidirectional)

    [<Fact>]
    let ``withLossNote adds note`` () =
        let bridge =
            sampleBridge ()
            |> KindBridge.withLossNote "precision lost"
            |> KindBridge.withLossNote "timezone info dropped"

        Assert.Equal(2, bridge.lossNotes.Length)

    [<Fact>]
    let ``makeBidirectional sets flag`` () =
        let bridge =
            KindBridge.create
                (ContextId.create "a")
                (KindId.create "x")
                (ContextId.create "b")
                (KindId.create "y")
                MappingType.Overlapping
                CongruenceLevel.CL2
            |> KindBridge.makeBidirectional

        Assert.True(bridge.bidirectional)

    [<Fact>]
    let ``validate catches CL4 with non-Equivalent`` () =
        let bridge =
            { KindBridge.create
                  (ContextId.create "a")
                  (KindId.create "x")
                  (ContextId.create "b")
                  (KindId.create "y")
                  MappingType.Subkind
                  CongruenceLevel.CL4 with
                bidirectional = false }

        let errors = KindBridge.validate bridge
        Assert.Single(errors) |> ignore
        Assert.Contains("CL4 requires Equivalent", errors.Head)

    [<Fact>]
    let ``validate catches Disjoint with non-CL0`` () =
        let bridge =
            KindBridge.create
                (ContextId.create "a")
                (KindId.create "x")
                (ContextId.create "b")
                (KindId.create "y")
                MappingType.Disjoint
                CongruenceLevel.CL2

        let errors = KindBridge.validate bridge
        Assert.Single(errors) |> ignore
        Assert.Contains("Disjoint mapping requires CL0", errors.Head)

    [<Fact>]
    let ``reverse swaps source and target`` () =
        let bridge = sampleBridge ()
        let reversed = KindBridge.reverse bridge
        Assert.Equal(bridge.sourceKind, reversed.targetKind)
        Assert.Equal(bridge.targetKind, reversed.sourceKind)

    [<Fact>]
    let ``reverse swaps Subkind to Superkind`` () =
        let bridge =
            KindBridge.create
                (ContextId.create "a")
                (KindId.create "x")
                (ContextId.create "b")
                (KindId.create "y")
                MappingType.Subkind
                CongruenceLevel.CL3

        let reversed = KindBridge.reverse bridge
        Assert.Equal(MappingType.Superkind, reversed.mappingType)

module ``Agent Context`` =

    [<Fact>]
    let ``create empty context`` () =
        let ctx = AgentContext.create (ContextId.create "test")
        Assert.Equal(ContextId.create "test", ctx.contextId)
        Assert.Equal(None, ctx.agentId)
        Assert.True(ctx.kindSignatures.IsEmpty)
        Assert.Empty(ctx.invariants)

    [<Fact>]
    let ``withAgentId sets agent`` () =
        let ctx =
            AgentContext.create (ContextId.create "test")
            |> AgentContext.withAgentId "agent-123"

        Assert.Equal(Some "agent-123", ctx.agentId)

    [<Fact>]
    let ``withVocabulary sets vocabulary ref`` () =
        let ctx =
            AgentContext.create (ContextId.create "test")
            |> AgentContext.withVocabulary "https://vocab.example.com/v1"

        Assert.Equal(Some "https://vocab.example.com/v1", ctx.vocabularyRef)

    [<Fact>]
    let ``addKind adds signature`` () =
        let sig' =
            KindSignature.create
                (KindId.create "task")
                "Task"
                (ContextId.create "test")
                (IntensionSpec.informal "Work item")

        let ctx = AgentContext.create (ContextId.create "test") |> AgentContext.addKind sig'
        Assert.True(AgentContext.hasKind (KindId.create "task") ctx)

    [<Fact>]
    let ``tryGetKind finds kind`` () =
        let kindId = KindId.create "task"

        let sig' =
            KindSignature.create kindId "Task" (ContextId.create "test") (IntensionSpec.informal "Work")

        let ctx = AgentContext.create (ContextId.create "test") |> AgentContext.addKind sig'

        match AgentContext.tryGetKind kindId ctx with
        | Some s -> Assert.Equal("Task", s.kindName)
        | None -> Assert.Fail("Kind not found")

    [<Fact>]
    let ``fingerprint produces stable hash`` () =
        let ctx =
            AgentContext.create (ContextId.create "test")
            |> AgentContext.addKind (
                KindSignature.create (KindId.create "a") "A" (ContextId.create "test") (IntensionSpec.informal "A")
            )

        let fp1 = AgentContext.fingerprint ctx
        let fp2 = AgentContext.fingerprint ctx
        Assert.Equal(fp1, fp2)
        Assert.StartsWith("test:", fp1)

module ``Alignment Bridge`` =

    let sampleKindBridge () =
        KindBridge.create
            (ContextId.create "ctx-a")
            (KindId.create "task-a")
            (ContextId.create "ctx-b")
            (KindId.create "task-b")
            MappingType.Equivalent
            CongruenceLevel.CL3

    [<Fact>]
    let ``create with empty bridges has CL0`` () =
        let bridge =
            AlignmentBridge.create "bridge-1" (ContextId.create "ctx-a") (ContextId.create "ctx-b") []

        Assert.Equal(CongruenceLevel.CL0, bridge.aggregateCL)

    [<Fact>]
    let ``create computes aggregate CL`` () =
        let kb1 =
            KindBridge.create
                (ContextId.create "a")
                (KindId.create "x")
                (ContextId.create "b")
                (KindId.create "y")
                MappingType.Equivalent
                CongruenceLevel.CL4

        let kb2 =
            KindBridge.create
                (ContextId.create "a")
                (KindId.create "p")
                (ContextId.create "b")
                (KindId.create "q")
                MappingType.Overlapping
                CongruenceLevel.CL2

        let bridge =
            AlignmentBridge.create "b1" (ContextId.create "a") (ContextId.create "b") [ kb1; kb2 ]
        // Aggregate should be weakest = CL2
        Assert.Equal(CongruenceLevel.CL2, bridge.aggregateCL)

    [<Fact>]
    let ``addKindBridge updates aggregate`` () =
        let bridge =
            AlignmentBridge.create "b1" (ContextId.create "a") (ContextId.create "b") []

        let kb =
            KindBridge.create
                (ContextId.create "a")
                (KindId.create "x")
                (ContextId.create "b")
                (KindId.create "y")
                MappingType.Equivalent
                CongruenceLevel.CL3

        let updated = AlignmentBridge.addKindBridge kb bridge
        Assert.Equal(CongruenceLevel.CL3, updated.aggregateCL)
        Assert.Single(updated.kindBridges) |> ignore

    [<Fact>]
    let ``isValid checks validity period`` () =
        let bridge =
            AlignmentBridge.create "b1" (ContextId.create "a") (ContextId.create "b") []

        let now = DateTimeOffset.UtcNow
        Assert.True(AlignmentBridge.isValid now bridge)

    [<Fact>]
    let ``isValid returns false after expiry`` () =
        let bridge =
            AlignmentBridge.create "b1" (ContextId.create "a") (ContextId.create "b") []
            |> AlignmentBridge.withValidity (DateTimeOffset.UtcNow.AddHours(-1.0))

        let now = DateTimeOffset.UtcNow
        Assert.False(AlignmentBridge.isValid now bridge)

    [<Fact>]
    let ``validate detects aggregate CL mismatch`` () =
        let kb =
            KindBridge.create
                (ContextId.create "a")
                (KindId.create "x")
                (ContextId.create "b")
                (KindId.create "y")
                MappingType.Equivalent
                CongruenceLevel.CL2

        let bridge =
            { AlignmentBridge.create "b1" (ContextId.create "a") (ContextId.create "b") [ kb ] with
                aggregateCL = CongruenceLevel.CL4 } // Lie about CL

        let errors = AlignmentBridge.validate bridge
        Assert.True(errors |> List.exists (fun e -> e.Contains("Aggregate CL mismatch")))

    [<Fact>]
    let ``tryFindKindBridge finds by source kind`` () =
        let kb =
            KindBridge.create
                (ContextId.create "a")
                (KindId.create "task")
                (ContextId.create "b")
                (KindId.create "item")
                MappingType.Subkind
                CongruenceLevel.CL3

        let bridge =
            AlignmentBridge.create "b1" (ContextId.create "a") (ContextId.create "b") [ kb ]

        match AlignmentBridge.tryFindKindBridge (KindId.create "task") bridge with
        | Some found -> Assert.Equal(CongruenceLevel.CL3, found.congruenceLevel)
        | None -> Assert.Fail("Should find kind bridge")

module ``Semantic Registry`` =

    [<Fact>]
    let ``empty registry has no contexts or bridges`` () =
        let reg = SemanticRegistry.empty
        Assert.True(reg.contexts.IsEmpty)
        Assert.True(reg.bridges.IsEmpty)

    [<Fact>]
    let ``registerContext adds context`` () =
        let ctx = AgentContext.create (ContextId.create "test")
        let reg = SemanticRegistry.empty |> SemanticRegistry.registerContext ctx
        Assert.True(reg.contexts.ContainsKey(ContextId.create "test"))

    [<Fact>]
    let ``registerBridge adds bridge`` () =
        let bridge =
            AlignmentBridge.create "b1" (ContextId.create "a") (ContextId.create "b") []

        let reg = SemanticRegistry.empty |> SemanticRegistry.registerBridge bridge
        Assert.True(reg.bridges.ContainsKey("b1"))

    [<Fact>]
    let ``tryFindBridge finds by source and target`` () =
        let bridge =
            AlignmentBridge.create "b1" (ContextId.create "a") (ContextId.create "b") []

        let reg = SemanticRegistry.empty |> SemanticRegistry.registerBridge bridge

        match SemanticRegistry.tryFindBridge (ContextId.create "a") (ContextId.create "b") reg with
        | Some found -> Assert.Equal("b1", found.bridgeId)
        | None -> Assert.Fail("Should find bridge")

    [<Fact>]
    let ``bridgesFrom returns bridges from context`` () =
        let b1 =
            AlignmentBridge.create "b1" (ContextId.create "a") (ContextId.create "b") []

        let b2 =
            AlignmentBridge.create "b2" (ContextId.create "a") (ContextId.create "c") []

        let b3 =
            AlignmentBridge.create "b3" (ContextId.create "b") (ContextId.create "c") []

        let reg =
            SemanticRegistry.empty
            |> SemanticRegistry.registerBridge b1
            |> SemanticRegistry.registerBridge b2
            |> SemanticRegistry.registerBridge b3

        let bridges = SemanticRegistry.bridgesFrom (ContextId.create "a") reg
        Assert.Equal(2, bridges.Length)

module ``Translation`` =

    let setupRegistry () =
        let kb =
            KindBridge.create
                (ContextId.create "src")
                (KindId.create "task")
                (ContextId.create "tgt")
                (KindId.create "item")
                MappingType.Subkind
                CongruenceLevel.CL3
            |> KindBridge.withLossNote "status mapping lossy"

        let bridge =
            AlignmentBridge.create "b1" (ContextId.create "src") (ContextId.create "tgt") [ kb ]

        SemanticRegistry.empty |> SemanticRegistry.registerBridge bridge

    [<Fact>]
    let ``translateKind succeeds with bridge`` () =
        let reg = setupRegistry ()

        match translateKind reg (ContextId.create "src") (ContextId.create "tgt") (KindId.create "task") with
        | Translated(targetKind, cl, lossNotes) ->
            Assert.Equal(KindId.create "item", targetKind)
            Assert.Equal(CongruenceLevel.CL3, cl)
            Assert.Single(lossNotes) |> ignore
        | other -> Assert.Fail(sprintf "Expected Translated, got %A" other)

    [<Fact>]
    let ``translateKind returns NoBridge when missing`` () =
        let reg = SemanticRegistry.empty

        match translateKind reg (ContextId.create "a") (ContextId.create "b") (KindId.create "x") with
        | NoBridge _ -> ()
        | other -> Assert.Fail(sprintf "Expected NoBridge, got %A" other)

    [<Fact>]
    let ``translateKind returns NoKindMapping when kind not mapped`` () =
        let reg = setupRegistry ()

        match translateKind reg (ContextId.create "src") (ContextId.create "tgt") (KindId.create "unknown") with
        | NoKindMapping _ -> ()
        | other -> Assert.Fail(sprintf "Expected NoKindMapping, got %A" other)

    [<Fact>]
    let ``translateKind returns Incompatible for Disjoint`` () =
        let kb =
            KindBridge.create
                (ContextId.create "a")
                (KindId.create "x")
                (ContextId.create "b")
                (KindId.create "y")
                MappingType.Disjoint
                CongruenceLevel.CL0

        let bridge =
            AlignmentBridge.create "b1" (ContextId.create "a") (ContextId.create "b") [ kb ]

        let reg = SemanticRegistry.empty |> SemanticRegistry.registerBridge bridge

        match translateKind reg (ContextId.create "a") (ContextId.create "b") (KindId.create "x") with
        | Incompatible reason -> Assert.Contains("disjoint", reason.ToLower())
        | other -> Assert.Fail(sprintf "Expected Incompatible, got %A" other)

module ``Bridge Penalty`` =

    [<Fact>]
    let ``applyBridgePenalty applies CL penalty to reliability`` () =
        let bridge =
            AlignmentBridge.create "b1" (ContextId.create "a") (ContextId.create "b") []
        // Start with L2 reliability
        let envelope =
            { AssuranceEnvelope.unassured with
                reliability =
                    { Reliability.unsubstantiated with
                        level = AssuranceLevel.L2 } }

        let result = applyBridgePenalty bridge envelope
        // CL0 bridge (empty bridges) drops reliability to L0
        Assert.Equal(AssuranceLevel.L0, result.reliability.level)

module ``Semantic Findings`` =

    [<Fact>]
    let ``finding codes are unique`` () =
        let findings =
            [ SemanticFinding.UndeclaredContext "agent"
              SemanticFinding.UnbridgedCrossReference(ContextId.create "a", ContextId.create "b", "term")
              SemanticFinding.CLInflation("bid", CongruenceLevel.CL4, CongruenceLevel.CL2)
              SemanticFinding.SemanticDrift(ContextId.create "ctx", 0.5)
              SemanticFinding.TermCollision("term", ContextId.create "a", ContextId.create "b")
              SemanticFinding.InvalidKindBridge("bid", "reason") ]

        let codes = findings |> List.map SemanticFinding.code
        Assert.Equal(codes.Length, codes |> List.distinct |> List.length)

    [<Fact>]
    let ``describe produces human-readable messages`` () =
        let finding = SemanticFinding.UndeclaredContext "agent-1"
        let desc = SemanticFinding.describe finding
        Assert.Contains("agent-1", desc)
        Assert.Contains("INV-SEM-01", desc)

module ``Semantic Drift`` =

    [<Fact>]
    let ``computeDrift returns 0 for identical contexts`` () =
        let ctx = AgentContext.create (ContextId.create "test")
        let drift = computeDrift ctx ctx
        Assert.Equal(0.0, drift)

    [<Fact>]
    let ``computeDrift detects added kinds`` () =
        let old = AgentContext.create (ContextId.create "test")

        let new' =
            old
            |> AgentContext.addKind (
                KindSignature.create
                    (KindId.create "task")
                    "Task"
                    (ContextId.create "test")
                    (IntensionSpec.informal "Work")
            )

        let drift = computeDrift old new'
        Assert.True(drift > 0.0)

    [<Fact>]
    let ``computeDrift detects removed kinds`` () =
        let old =
            AgentContext.create (ContextId.create "test")
            |> AgentContext.addKind (
                KindSignature.create
                    (KindId.create "task")
                    "Task"
                    (ContextId.create "test")
                    (IntensionSpec.informal "Work")
            )

        let new' = AgentContext.create (ContextId.create "test")
        let drift = computeDrift old new'
        Assert.True(drift > 0.0)

    [<Fact>]
    let ``computeDrift detects changed definitions`` () =
        let kindId = KindId.create "task"

        let old =
            AgentContext.create (ContextId.create "test")
            |> AgentContext.addKind (
                KindSignature.create kindId "Task" (ContextId.create "test") (IntensionSpec.informal "Work item")
            )

        let new' =
            AgentContext.create (ContextId.create "test")
            |> AgentContext.addKind (
                KindSignature.create
                    kindId
                    "Task"
                    (ContextId.create "test")
                    (IntensionSpec.informal "Changed definition")
            )

        let drift = computeDrift old new'
        Assert.True(drift > 0.0)

    [<Fact>]
    let ``detectDrift returns None when below threshold`` () =
        let ctx = AgentContext.create (ContextId.create "test")
        let result = detectDrift 0.5 ctx ctx
        Assert.Equal(None, result)

    [<Fact>]
    let ``detectDrift returns finding when above threshold`` () =
        let old = AgentContext.create (ContextId.create "test")

        let new' =
            old
            |> AgentContext.addKind (
                KindSignature.create (KindId.create "a") "A" (ContextId.create "test") (IntensionSpec.informal "A")
            )
            |> AgentContext.addKind (
                KindSignature.create (KindId.create "b") "B" (ContextId.create "test") (IntensionSpec.informal "B")
            )
        // With empty old context, adding 2 kinds = 200% drift
        match detectDrift 0.1 old new' with
        | Some(SemanticFinding.SemanticDrift _) -> ()
        | other -> Assert.Fail(sprintf "Expected SemanticDrift finding, got %A" other)
