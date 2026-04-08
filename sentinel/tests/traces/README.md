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

## Recording and Replay

The legacy standalone inspector CLI has been removed. Trace fixtures should be created by whatever harness or runner is exercising ACP and then validated with the unified CLI:

- Validate and normalize a trace: `dotnet run --project cli/apps/ACP.Cli -- inspect sentinel/tests/traces/my-trace.jsonl --record sentinel/tests/traces/normalized.jsonl`
- Replay locally: `dotnet run --project cli/apps/ACP.Cli -- replay sentinel/tests/traces/normalized.jsonl`

Each line still needs the canonical JSONL shape documented above: `ts`, `direction`, and `json`.
