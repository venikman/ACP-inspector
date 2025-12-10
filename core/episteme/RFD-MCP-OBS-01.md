---
id: RFD-MCP-OBS-01
title: "MCP adoption for ACP observability & tool wiring"
kind: RFD
state: Exploration
assurance_level: L0
related_anomaly: "ANOM-ACP-OBS-01: Missing FPF-aligned assurance story for ACP behavior from telemetry"
owners:
  - ACP Platform Lead (MCP Pilot Lead)
reviewers:
  - ACP Architect
  - Security Lead
created: 2025-12-10
updated: 2025-12-10
---

# RFD‑MCP‑OBS‑01 · MCP adoption for ACP observability & tool wiring

## 1. Context
ACP-style agent stacks need to show, from telemetry alone, that every agent/tool action passed sentinels, followed allowed ACP state transitions, and is backed by auditable F/G/R assurance (not opaque logs). Tool calls are custom; MCP is emerging as a de‑facto standard “USB‑C” for agents↔tools/context and is already used for observability, ticketing, data access. Question: should ACP adopt MCP, and if so, how, without destabilising the FPF/ACP core?

## 2. Anomaly (CC‑B5.2.1)
ANOM‑ACP‑OBS‑01: no FPF‑aligned assurance story for ACP behavior from telemetry (sentinels, state machine, F/G/R).

## 3. Proposal (short)
Evaluate MCP as a standardised tool/context interface for ACP agents, but only as an outer‑ring adapter:
- expose observability + non‑critical tools via MCP;
- treat MCP calls as telemetry surfaces enriched with ACP state step, gate outcome, PathId/PathSliceId, Γ_time, F/G/R tags, AssuranceLevel refs;
- keep MCP out of the kernel and ACP core semantics.

## 4. Forces
- Stability vs Evolvability
- Parsimony vs Local custom adapters
- Observability vs Complexity
- Safety vs Speed
- Kernel minimalism vs Convenience

## 5. Options
A) Ignore MCP (zero work, loses standardisation and keeps assurance opaque).  
B) Track only (no code; no real signal).  
C) Constrained MCP experiment (recommended): small, time‑boxed, edge‑only, reversible.

## 6. Recommendation
Choose Option C with explicit guardrails on scope, work, risk, outcomes. Bind details in ADR‑MCP‑01.

## 7. MCP Work & Risk Envelope (guardrails)

-### 7.1 Work envelope
- Time: ≤ 3 person‑weeks for v0.1.
- Pilot flows: (a) agent observability/diagnostics; (b) governance/metadata inspection.
- Servers: `mcp-observability` (read-only logs/traces/metrics), `mcp-governance` (read-only policy/metadata/UTS refs).
- Scope: read‑only; no sentinel/state‑machine changes.
- Touch radius: edges only; no kernel/ACP core types or transitions.
- Complexity: ≤ 5 new MCP‑specific episteme/event types; no kernel primitives.

### 7.2 Risk envelope
- Assurance: all MCP claims start L0–L1; no safety guarantee depends solely on MCP.
- Security: treat MCP servers as external; read‑only; authenticated backplane; no privileged creds via MCP.
- Ops: MCP optional/feature‑flagged; failures degrade observability only.

- Success: ≥ 80% of targeted ACP tool actions appear as MCP events with correct metadata; engineers report faster incident understanding; integration effort not worse than bespoke.
- Stop: over budget without coverage; opaque decisions appear; security degrades; ACP behavior depends on MCP.

## 8. Open questions
- Which ACP flows to pilot?
- Which two MCP servers first?
- Concrete X% and N?
- Minimal FPF metadata schema on MCP events?

## 9. Next steps
- Socialise RFD.
- Set envelope parameters (X, N, servers).
- Promote to ADR‑MCP‑01.
- Add roadmap item: “MCP Observability v0.1 (Exploration/Shaping only)”.
