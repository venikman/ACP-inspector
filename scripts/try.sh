#!/usr/bin/env bash
set -euo pipefail

export DOTNET_CLI_DO_NOT_SHOW_PROGRESS=1
export DOTNET_NOLOGO=1

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

echo "Building CLI (Release)..."
dotnet build "$ROOT/cli/apps/ACP.Cli/ACP.Cli.fsproj" -c Release -v minimal

echo
echo "Running CLI smoke checks..."
bash "$ROOT/cli/scripts/cli-smoke.sh"

echo
echo "Running CLI regressions..."
bash "$ROOT/cli/scripts/cli-regressions.sh"

echo
echo "OK"
