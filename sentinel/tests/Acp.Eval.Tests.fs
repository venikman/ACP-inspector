namespace Acp.Tests

open Xunit

open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Prompting
open Acp.Domain.Messaging
open Acp.Eval

module EvalTests =

    let sid = SessionId "s-eval"

    let private textBlock (text: string) : ContentBlock =
        ContentBlock.Text { text = text; annotations = None }

    let private toolText (text: string) : ToolCallContent =
        ToolCallContent.Content { content = textBlock text }

    [<Fact>]
    let ``non-empty text passes code judge`` () =
        let msg =
            Message.FromClient(
                ClientToAgentMessage.SessionPrompt
                    { sessionId = sid
                      prompt = [ textBlock "hi" ]
                      _meta = None }
            )

        let findings = runPromptChecks defaultProfile msg
        Assert.True(findings.IsEmpty)

    [<Fact>]
    let ``empty prompt fails code judge`` () =
        let msg =
            Message.FromClient(
                ClientToAgentMessage.SessionPrompt
                    { sessionId = sid
                      prompt = []
                      _meta = None }
            )

        let findings = runPromptChecks defaultProfile msg
        Assert.Equal(1, findings.Length)
        let f = findings.Head
        Assert.Equal("ACP.EVAL.EMPTY_INSTRUCTION", f.code)
        Assert.Equal(EvalJudgeKind.Code, f.judge)
        Assert.Equal(EvalSeverity.Error, f.severity)

    [<Fact>]
    let ``tool call with text passes`` () =
        let tc: ToolCall =
            { toolCallId = "t1"
              title = "t1"
              kind = ToolKind.Read
              status = ToolCallStatus.Pending
              content = [ toolText "do thing" ]
              locations = []
              rawInput = None
              rawOutput = None }

        let msg =
            Message.FromAgent(
                AgentToClientMessage.SessionUpdate
                    { sessionId = sid
                      update = SessionUpdate.ToolCall tc
                      _meta = None }
            )

        let findings = runPromptChecks defaultProfile msg
        Assert.True(findings.IsEmpty)

    [<Fact>]
    let ``tool call empty content fails`` () =
        let tc: ToolCall =
            { toolCallId = "t-empty"
              title = "t-empty"
              kind = ToolKind.Read
              status = ToolCallStatus.Pending
              content = []
              locations = []
              rawInput = None
              rawOutput = None }

        let msg =
            Message.FromAgent(
                AgentToClientMessage.SessionUpdate
                    { sessionId = sid
                      update = SessionUpdate.ToolCall tc
                      _meta = None }
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
                      prompt = [ textBlock code ]
                      _meta = None }
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
                      prompt = [ textBlock code ]
                      _meta = None }
            )

        let findings = runPromptChecks defaultProfile msg
        Assert.Contains(findings, fun f -> f.code = "ACP.EVAL.FSHARP_MANY_UNKNOWN_TOKENS")

    [<Fact>]
    let ``fsharp lexing flags unclosed block comments`` () =
        let code = "let x = (1 + 2)\n(* comment without closing"

        let msg =
            Message.FromClient(
                ClientToAgentMessage.SessionPrompt
                    { sessionId = sid
                      prompt = [ textBlock code ]
                      _meta = None }
            )

        let findings = runPromptChecks defaultProfile msg
        Assert.Contains(findings, fun f -> f.code = "ACP.EVAL.FSHARP_UNCLOSED_BLOCK_COMMENT")

    [<Fact>]
    let ``fsharp lexing runs for request permission tool calls`` () =
        let code = "let a = (1 + 2)\nlet b = \"hi"

        let tc =
            { toolCallId = "t-rp"
              title = None
              kind = None
              status = None
              content = Some [ toolText code ]
              locations = None
              rawInput = None
              rawOutput = None }

        let rp =
            { sessionId = sid
              toolCall = tc
              options = [] }

        let msg = Message.FromAgent(AgentToClientMessage.SessionRequestPermissionRequest rp)

        let findings = runPromptChecks defaultProfile msg
        Assert.Contains(findings, fun f -> f.code = "ACP.EVAL.FSHARP_UNCLOSED_STRING")
