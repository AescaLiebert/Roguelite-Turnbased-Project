# Multi-Agentic AI Workflow for Game Development
## From Chatbot Tinder → Structured Autonomous Pipeline

---

## 1. Where You Are Now (Level 0)

```
YOU (Human) ──prompt──► AI Chatbot A ──code──► Unity Project
             ──prompt──► AI Chatbot B ──code──►
             ──prompt──► AI Chatbot C ──code──►
```

**Pain points:**
- You are the bottleneck — every task requires your prompt, your review, your merge
- No QA, no code review, no architectural guard-rails
- No documentation trail for PM
- Game feel decisions live only in your head
- Merge conflicts from parallel AI outputs
- No file organization or hierarchy conventions

---

## 2. Where You Want To Be (Level 4)

```
Game Design Doc (GDD) ──► Agent Orchestrator
                              │
                ┌─────────────┼─────────────────┐
                ▼             ▼                  ▼
         🎮 Design Agent  🏗️ Architect Agent  📋 PM Agent
              │                │                  │
              ▼                ▼                  ▼
         🧪 QA Agent     👀 Review Agent    📊 Report Generator
                               │
                               ▼
                         Git + CI/CD
```

**You don't need to jump straight there.** Below is a 5-level maturity ladder.

---

## 3. The Maturity Ladder

### Level 0 → Level 1: Structured Prompting (⏱️ 1 week)

> **Goal:** Same tools, but with discipline.

| What to do | How |
|---|---|
| Create a **Game Design Document (GDD)** as a living markdown file | Store in repo at `Docs/GDD/GDD_Fighting_Allstar.md` (use `_TEMPLATE.md`) |
| Create **Architecture Decision Records (ADRs)** | `Docs/ADRs/001-{decision}.md` |
| Use **prompt templates** instead of freeform chat | See `AI-Context/feature-prompt-template.md` |
| Adopt **conventional commits** | `feat(inventory):`, `fix(survival):`, `refactor(ui):` |
| Write a **CHANGELOG.md** | Auto-generated from commit messages |
| Create **RULES_AND_POLICY.md** | File org, hierarchy, optimization, AI rules |

**Prompt Templates to create:**
```
📋 Feature Prompt Template:
## Context
- GDD Reference: [section]
- Architecture: [relevant ADR]
- Affected Systems: [list]

## Task
[specific implementation task]

## Constraints
- Must follow [architectural pattern]
- Must not break [existing system]
- Game feel target: [description]

## Acceptance Criteria
- [ ] Functional requirement
- [ ] Performance requirement
- [ ] Game feel requirement
```

---

### Level 1 → Level 2: Specialized AI Roles (⏱️ 2-3 weeks)

> **Goal:** Use different AI sessions with different system prompts for different roles.

Define these **agent personas** as reusable system prompts:

#### 🎮 Game Design Agent
```
Role: Game Designer focused on feel, juice, and player experience.
Input: Feature request or GDD section
Output: Detailed implementation spec with:
  - Player interaction flow
  - Feedback loops (visual, audio, haptic)
  - Juice parameters (screen shake, particles, easing curves)
  - Edge cases from player perspective
Does NOT write code. Writes specs.
```

#### 🏗️ Architect Agent
```
Role: Software Architect for Unity multiplayer games.
Input: Design spec from Game Design Agent
Output:
  - Class diagram / system diagram
  - Interface definitions
  - Data flow
  - Networking authority decisions (server vs client)
  - Which existing systems are affected
References: ADRs, existing codebase architecture
```

#### 💻 Implementation Agent (your current workflow)
```
Role: Unity C# Developer
Input: Architecture spec + Design spec
Output: Implementation code following the spec exactly
Constraints: Must follow existing patterns, naming conventions, 
             architecture decisions
```

#### 👀 Code Review Agent
```
Role: Senior Unity Developer / Code Reviewer
Input: Git diff or code file
Output:
  - Bug risks
  - Architecture violations
  - Performance concerns
  - Missing edge cases
  - Naming/convention issues
Tone: Constructive, specific, actionable
```

#### 🧪 QA Agent
```
Role: QA Engineer for multiplayer Unity games
Input: Feature spec + implementation
Output:
  - Test plan (manual + automated)
  - Edge cases to test
  - Multiplayer desync scenarios
  - Regression risks
  - Bug report template for issues found
```

#### 📋 PM Reporting Agent
```
Role: Technical Project Manager
Input: Git log, commit messages, branch diffs
Output:
  - Sprint summary document
  - Feature completion status
  - Risk assessment
  - Blocker list
  - Formatted markdown report for stakeholders
```

**Workflow at this level:**
```
You write GDD section
    └──► Game Design Agent → spec
           └──► Architect Agent → technical plan
                  └──► You implement (with Implementation Agent)
                         └──► Code Review Agent → feedback
                                └──► QA Agent → test plan
                                       └──► PM Agent → report
```

You're still the orchestrator, but each step has a defined input/output contract.

---

### Level 2 → Level 3: Automation Glue (⏱️ 1-2 months)

> **Goal:** Automate the handoffs between agents.

#### Option A: Script-Based (Simpler)

Create a CLI tool or set of scripts that:

```
game-pipeline feature "Add parry timing system"
```

1. Reads the GDD for context
2. Calls Design Agent API → saves spec to `Docs/Specs/parry-timing-design-spec.md`
3. Calls Architect Agent API → saves plan to `Docs/Specs/parry-timing-arch-plan.md`
4. Presents both for your approval (**human-in-the-loop checkpoint**)
5. After approval, creates a feature branch
6. Calls Implementation Agent → commits code
7. Calls Review Agent on the diff → saves review to PR comment
8. Calls QA Agent → saves test plan
9. After you verify → calls PM Agent → generates report

**Tech stack for glue:**
- Python + OpenAI/Anthropic/Google API
- Or Node.js with LangChain / CrewAI / AutoGen
- Git hooks for automatic review triggers

#### Option B: Framework-Based (More Powerful)

Use an existing multi-agent framework:

| Framework | Best For | Complexity |
|---|---|---|
| **CrewAI** | Role-based agent teams, sequential/parallel tasks | ⭐⭐ Low |
| **AutoGen (Microsoft)** | Multi-agent conversations, flexible topologies | ⭐⭐⭐ Medium |
| **LangGraph** | Complex stateful workflows with branching | ⭐⭐⭐⭐ High |
| **OpenAI Agents SDK** | If you're all-in on OpenAI ecosystem | ⭐⭐ Low |

**Recommended: Start with CrewAI** — it maps perfectly to your agent roles.

```python
# Pseudocode: CrewAI pipeline
from crewai import Agent, Task, Crew

design_agent = Agent(role="Game Designer", goal="...", backstory="...")
architect_agent = Agent(role="Software Architect", goal="...", backstory="...")
review_agent = Agent(role="Code Reviewer", goal="...", backstory="...")
qa_agent = Agent(role="QA Engineer", goal="...", backstory="...")
pm_agent = Agent(role="PM Reporter", goal="...", backstory="...")

crew = Crew(
    agents=[design_agent, architect_agent, review_agent, qa_agent, pm_agent],
    tasks=[design_task, arch_task, review_task, qa_task, pm_task],
    process=Process.sequential  # or hierarchical with a manager
)

result = crew.kickoff(inputs={"feature": "parry timing system"})
```

---

### Level 3 → Level 4: Full Pipeline with CI/CD (⏱️ 2-3 months)

> **Goal:** Agents integrated into your Git workflow with human checkpoints.

```
┌─────────────────────────────────────────────────────────────┐
│                    TRIGGER: You push GDD change             │
└──────────────────────────┬──────────────────────────────────┘
                           │
                           ▼
┌──────────────────────────────────────────────────────────────┐
│  1. DESIGN PHASE (Automated)                                 │
│     • Design Agent reads GDD diff                            │
│     • Generates implementation spec                          │
│     • Creates PR with spec for your review                   │
│     ⚠️ HUMAN CHECKPOINT: Approve spec                        │
└──────────────────────────┬───────────────────────────────────┘
                           │ approved
                           ▼
┌──────────────────────────────────────────────────────────────┐
│  2. ARCHITECTURE PHASE (Automated)                           │
│     • Architect Agent reads spec + codebase                  │
│     • Generates technical plan + class diagrams              │
│     • Updates ADRs if needed                                 │
│     ⚠️ HUMAN CHECKPOINT: Approve architecture                │
└──────────────────────────┬───────────────────────────────────┘
                           │ approved
                           ▼
┌──────────────────────────────────────────────────────────────┐
│  3. IMPLEMENTATION PHASE (Automated with supervision)        │
│     • Implementation Agent writes code on feature branch     │
│     • Review Agent auto-reviews each commit                  │
│     • QA Agent generates test cases                          │
│     ⚠️ HUMAN CHECKPOINT: Review PR before merge              │
└──────────────────────────┬───────────────────────────────────┘
                           │ merged
                           ▼
┌──────────────────────────────────────────────────────────────┐
│  4. REPORTING PHASE (Fully Automated)                        │
│     • PM Agent scans merged PRs                              │
│     • Generates weekly/sprint report                         │
│     • Pushes to Docs/Reports/ or Notion/Confluence           │
│     • No human action needed                                 │
└──────────────────────────────────────────────────────────────┘
```

**CI/CD Integration:**
- **GitHub Actions** or **GitLab CI** triggers agents on PR events
- **Unity Cloud Build** for automated builds after merge
- **Discord/Slack webhook** for notifications

---

## 4. Game Feel / Juice — Special Considerations

Game feel is the hardest thing to automate because it's subjective. Here's how to handle it:

### The "Juice Spec" Template

> The canonical Juice Spec template is in `AgentPrompts/game-design-agent.md` §4.
> It covers: Visual Feedback (screen shake, particles, UI, camera), Audio Feedback (SFX, music), Physical Feedback (haptics), Timing (input buffer, coyote time, hitstop, recovery), and References.

### The Feedback Loop
```
Design Agent writes Juice Spec
    └──► You playtest in editor
           └──► You record video/GIF of the feel
                  └──► Design Agent reviews recording + suggests tweaks
                         └──► Iterate until it feels right
```

> [!IMPORTANT]
> **Game feel ALWAYS requires human-in-the-loop.** The agents can propose parameters, write the code, and iterate on feedback — but YOU must play it and say "this doesn't feel right" or "more punch."

---

## 5. Git → PM Report Automation

This is the easiest thing to automate today. You can do it in Level 1.

### Minimum Viable Setup (Today)

```bash
# Generate a sprint report from git log
git log --since="2 weeks ago" --pretty=format:"- %s (%h, %an, %ar)" \
    --no-merges > sprint_report_raw.txt

# Feed to AI for formatting
# Prompt: "Format this git log into a PM sprint report with 
#          sections: Completed Features, Bug Fixes, Refactors, 
#          Known Issues. Add risk assessment."
```

### Better: GitHub Actions Auto-Report

```yaml
# .github/workflows/sprint-report.yml
name: Weekly Sprint Report
on:
  schedule:
    - cron: '0 9 * * 1'  # Every Monday 9 AM
jobs:
  report:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0
      - name: Generate Report
        run: |
          python scripts/generate_report.py \
            --since "1 week ago" \
            --output "Docs/Reports/$(date +%Y-%m-%d).md"
      - name: Create PR with report
        uses: peter-evans/create-pull-request@v5
```

---

## 6. Recommended Immediate Action Plan

### This Week (Level 0 → 1)
- [ ] Create `Docs/GDD/GDD_Fighting_Allstar.md` from `_TEMPLATE.md` — document your game design decisions
- [ ] Create `Docs/ADRs/` — start recording architecture decisions
- [ ] Copy `RULES_AND_POLICY.md` into your AI tool config
- [ ] Adopt conventional commits
- [ ] Create prompt templates for each agent role (just markdown files)
- [ ] Set up the git → PM report script

### Next 2 Weeks (Level 1 → 2)
- [ ] Create system prompt files for each agent role
- [ ] Practice the Design → Architect → Implement → Review → QA flow manually
- [ ] Identify which handoffs are most painful (automate those first)

### Month 2 (Level 2 → 3)
- [ ] Evaluate CrewAI / AutoGen — build a prototype pipeline for one feature
- [ ] Create the `game-pipeline` CLI tool
- [ ] Add human-in-the-loop checkpoints

### Month 3+ (Level 3 → 4)
- [ ] Integrate with GitHub Actions
- [ ] Add automated test generation
- [ ] Full CI/CD pipeline with agent triggers

---

## 7. Tool Recommendations

| Category | Tool | Why |
|---|---|---|
| **Agent Framework** | CrewAI | Simplest role-based multi-agent setup |
| **LLM Provider** | Claude / GPT-4 / Gemini | Mix and match per agent role |
| **IDE Agent** | Cursor / Windsurf / Claude Code / Gemini CLI | Implementation agent |
| **MCP Server** | Unity MCP (already have) | Direct Unity Editor integration |
| **CI/CD** | GitHub Actions | Free, great ecosystem |
| **Docs** | Obsidian / Notion | GDD and ADR management |
| **PM Reports** | Markdown in repo | Version controlled, auto-generated |
| **Communication** | Discord webhooks | Agent notifications |

---

## 8. Anti-Patterns to Avoid

> [!CAUTION]
> - **Don't fully automate game feel.** Always keep human-in-the-loop for subjective quality.
> - **Don't skip the spec phase.** Code-first with AI leads to architectural debt faster than humans.
> - **Don't let agents merge to main.** Always have a human approval before merge.
> - **Don't use one mega-prompt.** Specialized agents with focused system prompts outperform generalist prompts.
> - **Don't ignore context windows.** Feed agents only the relevant files, not the whole repo. Use RAG or targeted file selection.

---

## TL;DR

| Level | Effort | You Do | Agents Do |
|---|---|---|---|
| **0 (Now)** | - | Everything | Chat responses |
| **1** | 1 week | GDD + templates + review | Structured responses |
| **2** | 2-3 weeks | Orchestrate + approve | Design, review, QA, report |
| **3** | 1-2 months | Approve checkpoints | Full pipeline execution |
| **4** | 2-3 months | Play-test + final approve | Everything else |

**Start with Level 1 this week. It's free and immediately impactful.**
