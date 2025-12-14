#!/usr/bin/env npx ts-node
"use strict";
/**
 * TypeScript SDK Benchmark CLI
 * Mirrors the F# benchmark for cross-language comparison
 */
// Sample ACP messages
const initializeRequest = JSON.stringify({
    jsonrpc: "2.0",
    method: "initialize",
    params: {
        protocolVersion: 1,
        clientCapabilities: { fs: { readTextFile: true, writeTextFile: true }, terminal: true },
        clientInfo: { name: "benchmark", version: "1.0.0" }
    },
    id: 1
});
const sessionNewRequest = JSON.stringify({
    jsonrpc: "2.0",
    method: "session/new",
    params: { cwd: "/tmp", mcpServers: [] },
    id: 1
});
const sessionUpdateNotification = JSON.stringify({
    jsonrpc: "2.0",
    method: "session/update",
    params: {
        sessionId: "sess-001",
        update: { sessionUpdate: "agent_message_chunk", content: { type: "text", text: "Hello, this is a test message." } }
    }
});
const promptRequest = JSON.stringify({
    jsonrpc: "2.0",
    method: "session/prompt",
    params: { sessionId: "sess-001", prompt: [{ type: "text", text: "What is 2+2?" }] },
    id: 2
});
function makeTokenUpdate(tokenCount) {
    const text = "word ".repeat(tokenCount);
    return JSON.stringify({
        jsonrpc: "2.0",
        method: "session/update",
        params: {
            sessionId: "sess-001",
            update: { sessionUpdate: "agent_message_chunk", content: { type: "text", text } }
        }
    });
}
// Benchmark: Cold Start
function runColdStart() {
    const start = performance.now();
    // Parse an initialize request
    const parsed = JSON.parse(initializeRequest);
    const encoded = JSON.stringify({ jsonrpc: "2.0", result: { protocolVersion: 1 }, id: parsed.id });
    const elapsed = performance.now() - start;
    console.log(JSON.stringify({
        status: "ok",
        mode: "cold-start",
        elapsed_ms: Math.round(elapsed)
    }));
}
// Benchmark: Roundtrip
function runRoundtrip() {
    const start = performance.now();
    const parsed = JSON.parse(sessionNewRequest);
    const response = { jsonrpc: "2.0", result: { sessionId: "sess-benchmark" }, id: parsed.id };
    const encoded = JSON.stringify(response);
    const elapsed = performance.now() - start;
    console.log(JSON.stringify({
        status: "ok",
        mode: "roundtrip",
        elapsed_ms: Math.round(elapsed)
    }));
}
// Benchmark: Throughput
function runThroughput(count) {
    const messages = [initializeRequest, sessionNewRequest, sessionUpdateNotification, promptRequest];
    const start = performance.now();
    let decoded = 0;
    for (let i = 0; i < count; i++) {
        const msg = messages[i % messages.length];
        JSON.parse(msg);
        decoded++;
    }
    const elapsed = performance.now() - start;
    const msgsPerSec = elapsed > 0 ? (decoded / (elapsed / 1000)) : decoded * 1000;
    console.log(JSON.stringify({
        status: "ok",
        mode: "throughput",
        count: decoded,
        elapsed_ms: Math.round(elapsed),
        msgs_per_sec: Math.round(msgsPerSec)
    }));
}
// Benchmark: Codec
function runCodec(count) {
    const messages = [initializeRequest, sessionNewRequest, sessionUpdateNotification, promptRequest];
    const start = performance.now();
    let ops = 0;
    for (let i = 0; i < count; i++) {
        const msg = messages[i % messages.length];
        // Decode
        JSON.parse(msg);
        ops++;
        // Encode
        JSON.stringify({ jsonrpc: "2.0", result: { sessionId: "sess-bench" }, id: i });
        ops++;
    }
    const elapsed = performance.now() - start;
    const opsPerSec = elapsed > 0 ? (ops / (elapsed / 1000)) : ops * 1000;
    console.log(JSON.stringify({
        status: "ok",
        mode: "codec",
        ops,
        elapsed_ms: Math.round(elapsed),
        ops_per_sec: Math.round(opsPerSec)
    }));
}
// Benchmark: Tokens
function runTokens(count, tokensPerMsg) {
    const message = makeTokenUpdate(tokensPerMsg);
    const start = performance.now();
    let decoded = 0;
    let totalTokens = 0;
    for (let i = 0; i < count; i++) {
        JSON.parse(message);
        decoded++;
        totalTokens += tokensPerMsg;
    }
    const elapsed = performance.now() - start;
    const elapsedSec = elapsed / 1000;
    const tokensPerSec = elapsedSec > 0 ? totalTokens / elapsedSec : totalTokens * 1000;
    const msgsPerSec = elapsedSec > 0 ? decoded / elapsedSec : decoded * 1000;
    console.log(JSON.stringify({
        status: "ok",
        mode: "tokens",
        messages: decoded,
        tokens_per_msg: tokensPerMsg,
        total_tokens: totalTokens,
        elapsed_ms: Math.round(elapsed),
        tokens_per_sec: Math.round(tokensPerSec),
        msgs_per_sec: Math.round(msgsPerSec)
    }));
}
// Parse arguments
function parseArgs() {
    const args = process.argv.slice(2);
    let mode = "roundtrip";
    let count = 100;
    let tokens = 100;
    for (let i = 0; i < args.length; i++) {
        if (args[i] === "--mode" && args[i + 1]) {
            mode = args[i + 1];
            i++;
        }
        else if (args[i] === "--count" && args[i + 1]) {
            count = parseInt(args[i + 1], 10);
            i++;
        }
        else if (args[i] === "--tokens" && args[i + 1]) {
            tokens = parseInt(args[i + 1], 10);
            i++;
        }
    }
    return { mode, count, tokens };
}
// Main
const { mode, count, tokens } = parseArgs();
switch (mode) {
    case "cold-start":
        runColdStart();
        break;
    case "roundtrip":
        runRoundtrip();
        break;
    case "throughput":
        runThroughput(count);
        break;
    case "codec":
        runCodec(count);
        break;
    case "tokens":
        runTokens(count, tokens);
        break;
    default:
        console.error(`Unknown mode: ${mode}`);
        console.error("Usage: benchmark.ts --mode <cold-start|roundtrip|throughput|codec|tokens> [--count N] [--tokens T]");
        process.exit(1);
}
