#!/usr/bin/env dotnet fsi
/// Basic Client-Agent Example
///
/// Demonstrates how to set up a client and agent communicating over
/// in-memory transport, performing a complete session lifecycle.

#r "../src/bin/Debug/net10.0/ACP.dll"

open System
open System.Threading.Tasks
open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Prompting
open Acp.Domain.SessionModes
open Acp.Transport
open Acp.Connection

// ─────────────────────────────────────────────────────────────────────────────
// Agent Implementation
// ─────────────────────────────────────────────────────────────────────────────

/// Create agent handlers that implement the ACP protocol
let createAgentHandlers () =
    let mutable sessionCounter = 0

    { onInitialize =
        fun _params ->
            task {
                printfn "[Agent] Received initialize request"
                return Ok { agentName = "Example Agent"; agentVersion = "1.0.0" }
            }

      onNewSession =
        fun _params ->
            task {
                sessionCounter <- sessionCounter + 1
                let sessionId = SessionId $"session-{sessionCounter}"
                printfn $"[Agent] Created new session: {SessionId.value sessionId}"

                return
                    Ok
                        { sessionId = sessionId
                          modes = [ { modeId = SessionModeId "default"; name = "Default Mode" } ]
                          currentModeId = SessionModeId "default" }
            }

      onPrompt =
        fun params' ->
            task {
                printfn $"[Agent] Received prompt for session: {SessionId.value params'.sessionId}"

                // Extract user message if present
                let userText =
                    params'.prompt
                    |> List.tryPick (fun item ->
                        match item with
                        | PromptItem.UserMessage msg ->
                            msg.content
                            |> List.tryPick (fun block ->
                                match block with
                                | ContentBlock.Text t -> Some t.text
                                | _ -> None)
                        | _ -> None)

                printfn $"[Agent] User said: {userText |> Option.defaultValue \"(no text)\"}"

                return
                    Ok
                        { sessionId = params'.sessionId
                          outputTurnId = params'.expectedTurnId |> Option.defaultValue (TurnId "turn-1") }
            }

      onCancel =
        fun params' ->
            task {
                printfn $"[Agent] Session cancelled: {SessionId.value params'.sessionId}"
                return ()
            }

      onSetMode =
        fun params' ->
            task {
                printfn $"[Agent] Mode set to: {SessionModeId.value params'.modeId}"
                return Ok { currentModeId = params'.modeId }
            } }

// ─────────────────────────────────────────────────────────────────────────────
// Main Example
// ─────────────────────────────────────────────────────────────────────────────

let runExample () =
    task {
        printfn "=== Basic Client-Agent Example ==="
        printfn ""

        // Create duplex transport pair
        let clientTransport, agentTransport = DuplexTransport.CreatePair()

        // Set up agent
        let handlers = createAgentHandlers ()
        let agent = AgentConnection(agentTransport, handlers)
        let agentTask = agent.StartListening()

        // Set up client
        let client = ClientConnection(clientTransport)

        // 1. Initialize
        printfn "[Client] Sending initialize..."
        let! initResult = client.InitializeAsync({ clientName = "Example Client"; clientVersion = "1.0.0" })

        match initResult with
        | Error e -> printfn $"[Client] Initialize failed: {e}"
        | Ok init -> printfn $"[Client] Connected to: {init.agentName} v{init.agentVersion}"

        printfn ""

        // 2. Create session
        printfn "[Client] Creating new session..."
        let! sessionResult = client.NewSessionAsync({})

        match sessionResult with
        | Error e -> printfn $"[Client] NewSession failed: {e}"
        | Ok session ->
            printfn $"[Client] Session created: {SessionId.value session.sessionId}"
            printfn $"[Client] Available modes: {session.modes |> List.length}"
            printfn ""

            // 3. Send a prompt
            printfn "[Client] Sending prompt..."

            let prompt =
                [ PromptItem.UserMessage
                      { content =
                          [ ContentBlock.Text
                                { text = "Hello, Agent! How are you today?"
                                  annotations = None } ] } ]

            let! promptResult =
                client.PromptAsync(
                    { sessionId = session.sessionId
                      prompt = prompt
                      expectedTurnId = None }
                )

            match promptResult with
            | Error e -> printfn $"[Client] Prompt failed: {e}"
            | Ok result -> printfn $"[Client] Response received, turn: {TurnId.value result.outputTurnId}"

        printfn ""

        // Cleanup
        printfn "[Client] Closing connection..."
        do! client.CloseAsync()
        do! agent.StopAsync()

        printfn ""
        printfn "=== Example Complete ==="
    }

// Run the example
runExample().GetAwaiter().GetResult()
