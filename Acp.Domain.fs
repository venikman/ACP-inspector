namespace Acp

open System

/// Domain model for a core slice of the Agent Client Protocol (ACP).
/// JSON-RPC 2.0 framing and raw JSON live elsewhere; this is "what ACP means".
[<RequireQualifiedAccess>]
module Domain =

    // -------------
    // Primitives
    // -------------

    /// MAJOR protocol version negotiated during initialization.
    type ProtocolVersion = int

    /// Opaque server-chosen session identifier.
    [<Struct>]
    type SessionId = SessionId of string

    [<RequireQualifiedAccess>]
    module SessionId =
        let value (SessionId s) = s

    /// Reasons why an agent stops processing a prompt turn.
    /// Variants mirror the ACP StopReason enum, with an open "Other" for forwards-compat. :contentReference[oaicite:0]{index=0}
    [<RequireQualifiedAccess>]
    type StopReason =
        | EndTurn
        | MaxTokens
        | MaxTurnRequests
        | Refusal
        | Cancelled
        | Other of string

    // -------------
    // Capabilities
    // -------------

    /// File system capabilities supported by the client. :contentReference[oaicite:1]{index=1}
    type FileSystemCapabilities =
        { readTextFile  : bool
          writeTextFile : bool }

    /// Capabilities advertised by the client in initialize. :contentReference[oaicite:2]{index=2}
    type ClientCapabilities =
        { fs       : FileSystemCapabilities
          terminal : bool }

    /// MCP transport capabilities supported by the agent. :contentReference[oaicite:3]{index=3}
    type McpCapabilities =
        { http : bool
          sse  : bool }

    /// Non-baseline prompt content types the agent can process. :contentReference[oaicite:4]{index=4}
    type PromptCapabilities =
        { audio           : bool
          image           : bool
          embeddedContext : bool }

    /// Capabilities advertised by the agent during initialize. :contentReference[oaicite:5]{index=5}
    type AgentCapabilities =
        { loadSession        : bool
          mcpCapabilities    : McpCapabilities
          promptCapabilities : PromptCapabilities }

    /// Implementation metadata for clients and agents. :contentReference[oaicite:6]{index=6}
    type ImplementationInfo =
        { name    : string
          title   : string option
          version : string option }

    // -------------
    // Initialization
    // -------------

    /// Params for initialize (client → agent). :contentReference[oaicite:7]{index=7}
    type InitializeParams =
        { protocolVersion    : ProtocolVersion
          clientCapabilities : ClientCapabilities
          clientInfo         : ImplementationInfo option }

    /// Result for initialize (agent → client). :contentReference[oaicite:8]{index=8}
    type InitializeResult =
        { negotiatedVersion : ProtocolVersion
          agentCapabilities : AgentCapabilities
          agentInfo         : ImplementationInfo option }

    // -------------
    // MCP session wiring (minimal)
    // -------------

    /// Environment variable passed to an MCP server. :contentReference[oaicite:9]{index=9}
    type McpServerEnvVar =
        { name  : string
          value : string }

    /// Minimal MCP server configuration used in session/new & session/load.
    type McpServer =
        { name    : string
          command : string
          args    : string list
          env     : McpServerEnvVar list }

    // -------------
    // Session setup
    // -------------

    /// Params for session/new (client → agent). :contentReference[oaicite:10]{index=10}
    type NewSessionParams =
        { cwd        : string
          mcpServers : McpServer list }

    /// Params for session/load (client → agent). :contentReference[oaicite:11]{index=11}
    type LoadSessionParams =
        { sessionId  : SessionId
          cwd        : string
          mcpServers : McpServer list }

    /// Result for session/new (agent → client). :contentReference[oaicite:12]{index=12}
    type NewSessionResult =
        { sessionId : SessionId }

    /// Domain-level result for session/load (agent → client).
    /// JSON-RPC result is structurally empty; we reattach sessionId from the request. :contentReference[oaicite:13]{index=13}
    type LoadSessionResult =
        { sessionId : SessionId }

    // -------------
    // Content blocks (core subset)
    // -------------

    /// A resource that the client can read (usually a file URI). :contentReference[oaicite:14]{index=14}
    type ResourceLink =
        { uri      : string
          mimeType : string option }

    /// Content blocks used in prompts and session updates (subset of ACP ContentBlock). :contentReference[oaicite:15]{index=15}
    [<RequireQualifiedAccess>]
    type ContentBlock =
        | Text of string
        | Resource of ResourceLink
        /// Escape hatch for content kinds not modeled yet (images, audio, embedded contexts, ...).
        | Other of kind : string * raw : obj

    // -------------
    // Prompt turns
    // -------------

    /// Params for session/prompt (client → agent). :contentReference[oaicite:16]{index=16}
    type SessionPromptParams =
        { sessionId : SessionId
          content   : ContentBlock list }

    /// Result for session/prompt (agent → client). :contentReference[oaicite:17]{index=17}
    type SessionPromptResult =
        { sessionId  : SessionId
          stopReason : StopReason }

    // -------------
    // Plans & tool calls (lightweight)
    // -------------

    /// Plan entry priority in session/update (low/medium/high). :contentReference[oaicite:18]{index=18}
    [<RequireQualifiedAccess>]
    type PlanEntryPriority =
        | Low
        | Medium
        | High
        | Other of string

    /// Plan entry status in session/update. :contentReference[oaicite:19]{index=19}
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

    /// Execution status of a tool call reported via session/update. :contentReference[oaicite:20]{index=20}
    [<RequireQualifiedAccess>]
    type ToolCallStatus =
        | Requested
        | InProgress
        | Completed
        | Cancelled
        | Failed
        | Other of string

    /// Coarse tool kinds; keep open for extension. :contentReference[oaicite:21]{index=21}
    [<RequireQualifiedAccess>]
    type ToolKind =
        | FileSystem
        | Terminal
        | Mcp
        | Custom of string

    /// Tool call update embedded in session/update and request_permission. :contentReference[oaicite:22]{index=22}
    type ToolCallUpdate =
        { toolCallId : string
          title      : string option
          kind       : ToolKind option
          status     : ToolCallStatus
          content    : ContentBlock list }

    /// Different types of updates sent during session processing. :contentReference[oaicite:23]{index=23}
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

    // -------------
    // Cancellation
    // -------------

    /// Params for session/cancel (client → agent). :contentReference[oaicite:24]{index=24}
    type SessionCancelParams =
        { sessionId : SessionId }

    // -------------
    // Permissions
    // -------------

    /// Kind of permission option presented to the user. :contentReference[oaicite:25]{index=25}
    [<RequireQualifiedAccess>]
    type PermissionOptionKind =
        | AllowOnce
        | AllowAlways
        | RejectOnce
        | RejectAlways
        | Other of string

    type PermissionOptionId = string

    /// Option presented to the user when requesting permission. :contentReference[oaicite:26]{index=26}
    type PermissionOption =
        { optionId : PermissionOptionId
          name     : string
          kind     : PermissionOptionKind }

    /// Params for session/request_permission (agent → client). :contentReference[oaicite:27]{index=27}
    type RequestPermissionParams =
        { sessionId : SessionId
          toolCall  : ToolCallUpdate
          options   : PermissionOption list }

    // -------------
    // Message envelopes (decoded JSON-RPC)
    // -------------

    /// Methods and notifications originating at the client. :contentReference[oaicite:28]{index=28}
    [<RequireQualifiedAccess>]
    type ClientToAgentMessage =
        | Initialize of InitializeParams
        | SessionNew of NewSessionParams
        | SessionLoad of LoadSessionParams
        | SessionPrompt of SessionPromptParams
        | SessionCancel of SessionCancelParams
        // Future: authenticate, request_permission result, fs/*, terminal/*, ...

    /// Methods and notifications originating at the agent. :contentReference[oaicite:29]{index=29}
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
