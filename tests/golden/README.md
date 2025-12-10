# Golden set for Deep Research validator

This folder contains small, hand-curated expectations for FINDER/DEFT runs. Use
them to regression-test the validator:

- `deep-research-sample.yaml` — three sample tasks with expected checklist,
  structure, evidence-grounding, and DEFT failure counts.

Recommended use:
- Load this file in tests; compare emitted metrics from the validator against the
  expected values (allowing small tolerance for heuristic scores).
- Ensure taxonomy scores are monotone (more failures → lower S_f) on these cases.
- Keep this set small; update values only when the underlying task specs change.
