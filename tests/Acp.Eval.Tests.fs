namespace Acp.Tests

open Xunit

open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Prompting
open Acp.Domain.Messaging
open Acp.Eval

module EvalTests =

    let sid = SessionId "s-eval"

    [<Fact>]
    let ``non-empty text passes code judge`` () =
        let msg =
            Message.FromClient(
                ClientToAgentMessage.SessionPrompt
                    { sessionId = sid
                      content = [ ContentBlock.Text "hi" ] }
            )

        let findings = runPromptChecks defaultProfile msg
        Assert.True(findings.IsEmpty)

    [<Fact>]
    let ``empty prompt fails code judge`` () =
        let msg =
            Message.FromClient(ClientToAgentMessage.SessionPrompt { sessionId = sid; content = [] })

        let findings = runPromptChecks defaultProfile msg
        Assert.Equal(1, findings.Length)
        let f = findings.Head
        Assert.Equal("ACP.EVAL.EMPTY_INSTRUCTION", f.code)
        Assert.Equal(EvalJudgeKind.Code, f.judge)
        Assert.Equal(EvalSeverity.Error, f.severity)

    [<Fact>]
    let ``tool call with text passes`` () =
        let tc =
            { toolCallId = "t1"
              title = None
              kind = None
              status = ToolCallStatus.Requested
              content = [ ContentBlock.Text "do thing" ] }

        let msg =
            Message.FromAgent(
                AgentToClientMessage.SessionUpdate
                    { sessionId = sid
                      update = SessionUpdate.ToolCall tc }
            )

        let findings = runPromptChecks defaultProfile msg
        Assert.True(findings.IsEmpty)

    [<Fact>]
    let ``tool call empty content fails`` () =
        let tc =
            { toolCallId = "t-empty"
              title = None
              kind = None
              status = ToolCallStatus.Requested
              content = [] }

        let msg =
            Message.FromAgent(
                AgentToClientMessage.SessionUpdate
                    { sessionId = sid
                      update = SessionUpdate.ToolCall tc }
            )

        let findings = runPromptChecks defaultProfile msg
        Assert.Equal(1, findings.Length)
        Assert.Equal("ACP.EVAL.EMPTY_TOOL_CALL_CONTENT", findings.Head.code)

    [<Fact>]
    let ``fsharp lexing flags unclosed strings when code-like`` () =
        let code = "let a = (1 + 2)\nlet b = \"hi"

        let msg =
            Message.FromClient(
                ClientToAgentMessage.SessionPrompt
                    { sessionId = sid
                      content = [ ContentBlock.Text code ] }
            )

        let findings = runPromptChecks defaultProfile msg
        Assert.Contains(findings, fun f -> f.code = "ACP.EVAL.FSHARP_UNCLOSED_STRING")

    [<Fact>]
    let ``fsharp lexing warns on many unknown tokens`` () =
        let code = "let x = (1 + 2) ~~~ 3"

        let msg =
            Message.FromClient(
                ClientToAgentMessage.SessionPrompt
                    { sessionId = sid
                      content = [ ContentBlock.Text code ] }
            )

        let findings = runPromptChecks defaultProfile msg
        Assert.Contains(findings, fun f -> f.code = "ACP.EVAL.FSHARP_MANY_UNKNOWN_TOKENS")
