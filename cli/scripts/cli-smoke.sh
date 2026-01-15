#!/usr/bin/env bash
set -euo pipefail

export DOTNET_CLI_DO_NOT_SHOW_PROGRESS=1
export DOTNET_NOLOGO=1

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CLI_PROJECT="$ROOT/apps/ACP.Cli"
DEMO_DIR="$ROOT/examples/cli-demo"
TRACE="$DEMO_DIR/demo-session.jsonl"
SINGLE_MESSAGE="$DEMO_DIR/single-message.json"

if [[ ! -f "$TRACE" ]]; then
  echo "Missing demo trace: $TRACE" >&2
  exit 1
fi

if [[ ! -f "$SINGLE_MESSAGE" ]]; then
  echo "Missing single message: $SINGLE_MESSAGE" >&2
  exit 1
fi

echo "Running inspect..."
dotnet run --project "$CLI_PROJECT" -c Release --no-build -- inspect "$TRACE"

echo "Running analyze..."
dotnet run --project "$CLI_PROJECT" -c Release --no-build -- analyze "$TRACE"

echo "Running replay..."
dotnet run --project "$CLI_PROJECT" -c Release --no-build -- replay --stop-at 1 "$TRACE"

echo "Running validate..."
cat "$SINGLE_MESSAGE" | dotnet run --project "$CLI_PROJECT" -c Release --no-build -- validate --direction c2a

echo "CLI smoke checks passed."
