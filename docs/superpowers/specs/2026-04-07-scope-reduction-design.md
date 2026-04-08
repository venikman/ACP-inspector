---
title: Scope Reduction — Remove cli/ Subsystem
date: 2026-04-07
status: draft (awaiting user approval)
author: brainstorming session
supersedes: docs/superpowers/specs/2026-04-07-repo-housekeeping-design.md
implementation-location: isolated worktree at ../ACP-inspector-scope-reduction/
target-branch: chore/scope-reduction-2026-04-07
target-pr: "chore(scope): remove cli/ subsystem"
---

## 1. Context

Earlier on 2026-04-07 a repo-wide housekeeping pass was scoped, designed,
and partially executed. The pass STOPPED at Task 1 (baseline gate) when
`dotnet build ACP-inspector.slnx` failed with `Ambiguous project name
'ACP.Inspector'`. Investigation revealed two things both claiming the
name `ACP.Inspector` inside the slnx-level project resolver:

- The literal project file `cli/apps/ACP.Inspector/ACP.Inspector.fsproj`
- The assembly `ACP.Inspector.dll` produced by `sentinel/src/ACP.fsproj`
  (which sets `AssemblyName=ACP.Inspector` internally)

The user's in-flight bucket-B work in the primary copy contains a slnx
modification removing the `cli/apps/ACP.Inspector/ACP.Inspector.fsproj`
entry, which fixes the ambiguity locally but has not been committed.

Rather than execute the full housekeeping plan to clean up leftover state
around a CLI subsystem the user no longer needs, the user has chosen to
**reduce the project's scope** by removing the entire `cli/` directory
and reorienting ACP-inspector around its three F# core libraries plus
the documentation tree. This spec defines that scope reduction.

## 2. Goal

Remove the `cli/` subsystem (apps + src + examples + benchmarks, ~59
files) and update the build, CI, and pre-commit infrastructure that
references it. Resolve the slnx-level `ACP.Inspector` ambiguity as a
beneficial side effect. Preserve the user's other 81 in-flight bucket-B
files (the protocol/runtime/sentinel schema-upgrade work and
documentation modifications).

## 3. Non-Goals

- **Sweeping stale CLI references out of `docs/`**. Several documents
  (`docs/SDK-COMPARISON.md`, `docs/architecture/product-structure.md`,
  `docs/fpf/contexts/cli-tooling-v1.md`, `docs/tooling/acp-inspector.md`,
  `docs/REPO-IMPROVEMENT-IDEAS.md`, `docs/reports/review-batches-20260319.md`)
  contain references to the CLI. They are intentionally left untouched.
  Touching them would balloon the PR scope and conflict with bucket-B
  work in the same files.
- **Rewriting git history.** We use `git rm`, not `git filter-repo`.
  The `cli/` files remain recoverable via `git checkout <prev-sha> -- cli/`.
- **Modifying `protocol/`, `runtime/`, or `sentinel/` source.** The F#
  core stays unchanged.
- **Touching the user's other 81 in-flight bucket-B files.** Only the
  five files in `cli/` and the slnx are within scope.
- **Reviving any of the 11 unmerged origin branches** that the
  housekeeping spec catalogued in §10.B. That work is deferred to a
  follow-up session if the user wants it.
- **Reorganizing the `docs/` taxonomy.**
- **Adding features.**

## 4. What Stays vs. What Goes

### Stays

- `protocol/` — F# protocol library (8 files)
- `runtime/` — F# runtime library (22 files)
- `sentinel/` — F# sentinel/validation library (77 files)
- `docs/` — entire documentation tree (52 files), including stale CLI
  references in non-load-bearing docs (see §3 non-goals)
- `README.md` — kept, but with the CLI usage section stripped (§5.5)
- `ACP-inspector.slnx` — kept, but the `<Folder Name="/cli/">` block is
  removed (§5.2)
- `.github/workflows/` — all four workflow files kept; `ci.yml` is
  edited (§5.3)
- `.pre-commit-config.yaml` — kept, edited (§5.4)
- All other top-level config: `global.json`, `lychee.toml`, `.gitignore`,
  `.editorconfig`

### Goes

- `cli/` — entire directory deleted via `git rm -r cli/`. ~59 files,
  comprising:
  - `cli/apps/ACP.Cli/` — the inspect/validate/replay/analyze/benchmark CLI
  - `cli/apps/ACP.Benchmark/` — standalone benchmark harness
  - `cli/apps/ACP.Inspector/` — the dead project (already staged for
    deletion in primary copy bucket-B; subsumed by this removal)
  - `cli/src/Acp.MessageTag.fs` — shared CLI source file
  - `cli/examples/cli-demo/` — demo session and runner
  - `cli/benchmarks/` — benchmark results and READMEs

### Bundled with the same commit (user-approved)

- `docs/superpowers/specs/2026-04-07-repo-housekeeping-design.md` — the
  abandoned housekeeping spec from earlier today (committed at `68802b8`,
  refined at `1ace255`)
- `docs/superpowers/plans/2026-04-07-repo-housekeeping-plan.md` — the
  abandoned housekeeping plan from earlier today (committed at `15f5893`)

These two files describe a process that has been superseded by this
spec. They are removed from master in the same commit. Their content
remains available via `git show 15f5893:docs/superpowers/...`. See §5.6
for the operation.

## 5. File-Level Changes

### 5.1 `git rm -r cli/`

Single git operation. Removes ~59 files. Reversible via
`git checkout <prev-sha> -- cli/`.

### 5.2 `ACP-inspector.slnx`

Remove the entire folder block:

```xml
  <Folder Name="/cli/">
    <Project Path="cli/apps/ACP.Cli/ACP.Cli.fsproj" />
    <Project Path="cli/apps/ACP.Benchmark/ACP.Benchmark.fsproj" />
    <Project Path="cli/apps/ACP.Inspector/ACP.Inspector.fsproj" />
  </Folder>
```

The remaining slnx contains three folders (`/protocol/`, `/runtime/`,
`/sentinel/`) and seven projects: `protocol/src/ACP.fsproj`,
`runtime/src/ACP.fsproj`, `sentinel/src/ACP.fsproj`,
`sentinel/tests/ACP.Tests.fsproj`,
`sentinel/tests/Epistemology.Harness/Epistemology.Harness.fsproj`,
`sentinel/tests/SDK.Harness/SDK.Harness.fsproj`, and
`sentinel/tests/Validation.Harness/Validation.Harness.fsproj`.

### 5.3 `.github/workflows/ci.yml`

Three changes:

- **Line 22 (lychee step)**: drop `'cli/examples/**/*.md'` from the args.
  Final args: `--config lychee.toml 'README.md' 'docs/**/*.md' 'runtime/examples/**/*.md'`
- **Line 33 (fantomas step)**: drop `cli/src cli/apps` from the
  fantomas argument list. Final command:
  `dotnet fantomas protocol/src runtime/src sentinel/src sentinel/tests --check`
- **Line 74 (build step)**: delete the entire step that runs
  `dotnet build cli/apps/ACP.Cli/ACP.Cli.fsproj -c Release`. The slnx-level
  build (run elsewhere in the workflow) covers what's left.

### 5.4 `.pre-commit-config.yaml`

Single change at line 6:

- Drop `cli/apps cli/src` from the fantomas hook command. Final:
  `entry: bash -c 'dotnet tool restore > /dev/null 2>&1 && dotnet fantomas --check protocol/src runtime/src sentinel/src sentinel/tests'`

### 5.5 `README.md`

Strip the CLI usage section. Per `grep -n cli/ README.md`, lines roughly
30-90 are CLI command examples (`dotnet build cli/apps/ACP.Cli/...`,
`dotnet run --project cli/apps/ACP.Cli -- ...`), plus lines 121-135 are
formatter command-lines and an example block. Specific cuts:

- Delete the "How to use" / "Usage" section that documents
  `inspect`, `validate`, `replay`, `analyze`, `benchmark` commands.
- Delete the formatter command-line examples that pass `cli/src cli/apps`
  to fantomas.
- Delete the "Try the CLI" example block that references
  `cli/examples/cli-demo/demo-session.jsonl`.
- Keep the project description, library overview, and any build
  instructions for `protocol/`, `runtime/`, `sentinel/`.

The exact line numbers are pinned at implementation time (the
implementation plan re-greps to avoid drift).

### 5.6 Delete the superseded housekeeping spec/plan

```
git rm docs/superpowers/specs/2026-04-07-repo-housekeeping-design.md
git rm docs/superpowers/plans/2026-04-07-repo-housekeeping-plan.md
```

Both files were committed earlier today as part of the housekeeping
brainstorming session. The content remains accessible via
`git show 15f5893:docs/superpowers/...` for forensic reference.

## 6. Bucket-B Reconciliation

The user's in-flight bucket-B (86 files) is mostly preserved. The five
intersection points are handled as follows:

| Bucket-B file | This pass writes? | Outcome |
|---|---|---|
| `ACP-inspector.slnx` | Yes (more aggressively than user's mod) | Rebase: user accepts ours; their 1-line mod is a strict subset |
| `cli/apps/ACP.Benchmark/Program.fs` | Deleted | User's edits become orphaned; user `git rm`s on rebase |
| `cli/apps/ACP.Cli/Commands/BenchmarkCommand.fs` | Deleted | Same |
| `cli/src/Acp.MessageTag.fs` | Deleted | Same |
| `cli/apps/ACP.Inspector/{ACP.Inspector.fsproj,Program.fs}` (staged deletions) | Deleted | User's staged deletions become redundant (file no longer exists in either state); cleared on rebase |

The other **81 bucket-B files** (protocol/runtime/sentinel source +
docs modifications + untracked diagrams) are not touched by this pass.
The user can `git pull --rebase` cleanly for those files; the only
manual reconciliation is `git rm` on the 3 cli/ modifications and
`git checkout` on the slnx.

**Post-merge user steps:**

1. `cd /Users/stas-studio/Developer/ACP-inspector`
2. `git rm cli/apps/ACP.Benchmark/Program.fs cli/apps/ACP.Cli/Commands/BenchmarkCommand.fs cli/src/Acp.MessageTag.fs` — drop the orphaned modifications
3. `git checkout HEAD ACP-inspector.slnx` — discard the user's slnx mod (it's a subset of master's)
4. `git pull --rebase` — pull in the scope-reduction commit; should now apply cleanly
5. The user's remaining 81 bucket-B files are intact

## 7. Approach

- **Worktree**: fresh worktree at `../ACP-inspector-scope-reduction/`
  cut from `origin/master`. The existing
  `../ACP-inspector-housekeeping/` worktree (created earlier today for
  the abandoned housekeeping pass) is removed at the end as cleanup.
- **Branch**: `chore/scope-reduction-2026-04-07`
- **Commit structure**: single atomic commit titled
  `chore(scope): remove cli/ subsystem`. Diff includes ~60 file
  deletions (cli/ + the two superseded docs/superpowers/ files) and 5
  file edits (slnx, ci.yml, pre-commit, README, plus any small
  follow-on). One coherent change, one revert point.
- **Verification before commit** (run inside the worktree):
  1. `dotnet build ACP-inspector.slnx` — must succeed. This is the
     primary check; if the slnx-level resolver still complains, the
     scope reduction has missed something.
  2. `dotnet build sentinel/tests/ACP.Tests.fsproj` — per-project sanity
     check (this is what worked before scope reduction).
  3. `dotnet test sentinel/tests/ACP.Tests.fsproj --no-build` — confirm
     tests pass.
  4. Optional: `dotnet fantomas --check protocol/src runtime/src sentinel/src sentinel/tests`
     — make sure the formatter agrees with the kept files.
- **PR vs direct push**: push the branch and open a PR via `gh pr create`.
  CI runs against the workflow changes (verifying we didn't break the
  workflow file itself). The user can sanity-check the diff before
  merging. User can override and direct-push if PR ceremony is unwanted.

## 8. Risks and Rollback

### Risk: build still broken after slnx edit

**Why it might happen**: there could be ANOTHER `ACP.Inspector` reference
elsewhere (a `Directory.Build.props`, a workspace file, a hidden assembly
attribute) that we haven't found.

**Mitigation**: §7 verification step 1 catches this. If it fails, the
worktree is preserved for inspection and the commit is not made.

**Rollback**: `git worktree remove --force ../ACP-inspector-scope-reduction/`,
no commits exist yet, no impact on master.

### Risk: deleting cli/ breaks something we don't know about

**Why it might happen**: an unknown consumer of cli/ — perhaps a script,
external doc, or downstream tool — might depend on the cli/ tree.

**Mitigation**: the `grep -r 'cli/'` performed during brainstorming
found 20 files, all of which are accounted for in §5. Anything we
missed is either internal-to-cli (gone with the directory) or
non-load-bearing docs (left intentionally as snapshots).

**Rollback**: revert the merge commit. `git checkout <merge-sha>~1 -- cli/`
brings cli/ back. The PR's atomic structure makes this clean.

### Risk: user's in-flight schema-upgrade work has cli/ touchpoints we miss

**Why it might happen**: the schema upgrade may have edits in cli/ that
are conceptually meaningful — e.g., the user was rewriting BenchmarkCommand
to use the new ACP 0.11.3 frame format and that work captures real
design progress.

**User decision**: the user has explicitly approved dropping the cli/
in-flight modifications. This risk is acknowledged and accepted.

**Rollback**: if the user later wants to recover the in-flight cli/
edits, `git diff` against the snapshot at `/tmp/housekeeping-2026-04-07/snapshot-before.txt`
captures their state at session start. The orphaned edits remain in
the user's primary working copy until they explicitly `git rm` them
post-merge.

### Risk: docs/ stale references confuse future readers

**Why it might happen**: ~7 docs/ files reference cli/ commands, the
inspector, or CLI tooling concepts. After this pass they describe a
subsystem that no longer exists.

**Mitigation**: this is an intentional non-goal (§3). The PR description
calls it out explicitly so reviewers and future readers know the
references are stale.

**Future work**: a follow-up pass can sweep stale references out of
docs/ once the user's bucket-B schema-upgrade work in those same files
has been committed. Doing it now would conflict with bucket-B.

## 9. Success Criteria

All of the following must be true at PR open time:

- [ ] `cli/` directory is gone from the worktree
- [ ] `ACP-inspector.slnx` no longer contains a `<Folder Name="/cli/">` block
- [ ] `dotnet build ACP-inspector.slnx` succeeds with 0 errors and 0 warnings
- [ ] `dotnet test sentinel/tests/ACP.Tests.fsproj` passes
- [ ] `.github/workflows/ci.yml` no longer has any `cli/` references
- [ ] `.pre-commit-config.yaml` no longer has any `cli/` references
- [ ] `README.md` no longer documents `dotnet run --project cli/apps/ACP.Cli`
- [ ] The two superseded `docs/superpowers/{specs,plans}/2026-04-07-repo-housekeeping-*` files are deleted
- [ ] The single commit message is `chore(scope): remove cli/ subsystem`
- [ ] PR is open against `master` with a body that calls out the
  intentional non-goal of leaving stale CLI references in docs/
- [ ] User's primary working copy bucket-B (excluding the 5 cli/
  intersection files and the slnx) is unchanged

## 10. References

- Superseded spec: `docs/superpowers/specs/2026-04-07-repo-housekeeping-design.md` (committed at `68802b8`, refined at `1ace255`, plan at `15f5893`)
- Triggering bug investigation: `dotnet build ACP-inspector.slnx` →
  "Ambiguous project name 'ACP.Inspector'" — root cause is two
  artifacts both claiming the name `ACP.Inspector` inside the slnx
  project resolver
- Recent restructure: commit `f4d0756` — "Restructure repo into holons and align tooling (#35)"

---

**End of design document.** Awaiting user approval before commit.
