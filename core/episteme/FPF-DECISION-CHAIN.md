---
id: FPF-DECISION-CHAIN
title: "FPF Single-Chain Decision Flow (IT/ACP work)"
kind: Guide
state: Draft
assurance_level: L0
created: 2025-12-10
updated: 2025-12-10
---

# FPF Single-Chain Decision Flow (IT/ACP work)

Use this as a sequential filter. Any “No” halts progress until fixed.

## 1) Problem & owner (FIT-1)
- Clear problem/outcome in business language?
- Single accountable owner?
- 1–3 measurable success criteria?

## 2) Strategic & domain fit (FIT-2)
- Supports an existing strategy/program/KPI?
- Fits inside a defined domain/bounded context?
- If not: classify as exploration/spike, not roadmap.

## 3) Reuse & simplicity (PRACTICAL-1)
- Do we already have a product/platform/pattern covering ≥70–80%?
- Default to reuse/extension; new build needs an explicit exception.

## 4) Cost, capacity, risk (PRACTICAL-2)
- Rough TCO (build + 3–5y run) and capacity to operate?
- Value > cost + risk in leadership’s units?
- Key risks (security/compliance/safety) acceptable?
- If not: keep as research/prototype; don’t add to roadmap.

## 5) Architecture & integration (FUTURE-PROOF-1)
- Aligned to target architecture principles/stacks/patterns?
- Integrates via approved interfaces (APIs, events, MCP adapters), not one-offs?
- Clear long-term owner (ops/upgrades/decom)?
- If no: adjust design or log a time-boxed exception with review date.

## 6) Evolution & exit (FUTURE-PROOF-2)
- Plausible “version 2” path if successful?
- Exit/rollback plan if it fails (feature flags, deprecation, data migration)?
- If none: flag as high risk and escalate before committing.

## 7) Commit or classify
- If 1–6 are solid “yes”: classify as
  - Roadmap commitment (PRD/ACP plan), or
  - Guarded experiment (time-boxed, feature-flagged, explicit learning goals).
- If a step fails and can’t be fixed quickly: classify as
  - Idea backlog / parking lot, or
  - Research/spike with a fixed small budget.

## Usage notes
- Keep a 1-page note per big item answering these checkpoints; highlight the weakest step (main risk).
- Richer FPF machinery (Characteristics/Scales/Scores, Lean proofs, FINDER/DEFT, MCP, etc.) can plug into specific steps without changing this outer chain.
