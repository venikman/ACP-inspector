namespace Acp.Tests.Pbt

open Xunit
open FsCheck

module G = FsCheck.FSharp.Gen
module A = FsCheck.FSharp.Arb
module P = FsCheck.FSharp.Prop

open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Metadata
open Acp.Domain.Messaging
open Acp.RuntimeAdapter
open Acp.Validation

open Acp.Tests.Pbt.Generators

module RuntimeAdapterProperties =

    let private sid = SessionId "pbt-adapter"
    let private config = Generators.PbtConfig.config

    let private mkProfileWithLimit limit : RuntimeProfile =
        { metadata = MetadataPolicy.AllowOpaque
          transport =
            Some
                { lineSeparator = None
                  maxFrameBytes = None
                  maxMessageBytes = Some limit
                  metaEnvelope = None } }

    let private hasSizeFinding findings =
        findings
        |> List.exists (fun f ->
            f.lane = Lane.Transport
            && match f.failure with
               | Some failure -> failure.code = "ACP.TRANSPORT.MAX_MESSAGE_BYTES_EXCEEDED"
               | None -> false)

    [<Fact>]
    let ``validateInbound size checks match maxMessageBytes`` () =
        let limit = 10
        let profile = Some(mkProfileWithLimit limit)

        let gen = G.zip Generators.genMessageAny (G.choose (0, limit * 3))

        let prop =
            P.forAll (A.fromGen gen) (fun (msg: Message, n: int) ->
                let frame: InboundFrame =
                    { rawByteLength = Some n
                      message = msg }

                let r = validateInbound sid profile frame false
                hasSizeFinding r.findings = (n > limit))

        Check.One(config, prop)

    [<Fact>]
    let ``validateOutbound size checks match maxMessageBytes`` () =
        let limit = 10
        let profile = Some(mkProfileWithLimit limit)

        let gen = G.zip Generators.genMessageAny (G.choose (0, limit * 3))

        let prop =
            P.forAll (A.fromGen gen) (fun (msg: Message, n: int) ->
                let frame: OutboundFrame =
                    { rawByteLength = Some n
                      message = msg }

                let r = validateOutbound sid profile frame false
                hasSizeFinding r.findings = (n > limit))

        Check.One(config, prop)
