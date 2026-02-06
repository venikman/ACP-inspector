# Context Card (FPF)

## Header

- ContextId: urn:acp-inspector:context:codex-compound-engineering:v1
- Domain family (if any): engineering workflow (Codex prompts/skills)
- Edition / date: v1 / 2026-02-06
- Time stance: run
- Owner / maintainer: ACP Inspector maintainers

## Purpose (why does this context exist?)

- Intended use: Provide project-local Codex prompts/skills (prefixed `ce-`) imported from EveryInc's Compound Engineering plugin to support planning, work execution, review, and knowledge capture while building ACP Inspector.
- Non-goals: Any normative ACP protocol semantics, runtime behavior, or production code changes. This context is developer-assistance only.

## Scope boundary (semantic boundary)

- In-scope:
  - Project-local Codex bundle under `./.codex/` (prompts + skills).
  - Conversion + validation scripts under `./scripts/` used to generate/verify the bundle.
- Out-of-scope:
  - ACP Inspector protocol/runtime/sentinel/CLI implementation.
  - Global Codex home configuration (`~/.codex/*`).
- Boundary tests (how to tell):
  - If the change affects shipping packages or runtime behavior, it is out-of-scope.
  - If the change modifies `~/.codex/config.toml` or global Codex skills, it is out-of-scope.

## Primary sources (authoritative)

- Upstream: EveryInc/compound-engineering-plugin (Compound Engineering plugin)
  - Version: 2.30.0
  - Commit: e4ff6a874c1330e3e6b672db14e6d0b8847910de
- Local conversion + checks:
  - `scripts/ce_prefix_codex_bundle.py`
  - `scripts/ce_smoke_check.sh`
- FPF conceptual reference (external):
  - `/Users/stas-studio/Downloads/FPF-Spec (12).md`

## Key terms (seed list)

- `ce-` prefix: namespace applied to all imported prompts and skills to prevent collisions with global Codex skills.
- prompt: a Codex “slash command” entry under `./.codex/prompts/`.
- skill: a Codex instruction bundle under `./.codex/skills/<name>/SKILL.md`.
- smoke-check: repo-local acceptance harness verifying linkage and removal of Claude-only paths.

## Invariants (context-true)

- Imported prompts/skills are namespaced with `ce-`.
- No references to `${CLAUDE_PLUGIN_ROOT}` remain; paths are repo-relative under `./.codex/skills/...`.
- Validation is available as a single command: `bash scripts/ce_smoke_check.sh`.

## Adjacent contexts (do not merge)

- urn:acp-inspector:context:protocol-core:v1
- urn:acp-inspector:context:runtime-sdk:v1
- urn:acp-inspector:context:sentinel-validation:v1
- urn:acp-inspector:context:cli-tooling:v1

## Open questions / risks

- Risk 1: Upstream plugin content is not .NET/F#-specific; some commands are Rails/iOS oriented.
- Risk 2: Upstream updates may require re-import; prefer pinned commits and keep the smoke-check green.

## Changelog

- 2026-02-06: created
