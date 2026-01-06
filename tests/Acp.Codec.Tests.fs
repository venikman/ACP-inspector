namespace Acp.Tests

open System.Text.Json.Nodes
open Xunit

open Acp
open Acp.Domain
open Acp.Domain.JsonRpc
open Acp.Domain.PrimitivesAndParties
open Acp.Domain.Messaging
open Acp.Domain.Prompting

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
            Assert.True(r.usage.IsNone)
        | other -> failwithf "unexpected message %A" other

        Assert.True(state2.pendingClientRequests.IsEmpty)

    [<Fact>]
    let ``decode session prompt response preserves usage payload`` () =
        let state0 = Codec.CodecState.empty

        let promptReq =
            """{"jsonrpc":"2.0","id":"p2","method":"session/prompt","params":{"sessionId":"s-2","prompt":[{"type":"text","text":"hi"}]}}"""

        let state1, _ =
            match Codec.decode Codec.Direction.FromClient state0 promptReq with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        let promptRes =
            """{"jsonrpc":"2.0","id":"p2","result":{"stopReason":"end_turn","usage":{"inputTokens":5,"outputTokens":7}}}"""

        let state2, msg2 =
            match Codec.decode Codec.Direction.FromAgent state1 promptRes with
            | Ok r -> r
            | Error e -> failwithf "unexpected decode error: %A" e

        match msg2 with
        | Message.FromAgent(AgentToClientMessage.SessionPromptResult r) ->
            Assert.Equal("s-2", SessionId.value r.sessionId)
            Assert.Equal(StopReason.EndTurn, r.stopReason)
            Assert.True(r.usage.IsSome)

            match r.usage with
            | None -> failwith "expected usage payload"
            | Some usage ->
                match usage["inputTokens"] with
                | null -> failwith "expected inputTokens"
                | node ->
                    let value = (node :?> JsonValue).GetValue<int>()
                    Assert.Equal(5, value)
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
    let ``decode unknown session update preserves payload`` () =
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
            | SessionUpdate.Ext(tag, payload) ->
                Assert.Equal("session_info_update", tag)

                match payload["title"] with
                | null -> failwith "expected title in ext payload"
                | titleNode ->
                    let title = (titleNode :?> JsonValue).GetValue<string>()
                    Assert.Equal("New Title", title)
            | other -> failwithf "unexpected update payload %A" other
        | other -> failwithf "unexpected message %A" other
