# Background Tasks

Agent-executable tasks for ACP Inspector maintenance and development.

## Task Queue

| ID                                                 | Title                                   | Status      | Priority | Assignee |
| -------------------------------------------------- | --------------------------------------- | ----------- | -------- | -------- |
| [TASK-002](TASK-002-fpf-alignment-improvements.md) | FPF Alignment Improvements              | ✅ Done     | High     | Team     |
| [TASK-003](TASK-003-fpf-advanced-features.md)      | FPF Advanced Features (Phase 3)         | Not Started | Low      | Team     |
| [TASK-004](TASK-004-acp-meta-passthrough.md)       | ACP \_meta Passthrough & Domain Support | ✅ Done     | High     | Team     |
| [TASK-005](TASK-005-proxy-chains-support.md)       | Proxy Chains Draft Support              | ✅ Done     | Medium   | Team     |
| [TASK-006](TASK-006-telemetry-export-panel.md)     | Telemetry Export Panel                  | ✅ Done     | Medium   | Team     |
| [TASK-007](TASK-007-agent-registry.md)             | ACP Agent Registry Support              | ✅ Done     | Medium   | Team     |
| [TASK-008](TASK-008-schema-pin-and-ci-watch.md)    | ACP Schema Pin + CI Watchers            | ✅ Done     | High     | Team     |

## Status Legend

- **Pending**: Ready for an agent to pick up
- **In Progress**: Agent actively working
- **Blocked**: Waiting on external input
- **Spec-Wait**: Draft spec still in flux; deferred until upstream stabilizes
- **Review**: Work done, needs human review
- **Done**: Completed and verified

## How to Execute a Task

1. Read the task file completely
2. Check constraints and success criteria
3. Execute scope items in order
4. Create deliverables as specified
5. Update status when done

## Creating New Tasks

Use this template:

````markdown
# TASK-XXX: Title

**Status**: Pending
**Priority**: Low/Medium/High/Critical
**Assignee**: Background Agent / Human
**Created**: YYYY-MM-DD
**Context**: Why this task exists

## Objective

What needs to be done (1-2 sentences)

## Scope

- [ ] Checklist of items

## Deliverables

1. What outputs are expected

## Commands Reference

```bash
# Useful commands
```

## Constraints

- What NOT to do

## Success Criteria

- [ ] How to know it's done
````
