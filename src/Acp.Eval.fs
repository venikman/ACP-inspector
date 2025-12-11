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
        { code     : string
          message  : string
          severity : EvalSeverity
          judge    : EvalJudgeKind }

    /// Profile switches for eval behaviour (kept small for slice-01).
    type EvalProfile =
        { enableCodeJudge           : bool
          requireNonEmptyInstruction : bool }

    let defaultProfile : EvalProfile =
        { enableCodeJudge = true
          requireNonEmptyInstruction = true }

    let private hasNonEmptyText (blocks : ContentBlock list) =
        blocks
        |> List.exists (function
            | ContentBlock.Text t when not (String.IsNullOrWhiteSpace t) -> true
            | _ -> false)

    let private hasUsefulContent (blocks : ContentBlock list) =
        blocks
        |> List.exists (function
            | ContentBlock.Text t when not (String.IsNullOrWhiteSpace t) -> true
            | ContentBlock.Resource _ -> true
            | _ -> false)

    /// Run prompt-level checks that do not require external context.
    let runPromptChecks (profile : EvalProfile) (msg : Message) : EvalFinding list =
        if not profile.enableCodeJudge then []
        else
            match msg with
            | Message.FromClient (ClientToAgentMessage.SessionPrompt p) ->
                if profile.requireNonEmptyInstruction && not (hasNonEmptyText p.content) then
                    [ { code     = "ACP.EVAL.EMPTY_INSTRUCTION"
                        message  = "Prompt content must include at least one non-empty text block."
                        severity = EvalSeverity.Error
                        judge    = EvalJudgeKind.Code } ]
                else
                    []
            | Message.FromAgent (AgentToClientMessage.SessionUpdate u) ->
                match u.update with
                | SessionUpdate.ToolCall tc ->
                    if tc.content.IsEmpty || not (hasUsefulContent tc.content) then
                        [ { code     = "ACP.EVAL.EMPTY_TOOL_CALL_CONTENT"
                            message  = "Tool call content must include text or resource content."
                            severity = EvalSeverity.Error
                            judge    = EvalJudgeKind.Code } ]
                    else
                        []
                | _ -> []
            | Message.FromAgent (AgentToClientMessage.RequestPermission rp) ->
                if rp.toolCall.content.IsEmpty || not (hasUsefulContent rp.toolCall.content) then
                    [ { code     = "ACP.EVAL.EMPTY_TOOL_CALL_CONTENT"
                        message  = "Tool call content must include text or resource content."
                        severity = EvalSeverity.Error
                        judge    = EvalJudgeKind.Code } ]
                else
                    []
            | _ -> []
