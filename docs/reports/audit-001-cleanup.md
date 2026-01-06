# Audit Report: TASK-001 Codebase Cleanup

**Date**: 2026-01-06  
**Auditor**: Background Agent  
**Scope**: `src/`, `tests/`

## Executive Summary

| Category | Status | Count |
| -------- | ------ | ----- |
| Build Health | ✅ PASS | 0 warnings |
| Test Suite | ✅ PASS | 261 tests |
| Formatting | ⚠️ WARN | 6 files need formatting |
| Large Files | ⚠️ WARN | 5 files > 500 lines |
| TODOs | ℹ️ INFO | 2 placeholders |
| Doc Coverage | ⚠️ WARN | ~29% |

## 1. Build Health

```bash
dotnet build src/ACP.fsproj -warnaserror
→ Build succeeded (0 warnings, 0 errors)
```

**Verdict**: Clean build ✅

## 2. Code Size Analysis

### Files > 500 Lines (Refactoring Candidates)

| File | Lines | Priority | Notes |
| ------ | ----- | -------- | ----- |
| `src/Acp.Codec.fs` | 3130 | 🔴 HIGH | Needs splitting into multiple modules |
| `src/Acp.Connection.fs` | 884 | 🟡 MEDIUM | Consider extracting state machine |
| `src/Acp.Domain.fs` | 757 | 🟢 LOW | Domain types are cohesive |
| `src/Acp.Validation.fs` | 704 | 🟢 LOW | Validation logic is cohesive |
| `src/Acp.FSharpTokenizer.fs` | 528 | 🟢 LOW | Self-contained tokenizer |
| `tests/Acp.SemanticTests.fs` | 537 | 🟢 LOW | Test file, acceptable |

**Recommendation**: Split `Acp.Codec.fs` into:

- `Acp.Codec.Types.fs` - Type definitions
- `Acp.Codec.Json.fs` - JSON helpers
- `Acp.Codec.Decode.fs` - Decoding logic
- `Acp.Codec.Encode.fs` - Encoding logic
- `Acp.Codec.Router.fs` - Method routing

### Module Structure in Codec.fs

```fsharp
module Codec =
    module CodecState =     // State management
    module private Json =   // JSON utilities
    module private AcpJson = // ACP-specific JSON
```

## 3. Formatting Compliance

**Tool**: Fantomas 7.0.5

```bash
dotnet fantomas src tests/Acp.*.fs --check
```

**Files Needing Formatting** (6 total):

1. `src/Acp.Assurance.fs` ← New BC-001 module
2. `src/Acp.Capability.fs` ← New BC-003 module
3. `src/Acp.Semantic.fs` ← New BC-002 module
4. `tests/Acp.AssuranceTests.fs` ← New tests
5. `tests/Acp.CapabilityTests.fs` ← New tests
6. `tests/Acp.SemanticTests.fs` ← New tests

**Action**: Run `dotnet fantomas src tests --write` to auto-fix.

## 4. Technical Debt Markers

### TODOs Found

| File | Line | Content |
| ------ | ---- | ------- |
| `tests/Epistemology.Harness/EvalTests.fs` | 10 | `// TODO: Implement tests for epistemic reasoning` |
| `tests/Epistemology.Harness/ValidationTaxonomyTests.fs` | 10 | `// TODO: Implement actual tests` |

### Stub Tests (Assert.True(true))

Both files in `tests/Epistemology.Harness/` contain placeholder tests:

- `EvalTests.fs` - Empty test stub
- `ValidationTaxonomyTests.fs` - Empty test stub

**Status**: Epistemology.Harness appears to be a future expansion area, not currently integrated.

## 5. Documentation Coverage

| Metric | Value |
| ------ | ----- |
| XML doc comments (`///`) | 290 |
| Function definitions (`let`) | 996 |
| **Doc coverage ratio** | ~29% |

**Files with Best Coverage**:

- `src/Acp.Assurance.fs` - Well documented
- `src/Acp.Capability.fs` - Well documented
- `src/Acp.Semantic.fs` - Well documented
- `src/Acp.Codec.fs` - Module-level docs

**Files Needing Doc Improvement**:

- Most utility functions lack XML docs
- Consider adding `<summary>` tags to all public functions

## 6. Test Health

```bash
dotnet test tests/ACP.Tests.fsproj
→ total: 261, failed: 0, succeeded: 261, skipped: 0
```

### Test Distribution by Module

| Test File | Count |
| --------- | ----- |
| Acp.SemanticTests.fs | 51 |
| Acp.CapabilityTests.fs | 36 |
| Acp.AssuranceTests.fs | 34 |
| Acp.Validation.Tests.fs | ~30 |
| Other modules | ~110 |

### Coverage Gaps

- No coverage tool output available (XPlat Code Coverage not run)
- Epistemology.Harness tests are stubs

## 7. Recommendations

### Immediate Actions (Safe)

1. **Format all files**

   ```bash
   dotnet fantomas src tests --write
   ```

2. **Consider removing stub tests** in Epistemology.Harness or implement them

### Short-Term Refactoring

1. **Split Acp.Codec.fs** (3130 lines → ~600 lines each)
   - Requires careful testing, affects protocol layer

### Long-Term Improvements

1. **Improve doc coverage** to >50%
2. **Add code coverage** tooling to CI
3. **Implement Epistemology.Harness** tests or archive the project

## 8. Clean Items (No Action Needed)

- ✅ No unused `open` statements detected in sampled files
- ✅ No large commented-out code blocks
- ✅ Consistent naming conventions
- ✅ All tests pass
- ✅ No compiler warnings

## 9. Metrics Summary

```text
Source Lines:    ~9,500 (src/)
Test Lines:      ~5,800 (tests/)
Total:           ~15,300 lines F#
Modules:         18 source files
Test Files:      21 test files
Test Count:      261 tests
Doc Comments:    290
```

---

**Next Steps**:

1. Run fantomas to fix formatting
2. Decide on Epistemology.Harness fate
3. Plan Codec.fs refactoring (separate task)
