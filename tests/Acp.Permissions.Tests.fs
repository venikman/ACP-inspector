module Acp.Permissions.Tests

open System
open System.Threading.Tasks
open Xunit
open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Prompting
open Acp.Contrib.Permissions

let sessionId = SessionId "test-session"

let makeToolCallUpdate id title : ToolCallUpdate =
    { toolCallId = id
      title = Some title
      kind = Some ToolKind.Execute
      status = Some ToolCallStatus.Pending
      content = None
      locations = None
      rawInput = None
      rawOutput = None }

let makePermissionRequest toolCallId title : RequestPermissionParams =
    { sessionId = sessionId
      toolCall = makeToolCallUpdate toolCallId title
      options =
          [ { optionId = "allow-once"; name = "Allow Once"; kind = PermissionOptionKind.AllowOnce }
            { optionId = "allow-always"; name = "Allow Always"; kind = PermissionOptionKind.AllowAlways }
            { optionId = "reject-once"; name = "Reject Once"; kind = PermissionOptionKind.RejectOnce }
            { optionId = "reject-always"; name = "Reject Always"; kind = PermissionOptionKind.RejectAlways } ] }

// ─────────────────────────────────────────────────────────────────────────────
// Empty State
// ─────────────────────────────────────────────────────────────────────────────

[<Fact>]
let ``Empty broker has no pending requests`` () =
    let broker = PermissionBroker()
    Assert.Empty(broker.PendingRequests())
    Assert.False(broker.HasPending())

[<Fact>]
let ``Empty broker TryGetPending returns None`` () =
    let broker = PermissionBroker()
    Assert.True(broker.TryGetPending("nonexistent").IsNone)

// ─────────────────────────────────────────────────────────────────────────────
// Enqueue and Dequeue
// ─────────────────────────────────────────────────────────────────────────────

[<Fact>]
let ``Enqueue adds request to pending`` () =
    let broker = PermissionBroker()
    let request = makePermissionRequest "tc1" "Execute bash"

    let requestId = broker.Enqueue(request)

    Assert.True(broker.HasPending())
    Assert.Equal(1, broker.PendingRequests().Length)

    let pending = broker.TryGetPending(requestId)
    Assert.True(pending.IsSome)
    Assert.Equal("tc1", pending.Value.request.toolCall.toolCallId)

[<Fact>]
let ``Enqueue multiple requests preserves order`` () =
    let broker = PermissionBroker()

    let id1 = broker.Enqueue(makePermissionRequest "tc1" "First")
    let id2 = broker.Enqueue(makePermissionRequest "tc2" "Second")
    let id3 = broker.Enqueue(makePermissionRequest "tc3" "Third")

    let pending = broker.PendingRequests()
    Assert.Equal(3, pending.Length)
    Assert.Equal(id1, pending.[0].requestId)
    Assert.Equal(id2, pending.[1].requestId)
    Assert.Equal(id3, pending.[2].requestId)

[<Fact>]
let ``Respond removes request from pending`` () =
    let broker = PermissionBroker()
    let request = makePermissionRequest "tc1" "Execute bash"

    let requestId = broker.Enqueue(request)
    Assert.True(broker.HasPending())

    let result = broker.Respond(requestId, "allow-once")

    Assert.True(result.IsOk)
    Assert.False(broker.HasPending())
    Assert.True(broker.TryGetPending(requestId).IsNone)

[<Fact>]
let ``Respond to unknown request returns error`` () =
    let broker = PermissionBroker()

    let result = broker.Respond("unknown-id", "allow-once")

    Assert.True(result.IsError)

[<Fact>]
let ``Respond with invalid option returns error`` () =
    let broker = PermissionBroker()
    let request = makePermissionRequest "tc1" "Execute bash"

    let requestId = broker.Enqueue(request)
    let result = broker.Respond(requestId, "invalid-option")

    Assert.True(result.IsError)
    // Request should still be pending
    Assert.True(broker.HasPending())

// ─────────────────────────────────────────────────────────────────────────────
// Cancel
// ─────────────────────────────────────────────────────────────────────────────

[<Fact>]
let ``Cancel removes request from pending`` () =
    let broker = PermissionBroker()
    let request = makePermissionRequest "tc1" "Execute bash"

    let requestId = broker.Enqueue(request)
    Assert.True(broker.HasPending())

    let result = broker.Cancel(requestId)

    Assert.True(result)
    Assert.False(broker.HasPending())

[<Fact>]
let ``Cancel unknown request returns false`` () =
    let broker = PermissionBroker()

    let result = broker.Cancel("unknown-id")

    Assert.False(result)

// ─────────────────────────────────────────────────────────────────────────────
// Async Response Flow
// ─────────────────────────────────────────────────────────────────────────────

[<Fact>]
let ``WaitForResponse completes when Respond is called`` () =
    task {
        let broker = PermissionBroker()
        let request = makePermissionRequest "tc1" "Execute bash"

        let requestId = broker.Enqueue(request)

        // Start waiting in a task
        let waitTask = broker.WaitForResponseAsync(requestId)

        // Respond
        let _ = broker.Respond(requestId, "allow-once")

        // Wait should complete
        let! result = waitTask

        Assert.True(result.IsSome)
        match result.Value with
        | RequestPermissionOutcome.Selected optionId -> Assert.Equal("allow-once", optionId)
        | RequestPermissionOutcome.Cancelled -> Assert.Fail("Expected Selected, got Cancelled")
    }

[<Fact>]
let ``WaitForResponse completes with Cancelled when Cancel is called`` () =
    task {
        let broker = PermissionBroker()
        let request = makePermissionRequest "tc1" "Execute bash"

        let requestId = broker.Enqueue(request)

        // Start waiting in a task
        let waitTask = broker.WaitForResponseAsync(requestId)

        // Cancel
        let _ = broker.Cancel(requestId)

        // Wait should complete with Cancelled
        let! result = waitTask

        Assert.True(result.IsSome)
        match result.Value with
        | RequestPermissionOutcome.Cancelled -> () // Expected
        | RequestPermissionOutcome.Selected _ -> Assert.Fail("Expected Cancelled, got Selected")
    }

[<Fact>]
let ``WaitForResponse returns None for unknown request`` () =
    task {
        let broker = PermissionBroker()

        let! result = broker.WaitForResponseAsync("unknown-id")

        Assert.True(result.IsNone)
    }

// ─────────────────────────────────────────────────────────────────────────────
// Response History
// ─────────────────────────────────────────────────────────────────────────────

[<Fact>]
let ``Respond records response in history`` () =
    let broker = PermissionBroker()
    let request = makePermissionRequest "tc1" "Execute bash"

    let requestId = broker.Enqueue(request)
    let _ = broker.Respond(requestId, "allow-always")

    let history = broker.ResponseHistory()
    Assert.Equal(1, history.Length)
    Assert.Equal(requestId, history.[0].requestId)
    Assert.Equal("allow-always", history.[0].selectedOptionId)

[<Fact>]
let ``Cancel records cancelled in history`` () =
    let broker = PermissionBroker()
    let request = makePermissionRequest "tc1" "Execute bash"

    let requestId = broker.Enqueue(request)
    let _ = broker.Cancel(requestId)

    let history = broker.ResponseHistory()
    Assert.Equal(1, history.Length)
    Assert.Equal(requestId, history.[0].requestId)
    Assert.True(history.[0].wasCancelled)

// ─────────────────────────────────────────────────────────────────────────────
// Auto-Response Rules
// ─────────────────────────────────────────────────────────────────────────────

[<Fact>]
let ``AddAutoRule applies to matching requests`` () =
    let broker = PermissionBroker()

    // Add rule to auto-allow all Execute operations
    broker.AddAutoRule(fun req ->
        req.toolCall.kind = Some ToolKind.Execute, "allow-always")

    let request = makePermissionRequest "tc1" "Execute bash"
    let requestId = broker.Enqueue(request)

    // Request should be auto-responded
    Assert.False(broker.HasPending())

    let history = broker.ResponseHistory()
    Assert.Equal(1, history.Length)
    Assert.Equal("allow-always", history.[0].selectedOptionId)
    Assert.True(history.[0].wasAutoResponded)

[<Fact>]
let ``AddAutoRule does not apply when predicate returns false`` () =
    let broker = PermissionBroker()

    // Add rule that never matches
    broker.AddAutoRule(fun _ -> false, "allow-always")

    let request = makePermissionRequest "tc1" "Execute bash"
    let requestId = broker.Enqueue(request)

    // Request should still be pending
    Assert.True(broker.HasPending())

[<Fact>]
let ``Multiple auto rules checked in order`` () =
    let broker = PermissionBroker()

    // First rule rejects Read operations
    broker.AddAutoRule(fun req ->
        req.toolCall.kind = Some ToolKind.Read, "reject-once")

    // Second rule allows everything else
    broker.AddAutoRule(fun _ -> true, "allow-once")

    // Read request should be rejected
    let readRequest =
        { sessionId = sessionId
          toolCall = { makeToolCallUpdate "tc1" "Read file" with kind = Some ToolKind.Read }
          options = (makePermissionRequest "x" "x").options }

    let _ = broker.Enqueue(readRequest)
    let history1 = broker.ResponseHistory()
    Assert.Equal("reject-once", history1.[0].selectedOptionId)

    // Execute request should be allowed
    let execRequest = makePermissionRequest "tc2" "Execute bash"
    let _ = broker.Enqueue(execRequest)
    let history2 = broker.ResponseHistory()
    Assert.Equal("allow-once", history2.[1].selectedOptionId)

[<Fact>]
let ``RemoveAutoRule stops auto-responding`` () =
    let broker = PermissionBroker()

    let ruleId = broker.AddAutoRule(fun _ -> true, "allow-always")

    // First request should be auto-responded
    let _ = broker.Enqueue(makePermissionRequest "tc1" "First")
    Assert.False(broker.HasPending())

    // Remove the rule
    broker.RemoveAutoRule(ruleId)

    // Second request should be pending
    let _ = broker.Enqueue(makePermissionRequest "tc2" "Second")
    Assert.True(broker.HasPending())

// ─────────────────────────────────────────────────────────────────────────────
// Reset
// ─────────────────────────────────────────────────────────────────────────────

[<Fact>]
let ``Reset clears pending and history`` () =
    let broker = PermissionBroker()

    let _ = broker.Enqueue(makePermissionRequest "tc1" "First")
    let id2 = broker.Enqueue(makePermissionRequest "tc2" "Second")
    let _ = broker.Respond(id2, "allow-once")

    Assert.True(broker.HasPending())
    Assert.Equal(1, broker.ResponseHistory().Length)

    broker.Reset()

    Assert.False(broker.HasPending())
    Assert.Empty(broker.ResponseHistory())

// ─────────────────────────────────────────────────────────────────────────────
// Subscribe/Unsubscribe
// ─────────────────────────────────────────────────────────────────────────────

[<Fact>]
let ``Subscribe receives new request events`` () =
    let broker = PermissionBroker()
    let mutable received: PendingPermissionRequest option = None

    let unsubscribe = broker.SubscribeToRequests(fun req -> received <- Some req)

    let _ = broker.Enqueue(makePermissionRequest "tc1" "Execute bash")

    Assert.True(received.IsSome)
    Assert.Equal("tc1", received.Value.request.toolCall.toolCallId)

    unsubscribe()

[<Fact>]
let ``SubscribeToResponses receives response events`` () =
    let broker = PermissionBroker()
    let mutable received: PermissionResponse option = None

    let unsubscribe = broker.SubscribeToResponses(fun resp -> received <- Some resp)

    let requestId = broker.Enqueue(makePermissionRequest "tc1" "Execute bash")
    let _ = broker.Respond(requestId, "allow-once")

    Assert.True(received.IsSome)
    Assert.Equal(requestId, received.Value.requestId)
    Assert.Equal("allow-once", received.Value.selectedOptionId)

    unsubscribe()
