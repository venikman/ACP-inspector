namespace Acp

open System.Text.Json.Nodes
open FsToolkit.ErrorHandling

open Domain
open Domain.JsonRpc
open Domain.PrimitivesAndParties
open Domain.Capabilities
open Domain.Authentication
open Domain.Initialization
open Domain.SessionSetup
open Domain.SessionModes
open Domain.Prompting
open Domain.Messaging
open Domain.Proxying

open CodecTypes
open CodecJson

/// ACP-specific JSON encoders and decoders for protocol messages.
module internal CodecAcpJson =


    let private decodeSessionId (node: JsonNode) : Result<SessionId, string> =
        node |> asString |> Result.map SessionId

    let private encodeSessionId (SessionId s) : JsonNode | null = JsonValue.Create(s)

    let private decodeModeId (node: JsonNode) : Result<SessionModeId, string> =
        node |> asString |> Result.map SessionModeId

    let private encodeModeId (SessionModeId s) : JsonNode | null = JsonValue.Create(s)

    let private decodeImplementationInfo (node: JsonNode) : Result<ImplementationInfo, string> =
        result {
            let! o = asObject node
            let! nameNode = get "name" o
            let! versionNode = get "version" o
            let! name = asString nameNode
            let! version = asString versionNode

            let title =
                tryGet "title" o
                |> Option.bind (fun n ->
                    match n with
                    | :? JsonValue as _ -> asString n |> Result.toOption
                    | _ -> None)

            return
                { name = name
                  title = title
                  version = version }
        }

    let private encodeImplementationInfo (info: ImplementationInfo) : JsonObject =
        let o = JsonObject()
        o["name"] <- JsonValue.Create(info.name)
        o["version"] <- JsonValue.Create(info.version)

        match info.title with
        | None -> ()
        | Some t -> o["title"] <- JsonValue.Create(t)

        o

    let private decodeAnnotations (nodeOpt: JsonNode option) : Result<Annotations option, string> =
        match nodeOpt with
        | None -> Ok None
        | Some node ->
            result {
                let! o = asObject node

                let audience =
                    match tryGet "audience" o with
                    | None -> None
                    | Some a ->
                        match a with
                        | :? JsonArray as arr ->
                            let items =
                                arr
                                |> Seq.choose (fun n ->
                                    match n with
                                    | null -> None
                                    | n -> asString n |> Result.toOption)
                                |> Seq.toList

                            Some items
                        | _ -> None

                let priority =
                    match tryGet "priority" o with
                    | None -> None
                    | Some p -> asFloat p |> Result.toOption

                let lastModified =
                    match tryGet "lastModified" o with
                    | None -> None
                    | Some s -> asString s |> Result.toOption

                return
                    Some
                        { audience = audience
                          priority = priority
                          lastModified = lastModified }
            }

    let private encodeAnnotations (aOpt: Annotations option) : JsonNode option =
        match aOpt with
        | None -> None
        | Some a ->
            let o = JsonObject()

            match a.audience with
            | None -> ()
            | Some items ->
                let arr = JsonArray()
                items |> List.iter (fun s -> arr.Add(JsonValue.Create(s)))
                o["audience"] <- arr

            match a.priority with
            | None -> ()
            | Some p -> o["priority"] <- JsonValue.Create(p)

            match a.lastModified with
            | None -> ()
            | Some s -> o["lastModified"] <- JsonValue.Create(s)

            Some(o :> JsonNode)

    // ---- Capabilities ----

    let private decodeFileSystemCaps (nodeOpt: JsonNode option) : Result<FileSystemCapabilities, string> =
        match nodeOpt with
        | None ->
            Ok
                { readTextFile = false
                  writeTextFile = false }
        | Some node ->
            result {
                let! o = asObject node

                let read =
                    match tryGet "readTextFile" o with
                    | None -> false
                    | Some v -> asBool v |> Result.defaultValue false

                let write =
                    match tryGet "writeTextFile" o with
                    | None -> false
                    | Some v -> asBool v |> Result.defaultValue false

                return
                    { readTextFile = read
                      writeTextFile = write }
            }

    let private encodeFileSystemCaps (fs: FileSystemCapabilities) : JsonObject =
        let o = JsonObject()
        o["readTextFile"] <- JsonValue.Create(fs.readTextFile)
        o["writeTextFile"] <- JsonValue.Create(fs.writeTextFile)
        o

    let private decodeClientCaps (nodeOpt: JsonNode option) : Result<ClientCapabilities, string> =
        match nodeOpt with
        | None ->
            Ok
                { fs =
                    { readTextFile = false
                      writeTextFile = false }
                  terminal = false }
        | Some node ->
            result {
                let! o = asObject node
                let! fs = decodeFileSystemCaps (tryGet "fs" o)

                let terminal =
                    match tryGet "terminal" o with
                    | None -> false
                    | Some v -> asBool v |> Result.defaultValue false

                return { fs = fs; terminal = terminal }
            }

    let private encodeClientCaps (caps: ClientCapabilities) : JsonObject =
        let o = JsonObject()
        o["fs"] <- encodeFileSystemCaps caps.fs
        o["terminal"] <- JsonValue.Create(caps.terminal)
        o

    let private decodeMcpCaps (nodeOpt: JsonNode option) : Result<McpCapabilities, string> =
        match nodeOpt with
        | None -> Ok { http = false; sse = false }
        | Some node ->
            result {
                let! o = asObject node

                let http =
                    match tryGet "http" o with
                    | None -> false
                    | Some v -> asBool v |> Result.defaultValue false

                let sse =
                    match tryGet "sse" o with
                    | None -> false
                    | Some v -> asBool v |> Result.defaultValue false

                return { http = http; sse = sse }
            }

    let private encodeMcpCaps (caps: McpCapabilities) : JsonObject =
        let o = JsonObject()
        o["http"] <- JsonValue.Create(caps.http)
        o["sse"] <- JsonValue.Create(caps.sse)
        o

    let private decodePromptCaps (nodeOpt: JsonNode option) : Result<PromptCapabilities, string> =
        match nodeOpt with
        | None ->
            Ok
                { audio = false
                  image = false
                  embeddedContext = false }
        | Some node ->
            result {
                let! o = asObject node

                let audio =
                    match tryGet "audio" o with
                    | None -> false
                    | Some v -> asBool v |> Result.defaultValue false

                let image =
                    match tryGet "image" o with
                    | None -> false
                    | Some v -> asBool v |> Result.defaultValue false

                let embedded =
                    match tryGet "embeddedContext" o with
                    | None -> false
                    | Some v -> asBool v |> Result.defaultValue false

                return
                    { audio = audio
                      image = image
                      embeddedContext = embedded }
            }

    let private encodePromptCaps (caps: PromptCapabilities) : JsonObject =
        let o = JsonObject()
        o["audio"] <- JsonValue.Create(caps.audio)
        o["image"] <- JsonValue.Create(caps.image)
        o["embeddedContext"] <- JsonValue.Create(caps.embeddedContext)
        o

    let private decodeAgentCaps (nodeOpt: JsonNode option) : Result<AgentCapabilities, string> =
        match nodeOpt with
        | None ->
            Ok
                { loadSession = false
                  mcpCapabilities = { http = false; sse = false }
                  promptCapabilities =
                    { audio = false
                      image = false
                      embeddedContext = false }
                  sessionCapabilities = SessionCapabilities.empty }
        | Some node ->
            result {
                let! o = asObject node

                let load =
                    match tryGet "loadSession" o with
                    | None -> false
                    | Some v -> asBool v |> Result.defaultValue false

                let! mcp = decodeMcpCaps (tryGet "mcpCapabilities" o)
                let! prompt = decodePromptCaps (tryGet "promptCapabilities" o)

                // sessionCapabilities is an object today; treat missing as empty.
                let sessionCaps = SessionCapabilities.empty

                return
                    { loadSession = load
                      mcpCapabilities = mcp
                      promptCapabilities = prompt
                      sessionCapabilities = sessionCaps }
            }

    let private encodeAgentCaps (caps: AgentCapabilities) : JsonObject =
        let o = JsonObject()
        o["loadSession"] <- JsonValue.Create(caps.loadSession)
        o["mcpCapabilities"] <- encodeMcpCaps caps.mcpCapabilities
        o["promptCapabilities"] <- encodePromptCaps caps.promptCapabilities
        o["sessionCapabilities"] <- JsonObject()
        o

    // ---- Authentication ----

    let private decodeAuthMethod (node: JsonNode) : Result<AuthMethod, string> =
        result {
            let! o = asObject node
            let! idNode = get "id" o
            let! nameNode = get "name" o
            let! id = asString idNode
            let! name = asString nameNode

            let description =
                match tryGet "description" o with
                | None -> None
                | Some d -> asString d |> Result.toOption

            return
                { id = id
                  name = name
                  description = description }
        }

    let private encodeAuthMethod (m: AuthMethod) : JsonObject =
        let o = JsonObject()
        o["id"] <- JsonValue.Create(m.id)
        o["name"] <- JsonValue.Create(m.name)

        match m.description with
        | None -> ()
        | Some d -> o["description"] <- JsonValue.Create(d)

        o

    let private decodeAuthenticateParams (node: JsonNode) : Result<AuthenticateParams, string> =
        result {
            let! o = asObject node
            let! midNode = get "methodId" o
            let! methodId = asString midNode
            return { methodId = methodId }
        }

    let private encodeAuthenticateParams (p: AuthenticateParams) : JsonObject =
        let o = JsonObject()
        o["methodId"] <- JsonValue.Create(p.methodId)
        o

    let private decodeAuthenticateResult (_nodeOpt: JsonNode option) : AuthenticateResult = AuthenticateResult.empty

    let private encodeAuthenticateResult (_r: AuthenticateResult) : JsonObject = JsonObject()

    // ---- Initialization ----

    let private decodeInitializeParams (node: JsonNode) : Result<InitializeParams, string> =
        result {
            let! o = asObject node
            let! pvNode = get "protocolVersion" o
            let! pv = asInt pvNode
            let! cc = decodeClientCaps (tryGet "clientCapabilities" o)

            let clientInfo =
                match tryGet "clientInfo" o with
                | None -> None
                | Some ci -> decodeImplementationInfo ci |> Result.toOption

            return
                { protocolVersion = pv
                  clientCapabilities = cc
                  clientInfo = clientInfo }
        }

    let private encodeInitializeParams (p: InitializeParams) : JsonObject =
        let o = JsonObject()
        o["protocolVersion"] <- JsonValue.Create(p.protocolVersion)
        o["clientCapabilities"] <- encodeClientCaps p.clientCapabilities

        match p.clientInfo with
        | None -> ()
        | Some ci -> o["clientInfo"] <- encodeImplementationInfo ci

        o

    let private decodeInitializeResult (node: JsonNode) : Result<InitializeResult, string> =
        result {
            let! o = asObject node
            let! pvNode = get "protocolVersion" o
            let! pv = asInt pvNode
            let! ac = decodeAgentCaps (tryGet "agentCapabilities" o)

            let agentInfo =
                match tryGet "agentInfo" o with
                | None -> None
                | Some ai -> decodeImplementationInfo ai |> Result.toOption

            let authMethods =
                match tryGet "authMethods" o with
                | None -> []
                | Some am ->
                    match am with
                    | :? JsonArray as arr ->
                        arr
                        |> Seq.choose (fun n ->
                            match n with
                            | null -> None
                            | n -> decodeAuthMethod n |> Result.toOption)
                        |> Seq.toList
                    | _ -> []

            return
                { protocolVersion = pv
                  agentCapabilities = ac
                  agentInfo = agentInfo
                  authMethods = authMethods }
        }

    let private encodeInitializeResult (r: InitializeResult) : JsonObject =
        let o = JsonObject()
        o["protocolVersion"] <- JsonValue.Create(r.protocolVersion)
        o["agentCapabilities"] <- encodeAgentCaps r.agentCapabilities

        match r.agentInfo with
        | None -> ()
        | Some ai -> o["agentInfo"] <- encodeImplementationInfo ai

        let am = JsonArray()
        r.authMethods |> List.iter (fun m -> am.Add(encodeAuthMethod m))
        o["authMethods"] <- am
        o

    // ---- Session modes ----

    let private decodeSessionMode (node: JsonNode) : Result<SessionMode, string> =
        result {
            let! o = asObject node
            let! idNode = get "id" o
            let! nameNode = get "name" o
            let! id = decodeModeId idNode
            let! name = asString nameNode

            let description =
                match tryGet "description" o with
                | None -> None
                | Some d -> asString d |> Result.toOption

            return
                { id = id
                  name = name
                  description = description }
        }

    let private encodeSessionMode (m: SessionMode) : JsonObject =
        let o = JsonObject()
        o["id"] <- encodeModeId m.id
        o["name"] <- JsonValue.Create(m.name)

        match m.description with
        | None -> ()
        | Some d -> o["description"] <- JsonValue.Create(d)

        o

    let private decodeModeState (nodeOpt: JsonNode option) : Result<SessionModeState option, string> =
        match nodeOpt with
        | None -> Ok None
        | Some node ->
            result {
                let! o = asObject node
                let! curNode = get "currentModeId" o
                let! modesNode = get "availableModes" o
                let! currentModeId = decodeModeId curNode
                let! arr = asArray modesNode

                let modes =
                    arr
                    |> Seq.choose (fun n ->
                        match n with
                        | null -> None
                        | n -> decodeSessionMode n |> Result.toOption)
                    |> Seq.toList

                return
                    Some
                        { currentModeId = currentModeId
                          availableModes = modes }
            }

    let private encodeModeState (msOpt: SessionModeState option) : JsonNode option =
        match msOpt with
        | None -> None
        | Some ms ->
            let o = JsonObject()
            o["currentModeId"] <- encodeModeId ms.currentModeId
            let arr = JsonArray()
            ms.availableModes |> List.iter (fun m -> arr.Add(encodeSessionMode m))
            o["availableModes"] <- arr
            Some(o :> JsonNode)

    let private decodeCurrentModeUpdate (node: JsonNode) : Result<CurrentModeUpdate, string> =
        result {
            let! o = asObject node
            let! m = get "currentModeId" o
            let! id = decodeModeId m
            return { currentModeId = id }
        }

    let private encodeCurrentModeUpdate (u: CurrentModeUpdate) : JsonObject =
        let o = JsonObject()
        o["currentModeId"] <- encodeModeId u.currentModeId
        o

    let private decodeSetSessionModeParams (node: JsonNode) : Result<SetSessionModeParams, string> =
        result {
            let! o = asObject node
            let! sidNode = get "sessionId" o
            let! midNode = get "modeId" o
            let! sessionId = decodeSessionId sidNode
            let! modeId = decodeModeId midNode

            return
                { sessionId = sessionId
                  modeId = modeId }
        }

    let private encodeSetSessionModeParams (p: SetSessionModeParams) : JsonObject =
        let o = JsonObject()
        o["sessionId"] <- encodeSessionId p.sessionId
        o["modeId"] <- encodeModeId p.modeId
        o

    // ---- Session setup (mcp servers) ----

    let private decodeEnvVar (node: JsonNode) : Result<EnvVariable, string> =
        result {
            let! o = asObject node
            let! n = get "name" o |> Result.bind asString
            let! v = get "value" o |> Result.bind asString
            return { name = n; value = v }
        }

    let private encodeEnvVar (e: EnvVariable) : JsonObject =
        let o = JsonObject()
        o["name"] <- JsonValue.Create(e.name)
        o["value"] <- JsonValue.Create(e.value)
        o

    let private decodeHttpHeader (node: JsonNode) : Result<HttpHeader, string> =
        result {
            let! o = asObject node
            let! n = get "name" o |> Result.bind asString
            let! v = get "value" o |> Result.bind asString
            return { name = n; value = v }
        }

    let private encodeHttpHeader (h: HttpHeader) : JsonObject =
        let o = JsonObject()
        o["name"] <- JsonValue.Create(h.name)
        o["value"] <- JsonValue.Create(h.value)
        o

    let private decodeMcpServer (node: JsonNode) : Result<McpServer, string> =
        result {
            let! o = asObject node
            let transport = tryGet "transport" o |> Option.bind (asString >> Result.toOption)

            let kind =
                transport
                |> Option.orElseWith (fun () -> tryGet "type" o |> Option.bind (asString >> Result.toOption))

            match kind with
            | Some "http" ->
                let! name = get "name" o |> Result.bind asString
                let! url = get "url" o |> Result.bind asString
                let! headersNode = get "headers" o
                let! headersArr = asArray headersNode

                let headers =
                    headersArr
                    |> Seq.choose (fun n ->
                        match n with
                        | null -> None
                        | n -> decodeHttpHeader n |> Result.toOption)
                    |> Seq.toList

                return
                    McpServer.Http
                        { name = name
                          url = url
                          headers = headers }

            | Some "sse" ->
                let! name = get "name" o |> Result.bind asString
                let! url = get "url" o |> Result.bind asString
                let! headersNode = get "headers" o
                let! headersArr = asArray headersNode

                let headers =
                    headersArr
                    |> Seq.choose (fun n ->
                        match n with
                        | null -> None
                        | n -> decodeHttpHeader n |> Result.toOption)
                    |> Seq.toList

                return
                    McpServer.Sse
                        { name = name
                          url = url
                          headers = headers }

            | Some "acp" ->
                let! uuid = get "uuid" o |> Result.bind asString

                let name =
                    tryGet "name" o
                    |> Option.bind (asString >> Result.toOption)
                    |> Option.defaultValue uuid

                return McpServer.Acp { name = name; uuid = uuid }

            | _ ->
                // Stdio has no explicit discriminator in schema; fall back to stdio shape.
                let! name = get "name" o |> Result.bind asString
                let! command = get "command" o |> Result.bind asString

                let! argsNode = get "args" o
                let! argsArr = asArray argsNode

                let args =
                    argsArr
                    |> Seq.choose (fun n ->
                        match n with
                        | null -> None
                        | n -> asString n |> Result.toOption)
                    |> Seq.toList

                let! envNode = get "env" o
                let! envArr = asArray envNode

                let env =
                    envArr
                    |> Seq.choose (fun n ->
                        match n with
                        | null -> None
                        | n -> decodeEnvVar n |> Result.toOption)
                    |> Seq.toList

                return
                    McpServer.Stdio
                        { name = name
                          command = command
                          args = args
                          env = env }
        }

    let private encodeMcpServer (s: McpServer) : JsonObject =
        match s with
        | McpServer.Http v ->
            let o = JsonObject()
            o["type"] <- JsonValue.Create("http")
            o["name"] <- JsonValue.Create(v.name)
            o["url"] <- JsonValue.Create(v.url)
            let headers = JsonArray()
            v.headers |> List.iter (fun h -> headers.Add(encodeHttpHeader h))
            o["headers"] <- headers
            o
        | McpServer.Sse v ->
            let o = JsonObject()
            o["type"] <- JsonValue.Create("sse")
            o["name"] <- JsonValue.Create(v.name)
            o["url"] <- JsonValue.Create(v.url)
            let headers = JsonArray()
            v.headers |> List.iter (fun h -> headers.Add(encodeHttpHeader h))
            o["headers"] <- headers
            o
        | McpServer.Stdio v ->
            let o = JsonObject()
            o["name"] <- JsonValue.Create(v.name)
            o["command"] <- JsonValue.Create(v.command)
            let args = JsonArray()
            v.args |> List.iter (fun a -> args.Add(JsonValue.Create(a)))
            o["args"] <- args
            let env = JsonArray()
            v.env |> List.iter (fun e -> env.Add(encodeEnvVar e))
            o["env"] <- env
            o
        | McpServer.Acp v ->
            let o = JsonObject()
            o["transport"] <- JsonValue.Create("acp")
            o["name"] <- JsonValue.Create(v.name)
            o["uuid"] <- JsonValue.Create(v.uuid)
            o

    let private decodeNewSessionParams (node: JsonNode) : Result<NewSessionParams, string> =
        result {
            let! o = asObject node
            let! cwd = get "cwd" o |> Result.bind asString
            let! msNode = get "mcpServers" o
            let! arr = asArray msNode

            let servers =
                arr
                |> Seq.choose (fun n ->
                    match n with
                    | null -> None
                    | n -> decodeMcpServer n |> Result.toOption)
                |> Seq.toList

            return { cwd = cwd; mcpServers = servers }
        }

    let private encodeNewSessionParams (p: NewSessionParams) : JsonObject =
        let o = JsonObject()
        o["cwd"] <- JsonValue.Create(p.cwd)
        let arr = JsonArray()
        p.mcpServers |> List.iter (fun s -> arr.Add(encodeMcpServer s))
        o["mcpServers"] <- arr
        o

    let private decodeLoadSessionParams (node: JsonNode) : Result<LoadSessionParams, string> =
        result {
            let! o = asObject node
            let! sidNode = get "sessionId" o
            let! sessionId = decodeSessionId sidNode
            let! cwd = get "cwd" o |> Result.bind asString
            let! msNode = get "mcpServers" o
            let! arr = asArray msNode

            let servers =
                arr
                |> Seq.choose (fun n ->
                    match n with
                    | null -> None
                    | n -> decodeMcpServer n |> Result.toOption)
                |> Seq.toList

            return
                { sessionId = sessionId
                  cwd = cwd
                  mcpServers = servers }
        }

    let private encodeLoadSessionParams (p: LoadSessionParams) : JsonObject =
        let o = JsonObject()
        o["sessionId"] <- encodeSessionId p.sessionId
        o["cwd"] <- JsonValue.Create(p.cwd)
        let arr = JsonArray()
        p.mcpServers |> List.iter (fun s -> arr.Add(encodeMcpServer s))
        o["mcpServers"] <- arr
        o

    let private decodeNewSessionResult (node: JsonNode) : Result<NewSessionResult, string> =
        result {
            let! o = asObject node
            let! sidNode = get "sessionId" o
            let! sessionId = decodeSessionId sidNode
            let! modes = decodeModeState (tryGet "modes" o)
            return { sessionId = sessionId; modes = modes }
        }

    let private encodeNewSessionResult (r: NewSessionResult) : JsonObject =
        let o = JsonObject()
        o["sessionId"] <- encodeSessionId r.sessionId

        match encodeModeState r.modes with
        | None -> ()
        | Some ms -> o["modes"] <- ms

        o

    let private decodeLoadSessionResponseModes (nodeOpt: JsonNode option) : Result<SessionModeState option, string> =
        match nodeOpt with
        | None -> Ok None
        | Some node ->
            result {
                let! o = asObject node
                return! decodeModeState (tryGet "modes" o)
            }

    // ---- Prompting: content ----

    let private decodeTextContent (o: JsonObject) : Result<TextContent, string> =
        result {
            let! text = get "text" o |> Result.bind asString
            let! annotations = decodeAnnotations (tryGet "annotations" o)

            return
                { text = text
                  annotations = annotations }
        }

    let private encodeTextContent (t: TextContent) : JsonObject =
        let o = JsonObject()
        o["type"] <- JsonValue.Create("text")
        o["text"] <- JsonValue.Create(t.text)

        match encodeAnnotations t.annotations with
        | None -> ()
        | Some a -> o["annotations"] <- a

        o

    let private decodeImageContent (o: JsonObject) : Result<ImageContent, string> =
        result {
            let! data = get "data" o |> Result.bind asString
            let! mimeType = get "mimeType" o |> Result.bind asString

            let uri =
                match tryGet "uri" o with
                | None -> None
                | Some u -> asString u |> Result.toOption

            let! annotations = decodeAnnotations (tryGet "annotations" o)

            return
                { data = data
                  mimeType = mimeType
                  uri = uri
                  annotations = annotations }
        }

    let private encodeImageContent (i: ImageContent) : JsonObject =
        let o = JsonObject()
        o["type"] <- JsonValue.Create("image")
        o["data"] <- JsonValue.Create(i.data)
        o["mimeType"] <- JsonValue.Create(i.mimeType)

        match i.uri with
        | None -> ()
        | Some u -> o["uri"] <- JsonValue.Create(u)

        match encodeAnnotations i.annotations with
        | None -> ()
        | Some a -> o["annotations"] <- a

        o

    let private decodeAudioContent (o: JsonObject) : Result<AudioContent, string> =
        result {
            let! data = get "data" o |> Result.bind asString
            let! mimeType = get "mimeType" o |> Result.bind asString
            let! annotations = decodeAnnotations (tryGet "annotations" o)

            return
                { data = data
                  mimeType = mimeType
                  annotations = annotations }
        }

    let private encodeAudioContent (a: AudioContent) : JsonObject =
        let o = JsonObject()
        o["type"] <- JsonValue.Create("audio")
        o["data"] <- JsonValue.Create(a.data)
        o["mimeType"] <- JsonValue.Create(a.mimeType)

        match encodeAnnotations a.annotations with
        | None -> ()
        | Some an -> o["annotations"] <- an

        o

    let private decodeResourceLink (o: JsonObject) : Result<ResourceLink, string> =
        result {
            let! name = get "name" o |> Result.bind asString
            let! uri = get "uri" o |> Result.bind asString
            let title = tryGet "title" o |> Option.bind (asString >> Result.toOption)

            let description =
                tryGet "description" o |> Option.bind (asString >> Result.toOption)

            let mimeType = tryGet "mimeType" o |> Option.bind (asString >> Result.toOption)

            let size =
                match tryGet "size" o with
                | None -> None
                | Some s -> asInt64 s |> Result.toOption

            let! annotations = decodeAnnotations (tryGet "annotations" o)

            return
                { name = name
                  uri = uri
                  title = title
                  description = description
                  mimeType = mimeType
                  size = size
                  annotations = annotations }
        }

    let private encodeResourceLink (r: ResourceLink) : JsonObject =
        let o = JsonObject()
        o["type"] <- JsonValue.Create("resource_link")
        o["name"] <- JsonValue.Create(r.name)
        o["uri"] <- JsonValue.Create(r.uri)

        match r.title with
        | None -> ()
        | Some t -> o["title"] <- JsonValue.Create(t)

        match r.description with
        | None -> ()
        | Some d -> o["description"] <- JsonValue.Create(d)

        match r.mimeType with
        | None -> ()
        | Some m -> o["mimeType"] <- JsonValue.Create(m)

        match r.size with
        | None -> ()
        | Some s -> o["size"] <- JsonValue.Create(s)

        match encodeAnnotations r.annotations with
        | None -> ()
        | Some a -> o["annotations"] <- a

        o

    let private decodeEmbeddedResourceResource (node: JsonNode) : Result<EmbeddedResourceResource, string> =
        result {
            let! o = asObject node

            // Distinguish by field name (text vs blob).
            if tryGet "text" o |> Option.isSome then
                let! uri = get "uri" o |> Result.bind asString
                let! text = get "text" o |> Result.bind asString
                let mimeType = tryGet "mimeType" o |> Option.bind (asString >> Result.toOption)
                return EmbeddedResourceResource.Text(uri, text, mimeType)
            else
                let! uri = get "uri" o |> Result.bind asString
                let! blob = get "blob" o |> Result.bind asString
                let mimeType = tryGet "mimeType" o |> Option.bind (asString >> Result.toOption)
                return EmbeddedResourceResource.Blob(uri, blob, mimeType)
        }

    let private encodeEmbeddedResourceResource (r: EmbeddedResourceResource) : JsonObject =
        let o = JsonObject()

        match r with
        | EmbeddedResourceResource.Text(uri, text, mimeType) ->
            o["uri"] <- JsonValue.Create(uri)
            o["text"] <- JsonValue.Create(text)

            match mimeType with
            | None -> ()
            | Some m -> o["mimeType"] <- JsonValue.Create(m)
        | EmbeddedResourceResource.Blob(uri, blob, mimeType) ->
            o["uri"] <- JsonValue.Create(uri)
            o["blob"] <- JsonValue.Create(blob)

            match mimeType with
            | None -> ()
            | Some m -> o["mimeType"] <- JsonValue.Create(m)

        o

    let private decodeEmbeddedResource (o: JsonObject) : Result<EmbeddedResource, string> =
        result {
            let! resNode = get "resource" o
            let! res = decodeEmbeddedResourceResource resNode
            let! annotations = decodeAnnotations (tryGet "annotations" o)

            return
                { resource = res
                  annotations = annotations }
        }

    let private encodeEmbeddedResource (r: EmbeddedResource) : JsonObject =
        let o = JsonObject()
        o["type"] <- JsonValue.Create("resource")
        o["resource"] <- encodeEmbeddedResourceResource r.resource

        match encodeAnnotations r.annotations with
        | None -> ()
        | Some a -> o["annotations"] <- a

        o

    let private decodeContentBlock (node: JsonNode) : Result<ContentBlock, string> =
        result {
            let! o = asObject node
            let! tNode = get "type" o
            let! t = asString tNode

            match t with
            | "text" ->
                let! c = decodeTextContent o
                return ContentBlock.Text c
            | "image" ->
                let! c = decodeImageContent o
                return ContentBlock.Image c
            | "audio" ->
                let! c = decodeAudioContent o
                return ContentBlock.Audio c
            | "resource_link" ->
                let! c = decodeResourceLink o
                return ContentBlock.ResourceLink c
            | "resource" ->
                let! c = decodeEmbeddedResource o
                return ContentBlock.Resource c
            | other -> return! Error(sprintf "unknown ContentBlock.type '%s'" other)
        }

    let private encodeContentBlock (b: ContentBlock) : JsonObject =
        match b with
        | ContentBlock.Text t -> encodeTextContent t
        | ContentBlock.Image i -> encodeImageContent i
        | ContentBlock.Audio a -> encodeAudioContent a
        | ContentBlock.ResourceLink r -> encodeResourceLink r
        | ContentBlock.Resource r -> encodeEmbeddedResource r

    let private decodeContentChunk (node: JsonNode) : Result<ContentChunk, string> =
        result {
            let! o = asObject node
            let! cNode = get "content" o
            let! c = decodeContentBlock cNode
            return { content = c }
        }

    let private encodeContentChunk (c: ContentChunk) : JsonObject =
        let o = JsonObject()
        o["content"] <- encodeContentBlock c.content
        o

    let private decodeStopReason (node: JsonNode) : Result<StopReason, string> =
        result {
            let! s = asString node

            match s with
            | "end_turn" -> return StopReason.EndTurn
            | "max_tokens" -> return StopReason.MaxTokens
            | "max_turn_requests" -> return StopReason.MaxTurnRequests
            | "refusal" -> return StopReason.Refusal
            | "cancelled" -> return StopReason.Cancelled
            | other -> return! Error(sprintf "unknown StopReason '%s'" other)
        }

    let private encodeStopReason (sr: StopReason) : JsonNode | null =
        JsonValue.Create(
            match sr with
            | StopReason.EndTurn -> "end_turn"
            | StopReason.MaxTokens -> "max_tokens"
            | StopReason.MaxTurnRequests -> "max_turn_requests"
            | StopReason.Refusal -> "refusal"
            | StopReason.Cancelled -> "cancelled"
        )

    let private decodeSessionPromptParams (node: JsonNode) : Result<SessionPromptParams, string> =
        result {
            let! o = asObject node
            let! sidNode = get "sessionId" o
            let! sessionId = decodeSessionId sidNode
            let! promptNode = get "prompt" o
            let! arr = asArray promptNode

            let blocks =
                arr
                |> Seq.choose (fun n ->
                    match n with
                    | null -> None
                    | n -> decodeContentBlock n |> Result.toOption)
                |> Seq.toList

            let meta =
                tryGet "_meta" o |> Option.bind (fun n -> n |> asObject |> Result.toOption)

            return
                { sessionId = sessionId
                  prompt = blocks
                  _meta = meta }
        }

    let private encodeSessionPromptParams (p: SessionPromptParams) : JsonObject =
        let o = JsonObject()
        o["sessionId"] <- encodeSessionId p.sessionId
        let arr = JsonArray()
        p.prompt |> List.iter (fun b -> arr.Add(encodeContentBlock b))
        o["prompt"] <- arr

        match p._meta with
        | Some m -> o["_meta"] <- m.DeepClone()
        | None -> ()

        o

    let private decodeSessionCancelParams (node: JsonNode) : Result<SessionCancelParams, string> =
        result {
            let! o = asObject node
            let! sidNode = get "sessionId" o
            let! sessionId = decodeSessionId sidNode
            return { sessionId = sessionId }
        }

    let private encodeSessionCancelParams (p: SessionCancelParams) : JsonObject =
        let o = JsonObject()
        o["sessionId"] <- encodeSessionId p.sessionId
        o

    let private tryGetObjectClone (o: JsonObject) (name: string) : JsonObject option =
        match tryGet name o with
        | Some(:? JsonObject as obj) ->
            match obj.DeepClone() with
            | :? JsonObject as clone -> Some clone
            | _ -> None
        | _ -> None

    let private decodeSessionPromptResponse
        (node: JsonNode)
        : Result<StopReason * JsonObject option * JsonObject option, string> =
        result {
            let! o = asObject node
            let! srNode = get "stopReason" o
            let! stopReason = decodeStopReason srNode
            let usage = tryGetObjectClone o "usage"

            let meta =
                tryGet "_meta" o |> Option.bind (fun n -> n |> asObject |> Result.toOption)

            return stopReason, usage, meta
        }

    // ---- Plan ----

    let private decodePlanEntryPriority (node: JsonNode) : Result<PlanEntryPriority, string> =
        result {
            let! s = asString node

            match s with
            | "high" -> return PlanEntryPriority.High
            | "medium" -> return PlanEntryPriority.Medium
            | "low" -> return PlanEntryPriority.Low
            | other -> return! Error(sprintf "unknown priority '%s'" other)
        }

    let private encodePlanEntryPriority (p: PlanEntryPriority) : JsonNode | null =
        JsonValue.Create(
            match p with
            | PlanEntryPriority.High -> "high"
            | PlanEntryPriority.Medium -> "medium"
            | PlanEntryPriority.Low -> "low"
        )

    let private decodePlanEntryStatus (node: JsonNode) : Result<PlanEntryStatus, string> =
        result {
            let! s = asString node

            match s with
            | "pending" -> return PlanEntryStatus.Pending
            | "in_progress" -> return PlanEntryStatus.InProgress
            | "completed" -> return PlanEntryStatus.Completed
            | other -> return! Error(sprintf "unknown status '%s'" other)
        }

    let private encodePlanEntryStatus (s: PlanEntryStatus) : JsonNode | null =
        JsonValue.Create(
            match s with
            | PlanEntryStatus.Pending -> "pending"
            | PlanEntryStatus.InProgress -> "in_progress"
            | PlanEntryStatus.Completed -> "completed"
        )

    let private decodePlanEntry (node: JsonNode) : Result<PlanEntry, string> =
        result {
            let! o = asObject node
            let! content = get "content" o |> Result.bind asString
            let! priority = get "priority" o |> Result.bind decodePlanEntryPriority
            let! status = get "status" o |> Result.bind decodePlanEntryStatus

            return
                { content = content
                  priority = priority
                  status = status }
        }

    let private encodePlanEntry (e: PlanEntry) : JsonObject =
        let o = JsonObject()
        o["content"] <- JsonValue.Create(e.content)
        o["priority"] <- encodePlanEntryPriority e.priority
        o["status"] <- encodePlanEntryStatus e.status
        o

    let private decodePlan (node: JsonNode) : Result<Plan, string> =
        result {
            let! o = asObject node
            let! entriesNode = get "entries" o
            let! arr = asArray entriesNode

            let entries =
                arr
                |> Seq.choose (fun n ->
                    match n with
                    | null -> None
                    | n -> decodePlanEntry n |> Result.toOption)
                |> Seq.toList

            return { entries = entries }
        }

    let private encodePlan (p: Plan) : JsonObject =
        let o = JsonObject()
        let arr = JsonArray()
        p.entries |> List.iter (fun e -> arr.Add(encodePlanEntry e))
        o["entries"] <- arr
        o

    // ---- Available commands ----

    let private decodeUnstructuredCommandInput (node: JsonNode) : Result<UnstructuredCommandInput, string> =
        result {
            let! o = asObject node
            let! hint = get "hint" o |> Result.bind asString
            return { hint = hint }
        }

    let private encodeUnstructuredCommandInput (i: UnstructuredCommandInput) : JsonObject =
        let o = JsonObject()
        o["hint"] <- JsonValue.Create(i.hint)
        o

    let private decodeAvailableCommandInput (nodeOpt: JsonNode option) : Result<AvailableCommandInput option, string> =
        match nodeOpt with
        | None -> Ok None
        | Some node ->
            decodeUnstructuredCommandInput node
            |> Result.map (fun v -> Some(AvailableCommandInput.Unstructured v))

    let private encodeAvailableCommandInput (iOpt: AvailableCommandInput option) : JsonNode option =
        match iOpt with
        | None -> None
        | Some(AvailableCommandInput.Unstructured u) -> Some(encodeUnstructuredCommandInput u :> JsonNode)

    let private decodeAvailableCommand (node: JsonNode) : Result<AvailableCommand, string> =
        result {
            let! o = asObject node
            let! name = get "name" o |> Result.bind asString
            let! description = get "description" o |> Result.bind asString
            let! input = decodeAvailableCommandInput (tryGet "input" o)

            return
                { name = name
                  description = description
                  input = input }
        }

    let private encodeAvailableCommand (c: AvailableCommand) : JsonObject =
        let o = JsonObject()
        o["name"] <- JsonValue.Create(c.name)
        o["description"] <- JsonValue.Create(c.description)

        match encodeAvailableCommandInput c.input with
        | None -> ()
        | Some v -> o["input"] <- v

        o

    let private decodeAvailableCommandsUpdate (node: JsonNode) : Result<AvailableCommandsUpdate, string> =
        result {
            let! o = asObject node
            let! cmdsNode = get "availableCommands" o
            let! arr = asArray cmdsNode

            let cmds =
                arr
                |> Seq.choose (fun n ->
                    match n with
                    | null -> None
                    | n -> decodeAvailableCommand n |> Result.toOption)
                |> Seq.toList

            return { availableCommands = cmds }
        }

    let private encodeAvailableCommandsUpdate (u: AvailableCommandsUpdate) : JsonObject =
        let o = JsonObject()
        let arr = JsonArray()
        u.availableCommands |> List.iter (fun c -> arr.Add(encodeAvailableCommand c))
        o["availableCommands"] <- arr
        o

    // ---- Tool calls ----

    let private decodeToolKind (nodeOpt: JsonNode option) : Result<ToolKind, string> =
        match nodeOpt with
        | None -> Ok ToolKind.Other
        | Some node ->
            result {
                let! s = asString node

                return
                    match s with
                    | "read" -> ToolKind.Read
                    | "edit" -> ToolKind.Edit
                    | "delete" -> ToolKind.Delete
                    | "move" -> ToolKind.Move
                    | "search" -> ToolKind.Search
                    | "execute" -> ToolKind.Execute
                    | "think" -> ToolKind.Think
                    | "fetch" -> ToolKind.Fetch
                    | "switch_mode" -> ToolKind.SwitchMode
                    | "other" -> ToolKind.Other
                    | _ -> ToolKind.Other
            }

    let private encodeToolKind (k: ToolKind) : JsonNode | null =
        JsonValue.Create(
            match k with
            | ToolKind.Read -> "read"
            | ToolKind.Edit -> "edit"
            | ToolKind.Delete -> "delete"
            | ToolKind.Move -> "move"
            | ToolKind.Search -> "search"
            | ToolKind.Execute -> "execute"
            | ToolKind.Think -> "think"
            | ToolKind.Fetch -> "fetch"
            | ToolKind.SwitchMode -> "switch_mode"
            | ToolKind.Other -> "other"
        )

    let private decodeToolStatus (nodeOpt: JsonNode option) : Result<ToolCallStatus, string> =
        match nodeOpt with
        | None -> Ok ToolCallStatus.Pending
        | Some node ->
            result {
                let! s = asString node

                match s with
                | "pending" -> return ToolCallStatus.Pending
                | "in_progress" -> return ToolCallStatus.InProgress
                | "completed" -> return ToolCallStatus.Completed
                | "failed" -> return ToolCallStatus.Failed
                | other -> return! Error(sprintf "unknown tool status '%s'" other)
            }

    let private encodeToolStatus (s: ToolCallStatus) : JsonNode | null =
        JsonValue.Create(
            match s with
            | ToolCallStatus.Pending -> "pending"
            | ToolCallStatus.InProgress -> "in_progress"
            | ToolCallStatus.Completed -> "completed"
            | ToolCallStatus.Failed -> "failed"
        )

    let private decodeToolCallLocation (node: JsonNode) : Result<ToolCallLocation, string> =
        result {
            let! o = asObject node
            let! path = get "path" o |> Result.bind asString

            let line =
                match tryGet "line" o with
                | None -> None
                | Some l -> asInt l |> Result.toOption

            return { path = path; line = line }
        }

    let private encodeToolCallLocation (l: ToolCallLocation) : JsonObject =
        let o = JsonObject()
        o["path"] <- JsonValue.Create(l.path)

        match l.line with
        | None -> ()
        | Some ln -> o["line"] <- JsonValue.Create(ln)

        o

    let private decodeDiff (o: JsonObject) : Result<Diff, string> =
        result {
            let! path = get "path" o |> Result.bind asString
            let oldText = tryGet "oldText" o |> Option.bind (asString >> Result.toOption)
            let! newText = get "newText" o |> Result.bind asString

            return
                { path = path
                  oldText = oldText
                  newText = newText }
        }

    let private encodeDiff (d: Diff) : JsonObject =
        let o = JsonObject()
        o["type"] <- JsonValue.Create("diff")
        o["path"] <- JsonValue.Create(d.path)

        match d.oldText with
        | None -> ()
        | Some t -> o["oldText"] <- JsonValue.Create(t)

        o["newText"] <- JsonValue.Create(d.newText)
        o

    let private decodeTerminalRef (o: JsonObject) : Result<Terminal, string> =
        result {
            let! terminalId = get "terminalId" o |> Result.bind asString
            return { terminalId = terminalId }
        }

    let private encodeTerminalRef (t: Terminal) : JsonObject =
        let o = JsonObject()
        o["type"] <- JsonValue.Create("terminal")
        o["terminalId"] <- JsonValue.Create(t.terminalId)
        o

    let private decodeToolContent (o: JsonObject) : Result<Content, string> =
        result {
            let! cNode = get "content" o
            let! c = decodeContentBlock cNode
            return { content = c }
        }

    let private encodeToolContent (c: Content) : JsonObject =
        let o = JsonObject()
        o["type"] <- JsonValue.Create("content")
        o["content"] <- encodeContentBlock c.content
        o

    let private decodeToolCallContent (node: JsonNode) : Result<ToolCallContent, string> =
        result {
            let! o = asObject node
            let! t = get "type" o |> Result.bind asString

            match t with
            | "content" ->
                let! c = decodeToolContent o
                return ToolCallContent.Content c
            | "diff" ->
                let! d = decodeDiff o
                return ToolCallContent.Diff d
            | "terminal" ->
                let! t = decodeTerminalRef o
                return ToolCallContent.Terminal t
            | other -> return! Error(sprintf "unknown ToolCallContent.type '%s'" other)
        }

    let private encodeToolCallContent (c: ToolCallContent) : JsonObject =
        match c with
        | ToolCallContent.Content c -> encodeToolContent c
        | ToolCallContent.Diff d -> encodeDiff d
        | ToolCallContent.Terminal t -> encodeTerminalRef t

    let private decodeToolCall (node: JsonNode) : Result<ToolCall, string> =
        result {
            let! o = asObject node
            let! id = get "toolCallId" o |> Result.bind asString
            let! title = get "title" o |> Result.bind asString

            let! kind = decodeToolKind (tryGet "kind" o)
            let! status = decodeToolStatus (tryGet "status" o)

            let content =
                match tryGet "content" o with
                | None -> []
                | Some c ->
                    match c with
                    | :? JsonArray as arr ->
                        arr
                        |> Seq.choose (fun n ->
                            match n with
                            | null -> None
                            | n -> decodeToolCallContent n |> Result.toOption)
                        |> Seq.toList
                    | _ -> []

            let locations =
                match tryGet "locations" o with
                | None -> []
                | Some l ->
                    match l with
                    | :? JsonArray as arr ->
                        arr
                        |> Seq.choose (fun n ->
                            match n with
                            | null -> None
                            | n -> decodeToolCallLocation n |> Result.toOption)
                        |> Seq.toList
                    | _ -> []

            let rawInput = tryGet "rawInput" o |> cloneOpt
            let rawOutput = tryGet "rawOutput" o |> cloneOpt

            return
                { toolCallId = id
                  title = title
                  kind = kind
                  status = status
                  content = content
                  locations = locations
                  rawInput = rawInput
                  rawOutput = rawOutput }
        }

    let private encodeToolCall (c: ToolCall) : JsonObject =
        let o = JsonObject()
        o["toolCallId"] <- JsonValue.Create(c.toolCallId)
        o["title"] <- JsonValue.Create(c.title)
        o["kind"] <- encodeToolKind c.kind
        o["status"] <- encodeToolStatus c.status
        let content = JsonArray()
        c.content |> List.iter (fun v -> content.Add(encodeToolCallContent v))
        o["content"] <- content
        let locs = JsonArray()
        c.locations |> List.iter (fun l -> locs.Add(encodeToolCallLocation l))
        o["locations"] <- locs

        match c.rawInput with
        | None -> ()
        | Some v -> o["rawInput"] <- v.DeepClone()

        match c.rawOutput with
        | None -> ()
        | Some v -> o["rawOutput"] <- v.DeepClone()

        o

    let private decodeToolCallUpdate (node: JsonNode) : Result<ToolCallUpdate, string> =
        result {
            let! o = asObject node
            let! id = get "toolCallId" o |> Result.bind asString
            let title = tryGet "title" o |> Option.bind (asString >> Result.toOption)

            let kind =
                match tryGet "kind" o with
                | None -> None
                | Some k -> decodeToolKind (Some k) |> Result.toOption

            let status =
                match tryGet "status" o with
                | None -> None
                | Some s -> decodeToolStatus (Some s) |> Result.toOption

            let content =
                match tryGet "content" o with
                | None -> None
                | Some c ->
                    match c with
                    | :? JsonArray as arr ->
                        let items =
                            arr
                            |> Seq.choose (fun n ->
                                match n with
                                | null -> None
                                | n -> decodeToolCallContent n |> Result.toOption)
                            |> Seq.toList

                        Some items
                    | _ -> None

            let locations =
                match tryGet "locations" o with
                | None -> None
                | Some l ->
                    match l with
                    | :? JsonArray as arr ->
                        let items =
                            arr
                            |> Seq.choose (fun n ->
                                match n with
                                | null -> None
                                | n -> decodeToolCallLocation n |> Result.toOption)
                            |> Seq.toList

                        Some items
                    | _ -> None

            let rawInput = tryGet "rawInput" o |> cloneOpt
            let rawOutput = tryGet "rawOutput" o |> cloneOpt

            return
                { toolCallId = id
                  title = title
                  kind = kind
                  status = status
                  content = content
                  locations = locations
                  rawInput = rawInput
                  rawOutput = rawOutput }
        }

    let private encodeToolCallUpdate (u: ToolCallUpdate) : JsonObject =
        let o = JsonObject()
        o["toolCallId"] <- JsonValue.Create(u.toolCallId)

        match u.title with
        | None -> ()
        | Some t -> o["title"] <- JsonValue.Create(t)

        match u.kind with
        | None -> ()
        | Some k -> o["kind"] <- encodeToolKind k

        match u.status with
        | None -> ()
        | Some s -> o["status"] <- encodeToolStatus s

        match u.content with
        | None -> ()
        | Some items ->
            let arr = JsonArray()
            items |> List.iter (fun c -> arr.Add(encodeToolCallContent c))
            o["content"] <- arr

        match u.locations with
        | None -> ()
        | Some items ->
            let arr = JsonArray()
            items |> List.iter (fun l -> arr.Add(encodeToolCallLocation l))
            o["locations"] <- arr

        match u.rawInput with
        | None -> ()
        | Some v -> o["rawInput"] <- v.DeepClone()

        match u.rawOutput with
        | None -> ()
        | Some v -> o["rawOutput"] <- v.DeepClone()

        o

    // ---- Permission ----

    let private decodePermissionOptionKind (node: JsonNode) : Result<PermissionOptionKind, string> =
        result {
            let! s = asString node

            match s with
            | "allow_once" -> return PermissionOptionKind.AllowOnce
            | "allow_always" -> return PermissionOptionKind.AllowAlways
            | "reject_once" -> return PermissionOptionKind.RejectOnce
            | "reject_always" -> return PermissionOptionKind.RejectAlways
            | other -> return! Error(sprintf "unknown permission kind '%s'" other)
        }

    let private encodePermissionOptionKind (k: PermissionOptionKind) : JsonNode | null =
        JsonValue.Create(
            match k with
            | PermissionOptionKind.AllowOnce -> "allow_once"
            | PermissionOptionKind.AllowAlways -> "allow_always"
            | PermissionOptionKind.RejectOnce -> "reject_once"
            | PermissionOptionKind.RejectAlways -> "reject_always"
        )

    let private decodePermissionOption (node: JsonNode) : Result<PermissionOption, string> =
        result {
            let! o = asObject node
            let! optionId = get "optionId" o |> Result.bind asString
            let! name = get "name" o |> Result.bind asString
            let! kind = get "kind" o |> Result.bind decodePermissionOptionKind

            return
                { optionId = optionId
                  name = name
                  kind = kind }
        }

    let private encodePermissionOption (p: PermissionOption) : JsonObject =
        let o = JsonObject()
        o["optionId"] <- JsonValue.Create(p.optionId)
        o["name"] <- JsonValue.Create(p.name)
        o["kind"] <- encodePermissionOptionKind p.kind
        o

    let private decodePermissionOutcome (node: JsonNode) : Result<RequestPermissionOutcome, string> =
        result {
            let! o = asObject node
            let! kindNode = get "outcome" o
            let! k = asString kindNode

            match k with
            | "cancelled" -> return RequestPermissionOutcome.Cancelled
            | "selected" ->
                let! optionId = get "optionId" o |> Result.bind asString
                return RequestPermissionOutcome.Selected optionId
            | other -> return! Error(sprintf "unknown permission outcome '%s'" other)
        }

    let private encodePermissionOutcome (o': RequestPermissionOutcome) : JsonObject =
        let o = JsonObject()

        match o' with
        | RequestPermissionOutcome.Cancelled -> o["outcome"] <- JsonValue.Create("cancelled")
        | RequestPermissionOutcome.Selected optionId ->
            o["outcome"] <- JsonValue.Create("selected")
            o["optionId"] <- JsonValue.Create(optionId)

        o

    let private decodeRequestPermissionParams (node: JsonNode) : Result<RequestPermissionParams, string> =
        result {
            let! o = asObject node
            let! sidNode = get "sessionId" o
            let! sessionId = decodeSessionId sidNode
            let! tcNode = get "toolCall" o
            let! toolCall = decodeToolCallUpdate tcNode
            let! optsNode = get "options" o
            let! arr = asArray optsNode

            let options =
                arr
                |> Seq.choose (fun n ->
                    match n with
                    | null -> None
                    | n -> decodePermissionOption n |> Result.toOption)
                |> Seq.toList

            return
                { sessionId = sessionId
                  toolCall = toolCall
                  options = options }
        }

    let private encodeRequestPermissionParams (p: RequestPermissionParams) : JsonObject =
        let o = JsonObject()
        o["sessionId"] <- encodeSessionId p.sessionId
        o["toolCall"] <- encodeToolCallUpdate p.toolCall
        let arr = JsonArray()
        p.options |> List.iter (fun opt -> arr.Add(encodePermissionOption opt))
        o["options"] <- arr
        o

    let private decodeRequestPermissionResult (node: JsonNode) : Result<RequestPermissionResult, string> =
        result {
            let! o = asObject node
            let! ocNode = get "outcome" o
            // outcome is an object (discriminator union)
            let! outcome = decodePermissionOutcome ocNode
            return { outcome = outcome }
        }

    let private encodeRequestPermissionResult (r: RequestPermissionResult) : JsonObject =
        let o = JsonObject()
        o["outcome"] <- encodePermissionOutcome r.outcome
        o

    // ---- Session update ----

    let private decodeSessionUpdate (node: JsonNode) : Result<SessionUpdate, string> =
        result {
            let! o = asObject node
            let! tagNode = get "sessionUpdate" o
            let! tag = asString tagNode

            match tag with
            | "user_message_chunk" ->
                let! c = decodeContentChunk node
                return SessionUpdate.UserMessageChunk c
            | "agent_message_chunk" ->
                let! c = decodeContentChunk node
                return SessionUpdate.AgentMessageChunk c
            | "agent_thought_chunk" ->
                let! c = decodeContentChunk node
                return SessionUpdate.AgentThoughtChunk c
            | "tool_call" ->
                let! tc = decodeToolCall node
                return SessionUpdate.ToolCall tc
            | "tool_call_update" ->
                let! u = decodeToolCallUpdate node
                return SessionUpdate.ToolCallUpdate u
            | "plan" ->
                let! p = decodePlan node
                return SessionUpdate.Plan p
            | "available_commands_update" ->
                let! u = decodeAvailableCommandsUpdate node
                return SessionUpdate.AvailableCommandsUpdate u
            | "current_mode_update" ->
                let! u = decodeCurrentModeUpdate node
                return SessionUpdate.CurrentModeUpdate u
            | other ->
                let payload =
                    match o.DeepClone() with
                    | :? JsonObject as clone -> clone
                    | _ -> JsonObject()

                return SessionUpdate.Ext(other, payload)
        }

    let private encodeSessionUpdate (u: SessionUpdate) : JsonObject =
        match u with
        | SessionUpdate.UserMessageChunk c ->
            let o = encodeContentChunk c
            o["sessionUpdate"] <- JsonValue.Create("user_message_chunk")
            o
        | SessionUpdate.AgentMessageChunk c ->
            let o = encodeContentChunk c
            o["sessionUpdate"] <- JsonValue.Create("agent_message_chunk")
            o
        | SessionUpdate.AgentThoughtChunk c ->
            let o = encodeContentChunk c
            o["sessionUpdate"] <- JsonValue.Create("agent_thought_chunk")
            o
        | SessionUpdate.ToolCall c ->
            let o = encodeToolCall c
            o["sessionUpdate"] <- JsonValue.Create("tool_call")
            o
        | SessionUpdate.ToolCallUpdate u ->
            let o = encodeToolCallUpdate u
            o["sessionUpdate"] <- JsonValue.Create("tool_call_update")
            o
        | SessionUpdate.Plan p ->
            let o = encodePlan p
            o["sessionUpdate"] <- JsonValue.Create("plan")
            o
        | SessionUpdate.AvailableCommandsUpdate u ->
            let o = encodeAvailableCommandsUpdate u
            o["sessionUpdate"] <- JsonValue.Create("available_commands_update")
            o
        | SessionUpdate.CurrentModeUpdate u ->
            let o = encodeCurrentModeUpdate u
            o["sessionUpdate"] <- JsonValue.Create("current_mode_update")
            o
        | SessionUpdate.Ext(tag, payload) ->
            let o =
                match payload.DeepClone() with
                | :? JsonObject as clone -> clone
                | _ -> JsonObject()

            o["sessionUpdate"] <- JsonValue.Create(tag)
            o

    let private decodeSessionUpdateNotification (node: JsonNode) : Result<SessionUpdateNotification, string> =
        result {
            let! o = asObject node
            let! sidNode = get "sessionId" o
            let! updNode = get "update" o
            let! sid = decodeSessionId sidNode
            let! upd = decodeSessionUpdate updNode

            let meta =
                tryGet "_meta" o |> Option.bind (fun n -> n |> asObject |> Result.toOption)

            return
                { sessionId = sid
                  update = upd
                  _meta = meta }
        }

    let private encodeSessionUpdateNotification (n: SessionUpdateNotification) : JsonObject =
        let o = JsonObject()
        o["sessionId"] <- encodeSessionId n.sessionId
        o["update"] <- encodeSessionUpdate n.update

        match n._meta with
        | Some m -> o["_meta"] <- m.DeepClone()
        | None -> ()

        o

    // ---- Tool surface: fs + terminals ----

    let private decodeReadTextFileParams (node: JsonNode) : Result<ReadTextFileParams, string> =
        result {
            let! o = asObject node
            let! sidNode = get "sessionId" o
            let! sessionId = decodeSessionId sidNode
            let! path = get "path" o |> Result.bind asString
            let line = tryGet "line" o |> Option.bind (asInt >> Result.toOption)
            let limit = tryGet "limit" o |> Option.bind (asInt >> Result.toOption)

            return
                { sessionId = sessionId
                  path = path
                  line = line
                  limit = limit }
        }

    let private encodeReadTextFileParams (p: ReadTextFileParams) : JsonObject =
        let o = JsonObject()
        o["sessionId"] <- encodeSessionId p.sessionId
        o["path"] <- JsonValue.Create(p.path)

        match p.line with
        | None -> ()
        | Some v -> o["line"] <- JsonValue.Create(v)

        match p.limit with
        | None -> ()
        | Some v -> o["limit"] <- JsonValue.Create(v)

        o

    let private decodeReadTextFileResult (node: JsonNode) : Result<ReadTextFileResult, string> =
        result {
            let! o = asObject node
            let! content = get "content" o |> Result.bind asString
            return { content = content }
        }

    let private encodeReadTextFileResult (r: ReadTextFileResult) : JsonObject =
        let o = JsonObject()
        o["content"] <- JsonValue.Create(r.content)
        o

    let private decodeWriteTextFileParams (node: JsonNode) : Result<WriteTextFileParams, string> =
        result {
            let! o = asObject node
            let! sidNode = get "sessionId" o
            let! sessionId = decodeSessionId sidNode
            let! path = get "path" o |> Result.bind asString
            let! content = get "content" o |> Result.bind asString

            return
                { sessionId = sessionId
                  path = path
                  content = content }
        }

    let private encodeWriteTextFileParams (p: WriteTextFileParams) : JsonObject =
        let o = JsonObject()
        o["sessionId"] <- encodeSessionId p.sessionId
        o["path"] <- JsonValue.Create(p.path)
        o["content"] <- JsonValue.Create(p.content)
        o

    let private encodeEmptyObject () : JsonObject = JsonObject()

    let private decodeCreateTerminalParams (node: JsonNode) : Result<CreateTerminalParams, string> =
        result {
            let! o = asObject node
            let! sidNode = get "sessionId" o
            let! sessionId = decodeSessionId sidNode
            let! command = get "command" o |> Result.bind asString

            let args =
                match tryGet "args" o with
                | None -> []
                | Some a ->
                    match a with
                    | :? JsonArray as arr ->
                        arr
                        |> Seq.choose (fun n ->
                            match n with
                            | null -> None
                            | n -> asString n |> Result.toOption)
                        |> Seq.toList
                    | _ -> []

            let cwd = tryGet "cwd" o |> Option.bind (asString >> Result.toOption)

            let env =
                match tryGet "env" o with
                | None -> []
                | Some e ->
                    match e with
                    | :? JsonArray as arr ->
                        arr
                        |> Seq.choose (fun n ->
                            match n with
                            | null -> None
                            | n -> decodeEnvVar n |> Result.toOption)
                        |> Seq.toList
                    | _ -> []

            let outputByteLimit =
                match tryGet "outputByteLimit" o with
                | None -> None
                | Some v -> asUInt64 v |> Result.toOption

            return
                { sessionId = sessionId
                  command = command
                  args = args
                  cwd = cwd
                  env = env
                  outputByteLimit = outputByteLimit }
        }

    let private encodeCreateTerminalParams (p: CreateTerminalParams) : JsonObject =
        let o = JsonObject()
        o["sessionId"] <- encodeSessionId p.sessionId
        o["command"] <- JsonValue.Create(p.command)
        let args = JsonArray()
        p.args |> List.iter (fun a -> args.Add(JsonValue.Create(a)))
        o["args"] <- args

        match p.cwd with
        | None -> ()
        | Some c -> o["cwd"] <- JsonValue.Create(c)

        let env = JsonArray()
        p.env |> List.iter (fun e -> env.Add(encodeEnvVar e))
        o["env"] <- env

        match p.outputByteLimit with
        | None -> ()
        | Some v -> o["outputByteLimit"] <- JsonValue.Create(v)

        o

    let private decodeCreateTerminalResult (node: JsonNode) : Result<CreateTerminalResult, string> =
        result {
            let! o = asObject node
            let! terminalId = get "terminalId" o |> Result.bind asString
            return { terminalId = terminalId }
        }

    let private encodeCreateTerminalResult (r: CreateTerminalResult) : JsonObject =
        let o = JsonObject()
        o["terminalId"] <- JsonValue.Create(r.terminalId)
        o

    let private decodeTerminalExitStatus (nodeOpt: JsonNode option) : Result<TerminalExitStatus option, string> =
        match nodeOpt with
        | None -> Ok None
        | Some node ->
            result {
                let! o = asObject node
                let exitCode = tryGet "exitCode" o |> Option.bind (asInt >> Result.toOption)
                let signal = tryGet "signal" o |> Option.bind (asString >> Result.toOption)
                return Some { exitCode = exitCode; signal = signal }
            }

    let private encodeTerminalExitStatus (sOpt: TerminalExitStatus option) : JsonNode option =
        match sOpt with
        | None -> None
        | Some s ->
            let o = JsonObject()

            match s.exitCode with
            | None -> ()
            | Some c -> o["exitCode"] <- JsonValue.Create(c)

            match s.signal with
            | None -> ()
            | Some sig' -> o["signal"] <- JsonValue.Create(sig')

            Some(o :> JsonNode)

    let private decodeTerminalOutputParams (node: JsonNode) : Result<TerminalOutputParams, string> =
        result {
            let! o = asObject node
            let! sidNode = get "sessionId" o
            let! sessionId = decodeSessionId sidNode
            let! terminalId = get "terminalId" o |> Result.bind asString

            return
                { sessionId = sessionId
                  terminalId = terminalId }
        }

    let private encodeTerminalOutputParams (p: TerminalOutputParams) : JsonObject =
        let o = JsonObject()
        o["sessionId"] <- encodeSessionId p.sessionId
        o["terminalId"] <- JsonValue.Create(p.terminalId)
        o

    let private decodeTerminalOutputResult (node: JsonNode) : Result<TerminalOutputResult, string> =
        result {
            let! o = asObject node
            let! output = get "output" o |> Result.bind asString

            let truncated =
                match tryGet "truncated" o with
                | None -> false
                | Some v -> asBool v |> Result.defaultValue false

            let! exitStatus = decodeTerminalExitStatus (tryGet "exitStatus" o)

            return
                { output = output
                  truncated = truncated
                  exitStatus = exitStatus }
        }

    let private encodeTerminalOutputResult (r: TerminalOutputResult) : JsonObject =
        let o = JsonObject()
        o["output"] <- JsonValue.Create(r.output)
        o["truncated"] <- JsonValue.Create(r.truncated)

        match encodeTerminalExitStatus r.exitStatus with
        | None -> ()
        | Some s -> o["exitStatus"] <- s

        o

    let private decodeWaitForExitParams (node: JsonNode) : Result<WaitForTerminalExitParams, string> =
        result {
            let! o = asObject node
            let! sidNode = get "sessionId" o
            let! sessionId = decodeSessionId sidNode
            let! terminalId = get "terminalId" o |> Result.bind asString

            return
                { sessionId = sessionId
                  terminalId = terminalId }
        }

    let private encodeWaitForExitParams (p: WaitForTerminalExitParams) : JsonObject =
        let o = JsonObject()
        o["sessionId"] <- encodeSessionId p.sessionId
        o["terminalId"] <- JsonValue.Create(p.terminalId)
        o

    let private decodeTerminalExitStatusResult (node: JsonNode) : Result<TerminalExitStatus, string> =
        result {
            let! o = asObject node
            let exitCode = tryGet "exitCode" o |> Option.bind (asInt >> Result.toOption)
            let signal = tryGet "signal" o |> Option.bind (asString >> Result.toOption)
            return { exitCode = exitCode; signal = signal }
        }

    let private encodeTerminalExitStatusResult (s: TerminalExitStatus) : JsonObject =
        let o = JsonObject()

        match s.exitCode with
        | None -> ()
        | Some c -> o["exitCode"] <- JsonValue.Create(c)

        match s.signal with
        | None -> ()
        | Some sig' -> o["signal"] <- JsonValue.Create(sig')

        o

    let private decodeKillParams (node: JsonNode) : Result<KillTerminalCommandParams, string> =
        result {
            let! o = asObject node
            let! sidNode = get "sessionId" o
            let! sessionId = decodeSessionId sidNode
            let! terminalId = get "terminalId" o |> Result.bind asString

            return
                { sessionId = sessionId
                  terminalId = terminalId }
        }

    let private encodeKillParams (p: KillTerminalCommandParams) : JsonObject =
        let o = JsonObject()
        o["sessionId"] <- encodeSessionId p.sessionId
        o["terminalId"] <- JsonValue.Create(p.terminalId)
        o

    let private decodeReleaseParams (node: JsonNode) : Result<ReleaseTerminalParams, string> =
        result {
            let! o = asObject node
            let! sidNode = get "sessionId" o
            let! sessionId = decodeSessionId sidNode
            let! terminalId = get "terminalId" o |> Result.bind asString

            return
                { sessionId = sessionId
                  terminalId = terminalId }
        }

    let private encodeReleaseParams (p: ReleaseTerminalParams) : JsonObject =
        let o = JsonObject()
        o["sessionId"] <- encodeSessionId p.sessionId
        o["terminalId"] <- JsonValue.Create(p.terminalId)
        o

    // ---- Proxy chains (draft) ----

    let private decodeProxySuccessorParams (node: JsonNode) : Result<ProxySuccessorParams, string> =
        result {
            let! o = asObject node
            let! methodName = get "method" o |> Result.bind asString
            let parameters = tryGet "params" o |> cloneOpt

            let meta =
                match tryGet "meta" o with
                | None -> None
                | Some m ->
                    match m with
                    | :? JsonObject as o -> Some(o.DeepClone() :?> JsonObject)
                    | _ -> None

            return
                { method = methodName
                  parameters = parameters
                  meta = meta }
        }

    let private encodeProxySuccessorParams (p: ProxySuccessorParams) : JsonObject =
        let o = JsonObject()
        o["method"] <- JsonValue.Create(p.method)

        match p.parameters with
        | None -> ()
        | Some parameters -> o["params"] <- parameters.DeepClone()

        match p.meta with
        | None -> ()
        | Some meta -> o["meta"] <- meta.DeepClone()

        o

    // -----------------
    // Top-level method routing
    // -----------------

    let methodOfPendingClient =
        function
        | PendingClientRequest.Initialize -> "initialize"
        | PendingClientRequest.ProxyInitialize -> "proxy/initialize"
        | PendingClientRequest.Authenticate -> "authenticate"
        | PendingClientRequest.SessionNew -> "session/new"
        | PendingClientRequest.SessionLoad _ -> "session/load"
        | PendingClientRequest.SessionPrompt _ -> "session/prompt"
        | PendingClientRequest.SessionSetMode _ -> "session/set_mode"
        | PendingClientRequest.ProxySuccessor _ -> "proxy/successor"
        | PendingClientRequest.ExtRequest m -> m

    let methodOfPendingAgent =
        function
        | PendingAgentRequest.FsReadTextFile _ -> "fs/read_text_file"
        | PendingAgentRequest.FsWriteTextFile _ -> "fs/write_text_file"
        | PendingAgentRequest.SessionRequestPermission _ -> "session/request_permission"
        | PendingAgentRequest.TerminalCreate _ -> "terminal/create"
        | PendingAgentRequest.TerminalOutput _ -> "terminal/output"
        | PendingAgentRequest.TerminalWaitForExit _ -> "terminal/wait_for_exit"
        | PendingAgentRequest.TerminalKill _ -> "terminal/kill"
        | PendingAgentRequest.TerminalRelease _ -> "terminal/release"
        | PendingAgentRequest.ProxySuccessor _ -> "proxy/successor"
        | PendingAgentRequest.ExtRequest m -> m

    let decodeClientRequest
        (methodName: string)
        (paramsNodeOpt: JsonNode option)
        : Result<ClientToAgentMessage * PendingClientRequest, string> =
        let paramsNodeOpt = paramsNodeOpt |> cloneOpt

        result {
            let! paramsNode =
                match paramsNodeOpt with
                | Some p -> Ok p
                | None -> Error "missing params"

            match methodName with
            | "initialize" ->
                let! p = decodeInitializeParams paramsNode
                return ClientToAgentMessage.Initialize p, PendingClientRequest.Initialize
            | "proxy/initialize" ->
                let! p = decodeInitializeParams paramsNode
                return ClientToAgentMessage.ProxyInitialize p, PendingClientRequest.ProxyInitialize
            | "authenticate" ->
                let! p = decodeAuthenticateParams paramsNode
                return ClientToAgentMessage.Authenticate p, PendingClientRequest.Authenticate
            | "session/new" ->
                let! p = decodeNewSessionParams paramsNode
                return ClientToAgentMessage.SessionNew p, PendingClientRequest.SessionNew
            | "session/load" ->
                let! p = decodeLoadSessionParams paramsNode
                return ClientToAgentMessage.SessionLoad p, PendingClientRequest.SessionLoad p
            | "session/prompt" ->
                let! p = decodeSessionPromptParams paramsNode
                return ClientToAgentMessage.SessionPrompt p, PendingClientRequest.SessionPrompt p
            | "session/set_mode" ->
                let! p = decodeSetSessionModeParams paramsNode
                return ClientToAgentMessage.SessionSetMode p, PendingClientRequest.SessionSetMode p
            | "proxy/successor" ->
                let! p = decodeProxySuccessorParams paramsNode
                return ClientToAgentMessage.ProxySuccessorRequest p, PendingClientRequest.ProxySuccessor p.method
            | "session/cancel"
            | "session/update"
            | "session/request_permission"
            | "fs/read_text_file"
            | "fs/write_text_file"
            | "terminal/create"
            | "terminal/output"
            | "terminal/wait_for_exit"
            | "terminal/kill"
            | "terminal/release" -> return! Error "method is not a client->agent request"
            | other ->
                // Extension request (opaque params allowed).
                return ClientToAgentMessage.ExtRequest(other, paramsNodeOpt), PendingClientRequest.ExtRequest other
        }

    let decodeClientNotification
        (methodName: string)
        (paramsNodeOpt: JsonNode option)
        : Result<ClientToAgentMessage, string> =
        let paramsNodeOpt = paramsNodeOpt |> cloneOpt

        match methodName with
        | "session/cancel" ->
            match paramsNodeOpt with
            | None -> Error "missing params"
            | Some p ->
                decodeSessionCancelParams p
                |> Result.map (fun v -> ClientToAgentMessage.SessionCancel v)
        | "proxy/successor" ->
            match paramsNodeOpt with
            | None -> Error "missing params"
            | Some p ->
                decodeProxySuccessorParams p
                |> Result.map (fun v -> ClientToAgentMessage.ProxySuccessorNotification v)
        | other -> Ok(ClientToAgentMessage.ExtNotification(other, paramsNodeOpt))

    let decodeAgentRequest
        (methodName: string)
        (paramsNodeOpt: JsonNode option)
        : Result<AgentToClientMessage * PendingAgentRequest, string> =
        let paramsNodeOpt = paramsNodeOpt |> cloneOpt

        result {
            let! paramsNode =
                match paramsNodeOpt with
                | Some p -> Ok p
                | None -> Error "missing params"

            match methodName with
            | "proxy/successor" ->
                let! p = decodeProxySuccessorParams paramsNode
                return AgentToClientMessage.ProxySuccessorRequest p, PendingAgentRequest.ProxySuccessor p.method
            | "fs/read_text_file" ->
                let! p = decodeReadTextFileParams paramsNode
                return AgentToClientMessage.FsReadTextFileRequest p, PendingAgentRequest.FsReadTextFile p
            | "fs/write_text_file" ->
                let! p = decodeWriteTextFileParams paramsNode
                return AgentToClientMessage.FsWriteTextFileRequest p, PendingAgentRequest.FsWriteTextFile p
            | "session/request_permission" ->
                let! p = decodeRequestPermissionParams paramsNode

                return
                    AgentToClientMessage.SessionRequestPermissionRequest p,
                    PendingAgentRequest.SessionRequestPermission p
            | "terminal/create" ->
                let! p = decodeCreateTerminalParams paramsNode
                return AgentToClientMessage.TerminalCreateRequest p, PendingAgentRequest.TerminalCreate p
            | "terminal/output" ->
                let! p = decodeTerminalOutputParams paramsNode
                return AgentToClientMessage.TerminalOutputRequest p, PendingAgentRequest.TerminalOutput p
            | "terminal/wait_for_exit" ->
                let! p = decodeWaitForExitParams paramsNode
                return AgentToClientMessage.TerminalWaitForExitRequest p, PendingAgentRequest.TerminalWaitForExit p
            | "terminal/kill" ->
                let! p = decodeKillParams paramsNode
                return AgentToClientMessage.TerminalKillRequest p, PendingAgentRequest.TerminalKill p
            | "terminal/release" ->
                let! p = decodeReleaseParams paramsNode
                return AgentToClientMessage.TerminalReleaseRequest p, PendingAgentRequest.TerminalRelease p
            | "initialize"
            | "authenticate"
            | "session/new"
            | "session/load"
            | "session/prompt"
            | "session/set_mode"
            | "session/cancel"
            | "session/update" -> return! Error "method is not an agent->client request"
            | other ->
                return AgentToClientMessage.ExtRequest(other, paramsNodeOpt), PendingAgentRequest.ExtRequest other
        }

    let decodeAgentNotification
        (methodName: string)
        (paramsNodeOpt: JsonNode option)
        : Result<AgentToClientMessage, string> =
        let paramsNodeOpt = paramsNodeOpt |> cloneOpt

        match methodName with
        | "proxy/successor" ->
            match paramsNodeOpt with
            | None -> Error "missing params"
            | Some p ->
                decodeProxySuccessorParams p
                |> Result.map (fun v -> AgentToClientMessage.ProxySuccessorNotification v)
        | "session/update" ->
            match paramsNodeOpt with
            | None -> Error "missing params"
            | Some p ->
                decodeSessionUpdateNotification p
                |> Result.map (fun v -> AgentToClientMessage.SessionUpdate v)
        | other -> Ok(AgentToClientMessage.ExtNotification(other, paramsNodeOpt))

    // ---- Responses ----

    let decodeAgentResult
        (pending: PendingClientRequest)
        (resultNodeOpt: JsonNode option)
        : Result<AgentToClientMessage, string> =
        let resultNodeOpt = resultNodeOpt |> cloneOpt

        match pending with
        | PendingClientRequest.Initialize ->
            match resultNodeOpt with
            | None -> Error "missing result"
            | Some r -> decodeInitializeResult r |> Result.map AgentToClientMessage.InitializeResult
        | PendingClientRequest.ProxyInitialize ->
            match resultNodeOpt with
            | None -> Error "missing result"
            | Some r ->
                decodeInitializeResult r
                |> Result.map AgentToClientMessage.ProxyInitializeResult

        | PendingClientRequest.Authenticate ->
            Ok(AgentToClientMessage.AuthenticateResult(decodeAuthenticateResult resultNodeOpt))

        | PendingClientRequest.SessionNew ->
            match resultNodeOpt with
            | None -> Error "missing result"
            | Some r -> decodeNewSessionResult r |> Result.map AgentToClientMessage.SessionNewResult

        | PendingClientRequest.SessionLoad req ->
            match resultNodeOpt with
            | None ->
                Ok(
                    AgentToClientMessage.SessionLoadResult
                        { sessionId = req.sessionId
                          modes = None }
                )
            | Some r ->
                result {
                    let! modes = decodeLoadSessionResponseModes (Some r)

                    return
                        AgentToClientMessage.SessionLoadResult
                            { sessionId = req.sessionId
                              modes = modes }
                }

        | PendingClientRequest.SessionPrompt req ->
            match resultNodeOpt with
            | None -> Error "missing result"
            | Some r ->
                decodeSessionPromptResponse r
                |> Result.map (fun (sr, usage, meta) ->
                    AgentToClientMessage.SessionPromptResult
                        { sessionId = req.sessionId
                          stopReason = sr
                          usage = usage
                          _meta = meta })

        | PendingClientRequest.SessionSetMode req ->
            Ok(
                AgentToClientMessage.SessionSetModeResult
                    { sessionId = req.sessionId
                      modeId = req.modeId }
            )

        | PendingClientRequest.ProxySuccessor methodName ->
            Ok(AgentToClientMessage.ProxySuccessorResponse(methodName, resultNodeOpt))

        | PendingClientRequest.ExtRequest methodName -> Ok(AgentToClientMessage.ExtResponse(methodName, resultNodeOpt))

    let decodeAgentError (pending: PendingClientRequest) (err: Error) : AgentToClientMessage =
        match pending with
        | PendingClientRequest.Initialize -> AgentToClientMessage.InitializeError err
        | PendingClientRequest.ProxyInitialize -> AgentToClientMessage.ProxyInitializeError err
        | PendingClientRequest.Authenticate -> AgentToClientMessage.AuthenticateError err
        | PendingClientRequest.SessionNew -> AgentToClientMessage.SessionNewError err
        | PendingClientRequest.SessionLoad req -> AgentToClientMessage.SessionLoadError(req, err)
        | PendingClientRequest.SessionPrompt req -> AgentToClientMessage.SessionPromptError(req, err)
        | PendingClientRequest.SessionSetMode req -> AgentToClientMessage.SessionSetModeError(req, err)
        | PendingClientRequest.ProxySuccessor methodName -> AgentToClientMessage.ProxySuccessorError(methodName, err)
        | PendingClientRequest.ExtRequest methodName -> AgentToClientMessage.ExtError(methodName, err)

    let decodeClientResult
        (pending: PendingAgentRequest)
        (resultNodeOpt: JsonNode option)
        : Result<ClientToAgentMessage, string> =
        let resultNodeOpt = resultNodeOpt |> cloneOpt

        match pending with
        | PendingAgentRequest.ProxySuccessor methodName ->
            Ok(ClientToAgentMessage.ProxySuccessorResponse(methodName, resultNodeOpt))
        | PendingAgentRequest.FsReadTextFile _ ->
            match resultNodeOpt with
            | None -> Error "missing result"
            | Some r ->
                decodeReadTextFileResult r
                |> Result.map ClientToAgentMessage.FsReadTextFileResult

        | PendingAgentRequest.FsWriteTextFile _ ->
            Ok(ClientToAgentMessage.FsWriteTextFileResult WriteTextFileResult.empty)

        | PendingAgentRequest.SessionRequestPermission _ ->
            match resultNodeOpt with
            | None -> Error "missing result"
            | Some r ->
                decodeRequestPermissionResult r
                |> Result.map ClientToAgentMessage.SessionRequestPermissionResult

        | PendingAgentRequest.TerminalCreate _ ->
            match resultNodeOpt with
            | None -> Error "missing result"
            | Some r ->
                decodeCreateTerminalResult r
                |> Result.map ClientToAgentMessage.TerminalCreateResult

        | PendingAgentRequest.TerminalOutput _ ->
            match resultNodeOpt with
            | None -> Error "missing result"
            | Some r ->
                decodeTerminalOutputResult r
                |> Result.map ClientToAgentMessage.TerminalOutputResult

        | PendingAgentRequest.TerminalWaitForExit _ ->
            match resultNodeOpt with
            | None -> Error "missing result"
            | Some r ->
                decodeTerminalExitStatusResult r
                |> Result.map ClientToAgentMessage.TerminalWaitForExitResult

        | PendingAgentRequest.TerminalKill _ ->
            Ok(ClientToAgentMessage.TerminalKillResult KillTerminalCommandResult.empty)

        | PendingAgentRequest.TerminalRelease _ ->
            Ok(ClientToAgentMessage.TerminalReleaseResult ReleaseTerminalResult.empty)

        | PendingAgentRequest.ExtRequest methodName -> Ok(ClientToAgentMessage.ExtResponse(methodName, resultNodeOpt))

    let decodeClientError (pending: PendingAgentRequest) (err: Error) : ClientToAgentMessage =
        match pending with
        | PendingAgentRequest.ProxySuccessor methodName -> ClientToAgentMessage.ProxySuccessorError(methodName, err)
        | PendingAgentRequest.FsReadTextFile req -> ClientToAgentMessage.FsReadTextFileError(req, err)
        | PendingAgentRequest.FsWriteTextFile req -> ClientToAgentMessage.FsWriteTextFileError(req, err)
        | PendingAgentRequest.SessionRequestPermission req ->
            ClientToAgentMessage.SessionRequestPermissionError(req, err)
        | PendingAgentRequest.TerminalCreate req -> ClientToAgentMessage.TerminalCreateError(req, err)
        | PendingAgentRequest.TerminalOutput req -> ClientToAgentMessage.TerminalOutputError(req, err)
        | PendingAgentRequest.TerminalWaitForExit req -> ClientToAgentMessage.TerminalWaitForExitError(req, err)
        | PendingAgentRequest.TerminalKill req -> ClientToAgentMessage.TerminalKillError(req, err)
        | PendingAgentRequest.TerminalRelease req -> ClientToAgentMessage.TerminalReleaseError(req, err)
        | PendingAgentRequest.ExtRequest methodName -> ClientToAgentMessage.ExtError(methodName, err)

    // ---- Encoding: ACP values ----

    let encodeClientMessage (idOpt: RequestId option) (msg: ClientToAgentMessage) : Result<JsonObject, EncodeError> =
        let o = JsonObject()
        o["jsonrpc"] <- JsonValue.Create("2.0")

        match msg with
        // Requests
        | ClientToAgentMessage.Initialize p ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["method"] <- JsonValue.Create("initialize")
                o["params"] <- encodeInitializeParams p
                Ok o
        | ClientToAgentMessage.ProxyInitialize p ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["method"] <- JsonValue.Create("proxy/initialize")
                o["params"] <- encodeInitializeParams p
                Ok o
        | ClientToAgentMessage.Authenticate p ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["method"] <- JsonValue.Create("authenticate")
                o["params"] <- encodeAuthenticateParams p
                Ok o
        | ClientToAgentMessage.SessionNew p ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["method"] <- JsonValue.Create("session/new")
                o["params"] <- encodeNewSessionParams p
                Ok o
        | ClientToAgentMessage.SessionLoad p ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["method"] <- JsonValue.Create("session/load")
                o["params"] <- encodeLoadSessionParams p
                Ok o
        | ClientToAgentMessage.SessionPrompt p ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["method"] <- JsonValue.Create("session/prompt")
                o["params"] <- encodeSessionPromptParams p
                Ok o
        | ClientToAgentMessage.SessionSetMode p ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["method"] <- JsonValue.Create("session/set_mode")
                o["params"] <- encodeSetSessionModeParams p
                Ok o
        | ClientToAgentMessage.ProxySuccessorRequest p ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["method"] <- JsonValue.Create("proxy/successor")
                o["params"] <- encodeProxySuccessorParams p
                Ok o
        | ClientToAgentMessage.ExtRequest(methodName, parameters) ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["method"] <- JsonValue.Create(methodName)

                match parameters with
                | None -> ()
                | Some p -> o["params"] <- p.DeepClone()

                Ok o

        // Notifications
        | ClientToAgentMessage.SessionCancel p ->
            match idOpt with
            | Some _ -> Error EncodeError.UnexpectedRequestId
            | None ->
                o["method"] <- JsonValue.Create("session/cancel")
                o["params"] <- encodeSessionCancelParams p
                Ok o
        | ClientToAgentMessage.ProxySuccessorNotification p ->
            match idOpt with
            | Some _ -> Error EncodeError.UnexpectedRequestId
            | None ->
                o["method"] <- JsonValue.Create("proxy/successor")
                o["params"] <- encodeProxySuccessorParams p
                Ok o
        | ClientToAgentMessage.ExtNotification(methodName, parameters) ->
            match idOpt with
            | Some _ -> Error EncodeError.UnexpectedRequestId
            | None ->
                o["method"] <- JsonValue.Create(methodName)

                match parameters with
                | None -> ()
                | Some p -> o["params"] <- p.DeepClone()

                Ok o

        // Responses (success)
        | ClientToAgentMessage.FsReadTextFileResult r ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["result"] <- encodeReadTextFileResult r
                Ok o

        | ClientToAgentMessage.FsWriteTextFileResult _ ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["result"] <- encodeEmptyObject ()
                Ok o

        | ClientToAgentMessage.SessionRequestPermissionResult r ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["result"] <- encodeRequestPermissionResult r
                Ok o

        | ClientToAgentMessage.TerminalCreateResult r ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["result"] <- encodeCreateTerminalResult r
                Ok o

        | ClientToAgentMessage.TerminalOutputResult r ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["result"] <- encodeTerminalOutputResult r
                Ok o

        | ClientToAgentMessage.TerminalWaitForExitResult r ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["result"] <- encodeTerminalExitStatusResult r
                Ok o

        | ClientToAgentMessage.TerminalKillResult _ ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["result"] <- encodeEmptyObject ()
                Ok o

        | ClientToAgentMessage.TerminalReleaseResult _ ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["result"] <- encodeEmptyObject ()
                Ok o

        | ClientToAgentMessage.ExtResponse(_, resultNodeOpt) ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id

                match resultNodeOpt with
                | None -> o["result"] <- null
                | Some r -> o["result"] <- r.DeepClone()

                Ok o
        | ClientToAgentMessage.ProxySuccessorResponse(_, resultNodeOpt) ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id

                match resultNodeOpt with
                | None -> o["result"] <- null
                | Some r -> o["result"] <- r.DeepClone()

                Ok o

        // Responses (error)
        | ClientToAgentMessage.FsReadTextFileError(_, err)
        | ClientToAgentMessage.FsWriteTextFileError(_, err)
        | ClientToAgentMessage.SessionRequestPermissionError(_, err)
        | ClientToAgentMessage.TerminalCreateError(_, err)
        | ClientToAgentMessage.TerminalOutputError(_, err)
        | ClientToAgentMessage.TerminalWaitForExitError(_, err)
        | ClientToAgentMessage.TerminalKillError(_, err)
        | ClientToAgentMessage.TerminalReleaseError(_, err)
        | ClientToAgentMessage.ExtError(_, err)
        | ClientToAgentMessage.ProxySuccessorError(_, err) ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["error"] <- encodeError err
                Ok o

    let encodeAgentMessage (idOpt: RequestId option) (msg: AgentToClientMessage) : Result<JsonObject, EncodeError> =
        let o = JsonObject()
        o["jsonrpc"] <- JsonValue.Create("2.0")

        match msg with
        // Responses (success)
        | AgentToClientMessage.InitializeResult r ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["result"] <- encodeInitializeResult r
                Ok o
        | AgentToClientMessage.ProxyInitializeResult r ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["result"] <- encodeInitializeResult r
                Ok o

        | AgentToClientMessage.AuthenticateResult r ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["result"] <- encodeAuthenticateResult r
                Ok o

        | AgentToClientMessage.SessionNewResult r ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["result"] <- encodeNewSessionResult r
                Ok o

        | AgentToClientMessage.SessionLoadResult r ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                let res = JsonObject()

                match encodeModeState r.modes with
                | None -> ()
                | Some ms -> res["modes"] <- ms

                o["result"] <- res
                Ok o

        | AgentToClientMessage.SessionPromptResult r ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                let res = JsonObject()
                res["stopReason"] <- encodeStopReason r.stopReason

                match r.usage with
                | None -> ()
                | Some usage ->
                    let payload =
                        match usage.DeepClone() with
                        | :? JsonObject as clone -> clone
                        | _ -> JsonObject()

                    res["usage"] <- payload

                match r._meta with
                | Some m -> res["_meta"] <- m.DeepClone()
                | None -> ()

                o["result"] <- res
                Ok o

        | AgentToClientMessage.SessionSetModeResult _ ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["result"] <- encodeEmptyObject ()
                Ok o

        | AgentToClientMessage.ExtResponse(_, resultNodeOpt) ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id

                match resultNodeOpt with
                | None -> o["result"] <- null
                | Some r -> o["result"] <- r.DeepClone()

                Ok o
        | AgentToClientMessage.ProxySuccessorResponse(_, resultNodeOpt) ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id

                match resultNodeOpt with
                | None -> o["result"] <- null
                | Some r -> o["result"] <- r.DeepClone()

                Ok o

        // Responses (error)
        | AgentToClientMessage.InitializeError err
        | AgentToClientMessage.ProxyInitializeError err
        | AgentToClientMessage.AuthenticateError err
        | AgentToClientMessage.SessionNewError err ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["error"] <- encodeError err
                Ok o

        | AgentToClientMessage.SessionLoadError(_, err)
        | AgentToClientMessage.SessionPromptError(_, err)
        | AgentToClientMessage.SessionSetModeError(_, err)
        | AgentToClientMessage.ExtError(_, err)
        | AgentToClientMessage.ProxySuccessorError(_, err) ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["error"] <- encodeError err
                Ok o

        // Notifications
        | AgentToClientMessage.SessionUpdate n ->
            match idOpt with
            | Some _ -> Error EncodeError.UnexpectedRequestId
            | None ->
                o["method"] <- JsonValue.Create("session/update")
                o["params"] <- encodeSessionUpdateNotification n
                Ok o
        | AgentToClientMessage.ProxySuccessorNotification p ->
            match idOpt with
            | Some _ -> Error EncodeError.UnexpectedRequestId
            | None ->
                o["method"] <- JsonValue.Create("proxy/successor")
                o["params"] <- encodeProxySuccessorParams p
                Ok o
        | AgentToClientMessage.ExtNotification(methodName, parameters) ->
            match idOpt with
            | Some _ -> Error EncodeError.UnexpectedRequestId
            | None ->
                o["method"] <- JsonValue.Create(methodName)

                match parameters with
                | None -> ()
                | Some p -> o["params"] <- p.DeepClone()

                Ok o

        // Requests
        | AgentToClientMessage.FsReadTextFileRequest p ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["method"] <- JsonValue.Create("fs/read_text_file")
                o["params"] <- encodeReadTextFileParams p
                Ok o
        | AgentToClientMessage.FsWriteTextFileRequest p ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["method"] <- JsonValue.Create("fs/write_text_file")
                o["params"] <- encodeWriteTextFileParams p
                Ok o
        | AgentToClientMessage.SessionRequestPermissionRequest p ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["method"] <- JsonValue.Create("session/request_permission")
                o["params"] <- encodeRequestPermissionParams p
                Ok o
        | AgentToClientMessage.TerminalCreateRequest p ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["method"] <- JsonValue.Create("terminal/create")
                o["params"] <- encodeCreateTerminalParams p
                Ok o
        | AgentToClientMessage.TerminalOutputRequest p ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["method"] <- JsonValue.Create("terminal/output")
                o["params"] <- encodeTerminalOutputParams p
                Ok o
        | AgentToClientMessage.TerminalWaitForExitRequest p ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["method"] <- JsonValue.Create("terminal/wait_for_exit")
                o["params"] <- encodeWaitForExitParams p
                Ok o
        | AgentToClientMessage.TerminalKillRequest p ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["method"] <- JsonValue.Create("terminal/kill")
                o["params"] <- encodeKillParams p
                Ok o
        | AgentToClientMessage.TerminalReleaseRequest p ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["method"] <- JsonValue.Create("terminal/release")
                o["params"] <- encodeReleaseParams p
                Ok o
        | AgentToClientMessage.ProxySuccessorRequest p ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["method"] <- JsonValue.Create("proxy/successor")
                o["params"] <- encodeProxySuccessorParams p
                Ok o
        | AgentToClientMessage.ExtRequest(methodName, parameters) ->
            match idOpt with
            | None -> Error EncodeError.MissingRequestId
            | Some id ->
                o["id"] <- encodeRequestId id
                o["method"] <- JsonValue.Create(methodName)

                match parameters with
                | None -> ()
                | Some p -> o["params"] <- p.DeepClone()

                Ok o
