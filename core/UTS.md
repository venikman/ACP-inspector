# ACP Unified Term Sheet (UTS)

- ArtifactId: ACP-UTS-001
- Family: ConceptualCore
- Pattern: F.17 — Unified Term Sheet (UTS)
- Scope: ACP 0.x (Agent Client Protocol, coding assistants)
- Status: Draft

Related guides:
- FPF single-chain decision flow (IT/ACP work): see `core/episteme/FPF-DECISION-CHAIN.md`

To cut horizontal scrolling in plain-text editors, the wide UTS matrix is split
into two narrower tables (identity/roles and protocol mappings).

| Row | Unified Tech              | Plain meaning                 | FPF keywords                           |
| --- | ------------------------ | ---------------------------- | -------------------------------------- |
| R1  | ACPAgentParty            | Coding agent subprocess      | U.Holon; AgentRole; codebase scope     |
| R2  | ACPClientParty           | Editor or UI host            | U.Holon; ClientRole; issues requests   |
| R3  | ACPSession               | Conversation session         | U.System; stateful; WorkScope over repo |
| R4  | PromptTurn               | Single prompt cycle          | U.Work; method `session/prompt`        |
| R5  | ACPMethodCall            | Single ACP call              | U.Work; MethodDescription; schema based |
| R6  | ACPNotification          | One-way ACP message          | U.Work; no result; logged evidence     |
| R7  | CapabilityFlag           | Declared feature flag        | U.Status; gates methods & WorkScope    |
| R8  | JsonRpcErrorObject       | JSON-RPC error object        | U.Episteme; failure payload            |
| R9  | ProtocolErrorCode        | Error code classification    | U.Status; enumerated failure reason    |
| R10 | CancelledTurnOutcome     | Cancelled prompt turn        | U.Status; non-error cancellation       |
| R11 | ACPDomainErrorOutcome    | Domain-level error outcome   | U.Status; semantically expected failure |
| R12 | ValidationFailure        | Validator-detected violation | U.Episteme + U.Status; external verdict |
| R13 | FileSystemResourceAccess | File read/write effect       | U.Work; Resource; captures intent & diff |
| R14 | TerminalExecutionChannel | Terminal session             | U.System; TerminalRole; command channel |
| R15 | ToolCallExecution        | Single tool call             | U.Work; lifecycle pending→completed    |
| R16 | SessionTrace             | Full session trace           | U.Episteme; ordered events for checking |
| R17 | MetaEnvelope             | Opaque metadata carrier      | U.Episteme; optional enrichment        |
| R18 | ValidationFinding        | Structured validation event  | U.Episteme + U.Status; assurance signal |
| R19 | StdioFramingDefault      | Transport framing defaults   | U.System; channel assumptions          |
| R20 | ProblemDetailsEnvelope   | RFC9457 `application/problem+json` error surface | InteropCard; MVPK; publication surface |
| R21 | ProblemTypeUri           | Registered problem type URI  | U.Identifier; registry pin; Path to episteme |
| R22 | FpfErrorExtensionPins    | PathId/PathSliceId + lane/policy/sentinel ids inside Problem Details | EvidenceGraph pins; G.11 refresh hooks |

#### R18 · ValidationFinding – ACP-sentinel base validation profile

| Lane           | Enabled by default | Role in ACP-sentinel base profile                                      |
| -------------- | ------------------ | ---------------------------------------------------------------------- |
| Protocol       | Yes                | **Mandatory.** `Error` findings are gating (spec failure).             |
| Session        | Yes                | **Mandatory.** `Error` findings are gating; warnings are advisory.     |
| ToolSurface    | No                 | **Opt-in.** When enabled, findings are advisory (non-gating).          |
| Transport      | Yes                | **Mandatory.** `Error` for malformed / truncated frames is gating.     |
| Implementation | Yes                | **Advisory only.** Findings never gate spec conformance.               |

To keep rows scannable in edit mode, the wide mapping is split into two narrow
tables.

| Row | ACP 0.x Sense (term + gloss)                           | JSON-RPC 2.0 Sense                      |
| --- | ------------------------------------------------------ | --------------------------------------- |
| R1  | Agent — generative coding subprocess                   | Server — request/response handler       |
| R2  | Client — UI mediating between user and agent           | Client — JSON-RPC requester             |
| R3  | session — created via `session/new` or `session/load`  | *None* — no session concept             |
| R4  | Prompt Turn — prompt → update stream → result          | Request/Response — one call + result    |
| R5  | Method (e.g. `initialize`, `session/prompt`)           | Method — named JSON-RPC operation       |
| R6  | Notification (e.g. `session/update`)                   | Notification — no response              |
| R7  | capability (e.g. `fs.readTextFile`) advertised in `initialize` | *None*                                  |
| R8  | Error — ACP schema with code/message/data              | error object — JSON-RPC error structure |
| R9  | ErrorCode — standard + ACP-specific codes (auth_required, etc.) | error code — standard numeric codes     |
| R10 | StopReason.cancelled — must return on user cancel       | *None*                                  |
| R11 | auth_required, resource_not_found — ACP-specific codes  | Server error (reserved range)           |
| R12 | *None* — not defined by ACP                             | *None*                                  |
| R13 | File System — `fs/read_text_file`, `fs/write_text_file` | *None*                                  |
| R14 | Terminals — `terminal/create`, `terminal/kill`, embedded content | *None*                                  |
| R15 | Tool call — ACP tool statuses & content blocks          | *None*                                  |
| R16 | session — prompt turns, updates, calls & cancellations (by id) | JSON-RPC trace — ordered messages       |
| R17 | `_meta` — opaque map in ACP payloads                    | *None*                                  |
| R18 | *None* — ACP does not define                            | *None*                                  |
| R19 | *None* — ACP transport agnostic                         | UTF-8 + LF framing; bounded message size |
| R20 | Problem Details error payload (`application/problem+json`) | *None* — outside JSON-RPC base; media type negotiated |
| R21 | Problem type URI registry entry                         | *None*                                  |
| R22 | Extension pins: PathId, PathSliceId, assurance lane/level, policy/sentinel ids | *None*                                  |

| Row | Project sense (term + gloss)                               | Bridges / CL rationale                                                                       |
| --- | ---------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| R1  | AgentUnderTest — instance under checks                     | Agent–Server ≈ CL2; Agent–AgentUnderTest ≡ CL3; executes ACP methods                          |
| R2  | ClientEmulator — harness mimicking editor                  | Client–Client ≡ CL3; Client–ClientEmulator ≈ CL2 (no UI); controls agent/resources            |
| R3  | SessionModel — persisted protocol state                    | session–SessionModel ≈ CL3; no JSON-RPC peer; stateful channel                                |
| R4  | TurnExecution — behavioural validation unit                | PromptTurn→Req/Resp ⊑ CL2; PromptTurn–TurnExecution ≡ CL3; minimal ACP unit                  |
| R5  | MethodProbe — wrapper emitting traces & checks             | Method–Method ≡ CL3; Method–MethodProbe ≈ CL2; probe adds side effects                        |
| R6  | EventCapture — listener asserting order & shape            | Notification–Notification ≡ CL3; Notification–EventCapture ≈ CL2; fire-and-forget             |
| R7  | CapabilityMatrixEntry — spec row for each capability       | capability–CapabilityMatrixEntry ≈ CL3; no JSON-RPC peer; capability gate                     |
| R8  | WireErrorRecord — trace record with id & direction         | Error–error ≡ CL3; Error–WireErrorRecord ⊂ CL3 (adds indexing)                                |
| R9  | ErrorClass — categories (TransportError, ProtocolViolation…) | ErrorCode–error code ≡ CL3; ErrorCode–ErrorClass ≈ CL2 (grouped)                              |
| R10 | CancellationVerdict — verdict of proper cancellation       | StopReason–CancellationVerdict ≡ CL3; no JSON-RPC peer; user cancel                           |
| R11 | DomainErrorVerdict — expected error correctly encoded       | ACP code ⊂ JSON-RPC server error (CL2); DomainOutcome–Verdict ≡ CL3                           |
| R12 | SpecViolation — kind ∈ {ProtocolInvariantBreach, SafetyPolicyBreach…} | External only; validation result; extra-protocol diagnosis                                   |
| R13 | FileEffectObservation — record of path, op, before/after   | File System–FileEffectObservation ≈ CL3; no JSON-RPC peer; file side effects                  |
| R14 | TerminalProbe — probes commands, output & lifecycle        | Terminal–TerminalProbe ≈ CL2 (observer); no JSON-RPC peer; long-lived channel                 |
| R15 | ToolCallTrace — per-call trace with outputs and effects    | ToolCallExecution–ToolCallTrace ≡ CL3; no JSON-RPC peer; unit of tool work                    |
| R16 | ValidationTrace — canonical log keyed by sessionId         | SessionTrace–JSON-RPC trace ≈ CL2 (adds semantics); SessionTrace–ValidationTrace ≡ CL3        |
| R17 | MetaPassthrough — preserved, not validated unless rule opts in | `_meta` → MetaPassthrough ≡ CL3; ignored unless profile enables                              |
| R18 | ValidationFinding — {ruleId,severity,message,context}       | External to ACP; bridges sentinel evidence into assurance lanes                               |
| R19 | TransportAssumption — defaults for stdio runtime           | Needed for interoperable runtime; guardrails for framing/timeouts                             |
| R20 | ProblemDetailsEnvelope — RFC9457 error record with ACP extensions | InteropCard; PathId/policy/sentinel pins preserve trace to evidence graph                     |
| R21 | ProblemTypeRegistryEntry — catalogued URI + semantics pin   | Registry ensures lexical discipline; bridges to guard/policy episteme                         |
| R22 | ProblemDetailsExtensionPins — PathId/PathSliceId/assurance_lane/policy_id/sentinel_id | EvidenceGraph pins; feed G.11 refresh and assurance dashboards                               |
