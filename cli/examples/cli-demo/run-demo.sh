#!/bin/bash
set -e

# ACP CLI Demo Script
# This script demonstrates all 5 commands of the acp-inspector tool

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Project root (two directories up from here)
PROJECT_ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
CLI_PROJECT="$PROJECT_ROOT/apps/ACP.Cli"
DEMO_DIR="$PROJECT_ROOT/examples/cli-demo"

# Check if we're in the right directory
if [ ! -f "$DEMO_DIR/demo-session.jsonl" ]; then
    echo -e "${RED}Error: demo-session.jsonl not found${NC}"
    echo "Please run this script from the cli/examples/cli-demo directory"
    exit 1
fi

cd "$DEMO_DIR"

echo -e "${CYAN}╔══════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║         ACP CLI Demo - Complete Use Case                ║${NC}"
echo -e "${CYAN}╔══════════════════════════════════════════════════════════╗${NC}"
echo ""
echo -e "${BLUE}Scenario:${NC} Security analysis of authentication code"
echo -e "${BLUE}Agent:${NC} Reads files, runs commands, creates reports"
echo -e "${BLUE}Tools:${NC} File system (read/write) + Terminal"
echo ""
echo -e "${YELLOW}Press Enter to continue between demos...${NC}"
read -r

# ============================================================================
# 1. INSPECT - Full Validation
# ============================================================================
echo ""
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN}[1/5] INSPECT - Full Protocol Validation${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo -e "${BLUE}Command:${NC} inspect demo-session.jsonl"
echo -e "${BLUE}Purpose:${NC} Validate complete trace file for protocol compliance"
echo ""
echo -e "${YELLOW}Running inspection...${NC}"
echo ""

dotnet run --project "$CLI_PROJECT" -- inspect demo-session.jsonl

echo ""
echo -e "${GREEN}✓ Inspection complete!${NC}"
echo ""
echo -e "${YELLOW}What you see:${NC}"
echo "  • Total messages processed"
echo "  • Sessions created"
echo "  • Tool calls executed"
echo "  • Validation findings (if any)"
echo "  • Protocol compliance summary"
echo ""
echo -e "${YELLOW}Press Enter for next demo...${NC}"
read -r

# ============================================================================
# 2. VALIDATE - Real-time Validation
# ============================================================================
echo ""
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN}[2/5] VALIDATE - Real-time Message Validation${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo -e "${BLUE}Command:${NC} validate --direction c2a < single-message.json"
echo -e "${BLUE}Purpose:${NC} Validate individual messages from stdin"
echo ""
echo -e "${YELLOW}Running validation on a single message...${NC}"
echo ""

cat single-message.json | dotnet run --project "$CLI_PROJECT" -- validate --direction c2a

echo ""
echo -e "${GREEN}✓ Validation complete!${NC}"
echo ""
echo -e "${YELLOW}What you see:${NC}"
echo "  • Per-message validation results"
echo "  • Color-coded pass/fail indicators"
echo "  • Immediate feedback on protocol issues"
echo ""
echo -e "${YELLOW}Use case:${NC} CI/CD pipelines, integration testing"
echo ""
echo -e "${YELLOW}Press Enter for next demo...${NC}"
read -r

# ============================================================================
# 3. REPLAY - Interactive Debugging
# ============================================================================
echo ""
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN}[3/5] REPLAY - Interactive Trace Stepping${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo -e "${BLUE}Command:${NC} replay --stop-at 5 demo-session.jsonl"
echo -e "${BLUE}Purpose:${NC} Step through trace messages one at a time"
echo ""
echo -e "${YELLOW}Replaying first 5 frames...${NC}"
echo ""

dotnet run --project "$CLI_PROJECT" -- replay --stop-at 5 demo-session.jsonl

echo ""
echo -e "${GREEN}✓ Replay complete!${NC}"
echo ""
echo -e "${YELLOW}What you see:${NC}"
echo "  • Frame-by-frame message display"
echo "  • Timestamps and direction"
echo "  • Parsed JSON-RPC content"
echo "  • Validation per message"
echo ""
echo -e "${YELLOW}Interactive mode:${NC} Use --interactive flag to step manually"
echo ""
echo -e "${YELLOW}Press Enter for next demo...${NC}"
read -r

# ============================================================================
# 4. ANALYZE - Statistical Analysis
# ============================================================================
echo ""
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN}[4/5] ANALYZE - Protocol Statistics${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo -e "${BLUE}Command:${NC} analyze demo-session.jsonl"
echo -e "${BLUE}Purpose:${NC} Generate statistics and insights from traces"
echo ""
echo -e "${YELLOW}Analyzing trace...${NC}"
echo ""

dotnet run --project "$CLI_PROJECT" -- analyze demo-session.jsonl

echo ""
echo -e "${GREEN}✓ Analysis complete!${NC}"
echo ""
echo -e "${YELLOW}What you see:${NC}"
echo "  • Method call frequency"
echo "  • Timing analysis (min/max/avg)"
echo "  • Message counts by direction"
echo "  • Session statistics"
echo "  • Tool call breakdown"
echo ""
echo -e "${YELLOW}Use case:${NC} Performance analysis, capacity planning"
echo ""
echo -e "${YELLOW}Press Enter for final demo...${NC}"
read -r

# ============================================================================
# 5. BENCHMARK - Performance Testing
# ============================================================================
echo ""
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN}[5/5] BENCHMARK - Performance Testing${NC}"
echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo -e "${BLUE}Command:${NC} benchmark --mode cold-start"
echo -e "${BLUE}Purpose:${NC} Measure performance characteristics"
echo ""
echo -e "${YELLOW}Running cold-start benchmark...${NC}"
echo ""

dotnet run --project "$CLI_PROJECT" -- benchmark --mode cold-start

echo ""
echo -e "${GREEN}✓ Benchmark complete!${NC}"
echo ""
echo -e "${YELLOW}What you see:${NC}"
echo "  • Initialization latency"
echo "  • Performance metrics"
echo "  • Timing percentiles"
echo ""
echo -e "${YELLOW}Other modes available:${NC}"
echo "  • roundtrip   - Message round-trip time"
echo "  • throughput  - End-to-end processing speed"
echo "  • codec       - Encode/decode performance"
echo "  • tokens      - Token processing speed"
echo "  • raw-json    - Raw JSON parsing speed"
echo ""

# ============================================================================
# Summary
# ============================================================================
echo ""
echo -e "${CYAN}╔══════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║                    Demo Complete!                        ║${NC}"
echo -e "${CYAN}╚══════════════════════════════════════════════════════════╝${NC}"
echo ""
echo -e "${GREEN}All 5 CLI commands demonstrated successfully!${NC}"
echo ""
echo -e "${YELLOW}Next steps:${NC}"
echo "  1. Try interactive replay: ${BLUE}dotnet run --project $CLI_PROJECT -- replay --interactive demo-session.jsonl${NC}"
echo "  2. Modify demo-session.jsonl to test different scenarios"
echo "  3. Create your own trace files from real ACP sessions"
echo "  4. Integrate validation into your CI/CD pipeline"
echo ""
echo -e "${YELLOW}Learn more:${NC}"
echo "  • README.md in this directory"
echo "  • Documentation: $PROJECT_ROOT/docs/tooling/acp-inspector.md"
echo "  • ACP Spec: https://github.com/agentclientprotocol/agent-client-protocol"
echo ""
echo -e "${GREEN}Happy debugging! 🚀${NC}"
echo ""
