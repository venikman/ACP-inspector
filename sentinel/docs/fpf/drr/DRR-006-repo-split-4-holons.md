# DRR (Design-Rationale Record)

- DRR Id: DRR-006
- Date: 2026-01-13
- Owners: ACP Inspector maintainers
- Status: draft
- Links: README.md, docs/architecture/product-structure.md, docs/fpf/contexts/, docs/fpf/bridges/

## 1. Problem frame (why are we talking about this?)

- Trigger: The monorepo mixes protocol, runtime, sentinel, and CLI concerns with different change rates.
- Context: README already defines holons; FPF requires explicit bridges for cross-context use.
- Constraints: No cross-repo source imports; coupling only through versioned package APIs.

## 2. Decision (what will we do?)

- Decision statement: Split ACP Inspector into four repos (protocol core, runtime SDK, sentinel validation, CLI tooling) with explicit package bridges only.
- Scope: F# implementation and its build/test/CI structure.
- Non-goals: Changing ACP semantics or redesigning CLI UX.

## 3. Rationale (why is this the right thing?)

- Options considered:
  - Option A: Keep monorepo unchanged.
  - Option B: 3 repos (protocol + runtime+sentinel + CLI).
  - Option C: 4 repos (protocol, runtime, sentinel, CLI).
- Trade-offs:
  - Option A: Lowest overhead, but weak boundaries and tangled release cadence.
  - Option B: Fewer repos, but couples runtime and sentinel change rates.
  - Option C: More packaging/CI work, but aligns with holons and isolates CLI churn.
- Key assumptions: Internal packages (NuGet or equivalent) are acceptable; maintainers will version packages per repo.

## 4. Consequences (what happens next?)

- Expected benefits: Clear ownership, independent releases, explicit boundary enforcement.
- Risks: Version drift, packaging overhead, boundary leakage during extraction.
- Follow-ups / TODOs: Define public APIs per repo, extract protocol first, publish packages, update CI per repo.
- Migration / deprecation plan (if any): Stepwise extraction with PackageReference bridges; forbid cross-repo internal imports.
