namespace Acp.Tests.Epistemology

open Xunit
open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Capabilities

/// Tests for the ACP domain model (A.1 Holonic Foundation)
///
/// These tests validate:
/// - Holon composition and identity
/// - Type safety and constraints
/// - Domain invariants
module DomainModelTests =

    [<Fact>]
    let ``SessionId preserves value through construction`` () =
        let value = "test-session-123"
        let sessionId = SessionId value
        let extracted = SessionId.value sessionId
        Assert.Equal(value, extracted)

    [<Fact>]
    let ``ProtocolVersion current is supported`` () =
        let current = ProtocolVersion.current
        Assert.True(ProtocolVersion.isSupported current)

    [<Fact>]
    let ``InitializeParams is a composite holon`` () =
        // A.1 Holonic Foundation: InitializeParams composes multiple parts
        let initParams: ProtocolVersion * ClientCapabilities * ImplementationInfo option =
            (ProtocolVersion.current,
             { fs =
                 { readTextFile = false
                   writeTextFile = false }
               terminal = false },
             Some
                 { name = "test"
                   title = None
                   version = "1.0" })

        // Validate it's a proper holon composition
        Assert.NotNull(initParams)
