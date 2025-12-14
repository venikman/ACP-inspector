#!/bin/bash
set -euo pipefail

# TypeScript SDK Benchmark Target
# Placeholder - replace with actual SDK path

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# TODO: Update these paths to your TypeScript SDK location
SDK_PATH="${TYPESCRIPT_SDK_PATH:-/path/to/typescript-sdk}"
CLI_ENTRY="$SDK_PATH/dist/benchmark-cli.js"

MODE="${1:-roundtrip}"
COUNT="${3:-1}"

case "$MODE" in
    --mode)
        MODE="$2"
        shift 2
        ;;
esac

if [[ ! -f "$CLI_ENTRY" ]]; then
    echo "TypeScript SDK not found at: $CLI_ENTRY"
    echo "Set TYPESCRIPT_SDK_PATH environment variable or update this script"
    exit 1
fi

case "$MODE" in
    cold-start)
        node "$CLI_ENTRY" --mode cold-start
        ;;
    roundtrip)
        node "$CLI_ENTRY" --mode roundtrip
        ;;
    throughput)
        COUNT="${2:-100}"
        [[ "$1" == "--count" ]] && COUNT="$2"
        node "$CLI_ENTRY" --mode throughput --count "$COUNT"
        ;;
    codec)
        COUNT="${2:-1000}"
        [[ "$1" == "--count" ]] && COUNT="$2"
        node "$CLI_ENTRY" --mode codec --count "$COUNT"
        ;;
    *)
        echo "Unknown mode: $MODE"
        exit 1
        ;;
esac
