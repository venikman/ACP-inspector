#!/usr/bin/env dotnet fsi
/// Full Integration Example
///
/// Demonstrates a complete client-agent session using all SDK components:
/// - Transport and Connection layers
/// - Session state accumulation
/// - Tool call tracking
/// - Permission handling

#r "../src/bin/Debug/net9.0/ACP.dll"

open System
open System.Threading.Tasks
open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Prompting
open Acp.Domain.SessionModes
open Acp.Transport
open Acp.Connection
open Acp.Contrib.SessionState
open Acp.Contrib.ToolCalls
open Acp.Contrib.Permissions

// ─────────────────────────────────────────────────────────────────────────────
// Client-Side Components
// ─────────────────────────────────────────────────────────────────────────────

/// Client that uses all SDK components to interact with an agent
type SmartClient(transport: ITransport) =
    let connection = ClientConnection(transport)
    let sessionState = SessionAccumulator()
    let toolTracker = ToolCallTracker()
    let permissions = PermissionBroker()

    let mutable currentSession: SessionId option = None

    // Auto-allow Read operations
    do
        permissions.AddAutoRule(fun req -> req.toolCall.kind = Some ToolKind.Read, "allow-once")
        |> ignore

    member _.Connection = connection
    member _.SessionState = sessionState
    member _.ToolTracker = toolTracker
    member _.Permissions = permissions

    member _.InitializeAsync() =
        task {
            let! result =
                connection.InitializeAsync(
                    { clientName = "SmartClient"
                      clientVersion = "1.0.0" }
                )

            match result with
            | Ok init -> printfn $"[SmartClient] Connected to {init.agentName}"
            | Error e -> printfn $"[SmartClient] Init failed: {e}"

            return result
        }

    member _.StartSessionAsync() =
        task {
            let! result = connection.NewSessionAsync({ })

            match result with
            | Ok session ->
                currentSession <- Some session.sessionId
                printfn $"[SmartClient] Session started: {SessionId.value session.sessionId}"
            | Error e -> printfn $"[SmartClient] Session failed: {e}"

            return result
        }

    member _.SendPromptAsync(text: string) =
        task {
            match currentSession with
            | None -> return Error(ConnectionError.ProtocolError "No session")
            | Some sid ->
                let prompt =
                    [ PromptItem.UserMessage { content = [ ContentBlock.Text { text = text; annotations = None } ] } ]

                return!
                    connection.PromptAsync(
                        { sessionId = sid
                          prompt = prompt
                          expectedTurnId = None }
                    )
        }

    /// Process a session update notification
    member _.ProcessUpdate(notification: SessionUpdateNotification) =
        // Update all trackers
        let snapshot = sessionState.Apply(notification)
        toolTracker.Apply(notification)

        // Log what happened
        match notification.update with
        | SessionUpdate.ToolCall tc -> printfn $"  [Tool] Started: {tc.title}"
        | SessionUpdate.ToolCallUpdate u ->
            match u.status with
            | Some ToolCallStatus.Completed -> printfn $"  [Tool] Completed: {u.toolCallId}"
            | Some ToolCallStatus.Failed -> printfn $"  [Tool] Failed: {u.toolCallId}"
            | _ -> ()
        | SessionUpdate.AgentMessageChunk _ -> printfn "  [Agent] Message chunk received"
        | _ -> ()

        snapshot

    /// Handle a permission request
    member _.HandlePermissionRequest(request: RequestPermissionParams) =
        let requestId = permissions.Enqueue(request)

        // Check if auto-handled
        if not (permissions.HasPending()) then
            printfn $"  [Permission] Auto-handled: {request.toolCall.title}"
            let history = permissions.ResponseHistory()
            let last = history |> List.last
            RequestPermissionOutcome.Selected last.selectedOptionId
        else
            // Simulate user approval for this example
            printfn $"  [Permission] Awaiting approval: {request.toolCall.title}"
            printfn "  [Permission] (Auto-approving for demo...)"

            match permissions.Respond(requestId, "allow-once") with
            | Ok _ -> RequestPermissionOutcome.Selected "allow-once"
            | Error _ -> RequestPermissionOutcome.Cancelled

    member _.PrintStatus() =
        printfn ""
        printfn "=== Client Status ==="

        match currentSession with
        | Some sid -> printfn $"Session: {SessionId.value sid}"
        | None -> printfn "Session: (none)"

        printfn $"Tool Calls - Total: {toolTracker.All().Length}, InProgress: {toolTracker.InProgress().Length}"
        printfn $"Permissions - Pending: {permissions.PendingRequests().Length}"

        try
            let snapshot = sessionState.Snapshot()
            printfn $"Messages - User: {snapshot.userMessages.Length}, Agent: {snapshot.agentMessages.Length}"
        with :? SessionSnapshotUnavailableError ->
            printfn "Messages - (no session data)"

    member _.CloseAsync() =
        task {
            do! connection.CloseAsync()
            printfn "[SmartClient] Connection closed"
        }

// ─────────────────────────────────────────────────────────────────────────────
// Agent Implementation
// ─────────────────────────────────────────────────────────────────────────────

let createAgent (transport: ITransport) =
    let mutable sessionCounter = 0

    let handlers =
        { onInitialize =
            fun _ ->
                task {
                    return
                        Ok
                            { agentName = "DemoAgent"
                              agentVersion = "1.0.0" }
                }

          onNewSession =
            fun _ ->
                task {
                    sessionCounter <- sessionCounter + 1

                    return
                        Ok
                            { sessionId = SessionId $"demo-{sessionCounter}"
                              modes =
                                [ { modeId = SessionModeId "default"
                                    name = "Default" } ]
                              currentModeId = SessionModeId "default" }
                }

          onPrompt =
            fun p ->
                task {
                    printfn $"[Agent] Processing prompt..."

                    return
                        Ok
                            { sessionId = p.sessionId
                              outputTurnId = TurnId "turn-1" }
                }

          onCancel = fun _ -> Task.FromResult()
          onSetMode = fun p -> task { return Ok { currentModeId = p.modeId } } }

    AgentConnection(transport, handlers)

// ─────────────────────────────────────────────────────────────────────────────
// Main Example
// ─────────────────────────────────────────────────────────────────────────────

let runExample () =
    task {
        printfn "╔══════════════════════════════════════════════════════════════╗"
        printfn "║          Full SDK Integration Example                        ║"
        printfn "╚══════════════════════════════════════════════════════════════╝"
        printfn ""

        // Set up transport
        let clientTransport, agentTransport = DuplexTransport.CreatePair()

        // Create agent and client
        let agent = createAgent agentTransport
        let agentTask = agent.StartListening()
        let client = SmartClient(clientTransport)

        // Initialize
        printfn "1. Initializing connection..."
        let! _ = client.InitializeAsync()
        printfn ""

        // Start session
        printfn "2. Starting session..."
        let! _ = client.StartSessionAsync()
        printfn ""

        // Simulate receiving updates
        printfn "3. Simulating agent activity..."
        let sessionId = SessionId "demo-1"

        // Simulate tool calls coming from agent
        let updates =
            [ SessionUpdate.ToolCall
                  { toolCallId = "read-1"
                    title = "Read package.json"
                    kind = ToolKind.Read
                    status = ToolCallStatus.InProgress
                    content = []
                    locations = [ { path = "package.json"; line = None } ]
                    rawInput = None
                    rawOutput = None }

              SessionUpdate.ToolCallUpdate
                  { toolCallId = "read-1"
                    title = None
                    kind = None
                    status = Some ToolCallStatus.Completed
                    content = None
                    locations = None
                    rawInput = None
                    rawOutput = None }

              SessionUpdate.ToolCall
                  { toolCallId = "exec-1"
                    title = "Execute: npm test"
                    kind = ToolKind.Execute
                    status = ToolCallStatus.Pending
                    content = []
                    locations = []
                    rawInput = None
                    rawOutput = None }

              SessionUpdate.AgentMessageChunk
                  { content =
                      ContentBlock.Text
                          { text = "Running tests..."
                            annotations = None } } ]

        for update in updates do
            let notification =
                { sessionId = sessionId
                  update = update }

            client.ProcessUpdate(notification) |> ignore

        printfn ""

        // Handle permission request
        printfn "4. Handling permission request..."

        let permRequest =
            { sessionId = sessionId
              toolCall =
                { toolCallId = "exec-1"
                  title = Some "Execute: npm test"
                  kind = Some ToolKind.Execute
                  status = Some ToolCallStatus.Pending
                  content = None
                  locations = None
                  rawInput = None
                  rawOutput = None }
              options =
                [ { optionId = "allow-once"
                    name = "Allow Once"
                    kind = PermissionOptionKind.AllowOnce }
                  { optionId = "reject"
                    name = "Reject"
                    kind = PermissionOptionKind.RejectOnce } ] }

        let outcome = client.HandlePermissionRequest(permRequest)
        printfn $"  Outcome: {outcome}"
        printfn ""

        // Show final status
        client.PrintStatus()
        printfn ""

        // Cleanup
        printfn "5. Closing..."
        do! client.CloseAsync()
        do! agent.StopAsync()

        printfn ""
        printfn "╔══════════════════════════════════════════════════════════════╗"
        printfn "║                    Example Complete                          ║"
        printfn "╚══════════════════════════════════════════════════════════════╝"
    }

runExample().GetAwaiter().GetResult()
