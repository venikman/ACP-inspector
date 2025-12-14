#!/bin/bash
set -euo pipefail

# Throughput Benchmark
# Measures: Messages processed per second (sustained load)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RESULTS_DIR="$SCRIPT_DIR/results"
SCENARIOS_DIR="$SCRIPT_DIR/scenarios"

mkdir -p "$RESULTS_DIR"

echo "Throughput Benchmark"
echo "===================="
echo "Measures sustained message processing rate"
echo

# Build command list for hyperfine
COMMANDS=()
NAMES=()

for target in "$SCRIPT_DIR"/targets/*.sh; do
    if [[ -x "$target" ]]; then
        name=$(basename "$target" .sh)
        NAMES+=("$name")
        # Throughput mode: process 100 messages, measure total time
        COMMANDS+=("$target --mode throughput --count 100 < $SCENARIOS_DIR/throughput-batch.json")
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
    --export-json "$RESULTS_DIR/throughput.json"
    --export-markdown "$RESULTS_DIR/throughput.md"
)

for i in "${!COMMANDS[@]}"; do
    HYPERFINE_ARGS+=(--command-name "${NAMES[$i]}" "${COMMANDS[$i]}")
done

# Run benchmark
hyperfine "${HYPERFINE_ARGS[@]}"

echo
echo "Results saved to:"
echo "  - $RESULTS_DIR/throughput.json"
echo "  - $RESULTS_DIR/throughput.md"
echo
echo "To calculate messages/second:"
echo "  100 messages / mean_time_seconds = msg/sec"
