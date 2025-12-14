namespace Acp

open System
open System.Collections.Concurrent
open System.Threading
open System.Threading.Tasks

open Acp.Domain
open Acp.Domain.JsonRpc
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Capabilities
open Acp.Domain.Initialization
open Acp.Domain.SessionSetup
open Acp.Domain.SessionModes
open Acp.Domain.Prompting
open Acp.Domain.Messaging

/// Connection abstractions for ACP clients and agents.
/// Provides high-level APIs similar to the official TypeScript/Python SDKs.
module Connection =

    /// Error type for connection operations.
    [<RequireQualifiedAccess>]
    type ConnectionError =
        | TransportClosed
        | EncodeFailed of string
        | DecodeFailed of Codec.DecodeError
        | Timeout
        | ProtocolError of string

    /// Handlers for agent-side message processing.
    type AgentHandlers =
        { onInitialize: InitializeParams -> Task<Result<InitializeResult, string>>
          onNewSession: NewSessionParams -> Task<Result<NewSessionResult, string>>
          onPrompt: SessionPromptParams -> Task<Result<SessionPromptResult, string>>
          onCancel: SessionCancelParams -> Task<unit>
          onSetMode: SetSessionModeParams -> Task<Result<SetSessionModeResult, string>> }

    // ============================================================
    // ClientConnection
    // ============================================================

    /// Client-side connection for sending requests to an agent.
    type ClientConnection(transport: Transport.ITransport) =
        let mutable codecState = Codec.CodecState.empty
        let mutable nextId = 1L

        let pendingRequests =
            ConcurrentDictionary<RequestId, TaskCompletionSource<Message>>()

        let codecLock = obj ()

        let getNextId () =
            let id = Interlocked.Increment(&nextId)
            RequestId.Number id

        let sendRequest (msg: ClientToAgentMessage) : Task<Result<Message, ConnectionError>> =
            task {
                let reqId = getNextId ()

                let encodeResult =
                    lock codecLock (fun () ->
                        let result = Codec.encode (Some reqId) (Message.FromClient msg)

                        match result with
                        | Ok json ->
                            // Update codec state to track pending request
                            match Codec.decode Codec.Direction.FromClient codecState json with
                            | Ok(newState, _) -> codecState <- newState
                            | Error _ -> ()

                            Ok json
                        | Error e -> Error(ConnectionError.EncodeFailed(sprintf "%A" e)))

                match encodeResult with
                | Error e -> return Error e
                | Ok json ->
                    let tcs = TaskCompletionSource<Message>()
                    pendingRequests.[reqId] <- tcs

                    do! transport.SendAsync(json)

                    // Wait for response
                    let! received = transport.ReceiveAsync()

                    match received with
                    | None ->
                        pendingRequests.TryRemove(reqId) |> ignore
                        return Error ConnectionError.TransportClosed
                    | Some responseJson ->
                        let decodeResult =
                            lock codecLock (fun () ->
                                match Codec.decode Codec.Direction.FromAgent codecState responseJson with
                                | Ok(newState, msg) ->
                                    codecState <- newState
                                    Ok msg
                                | Error e -> Error(ConnectionError.DecodeFailed e))

                        pendingRequests.TryRemove(reqId) |> ignore
                        return decodeResult
            }

        let sendNotification (msg: ClientToAgentMessage) : Task<Result<unit, ConnectionError>> =
            task {
                let encodeResult = Codec.encode None (Message.FromClient msg)

                match encodeResult with
                | Error e -> return Error(ConnectionError.EncodeFailed(sprintf "%A" e))
                | Ok json ->
                    do! transport.SendAsync(json)
                    return Ok()
            }

        /// Send initialize request.
        member _.InitializeAsync(params': InitializeParams) : Task<Result<InitializeResult, ConnectionError>> =
            task {
                let! result = sendRequest (ClientToAgentMessage.Initialize params')

                match result with
                | Error e -> return Error e
                | Ok(Message.FromAgent(AgentToClientMessage.InitializeResult r)) -> return Ok r
                | Ok(Message.FromAgent(AgentToClientMessage.InitializeError e)) ->
                    return Error(ConnectionError.ProtocolError e.message)
                | Ok other -> return Error(ConnectionError.ProtocolError(sprintf "Unexpected response: %A" other))
            }

        /// Send session/new request.
        member _.NewSessionAsync(params': NewSessionParams) : Task<Result<NewSessionResult, ConnectionError>> =
            task {
                let! result = sendRequest (ClientToAgentMessage.SessionNew params')

                match result with
                | Error e -> return Error e
                | Ok(Message.FromAgent(AgentToClientMessage.SessionNewResult r)) -> return Ok r
                | Ok(Message.FromAgent(AgentToClientMessage.SessionNewError e)) ->
                    return Error(ConnectionError.ProtocolError e.message)
                | Ok other -> return Error(ConnectionError.ProtocolError(sprintf "Unexpected response: %A" other))
            }

        /// Send session/prompt request.
        member _.PromptAsync(params': SessionPromptParams) : Task<Result<SessionPromptResult, ConnectionError>> =
            task {
                let! result = sendRequest (ClientToAgentMessage.SessionPrompt params')

                match result with
                | Error e -> return Error e
                | Ok(Message.FromAgent(AgentToClientMessage.SessionPromptResult r)) -> return Ok r
                | Ok(Message.FromAgent(AgentToClientMessage.SessionPromptError(_, e))) ->
                    return Error(ConnectionError.ProtocolError e.message)
                | Ok other -> return Error(ConnectionError.ProtocolError(sprintf "Unexpected response: %A" other))
            }

        /// Send session/cancel notification.
        member _.CancelAsync(sessionId: SessionId) : Task<Result<unit, ConnectionError>> =
            sendNotification (ClientToAgentMessage.SessionCancel { sessionId = sessionId })

        /// Send session/set_mode request.
        member _.SetModeAsync
            (sessionId: SessionId, modeId: SessionModeId)
            : Task<Result<SetSessionModeResult, ConnectionError>> =
            task {
                let! result =
                    sendRequest (
                        ClientToAgentMessage.SessionSetMode
                            { sessionId = sessionId
                              modeId = modeId }
                    )

                match result with
                | Error e -> return Error e
                | Ok(Message.FromAgent(AgentToClientMessage.SessionSetModeResult r)) -> return Ok r
                | Ok(Message.FromAgent(AgentToClientMessage.SessionSetModeError(_, e))) ->
                    return Error(ConnectionError.ProtocolError e.message)
                | Ok other -> return Error(ConnectionError.ProtocolError(sprintf "Unexpected response: %A" other))
            }

        /// Close the connection.
        member _.CloseAsync() = transport.CloseAsync()

    // ============================================================
    // AgentConnection
    // ============================================================

    /// Agent-side connection for receiving requests from a client.
    type AgentConnection(transport: Transport.ITransport, handlers: AgentHandlers) =
        let mutable codecState = Codec.CodecState.empty
        let mutable nextId = 1L
        let mutable running = false
        let mutable listenTask: Task option = None

        let pendingRequests =
            ConcurrentDictionary<RequestId, TaskCompletionSource<Message>>()

        let codecLock = obj ()
        let cts = new CancellationTokenSource()

        let getNextId () =
            let id = Interlocked.Increment(&nextId)
            RequestId.Number id

        let sendResponse (reqId: RequestId) (msg: AgentToClientMessage) =
            task {
                let encodeResult = Codec.encode (Some reqId) (Message.FromAgent msg)

                match encodeResult with
                | Ok json -> do! transport.SendAsync(json)
                | Error _ -> ()
            }

        let sendNotification (msg: AgentToClientMessage) =
            task {
                let encodeResult = Codec.encode None (Message.FromAgent msg)

                match encodeResult with
                | Ok json -> do! transport.SendAsync(json)
                | Error _ -> ()
            }

        let sendRequestToClient (msg: AgentToClientMessage) : Task<Result<Message, ConnectionError>> =
            task {
                let reqId = getNextId ()
                let encodeResult = Codec.encode (Some reqId) (Message.FromAgent msg)

                match encodeResult with
                | Error e -> return Error(ConnectionError.EncodeFailed(sprintf "%A" e))
                | Ok json ->
                    let tcs = TaskCompletionSource<Message>()
                    pendingRequests.[reqId] <- tcs

                    do! transport.SendAsync(json)

                    // Wait for response
                    let! received = transport.ReceiveAsync()

                    match received with
                    | None ->
                        pendingRequests.TryRemove(reqId) |> ignore
                        return Error ConnectionError.TransportClosed
                    | Some responseJson ->
                        let decodeResult =
                            lock codecLock (fun () ->
                                match Codec.decode Codec.Direction.FromClient codecState responseJson with
                                | Ok(newState, msg) ->
                                    codecState <- newState
                                    Ok msg
                                | Error e -> Error(ConnectionError.DecodeFailed e))

                        pendingRequests.TryRemove(reqId) |> ignore
                        return decodeResult
            }

        let extractRequestId (json: string) : RequestId option =
            try
                match System.Text.Json.Nodes.JsonNode.Parse(json) with
                | null -> None
                | node ->
                    let jsonObj = node.AsObject()
                    let mutable idNode: System.Text.Json.Nodes.JsonNode | null = null

                    if jsonObj.TryGetPropertyValue("id", &idNode) then
                        match Option.ofObj idNode with
                        | None -> Some RequestId.Null
                        | Some idNode ->
                            match idNode.GetValueKind() with
                            | System.Text.Json.JsonValueKind.Number -> Some(RequestId.Number(idNode.GetValue<int64>()))
                            | System.Text.Json.JsonValueKind.String -> Some(RequestId.String(idNode.GetValue<string>()))
                            | System.Text.Json.JsonValueKind.Null -> Some RequestId.Null
                            | _ -> None
                    else
                        None
            with _ ->
                None

        let handleMessage (json: string) =
            task {
                let decodeResult =
                    lock codecLock (fun () ->
                        match Codec.decode Codec.Direction.FromClient codecState json with
                        | Ok(newState, msg) ->
                            codecState <- newState
                            Ok msg
                        | Error e -> Error e)

                match decodeResult with
                | Error _ -> ()
                | Ok(Message.FromClient clientMsg) ->
                    let reqIdOpt = extractRequestId json

                    match clientMsg, reqIdOpt with
                    | ClientToAgentMessage.Initialize p, Some reqId ->
                        let! result = handlers.onInitialize p

                        match result with
                        | Ok r -> do! sendResponse reqId (AgentToClientMessage.InitializeResult r)
                        | Error msg ->
                            do!
                                sendResponse
                                    reqId
                                    (AgentToClientMessage.InitializeError
                                        { code = -32603
                                          message = msg
                                          data = None })

                    | ClientToAgentMessage.SessionNew p, Some reqId ->
                        let! result = handlers.onNewSession p

                        match result with
                        | Ok r -> do! sendResponse reqId (AgentToClientMessage.SessionNewResult r)
                        | Error msg ->
                            do!
                                sendResponse
                                    reqId
                                    (AgentToClientMessage.SessionNewError
                                        { code = -32603
                                          message = msg
                                          data = None })

                    | ClientToAgentMessage.SessionPrompt p, Some reqId ->
                        let! result = handlers.onPrompt p

                        match result with
                        | Ok r -> do! sendResponse reqId (AgentToClientMessage.SessionPromptResult r)
                        | Error msg ->
                            do!
                                sendResponse
                                    reqId
                                    (AgentToClientMessage.SessionPromptError(
                                        p,
                                        { code = -32603
                                          message = msg
                                          data = None }
                                    ))

                    | ClientToAgentMessage.SessionSetMode p, Some reqId ->
                        let! result = handlers.onSetMode p

                        match result with
                        | Ok r -> do! sendResponse reqId (AgentToClientMessage.SessionSetModeResult r)
                        | Error msg ->
                            do!
                                sendResponse
                                    reqId
                                    (AgentToClientMessage.SessionSetModeError(
                                        p,
                                        { code = -32603
                                          message = msg
                                          data = None }
                                    ))

                    | ClientToAgentMessage.SessionCancel p, _ -> do! handlers.onCancel p

                    | _ -> ()
                | Ok(Message.FromAgent _) -> () // Responses to our requests
            }

        /// Start listening for incoming messages.
        member _.StartListening() : Task =
            running <- true

            let t =
                task {
                    while running && not cts.Token.IsCancellationRequested do
                        try
                            let! received = transport.ReceiveAsync()

                            match received with
                            | None -> running <- false
                            | Some json -> do! handleMessage json
                        with _ ->
                            ()
                }

            listenTask <- Some t
            t

        /// Stop listening.
        member _.StopAsync() =
            task {
                running <- false
                cts.Cancel()
                do! transport.CloseAsync()
            }

        /// Send session/update notification to client.
        member _.SessionUpdateAsync(sessionId: SessionId, update: SessionUpdate) =
            sendNotification (
                AgentToClientMessage.SessionUpdate
                    { sessionId = sessionId
                      update = update }
            )

        /// Request fs/read_text_file from client.
        member _.ReadTextFileAsync
            (sessionId: SessionId, path: string)
            : Task<Result<ReadTextFileResult, ConnectionError>> =
            task {
                let params' =
                    { sessionId = sessionId
                      path = path
                      line = None
                      limit = None }

                let! result = sendRequestToClient (AgentToClientMessage.FsReadTextFileRequest params')

                match result with
                | Error e -> return Error e
                | Ok(Message.FromClient(ClientToAgentMessage.FsReadTextFileResult r)) -> return Ok r
                | Ok(Message.FromClient(ClientToAgentMessage.FsReadTextFileError(_, e))) ->
                    return Error(ConnectionError.ProtocolError e.message)
                | Ok other -> return Error(ConnectionError.ProtocolError(sprintf "Unexpected response: %A" other))
            }

        /// Request fs/write_text_file from client.
        member _.WriteTextFileAsync
            (sessionId: SessionId, path: string, content: string)
            : Task<Result<WriteTextFileResult, ConnectionError>> =
            task {
                let params' =
                    { sessionId = sessionId
                      path = path
                      content = content }

                let! result = sendRequestToClient (AgentToClientMessage.FsWriteTextFileRequest params')

                match result with
                | Error e -> return Error e
                | Ok(Message.FromClient(ClientToAgentMessage.FsWriteTextFileResult r)) -> return Ok r
                | Ok(Message.FromClient(ClientToAgentMessage.FsWriteTextFileError(_, e))) ->
                    return Error(ConnectionError.ProtocolError e.message)
                | Ok other -> return Error(ConnectionError.ProtocolError(sprintf "Unexpected response: %A" other))
            }

        /// Request session/request_permission from client.
        member _.RequestPermissionAsync
            (sessionId: SessionId, toolCall: ToolCallUpdate, options: PermissionOption list)
            : Task<Result<RequestPermissionResult, ConnectionError>> =
            task {
                let params' =
                    { sessionId = sessionId
                      toolCall = toolCall
                      options = options }

                let! result = sendRequestToClient (AgentToClientMessage.SessionRequestPermissionRequest params')

                match result with
                | Error e -> return Error e
                | Ok(Message.FromClient(ClientToAgentMessage.SessionRequestPermissionResult r)) -> return Ok r
                | Ok(Message.FromClient(ClientToAgentMessage.SessionRequestPermissionError(_, e))) ->
                    return Error(ConnectionError.ProtocolError e.message)
                | Ok other -> return Error(ConnectionError.ProtocolError(sprintf "Unexpected response: %A" other))
            }
