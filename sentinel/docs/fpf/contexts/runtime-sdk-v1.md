# Context Card (FPF)

## Header
- ContextId: urn:acp-inspector:context:runtime-sdk:v1
- Domain family (if any): ACP runtime integration
- Edition / date: v1 / 2026-01-13
- Time stance: run
- Owner / maintainer: ACP Inspector maintainers

## Purpose (why does this context exist?)
- Intended use: Provide transport, codec, connection APIs, and contrib helpers for ACP clients and agents.
- Non-goals: Defining protocol semantics, validation/assurance rules, or CLI presentation.

## Scope boundary (semantic boundary)
- In-scope: Transport abstractions, codec encode/decode, connection lifecycle, contrib helpers, observability metrics.
- Out-of-scope: Protocol type definitions, validation findings, runtime adapter validation, CLI commands.
- Boundary tests (how to tell): If code validates protocol correctness or defines protocol types, it is out-of-scope.

## Primary sources (authoritative)
- README.md (SDK modules)
- runtime/src/Acp.Transport.fs and runtime/src/Acp.Connection.fs
- runtime/src/Acp.Codec.fs and runtime/src/Acp.Observability.fs

## Key terms (seed list)
- ITransport: send/receive abstraction
- Codec: JSON-RPC encode/decode
- ClientConnection: client-side ACP connection
- AgentConnection: agent-side ACP connection
- SessionAccumulator: contrib session state

## Invariants (context-true)
- Depends only on the protocol package.
- No validation lanes or assurance logic in runtime APIs.
- Observability tags are duplicated (shared shape, separate implementation) with sentinel.

## Adjacent contexts (do not merge)
- urn:acp-inspector:context:protocol-core:v1: protocol types and state machine
- urn:acp-inspector:context:sentinel-validation:v1: validation/assurance layer
- urn:acp-inspector:context:cli-tooling:v1: CLI tooling consumes runtime APIs

## Open questions / risks
- Unknown 1: Final package name and versioning scheme for runtime SDK.
- Risk 1: Observability tags drift between runtime and sentinel copies.

## Changelog
- 2026-01-13: created
