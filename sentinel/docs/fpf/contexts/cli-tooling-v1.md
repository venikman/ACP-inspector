# Context Card (FPF)

## Header
- ContextId: urn:acp-inspector:context:cli-tooling:v1
- Domain family (if any): ACP tooling and analysis
- Edition / date: v1 / 2026-01-13
- Time stance: run
- Owner / maintainer: ACP Inspector maintainers

## Purpose (why does this context exist?)
- Intended use: Provide CLI tooling for inspect/validate/replay/analyze/benchmark over ACP traces.
- Non-goals: Defining protocol semantics, validation rules, or transport implementations.

## Scope boundary (semantic boundary)
- In-scope: CLI commands, argument parsing, file IO, output formatting, telemetry wiring.
- Out-of-scope: Protocol type definitions, codec implementation, validation logic.
- Boundary tests (how to tell): If logic is reusable library code, it belongs in runtime or sentinel.

## Primary sources (authoritative)
- README.md (CLI commands)
- cli/apps/ACP.Cli/ and cli/apps/ACP.Inspector/
- cli/apps/ACP.Benchmark/ and cli/examples/cli-demo/

## Key terms (seed list)
- inspect: full validation of traces
- validate: stdin message validation
- replay: trace replay workflow
- analyze: trace statistics
- benchmark: performance modes

## Invariants (context-true)
- Depends only on published packages (protocol, runtime, sentinel).
- No direct source imports from other repos.

## Adjacent contexts (do not merge)
- urn:acp-inspector:context:runtime-sdk:v1: runtime APIs consumed by CLI
- urn:acp-inspector:context:sentinel-validation:v1: validation findings consumed by CLI
- urn:acp-inspector:context:protocol-core:v1: protocol types consumed transitively

## Open questions / risks
- Resolved: CLI tool name is `acp-inspector`.
- Risk 1: CLI commands drift from inspector/runtime package versions.

## Changelog
- 2026-01-13: created
