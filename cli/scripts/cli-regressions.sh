#!/usr/bin/env bash
set -euo pipefail

export DOTNET_CLI_DO_NOT_SHOW_PROGRESS=1
export DOTNET_NOLOGO=1

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CLI_PROJECT="$ROOT/apps/ACP.Cli"
DEMO_DIR="$ROOT/examples/cli-demo"
TRACE_DECODE_ERROR="$DEMO_DIR/decode-error-fixture.jsonl"

if [[ ! -f "$TRACE_DECODE_ERROR" ]]; then
  echo "Missing regression trace fixture: $TRACE_DECODE_ERROR" >&2
  exit 1
fi

mktemp_file() {
  mktemp -t acp-inspector.XXXXXX 2>/dev/null || mktemp "/tmp/acp-inspector.XXXXXX"
}

echo "Running inspect --stop-on-error regression..."
STOP_LOG="$(mktemp_file)"

set +e
dotnet run --project "$CLI_PROJECT" -c Release --no-build -- inspect --stop-on-error "$TRACE_DECODE_ERROR" >"$STOP_LOG" 2>&1
STOP_CODE="$?"
set -e

if [[ "$STOP_CODE" -eq 0 ]]; then
  echo "Expected inspect --stop-on-error to fail on a decode error" >&2
  exit 1
fi

if ! rg -n "Stopping on first decode error" "$STOP_LOG" >/dev/null; then
  echo "Expected output to mention stopping on first decode error" >&2
  tail -n 50 "$STOP_LOG" >&2 || true
  exit 1
fi

if rg -n "session/new" "$STOP_LOG" >/dev/null; then
  echo "Expected inspect --stop-on-error to stop before later frames are processed" >&2
  rg -n "session/new" "$STOP_LOG" | head -n 20 >&2 || true
  exit 1
fi

echo "Running inspect --record regression..."
REC_TRACE="$(mktemp_file)"
REC_LOG="$(mktemp_file)"

set +e
dotnet run --project "$CLI_PROJECT" -c Release --no-build -- inspect --record "$REC_TRACE" "$TRACE_DECODE_ERROR" >"$REC_LOG" 2>&1
REC_CODE="$?"
set -e

if [[ ! -f "$REC_TRACE" ]]; then
  echo "Expected inspect --record to create output file: $REC_TRACE" >&2
  tail -n 50 "$REC_LOG" >&2 || true
  exit 1
fi

REC_LINES="$(wc -l <"$REC_TRACE" | tr -d ' ')"
if [[ "$REC_LINES" -ne 4 ]]; then
  echo "Expected recorded trace to contain 4 lines, got $REC_LINES: $REC_TRACE" >&2
  exit 1
fi

if rg -n -F "{not-json}" "$REC_TRACE" >/dev/null; then
  echo "Expected recorded trace to skip invalid JSON frames" >&2
  rg -n -F "{not-json}" "$REC_TRACE" | head -n 20 >&2 || true
  exit 1
fi

dotnet run --project "$CLI_PROJECT" -c Release --no-build -- inspect "$REC_TRACE" >/dev/null

echo "OK"
