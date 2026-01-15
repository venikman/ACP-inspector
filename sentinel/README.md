# ACP.Inspector (F#)

ACP.Inspector provides validation lanes, findings, assurance helpers, and the runtime adapter for ACP traffic.

## Build

dotnet build sentinel/src/ACP.fsproj

## Tests

dotnet test sentinel/tests/ACP.Tests.fsproj -c Release

## CI policy

This repo intentionally does not include Azure Pipelines YAML. CI is configured externally; do not add `azure-pipelines.yml` here.

## Notes

- Depends on ACP.Protocol.
- Observability tags are duplicated from ACP.Runtime to avoid a dependency edge.
