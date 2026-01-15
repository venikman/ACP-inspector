#!/usr/bin/env python3
"""
Python SDK Benchmark CLI
Mirrors the F# benchmark for cross-language comparison
"""

import json
import time
import argparse
from typing import Any

# Sample ACP messages
INITIALIZE_REQUEST = json.dumps({
    "jsonrpc": "2.0",
    "method": "initialize",
    "params": {
        "protocolVersion": 1,
        "clientCapabilities": {"fs": {"readTextFile": True, "writeTextFile": True}, "terminal": True},
        "clientInfo": {"name": "benchmark", "version": "1.0.0"}
    },
    "id": 1
})

SESSION_NEW_REQUEST = json.dumps({
    "jsonrpc": "2.0",
    "method": "session/new",
    "params": {"cwd": "/tmp", "mcpServers": []},
    "id": 1
})

SESSION_UPDATE_NOTIFICATION = json.dumps({
    "jsonrpc": "2.0",
    "method": "session/update",
    "params": {
        "sessionId": "sess-001",
        "update": {"sessionUpdate": "agent_message_chunk", "content": {"type": "text", "text": "Hello, this is a test message."}}
    }
})

PROMPT_REQUEST = json.dumps({
    "jsonrpc": "2.0",
    "method": "session/prompt",
    "params": {"sessionId": "sess-001", "prompt": [{"type": "text", "text": "What is 2+2?"}]},
    "id": 2
})


def make_token_update(token_count: int) -> str:
    text = "word " * token_count
    return json.dumps({
        "jsonrpc": "2.0",
        "method": "session/update",
        "params": {
            "sessionId": "sess-001",
            "update": {"sessionUpdate": "agent_message_chunk", "content": {"type": "text", "text": text}}
        }
    })


def run_cold_start() -> None:
    """Benchmark: Cold Start"""
    start = time.perf_counter_ns()

    # Parse an initialize request
    parsed = json.loads(INITIALIZE_REQUEST)
    encoded = json.dumps({"jsonrpc": "2.0", "result": {"protocolVersion": 1}, "id": parsed["id"]})

    elapsed_ns = time.perf_counter_ns() - start
    elapsed_ms = elapsed_ns // 1_000_000

    print(json.dumps({
        "status": "ok",
        "mode": "cold-start",
        "elapsed_ms": elapsed_ms
    }))


def run_roundtrip() -> None:
    """Benchmark: Roundtrip"""
    start = time.perf_counter_ns()

    parsed = json.loads(SESSION_NEW_REQUEST)
    response = {"jsonrpc": "2.0", "result": {"sessionId": "sess-benchmark"}, "id": parsed["id"]}
    encoded = json.dumps(response)

    elapsed_ns = time.perf_counter_ns() - start
    elapsed_ms = elapsed_ns // 1_000_000

    print(json.dumps({
        "status": "ok",
        "mode": "roundtrip",
        "elapsed_ms": elapsed_ms
    }))


def run_throughput(count: int) -> None:
    """Benchmark: Throughput"""
    messages = [INITIALIZE_REQUEST, SESSION_NEW_REQUEST, SESSION_UPDATE_NOTIFICATION, PROMPT_REQUEST]

    start = time.perf_counter_ns()
    decoded = 0

    for i in range(count):
        msg = messages[i % len(messages)]
        json.loads(msg)
        decoded += 1

    elapsed_ns = time.perf_counter_ns() - start
    elapsed_ms = elapsed_ns // 1_000_000
    elapsed_sec = elapsed_ns / 1_000_000_000

    msgs_per_sec = int(decoded / elapsed_sec) if elapsed_sec > 0 else decoded * 1000

    print(json.dumps({
        "status": "ok",
        "mode": "throughput",
        "count": decoded,
        "elapsed_ms": elapsed_ms,
        "msgs_per_sec": msgs_per_sec
    }))


def run_codec(count: int) -> None:
    """Benchmark: Codec"""
    messages = [INITIALIZE_REQUEST, SESSION_NEW_REQUEST, SESSION_UPDATE_NOTIFICATION, PROMPT_REQUEST]

    start = time.perf_counter_ns()
    ops = 0

    for i in range(count):
        msg = messages[i % len(messages)]

        # Decode
        json.loads(msg)
        ops += 1

        # Encode
        json.dumps({"jsonrpc": "2.0", "result": {"sessionId": "sess-bench"}, "id": i})
        ops += 1

    elapsed_ns = time.perf_counter_ns() - start
    elapsed_ms = elapsed_ns // 1_000_000
    elapsed_sec = elapsed_ns / 1_000_000_000

    ops_per_sec = int(ops / elapsed_sec) if elapsed_sec > 0 else ops * 1000

    print(json.dumps({
        "status": "ok",
        "mode": "codec",
        "ops": ops,
        "elapsed_ms": elapsed_ms,
        "ops_per_sec": ops_per_sec
    }))


def run_tokens(count: int, tokens_per_msg: int) -> None:
    """Benchmark: Tokens"""
    message = make_token_update(tokens_per_msg)

    start = time.perf_counter_ns()
    decoded = 0
    total_tokens = 0

    for _ in range(count):
        json.loads(message)
        decoded += 1
        total_tokens += tokens_per_msg

    elapsed_ns = time.perf_counter_ns() - start
    elapsed_ms = elapsed_ns // 1_000_000
    elapsed_sec = elapsed_ns / 1_000_000_000

    tokens_per_sec = int(total_tokens / elapsed_sec) if elapsed_sec > 0 else total_tokens * 1000
    msgs_per_sec = int(decoded / elapsed_sec) if elapsed_sec > 0 else decoded * 1000

    print(json.dumps({
        "status": "ok",
        "mode": "tokens",
        "messages": decoded,
        "tokens_per_msg": tokens_per_msg,
        "total_tokens": total_tokens,
        "elapsed_ms": elapsed_ms,
        "tokens_per_sec": tokens_per_sec,
        "msgs_per_sec": msgs_per_sec
    }))


def main() -> None:
    parser = argparse.ArgumentParser(description="ACP SDK Benchmark")
    parser.add_argument("--mode", default="roundtrip",
                        choices=["cold-start", "roundtrip", "throughput", "codec", "tokens"])
    parser.add_argument("--count", type=int, default=100)
    parser.add_argument("--tokens", type=int, default=100)

    args = parser.parse_args()

    if args.mode == "cold-start":
        run_cold_start()
    elif args.mode == "roundtrip":
        run_roundtrip()
    elif args.mode == "throughput":
        run_throughput(args.count)
    elif args.mode == "codec":
        run_codec(args.count)
    elif args.mode == "tokens":
        run_tokens(args.count, args.tokens)


if __name__ == "__main__":
    main()
