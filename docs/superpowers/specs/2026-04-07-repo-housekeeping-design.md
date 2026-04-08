---
title: Repo-Wide Housekeeping — Mega-Rollup Design
date: 2026-04-07
status: draft (awaiting user approval)
author: brainstorming session
supersedes: n/a
follows: docs/reports/audit-001-cleanup.md (2026-01-06)
implementation-location: isolated worktree at ../ACP-inspector-housekeeping/
target-branch: chore/housekeeping-2026-04-07
target-pr: "chore: repo-wide housekeeping and non-codec revivals"
---

## 1. Context

The `ACP-inspector` working copy has three distinct bodies of state at the time
of this design:

1. **An in-flight feature branch materialized in the working tree**: an ACP
   schema upgrade from 0.10.5 to 0.11.3, touching `protocol/src/Acp.Domain.fs`,
   `runtime/src/Acp.Codec.AcpJson.fs`, and related files. This is the user's
   active, uncommitted work. It is **not part of this housekeeping effort** and
   must remain byte-identical before and after.

2. **Cleanup leftovers from the holon restructure** (commit `f4d0756`, 2026-04).
   Files promoted from `sentinel/docs/` to top-level `docs/` without removing
   the originals; `cli/apps/ACP.Inspector/` deleted but not committed; a dead
   `.gitmodules` entry pointing at `core/roadmap/sub-ACP/` for a directory that
   no longer exists; a dead `.gitignore` line for `core/evidence/pbt/`; and one
   stale doc comment in `sentinel/tests/Pbt/EvidenceRunner.fs`.

3. **Stale branches on origin**: 18 branches in total, 7 reachable from
   `origin/master` (safe to delete), 11 not reachable (require individual
   disposition). Most predate the holon restructure and will not cherry-pick
   cleanly onto current `master`.

Prior housekeeping work (`audit-001-cleanup`, 2026-01-06) reported a clean
build, 261 passing tests, six files needing Fantomas, and a three-month-old
intent to split `runtime/src/Acp.Codec.fs` into Types/Json/Decode/Encode/Router
modules. Some of that intent has since landed; the rest needs re-verification
under the current holon layout.

This spec defines a single, isolated, mega-rollup housekeeping pass that
addresses items (2) and (3) without touching item (1).

## 2. The Invariant (Section 1 — Non-Negotiable)

**Before and after the housekeeping session, `git status` run in
`/Users/stas-studio/Developer/ACP-inspector` MUST show byte-identical pending
changes.**

Byte-identical is defined as:

- Same set of modified files (`git diff --name-only`)
- Same set of staged deletions (`git diff --cached --name-only --diff-filter=D`)
- Same set of untracked files (`git ls-files --others --exclude-standard`)
- Same diff content hash for every modified file
  (`git diff | sha256sum` produces the same digest)
- Same HEAD commit (no advance of the primary working copy's HEAD)

This invariant is enforced by:

- Performing **all implementation work in an isolated git worktree** at
  `../ACP-inspector-housekeeping/`, cut from `origin/master`, not from the
  primary working copy.
- Snapshotting `git status` and `git diff | sha256sum` from the primary copy
  at session start and at session end; aborting if they do not match.
- The **bucket-B exclusion set** (§6), which prevents the rollup from writing
  to any file that currently has in-flight content in the primary copy.

Any operation in this spec that violates the invariant is a bug in this spec,
not a policy decision. Fix the spec.

## 3. Goals

1. Collapse the cleanup-leftover state (`sentinel/docs/*` deletions, stale
   `core/` references, dead `ACP.Inspector` project) into committed history on
   `master` via a single PR.
2. Delete merged origin branches that are reachable from `origin/master`.
3. Produce a per-branch disposition report for the 11 unmerged origin branches,
   with explicit sign-off required before any delete.
4. Re-run the audit-001 battery (build warnings, test pass, Fantomas,
   markdownlint, lychee, dead-code scan) and publish a delta report.
5. Establish a reproducible baseline-green state on the current holon layout.
6. Execute all of the above **without touching the user's in-flight schema
   upgrade work** (see §2).

## 4. Non-Goals

- **Splitting large F# files.** `Acp.Codec.AcpJson.fs` grew during the schema
  upgrade and is currently in bucket-B. Splitting is deferred until after the
  schema upgrade lands.
- **Resolving the schema upgrade.** That is a separate feature effort. This
  spec treats the schema upgrade as immutable in-flight state.
- **Rewriting the 11 unmerged branches into merge-ready PRs.** Revival is a
  per-branch judgment call (§11) and may be deferred.
- **Reorganizing `docs/` structure.** We clean stale paths, not re-plan the
  taxonomy.
- **Adding features.** Housekeeping only.
- **Changing dependencies, tool versions, or CI configuration.** Except to
  fix broken references (e.g. dead paths in `.gitignore`).

## 5. Overall Shape — Mega-Rollup

**One branch. One PR. Between 7 and 14 commits, each individually sensible.**

- **Branch**: `chore/housekeeping-2026-04-07`
- **Cut from**: `origin/master` (fresh fetch)
- **Target**: `master`
- **PR title**: `chore: repo-wide housekeeping and non-codec revivals`
- **PR body**: Narrative summary + links to baseline report, audit delta, and
  branch-disposition log.

Why mega-rollup and not N separate PRs:

1. The tracks are not fully independent — the Fantomas sweep's scope depends on
   the bucket-B exclusion, which depends on the branch-disposition outcomes.
2. Review cost on a solo repo is dominated by context-switching, not by commit
   count. One cold-read of a staged PR is cheaper than N cold-reads.
3. CI minutes: one rollup pipeline instead of four.
4. Rollback atomicity: reverting a single PR unwinds the entire housekeeping
   session cleanly.

The rollup is structured so that any single commit can be dropped during review
without disturbing the others (see §7).

## 6. Bucket-B Exclusion Set

**Definition**: the set of files the rollup MUST NOT write. Any file that has
uncommitted content in the primary working copy at the time of the operation is
in bucket B for that operation.

**Snapshot at 2026-04-07 session start** (from
`git -C /Users/stas-studio/Developer/ACP-inspector {diff,diff --cached --diff-filter=D,ls-files --others --exclude-standard} --name-only`):

- **Modified files (35)**: `ACP-inspector.slnx`, `README.md`,
  `cli/apps/ACP.Benchmark/Program.fs`, `cli/apps/ACP.Cli/Commands/BenchmarkCommand.fs`,
  `cli/src/Acp.MessageTag.fs`, `docs/ACP-RFD-TRACKER.md`, `docs/SDK-COMPARISON.md`,
  `docs/architecture/product-structure.md`, `docs/contexts/BC-001-assurance.md`,
  `docs/contexts/BC-004-protocol-evolution.md`, `docs/drr/DRR-005-agent-tool-coordination.md`,
  `docs/fpf/bridges/BR-ACP-002-protocol-to-sentinel.md`, `docs/fpf/contexts/cli-tooling-v1.md`,
  `docs/index.md`, `docs/protocol.md`, `docs/reports/README.md`,
  `docs/reports/fpf-alignment-evaluation-20260106.md`,
  `docs/tasks/TASK-008-schema-pin-and-ci-watch.md`,
  `protocol/src/ACP.fsproj`, `protocol/src/Acp.Domain.fs`, `protocol/src/Acp.Protocol.fs`,
  `runtime/src/ACP.fsproj`, `runtime/src/Acp.Codec.AcpJson.fs`, `runtime/src/Acp.Codec.Types.fs`,
  `runtime/src/Acp.Connection.fs`, `runtime/src/Acp.Contrib.SessionState.fs`,
  `sentinel/src/ACP.fsproj`, `sentinel/tests/Acp.Codec.Tests.fs`,
  `sentinel/tests/Acp.Connection.Tests.fs`, `sentinel/tests/Acp.SessionState.Tests.fs`,
  `sentinel/tests/Acp.Validation.Tests.fs`, `sentinel/tests/Pbt/Generators.fs`,
  `sentinel/tests/Pbt/ProtocolStateMachine.fs`, `sentinel/tests/Pbt/SessionProperties.fs`,
  `sentinel/tests/traces/README.md`

- **Staged deletions (46)**: `cli/apps/ACP.Inspector/ACP.Inspector.fsproj`,
  `cli/apps/ACP.Inspector/Program.fs`, plus all 44 files under `sentinel/docs/`.

- **Untracked (5)**: `docs/architecture/diagram-set-20260319.md`,
  `docs/reports/review-batches-20260319.md`,
  `docs/assets/generated-diagrams/acp-architecture-overview.png`,
  `docs/assets/generated-diagrams/acp-module-map.png`,
  `docs/assets/generated-diagrams/acp-runtime-flow.png`.

**Total bucket-B at session start**: 86 files.

**Staleness policy (two-layer guard)**:

- **Layer 1 — Initial guard**: at session start, snapshot the three lists above
  into `/tmp/housekeeping-2026-04-07/bucket-b.initial.txt`. Use this for early
  planning (what commits to skip, what revivals to attempt).
- **Layer 2 — Per-commit guard**: before **any commit that writes files**
  (Fantomas sweep, revival commits, dead-ref cleanups), recompute
  `bucket-b.live.txt` from the primary repo via the same three commands. Diff
  against `bucket-b.initial.txt`. If a file appeared during the session, append
  it to the active exclusion set and log the diff in
  `docs/reports/housekeeping-2026-04-07/bucket-b-drift.log`. If a planned write
  in the current commit targets a newly-protected file, **skip that specific
  file**, log the skip in the commit message (`SKIP: path — reason`), and
  continue. Do **not** abort the whole rollup on bucket-B drift; only abort on
  the Section 2 full-status mismatch check at session end.

**Bucket-B is a policy, not a file.** Its contents may change during the
session; the policy is stable.

## 7. Commit Structure

| # | Type | Title | Source | Depends on | Notes |
|---|------|-------|--------|-----------|-------|
| 1 | verify | `chore(housekeeping): baseline verification report` | script-generated | — | Produces `docs/reports/housekeeping-2026-04-07/baseline.md`. Runs classified gate (§9). Failing STOP-class checks aborts. |
| 2 | clean | `chore(housekeeping): audit-001 delta report` | script-generated | #1 | Produces `docs/reports/housekeeping-2026-04-07/audit-001-delta.md`. Compares current findings against 2026-01-06 report. |
| 3 | format | `chore(housekeeping): fantomas sweep (non-bucket-B)` | `dotnet fantomas` | #1 | Formats all `.fs`/`.fsi` files not in bucket-B. Re-checks bucket-B before write. Skipped files logged in commit message. |
| 4 | clean | `chore(housekeeping): remove dead core/ references` | manual edit | — | Three changes: `.gitmodules` submodule entry, `.gitignore` line 23, `sentinel/tests/Pbt/EvidenceRunner.fs:8` comment. None of these files are in bucket-B. |
| 5 | clean | `chore(housekeeping): delete merged origin branches` | remote op | #1 | Deletes the 7 merged branches listed in §10.A. Each deletion precedes a corresponding `archive/<branch>` tag push. Reversible via tag. |
| 6 | report | `chore(housekeeping): stale-branch disposition report` | script-generated | #5 | Produces `docs/reports/housekeeping-2026-04-07/branch-disposition.md`. **Does not delete anything.** Each of the 11 unmerged branches gets a recommended outcome (§11) and awaits per-branch sign-off in PR review. |
| 7..N−1 | revive | `fix(<area>): <original branch title> (revived from <branch>)` | cherry-pick / re-apply / extract | #3 | One commit per branch for which the user approved revival during §11. Each such commit re-checks bucket-B live before write. Expected 0-3 commits here. |
| N | close | `chore(housekeeping): close session log` | script-generated | all prior | Produces `docs/reports/housekeeping-2026-04-07/session-log.md`. Final Section-2 invariant check runs in the post-commit hook. |

Total: 7 fixed + 0-3 revival = **7 to 10 commits**. Max ceiling 14 if multiple
extract-specific-files operations are split across commits.

**Dropping a commit during review**: commits #3, #5, and any revival commit
(#7..N−1) are independently droppable. Commits #1, #2, #4, #6, and #N are
effectively free (report-only or small fixed edits) and should not be dropped.

## 8. Operational Sequencing

Exactly 13 steps, performed inside the isolated worktree at
`../ACP-inspector-housekeeping/`. Primary working copy is never written.

1. **Freeze snapshot**: `cd /Users/stas-studio/Developer/ACP-inspector && git status --porcelain=v2 > /tmp/housekeeping-2026-04-07/snapshot-before.txt && git diff | sha256sum > /tmp/housekeeping-2026-04-07/diff-before.sha`.
2. **Compute bucket-B initial**: write the three files to
   `/tmp/housekeeping-2026-04-07/bucket-b.initial.txt`.
3. **Fetch**: `git fetch --prune origin`.
4. **Create worktree**: `git worktree add -b chore/housekeeping-2026-04-07 ../ACP-inspector-housekeeping origin/master`.
5. **Enter worktree**: all subsequent steps run in `../ACP-inspector-housekeeping/`.
6. **Commit #1 (baseline verification)**: run classified gate (§9). If
   STOP-class triggers, write report, abort with clear message. If only
   AUTO-FIX and LOG-AND-CONTINUE triggers, write report and proceed.
7. **Commit #2 (audit delta)**: diff current findings against audit-001.
8. **Commit #3 (Fantomas sweep)**: recompute bucket-B live; run
   `dotnet fantomas --recurse .` with bucket-B files fed to `--exclude`; commit
   result with a skip-log footer.
9. **Commit #4 (dead `core/` refs)**: edit `.gitmodules`, `.gitignore` line 23,
   and `sentinel/tests/Pbt/EvidenceRunner.fs:8` comment. None are in bucket-B;
   verify live anyway.
10. **Commit #5 (merged-branch delete)**: for each merged branch in §10.A, push
    an archive tag `archive/<branch>` whose message contains the branch tip
    SHA, then `git push origin :<branch>`. Commit is a report-only edit to
    `docs/reports/housekeeping-2026-04-07/merged-branches-deleted.md`.
11. **Commit #6 (stale-branch disposition)**: for each unmerged branch in
    §10.B, produce a per-branch report entry with recommended outcome
    (§11). Commit the report. No branch is touched yet — deletion and
    revival are gated on per-branch sign-off during PR review.
12. **Commits #7..N−1 (revivals)**: **conditional**. Only if the user has
    pre-approved revivals during design review (default: zero revivals in
    this spec). Each revival commit re-checks bucket-B live before write.
13. **Commit #N (close)**: produce session log, then run the final Section-2
    invariant check against the primary working copy: if
    `snapshot-before.txt == git status --porcelain=v2` AND
    `diff-before.sha == (git diff | sha256sum)` in the primary copy, the
    session is valid. Otherwise, abort the PR creation and surface the drift.

**PR creation**: only after step 13 succeeds. `gh pr create --base master --head
chore/housekeeping-2026-04-07 --title '...' --body '...'`. Includes explicit
checklist for per-branch revival sign-off.

## 9. Classified Gate Policy (Commit #1)

**Supersedes the earlier "hard STOP on any red" policy.** Housekeeping must not
abort on a condition it exists to repair.

| Check | Command | Class | Action on failure |
|-------|---------|-------|-------------------|
| Build errors | `dotnet build ACP-inspector.slnx --no-restore` | **STOP** | Abort session. Report. |
| Test failures | `dotnet test --no-build --nologo --verbosity quiet` | **STOP** | Abort session. Report. |
| Section-2 drift | `diff snapshot-before.txt <(git -C $PRIMARY status --porcelain=v2)` | **STOP** | Abort session. Surface diff. |
| Fantomas violations, non-bucket-B files | `dotnet fantomas --check <non-B files>` | **AUTO-FIX** | Record in baseline report. Commit #3 will repair. |
| Fantomas violations, bucket-B files | `dotnet fantomas --check <B files>` | **LOG & SKIP** | Record in baseline report. Do not touch (invariant §2). |
| markdownlint warnings | `markdownlint-cli2 '**/*.md'` | **LOG & CONTINUE** | Record. Commit #4 may auto-fix if `--fix` is safe (will assess). |
| lychee broken links | `lychee --offline docs/` | **LOG & CONTINUE** | Record only. Link rot is not a blocker. |
| Dead-code scan | `dotnet build -warnaserror:false 2>&1 \| grep 'FS1182\|FS0020'` | **LOG & CONTINUE** | Record. Resolution is out of scope. |

**Why classified and not binary**:

- The earlier "STOP on any red" policy was self-defeating: housekeeping that
  aborts on Fantomas violations cannot run the Fantomas sweep that fixes
  Fantomas violations.
- The invariant lives in Section 2, not Section 9. The STOP conditions in this
  gate are limited to conditions housekeeping cannot repair (build errors, test
  failures) or conditions that indicate the invariant is already broken
  (Section-2 drift).
- Anything housekeeping can repair is fed forward to the commit that repairs
  it. Anything in bucket-B is logged and skipped.

The baseline report (`docs/reports/housekeeping-2026-04-07/baseline.md`) makes
every skipped and logged condition explicit, so the user can see in PR review
what was not addressed and why.

## 10. Stale-Branch Classification (2026-04-07)

18 non-`HEAD` non-`master` branches on `origin`. Fetched 2026-04-07.

### 10.A — Merged into `origin/master` (7 branches; safe delete after archive tag)

| Branch | Last commit | Archive tag |
|---|---|---|
| `feat/fsharp-tokenizer-eval` | 2025-12-12 | `archive/feat/fsharp-tokenizer-eval` |
| `feature/observability` | 2025-12-15 | `archive/feature/observability` |
| `first-stage` | 2025-12-16 | `archive/first-stage` |
| `feature/acp-draft-support` | 2026-01-06 | `archive/feature/acp-draft-support` |
| `feature/fpf-alignment-phase1-phase2` | 2026-01-06 | `archive/feature/fpf-alignment-phase1-phase2` |
| `feature/schema-pin-ci-watch` | 2026-01-07 | `archive/feature/schema-pin-ci-watch` |
| `followup/registry-hardening` | 2026-01-07 | `archive/followup/registry-hardening` |

All 7 are reachable from `origin/master` via `git branch -r --merged
origin/master`. Deletion is safe — the commits remain in master's history.
Archive tags preserve the branch name for later reference.

### 10.B — Not merged on `origin/master` (11 branches; per-branch disposition required)

**Triage performed 2026-04-07** using `git cherry origin/master <branch>`
(patch-id matching, catches squash-merge artifacts that ancestry-based
`--no-merged` misses) and `git diff --name-only origin/master...origin/<branch>`
cross-referenced against the current bucket-B (§6).

| # | Branch | Cherry status | Files | Lines (+/−) | Disposition | Confidence |
|---|---|---|---|---|---|---|
| 1 | `docs/linear-migration-planning` | 1 unmerged | 1 | +63 | EXTRACT-CANDIDATE | medium |
| 2 | `add-claude-github-actions-1765859631445` | 2 unmerged | 2 | +107 | ARCHIVE | high |
| 3 | `copilot/sub-pr-17` | 4 unmerged + 4 squash-merged | 12 | +1256 / −105 | ARCHIVE | high |
| 4 | `fix/compliance-feedback` | 4 unmerged | 8 | +240 / −190 | ARCHIVE | high |
| 5 | `chore/2025-12-16` | 11 unmerged | 14 | +332 / −47 | ARCHIVE | high |
| 6 | `feat/bounded-contexts-and-codec-refactor` | 4 unmerged | 29 | +9549 / −2979 | ARCHIVE | high |
| 7 | `docs/overview-image-and-task-index` | **0 unmerged + 1 squash-merged** | 6 | +7 / −163 | ARCHIVE-SQUASH-MERGED | certain |
| 8 | `docs/remove-mermaid-diagrams` | **0 unmerged + 1 squash-merged** | 1 | −34 | ARCHIVE-SQUASH-MERGED | certain |
| 9 | `fix/remove-mistaken-image` | **0 unmerged + 1 squash-merged** | 1 | binary | ARCHIVE-SQUASH-MERGED | certain |
| 10 | `codex/make-acp-inspector-valuable` | 18 unmerged | 639 | +42902 / −496484 | ARCHIVE | high |
| 11 | `codex/sub-pr-35` | 10 unmerged | 503 | +10508 / −496114 | ARCHIVE | high |

**Per-branch evidence** (numbered to match the table):

1. **`docs/linear-migration-planning`** — single new file
   `docs/planning/linear-migration.md`. The `docs/planning/` directory does not
   exist on current master, so a clean apply would land without conflict.
   Outcome depends on whether the linear migration is still relevant; review
   the file content before deciding revive vs. archive. **EXTRACT-CANDIDATE
   pending content review.**

2. **`add-claude-github-actions-1765859631445`** — touches
   `.github/workflows/claude-code-review.yml` and `.github/workflows/claude.yml`.
   **Both files already exist on current master.** Whatever this branch tried
   to do has been re-implemented or merged differently. **ARCHIVE.**

3. **`copilot/sub-pr-17`** — 8 commits, 4 already squash-merged into master.
   Surviving 4 commits touch pre-restructure paths (`apps/`, `src/` instead of
   `cli/apps/`, `protocol/src/`). Path translation would be substantial. Half
   the work is already in master anyway. **ARCHIVE.**

4. **`fix/compliance-feedback`** — pre-restructure paths, plus
   `.github/workflows/ci.yml` changes that may or may not still be relevant.
   The CI workflow change is the only piece worth a second look; the `apps/`,
   `src/`, `tests/` edits are dead-on-arrival under the holon layout.
   **ARCHIVE** by default; flag if you want the ci.yml diff extracted for
   review.

5. **`chore/2025-12-16`** — date-named scratch branch with 11 commits across
   `.vscode/settings.json`, the .slnx, pre-restructure source files, and a
   now-renamed `docs/spec/fpf/FPF-Spec.md`. No coherent intent visible.
   **ARCHIVE.**

6. **`feat/bounded-contexts-and-codec-refactor`** — large branch (29 files,
   +9549/−2979) that originally proposed splitting `Acp.Codec.fs` (matching
   audit-001's recommendation) and adding bounded-context docs. Two problems:
   (a) source files use pre-restructure `src/Acp.*.fs` paths and would need
   full translation; (b) **3 of its files intersect current bucket-B**
   (`docs/contexts/BC-001-assurance.md`, `docs/contexts/BC-004-protocol-evolution.md`,
   `docs/reports/README.md`), meaning revival would conflict with your
   in-flight schema work. The codec-split intent has been partially absorbed
   by the holon restructure; the remainder is best deferred until after the
   schema upgrade lands. **ARCHIVE.**

7. **`docs/overview-image-and-task-index`** — `git cherry` reports
   **0 unmerged + 1 squash-merged**. The work is already in master (PR #27).
   Branch is a leftover ref. **ARCHIVE-SQUASH-MERGED.**

8. **`docs/remove-mermaid-diagrams`** — `git cherry` reports
   **0 unmerged + 1 squash-merged**. The work is already in master (PR #28:
   `3a17be9 docs: remove mermaid diagrams from README (#28)`).
   **ARCHIVE-SQUASH-MERGED.**

9. **`fix/remove-mistaken-image`** — `git cherry` reports
   **0 unmerged + 1 squash-merged**. Single binary-file change (image swap).
   The work is already in master. **ARCHIVE-SQUASH-MERGED.**

10. **`codex/make-acp-inspector-valuable`** — codex agent's exploration.
    Touches `.codex/AGENTS.md`, `.codex/config.toml`, prompts, and 631 other
    files. `.codex/` directory does not exist on current master, indicating
    the experiment was never adopted. Massive deletion volume (−496484 lines)
    suggests heavy state inversion. **ARCHIVE.**

11. **`codex/sub-pr-35`** — codex agent's branch related to PR #35 (the holon
    restructure), but `git cherry` shows 0 squash-merged, meaning the actual
    SHA-level work is distinct from `f4d0756`. The branch has
    accidentally-committed `node_modules` (responsible for the −496114
    deletions when diffed against master). Even if conceptually adjacent to
    PR #35, the branch is unfit for revival. **ARCHIVE.**

**Distribution after triage**:

| Outcome | Count | Branches |
|---|---|---|
| ARCHIVE-SQUASH-MERGED | **3** | #7, #8, #9 (work already in master; branches are leftover refs) |
| ARCHIVE | **7** | #2, #3, #4, #5, #6, #10, #11 (work unrecoverable or superseded) |
| EXTRACT-CANDIDATE | **1** | #1 (pending content review) |
| CHERRY-PICK / RE-APPLY / SQUASH-MERGE / REBASE-REVIVE | 0 | — |

**Sign-off implication**: 10 of the 11 are high or certain confidence,
requiring quick yes/no per branch. Only branch #1
(`docs/linear-migration-planning`) needs a content read before sign-off. The
expected sign-off session is short — minutes, not hours.

**Classifications above are evidence-based recommendations.** Commit #6 still
produces the full disposition report (with per-branch commit lists and exact
archive tag messages) at implementation time, but the dispositions themselves
are now pre-decided up to the user's per-branch sign-off. No unmerged branch
is deleted or revived without explicit approval.

## 11. Revival Decision Framework (7-Outcome Taxonomy)

For each unmerged branch, the disposition report (commit #6) assigns one of
seven outcomes based on the answers to four questions:

1. Is the branch's intent still relevant to current `master`?
2. Does the branch touch files currently in bucket-B?
3. Does the branch's diff cleanly cherry-pick onto current `master`?
4. Is the author's original context available, or is this cold archaeology?

| Outcome | When to use | Operation |
|---|---|---|
| **CHERRY-PICK** | Small, targeted fix; clean apply; relevant intent | `git cherry-pick <sha>` into rollup |
| **REBASE-AND-REVIVE** | Multi-commit branch; conflicts resolvable; relevant intent | New branch from `master`, rebase the original, become a sub-PR — **not part of this rollup, deferred** |
| **RE-APPLY FRESH** | Intent still relevant but original doesn't apply cleanly | Re-author the change as a fresh commit in the rollup, citing original branch |
| **SQUASH-MERGE** | Branch is ready but never opened as PR | `git merge --squash origin/<branch>` into rollup |
| **EXTRACT FILES** | Only a subset of changes is still relevant | `git show <sha> -- <path> \| git apply` for selected paths; commit in rollup |
| **ARCHIVE-AND-ABANDON** | Superseded, wrong direction, or cold-archaeology | Push `archive/<branch>` tag; do not delete until per-branch sign-off; record in disposition report |
| **ARCHIVE-SQUASH-MERGED** | `git cherry` shows all branch commits already squash-merged into master (work IS in master under different SHAs) | Same operation as ARCHIVE-AND-ABANDON, but the tag message records the source PR number for forensic clarity. Sign-off is fast-track because no work is at risk. |

The seventh outcome (ARCHIVE-SQUASH-MERGED) was added after the 2026-04-07
triage discovered that 3 of the 11 unmerged branches were already-done work
in disguise: their tip commits are not ancestors of `master` (because
squash-merge breaks ancestry), but `git cherry origin/master <branch>` reports
their patch IDs as already present. Operationally identical to
ARCHIVE-AND-ABANDON, but rationale and risk profile differ.

**Actual 2026-04-07 distribution for the 11 unmerged branches** (from §10.B
triage):

| Outcome | Count |
|---|---|
| ARCHIVE-SQUASH-MERGED | 3 |
| ARCHIVE-AND-ABANDON | 7 |
| EXTRACT-CANDIDATE (pending content review) | 1 |
| CHERRY-PICK / RE-APPLY / SQUASH-MERGE / REBASE-REVIVE | 0 |

**Default for this spec**: zero revivals attempted in the rollup. Commit #6
produces the full disposition report at implementation time but the
classifications are pre-decided. This spec ships with zero revivals
pre-approved, meaning the default rollup has 7 commits (1–6 plus close).
The single EXTRACT-CANDIDATE (`docs/linear-migration-planning`) becomes a
revival commit only if the user reviews the file content before
implementation and decides to keep it.

## 12. Archive Tag Pattern

Every deleted branch (merged or unmerged) is preceded by an archive tag push:

```
git tag -a archive/<branch-name> <branch-tip-sha> \
  -m "Archived on 2026-04-07. Original remote ref: origin/<branch-name>. Reason: <short>."
git push origin archive/<branch-name>
```

Tag name matches the original branch name (with slashes preserved — tags
support `archive/feature/foo`). The tag message includes:

- Archival date (absolute, not relative)
- Original remote ref at the time of archival
- Reason (one-line classification from the disposition report)
- Last commit author and committer date (for forensic reference)

**Restore procedure** (for future reference): `git checkout -b <branch>
archive/<branch> && git push -u origin <branch>`. No history is lost.

**Retention**: tags are not auto-pruned. Review in a future cleanup pass (e.g.
2026-10 or later).

## 13. Success Criteria (Check Before Opening PR)

All of the following must be true at the end of the session, verified by the
close script in commit #N:

- [ ] Primary working copy `git status --porcelain=v2` is byte-identical to
      session-start snapshot.
- [ ] Primary working copy `git diff | sha256sum` is byte-identical to
      session-start digest.
- [ ] Primary working copy `HEAD` SHA is unchanged.
- [ ] Worktree `git log --oneline origin/master..HEAD` shows 7–10 commits (or
      up to 14 if extract-files splits).
- [ ] `dotnet build ACP-inspector.slnx --no-restore` succeeds with 0 warnings
      in the worktree.
- [ ] `dotnet test --no-build --nologo --verbosity quiet` succeeds with all
      tests passing in the worktree.
- [ ] `dotnet fantomas --check .` succeeds (all non-bucket-B files are
      Fantomas-clean).
- [ ] `markdownlint-cli2 '**/*.md'` status is at most equal to baseline
      (no new warnings introduced).
- [ ] Every commit message in the rollup follows the conventional-commit
      format (`chore(housekeeping):`, `fix(<area>):`, etc.).
- [ ] All 7 merged-branch archive tags exist on `origin`.
- [ ] The disposition report has an entry for every one of the 11 unmerged
      branches.
- [ ] No unmerged branch has been deleted in this session.
- [ ] `docs/reports/housekeeping-2026-04-07/` exists and contains:
      `baseline.md`, `audit-001-delta.md`, `merged-branches-deleted.md`,
      `branch-disposition.md`, `session-log.md`, and (if any skips occurred)
      `bucket-b-drift.log`.
- [ ] The PR body links to all report artifacts and includes per-branch
      sign-off checkboxes for the 11 unmerged branches.

Failure of any item aborts PR creation and surfaces a clear error. The
worktree remains for inspection.

## 14. Rollback Plan

**If something goes wrong mid-session** (before PR merge):

1. The primary working copy is untouched by construction. There is nothing to
   undo there.
2. The worktree at `../ACP-inspector-housekeeping/` can be deleted via
   `git worktree remove ../ACP-inspector-housekeeping --force`.
3. The branch `chore/housekeeping-2026-04-07` can be deleted locally
   (`git branch -D`) and has not been pushed yet unless explicitly pushed.
4. The 7 merged-branch deletions (commit #5) execute against `origin`. If
   this step has run, the archive tags are already in place — restore with
   `git checkout -b <branch> archive/<branch> && git push -u origin <branch>`.
5. No unmerged branch is ever deleted in this session, so there is nothing
   to restore for the 11 unmerged branches.

**If PR is opened and merged, then discovered bad**:

1. Revert the PR with `gh pr revert` or `git revert -m 1 <merge-sha>`.
2. Restore any over-eagerly deleted branches from their archive tags.
3. Recovery is bounded by the rollup being a single, revertible merge
   commit — that's the whole point of §5.

## 15. Risks and Open Questions

### Risk: bucket-B drift during the session

**Mitigation**: two-layer guard (§6). Before any write, recompute live
bucket-B and skip newly-protected files.

**Residual risk**: the session-end Section-2 check catches any file the user
touches during the session. If drift is detected, the rollup is aborted (PR
not created). The worktree is preserved for inspection.

### Risk: Fantomas sweep produces a large diff

**Mitigation**: commit #3 is droppable (§7). If the diff is unreviewable, drop
the commit and handle formatting in a dedicated follow-up PR.

### Risk: Schema upgrade work conflicts with the dead-`core/` cleanups

**Assessment**: `.gitmodules`, `.gitignore` line 23, and
`sentinel/tests/Pbt/EvidenceRunner.fs:8` (comment only) — none are in the
current bucket-B. Verified at design time. Per-commit guard verifies again at
write time.

### Risk: Unmerged branches contain work the user has forgotten

**Mitigation**: no unmerged branch is deleted in this session. The disposition
report (commit #6) is the sign-off surface, and the user can promote any
recommendation to ARCHIVE-AND-ABANDON → RE-APPLY or similar during PR review.

### Open question: markdownlint `--fix` auto-repair

Deferred: commit #4 (dead `core/` refs) is the only commit that currently
edits markdown, and only as a side-effect. If commit #1's baseline report
reveals many `markdownlint` warnings on non-bucket-B docs, we can add a
dedicated `chore(housekeeping): markdownlint auto-fix` commit between `#4` and
`#5`, but only if `markdownlint-cli2 --fix` output diff is small and reviewable.
Assessment happens at step 6 (baseline gate), not now.

### Open question: lychee broken-link remediation

Deferred: link rot is flagged in the baseline report but not fixed in this
pass. If the count is high enough, a dedicated follow-up pass is warranted.

## 16. References

- Previous audit: `docs/reports/audit-001-cleanup.md` (2026-01-06)
- Recent restructure: commit `f4d0756` — "Restructure repo into holons and align tooling (#35)"
- Registry hardening follow-ups: `bb57747`, `cc15c39`, `ea8514c` (commits merged 2026-01-07)
- Fantomas config: `.editorconfig` at repo root
- Pre-commit hooks: `.pre-commit-config.yaml` at repo root (if present)

## 17. Appendix A — Full Bucket-B File List (2026-04-07)

### A.1 Modified (35 files)

```
ACP-inspector.slnx
README.md
cli/apps/ACP.Benchmark/Program.fs
cli/apps/ACP.Cli/Commands/BenchmarkCommand.fs
cli/src/Acp.MessageTag.fs
docs/ACP-RFD-TRACKER.md
docs/SDK-COMPARISON.md
docs/architecture/product-structure.md
docs/contexts/BC-001-assurance.md
docs/contexts/BC-004-protocol-evolution.md
docs/drr/DRR-005-agent-tool-coordination.md
docs/fpf/bridges/BR-ACP-002-protocol-to-sentinel.md
docs/fpf/contexts/cli-tooling-v1.md
docs/index.md
docs/protocol.md
docs/reports/README.md
docs/reports/fpf-alignment-evaluation-20260106.md
docs/tasks/TASK-008-schema-pin-and-ci-watch.md
protocol/src/ACP.fsproj
protocol/src/Acp.Domain.fs
protocol/src/Acp.Protocol.fs
runtime/src/ACP.fsproj
runtime/src/Acp.Codec.AcpJson.fs
runtime/src/Acp.Codec.Types.fs
runtime/src/Acp.Connection.fs
runtime/src/Acp.Contrib.SessionState.fs
sentinel/src/ACP.fsproj
sentinel/tests/Acp.Codec.Tests.fs
sentinel/tests/Acp.Connection.Tests.fs
sentinel/tests/Acp.SessionState.Tests.fs
sentinel/tests/Acp.Validation.Tests.fs
sentinel/tests/Pbt/Generators.fs
sentinel/tests/Pbt/ProtocolStateMachine.fs
sentinel/tests/Pbt/SessionProperties.fs
sentinel/tests/traces/README.md
```

### A.2 Staged deletions (46 files)

```
cli/apps/ACP.Inspector/ACP.Inspector.fsproj
cli/apps/ACP.Inspector/Program.fs
sentinel/docs/ACP-RFD-TRACKER.md
sentinel/docs/REPO-ASSESSMENT-INDEX.md
sentinel/docs/REPO-EVALUATION-FRAMEWORK.md
sentinel/docs/REPO-EVALUATION-RESULTS.md
sentinel/docs/REPO-IMPROVEMENT-IDEAS.md
sentinel/docs/SDK-COMPARISON.md
sentinel/docs/architecture/product-structure.md
sentinel/docs/assets/acp-inspector-overview.jpg
sentinel/docs/contexts/BC-001-assurance.md
sentinel/docs/contexts/BC-002-semantic-alignment.md
sentinel/docs/contexts/BC-003-capability-verification.md
sentinel/docs/contexts/BC-004-protocol-evolution.md
sentinel/docs/contexts/README.md
sentinel/docs/drr/DRR-001-agent-output-trustworthiness.md
sentinel/docs/drr/DRR-002-cross-agent-semantic-alignment.md
sentinel/docs/drr/DRR-003-capability-claim-verification.md
sentinel/docs/drr/DRR-004-protocol-evolution-stability.md
sentinel/docs/drr/DRR-005-agent-tool-coordination.md
sentinel/docs/drr/README.md
sentinel/docs/fpf/bridges/BR-ACP-001-protocol-to-runtime.md
sentinel/docs/fpf/bridges/BR-ACP-002-protocol-to-sentinel.md
sentinel/docs/fpf/bridges/BR-ACP-003-runtime-to-cli.md
sentinel/docs/fpf/bridges/BR-ACP-004-sentinel-to-cli.md
sentinel/docs/fpf/contexts/cli-tooling-v1.md
sentinel/docs/fpf/contexts/protocol-core-v1.md
sentinel/docs/fpf/contexts/runtime-sdk-v1.md
sentinel/docs/fpf/contexts/sentinel-validation-v1.md
sentinel/docs/fpf/drr/DRR-006-repo-split-4-holons.md
sentinel/docs/index.md
sentinel/docs/protocol.md
sentinel/docs/reports/README.md
sentinel/docs/reports/audit-001-cleanup.md
sentinel/docs/reports/fpf-alignment-evaluation-20260106.md
sentinel/docs/tasks/BACKLOG-tech-debt.md
sentinel/docs/tasks/README.md
sentinel/docs/tasks/TASK-002-fpf-alignment-improvements.md
sentinel/docs/tasks/TASK-003-fpf-advanced-features.md
sentinel/docs/tasks/TASK-004-acp-meta-passthrough.md
sentinel/docs/tasks/TASK-005-proxy-chains-support.md
sentinel/docs/tasks/TASK-006-telemetry-export-panel.md
sentinel/docs/tasks/TASK-007-agent-registry.md
sentinel/docs/tasks/TASK-008-schema-pin-and-ci-watch.md
sentinel/docs/tooling/acp-inspector.md
sentinel/docs/tooling/coding-standards.md
```

### A.3 Untracked (5 files)

```
docs/architecture/diagram-set-20260319.md
docs/assets/generated-diagrams/acp-architecture-overview.png
docs/assets/generated-diagrams/acp-module-map.png
docs/assets/generated-diagrams/acp-runtime-flow.png
docs/reports/review-batches-20260319.md
```

## 18. Appendix B — Branches on Origin (2026-04-07)

Fetched with `git fetch --prune origin` at 2026-04-07. Classified with
`git branch -r --merged origin/master`.

### B.1 Merged (7)

```
feat/fsharp-tokenizer-eval                 2025-12-12
feature/observability                      2025-12-15
first-stage                                2025-12-16
feature/acp-draft-support                  2026-01-06
feature/fpf-alignment-phase1-phase2        2026-01-06
feature/schema-pin-ci-watch                2026-01-07
followup/registry-hardening                2026-01-07
```

### B.2 Unmerged (11)

```
docs/linear-migration-planning             2025-12-08
add-claude-github-actions-1765859631445    2025-12-15
copilot/sub-pr-17                          2025-12-15
fix/compliance-feedback                    2025-12-15
chore/2025-12-16                           2025-12-16
feat/bounded-contexts-and-codec-refactor   2026-01-05
docs/overview-image-and-task-index         2026-01-06
docs/remove-mermaid-diagrams               2026-01-06
fix/remove-mistaken-image                  2026-01-06
codex/make-acp-inspector-valuable          2026-02-06
codex/sub-pr-35                            2026-02-07
```

---

**End of design document.** Awaiting user approval before commit.
