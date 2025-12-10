namespace Acp

open System

/// Domain model for a core slice of the Agent Client Protocol (ACP).
/// JSON-RPC 2.0 framing and raw JSON live elsewhere; this is "what ACP means".
module Domain =

    // -------------
    // Primitives & Parties (R1-R4)
    // -------------

    module PrimitivesAndParties =

        /// MAJOR protocol version negotiated during initialization.
        type ProtocolVersion = int

        /// Opaque server-chosen session identifier.
        [<Struct>]
        type SessionId = SessionId of string

        [<RequireQualifiedAccess>]
        module SessionId =
            let value (SessionId s) = s

        /// Implementation metadata for clients and agents.
        type ImplementationInfo =
            { name    : string
              title   : string option
              version : string option }

        /// R1/R2 - parties (agent/client). Thin wrappers over ImplementationInfo today.
        [<RequireQualifiedAccess>]
        type PartyRole =
            | Agent
            | Client

        type Party =
            { role : PartyRole
              implementation : ImplementationInfo }

        /// R4 - reasons why a prompt turn stopped.
        [<RequireQualifiedAccess>]
        type StopReason =
            | EndTurn
            | MaxTokens
            | MaxTurnRequests
            | Refusal
            | Cancelled
            | Other of string

    // -------------
    // Capabilities (R7, R13-R15)
    // -------------

    module Capabilities =

        /// File system capabilities supported by the client.
        type FileSystemCapabilities =
            { readTextFile  : bool
              writeTextFile : bool }

        /// Capabilities advertised by the client in initialize.
        type ClientCapabilities =
            { fs       : FileSystemCapabilities
              terminal : bool }

        /// MCP transport capabilities supported by the agent.
        type McpCapabilities =
            { http : bool
              sse  : bool }

        /// Non-baseline prompt content types the agent can process.
        type PromptCapabilities =
            { audio           : bool
              image           : bool
              embeddedContext : bool }

        /// Capabilities advertised by the agent during initialize.
        type AgentCapabilities =
            { loadSession        : bool
              mcpCapabilities    : McpCapabilities
              promptCapabilities : PromptCapabilities }

    // -------------
    // Initialization (R1-R3, R19)
    // -------------

    module Initialization =

        open PrimitivesAndParties
        open Capabilities

        /// Params for initialize (client -> agent).
        type InitializeParams =
            { protocolVersion    : ProtocolVersion
              clientCapabilities : ClientCapabilities
              clientInfo         : ImplementationInfo option }

        /// Result for initialize (agent -> client).
        type InitializeResult =
            { negotiatedVersion : ProtocolVersion
              agentCapabilities : AgentCapabilities
              agentInfo         : ImplementationInfo option }

    // -------------
    // Session setup (R3, R4, R16)
    // -------------

    module SessionSetup =

        open PrimitivesAndParties

        /// Environment variable passed to an MCP server.
        type McpServerEnvVar =
            { name  : string
              value : string }

        /// Minimal MCP server configuration used in session/new & session/load.
        type McpServer =
            { name    : string
              command : string
              args    : string list
              env     : McpServerEnvVar list }

        /// Params for session/new (client -> agent).
        type NewSessionParams =
            { cwd        : string
              mcpServers : McpServer list }

        /// Params for session/load (client -> agent).
        type LoadSessionParams =
            { sessionId  : SessionId
              cwd        : string
              mcpServers : McpServer list }

        /// Result for session/new (agent -> client).
        type NewSessionResult =
            { sessionId : SessionId }

        /// Domain-level result for session/load (agent -> client).
        /// JSON-RPC result is structurally empty; we reattach sessionId from the request.
        type LoadSessionResult =
            { sessionId : SessionId }

    // -------------
    // Session context (R3, R16)
    // -------------

    module SessionContext =

        open PrimitivesAndParties
        open Initialization

        /// Per-session state container; the turnState is protocol-specific.
        type SessionState<'turnState> =
            { sessionId : SessionId
              turnState : 'turnState }

        /// Connection-level initialized context: negotiated handshake + session table.
        type InitializedContext<'turnState> =
            { clientInit : InitializeParams
              agentInit  : InitializeResult
              sessions   : Map<SessionId, SessionState<'turnState>> }

    // -------------
    // Metadata (R17)
    // -------------

    module Metadata =

        /// R17 - opaque metadata carrier preserved by the runtime.
        [<Struct>]
        type MetaEnvelope =
            | MetaEnvelope of obj  // Runtime-specific; sentinel must not rely on shape unless profile enables it.

        /// R17/R19 - metadata and transport policy knobs. Keep opaque to the protocol core.
        [<RequireQualifiedAccess>]
        type MetadataPolicy =
            | Disallow                // Strip metadata entirely.
            | AllowOpaque             // Preserve but treat as opaque.
            | AllowKinds of string list // Allowlisted kinds (profile-owned schema).

        /// R19 - transport defaults surfaced to validation.
        type TransportDefaults =
            { lineSeparator   : string option   // e.g., "\n" vs "\r\n"
              maxFrameBytes   : int option      // framing chunk limit
              maxMessageBytes : int option      // logical JSON-RPC message budget
              metaEnvelope    : MetaEnvelope option }

        /// Runtime profile for metadata/transport handling.
        type RuntimeProfile =
            { metadata  : MetadataPolicy
              transport : TransportDefaults option }

    // -------------
    // Prompt turns & updates (R4, R5, R6, R10, R15)
    // -------------

    module Prompting =

        open PrimitivesAndParties

        /// A resource that the client can read (usually a file URI).
        type ResourceLink =
            { uri      : string
              mimeType : string option }

        /// Content blocks used in prompts and session updates (subset of ACP ContentBlock).
        [<RequireQualifiedAccess>]
        type ContentBlock =
            | Text of string
            | Resource of ResourceLink
            /// Escape hatch for content kinds not modeled yet (images, audio, embedded contexts, ...).
            | Other of kind : string * raw : obj
            // TODO: connect ContentBlock.Other + Metadata.MetaEnvelope into a profile system so that
            // validation rules remain agnostic to raw metadata unless explicitly enabled.

        /// Params for session/prompt (client -> agent).
        type SessionPromptParams =
            { sessionId : SessionId
              content   : ContentBlock list }

        /// Result for session/prompt (agent -> client). This mirrors the ACP wire type.
        type SessionPromptResult =
            { sessionId  : SessionId
              stopReason : StopReason }

        /// R11 - domain-level error outcome (non-wire, for sentinel and UTS).
        [<RequireQualifiedAccess>]
        type DomainErrorOutcome =
            | ProtocolViolation of ProtocolErrorCode : string * details : string
            | AgentInternalFailure of details : string
            | ClientInternalFailure of details : string
            | ToolingFailure of toolName : string * details : string

        /// R10/R11 - canonical prompt turn outcome classification.
        [<RequireQualifiedAccess>]
        type PromptTurnOutcome =
            | Completed of stopReason : StopReason
            | CancelledByUser
            | DomainError of DomainErrorOutcome

        /// Plan entry priority in session/update (low/medium/high).
        [<RequireQualifiedAccess>]
        type PlanEntryPriority =
            | Low
            | Medium
            | High
            | Other of string

        /// Plan entry status in session/update.
        [<RequireQualifiedAccess>]
        type PlanEntryStatus =
            | Pending
            | InProgress
            | Completed
            | Cancelled
            | Failed
            | Other of string

        type PlanEntry =
            { content  : string
              priority : PlanEntryPriority
              status   : PlanEntryStatus }

        /// Execution status of a tool call reported via session/update.
        [<RequireQualifiedAccess>]
        type ToolCallStatus =
            | Requested
            | InProgress
            | Completed
            | Cancelled
            | Failed
            | Other of string

        /// Coarse tool kinds; keep open for extension.
        [<RequireQualifiedAccess>]
        type ToolKind =
            | FileSystem
            | Terminal
            | Mcp
            | Custom of string

        /// Tool call update embedded in session/update and request_permission.
        type ToolCallUpdate =
            { toolCallId : string
              title      : string option
              kind       : ToolKind option
              status     : ToolCallStatus
              content    : ContentBlock list }

        /// Different types of updates sent during session processing.
        [<RequireQualifiedAccess>]
        type SessionUpdate =
            | Plan of PlanEntry list
            | UserMessageChunk of ContentBlock
            | AgentMessageChunk of ContentBlock
            | ToolCall of ToolCallUpdate
            | StatusText of string
            | Other of kind : string * raw : obj

        type SessionUpdateNotification =
            { sessionId : SessionId
              update    : SessionUpdate }

        /// Params for session/cancel (client -> agent).
        type SessionCancelParams =
            { sessionId : SessionId }

        /// Kind of permission option presented to the user.
        [<RequireQualifiedAccess>]
        type PermissionOptionKind =
            | AllowOnce
            | AllowAlways
            | RejectOnce
            | RejectAlways
            | Other of string

        type PermissionOptionId = string

        /// Option presented to the user when requesting permission.
        type PermissionOption =
            { optionId : PermissionOptionId
              name     : string
              kind     : PermissionOptionKind }

        /// Params for session/request_permission (agent -> client).
        type RequestPermissionParams =
            { sessionId : SessionId
              toolCall  : ToolCallUpdate
              options   : PermissionOption list }

    // -------------
    // Message envelopes (decoded JSON-RPC)
    // -------------

    module Messaging =

        open Initialization
        open SessionSetup
        open Prompting

        /// Methods and notifications originating at the client.
        [<RequireQualifiedAccess>]
        type ClientToAgentMessage =
            | Initialize of InitializeParams
            | SessionNew of NewSessionParams
            | SessionLoad of LoadSessionParams
            | SessionPrompt of SessionPromptParams
            | SessionCancel of SessionCancelParams
            // Future: authenticate, request_permission result, fs/*, terminal/*, ...

        /// Methods and notifications originating at the agent.
        [<RequireQualifiedAccess>]
        type AgentToClientMessage =
            | InitializeResult of InitializeResult
            | SessionNewResult of NewSessionResult
            | SessionLoadResult of LoadSessionResult
            | SessionPromptResult of SessionPromptResult
            | SessionUpdate of SessionUpdateNotification
            | RequestPermission of RequestPermissionParams

        /// Direction-tagged domain message stream for a single connection.
        [<RequireQualifiedAccess>]
        type Message =
            | FromClient of ClientToAgentMessage
            | FromAgent of AgentToClientMessage

    // -------------
    // Tracing (R16)
    // -------------

    module Tracing =

        open PrimitivesAndParties
        open Messaging

        /// R16 - full session trace, the event history for invariants. Message list is ordered as observed on the connection.
        /// Derived turn views can be layered on top (from Messaging.Message) without changing wire types.
        type SessionTrace =
            { sessionId : SessionId
              messages  : Message list }
