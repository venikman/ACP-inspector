namespace Acp.Contrib

open System
open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Prompting

/// Tool call tracker for monitoring tool call lifecycle.
/// Similar to Python SDK's tool call tracking functionality.
module ToolCalls =

    /// Raised when the tracker receives notifications from a different session.
    exception ToolCallSessionMismatchError of expected: string * actual: string

    /// Immutable view of a tool call.
    type ToolCallView =
        { toolCallId: string
          title: string
          kind: ToolKind
          status: ToolCallStatus
          content: ToolCallContent list
          locations: ToolCallLocation list
          rawInput: System.Text.Json.Nodes.JsonNode option
          rawOutput: System.Text.Json.Nodes.JsonNode option }

    /// Mutable tool call state for tracking.
    type private MutableToolCallState =
        { mutable title: string
          mutable kind: ToolKind
          mutable status: ToolCallStatus
          mutable content: ToolCallContent list
          mutable locations: ToolCallLocation list
          mutable rawInput: System.Text.Json.Nodes.JsonNode option
          mutable rawOutput: System.Text.Json.Nodes.JsonNode option }

    /// Track tool calls from session notifications with status filtering.
    type ToolCallTracker(?autoResetOnSessionChange: bool) =
        let autoReset = defaultArg autoResetOnSessionChange true

        let mutable sessionId: SessionId option = None
        let mutable toolCallOrder: string list = []
        let mutable toolCalls: Map<string, MutableToolCallState> = Map.empty

        let mutable subscribers: (ToolCallView list -> SessionUpdateNotification -> unit) list =
            []

        let toView (id: string) (state: MutableToolCallState) : ToolCallView =
            { toolCallId = id
              title = state.title
              kind = state.kind
              status = state.status
              content = state.content
              locations = state.locations
              rawInput = state.rawInput
              rawOutput = state.rawOutput }

        let allViews () : ToolCallView list =
            toolCallOrder
            |> List.choose (fun id -> toolCalls |> Map.tryFind id |> Option.map (toView id))

        let notifySubscribers (notification: SessionUpdateNotification) =
            let views = allViews ()

            for callback in subscribers do
                callback views notification

        let ensureSession (notificationSessionId: SessionId) =
            match sessionId with
            | None -> sessionId <- Some notificationSessionId
            | Some currentId when currentId = notificationSessionId -> ()
            | Some currentId ->
                if autoReset then
                    toolCallOrder <- []
                    toolCalls <- Map.empty
                    sessionId <- Some notificationSessionId
                else
                    raise (
                        ToolCallSessionMismatchError(SessionId.value currentId, SessionId.value notificationSessionId)
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

            if not (toolCalls |> Map.containsKey tc.toolCallId) then
                toolCallOrder <- toolCallOrder @ [ tc.toolCallId ]

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

                toolCallOrder <- toolCallOrder @ [ update.toolCallId ]
                toolCalls <- toolCalls |> Map.add update.toolCallId state

        /// Current session ID if any notifications have been processed.
        member _.SessionId = sessionId

        /// Apply a session notification.
        member _.Apply(notification: SessionUpdateNotification) =
            ensureSession notification.sessionId

            match notification.update with
            | SessionUpdate.ToolCall tc -> applyToolCall tc
            | SessionUpdate.ToolCallUpdate update -> applyToolCallUpdate update
            | _ -> () // Ignore non-tool-call updates

            notifySubscribers notification

        /// Get all tool calls in order.
        member _.All() : ToolCallView list = allViews ()

        /// Get a specific tool call by ID.
        member _.TryGet(toolCallId: string) : ToolCallView option =
            toolCalls |> Map.tryFind toolCallId |> Option.map (toView toolCallId)

        /// Get all pending tool calls.
        member _.Pending() : ToolCallView list =
            allViews () |> List.filter (fun v -> v.status = ToolCallStatus.Pending)

        /// Get all in-progress tool calls.
        member _.InProgress() : ToolCallView list =
            allViews () |> List.filter (fun v -> v.status = ToolCallStatus.InProgress)

        /// Get all completed tool calls.
        member _.Completed() : ToolCallView list =
            allViews () |> List.filter (fun v -> v.status = ToolCallStatus.Completed)

        /// Get all failed tool calls.
        member _.Failed() : ToolCallView list =
            allViews () |> List.filter (fun v -> v.status = ToolCallStatus.Failed)

        /// Check if any tool calls are in progress.
        member _.HasInProgress() : bool =
            toolCalls
            |> Map.exists (fun _ state -> state.status = ToolCallStatus.InProgress)

        /// Clear all tracked tool calls.
        member _.Reset() =
            sessionId <- None
            toolCallOrder <- []
            toolCalls <- Map.empty

        /// Subscribe to tool call updates. Returns unsubscribe function.
        member _.Subscribe(callback: ToolCallView list -> SessionUpdateNotification -> unit) : unit -> unit =
            subscribers <- subscribers @ [ callback ]

            fun () -> subscribers <- subscribers |> List.filter (fun c -> not (Object.ReferenceEquals(c, callback)))
