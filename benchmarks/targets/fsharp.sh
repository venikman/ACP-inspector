#!/bin/bash
set -euo pipefail

# F# SDK Benchmark Target
# Wrapper for the unified ACP CLI

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$(dirname "$SCRIPT_DIR")")"

MODE="${1:-roundtrip}"
COUNT="${3:-1}"

case "$MODE" in
    --mode)
        MODE="$2"
        shift 2
        ;;
esac

case "$MODE" in
    cold-start)
        # Measure: dotnet startup + SDK init + first message
        dotnet run --project "$PROJECT_ROOT/apps/ACP.Cli" --configuration Release -- benchmark --mode cold-start
        ;;
    roundtrip)
        # Measure: single message roundtrip (warm)
        dotnet run --project "$PROJECT_ROOT/apps/ACP.Cli" --configuration Release -- benchmark --mode roundtrip
        ;;
    throughput)
        # Measure: process N messages
        COUNT="${2:-100}"
        [[ "$1" == "--count" ]] && COUNT="$2"
        dotnet run --project "$PROJECT_ROOT/apps/ACP.Cli" --configuration Release -- benchmark --mode throughput --count "$COUNT"
        ;;
    codec)
        # Measure: encode/decode N messages
        COUNT="${2:-1000}"
        [[ "$1" == "--count" ]] && COUNT="$2"
        dotnet run --project "$PROJECT_ROOT/apps/ACP.Cli" --configuration Release -- benchmark --mode codec --count "$COUNT"
        ;;
    *)
        echo "Unknown mode: $MODE"
        echo "Usage: $0 --mode <cold-start|roundtrip|throughput|codec> [--count N]"
        exit 1
        ;;
esac
