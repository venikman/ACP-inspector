# ACP.Protocol (F#)

ACP.Protocol is the protocol core for ACP: a typed domain model plus the protocol state machine.
It contains no IO, codec, or validation logic.

## Build

dotnet build protocol/src/ACP.fsproj

## CI policy

This repo intentionally does not include Azure Pipelines YAML. CI is configured externally; do not add `azure-pipelines.yml` here.

## Usage

```fsharp
open Acp.Domain
open Acp.Protocol

let spec = Protocol.spec
```
