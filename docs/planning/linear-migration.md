# ACP Planning Migration to Linear

## Context

This document captures the migration of planning and strategy from GitHub issues in the `ACP-inspector` repository to Linear. The goal is to leave the GitHub repository for code only and manage all planning in Linear using the First Principles Framework (FPF) and UTS (User, Trace, Spec) categories.

## Proposed Linear Structure

### Project: ACP via FPF
A single Linear project serves as the container for all ACP work. It groups epics representing the core themes of the planning.

### Epics

1. **UTS-User – ACP User Workflows**  
   - Focus: End-user stories, acceptance criteria, and user-facing workflows.  
   - Example issues: define primary ACP user stories and acceptance criteria; design templates for UTS rows representing user intents; implement minimal CLI for Obsidian (#2).

2. **UTS-Trace – Work & Telemetry**  
   - Focus: Capturing complete execution traces and work objects.  
   - Example issues: implement instrumentation for session traces; design trace viewer; align telemetry formats; build error analysis pipeline.

3. **UTS-Spec – Specs, Taxonomies & Evals**  
   - Focus: Specification of expected behavior, failure mode taxonomies, and evaluation infrastructure.  
   - Example issues: map eval patterns to Sentinel holon (#7); extend UTS rows R20–R25 (#8); implement domain types for `EvalProfile`, `EvalCase`, `EvalJudge`, `EvalRun`, etc. (#9); implement code-based evals (#10); design LLM-as-judge kind (#11); track CI/CD and monitoring eval runs (#12).

4. **Platform & CI – ACP Runtime & Eval Pipeline**  
   - Focus: Continuous integration, build tooling, test automation, and runtime environment.  
   - Example issues: integrate code-based evals into CI/CD; run LLM-based judges in nightly builds; track evaluation metrics; instrument production monitoring.

5. **FPF Core & Docs – Patterns, Examples & Pedagogy**  
   - Focus: Documentation, FPF alignment, and educational examples.  
   - Example issues: maintain UTS and FPF documentation; create worked examples mapping ACP runs to UTS rows and evals; develop user-friendly documentation for ACP and FPF.

### Labels

- **UTS Dimension:** `UTS:User`, `UTS:Trace`, `UTS:Spec` (one per issue).  
- **FPF Layer:** `FPF:Core`, `FPF:Tooling`, `FPF:Pedagogy`.  
- **Eval Type (optional):** `Eval:Code`, `Eval:Judge`, `Eval:Dataset`.  

Assign exactly one UTS label and one FPF layer label to each issue. Use optional Eval labels for evaluation-related tasks.

## Mapping of GitHub Issues

| GitHub Issue # | Title | Suggested Epic | Notes |
|---|---|---|---|
| #12 | Track CI/CD and monitoring eval runs in ACP-sentinel | Platform & CI | Implement evaluation run tracking, surface metrics in dashboards, and document impact on the improvement flywheel. |
| #11 | Design LLM‑as‑judge EvalJudgeKind & integration hooks (config, F# sigs) | UTS-Spec | Create configuration and type signatures for LLM judges; gather requirements for runtime integration. |
| #10 | Implement code‑based EvalJudge: rule for non‑empty instruction in session/prompt | UTS-Spec | Develop a deterministic evaluator ensuring that each prompt contains a non-empty instruction; integrate tests. |
| #9 | Add Acp.Eval module with domain types for eval framework | UTS-Spec | Add F# types representing eval profiles, cases, judges, runs, metrics, and error cycles. |
| #8 | Extend UTS.md: Add R20‑R25 for eval profiles, golden cases, runs, metrics, error cycles | UTS-Spec | Document the new UTS rows that correspond to evaluation components. |
| #7 | Map eval patterns into Sentinel holon | UTS-Spec | Align eval workflow (golden datasets, eval runs, profiles, metrics) with UTS rows; design domain types; plan minimal implementation. |
| #6 | Polish Client/Editor Contributions | Platform & CI | After core features are stable, improve contributor experience for additional clients or editor integrations. |
| #5 | Validate the ACP Agent Binary Across Multiple ACP Clients | Platform & CI | Test the agent with non-Obsidian clients (e.g., JetBrains, Zed); adapt features/protocol. |
| #4 | Align Privacy and Permissions for ACP Agent Used with Obsidian | Platform & CI | Surface privacy-sensitive actions; ensure no undetectable network or file access; document trust model. |
| #3 | Implement Obsidian‑Aware Capabilities for the ACP Agent | UTS-User | Add file/vault access using ACP FS APIs; note-centric workflows; slash command support for UI integration. |
| #2 | Implement Minimal ACP Agent CLI for Obsidian | UTS-User | Build a minimal ACP-compliant CLI agent for Obsidian; communicate via JSON-RPC; support subprocess mode; log protocol events. |
| #1 | ACP Obsidian Agent CLI: Top Priority and Roadmap Change | UTS-User | Epic capturing the reprioritization of features: minimal CLI first; Obsidian-aware capabilities; privacy alignment; multi-client generalization; client/editor contributions. |

## Next Steps

Since Linear’s write API is not accessible and the Linear app is unreachable from this environment, the planning migration cannot be executed programmatically. You can use this document as a guide to manually create the project, epics, and issues in Linear. Each entry above should be copied over, assigned appropriate labels, and linked to its epic.

Once the Linear project is set up, update the GitHub repository’s README or issue templates to point contributors to the new planning location and disable new issue creation in GitHub.
