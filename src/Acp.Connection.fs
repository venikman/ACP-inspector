namespace Acp

open System
open System.Collections.Concurrent
open System.Diagnostics
open System.Text
open System.Text.Json.Nodes
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

    let private requestIdToString (id: RequestId) =
        match id with
        | RequestId.Null -> "null"
        | RequestId.Number n -> string n
        | RequestId.String s -> s

    let private methodOfClientMessage =
        function
        | ClientToAgentMessage.Initialize _ -> "initialize"
        | ClientToAgentMessage.ProxyInitialize _ -> "proxy/initialize"
        | ClientToAgentMessage.Authenticate _ -> "authenticate"
        | ClientToAgentMessage.SessionNew _ -> "session/new"
        | ClientToAgentMessage.SessionLoad _ -> "session/load"
        | ClientToAgentMessage.SessionPrompt _ -> "session/prompt"
        | ClientToAgentMessage.SessionSetMode _ -> "session/set_mode"
        | ClientToAgentMessage.SessionCancel _ -> "session/cancel"
        | ClientToAgentMessage.ProxySuccessorRequest _
        | ClientToAgentMessage.ProxySuccessorNotification _
        | ClientToAgentMessage.ProxySuccessorResponse _
        | ClientToAgentMessage.ProxySuccessorError _ -> "proxy/successor"
        | ClientToAgentMessage.ExtRequest(methodName, _)
        | ClientToAgentMessage.ExtNotification(methodName, _)
        | ClientToAgentMessage.ExtResponse(methodName, _)
        | ClientToAgentMessage.ExtError(methodName, _) -> methodName
        | ClientToAgentMessage.FsReadTextFileResult _ -> "fs/read_text_file"
        | ClientToAgentMessage.FsWriteTextFileResult _ -> "fs/write_text_file"
        | ClientToAgentMessage.SessionRequestPermissionResult _ -> "session/request_permission"
        | ClientToAgentMessage.TerminalCreateResult _ -> "terminal/create"
        | ClientToAgentMessage.TerminalOutputResult _ -> "terminal/output"
        | ClientToAgentMessage.TerminalWaitForExitResult _ -> "terminal/wait_for_exit"
        | ClientToAgentMessage.TerminalKillResult _ -> "terminal/kill"
        | ClientToAgentMessage.TerminalReleaseResult _ -> "terminal/release"
        | ClientToAgentMessage.FsReadTextFileError _ -> "fs/read_text_file"
        | ClientToAgentMessage.FsWriteTextFileError _ -> "fs/write_text_file"
        | ClientToAgentMessage.SessionRequestPermissionError _ -> "session/request_permission"
        | ClientToAgentMessage.TerminalCreateError _ -> "terminal/create"
        | ClientToAgentMessage.TerminalOutputError _ -> "terminal/output"
        | ClientToAgentMessage.TerminalWaitForExitError _ -> "terminal/wait_for_exit"
        | ClientToAgentMessage.TerminalKillError _ -> "terminal/kill"
        | ClientToAgentMessage.TerminalReleaseError _ -> "terminal/release"

    let private methodOfAgentMessage =
        function
        | AgentToClientMessage.InitializeResult _ -> "initialize"
        | AgentToClientMessage.InitializeError _ -> "initialize"
        | AgentToClientMessage.ProxyInitializeResult _ -> "proxy/initialize"
        | AgentToClientMessage.ProxyInitializeError _ -> "proxy/initialize"
        | AgentToClientMessage.AuthenticateResult _ -> "authenticate"
        | AgentToClientMessage.AuthenticateError _ -> "authenticate"
        | AgentToClientMessage.SessionNewResult _ -> "session/new"
        | AgentToClientMessage.SessionNewError _ -> "session/new"
        | AgentToClientMessage.SessionLoadResult _ -> "session/load"
        | AgentToClientMessage.SessionLoadError _ -> "session/load"
        | AgentToClientMessage.SessionPromptResult _ -> "session/prompt"
        | AgentToClientMessage.SessionPromptError _ -> "session/prompt"
        | AgentToClientMessage.SessionSetModeResult _ -> "session/set_mode"
        | AgentToClientMessage.SessionSetModeError _ -> "session/set_mode"
        | AgentToClientMessage.SessionUpdate _ -> "session/update"
        | AgentToClientMessage.ProxySuccessorRequest _
        | AgentToClientMessage.ProxySuccessorNotification _
        | AgentToClientMessage.ProxySuccessorResponse _
        | AgentToClientMessage.ProxySuccessorError _ -> "proxy/successor"
        | AgentToClientMessage.FsReadTextFileRequest _ -> "fs/read_text_file"
        | AgentToClientMessage.FsWriteTextFileRequest _ -> "fs/write_text_file"
        | AgentToClientMessage.SessionRequestPermissionRequest _ -> "session/request_permission"
        | AgentToClientMessage.TerminalCreateRequest _ -> "terminal/create"
        | AgentToClientMessage.TerminalOutputRequest _ -> "terminal/output"
        | AgentToClientMessage.TerminalWaitForExitRequest _ -> "terminal/wait_for_exit"
        | AgentToClientMessage.TerminalKillRequest _ -> "terminal/kill"
        | AgentToClientMessage.TerminalReleaseRequest _ -> "terminal/release"
        | AgentToClientMessage.ExtRequest(methodName, _)
        | AgentToClientMessage.ExtNotification(methodName, _)
        | AgentToClientMessage.ExtResponse(methodName, _)
        | AgentToClientMessage.ExtError(methodName, _) -> methodName

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
                let transportName = transport.GetType().Name
                let methodName = methodOfClientMessage msg
                let reqIdText = requestIdToString reqId

                let requestSw = Stopwatch.StartNew()

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
                | Error e ->
                    Observability.recordCodecEncodeError
                        (Some transportName)
                        (Some "fromClient")
                        (Some methodName)
                        (Some reqIdText)

                    requestSw.Stop()

                    Observability.recordConnectionRequestDuration
                        transportName
                        "fromClient"
                        (Some methodName)
                        (Some reqIdText)
                        requestSw.Elapsed.TotalMilliseconds

                    return Error e
                | Ok json ->
                    use activity =
                        Observability.startActivity
                            "acp.client.request"
                            ActivityKind.Client
                            [ Observability.TransportTag, transportName
                              Observability.MethodTag, methodName
                              Observability.JsonRpcIdTag, reqIdText
                              Observability.DirectionTag, "fromClient" ]

                    let tcs = TaskCompletionSource<Message>()
                    pendingRequests.[reqId] <- tcs

                    try
                        let sendBytes = int64 (Encoding.UTF8.GetByteCount(json))
                        let sendSw = Stopwatch.StartNew()

                        try
                            do! transport.SendAsync(json)
                        with ex ->
                            sendSw.Stop()
                            Observability.recordException activity ex

                            Observability.recordTransportSendDuration
                                transportName
                                "fromClient"
                                (Some methodName)
                                (Some reqIdText)
                                sendSw.Elapsed.TotalMilliseconds

                            requestSw.Stop()

                            Observability.recordConnectionRequestDuration
                                transportName
                                "fromClient"
                                (Some methodName)
                                (Some reqIdText)
                                requestSw.Elapsed.TotalMilliseconds

                            raise ex

                        sendSw.Stop()

                        Observability.recordTransportSend
                            transportName
                            "fromClient"
                            (Some methodName)
                            (Some reqIdText)
                            sendBytes
                            sendSw.Elapsed.TotalMilliseconds

                        // Wait for response
                        let receiveSw = Stopwatch.StartNew()
                        let! received = transport.ReceiveAsync()
                        receiveSw.Stop()

                        match received with
                        | None ->
                            Observability.recordTransportReceive
                                transportName
                                "fromAgent"
                                (Some methodName)
                                (Some reqIdText)
                                None
                                receiveSw.Elapsed.TotalMilliseconds

                            requestSw.Stop()

                            Observability.recordConnectionRequestDuration
                                transportName
                                "fromClient"
                                (Some methodName)
                                (Some reqIdText)
                                requestSw.Elapsed.TotalMilliseconds

                            return Error ConnectionError.TransportClosed
                        | Some responseJson ->
                            let recvBytes = int64 (Encoding.UTF8.GetByteCount(responseJson))

                            Observability.recordTransportReceive
                                transportName
                                "fromAgent"
                                (Some methodName)
                                (Some reqIdText)
                                (Some recvBytes)
                                receiveSw.Elapsed.TotalMilliseconds

                            let decodeResult =
                                lock codecLock (fun () ->
                                    match Codec.decode Codec.Direction.FromAgent codecState responseJson with
                                    | Ok(newState, msg) ->
                                        codecState <- newState
                                        Ok msg
                                    | Error e ->
                                        Observability.recordCodecDecodeError
                                            (Some transportName)
                                            (Some "fromAgent")
                                            (Some methodName)
                                            (Some reqIdText)

                                        Error(ConnectionError.DecodeFailed e))

                            requestSw.Stop()

                            Observability.recordConnectionRequestDuration
                                transportName
                                "fromClient"
                                (Some methodName)
                                (Some reqIdText)
                                requestSw.Elapsed.TotalMilliseconds

                            return decodeResult
                    finally
                        pendingRequests.TryRemove(reqId) |> ignore
            }

        let sendNotification (msg: ClientToAgentMessage) : Task<Result<unit, ConnectionError>> =
            task {
                let transportName = transport.GetType().Name
                let methodName = methodOfClientMessage msg

                use activity =
                    Observability.startActivity
                        "acp.client.notification"
                        ActivityKind.Client
                        [ Observability.TransportTag, transportName
                          Observability.MethodTag, methodName
                          Observability.DirectionTag, "fromClient" ]

                let encodeResult = Codec.encode None (Message.FromClient msg)

                match encodeResult with
                | Error e ->
                    Observability.recordCodecEncodeError (Some transportName) (Some "fromClient") (Some methodName) None
                    return Error(ConnectionError.EncodeFailed(sprintf "%A" e))
                | Ok json ->
                    let sendBytes = int64 (Encoding.UTF8.GetByteCount(json))
                    let sendSw = Stopwatch.StartNew()

                    try
                        do! transport.SendAsync(json)
                    with ex ->
                        sendSw.Stop()
                        Observability.recordException activity ex

                        Observability.recordTransportSendDuration
                            transportName
                            "fromClient"
                            (Some methodName)
                            None
                            sendSw.Elapsed.TotalMilliseconds

                        raise ex

                    sendSw.Stop()

                    Observability.recordTransportSend
                        transportName
                        "fromClient"
                        (Some methodName)
                        None
                        sendBytes
                        sendSw.Elapsed.TotalMilliseconds

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
                let transportName = transport.GetType().Name
                let methodName = methodOfAgentMessage msg
                let reqIdText = requestIdToString reqId

                use activity =
                    Observability.startActivity
                        "acp.agent.response"
                        ActivityKind.Server
                        [ Observability.TransportTag, transportName
                          Observability.MethodTag, methodName
                          Observability.JsonRpcIdTag, reqIdText
                          Observability.DirectionTag, "fromAgent" ]

                let encodeResult = Codec.encode (Some reqId) (Message.FromAgent msg)

                match encodeResult with
                | Ok json ->
                    let sendBytes = int64 (Encoding.UTF8.GetByteCount(json))
                    let sendSw = Stopwatch.StartNew()

                    try
                        do! transport.SendAsync(json)
                    with ex ->
                        sendSw.Stop()
                        Observability.recordException activity ex

                        Observability.recordTransportSendDuration
                            transportName
                            "fromAgent"
                            (Some methodName)
                            (Some reqIdText)
                            sendSw.Elapsed.TotalMilliseconds

                        raise ex

                    sendSw.Stop()

                    Observability.recordTransportSend
                        transportName
                        "fromAgent"
                        (Some methodName)
                        (Some reqIdText)
                        sendBytes
                        sendSw.Elapsed.TotalMilliseconds
                | Error _ ->
                    Observability.recordCodecEncodeError
                        (Some transportName)
                        (Some "fromAgent")
                        (Some methodName)
                        (Some reqIdText)

                    ()
            }

        let sendNotification (msg: AgentToClientMessage) =
            task {
                let transportName = transport.GetType().Name
                let methodName = methodOfAgentMessage msg

                use activity =
                    Observability.startActivity
                        "acp.agent.notification"
                        ActivityKind.Server
                        [ Observability.TransportTag, transportName
                          Observability.MethodTag, methodName
                          Observability.DirectionTag, "fromAgent" ]

                let encodeResult = Codec.encode None (Message.FromAgent msg)

                match encodeResult with
                | Ok json ->
                    let sendBytes = int64 (Encoding.UTF8.GetByteCount(json))
                    let sendSw = Stopwatch.StartNew()

                    try
                        do! transport.SendAsync(json)
                    with ex ->
                        sendSw.Stop()
                        Observability.recordException activity ex

                        Observability.recordTransportSendDuration
                            transportName
                            "fromAgent"
                            (Some methodName)
                            None
                            sendSw.Elapsed.TotalMilliseconds

                        raise ex

                    sendSw.Stop()

                    Observability.recordTransportSend
                        transportName
                        "fromAgent"
                        (Some methodName)
                        None
                        sendBytes
                        sendSw.Elapsed.TotalMilliseconds
                | Error _ ->
                    Observability.recordCodecEncodeError (Some transportName) (Some "fromAgent") (Some methodName) None
                    ()
            }

        let sendRequestToClient (msg: AgentToClientMessage) : Task<Result<Message, ConnectionError>> =
            task {
                let reqId = getNextId ()
                let transportName = transport.GetType().Name
                let methodName = methodOfAgentMessage msg
                let reqIdText = requestIdToString reqId

                let requestSw = Stopwatch.StartNew()

                try
                    let encodeResult = Codec.encode (Some reqId) (Message.FromAgent msg)

                    match encodeResult with
                    | Error e ->
                        Observability.recordCodecEncodeError
                            (Some transportName)
                            (Some "fromAgent")
                            (Some methodName)
                            (Some reqIdText)

                        return Error(ConnectionError.EncodeFailed(sprintf "%A" e))
                    | Ok json ->
                        use activity =
                            Observability.startActivity
                                "acp.agent.request"
                                ActivityKind.Client
                                [ Observability.TransportTag, transportName
                                  Observability.MethodTag, methodName
                                  Observability.JsonRpcIdTag, reqIdText
                                  Observability.DirectionTag, "fromAgent" ]

                        try
                            let tcs = TaskCompletionSource<Message>()
                            pendingRequests.[reqId] <- tcs

                            try
                                let sendBytes = int64 (Encoding.UTF8.GetByteCount(json))
                                let sendSw = Stopwatch.StartNew()

                                try
                                    do! transport.SendAsync(json)
                                with ex ->
                                    sendSw.Stop()

                                    Observability.recordTransportSendDuration
                                        transportName
                                        "fromAgent"
                                        (Some methodName)
                                        (Some reqIdText)
                                        sendSw.Elapsed.TotalMilliseconds

                                    raise ex

                                sendSw.Stop()

                                Observability.recordTransportSend
                                    transportName
                                    "fromAgent"
                                    (Some methodName)
                                    (Some reqIdText)
                                    sendBytes
                                    sendSw.Elapsed.TotalMilliseconds

                                // Wait for response
                                let receiveSw = Stopwatch.StartNew()
                                let! received = transport.ReceiveAsync()
                                receiveSw.Stop()

                                match received with
                                | None ->
                                    Observability.recordTransportReceive
                                        transportName
                                        "fromClient"
                                        (Some methodName)
                                        (Some reqIdText)
                                        None
                                        receiveSw.Elapsed.TotalMilliseconds

                                    return Error ConnectionError.TransportClosed
                                | Some responseJson ->
                                    let recvBytes = int64 (Encoding.UTF8.GetByteCount(responseJson))

                                    Observability.recordTransportReceive
                                        transportName
                                        "fromClient"
                                        (Some methodName)
                                        (Some reqIdText)
                                        (Some recvBytes)
                                        receiveSw.Elapsed.TotalMilliseconds

                                    let decodeResult =
                                        lock codecLock (fun () ->
                                            match Codec.decode Codec.Direction.FromClient codecState responseJson with
                                            | Ok(newState, msg) ->
                                                codecState <- newState
                                                Ok msg
                                            | Error e ->
                                                Observability.recordCodecDecodeError
                                                    (Some transportName)
                                                    (Some "fromClient")
                                                    (Some methodName)
                                                    (Some reqIdText)

                                                Error(ConnectionError.DecodeFailed e))

                                    return decodeResult
                            finally
                                pendingRequests.TryRemove(reqId) |> ignore
                        with ex ->
                            Observability.recordException activity ex
                            return raise ex
                finally
                    requestSw.Stop()

                    Observability.recordConnectionRequestDuration
                        transportName
                        "fromAgent"
                        (Some methodName)
                        (Some reqIdText)
                        requestSw.Elapsed.TotalMilliseconds
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
                let transportName = transport.GetType().Name

                let decodeResult =
                    lock codecLock (fun () ->
                        match Codec.decode Codec.Direction.FromClient codecState json with
                        | Ok(newState, msg) ->
                            codecState <- newState
                            Ok msg
                        | Error e -> Error e)

                match decodeResult with
                | Error _ ->
                    Observability.recordCodecDecodeError (Some transportName) (Some "fromClient") None None
                    ()
                | Ok(Message.FromClient clientMsg) ->
                    let reqIdOpt = extractRequestId json
                    let methodName = methodOfClientMessage clientMsg

                    use activity =
                        Observability.startActivity
                            "acp.agent.handle"
                            ActivityKind.Server
                            [ Observability.TransportTag, transportName
                              Observability.MethodTag, methodName
                              Observability.DirectionTag, "fromClient"
                              match reqIdOpt with
                              | Some id -> Observability.JsonRpcIdTag, requestIdToString id
                              | None -> Observability.JsonRpcIdTag, "none" ]

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
                    | ClientToAgentMessage.ProxyInitialize p, Some reqId ->
                        let! result = handlers.onInitialize p

                        match result with
                        | Ok r -> do! sendResponse reqId (AgentToClientMessage.ProxyInitializeResult r)
                        | Error msg ->
                            do!
                                sendResponse
                                    reqId
                                    (AgentToClientMessage.ProxyInitializeError
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
                            let receiveSw = Stopwatch.StartNew()
                            let! received = transport.ReceiveAsync()
                            receiveSw.Stop()

                            let bytesOpt =
                                received |> Option.map (fun v -> int64 (Encoding.UTF8.GetByteCount(v)))

                            Observability.recordTransportReceive
                                (transport.GetType().Name)
                                "fromClient"
                                None
                                None
                                bytesOpt
                                receiveSw.Elapsed.TotalMilliseconds

                            match received with
                            | None -> running <- false
                            | Some json -> do! handleMessage json
                        with
                        | :? OperationCanceledException -> ()
                        | :? ObjectDisposedException -> ()
                        | ex ->
                            use activity =
                                Observability.startActivity
                                    "acp.agent.listen.error"
                                    ActivityKind.Internal
                                    [ Observability.TransportTag, transport.GetType().Name
                                      Observability.DirectionTag, "fromClient" ]

                            Observability.recordException activity ex
                            running <- false
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
        member _.SessionUpdateAsync(sessionId: SessionId, update: SessionUpdate, ?meta: JsonObject) =
            sendNotification (
                AgentToClientMessage.SessionUpdate
                    { sessionId = sessionId
                      update = update
                      _meta = meta }
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
