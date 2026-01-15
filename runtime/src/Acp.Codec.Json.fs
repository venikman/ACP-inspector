namespace Acp

open System.Text.Json.Nodes
open FsToolkit.ErrorHandling

open Domain.JsonRpc

/// Low-level JSON parsing helpers for codec.
module internal CodecJson =

    let inline private ok v = Ok v

    let tryGet (name: string) (o: JsonObject) : JsonNode option =
        let mutable v: JsonNode | null = null

        if o.TryGetPropertyValue(name, &v) then
            match v with
            | null -> None
            | v -> Some v
        else
            None

    /// Like tryGet, but preserves JSON null as Some null.
    let tryGetAllowNull (name: string) (o: JsonObject) : (JsonNode | null) option =
        let mutable v: JsonNode | null = null
        if o.TryGetPropertyValue(name, &v) then Some v else None

    let get (name: string) (o: JsonObject) : Result<JsonNode, string> =
        match tryGet name o with
        | Some v -> ok v
        | None -> Error(sprintf "missing property '%s'" name)

    let asObject (node: JsonNode) : Result<JsonObject, string> =
        match node with
        | :? JsonObject as o -> ok o
        | _ -> Error "expected object"

    let asArray (node: JsonNode) : Result<JsonArray, string> =
        match node with
        | :? JsonArray as a -> ok a
        | _ -> Error "expected array"

    let asString (node: JsonNode) : Result<string, string> =
        match node with
        | :? JsonValue as v ->
            try
                ok (v.GetValue<string>())
            with _ ->
                Error "expected string"
        | _ -> Error "expected string"

    let asBool (node: JsonNode) : Result<bool, string> =
        match node with
        | :? JsonValue as v ->
            try
                ok (v.GetValue<bool>())
            with _ ->
                Error "expected boolean"
        | _ -> Error "expected boolean"

    let asInt (node: JsonNode) : Result<int, string> =
        match node with
        | :? JsonValue as v ->
            try
                ok (v.GetValue<int>())
            with _ ->
                Error "expected integer"
        | _ -> Error "expected integer"

    let asInt64 (node: JsonNode) : Result<int64, string> =
        match node with
        | :? JsonValue as v ->
            try
                ok (v.GetValue<int64>())
            with _ ->
                Error "expected int64"
        | _ -> Error "expected int64"

    let asUInt64 (node: JsonNode) : Result<uint64, string> =
        match node with
        | :? JsonValue as v ->
            try
                ok (v.GetValue<uint64>())
            with _ ->
                Error "expected uint64"
        | _ -> Error "expected uint64"

    let asFloat (node: JsonNode) : Result<float, string> =
        match node with
        | :? JsonValue as v ->
            try
                ok (v.GetValue<float>())
            with _ ->
                Error "expected number"
        | _ -> Error "expected number"

    let cloneOpt (nodeOpt: JsonNode option) : JsonNode option =
        nodeOpt |> Option.map (fun n -> n.DeepClone())

    let cloneOptAllowNull (nodeOpt: (JsonNode | null) option) : JsonNode option =
        match nodeOpt with
        | None -> None
        | Some node ->
            match node with
            | null -> None
            | node -> Some(node.DeepClone())

    let decodeRequestId (nodeOpt: (JsonNode | null) option) : Result<RequestId option, string> =
        match nodeOpt with
        | None -> Ok None
        | Some null -> Ok(Some RequestId.Null)
        | Some node ->
            match node with
            | :? JsonValue as v ->
                let mutable s = Unchecked.defaultof<string>
                let mutable n = Unchecked.defaultof<int64>

                if v.TryGetValue(&s) then Ok(Some(RequestId.String s))
                else if v.TryGetValue(&n) then Ok(Some(RequestId.Number n))
                else Error "id must be string, number, or null"
            | _ -> Error "id must be string, number, or null"

    let encodeRequestId (id: RequestId) : JsonNode | null =
        match id with
        | RequestId.Null -> null
        | RequestId.Number n -> JsonValue.Create(n)
        | RequestId.String s -> JsonValue.Create(s)

    let decodeError (node: JsonNode) : Result<Error, string> =
        result {
            let! o = asObject node
            let! codeNode = get "code" o
            let! msgNode = get "message" o
            let! code = asInt codeNode
            let! message = asString msgNode
            let data = tryGet "data" o |> cloneOpt

            return
                { code = code
                  message = message
                  data = data }
        }

    let encodeError (err: Error) : JsonObject =
        let o = JsonObject()
        o["code"] <- JsonValue.Create(err.code)
        o["message"] <- JsonValue.Create(err.message)

        match err.data with
        | None -> ()
        | Some d -> o["data"] <- d.DeepClone()

        o
