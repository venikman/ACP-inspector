#!/usr/bin/env bash
set -euo pipefail

fail=0

# 1) Docs should not claim NuGet availability until we actually publish.
if rg -n '#r \"nuget: ACP\\.Inspector\"' README.md docs >/dev/null; then
  echo "FAIL: found NuGet reference '#r \"nuget: ACP.Inspector\"' in README/docs" >&2
  rg -n '#r \"nuget: ACP\\.Inspector\"' README.md docs | head -n 50 >&2 || true
  fail=1
fi

# 2) Unify on the canonical CLI path (ACP.Cli). The legacy ACP.Inspector app should not appear in docs.
if rg -n 'cli/apps/ACP\\.Inspector/' README.md docs >/dev/null; then
  echo "FAIL: found legacy CLI path 'cli/apps/ACP.Inspector/' in README/docs" >&2
  rg -n 'cli/apps/ACP\\.Inspector/' README.md docs | head -n 50 >&2 || true
  fail=1
fi

# 3) Avoid duplicated docs trees which drift over time.
if [[ -d sentinel/docs ]]; then
  echo "FAIL: sentinel/docs exists (docs/ should be canonical)" >&2
  fail=1
fi

if [[ "$fail" -eq 0 ]]; then
  echo "OK"
fi

exit "$fail"

