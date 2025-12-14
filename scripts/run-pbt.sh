#!/usr/bin/env bash
set -euo pipefail

# Runs only the PBT suite (Acp.Tests.Pbt.*) with FsCheck config driven by env vars.
# Env knobs (defaults in tests/Pbt/Generators.fs):
#   ACP_PBT_MAX_TEST
#   ACP_PBT_START_SIZE
#   ACP_PBT_END_SIZE
#   ACP_PBT_SEED

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")"/.. && pwd)"
cd "$repo_root/tests"

DOTNET_BIN="${DOTNET_BIN:-dotnet}"

export ACP_PBT_MAX_TEST="${ACP_PBT_MAX_TEST:-500}"
export ACP_PBT_START_SIZE="${ACP_PBT_START_SIZE:-1}"
export ACP_PBT_END_SIZE="${ACP_PBT_END_SIZE:-50}"

echo "Using dotnet: $DOTNET_BIN"
echo "PBT config: maxTest=$ACP_PBT_MAX_TEST startSize=$ACP_PBT_START_SIZE endSize=$ACP_PBT_END_SIZE seed=${ACP_PBT_SEED:-<none>}"

"$DOTNET_BIN" test --filter "FullyQualifiedName~Acp.Tests.Pbt"
