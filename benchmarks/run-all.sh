#!/bin/bash
set -euo pipefail

# Cross-language ACP SDK benchmarks
# Requires: hyperfine (brew install hyperfine)

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
RESULTS_DIR="$SCRIPT_DIR/results"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"
echo -e "${BLUE}           ACP SDK Cross-Language Benchmarks                ${NC}"
echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"
echo

# Check for hyperfine
if ! command -v hyperfine &> /dev/null; then
    echo -e "${RED}Error: hyperfine is not installed${NC}"
    echo "Install with: brew install hyperfine"
    exit 1
fi

# Create results directory
mkdir -p "$RESULTS_DIR"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)

# Discover available SDKs
declare -A SDKS
for target in "$SCRIPT_DIR"/targets/*.sh; do
    if [[ -x "$target" ]]; then
        name=$(basename "$target" .sh)
        SDKS["$name"]="$target"
    fi
done

if [[ ${#SDKS[@]} -eq 0 ]]; then
    echo -e "${RED}Error: No SDK targets found in $SCRIPT_DIR/targets/${NC}"
    exit 1
fi

echo -e "${GREEN}Found ${#SDKS[@]} SDK targets:${NC}"
for name in "${!SDKS[@]}"; do
    echo "  - $name: ${SDKS[$name]}"
done
echo

# Run individual benchmark suites
echo -e "${YELLOW}Running benchmark suites...${NC}"
echo

# 1. Cold Start
echo -e "${BLUE}[1/4] Cold Start Benchmark${NC}"
"$SCRIPT_DIR/run-cold-start.sh" || echo -e "${YELLOW}Warning: Cold start benchmark had issues${NC}"
echo

# 2. Single Message Roundtrip
echo -e "${BLUE}[2/4] Single Message Roundtrip${NC}"
"$SCRIPT_DIR/run-roundtrip.sh" || echo -e "${YELLOW}Warning: Roundtrip benchmark had issues${NC}"
echo

# 3. Throughput
echo -e "${BLUE}[3/4] Throughput Benchmark${NC}"
"$SCRIPT_DIR/run-throughput.sh" || echo -e "${YELLOW}Warning: Throughput benchmark had issues${NC}"
echo

# 4. Codec (if applicable)
echo -e "${BLUE}[4/4] Codec Benchmark${NC}"
"$SCRIPT_DIR/run-codec.sh" || echo -e "${YELLOW}Warning: Codec benchmark had issues${NC}"
echo

# Summary
echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}Benchmarks complete!${NC}"
echo -e "Results saved to: ${RESULTS_DIR}/"
echo
echo "Files:"
ls -la "$RESULTS_DIR"/*.{json,md} 2>/dev/null || echo "  (no results files found)"
echo -e "${BLUE}═══════════════════════════════════════════════════════════${NC}"
