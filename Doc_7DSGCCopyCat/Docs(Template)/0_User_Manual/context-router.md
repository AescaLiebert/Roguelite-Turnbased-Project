# Context Router — What to Load, When

> **Purpose:** Every agent call needs context from this folder.
> This file tells you exactly which files to load for each role.
> Do NOT load everything — load only what your current role needs.
>
> For token budget details, see `Docs/0_User_Manual/RULES_AND_POLICY.md` §8.

---

## Universal Bootstrap (Always Load First)

- `project-stack.md` (~400 tokens) — project identity, tech stack, core systems map

---

## Per-Role Context Pack

| Role | Load | Token Budget | Skip |
|------|------|-------------|------|
| 🔀 Orchestrator | project-stack + task card + workflow index | ~2,000 | GDD sections, source files, ADRs |
| 🎮 Game Designer | project-stack + GDD `@tag:` section + design agent prompt | ~4,000 | Source code, ADRs, other agent prompts |
| 🏗️ Architect | project-stack + GDD `@tag:` + design spec + relevant ADRs + architect prompt | ~6,000 | Full GDD, all ADRs, source files |
| 💻 Implementer | project-stack + arch spec + design spec + affected source files + impl prompt | ~8,000 | Full GDD, unrelated systems |
| 👀 Reviewer | project-stack + git diff + review prompt | ~4,000 | Full files (unless diff is insufficient) |
| 🧪 QA | project-stack + design spec + impl summary + QA prompt | ~4,000 | Source code (unless verifying behavior) |
| 📋 PM Reporter | project-stack + git log + DevLogs + report prompt | ~2,000 | GDD, source code |

---

## Context Resolution Order
 
When setting up an agent call, load context in this order:
 
1. `Docs/1_Inputs_Templates/project-stack.md` — universal bootstrap
2. `Docs/3_Outputs/Specs/{slug}-task-card.md` — current task
3. `Docs/1_Inputs_Templates/GDD_Fighting_Allstar.md @tag:{relevant-tag}` — design truth (**one** section, not the full doc)
4. `Docs/2_System_Files/Agent_Prompts/{role}-agent.md` — current role prompt **only**
5. Affected source files, specs, or ADRs — only when the current role needs them

---

## File Index
 
| File | When to Use | Who Uses It |
|------|------------|-------------|
| `Docs/1_Inputs_Templates/project-stack.md` | Every agent call | All agents |
| `Docs/1_Inputs_Templates/project-context-brief.md` | `/bootstrap-project` only | Bootstrap workflow |
| `Docs/0_User_Manual/context-router.md` | When deciding what to load | Orchestrator, humans setting up agents |
| `Docs/0_User_Manual/prompt-cookbook.md` | Starting a task quickly | Humans who want quick-start recipes |
 
---
 
## Anti-Patterns
 
> [!CAUTION]
> These loading mistakes waste tokens and degrade agent quality:
>
> - ❌ Loading the full GDD (7,000+ tokens) when one `@tag:` section is enough
> - ❌ Loading all agent prompts into one session — only one role is active at a time
> - ❌ Loading `RULES_AND_POLICY.md` in full — reference specific §sections
> - ❌ Loading `project-context-brief.md` outside of `/bootstrap-project` (should be `Docs/1_Inputs_Templates/project-context-brief.md`)
> - ❌ Duplicating conventions inline instead of referencing the canonical source
> - ❌ Feeding the entire repo or `Docs/` folder to a single agent
 
---
 
## Multi-Agent Framework Setup
 
For **CrewAI**, **AutoGen**, **LangGraph**, or **IDE subagents** (Antigravity, Cursor, etc.):
 
1. Load each `Docs/2_System_Files/Agent_Prompts/{role}-agent.md` as that agent's `system_message` or role definition
2. Inject `Docs/1_Inputs_Templates/project-stack.md` as shared context for every agent
3. Use this router table to configure per-agent context injection
4. Route tasks through the orchestrator first — do not skip to implementation
 
For single-agent IDE copilots (GitHub Copilot, Cody, etc.):
- Paste `Docs/1_Inputs_Templates/project-stack.md` into the system prompt
- Use `Docs/0_User_Manual/prompt-cookbook.md` recipes for structured task input
- Reference the relevant `@tag:` section instead of the full GDD
