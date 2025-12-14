namespace Acp.Contrib

open System
open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Prompting
open Acp.Domain.SessionModes

/// Session state accumulator for merging session notifications into snapshots.
/// Similar to Python SDK's SessionAccumulator in acp.contrib.session_state.
module SessionState =

    /// Raised when the accumulator receives notifications from a different session.
    exception SessionNotificationMismatchError of expected: string * actual: string

    /// Raised when a session snapshot is requested before any notifications.
    exception SessionSnapshotUnavailableError

    /// Immutable view of a tool call in the session.
    type ToolCallView =
        { toolCallId: string
          title: string
          kind: ToolKind
          status: ToolCallStatus
          content: ToolCallContent list
          locations: ToolCallLocation list
          rawInput: System.Text.Json.Nodes.JsonNode option
          rawOutput: System.Text.Json.Nodes.JsonNode option }

    /// Immutable snapshot of the current session state.
    type SessionSnapshot =
        { sessionId: SessionId
          toolCalls: Map<string, ToolCallView>
          planEntries: PlanEntry list
          currentModeId: SessionModeId option
          availableCommands: AvailableCommand list
          userMessages: ContentChunk list
          agentMessages: ContentChunk list
          agentThoughts: ContentChunk list }

    /// Mutable tool call state for accumulation.
    type private MutableToolCallState =
        { mutable title: string
          mutable kind: ToolKind
          mutable status: ToolCallStatus
          mutable content: ToolCallContent list
          mutable locations: ToolCallLocation list
          mutable rawInput: System.Text.Json.Nodes.JsonNode option
          mutable rawOutput: System.Text.Json.Nodes.JsonNode option }

    /// Merge session notifications into a session snapshot.
    type SessionAccumulator(?autoResetOnSessionChange: bool) =
        let autoReset = defaultArg autoResetOnSessionChange true

        let mutable sessionId: SessionId option = None
        let mutable toolCalls: Map<string, MutableToolCallState> = Map.empty
        let mutable planEntries: PlanEntry list = []
        let mutable currentModeId: SessionModeId option = None
        let mutable availableCommands: AvailableCommand list = []
        let mutable userMessages: ContentChunk list = []
        let mutable agentMessages: ContentChunk list = []
        let mutable agentThoughts: ContentChunk list = []

        let mutable subscribers: (SessionSnapshot -> SessionUpdateNotification -> unit) list = []

        let notifySubscribers (snapshot: SessionSnapshot) (notification: SessionUpdateNotification) =
            for callback in subscribers do
                callback snapshot notification

        let ensureSession (notificationSessionId: SessionId) =
            match sessionId with
            | None -> sessionId <- Some notificationSessionId
            | Some currentId when currentId = notificationSessionId -> ()
            | Some currentId ->
                if autoReset then
                    // Reset and set new session
                    toolCalls <- Map.empty
                    planEntries <- []
                    currentModeId <- None
                    availableCommands <- []
                    userMessages <- []
                    agentMessages <- []
                    agentThoughts <- []
                    sessionId <- Some notificationSessionId
                else
                    raise (
                        SessionNotificationMismatchError(SessionId.value currentId, SessionId.value notificationSessionId)
                    )

        let applyToolCall (tc: ToolCall) =
            let state =
                { title = tc.title
                  kind = tc.kind
                  status = tc.status
                  content = tc.content
                  locations = tc.locations
                  rawInput = tc.rawInput
                  rawOutput = tc.rawOutput }

            toolCalls <- toolCalls |> Map.add tc.toolCallId state

        let applyToolCallUpdate (update: ToolCallUpdate) =
            match toolCalls |> Map.tryFind update.toolCallId with
            | Some state ->
                update.title |> Option.iter (fun v -> state.title <- v)
                update.kind |> Option.iter (fun v -> state.kind <- v)
                update.status |> Option.iter (fun v -> state.status <- v)
                update.content |> Option.iter (fun v -> state.content <- v)
                update.locations |> Option.iter (fun v -> state.locations <- v)

                if update.rawInput.IsSome then
                    state.rawInput <- update.rawInput

                if update.rawOutput.IsSome then
                    state.rawOutput <- update.rawOutput
            | None ->
                // Create new entry from update
                let state =
                    { title = update.title |> Option.defaultValue ""
                      kind = update.kind |> Option.defaultValue ToolKind.Other
                      status = update.status |> Option.defaultValue ToolCallStatus.Pending
                      content = update.content |> Option.defaultValue []
                      locations = update.locations |> Option.defaultValue []
                      rawInput = update.rawInput
                      rawOutput = update.rawOutput }

                toolCalls <- toolCalls |> Map.add update.toolCallId state

        let applyUpdate (update: SessionUpdate) =
            match update with
            | SessionUpdate.UserMessageChunk chunk -> userMessages <- userMessages @ [ chunk ]
            | SessionUpdate.AgentMessageChunk chunk -> agentMessages <- agentMessages @ [ chunk ]
            | SessionUpdate.AgentThoughtChunk chunk -> agentThoughts <- agentThoughts @ [ chunk ]
            | SessionUpdate.ToolCall tc -> applyToolCall tc
            | SessionUpdate.ToolCallUpdate update -> applyToolCallUpdate update
            | SessionUpdate.Plan plan -> planEntries <- plan.entries
            | SessionUpdate.AvailableCommandsUpdate update -> availableCommands <- update.availableCommands
            | SessionUpdate.CurrentModeUpdate update -> currentModeId <- Some update.currentModeId

        let createSnapshot () : SessionSnapshot =
            match sessionId with
            | None -> raise SessionSnapshotUnavailableError
            | Some sid ->
                let toolCallViews =
                    toolCalls
                    |> Map.map (fun _ state ->
                        { toolCallId = ""
                          title = state.title
                          kind = state.kind
                          status = state.status
                          content = state.content
                          locations = state.locations
                          rawInput = state.rawInput
                          rawOutput = state.rawOutput })
                    |> Map.toList
                    |> List.map (fun (id, view) -> (id, { view with toolCallId = id }))
                    |> Map.ofList

                { sessionId = sid
                  toolCalls = toolCallViews
                  planEntries = List.map id planEntries
                  currentModeId = currentModeId
                  availableCommands = List.map id availableCommands
                  userMessages = List.map id userMessages
                  agentMessages = List.map id agentMessages
                  agentThoughts = List.map id agentThoughts }

        /// Current session ID if any notifications have been processed.
        member _.SessionId = sessionId

        /// Apply a session notification and return the updated snapshot.
        member _.Apply(notification: SessionUpdateNotification) : SessionSnapshot =
            ensureSession notification.sessionId
            applyUpdate notification.update
            let snapshot = createSnapshot ()
            notifySubscribers snapshot notification
            snapshot

        /// Get current snapshot without applying new notifications.
        member _.Snapshot() : SessionSnapshot = createSnapshot ()

        /// Clear all accumulated state.
        member _.Reset() =
            sessionId <- None
            toolCalls <- Map.empty
            planEntries <- []
            currentModeId <- None
            availableCommands <- []
            userMessages <- []
            agentMessages <- []
            agentThoughts <- []

        /// Subscribe to snapshot updates. Returns unsubscribe function.
        member _.Subscribe(callback: SessionSnapshot -> SessionUpdateNotification -> unit) : unit -> unit =
            subscribers <- subscribers @ [ callback ]

            fun () -> subscribers <- subscribers |> List.filter (fun c -> not (Object.ReferenceEquals(c, callback)))
