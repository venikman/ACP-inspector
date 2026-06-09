namespace Acp.Tests

open System.Text.Json.Nodes
open Xunit

open Acp
open Acp.Domain
open Acp.Domain.JsonRpc
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Messaging
open Acp.Domain.Prompting
open Acp.Domain.SessionSetup

module CodecTests =

    [<Fact>]
    let ``decode initialize request and response correlates by id`` () =
        let state0 = Codec.CodecState.empty

        let initReq =
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":1}}"""

        let state1, msg1 =
            match Codec.decode Codec.Direction.FromClient state0 initReq with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg1 with
        | Message.FromClient(ClientToAgentMessage.Initialize p) ->
            Assert.Equal(ProtocolVersion.current, p.protocolVersion)
        | other -> failwithf "unexpected message %A" other

        Assert.True(state1.pendingClientRequests |> Map.containsKey (RequestId.Number 1L))

        let initRes = """{"jsonrpc":"2.0","id":1,"result":{"protocolVersion":1}}"""

        let state2, msg2 =
            match Codec.decode Codec.Direction.FromAgent state1 initRes with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg2 with
        | Message.FromAgent(AgentToClientMessage.InitializeResult r) ->
            Assert.Equal(ProtocolVersion.current, r.protocolVersion)
        | other -> failwithf "unexpected message %A" other

        Assert.True(state2.pendingClientRequests.IsEmpty)

    [<Fact>]
    let ``decode proxy initialize request and response correlates by id`` () =
        let state0 = Codec.CodecState.empty

        let initReq =
            """{"jsonrpc":"2.0","id":10,"method":"proxy/initialize","params":{"protocolVersion":1,"clientCapabilities":{"fs":{"readTextFile":true,"writeTextFile":true},"terminal":false}}}"""

        let state1, msg1 =
            match Codec.decode Codec.Direction.FromClient state0 initReq with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg1 with
        | Message.FromClient(ClientToAgentMessage.ProxyInitialize p) ->
            Assert.Equal(ProtocolVersion.current, p.protocolVersion)
        | other -> failwithf "unexpected message %A" other

        Assert.True(state1.pendingClientRequests |> Map.containsKey (RequestId.Number 10L))

        let initRes =
            """{"jsonrpc":"2.0","id":10,"result":{"protocolVersion":1,"agentCapabilities":{"loadSession":true,"mcpCapabilities":{"http":false,"sse":false},"promptCapabilities":{"audio":false,"image":false,"embeddedContext":false}},"authMethods":[]}}"""

        let state2, msg2 =
            match Codec.decode Codec.Direction.FromAgent state1 initRes with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg2 with
        | Message.FromAgent(AgentToClientMessage.ProxyInitializeResult r) ->
            Assert.Equal(ProtocolVersion.current, r.protocolVersion)
        | other -> failwithf "unexpected message %A" other

        Assert.True(state2.pendingClientRequests.IsEmpty)

    [<Fact>]
    let ``decode proxy successor request and response preserves inner method`` () =
        let state0 = Codec.CodecState.empty

        let proxyReq =
            """{"jsonrpc":"2.0","id":"proxy-1","method":"proxy/successor","params":{"method":"session/prompt","params":{"sessionId":"s-proxy","prompt":[{"type":"text","text":"hi"}]},"meta":{"traceparent":"00-abc-123-01"}}}"""

        let state1, msg1 =
            match Codec.decode Codec.Direction.FromClient state0 proxyReq with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg1 with
        | Message.FromClient(ClientToAgentMessage.ProxySuccessorRequest p) ->
            Assert.Equal("session/prompt", p.method)
            Assert.True(p.parameters.IsSome)
            Assert.True(p.meta.IsSome)

            match p.parameters with
            | Some(:? JsonObject as paramsObj) ->
                let sessionId = (paramsObj["sessionId"] :?> JsonValue).GetValue<string>()
                Assert.Equal("s-proxy", sessionId)
            | _ -> failwith "expected params object"

            match p.meta with
            | Some meta ->
                let traceparent = (meta["traceparent"] :?> JsonValue).GetValue<string>()
                Assert.Equal("00-abc-123-01", traceparent)
            | None -> failwith "expected meta object"
        | other -> failwithf "unexpected message %A" other

        let proxyRes =
            """{"jsonrpc":"2.0","id":"proxy-1","result":{"stopReason":"end_turn"}}"""

        let state2, msg2 =
            match Codec.decode Codec.Direction.FromAgent state1 proxyRes with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg2 with
        | Message.FromAgent(AgentToClientMessage.ProxySuccessorResponse(methodName, resultOpt)) ->
            Assert.Equal("session/prompt", methodName)
            Assert.True(resultOpt.IsSome)
        | other -> failwithf "unexpected message %A" other

        Assert.True(state2.pendingClientRequests.IsEmpty)

    [<Fact>]
    let ``decode session new with acp transport mcp server`` () =
        let state0 = Codec.CodecState.empty

        let sessionReq =
            """{"jsonrpc":"2.0","id":3,"method":"session/new","params":{"cwd":"/tmp","mcpServers":[{"transport":"acp","uuid":"srv-1"}]}}"""

        let _, msg =
            match Codec.decode Codec.Direction.FromClient state0 sessionReq with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg with
        | Message.FromClient(ClientToAgentMessage.SessionNew p) ->
            match p.mcpServers with
            | [ McpServer.Acp server ] ->
                Assert.Equal("srv-1", server.uuid)
                Assert.Equal("srv-1", server.name)
            | other -> failwithf "unexpected mcp server list %A" other
        | other -> failwithf "unexpected message %A" other

    [<Fact>]
    let ``decode session prompt response reattaches sessionId`` () =
        let state0 = Codec.CodecState.empty

        let promptReq =
            """{"jsonrpc":"2.0","id":"p1","method":"session/prompt","params":{"sessionId":"s-1","prompt":[{"type":"text","text":"hi"}]}}"""

        let state1, _ =
            match Codec.decode Codec.Direction.FromClient state0 promptReq with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        let promptRes = """{"jsonrpc":"2.0","id":"p1","result":{"stopReason":"end_turn"}}"""

        let state2, msg2 =
            match Codec.decode Codec.Direction.FromAgent state1 promptRes with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg2 with
        | Message.FromAgent(AgentToClientMessage.SessionPromptResult r) ->
            Assert.Equal("s-1", SessionId.value r.sessionId)
            Assert.Equal(StopReason.EndTurn, r.stopReason)
        | other -> failwithf "unexpected message %A" other

        Assert.True(state2.pendingClientRequests.IsEmpty)

    [<Fact>]
    let ``decode session prompt error response preserves request context`` () =
        let state0 = Codec.CodecState.empty

        let promptReq =
            """{"jsonrpc":"2.0","id":2,"method":"session/prompt","params":{"sessionId":"s-err","prompt":[{"type":"text","text":"hi"}]}}"""

        let state1, _ =
            match Codec.decode Codec.Direction.FromClient state0 promptReq with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        let promptErr =
            """{"jsonrpc":"2.0","id":2,"error":{"code":-32602,"message":"Invalid params"}}"""

        let state2, msg2 =
            match Codec.decode Codec.Direction.FromAgent state1 promptErr with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg2 with
        | Message.FromAgent(AgentToClientMessage.SessionPromptError(req, err)) ->
            Assert.Equal("s-err", SessionId.value req.sessionId)
            Assert.Equal(-32602, err.code)
        | other -> failwithf "unexpected message %A" other

        Assert.True(state2.pendingClientRequests.IsEmpty)

    [<Fact>]
    let ``decode session list request and response correlates by id`` () =
        let state0 = Codec.CodecState.empty

        let listReq =
            """{"jsonrpc":"2.0","id":21,"method":"session/list","params":{"cwd":"/tmp/project","cursor":"cursor-1"}}"""

        let state1, msg1 =
            match Codec.decode Codec.Direction.FromClient state0 listReq with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg1 with
        | Message.FromClient(ClientToAgentMessage.SessionList p) ->
            Assert.Equal(Some "/tmp/project", p.cwd)
            Assert.Equal(Some "cursor-1", p.cursor)
        | other -> failwithf "unexpected message %A" other

        let listRes =
            """{"jsonrpc":"2.0","id":21,"result":{"sessions":[{"sessionId":"sess-1","cwd":"/tmp/project","title":"Investigate parity","updatedAt":"2026-03-19T10:00:00Z","_meta":{"messageCount":3}}],"nextCursor":"cursor-2"}}"""

        let state2, msg2 =
            match Codec.decode Codec.Direction.FromAgent state1 listRes with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg2 with
        | Message.FromAgent(AgentToClientMessage.SessionListResult r) ->
            Assert.Single(r.sessions) |> ignore
            Assert.Equal(Some "cursor-2", r.nextCursor)
            Assert.Equal("sess-1", SessionId.value r.sessions.[0].sessionId)
            Assert.Equal("/tmp/project", r.sessions.[0].cwd)
            Assert.Equal(Some "Investigate parity", r.sessions.[0].title)
            Assert.True(r.sessions.[0]._meta.IsSome)
        | other -> failwithf "unexpected message %A" other

        Assert.True(state2.pendingClientRequests.IsEmpty)

    [<Fact>]
    let ``decode set session config option request and response preserves config state`` () =
        let state0 = Codec.CodecState.empty

        let configReq =
            """{"jsonrpc":"2.0","id":22,"method":"session/set_config_option","params":{"sessionId":"sess-1","configId":"mode","value":"code"}}"""

        let state1, msg1 =
            match Codec.decode Codec.Direction.FromClient state0 configReq with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg1 with
        | Message.FromClient(ClientToAgentMessage.SessionSetConfigOption p) ->
            Assert.Equal("sess-1", SessionId.value p.sessionId)
            Assert.Equal("mode", SessionConfigId.value p.configId)
            Assert.Equal("code", SessionConfigValueId.value p.value)
        | other -> failwithf "unexpected message %A" other

        let configRes =
            """{"jsonrpc":"2.0","id":22,"result":{"configOptions":[{"id":"mode","name":"Session Mode","description":"Controls how the agent works","category":"mode","type":"select","currentValue":"code","options":[{"value":"ask","name":"Ask","description":"Request permission first"},{"value":"code","name":"Code","description":"Modify code directly"}]}]}}"""

        let state2, msg2 =
            match Codec.decode Codec.Direction.FromAgent state1 configRes with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg2 with
        | Message.FromAgent(AgentToClientMessage.SessionSetConfigOptionResult r) ->
            Assert.Single(r.configOptions) |> ignore
            Assert.Equal("mode", SessionConfigId.value r.configOptions.[0].id)
            Assert.Equal("code", SessionConfigValueId.value r.configOptions.[0].currentValue)
            Assert.Equal(Some SessionConfigOptionCategory.Mode, r.configOptions.[0].category)
        | other -> failwithf "unexpected message %A" other

        Assert.True(state2.pendingClientRequests.IsEmpty)

    [<Fact>]
    let ``decode typed session info update notification`` () =
        let state0 = Codec.CodecState.empty

        let raw =
            """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"sess-1","update":{"sessionUpdate":"session_info_update","title":"Parity roadmap","updatedAt":"2026-03-19T12:00:00Z","_meta":{"tags":["roadmap"]}}}}"""

        let _, msg =
            match Codec.decode Codec.Direction.FromAgent state0 raw with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg with
        | Message.FromAgent(AgentToClientMessage.SessionUpdate notification) ->
            match notification.update with
            | Acp.Domain.Prompting.SessionUpdate.SessionInfoUpdate update ->
                Assert.Equal(Some "Parity roadmap", update.title)
                Assert.Equal(Some "2026-03-19T12:00:00Z", update.updatedAt)
                Assert.True(update._meta.IsSome)
            | other -> failwithf "unexpected session update %A" other
        | other -> failwithf "unexpected message %A" other

    [<Fact>]
    let ``decode typed config option update notification`` () =
        let state0 = Codec.CodecState.empty

        let raw =
            """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"sess-1","update":{"sessionUpdate":"config_option_update","configOptions":[{"id":"mode","name":"Session Mode","category":"mode","type":"select","currentValue":"ask","options":[{"value":"ask","name":"Ask"},{"value":"code","name":"Code"}]}]}}}"""

        let _, msg =
            match Codec.decode Codec.Direction.FromAgent state0 raw with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg with
        | Message.FromAgent(AgentToClientMessage.SessionUpdate notification) ->
            match notification.update with
            | Acp.Domain.Prompting.SessionUpdate.ConfigOptionUpdate update ->
                Assert.Single(update.configOptions) |> ignore
                Assert.Equal("mode", SessionConfigId.value update.configOptions.[0].id)
            | other -> failwithf "unexpected session update %A" other
        | other -> failwithf "unexpected message %A" other

    [<Fact>]
    let ``direction mismatch surfaces a codec error`` () =
        let state0 = Codec.CodecState.empty

        let badReq =
            """{"jsonrpc":"2.0","id":99,"method":"fs/read_text_file","params":{"sessionId":"s-1","path":"README.md"}}"""

        match Codec.decode Codec.Direction.FromClient state0 badReq with
        | Ok _ -> failwith "expected decode to fail"
        | Error(Codec.DecodeError.DirectionMismatch(methodName, expected)) ->
            Assert.Equal("fs/read_text_file", methodName)
            Assert.Equal(Codec.Direction.FromAgent, expected)
        | Error other -> failwithf "unexpected error %A" other

    [<Fact>]
    let ``decode session info update uses typed payload`` () =
        let state0 = Codec.CodecState.empty

        let update =
            """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-1","update":{"sessionUpdate":"session_info_update","title":"New Title"}}}"""

        let _, msg =
            match Codec.decode Codec.Direction.FromAgent state0 update with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg with
        | Message.FromAgent(AgentToClientMessage.SessionUpdate u) ->
            match u.update with
            | Acp.Domain.Prompting.SessionUpdate.SessionInfoUpdate info ->
                Assert.Equal(Some "New Title", info.title)
                Assert.Equal(None, info.updatedAt)
                Assert.Equal(None, info._meta)
            | other -> failwithf "unexpected update payload %A" other
        | other -> failwithf "unexpected message %A" other

    // ───────────────────────────────────────────────────────────────────────────────
    // _meta passthrough tests (W3C Trace Context)
    // ───────────────────────────────────────────────────────────────────────────────

    [<Fact>]
    let ``decode session prompt request preserves _meta payload`` () =
        let state0 = Codec.CodecState.empty

        let promptReq =
            """{"jsonrpc":"2.0","id":"m1","method":"session/prompt","params":{"sessionId":"s-meta","prompt":[{"type":"text","text":"hello"}],"_meta":{"traceparent":"00-abc-123-01","tracestate":"vendor=x","baggage":"key=val"}}}"""

        let _, msg =
            match Codec.decode Codec.Direction.FromClient state0 promptReq with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg with
        | Message.FromClient(ClientToAgentMessage.SessionPrompt p) ->
            Assert.Equal("s-meta", SessionId.value p.sessionId)
            Assert.True(p._meta.IsSome)

            match p._meta with
            | None -> failwith "expected _meta payload"
            | Some meta ->
                let traceparent = (meta["traceparent"] :?> JsonValue).GetValue<string>()
                let tracestate = (meta["tracestate"] :?> JsonValue).GetValue<string>()
                let baggage = (meta["baggage"] :?> JsonValue).GetValue<string>()
                Assert.Equal("00-abc-123-01", traceparent)
                Assert.Equal("vendor=x", tracestate)
                Assert.Equal("key=val", baggage)
        | other -> failwithf "unexpected message %A" other

    [<Fact>]
    let ``decode session update notification preserves _meta payload`` () =
        let state0 = Codec.CodecState.empty

        let update =
            """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"s-meta2","update":{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"hi"}},"_meta":{"traceparent":"00-def-456-02"}}}"""

        let _, msg =
            match Codec.decode Codec.Direction.FromAgent state0 update with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg with
        | Message.FromAgent(AgentToClientMessage.SessionUpdate u) ->
            Assert.Equal("s-meta2", SessionId.value u.sessionId)
            Assert.True(u._meta.IsSome)

            match u._meta with
            | None -> failwith "expected _meta payload"
            | Some meta ->
                let traceparent = (meta["traceparent"] :?> JsonValue).GetValue<string>()
                Assert.Equal("00-def-456-02", traceparent)
        | other -> failwithf "unexpected message %A" other

    [<Fact>]
    let ``decode session prompt response preserves _meta payload`` () =
        let state0 = Codec.CodecState.empty

        let promptReq =
            """{"jsonrpc":"2.0","id":"meta-res","method":"session/prompt","params":{"sessionId":"s-res-meta","prompt":[{"type":"text","text":"hi"}]}}"""

        let state1, _ =
            match Codec.decode Codec.Direction.FromClient state0 promptReq with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        let promptRes =
            """{"jsonrpc":"2.0","id":"meta-res","result":{"stopReason":"end_turn","_meta":{"traceparent":"00-ghi-789-03"}}}"""

        let _, msg2 =
            match Codec.decode Codec.Direction.FromAgent state1 promptRes with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg2 with
        | Message.FromAgent(AgentToClientMessage.SessionPromptResult r) ->
            Assert.Equal("s-res-meta", SessionId.value r.sessionId)
            Assert.True(r._meta.IsSome)

            match r._meta with
            | None -> failwith "expected _meta payload"
            | Some meta ->
                let traceparent = (meta["traceparent"] :?> JsonValue).GetValue<string>()
                Assert.Equal("00-ghi-789-03", traceparent)
        | other -> failwithf "unexpected message %A" other

    [<Fact>]
    let ``decode session prompt without _meta yields None`` () =
        let state0 = Codec.CodecState.empty

        let promptReq =
            """{"jsonrpc":"2.0","id":"no-meta","method":"session/prompt","params":{"sessionId":"s-nometa","prompt":[{"type":"text","text":"hello"}]}}"""

        let _, msg =
            match Codec.decode Codec.Direction.FromClient state0 promptReq with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg with
        | Message.FromClient(ClientToAgentMessage.SessionPrompt p) ->
            Assert.Equal("s-nometa", SessionId.value p.sessionId)
            Assert.True(p._meta.IsNone)
        | other -> failwithf "unexpected message %A" other

    [<Fact>]
    let ``decode agent message chunk preserves messageId`` () =
        let state0 = Codec.CodecState.empty

        let raw =
            """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"sess-1","update":{"sessionUpdate":"agent_message_chunk","messageId":"m-7","content":{"type":"text","text":"hi"}}}}"""

        let _, msg =
            match Codec.decode Codec.Direction.FromAgent state0 raw with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg with
        | Message.FromAgent(AgentToClientMessage.SessionUpdate notification) ->
            match notification.update with
            | Acp.Domain.Prompting.SessionUpdate.AgentMessageChunk chunk -> Assert.Equal(Some "m-7", chunk.messageId)
            | other -> failwithf "unexpected session update %A" other
        | other -> failwithf "unexpected message %A" other

    [<Fact>]
    let ``decode agent message chunk without messageId is None`` () =
        let state0 = Codec.CodecState.empty

        let raw =
            """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"sess-1","update":{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"hi"}}}}"""

        let _, msg =
            match Codec.decode Codec.Direction.FromAgent state0 raw with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg with
        | Message.FromAgent(AgentToClientMessage.SessionUpdate notification) ->
            match notification.update with
            | Acp.Domain.Prompting.SessionUpdate.AgentMessageChunk chunk -> Assert.True(chunk.messageId.IsNone)
            | other -> failwithf "unexpected session update %A" other
        | other -> failwithf "unexpected message %A" other

    [<Fact>]
    let ``decode typed usage update notification`` () =
        let state0 = Codec.CodecState.empty

        let raw =
            """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"sess-1","update":{"sessionUpdate":"usage_update","used":120,"size":200,"cost":{"amount":0.42,"currency":"USD"}}}}"""

        let _, msg =
            match Codec.decode Codec.Direction.FromAgent state0 raw with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg with
        | Message.FromAgent(AgentToClientMessage.SessionUpdate notification) ->
            match notification.update with
            | Acp.Domain.Prompting.SessionUpdate.UsageUpdate usage ->
                Assert.Equal(120L, usage.used)
                Assert.Equal(200L, usage.size)

                match usage.cost with
                | Some cost ->
                    Assert.Equal("USD", cost.currency)
                    Assert.Equal(0.42, cost.amount, 3)
                | None -> failwith "expected cost"
            | other -> failwithf "unexpected session update %A" other
        | other -> failwithf "unexpected message %A" other

    [<Fact>]
    let ``decode usage update without cost tolerates extras`` () =
        let state0 = Codec.CodecState.empty

        let raw =
            """{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"sess-1","update":{"sessionUpdate":"usage_update","used":1,"size":2,"unknownField":true}}}"""

        let _, msg =
            match Codec.decode Codec.Direction.FromAgent state0 raw with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg with
        | Message.FromAgent(AgentToClientMessage.SessionUpdate n) ->
            match n.update with
            | Acp.Domain.Prompting.SessionUpdate.UsageUpdate usage -> Assert.True(usage.cost.IsNone)
            | other -> failwithf "unexpected %A" other
        | other -> failwithf "unexpected %A" other

    // ───────────────────────────────────────────────────────────────────────────────
    // Task 3: logout (0.13.6 A5)
    // ───────────────────────────────────────────────────────────────────────────────

    [<Fact>]
    let ``decode logout request and response correlates by id`` () =
        let state0 = Codec.CodecState.empty
        let logoutReq = """{"jsonrpc":"2.0","id":31,"method":"logout"}"""

        let state1, msg1 =
            match Codec.decode Codec.Direction.FromClient state0 logoutReq with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg1 with
        | Message.FromClient(ClientToAgentMessage.Logout _) -> ()
        | other -> failwithf "unexpected message %A" other

        Assert.True(state1.pendingClientRequests |> Map.containsKey (RequestId.Number 31L))
        let logoutRes = """{"jsonrpc":"2.0","id":31,"result":{}}"""

        let state2, msg2 =
            match Codec.decode Codec.Direction.FromAgent state1 logoutRes with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg2 with
        | Message.FromAgent(AgentToClientMessage.LogoutResult _) -> ()
        | other -> failwithf "unexpected message %A" other

        Assert.True(state2.pendingClientRequests.IsEmpty)

    [<Fact>]
    let ``initialize result with auth logout capability roundtrips`` () =
        let state0 = Codec.CodecState.empty

        // Register a pending initialize request first
        let initReq =
            """{"jsonrpc":"2.0","id":10,"method":"initialize","params":{"protocolVersion":1}}"""

        let state1, _ =
            match Codec.decode Codec.Direction.FromClient state0 initReq with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        let initRes =
            """{"jsonrpc":"2.0","id":10,"result":{"protocolVersion":1,"agentCapabilities":{"loadSession":true,"mcpCapabilities":{"http":false,"sse":false},"promptCapabilities":{"audio":false,"image":false,"embeddedContext":false},"auth":{"logout":{}}},"authMethods":[]}}"""

        let _, msg =
            match Codec.decode Codec.Direction.FromAgent state1 initRes with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg with
        | Message.FromAgent(AgentToClientMessage.InitializeResult r) ->
            Assert.True(r.agentCapabilities.auth.logout.IsSome)
        | other -> failwithf "unexpected message %A" other

    [<Fact>]
    let ``initialize result with empty auth omits auth key from wire`` () =
        // CRITICAL: absent auth must not be emitted so existing 0.11.x payloads round-trip unchanged.
        let state0 = Codec.CodecState.empty

        // First decode an initialize request so we have a pending entry
        let initReq =
            """{"jsonrpc":"2.0","id":10,"method":"initialize","params":{"protocolVersion":1}}"""

        let state1, _ =
            match Codec.decode Codec.Direction.FromClient state0 initReq with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        // Build an InitializeResult with auth = AgentAuthCapabilities.empty
        let result: Domain.Initialization.InitializeResult =
            { protocolVersion = Domain.PrimitivesAndParties.ProtocolVersion.current
              agentCapabilities =
                { loadSession = false
                  mcpCapabilities = { http = false; sse = false }
                  promptCapabilities =
                    { audio = false
                      image = false
                      embeddedContext = false }
                  sessionCapabilities = Domain.Capabilities.SessionCapabilities.empty
                  auth = Domain.Capabilities.AgentAuthCapabilities.empty }
              agentInfo = None
              authMethods = [] }

        let msg = Message.FromAgent(AgentToClientMessage.InitializeResult result)

        let serialized =
            match Codec.encode (Some(RequestId.Number 10L)) msg with
            | Ok s -> s
            | Error e -> failwithf "unexpected encode error: %A" e

        Assert.DoesNotContain("\"auth\"", serialized)

    [<Fact>]
    let ``decode session close request and response correlates by id`` () =
        let state0 = Codec.CodecState.empty

        let closeReq =
            """{"jsonrpc":"2.0","id":30,"method":"session/close","params":{"sessionId":"sess-1"}}"""

        let state1, msg1 =
            match Codec.decode Codec.Direction.FromClient state0 closeReq with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg1 with
        | Message.FromClient(ClientToAgentMessage.SessionClose p) -> Assert.Equal("sess-1", SessionId.value p.sessionId)
        | other -> failwithf "unexpected message %A" other

        Assert.True(state1.pendingClientRequests |> Map.containsKey (RequestId.Number 30L))

        let closeRes = """{"jsonrpc":"2.0","id":30,"result":{}}"""

        let state2, msg2 =
            match Codec.decode Codec.Direction.FromAgent state1 closeRes with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg2 with
        | Message.FromAgent(AgentToClientMessage.SessionCloseResult _) -> ()
        | other -> failwithf "unexpected message %A" other

        Assert.True(state2.pendingClientRequests.IsEmpty)

    [<Fact>]
    let ``decode session delete request and response correlates by id`` () =
        let state0 = Codec.CodecState.empty

        let deleteReq =
            """{"jsonrpc":"2.0","id":31,"method":"session/delete","params":{"sessionId":"sess-1"}}"""

        let state1, msg1 =
            match Codec.decode Codec.Direction.FromClient state0 deleteReq with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg1 with
        | Message.FromClient(ClientToAgentMessage.SessionDelete p) ->
            Assert.Equal("sess-1", SessionId.value p.sessionId)
        | other -> failwithf "unexpected message %A" other

        Assert.True(state1.pendingClientRequests |> Map.containsKey (RequestId.Number 31L))

        let deleteRes = """{"jsonrpc":"2.0","id":31,"result":{}}"""

        let state2, msg2 =
            match Codec.decode Codec.Direction.FromAgent state1 deleteRes with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg2 with
        | Message.FromAgent(AgentToClientMessage.SessionDeleteResult _) -> ()
        | other -> failwithf "unexpected message %A" other

        Assert.True(state2.pendingClientRequests.IsEmpty)

    [<Fact>]
    let ``decode session resume request and response reattaches sessionId`` () =
        let state0 = Codec.CodecState.empty

        let resumeReq =
            """{"jsonrpc":"2.0","id":"r1","method":"session/resume","params":{"sessionId":"s-1","cwd":"/tmp","mcpServers":[],"additionalDirectories":["/extra"]}}"""

        let state1, msg1 =
            match Codec.decode Codec.Direction.FromClient state0 resumeReq with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg1 with
        | Message.FromClient(ClientToAgentMessage.SessionResume p) ->
            Assert.Equal<string list>([ "/extra" ], p.additionalDirectories)
        | other -> failwithf "unexpected message %A" other

        let resumeRes = """{"jsonrpc":"2.0","id":"r1","result":{}}"""

        let state2, msg2 =
            match Codec.decode Codec.Direction.FromAgent state1 resumeRes with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg2 with
        | Message.FromAgent(AgentToClientMessage.SessionResumeResult r) ->
            Assert.Equal("s-1", SessionId.value r.sessionId)
        | other -> failwithf "unexpected message %A" other

        Assert.True(state2.pendingClientRequests.IsEmpty)

    [<Fact>]
    let ``new session params omit additionalDirectories when empty`` () =
        let state0 = Codec.CodecState.empty

        let req =
            """{"jsonrpc":"2.0","id":3,"method":"session/new","params":{"cwd":"/tmp","mcpServers":[]}}"""

        let _, msg =
            match Codec.decode Codec.Direction.FromClient state0 req with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg with
        | Message.FromClient(ClientToAgentMessage.SessionNew p) -> Assert.True(p.additionalDirectories.IsEmpty)
        | other -> failwithf "unexpected %A" other

    [<Fact>]
    let ``session resume capability roundtrips`` () =
        let state0 = Codec.CodecState.empty

        let initReq =
            """{"jsonrpc":"2.0","id":10,"method":"initialize","params":{"protocolVersion":1}}"""

        let state1, _ =
            match Codec.decode Codec.Direction.FromClient state0 initReq with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        let initRes =
            """{"jsonrpc":"2.0","id":10,"result":{"protocolVersion":1,"agentCapabilities":{"loadSession":true,"mcpCapabilities":{"http":false,"sse":false},"promptCapabilities":{"audio":false,"image":false,"embeddedContext":false},"sessionCapabilities":{"resume":{},"additionalDirectories":{}}},"authMethods":[]}}"""

        let _, msg =
            match Codec.decode Codec.Direction.FromAgent state1 initRes with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg with
        | Message.FromAgent(AgentToClientMessage.InitializeResult r) ->
            Assert.True(r.agentCapabilities.sessionCapabilities.resume.IsSome)
            Assert.True(r.agentCapabilities.sessionCapabilities.additionalDirectories.IsSome)
        | other -> failwithf "unexpected message %A" other

    [<Fact>]
    let ``decode session resume without mcpServers succeeds with empty lists`` () =
        let state0 = Codec.CodecState.empty

        let resumeReq =
            """{"jsonrpc":"2.0","id":"r2","method":"session/resume","params":{"sessionId":"s-1","cwd":"/tmp"}}"""

        let state1, msg1 =
            match Codec.decode Codec.Direction.FromClient state0 resumeReq with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg1 with
        | Message.FromClient(ClientToAgentMessage.SessionResume p) ->
            Assert.Equal<McpServer list>([], p.mcpServers)
            Assert.Equal<string list>([], p.additionalDirectories)
        | other -> failwithf "unexpected message %A" other

        Assert.True(state1.pendingClientRequests |> Map.containsKey (RequestId.String "r2"))
