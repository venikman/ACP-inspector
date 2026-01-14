# Bridge (FPF)

## Header
- BridgeId: BR-ACP-001
- Source SenseCell: <urn:acp-inspector:context:protocol-core:v1, protocol-public-types>
- Target SenseCell: <urn:acp-inspector:context:runtime-sdk:v1, runtime-public-api>
- senseFamily: other (module-interface)

## Bridge attributes
- kind: subset
- direction: A->B
- CL (congruence level): 3
- scope: type-structure
- loss notes (what is dropped/changed): Runtime adds IO/transport metadata; protocol core does not model framing, timeouts, or observability.

## Valid uses
- Allowed substitutions: Runtime APIs accept/return protocol core types (Message, SessionId, ProtocolError).
- Forbidden substitutions: Protocol core must not depend on runtime types; runtime must not redefine protocol types.

## Examples / counterexamples
- Example that passes: Acp.Connection uses Acp.Domain.Messaging.Message from the protocol package.
- Example that fails: Protocol package references Acp.Transport.ITransport or Codec.decode.

## Changelog
- 2026-01-13: created
