#!/usr/bin/env dotnet fsi
/// Session State Tracking Example
///
/// Demonstrates how to use SessionAccumulator to merge streaming
/// session notifications into coherent snapshots.

#r "../src/bin/Debug/net10.0/ACP.dll"

open System
open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Prompting
open Acp.Domain.SessionModes
open Acp.Contrib.SessionState

// ─────────────────────────────────────────────────────────────────────────────
// Simulated Session Notifications
// ─────────────────────────────────────────────────────────────────────────────

let sessionId = SessionId "session-123"

/// Simulate a stream of notifications that would come from an agent
let simulatedNotifications =
    [ // Agent starts thinking
      { sessionId = sessionId
        update =
          SessionUpdate.AgentThoughtChunk
              { content = ContentBlock.Text { text = "Let me analyze the request..."; annotations = None } } }

      // First tool call starts
      { sessionId = sessionId
        update =
          SessionUpdate.ToolCall
              { toolCallId = "tc-001"
                title = "Reading file"
                kind = ToolKind.Read
                status = ToolCallStatus.InProgress
                content = []
                locations = [ { path = "src/main.fs"; line = Some 1 } ]
                rawInput = None
                rawOutput = None } }

      // Tool call completes
      { sessionId = sessionId
        update =
          SessionUpdate.ToolCallUpdate
              { toolCallId = "tc-001"
                title = Some "Read src/main.fs"
                kind = None
                status = Some ToolCallStatus.Completed
                content = None
                locations = None
                rawInput = None
                rawOutput = None } }

      // Second tool call
      { sessionId = sessionId
        update =
          SessionUpdate.ToolCall
              { toolCallId = "tc-002"
                title = "Editing file"
                kind = ToolKind.Edit
                status = ToolCallStatus.InProgress
                content = []
                locations = [ { path = "src/main.fs"; line = Some 42 } ]
                rawInput = None
                rawOutput = None } }

      // Mode change
      { sessionId = sessionId
        update = SessionUpdate.CurrentModeUpdate { currentModeId = SessionModeId "code-review" } }

      // Second tool completes
      { sessionId = sessionId
        update =
          SessionUpdate.ToolCallUpdate
              { toolCallId = "tc-002"
                title = Some "Edited src/main.fs:42"
                kind = None
                status = Some ToolCallStatus.Completed
                content = None
                locations = None
                rawInput = None
                rawOutput = None } }

      // Agent sends message
      { sessionId = sessionId
        update =
          SessionUpdate.AgentMessageChunk
              { content =
                  ContentBlock.Text
                      { text = "I've reviewed and updated the file."
                        annotations = None } } } ]

// ─────────────────────────────────────────────────────────────────────────────
// Main Example
// ─────────────────────────────────────────────────────────────────────────────

printfn "=== Session State Tracking Example ==="
printfn ""

// Create accumulator
let accumulator = SessionAccumulator()

// Subscribe to updates
let unsubscribe =
    accumulator.Subscribe(fun snapshot notification ->
        printfn $"[Update] Tool calls: {snapshot.toolCalls.Count}, Mode: {snapshot.currentModeId}")

// Process notifications
printfn "Processing %d notifications..." simulatedNotifications.Length
printfn ""

for notification in simulatedNotifications do
    let snapshot = accumulator.Apply(notification)

    // Show progress
    match notification.update with
    | SessionUpdate.ToolCall tc -> printfn $"  → New tool call: {tc.title} ({tc.status})"
    | SessionUpdate.ToolCallUpdate u -> printfn $"  → Tool update: {u.toolCallId} → {u.status}"
    | SessionUpdate.CurrentModeUpdate m -> printfn $"  → Mode changed to: {SessionModeId.value m.currentModeId}"
    | SessionUpdate.AgentThoughtChunk _ -> printfn "  → Agent thinking..."
    | SessionUpdate.AgentMessageChunk _ -> printfn "  → Agent message received"
    | _ -> ()

printfn ""

// Get final snapshot
let finalSnapshot = accumulator.Snapshot()

printfn "=== Final Session State ==="
printfn $"Session ID: {SessionId.value finalSnapshot.sessionId}"
printfn $"Current Mode: {finalSnapshot.currentModeId |> Option.map SessionModeId.value |> Option.defaultValue \"(none)\"}"
printfn $"Tool Calls: {finalSnapshot.toolCalls.Count}"
printfn ""

printfn "Tool Call Details:"

for KeyValue(id, tc) in finalSnapshot.toolCalls do
    printfn $"  [{id}] {tc.title}"
    printfn $"      Kind: {tc.kind}, Status: {tc.status}"

    for loc in tc.locations do
        printfn $"      Location: {loc.path}:{loc.line |> Option.map string |> Option.defaultValue \"?\"}"

printfn ""
printfn $"Agent Thoughts: {finalSnapshot.agentThoughts.Length} chunk(s)"
printfn $"Agent Messages: {finalSnapshot.agentMessages.Length} chunk(s)"

// Cleanup
unsubscribe ()

printfn ""
printfn "=== Example Complete ==="
