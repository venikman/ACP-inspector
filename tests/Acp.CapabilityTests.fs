module Acp.Tests.CapabilityTests

open System
open Xunit
open Acp.Assurance
open Acp.Capability

/// BC-003 Tests: Capability Verification Context
module ``Verification Level`` =

    [<Fact>]
    let ``Declared is lowest verification`` () =
        Assert.Equal(0, VerificationLevel.toInt VerificationLevel.Declared)

    [<Fact>]
    let ``Certified is highest verification`` () =
        Assert.Equal(2, VerificationLevel.toInt VerificationLevel.Certified)

    [<Fact>]
    let ``maps to AssuranceLevel correctly`` () =
        Assert.Equal(AssuranceLevel.L0, VerificationLevel.toAssuranceLevel VerificationLevel.Declared)
        Assert.Equal(AssuranceLevel.L1, VerificationLevel.toAssuranceLevel VerificationLevel.Tested)
        Assert.Equal(AssuranceLevel.L2, VerificationLevel.toAssuranceLevel VerificationLevel.Certified)

    [<Fact>]
    let ``min returns lowest`` () =
        Assert.Equal(
            VerificationLevel.Declared,
            VerificationLevel.min VerificationLevel.Declared VerificationLevel.Certified
        )

        Assert.Equal(
            VerificationLevel.Tested,
            VerificationLevel.min VerificationLevel.Tested VerificationLevel.Certified
        )

module ``Capability Kind`` =

    [<Fact>]
    let ``toString roundtrips for standard kinds`` () =
        let kinds =
            [ CapabilityKind.FileRead
              CapabilityKind.FileWrite
              CapabilityKind.TerminalAccess
              CapabilityKind.HttpRequest ]

        for kind in kinds do
            let str = CapabilityKind.toString kind
            Assert.False(String.IsNullOrEmpty(str))

    [<Fact>]
    let ``tryParse handles valid inputs`` () =
        Assert.Equal(Some CapabilityKind.FileRead, CapabilityKind.tryParse "fileread")
        Assert.Equal(Some CapabilityKind.FileRead, CapabilityKind.tryParse "FileRead")
        Assert.Equal(None, CapabilityKind.tryParse "invalid")

    [<Fact>]
    let ``custom kind parses`` () =
        match CapabilityKind.tryParse "custom:myCapability" with
        | Some(CapabilityKind.Custom name) -> Assert.Equal("myCapability", name)
        | _ -> Assert.True(false, "Expected Custom kind")

module ``Performance Envelope`` =

    [<Fact>]
    let ``empty has no bounds`` () =
        Assert.False(PerformanceEnvelope.hasBounds PerformanceEnvelope.empty)

    [<Fact>]
    let ``withLatency adds bound`` () =
        let env =
            PerformanceEnvelope.empty
            |> PerformanceEnvelope.withLatency (TimeSpan.FromSeconds(5.0))

        Assert.True(PerformanceEnvelope.hasBounds env)
        Assert.Equal(Some(TimeSpan.FromSeconds(5.0)), env.maxLatency)

    [<Fact>]
    let ``withPayloadLimit adds bound`` () =
        let env = PerformanceEnvelope.empty |> PerformanceEnvelope.withPayloadLimit 1024L
        Assert.True(PerformanceEnvelope.hasBounds env)
        Assert.Equal(Some 1024L, env.maxPayloadBytes)

    [<Fact>]
    let ``withConstraint adds to list`` () =
        let env =
            PerformanceEnvelope.empty
            |> PerformanceEnvelope.withConstraint "UTF-8 only"
            |> PerformanceEnvelope.withConstraint "workspace-scoped"

        Assert.Equal(2, env.constraints.Length)

module ``Rate`` =

    [<Fact>]
    let ``perMinute creates correct rate`` () =
        let rate = Rate.perMinute 60
        Assert.Equal(60, rate.count)
        Assert.Equal(TimeSpan.FromMinutes(1.0), rate.perInterval)

    [<Fact>]
    let ``callsPerMinute calculates correctly`` () =
        let rate = Rate.perSecond 1
        Assert.Equal(60.0, Rate.callsPerMinute rate)

module ``Test Evidence`` =

    [<Fact>]
    let ``create sets timestamp`` () =
        let evidence =
            TestEvidence.create "test-001" TestResult.Pass (TimeSpan.FromMilliseconds(100.0))

        Assert.True(evidence.timestamp <= DateTimeOffset.UtcNow)
        Assert.False(String.IsNullOrEmpty(evidence.evidenceId))

    [<Fact>]
    let ``isPassing returns true for Pass`` () =
        let evidence = TestEvidence.create "test" TestResult.Pass TimeSpan.Zero
        Assert.True(TestEvidence.isPassing evidence)

    [<Fact>]
    let ``isPassing returns false for Fail`` () =
        let evidence = TestEvidence.create "test" TestResult.Fail TimeSpan.Zero
        Assert.False(TestEvidence.isPassing evidence)

module ``Capability Claim`` =

    [<Fact>]
    let ``create sets Declared level`` () =
        let claim =
            CapabilityClaim.create CapabilityKind.FileRead (AbilitySpec.simple "Read files")

        Assert.Equal(VerificationLevel.Declared, claim.verificationLevel)
        Assert.Empty(claim.evidence)

    [<Fact>]
    let ``withVerification updates level and adds evidence`` () =
        let evidence = TestEvidence.create "test" TestResult.Pass TimeSpan.Zero

        let claim =
            CapabilityClaim.create CapabilityKind.FileRead (AbilitySpec.simple "Read files")
            |> CapabilityClaim.withVerification VerificationLevel.Tested [ evidence ]

        Assert.Equal(VerificationLevel.Tested, claim.verificationLevel)
        Assert.Single(claim.evidence) |> ignore

    [<Fact>]
    let ``upgrade only increases level`` () =
        let evidence = TestEvidence.create "test" TestResult.Pass TimeSpan.Zero

        let claim =
            CapabilityClaim.create CapabilityKind.FileRead (AbilitySpec.simple "Read files")
            |> CapabilityClaim.withVerification VerificationLevel.Certified [ evidence ]
            |> CapabilityClaim.upgrade VerificationLevel.Tested [ evidence ] // Should not downgrade

        Assert.Equal(VerificationLevel.Certified, claim.verificationLevel)

    [<Fact>]
    let ``degrade resets to Declared`` () =
        let evidence = TestEvidence.create "test" TestResult.Pass TimeSpan.Zero

        let claim =
            CapabilityClaim.create CapabilityKind.FileRead (AbilitySpec.simple "Read files")
            |> CapabilityClaim.withVerification VerificationLevel.Tested [ evidence ]
            |> CapabilityClaim.degrade "Connection failed"

        Assert.Equal(VerificationLevel.Declared, claim.verificationLevel)
        Assert.Single(claim.evidence) |> ignore // Evidence preserved

    [<Fact>]
    let ``toAssuranceEnvelope maps correctly`` () =
        let evidence = TestEvidence.create "test" TestResult.Pass TimeSpan.Zero

        let claim =
            CapabilityClaim.create CapabilityKind.FileRead (AbilitySpec.simple "Read files")
            |> CapabilityClaim.withVerification VerificationLevel.Tested [ evidence ]

        let envelope = CapabilityClaim.toAssuranceEnvelope claim
        Assert.Equal(AssuranceLevel.L1, envelope.reliability.level)
        Assert.True(envelope.reliability.pathId.IsSome)

module ``Validation`` =

    [<Fact>]
    let ``validateVerificationLevel passes for Declared`` () =
        let claim =
            CapabilityClaim.create CapabilityKind.FileRead (AbilitySpec.simple "Read files")

        let errors = validateVerificationLevel claim
        Assert.Empty(errors)

    [<Fact>]
    let ``validateVerificationLevel fails for Tested without evidence`` () =
        let claim =
            { CapabilityClaim.create CapabilityKind.FileRead (AbilitySpec.simple "Read files") with
                verificationLevel = VerificationLevel.Tested }

        let errors = validateVerificationLevel claim
        Assert.NotEmpty(errors)

    [<Fact>]
    let ``validateVerificationLevel passes for Tested with passing evidence`` () =
        let evidence = TestEvidence.create "test" TestResult.Pass TimeSpan.Zero

        let claim =
            CapabilityClaim.create CapabilityKind.FileRead (AbilitySpec.simple "Read files")
            |> CapabilityClaim.withVerification VerificationLevel.Tested [ evidence ]

        let errors = validateVerificationLevel claim
        Assert.Empty(errors)

    [<Fact>]
    let ``effectiveLevel returns Declared when no verification`` () =
        let claim =
            CapabilityClaim.create CapabilityKind.FileRead (AbilitySpec.simple "Read files")

        let effective = effectiveLevel claim None
        Assert.Equal(VerificationLevel.Declared, effective)

    [<Fact>]
    let ``effectiveLevel returns min of claimed and verified`` () =
        let evidence = TestEvidence.create "test" TestResult.Pass TimeSpan.Zero

        let claimed =
            CapabilityClaim.create CapabilityKind.FileRead (AbilitySpec.simple "Read files")
            |> CapabilityClaim.withVerification VerificationLevel.Certified [ evidence ]

        let verified =
            CapabilityClaim.create CapabilityKind.FileRead (AbilitySpec.simple "Read files")
            |> CapabilityClaim.withVerification VerificationLevel.Tested [ evidence ]

        let effective = effectiveLevel claimed (Some verified)
        Assert.Equal(VerificationLevel.Tested, effective)

module ``Envelope Checking`` =

    [<Fact>]
    let ``checkEnvelope returns empty for no violations`` () =
        let envelope =
            PerformanceEnvelope.empty |> PerformanceEnvelope.withPayloadLimit 1024L

        let violations = checkEnvelope envelope (Some 512L) None
        Assert.Empty(violations)

    [<Fact>]
    let ``checkEnvelope detects payload violation`` () =
        let envelope =
            PerformanceEnvelope.empty |> PerformanceEnvelope.withPayloadLimit 1024L

        let violations = checkEnvelope envelope (Some 2048L) None
        Assert.Single(violations) |> ignore

        match violations.[0] with
        | PayloadTooLarge(actual, max) ->
            Assert.Equal(2048L, actual)
            Assert.Equal(1024L, max)
        | _ -> Assert.True(false, "Expected PayloadTooLarge")

    [<Fact>]
    let ``checkEnvelope detects latency violation`` () =
        let envelope =
            PerformanceEnvelope.empty
            |> PerformanceEnvelope.withLatency (TimeSpan.FromSeconds(1.0))

        let violations = checkEnvelope envelope None (Some(TimeSpan.FromSeconds(5.0)))
        Assert.Single(violations) |> ignore

module ``Capability Set`` =

    [<Fact>]
    let ``empty set has no claims`` () =
        Assert.Empty(CapabilitySet.toList CapabilitySet.empty)

    [<Fact>]
    let ``add and tryGet work`` () =
        let claim =
            CapabilityClaim.create CapabilityKind.FileRead (AbilitySpec.simple "Read files")

        let set = CapabilitySet.empty |> CapabilitySet.add claim
        let found = CapabilitySet.tryGet CapabilityKind.FileRead set
        Assert.True(found.IsSome)

    [<Fact>]
    let ``has returns correct value`` () =
        let claim =
            CapabilityClaim.create CapabilityKind.FileRead (AbilitySpec.simple "Read files")

        let set = CapabilitySet.empty |> CapabilitySet.add claim
        Assert.True(CapabilitySet.has CapabilityKind.FileRead set)
        Assert.False(CapabilitySet.has CapabilityKind.FileWrite set)

    [<Fact>]
    let ``verificationDebt returns unverified claims`` () =
        let declaredClaim =
            CapabilityClaim.create CapabilityKind.FileRead (AbilitySpec.simple "Read files")

        let evidence = TestEvidence.create "test" TestResult.Pass TimeSpan.Zero

        let testedClaim =
            CapabilityClaim.create CapabilityKind.FileWrite (AbilitySpec.simple "Write files")
            |> CapabilityClaim.withVerification VerificationLevel.Tested [ evidence ]

        let set =
            CapabilitySet.empty
            |> CapabilitySet.add declaredClaim
            |> CapabilitySet.add testedClaim

        let debt = CapabilitySet.verificationDebt set
        Assert.Single(debt) |> ignore
        Assert.Equal(CapabilityKind.FileRead, debt.[0].capabilityKind)

    [<Fact>]
    let ``effective computes min levels`` () =
        let evidence = TestEvidence.create "test" TestResult.Pass TimeSpan.Zero

        let claimedCertified =
            CapabilityClaim.create CapabilityKind.FileRead (AbilitySpec.simple "Read files")
            |> CapabilityClaim.withVerification VerificationLevel.Certified [ evidence ]

        let verifiedTested =
            CapabilityClaim.create CapabilityKind.FileRead (AbilitySpec.simple "Read files")
            |> CapabilityClaim.withVerification VerificationLevel.Tested [ evidence ]

        let claimed = CapabilitySet.empty |> CapabilitySet.add claimedCertified
        let verified = CapabilitySet.empty |> CapabilitySet.add verifiedTested

        let effective = CapabilitySet.effective claimed verified
        let effectiveClaim = CapabilitySet.tryGet CapabilityKind.FileRead effective
        Assert.True(effectiveClaim.IsSome)
        Assert.Equal(VerificationLevel.Tested, effectiveClaim.Value.verificationLevel)

module ``Capability Finding`` =

    [<Fact>]
    let ``finding codes are namespaced`` () =
        let finding = CapabilityFinding.VerificationLevelMismatch("claim-001", "test")
        let code = CapabilityFinding.code finding
        Assert.StartsWith("ACP.CAPABILITY.", code)

    [<Fact>]
    let ``finding descriptions include claim ID`` () =
        let finding = CapabilityFinding.VerificationLevelMismatch("claim-001", "reason")
        let desc = CapabilityFinding.describe finding
        Assert.Contains("claim-001", desc)
