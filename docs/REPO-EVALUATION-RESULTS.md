# ACP-Inspector Evaluation Results

Evaluation conducted: 2026-01-09
Framework used: [REPO-EVALUATION-FRAMEWORK.md](./REPO-EVALUATION-FRAMEWORK.md)

---

## Executive Summary

| Metric | Value |
|--------|-------|
| Aggregate Score | **2.3 / 5.0** |
| Status | 🔴 Below minimum viable (3.0) |
| Strongest Dimension | Leverage (3.5) |
| Weakest Dimensions | Clarity, Accessibility, Proportionality, Coherence (all 2.0) |
| Recommendation | Address quick wins before external promotion |

---

## Dimension Scores

### 1. Clarity: 2.0 / 5.0 🔴

**Finding:** Requires code exploration to understand purpose.

**Evidence:**

| Issue | Location | Impact |
|-------|----------|--------|
| No ACP explanation | README.md | Reader must already know ACP |
| "Holon" undefined | README.md:9-11 | Obscure systems theory term |
| "Sentinel" undefined | Throughout | Unclear if security or validation |
| "Lanes" undefined | Acp.Validation.fs | Domain jargon |
| "Findings" undefined | Throughout | Could mean anything |

**Test performed:** Read README without prior ACP knowledge.
**Result:** After full README, still unclear what problem this solves.

---

### 2. Necessity: 2.5 / 5.0 🔴

**Finding:** Solution appears to precede problem articulation.

**Evidence:**

| Question | Answer in Docs? |
|----------|-----------------|
| What problem does this solve? | No explicit statement |
| Who has this problem? | Vague ("agent platform teams") |
| What happens without this? | Not described |
| Are people asking for this? | No external evidence cited |

**Test performed:** Search README for "problem", "pain", "without this".
**Result:** Zero matches. "Who benefits" section describes capabilities, not problems.

**Partial credit (0.5):** The "Typical scenarios" section hints at use cases, but frames them as features rather than pain relief.

---

### 3. Accessibility: 2.0 / 5.0 🔴

**Finding:** Significant barriers to adoption.

**Evidence:**

| Barrier | Severity | Notes |
|---------|----------|-------|
| F# language | High | ~1% developer mindshare |
| .NET 10 required | Medium | Preview SDK, not mainstream |
| No other language bindings | High | Can't use from Python/TS |
| ACP knowledge assumed | High | Niche protocol |
| 60-second start = 12 lines F# | Medium | Not actually quick |

**Test performed:** Count steps from "interested" to "working example".
**Result:**
1. Install .NET 10 SDK
2. Clone repo
3. Build SDK
4. Open F# interactive
5. Type 12 lines of F#
6. Understand output

**Comparison:** Official ACP TypeScript SDK: `npm install @anthropic/acp && import { Client }`

---

### 4. Proportionality: 2.0 / 5.0 🔴

**Finding:** Complexity exceeds apparent need.

**Evidence:**

| Component | Count | Justification Found? |
|-----------|-------|---------------------|
| Validation lanes | 6 | No user story for all 6 |
| F# modules | 25+ | Many seem internal |
| CLI commands | 5 | Reasonable |
| Design Records (DRR) | 5 | Internal process artifact |
| Business Contexts (BC) | 4 | Internal process artifact |
| Benchmark modes | 6 | Before proving basic value |

**Test performed:** Map major features to documented user needs.
**Result:**

| Feature | Documented User Need |
|---------|---------------------|
| Protocol validation | ✓ Mentioned in scenarios |
| Transport abstraction | ✓ "Runtime integrators" |
| Session state tracking | ✓ SDK feature |
| 6 validation lanes | ✗ No justification |
| OpenTelemetry export | ✗ No user story |
| Benchmark suite | ✗ No user story |
| DRR/BC documents | ✗ Internal only |

**Orphan ratio:** 4/7 major features lack user justification = 57% potentially over-engineered.

---

### 5. Coherence: 2.0 / 5.0 🔴

**Finding:** Multiple competing identities.

**Evidence:**

| Artifact | Name Used |
|----------|-----------|
| GitHub folder | ACP-inspector |
| README title | ACP-sentinel |
| NuGet package | ACP.Sentinel |
| CLI tool | acp-cli |
| Some docs | ACP SDK |

**Test performed:** `grep -r` for each name variant.
**Result:** All four names actively used. No single canonical identity.

**Additional incoherence:**

| Aspect | Inconsistency |
|--------|---------------|
| Audience | "Enterprise" vs "prototypers" vs "researchers" |
| Complexity | "60-second start" vs 12-line F# snippet |
| Positioning | "Reference implementation" vs "Production-grade" |

---

### 6. Leverage: 3.5 / 5.0 🟢

**Finding:** Reusable patterns exist beyond stated purpose.

**Evidence:**

| Component | Reuse Potential | Beyond ACP? |
|-----------|-----------------|-------------|
| Transport abstraction | High | Any protocol testing |
| State machine pattern | High | Any stateful protocol |
| Trace replay | High | Any recorded session |
| Multi-lane validation | Medium | Complex validation needs |
| Session accumulator | Medium | Streaming state |
| F# domain modeling | High | Study reference |

**Test performed:** Identify uses not mentioned in docs.
**Result:**
1. ✓ Protocol design study material
2. ✓ Testing infrastructure patterns
3. ✓ F# domain modeling reference
4. ~ Actual ACP validation (requires F#)

**Why not higher:** Leverage is limited by F# requirement. Patterns are visible but not extractable to other languages without significant effort.

---

## Aggregate Calculation

| Dimension | Weight | Score | Weighted |
|-----------|--------|-------|----------|
| Clarity | 1.0 | 2.0 | 2.0 |
| Necessity | 1.0 | 2.5 | 2.5 |
| Accessibility | 1.0 | 2.0 | 2.0 |
| Proportionality | 1.0 | 2.0 | 2.0 |
| Coherence | 1.0 | 2.0 | 2.0 |
| Leverage | 1.0 | 3.5 | 3.5 |
| **Total** | **6.0** | | **14.0** |
| **Aggregate** | | | **2.33** |

---

## Gap Analysis

| Dimension | Current | Minimum (3.0) | Gap |
|-----------|---------|---------------|-----|
| Clarity | 2.0 | 3.0 | -1.0 |
| Necessity | 2.5 | 3.0 | -0.5 |
| Accessibility | 2.0 | 3.0 | -1.0 |
| Proportionality | 2.0 | 3.0 | -1.0 |
| Coherence | 2.0 | 3.0 | -1.0 |
| Leverage | 3.5 | 3.0 | +0.5 |

**Total gap to minimum:** -4.0 points across 5 dimensions

---

## Root Cause Analysis

### Primary Root Cause: Inside-Out Development

The repo was built from implementer perspective, not user perspective:

```
How it was built:          What users need:
─────────────────          ────────────────
1. Build protocol types    1. Understand why
2. Build validation        2. See if it's for them
3. Build transports        3. Try it quickly
4. Build CLI               4. Evaluate fit
5. Write docs              5. Adopt incrementally
6. Hope for adoption       6. Go deeper as needed
```

### Secondary Root Causes

| Cause | Evidence | Affected Dimensions |
|-------|----------|---------------------|
| Internal team vocabulary leaked | Holon, sentinel, lanes | Clarity |
| No external user research | No problem statement | Necessity |
| Technology enthusiasm over pragmatism | F# choice | Accessibility |
| Completionism | 6 lanes, benchmarks | Proportionality |
| Iterative naming without cleanup | 4 names | Coherence |

---

## Recommendations

### Immediate (This Week)

| Action | Expected Impact | Dimensions |
|--------|-----------------|------------|
| Add "What is ACP?" paragraph | +0.5 Clarity | Clarity |
| Add "Problem" section | +0.5 Necessity | Necessity |
| Pick one name, grep-replace others | +1.0 Coherence | Coherence |
| Add glossary for jargon | +0.5 Clarity | Clarity |

**Projected score after immediate actions: ~2.9**

### Short-term (This Month)

| Action | Expected Impact | Dimensions |
|--------|-----------------|------------|
| 3-line hello world (not 12) | +0.5 Accessibility | Accessibility |
| Document why each lane exists | +0.5 Proportionality | Proportionality |
| Hide DRR/BC from top-level | +0.3 Proportionality | Proportionality |

**Projected score after short-term: ~3.2** (above minimum)

### Long-term (Quarter)

| Action | Expected Impact | Dimensions |
|--------|-----------------|------------|
| Python bindings | +1.0 Accessibility | Accessibility |
| User testimonials/case studies | +0.5 Necessity | Necessity |
| Consolidate to core + extensions | +0.5 Proportionality | Proportionality |

**Projected score after long-term: ~3.7** (production ready)

---

## Conclusion

ACP-inspector/sentinel is a technically competent implementation with genuine reusable value (Leverage: 3.5). However, it fails to communicate that value externally due to:

1. **Assumed context** (ACP knowledge, F# familiarity)
2. **Inside-out documentation** (features before problems)
3. **Identity fragmentation** (four names)
4. **Complexity exposure** (internal artifacts visible)

The gap to minimum viable (3.0) is closeable with ~4 hours of documentation work. The repo's strongest asset—its patterns and architecture—is currently locked behind barriers that prevent discovery.

**Bottom line:** Good implementation, poor packaging.
