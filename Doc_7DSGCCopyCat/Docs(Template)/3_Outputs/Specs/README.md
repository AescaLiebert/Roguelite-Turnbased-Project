# Design Specs, Task Cards, and Architecture Plans

Output directory for task intake, Game Design Agent specs, Architect Agent plans, and refactor impact analyses.

## Naming Convention

| Type | Pattern | Example |
| --- | --- | --- |
| Task Card | `{feature-name}-task-card.md` | `parry-timing-task-card.md` |
| Design Spec | `{feature-name}-design-spec.md` | `parry-timing-design-spec.md` |
| Architecture Plan | `{feature-name}-arch-plan.md` | `parry-timing-arch-plan.md` |
| Impact Analysis | `{refactor-name}-impact-analysis.md` | `inventory-split-impact-analysis.md` |
| Review Export | `{feature-name}-review.md` | `parry-timing-review.md` |

## Lifecycle

1. Orchestrator Agent creates or validates a task card and saves it here.
2. Game Design Agent reads the approved task card and writes a design spec.
3. Architect Agent reads the design spec and writes an architecture plan or impact analysis.
4. Implementation Agent reads approved specs and writes code.
5. Code Review Agent and QA Agent read only the artifacts for the current slug.
6. After merge, specs remain as historical reference. Do not delete them.

## Handoff Contract
 
All specs should follow `Docs/2_System_Files/Handoff_Contracts/README.md`.
 
Start new work from `Docs/1_Inputs_Templates/Task_Card_Template.md`.
 
## Context Loading Rule
 
Agents should load only the artifact relevant to their current task, not the entire directory.
 
See `Docs/0_User_Manual/RULES_AND_POLICY.md` Section 8 for token budget policy.
