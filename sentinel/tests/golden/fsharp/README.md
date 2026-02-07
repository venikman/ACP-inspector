# Golden corpus for F# tokenizer + Eval heuristics

Small, hand‑curated F# (and near‑F#) snippets used as a lightweight oracle for:

- `Acp.FSharpTokenizer.tokenizeWithSpans`
- `Acp.Eval` F#‑lexing checks (`FSHARP_*` findings).

Each file has an expected set of Eval finding codes asserted in
`tests/Acp.Eval.Golden.Tests.fs`.

Files:

- `valid.fs` — normal F#; expects **no** Eval findings.
- `unknown-heavy.fs` — code‑like prefix plus many unknown chars; expects
  `ACP.EVAL.FSHARP_MANY_UNKNOWN_TOKENS`.
- `unclosed-string.fs` — unclosed string late in file; expects
  `ACP.EVAL.FSHARP_UNCLOSED_STRING`.
- `unclosed-block-comment.fs` — unclosed block comment late in file; expects
  `ACP.EVAL.FSHARP_UNCLOSED_BLOCK_COMMENT`.

Keep this corpus tiny and update expectations only when tokenizer/Eval contracts change.
