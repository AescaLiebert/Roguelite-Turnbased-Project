---
description: Agent workflow index and router entrypoint
---

# Agent Workflows

IDE workflow automation files for AI-assisted game development.

Compatible with Cursor rules, Claude Code `CLAUDE.md`, Gemini CLI, Windsurf, or any agent that reads markdown workflows.

## Available Workflows

| Workflow | Trigger | Description |
| --- | --- | --- |
| [bootstrap-project.md](bootstrap-project.md) | `/bootstrap-project` | One-time setup: project brief -> filled GDD + project stack |
| [implement-feature.md](implement-feature.md) | `/implement-feature` | Full pipeline: GDD -> Design -> Architecture -> Code -> Review -> QA |
| [fix-bug.md](fix-bug.md) | `/fix-bug` | Investigate -> Fix -> Regression check |
| [code-review.md](code-review.md) | `/code-review` | Review a file, diff, or PR against project conventions |
| [refactor.md](refactor.md) | `/refactor` | Impact analysis -> safe refactor -> regression check |
| [report.md](report.md) | `/report` | Generate a weekly or sprint PM report from git logs and DevLogs |

## Routing
 
For multi-agent work, start with `2_System_Files/Agent_Prompts/orchestrator-agent.md`.
 
The orchestrator must:
 
- Convert Discord, Notion, GitHub, or direct requests into `Docs/3_Outputs/Specs/{slug}-task-card.md`.
- Validate the task card against `Docs/2_System_Files/Handoff_Contracts/README.md`.
- Select exactly one workflow to run next.
- Stop at human checkpoints instead of guessing.
 
## How to Use
 
### In Cursor / Windsurf
 
Copy the workflow markdown files into your project's `.cursorrules` or equivalent config directory.
 
### In Claude Code
 
Reference these workflows in your `CLAUDE.md` file:
 
```markdown
See 2_System_Files/Workflows/ for available workflow commands.
Start multi-agent work with 2_System_Files/Agent_Prompts/orchestrator-agent.md.
```

### In Any AI Chat

Paste the relevant workflow steps into the conversation. For multi-agent work, paste the orchestrator prompt first so the AI routes the task instead of jumping straight into code.
