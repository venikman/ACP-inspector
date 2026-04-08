# Remote Branch Disposition

- Date: 2026-04-07
- Basis: comparison against `origin/master` at `e6a53cee53cecdf606d45638d07b10e463642ab5`
- Policy in this pass: report only, no remote deletions or rewrites

## Summary

- Merged into `origin/master`: `7` branches
- Not merged into `origin/master`: `11` branches
- Action taken in this pass: none

## Merged Branches

These branches are reachable from `origin/master` and are safe deletion candidates for a later, explicitly approved follow-up.

| Branch | Last branch tip | Recommendation |
| --- | --- | --- |
| `origin/feat/fsharp-tokenizer-eval` | `2025-12-12 / e039a4c / Address PR review feedback` | Archive tag, then delete in a future branch-cleanup pass |
| `origin/feature/acp-draft-support` | `2026-01-06 / 54d542d / Hyphenate priority adjectives` | Archive tag, then delete in a future branch-cleanup pass |
| `origin/feature/fpf-alignment-phase1-phase2` | `2026-01-06 / 21739a9 / feat(evidence-graph): allow grounding nodes to specify assurance` | Archive tag, then delete in a future branch-cleanup pass |
| `origin/feature/observability` | `2025-12-15 / e79b87c / Add OpenTelemetry runtime instrumentation and telemetry package` | Archive tag, then delete in a future branch-cleanup pass |
| `origin/feature/schema-pin-ci-watch` | `2026-01-07 / 5d61740 / Add draft proxy chains, telemetry hints, and registry tooling` | Archive tag, then delete in a future branch-cleanup pass |
| `origin/first-stage` | `2025-12-16 / 49c0218 / fix: revert to net10.0 and fix activitySource visibility` | Archive tag, then delete in a future branch-cleanup pass |
| `origin/followup/registry-hardening` | `2026-01-07 / bb57747 / Avoid double-read and TOCTOU in registry loading` | Archive tag, then delete in a future branch-cleanup pass |

## Unmerged Branches

These branches are not touched in this pass. Recommendations are intentionally conservative and based on reachability, branch age, commit subject, and changed-file footprint.

| Branch | Ahead / behind vs `origin/master` | Scope signal | Recommendation |
| --- | --- | --- | --- |
| `origin/add-claude-github-actions-1765859631445` | `ahead 2`, `behind 74` | Two GitHub workflow files only | Review as a workflow-only extract; otherwise archive |
| `origin/chore/2025-12-16` | `ahead 11`, `behind 74` | Pre-holon paths such as `apps/ACP.Inspector` and `docs/spec/...` | Archive unless a specific missing doc or setting is identified |
| `origin/codex/make-acp-inspector-valuable` | `ahead 18`, `behind 9` | Large divergent Codex/meta branch, 639 changed files | Do not merge blindly; reopen only as a new scoped initiative |
| `origin/codex/sub-pr-35` | `ahead 10`, `behind 9` | Large mixed branch, 503 changed files including vendored output | Archive or re-cut narrowly from current `master` |
| `origin/copilot/sub-pr-17` | `ahead 8`, `behind 81` | Pre-holon `apps/` and `src/` layout | Archive |
| `origin/docs/linear-migration-planning` | `ahead 1`, `behind 119` | Single doc: `docs/planning/linear-migration.md` | Manual extract candidate |
| `origin/docs/overview-image-and-task-index` | `ahead 1`, `behind 29` | Small docs/image patch touching README and task index files | Manual extract candidate |
| `origin/docs/remove-mermaid-diagrams` | `ahead 1`, `behind 27` | README-only docs patch | Review for relevance, then cherry-pick or archive |
| `origin/feat/bounded-contexts-and-codec-refactor` | `ahead 4`, `behind 42` | 29 files across bounded-context docs and runtime work | Deep manual review before any extraction |
| `origin/fix/compliance-feedback` | `ahead 4`, `behind 75` | CI and legacy-layout path fixes | Archive; port any still-needed CI intent manually |
| `origin/fix/remove-mistaken-image` | `ahead 1`, `behind 28` | Single image replacement | Manual extract candidate |

## Follow-Up Order

If a separate remote-branch cleanup session is approved later, the lowest-risk order is:

1. Delete the 7 merged branches after creating archive tags.
2. Review the 4 small extract candidates:
   - `origin/docs/linear-migration-planning`
   - `origin/docs/overview-image-and-task-index`
   - `origin/docs/remove-mermaid-diagrams`
   - `origin/fix/remove-mistaken-image`
3. Decide whether the 4 large divergent branches have any salvageable intent:
   - `origin/chore/2025-12-16`
   - `origin/codex/make-acp-inspector-valuable`
   - `origin/codex/sub-pr-35`
   - `origin/feat/bounded-contexts-and-codec-refactor`
4. Archive the remaining legacy workflow or compliance branches if no owner claims them.

## Decision

This housekeeping PR intentionally leaves every remote branch untouched. The deliverable here is the classification itself, not the deletion.
