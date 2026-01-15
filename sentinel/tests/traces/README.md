# Trace fixtures (JSONL)

This folder contains newline-delimited JSON (`*.jsonl`) recordings of ACP JSON-RPC traffic.

They are used by regression tests to ensure recorded real-world sessions remain valid under the sentinel
(`Codec.decode` + `Validation.runWithValidation`).

## Format

Each line is a JSON object with at least:

- `direction`: `"fromClient"` or `"fromAgent"`
- `json`: the raw JSON-RPC message (string)

Example line:

```json
{"ts":"2025-01-01T00:00:00.0000000+00:00","direction":"fromClient","json":"{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":1}}"}
```

## Recording

Use the inspector to record and then replay:

- Record: `dotnet run --project cli/apps/ACP.Inspector/ACP.Inspector.fsproj -- proxy-stdio --client-cmd "<client>" --agent-cmd "<agent>" --record sentinel/tests/traces/my-trace.jsonl`
- Replay locally: `dotnet run --project cli/apps/ACP.Inspector/ACP.Inspector.fsproj -- replay --trace sentinel/tests/traces/my-trace.jsonl`
