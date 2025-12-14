# Cross-Language SDK Benchmarks

Language-agnostic performance testing for ACP SDK implementations.

## Prerequisites

```bash
# Install hyperfine (cross-platform benchmark tool)
brew install hyperfine        # macOS
cargo install hyperfine       # via Rust
apt install hyperfine         # Debian/Ubuntu
choco install hyperfine       # Windows
```

## Quick Start

```bash
# Run all benchmarks
./benchmarks/run-all.sh

# Run specific benchmark
./benchmarks/run-cold-start.sh
./benchmarks/run-throughput.sh
./benchmarks/run-codec.sh
```

## Available Benchmarks

| Script | What it measures | Duration |
|--------|------------------|----------|
| `run-cold-start.sh` | Process startup + initialization | ~1 min |
| `run-throughput.sh` | Sustained message processing | ~2 min |
| `run-codec.sh` | JSON encode/decode performance | ~1 min |
| `run-memory.sh` | Memory usage per operation | ~1 min |
| `run-all.sh` | All benchmarks | ~5 min |

## SDK Targets

Each SDK needs a wrapper script in `targets/` that:
1. Accepts ACP messages via stdin
2. Outputs ACP responses to stdout
3. Supports `--mode` flag for different test scenarios

### Current Targets

| SDK | Status | Wrapper |
|-----|--------|---------|
| F# (.NET) | ✅ Ready | `targets/fsharp.sh` |
| TypeScript | 🔲 Placeholder | `targets/typescript.sh` |
| Python | 🔲 Placeholder | `targets/python.sh` |
| Rust | 🔲 Placeholder | `targets/rust.sh` |

## Adding a New SDK

1. Create wrapper script:
```bash
# targets/my-sdk.sh
#!/bin/bash
cd /path/to/my-sdk
./my-sdk-cli "$@"
```

2. Add to `run-all.sh`:
```bash
SDKS["my-sdk"]="./targets/my-sdk.sh"
```

3. Run benchmarks:
```bash
./benchmarks/run-all.sh
```

## Test Scenarios

### Cold Start (`scenarios/cold-start.json`)
Measures time from process spawn to first response:
- Runtime initialization
- SDK setup
- Transport handshake

### Throughput (`scenarios/throughput.json`)
Measures sustained message processing:
- Messages per second
- Latency under load
- Resource utilization

### Codec (`scenarios/codec.json`)
Measures JSON-RPC encoding/decoding:
- Parse speed
- Serialization speed
- Memory allocations

## Output

Results are saved to `results/` in multiple formats:
- `results.json` - Machine-readable
- `results.md` - Markdown table
- `results.csv` - Spreadsheet-compatible

### Example Output

```
Benchmark 1: F# SDK
  Time (mean ± σ):     234.2 ms ±  12.1 ms    [User: 180.1 ms, System: 42.3 ms]
  Range (min … max):   215.8 ms … 267.3 ms    50 runs

Benchmark 2: TypeScript SDK
  Time (mean ± σ):     156.8 ms ±   8.3 ms    [User: 120.5 ms, System: 28.1 ms]
  Range (min … max):   142.1 ms … 178.4 ms    50 runs

Summary
  TypeScript SDK ran 1.49 ± 0.10 times faster than F# SDK
```

## CI Integration

```yaml
# .github/workflows/benchmarks.yml
- name: Run benchmarks
  run: ./benchmarks/run-all.sh

- name: Upload results
  uses: actions/upload-artifact@v3
  with:
    name: benchmark-results
    path: benchmarks/results/
```

## Interpreting Results

### What to compare

| Metric | Good for | Watch out for |
|--------|----------|---------------|
| Mean time | General comparison | Outliers skew results |
| Median time | Typical performance | Hides variance |
| P99 time | Worst-case latency | May be noise |
| Min time | Best possible | Unrealistic expectation |

### Expected characteristics

| SDK | Startup | Throughput | Memory |
|-----|---------|------------|--------|
| Rust | Fastest | Fastest | Lowest |
| F# (AOT) | Fast | Fast | Low |
| F# (JIT) | Slow cold, fast warm | Fast | Low |
| TypeScript | Medium | Medium | Medium |
| Python | Medium | Slowest | Highest |

### Fair comparison notes

- **JIT warmup**: .NET/JVM need warmup runs
- **AOT compilation**: F#/Rust can compile to native
- **Cold vs warm**: First run vs subsequent runs differ
- **GC pressure**: Measure allocations, not just time
