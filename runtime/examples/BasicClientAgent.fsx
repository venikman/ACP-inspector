#!/usr/bin/env dotnet fsi
/// Basic Client-Agent Example
///
/// Demonstrates how to set up a client and agent communicating over
/// in-memory transport, performing a complete session lifecycle.

#r "nuget: FsToolkit.ErrorHandling, 5.1.0"
#r "../src/bin/Debug/net10.0/ACP.Protocol.dll"
#r "../src/bin/Debug/net10.0/ACP.Runtime.dll"

open System
open System.Threading.Tasks
open Acp.Domain
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Capabilities
open Acp.Domain.Initialization
open Acp.Domain.Prompting
open Acp.Domain.SessionSetup
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

                return
                    Ok
                        { protocolVersion = ProtocolVersion.current
                          agentCapabilities =
                            { loadSession = false
                              mcpCapabilities = { http = false; sse = false }
                              promptCapabilities =
                                { audio = false
                                  image = false
                                  embeddedContext = false }
                              sessionCapabilities = Capabilities.SessionCapabilities }
                          agentInfo =
                            Some
                                { name = "example-agent"
                                  title = Some "Example Agent"
                                  version = "1.0.0" }
                          authMethods = [] }
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
                          modes =
                            Some
                                { currentModeId = SessionModeId "default"
                                  availableModes =
                                    [ { id = SessionModeId "default"
                                        name = "Default Mode"
                                        description = None } ] } }
            }

      onPrompt =
        fun params' ->
            task {
                printfn $"[Agent] Received prompt for session: {SessionId.value params'.sessionId}"

                // Extract user message if present
                let userText =
                    params'.prompt
                    |> List.tryPick (fun block ->
                        match block with
                        | ContentBlock.Text t -> Some t.text
                        | _ -> None)

                let displayText = userText |> Option.defaultValue "(no text)"
                printfn $"[Agent] User said: {displayText}"

                return
                    Ok
                        { sessionId = params'.sessionId
                          stopReason = StopReason.EndTurn
                          usage = None
                          _meta = None }
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

                return
                    Ok
                        { sessionId = params'.sessionId
                          modeId = params'.modeId }
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

        let! initResult =
            client.InitializeAsync(
                { protocolVersion = ProtocolVersion.current
                  clientCapabilities =
                    { fs =
                        { readTextFile = false
                          writeTextFile = false }
                      terminal = false }
                  clientInfo =
                    Some
                        { name = "example-client"
                          title = Some "Example Client"
                          version = "1.0.0" } }
            )

        match initResult with
        | Error e -> printfn $"[Client] Initialize failed: {e}"
        | Ok init ->
            let agentName =
                init.agentInfo |> Option.map (fun i -> i.name) |> Option.defaultValue "Unknown"

            let agentVersion =
                init.agentInfo |> Option.map (fun i -> i.version) |> Option.defaultValue "?"

            printfn $"[Client] Connected to: {agentName} v{agentVersion}"

        printfn ""

        // 2. Create session
        printfn "[Client] Creating new session..."

        let! sessionResult = client.NewSessionAsync({ cwd = "."; mcpServers = [] })

        match sessionResult with
        | Error e -> printfn $"[Client] NewSession failed: {e}"
        | Ok session ->
            printfn $"[Client] Session created: {SessionId.value session.sessionId}"

            let modeCount =
                session.modes
                |> Option.map (fun m -> m.availableModes.Length)
                |> Option.defaultValue 0

            printfn $"[Client] Available modes: {modeCount}"
            printfn ""

            // 3. Send a prompt
            printfn "[Client] Sending prompt..."

            let prompt =
                [ ContentBlock.Text
                      { text = "Hello, Agent! How are you today?"
                        annotations = None } ]

            let! promptResult =
                client.PromptAsync(
                    { sessionId = session.sessionId
                      prompt = prompt
                      _meta = None }
                )

            match promptResult with
            | Error e -> printfn $"[Client] Prompt failed: {e}"
            | Ok result -> printfn $"[Client] Response received with stop reason: {result.stopReason}"

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
