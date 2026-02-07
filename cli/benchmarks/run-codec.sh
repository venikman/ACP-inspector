#!/bin/bash
set -euo pipefail

# Codec Benchmark
# Measures: JSON-RPC encode/decode performance

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RESULTS_DIR="$SCRIPT_DIR/results"
SCENARIOS_DIR="$SCRIPT_DIR/scenarios"

mkdir -p "$RESULTS_DIR"

echo "Codec Benchmark"
echo "==============="
echo "Measures JSON-RPC encoding/decoding speed"
echo

# Build command list for hyperfine
COMMANDS=()
NAMES=()

for target in "$SCRIPT_DIR"/targets/*.sh; do
    if [[ -x "$target" ]]; then
        name=$(basename "$target" .sh)
        NAMES+=("$name")
        # Codec mode: decode + encode 1000 messages
        COMMANDS+=("$target --mode codec --count 1000 < $SCENARIOS_DIR/codec-batch.json")
    fi
done

if [[ ${#COMMANDS[@]} -eq 0 ]]; then
    echo "No SDK targets found. Add executable scripts to targets/"
    exit 1
fi

# Build hyperfine arguments
HYPERFINE_ARGS=(
    --warmup 3
    --runs 10
    --export-json "$RESULTS_DIR/codec.json"
    --export-markdown "$RESULTS_DIR/codec.md"
)

for i in "${!COMMANDS[@]}"; do
    HYPERFINE_ARGS+=(--command-name "${NAMES[$i]}" "${COMMANDS[$i]}")
done

# Run benchmark
hyperfine "${HYPERFINE_ARGS[@]}"

echo
echo "Results saved to:"
echo "  - $RESULTS_DIR/codec.json"
echo "  - $RESULTS_DIR/codec.md"
echo
echo "To calculate ops/second:"
echo "  1000 messages / mean_time_seconds = ops/sec"
