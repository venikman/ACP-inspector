# Repository Evaluation Framework

A measurement system for assessing open-source project quality and viability from an external perspective.

## Purpose

Establish objective criteria to evaluate whether a repository effectively communicates its value and can be adopted by its target audience.

---

## Dimensions

### 1. Clarity

**What it measures:** Can someone understand the purpose quickly?

| Score | Criteria |
|-------|----------|
| 5 | Purpose obvious in 30 seconds |
| 4 | Clear after reading README intro |
| 3 | Clear after reading full README |
| 2 | Need to explore code to understand |
| 1 | Still confused after significant effort |

**Test:** Time a newcomer from landing to "I get what this does"

---

### 2. Necessity

**What it measures:** Does it solve a problem people actually have?

| Score | Criteria |
|-------|----------|
| 5 | Solves urgent, widespread pain |
| 4 | Solves real pain for specific audience |
| 3 | Nice-to-have for niche audience |
| 2 | Solution looking for problem |
| 1 | No discernible need |

**Test:** Find evidence of people asking for this solution before it existed

---

### 3. Accessibility

**What it measures:** Can the target audience actually use it?

| Score | Criteria |
|-------|----------|
| 5 | Anyone can use immediately |
| 4 | Target audience can use easily |
| 3 | Requires learning but achievable |
| 2 | Significant barriers (language, setup, concepts) |
| 1 | Effectively unusable for most |

**Test:** Count barriers between "I want to use this" and "I'm using this"

---

### 4. Proportionality

**What it measures:** Is complexity justified by value delivered?

| Score | Criteria |
|-------|----------|
| 5 | Minimal complexity, maximum value |
| 4 | Complexity justified by features |
| 3 | Some unnecessary complexity |
| 2 | Significantly over-engineered |
| 1 | Complexity obscures value |

**Test:** Map every major component to a concrete user need

---

### 5. Coherence

**What it measures:** Do naming, structure, and docs tell one consistent story?

| Score | Criteria |
|-------|----------|
| 5 | Everything aligns perfectly |
| 4 | Minor inconsistencies |
| 3 | Noticeable friction between parts |
| 2 | Multiple competing narratives |
| 1 | Contradictory/confusing throughout |

**Test:** List all names/terms used for the same concept; count > 1 = problem

---

### 6. Leverage

**What it measures:** Can it be used beyond its stated purpose?

| Score | Criteria |
|-------|----------|
| 5 | Highly reusable for unintended purposes |
| 4 | Some reusable components |
| 3 | Useful only for stated purpose |
| 2 | Limited even for stated purpose |
| 1 | Hard to use for anything |

**Test:** Attempt three uses not mentioned in docs

---

## Scoring Template

```
Project: _______________
Date: _______________
Evaluator: _______________

| Dimension       | Score (1-5) | Evidence |
|-----------------|-------------|----------|
| Clarity         |             |          |
| Necessity       |             |          |
| Accessibility   |             |          |
| Proportionality |             |          |
| Coherence       |             |          |
| Leverage        |             |          |
|-----------------|-------------|----------|
| **Aggregate**   |             |          |
```

---

## Acceptance Bars

### Minimum Viable (External Adoption)

| Dimension | Minimum | Notes |
|-----------|---------|-------|
| Clarity | 3 | Must be understandable from docs |
| Necessity | 3 | Must solve real problem for someone |
| Accessibility | 3 | Must be learnable |
| Proportionality | 3 | Some bloat acceptable |
| Coherence | 3 | Minor friction acceptable |
| Leverage | 2 | Bonus, not required |

**Minimum aggregate: 3.0 / 5**

### Production Ready (Recommended for Teams)

| Dimension | Target | Notes |
|-----------|--------|-------|
| Clarity | 4 | Clear from intro paragraph |
| Necessity | 4 | Documented pain points |
| Accessibility | 4 | Easy for target audience |
| Proportionality | 4 | Justified complexity |
| Coherence | 4 | Consistent naming/structure |
| Leverage | 3 | Works well for stated purpose |

**Target aggregate: 3.8 / 5**

---

## Anti-Patterns to Detect

| Anti-Pattern | Signals | Affected Dimension |
|--------------|---------|-------------------|
| Jargon Wall | Unexplained domain terms in intro | Clarity |
| Solution Searching | No "problem" section in docs | Necessity |
| Technology Moat | Niche language, complex setup | Accessibility |
| Resume-Driven | Features nobody asked for | Proportionality |
| Identity Crisis | Multiple names for same thing | Coherence |
| One-Trick Pony | Impossible to adapt/extend | Leverage |

---

## Application: ACP-Inspector/Sentinel

### Initial Assessment (Pre-Investigation)

| Dimension | Score | Evidence |
|-----------|-------|----------|
| Clarity | 2 | Jargon heavy; requires code exploration |
| Necessity | 2.5 | ACP is niche; unclear urgent need |
| Accessibility | 2 | F# barrier; assumes ACP knowledge |
| Proportionality | 2 | 6 validation lanes, 25+ modules |
| Coherence | 2 | 4 names: inspector/sentinel/cli/package |
| Leverage | 3.5 | Patterns reusable; testing tools valuable |
| **Aggregate** | **2.3** | Below minimum viable bar |

### Identified Issues

1. **Naming inconsistency**
   - Folder: `ACP-inspector`
   - README: `ACP-sentinel`
   - Package: `ACP.Sentinel`
   - CLI: `acp-cli`

2. **Missing context**
   - No "What is ACP?" explanation
   - Assumes reader knows the protocol

3. **Jargon without glossary**
   - Holon, Sentinel, Lanes, Findings
   - Domain terms used without definition

4. **Language barrier**
   - F# (~1% developer mindshare)
   - Official SDKs are TypeScript/Python

5. **Over-architecture signals**
   - 6 validation lanes
   - OpenTelemetry before user validation
   - Extensive internal docs (DRR, BC)

---

## Improvement Roadmap

To reach minimum viable (3.0):

| Priority | Action | Target Dimension | Effort |
|----------|--------|------------------|--------|
| 1 | Add "What is ACP?" section | Clarity | Low |
| 2 | Unify naming to one term | Coherence | Low |
| 3 | Add glossary for terms | Clarity | Low |
| 4 | Write problem/pain statement | Necessity | Medium |
| 5 | Create "hello world" in 5 lines | Accessibility | Medium |
| 6 | Document which features map to which needs | Proportionality | Medium |

---

## Usage

1. Score the project honestly using the template
2. Compare aggregate to acceptance bars
3. Identify lowest-scoring dimensions
4. Use anti-pattern list to diagnose issues
5. Prioritize improvements by impact/effort
6. Re-score after changes

This framework prioritizes external viability over internal quality. A well-tested, well-architected project can still score poorly if it fails to communicate value to potential users.
