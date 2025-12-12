#!/usr/bin/env bash
set -euo pipefail

# Runs the test suite and emits a TRX report for stakeholders/CI.
# Output:
# - Console summary (normal verbosity)
# - tests/TestResults.trx (overwritten on each run)

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")"/../.. && pwd)"
cd "$repo_root/tests"

# Use the dotnet on PATH by default (must be SDK 9 for net9.0).
# Allow override via DOTNET_BIN.
DOTNET_BIN="${DOTNET_BIN:-dotnet}"

echo "Using dotnet: $DOTNET_BIN"

"$DOTNET_BIN" test --logger "console;verbosity=normal" --logger "trx;LogFileName=TestResults.trx"

echo
echo "TestResults.trx written to: $(pwd)/TestResults/TestResults.trx"
