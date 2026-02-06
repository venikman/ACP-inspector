#!/usr/bin/env bash
set -euo pipefail

CODEX_ROOT="${1:-.codex}"

if [[ ! -d "$CODEX_ROOT/prompts" ]]; then
  echo "FAIL: missing $CODEX_ROOT/prompts" >&2
  exit 1
fi
if [[ ! -d "$CODEX_ROOT/skills" ]]; then
  echo "FAIL: missing $CODEX_ROOT/skills" >&2
  exit 1
fi

# 1) No Claude-only plugin root references should remain.
if rg --hidden -n '\$\{CLAUDE_PLUGIN_ROOT\}' "$CODEX_ROOT" >/dev/null; then
  echo "FAIL: found \${CLAUDE_PLUGIN_ROOT} references in $CODEX_ROOT" >&2
  rg --hidden -n '\$\{CLAUDE_PLUGIN_ROOT\}' "$CODEX_ROOT" | head -n 50 >&2 || true
  exit 1
fi

# 1b) Known-bad references from upstream conversion (should be patched/removed).
if rg --hidden -n '\$agent-' "$CODEX_ROOT" >/dev/null; then
  echo "FAIL: found \$agent-* references in $CODEX_ROOT (expected ce-* skills)" >&2
  rg --hidden -n '\$agent-' "$CODEX_ROOT" | head -n 50 >&2 || true
  exit 1
fi
if rg --hidden -n '\$general-purpose\b' "$CODEX_ROOT" >/dev/null; then
  echo "FAIL: found \$general-purpose references in $CODEX_ROOT (expected ce-general-purpose)" >&2
  rg --hidden -n '\$general-purpose\b' "$CODEX_ROOT" | head -n 50 >&2 || true
  exit 1
fi

# 2) Each prompt should reference an existing skill directory.
while IFS= read -r prompt; do
  skill_line="$(rg -n '^Use the \$' "$prompt" || true)"
  skill_ref="$(printf '%s\n' "$skill_line" | head -n 1 | sed -E 's/^.*Use the \$([^ ]+).*$/\1/')"
  if [[ -z "$skill_ref" ]]; then
    echo "FAIL: could not find 'Use the $<skill>' line in $prompt" >&2
    exit 1
  fi
  if [[ ! -d "$CODEX_ROOT/skills/$skill_ref" ]]; then
    echo "FAIL: prompt $prompt references missing skill dir: $CODEX_ROOT/skills/$skill_ref" >&2
    exit 1
  fi
  if [[ ! -f "$CODEX_ROOT/skills/$skill_ref/SKILL.md" ]]; then
    echo "FAIL: missing SKILL.md for skill: $CODEX_ROOT/skills/$skill_ref" >&2
    exit 1
  fi
done < <(find "$CODEX_ROOT/prompts" -maxdepth 1 -type f -name '*.md' | sort)

# 3) Every referenced $ce-* skill should exist as a directory.
skill_refs="$(rg --hidden --no-filename -o '\$ce-[a-z0-9_-]+' "$CODEX_ROOT/prompts" "$CODEX_ROOT/skills" | sed 's/^\$//' | sort -u || true)"
if [[ -n "$skill_refs" ]]; then
  while IFS= read -r s; do
    [[ -z "$s" ]] && continue
    if [[ ! -d "$CODEX_ROOT/skills/$s" ]]; then
      echo "FAIL: referenced skill is missing: $CODEX_ROOT/skills/$s" >&2
      exit 1
    fi
  done <<<"$skill_refs"
fi

# 4) Every referenced /prompts:ce-* should exist as a prompt file.
prompt_refs="$(rg --hidden --no-filename -o '/prompts:ce-[a-z0-9_-]+' "$CODEX_ROOT/prompts" "$CODEX_ROOT/skills" | sed 's@^/prompts:@@' | sort -u || true)"
if [[ -n "$prompt_refs" ]]; then
  while IFS= read -r p; do
    [[ -z "$p" ]] && continue
    if [[ ! -f "$CODEX_ROOT/prompts/$p.md" ]]; then
      echo "FAIL: referenced prompt is missing: $CODEX_ROOT/prompts/$p.md" >&2
      exit 1
    fi
  done <<<"$prompt_refs"
fi

echo "OK"
