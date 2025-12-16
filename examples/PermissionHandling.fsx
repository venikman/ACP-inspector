#!/usr/bin/env dotnet fsi
/// Permission Handling Example
///
/// Demonstrates how to use PermissionBroker to manage permission
/// requests with manual responses and auto-response rules.

#r "../src/bin/Debug/net10.0/ACP.dll"

open System
open System.Threading.Tasks
open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Prompting
open Acp.Contrib.Permissions

// ─────────────────────────────────────────────────────────────────────────────
// Helper Functions
// ─────────────────────────────────────────────────────────────────────────────

let sessionId = SessionId "session-789"

let makePermissionRequest toolCallId title kind : RequestPermissionParams =
    { sessionId = sessionId
      toolCall =
        { toolCallId = toolCallId
          title = Some title
          kind = Some kind
          status = Some ToolCallStatus.Pending
          content = None
          locations = None
          rawInput = None
          rawOutput = None }
      options =
        [ { optionId = "allow-once"
            name = "Allow Once"
            kind = PermissionOptionKind.AllowOnce }
          { optionId = "allow-always"
            name = "Allow Always"
            kind = PermissionOptionKind.AllowAlways }
          { optionId = "reject-once"
            name = "Reject Once"
            kind = PermissionOptionKind.RejectOnce }
          { optionId = "reject-always"
            name = "Reject Always"
            kind = PermissionOptionKind.RejectAlways } ] }

// ─────────────────────────────────────────────────────────────────────────────
// Part 1: Manual Permission Handling
// ─────────────────────────────────────────────────────────────────────────────

printfn "=== Permission Handling Example ==="
printfn ""
printfn "--- Part 1: Manual Permission Handling ---"
printfn ""

let broker = PermissionBroker()

// Subscribe to events
let unsubRequests =
    broker.SubscribeToRequests(fun req -> printfn $"  [Event] New request: {req.request.toolCall.title}")

let unsubResponses =
    broker.SubscribeToResponses(fun resp ->
        let outcome =
            if resp.wasCancelled then
                "CANCELLED"
            elif resp.wasAutoResponded then
                $"AUTO: {resp.selectedOptionId}"
            else
                resp.selectedOptionId

        printfn $"  [Event] Response: {outcome}")

// Enqueue some requests
printfn "Enqueueing permission requests..."

let req1 =
    broker.Enqueue(makePermissionRequest "tc1" "Execute: rm -rf /tmp/cache" ToolKind.Execute)

let req2 =
    broker.Enqueue(makePermissionRequest "tc2" "Read: ~/.ssh/id_rsa" ToolKind.Read)

let req3 =
    broker.Enqueue(makePermissionRequest "tc3" "Edit: config.json" ToolKind.Edit)

printfn ""
printfn $"Pending requests: {broker.PendingRequests().Length}"
printfn ""

// Show pending
printfn "Pending permission requests:"

for pending in broker.PendingRequests() do
    printfn $"  [{pending.requestId}] {pending.request.toolCall.title}"

printfn ""

// Respond to requests
printfn "Responding to requests..."

match broker.Respond(req1, "reject-once") with
| Ok _ -> printfn $"  Rejected: {req1}"
| Error e -> printfn $"  Error: {e}"

match broker.Respond(req2, "allow-once") with
| Ok _ -> printfn $"  Allowed: {req2}"
| Error e -> printfn $"  Error: {e}"

// Cancel the third
if broker.Cancel(req3) then
    printfn $"  Cancelled: {req3}"

printfn ""
printfn $"Pending after responses: {broker.PendingRequests().Length}"
printfn ""

// Show history
printfn "Response history:"

for resp in broker.ResponseHistory() do
    let status =
        if resp.wasCancelled then
            "Cancelled"
        else
            $"Selected: {resp.selectedOptionId}"

    printfn $"  [{resp.requestId}] {status}"

printfn ""

// ─────────────────────────────────────────────────────────────────────────────
// Part 2: Auto-Response Rules
// ─────────────────────────────────────────────────────────────────────────────

printfn "--- Part 2: Auto-Response Rules ---"
printfn ""

broker.Reset()

// Add auto-response rules
printfn "Adding auto-response rules..."

// Always allow Read operations
let rule1 =
    broker.AddAutoRule(fun req -> req.toolCall.kind = Some ToolKind.Read, "allow-always")

printfn "  Rule 1: Auto-allow all Read operations"

// Always reject Execute operations (safety!)
let rule2 =
    broker.AddAutoRule(fun req -> req.toolCall.kind = Some ToolKind.Execute, "reject-once")

printfn "  Rule 2: Auto-reject all Execute operations"

printfn ""

// Test auto-responses
printfn "Testing auto-response rules..."
printfn ""

let testReq1 =
    broker.Enqueue(makePermissionRequest "tc4" "Read: package.json" ToolKind.Read)

printfn $"  Read request → Pending: {broker.HasPending()}"

let testReq2 =
    broker.Enqueue(makePermissionRequest "tc5" "Execute: npm install" ToolKind.Execute)

printfn $"  Execute request → Pending: {broker.HasPending()}"

let testReq3 =
    broker.Enqueue(makePermissionRequest "tc6" "Edit: README.md" ToolKind.Edit)

printfn $"  Edit request → Pending: {broker.HasPending()}"

printfn ""

// Show what happened
printfn "Auto-response results:"

for resp in broker.ResponseHistory() do
    let auto = if resp.wasAutoResponded then " (auto)" else ""
    printfn $"  {resp.request.toolCall.title} → {resp.selectedOptionId}{auto}"

printfn ""

// The Edit request should still be pending
printfn $"Remaining pending: {broker.PendingRequests().Length}"

for pending in broker.PendingRequests() do
    printfn $"  - {pending.request.toolCall.title}"

printfn ""

// Remove a rule and test again
printfn "Removing Execute reject rule..."
broker.RemoveAutoRule(rule2)

let testReq4 =
    broker.Enqueue(makePermissionRequest "tc7" "Execute: dotnet build" ToolKind.Execute)

printfn $"  Execute request now → Pending: {broker.HasPending()}"
printfn $"  Pending count: {broker.PendingRequests().Length}"

// Cleanup
unsubRequests ()
unsubResponses ()

printfn ""
printfn "=== Example Complete ==="
