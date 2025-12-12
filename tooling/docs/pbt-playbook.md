# Property‑Based Testing (PBT) Playbook

This repo uses FsCheck PBT to generate adversarial ACP message traces at pure boundaries
(`Acp.Protocol.spec` and `Acp.RuntimeAdapter.validateInbound/validateOutbound`). PBT complements,
but does not replace, small scenario tests.

## 1. Writing generators

Generators live under `tests/Pbt/Generators.fs`.

Guidelines:

- Prefer **near‑valid traces** with rare invalid injections. This keeps discard low and shrinking useful.
- Keep payload values small and stable (capabilities, info records) unless a property is explicitly about them.
- Use state‑aware generation for sequencing rules (init handshake, session tracking, prompt lifecycle).

When adding a new domain type:

1. Add a `Gen<'t>` and `Arbitrary<'t>` in `Generators.fs`.
2. Bias toward “just‑over‑the‑edge” values (e.g., short strings, 0/1/limit±1 sizes).
3. Ensure any `obj` payloads are comparable (strings/ints) so determinism checks stay reliable.

## 2. Classifying properties into FPF lanes

Each property should be tagged by:

- **Validator lane**: where violations are detected (`Protocol`, `Session`, `Transport`, …).
- **FPF lane**: what kind of evidence this is:
  - **F‑lane (Form)**: structure/shape invariants (e.g., determinism, stable codes).
  - **G‑lane (Grounding)**: model‑vs‑runtime contract checks (e.g., adapter size rules).
  - **R‑lane (Assurance)**: end‑to‑end assurance claims (e.g., trace‑replay totality/localization).

Keep these lane tags independent.

## 3. Reproducibility

FsCheck config is env‑driven in `tests/Pbt/Generators.fs`:

- `ACP_PBT_MAX_TEST` (default 200)
- `ACP_PBT_START_SIZE` (default 1)
- `ACP_PBT_END_SIZE` (default 50)
- `ACP_PBT_SEED` (optional replay seed)

Scripts:

- `tooling/scripts/run-pbt.sh` runs only PBT tests.
- `tooling/scripts/replay-pbt.sh <seed> <size> [filter]` reproduces failures.

PR checklist: if a PBT fails, paste the seed, size, and shrunk trace into the PR.

## 4. Evidence artifacts

Machine‑readable evidence items live in `core/evidence/pbt/`.
`ACP-EVD-PBT-001.json` is the canonical template for recording:

- git commit
- FsCheck seed and config
- shrink count and minimal counterexample trace
- extracted `ValidationFinding` list

Update the evidence item whenever a new minimal counterexample is found.

On any PBT failure, FsCheck’s custom runner (`PbtEvidenceRunner`) automatically writes
`core/evidence/pbt/ACP-EVD-PBT-latest-failure.json` containing the seed, shrink count,
and pre/shrunk arguments. Copy that into the canonical evidence item when accepting
the counterexample.

## 5. What to add next

Next high‑leverage properties:

1. Add explicit “valid‑only” acceptance invariants for each protocol slice you extend.
2. Extend the Protocol state machine with new ACP methods as they land.
3. Add fault‑injection variants (size/ordering/duplication) once IO boundaries are modeled.
