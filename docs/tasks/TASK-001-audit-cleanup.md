# TASK-001: Codebase Audit & Cleanup

**Status**: ✅ Complete  
**Priority**: Medium  
**Assignee**: Background Agent  
**Started**: 2026-01-06T02:26:00Z  
**Completed**: 2026-01-06T02:48:00Z  
**Context**: Pre-implementation cleanup before DRR/BC work begins

## Objective

Perform a comprehensive audit of the ACP-inspector codebase to identify and clean up:
- Dead code and unused files
- Inconsistent patterns
- Documentation gaps
- Test coverage holes
- Style/formatting issues

## Scope

### 1. Code Audit

#### 1.1 Unused Code Detection
- [ ] Identify unused F# modules/functions in `src/`
- [ ] Find orphaned test files in `tests/`
- [ ] Detect dead imports/opens
- [ ] Check for commented-out code blocks (> 10 lines)

#### 1.2 Consistency Check
- [ ] Verify all public functions have XML doc comments
- [ ] Check naming conventions match `STYLEGUIDE.md`
- [ ] Ensure Result/Option usage is consistent
- [ ] Validate discriminated union patterns are idiomatic

#### 1.3 Duplication Analysis
- [ ] Find copy-pasted code blocks
- [ ] Identify functions that could be consolidated
- [ ] Check for repeated patterns that should be abstracted

### 2. Documentation Audit

#### 2.1 Code Documentation
- [ ] List functions missing doc comments
- [ ] Find outdated comments (reference old APIs)
- [ ] Check module-level documentation completeness

#### 2.2 Project Documentation
- [ ] Verify README accuracy against current code
- [ ] Check all doc links resolve
- [ ] Ensure examples in docs compile/run
- [ ] Cross-reference `docs/` with actual implementation

### 3. Test Audit

#### 3.1 Coverage Analysis
- [ ] Run coverage report: `dotnet test --collect:"XPlat Code Coverage"`
- [ ] Identify modules with < 50% coverage
- [ ] List critical paths without tests
- [ ] Find tests that always pass (trivial assertions)

#### 3.2 Test Quality
- [ ] Check for flaky tests
- [ ] Verify test isolation (no shared state)
- [ ] Ensure test names describe behavior
- [ ] Find tests without assertions

### 4. Cleanup Actions

#### 4.1 Safe Deletions (confirm before removing)
- [ ] Remove confirmed dead code
- [ ] Delete orphaned files
- [ ] Clean up `.gitignore` if needed

#### 4.2 Refactoring Candidates
- [ ] List functions > 50 lines for potential split
- [ ] Identify deeply nested code (> 4 levels)
- [ ] Note modules with > 500 lines

#### 4.3 Formatting
- [ ] Run `dotnet fantomas src tests apps --check`
- [ ] Fix any formatting violations
- [ ] Verify `.editorconfig` is respected

## Deliverables

1. **Audit Report** (`docs/reports/audit-001-cleanup.md`)
   - Summary of findings
   - Categorized issues (Critical/High/Medium/Low)
   - Metrics (lines of dead code, coverage %, etc.)

2. **Cleanup PR** (if issues found)
   - Safe deletions only
   - Formatting fixes
   - Doc comment additions

3. **Tech Debt Backlog** (`docs/tasks/BACKLOG-tech-debt.md`)
   - Issues requiring human decision
   - Refactoring candidates
   - Architecture concerns

## Commands Reference

```bash
# Build and check for warnings
dotnet build src/ACP.fsproj -warnaserror

# Run all tests
dotnet test tests/ACP.Tests.fsproj -c Release

# Format check
dotnet tool restore && dotnet fantomas src tests apps --check

# Find TODOs/FIXMEs
grep -r "TODO\|FIXME\|HACK\|XXX" src/ tests/

# Check for large files
find src tests -name "*.fs" -exec wc -l {} \; | sort -rn | head -20

# List recent changes (context)
git log --oneline -20
```

## Constraints

- **DO NOT** modify core protocol logic
- **DO NOT** change public API signatures
- **DO NOT** delete files without listing in report first
- **ASK** before any refactoring > 50 lines changed
- **PRESERVE** all test golden files in `tests/golden/`

## Success Criteria

- [ ] Audit report created with all sections filled
- [ ] No new compiler warnings introduced
- [ ] All tests pass after cleanup
- [ ] Fantomas check passes
- [ ] No files deleted without documentation

## Notes

This task prepares the codebase for implementing the DRR/BC specifications:
- DRR-001: Agent Output Trustworthiness
- DRR-002: Cross-Agent Semantic Alignment  
- DRR-003: Capability Claim Verification
- DRR-004: Protocol Evolution Stability

Clean foundation → easier implementation.
