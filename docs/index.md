# ACP-Sentinel Documentation

ACP-Sentinel is an F# implementation of the Agent Client Protocol (ACP) with a validation and sentinel layer.

## Quick Links

- [API Reference](reference/index.html) - Generated API documentation
- [SDK Comparison](SDK-COMPARISON.html) - Comparison with official SDKs

## Modules

| Module | Description |
|--------|-------------|
| `Acp.Domain` | Core ACP types: sessions, messages, capabilities |
| `Acp.Codec` | JSON-RPC 2.0 encoding/decoding for ACP messages |
| `Acp.Protocol` | Protocol state machine and transitions |
| `Acp.Transport` | Transport abstractions (stdio, memory, duplex) |
| `Acp.Connection` | High-level client and agent connections |
| `Acp.Validation` | Runtime validation and sentinel findings |
| `Acp.RuntimeAdapter` | Runtime boundary validation helpers |
| `Acp.Contrib.SessionState` | Session state accumulation |
| `Acp.Contrib.ToolCalls` | Tool call lifecycle tracking |
| `Acp.Contrib.Permissions` | Permission request/response handling |

## Getting Started

```fsharp
#r "nuget: ACP.Sentinel"

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
