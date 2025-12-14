namespace Acp

open System
open Domain
open Domain.Prompting
open Domain.Messaging

/// Eval surface (slice-01): minimal judges and findings.
module Eval =

    [<RequireQualifiedAccess>]
    type EvalJudgeKind =
        | Code
        | Llm

    [<RequireQualifiedAccess>]
    type EvalSeverity =
        | Info
        | Warning
        | Error

    /// Minimal finding surface for eval runs.
    type EvalFinding =
        { code: string
          message: string
          severity: EvalSeverity
          judge: EvalJudgeKind }

    /// Profile switches for eval behaviour (kept small for slice-01).
    type EvalProfile =
        { enableCodeJudge: bool
          requireNonEmptyInstruction: bool
          enableFSharpLexing: bool
          fsharpUnknownTokenRatioThreshold: float
          fsharpMinCodeTokenCount: int }

    let defaultProfile: EvalProfile =
        { enableCodeJudge = true
          requireNonEmptyInstruction = true
          enableFSharpLexing = true
          fsharpUnknownTokenRatioThreshold = 0.10
          fsharpMinCodeTokenCount = 5 }

    let private hasNonEmptyText (blocks: ContentBlock list) =
        blocks
        |> List.exists (function
            | ContentBlock.Text t when not (String.IsNullOrWhiteSpace t.text) -> true
            | _ -> false)

    let private hasUsefulPromptContent (blocks: ContentBlock list) =
        blocks
        |> List.exists (function
            | ContentBlock.Text t when not (String.IsNullOrWhiteSpace t.text) -> true
            | ContentBlock.Text _ -> false
            | ContentBlock.Image _
            | ContentBlock.Audio _
            | ContentBlock.ResourceLink _
            | ContentBlock.Resource _ -> true)

    let private fsharpLexFindings (profile: EvalProfile) (text: string) : EvalFinding list =
        if not profile.enableFSharpLexing || String.IsNullOrWhiteSpace text then
            []
        else
            let spanned = FSharpTokenizer.tokenizeWithSpans text

            // Non-trivia tokens are used for code-signal counting and unknown-ratio checks.
            let tokens =
                spanned
                |> List.filter (fun st ->
                    match st.token with
                    | FSharpTokenizer.Token.Whitespace _
                    | FSharpTokenizer.Token.Comment _ -> false
                    | _ -> true)

            let isCodeSignal =
                function
                | FSharpTokenizer.Token.Keyword _
                | FSharpTokenizer.Token.Operator _
                | FSharpTokenizer.Token.Delimiter _
                | FSharpTokenizer.Token.Directive _ -> true
                | _ -> false

            let codeSignals =
                tokens |> List.filter (fun st -> isCodeSignal st.token) |> List.length

            if codeSignals < profile.fsharpMinCodeTokenCount then
                []
            else
                let total = max 1 tokens.Length

                let unknownCount =
                    tokens
                    |> List.filter (fun st ->
                        match st.token with
                        | FSharpTokenizer.Token.Unknown _ -> true
                        | _ -> false)
                    |> List.length

                let unknownRatio = float unknownCount / float total

                let isClosedString (s: string) =
                    if s.StartsWith("$\"\"\"") || s.StartsWith("\"\"\"") then
                        s.EndsWith("\"\"\"")
                    elif s.StartsWith("$@\"") || s.StartsWith("@\"") then
                        s.EndsWith("\"")
                    elif s.StartsWith("$\"") || s.StartsWith("\"") then
                        s.EndsWith("\"")
                    else
                        true

                let unclosedStrings =
                    tokens
                    |> List.choose (fun st ->
                        match st.token with
                        | FSharpTokenizer.Token.StringLit s when not (isClosedString s) -> Some st.span.start
                        | _ -> None)

                let isUnclosedBlockComment (c: string) =
                    c.StartsWith("(*") && not (c.EndsWith("*)"))

                // Unclosed block comments must be detected over all tokens (including comments).
                let unclosedComments =
                    spanned
                    |> List.choose (fun st ->
                        match st.token with
                        | FSharpTokenizer.Token.Comment c when isUnclosedBlockComment c -> Some st.span.start
                        | _ -> None)

                let findings = ResizeArray<EvalFinding>()

                if unknownCount > 0 && unknownRatio > profile.fsharpUnknownTokenRatioThreshold then
                    findings.Add
                        { code = "ACP.EVAL.FSHARP_MANY_UNKNOWN_TOKENS"
                          message =
                            sprintf
                                "F# lexing found a high unknown-token ratio (%.0f%%). This may be non-F# text or unsupported syntax."
                                (unknownRatio * 100.0)
                          severity = EvalSeverity.Warning
                          judge = EvalJudgeKind.Code }

                if unclosedStrings.Length > 0 then
                    let first = unclosedStrings.Head

                    findings.Add
                        { code = "ACP.EVAL.FSHARP_UNCLOSED_STRING"
                          message =
                            sprintf
                                "F# lexing found %d unclosed string literal(s). First at line %d, column %d."
                                unclosedStrings.Length
                                first.line
                                first.column
                          severity = EvalSeverity.Warning
                          judge = EvalJudgeKind.Code }

                if unclosedComments.Length > 0 then
                    let first = unclosedComments.Head

                    findings.Add
                        { code = "ACP.EVAL.FSHARP_UNCLOSED_BLOCK_COMMENT"
                          message =
                            sprintf
                                "F# lexing found %d unclosed block comment(s). First at line %d, column %d."
                                unclosedComments.Length
                                first.line
                                first.column
                          severity = EvalSeverity.Warning
                          judge = EvalJudgeKind.Code }

                findings |> Seq.toList

    let private collectFSharpFindingsFromContent (profile: EvalProfile) (blocks: ContentBlock list) =
        blocks
        |> List.choose (function
            | ContentBlock.Text t -> Some t.text
            | _ -> None)
        |> List.collect (fsharpLexFindings profile)

    let private toolCallContentHasUsefulContent (items: ToolCallContent list) =
        items
        |> List.exists (function
            | ToolCallContent.Content _ -> true
            | ToolCallContent.Diff _ -> true
            | ToolCallContent.Terminal _ -> true)

    let private collectFSharpFindingsFromToolCallContent (profile: EvalProfile) (items: ToolCallContent list) =
        items
        |> List.choose (function
            | ToolCallContent.Content c ->
                match c.content with
                | ContentBlock.Text t -> Some t.text
                | _ -> None
            | _ -> None)
        |> List.collect (fsharpLexFindings profile)

    /// Run prompt-level checks that do not require external context.
    let runPromptChecks (profile: EvalProfile) (msg: Message) : EvalFinding list =
        if not profile.enableCodeJudge then
            []
        else
            match msg with
            | Message.FromClient(ClientToAgentMessage.SessionPrompt p) ->
                let baseFindings =
                    if profile.requireNonEmptyInstruction && not (hasNonEmptyText p.prompt) then
                        [ { code = "ACP.EVAL.EMPTY_INSTRUCTION"
                            message = "Prompt content must include at least one non-empty text block."
                            severity = EvalSeverity.Error
                            judge = EvalJudgeKind.Code } ]
                    else
                        []

                let fsharpFindings = collectFSharpFindingsFromContent profile p.prompt

                baseFindings @ fsharpFindings
            | Message.FromAgent(AgentToClientMessage.SessionUpdate u) ->
                match u.update with
                | SessionUpdate.ToolCall tc ->
                    let baseFindings =
                        if tc.content.IsEmpty || not (toolCallContentHasUsefulContent tc.content) then
                            [ { code = "ACP.EVAL.EMPTY_TOOL_CALL_CONTENT"
                                message = "Tool call content must include text or resource content."
                                severity = EvalSeverity.Error
                                judge = EvalJudgeKind.Code } ]
                        else
                            []

                    let fsharpFindings = collectFSharpFindingsFromToolCallContent profile tc.content

                    baseFindings @ fsharpFindings
                | _ -> []
            | Message.FromAgent(AgentToClientMessage.SessionRequestPermissionRequest rp) ->
                let toolItems = rp.toolCall.content |> Option.defaultValue []

                let baseFindings =
                    if toolItems.IsEmpty || not (toolCallContentHasUsefulContent toolItems) then
                        [ { code = "ACP.EVAL.EMPTY_TOOL_CALL_CONTENT"
                            message = "Tool call content must include text or resource content."
                            severity = EvalSeverity.Error
                            judge = EvalJudgeKind.Code } ]
                    else
                        []

                let fsharpFindings = collectFSharpFindingsFromToolCallContent profile toolItems

                baseFindings @ fsharpFindings
            | _ -> []
