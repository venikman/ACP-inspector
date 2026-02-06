# ExecPlan: Make ACP Inspector Valuable (OSS-Ready, 2-Minute Try, CI-Friendly)

## Goal
Ship a trustworthy, easy-to-try ACP Inspector:

- A new user can try it end-to-end in **<= 2 minutes** (assuming .NET 10 installed).
- CI/log users get **machine-friendly output** (no ANSI when redirected) + correct exit codes.
- Installation without building from source via **GitHub Releases** prebuilt binaries.

## Success criteria (observable)

- From repo root: `dotnet build -c Release` succeeds.
- From repo root: `bash scripts/try.sh` exits `0` in <= ~2 minutes on a clean machine (with .NET 10).
- Redirected output contains no ANSI escape codes:
  - `bash cli/scripts/cli-smoke.sh > /tmp/acp-smoke.txt` then `rg $'\\x1b\\[' /tmp/acp-smoke.txt` has no matches.
- `inspect --record <path>` creates a valid JSONL trace that can be re-inspected successfully.
- `inspect --stop-on-error` truly stops on the first frame/direction/decode error (not just prints a message).
- A `LICENSE` file exists and matches the project’s MIT license claims.
- A GitHub Release workflow produces self-contained binaries for macOS/Linux/Windows.

## Non-goals

- No protocol semantic changes (ACP spec/schema remains the external source of truth).
- No NuGet publishing this sprint.
- No repo split/extraction work (monorepo stays).
- No long-running foreground servers (use timeouts / scripts).

## Constraints (sandbox, network, OS, time, dependencies)

- OS: macOS local; release artifacts must work on Linux/Windows.
- .NET SDK: pinned by `global.json` to `10.0.101`.
- Network: avoid unless required. Link checking uses `lychee.toml` (offline `file://` scheme).
- Safety: avoid destructive shell commands; use git-tracked deletions so rollback is `git revert`.

## Repo map (key files/dirs)

- Root solution: `ACP-inspector.slnx`
- CLI:
  - `cli/apps/ACP.Cli/ACP.Cli.fsproj`
  - `cli/apps/ACP.Cli/Common/Output.fs`
  - `cli/apps/ACP.Cli/Commands/InspectCommand.fs`
  - `cli/scripts/cli-smoke.sh`
- Docs:
  - `README.md`
  - `docs/` (canonical docs)
  - `docs/fpf/` (FPF artifacts)
- Work scripts:
  - `scripts/` (repo-root scripts)

## Milestones

### 0. Docs Coherence Cleanup (pre-work)

#### Why
Before implementing more behavior, ensure docs are consistent and not factually outdated (avoid “trust debt”).

#### Steps

- Make `docs/` canonical; remove duplicated `sentinel/docs/` tree (it already drifts).
- Delete outdated repo-evaluation docs (`docs/REPO-*.md` and any duplicates) per user request.
- Fix factual inconsistencies:
  - Remove/replace `#r "nuget: ACP.Inspector"` claims (packages are not on nuget.org).
  - Update any references that claim the CLI is `cli/apps/ACP.Inspector/` (unified CLI is `cli/apps/ACP.Cli/`).
- Make Markdown lint scope match intended docs scope:
  - Ensure `.codex/` is excluded from Markdownlint in CI (it is contributor tooling).
  - Ensure docs that remain pass Markdownlint.
- Add `scripts/docs_audit.sh` as an acceptance harness to prevent regressions:
  - Fail on `#r "nuget: ACP.Inspector"`.
  - Fail on `cli/apps/ACP.Inspector/` unless explicitly labeled “legacy”.

#### Validation

- `bunx markdownlint-cli2 --config .markdownlint-cli2.yaml README.md "docs/**/*.md" "cli/examples/**/*.md" "runtime/examples/**/*.md"`
  - Expected: exit `0`
- `lychee --config lychee.toml README.md docs/**/*.md cli/examples/**/*.md runtime/examples/**/*.md`
  - Expected: exit `0`
- `bash scripts/docs_audit.sh`
  - Expected: prints `OK` and exits `0`

#### Rollback
Revert the milestone commit(s) via `git revert <sha>`.

---

### 1. Make “dotnet build” Work From Repo Root

#### Why
`dotnet build -c Release` currently fails due to an ambiguity in `ACP-inspector.slnx`.

#### Steps

- Adjust `ACP-inspector.slnx` so `dotnet build -c Release` succeeds.
  - Remove the legacy `cli/apps/ACP.Inspector/ACP.Inspector.fsproj` from the solution (keep it on disk).
- Update `docs/fpf/contexts/cli-tooling-v1.md` to reflect the unified CLI and mark the legacy app as not part of the default build path.

#### Validation

- `dotnet build -c Release -v minimal`
  - Expected: exit `0`

#### Rollback
Revert the milestone commit(s) via `git revert <sha>`.

---

### 2. CI/Log-Friendly CLI Output (No ANSI When Redirected)

#### Why
The CLI currently emits ANSI sequences even when output is redirected, which breaks CI logs and parsing.

#### Steps

- Update `cli/apps/ACP.Cli/Common/Output.fs` so ANSI colors are gated by `Output.supportsColor()`.
- Ensure all helpers (`printSuccess`, `printError`, headings, key/value) respect the gating.

#### Validation

- `dotnet build cli/apps/ACP.Cli/ACP.Cli.fsproj -c Release`
  - Expected: exit `0`
- `bash cli/scripts/cli-smoke.sh > /tmp/acp-inspector-smoke.txt`
- `! rg -n $'\\x1b\\[' /tmp/acp-inspector-smoke.txt`
  - Expected: no matches

#### Rollback
Revert the milestone commit(s) via `git revert <sha>`.

---

### 3. Fix `inspect` Correctness (`--record`, `--stop-on-error`)

#### Why
`--record` is advertised but not implemented; `--stop-on-error` does not actually stop on decode errors.

#### Steps

- Implement `inspect --record <path>`:
  - Validate output path using `Security.validateOutputPath`.
  - Record only frames that decode and validate at the codec boundary.
  - Canonical output is JSONL frames with fields: `ts`, `direction`, `json` (string).
- Fix `inspect --stop-on-error` to stop the loop on:
  - invalid frame
  - unknown direction
  - codec decode error
- Add a small decode-error fixture trace under `cli/examples/cli-demo/`.
- Add `cli/scripts/cli-regressions.sh` to lock expected behavior.

#### Validation

- `dotnet build cli/apps/ACP.Cli/ACP.Cli.fsproj -c Release`
  - Expected: exit `0`
- `bash cli/scripts/cli-regressions.sh`
  - Expected: exits `0`

#### Rollback
Revert the milestone commit(s) via `git revert <sha>`.

---

### 4. 2-Minute Try Script + README Quick Start

#### Steps

- Add `scripts/try.sh`:
  - Build CLI release
  - Run `cli/scripts/cli-smoke.sh`
  - Run `cli/scripts/cli-regressions.sh`
- Update `README.md` to point to `bash scripts/try.sh` as the primary “try it now” path.
- Add minimal docs:
  - `docs/tooling/trace-format.md`
  - `docs/tooling/ci-integration.md`

#### Validation

- `bash scripts/try.sh`
  - Expected: exits `0`

#### Rollback
Revert the milestone commit(s) via `git revert <sha>`.

---

### 5. Distribution Via GitHub Releases (Prebuilt Binaries)

#### Steps

- Add `.github/workflows/release.yml` to publish self-contained, single-file binaries on `v*` tags.
- Update `README.md` “Install” section with GitHub Releases steps.

#### Validation

- Local publish (at least one RID):
  - `dotnet publish cli/apps/ACP.Cli/ACP.Cli.fsproj -c Release -r osx-arm64 --self-contained true /p:PublishSingleFile=true`
  - Expected: runnable output binary exists
- CI: tag-based release produces assets (manual verification).

#### Rollback
Revert the milestone commit(s) via `git revert <sha>`.

---

### 6. OSS Hygiene: License + Package Metadata Consistency

#### Steps

- Add `LICENSE` (MIT).
- Align NuGet metadata URLs in:
  - `protocol/src/ACP.fsproj`
  - `runtime/src/ACP.fsproj`
  - `sentinel/src/ACP.fsproj`
  so they reference this repo until/if it’s split.
- Optional: add `CONTRIBUTING.md` with the supported build/test/try path.

#### Validation

- `test -f LICENSE`
  - Expected: exit `0`
- `dotnet build -c Release`
  - Expected: exit `0`

#### Rollback
Revert the milestone commit(s) via `git revert <sha>`.

## Decisions log (why changes)

- Canonical docs live in `docs/` to avoid drift.
- `.codex/` is kept in-repo as contributor tooling but excluded from doc lint scope.
- Release distribution is via GitHub Releases this sprint (NuGet deferred).

## Progress log (ISO-8601 timestamps)

- 2026-02-06: Baseline observed: root `dotnet build -c Release` fails (solution ambiguity); CLI build + tests pass; docs contained outdated NuGet/CLI-path claims; markdownlint failed for docs scope.
- 2026-02-06T08:58:02Z: Completed Milestone 0 (docs coherence cleanup).
  - Validation:
    - `bunx markdownlint-cli2 --config .markdownlint-cli2.yaml README.md "docs/**/*.md" "cli/examples/**/*.md" "runtime/examples/**/*.md"` (0 errors)
    - `lychee --config lychee.toml README.md docs/**/*.md cli/examples/**/*.md runtime/examples/**/*.md` (0 errors)
    - `bash scripts/docs_audit.sh` (OK)
- 2026-02-06T09:04:37Z: Completed Milestone 1 (root `dotnet build -c Release` works).
  - Validation:
    - `dotnet build -c Release -v minimal` (exit 0; warnings only)
- Next: Milestone 2 (gate ANSI output in CLI; no color sequences when redirected).
