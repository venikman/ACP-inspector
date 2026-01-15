module Acp.Tests.AssuranceTests

open System
open Xunit
open Acp.Assurance

/// BC-001 Tests: Assurance Context
module ``Assurance Level`` =

    [<Fact>]
    let ``L0 is lowest assurance`` () =
        Assert.Equal(0, AssuranceLevel.toInt AssuranceLevel.L0)

    [<Fact>]
    let ``L2 is highest assurance`` () =
        Assert.Equal(2, AssuranceLevel.toInt AssuranceLevel.L2)

    [<Fact>]
    let ``min returns lowest level`` () =
        Assert.Equal(AssuranceLevel.L0, AssuranceLevel.min AssuranceLevel.L0 AssuranceLevel.L2)
        Assert.Equal(AssuranceLevel.L1, AssuranceLevel.min AssuranceLevel.L1 AssuranceLevel.L2)
        Assert.Equal(AssuranceLevel.L1, AssuranceLevel.min AssuranceLevel.L2 AssuranceLevel.L1)

    [<Fact>]
    let ``tryParse handles valid inputs`` () =
        Assert.Equal(Some AssuranceLevel.L0, AssuranceLevel.tryParse "L0")
        Assert.Equal(Some AssuranceLevel.L1, AssuranceLevel.tryParse "l1")
        Assert.Equal(Some AssuranceLevel.L2, AssuranceLevel.tryParse "EVIDENCED")
        Assert.Equal(None, AssuranceLevel.tryParse "invalid")

module ``Formality Level`` =

    [<Fact>]
    let ``F0 to F9 roundtrip via int`` () =
        for i in 0..9 do
            let f = Formality.fromInt i
            Assert.Equal(i, Formality.toInt f)

    [<Fact>]
    let ``tryFromInt returns None for invalid`` () =
        Assert.Equal(None, Formality.tryFromInt -1)
        Assert.Equal(None, Formality.tryFromInt 10)
        Assert.Equal(Some Formality.F5, Formality.tryFromInt 5)

module ``Congruence Level`` =

    [<Fact>]
    let ``CL4 has no penalty`` () =
        Assert.Equal(1.0, CongruenceLevel.penalty CongruenceLevel.CL4)

    [<Fact>]
    let ``CL0 has full penalty`` () =
        Assert.Equal(0.0, CongruenceLevel.penalty CongruenceLevel.CL0)

    [<Fact>]
    let ``penalties decrease with CL`` () =
        let p0 = CongruenceLevel.penalty CongruenceLevel.CL0
        let p1 = CongruenceLevel.penalty CongruenceLevel.CL1
        let p2 = CongruenceLevel.penalty CongruenceLevel.CL2
        let p3 = CongruenceLevel.penalty CongruenceLevel.CL3
        let p4 = CongruenceLevel.penalty CongruenceLevel.CL4
        Assert.True(p0 < p1)
        Assert.True(p1 < p2)
        Assert.True(p2 < p3)
        Assert.True(p3 <= p4)

module ``Claim Scope`` =

    [<Fact>]
    let ``empty scope is empty`` () = Assert.True(ClaimScope.Empty.IsEmpty)

    [<Fact>]
    let ``single scope is not empty`` () =
        let scope = ClaimScope.Single(ContextSlice.create "test")
        Assert.False(scope.IsEmpty)

    [<Fact>]
    let ``contains works correctly`` () =
        let slice1 = ContextSlice.create "ctx1"
        let slice2 = ContextSlice.create "ctx2"
        let scope = ClaimScope.Of([ slice1 ])
        Assert.True(scope.Contains(slice1))
        Assert.False(scope.Contains(slice2))

module ``Reliability`` =

    [<Fact>]
    let ``unsubstantiated has L0`` () =
        let r = Reliability.unsubstantiated
        Assert.Equal(AssuranceLevel.L0, r.level)
        Assert.Equal(None, r.pathId)

    [<Fact>]
    let ``circumstantial has L1`` () =
        let pathId = PathId.create "test-path"
        let r = Reliability.circumstantial pathId
        Assert.Equal(AssuranceLevel.L1, r.level)
        Assert.Equal(Some pathId, r.pathId)

    [<Fact>]
    let ``evidenced has L2`` () =
        let pathId = PathId.create "test-path"
        let decay = TimeSpan.FromHours(1.0)
        let r = Reliability.evidenced pathId decay
        Assert.Equal(AssuranceLevel.L2, r.level)
        Assert.Equal(Some pathId, r.pathId)
        Assert.Equal(Some decay, r.decay)

    [<Fact>]
    let ``isFresh returns true when no decay`` () =
        let now = DateTimeOffset.Parse("2026-01-01T00:00:00Z")
        let r = Reliability.unsubstantiated
        Assert.True(Reliability.isFresh now r)

    [<Fact>]
    let ``isFresh returns false when stale`` () =
        let now = DateTimeOffset.Parse("2026-01-01T00:00:00Z")

        let r =
            { level = AssuranceLevel.L1
              pathId = Some(PathId.create "old")
              decay = Some(TimeSpan.FromMinutes(1.0))
              timestamp = Some(now.AddHours(-1.0)) }

        Assert.False(Reliability.isFresh now r)

    [<Fact>]
    let ``status returns Fresh when within window`` () =
        let now = DateTimeOffset.UtcNow

        let r =
            { level = AssuranceLevel.L2
              pathId = Some(PathId.create "recent")
              decay = Some(TimeSpan.FromHours(2.0))
              timestamp = Some(now.AddMinutes(-30.0)) }

        Assert.Equal(EvidenceStatus.Fresh, Reliability.status now r)

    [<Fact>]
    let ``status returns Stale when outside window`` () =
        let now = DateTimeOffset.UtcNow

        let r =
            { level = AssuranceLevel.L2
              pathId = Some(PathId.create "old")
              decay = Some(TimeSpan.FromMinutes(10.0))
              timestamp = Some(now.AddHours(-1.0)) }

        Assert.Equal(EvidenceStatus.Stale, Reliability.status now r)

    [<Fact>]
    let ``status returns Fresh when no decay specified`` () =
        let now = DateTimeOffset.UtcNow

        let r =
            { level = AssuranceLevel.L1
              pathId = Some(PathId.create "nodecay")
              decay = None
              timestamp = Some(now.AddYears(-10)) }

        Assert.Equal(EvidenceStatus.Fresh, Reliability.status now r)

    [<Fact>]
    let ``status returns Stale when decay specified but no timestamp`` () =
        let now = DateTimeOffset.UtcNow

        let r =
            { level = AssuranceLevel.L1
              pathId = Some(PathId.create "notime")
              decay = Some(TimeSpan.FromHours(1.0))
              timestamp = None }

        Assert.Equal(EvidenceStatus.Stale, Reliability.status now r)

    [<Fact>]
    let ``isStale returns true when evidence expired`` () =
        let now = DateTimeOffset.UtcNow

        let r =
            { level = AssuranceLevel.L2
              pathId = Some(PathId.create "expired")
              decay = Some(TimeSpan.FromSeconds(1.0))
              timestamp = Some(now.AddSeconds(-10.0)) }

        Assert.True(Reliability.isStale now r)

    [<Fact>]
    let ``isStale returns false when evidence fresh`` () =
        let now = DateTimeOffset.UtcNow

        let r =
            { level = AssuranceLevel.L2
              pathId = Some(PathId.create "fresh")
              decay = Some(TimeSpan.FromHours(1.0))
              timestamp = Some(now.AddMinutes(-10.0)) }

        Assert.False(Reliability.isStale now r)

    [<Fact>]
    let ``evidence at exact boundary is considered fresh`` () =
        let now = DateTimeOffset.UtcNow
        let decay = TimeSpan.FromHours(1.0)

        let r =
            { level = AssuranceLevel.L2
              pathId = Some(PathId.create "boundary")
              decay = Some decay
              timestamp = Some(now.Add(-decay)) }

        // At exact boundary, should be fresh (<=)
        Assert.Equal(EvidenceStatus.Fresh, Reliability.status now r)

module ``Assurance Envelope`` =

    [<Fact>]
    let ``unassured envelope has L0 and no scope`` () =
        let env = AssuranceEnvelope.unassured
        Assert.Equal(Formality.F0, env.formality)
        Assert.True(env.scope.IsEmpty)
        Assert.Equal(AssuranceLevel.L0, env.reliability.level)

    [<Fact>]
    let ``hasScope returns false for empty`` () =
        Assert.False(AssuranceEnvelope.hasScope AssuranceEnvelope.unassured)

    [<Fact>]
    let ``hasScope returns true for non-empty`` () =
        let env =
            { AssuranceEnvelope.unassured with
                scope = ClaimScope.Single(ContextSlice.create "test") }

        Assert.True(AssuranceEnvelope.hasScope env)

    [<Fact>]
    let ``hasRequiredGrounding returns true for L0 and L1`` () =
        let env0 = AssuranceEnvelope.unassured

        let env1 =
            { env0 with
                reliability =
                    { env0.reliability with
                        level = AssuranceLevel.L1 } }

        Assert.True(AssuranceEnvelope.hasRequiredGrounding env0)
        Assert.True(AssuranceEnvelope.hasRequiredGrounding env1)

    [<Fact>]
    let ``hasRequiredGrounding returns false for L2 without grounding`` () =
        let env =
            { AssuranceEnvelope.unassured with
                reliability =
                    { Reliability.unsubstantiated with
                        level = AssuranceLevel.L2 } }

        Assert.False(AssuranceEnvelope.hasRequiredGrounding env)

    [<Fact>]
    let ``hasRequiredGrounding returns true for L2 with grounding`` () =
        let env =
            { AssuranceEnvelope.unassured with
                reliability =
                    { Reliability.unsubstantiated with
                        level = AssuranceLevel.L2 }
                groundingRef = Some(GroundingRef.create "file:///test.txt") }

        Assert.True(AssuranceEnvelope.hasRequiredGrounding env)

    [<Fact>]
    let ``validate catches unbounded scope`` () =
        let errors = AssuranceEnvelope.validate AssuranceEnvelope.unassured
        Assert.Contains("INV-ASR-01", errors |> List.head)

    [<Fact>]
    let ``validate catches ungrounded L2`` () =
        let env =
            { formality = Formality.F3
              scope = ClaimScope.Single(ContextSlice.create "test")
              reliability =
                { Reliability.unsubstantiated with
                    level = AssuranceLevel.L2 }
              groundingRef = None }

        let errors = AssuranceEnvelope.validate env
        Assert.True(errors |> List.exists (fun e -> e.Contains("INV-ASR-05")))

module ``Aggregation`` =

    [<Fact>]
    let ``aggregateAssurance returns L0 for empty`` () =
        Assert.Equal(AssuranceLevel.L0, aggregateAssurance [])

    [<Fact>]
    let ``aggregateAssurance returns weakest`` () =
        let levels = [ AssuranceLevel.L2; AssuranceLevel.L1; AssuranceLevel.L2 ]
        Assert.Equal(AssuranceLevel.L1, aggregateAssurance levels)

    [<Fact>]
    let ``aggregateReliability returns unsubstantiated for empty`` () =
        let r = aggregateReliability []
        Assert.Equal(AssuranceLevel.L0, r.level)

    [<Fact>]
    let ``aggregateReliability uses weakest link`` () =
        let r1 =
            { Reliability.unsubstantiated with
                level = AssuranceLevel.L2 }

        let r2 =
            { Reliability.unsubstantiated with
                level = AssuranceLevel.L1 }

        let result = aggregateReliability [ r1; r2 ]
        Assert.Equal(AssuranceLevel.L1, result.level)

module ``Bridge Crossing`` =

    [<Fact>]
    let ``crossBridge CL4 preserves level`` () =
        let env =
            { AssuranceEnvelope.unassured with
                reliability =
                    { Reliability.unsubstantiated with
                        level = AssuranceLevel.L2 } }

        let crossed = crossBridge CongruenceLevel.CL4 env
        Assert.Equal(AssuranceLevel.L2, crossed.reliability.level)

    [<Fact>]
    let ``crossBridge CL0 drops to L0`` () =
        let env =
            { AssuranceEnvelope.unassured with
                reliability =
                    { Reliability.unsubstantiated with
                        level = AssuranceLevel.L2 } }

        let crossed = crossBridge CongruenceLevel.CL0 env
        Assert.Equal(AssuranceLevel.L0, crossed.reliability.level)

    [<Fact>]
    let ``crossBridge CL1 degrades L2 to L0`` () =
        // CL1 has 60% loss (0.4 penalty factor) - too lossy to preserve any assurance
        let env =
            { AssuranceEnvelope.unassured with
                reliability =
                    { Reliability.unsubstantiated with
                        level = AssuranceLevel.L2 } }

        let crossed = crossBridge CongruenceLevel.CL1 env
        Assert.Equal(AssuranceLevel.L0, crossed.reliability.level)

    [<Fact>]
    let ``crossBridge CL2 degrades L2 to L1`` () =
        // CL2 has 30% loss (0.7 penalty factor) - L2 becomes L1
        let env =
            { AssuranceEnvelope.unassured with
                reliability =
                    { Reliability.unsubstantiated with
                        level = AssuranceLevel.L2 } }

        let crossed = crossBridge CongruenceLevel.CL2 env
        Assert.Equal(AssuranceLevel.L1, crossed.reliability.level)

    [<Fact>]
    let ``crossBridge CL3 preserves L2`` () =
        // CL3 has 10% loss (0.9 penalty factor) - minor penalty, preserve discrete level
        let env =
            { AssuranceEnvelope.unassured with
                reliability =
                    { Reliability.unsubstantiated with
                        level = AssuranceLevel.L2 } }

        let crossed = crossBridge CongruenceLevel.CL3 env
        Assert.Equal(AssuranceLevel.L2, crossed.reliability.level)

module ``Assurance Finding`` =

    [<Fact>]
    let ``finding codes are namespaced`` () =
        let finding = AssuranceFinding.UnboundedScope "msg-001"
        let code = AssuranceFinding.code finding
        Assert.StartsWith("ACP.ASSURANCE.", code)

    [<Fact>]
    let ``finding descriptions include message ID`` () =
        let finding = AssuranceFinding.UnboundedScope "msg-001"
        let desc = AssuranceFinding.describe finding
        Assert.Contains("msg-001", desc)
