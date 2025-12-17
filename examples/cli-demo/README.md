# ACP CLI Demo - Complete Use Case

This example demonstrates all 5 commands of the `acp-cli` tool with a realistic ACP session.

## Scenario

A developer is debugging an AI agent that performs code analysis. The agent:

1. Initializes with file system and terminal capabilities
2. Creates a new session
3. Receives a prompt to analyze code security
4. Reads source files (auth.ts, package.json)
5. Runs terminal commands (ls)
6. Writes a security report
7. Returns the complete analysis with findings

## Files

- `demo-session.jsonl` - Complete trace of the ACP session (31 frames)
- `single-message.json` - Single message for validation testing
- `client-messages.json` - Sample client-to-agent messages
- `agent-messages.json` - Sample agent-to-client messages
- `run-demo.sh` - Executable script that runs all CLI commands

## Quick Start

```bash
# Make the demo script executable
chmod +x run-demo.sh

# Run the complete demo
./run-demo.sh
```

Or run commands individually (see below).

## Command-by-Command Walkthrough

### 1. Inspect - Full Validation

Analyze the complete trace file with detailed validation findings:

```bash
dotnet run --project ../../apps/ACP.Cli -- inspect demo-session.jsonl
```

**What it shows:**

- ✓ Protocol compliance check
- ✓ Message sequence validation
- ✓ Session lifecycle verification
- ✓ Tool call status tracking
- ✓ Validation findings with severity levels
- ✓ Summary statistics (messages, sessions, tool calls)

**Raw JSON output** (for programmatic processing):

```bash
dotnet run --project ../../apps/ACP.Cli -- inspect --raw demo-session.jsonl
```

### 2. Validate - Real-time Validation

Validate individual messages from stdin (useful for CI/CD pipelines):

```bash
# Validate a single message
cat single-message.json | dotnet run --project ../../apps/ACP.Cli -- validate --direction c2a

# Validate client-to-agent messages
cat client-messages.json | dotnet run --project ../../apps/ACP.Cli -- validate --direction c2a

# Validate agent-to-client messages
cat agent-messages.json | dotnet run --project ../../apps/ACP.Cli -- validate --direction a2c
```

**What it shows:**

- ✓ Per-message validation results
- ✓ Immediate feedback on protocol violations
- ✓ Color-coded pass/fail indicators
- ✗ Errors and warnings with context

**Use cases:**

- CI/CD pipeline validation
- Integration testing
- Live message monitoring

### 3. Replay - Interactive Debugging

Step through the trace file message-by-message:

```bash
# Interactive mode (press Enter to advance)
dotnet run --project ../../apps/ACP.Cli -- replay --interactive demo-session.jsonl

# Stop at specific frame for debugging
dotnet run --project ../../apps/ACP.Cli -- replay --stop-at 8 demo-session.jsonl

# Verbose output with full message details
dotnet run --project ../../apps/ACP.Cli -- replay --interactive --verbose demo-session.jsonl
```

**What it shows:**

- 📍 Frame-by-frame message display
- 🕐 Timestamps and direction (→ client-to-agent, ← agent-to-client)
- 📝 Parsed JSON-RPC message content
- ✓ Validation findings per message
- 🎯 Interactive controls (Enter=next, q=quit)

**Use cases:**

- Debugging protocol sequences
- Understanding message flow
- Training and demonstrations

### 4. Analyze - Statistical Analysis

Generate statistics and insights from the trace:

```bash
dotnet run --project ../../apps/ACP.Cli -- analyze demo-session.jsonl
```

**What it shows:**

- 📊 Method call frequency (initialize, session/new, session/prompt, etc.)
- ⏱️ Timing analysis (min/max/avg latencies)
- 🔢 Message counts by direction
- 🎯 Session statistics
- 🔧 Tool call breakdown
- 📈 Protocol version distribution

**Use cases:**

- Performance analysis
- Protocol usage patterns
- Capacity planning
- Troubleshooting bottlenecks

### 5. Benchmark - Performance Testing

Measure performance of different components:

```bash
# Cold start latency (initialization overhead)
dotnet run --project ../../apps/ACP.Cli -- benchmark --mode cold-start

# Message round-trip time
dotnet run --project ../../apps/ACP.Cli -- benchmark --mode roundtrip --count 1000

# Codec throughput (encode/decode performance)
dotnet run --project ../../apps/ACP.Cli -- benchmark --mode codec --count 10000

# Message throughput (end-to-end processing)
dotnet run --project ../../apps/ACP.Cli -- benchmark --mode throughput --count 5000

# Token processing speed
dotnet run --project ../../apps/ACP.Cli -- benchmark --mode tokens --tokens 1000000

# Raw JSON parsing speed
dotnet run --project ../../apps/ACP.Cli -- benchmark --mode raw-json --count 5000
```

**What it shows:**

- ⏱️ Latency measurements (p50, p95, p99)
- 🚀 Throughput (messages/sec, tokens/sec)
- 📊 Memory usage patterns
- 💾 Allocation statistics

**Use cases:**

- Performance regression testing
- SDK optimization
- Capacity planning
- Comparing implementations

## Understanding the Output

### Color Coding

- 🟢 **Green** - Success, valid protocol messages
- 🔴 **Red** - Errors, protocol violations
- 🟡 **Yellow** - Warnings, potential issues
- 🔵 **Blue** - Info, general messages

### Validation Findings

Each finding includes:

- **Severity**: Info, Warning, Error
- **Lane**: Which validation lane detected it (R12, R18, etc.)
- **Message**: Human-readable description
- **Context**: Relevant protocol details

### Exit Codes

- `0` - Success, all validation passed
- `1` - Validation errors found
- `2` - Runtime errors (file not found, parse errors)

## Real-World Workflow

### Development Workflow

```bash
# 1. During development: validate in real-time
my-acp-client | dotnet run --project ../../apps/ACP.Cli -- validate --direction c2a

# 2. After testing: inspect full trace
dotnet run --project ../../apps/ACP.Cli -- inspect session-trace.jsonl

# 3. Debug specific issues: replay interactively
dotnet run --project ../../apps/ACP.Cli -- replay --interactive session-trace.jsonl
```

### CI/CD Pipeline

```bash
#!/bin/bash
# run-acp-tests.sh

# Run integration tests and capture trace
./integration-tests > trace.jsonl

# Validate the trace
if dotnet run --project apps/ACP.Cli -- inspect trace.jsonl; then
    echo "✓ Protocol compliance verified"
else
    echo "✗ Protocol violations detected"
    exit 1
fi

# Performance regression check
dotnet run --project apps/ACP.Cli -- benchmark --mode throughput --count 1000
```

### Performance Analysis

```bash
# Collect traces from production
scp prod:/var/log/acp/*.jsonl ./traces/

# Analyze each trace
for trace in traces/*.jsonl; do
    echo "Analyzing $trace..."
    dotnet run --project ../../apps/ACP.Cli -- analyze "$trace" > "analysis-$(basename $trace .jsonl).txt"
done

# Aggregate results
cat analysis-*.txt | grep "Method calls:" | sort | uniq -c
```

## Next Steps

1. **Modify the trace** - Edit `demo-session.jsonl` to test different scenarios
2. **Create invalid messages** - Test error handling by breaking protocol rules
3. **Benchmark your system** - Run performance tests on your hardware
4. **Integrate into CI** - Add validation to your test pipeline

## Troubleshooting

### File Not Found

```text
Error: File not found: demo-session.jsonl
```

**Solution**: Run commands from the `examples/cli-demo/` directory

### Invalid JSON

```text
Error: Failed to parse JSON on line 5
```

**Solution**: Check JSONL format - each line must be valid JSON

### Direction Mismatch

```text
Warning: Expected fromClient, got fromAgent
```

**Solution**: Use correct `--direction` flag (c2a or a2c)

## Learn More

- [ACP Specification](https://github.com/agentclientprotocol/agent-client-protocol)
- [Tooling Documentation](../../docs/tooling/acp-sentinel.md)
- [SDK Examples](../README.md)
