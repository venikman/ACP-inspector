# Bridge (FPF)

## Header

- BridgeId: BR-ACP-002
- Source SenseCell: <urn:acp-inspector:context:protocol-core:v1, protocol-public-types>
- Target SenseCell: <urn:acp-inspector:context:sentinel-validation:v1, sentinel-public-api>
- senseFamily: other (module-interface)

## Bridge attributes

- kind: subset
- direction: A->B
- CL (congruence level): 3
- scope: type-structure
- loss notes (what is dropped/changed): Sentinel adds findings and assurance models not present in protocol core.

## Valid uses

- Allowed substitutions: Sentinel validation accepts protocol messages and protocol state types.
- Forbidden substitutions: Sentinel must not mutate protocol types or embed findings into domain messages.

## Examples / counterexamples

- Example that passes: Validation.runWithValidation takes a list of Message values.
- Example that fails: Adding ValidationFinding fields into Acp.Domain message records.

## Changelog

- 2026-01-13: created
