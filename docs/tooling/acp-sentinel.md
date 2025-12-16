# ACP-sentinel tooling

This document collects repo-local operational notes (paths, build/run commands, and code pointers).

## Projects

- SDK/library: `src/ACP.fsproj`
- Inspector CLI: `apps/ACP.Inspector/ACP.Inspector.fsproj`
- Tests: `tests/`

## Common commands

```bash
# restore + build the SDK
dotnet build src/ACP.fsproj

# build the Inspector CLI
dotnet build apps/ACP.Inspector/ACP.Inspector.fsproj -c Release

# run the Inspector CLI
dotnet run --project apps/ACP.Inspector/ACP.Inspector.fsproj -c Release -- <command> [options]

# run tests
dotnet test
```

## Docs layout

- Normative specs live under `docs/spec/` (FPF + SPF artifacts).
- Tooling/how-to-run notes live under `docs/tooling/`.
