# ACP-sentinel tooling

This document collects repo-local operational notes (paths, build/run commands, and code pointers).

## Projects

- SDK/library: `src/ACP.fsproj`
- Unified CLI: `apps/ACP.Cli/ACP.Cli.fsproj`
- Tests: `tests/`

## Common commands

### Unified CLI

The `acp-cli` tool provides inspection, validation, replay, analysis, and benchmarking capabilities:

```bash
# Build the CLI
dotnet build apps/ACP.Cli/ACP.Cli.fsproj -c Release

# Inspect trace files
dotnet run --project apps/ACP.Cli -- inspect trace.jsonl

# Validate messages from stdin
cat messages.json | dotnet run --project apps/ACP.Cli -- validate --direction c2a

# Interactive replay
dotnet run --project apps/ACP.Cli -- replay --interactive trace.jsonl

# Statistical analysis
dotnet run --project apps/ACP.Cli -- analyze trace.jsonl

# Benchmark performance
dotnet run --project apps/ACP.Cli -- benchmark --mode throughput --count 1000

# Help
dotnet run --project apps/ACP.Cli -- --help
```

### SDK

```bash
# restore + build the SDK
dotnet build src/ACP.fsproj
```

### Tests

```bash
# run all tests
dotnet test

# run specific test category
dotnet test --filter "FullyQualifiedName~Codec"
```

## Docs layout

- Normative specs live under `docs/fpf/` (FPF) and `docs/spf/` (SPF).
- `docs/spec/` contains compatibility pointers for older links.
- Tooling/how-to-run notes live under `docs/tooling/`.
