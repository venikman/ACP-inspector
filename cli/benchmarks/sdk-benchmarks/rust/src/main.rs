//! Rust SDK Benchmark CLI
//! Mirrors the F# benchmark for cross-language comparison

use clap::{Parser, ValueEnum};
use serde_json::{json, Value};
use std::time::Instant;

/// Sample ACP messages
const INITIALIZE_REQUEST: &str = r#"{"jsonrpc":"2.0","method":"initialize","params":{"protocolVersion":1,"clientCapabilities":{"fs":{"readTextFile":true,"writeTextFile":true},"terminal":true},"clientInfo":{"name":"benchmark","version":"1.0.0"}},"id":1}"#;

const SESSION_NEW_REQUEST: &str = r#"{"jsonrpc":"2.0","method":"session/new","params":{"cwd":"/tmp","mcpServers":[]},"id":1}"#;

const SESSION_UPDATE_NOTIFICATION: &str = r#"{"jsonrpc":"2.0","method":"session/update","params":{"sessionId":"sess-001","update":{"sessionUpdate":"agent_message_chunk","content":{"type":"text","text":"Hello, this is a test message."}}}}"#;

const PROMPT_REQUEST: &str = r#"{"jsonrpc":"2.0","method":"session/prompt","params":{"sessionId":"sess-001","prompt":[{"type":"text","text":"What is 2+2?"}]},"id":2}"#;

fn make_token_update(token_count: usize) -> String {
    let text = "word ".repeat(token_count);
    serde_json::to_string(&json!({
        "jsonrpc": "2.0",
        "method": "session/update",
        "params": {
            "sessionId": "sess-001",
            "update": {
                "sessionUpdate": "agent_message_chunk",
                "content": {"type": "text", "text": text}
            }
        }
    })).unwrap()
}

#[derive(Debug, Clone, ValueEnum)]
enum Mode {
    ColdStart,
    Roundtrip,
    Throughput,
    Codec,
    Tokens,
}

#[derive(Parser, Debug)]
#[command(name = "acp-benchmark")]
#[command(about = "ACP SDK Benchmark CLI")]
struct Args {
    #[arg(long, value_enum, default_value = "roundtrip")]
    mode: Mode,

    #[arg(long, default_value = "100")]
    count: usize,

    #[arg(long, default_value = "100")]
    tokens: usize,
}

fn run_cold_start() {
    let start = Instant::now();

    // Parse an initialize request
    let parsed: Value = serde_json::from_str(INITIALIZE_REQUEST).unwrap();
    let response = json!({"jsonrpc": "2.0", "result": {"protocolVersion": 1}, "id": parsed["id"]});
    let _encoded = serde_json::to_string(&response).unwrap();

    let elapsed = start.elapsed();
    let elapsed_ms = elapsed.as_millis();

    println!(
        r#"{{"status":"ok","mode":"cold-start","elapsed_ms":{}}}"#,
        elapsed_ms
    );
}

fn run_roundtrip() {
    let start = Instant::now();

    let parsed: Value = serde_json::from_str(SESSION_NEW_REQUEST).unwrap();
    let response = json!({"jsonrpc": "2.0", "result": {"sessionId": "sess-benchmark"}, "id": parsed["id"]});
    let _encoded = serde_json::to_string(&response).unwrap();

    let elapsed = start.elapsed();
    let elapsed_ms = elapsed.as_millis();

    println!(
        r#"{{"status":"ok","mode":"roundtrip","elapsed_ms":{}}}"#,
        elapsed_ms
    );
}

fn run_throughput(count: usize) {
    let messages = [
        INITIALIZE_REQUEST,
        SESSION_NEW_REQUEST,
        SESSION_UPDATE_NOTIFICATION,
        PROMPT_REQUEST,
    ];

    let start = Instant::now();
    let mut decoded = 0usize;

    for i in 0..count {
        let msg = messages[i % messages.len()];
        let _: Value = serde_json::from_str(msg).unwrap();
        decoded += 1;
    }

    let elapsed = start.elapsed();
    let elapsed_ms = elapsed.as_millis();
    let elapsed_sec = elapsed.as_secs_f64();
    let msgs_per_sec = if elapsed_sec > 0.0 {
        (decoded as f64 / elapsed_sec) as u64
    } else {
        decoded as u64 * 1000
    };

    println!(
        r#"{{"status":"ok","mode":"throughput","count":{},"elapsed_ms":{},"msgs_per_sec":{}}}"#,
        decoded, elapsed_ms, msgs_per_sec
    );
}

fn run_codec(count: usize) {
    let messages = [
        INITIALIZE_REQUEST,
        SESSION_NEW_REQUEST,
        SESSION_UPDATE_NOTIFICATION,
        PROMPT_REQUEST,
    ];

    let start = Instant::now();
    let mut ops = 0usize;

    for i in 0..count {
        let msg = messages[i % messages.len()];

        // Decode
        let _: Value = serde_json::from_str(msg).unwrap();
        ops += 1;

        // Encode
        let response = json!({"jsonrpc": "2.0", "result": {"sessionId": "sess-bench"}, "id": i});
        let _ = serde_json::to_string(&response).unwrap();
        ops += 1;
    }

    let elapsed = start.elapsed();
    let elapsed_ms = elapsed.as_millis();
    let elapsed_sec = elapsed.as_secs_f64();
    let ops_per_sec = if elapsed_sec > 0.0 {
        (ops as f64 / elapsed_sec) as u64
    } else {
        ops as u64 * 1000
    };

    println!(
        r#"{{"status":"ok","mode":"codec","ops":{},"elapsed_ms":{},"ops_per_sec":{}}}"#,
        ops, elapsed_ms, ops_per_sec
    );
}

fn run_tokens(count: usize, tokens_per_msg: usize) {
    let message = make_token_update(tokens_per_msg);

    let start = Instant::now();
    let mut decoded = 0usize;
    let mut total_tokens = 0usize;

    for _ in 0..count {
        let _: Value = serde_json::from_str(&message).unwrap();
        decoded += 1;
        total_tokens += tokens_per_msg;
    }

    let elapsed = start.elapsed();
    let elapsed_ms = elapsed.as_millis();
    let elapsed_sec = elapsed.as_secs_f64();

    let tokens_per_sec = if elapsed_sec > 0.0 {
        (total_tokens as f64 / elapsed_sec) as u64
    } else {
        total_tokens as u64 * 1000
    };

    let msgs_per_sec = if elapsed_sec > 0.0 {
        (decoded as f64 / elapsed_sec) as u64
    } else {
        decoded as u64 * 1000
    };

    println!(
        r#"{{"status":"ok","mode":"tokens","messages":{},"tokens_per_msg":{},"total_tokens":{},"elapsed_ms":{},"tokens_per_sec":{},"msgs_per_sec":{}}}"#,
        decoded, tokens_per_msg, total_tokens, elapsed_ms, tokens_per_sec, msgs_per_sec
    );
}

fn main() {
    let args = Args::parse();

    match args.mode {
        Mode::ColdStart => run_cold_start(),
        Mode::Roundtrip => run_roundtrip(),
        Mode::Throughput => run_throughput(args.count),
        Mode::Codec => run_codec(args.count),
        Mode::Tokens => run_tokens(args.count, args.tokens),
    }
}
