# ACP Inspector tooling

This document collects repo-local operational notes (paths, build/run commands, and code pointers).

## Projects

- Protocol core: `protocol/src/ACP.fsproj`
- Runtime SDK: `runtime/src/ACP.fsproj`
- Sentinel validation: `sentinel/src/ACP.fsproj`
- Unified CLI: `cli/apps/ACP.Cli/ACP.Cli.fsproj`
- Tests: `sentinel/tests/`

## Common commands

### Unified CLI

The ACP Inspector CLI (`acp-inspector`) provides inspection, validation, replay, analysis, and benchmarking capabilities:

```bash
# Build the CLI
dotnet build cli/apps/ACP.Cli/ACP.Cli.fsproj -c Release

# Inspect trace files
dotnet run --project cli/apps/ACP.Cli -- inspect trace.jsonl

# Validate messages from stdin
cat messages.json | dotnet run --project cli/apps/ACP.Cli -- validate --direction c2a

# Interactive replay
dotnet run --project cli/apps/ACP.Cli -- replay --interactive trace.jsonl

# Statistical analysis
dotnet run --project cli/apps/ACP.Cli -- analyze trace.jsonl

# Benchmark performance
dotnet run --project cli/apps/ACP.Cli -- benchmark --mode throughput --count 1000

# Help
dotnet run --project cli/apps/ACP.Cli -- --help
```

### SDK

```bash
# build protocol/runtime/sentinel libraries
dotnet build protocol/src/ACP.fsproj
dotnet build runtime/src/ACP.fsproj
dotnet build sentinel/src/ACP.fsproj
```

### Tests

```bash
# run all tests
dotnet test sentinel/tests/ACP.Tests.fsproj -c Release

# run specific test category
dotnet test sentinel/tests/ACP.Tests.fsproj --filter "FullyQualifiedName~Codec"
```

## Docs layout

- Docs live under `docs/`.
- Tooling/how-to-run notes live under `docs/tooling/`.
