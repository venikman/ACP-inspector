# ACP Inspector CLI (`acp-inspector`)

CLI tooling for ACP Inspector inspect/validate/replay/analyze/benchmark workflows.

## Build

dotnet build cli/apps/ACP.Cli/ACP.Cli.fsproj -c Release

## CI policy

This repo intentionally does not include Azure Pipelines YAML. CI is configured externally; do not add `azure-pipelines.yml` here.

## Examples

dotnet run --project cli/apps/ACP.Cli -- inspect trace.jsonl
dotnet run --project cli/apps/ACP.Cli -- validate --direction c2a < messages.json
dotnet run --project cli/apps/ACP.Cli -- replay --interactive trace.jsonl
dotnet run --project cli/apps/ACP.Cli -- analyze trace.jsonl
dotnet run --project cli/apps/ACP.Cli -- benchmark --mode throughput --count 1000
