# Cross-Language SDK Performance Comparison

All measurements taken with hyperfine (20 runs, 5 warmup) on Apple Silicon.

## Summary Table

| Metric                    | Rust      | TypeScript | Python | F#      |
| ------------------------- | --------- | ---------- | ------ | ------- |
| **Cold Start**            | **1.8ms** | 29.3ms     | 35.8ms | 60.7ms  |
| **Throughput (1K msgs)**  | **2.0ms** | 29.6ms     | 34.2ms | 81.3ms  |
| **Codec (10K ops)**       | **9.7ms** | 36.9ms     | 62.4ms | 142.7ms |
| **Tokens (100/msg, 10K)** | **8.5ms** | 38.9ms     | 51.7ms | 119.3ms |

## Relative Performance (vs Rust baseline)

| Metric         | Rust | TypeScript   | Python       | F#           |
| -------------- | ---- | ------------ | ------------ | ------------ |
| **Cold Start** | 1.0x | 16.0x slower | 19.5x slower | 33.1x slower |
| **Throughput** | 1.0x | 14.8x slower | 17.1x slower | 40.6x slower |
| **Codec**      | 1.0x | 3.8x slower  | 6.4x slower  | 14.7x slower |
| **Tokens**     | 1.0x | 4.6x slower  | 6.1x slower  | 14.0x slower |

---

## Detailed Results

### 1. Cold Start (Process Startup → First Response)

| SDK            | Mean           | Min    | Max    | Relative |
| -------------- | -------------- | ------ | ------ | -------- |
| **Rust**       | 1.8ms ± 0.5ms  | 1.0ms  | 2.8ms  | 1.00     |
| **TypeScript** | 29.3ms ± 1.3ms | 27.2ms | 32.6ms | 16.0x    |
| **Python**     | 35.8ms ± 2.6ms | 32.0ms | 40.3ms | 19.5x    |
| **F#**         | 60.7ms ± 1.7ms | 58.0ms | 64.9ms | 33.1x    |

### 2. Message Throughput (1000 messages)

| SDK            | Mean           | Msgs/sec | Relative |
| -------------- | -------------- | -------- | -------- |
| **Rust**       | 2.0ms ± 0.1ms  | ~500,000 | 1.00     |
| **TypeScript** | 29.6ms ± 0.9ms | ~33,800  | 14.8x    |
| **Python**     | 34.2ms ± 0.6ms | ~29,200  | 17.1x    |
| **F#**         | 81.3ms ± 2.6ms | ~12,300  | 40.6x    |

### 3. Codec Operations (10000 encode+decode)

| SDK            | Mean            | Ops/sec    | Relative |
| -------------- | --------------- | ---------- | -------- |
| **Rust**       | 9.7ms ± 0.2ms   | ~2,060,000 | 1.00     |
| **TypeScript** | 36.9ms ± 1.2ms  | ~542,000   | 3.8x     |
| **Python**     | 62.4ms ± 2.5ms  | ~320,000   | 6.4x     |
| **F#**         | 142.7ms ± 2.9ms | ~140,000   | 14.7x    |

### 4. Token Throughput (100 tokens/msg, 10K messages = 1M tokens)

| SDK            | Mean            | Tokens/sec | Relative |
| -------------- | --------------- | ---------- | -------- |
| **Rust**       | 8.5ms ± 0.4ms   | ~117M      | 1.00     |
| **TypeScript** | 38.9ms ± 0.9ms  | ~25.7M     | 4.6x     |
| **Python**     | 51.7ms ± 1.4ms  | ~19.3M     | 6.1x     |
| **F#**         | 119.3ms ± 3.2ms | ~8.4M      | 14.0x    |

---

## Analysis

### Why F# Appears Slower

The F# results include **~60ms .NET runtime startup overhead** in every measurement. This is a one-time cost per process, not per operation.

**Adjusted codec performance** (subtracting 60ms startup):

- F# actual codec time: ~82ms for 20K ops = ~244K ops/sec
- Still slower than Rust, but comparable to Python

### Real-World Implications

| LLM Output Speed                  | SDK Bottleneck?                           |
| --------------------------------- | ----------------------------------------- |
| Claude/GPT: ~50-100 tokens/sec    | **Never** - all SDKs process millions/sec |
| Fast streaming: ~500 tokens/sec   | **Never**                                 |
| Batch processing: ~10K tokens/sec | **Never**                                 |

**All SDKs are fast enough** - the LLM is always the bottleneck, not the SDK.

### When to Choose Each SDK

| SDK            | Best For                                                           |
| -------------- | ------------------------------------------------------------------ |
| **Rust**       | Maximum performance, embedded systems, WebAssembly                 |
| **TypeScript** | Node.js servers, browser integration, JS ecosystem                 |
| **Python**     | ML/AI pipelines, rapid prototyping, Jupyter notebooks              |
| **F#**         | .NET ecosystem, type safety, validation layer, functional patterns |

---

## Unique Features by SDK

| Feature                 | Rust              | TypeScript    | Python         | F#                |
| ----------------------- | ----------------- | ------------- | -------------- | ----------------- |
| **Type Safety**         | ✅ Compile-time   | ⚠️ Optional   | ❌ Runtime     | ✅ Compile-time   |
| **Protocol Validation** | ❌                | ❌            | ❌             | ✅ Sentinel layer |
| **Memory Safety**       | ✅ Borrow checker | ⚠️ GC         | ⚠️ GC          | ⚠️ GC             |
| **Async**               | ✅ Zero-cost      | ✅ Event loop | ⚠️ GIL-limited | ✅ Task-based     |
| **Cross-platform**      | ✅ Native         | ✅ Node       | ✅ Interpreter | ✅ .NET           |
| **WebAssembly**         | ✅ Excellent      | ⚠️ Via wasm   | ❌             | ⚠️ Blazor         |

---

## Raw Performance Numbers

```
Cold Start (process startup):
  Rust:       1.8ms   ████
  TypeScript: 29.3ms  ███████████████████████████████████████████████████
  Python:     35.8ms  ██████████████████████████████████████████████████████████████
  F#:         60.7ms  ████████████████████████████████████████████████████████████████████████████████████████████████████████

Codec Ops/sec (10K operations):
  Rust:       2,060,000  ████████████████████████████████████████████████████████████████████████████████████████████████████████
  TypeScript:   542,000  ███████████████████████████
  Python:       320,000  ████████████████
  F#:           140,000  ███████
```

---

## Methodology

- **Hardware**: Apple Silicon (ARM64)
- **Tool**: hyperfine 1.20.0
- **Runs**: 20 iterations + 5 warmup
- **Mode**: Release/optimized builds for all languages
- **JSON Library**:
  - Rust: serde_json
  - TypeScript: native JSON
  - Python: native json
  - F#: System.Text.Json (hand-written codec)

## Reproducing Results

```bash
# Install hyperfine
brew install hyperfine

# Build all SDKs
dotnet build apps/ACP.Benchmark -c Release
cd benchmarks/sdk-benchmarks/typescript && npm install && npx tsc && cd -
cd benchmarks/sdk-benchmarks/rust && cargo build --release && cd -

# Run comparison
hyperfine \
  './benchmarks/sdk-benchmarks/rust/target/release/acp-benchmark --mode codec --count 10000' \
  'dotnet apps/ACP.Benchmark/bin/Release/net9.0/ACP.Benchmark.dll --mode codec --count 10000' \
  'node benchmarks/sdk-benchmarks/typescript/dist/benchmark.js --mode codec --count 10000' \
  'python3 benchmarks/sdk-benchmarks/python/benchmark.py --mode codec --count 10000'
```
