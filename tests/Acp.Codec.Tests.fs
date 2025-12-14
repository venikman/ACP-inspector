namespace Acp.Tests

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
