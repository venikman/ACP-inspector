ACP-sentiel — an F# implementation of the Agent Client Protocol (ACP) plus a validation/sentinel layer.

- Holons: Protocol (types + codecs, pure), Runtime (transports/clients/agents over stdio), Sentinel (stateful validation + assurance).
- Defaults: JSON-RPC 2.0 over stdio, UTF-8, LF, bounded message size/timeout; no silent drops.
- Normative source: ACP public spec/schema; repo logic follows it. Unknown shapes/methods surface as ValidationFindings.
- Assurance: validation rules are composable profiles (strict/compat); findings carry ruleId, severity, context.
- See `docs/AGENT_ACP_SENTIEL.md` for the full playbook and `CODEX.md` for contributor guidance.
