# ACP Inspector: Ideas for Improvement

Ideas evaluated against [REPO-EVALUATION-FRAMEWORK.md](./REPO-EVALUATION-FRAMEWORK.md).
Each idea includes projected score impact and effort estimate.

---

## Idea Evaluation Criteria

For an idea to be "good" it must:
- Improve at least one dimension by ≥ 0.5 points
- Not decrease any dimension
- Have clear implementation path
- Effort must be proportional to impact

---

## High-Impact Ideas

### Idea 1: "What is ACP?" Opening Section

**The Change:**
Add 2 paragraphs at README top before any code:

```markdown
## What is ACP?

The Agent Client Protocol (ACP) is a standard for communication between
AI agents and the applications that host them. Think of it like HTTP
for AI agents — it defines how to start sessions, send prompts, receive
responses, and handle tool calls.

ACP matters because AI agents are becoming infrastructure. Without a
standard protocol, every agent framework invents its own wire format,
making integration painful and vendor lock-in inevitable.
```

**Score Impact:**

| Dimension | Before | After | Change |
|-----------|--------|-------|--------|
| Clarity | 2.0 | 3.0 | +1.0 |
| Necessity | 2.5 | 3.0 | +0.5 |

**Effort:** 30 minutes
**ROI:** Highest (1.5 points / 0.5 hours = 3.0 points/hour)

---

### Idea 2: Single Canonical Name

**The Change:**
Pick "ACP Inspector" and use it everywhere:

| Current | Change To |
|---------|-----------|
| Folder: ACP-inspector | Keep |
| README: ACP Inspector | Keep |
| Package: ACP.Inspector | Keep |
| CLI: acp-inspector | Keep |

**Score Impact:**

| Dimension | Before | After | Change |
|-----------|--------|-------|--------|
| Coherence | 2.0 | 4.0 | +2.0 |

**Effort:** 2 hours (grep-replace + test)
**ROI:** High (2.0 points / 2 hours = 1.0 points/hour)

---

### Idea 3: Problem Statement Section

**The Change:**
Add after "What is ACP?":

```markdown
## The Problem

When you build or integrate an ACP agent, things break silently:
- Malformed messages pass through and cause downstream errors
- Protocol violations are hard to debug without visibility
- Version mismatches between client and agent go undetected
- No way to replay and analyze production conversations

ACP Inspector sits at your agent's IO boundary and catches these
issues before they become incidents.
```

**Score Impact:**

| Dimension | Before | After | Change |
|-----------|--------|-------|--------|
| Necessity | 2.5 | 3.5 | +1.0 |
| Clarity | 2.0 | 2.5 | +0.5 |

**Effort:** 1 hour
**ROI:** High (1.5 points / 1 hour = 1.5 points/hour)

---

### Idea 4: Glossary Section

**The Change:**
Add glossary defining project-specific terms:

```markdown
## Glossary

| Term | Meaning |
|------|---------|
| **Inspector** | The validation layer that checks ACP messages |
| **Lane** | A category of validation (protocol, session, transport, etc.) |
| **Finding** | A validation result (error, warning, or info) |
| **Transport** | How messages move (stdio, memory, network) |
| **Session** | A conversation context between client and agent |
| **Turn** | One prompt-response cycle within a session |
```

**Score Impact:**

| Dimension | Before | After | Change |
|-----------|--------|-------|--------|
| Clarity | 2.0 | 3.0 | +1.0 |

**Effort:** 30 minutes
**ROI:** High (1.0 points / 0.5 hours = 2.0 points/hour)

---

### Idea 5: True 3-Line Quick Start

**The Change:**
Replace 12-line F# snippet with:

```markdown
## Quick Start

```bash
# Validate an existing trace file
dotnet run --project cli/apps/ACP.Cli -- inspect your-trace.jsonl

# Pipe agent output through validator
your-agent | dotnet run --project cli/apps/ACP.Cli -- validate --direction a2c
```

That's it. No F# knowledge required for basic validation.
```

**Score Impact:**

| Dimension | Before | After | Change |
|-----------|--------|-------|--------|
| Accessibility | 2.0 | 3.0 | +1.0 |
| Clarity | 2.0 | 2.5 | +0.5 |

**Effort:** 30 minutes
**ROI:** High (1.5 points / 0.5 hours = 3.0 points/hour)

---

### Idea 6: Feature-to-Need Mapping

**The Change:**
Add section showing why each major feature exists:

```markdown
## Why These Features?

| You Need To... | Use This | Why It Exists |
|----------------|----------|---------------|
| Catch bad messages at runtime | `RuntimeAdapter.validateInbound` | Production safety |
| Debug protocol issues | `acp-inspector replay` | Incident response |
| Test without real agents | `DuplexTransport` | Fast CI/CD |
| Prove compliance | `acp-inspector analyze` | Audit evidence |
| Track conversation state | `SessionAccumulator` | Stateful UIs |
| Monitor tool execution | `ToolCallTracker` | Observability |
```

**Score Impact:**

| Dimension | Before | After | Change |
|-----------|--------|-------|--------|
| Proportionality | 2.0 | 3.0 | +1.0 |
| Necessity | 2.5 | 3.0 | +0.5 |

**Effort:** 2 hours
**ROI:** Medium (1.5 points / 2 hours = 0.75 points/hour)

---

### Idea 7: Hide Internal Docs

**The Change:**
Move internal process artifacts out of main view:

```
Current:                    Proposed:
docs/                       docs/
├── drr/                    ├── index.md
├── contexts/               ├── getting-started.md
├── tasks/          →       ├── api/
├── index.md                └── internal/
└── ...                         ├── drr/
                                ├── contexts/
                                └── tasks/
```

**Score Impact:**

| Dimension | Before | After | Change |
|-----------|--------|-------|--------|
| Proportionality | 2.0 | 2.5 | +0.5 |
| Coherence | 2.0 | 2.5 | +0.5 |

**Effort:** 1 hour
**ROI:** Medium (1.0 points / 1 hour = 1.0 points/hour)

---

## Medium-Impact Ideas

### Idea 8: JSON Schema Output

**The Change:**
Export TypeScript/JSON Schema types from F# domain:

```bash
acp-inspector export-schema --format typescript > acp-types.d.ts
acp-inspector export-schema --format json-schema > acp-schema.json
```

**Score Impact:**

| Dimension | Before | After | Change |
|-----------|--------|-------|--------|
| Accessibility | 2.0 | 2.5 | +0.5 |
| Leverage | 3.5 | 4.0 | +0.5 |

**Effort:** 1-2 days
**ROI:** Medium (implementation complexity)

---

### Idea 9: Docker Image

**The Change:**
Publish pre-built Docker image:

```bash
docker run ghcr.io/venikman/acp-inspector inspect trace.jsonl
```

Removes .NET SDK requirement for CLI usage.

**Score Impact:**

| Dimension | Before | After | Change |
|-----------|--------|-------|--------|
| Accessibility | 2.0 | 3.0 | +1.0 |

**Effort:** 4 hours
**ROI:** Medium (1.0 points / 4 hours = 0.25 points/hour)

---

### Idea 10: Validation Profile Presets

**The Change:**
Instead of exposing 6 lanes, offer named profiles:

```bash
# Simple: just check message format
acp-inspector inspect --profile basic trace.jsonl

# Standard: format + protocol state
acp-inspector inspect --profile standard trace.jsonl

# Strict: all 6 lanes
acp-inspector inspect --profile strict trace.jsonl
```

**Score Impact:**

| Dimension | Before | After | Change |
|-----------|--------|-------|--------|
| Proportionality | 2.0 | 3.0 | +1.0 |
| Accessibility | 2.0 | 2.5 | +0.5 |

**Effort:** 4 hours
**ROI:** Medium (1.5 points / 4 hours = 0.375 points/hour)

---

## Lower-Priority Ideas

### Idea 11: Python Wrapper

**The Change:**
Thin Python package calling CLI via subprocess:

```python
from acp_inspector import inspect, validate

findings = inspect("trace.jsonl")
result = validate(message, direction="c2a")
```

**Score Impact:**

| Dimension | Before | After | Change |
|-----------|--------|-------|--------|
| Accessibility | 2.0 | 3.5 | +1.5 |

**Effort:** 2-3 days
**ROI:** Lower due to effort, but high strategic value

---

### Idea 12: Interactive Tutorial

**The Change:**
`acp-inspector tutorial` command that walks through:
1. What ACP is
2. Inspecting a sample trace
3. Understanding findings
4. Integrating into your project

**Score Impact:**

| Dimension | Before | After | Change |
|-----------|--------|-------|--------|
| Clarity | 2.0 | 3.5 | +1.5 |
| Accessibility | 2.0 | 3.0 | +1.0 |

**Effort:** 1-2 days
**ROI:** Medium (educational value)

---

## Implementation Priority Matrix

### Tier 1: Do This Week (Documentation Only)

| Idea | Effort | Total Impact | ROI |
|------|--------|--------------|-----|
| #1 What is ACP? | 30 min | +1.5 | 3.0/hr |
| #5 3-line quick start | 30 min | +1.5 | 3.0/hr |
| #4 Glossary | 30 min | +1.0 | 2.0/hr |
| #3 Problem statement | 1 hr | +1.5 | 1.5/hr |

**Total: 2.5 hours → +5.5 points potential**

### Tier 2: Do This Month (Light Code Changes)

| Idea | Effort | Total Impact | ROI |
|------|--------|--------------|-----|
| #2 Single name | 2 hr | +2.0 | 1.0/hr |
| #6 Feature mapping | 2 hr | +1.5 | 0.75/hr |
| #7 Hide internal docs | 1 hr | +1.0 | 1.0/hr |

**Total: 5 hours → +4.5 points potential**

### Tier 3: Do Later (Significant Code)

| Idea | Effort | Total Impact |
|------|--------|--------------|
| #10 Validation profiles | 4 hr | +1.5 |
| #9 Docker image | 4 hr | +1.0 |
| #8 Schema export | 1-2 days | +1.0 |
| #11 Python wrapper | 2-3 days | +1.5 |
| #12 Interactive tutorial | 1-2 days | +2.5 |

---

## Projected Scores After Implementation

### After Tier 1 (2.5 hours work)

| Dimension | Before | After |
|-----------|--------|-------|
| Clarity | 2.0 | 3.5 |
| Necessity | 2.5 | 3.5 |
| Accessibility | 2.0 | 3.0 |
| Proportionality | 2.0 | 2.0 |
| Coherence | 2.0 | 2.0 |
| Leverage | 3.5 | 3.5 |
| **Aggregate** | **2.3** | **2.9** |

### After Tier 1 + Tier 2 (7.5 hours work)

| Dimension | Before | After |
|-----------|--------|-------|
| Clarity | 2.0 | 3.5 |
| Necessity | 2.5 | 4.0 |
| Accessibility | 2.0 | 3.0 |
| Proportionality | 2.0 | 3.0 |
| Coherence | 2.0 | 4.0 |
| Leverage | 3.5 | 3.5 |
| **Aggregate** | **2.3** | **3.5** |

### After All Tiers

| Dimension | Before | After |
|-----------|--------|-------|
| Clarity | 2.0 | 4.0 |
| Necessity | 2.5 | 4.0 |
| Accessibility | 2.0 | 4.0 |
| Proportionality | 2.0 | 3.5 |
| Coherence | 2.0 | 4.0 |
| Leverage | 3.5 | 4.0 |
| **Aggregate** | **2.3** | **3.9** |

---

## Summary

**Best ROI ideas (do first):**
1. "What is ACP?" section (3.0 points/hour)
2. 3-line quick start (3.0 points/hour)
3. Glossary (2.0 points/hour)
4. Problem statement (1.5 points/hour)

**Biggest single impact:**
- Single canonical name (+2.0 to Coherence)

**Strategic long-term:**
- Python wrapper (unlocks largest potential user base)

**Path to viable (3.0):** Tier 1 alone gets to 2.9
**Path to good (3.5):** Tier 1 + Tier 2 gets to 3.5
**Path to excellent (4.0):** All tiers gets to 3.9
