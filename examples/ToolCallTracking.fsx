#!/usr/bin/env dotnet fsi
/// Tool Call Tracking Example
///
/// Demonstrates how to use ToolCallTracker to monitor tool call
/// lifecycle and filter by status.

#r "../src/bin/Debug/net9.0/ACP.dll"

open System
open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Prompting
open Acp.Contrib.ToolCalls

// ─────────────────────────────────────────────────────────────────────────────
// Helper Functions
// ─────────────────────────────────────────────────────────────────────────────

let sessionId = SessionId "session-456"

let makeToolCall id title kind status : ToolCall =
    { toolCallId = id
      title = title
      kind = kind
      status = status
      content = []
      locations = []
      rawInput = None
      rawOutput = None }

let makeUpdate id status : ToolCallUpdate =
    { toolCallId = id
      title = None
      kind = None
      status = Some status
      content = None
      locations = None
      rawInput = None
      rawOutput = None }

let notify update =
    { sessionId = sessionId
      update = update }

let printStatus (tracker: ToolCallTracker) =
    printfn
        $"  Pending: {tracker.Pending().Length}, InProgress: {tracker.InProgress().Length}, Completed: {tracker.Completed().Length}, Failed: {tracker.Failed().Length}"

// ─────────────────────────────────────────────────────────────────────────────
// Main Example
// ─────────────────────────────────────────────────────────────────────────────

printfn "=== Tool Call Tracking Example ==="
printfn ""

let tracker = ToolCallTracker()

// Subscribe to changes
let unsubscribe =
    tracker.Subscribe(fun allCalls _notification ->
        let inProgress =
            allCalls |> List.filter (fun tc -> tc.status = ToolCallStatus.InProgress)

        if not (List.isEmpty inProgress) then
            printfn $"  [Live] {inProgress.Length} tool(s) running...")

// Simulate tool call lifecycle
printfn "1. Starting three tool calls..."

tracker.Apply(
    notify (SessionUpdate.ToolCall(makeToolCall "tc1" "Read config.json" ToolKind.Read ToolCallStatus.Pending))
)

tracker.Apply(
    notify (SessionUpdate.ToolCall(makeToolCall "tc2" "Search for errors" ToolKind.Search ToolCallStatus.Pending))
)

tracker.Apply(
    notify (SessionUpdate.ToolCall(makeToolCall "tc3" "Execute build" ToolKind.Execute ToolCallStatus.Pending))
)

printStatus tracker
printfn ""

printfn "2. First two start executing..."
tracker.Apply(notify (SessionUpdate.ToolCallUpdate(makeUpdate "tc1" ToolCallStatus.InProgress)))
tracker.Apply(notify (SessionUpdate.ToolCallUpdate(makeUpdate "tc2" ToolCallStatus.InProgress)))
printStatus tracker
printfn $"  HasInProgress: {tracker.HasInProgress()}"
printfn ""

printfn "3. Read completes, execute starts..."
tracker.Apply(notify (SessionUpdate.ToolCallUpdate(makeUpdate "tc1" ToolCallStatus.Completed)))
tracker.Apply(notify (SessionUpdate.ToolCallUpdate(makeUpdate "tc3" ToolCallStatus.InProgress)))
printStatus tracker
printfn ""

printfn "4. Search fails, execute completes..."
tracker.Apply(notify (SessionUpdate.ToolCallUpdate(makeUpdate "tc2" ToolCallStatus.Failed)))
tracker.Apply(notify (SessionUpdate.ToolCallUpdate(makeUpdate "tc3" ToolCallStatus.Completed)))
printStatus tracker
printfn ""

// Query results
printfn "=== Final Results ==="
printfn ""

printfn "All tool calls (in order):"

for tc in tracker.All() do
    let statusIcon =
        match tc.status with
        | ToolCallStatus.Completed -> "✓"
        | ToolCallStatus.Failed -> "✗"
        | ToolCallStatus.InProgress -> "⋯"
        | ToolCallStatus.Pending -> "○"

    printfn $"  {statusIcon} [{tc.toolCallId}] {tc.title} ({tc.kind})"

printfn ""

printfn "Completed tools:"

for tc in tracker.Completed() do
    printfn $"  - {tc.title}"

printfn ""

printfn "Failed tools:"

for tc in tracker.Failed() do
    printfn $"  - {tc.title}"

printfn ""

// Lookup specific tool
match tracker.TryGet("tc2") with
| Some tc -> printfn $"Tool tc2: {tc.title} - Status: {tc.status}"
| None -> printfn "Tool tc2 not found"

unsubscribe ()

printfn ""
printfn "=== Example Complete ==="
