namespace Acp.Tests

open System.IO
open Xunit

open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Prompting
open Acp.Domain.Messaging
open Acp.Eval

module EvalGoldenTests =

    let private sid = SessionId "golden-eval"

    let private repoRoot = Path.GetFullPath(Path.Combine(__SOURCE_DIRECTORY__, ".."))

    let private corpusDir = Path.Combine(repoRoot, "tests", "golden", "fsharp")

    let private runOnFile (name: string) =
        let text = File.ReadAllText(Path.Combine(corpusDir, name))

        let msg =
            Message.FromClient(
                ClientToAgentMessage.SessionPrompt
                    { sessionId = sid
                      prompt = [ ContentBlock.Text { text = text; annotations = None } ]
                      _meta = None }
            )

        runPromptChecks defaultProfile msg |> List.map (fun f -> f.code) |> Set.ofList

    [<Fact>]
    let ``golden fsharp corpus yields expected Eval findings`` () =
        let cases =
            [ "valid.fs", Set.empty
              "unknown-heavy.fs", set [ "ACP.EVAL.FSHARP_MANY_UNKNOWN_TOKENS" ]
              "unclosed-string.fs", set [ "ACP.EVAL.FSHARP_UNCLOSED_STRING" ]
              "unclosed-block-comment.fs", set [ "ACP.EVAL.FSHARP_UNCLOSED_BLOCK_COMMENT" ] ]

        for (file, expected) in cases do
            let actual = runOnFile file
            Assert.Equal<Set<string>>(expected, actual)
