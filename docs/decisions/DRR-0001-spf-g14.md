# DRR-0001: SPF publication profile + G.14 orchestrator + SoTA binding + ATS compatibility

**Status**: Accepted (Stage 0 spine)
**Date**: 2025-12-17

## Context

The SPF workstream needs stable terminology and a small set of pinned architectural decisions *before* introducing new patterns (G.14) or migration edits (ATS → `E.TGA`). Without these pins, downstream doc work risks rework (different “what is SPF?” interpretations, duplicated SoTA, or premature removal of legacy hooks).

This repo already treats FPF specs as normative documentation that drives implementation and validation shape. SPF must therefore be defined in a way that:

- Preserves existing model roots and avoids inventing new top-level entities.
- Lets “one-button generation” be expressed as a composition of patterns (E.8 templates), not a bespoke monolith.
- Avoids duplicating SoTA while still producing usable, citation-backed discipline packs.
- Provides a no-breakage migration path away from ATS-only compliance language.

## Decision (pinned)

1. **SPF is a publication profile of `U.AppliedDiscipline`**.
   - SPF is *not* a new root entity, model, or subsystem.
   - SPF outputs are published “views” (profiles) over the same applied discipline content.

2. **Add a `G.14` orchestrator to Part G; SPF-Min/Lite/Full are strictness profiles**.
   - `G.14` is the orchestrator that composes existing patterns into a coherent SPF output set.
   - **SPF-Min**, **SPF-Lite**, **SPF-Full** are *strictness profiles* over the same discipline pack (not different pack types).

3. **SoTA-Pack vs SoTA-Echoing binding rule**.
   - **SoTA-Pack** is a curated bundle of sources and decisions (adopt/adapt/reject) used to ground a discipline pack.
   - **SoTA-Echoing** is a *projection/face* over existing SoTA data (G.2) and cited sources; it is not a duplicated, competing SoTA artifact.
   - SPF outputs may include SoTA-Echoing sections, but they must cite sources and reference the SoTA data they project from.

4. **ATS migration path: keep ATS; add compatibility via `E.TGA` hooks/crossings**.
   - Do **not** rip out ATS or require an immediate rewrite of existing hook integrations.
   - Introduce (and prefer) `E.TGA` crossings as the enforceable control surface.
   - Provide a compatibility layer so existing ATS-oriented integrations can map into `E.TGA` (via crossings/hooks) during the transition window.
   - Deprecate ATS over 1–2 editions of the normative text (not in one breaking change).

## Consequences

- SPF can be implemented and published without expanding the root ontology: it stays a profile of `U.AppliedDiscipline`.
- “One-button generation” is naturally explained as orchestrating pattern templates (E.8), which keeps the spec modular and compositional.
- SoTA does not fork: SoTA-Echoing remains traceable to sources and existing SoTA data, preventing drift and duplication.
- Existing ATS consumers remain functional while the spec shifts normative compliance toward `E.TGA` crossings.

## Rejected alternatives (short)

- **Make SPF a new root entity**: increases ontology surface area and invites parallel/duplicated definitions.
- **Duplicate SoTA into SPF packs**: creates drift, conflicting updates, and unclear authority.
- **Remove ATS immediately**: introduces breaking changes without a compatible migration path.

## Follow-ups (tracked in `docs/spf/SPF-zadanie.md`)

- Stage 1: add `G.14` (first increment) with profile hooks and step→pattern mapping.
- Stage 2: update normative text to accept “ATS or TGA” and deprecate ATS explicitly.
- Stage 3: ship one SPF‑Min canary discipline pack with ≥1 realization.
