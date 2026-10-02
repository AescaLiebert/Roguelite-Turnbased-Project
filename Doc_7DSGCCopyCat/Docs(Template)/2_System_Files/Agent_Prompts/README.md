# Agent System Prompts

Ready-to-use system prompts for each role in the multi-agent workflow.

## Prerequisites

Run `/bootstrap-project` first. These prompts reference `Docs/1_Inputs_Templates/project-stack.md`, which is empty until the project context brief has been filled and bootstrap has generated real context.

## How to Use

1. Run `/bootstrap-project` to fill in your GDD and project stack.
2. Start multi-agent work with `orchestrator-agent.md`.
3. Load `project-stack.md` as the universal context for every agent call.
4. Load the current task card or current artifact.
5. Load the prompt for the current role only.
6. Load the relevant GDD `@tag:` section for the task, not the full GDD.

## Agent Roster

| Agent | File | Input | Output | Saves To |
| --- | --- | --- | --- | --- |
| Router | [orchestrator-agent.md](orchestrator-agent.md) | Task, issue, Discord/Notion summary, artifact status | Workflow route, context pack, next checkpoint | `Docs/3_Outputs/Specs/` |
| Game Designer | [game-design-agent.md](game-design-agent.md) | Approved task card or GDD section | Design spec with juice/feel parameters | `Docs/3_Outputs/Specs/` |
| Architect | [architect-agent.md](architect-agent.md) | Approved design spec | Technical plan, class diagrams, ADR draft | `Docs/3_Outputs/Specs/` |
| Implementer | [implementation-agent.md](implementation-agent.md) | Approved architecture spec + design spec | Production C# code | `Assets/` |
| Reviewer | [code-review-agent.md](code-review-agent.md) | Git diff or code file | Review with actionable feedback | PR comment or `Docs/3_Outputs/Specs/` |
| QA | [qa-agent.md](qa-agent.md) | Feature spec + implementation summary | Test plan + edge cases | `Docs/3_Outputs/TestPlans/` |
| PM Reporter | [pm-report-agent.md](pm-report-agent.md) | Git log + DevLogs | Formatted sprint report | `Docs/3_Outputs/PM_Reports/` |

## Pipeline Order

```text
Task card (Docs/3_Outputs/Specs/{slug}-task-card.md)
  -> Router -> workflow + context pack
    -> Game Design Agent -> design spec (Docs/3_Outputs/Specs/)
      -> Human design checkpoint
        -> Architect Agent -> technical plan + ADR draft (Docs/3_Outputs/Specs/)
          -> Human architecture checkpoint
            -> Implementation Agent -> code on branch
              -> Code Review Agent -> feedback
              -> QA Agent -> test plan (Docs/3_Outputs/TestPlans/)
                -> Human playtest / PR review
                  -> PM Agent -> sprint report (Docs/3_Outputs/PM_Reports/)
```

## Context Loading

> For the full per-role context table, see `Docs/0_User_Manual/context-router.md`.

Per agent call:

1. Load `project-stack.md` for universal bootstrap context.
2. Load the current task card or current artifact.
3. Load the relevant GDD `@tag:` section.
4. Load the current agent prompt only.
5. Load affected source files only when the current role requires them.

Avoid loading all prompts, the full GDD, full `RULES_AND_POLICY.md`, or entire `Docs/` directories at once.

For framework-based pipelines such as CrewAI, AutoGen, or LangGraph, load each markdown prompt as that agent's `system_message` or role definition.
