namespace Acp.Tests.Pbt

open Xunit
open FsCheck

module P = FsCheck.FSharp.Prop

open Acp.Domain.PrimitivesAndParties
open Acp.Protocol
open Acp.Validation

open Acp.Tests.Pbt.Generators

module TraceReplay =

    let private sid = SessionId "pbt-trace"
    let private config = Generators.PbtConfig.config

    [<Fact>]
    let ``Trace replay is deterministic (TR-1)`` () =
        let prop =
            P.forAll (Generators.arbTrace) (fun msgs ->
                let r1 = runWithValidation sid spec msgs false None None
                let r2 = runWithValidation sid spec msgs false None None
                r1 = r2)

        Check.One(config, prop)

    [<Fact>]
    let ``Findings are localized to trace indices when present (TR-2)`` () =
        let prop =
            P.forAll (Generators.arbTrace) (fun msgs ->
                let r = runWithValidation sid spec msgs false None None
                let traceMsgs = r.trace.messages

                r.findings
                |> List.forall (fun f ->
                    match f.traceIndex with
                    | None -> true
                    | Some i ->
                        i >= 0
                        && i < traceMsgs.Length
                        && match f.subject with
                           | Subject.MessageAt(j, m) -> j = i && m = traceMsgs[i]
                           | _ -> true))

        Check.One(config, prop)
