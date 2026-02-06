# Trace Format (JSONL)

ACP Inspector tooling uses a newline-delimited JSON (`*.jsonl`) trace format.

Each line is a single JSON object with these fields:

- `ts`: timestamp for the frame (ISO-8601 string or Unix milliseconds).
- `direction`: `"fromClient"` or `"fromAgent"` (case-insensitive).
- `json`: the raw JSON-RPC message as a string.

Example:

```json
{"ts":"2025-01-15T10:00:00.000Z","direction":"fromClient","json":"{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":1}}"}
```

## Notes

- `json` is a string that contains JSON. That means it must be escaped inside the outer JSON object.
- Traces are order-sensitive: the CLI and sentinel validate the sequence as it happened.
- `acp-inspector inspect --record <out.jsonl> <in.jsonl>` writes a canonical JSONL file containing only frames that successfully decode at the codec boundary.
