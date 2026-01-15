#!/bin/bash
set -euo pipefail

# Rust SDK Benchmark Target
# Placeholder - replace with actual SDK path

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# TODO: Update these paths to your Rust SDK location
SDK_PATH="${RUST_SDK_PATH:-/path/to/rust-sdk}"
BINARY="$SDK_PATH/target/release/acp-benchmark"

MODE="${1:-roundtrip}"
COUNT="${3:-1}"

case "$MODE" in
    --mode)
        MODE="$2"
        shift 2
        ;;
esac

if [[ ! -x "$BINARY" ]]; then
    echo "Rust SDK binary not found at: $BINARY"
    echo "Build with: cd $SDK_PATH && cargo build --release"
    echo "Or set RUST_SDK_PATH environment variable"
    exit 1
fi

case "$MODE" in
    cold-start)
        "$BINARY" --mode cold-start
        ;;
    roundtrip)
        "$BINARY" --mode roundtrip
        ;;
    throughput)
        COUNT="${2:-100}"
        [[ "$1" == "--count" ]] && COUNT="$2"
        "$BINARY" --mode throughput --count "$COUNT"
        ;;
    codec)
        COUNT="${2:-1000}"
        [[ "$1" == "--count" ]] && COUNT="$2"
        "$BINARY" --mode codec --count "$COUNT"
        ;;
    *)
        echo "Unknown mode: $MODE"
        exit 1
        ;;
esac
