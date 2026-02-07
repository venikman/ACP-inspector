module Acp.ToolCalls.Tests

open System
open Xunit
open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Prompting
open Acp.Contrib.ToolCalls

let sessionId = SessionId "test-session"

let makeToolCall id title kind status : ToolCall =
    { toolCallId = id
      title = title
      kind = kind
      status = status
      content = []
      locations = []
      rawInput = None
      rawOutput = None }

let makeToolCallUpdate
    id
    (title: string option)
    (kind: ToolKind option)
    (status: ToolCallStatus option)
    : ToolCallUpdate =
    { toolCallId = id
      title = title
      kind = kind
      status = status
      content = None
      locations = None
      rawInput = None
      rawOutput = None }

let makeNotification update : SessionUpdateNotification =
    { sessionId = sessionId
      update = update
      _meta = None }

// ─────────────────────────────────────────────────────────────────────────────
// Empty State
// ─────────────────────────────────────────────────────────────────────────────

[<Fact>]
let ``Empty tracker has no tool calls`` () =
    let tracker = ToolCallTracker()
    Assert.Empty(tracker.All())
    Assert.Empty(tracker.Pending())
    Assert.Empty(tracker.InProgress())
    Assert.Empty(tracker.Completed())
    Assert.Empty(tracker.Failed())

[<Fact>]
let ``Empty tracker HasInProgress returns false`` () =
    let tracker = ToolCallTracker()
    Assert.False(tracker.HasInProgress())

[<Fact>]
let ``Empty tracker TryGet returns None`` () =
    let tracker = ToolCallTracker()
    Assert.True(tracker.TryGet("nonexistent").IsNone)

// ─────────────────────────────────────────────────────────────────────────────
// Adding Tool Calls
// ─────────────────────────────────────────────────────────────────────────────

[<Fact>]
let ``Apply ToolCall adds to tracker`` () =
    let tracker = ToolCallTracker()
    let tc = makeToolCall "tc1" "Read file" ToolKind.Read ToolCallStatus.Pending

    tracker.Apply(makeNotification (SessionUpdate.ToolCall tc))

    Assert.Equal(1, tracker.All().Length)
    let view = tracker.TryGet("tc1")
    Assert.True(view.IsSome)
    Assert.Equal("Read file", view.Value.title)
    Assert.Equal(ToolKind.Read, view.Value.kind)
    Assert.Equal(ToolCallStatus.Pending, view.Value.status)

[<Fact>]
let ``Apply multiple ToolCalls preserves order`` () =
    let tracker = ToolCallTracker()

    tracker.Apply(
        makeNotification (SessionUpdate.ToolCall(makeToolCall "tc1" "First" ToolKind.Read ToolCallStatus.Pending))
    )

    tracker.Apply(
        makeNotification (SessionUpdate.ToolCall(makeToolCall "tc2" "Second" ToolKind.Edit ToolCallStatus.Pending))
    )

    tracker.Apply(
        makeNotification (SessionUpdate.ToolCall(makeToolCall "tc3" "Third" ToolKind.Execute ToolCallStatus.Pending))
    )

    let all = tracker.All()
    Assert.Equal(3, all.Length)
    Assert.Equal("tc1", all.[0].toolCallId)
    Assert.Equal("tc2", all.[1].toolCallId)
    Assert.Equal("tc3", all.[2].toolCallId)

// ─────────────────────────────────────────────────────────────────────────────
// Status Updates
// ─────────────────────────────────────────────────────────────────────────────

[<Fact>]
let ``ToolCallUpdate changes status`` () =
    let tracker = ToolCallTracker()
    let tc = makeToolCall "tc1" "Read file" ToolKind.Read ToolCallStatus.Pending

    tracker.Apply(makeNotification (SessionUpdate.ToolCall tc))
    Assert.Equal(ToolCallStatus.Pending, tracker.TryGet("tc1").Value.status)

    let update = makeToolCallUpdate "tc1" None None (Some ToolCallStatus.InProgress)
    tracker.Apply(makeNotification (SessionUpdate.ToolCallUpdate update))
    Assert.Equal(ToolCallStatus.InProgress, tracker.TryGet("tc1").Value.status)

    let update2 = makeToolCallUpdate "tc1" None None (Some ToolCallStatus.Completed)
    tracker.Apply(makeNotification (SessionUpdate.ToolCallUpdate update2))
    Assert.Equal(ToolCallStatus.Completed, tracker.TryGet("tc1").Value.status)

[<Fact>]
let ``ToolCallUpdate can update title`` () =
    let tracker = ToolCallTracker()
    let tc = makeToolCall "tc1" "Reading..." ToolKind.Read ToolCallStatus.InProgress

    tracker.Apply(makeNotification (SessionUpdate.ToolCall tc))
    Assert.Equal("Reading...", tracker.TryGet("tc1").Value.title)

    let update = makeToolCallUpdate "tc1" (Some "Read file.txt") None None
    tracker.Apply(makeNotification (SessionUpdate.ToolCallUpdate update))
    Assert.Equal("Read file.txt", tracker.TryGet("tc1").Value.title)

[<Fact>]
let ``ToolCallUpdate creates entry if not exists`` () =
    let tracker = ToolCallTracker()

    let update =
        makeToolCallUpdate "tc1" (Some "New call") None (Some ToolCallStatus.InProgress)

    tracker.Apply(makeNotification (SessionUpdate.ToolCallUpdate update))

    let view = tracker.TryGet("tc1")
    Assert.True(view.IsSome)
    Assert.Equal("New call", view.Value.title)
    Assert.Equal(ToolCallStatus.InProgress, view.Value.status)

// ─────────────────────────────────────────────────────────────────────────────
// Filtering by Status
// ─────────────────────────────────────────────────────────────────────────────

[<Fact>]
let ``Pending returns only pending calls`` () =
    let tracker = ToolCallTracker()

    tracker.Apply(
        makeNotification (SessionUpdate.ToolCall(makeToolCall "tc1" "Pending" ToolKind.Read ToolCallStatus.Pending))
    )

    tracker.Apply(
        makeNotification (
            SessionUpdate.ToolCall(makeToolCall "tc2" "InProgress" ToolKind.Edit ToolCallStatus.InProgress)
        )
    )

    tracker.Apply(
        makeNotification (
            SessionUpdate.ToolCall(makeToolCall "tc3" "Completed" ToolKind.Search ToolCallStatus.Completed)
        )
    )

    let pending = tracker.Pending()
    Assert.Equal(1, pending.Length)
    Assert.Equal("tc1", pending.[0].toolCallId)

[<Fact>]
let ``InProgress returns only in-progress calls`` () =
    let tracker = ToolCallTracker()

    tracker.Apply(
        makeNotification (SessionUpdate.ToolCall(makeToolCall "tc1" "Pending" ToolKind.Read ToolCallStatus.Pending))
    )

    tracker.Apply(
        makeNotification (
            SessionUpdate.ToolCall(makeToolCall "tc2" "InProgress" ToolKind.Edit ToolCallStatus.InProgress)
        )
    )

    tracker.Apply(
        makeNotification (
            SessionUpdate.ToolCall(makeToolCall "tc3" "Completed" ToolKind.Search ToolCallStatus.Completed)
        )
    )

    let inProgress = tracker.InProgress()
    Assert.Equal(1, inProgress.Length)
    Assert.Equal("tc2", inProgress.[0].toolCallId)

[<Fact>]
let ``Completed returns only completed calls`` () =
    let tracker = ToolCallTracker()

    tracker.Apply(
        makeNotification (SessionUpdate.ToolCall(makeToolCall "tc1" "Pending" ToolKind.Read ToolCallStatus.Pending))
    )

    tracker.Apply(
        makeNotification (
            SessionUpdate.ToolCall(makeToolCall "tc2" "InProgress" ToolKind.Edit ToolCallStatus.InProgress)
        )
    )

    tracker.Apply(
        makeNotification (
            SessionUpdate.ToolCall(makeToolCall "tc3" "Completed" ToolKind.Search ToolCallStatus.Completed)
        )
    )

    let completed = tracker.Completed()
    Assert.Equal(1, completed.Length)
    Assert.Equal("tc3", completed.[0].toolCallId)

[<Fact>]
let ``Failed returns only failed calls`` () =
    let tracker = ToolCallTracker()

    tracker.Apply(
        makeNotification (SessionUpdate.ToolCall(makeToolCall "tc1" "Success" ToolKind.Read ToolCallStatus.Completed))
    )

    tracker.Apply(
        makeNotification (SessionUpdate.ToolCall(makeToolCall "tc2" "Failed" ToolKind.Execute ToolCallStatus.Failed))
    )

    let failed = tracker.Failed()
    Assert.Equal(1, failed.Length)
    Assert.Equal("tc2", failed.[0].toolCallId)

// ─────────────────────────────────────────────────────────────────────────────
// HasInProgress
// ─────────────────────────────────────────────────────────────────────────────

[<Fact>]
let ``HasInProgress returns true when calls in progress`` () =
    let tracker = ToolCallTracker()

    tracker.Apply(
        makeNotification (
            SessionUpdate.ToolCall(makeToolCall "tc1" "InProgress" ToolKind.Read ToolCallStatus.InProgress)
        )
    )

    Assert.True(tracker.HasInProgress())

[<Fact>]
let ``HasInProgress returns false when all completed`` () =
    let tracker = ToolCallTracker()

    tracker.Apply(
        makeNotification (SessionUpdate.ToolCall(makeToolCall "tc1" "Done" ToolKind.Read ToolCallStatus.Completed))
    )

    tracker.Apply(
        makeNotification (SessionUpdate.ToolCall(makeToolCall "tc2" "Also Done" ToolKind.Edit ToolCallStatus.Completed))
    )

    Assert.False(tracker.HasInProgress())

// ─────────────────────────────────────────────────────────────────────────────
// Reset
// ─────────────────────────────────────────────────────────────────────────────

[<Fact>]
let ``Reset clears all tool calls`` () =
    let tracker = ToolCallTracker()

    tracker.Apply(
        makeNotification (SessionUpdate.ToolCall(makeToolCall "tc1" "First" ToolKind.Read ToolCallStatus.Pending))
    )

    tracker.Apply(
        makeNotification (SessionUpdate.ToolCall(makeToolCall "tc2" "Second" ToolKind.Edit ToolCallStatus.InProgress))
    )

    Assert.Equal(2, tracker.All().Length)

    tracker.Reset()

    Assert.Empty(tracker.All())
    Assert.True(tracker.TryGet("tc1").IsNone)

// ─────────────────────────────────────────────────────────────────────────────
// Ignores Non-ToolCall Updates
// ─────────────────────────────────────────────────────────────────────────────

[<Fact>]
let ``Ignores non-tool-call updates`` () =
    let tracker = ToolCallTracker()

    // Apply various non-tool-call updates
    let textContent = ContentBlock.Text { text = "Hello"; annotations = None }
    tracker.Apply(makeNotification (SessionUpdate.UserMessageChunk { content = textContent }))
    tracker.Apply(makeNotification (SessionUpdate.AgentMessageChunk { content = textContent }))
    tracker.Apply(makeNotification (SessionUpdate.Plan { entries = [] }))

    Assert.Empty(tracker.All())

// ─────────────────────────────────────────────────────────────────────────────
// Session Handling
// ─────────────────────────────────────────────────────────────────────────────

[<Fact>]
let ``AutoReset on session change clears tool calls`` () =
    let tracker = ToolCallTracker(autoResetOnSessionChange = true)

    let session1 = SessionId "session-1"
    let session2 = SessionId "session-2"

    let tc1 = makeToolCall "tc1" "First" ToolKind.Read ToolCallStatus.Completed

    tracker.Apply(
        { sessionId = session1
          update = SessionUpdate.ToolCall tc1
          _meta = None }
    )

    Assert.Equal(1, tracker.All().Length)
    Assert.Equal(Some session1, tracker.SessionId)

    let tc2 = makeToolCall "tc2" "Second" ToolKind.Edit ToolCallStatus.Pending

    tracker.Apply(
        { sessionId = session2
          update = SessionUpdate.ToolCall tc2
          _meta = None }
    )

    Assert.Equal(1, tracker.All().Length)
    Assert.Equal("tc2", tracker.All().[0].toolCallId)
    Assert.Equal(Some session2, tracker.SessionId)

[<Fact>]
let ``No autoReset throws on session change`` () =
    let tracker = ToolCallTracker(autoResetOnSessionChange = false)

    let session1 = SessionId "session-1"
    let session2 = SessionId "session-2"

    let tc1 = makeToolCall "tc1" "First" ToolKind.Read ToolCallStatus.Completed

    tracker.Apply(
        { sessionId = session1
          update = SessionUpdate.ToolCall tc1
          _meta = None }
    )

    let tc2 = makeToolCall "tc2" "Second" ToolKind.Edit ToolCallStatus.Pending

    Assert.Throws<ToolCallSessionMismatchError>(fun () ->
        tracker.Apply(
            { sessionId = session2
              update = SessionUpdate.ToolCall tc2
              _meta = None }
        ))
    |> ignore

// ─────────────────────────────────────────────────────────────────────────────
// Subscribe/Unsubscribe
// ─────────────────────────────────────────────────────────────────────────────

[<Fact>]
let ``Subscribe receives updates`` () =
    let tracker = ToolCallTracker()
    let mutable received: ToolCallView list = []

    let unsubscribe = tracker.Subscribe(fun all _notification -> received <- all)

    tracker.Apply(
        makeNotification (SessionUpdate.ToolCall(makeToolCall "tc1" "First" ToolKind.Read ToolCallStatus.Pending))
    )

    Assert.Equal(1, received.Length)
    Assert.Equal("tc1", received.[0].toolCallId)

    unsubscribe ()

[<Fact>]
let ``Unsubscribe stops receiving updates`` () =
    let tracker = ToolCallTracker()
    let mutable callCount = 0

    let unsubscribe = tracker.Subscribe(fun _ _ -> callCount <- callCount + 1)

    tracker.Apply(
        makeNotification (SessionUpdate.ToolCall(makeToolCall "tc1" "First" ToolKind.Read ToolCallStatus.Pending))
    )

    Assert.Equal(1, callCount)

    unsubscribe ()

    tracker.Apply(
        makeNotification (SessionUpdate.ToolCall(makeToolCall "tc2" "Second" ToolKind.Edit ToolCallStatus.Pending))
    )

    Assert.Equal(1, callCount) // Still 1, not incremented
