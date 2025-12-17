# SPF — zadanie (task)

**Status**: Active (Stage 0: docs spine + terminology + DRR)

This artifact defines the SPF workstream: terminology, key decisions, required patterns, and acceptance criteria.

## Core intent

- **SPF** is a publication profile of `U.AppliedDiscipline` (not a new root entity).
- **G.14** is an orchestrator pattern added to Part G (FPF Spec) to enable “one-button” SPF generation as a _pattern-set output_ (E.8 templates per pattern), not “one big text”.
- **SPF-Min/Lite/Full** are strictness profiles over the same discipline pack.

## Terminology (Step 0 — must be stable)

See: `docs/spf/README.md`

## Sequencing (must follow)

0. **Terminology**: freeze SPF terms and artifact locations.
1. **DRR**: record the architectural decisions that pin SPF scope and bindings.
2. **G.14**: write the first increment of the orchestrator (Part G).
3. **ATS → E.TGA**: migration by references (no breakage), with a compatibility layer.
4. **Canary pack**: ship one usable SPF‑Min discipline pack with ≥1 realization.

## Step → pattern mapping (short)

- Step 0 (Terminology) → **E.8** (authoring conventions + stable artifact layout) + **G.14 output contract** (what gets generated)
- Step 1 (DRR) → **E.9** (Decision Records / DRR)
- Step 2 (G.14) → **G.14** (SPF orchestrator; profile hooks + pattern-set outputs)
- Step 3 (ATS → `E.TGA`) → **G.10** (hook surface) + **E.TGA** (GateCrossing) + **G.14.2** (vertical slice requirement)
- Step 4 (Canary SPF‑Min pack) → **G.3** (CHR), **G.4** (CAL), **E.TGA** (crossing doc), **G.14.5** (realization minimum)

## Repo staging plan (each stage = one clean commit)

### Stage 0 — Docs spine + terminology + DRR

Deliverables:

- `docs/fpf/FPF-Spec.md` (stable spec path)
- `docs/spf/SPF-zadanie.md` (this task)
- `docs/spf/README.md` (profiles + artifact layout)
- `docs/decisions/DRR-0001-spf-g14.md` (lean DRR)

Definition of Done:

- No code behavior changes.
- `dotnet test` passes.
- From repo root `README.md`, a reader can locate: SPF task, FPF spec, DRR.

### Stage 1 — G.14 (first increment) + profile hooks

Deliverables:

- Add **G.14** to Part G with subpatterns:
  - G.14.1 Passport & Signature (skeleton)
  - G.14.2 Vertical slice + `E.TGA`
  - G.14.3 CHR profile hook (reuse G.3)
  - G.14.4 CAL profile hook (reuse G.4)
  - G.14.5 Realization minimum
  - G.14.6 SoTA‑Echo binding
- Add a short “Step-to-pattern mapping” note (each SPF step tied to existing patterns).

Definition of Done:

- G.14 describes “one-button” generation as a pattern-set output (E.8 template per pattern).

### Stage 2 — ATS → `E.TGA` migration (by references; no breakage)

Deliverables (FPF Spec):

- In G.10, add an explicit alternative path: “TGA hooks” alongside any legacy AH hooks.
- Replace “MUST expose ATS hooks” with “MUST expose crossing hooks (ATS or TGA)”, then formally deprecate ATS over 1–2 editions.

Definition of Done:

- No normative text still requires AH‑1..AH‑4 as the only compliance route.
- Crossings are expressed in `E.TGA` terms (GateCrossing + Bridge + UTS + penalties → `R_eff`).

### Stage 3 — Canary SPF‑Min pack (Evolutionary Architecture)

Deliverables (docs-only is acceptable):

- `docs/spf/packs/evolutionary-architecture/` with E.8‑conformant patterns:
  - `CHR-Sys.md`
  - `CHR-Epi.md`
  - `CAL.md`
  - `E-TGA-01.md`
  - `Realization-Case-01.md`

Definition of Done:

- You can honestly execute: “generate SPF‑Min for <discipline> and get a usable pack.”
- SoTA‑Echoing sections cite chosen SoTA sources and record adopt/adapt/reject decisions.

## Decision record

See: `docs/decisions/DRR-0001-spf-g14.md`
