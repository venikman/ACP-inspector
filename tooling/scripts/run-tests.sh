#!/usr/bin/env bash
set -euo pipefail

# Runs the test suite and emits a TRX report for stakeholders/CI.
# Output:
# - Console summary (normal verbosity)
# - tests/TestResults.trx (overwritten on each run)

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")"/../.. && pwd)"
cd "$repo_root/tests"

# Prefer .NET 10 SDK installed at /usr/local/share/dotnet/dotnet (required for net10.0),
# but allow override via DOTNET_BIN or fallback to PATH.
DOTNET_BIN="${DOTNET_BIN:-}"
if [[ -z "$DOTNET_BIN" ]]; then
  if [[ -x /usr/local/share/dotnet/dotnet ]]; then
    DOTNET_BIN=/usr/local/share/dotnet/dotnet
  else
    DOTNET_BIN=dotnet
  fi
fi

echo "Using dotnet: $DOTNET_BIN"

"$DOTNET_BIN" test --logger "console;verbosity=normal" --logger "trx;LogFileName=TestResults.trx"

echo
echo "TestResults.trx written to: $(pwd)/TestResults.trx"
