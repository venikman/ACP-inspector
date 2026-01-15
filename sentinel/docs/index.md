# ACP Inspector Documentation

![ACP Inspector overview](assets/acp-inspector-overview.jpg)

ACP Inspector is an F# implementation of the Agent Client Protocol (ACP) with a validation and sentinel layer.

Naming: Product = ACP Inspector, repo = `ACP-inspector`, CLI tool = `acp-inspector`, SDK package = `ACP.Inspector`.

## Quick Links

- [ACP RFD Tracker](ACP-RFD-TRACKER.md) - Draft RFD implementation status and spec tracking
- [API Reference](#building-docs) - Generated API documentation (output: `docs/_site/reference/index.html`)

## Modules

| Module                     | Description                                      |
| -------------------------- | ------------------------------------------------ |
| `Acp.Domain`               | Core ACP types: sessions, messages, capabilities |
| `Acp.Codec`                | JSON-RPC 2.0 encoding/decoding for ACP messages  |
| `Acp.Protocol`             | Protocol state machine and transitions           |
| `Acp.Transport`            | Transport abstractions (stdio, memory, duplex)   |
| `Acp.Connection`           | High-level client and agent connections          |
| `Acp.Validation`           | Runtime validation and sentinel findings         |
| `Acp.RuntimeAdapter`       | Runtime boundary validation helpers              |
| `Acp.Contrib.SessionState` | Session state accumulation                       |
| `Acp.Contrib.ToolCalls`    | Tool call lifecycle tracking                     |
| `Acp.Contrib.Permissions`  | Permission request/response handling             |

## Getting Started

```fsharp
#r "nuget: ACP.Inspector"

open Acp
open Acp.Domain
open Acp.Transport
open Acp.Connection

// Create a duplex transport pair for testing
let clientTransport, agentTransport = DuplexTransport.CreatePair()

// Set up a client
let client = ClientConnection(clientTransport)
```

## Building Docs

```bash
# Generate API docs
dotnet fsdocs build --input docs --output docs/_site

# Watch mode for development
dotnet fsdocs watch --input docs
```
