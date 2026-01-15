# Context Card (FPF)

## Header

- ContextId: urn:acp-inspector:context:protocol-core:v1
- Domain family (if any): ACP protocol
- Edition / date: v1 / 2026-01-13
- Time stance: design
- Owner / maintainer: ACP Inspector maintainers

## Purpose (why does this context exist?)

- Intended use: Define pure ACP domain types and the protocol state machine used by runtime and sentinel packages.
- Non-goals: Transport IO, JSON encoding/decoding, validation lanes, observability, CLI behavior.

## Scope boundary (semantic boundary)

- In-scope: Domain types (sessions, messages, capabilities), protocol phase/state transitions, protocol error codes.
- Out-of-scope: Codec, transports, validation findings, assurance/eval, CLI commands.
- Boundary tests (how to tell): If a type/function needs IO, JSON parsing, or validation logic, it is out-of-scope.

## Primary sources (authoritative)

- README.md (holon definitions)
- [ACP spec](https://github.com/agentclientprotocol/agent-client-protocol)
- protocol/src/Acp.Domain.fs and protocol/src/Acp.Protocol.fs

## Key terms (seed list)

- Message: typed ACP message union
- SessionId: opaque session identifier
- ProtocolVersion: negotiated major version
- Phase: protocol phase state
- ProtocolError: protocol error classification

## Invariants (context-true)

- No IO or side effects in public API.
- No dependencies on runtime, sentinel, or CLI packages.

## Adjacent contexts (do not merge)

- urn:acp-inspector:context:runtime-sdk:v1: runtime APIs consume protocol types via package bridge
- urn:acp-inspector:context:sentinel-validation:v1: validation consumes protocol types via package bridge
- urn:acp-inspector:context:cli-tooling:v1: CLI consumes protocol types transitively

## Open questions / risks

- Unknown 1: Confirm whether protocol ContextId should align with external ACP URN.
- Risk 1: Protocol version upgrades ripple through runtime and sentinel packages.

## Changelog

- 2026-01-13: created
