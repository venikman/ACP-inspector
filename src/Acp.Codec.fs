namespace Acp

open System
open System.Text.Json.Nodes

open Domain.JsonRpc
open Domain.Messaging

open CodecTypes
open CodecJson
open CodecAcpJson

/// JSON-RPC 2.0 codec + ACP method routing.
///
/// This layer:
/// - decodes raw JSON-RPC objects into typed ACP domain messages
/// - correlates JSON-RPC responses to requests via `id`
/// - reattaches context that is absent from some wire responses (e.g. sessionId)
module Codec =

    // Re-export types for backward compatibility
    type Direction = CodecTypes.Direction
    type DecodeError = CodecTypes.DecodeError
    type EncodeError = CodecTypes.EncodeError
    type PendingClientRequest = CodecTypes.PendingClientRequest
    type PendingAgentRequest = CodecTypes.PendingAgentRequest
    type CodecState = CodecTypes.CodecState

    [<RequireQualifiedAccess>]
    module CodecState =
        let empty = CodecTypes.CodecState.empty

    let decode (direction: Direction) (state: CodecState) (json: string) : Result<CodecState * Message, DecodeError> =
        let parseNode () =
            try
                Ok(JsonNode.Parse(json))
            with ex ->
                Error(DecodeError.InvalidJson ex.Message)

        let decodeCore (node: JsonNode | null) : Result<CodecState * Message, DecodeError> =
            match node with
            | null -> Error(DecodeError.InvalidJsonRpc "top-level JSON must be an object")
            | :? JsonObject as o ->
                // jsonrpc must be "2.0"
                match tryGet "jsonrpc" o with
                | None -> Error(DecodeError.InvalidJsonRpc "missing 'jsonrpc'")
                | Some v ->
                    match asString v with
                    | Error e -> Error(DecodeError.InvalidJsonRpc e)
                    | Ok ver when ver <> "2.0" -> Error(DecodeError.InvalidJsonRpc "jsonrpc must be '2.0'")
                    | Ok _ ->
                        let hasMethod = tryGetAllowNull "method" o |> Option.isSome
                        let hasResult = tryGetAllowNull "result" o |> Option.isSome
                        let hasError = tryGetAllowNull "error" o |> Option.isSome

                        // request/notification
                        if hasMethod then
                            let methodName =
                                tryGet "method" o
                                |> Option.bind (fun n -> asString n |> Result.toOption)
                                |> Option.defaultValue ""

                            if String.IsNullOrWhiteSpace methodName then
                                Error(DecodeError.InvalidJsonRpc "method must be a non-empty string")
                            else
                                let paramsNodeOpt = tryGet "params" o |> cloneOpt
                                let idNodeOpt = tryGet "id" o

                                match decodeRequestId idNodeOpt with
                                | Error e -> Error(DecodeError.InvalidJsonRpc e)
                                | Ok None ->
                                    // Notification
                                    match direction with
                                    | Direction.FromClient ->
                                        match decodeClientNotification methodName paramsNodeOpt with
                                        | Ok msg -> Ok(state, Message.FromClient msg)
                                        | Error e -> Error(DecodeError.InvalidParams(methodName, e))
                                    | Direction.FromAgent ->
                                        match decodeAgentNotification methodName paramsNodeOpt with
                                        | Ok msg -> Ok(state, Message.FromAgent msg)
                                        | Error e -> Error(DecodeError.InvalidParams(methodName, e))
                                | Ok(Some id) ->
                                    // Request
                                    match direction with
                                    | Direction.FromClient ->
                                        match decodeClientRequest methodName paramsNodeOpt with
                                        | Error e when e = "method is not a client->agent request" ->
                                            Error(DecodeError.DirectionMismatch(methodName, Direction.FromAgent))
                                        | Error e -> Error(DecodeError.InvalidParams(methodName, e))
                                        | Ok(msg, pending) ->
                                            if state.pendingClientRequests |> Map.containsKey id then
                                                Error(DecodeError.DuplicateRequestId id)
                                            else
                                                let next =
                                                    { state with
                                                        pendingClientRequests =
                                                            state.pendingClientRequests |> Map.add id pending }

                                                Ok(next, Message.FromClient msg)
                                    | Direction.FromAgent ->
                                        match decodeAgentRequest methodName paramsNodeOpt with
                                        | Error e when e = "method is not an agent->client request" ->
                                            Error(DecodeError.DirectionMismatch(methodName, Direction.FromClient))
                                        | Error e -> Error(DecodeError.InvalidParams(methodName, e))
                                        | Ok(msg, pending) ->
                                            if state.pendingAgentRequests |> Map.containsKey id then
                                                Error(DecodeError.DuplicateRequestId id)
                                            else
                                                let next =
                                                    { state with
                                                        pendingAgentRequests =
                                                            state.pendingAgentRequests |> Map.add id pending }

                                                Ok(next, Message.FromAgent msg)

                        // response (result or error)
                        else if hasResult || hasError then
                            let idNodeOpt = tryGet "id" o

                            match decodeRequestId idNodeOpt with
                            | Error e -> Error(DecodeError.InvalidJsonRpc e)
                            | Ok None -> Error(DecodeError.InvalidJsonRpc "response must include 'id'")
                            | Ok(Some id) ->
                                match direction with
                                | Direction.FromAgent ->
                                    match state.pendingClientRequests |> Map.tryFind id with
                                    | None -> Error(DecodeError.UnknownRequestId id)
                                    | Some pending ->
                                        let next =
                                            { state with
                                                pendingClientRequests = state.pendingClientRequests |> Map.remove id }

                                        if hasError then
                                            match tryGet "error" o with
                                            | None ->
                                                Error(
                                                    DecodeError.InvalidJsonRpc "response has error flag but no 'error'"
                                                )
                                            | Some errNode ->
                                                match decodeError errNode with
                                                | Error e -> Error(DecodeError.InvalidError e)
                                                | Ok err ->
                                                    let msg = CodecAcpJson.decodeAgentError pending err
                                                    Ok(next, Message.FromAgent msg)
                                        else
                                            let resultNodeOpt = tryGet "result" o |> cloneOpt

                                            match CodecAcpJson.decodeAgentResult pending resultNodeOpt with
                                            | Ok msg -> Ok(next, Message.FromAgent msg)
                                            | Error e ->
                                                Error(DecodeError.InvalidResult(methodOfPendingClient pending, e))

                                | Direction.FromClient ->
                                    match state.pendingAgentRequests |> Map.tryFind id with
                                    | None -> Error(DecodeError.UnknownRequestId id)
                                    | Some pending ->
                                        let next =
                                            { state with
                                                pendingAgentRequests = state.pendingAgentRequests |> Map.remove id }

                                        if hasError then
                                            match tryGet "error" o with
                                            | None ->
                                                Error(
                                                    DecodeError.InvalidJsonRpc "response has error flag but no 'error'"
                                                )
                                            | Some errNode ->
                                                match decodeError errNode with
                                                | Error e -> Error(DecodeError.InvalidError e)
                                                | Ok err ->
                                                    let msg = CodecAcpJson.decodeClientError pending err
                                                    Ok(next, Message.FromClient msg)
                                        else
                                            let resultNodeOpt = tryGet "result" o |> cloneOpt

                                            match CodecAcpJson.decodeClientResult pending resultNodeOpt with
                                            | Ok msg -> Ok(next, Message.FromClient msg)
                                            | Error e ->
                                                Error(DecodeError.InvalidResult(methodOfPendingAgent pending, e))

                        else
                            Error(DecodeError.InvalidJsonRpc "message must be a request, notification, or response")
            | _ -> Error(DecodeError.InvalidJsonRpc "top-level JSON must be an object")

        parseNode () |> Result.bind decodeCore

    let encode (idOpt: RequestId option) (msg: Message) : Result<string, EncodeError> =
        let objResult =
            match msg with
            | Message.FromClient c -> encodeClientMessage idOpt c
            | Message.FromAgent a -> encodeAgentMessage idOpt a

        objResult |> Result.map (fun o -> (o :> JsonNode).ToJsonString())
