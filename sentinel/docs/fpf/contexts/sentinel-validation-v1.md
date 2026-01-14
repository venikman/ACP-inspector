# Context Card (FPF)

## Header

- ContextId: urn:acp-inspector:context:sentinel-validation:v1
- Domain family (if any): ACP validation and assurance
- Edition / date: v1 / 2026-01-13
- Time stance: run
- Owner / maintainer: ACP Inspector maintainers

## Purpose (why does this context exist?)

- Intended use: Validate ACP traffic, emit findings, and provide assurance/eval tooling at the runtime boundary.
- Non-goals: Transport IO, codec implementation, CLI presentation.

## Scope boundary (semantic boundary)

- In-scope: Validation lanes/findings, protocol compliance checks, eval profiles, assurance models, runtime adapter.
- Out-of-scope: Transports, JSON-RPC codec, CLI command handling.
- Boundary tests (how to tell): If code reads/writes IO or parses raw JSON, it is out-of-scope.

## Primary sources (authoritative)

- README.md (sentinel holon)
- sentinel/src/Acp.Validation.fs and sentinel/src/Acp.RuntimeAdapter.fs
- sentinel/src/Acp.Assurance.fs and sentinel/src/Acp.Eval.fs

## Key terms (seed list)

- ValidationFinding: structured validation event
- Lane: validation lane classification
- Severity: finding severity
- RuntimeAdapter: inbound/outbound validation boundary
- EvalProfile: evaluation switches

## Invariants (context-true)

- Depends only on the protocol package.
- Validation findings are immutable and do not mutate protocol types.
- Observability tags are duplicated (shared shape, separate implementation) with runtime.

## Adjacent contexts (do not merge)

- urn:acp-inspector:context:protocol-core:v1: protocol types and state machine
- urn:acp-inspector:context:runtime-sdk:v1: runtime IO/codec surface
- urn:acp-inspector:context:cli-tooling:v1: CLI consumes findings for presentation

## Open questions / risks

- Unknown 1: Whether runtime adapter needs optional runtime types in future.
- Risk 1: Boundary leakage if runtime or CLI bypass sentinel APIs.
- Risk 2: Observability tags drift between sentinel and runtime copies.

## Changelog

- 2026-01-13: created
