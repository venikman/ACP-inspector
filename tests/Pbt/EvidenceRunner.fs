namespace Acp.Tests.Pbt

open System
open System.IO
open System.Text.Json
open FsCheck

/// FsCheck runner that persists the latest failing counterexample to core/evidence/pbt/.
/// Only writes on failure to keep noise low.
module EvidenceRunner =

    type private FailureRecord =
        { propertyName  : string
          timestampUtc  : string
          seed          : string
          shrinks       : int
          args          : string list
          shrunkArgs    : string list
          outcome       : string }

    type PbtEvidenceRunner() =
        interface IRunner with
            member _.OnStartFixture _ = ()
            member _.OnArguments(_, _, _) = ()
            member _.OnShrink(_, _) = ()
            member _.OnFinished(name, result) =
                match result with
                | TestResult.Failed (data, args, shrunkArgs, outcome, seed0, _seed1, shrinkCount) ->
                    let argsStr = args |> List.map (fun o -> sprintf "%A" o)
                    let shrunkStr = shrunkArgs |> List.map (fun o -> sprintf "%A" o)

                    let record : FailureRecord =
                        { propertyName = name
                          timestampUtc = DateTime.UtcNow.ToString("O")
                          seed = sprintf "%A" seed0
                          shrinks = shrinkCount
                          args = argsStr
                          shrunkArgs = shrunkStr
                          outcome = sprintf "%A" outcome }

                    try
                        let repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, "../.."))
                        let evidenceDir = Path.Combine(repoRoot, "core", "evidence", "pbt")
                        Directory.CreateDirectory evidenceDir |> ignore
                        let outPath = Path.Combine(evidenceDir, "ACP-EVD-PBT-latest-failure.json")

                        let options = JsonSerializerOptions(WriteIndented = true)
                        let json = JsonSerializer.Serialize(record, options)
                        File.WriteAllText(outPath, json)
                    with _ ->
                        () // never fail the test run because evidence write failed

                | _ -> ()
