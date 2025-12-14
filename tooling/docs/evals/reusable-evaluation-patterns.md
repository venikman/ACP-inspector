# Reusable Evaluation Patterns (User-Trace-Spec Model)

- ArtifactId: ACP-EVAL-PATTERNS-001
- Family: PedagogicalCompanion
- Pattern: User–Trace–Spec (UTS evaluation loop)
- Scope: Evaluation of ACP-sentinel and adjacent AI coding tools

## User-Level Patterns (User Perspective)
- Leverage real and realistic user scenarios: Use production traces and high-fidelity synthetic inputs as test cases; convert ambiguous or malformed user instructions into evaluable prompts so the suite reflects actual user needs.
- Define user-centric success metrics: Specify what “good” means (correctness, relevance, completeness, proper use of context, tone) and score outputs against those criteria with LLM judges or human reviewers.
- Incorporate user feedback and human review: Let users or labelers flag issues; treat low ratings or qualitative notes as first-class data and feed them back into offline evals to gate deployments.
- Avoid “vibe-check” development: Maintain a broad regression suite of prompts (including past bugs) and require new models or prompts to pass it before release instead of ad-hoc LGTM testing.

## Trace-Level Patterns (Trace/Process Perspective)
- Capture and utilize full execution traces: Log inputs, intermediate reasoning, tool calls and outputs, and the final response so each run can be replayed for debugging and test creation.
- Perform open-ended error analysis on traces: Annotate traces freely to surface failure observations (e.g., missed steps, repeated clarifications) beyond predefined labels.
- Cluster failures into recurring modes and label them: Group observations into failure modes (date handling errors, tool selection mistakes, incomplete follow-up, etc.) and tag traces to quantify prevalence and guide fixes.
- Create targeted trace-level tests (unit/integration evals): Reproduce multi-step scenarios deterministically (stub APIs or reuse stored tool outputs) and assert correctness of each critical decision.
- Evaluate tool use and reasoning completeness: Check for tool misuse and premature stopping; verify correct API selection, handling of responses, retries, and follow-through on reasoning branches.
- End-to-end trace coverage (multi-agent and full workflows): Trace across agent boundaries to catch context propagation issues and misalignments in orchestrated flows.
- Close the loop with continuous trace-driven improvement: Turn failed or low-scoring production traces into regression tests; track coverage of failure modes and code paths to find blind spots.

## Spec-Level Patterns (Specification Perspective)
- Set explicit success criteria from the start: Treat eval design as part of product design; each requirement becomes a metric or checker.
- Choose evaluator types based on the metric’s nature:
  - Code-based: Deterministic checks for format, JSON validity, numeric accuracy, required keywords, or other objective rules.
  - LLM-as-judge: Rubric-driven judgments for relevance, coherence, reasoning quality, tone, and other subjective qualities; keep prompts specific and favor simple pass/fail when possible.
- Combine multiple metrics for robust evaluation: Pair critical pass/fail checks (e.g., factual accuracy, safety) with weighted secondary scores (style, completeness); penalize critical failures heavily.
- Version-control your evals and keep them in sync with code: Track datasets, prompts, rubrics, and scripts alongside model and application versions to ensure reproducibility and comparability.
- Measure and expand spec coverage: Map requirements and failure modes to eval IDs; add tests for new features or newly discovered errors to avoid “unknown unknowns.”
- Iterate and refine evaluators as the system evolves: Adjust criteria, thresholds, and rubrics when noise or gaps appear so metrics stay aligned with current product expectations.
- Address specification and generalization failures: Clarify underspecified instructions, and broaden eval and training data to cover novel or edge scenarios where the model falters.
