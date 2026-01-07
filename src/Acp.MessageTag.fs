namespace Acp

open Acp.Domain.Messaging
open Acp.Domain.Prompting

/// Rendering helpers for short, human-friendly message tags.
module MessageTag =

    let private sessionUpdateTag =
        function
        | SessionUpdate.UserMessageChunk _ -> "user_message_chunk"
        | SessionUpdate.AgentMessageChunk _ -> "agent_message_chunk"
        | SessionUpdate.AgentThoughtChunk _ -> "agent_thought_chunk"
        | SessionUpdate.ToolCall _ -> "tool_call"
        | SessionUpdate.ToolCallUpdate _ -> "tool_call_update"
        | SessionUpdate.Plan _ -> "plan"
        | SessionUpdate.AvailableCommandsUpdate _ -> "available_commands_update"
        | SessionUpdate.CurrentModeUpdate _ -> "current_mode_update"
        | SessionUpdate.Ext(tag, _) -> $"ext:{tag}"

    let render (msg: Message) =
        match msg with
        | Message.FromClient c ->
            match c with
            | ClientToAgentMessage.Initialize p -> $"initialize pv={p.protocolVersion}"
            | ClientToAgentMessage.ProxyInitialize p -> $"proxy/initialize pv={p.protocolVersion}"
            | ClientToAgentMessage.Authenticate _ -> "authenticate"
            | ClientToAgentMessage.SessionNew _ -> "session/new"
            | ClientToAgentMessage.SessionLoad _ -> "session/load"
            | ClientToAgentMessage.SessionPrompt _ -> "session/prompt"
            | ClientToAgentMessage.SessionSetMode _ -> "session/set_mode"
            | ClientToAgentMessage.SessionCancel _ -> "session/cancel"
            | ClientToAgentMessage.ProxySuccessorRequest p -> $"proxy/successor inner={p.method}"
            | ClientToAgentMessage.ProxySuccessorNotification p -> $"proxy/successor inner={p.method}"
            | ClientToAgentMessage.ProxySuccessorResponse(methodName, _) ->
                $"proxy/successor (result) inner={methodName}"
            | ClientToAgentMessage.ProxySuccessorError(methodName, _) -> $"proxy/successor (error) inner={methodName}"
            | ClientToAgentMessage.ExtRequest(methodName, _) -> methodName
            | ClientToAgentMessage.ExtNotification(methodName, _) -> methodName
            | ClientToAgentMessage.ExtResponse(methodName, _) -> methodName
            | ClientToAgentMessage.ExtError(methodName, _) -> methodName
            | ClientToAgentMessage.FsReadTextFileResult _ -> "fs/read_text_file (result)"
            | ClientToAgentMessage.FsWriteTextFileResult _ -> "fs/write_text_file (result)"
            | ClientToAgentMessage.SessionRequestPermissionResult _ -> "session/request_permission (result)"
            | ClientToAgentMessage.TerminalCreateResult _ -> "terminal/create (result)"
            | ClientToAgentMessage.TerminalOutputResult _ -> "terminal/output (result)"
            | ClientToAgentMessage.TerminalWaitForExitResult _ -> "terminal/wait_for_exit (result)"
            | ClientToAgentMessage.TerminalKillResult _ -> "terminal/kill (result)"
            | ClientToAgentMessage.TerminalReleaseResult _ -> "terminal/release (result)"
            | ClientToAgentMessage.FsReadTextFileError _ -> "fs/read_text_file (error)"
            | ClientToAgentMessage.FsWriteTextFileError _ -> "fs/write_text_file (error)"
            | ClientToAgentMessage.SessionRequestPermissionError _ -> "session/request_permission (error)"
            | ClientToAgentMessage.TerminalCreateError _ -> "terminal/create (error)"
            | ClientToAgentMessage.TerminalOutputError _ -> "terminal/output (error)"
            | ClientToAgentMessage.TerminalWaitForExitError _ -> "terminal/wait_for_exit (error)"
            | ClientToAgentMessage.TerminalKillError _ -> "terminal/kill (error)"
            | ClientToAgentMessage.TerminalReleaseError _ -> "terminal/release (error)"
        | Message.FromAgent a ->
            match a with
            | AgentToClientMessage.InitializeResult r -> $"initialize (result) pv={r.protocolVersion}"
            | AgentToClientMessage.ProxyInitializeResult r -> $"proxy/initialize (result) pv={r.protocolVersion}"
            | AgentToClientMessage.AuthenticateResult _ -> "authenticate (result)"
            | AgentToClientMessage.SessionNewResult _ -> "session/new (result)"
            | AgentToClientMessage.SessionLoadResult _ -> "session/load (result)"
            | AgentToClientMessage.SessionPromptResult _ -> "session/prompt (result)"
            | AgentToClientMessage.SessionSetModeResult _ -> "session/set_mode (result)"
            | AgentToClientMessage.ExtResponse(methodName, _) -> methodName
            | AgentToClientMessage.ProxySuccessorResponse(methodName, _) ->
                $"proxy/successor (result) inner={methodName}"
            | AgentToClientMessage.InitializeError _ -> "initialize (error)"
            | AgentToClientMessage.ProxyInitializeError _ -> "proxy/initialize (error)"
            | AgentToClientMessage.AuthenticateError _ -> "authenticate (error)"
            | AgentToClientMessage.SessionNewError _ -> "session/new (error)"
            | AgentToClientMessage.SessionLoadError _ -> "session/load (error)"
            | AgentToClientMessage.SessionPromptError _ -> "session/prompt (error)"
            | AgentToClientMessage.SessionSetModeError _ -> "session/set_mode (error)"
            | AgentToClientMessage.ExtError(methodName, _) -> methodName
            | AgentToClientMessage.ProxySuccessorError(methodName, _) -> $"proxy/successor (error) inner={methodName}"
            | AgentToClientMessage.SessionUpdate u -> $"session/update ({sessionUpdateTag u.update})"
            | AgentToClientMessage.ProxySuccessorNotification p -> $"proxy/successor inner={p.method}"
            | AgentToClientMessage.ExtNotification(methodName, _) -> methodName
            | AgentToClientMessage.FsReadTextFileRequest _ -> "fs/read_text_file"
            | AgentToClientMessage.FsWriteTextFileRequest _ -> "fs/write_text_file"
            | AgentToClientMessage.SessionRequestPermissionRequest _ -> "session/request_permission"
            | AgentToClientMessage.TerminalCreateRequest _ -> "terminal/create"
            | AgentToClientMessage.TerminalOutputRequest _ -> "terminal/output"
            | AgentToClientMessage.TerminalWaitForExitRequest _ -> "terminal/wait_for_exit"
            | AgentToClientMessage.TerminalKillRequest _ -> "terminal/kill"
            | AgentToClientMessage.TerminalReleaseRequest _ -> "terminal/release"
            | AgentToClientMessage.ProxySuccessorRequest p -> $"proxy/successor inner={p.method}"
            | AgentToClientMessage.ExtRequest(methodName, _) -> methodName
