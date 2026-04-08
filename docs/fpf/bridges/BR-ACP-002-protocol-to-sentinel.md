# Bridge (FPF)

## Header

- BridgeId: BR-ACP-002
- Source SenseCell: <urn:acp-inspector:context:protocol-core:v1, protocol-public-types>
- Target SenseCell: <urn:acp-inspector:context:sentinel-validation:v1, sentinel-public-api>
- senseFamily: other (module-interface)

## Bridge attributes

- kind: subset-plus-projection
- direction: A->B
- CL (congruence level): 3
- scope: protocol messages, protocol state, validation findings, assurance projections

## Bridge scope

- Protocol core is the source of truth for ACP messages, session state, and protocol admissibility.
- Sentinel consumes protocol artifacts, validates them, and publishes derived findings and assurance views.
- Sentinel does not own ACP message meaning and does not redefine protocol semantics.

## No New Semantics On Projection

Sentinel may classify, validate, summarize, and report on protocol artifacts, but every sentinel publication surface remains a projection. CLI output, findings, parity reports, and assurance summaries must not introduce protocol meaning that the protocol core does not already carry or that the bridge does not explicitly mark as derived.

## A.6 Mapping

| Claim class | Owner across the bridge | Sentinel role |
| ----------- | ----------------------- | ------------- |
| Laws / invariants | Protocol state machine and protocol domain rules | Read and validate them; do not rewrite them |
| Admissibility | Protocol step rules and capability/state checks | Report admissibility failures as findings |
| Commitments / deontics | ACP spec, repo docs, and declared protocol capabilities | Read commitments; do not invent new ones in sentinel output |
| Evidence / work-effects | Trace messages, tool results, plan updates, protocol events | Emit findings, reports, and assurance projections over those events |

## Valid uses

- Sentinel validation may accept `Message` values and protocol state snapshots as input.
- Sentinel may publish findings, assurance metadata, and reports that cite protocol artifacts.
- Sentinel may attach loss notes when it projects unstable ACP features or heuristic interpretations.

## Forbidden uses

- Embedding sentinel findings into protocol core message types as if they were native ACP fields.
- Treating parity reports, CLI summaries, or assurance classifications as replacement protocol objects.
- Using sentinel-local interpretations to silently extend ACP stable semantics.

## Loss Notes

- Findings are sentinel-local projections, not protocol-native objects.
- Assurance metadata is local unless ACP carries it explicitly.
- Parity trackers, reports, and CLI summaries are publication surfaces over protocol artifacts, not additions to the ACP schema.

## A.16 Clause

When sentinel validates unstable ACP features, the bridge must mark those projections as draft-language-state material. Draft validation support may be published, but it must not be presented as stable protocol guarantee unless the underlying ACP surface is itself stable.

## Examples / counterexamples

- Example that passes: `Validation.runWithValidation` accepts a list of protocol `Message` values and emits `ValidationFinding` projections.
- Example that passes: CLI `inspect` renders protocol frames plus sentinel findings without mutating the underlying message model.
- Example that fails: adding `ValidationFinding` fields directly to protocol domain records.
- Example that fails: describing a sentinel report as if it were ACP-native protocol state.

## Changelog

- 2026-01-13: created
- 2026-03-19: expanded for `A.6`, `E.17`, and `A.16`; added no-new-semantics guardrail
