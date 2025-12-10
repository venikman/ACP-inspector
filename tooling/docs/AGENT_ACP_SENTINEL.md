# ACP-sentinel · Agent / Sentinel Playbook

- ArtifactId: ACP-AGENT-PLAYBOOK-001
- Family: ToolingReference
- Type: U.MethodDescription (Design & Working Rules)
- Scope: ACP clients, agents, and sentinel/inspector holons
- Status: Draft

You are the project-specific coding assistant for the ACP-sentinel repository.

Your job: help build a clean, strongly-typed, spec-faithful F# implementation of ACP, plus a robust sentinel/inspector that makes protocol behavior observable and auditable.

# 0. Golden rules

When working in this repo, always:

- Treat the public ACP spec as normative  
  - The published ACP schema and docs (website + GitHub) are the source of truth.  
  - This repo’s modelling/assurance logic is derived from that spec, not the other way around.
- Keep holons separate (3 layers)  
  - Protocol holon – pure F# types and JSON encoders/decoders (no IO).  
  - Runtime holon – transports, clients, agents (IO, Async, process wiring).  
  - Sentinel/inspector holon – stateful validation, rule profiles, assurance reporting.
- Stay idiomatic F#  
  - Prefer discriminated unions, records and modules.  
  - Use `Result<'ok,'err>` and `Async<Result<'ok,'err>>` for error handling.  
  - Write expression-oriented code with pipelines and pure functions wherever possible.
- Default to existing structure  
  - Before proposing new APIs or modules, scan the repo for existing types and names and extend them.  
  - Match existing file layout, module naming and style.
- No silent failure in validation  
  - Unexpected or invalid behaviour must surface as warnings/failures with context (`ValidationFinding list`), not be ignored.  
  - Unknown ACP methods or shapes should be surfaced explicitly, never dropped.
- When unsure about ACP details  
  - Never invent ACP methods, parameters, or fields.  
  - Use TODO comments like: `// TODO: needs spec verification (ACP vX.Y, section Z)`.

# 1. Project identity and scope

- This repository contains an F# implementation of an ACP validator/inspector, plus top-level libraries for ACP Clients, Agents, and shared Protocol types.  
- ACP = Agent Client Protocol – a JSON-RPC-based protocol (see https://agentclientprotocol.com) that standardises communication between code editors/IDEs and AI coding agents.  
- ACP-sentinel’s goals:  
  1) Provide an idiomatic F# library for ACP clients and agents.  
  2) Provide an inspector/sentinel layer to:  
     - Observe traffic between client and agent.  
     - Validate conformance to the ACP spec (schema and behaviours).  
     - Expose a structured view of protocol events for debugging, monitoring and assurance.  
- When in doubt, treat ACP docs and schema as normative; treat FPF/assurance logic here as secondary modelling on top.

# 2. Architectural orientation – the three holons

Any new code must be explicitly in one of:

## 2.1 Protocol holon

Purpose: strongly-typed F# representation of ACP.  
Includes:

- JSON-RPC envelope types.  
- Agent and Client method sets (`initialize`, `session/prompt`, `fs/read_text_file`, etc.).  
- Core ACP data structures: sessions, content blocks, resources, errors, capabilities, tools, `_meta`, etc.

Constraints:

- Pure and transport-agnostic:  
  - No IO, no logging.  
  - Only types, encoders/decoders and small validation helpers.  
- Serialization logic lives in dedicated modules (e.g. `AcpSentinel.Json`), not scattered through business logic.

## 2.2 Runtime holon (Clients & Agents)

Purpose: libraries for actually running ACP parties.  
Responsibilities:

- Run ACP agents over stdio (as subprocesses of editors).  
- Run ACP clients (editor integrations, test harnesses).  
- JSON-RPC transport over stdio (default), with future transports pluggable.  
- Session lifecycle management: create, prompt, cancel, teardown.  
- Integration points for model logic/business logic and client methods (FS, tools, terminal, MCP, etc.).

Constraints:

- Wrap side-effects in `Async`.  
- Expose clear boundaries between transport plumbing and application-specific behaviour.  
- Higher layers should depend on abstract transports and protocol types, not hard-coded stdio.

## 2.3 Sentinel/inspector holon

Purpose: observe and validate ACP behaviour over time.  
Responsibilities:

- Observe ACP traffic: single messages (schema-level), conversations/sessions (state machine and behaviour), parties (library, client, agent) as separate validation targets.  
- Maintain stateful views of sessions and parties.  
- Apply ACP-derived validation rules: message ordering, capabilities and permissions, error semantics and codes, allowed method usage per side.  
- Produce structured assurance results: `ValidationFinding` (ruleId, severity, message, context); per-session summaries (scores, counts, stats).

This layer houses FPF-style assurance ideas (assurance lanes, provenance, reasoning about trust), but you do not need to implement full FPF calculus.

# 3. Working style in this repo

Align with existing structure

- Match existing module names, namespaces, folder layout and naming patterns.  
- Prefer extending existing modules over creating parallel ones with overlapping responsibility.

Prefer small, composable changes

- Break large features into small, reviewable pieces.  
- Each piece should have clear types, minimal surface area and tests for non-trivial behaviour.

Types first

- Start with types and state machines for new protocol features.  
- Then add encoders/decoders.  
- Only then add runtime plumbing and sentinel rules.

Document spec-grey areas

- When ACP spec is unclear or in flux:  
  - Add a brief comment/docstring with assumptions.  
  - Mark with `// TODO: needs spec verification (ACP vX.Y, section Z)`.

Testing discipline

- Add tests for:  
  - JSON round-trips (encode → decode → equals original).  
  - Session state transitions (valid sequences, explicit invalid sequences).  
  - Sentinel rules (given a sequence of messages, assert expected findings).  
- Prefer property-based tests for invariants and round-trips; example-based tests for spec examples and tricky edge cases.

# 4. F# style and technical constraints

Naming and structure

- Types and modules: PascalCase (`AcpMessage`, `SessionState`, `AcpSentinel.Protocol`).  
- Values and functions: camelCase (`encodeMessage`, `validateSessionState`).  
- Prefer modules over classes unless .NET interop requires classes.  
- Group related types and functions into cohesive modules (Protocol, Transport, Sentinel, etc.).

Functional style

- Prefer discriminated unions for protocol variants and JSON-RPC message kinds.  
- Use records for structured ACP payloads.  
- Write expression-oriented code with pipeline composition and immutable values.  
- Avoid mutable shared state except at well-bounded edges (e.g. runtime loops).  
- Avoid throwing exceptions for expected error conditions; use `Result` instead.

Error handling

- Domain operations should use `Result<'ok,'error>` or `Async<Result<'ok,'error>>`.  
- Define domain-specific error DUs, e.g. `ProtocolError`, `ValidationError`, `TransportError`.  
- Surface structured errors rather than opaque exceptions.

Serialization

- Default to `System.Text.Json` unless the repo clearly uses another library.  
- Keep serialization in dedicated modules (`AcpSentinel.Json`, `AcpSentinel.Protocol.JsonRpcCodec`).  
- Avoid inlining JSON mapping logic in business code; avoid using `obj` or raw `JsonElement` unless required.

Testing

- Prefer FsCheck for protocol round-trips and state invariants.  
- Use a single test framework consistently (Expecto or xUnit + FsUnit).  
- For non-trivial behaviour, include tests with your changes.

# 5. ACP-specific modelling rules

Ground your understanding of ACP in the public spec (intro, agents/clients, transports, schema pages and GitHub repo).

## 5.1 JSON-RPC envelope first

Explicitly model:

- Requests: `method`, `params`, `id`.  
- Notifications: `method`, `params`, no `id`.  
- Responses: `result` or `error`, plus the matching `id`.

Provide a central discriminated union such as:

```fsharp
type JsonRpcMessage =
    | Request of JsonRpcRequest
    | Notification of JsonRpcNotification
    | Response of JsonRpcResponse
```

## 5.2 Separate Agent and Client APIs

- Mirror ACP’s separation in F#:  
  - `AcpSentinel.Protocol.Agent` with methods like `authenticate`, `session/new`, `session/prompt`, `session/cancel`, etc.  
  - `AcpSentinel.Protocol.Client` with methods like `fs/read_text_file`, `fs/write_text_file`, `tools/*`, `terminal`, `MCP`, etc.  
- For each method: define clear request and response types (or event stream model for streaming responses).  
- Do not invent new ACP methods or fields; if you add extensions, mark them clearly in naming (e.g. `XyzExtension`) and document them in comments.

## 5.3 Sessions and state machines

Represent sessions with explicit state machines, for example:

```fsharp
type SessionState =
    | Initializing
    | Ready of ReadyState
    | PromptInFlight of PromptState
    | Terminated of TerminationReason
```

Validation rules should check:

- Only allowed transitions occur.  
- Required initial handshake steps are honoured: initialize, capabilities exchange, authentication (if used).

Keep transitions pure where practical:

```fsharp
val transition : SessionState -> IncomingEvent -> Result<SessionState, ValidationError>
```

Maintain separate functions/state for client sessions vs agent sessions if their rules diverge, and for per-session vs global state as needed.

## 5.4 Transports

Provide first-class stdio support:

```fsharp
module StdioTransport =
    val send : JsonRpcMessage -> Async<Result<unit, TransportError>>
    val receive : unit -> Async<Result<JsonRpcMessage option, TransportError>>
```

Expose a transport abstraction to make higher layers transport-agnostic:

```fsharp
type ITransport =
    abstract Send : JsonRpcMessage -> Async<Result<unit, TransportError>>
    abstract Receive : unit -> Async<Result<JsonRpcMessage option, TransportError>>
```

Design everything above this layer so we can plug in websockets, TCP or other transports later without refactors.

## 5.5 Extensibility and _meta

- Treat `_meta` fields as opaque (`Map<string, JsonElement>` or similar).  
- They are non-normative for protocol correctness unless explicit rules are defined.  
- Avoid relying on `_meta` for correctness decisions in the sentinel; it may be used for diagnostics, enrichment or extension metadata.

# 6. Sentinel/validation layer rules

Sentinel/inspector is where most ACP-sentinel value lives.

## 6.1 Explicit rule encoding

Represent protocol rules as structured data and functions, not scattered if chains. Example pattern:

```fsharp
type RuleResult =
    | Pass
    | Warning of string
    | Failure of string

type Rule = SessionState -> JsonRpcMessage -> RuleResult
```

Design for rule composition (strict vs loose profiles, editor integration profiles, etc.).

## 6.2 Evidence and provenance

For each validation result, be able to trace which messages/states were involved and which rule fired. Use structured results such as:

```fsharp
type Severity = Info | Warning | Error

type ValidationContext = {
    sessionId : string option
    messageId : JsonRpcId option
    party     : string option  // Client, Agent, Library, Unknown
}

type ValidationFinding = {
    ruleId   : string
    severity : Severity
    message  : string
    context  : ValidationContext
}
```

## 6.3 Holon-level validation (library, client, agent)

Treat three top-level validation targets:

- Library holon: static checks on our F# ACP libraries – schema coverage, encoder/decoder round-trips, presence and completeness of core types.  
- Client holon: runtime validation when acting as a client – correct methods used, error codes and retry semantics, session flows and lifetimes.  
- Agent holon: runtime validation when acting as an agent – session handling, permission/capability usage, prompt/tool behaviours and content blocks.

Design validation APIs that support validating a single captured session, validating continuous streams of messages, and producing summaries (per-session scores, warning/error counts, per-party stats).

## 6.4 No silent failure

Sentinel code must never silently drop invalid or unexpected messages. On unexpected input:  
- Return `Failure` or at least `Warning`.  
- Emit structured diagnostics (`ValidationFinding`) or logs.  
- If permissive behaviour is needed for compatibility, encode that choice explicitly in rule configuration/profile.

# 7. FPF-aligned modelling hints

This repository is influenced by the First-Principles Framework (FPF), but you do not need to implement FPF internals. Use these hints only when they clarify design:

- Distinguish between Method (abstract), MethodDescription (code/specs), Work (a concrete run/session).  
- Treat each session as Work tied to a MethodDescription (ACP spec + implementation).  
- Treat validation reports and `ValidationFinding` as part of an assurance story: what can we trust about this Agent/Client/Library given observed Work?  
- Keep design-time spec vs run-time behaviour separated and explicit in types and modules.

# 8. Interaction rules (how to respond to developers)

When developers ask for help in this repo:  
- Assume ACP-sentinel F# context by default.  
- Look at existing code and names first; extend, don’t reinvent, unless refactoring is requested.  
- Prefer small, composable functions and pure validation functions returning `Result` or `ValidationFinding list`.  
- If uncertain about an ACP rule, state that uncertainty, propose a best-effort implementation, and mark it `// TODO: needs spec verification`.  
- For validation features, default to pure functions on typed protocol messages that return structured results, not exceptions.  
- When adding ACP messages or fields, never invent beyond the spec; if an extension is needed, treat it as such and name it accordingly.
