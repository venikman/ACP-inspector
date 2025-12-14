#!/usr/bin/env bash
set -euo pipefail

# Replays a failing PBT run by fixing seed and size.
# Usage:
#   scripts/replay-pbt.sh <seed> <size> [filter]
#
# Example:
#   scripts/replay-pbt.sh 12345 25 "TraceReplay"

if [[ $# -lt 2 ]]; then
  echo "usage: $0 <seed> <size> [filter]"
  exit 1
fi

seed="$1"
size="$2"
filter="${3:-Acp.Tests.Pbt}"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")"/.. && pwd)"
cd "$repo_root/tests"

DOTNET_BIN="${DOTNET_BIN:-dotnet}"

export ACP_PBT_SEED="$seed"
export ACP_PBT_START_SIZE="$size"
export ACP_PBT_END_SIZE="$size"

echo "Using dotnet: $DOTNET_BIN"
echo "Replaying PBT with seed=$seed size=$size filter=$filter"

"$DOTNET_BIN" test --filter "FullyQualifiedName~$filter"
