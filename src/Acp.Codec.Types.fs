namespace Acp

open Domain.JsonRpc
open Domain.SessionSetup
open Domain.Prompting
open Domain.SessionModes
open Domain.Messaging

/// Codec type definitions for JSON-RPC 2.0 + ACP message routing.
module CodecTypes =

    /// Direction of message flow
    [<RequireQualifiedAccess>]
    type Direction =
        | FromClient
        | FromAgent

    /// Errors that can occur during decoding
    [<RequireQualifiedAccess>]
    type DecodeError =
        | InvalidJson of details: string
        | InvalidJsonRpc of details: string
        | UnknownMethod of methodName: string
        | DirectionMismatch of methodName: string * expected: Direction
        | DuplicateRequestId of id: RequestId
        | UnknownRequestId of id: RequestId
        | InvalidParams of methodName: string * details: string
        | InvalidResult of methodName: string * details: string
        | InvalidError of details: string

    /// Errors that can occur during encoding
    [<RequireQualifiedAccess>]
    type EncodeError =
        | MissingRequestId
        | UnexpectedRequestId
        | UnsupportedMessage of details: string

    /// Pending client request - tracks what we're waiting for from agent
    [<RequireQualifiedAccess>]
    type PendingClientRequest =
        | Initialize
        | Authenticate
        | SessionNew
        | SessionLoad of request: LoadSessionParams
        | SessionPrompt of request: SessionPromptParams
        | SessionSetMode of request: SetSessionModeParams
        | ExtRequest of methodName: string

    /// Pending agent request - tracks what we're waiting for from client
    [<RequireQualifiedAccess>]
    type PendingAgentRequest =
        | FsReadTextFile of request: ReadTextFileParams
        | FsWriteTextFile of request: WriteTextFileParams
        | SessionRequestPermission of request: RequestPermissionParams
        | TerminalCreate of request: CreateTerminalParams
        | TerminalOutput of request: TerminalOutputParams
        | TerminalWaitForExit of request: WaitForTerminalExitParams
        | TerminalKill of request: KillTerminalCommandParams
        | TerminalRelease of request: ReleaseTerminalParams
        | ExtRequest of methodName: string

    /// Codec state - tracks pending requests for correlation
    type CodecState =
        { pendingClientRequests: Map<RequestId, PendingClientRequest>
          pendingAgentRequests: Map<RequestId, PendingAgentRequest> }

    [<RequireQualifiedAccess>]
    module CodecState =
        let empty: CodecState =
            { pendingClientRequests = Map.empty
              pendingAgentRequests = Map.empty }
