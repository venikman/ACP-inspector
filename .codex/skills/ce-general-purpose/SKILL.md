---
name: ce-general-purpose
description: Utility shim for converted Compound prompts that ask to "use the general-purpose skill" to run a one-off instruction.
---

# General Purpose Shim

Some converted Compound prompts reference `$ce-general-purpose` as a placeholder for “spawn a generic subagent to do X”.

In Codex, treat the quoted instruction as a work item:

1. Execute the instruction in the current thread using available tools (shell, file edits, etc.).
2. If it contains a `/prompts:...` reference, run that prompt (or follow its corresponding skill).
3. Report results concisely and include any validation commands you ran.

If the work splits cleanly into independent shell reads/commands, parallelize those with `multi_tool_use.parallel`.

