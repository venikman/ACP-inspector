# Cross-Language SDK Performance Comparison

## Benchmark Matrix

All measurements taken with hyperfine (20 runs, 5 warmup).

### Cold Start (Process Startup → First Response)

| SDK | Mean | Min | Max | Relative |
|-----|------|-----|-----|----------|
| **Rust** | _TBD_ | _TBD_ | _TBD_ | 1.00 (expected) |
| **F#** | 60.9ms ± 2.2ms | 58.1ms | 66.2ms | _TBD_ |
| **TypeScript** | _TBD_ | _TBD_ | _TBD_ | _TBD_ |
| **Python** | _TBD_ | _TBD_ | _TBD_ | _TBD_ |

### Message Roundtrip (Decode + Encode Single Message)

| SDK | Mean | Min | Max | Relative |
|-----|------|-----|-----|----------|
| **Rust** | _TBD_ | _TBD_ | _TBD_ | 1.00 (expected) |
| **F#** | 64.8ms ± 3.2ms | 61.1ms | 73.8ms | _TBD_ |
| **TypeScript** | _TBD_ | _TBD_ | _TBD_ | _TBD_ |
| **Python** | _TBD_ | _TBD_ | _TBD_ | _TBD_ |

### Message Throughput (1000 messages)

| SDK | Mean | Msgs/sec | Relative |
|-----|------|----------|----------|
| **Rust** | _TBD_ | _TBD_ | 1.00 (expected) |
| **F#** | 86.4ms ± 2.4ms | ~38,500 | _TBD_ |
| **TypeScript** | _TBD_ | _TBD_ | _TBD_ |
| **Python** | _TBD_ | _TBD_ | _TBD_ |

### Codec Operations (10000 encode+decode)

| SDK | Mean | Ops/sec | Relative |
|-----|------|---------|----------|
| **Rust** | _TBD_ | _TBD_ | 1.00 (expected) |
| **F#** | 144.5ms ± 3.6ms | ~138,400 | _TBD_ |
| **TypeScript** | _TBD_ | _TBD_ | _TBD_ |
| **Python** | _TBD_ | _TBD_ | _TBD_ |

### Token Throughput (100 tokens/msg, 10K messages)

| SDK | Mean | Tokens/sec | Msgs/sec | Relative |
|-----|------|------------|----------|----------|
| **Rust** | _TBD_ | _TBD_ | _TBD_ | 1.00 (expected) |
| **F#** | 116.4ms ± 2.7ms | ~16M | ~161K | _TBD_ |
| **TypeScript** | _TBD_ | _TBD_ | _TBD_ | _TBD_ |
| **Python** | _TBD_ | _TBD_ | _TBD_ | _TBD_ |

### Token Throughput (1000 tokens/msg, 1K messages)

| SDK | Mean | Tokens/sec | Msgs/sec | Relative |
|-----|------|------------|----------|----------|
| **Rust** | _TBD_ | _TBD_ | _TBD_ | 1.00 (expected) |
| **F#** | 77.0ms ± 3.0ms | ~45M | ~45K | _TBD_ |
| **TypeScript** | _TBD_ | _TBD_ | _TBD_ | _TBD_ |
| **Python** | _TBD_ | _TBD_ | _TBD_ | _TBD_ |

---

## Summary Table

| Metric | Rust | F# | TypeScript | Python |
|--------|------|-----|------------|--------|
| **Cold Start** | _TBD_ | 60.9ms | _TBD_ | _TBD_ |
| **Roundtrip** | _TBD_ | 64.8ms | _TBD_ | _TBD_ |
| **Msgs/sec (1K)** | _TBD_ | 38,500 | _TBD_ | _TBD_ |
| **Codec ops/sec** | _TBD_ | 138,400 | _TBD_ | _TBD_ |
| **Tokens/sec (100/msg)** | _TBD_ | 16M | _TBD_ | _TBD_ |
| **Tokens/sec (1000/msg)** | _TBD_ | 45M | _TBD_ | _TBD_ |

---

## Expected Performance Ranking

Based on language characteristics:

```
Fastest ─────────────────────────────────► Slowest

  Rust     F#/.NET    TypeScript/Node    Python
   │          │              │              │
   │          │              │              │
 Native    JIT/AOT      V8 JIT         Interpreted
 Zero GC   Low GC       GC             GC + GIL
```

### Theoretical Expectations

| SDK | Expected Relative Performance | Notes |
|-----|-------------------------------|-------|
| **Rust** | 1.0x (baseline) | Native, zero-cost abstractions |
| **F#** | 2-5x slower | JIT overhead, but efficient runtime |
| **TypeScript** | 5-15x slower | V8 is fast but JS overhead |
| **Python** | 20-100x slower | Interpreter + GIL limitations |

---

## How to Run Benchmarks

### Prerequisites

```bash
# Install hyperfine
brew install hyperfine  # macOS
cargo install hyperfine # via Rust

# Set SDK paths
export TYPESCRIPT_SDK_PATH=/path/to/typescript-sdk
export PYTHON_SDK_PATH=/path/to/python-sdk
export RUST_SDK_PATH=/path/to/rust-sdk
```

### Run All SDKs

```bash
./benchmarks/run-all.sh
```

### Run Individual SDK

```bash
# F# (ready)
dotnet apps/ACP.Benchmark/bin/Release/net9.0/ACP.Benchmark.dll --mode <mode> --count N

# TypeScript (needs setup)
node $TYPESCRIPT_SDK_PATH/dist/benchmark.js --mode <mode> --count N

# Python (needs setup)
python -m acp_sdk.benchmark --mode <mode> --count N

# Rust (needs setup)
$RUST_SDK_PATH/target/release/acp-benchmark --mode <mode> --count N
```

---

## Benchmark Modes

| Mode | Description | Parameters |
|------|-------------|------------|
| `cold-start` | Process startup to first response | - |
| `roundtrip` | Single message decode + encode | - |
| `throughput` | Process N messages | `--count N` |
| `codec` | Encode + decode N times | `--count N` |
| `tokens` | Token throughput simulation | `--count N --tokens T` |

---

## Data Collection Checklist

- [x] F# SDK benchmarks complete
- [ ] TypeScript SDK benchmark CLI
- [ ] Python SDK benchmark CLI
- [ ] Rust SDK benchmark CLI
- [ ] Run comparison with hyperfine
- [ ] Generate final comparison report

---

## Notes

- **~60ms baseline** in F# results is .NET runtime startup (one-time cost)
- **Token throughput** far exceeds any LLM generation speed (~50-100 tok/sec)
- All SDKs should implement the same benchmark modes for fair comparison
- Memory usage comparison would require separate tooling (e.g., Valgrind, dotMemory)
