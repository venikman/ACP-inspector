# Glossary

- **ACP**: Agent Client Protocol, a JSON-RPC 2.0 protocol for client/agent sessions, prompts, tool calls, and streaming updates.
- **Inspector**: CLI mode that reads ACP traces and reports validation findings.
- **Sentinel**: Validation layer that checks ACP traffic and emits findings.
- **Lane**: A validation category used to group findings.
- **Finding**: A validation result with severity and context.
- **Holon**: A coarse-grained layer in this repo (protocol, runtime, sentinel).
- **Trace**: A JSONL stream or file of ACP messages, typically captured from runtime traffic.
- **Draft RFD**: A draft Request for Dialog that proposes future ACP changes; supported behind a feature gate.
- **Unstable gate**: The `--acp-unstable` flag that enables draft RFD parsing/display.
