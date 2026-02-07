# Bridge (FPF)

## Header

- BridgeId: BR-ACP-003
- Source SenseCell: <urn:acp-inspector:context:runtime-sdk:v1, runtime-public-api>
- Target SenseCell: <urn:acp-inspector:context:cli-tooling:v1, cli-commands>
- senseFamily: other (module-interface)

## Bridge attributes

- kind: approx
- direction: A->B
- CL (congruence level): 2
- scope: role-assignment/enactment
- loss notes (what is dropped/changed): CLI adds filesystem and terminal IO, argument parsing, and user-facing formatting.

## Valid uses

- Allowed substitutions: CLI commands call runtime Codec/Connection/Transport APIs via public package surface.
- Forbidden substitutions: CLI must not copy or depend on runtime internals outside the public API.

## Examples / counterexamples

- Example that passes: InspectCommand uses Codec.decode from the runtime package.
- Example that fails: CLI code accesses internal runtime modules or private helper functions.

## Changelog

- 2026-01-13: created
