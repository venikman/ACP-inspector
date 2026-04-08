# Repo-Wide Housekeeping Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Execute the 2026-04-07 mega-rollup housekeeping pass on `ACP-inspector` — clean up leftover state from the holon restructure (`f4d0756`), delete merged origin branches, produce a stale-branch disposition report for sign-off, and re-establish a baseline-green build — all without disturbing the user's in-flight ACP 0.10.5 → 0.11.3 schema upgrade work in the primary working copy.

**Architecture:** Single isolated git worktree at `../ACP-inspector-housekeeping/` cut from `origin/master`. All writes happen in the worktree. The primary working copy is **read-only** for the duration of the session — its `git status`, `git diff` content hash, and `HEAD` SHA must be byte-identical before and after. A two-layer **bucket-B exclusion guard** (initial snapshot + per-commit live recomputation) prevents the rollup from writing to any file that the user has uncommitted content for in the primary copy. The result is a single branch `chore/housekeeping-2026-04-07` with 7-10 commits, opened as one PR `chore: repo-wide housekeeping and non-codec revivals`.

**Tech Stack:** Git 2.50+, GitHub CLI (`gh`), .NET 9 (`dotnet build`, `dotnet test`, `dotnet fantomas`), `markdownlint-cli2` via `bunx`, `lychee` link checker, bash 4+, standard Unix tools (`diff`, `sha256sum`, `comm`, `sort`, `awk`).

**Reference spec:** `docs/superpowers/specs/2026-04-07-repo-housekeeping-design.md` (committed at `1ace255`). Read §2 (the invariant), §6 (bucket-B exclusion policy), §9 (classified gate), §10 (branch classifications), §11 (revival framework) before executing.

---

## Critical Invariants (memorize these)

These three rules apply to **every task in this plan**. A task that violates them is a bug in the task, not a policy decision.

1. **Section 2 invariant (byte-identical primary copy).** The primary working copy at `/Users/stas-studio/Developer/ACP-inspector` is read-only. Its `git status --porcelain=v2`, `git diff | sha256sum`, and `HEAD` SHA must be identical at session start and session end. The session-end check in Task 8 enforces this; a failure aborts PR creation.

2. **Bucket-B per-commit guard.** Before any task that **writes** files (Task 3 Fantomas sweep, Task 4 dead-`core/` cleanup, Task 7 revivals), recompute the live bucket-B set and skip any file that appears in it. Use the `recompute_bucket_b_live` helper from `/tmp/housekeeping-2026-04-07/helpers.sh`.

3. **No unmerged branch is deleted without sign-off.** Task 6 produces the disposition report and **stops**. Task 7 only proceeds after the user has marked per-branch sign-off in the report. The 3 ARCHIVE-SQUASH-MERGED branches (work already in master) can fast-track sign-off but still require it.

   **Spec §13 reconciliation.** Spec §13 item 12 reads "No unmerged branch has been deleted in this session" — that wording assumes sign-off happens during PR review and deletion happens in a follow-up session. The refined §10.B triage (and §10's closing sentence "No unmerged branch is deleted or revived without explicit approval") supersedes that: the plan lets sign-off happen **mid-session** via the disposition-file checkboxes, then Task 7 executes. If the user wants the strict §13 behavior instead, they leave every box unchecked in Task 6 — Task 7 then becomes a no-op and the rollup ships with only the disposition report (no §10.B deletions). Task 9's PR body still lists the unmerged branches and their classifications, so a post-merge follow-up session can do the deletions later. **Both modes are supported.**

---

## File Structure

**Created during the session** (in the worktree, committed to `chore/housekeeping-2026-04-07`):

- `docs/reports/housekeeping-2026-04-07/baseline.md` — classified gate findings (Task 1)
- `docs/reports/housekeeping-2026-04-07/audit-001-delta.md` — diff vs prior audit (Task 2)
- `docs/reports/housekeeping-2026-04-07/merged-branches-deleted.md` — §10.A delete log (Task 5)
- `docs/reports/housekeeping-2026-04-07/branch-disposition.md` — §10.B sign-off surface (Task 6, updated in Task 7)
- `docs/reports/housekeeping-2026-04-07/session-log.md` — session close (Task 8)
- `docs/reports/housekeeping-2026-04-07/bucket-b-drift.log` — only if Layer-2 guard detects drift
- `docs/planning/linear-migration.md` — only if user approves the EXTRACT-CANDIDATE in Task 7

**Modified during the session** (in the worktree):

- `.gitmodules` — Task 4, remove `[submodule "core/roadmap/sub-ACP"]` block
- `.gitignore` — Task 4, remove line 23 (`core/evidence/pbt/*latest-failure.json`)
- `sentinel/tests/Pbt/EvidenceRunner.fs` — Task 4, fix line 8 doc comment
- 0..N `.fs`/`.fsi` files — Task 3 Fantomas sweep, scope determined by baseline + bucket-B exclusion

**Scratch (in `/tmp/housekeeping-2026-04-07/`, never committed):**

- `snapshot-before.txt` — primary copy `git status --porcelain=v2` at session start
- `diff-before.sha` — primary copy `git diff` SHA-256 at session start
- `bucket-b.initial.txt` — combined bucket-B file list at session start
- `bucket-b.live.txt` — recomputed before each writing task
- `helpers.sh` — bash helper functions sourced by every task

**Repo state changes (origin):**

- 7 archive tags pushed for §10.A merged branches; 7 branch deletions
- Up to 11 archive tags pushed for §10.B branches (3 squash-merged + 7 archive + 1 conditional); up to 11 branch deletions, all gated on Task 7 sign-off
- 1 PR opened: `chore/housekeeping-2026-04-07` → `master`

---

## Task 0: Pre-flight and Worktree Setup

**Files:**

- Create: `/tmp/housekeeping-2026-04-07/snapshot-before.txt`
- Create: `/tmp/housekeeping-2026-04-07/diff-before.sha`
- Create: `/tmp/housekeeping-2026-04-07/bucket-b.initial.txt`
- Create: `/tmp/housekeeping-2026-04-07/helpers.sh`
- Create: worktree at `/Users/stas-studio/Developer/ACP-inspector-housekeeping/`

- [ ] **Step 1: Create the scratch directory**

```bash
mkdir -p /tmp/housekeeping-2026-04-07
ls -d /tmp/housekeeping-2026-04-07
```

Expected: `/tmp/housekeeping-2026-04-07`

- [ ] **Step 2: Snapshot the primary working copy state**

Run from anywhere — uses absolute paths.

```bash
PRIMARY="/Users/stas-studio/Developer/ACP-inspector"
SCRATCH="/tmp/housekeeping-2026-04-07"

git -C "$PRIMARY" status --porcelain=v2 > "$SCRATCH/snapshot-before.txt"
git -C "$PRIMARY" diff | sha256sum > "$SCRATCH/diff-before.sha"
git -C "$PRIMARY" rev-parse HEAD > "$SCRATCH/head-before.sha"

wc -l "$SCRATCH/snapshot-before.txt"
cat "$SCRATCH/diff-before.sha"
cat "$SCRATCH/head-before.sha"
```

Expected: a non-empty `snapshot-before.txt` (~80+ lines for the current bucket-B), a 64-char hex SHA in `diff-before.sha`, and a 40-char SHA in `head-before.sha`. Record these three values mentally — the session-end check (Task 8) compares against them exactly.

- [ ] **Step 3: Compute the initial bucket-B file list**

```bash
PRIMARY="/Users/stas-studio/Developer/ACP-inspector"
SCRATCH="/tmp/housekeeping-2026-04-07"

{
  git -C "$PRIMARY" diff --name-only
  git -C "$PRIMARY" diff --cached --name-only --diff-filter=D
  git -C "$PRIMARY" ls-files --others --exclude-standard
} | sort -u > "$SCRATCH/bucket-b.initial.txt"

wc -l "$SCRATCH/bucket-b.initial.txt"
head -5 "$SCRATCH/bucket-b.initial.txt"
```

Expected: ~86 lines (35 modified + 46 staged-deleted + 5 untracked, sorted unique). The first 5 entries should include filenames you recognize from the spec's §6 list (e.g., `ACP-inspector.slnx`, `README.md`, `cli/apps/ACP.Benchmark/Program.fs`).

- [ ] **Step 4: Write the helper functions to `helpers.sh`**

```bash
cat > /tmp/housekeeping-2026-04-07/helpers.sh <<'HELPERS_EOF'
#!/usr/bin/env bash
# helpers.sh — sourced by every housekeeping task

PRIMARY="/Users/stas-studio/Developer/ACP-inspector"
WORKTREE="/Users/stas-studio/Developer/ACP-inspector-housekeeping"
SCRATCH="/tmp/housekeeping-2026-04-07"

# recompute_bucket_b_live: Layer-2 guard. Snapshots the live bucket-B from the
# primary copy and compares against the initial snapshot. Logs drift to
# bucket-b-drift.log. Does NOT abort — callers decide what to do with drift.
recompute_bucket_b_live() {
  {
    git -C "$PRIMARY" diff --name-only
    git -C "$PRIMARY" diff --cached --name-only --diff-filter=D
    git -C "$PRIMARY" ls-files --others --exclude-standard
  } | sort -u > "$SCRATCH/bucket-b.live.txt"

  if ! diff -q "$SCRATCH/bucket-b.initial.txt" "$SCRATCH/bucket-b.live.txt" >/dev/null 2>&1; then
    {
      echo "=== bucket-B drift detected at $(date -u +%Y-%m-%dT%H:%M:%SZ) ==="
      echo "    callsite: ${BASH_SOURCE[1]:-?}:${BASH_LINENO[0]:-?}"
      diff "$SCRATCH/bucket-b.initial.txt" "$SCRATCH/bucket-b.live.txt"
      echo
    } >> "$SCRATCH/bucket-b-drift.log"
    echo "WARNING: bucket-B drift detected. See $SCRATCH/bucket-b-drift.log" >&2
  fi
}

# is_in_bucket_b <path>: returns 0 if the path is in the live bucket-B set,
# 1 otherwise. Caller MUST have run recompute_bucket_b_live first.
is_in_bucket_b() {
  local path="$1"
  grep -Fxq "$path" "$SCRATCH/bucket-b.live.txt"
}

# verify_section_2_invariant: compares current primary state against snapshot.
# Returns 0 if identical, 1 if drifted. Used by Task 8.
verify_section_2_invariant() {
  local current_status current_diff_sha current_head
  current_status=$(git -C "$PRIMARY" status --porcelain=v2)
  current_diff_sha=$(git -C "$PRIMARY" diff | sha256sum | awk '{print $1}')
  current_head=$(git -C "$PRIMARY" rev-parse HEAD)

  local stored_status stored_diff_sha stored_head
  stored_status=$(cat "$SCRATCH/snapshot-before.txt")
  stored_diff_sha=$(awk '{print $1}' "$SCRATCH/diff-before.sha")
  stored_head=$(cat "$SCRATCH/head-before.sha")

  local ok=0
  if [ "$current_status" != "$stored_status" ]; then
    echo "FAIL: git status drifted in primary copy" >&2
    diff <(printf '%s\n' "$stored_status") <(printf '%s\n' "$current_status") >&2
    ok=1
  fi
  if [ "$current_diff_sha" != "$stored_diff_sha" ]; then
    echo "FAIL: git diff content hash changed in primary copy" >&2
    echo "  expected: $stored_diff_sha" >&2
    echo "  actual:   $current_diff_sha" >&2
    ok=1
  fi
  if [ "$current_head" != "$stored_head" ]; then
    echo "FAIL: HEAD advanced in primary copy" >&2
    echo "  expected: $stored_head" >&2
    echo "  actual:   $current_head" >&2
    ok=1
  fi
  if [ "$ok" -eq 0 ]; then
    echo "OK: Section-2 invariant preserved" >&2
  fi
  return "$ok"
}
HELPERS_EOF
chmod +x /tmp/housekeeping-2026-04-07/helpers.sh
ls -la /tmp/housekeeping-2026-04-07/helpers.sh
```

Expected: `helpers.sh` exists, ~70 lines, executable bit set.

- [ ] **Step 5: Fetch origin to ensure remote refs are current**

```bash
git -C /Users/stas-studio/Developer/ACP-inspector fetch --prune origin
```

Expected: prune output if any branches were deleted upstream; no errors. The fetch updates `refs/remotes/origin/*` in the shared `.git/` directory but does NOT modify `git status` output, so the Section-2 invariant is preserved.

- [ ] **Step 6: Create the worktree**

```bash
cd /Users/stas-studio/Developer/ACP-inspector
git worktree add -b chore/housekeeping-2026-04-07 ../ACP-inspector-housekeeping origin/master
ls -d ../ACP-inspector-housekeeping/.git
```

Expected: `Preparing worktree (new branch 'chore/housekeeping-2026-04-07')` and `HEAD is now at <sha> Restructure repo into holons and align tooling (#35)`. The `../ACP-inspector-housekeeping/.git` exists (as a file pointing to the primary `.git/worktrees/ACP-inspector-housekeeping/`, not as a directory).

- [ ] **Step 7: Verify worktree HEAD matches `origin/master`**

```bash
cd /Users/stas-studio/Developer/ACP-inspector-housekeeping
git rev-parse HEAD
git rev-parse origin/master
test "$(git rev-parse HEAD)" = "$(git rev-parse origin/master)" && echo OK || echo MISMATCH
```

Expected: same SHA twice, then `OK`. If `MISMATCH`, abort — your `origin/master` is stale or the worktree didn't cut from where expected.

- [ ] **Step 8: Verify the Section-2 snapshot is still valid (sanity check)**

```bash
source /tmp/housekeeping-2026-04-07/helpers.sh
verify_section_2_invariant
```

Expected: `OK: Section-2 invariant preserved`. If FAIL here, something modified the primary copy between Steps 2 and 8 — abort and investigate. Do NOT proceed.

- [ ] **Step 9: Print "Pre-flight complete" and record the worktree path**

```bash
echo "================================="
echo "Pre-flight complete"
echo "  worktree: /Users/stas-studio/Developer/ACP-inspector-housekeeping"
echo "  branch:   chore/housekeeping-2026-04-07"
echo "  scratch:  /tmp/housekeeping-2026-04-07/"
echo "  bucket-B initial: $(wc -l < /tmp/housekeeping-2026-04-07/bucket-b.initial.txt) files"
echo "================================="
```

Expected: status block printed. Do NOT commit yet — Task 0 produces no commits, only state. The first commit happens in Task 1.

---

## Task 1: Commit #1 — Baseline Verification Report (classified gate)

**Files:**

- Create: `docs/reports/housekeeping-2026-04-07/baseline.md` (in worktree)

**All commands run inside the worktree** (`/Users/stas-studio/Developer/ACP-inspector-housekeeping/`).

- [ ] **Step 1: Source helpers and `cd` to the worktree**

```bash
source /tmp/housekeeping-2026-04-07/helpers.sh
cd "$WORKTREE"
pwd
```

Expected: `/Users/stas-studio/Developer/ACP-inspector-housekeeping`

- [ ] **Step 2: Run `dotnet build` (STOP-class check)**

```bash
dotnet build ACP-inspector.slnx --no-restore 2>&1 | tee /tmp/housekeeping-2026-04-07/baseline-build.log
echo "Exit: $?"
```

Expected: `Build succeeded` with `0 Error(s)`. If errors: STOP. Write the error block into `baseline.md`'s `## Build` section, do NOT commit, abort the session, and surface the error to the user. Build errors are not housekeeping's job to fix.

- [ ] **Step 3: Run the test suites (STOP-class check)**

```bash
dotnet test sentinel/tests/ACP.Tests.fsproj --no-build --nologo --logger "console;verbosity=minimal" 2>&1 | tee /tmp/housekeeping-2026-04-07/baseline-test-acp.log
echo "Exit: $?"
```

Expected: `Passed!` with `Failed: 0`. Repeat for the other harness projects:

```bash
for p in Epistemology.Harness Validation.Harness SDK.Harness; do
  dotnet test "sentinel/tests/$p/$p.fsproj" --no-build --nologo --logger "console;verbosity=minimal" 2>&1 | tee "/tmp/housekeeping-2026-04-07/baseline-test-$p.log"
  echo "$p exit: $?"
done
```

Expected: all `Passed!` with no failures. If any fail: STOP, record into `baseline.md`, abort session.

- [ ] **Step 4: Run Fantomas in check mode (AUTO-FIX class)**

```bash
dotnet fantomas --check cli/apps cli/src runtime/src protocol/src sentinel/src sentinel/tests 2>&1 | tee /tmp/housekeeping-2026-04-07/baseline-fantomas.log
echo "Exit: $?"
```

Expected: either exit 0 (all clean — no work for Task 3) or exit 99 with a list of files needing formatting. **Either is acceptable** — the result feeds into Task 3's scope. Save the file list:

```bash
grep -E '^[a-zA-Z].*\.(fs|fsi)' /tmp/housekeeping-2026-04-07/baseline-fantomas.log > /tmp/housekeeping-2026-04-07/fantomas-targets-raw.txt || true
wc -l /tmp/housekeeping-2026-04-07/fantomas-targets-raw.txt
```

- [ ] **Step 5: Run markdownlint (LOG-AND-CONTINUE class)**

```bash
BUN_INSTALL_BACKEND=copyfile bunx markdownlint-cli2 --config .markdownlint-cli2.yaml '**/*.md' 2>&1 | tee /tmp/housekeeping-2026-04-07/baseline-markdownlint.log
echo "Exit: $?"
```

Expected: any exit code is acceptable. Record warnings into `baseline.md`. Do not stop on failures.

- [ ] **Step 6: Run lychee link check (LOG-AND-CONTINUE class)**

```bash
lychee --offline --config lychee.toml docs/ 2>&1 | tee /tmp/housekeeping-2026-04-07/baseline-lychee.log || true
echo "Exit: $?"
```

Expected: any exit code is acceptable. Record broken links into `baseline.md`.

- [ ] **Step 6b: Dead-code scan (LOG-AND-CONTINUE class, §9 of the spec)**

```bash
grep -E 'warning (FS1182|FS0020)' /tmp/housekeeping-2026-04-07/baseline-build.log > /tmp/housekeeping-2026-04-07/baseline-deadcode.log || true
wc -l /tmp/housekeeping-2026-04-07/baseline-deadcode.log
```

Expected: the log captures any `FS1182` (unused binding) or `FS0020` (ignored expression) warnings from the earlier build output. Zero lines is fine; any lines are recorded but not fixed — resolution is out of scope for housekeeping.

- [ ] **Step 7: Cross-reference Fantomas targets against bucket-B (LOG-AND-SKIP class for B-set)**

```bash
recompute_bucket_b_live
sort -u /tmp/housekeeping-2026-04-07/fantomas-targets-raw.txt > /tmp/housekeeping-2026-04-07/fantomas-targets-sorted.txt
comm -12 /tmp/housekeeping-2026-04-07/fantomas-targets-sorted.txt /tmp/housekeeping-2026-04-07/bucket-b.live.txt > /tmp/housekeeping-2026-04-07/fantomas-bucket-b-skip.txt
comm -23 /tmp/housekeeping-2026-04-07/fantomas-targets-sorted.txt /tmp/housekeeping-2026-04-07/bucket-b.live.txt > /tmp/housekeeping-2026-04-07/fantomas-eligible.txt
echo "Total Fantomas violations: $(wc -l < /tmp/housekeeping-2026-04-07/fantomas-targets-sorted.txt)"
echo "  in bucket-B (LOG-SKIP):  $(wc -l < /tmp/housekeeping-2026-04-07/fantomas-bucket-b-skip.txt)"
echo "  eligible for Task 3:     $(wc -l < /tmp/housekeeping-2026-04-07/fantomas-eligible.txt)"
```

Expected: a three-way split. The eligible count is what Task 3 will process.

- [ ] **Step 8: Compose `baseline.md`**

```bash
mkdir -p docs/reports/housekeeping-2026-04-07
cat > docs/reports/housekeeping-2026-04-07/baseline.md <<EOF
# Baseline Verification Report

**Date:** $(date -u +%Y-%m-%dT%H:%M:%SZ)
**Worktree:** \`chore/housekeeping-2026-04-07\` cut from \`origin/master\` ($(git rev-parse origin/master | cut -c1-7))
**Spec:** \`docs/superpowers/specs/2026-04-07-repo-housekeeping-design.md\`
**Gate policy:** §9 of the spec (classified — STOP only on build/test failures)

## Build (STOP-class)

Result: PASS — 0 errors, $(grep -c 'warning' /tmp/housekeeping-2026-04-07/baseline-build.log) warnings.

## Tests (STOP-class)

| Project | Result |
|---|---|
| ACP.Tests | PASS |
| Epistemology.Harness | PASS |
| Validation.Harness | PASS |
| SDK.Harness | PASS |

## Fantomas (AUTO-FIX class for non-bucket-B / LOG-SKIP for bucket-B)

- Total violations: $(wc -l < /tmp/housekeeping-2026-04-07/fantomas-targets-sorted.txt)
- In bucket-B (will be SKIPPED in Task 3): $(wc -l < /tmp/housekeeping-2026-04-07/fantomas-bucket-b-skip.txt)
- Eligible for Task 3 sweep: $(wc -l < /tmp/housekeeping-2026-04-07/fantomas-eligible.txt)

### Files in bucket-B (skipped)

\`\`\`
$(cat /tmp/housekeeping-2026-04-07/fantomas-bucket-b-skip.txt)
\`\`\`

### Files eligible for Task 3 sweep

\`\`\`
$(cat /tmp/housekeeping-2026-04-07/fantomas-eligible.txt)
\`\`\`

## Markdownlint (LOG-AND-CONTINUE)

\`\`\`
$(tail -20 /tmp/housekeeping-2026-04-07/baseline-markdownlint.log)
\`\`\`

## Lychee (LOG-AND-CONTINUE)

\`\`\`
$(tail -20 /tmp/housekeeping-2026-04-07/baseline-lychee.log)
\`\`\`

## Dead-code scan (LOG-AND-CONTINUE)

$(wc -l < /tmp/housekeeping-2026-04-07/baseline-deadcode.log) FS1182/FS0020 warnings recorded. Resolution is out of scope for this pass.

\`\`\`
$(cat /tmp/housekeeping-2026-04-07/baseline-deadcode.log)
\`\`\`

## Bucket-B at session start

$(wc -l < /tmp/housekeeping-2026-04-07/bucket-b.initial.txt) files in primary working copy. Full list in spec §6 / Appendix A.

## Verdict

All STOP-class checks PASS. Proceeding to Task 2 (audit-001 delta).
EOF
wc -l docs/reports/housekeeping-2026-04-07/baseline.md
```

Expected: a baseline.md file ~50-100 lines depending on findings.

- [ ] **Step 9: Verify the file is well-formed**

```bash
head -5 docs/reports/housekeeping-2026-04-07/baseline.md
test -s docs/reports/housekeeping-2026-04-07/baseline.md && echo "non-empty: OK"
```

Expected: title and frontmatter visible, file is non-empty.

- [ ] **Step 10: Commit**

```bash
git add docs/reports/housekeeping-2026-04-07/baseline.md
git commit -m "$(cat <<'MSG'
chore(housekeeping): baseline verification report

Records the classified gate results for the 2026-04-07 housekeeping pass.
All STOP-class checks (build, test) PASS. Fantomas violations and lint
warnings are recorded for Task 3 processing.
MSG
)"
git log --oneline -1
```

Expected: commit succeeds, log shows `chore(housekeeping): baseline verification report` as HEAD.

---

## Task 2: Commit #2 — Audit-001 Delta Report

**Files:**

- Create: `docs/reports/housekeeping-2026-04-07/audit-001-delta.md`
- Read: `docs/reports/audit-001-cleanup.md` (the prior audit)

- [ ] **Step 1: Source helpers and confirm worktree position**

```bash
source /tmp/housekeeping-2026-04-07/helpers.sh
cd "$WORKTREE"
pwd
git log --oneline -1
```

Expected: in worktree, HEAD is the baseline commit from Task 1.

- [ ] **Step 2: Read the prior audit findings**

```bash
head -100 docs/reports/audit-001-cleanup.md
```

Expected: prior audit content visible. Read the "Findings" or "Summary" section to enumerate prior items: build clean, 261 tests passing, 6 files needing Fantomas, 5 files >500 lines, codec split intent, 2 empty test stubs, ~29% doc coverage.

- [ ] **Step 3: Compose the delta report**

```bash
PRIOR_FANTOMAS=6  # From audit-001 — verify this in Step 2 output
PRIOR_TESTS=261
CURR_FANTOMAS=$(wc -l < /tmp/housekeeping-2026-04-07/fantomas-targets-sorted.txt)
CURR_FANTOMAS_ELIGIBLE=$(wc -l < /tmp/housekeeping-2026-04-07/fantomas-eligible.txt)

cat > docs/reports/housekeeping-2026-04-07/audit-001-delta.md <<EOF
# Audit-001 Delta Report

**Date:** $(date -u +%Y-%m-%dT%H:%M:%SZ)
**Prior audit:** \`docs/reports/audit-001-cleanup.md\` (2026-01-06)
**This pass:** 2026-04-07 housekeeping

## Build status

| Metric | 2026-01-06 | 2026-04-07 | Delta |
|---|---|---|---|
| Build errors | 0 | 0 | unchanged |
| Build warnings | 0 | $(grep -c 'warning' /tmp/housekeeping-2026-04-07/baseline-build.log) | see baseline.md |
| Test pass count | $PRIOR_TESTS | (see baseline.md test results) | see baseline.md |

## Fantomas

- 2026-01-06: 6 files needing format
- 2026-04-07: $CURR_FANTOMAS files needing format ($CURR_FANTOMAS_ELIGIBLE eligible after bucket-B exclusion)
- Net change: see Task 3's sweep result for what gets fixed in this pass.

## Codec split intent (audit-001 recommendation)

The 2026-01-06 audit recommended splitting \`runtime/src/Acp.Codec.fs\` (then 3,130 lines) into Codec.Types.fs / Codec.Json.fs / Codec.Decode.fs / Codec.Encode.fs / Codec.Router.fs. Current state under the holon layout:

\`\`\`
$(ls -la runtime/src/Acp.Codec*.fs 2>&1)
\`\`\`

Status: PARTIALLY ADDRESSED via the holon restructure (\`f4d0756\`). The codec is now split across multiple files. Further splitting is **deferred** because \`runtime/src/Acp.Codec.AcpJson.fs\` is currently in bucket-B (in-flight schema upgrade).

## Empty test stubs flagged in audit-001

Audit-001 flagged \`sentinel/tests/Epistemology.Harness/EvalTests.fs:10\` and \`ValidationTaxonomyTests.fs:10\` as empty. Current state:

\`\`\`
$(grep -c 'let.*test' sentinel/tests/Epistemology.Harness/EvalTests.fs 2>/dev/null || echo 'file not found') tests in EvalTests.fs
$(grep -c 'let.*test' sentinel/tests/Epistemology.Harness/ValidationTaxonomyTests.fs 2>/dev/null || echo 'file not found') tests in ValidationTaxonomyTests.fs
\`\`\`

Status: still empty / unchanged. Not in scope for this housekeeping pass — recorded for future audit.

## Doc coverage

The audit-001 ~29% doc coverage estimate is unchanged in scope. The holon restructure relocated docs but did not change the underlying coverage. Recorded for future audit.

## Verdict

Net delta from audit-001: holon restructure addressed the codec split (partially), all other items (test stubs, doc coverage) remain unchanged. Fantomas count is comparable (within noise). No regression detected.
EOF
wc -l docs/reports/housekeeping-2026-04-07/audit-001-delta.md
```

Expected: a delta report ~60 lines. If the prior audit's numbers (`PRIOR_FANTOMAS=6`, `PRIOR_TESTS=261`) are wrong based on Step 2, fix them inline before running.

- [ ] **Step 4: Commit**

```bash
git add docs/reports/housekeeping-2026-04-07/audit-001-delta.md
git commit -m "$(cat <<'MSG'
chore(housekeeping): audit-001 delta report

Compares 2026-04-07 findings against audit-001-cleanup.md (2026-01-06).
Net delta: holon restructure partially addressed codec split. Empty test
stubs and doc coverage unchanged — recorded for future audit, out of
scope for this pass.
MSG
)"
git log --oneline -2
```

Expected: 2 commits in worktree now (baseline + delta).

---

## Task 3: Commit #3 — Fantomas Sweep (non-bucket-B only)

**Files:**

- Modify: 0..N `.fs`/`.fsi` files (scope from `fantomas-eligible.txt`)

- [ ] **Step 1: Source helpers, confirm worktree, recompute bucket-B live**

```bash
source /tmp/housekeeping-2026-04-07/helpers.sh
cd "$WORKTREE"
recompute_bucket_b_live
echo "Eligible files (initial): $(wc -l < /tmp/housekeeping-2026-04-07/fantomas-eligible.txt)"
```

Expected: live bucket-B recomputed. Drift warning printed if anything changed since Task 0; otherwise silent.

- [ ] **Step 2: Re-derive the eligible Fantomas target list against the LIVE bucket-B**

The eligible list from Task 1 is based on the initial bucket-B. Re-derive against live to catch any drift:

```bash
comm -12 /tmp/housekeeping-2026-04-07/fantomas-targets-sorted.txt /tmp/housekeeping-2026-04-07/bucket-b.live.txt > /tmp/housekeeping-2026-04-07/fantomas-bucket-b-skip-live.txt
comm -23 /tmp/housekeeping-2026-04-07/fantomas-targets-sorted.txt /tmp/housekeeping-2026-04-07/bucket-b.live.txt > /tmp/housekeeping-2026-04-07/fantomas-eligible-live.txt
echo "Eligible after live recompute: $(wc -l < /tmp/housekeeping-2026-04-07/fantomas-eligible-live.txt)"
diff /tmp/housekeeping-2026-04-07/fantomas-eligible.txt /tmp/housekeeping-2026-04-07/fantomas-eligible-live.txt || echo "(eligible list changed since Task 1)"
```

Expected: identical lists if no drift, or a small diff if the user touched files in the primary copy between Task 1 and Task 3.

- [ ] **Step 3: If the eligible list is empty, skip to Step 7 (no-op commit not needed)**

```bash
if [ ! -s /tmp/housekeeping-2026-04-07/fantomas-eligible-live.txt ]; then
  echo "No eligible files. Skipping Fantomas sweep — no commit will be created for Task 3."
  echo "Update the rollup commit table to reflect a 6-commit (or 6+revivals) result."
  exit 0
fi
```

Expected: if empty, Task 3 produces no commit. Document this in the session log (Task 8) and proceed to Task 4. The rollup will have one fewer commit than the spec's max.

- [ ] **Step 4: Run Fantomas on the eligible list**

```bash
xargs -a /tmp/housekeeping-2026-04-07/fantomas-eligible-live.txt dotnet fantomas 2>&1 | tee /tmp/housekeeping-2026-04-07/fantomas-sweep.log
echo "Exit: $?"
```

Expected: Fantomas reformats each file in place. Exit 0. The log shows `Formatted: <path>` for each file changed.

- [ ] **Step 5: Verify build still passes after formatting**

```bash
dotnet build ACP-inspector.slnx --no-restore 2>&1 | tail -10
echo "Exit: $?"
```

Expected: `Build succeeded` with 0 errors. If errors: Fantomas produced a syntactically broken file. Run `git diff` to investigate, revert with `git checkout -- <files>`, and abort the task — do NOT commit broken code.

- [ ] **Step 6: Verify tests still pass**

```bash
dotnet test sentinel/tests/ACP.Tests.fsproj --no-build --nologo --logger "console;verbosity=quiet" 2>&1 | tail -5
echo "Exit: $?"
```

Expected: `Passed!`. If failures: revert and abort.

- [ ] **Step 7: List the files Fantomas actually changed**

```bash
git diff --name-only > /tmp/housekeeping-2026-04-07/fantomas-changed-actual.txt
wc -l /tmp/housekeeping-2026-04-07/fantomas-changed-actual.txt
cat /tmp/housekeeping-2026-04-07/fantomas-changed-actual.txt
```

Expected: list of files that Fantomas modified. May be a subset of the eligible list (some files might already match the formatter).

- [ ] **Step 8: Verify NONE of the changed files are in bucket-B (defense in depth)**

```bash
recompute_bucket_b_live  # one more time, in case of drift during Steps 4-7
INTERSECTION=$(comm -12 <(sort /tmp/housekeeping-2026-04-07/fantomas-changed-actual.txt) /tmp/housekeeping-2026-04-07/bucket-b.live.txt)
if [ -n "$INTERSECTION" ]; then
  echo "FAIL: Fantomas modified bucket-B files. Reverting." >&2
  echo "$INTERSECTION" >&2
  git checkout -- .
  exit 1
fi
echo "OK: no bucket-B intersection"
```

Expected: empty intersection, `OK: no bucket-B intersection`. If non-empty: revert and investigate (this should not happen given Step 2's filtering — if it does, there's a Fantomas behavior we don't understand).

- [ ] **Step 9: Commit with skip-log footer**

```bash
SKIP_COUNT=$(wc -l < /tmp/housekeeping-2026-04-07/fantomas-bucket-b-skip-live.txt)
git add -A
git commit -m "$(cat <<MSG
chore(housekeeping): fantomas sweep (non-bucket-B)

Reformats $(wc -l < /tmp/housekeeping-2026-04-07/fantomas-changed-actual.txt) F# files to match the project Fantomas config.

Bucket-B exclusions: $SKIP_COUNT files skipped because they have
uncommitted content in the primary working copy (in-flight ACP 0.10.5
to 0.11.3 schema upgrade). These files will need to be reformatted in
a follow-up pass after the schema upgrade lands.

Skipped files:
$(sed 's/^/  - /' /tmp/housekeeping-2026-04-07/fantomas-bucket-b-skip-live.txt)
MSG
)"
git log --oneline -3
```

Expected: 3 commits in worktree. If the eligible list was empty (Step 3 exit), this step is skipped — Task 3 produces no commit.

---

## Task 4: Commit #4 — Remove Dead `core/` References

**Files:**

- Modify: `.gitmodules`
- Modify: `.gitignore`
- Modify: `sentinel/tests/Pbt/EvidenceRunner.fs`

- [ ] **Step 1: Source helpers, confirm worktree, recompute bucket-B live, verify targets are NOT in bucket-B**

```bash
source /tmp/housekeeping-2026-04-07/helpers.sh
cd "$WORKTREE"
recompute_bucket_b_live

for f in .gitmodules .gitignore sentinel/tests/Pbt/EvidenceRunner.fs; do
  if is_in_bucket_b "$f"; then
    echo "FAIL: $f is in bucket-B — abort task" >&2
    exit 1
  fi
  echo "OK: $f not in bucket-B"
done
```

Expected: all three files print `OK: ... not in bucket-B`. None of them appear in the spec §6 list, so this should pass cleanly.

- [ ] **Step 2: Inspect the current `.gitmodules` block to delete**

```bash
cat .gitmodules
```

Expected output:

```
[submodule "core/roadmap/sub-ACP"]
 path = core/roadmap/sub-ACP
 url = https://github.com/agentclientprotocol/agent-client-protocol
 branch = main
```

If `.gitmodules` has different content, STOP and investigate — the spec assumes this exact block.

- [ ] **Step 3: Edit `.gitmodules` — remove the dead submodule block**

If the block above is the ONLY content, delete the entire file. If there are other submodules, edit to remove only this block.

```bash
# Check if it's the only block
SUBMODULE_COUNT=$(grep -c '^\[submodule' .gitmodules)
echo "Submodule count: $SUBMODULE_COUNT"
```

If `SUBMODULE_COUNT == 1`:

```bash
git rm .gitmodules
ls .gitmodules 2>&1
```

Expected: `'.gitmodules' deleted` and `ls: .gitmodules: No such file or directory`.

If `SUBMODULE_COUNT > 1`: use the editor to remove only the `[submodule "core/roadmap/sub-ACP"]` block and its three indented lines. Then verify with `cat .gitmodules`.

- [ ] **Step 4: Verify no other config references the dead path**

```bash
grep -r 'core/roadmap/sub-ACP' . --include='*.fs' --include='*.fsproj' --include='*.yaml' --include='*.yml' --include='*.toml' --include='*.json' 2>/dev/null || echo "(no other references)"
```

Expected: `(no other references)`.

- [ ] **Step 5: Inspect `.gitignore` line 23**

```bash
sed -n '20,26p' .gitignore
```

Expected output around line 23:

```
core/evidence/pbt/*latest-failure.json
```

If line 23 has different content, find the actual line with the dead path:

```bash
grep -n 'core/evidence' .gitignore
```

Use the actual line number in Step 6.

- [ ] **Step 6: Edit `.gitignore` — remove the dead `core/evidence/` line**

Use the actual line number from Step 5 (substitute `23` if different):

```bash
# Method A: sed in place (verify with diff first)
sed -i.bak '/core\/evidence\/pbt\/\*latest-failure\.json/d' .gitignore
diff .gitignore.bak .gitignore
rm .gitignore.bak
```

Expected: the diff shows exactly one line removed (`core/evidence/pbt/*latest-failure.json`).

- [ ] **Step 7: Verify no files match the dead `.gitignore` rule**

```bash
ls core/evidence/ 2>&1
```

Expected: `ls: core/evidence/: No such file or directory`. Confirms the rule was orphaned.

- [ ] **Step 8: Inspect `sentinel/tests/Pbt/EvidenceRunner.fs:8`**

```bash
sed -n '1,15p' sentinel/tests/Pbt/EvidenceRunner.fs
```

Expected: line 8 contains a comment like `/// FsCheck runner that persists the latest failing counterexample to core/evidence/pbt/`. The path `core/evidence/pbt/` no longer exists.

- [ ] **Step 9: Edit the comment to remove the dead path reference**

Determine the correct current path. The PBT evidence is now likely persisted to a different location (or no location if the runner was simplified). Inspect the rest of the file:

```bash
grep -n 'core/evidence\|evidence' sentinel/tests/Pbt/EvidenceRunner.fs
```

If evidence is now persisted somewhere else, use that path. If it's no longer persisted (and only stored in memory or printed), update the comment to reflect that.

For example, if the new behavior is "prints to stdout":

```bash
sed -i.bak 's|FsCheck runner that persists the latest failing counterexample to core/evidence/pbt/|FsCheck runner that prints the latest failing counterexample to stdout|' sentinel/tests/Pbt/EvidenceRunner.fs
diff sentinel/tests/Pbt/EvidenceRunner.fs.bak sentinel/tests/Pbt/EvidenceRunner.fs
rm sentinel/tests/Pbt/EvidenceRunner.fs.bak
```

If you can't determine the new behavior from reading the file, use a generic comment that doesn't lie:

```bash
sed -i.bak 's|FsCheck runner that persists the latest failing counterexample to core/evidence/pbt/|FsCheck runner that surfaces the latest failing counterexample for property-based tests|' sentinel/tests/Pbt/EvidenceRunner.fs
diff sentinel/tests/Pbt/EvidenceRunner.fs.bak sentinel/tests/Pbt/EvidenceRunner.fs
rm sentinel/tests/Pbt/EvidenceRunner.fs.bak
```

Expected: the diff shows exactly one line changed.

- [ ] **Step 10: Verify build still passes**

```bash
dotnet build ACP-inspector.slnx --no-restore 2>&1 | tail -5
echo "Exit: $?"
```

Expected: `Build succeeded` with 0 errors. The comment edit shouldn't affect compilation.

- [ ] **Step 11: Verify tests still pass**

```bash
dotnet test sentinel/tests/ACP.Tests.fsproj --no-build --nologo --logger "console;verbosity=quiet" 2>&1 | tail -5
echo "Exit: $?"
```

Expected: `Passed!`.

- [ ] **Step 12: Commit**

```bash
git add -A
git status --short
git commit -m "$(cat <<'MSG'
chore(housekeeping): remove dead core/ references

Three orphan references to the pre-restructure `core/` directory tree:

- .gitmodules: removed `[submodule "core/roadmap/sub-ACP"]` block.
  The submodule path no longer exists; `git submodule status` was empty.
- .gitignore: removed `core/evidence/pbt/*latest-failure.json` rule.
  The directory no longer exists; the rule was orphaned.
- sentinel/tests/Pbt/EvidenceRunner.fs:8: comment referenced
  `core/evidence/pbt/` as the persistence target. Updated to reflect
  current behavior (no fictional path).

No build or test impact.
MSG
)"
git log --oneline -4
```

Expected: 4 commits in worktree (or 3 if Task 3 was a no-op).

---

## Task 5: Commit #5 — Delete Merged Origin Branches (§10.A)

**Files:**

- Create: `docs/reports/housekeeping-2026-04-07/merged-branches-deleted.md`

**Branches to process** (from spec §10.A — verify still merged before deleting):

1. `feat/fsharp-tokenizer-eval`
2. `feature/observability`
3. `first-stage`
4. `feature/acp-draft-support`
5. `feature/fpf-alignment-phase1-phase2`
6. `feature/schema-pin-ci-watch`
7. `followup/registry-hardening`

- [ ] **Step 1: Source helpers and re-fetch (in case the spec is stale)**

```bash
source /tmp/housekeeping-2026-04-07/helpers.sh
cd "$WORKTREE"
git fetch --prune origin
```

Expected: prune output if anything was deleted upstream. If any of the 7 branches above were already deleted on origin, the loop in Step 3 will skip them gracefully.

- [ ] **Step 2: Re-verify each branch is merged into `origin/master`**

```bash
MERGED=(
  "feat/fsharp-tokenizer-eval"
  "feature/observability"
  "first-stage"
  "feature/acp-draft-support"
  "feature/fpf-alignment-phase1-phase2"
  "feature/schema-pin-ci-watch"
  "followup/registry-hardening"
)
for b in "${MERGED[@]}"; do
  if git merge-base --is-ancestor "origin/$b" origin/master 2>/dev/null; then
    echo "OK: origin/$b is reachable from origin/master"
  elif git ls-remote --exit-code origin "refs/heads/$b" >/dev/null 2>&1; then
    echo "WARN: origin/$b exists but is NOT reachable from origin/master — DO NOT DELETE in this task"
  else
    echo "INFO: origin/$b no longer exists on remote — skip"
  fi
done
```

Expected: 7 `OK` lines. If any `WARN` appears, that branch's classification has changed since the spec was written — STOP, update §10.A in the spec, re-commit the spec, then resume.

- [ ] **Step 3: Push archive tag for each branch, then delete the branch from origin**

```bash
mkdir -p /tmp/housekeeping-2026-04-07/archive-tags-pushed
for b in "${MERGED[@]}"; do
  if ! git ls-remote --exit-code origin "refs/heads/$b" >/dev/null 2>&1; then
    echo "SKIP: origin/$b already gone"
    continue
  fi
  TIP=$(git rev-parse "origin/$b")
  TAG="archive/$b"
  AUTHOR=$(git log -1 --format='%an <%ae>' "$TIP")
  COMMITTED=$(git log -1 --format='%cI' "$TIP")
  echo "=== $b ==="
  echo "  tip:       $TIP"
  echo "  tag:       $TAG"
  echo "  author:    $AUTHOR"
  echo "  committed: $COMMITTED"
  # Create annotated tag with forensic metadata per spec §12
  git tag -a "$TAG" "$TIP" -m "$(cat <<TAGMSG
Archived on 2026-04-07.
Original ref:     refs/heads/$b
Tip SHA:          $TIP
Last author:      $AUTHOR
Committed:        $COMMITTED
Reason:           Merged into origin/master (reachable via git merge-base --is-ancestor).
Restore:          git checkout -b $b $TAG && git push -u origin $b
TAGMSG
)"
  git push origin "$TAG"
  git push origin --delete "$b"
  echo "$b $TIP" >> /tmp/housekeeping-2026-04-07/archive-tags-pushed/merged.log
done
```

Expected: for each branch, three operations succeed (`tag`, `push tag`, `push delete`). The log file accumulates one line per processed branch.

- [ ] **Step 4: Verify all 7 archive tags exist on origin**

```bash
for b in "${MERGED[@]}"; do
  if git ls-remote --exit-code origin "refs/tags/archive/$b" >/dev/null 2>&1; then
    echo "OK: archive/$b on origin"
  else
    echo "MISSING: archive/$b not on origin"
  fi
done
```

Expected: 7 `OK` lines. Any `MISSING` is a bug — STOP and investigate.

- [ ] **Step 5: Verify all 7 branches are gone from origin**

```bash
git fetch --prune origin
for b in "${MERGED[@]}"; do
  if git ls-remote --exit-code origin "refs/heads/$b" >/dev/null 2>&1; then
    echo "STILL PRESENT: origin/$b"
  else
    echo "GONE: origin/$b"
  fi
done
```

Expected: 7 `GONE` lines.

- [ ] **Step 6: Compose the deletion report**

```bash
cat > docs/reports/housekeeping-2026-04-07/merged-branches-deleted.md <<EOF
# Merged-Branch Deletion Log (§10.A)

**Date:** $(date -u +%Y-%m-%dT%H:%M:%SZ)
**Spec section:** §10.A — Merged into \`origin/master\` (7 branches)

7 branches that were reachable from \`origin/master\` (verified via \`git merge-base --is-ancestor\` immediately before deletion). Each was archived as an annotated tag, then the branch was deleted from origin. Restore via \`git checkout -b <branch> archive/<branch>\`.

| # | Branch | Tip SHA | Archive tag | Status |
|---|---|---|---|---|
$(awk '{printf "| %d | %s | %s | archive/%s | DELETED |\n", NR, $1, substr($2,1,7), $1}' /tmp/housekeeping-2026-04-07/archive-tags-pushed/merged.log)

## Restore procedure (for future reference)

\`\`\`
git checkout -b <branch> archive/<branch>
git push -u origin <branch>
\`\`\`

No history was lost. The annotated tags preserve the exact tip SHAs and contain restoration instructions in their tag messages.
EOF
wc -l docs/reports/housekeeping-2026-04-07/merged-branches-deleted.md
```

Expected: a deletion log with 7 rows.

- [ ] **Step 7: Commit the report**

```bash
git add docs/reports/housekeeping-2026-04-07/merged-branches-deleted.md
git commit -m "$(cat <<'MSG'
chore(housekeeping): delete merged origin branches

7 branches reachable from origin/master have been archived as annotated
tags (refs/tags/archive/<branch>) and deleted from origin. Restore via
`git checkout -b <branch> archive/<branch>`.

See docs/reports/housekeeping-2026-04-07/merged-branches-deleted.md for
the per-branch tip SHAs.
MSG
)"
git log --oneline -5
```

Expected: 5 commits in worktree (or 4 if Task 3 was a no-op).

---

## Task 6: Commit #6 — Stale-Branch Disposition Report (§10.B)

**Files:**

- Create: `docs/reports/housekeeping-2026-04-07/branch-disposition.md`

**This task ENDS at commit time. Task 7 cannot start until the user has signed off on at least one branch in the report.**

- [ ] **Step 1: Source helpers, fetch, confirm worktree position**

```bash
source /tmp/housekeeping-2026-04-07/helpers.sh
cd "$WORKTREE"
git fetch --prune origin
```

- [ ] **Step 2: Re-verify the 11 unmerged branches are still on origin**

```bash
UNMERGED=(
  "docs/linear-migration-planning"
  "add-claude-github-actions-1765859631445"
  "copilot/sub-pr-17"
  "fix/compliance-feedback"
  "chore/2025-12-16"
  "feat/bounded-contexts-and-codec-refactor"
  "docs/overview-image-and-task-index"
  "docs/remove-mermaid-diagrams"
  "fix/remove-mistaken-image"
  "codex/make-acp-inspector-valuable"
  "codex/sub-pr-35"
)
for b in "${UNMERGED[@]}"; do
  if git ls-remote --exit-code origin "refs/heads/$b" >/dev/null 2>&1; then
    TIP=$(git rev-parse "origin/$b")
    echo "OK: $b ($TIP)"
  else
    echo "GONE: $b (already deleted on origin since spec was written)"
  fi
done > /tmp/housekeeping-2026-04-07/unmerged-status.txt
cat /tmp/housekeeping-2026-04-07/unmerged-status.txt
```

Expected: 11 `OK` lines with current tip SHAs. Any `GONE` means the spec is stale — record but proceed; the disposition report will mark gone branches as already-handled.

- [ ] **Step 3: Compose the disposition report from the spec's §10.B classifications**

```bash
cat > docs/reports/housekeeping-2026-04-07/branch-disposition.md <<EOF
# Stale-Branch Disposition Report (§10.B)

**Date:** $(date -u +%Y-%m-%dT%H:%M:%SZ)
**Spec section:** §10.B — Not merged on \`origin/master\` (11 branches; per-branch sign-off required)
**Triage source:** \`docs/superpowers/specs/2026-04-07-repo-housekeeping-design.md\` §10.B

> **PER-BRANCH SIGN-OFF REQUIRED.** Mark each box below before Task 7 executes the disposition. No box checked = branch is left untouched. Boxes are persistent — re-running Task 7 only acts on newly-checked boxes.

## Branches by classification

### 🟢 ARCHIVE-SQUASH-MERGED (3 branches — fast-track sign-off; work is already in master)

These branches have all their commits already squash-merged into master under different SHAs. Deleting them loses no work. Verified via \`git cherry origin/master <branch>\`.

- [ ] \`docs/overview-image-and-task-index\` (PR #27 — work in master)
- [ ] \`docs/remove-mermaid-diagrams\` (PR #28 — work in master)
- [ ] \`fix/remove-mistaken-image\` (image swap — work in master)

### 🔴 ARCHIVE (7 branches — work unrecoverable or superseded)

- [ ] \`add-claude-github-actions-1765859631445\` — both target workflow files already exist on current master; superseded.
- [ ] \`copilot/sub-pr-17\` — pre-restructure paths (\`apps/\`, \`src/\`); 4 of 8 commits already squash-merged.
- [ ] \`fix/compliance-feedback\` — pre-restructure paths; only \`.github/workflows/ci.yml\` change is potentially relevant. Flag below if you want it extracted.
- [ ] \`chore/2025-12-16\` — date-named scratch branch; no coherent intent.
- [ ] \`feat/bounded-contexts-and-codec-refactor\` — pre-restructure paths; intersects current bucket-B in 3 files; codec-split intent partially absorbed by holon restructure.
- [ ] \`codex/make-acp-inspector-valuable\` — codex experiment; \`.codex/\` directory not adopted on master; massive state inversion.
- [ ] \`codex/sub-pr-35\` — codex branch; not actually squash-merged into PR #35; has accidentally-committed node_modules.

### 🟡 EXTRACT-CANDIDATE (1 branch — requires content review)

Single new file at \`docs/planning/linear-migration.md\`. The \`docs/planning/\` directory does not exist on current master, so a clean apply is possible if the content is still relevant. **Read the file before deciding.**

To preview:

\`\`\`
git -C $WORKTREE show origin/docs/linear-migration-planning:docs/planning/linear-migration.md | less
\`\`\`

Choose ONE:

- [ ] **EXTRACT**: I have read the file and I want it on master. Task 7 will commit it as \`docs/planning/linear-migration.md\` and then archive+delete the branch.
- [ ] **ARCHIVE**: I have read the file and I do not want it. Task 7 will archive+delete the branch without extracting.

## Optional sub-extractions

- [ ] **Extract \`fix/compliance-feedback\`'s \`.github/workflows/ci.yml\` change** for review (do not auto-commit; produces a diff file in scratch for manual inspection).

## Sign-off summary

When you're done checking boxes, reply with:

> "Task 7: $(date -u +%Y-%m-%d) sign-off complete"

…and Task 7 will execute the dispositions for every checked box.

## Per-branch tip SHAs (for archive tags)

| Branch | Tip SHA |
|---|---|
$(awk '/^OK:/ {gsub(/[()]/,"",$3); printf "| %s | %s |\n", $2, substr($3,1,7)}' /tmp/housekeeping-2026-04-07/unmerged-status.txt)

EOF
wc -l docs/reports/housekeeping-2026-04-07/branch-disposition.md
```

Expected: a disposition report with sign-off checkboxes for all 11 branches plus the EXTRACT decision and optional sub-extractions.

- [ ] **Step 4: Commit**

```bash
git add docs/reports/housekeeping-2026-04-07/branch-disposition.md
git commit -m "$(cat <<'MSG'
chore(housekeeping): stale-branch disposition report

Per-branch sign-off surface for the 11 unmerged origin branches in §10.B.
Pre-classified into 3 ARCHIVE-SQUASH-MERGED (work already in master),
7 ARCHIVE, and 1 EXTRACT-CANDIDATE pending content review.

NO BRANCH IS DELETED until the user marks the corresponding checkbox in
this report. Task 7 reads the checked boxes and executes the dispositions.

See spec §10.B for the full evidence and §11 for the 7-outcome taxonomy.
MSG
)"
git log --oneline -6
```

Expected: 6 commits in worktree (or 5 if Task 3 was a no-op).

- [ ] **Step 5: STOP — wait for user sign-off**

```bash
echo "================================="
echo "TASK 6 COMPLETE — STOPPING FOR SIGN-OFF"
echo "================================="
echo
echo "The disposition report is committed at:"
echo "  $WORKTREE/docs/reports/housekeeping-2026-04-07/branch-disposition.md"
echo
echo "Open the file, check the boxes for branches you approve, then say"
echo "  'Task 7: $(date -u +%Y-%m-%d) sign-off complete'"
echo
echo "Task 7 will not run until sign-off is received."
exit 0
```

Expected: clear stop signal printed. Do not proceed to Task 7 until the user has explicitly confirmed sign-off and you have re-read the disposition file to see which boxes are checked.

---

## Task 7: Per-Branch Sign-Off Execution (gated on user input)

**Pre-condition:** The user has explicitly said the sign-off is complete AND the boxes in `docs/reports/housekeeping-2026-04-07/branch-disposition.md` reflect their decisions.

**Files:**

- Modify: `docs/reports/housekeeping-2026-04-07/branch-disposition.md` (update with executed marks)
- Possibly create: `docs/planning/linear-migration.md` (only if EXTRACT box is checked)

- [ ] **Step 1: Source helpers, confirm worktree, parse the disposition file**

```bash
source /tmp/housekeeping-2026-04-07/helpers.sh
cd "$WORKTREE"
git pull --rebase  # in case the user edited the file in the worktree directly
```

Then read the file and extract which boxes are checked:

```bash
DISPOSITION="docs/reports/housekeeping-2026-04-07/branch-disposition.md"
grep -E '^\- \[x\]' "$DISPOSITION" || echo "(no boxes checked — Task 7 has nothing to do)"
```

Expected: a list of checked-box lines, or the "no boxes checked" message. If the latter, exit Task 7 and proceed to Task 8.

- [ ] **Step 2: Build the list of approved branches**

Extract branch names from checked boxes. The format in the report is `- [x] \`branch-name\` ...`.

```bash
APPROVED=()
while IFS= read -r line; do
  # Extract the branch name from `- [x] ` followed by a backtick-quoted name
  b=$(echo "$line" | grep -oE '`[^`]+`' | head -1 | tr -d '`')
  if [ -n "$b" ]; then
    APPROVED+=("$b")
  fi
done < <(grep -E '^\- \[x\]' "$DISPOSITION" | grep -v 'EXTRACT\|ARCHIVE:\|sub-extractions')

echo "Approved for archive+delete: ${#APPROVED[@]} branches"
printf '  %s\n' "${APPROVED[@]}"
```

Expected: a list of 0-11 branches the user approved.

- [ ] **Step 3: Check if EXTRACT was approved for `docs/linear-migration-planning`**

```bash
EXTRACT_APPROVED=false
if grep -E '^\- \[x\] \*\*EXTRACT\*\*' "$DISPOSITION" >/dev/null; then
  EXTRACT_APPROVED=true
  echo "EXTRACT approved for docs/linear-migration-planning"
fi
```

- [ ] **Step 4: If EXTRACT approved, perform the extraction**

```bash
if [ "$EXTRACT_APPROVED" = true ]; then
  recompute_bucket_b_live
  if is_in_bucket_b "docs/planning/linear-migration.md"; then
    echo "FAIL: docs/planning/linear-migration.md is in bucket-B — cannot extract" >&2
    EXTRACT_APPROVED=false
  else
    mkdir -p docs/planning
    git show "origin/docs/linear-migration-planning:docs/planning/linear-migration.md" > docs/planning/linear-migration.md
    test -s docs/planning/linear-migration.md && echo "OK: extracted $(wc -l < docs/planning/linear-migration.md) lines"
    git add docs/planning/linear-migration.md
    git commit -m "$(cat <<'MSG'
docs(planning): extract linear-migration.md from docs/linear-migration-planning branch

Pulled from origin/docs/linear-migration-planning per Task 7 sign-off.
The branch will be archived and deleted in the same Task 7 batch.
MSG
)"
    echo "EXTRACT commit:"
    git log --oneline -1
  fi
fi
```

Expected: if approved and not in bucket-B, a new commit with the extracted file. If in bucket-B (shouldn't happen), the extraction is skipped with a clear error.

- [ ] **Step 5: For each approved branch, push archive tag and delete from origin**

```bash
mkdir -p /tmp/housekeeping-2026-04-07/archive-tags-pushed
for b in "${APPROVED[@]}"; do
  if ! git ls-remote --exit-code origin "refs/heads/$b" >/dev/null 2>&1; then
    echo "SKIP: origin/$b already gone"
    continue
  fi
  TIP=$(git rev-parse "origin/$b")
  TAG="archive/$b"
  AUTHOR=$(git log -1 --format='%an <%ae>' "$TIP")
  COMMITTED=$(git log -1 --format='%cI' "$TIP")
  # Build classification-specific archive message
  CLASS="ARCHIVE"
  if awk '/### .* ARCHIVE-SQUASH-MERGED/,/^### /' "$DISPOSITION" | grep -F "\`$b\`" >/dev/null; then
    CLASS="ARCHIVE-SQUASH-MERGED"
  fi
  echo "=== $b ==="
  echo "  tip:       $TIP"
  echo "  class:     $CLASS"
  echo "  author:    $AUTHOR"
  echo "  committed: $COMMITTED"
  # Annotated tag with forensic metadata per spec §12
  git tag -a "$TAG" "$TIP" -m "$(cat <<TAGMSG
Archived on 2026-04-07.
Original ref:     refs/heads/$b
Tip SHA:          $TIP
Last author:      $AUTHOR
Committed:        $COMMITTED
Classification:   $CLASS
Reason:           Per Task 7 sign-off in docs/reports/housekeeping-2026-04-07/branch-disposition.md.
Restore:          git checkout -b $b $TAG && git push -u origin $b
TAGMSG
)"
  git push origin "$TAG"
  git push origin --delete "$b"
  echo "$b $TIP $CLASS" >> /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log
done
```

Expected: for each approved branch, archive tag pushed and remote branch deleted.

- [ ] **Step 6: Update the disposition report with executed marks**

For each branch in `archive-tags-pushed/unmerged.log`, append `[EXECUTED 2026-04-07]` to its line in the disposition report:

```bash
TODAY=$(date -u +%Y-%m-%d)
while IFS=' ' read -r b tip cls; do
  # Append marker to lines in the disposition file that contain this branch name
  sed -i.bak "s|\(- \[x\] \`$b\`[^\\n]*\)|\1 [EXECUTED $TODAY tip=${tip:0:7}]|" "$DISPOSITION"
done < /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log
rm -f "$DISPOSITION.bak"
git diff "$DISPOSITION"
```

Expected: each executed branch's line now ends with `[EXECUTED 2026-04-07 tip=abc1234]`.

- [ ] **Step 7: Commit the disposition update**

```bash
git add "$DISPOSITION"
git commit -m "$(cat <<MSG
chore(housekeeping): execute disposition for ${#APPROVED[@]} approved branches

Acted on user sign-off marks in branch-disposition.md. Each approved
branch was archived as a tag (refs/tags/archive/<branch>) and deleted
from origin. Updated the report with [EXECUTED $TODAY] markers.

Approved this round: ${#APPROVED[@]} of 11 branches.
EXTRACT applied: $EXTRACT_APPROVED
MSG
)"
git log --oneline -8
```

Expected: a commit advancing the rollup. If EXTRACT was applied, this is one commit AFTER the extraction commit from Step 4.

---

## Task 8: Commit #N — Close Session Log + Section-2 Invariant Check

**Files:**

- Create: `docs/reports/housekeeping-2026-04-07/session-log.md`

- [ ] **Step 1: Source helpers, confirm worktree position**

```bash
source /tmp/housekeeping-2026-04-07/helpers.sh
cd "$WORKTREE"
```

- [ ] **Step 2: Run the Section-2 invariant check (CRITICAL — STOPS the rollup if it fails)**

```bash
verify_section_2_invariant
EXIT=$?
if [ "$EXIT" -ne 0 ]; then
  echo "================================="
  echo "ABORT: Section-2 invariant violated"
  echo "================================="
  echo "The primary working copy was modified during the housekeeping session."
  echo "Worktree is preserved at $WORKTREE for inspection."
  echo "Do NOT open a PR. Investigate the drift, then either:"
  echo "  1. Resolve the drift and re-run Task 8, or"
  echo "  2. Abandon the rollup with: git worktree remove --force $WORKTREE"
  exit 1
fi
echo "Section-2 invariant: OK"
```

Expected: `OK: Section-2 invariant preserved` and `Section-2 invariant: OK`. If FAIL: STOP. Do NOT commit. Do NOT open a PR. The worktree stays for forensic inspection.

- [ ] **Step 2b: Final build/test/fantomas verification (spec §13 items 5-7)**

Re-run the three STOP-class checks against the worktree head to confirm the rollup still builds clean. These duplicate the per-task verifications but the spec requires a single-pass verification at close time.

```bash
# Build
dotnet build ACP-inspector.slnx --no-restore 2>&1 | tee /tmp/housekeeping-2026-04-07/close-build.log | tail -5
BUILD_EXIT=$?

# Tests
dotnet test sentinel/tests/ACP.Tests.fsproj --no-build --nologo --logger "console;verbosity=minimal" 2>&1 | tee /tmp/housekeeping-2026-04-07/close-test.log | tail -5
TEST_EXIT=$?

# Fantomas --check on the eligible set (non-bucket-B should now be clean after Task 3)
if [ -s /tmp/housekeeping-2026-04-07/fantomas-eligible-live.txt ]; then
  xargs -a /tmp/housekeeping-2026-04-07/fantomas-eligible-live.txt dotnet fantomas --check 2>&1 | tee /tmp/housekeeping-2026-04-07/close-fantomas.log | tail -5
  FANTOMAS_EXIT=$?
else
  echo "(no eligible files — skip fantomas --check)"
  FANTOMAS_EXIT=0
fi

echo "BUILD_EXIT=$BUILD_EXIT  TEST_EXIT=$TEST_EXIT  FANTOMAS_EXIT=$FANTOMAS_EXIT"

if [ "$BUILD_EXIT" -ne 0 ] || [ "$TEST_EXIT" -ne 0 ] || [ "$FANTOMAS_EXIT" -ne 0 ]; then
  echo "ABORT: final verification failed. Do not commit session log. Do not open PR." >&2
  exit 1
fi
echo "Final verification: OK"
```

Expected: all three exit 0, prints `Final verification: OK`. If any fail: STOP. Do NOT create the session log or the PR. Investigate the failure in the worktree. (The per-task verifications should have caught this earlier — a failure here means something drifted between tasks.)

- [ ] **Step 3: Compose the session log**

```bash
COMMITS_IN_ROLLUP=$(git log --oneline origin/master..HEAD | wc -l)
DRIFT_LOG_EXISTS=$(test -f /tmp/housekeeping-2026-04-07/bucket-b-drift.log && echo "yes" || echo "no")
DRIFT_EVENTS=0
if [ "$DRIFT_LOG_EXISTS" = "yes" ]; then
  DRIFT_EVENTS=$(grep -c '^=== bucket-B drift detected' /tmp/housekeeping-2026-04-07/bucket-b-drift.log || echo 0)
fi

cat > docs/reports/housekeeping-2026-04-07/session-log.md <<EOF
# Housekeeping Session Log

**Date:** $(date -u +%Y-%m-%dT%H:%M:%SZ)
**Branch:** \`chore/housekeeping-2026-04-07\`
**Spec:** \`docs/superpowers/specs/2026-04-07-repo-housekeeping-design.md\` (refined at \`1ace255\`)
**Commits in rollup:** $COMMITS_IN_ROLLUP

## Section-2 invariant check

- Primary status drift: NONE
- Primary diff content hash: UNCHANGED
- Primary HEAD: UNCHANGED (still \`$(cat /tmp/housekeeping-2026-04-07/head-before.sha)\`)

## Final verification (spec §13)

- dotnet build:        PASS
- dotnet test:         PASS
- dotnet fantomas --check (non-bucket-B): PASS
- Commit count in rollup: $COMMITS_IN_ROLLUP (target 7-10, max 14)
- All 7 merged-branch archive tags present on origin: verified in Task 5 Step 4
- Disposition report has entry per unmerged branch: verified in Task 6 Step 3

## Bucket-B drift events during the session

- Drift log present: $DRIFT_LOG_EXISTS
- Drift events recorded: $DRIFT_EVENTS

$(if [ "$DRIFT_LOG_EXISTS" = "yes" ] && [ "$DRIFT_EVENTS" -gt 0 ]; then echo "See \`bucket-b-drift.log\` (committed below) for the per-event details."; else echo "No drift detected. The user did not modify any primary-copy files during the session."; fi)

## Commits in this rollup

\`\`\`
$(git log --oneline origin/master..HEAD)
\`\`\`

## Branches deleted

### From §10.A (merged-into-master, 7 branches)

\`\`\`
$(cat /tmp/housekeeping-2026-04-07/archive-tags-pushed/merged.log 2>/dev/null || echo "(none)")
\`\`\`

### From §10.B (per-branch sign-off, $([ -f /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log ] && wc -l < /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log || echo 0) branches)

\`\`\`
$(cat /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log 2>/dev/null || echo "(none)")
\`\`\`

## Session verdict

PASS — Section-2 invariant preserved. Ready to open PR.
EOF
wc -l docs/reports/housekeeping-2026-04-07/session-log.md
```

Expected: a session log with all the key facts.

- [ ] **Step 4: If a drift log exists, copy it into the worktree for committing**

```bash
if [ -f /tmp/housekeeping-2026-04-07/bucket-b-drift.log ]; then
  cp /tmp/housekeeping-2026-04-07/bucket-b-drift.log docs/reports/housekeeping-2026-04-07/bucket-b-drift.log
  git add docs/reports/housekeeping-2026-04-07/bucket-b-drift.log
fi
```

Expected: drift log committed alongside session log if drift occurred; otherwise no-op.

- [ ] **Step 5: Commit the session log**

```bash
git add docs/reports/housekeeping-2026-04-07/session-log.md
git commit -m "$(cat <<MSG
chore(housekeeping): close session log

Final session report. Section-2 invariant verified — primary working copy
is byte-identical to session start. Ready for PR.

Total commits in rollup: $COMMITS_IN_ROLLUP
Drift events: $DRIFT_EVENTS
MSG
)"
git log --oneline -10
```

Expected: the close commit advances the rollup. Total commit count should match the spec's prediction (7-10 base + revivals).

---

## Task 9: Open the Pull Request

**No file changes** — only `gh pr create`.

- [ ] **Step 1: Source helpers, confirm worktree, push the branch**

```bash
source /tmp/housekeeping-2026-04-07/helpers.sh
cd "$WORKTREE"
git push -u origin chore/housekeeping-2026-04-07
```

Expected: `Branch 'chore/housekeeping-2026-04-07' set up to track remote branch ...`. The branch now exists on origin.

- [ ] **Step 2: Compose the PR body**

```bash
cat > /tmp/housekeeping-2026-04-07/pr-body.md <<EOF
## Summary

Repo-wide housekeeping pass per \`docs/superpowers/specs/2026-04-07-repo-housekeeping-design.md\`. Single mega-rollup of $(git log --oneline origin/master..HEAD | wc -l) commits, executed in an isolated worktree to preserve the in-flight ACP 0.10.5 → 0.11.3 schema upgrade in the primary working copy.

### Reports (read these in order)

1. \`docs/reports/housekeeping-2026-04-07/baseline.md\` — classified gate findings
2. \`docs/reports/housekeeping-2026-04-07/audit-001-delta.md\` — diff vs the 2026-01-06 audit
3. \`docs/reports/housekeeping-2026-04-07/merged-branches-deleted.md\` — §10.A deletion log
4. \`docs/reports/housekeeping-2026-04-07/branch-disposition.md\` — §10.B sign-off and execution log
5. \`docs/reports/housekeeping-2026-04-07/session-log.md\` — final invariant check and rollup summary
$(if [ -f docs/reports/housekeeping-2026-04-07/bucket-b-drift.log ]; then echo "6. \`docs/reports/housekeeping-2026-04-07/bucket-b-drift.log\` — Layer-2 guard events"; fi)

### Operational changes

- 7 merged branches deleted from origin (with archive tags). See report 3.
- $([ -f /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log ] && wc -l < /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log || echo 0) unmerged branches deleted with per-branch sign-off (with archive tags). See report 4.
- 3 dead \`core/\` references removed (\`.gitmodules\`, \`.gitignore\` line 23, \`EvidenceRunner.fs:8\` comment).
- Fantomas sweep on $(wc -l < /tmp/housekeeping-2026-04-07/fantomas-changed-actual.txt 2>/dev/null || echo 0) non-bucket-B F# files.

## Unmerged branch sign-off surface (spec §13 item 14)

Per-branch status for the 11 branches in §10.B. Already-executed dispositions are marked \`[EXECUTED]\`; pending ones still need sign-off in a follow-up session (mark the box in \`docs/reports/housekeeping-2026-04-07/branch-disposition.md\` and re-run Task 7 post-merge).

### ARCHIVE-SQUASH-MERGED (work already in master)

- [$([ -f /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log ] && grep -q '^docs/overview-image-and-task-index ' /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log && echo 'x' || echo ' ')] \`docs/overview-image-and-task-index\` (PR #27)
- [$([ -f /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log ] && grep -q '^docs/remove-mermaid-diagrams ' /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log && echo 'x' || echo ' ')] \`docs/remove-mermaid-diagrams\` (PR #28)
- [$([ -f /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log ] && grep -q '^fix/remove-mistaken-image ' /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log && echo 'x' || echo ' ')] \`fix/remove-mistaken-image\` (image swap)

### ARCHIVE (work unrecoverable or superseded)

- [$([ -f /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log ] && grep -q '^add-claude-github-actions-1765859631445 ' /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log && echo 'x' || echo ' ')] \`add-claude-github-actions-1765859631445\`
- [$([ -f /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log ] && grep -q '^copilot/sub-pr-17 ' /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log && echo 'x' || echo ' ')] \`copilot/sub-pr-17\`
- [$([ -f /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log ] && grep -q '^fix/compliance-feedback ' /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log && echo 'x' || echo ' ')] \`fix/compliance-feedback\`
- [$([ -f /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log ] && grep -q '^chore/2025-12-16 ' /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log && echo 'x' || echo ' ')] \`chore/2025-12-16\`
- [$([ -f /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log ] && grep -q '^feat/bounded-contexts-and-codec-refactor ' /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log && echo 'x' || echo ' ')] \`feat/bounded-contexts-and-codec-refactor\`
- [$([ -f /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log ] && grep -q '^codex/make-acp-inspector-valuable ' /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log && echo 'x' || echo ' ')] \`codex/make-acp-inspector-valuable\`
- [$([ -f /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log ] && grep -q '^codex/sub-pr-35 ' /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log && echo 'x' || echo ' ')] \`codex/sub-pr-35\`

### EXTRACT-CANDIDATE (pending content review)

- [$([ -f /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log ] && grep -q '^docs/linear-migration-planning ' /tmp/housekeeping-2026-04-07/archive-tags-pushed/unmerged.log && echo 'x' || echo ' ')] \`docs/linear-migration-planning\` — read \`docs/planning/linear-migration.md\` from the branch before deciding

## Test plan

- [ ] \`dotnet build ACP-inspector.slnx\` succeeds with 0 warnings
- [ ] \`dotnet test sentinel/tests/ACP.Tests.fsproj\` passes
- [ ] \`dotnet test sentinel/tests/Epistemology.Harness/\` passes
- [ ] \`dotnet test sentinel/tests/Validation.Harness/\` passes
- [ ] \`dotnet test sentinel/tests/SDK.Harness/\` passes
- [ ] \`dotnet fantomas --check\` passes on all non-bucket-B files
- [ ] Spot-check that \`.gitmodules\` deletion or edit doesn't break submodule consumers (none in this repo)
- [ ] Spot-check the disposition report archive tags are present on origin
- [ ] After merge: \`git pull --rebase\` in the primary working copy still leaves your in-flight bucket-B work intact

## Invariant verified

Section 2 of the spec ("byte-identical primary working copy") was verified by Task 8's close commit:
- \`git status --porcelain=v2\` unchanged
- \`git diff | sha256sum\` unchanged
- \`HEAD\` SHA unchanged

Your in-flight schema-upgrade work is untouched.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
cat /tmp/housekeeping-2026-04-07/pr-body.md
```

- [ ] **Step 3: Open the PR**

```bash
gh pr create \
  --base master \
  --head chore/housekeeping-2026-04-07 \
  --title "chore: repo-wide housekeeping and non-codec revivals" \
  --body "$(cat /tmp/housekeeping-2026-04-07/pr-body.md)"
```

Expected: PR URL printed. Save it.

- [ ] **Step 4: Verify the PR is open and CI is queued**

```bash
gh pr view --json url,state,statusCheckRollup
```

Expected: `state: OPEN` and either CI queued or no required checks. Surface the PR URL to the user.

- [ ] **Step 5: Print the final summary**

```bash
PR_URL=$(gh pr view --json url -q .url)
echo "================================="
echo "HOUSEKEEPING ROLLUP COMPLETE"
echo "================================="
echo
echo "PR:        $PR_URL"
echo "Branch:    chore/housekeeping-2026-04-07"
echo "Commits:   $(git log --oneline origin/master..HEAD | wc -l)"
echo "Worktree:  $WORKTREE (preserved until you merge or abandon)"
echo
echo "Next steps:"
echo "  1. Review the PR (especially the 5 reports under docs/reports/housekeeping-2026-04-07/)."
echo "  2. Merge the PR when ready."
echo "  3. After merge, clean up the worktree:"
echo "       cd $PRIMARY"
echo "       git worktree remove $WORKTREE"
echo "       git pull --rebase  # picks up the merged rollup"
echo "  4. Your in-flight schema-upgrade work in the primary copy is unchanged."
echo
echo "Session scratch will be cleaned up automatically by /tmp policy. To delete now:"
echo "  rm -rf /tmp/housekeeping-2026-04-07"
```

Expected: a summary block with the PR URL and next-step instructions.

---

## Task Dependency Graph

```
Task 0 (pre-flight + worktree)
  └─→ Task 1 (baseline.md, classified gate)
        └─→ Task 2 (audit-001 delta)
              └─→ Task 3 (Fantomas sweep, may be no-op)
                    └─→ Task 4 (dead core/ refs)
                          └─→ Task 5 (delete merged §10.A branches)
                                └─→ Task 6 (disposition report) ─── STOP for sign-off
                                                                       │
                                  ┌────────────────────────────────────┘
                                  ↓
                                Task 7 (execute approved §10.B dispositions)
                                  └─→ Task 8 (close + Section-2 invariant)
                                        └─→ Task 9 (PR creation)
```

The only cross-task dependency that isn't strictly sequential is **Task 7** waits on **user input**, not on Task 6's commit. Once Task 6 commits, the executor halts and asks the user to fill out the report. When the user replies with sign-off, Task 7 proceeds. Tasks 8 and 9 then run automatically.

---

## Recovery Procedures

**If Task 0 fails** (worktree creation, fetch, snapshot):

- No state has been committed. Just retry after fixing the cause.

**If Task 1 STOP-class fails** (build error, test failure):

- Worktree exists but has no commits yet. Investigate the underlying cause in the worktree. Once fixed, drop the worktree (`git worktree remove --force ../ACP-inspector-housekeeping`) and start over from Task 0. This is rare and indicates the master branch is broken — fix that first.

**If Task 3 fails** (Fantomas breaks build):

- `git checkout -- .` in the worktree to revert. Investigate which file Fantomas mangled. File a Fantomas bug if reproducible. Skip Task 3 and proceed to Task 4 — record the skip in `session-log.md`.

**If Task 5 partially fails** (some branches archived but not deleted, or vice versa):

- Re-run Step 3 (the loop) — it idempotently checks `git ls-remote --exit-code` before acting on each branch.

**If Task 6's STOP gets ignored and Task 7 runs prematurely**:

- Task 7's Step 1 reads the disposition file. If no boxes are checked, Task 7 is a no-op. No harm done.

**If Task 8's invariant check fails**:

- The worktree commits are intact but the rollup CANNOT be merged as-is — it would violate the user's expectation that their primary copy is untouched. Investigate the drift in the primary copy. If the user intentionally edited primary-copy files during the session, the resolution is to either (a) take a new snapshot and re-verify, or (b) abandon the rollup. **Default: abandon and surface the drift to the user**, because silent invariant violations are how trust gets broken.

**If Task 9's `gh pr create` fails**:

- Check `gh auth status`. If auth is fine, the branch may already have a PR (idempotency check: `gh pr list --head chore/housekeeping-2026-04-07`). If a PR exists, update its body with `gh pr edit`.

---

## Post-Merge Cleanup

After the PR is merged (manually by the user, not by this plan):

1. The user runs `git pull --rebase` in the primary working copy. This advances HEAD to include the rollup. The bucket-B working tree state is preserved by the rebase.
2. The user (or this plan's recovery procedure) removes the worktree:

   ```bash
   git worktree remove /Users/stas-studio/Developer/ACP-inspector-housekeeping
   ```

3. The local `chore/housekeeping-2026-04-07` branch is deleted automatically by the worktree removal.
4. The scratch directory `/tmp/housekeeping-2026-04-07/` can be deleted (`rm -rf`) or left for `/tmp` cleanup.

After cleanup, the primary working copy still has the user's in-flight schema upgrade staged exactly as before, but now sitting on top of the merged rollup commit. The user can continue their schema-upgrade work without any reconciliation.
