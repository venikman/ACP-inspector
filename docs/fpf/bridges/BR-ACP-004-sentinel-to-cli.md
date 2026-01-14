# Bridge (FPF)

## Header
- BridgeId: BR-ACP-004
- Source SenseCell: <urn:acp-inspector:context:sentinel-validation:v1, sentinel-public-api>
- Target SenseCell: <urn:acp-inspector:context:cli-tooling:v1, cli-commands>
- senseFamily: other (module-interface)

## Bridge attributes
- kind: approx
- direction: A->B
- CL (congruence level): 2
- scope: role-assignment/enactment
- loss notes (what is dropped/changed): CLI is presentation and IO; sentinel is validation library logic.

## Valid uses
- Allowed substitutions: CLI formats ValidationFinding values and uses sentinel validation entry points.
- Forbidden substitutions: Sentinel must not depend on CLI output or argument parsing libraries.

## Examples / counterexamples
- Example that passes: CLI prints ValidationFinding severity and lane.
- Example that fails: Sentinel references Argu or CLI Output helpers.

## Changelog
- 2026-01-13: created
