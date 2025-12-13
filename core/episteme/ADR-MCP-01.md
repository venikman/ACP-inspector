---
id: ADR-MCP-01
title: "Constrained adoption of MCP for ACP observability and non-critical tools"
kind: ADR
state: Decided
assurance_level: L0-L1
decision_date: 2025-12-10
supersedes: []
related_rfd: RFD-MCP-OBS-01
related_anomaly: "ANOM-ACP-OBS-01: Missing FPF-aligned assurance story for ACP behavior from telemetry"
owners:
  - ACP Platform Lead (MCP Pilot Lead)
reviewers:
  - ACP Architect
  - Security Lead
---

# ADR‑MCP‑01 · Constrained adoption of MCP for ACP observability and non‑critical tools

## 1. Context

RFD‑MCP‑OBS‑01 proposes MCP to improve ACP observability/assurance without touching the kernel. MCP is rising as a standard for agent↔tool/context; we need real signal, with tight bounds.

## 2. Decision

- Adopt MCP **only** for observability surfaces and non‑critical **read‑only** tools in ACP‑adjacent flows.
- Keep MCP **outside** FPF kernel and ACP core semantics; no kernel/type/state‑machine changes.
- Apply the **Work & Risk Envelope** from the RFD (time/scope/complexity caps; edge‑only touch; assurance/security/ops guardrails; success/stop criteria).
- Tag all MCP artifacts as **experimental** (state: Exploration/Shaping; assurance: L0–L1).
- MCP is **optional/feature‑flagged**; ACP must run without it (observability may degrade).

## 3. Rationale

- Preserves stability (kernel untouched) while enabling evolvability (experiment with a standard protocol).
- Aligns with FPF abductive protocol: MCP is an L0 hypothesis addressing a concrete anomaly, with falsifiability.
- Controls scope and blast radius via explicit envelopes.

Alternatives: ignore MCP (no signal), track only (no evidence), unconstrained adoption (risk of kernel pollution). We choose constrained adoption.

## 4. Scope

### In‑scope (v0.1)

- MCP host integration for ACP‑adjacent agents in two pilot flows:
  1. Observability/diagnostics: read-only logs/traces/metrics via `mcp-observability`.
  2. Governance/metadata inspection: read-only policy/metadata/UTS refs via `mcp-governance`.
- MCP events enriched with ACP state step, gate outcome, PathId/PathSliceId, Γ_time, F/G/R tags, AssuranceLevel refs.

### Out‑of‑scope (v0.1)

- Write/actuation via MCP; changes to ACP config/safety data.
- Any kernel ontology or ACP state‑machine changes.
- Guarantees that depend solely on MCP evidence.

## 5. Constraints (binding)

### Work

- Time ≤ 3 pw; servers = {`mcp-observability`, `mcp-governance`}; MCP‑specific types ≤ 5; clear adapter namespace; no kernel primitives.

### Risk

- Assurance: L0–L1; experimental states.
- Optional/feature‑flagged; failures must not break ACP behavior.
- Security: treat as external; read‑only; no privileged creds.

### Outcomes

- Track success (target ≥80% coverage of MCP-tagged tool actions, usability feedback, integration effort) and stop triggers (scope/complexity overrun, opacity, security regressions, unintended dependency).

## 6. Status & follow‑ups

- Status: Decided (experiment level).
- Immediate next steps:
  1. Pick 1–2 ACP flows for MCP pilot.
  2. Choose the two MCP servers and schemas.
  3. Define minimal FPF metadata on MCP events (PathId, PathSliceId, Γ_time, lanes).
  4. Implement metrics/dashboards for success/stop criteria.
  5. Track as **post‑slice‑01** work (do not compete with protocol parity): “MCP Observability v0.1 (Exploration/Shaping only)”.

## 7. Consequences

Positive: ecosystem alignment, structured telemetry, reversible experiment, evidence‑based future decisions.  
Negative: added short‑term complexity; requires security review; risk of “pilot forever” if not later consolidated.
