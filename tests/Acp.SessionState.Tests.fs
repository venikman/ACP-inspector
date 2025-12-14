namespace Acp.Tests

open Xunit

open Acp
open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Prompting
open Acp.Domain.SessionModes
open Acp.Contrib

module SessionStateTests =

    // ============================================================
    // SessionAccumulator basic tests
    // ============================================================

    [<Fact>]
    let ``SessionAccumulator starts empty`` () =
        let acc = SessionState.SessionAccumulator()
        Assert.True(acc.SessionId.IsNone)

        Assert.Throws<SessionState.SessionSnapshotUnavailableError>(fun () -> acc.Snapshot() |> ignore)
        |> ignore

    [<Fact>]
    let ``Apply sets session id from first notification`` () =
        let acc = SessionState.SessionAccumulator()

        let notification: SessionUpdateNotification =
            { sessionId = SessionId "test-session"
              update =
                SessionUpdate.AgentMessageChunk { content = ContentBlock.Text { text = "Hello"; annotations = None } } }

        let snapshot = acc.Apply(notification)

        Assert.Equal(Some(SessionId "test-session"), acc.SessionId)
        Assert.Equal("test-session", SessionId.value snapshot.sessionId)

    [<Fact>]
    let ``Apply accumulates agent messages`` () =
        let acc = SessionState.SessionAccumulator()

        let notify1: SessionUpdateNotification =
            { sessionId = SessionId "s1"
              update =
                SessionUpdate.AgentMessageChunk { content = ContentBlock.Text { text = "First"; annotations = None } } }

        let notify2: SessionUpdateNotification =
            { sessionId = SessionId "s1"
              update =
                SessionUpdate.AgentMessageChunk { content = ContentBlock.Text { text = "Second"; annotations = None } } }

        let _ = acc.Apply(notify1)
        let snapshot = acc.Apply(notify2)

        Assert.Equal(2, snapshot.agentMessages.Length)

    [<Fact>]
    let ``Apply accumulates user messages`` () =
        let acc = SessionState.SessionAccumulator()

        let notify: SessionUpdateNotification =
            { sessionId = SessionId "s1"
              update =
                SessionUpdate.UserMessageChunk
                    { content =
                        ContentBlock.Text
                            { text = "User says hi"
                              annotations = None } } }

        let snapshot = acc.Apply(notify)

        Assert.Equal(1, snapshot.userMessages.Length)

    [<Fact>]
    let ``Apply tracks tool calls`` () =
        let acc = SessionState.SessionAccumulator()

        let toolCall: ToolCall =
            { toolCallId = "tc1"
              title = "Read file"
              kind = ToolKind.Read
              status = ToolCallStatus.InProgress
              content = []
              locations = []
              rawInput = None
              rawOutput = None }

        let notify: SessionUpdateNotification =
            { sessionId = SessionId "s1"
              update = SessionUpdate.ToolCall toolCall }

        let snapshot = acc.Apply(notify)

        Assert.True(snapshot.toolCalls.ContainsKey("tc1"))

        let tc = snapshot.toolCalls.["tc1"]
        Assert.Equal("Read file", tc.title)
        Assert.Equal(ToolKind.Read, tc.kind)
        Assert.Equal(ToolCallStatus.InProgress, tc.status)

    [<Fact>]
    let ``Apply updates tool call status`` () =
        let acc = SessionState.SessionAccumulator()

        // Start tool call
        let toolCall: ToolCall =
            { toolCallId = "tc1"
              title = "Read file"
              kind = ToolKind.Read
              status = ToolCallStatus.InProgress
              content = []
              locations = []
              rawInput = None
              rawOutput = None }

        let notify1: SessionUpdateNotification =
            { sessionId = SessionId "s1"
              update = SessionUpdate.ToolCall toolCall }

        let _ = acc.Apply(notify1)

        // Update tool call
        let toolCallUpdate: ToolCallUpdate =
            { toolCallId = "tc1"
              title = None
              kind = None
              status = Some ToolCallStatus.Completed
              content = None
              locations = None
              rawInput = None
              rawOutput = None }

        let notify2: SessionUpdateNotification =
            { sessionId = SessionId "s1"
              update = SessionUpdate.ToolCallUpdate toolCallUpdate }

        let snapshot = acc.Apply(notify2)

        let tc = snapshot.toolCalls.["tc1"]
        Assert.Equal(ToolCallStatus.Completed, tc.status)
        // Title should remain from original
        Assert.Equal("Read file", tc.title)

    [<Fact>]
    let ``Apply tracks current mode`` () =
        let acc = SessionState.SessionAccumulator()

        let notify: SessionUpdateNotification =
            { sessionId = SessionId "s1"
              update = SessionUpdate.CurrentModeUpdate { currentModeId = SessionModeId "code" } }

        let snapshot = acc.Apply(notify)

        Assert.Equal(Some(SessionModeId "code"), snapshot.currentModeId)

    [<Fact>]
    let ``Apply tracks plan entries`` () =
        let acc = SessionState.SessionAccumulator()

        let plan: Plan =
            { entries =
                [ { content = "Step 1"
                    priority = PlanEntryPriority.High
                    status = PlanEntryStatus.Pending }
                  { content = "Step 2"
                    priority = PlanEntryPriority.Medium
                    status = PlanEntryStatus.InProgress } ] }

        let notify: SessionUpdateNotification =
            { sessionId = SessionId "s1"
              update = SessionUpdate.Plan plan }

        let snapshot = acc.Apply(notify)

        Assert.Equal(2, snapshot.planEntries.Length)
        Assert.Equal("Step 1", snapshot.planEntries.[0].content)

    [<Fact>]
    let ``Apply tracks available commands`` () =
        let acc = SessionState.SessionAccumulator()

        let update: AvailableCommandsUpdate =
            { availableCommands =
                [ { name = "/help"
                    description = "Show help"
                    input = None }
                  { name = "/clear"
                    description = "Clear screen"
                    input = None } ] }

        let notify: SessionUpdateNotification =
            { sessionId = SessionId "s1"
              update = SessionUpdate.AvailableCommandsUpdate update }

        let snapshot = acc.Apply(notify)

        Assert.Equal(2, snapshot.availableCommands.Length)
        Assert.Equal("/help", snapshot.availableCommands.[0].name)

    [<Fact>]
    let ``Reset clears all state`` () =
        let acc = SessionState.SessionAccumulator()

        let notify: SessionUpdateNotification =
            { sessionId = SessionId "s1"
              update =
                SessionUpdate.AgentMessageChunk { content = ContentBlock.Text { text = "Hello"; annotations = None } } }

        let _ = acc.Apply(notify)
        acc.Reset()

        Assert.True(acc.SessionId.IsNone)

    [<Fact>]
    let ``Auto reset on session change`` () =
        let acc = SessionState.SessionAccumulator(autoResetOnSessionChange = true)

        let notify1: SessionUpdateNotification =
            { sessionId = SessionId "session-1"
              update =
                SessionUpdate.AgentMessageChunk
                    { content =
                        ContentBlock.Text
                            { text = "Message 1"
                              annotations = None } } }

        let notify2: SessionUpdateNotification =
            { sessionId = SessionId "session-2"
              update =
                SessionUpdate.AgentMessageChunk
                    { content =
                        ContentBlock.Text
                            { text = "Message 2"
                              annotations = None } } }

        let _ = acc.Apply(notify1)
        let snapshot = acc.Apply(notify2)

        // Should have reset and only have the new session's message
        Assert.Equal("session-2", SessionId.value snapshot.sessionId)
        Assert.Equal(1, snapshot.agentMessages.Length)

    [<Fact>]
    let ``Throws on session mismatch when auto reset disabled`` () =
        let acc = SessionState.SessionAccumulator(autoResetOnSessionChange = false)

        let notify1: SessionUpdateNotification =
            { sessionId = SessionId "session-1"
              update =
                SessionUpdate.AgentMessageChunk
                    { content =
                        ContentBlock.Text
                            { text = "Message 1"
                              annotations = None } } }

        let notify2: SessionUpdateNotification =
            { sessionId = SessionId "session-2"
              update =
                SessionUpdate.AgentMessageChunk
                    { content =
                        ContentBlock.Text
                            { text = "Message 2"
                              annotations = None } } }

        let _ = acc.Apply(notify1)

        Assert.Throws<SessionState.SessionNotificationMismatchError>(fun () -> acc.Apply(notify2) |> ignore)
        |> ignore

    [<Fact>]
    let ``Subscribe receives notifications`` () =
        let acc = SessionState.SessionAccumulator()
        let mutable callCount = 0
        let mutable lastSnapshot: SessionState.SessionSnapshot option = None

        let unsubscribe =
            acc.Subscribe(fun snapshot _ ->
                callCount <- callCount + 1
                lastSnapshot <- Some snapshot)

        let notify: SessionUpdateNotification =
            { sessionId = SessionId "s1"
              update =
                SessionUpdate.AgentMessageChunk { content = ContentBlock.Text { text = "Hello"; annotations = None } } }

        let _ = acc.Apply(notify)

        Assert.Equal(1, callCount)
        Assert.True(lastSnapshot.IsSome)

        // Unsubscribe and verify no more calls
        unsubscribe ()

        let _ = acc.Apply(notify)
        Assert.Equal(1, callCount) // Should not have increased

    [<Fact>]
    let ``Snapshot is immutable`` () =
        let acc = SessionState.SessionAccumulator()

        let notify1: SessionUpdateNotification =
            { sessionId = SessionId "s1"
              update =
                SessionUpdate.AgentMessageChunk { content = ContentBlock.Text { text = "First"; annotations = None } } }

        let snapshot1 = acc.Apply(notify1)

        let notify2: SessionUpdateNotification =
            { sessionId = SessionId "s1"
              update =
                SessionUpdate.AgentMessageChunk { content = ContentBlock.Text { text = "Second"; annotations = None } } }

        let snapshot2 = acc.Apply(notify2)

        // Original snapshot should be unchanged
        Assert.Equal(1, snapshot1.agentMessages.Length)
        Assert.Equal(2, snapshot2.agentMessages.Length)
