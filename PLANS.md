# ExecPlan: Compound Engineering Plugin -> Codex (FPF-Wrapped)

## Goal
Convert the upstream `EveryInc/compound-engineering-plugin` (Claude Code plugin) into **project-local** Codex prompts/skills for ACP-inspector, and add minimal FPF artifacts that make the conversion auditable and repeatable.

## Success criteria (observable)
- A project-local Codex bundle exists at `./.codex/`:
  - `./.codex/prompts/` contains prefixed prompts (no collisions with global skills).
  - `./.codex/skills/` contains prefixed skill directories with `SKILL.md` and any required scripts/assets.
- All internal references are consistent:
  - Prompt content references `$<skill>` names that exist under `./.codex/skills/`.
  - No remaining references to `${CLAUDE_PLUGIN_ROOT}` in generated skills (replaced with repo-relative paths).
- Minimal FPF wrapper artifacts exist:
  - Context Card describing the meaning boundary for these agents.
  - Task Frame documenting the conversion method + acceptance harness.
- A smoke-check passes (see Milestones).

## Non-goals
- Do not modify `~/.codex/config.toml` or `~/.codex/AGENTS.md`.
- Do not “port” agent logic into runtime code; this is documentation + Codex prompt/skill wiring only.
- Do not rewrite the entire Compound content into perfect FPF Role/Method cards; only minimal FPF wrappers needed for auditability.

## Constraints (sandbox, network, OS, time, dependencies)
- OS: macOS (repo is at `/Users/stas-studio/Developer/ACP-inspector`).
- Network: required to fetch upstream repo and/or Bun dependencies (explicit user request).
- Tools: use `bun` (available) + `git`.
- Safety: avoid destructive commands; keep changes reversible; do not stage/commit unrelated local changes.

## Repo map (key files/dirs)
- `./docs/fpf/contexts/` (existing Context Cards)
- `./docs/fpf/bridges/` (existing Bridges)
- `./docs/fpf/drr/` (existing DRRs)
- `./.codex/` (to be created: project-local Codex prompts/skills)
- `./scripts/` (optional: repeatable import script)

## Milestones
1. Plan + scaffolding
   - Steps
     - Create `PLANS.md` (this file).
     - Decide output location and namespacing to avoid collisions (prefix `ce-`).
   - Validation: `test -f PLANS.md`
     - Expected: exit 0
   - Rollback: delete `PLANS.md`

2. Generate upstream Codex bundle into repo-local `.codex/`
   - Steps
     - Clone (or reuse clone of) upstream repo at a pinned commit.
     - Run upstream Bun CLI conversion targeting `./.codex/` (not `~/.codex`).
   - Validation:
     - `test -d .codex/prompts && test -d .codex/skills`
     - Expected: both dirs exist
   - Rollback: `rm -rf .codex/` (only if explicitly requested)

3. Namespace + patch for Codex usability
   - Steps
     - Prefix all prompts/skills with `ce-`.
     - Rewrite `$skill` and `/prompts:` references accordingly.
     - Replace `${CLAUDE_PLUGIN_ROOT}` references with repo-local paths to `.codex/skills/...`.
   - Validation:
     - `rg -n \"\\$ce-\" .codex/prompts .codex/skills | head`
     - `! rg -n \"\\$[a-z].*\\bskill\\b\" .codex/prompts .codex/skills | rg -n \"\\$ce-\"` (spot check only)
     - `! rg -n \"\\$\\{CLAUDE_PLUGIN_ROOT\\}\" .codex`
   - Rollback: restore from pre-patch snapshot or delete `.codex/` and re-run Milestone 2.

4. Add minimal FPF wrappers
   - Steps
     - Add Context Card for the new “Codex workflow agents” context.
     - Add Task Frame documenting the conversion and acceptance harness.
   - Validation:
     - `test -f docs/fpf/contexts/codex-compound-engineering-v1.md`
     - `test -f docs/fpf/task-frames/TF-codex-compound-engineering-import-v1.md`
   - Rollback: delete the added docs files.

5. Smoke-check (acceptance harness)
   - Steps
     - Verify prompt -> skill linkage exists for every prompt.
     - Verify no `${CLAUDE_PLUGIN_ROOT}` remains.
   - Validation (exact):
     - `bash scripts/ce_smoke_check.sh`
     - Expected: prints `OK` and exits 0
   - Rollback: fix errors or revert `.codex/` to a known-good state.

## Decisions log (why changes)
- Namespace prefix `ce-` prevents collisions with global Codex skills (notably existing `skill-creator`, etc.).
- Project-local `.codex/` avoids overwriting `~/.codex/config.toml` (upstream converter overwrites).
- Minimal FPF wrappers provide auditability without rewriting all upstream content into full Role/Method cards.

## Progress log (ISO-8601 timestamps)
- 2026-02-06: Completed Milestones 1-5: generated project-local `.codex/` bundle, prefixed with `ce-`, removed Claude-only `${CLAUDE_PLUGIN_ROOT}` paths, added FPF wrapper docs, and validated with `bash scripts/ce_smoke_check.sh` (OK).
