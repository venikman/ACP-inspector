# Reviewer’s Guide · PR #14 (F# tokenizer spans + Eval integration)

This PR adds a small F#‑subset tokenizer with spans and uses it in `Acp.Eval` to emit advisory findings on code‑like text blocks.

## Intended scope / non‑goals

**Intended use**

- Provide “good‑enough” lexing for Eval heuristics on ACP prompt/toolcall text.
- Surface coarse signals: unknown‑token ratio, unclosed strings, unclosed block comments.

**Non‑goals**

- Full compiler‑accurate F# lexing/parsing.
- Perfect coverage of all F# syntax (active patterns, computation expressions, SRTP edge cases, etc.).
- Emitting semantic findings (type errors, binding issues).

## Span contract (`tokenizeWithSpans`)

- `Position.index` is **0‑based** absolute char offset into the original input.
- `Span.length` is a **positive** char count; token end is **exclusive** at `start.index + length`.
- `Position.line` and `Position.column` are **1‑based**.
- CRLF (`\r\n`) counts as **one** newline; column resets to 1 after any newline.
- Tabs are treated as **one** column step (no visual tab width).

## Falsifiable invariants (what must always hold)

1. **Coverage/contiguity**: spans are ordered, non‑overlapping, and their concatenated slices equal the original input.
2. **Span soundness**: every span has `length > 0`, and `start.index + length <= input.Length`.
3. **Trivia stability**: changing only whitespace or comment contents does not change the non‑trivia token sequence.

These invariants are enforced in `tests/Pbt/TokenizerProperties.fs`.

## Eval acceptance criteria (oracle‑based)

Tokenizer‑driven Eval checks are validated against a tiny golden corpus in `tests/golden/fsharp/`:

- `valid.fs` → no `FSHARP_*` findings.
- `unknown-heavy.fs` → `ACP.EVAL.FSHARP_MANY_UNKNOWN_TOKENS`.
- `unclosed-string.fs` → `ACP.EVAL.FSHARP_UNCLOSED_STRING`.
- `unclosed-block-comment.fs` → `ACP.EVAL.FSHARP_UNCLOSED_BLOCK_COMMENT`.

Defaults live in `Acp.Eval.defaultProfile`:

| Setting                            | Default | Rationale                                                  |
| ---------------------------------- | ------: | ---------------------------------------------------------- |
| `fsharpUnknownTokenRatioThreshold` |    0.10 | tolerate minor unsupported syntax, but warn on non‑F# text |
| `fsharpMinCodeTokenCount`          |       5 | avoid firing on prose / short fragments                    |

## Known limitations / risks

- Heuristics can drift without a corpus/oracle; this golden set is the minimal guardrail.
- Unsupported syntax may be classified as `Unknown` (strings/directives/nested comments dominate edge‑case load).
- Findings are **advisory**; they should not block protocol correctness.

## How to verify

- Run unit + golden tests: `dotnet test tests/ACP.Tests.fsproj`.
- Run tokenizer PBT only: `scripts/run-pbt.sh` (filter `TokenizerProperties` if needed).

## Three reviewer questions

1. Is the **scope** right for Eval (good‑enough subset vs full lexer)?
2. Do the **span contracts/invariants** match how you expect spans to behave?
3. Are the **Eval thresholds** sensible on real ACP prompt/toolcall data?
