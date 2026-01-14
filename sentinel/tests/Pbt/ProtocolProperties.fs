namespace Acp.Tests.Pbt

open Xunit
open FsCheck

module P = FsCheck.FSharp.Prop

open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Messaging
open Acp.Protocol
open Acp.Validation

open Acp.Tests.Pbt.Generators

module ProtocolProperties =

    let private sid = SessionId "pbt-protocol"
    let private config = Generators.PbtConfig.config

    [<Fact>]
    let ``Protocol.spec is deterministic over traces`` () =
        let prop =
            P.forAll (Generators.arbTrace) (fun msgs ->
                let r1 = Spec.run spec msgs
                let r2 = Spec.run spec msgs
                r1 = r2)

        Check.One(config, prop)

    [<Fact>]
    let ``Protocol errors map to Protocol lane + Error severity`` () =
        let prop =
            P.forAll (Generators.arbTrace) (fun msgs ->
                let r = runWithValidation sid spec msgs false None None

                r.findings
                |> List.filter (fun f -> f.lane = Lane.Protocol)
                |> List.forall (fun f -> f.severity = Severity.Error && f.failure.IsSome))

        Check.One(config, prop)

    [<Fact>]
    let ``Valid traces always yield Ok phase and no Protocol/Session findings`` () =
        let prop =
            P.forAll (Generators.arbValidTrace) (fun msgs ->
                let r = runWithValidation sid spec msgs false None None

                match r.finalPhase with
                | Ok _ ->
                    r.findings
                    |> List.forall (fun f -> f.lane <> Lane.Protocol && f.lane <> Lane.Session)
                | Error _ -> false)

        Check.One(config, prop)

    [<Fact>]
    let ``Protocol error mapping is canonical (code/subject/index)`` () =
        let findFirstError (msgs: Message list) =
            let rec loop idx phase remaining =
                match remaining with
                | [] -> None
                | msg :: rest ->
                    match spec.step phase msg with
                    | Ok phase' -> loop (idx + 1) phase' rest
                    | Error e -> Some(idx, msg, e, phase)

            loop 0 spec.initial msgs

        let prop =
            P.forAll (Generators.arbTrace) (fun msgs ->
                let expected = findFirstError msgs
                let r = runWithValidation sid spec msgs false None None

                match expected with
                | None -> r.findings |> List.exists (fun f -> f.lane = Lane.Protocol) |> not
                | Some(idx, msg, e, phaseBefore) ->
                    let ctxOpt =
                        match phaseBefore with
                        | Phase.Ready ctx -> Some ctx
                        | _ -> None

                    let expectedFinding = FromProtocol.ofProtocolError ctxOpt msg e (Some idx)
                    r.findings |> List.exists ((=) expectedFinding))

        Check.One(config, prop)
