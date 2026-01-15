# Tech Debt Backlog

Items identified during TASK-001 audit requiring human decision or significant refactoring.

## High Priority

(No high priority items remaining)

---

## Medium Priority

### TD-002: Epistemology.Harness Stub Tests

**Files**:

- `sentinel/tests/Epistemology.Harness/EvalTests.fs`
- `sentinel/tests/Epistemology.Harness/ValidationTaxonomyTests.fs`

**Options**:

1. Implement actual tests
2. Remove stubs and archive project
3. Leave as placeholder for future work

**Decision Needed**: What is the purpose of Epistemology.Harness?

---

### TD-003: Documentation Coverage

**Current**: ~29% (290 doc comments / 996 functions)  
**Target**: >50%

**Focus Areas**:

- Public API functions
- Complex domain types
- Validation rules

**Suggested Approach**:

- Add doc comments during code reviews
- Prioritize `Acp.Domain.fs` and `Acp.Validation.fs`

---

## Low Priority

### TD-004: Code Coverage Tooling

**Status**: No coverage reports generated  
**Action**: Add to CI pipeline

```yaml
# Example GitHub Actions step
- run: dotnet test --collect:"XPlat Code Coverage"
- uses: codecov/codecov-action@v3
```

---

### TD-005: Large File Monitoring

Files approaching refactoring threshold (>500 lines):

| File | Lines | Trend |
| ------ | ----- | ----- |
| `runtime/src/Acp.Connection.fs` | 884 | Stable |
| `protocol/src/Acp.Domain.fs` | 757 | Growing |
| `sentinel/src/Acp.Validation.fs` | 704 | Growing |

**Note**: Domain and Validation growth is expected as BC modules are integrated.

---

## Completed

- [x] **TD-001: Split Acp.Codec.fs** - 2026-01-06
  - Split 3130-line file into 4 modules:
    - `Acp.Codec.Types.fs` (71 lines) - Type definitions
    - `Acp.Codec.Json.fs` (153 lines) - JSON helpers
    - `Acp.Codec.AcpJson.fs` (2740 lines) - ACP encoders/decoders
    - `Acp.Codec.fs` (197 lines) - Public API
  - All 314 tests passing
- [x] Format new BC modules (fantomas) - 2026-01-06
- [x] Audit report generated - 2026-01-06

---

Last Updated: 2026-01-06
