# ACP Protocol (repo-local pointers)

**Status**: Informative (the upstream ACP spec/schema is the normative source of truth)

This repo implements the Agent Client Protocol (ACP) schema and semantics. The normative ACP specification is published externally:

- Spec + schema (GitHub): <https://github.com/agentclientprotocol/agent-client-protocol>
- Overview/intro (website): <https://agentclientprotocol.com/overview/introduction>

This document exists to provide repo-local navigation and stable links from other docs (architecture notes, references, etc.).

## Implementation pointers

- Domain model: `src/Acp.Domain.fs`
- Protocol state machine: `src/Acp.Protocol.fs`
- Codec: `src/Acp.Codec.fs`
- Validation/sentinel: `src/Acp.Validation.fs`
- Runtime boundary: `src/Acp.RuntimeAdapter.fs`

## Version pins (code)

- ACP schema target: `Acp.Domain.Spec.Schema` (see `src/Acp.Domain.fs`)
- Negotiated `protocolVersion`: `Acp.Domain.PrimitivesAndParties.ProtocolVersion.current`
