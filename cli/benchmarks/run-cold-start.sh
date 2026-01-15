#!/bin/bash
set -euo pipefail

# Cold Start Benchmark
# Measures: Process spawn → first ACP response

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RESULTS_DIR="$SCRIPT_DIR/results"
SCENARIOS_DIR="$SCRIPT_DIR/scenarios"

mkdir -p "$RESULTS_DIR"

echo "Cold Start Benchmark"
echo "===================="
echo "Measures time from process spawn to first ACP response"
echo

# Build command list for hyperfine
COMMANDS=()
NAMES=()

for target in "$SCRIPT_DIR"/targets/*.sh; do
    if [[ -x "$target" ]]; then
        name=$(basename "$target" .sh)
        NAMES+=("$name")
        COMMANDS+=("$target --mode cold-start < $SCENARIOS_DIR/initialize.json")
    fi
done

if [[ ${#COMMANDS[@]} -eq 0 ]]; then
    echo "No SDK targets found. Add executable scripts to targets/"
    exit 1
fi

# Build hyperfine arguments
HYPERFINE_ARGS=(
    --warmup 2
    --runs 20
    --export-json "$RESULTS_DIR/cold-start.json"
    --export-markdown "$RESULTS_DIR/cold-start.md"
)

for i in "${!COMMANDS[@]}"; do
    HYPERFINE_ARGS+=(--command-name "${NAMES[$i]}" "${COMMANDS[$i]}")
done

# Run benchmark
hyperfine "${HYPERFINE_ARGS[@]}"

echo
echo "Results saved to:"
echo "  - $RESULTS_DIR/cold-start.json"
echo "  - $RESULTS_DIR/cold-start.md"
