# Coding Standards

This document defines the coding standards and style conventions for the ACP-inspector codebase.

---

## F# Style Guide

This repository follows the official F# style guide for naming, layout, and idioms.

**Source of Truth**: https://github.com/fsharp/fslang-design#style-guide

### Formatting

Code formatting is enforced with **Fantomas**.

**Format code:**

```bash
dotnet tool restore && dotnet fantomas src tests apps
```

**Check formatting (CI):**

```bash
dotnet tool restore && dotnet fantomas src tests apps --check
```

---

## Key Conventions

### Naming

- **Modules**: PascalCase (e.g., `Domain`, `Connection`, `SessionState`)
- **Types**: PascalCase (e.g., `SessionId`, `ProtocolVersion`)
- **Functions**: camelCase (e.g., `validateMessage`, `parseFrame`)
- **Constants**: camelCase with descriptive names (e.g., `current`, `supported`)

### Module Organization

```fsharp
namespace Acp

// 1. Domain types
module Domain =
    type SessionId = SessionId of string

// 2. Module-scoped helpers
[<RequireQualifiedAccess>]
module SessionId =
    let value (SessionId s) = s
    let newId() = SessionId (Guid.NewGuid().ToString())

// 3. Public API
module Connection =
    type ClientConnection(transport: ITransport) =
        member _.InitializeAsync(params) = task { ... }
```

### Type Design

**Single-case discriminated unions** for type safety:

```fsharp
type SessionId = SessionId of string  // Not just: type SessionId = string
type MessageId = MessageId of string
```

**Record types** for structured data:

```fsharp
type InitializeParams = {
    protocolVersion: ProtocolVersion
    clientCapabilities: ClientCapabilities
    clientInfo: ImplementationInfo option
}
```

**Discriminated unions** for variants:

```fsharp
type ContentBlock =
    | Text of TextContent
    | Image of ImageContent
    | Audio of AudioContent
```

### Async Patterns

Use **task computation expressions** (.NET 10):

```fsharp
member _.InitializeAsync(params) = task {
    let! response = transport.SendAsync(request)
    return response.result
}
```

### Error Handling

Use **Result types** for expected failures:

```fsharp
type ValidationResult =
    | Valid
    | Invalid of ValidationError list

let validate (message: JsonNode) : ValidationResult =
    // ...
```

Use **exceptions** only for unexpected failures.

### Option Handling

Prefer **Option module** functions:

```fsharp
// Good
sessionId |> Option.map SessionId.value |> Option.defaultValue "(none)"

// Avoid
match sessionId with
| Some id -> SessionId.value id
| None -> "(none)"
```

### Pipeline Style

Use **forward pipe** for data flow:

```fsharp
messages
|> List.filter (fun m -> m.direction = "c2a")
|> List.map parseMessage
|> List.choose Result.toOption
```

---

## Testing Standards

### Test Naming

Use pattern: `Method_Condition_Expected`

```fsharp
[<TestMethod>]
member _.SessionId_RoundTrip_PreservesValue() =
    // Arrange
    let original = SessionId.newId()

    // Act
    let json = Codec.encode original
    let decoded = Codec.decode json

    // Assert
    decoded.Should().Be(original)
```

### Test Organization

```fsharp
namespace Acp.Tests.Unit

[<TestClass>]
type SessionStateTests() =

    [<TestMethod>]
    member _.Apply_NewSession_CreatesSnapshot() = ...

    [<TestMethod>]
    member _.Apply_MultipleNotifications_MergesState() = ...
```

### Property-Based Testing

Use **FsCheck** for invariant testing:

```fsharp
[<Property>]
let ``SessionId roundtrip preserves value`` (id: SessionId) =
    let encoded = Codec.encode id
    let decoded = Codec.decode encoded
    decoded = id
```

---

## Documentation Standards

### XML Documentation

Document public APIs:

```fsharp
/// Initializes a new ACP session with the agent.
/// Returns the session ID and agent capabilities.
member _.InitializeAsync(params: InitializeParams) : Task<InitializeResult> =
    // ...
```

### Code Comments

Use comments sparingly - prefer self-documenting code:

```fsharp
// Good: Intent is clear from names
let validMessages = messages |> List.filter isValid

// Avoid: Redundant comment
// Filter for valid messages
let validMessages = messages |> List.filter isValid
```

Use comments for **non-obvious decisions**:

```fsharp
// Match ACP spec 0.10.x: maxTokens is optional, defaults to None
let maxTokens = json.TryGetProperty("maxTokens") |> Option.ofObj
```

### Documentation Files

**DO NOT create these files**:

- `WORK-SUMMARY.md` - Work tracking belongs in git history and PRs
- `TODO.md` - Use GitHub Issues instead
- `CHANGELOG.md` - Use git log and release notes
- Session summaries or progress tracking files

**Rationale**:

- Git history provides complete work tracking
- These files go stale and create maintenance burden
- Information duplicates what's already in commits/PRs/issues

**DO create**:

- `README.md` - Project overview and getting started
- `docs/**/*.md` - Architecture, specifications, guides
- API documentation from XML comments

---

## File Organization

### Source Structure

```
src/
├── Acp.Domain.fs           # Core types and protocol model
├── Acp.Codec.fs            # JSON encoding/decoding
├── Acp.Validation.fs       # Protocol validation
├── Acp.Connection.fs       # Client/Agent connection layer
└── Acp.Contrib.*.fs        # Optional contrib modules
```

### Test Structure

```
tests/
├── Unit/                   # Unit tests (fast, deterministic)
├── Pbt/                    # Property-based tests (FsCheck)
└── Integration/            # Integration tests (if needed)
```

---

## FPF Alignment

These coding standards align with FPF patterns:

- **E.8 Authoring Conventions**: This document
- **A.1 Holonic Foundation**: Type composition guidelines
- **A.2 Role Taxonomy**: Module organization by role
- **A.10 Evidence Graph**: Test naming and PBT evidence

See `docs/spec/fpf/FPF-Spec.md` for architectural patterns.

---

## Tools

### Required

- **.NET 10 SDK**: `dotnet --version`
- **Fantomas**: `dotnet tool restore`
- **FsCheck**: Included in test dependencies

### Recommended

- **Ionide** (VS Code): F# language support
- **Rider** or **Visual Studio**: Full IDE support

---

## Enforcement

1. **Fantomas**: Runs in CI pipeline (`--check` mode)
2. **Tests**: All tests must pass (140+ tests)
3. **Code Review**: PR reviews check adherence to standards

---

**Last Updated**: 2025-12-16
**Version**: 1.0
