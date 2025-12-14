#!/bin/bash
set -euo pipefail

# Python SDK Benchmark Target
# Placeholder - replace with actual SDK path

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# TODO: Update these paths to your Python SDK location
SDK_PATH="${PYTHON_SDK_PATH:-/path/to/python-sdk}"
VENV_PATH="$SDK_PATH/.venv"

MODE="${1:-roundtrip}"
COUNT="${3:-1}"

case "$MODE" in
    --mode)
        MODE="$2"
        shift 2
        ;;
esac

# Activate virtual environment if it exists
if [[ -d "$VENV_PATH" ]]; then
    source "$VENV_PATH/bin/activate"
fi

if ! python -c "import acp_sdk" 2>/dev/null; then
    echo "Python SDK (acp_sdk) not found"
    echo "Set PYTHON_SDK_PATH environment variable or install acp-sdk"
    exit 1
fi

case "$MODE" in
    cold-start)
        python -m acp_sdk.benchmark --mode cold-start
        ;;
    roundtrip)
        python -m acp_sdk.benchmark --mode roundtrip
        ;;
    throughput)
        COUNT="${2:-100}"
        [[ "$1" == "--count" ]] && COUNT="$2"
        python -m acp_sdk.benchmark --mode throughput --count "$COUNT"
        ;;
    codec)
        COUNT="${2:-1000}"
        [[ "$1" == "--count" ]] && COUNT="$2"
        python -m acp_sdk.benchmark --mode codec --count "$COUNT"
        ;;
    *)
        echo "Unknown mode: $MODE"
        exit 1
        ;;
esac
