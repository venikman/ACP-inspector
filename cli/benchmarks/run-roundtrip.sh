#!/bin/bash
set -euo pipefail

# Single Message Roundtrip Benchmark
# Measures: Send ACP message → receive response (warm process)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RESULTS_DIR="$SCRIPT_DIR/results"
SCENARIOS_DIR="$SCRIPT_DIR/scenarios"

mkdir -p "$RESULTS_DIR"

echo "Single Message Roundtrip Benchmark"
echo "==================================="
echo "Measures warm message roundtrip latency"
echo

# Build command list for hyperfine
COMMANDS=()
NAMES=()

for target in "$SCRIPT_DIR"/targets/*.sh; do
    if [[ -x "$target" ]]; then
        name=$(basename "$target" .sh)
        NAMES+=("$name")
        COMMANDS+=("$target --mode roundtrip < $SCENARIOS_DIR/session-new.json")
    fi
done

if [[ ${#COMMANDS[@]} -eq 0 ]]; then
    echo "No SDK targets found. Add executable scripts to targets/"
    exit 1
fi

# Build hyperfine arguments
HYPERFINE_ARGS=(
    --warmup 5
    --runs 50
    --export-json "$RESULTS_DIR/roundtrip.json"
    --export-markdown "$RESULTS_DIR/roundtrip.md"
)

for i in "${!COMMANDS[@]}"; do
    HYPERFINE_ARGS+=(--command-name "${NAMES[$i]}" "${COMMANDS[$i]}")
done

# Run benchmark
hyperfine "${HYPERFINE_ARGS[@]}"

echo
echo "Results saved to:"
echo "  - $RESULTS_DIR/roundtrip.json"
echo "  - $RESULTS_DIR/roundtrip.md"
