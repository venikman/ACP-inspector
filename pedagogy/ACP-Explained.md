# Agent Client Protocol (ACP) in non‑technical terms

- ArtifactId: ACP-EXPLAINED-001
- Family: PedagogicalCompanion
- Audience: Product / leadership / non-implementers
- Status: Stable draft

ACP is a common “language” that lets code editors (like VS Code, JetBrains IDEs, etc.) talk to AI coding assistants in a consistent way. It’s an open standard created so any editor can plug into any compatible AI coding tool without custom one‑off integrations.

## Simple explanation

Agent Client Protocol (ACP) is a standard way for coding tools and AI helpers to work together. It lets software editors and AI coding assistants “plug into” each other using the same set of rules, so you’re not locked into one editor or one AI vendor. That’s it at a high level.

## Analogy: a universal power adapter for AI coding tools

Today, every AI coding assistant tends to have its own special plug:

- Editor A wants integration style A.
- Editor B wants integration style B.
- Each AI assistant has to build custom connectors for each editor.

ACP is like inventing a universal plug:

- If your editor supports ACP, it can work with any AI coding assistant that also supports ACP.
- If your AI assistant supports ACP, it can work inside any ACP‑compatible editor.

So instead of dozens of one‑off integrations, everyone agrees on one standard way to talk.

## What problem is ACP solving?

Right now, without ACP:

- Each editor has to build a separate, custom integration for each AI agent it wants to support.
- Each AI agent has to implement different APIs for each editor it wants to run in.
- Teams can end up locked in to a specific editor or vendor because that’s where the AI integration exists.

ACP’s goal:

- Reduce integration overhead.
- Increase compatibility between tools.
- Avoid lock‑in so teams can choose the editor they like and the AI agent they like, independently.

## How ACP works conceptually (still non‑technical)

You can think of ACP as a shared script that both sides agree to follow:

- The editor is where the human is typing and viewing code.
- The AI agent is the assistant that can read, understand, and propose changes to that code.
- ACP defines how the editor asks the agent questions (e.g., “Help me refactor this file”), how the agent asks for information (e.g., “Show me the contents of these files”), and how the agent sends back results (e.g., suggested code changes, diffs, explanations).

You don’t need to know the technical details. The key is: everyone follows the same script, so they can understand each other.

## Concrete example

Imagine a team where:

- One developer uses VS Code.
- Another uses a JetBrains IDE.
- Another uses the Zed editor.

The company wants to standardize on a single AI coding assistant.

Without ACP:

- The AI vendor has to build and maintain three separate integrations.
- If one editor is not supported, those users are left out.
- Switching AI vendors later could mean redoing all integrations.

With ACP:

- Each editor supports ACP once.
- The AI assistant supports ACP once.
- The assistant then works across all those editors automatically, via the shared protocol.

If the team later wants to try a different AI assistant that also speaks ACP, they can swap it in without changing their editors.

## What ACP is not

To avoid confusion:

- ACP is not an AI model or product by itself.
- ACP is not a coding assistant.
- ACP is not limited to one company’s ecosystem.

It’s an open standard that anyone can implement, similar in spirit to other interoperability standards that let tools from different vendors work together.

## Executive‑level summary (ready to reuse)

Agent Client Protocol (ACP) is an open standard that makes AI coding assistants portable across development tools. Instead of every editor and every AI assistant needing a custom integration with each other, ACP defines a common way for them to communicate. This reduces integration cost, prevents vendor lock‑in, and lets teams choose the best combination of editor and AI assistant for their workflow.
