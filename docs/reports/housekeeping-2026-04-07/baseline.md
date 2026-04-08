# Housekeeping Baseline

- Date: 2026-04-07
- Branch: `codex/fpf-housekeeping-2026-04-07`
- Base commit: `e6a53cee53cecdf606d45638d07b10e463642ab5`
- Lens: March 2026 FPF routing from `/Users/stas-studio/Downloads/FPF-main.zip`

## Scope

This pass preserves live code, tests, docs, and examples. It removes only dead or misleading residue and records current repository health without changing public ACP shapes.

The primary checkout at `/Users/stas-studio/Developer/ACP-inspector` was treated as protected state. At session start it already contained these untracked entries and they were left untouched:

- `.local/`
- `Library/`

## Measured Baseline

### .NET tools

Command:

```bash
env DOTNET_CLI_HOME=/tmp/acp-dotnet-home HOME=/tmp/acp-home \
  NUGET_PACKAGES=/tmp/acp-nuget-packages \
  NUGET_HTTP_CACHE_PATH=/tmp/acp-nuget-http \
  dotnet tool restore
```

Result:

- `fantomas` 7.0.5 restored successfully from the local tool manifest.
- `fsdocs-tool` 21.0.0 restored successfully from the local tool manifest.

### Build

Command:

```bash
env DOTNET_CLI_HOME=/tmp/acp-dotnet-home HOME=/tmp/acp-home \
  NUGET_PACKAGES=/tmp/acp-nuget-packages \
  NUGET_HTTP_CACHE_PATH=/tmp/acp-nuget-http \
  dotnet build ACP-inspector.slnx --nologo
```

Result:

- Build succeeded.
- Warnings: `0`
- Errors: `0`

### Tests

Command:

```bash
env DOTNET_CLI_HOME=/tmp/acp-dotnet-home HOME=/tmp/acp-home \
  NUGET_PACKAGES=/tmp/acp-nuget-packages \
  NUGET_HTTP_CACHE_PATH=/tmp/acp-nuget-http \
  dotnet test sentinel/tests/ACP.Tests.fsproj --no-build --nologo --verbosity quiet
```

Result:

- Passed: `396`
- Failed: `0`
- Skipped: `0`

### Fantomas

Command:

```bash
env DOTNET_CLI_HOME=/tmp/acp-dotnet-home HOME=/tmp/acp-home \
  NUGET_PACKAGES=/tmp/acp-nuget-packages \
  NUGET_HTTP_CACHE_PATH=/tmp/acp-nuget-http \
  dotnet fantomas protocol/src runtime/src sentinel/src sentinel/tests --check
```

Result before this housekeeping pass:

- `protocol/src/Acp.Protocol.fs`
- `protocol/src/Acp.Domain.fs`
- `runtime/src/Acp.Codec.AcpJson.fs`
- `runtime/src/Acp.Contrib.SessionState.fs`
- `sentinel/tests/Acp.Codec.Tests.fs`
- `sentinel/tests/Acp.SessionState.Tests.fs`
- `sentinel/tests/Acp.Connection.Tests.fs`
- `sentinel/tests/Pbt/Generators.fs`

### Link check

Command:

```bash
lychee --no-progress docs/ README.md
```

Result:

- Total references: `81`
- OK: `56`
- Errors: `0`
- Excluded: `25`

### Markdown lint

Command:

```bash
env HOME=/tmp/acp-bun-home bunx markdownlint-cli2 '**/*.md'
```

Result:

- `markdownlint-cli2` executed successfully through `bunx`.
- 25 pre-existing markdown issues were reported.
- Affected files:
  - `docs/superpowers/plans/2026-04-07-repo-housekeeping-plan.md`
  - `docs/superpowers/specs/2026-04-07-repo-housekeeping-design.md`
  - `docs/superpowers/specs/2026-04-07-scope-reduction-design.md`

## Dead Reference Targets

This housekeeping pass targets the following residue:

- Root-level submodule metadata for `core/roadmap/sub-ACP`, even though no live `core/` tree exists.
- Root-level ignore rule for `core/evidence/pbt/*latest-failure.json`, which no longer maps to a tracked repo path.
- A PBT runner doc comment that referenced the removed `core/evidence/pbt/` location directly.

## Baseline Verdict

- Repository health is green for build, tests, and link checks.
- Formatting drift is limited to eight F# files and is safe to auto-apply.
- Markdown lint debt remains in legacy planning/spec documents and is reported rather than expanded in this pass.
