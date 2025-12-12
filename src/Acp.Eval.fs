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
            | ContentBlock.Text t when not (String.IsNullOrWhiteSpace t) -> true
            | _ -> false)

    let private hasUsefulContent (blocks: ContentBlock list) =
        blocks
        |> List.exists (function
            | ContentBlock.Text t when not (String.IsNullOrWhiteSpace t) -> true
            | ContentBlock.Resource _ -> true
            | _ -> false)

    let private fsharpLexFindings (profile: EvalProfile) (text: string) : EvalFinding list =
        if not profile.enableFSharpLexing || String.IsNullOrWhiteSpace text then
            []
        else
            let tokens =
                FSharpTokenizer.tokenizeWithSpans text
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

                let unclosedComments =
                    tokens
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

    /// Run prompt-level checks that do not require external context.
    let runPromptChecks (profile: EvalProfile) (msg: Message) : EvalFinding list =
        if not profile.enableCodeJudge then
            []
        else
            match msg with
            | Message.FromClient(ClientToAgentMessage.SessionPrompt p) ->
                let baseFindings =
                    if profile.requireNonEmptyInstruction && not (hasNonEmptyText p.content) then
                        [ { code = "ACP.EVAL.EMPTY_INSTRUCTION"
                            message = "Prompt content must include at least one non-empty text block."
                            severity = EvalSeverity.Error
                            judge = EvalJudgeKind.Code } ]
                    else
                        []

                let fsharpFindings =
                    p.content
                    |> List.choose (function
                        | ContentBlock.Text t -> Some t
                        | _ -> None)
                    |> List.collect (fsharpLexFindings profile)

                baseFindings @ fsharpFindings
            | Message.FromAgent(AgentToClientMessage.SessionUpdate u) ->
                match u.update with
                | SessionUpdate.ToolCall tc ->
                    let baseFindings =
                        if tc.content.IsEmpty || not (hasUsefulContent tc.content) then
                            [ { code = "ACP.EVAL.EMPTY_TOOL_CALL_CONTENT"
                                message = "Tool call content must include text or resource content."
                                severity = EvalSeverity.Error
                                judge = EvalJudgeKind.Code } ]
                        else
                            []

                    let fsharpFindings =
                        tc.content
                        |> List.choose (function
                            | ContentBlock.Text t -> Some t
                            | _ -> None)
                        |> List.collect (fsharpLexFindings profile)

                    baseFindings @ fsharpFindings
                | _ -> []
            | Message.FromAgent(AgentToClientMessage.RequestPermission rp) ->
                let baseFindings =
                    if rp.toolCall.content.IsEmpty || not (hasUsefulContent rp.toolCall.content) then
                        [ { code = "ACP.EVAL.EMPTY_TOOL_CALL_CONTENT"
                            message = "Tool call content must include text or resource content."
                            severity = EvalSeverity.Error
                            judge = EvalJudgeKind.Code } ]
                    else
                        []

                let fsharpFindings =
                    rp.toolCall.content
                    |> List.choose (function
                        | ContentBlock.Text t -> Some t
                        | _ -> None)
                    |> List.collect (fsharpLexFindings profile)

                baseFindings @ fsharpFindings
            | _ -> []
