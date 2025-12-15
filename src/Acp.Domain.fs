namespace Acp

open System
open System.Text.Json.Nodes

/// Domain model for ACP v0.10.x (schema.json).
/// This is the typed, transport-agnostic meaning of ACP after JSON-RPC framing is decoded.
module Domain =

    /// Spec/version metadata for this implementation.
    [<RequireQualifiedAccess>]
    module Spec =
        /// ACP schema version this repo targets (spec line: schema.json).
        [<Literal>]
        let Schema = "0.10.x"

        /// JSON-RPC framing version used by the codec.
        [<Literal>]
        let JsonRpc = "2.0"

    // -------------
    // Primitives (schema)
    // -------------

    module PrimitivesAndParties =

        /// MAJOR protocol version negotiated during initialization (uint16 in schema).
        type ProtocolVersion = int

        [<RequireQualifiedAccess>]
        module ProtocolVersion =
            /// Current protocol major version implemented by this repo.
            let current: ProtocolVersion = 1

            /// Supported protocol major versions (currently only `current`).
            let supported: ProtocolVersion list = [ current ]

            let isSupported (v: ProtocolVersion) = supported |> List.contains v

        /// Opaque session identifier.
        [<Struct>]
        type SessionId = SessionId of string

        [<RequireQualifiedAccess>]
        module SessionId =
            let value (SessionId s) = s

        /// Implementation metadata for clients and agents.
        type ImplementationInfo =
            { name: string
              title: string option
              version: string }

        /// Reasons why a prompt turn stopped (StopReason).
        [<RequireQualifiedAccess>]
        type StopReason =
            | EndTurn
            | MaxTokens
            | MaxTurnRequests
            | Refusal
            | Cancelled

    // -------------
    // Capabilities (schema)
    // -------------

    module Capabilities =

        /// File system capabilities supported by the client.
        type FileSystemCapabilities =
            { readTextFile: bool
              writeTextFile: bool }

        /// Capabilities advertised by the client in initialize.
        type ClientCapabilities =
            { fs: FileSystemCapabilities
              terminal: bool }

        /// MCP transport capabilities supported by the agent.
        type McpCapabilities = { http: bool; sse: bool }

        /// Prompt content types the agent can process in `session/prompt`.
        type PromptCapabilities =
            { audio: bool
              image: bool
              embeddedContext: bool }

        /// Session capabilities supported by the agent. Currently an empty object in schema.
        [<Struct>]
        type SessionCapabilities = SessionCapabilities

        [<RequireQualifiedAccess>]
        module SessionCapabilities =
            let empty = SessionCapabilities

        /// Capabilities advertised by the agent during initialize.
        type AgentCapabilities =
            { loadSession: bool
              mcpCapabilities: McpCapabilities
              promptCapabilities: PromptCapabilities
              sessionCapabilities: SessionCapabilities }

    // -------------
    // Authentication (schema)
    // -------------

    module Authentication =

        type AuthMethod =
            { id: string
              name: string
              description: string option }

        type AuthenticateParams = { methodId: string }

        /// Empty result object in schema.
        [<Struct>]
        type AuthenticateResult = AuthenticateResult

        [<RequireQualifiedAccess>]
        module AuthenticateResult =
            let empty = AuthenticateResult

    // -------------
    // Initialization (schema)
    // -------------

    module Initialization =

        open PrimitivesAndParties
        open Capabilities
        open Authentication

        /// Params for initialize (client -> agent).
        type InitializeParams =
            { protocolVersion: ProtocolVersion
              clientCapabilities: ClientCapabilities
              clientInfo: ImplementationInfo option }

        /// Result for initialize (agent -> client).
        type InitializeResult =
            { protocolVersion: ProtocolVersion
              agentCapabilities: AgentCapabilities
              agentInfo: ImplementationInfo option
              authMethods: AuthMethod list }

    // -------------
    // Session modes (schema)
    // -------------

    module SessionModes =

        open PrimitivesAndParties

        /// Unique identifier for a session mode.
        [<Struct>]
        type SessionModeId = SessionModeId of string

        [<RequireQualifiedAccess>]
        module SessionModeId =
            let value (SessionModeId s) = s

        type SessionMode =
            { id: SessionModeId
              name: string
              description: string option }

        /// The set of modes and the one currently active.
        type SessionModeState =
            { currentModeId: SessionModeId
              availableModes: SessionMode list }

        /// Params for session/set_mode (client -> agent).
        type SetSessionModeParams =
            { sessionId: SessionId
              modeId: SessionModeId }

        /// Domain-level result for session/set_mode (agent -> client).
        /// Wire result is structurally empty; we reattach sessionId + modeId from the request.
        type SetSessionModeResult =
            { sessionId: SessionId
              modeId: SessionModeId }

        type CurrentModeUpdate = { currentModeId: SessionModeId }

    // -------------
    // Session setup (schema)
    // -------------

    module SessionSetup =

        open PrimitivesAndParties
        open SessionModes

        type EnvVariable = { name: string; value: string }

        type HttpHeader = { name: string; value: string }

        type McpServerHttp =
            { name: string
              url: string
              headers: HttpHeader list }

        type McpServerSse =
            { name: string
              url: string
              headers: HttpHeader list }

        type McpServerStdio =
            { name: string
              command: string
              args: string list
              env: EnvVariable list }

        [<RequireQualifiedAccess>]
        type McpServer =
            | Http of McpServerHttp
            | Sse of McpServerSse
            | Stdio of McpServerStdio

        /// Params for session/new (client -> agent).
        type NewSessionParams =
            { cwd: string
              mcpServers: McpServer list }

        /// Params for session/load (client -> agent).
        type LoadSessionParams =
            { sessionId: SessionId
              cwd: string
              mcpServers: McpServer list }

        /// Result for session/new (agent -> client).
        type NewSessionResult =
            { sessionId: SessionId
              modes: SessionModeState option }

        /// Domain-level result for session/load (agent -> client).
        /// Wire result does not include a session id; we reattach it from the request.
        type LoadSessionResult =
            { sessionId: SessionId
              modes: SessionModeState option }

    // -------------
    // Session context (runtime helper)
    // -------------

    module SessionContext =

        open PrimitivesAndParties
        open Initialization
        open SessionModes

        /// Per-session state container; the turnState is protocol-specific.
        type SessionState<'turnState> =
            { sessionId: SessionId
              modeState: SessionModeState option
              turnState: 'turnState }

        /// Connection-level initialized context: negotiated handshake + session table.
        type InitializedContext<'turnState> =
            { clientInit: InitializeParams
              agentInit: InitializeResult
              sessions: Map<SessionId, SessionState<'turnState>> }

    // -------------
    // Metadata (sentinel/runtime policy, not wire _meta)
    // -------------

    module Metadata =

        /// Opaque metadata carrier preserved by the runtime.
        [<Struct>]
        type MetaEnvelope = MetaEnvelope of obj // Runtime-specific.

        /// Metadata and transport policy knobs.
        [<RequireQualifiedAccess>]
        type MetadataPolicy =
            | Disallow
            | AllowOpaque
            | AllowKinds of string list

        /// Transport defaults surfaced to validation.
        type TransportDefaults =
            { lineSeparator: string option
              maxFrameBytes: int option
              maxMessageBytes: int option
              metaEnvelope: MetaEnvelope option }

        /// Runtime profile for metadata/transport handling.
        type RuntimeProfile =
            { metadata: MetadataPolicy
              transport: TransportDefaults option }

    // -------------
    // JSON-RPC (wire errors + ids)
    // -------------

    module JsonRpc =

        [<RequireQualifiedAccess>]
        type RequestId =
            | Null
            | Number of int64
            | String of string

        type Error =
            { code: int
              message: string
              data: JsonNode option }

        /// Standard JSON-RPC 2.0 error codes.
        [<RequireQualifiedAccess>]
        module ErrorCode =
            // Standard JSON-RPC 2.0 error codes
            [<Literal>]
            let ParseError = -32700

            [<Literal>]
            let InvalidRequest = -32600

            [<Literal>]
            let MethodNotFound = -32601

            [<Literal>]
            let InvalidParams = -32602

            [<Literal>]
            let InternalError = -32603

            // ACP-specific error codes (reserved range -32000 to -32099)
            [<Literal>]
            let AuthenticationRequired = -32000

            [<Literal>]
            let ResourceNotFound = -32002

            /// Check if a code is a standard JSON-RPC error code.
            let isStandard code = code <= -32600 && code >= -32700

            /// Check if a code is in the ACP reserved range.
            let isAcpReserved code = code >= -32099 && code <= -32000

            /// Check if a code is a known ACP error code.
            let isKnownAcp code =
                code = AuthenticationRequired || code = ResourceNotFound

            /// Describe a known error code.
            let describe =
                function
                | c when c = ParseError -> Some "Parse error"
                | c when c = InvalidRequest -> Some "Invalid request"
                | c when c = MethodNotFound -> Some "Method not found"
                | c when c = InvalidParams -> Some "Invalid params"
                | c when c = InternalError -> Some "Internal error"
                | c when c = AuthenticationRequired -> Some "Authentication required"
                | c when c = ResourceNotFound -> Some "Resource not found"
                | _ -> None

    // -------------
    // Tooling + content (schema)
    // -------------

    module Prompting =

        open PrimitivesAndParties
        open SessionModes
        open SessionSetup

        // ---- Content blocks ----

        type Annotations =
            { audience: string list option
              priority: float option
              lastModified: string option }

        type TextContent =
            { text: string
              annotations: Annotations option }

        type ImageContent =
            { data: string
              mimeType: string
              uri: string option
              annotations: Annotations option }

        type AudioContent =
            { data: string
              mimeType: string
              annotations: Annotations option }

        type ResourceLink =
            { name: string
              uri: string
              title: string option
              description: string option
              mimeType: string option
              size: int64 option
              annotations: Annotations option }

        [<RequireQualifiedAccess>]
        type EmbeddedResourceResource =
            | Text of uri: string * text: string * mimeType: string option
            | Blob of uri: string * blob: string * mimeType: string option

        type EmbeddedResource =
            { resource: EmbeddedResourceResource
              annotations: Annotations option }

        [<RequireQualifiedAccess>]
        type ContentBlock =
            | Text of TextContent
            | Image of ImageContent
            | Audio of AudioContent
            | ResourceLink of ResourceLink
            | Resource of EmbeddedResource

        type ContentChunk = { content: ContentBlock }

        // ---- Prompt request/response ----

        type SessionPromptParams =
            { sessionId: SessionId
              prompt: ContentBlock list }

        /// Domain-level result for session/prompt (agent -> client).
        /// Wire result does not include a session id; we reattach it from the request.
        type SessionPromptResult =
            { sessionId: SessionId
              stopReason: StopReason }

        /// Domain-level error outcome (non-wire, for sentinel and higher-level tooling).
        [<RequireQualifiedAccess>]
        type DomainErrorOutcome =
            | ProtocolViolation of code: string * details: string
            | AgentInternalFailure of details: string
            | ClientInternalFailure of details: string
            | ToolingFailure of toolName: string * details: string

        /// Canonical prompt turn outcome classification (non-wire).
        [<RequireQualifiedAccess>]
        type PromptTurnOutcome =
            | Completed of stopReason: StopReason
            | CancelledByUser
            | DomainError of DomainErrorOutcome

        // ---- Plan ----

        [<RequireQualifiedAccess>]
        type PlanEntryPriority =
            | High
            | Medium
            | Low

        [<RequireQualifiedAccess>]
        type PlanEntryStatus =
            | Pending
            | InProgress
            | Completed

        type PlanEntry =
            { content: string
              priority: PlanEntryPriority
              status: PlanEntryStatus }

        type Plan = { entries: PlanEntry list }

        // ---- Commands ----

        type UnstructuredCommandInput = { hint: string }

        [<RequireQualifiedAccess>]
        type AvailableCommandInput = Unstructured of UnstructuredCommandInput

        type AvailableCommand =
            { name: string
              description: string
              input: AvailableCommandInput option }

        type AvailableCommandsUpdate =
            { availableCommands: AvailableCommand list }

        // ---- Tool calls ----

        type Diff =
            { path: string
              oldText: string option
              newText: string }

        type Terminal = { terminalId: string }

        type Content = { content: ContentBlock }

        [<RequireQualifiedAccess>]
        type ToolCallContent =
            | Content of Content
            | Diff of Diff
            | Terminal of Terminal

        [<RequireQualifiedAccess>]
        type ToolKind =
            | Read
            | Edit
            | Delete
            | Move
            | Search
            | Execute
            | Think
            | Fetch
            | SwitchMode
            | Other

        [<RequireQualifiedAccess>]
        type ToolCallStatus =
            | Pending
            | InProgress
            | Completed
            | Failed

        type ToolCallLocation = { path: string; line: int option }

        type ToolCall =
            { toolCallId: string
              title: string
              kind: ToolKind
              status: ToolCallStatus
              content: ToolCallContent list
              locations: ToolCallLocation list
              rawInput: JsonNode option
              rawOutput: JsonNode option }

        type ToolCallUpdate =
            { toolCallId: string
              title: string option
              kind: ToolKind option
              status: ToolCallStatus option
              content: ToolCallContent list option
              locations: ToolCallLocation list option
              rawInput: JsonNode option
              rawOutput: JsonNode option }

        // ---- Permission requests ----

        [<RequireQualifiedAccess>]
        type PermissionOptionKind =
            | AllowOnce
            | AllowAlways
            | RejectOnce
            | RejectAlways

        type PermissionOption =
            { optionId: string
              name: string
              kind: PermissionOptionKind }

        type RequestPermissionParams =
            { sessionId: SessionId
              toolCall: ToolCallUpdate
              options: PermissionOption list }

        [<RequireQualifiedAccess>]
        type RequestPermissionOutcome =
            | Cancelled
            | Selected of optionId: string

        type RequestPermissionResult = { outcome: RequestPermissionOutcome }

        // ---- Session updates ----

        [<RequireQualifiedAccess>]
        type SessionUpdate =
            | UserMessageChunk of ContentChunk
            | AgentMessageChunk of ContentChunk
            | AgentThoughtChunk of ContentChunk
            | ToolCall of ToolCall
            | ToolCallUpdate of ToolCallUpdate
            | Plan of Plan
            | AvailableCommandsUpdate of AvailableCommandsUpdate
            | CurrentModeUpdate of CurrentModeUpdate

        type SessionUpdateNotification =
            { sessionId: SessionId
              update: SessionUpdate }

        /// Params for session/cancel (client -> agent). Notification.
        type SessionCancelParams = { sessionId: SessionId }

        // ---- File system + terminal tool surface (agent -> client requests) ----

        type ReadTextFileParams =
            { sessionId: SessionId
              path: string
              line: int option
              limit: int option }

        type ReadTextFileResult = { content: string }

        type WriteTextFileParams =
            { sessionId: SessionId
              path: string
              content: string }

        [<Struct>]
        type WriteTextFileResult = WriteTextFileResult

        [<RequireQualifiedAccess>]
        module WriteTextFileResult =
            let empty = WriteTextFileResult

        type CreateTerminalParams =
            { sessionId: SessionId
              command: string
              args: string list
              cwd: string option
              env: EnvVariable list
              outputByteLimit: uint64 option }

        type CreateTerminalResult = { terminalId: string }

        type TerminalExitStatus =
            { exitCode: int option
              signal: string option }

        type TerminalOutputParams =
            { sessionId: SessionId
              terminalId: string }

        type TerminalOutputResult =
            { output: string
              truncated: bool
              exitStatus: TerminalExitStatus option }

        type WaitForTerminalExitParams =
            { sessionId: SessionId
              terminalId: string }

        type WaitForTerminalExitResult = TerminalExitStatus

        type KillTerminalCommandParams =
            { sessionId: SessionId
              terminalId: string }

        [<Struct>]
        type KillTerminalCommandResult = KillTerminalCommandResult

        [<RequireQualifiedAccess>]
        module KillTerminalCommandResult =
            let empty = KillTerminalCommandResult

        type ReleaseTerminalParams =
            { sessionId: SessionId
              terminalId: string }

        [<Struct>]
        type ReleaseTerminalResult = ReleaseTerminalResult

        [<RequireQualifiedAccess>]
        module ReleaseTerminalResult =
            let empty = ReleaseTerminalResult

    // -------------
    // Message envelopes (decoded JSON-RPC + correlated responses)
    // -------------

    module Messaging =

        open Initialization
        open Authentication
        open JsonRpc
        open SessionSetup
        open SessionModes
        open Prompting

        /// Methods/notifications/responses originating at the client.
        [<RequireQualifiedAccess>]
        type ClientToAgentMessage =
            // Requests (client -> agent)
            | Initialize of InitializeParams
            | Authenticate of AuthenticateParams
            | SessionNew of NewSessionParams
            | SessionLoad of LoadSessionParams
            | SessionPrompt of SessionPromptParams
            | SessionSetMode of SetSessionModeParams
            | ExtRequest of methodName: string * parameters: JsonNode option
            // Notifications (client -> agent)
            | SessionCancel of SessionCancelParams
            | ExtNotification of methodName: string * parameters: JsonNode option
            // Responses (client -> agent) to agent->client requests
            | FsReadTextFileResult of ReadTextFileResult
            | FsWriteTextFileResult of WriteTextFileResult
            | SessionRequestPermissionResult of RequestPermissionResult
            | TerminalCreateResult of CreateTerminalResult
            | TerminalOutputResult of TerminalOutputResult
            | TerminalWaitForExitResult of WaitForTerminalExitResult
            | TerminalKillResult of KillTerminalCommandResult
            | TerminalReleaseResult of ReleaseTerminalResult
            | FsReadTextFileError of request: ReadTextFileParams * error: Error
            | FsWriteTextFileError of request: WriteTextFileParams * error: Error
            | SessionRequestPermissionError of request: RequestPermissionParams * error: Error
            | TerminalCreateError of request: CreateTerminalParams * error: Error
            | TerminalOutputError of request: TerminalOutputParams * error: Error
            | TerminalWaitForExitError of request: WaitForTerminalExitParams * error: Error
            | TerminalKillError of request: KillTerminalCommandParams * error: Error
            | TerminalReleaseError of request: ReleaseTerminalParams * error: Error
            | ExtError of methodName: string * error: Error
            | ExtResponse of methodName: string * result: JsonNode option

        /// Methods/notifications/requests originating at the agent.
        [<RequireQualifiedAccess>]
        type AgentToClientMessage =
            // Responses (agent -> client) to client->agent requests
            | InitializeResult of InitializeResult
            | AuthenticateResult of AuthenticateResult
            | SessionNewResult of NewSessionResult
            | SessionLoadResult of LoadSessionResult
            | SessionPromptResult of SessionPromptResult
            | SessionSetModeResult of SetSessionModeResult
            | ExtResponse of methodName: string * result: JsonNode option
            | InitializeError of error: Error
            | AuthenticateError of error: Error
            | SessionNewError of error: Error
            | SessionLoadError of request: LoadSessionParams * error: Error
            | SessionPromptError of request: SessionPromptParams * error: Error
            | SessionSetModeError of request: SetSessionModeParams * error: Error
            | ExtError of methodName: string * error: Error
            // Notifications (agent -> client)
            | SessionUpdate of SessionUpdateNotification
            | ExtNotification of methodName: string * parameters: JsonNode option
            // Requests (agent -> client)
            | FsReadTextFileRequest of ReadTextFileParams
            | FsWriteTextFileRequest of WriteTextFileParams
            | SessionRequestPermissionRequest of RequestPermissionParams
            | TerminalCreateRequest of CreateTerminalParams
            | TerminalOutputRequest of TerminalOutputParams
            | TerminalWaitForExitRequest of WaitForTerminalExitParams
            | TerminalKillRequest of KillTerminalCommandParams
            | TerminalReleaseRequest of ReleaseTerminalParams
            | ExtRequest of methodName: string * parameters: JsonNode option

        /// Direction-tagged domain message stream for a single connection.
        [<RequireQualifiedAccess>]
        type Message =
            | FromClient of ClientToAgentMessage
            | FromAgent of AgentToClientMessage

    // -------------
    // Tracing
    // -------------

    module Tracing =

        open PrimitivesAndParties
        open Messaging

        /// Full session trace: message list is ordered as observed on the connection.
        type SessionTrace =
            { sessionId: SessionId
              messages: Message list }
