# FPF Task Frame

## 0. Prompt (verbatim)
> Convert https://github.com/EveryInc/compound-engineering-plugin/tree/main into Codex agents, using FPF.

## 1. BoundedContext (U.BoundedContext)
- ContextId: urn:acp-inspector:context:codex-compound-engineering:v1
- Scope boundary (in / out):
  - In: project-local Codex prompts/skills under `./.codex/` + scripts to generate/validate them.
  - Out: ACP Inspector runtime/protocol/sentinel/CLI code changes; any global Codex home modifications.
- Time stance: run
- Primary sources (standards, repos, docs):
  - Upstream plugin repo (pinned): EveryInc/compound-engineering-plugin @ e4ff6a874c1330e3e6b672db14e6d0b8847910de (v2.30.0)
  - Local conversion scripts: `scripts/ce_prefix_codex_bundle.py`, `scripts/ce_smoke_check.sh`
  - FPF conceptual reference: `/Users/stas-studio/Downloads/FPF-Spec (12).md`

## 2. Role (U.Role) and holder
- Role (who is acting): Codex (agent) acting on behalf of ACP Inspector maintainers
- Holder scope: this repository working tree (`/Users/stas-studio/Developer/ACP-inspector`)
- Constraints (permissions, policies, safety):
  - Project-local only; do not write to `~/.codex`.
  - Namespace all imported items with `ce-` to avoid collisions with existing global skills.
  - Keep conversion repeatable and verifiable with a smoke-check.

## 3. Capability (what can be done)
- Capability statement:
  - Import upstream Compound Engineering plugin markdown assets into Codex prompts/skills for use while developing ACP Inspector, while maintaining FPF auditability (Context + Task Frame + acceptance harness).
- Non-goals:
  - Do not attempt to “make every imported agent perfect for F#/.NET” in this pass.
  - Do not introduce new runtime dependencies in ACP Inspector itself.

## 4. Method / MethodDescription / Work
- Method (idea):
  - Generate a Codex bundle from the upstream plugin, then post-process it to be project-safe: namespaced + Claude-only paths removed + smoke-checked.
- MethodDescription (recipe artifact to create):
  - `scripts/ce_prefix_codex_bundle.py` (idempotent post-processor)
  - `scripts/ce_smoke_check.sh` (acceptance harness)
- Work (execution artifacts to produce, with paths):
  - Imported bundle: `./.codex/prompts/ce-*.md`, `./.codex/skills/ce-*/...`
  - FPF wrapper docs:
    - `docs/fpf/contexts/codex-compound-engineering-v1.md`
    - `docs/fpf/task-frames/TF-codex-compound-engineering-import-v1.md`

## 5. Acceptance harness (tests)
- Success criteria (objective, measurable):
  - `bash scripts/ce_smoke_check.sh` exits 0 and prints `OK`.
- Negative tests / failure modes:
  - Any remaining `${CLAUDE_PLUGIN_ROOT}` reference under `./.codex/` fails the smoke-check.
  - Any prompt that references a missing skill fails the smoke-check.
  - Any `/prompts:ce-*` reference that points to a non-existent prompt fails the smoke-check.
- Minimal verification steps:
  - Run: `bash scripts/ce_smoke_check.sh`

## 6. Unknowns and next questions
- Dominant unknowns:
  - Whether to commit `./.codex/` to git (team-shared) or treat it as local tooling only.
- Fastest way to reduce uncertainty:
  - Decide repo policy:
    - Option A: commit `./.codex/` and scripts (recommended for team use).
    - Option B: add `.codex/` to `.gitignore` and treat as local-only.

