# CI Integration

ACP Inspector is designed to be CI-friendly:

- Exit codes are non-zero on decode/validation failures.
- ANSI color sequences are disabled automatically when output is redirected, and when `NO_COLOR` is set.

## Validate A Trace File

```bash
dotnet build cli/apps/ACP.Cli/ACP.Cli.fsproj -c Release
dotnet run --project cli/apps/ACP.Cli -c Release --no-build -- inspect path/to/trace.jsonl
```

## Validate Messages From Stdin

```bash
cat messages.json | dotnet run --project cli/apps/ACP.Cli -c Release --no-build -- validate --direction c2a
```

## Color-Free Output (Log Parsing)

```bash
NO_COLOR=1 dotnet run --project cli/apps/ACP.Cli -c Release --no-build -- inspect path/to/trace.jsonl > /tmp/inspect.log
```

## Suggested CI Entry Point

If you want an easy "is this repo healthy" check (build + smoke + regressions), run:

```bash
bash scripts/try.sh
```
