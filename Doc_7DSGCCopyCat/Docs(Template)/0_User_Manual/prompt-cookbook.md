# Prompt Cookbook — Quick-Start Recipes

> **When to use:** You want to start a task quickly but still follow the pipeline.
> Each recipe shows the minimal structured input that feeds into the correct workflow.
>
For full workflow details, see `Docs/2_System_Files/Workflows/`.
For multi-agent framework setup, see `context-router.md`.

---

## Recipe: New Feature

**Workflow:** `/implement-feature`
**Intake artifact:** `Docs/3_Outputs/Specs/{slug}-task-card.md`

### Minimal Input

```
Feature: {one sentence — what should the player experience?}
GDD Section: @tag:{relevant-tag}
Priority reason: {what does this unblock?}
Systems affected: {from project-stack.md system table}
Files to read first: {specific paths from project-stack.md}
Platform constraint: {primary platform input model}
```

### Pipeline

1. Orchestrator validates task card → routes to `/implement-feature`
2. 🎮 Game Design Agent → design spec with juice parameters
3. **⏸ Human checkpoint: design / game-feel approval**
4. 🏗️ Architect Agent → technical plan + ADR draft
5. **⏸ Human checkpoint: architecture approval**
6. 💻 Implementation Agent → code on feature branch
7. 👀 Code Review Agent → feedback
8. 🧪 QA Agent → test plan
9. **⏸ Human checkpoint: PR review + playtest**
10. 📋 PM Agent → DevLog + sprint report

### What You Do NOT Need to Specify

- Juice details (screen shake, particles, audio) — the Game Design Agent produces these
- Class diagrams or interfaces — the Architect Agent produces these
- Acceptance criteria beyond player-facing goals — QA Agent derives test cases from the design spec

---

## Recipe: Bug Fix

**Workflow:** `/fix-bug`
**Intake artifact:** `Docs/3_Outputs/Specs/{slug}-task-card.md` with type = Bug fix

### Minimal Input

```
Bug: {what's broken — one sentence}
Severity: Critical / Major / Minor / Cosmetic
Repro steps:
  1. {step}
  2. {step}
  3. {step}
Expected: {what should happen per GDD @tag:{section}}
Actual: {what happens instead}
Platform: {which platform, which input method}
Frequency: Always / Sometimes / Rare
```

### Pipeline

1. Orchestrator validates → routes to `/fix-bug`
2. Root cause analysis on affected files
3. Minimal fix implementation
4. Regression check against related systems
5. QA update (if existing test plan covers this area)
6. **⏸ Human checkpoint: PR review**

---

## Recipe: Refactor

**Workflow:** `/refactor`
**Intake artifact:** `Docs/3_Outputs/Specs/{slug}-task-card.md` with type = Refactor

### Minimal Input

```
Goal: {one-sentence refactor goal — be specific, not "clean up code"}
Motivation: @tag:{section} or ADR-{NNN}
Scope: {which files / systems}
Not in scope: {explicit non-goals to prevent scope creep}
Behavior change: None (refactor only)
```

### Pipeline

1. Orchestrator validates → routes to `/refactor`
2. 🏗️ Architect Agent → impact analysis with before/after class responsibility table
3. **⏸ Human checkpoint: refactor scope + architecture approval**
4. ADR draft (if architecture decision changes)
5. 💻 Incremental implementation — one commit per logical change
6. 👀 Self-review focused on "no behavior change"
7. Regression checklist
8. **⏸ Human checkpoint: PR review**

> [!WARNING]
> Refactors must NOT add features, fix bugs, or change behavior. If you discover a bug during refactor, file a separate task card.

---

## Recipe: Code Review

**Workflow:** `/code-review`
**Intake:** Git diff, PR reference, or code file

### Minimal Input

```
PR / Diff: {reference or branch name}
Task card: {slug of the original task}
Focus: Architecture / Performance / Conventions / All
```

### Pipeline

1. 👀 Code Review Agent evaluates against:
   - Architecture spec (if exists)
   - `Docs/0_User_Manual/RULES_AND_POLICY.md` conventions
   - Performance rules (§5)
   - Naming conventions (§2)
2. Produces actionable feedback with line references
3. **⏸ Human checkpoint: approve, request changes, or discuss**

---

## Recipe: Sprint Report

**Workflow:** `/report`
**Intake:** Date range or sprint label

### Minimal Input

```
Period: {start date} to {end date}
Source: git log + Docs/3_Outputs/DevLog/{relevant entries}
Audience: team / stakeholder / personal
```

### Pipeline

1. 📋 PM Reporter Agent gathers git log + DevLog entries
2. Produces formatted sprint report at `Docs/3_Outputs/PM_Reports/`
3. **⏸ Human checkpoint: review before team publishing**

---

## When NOT to Use These Recipes

| Situation | Do This Instead |
|-----------|----------------|
| Setting up a multi-agent pipeline | Load `Docs/2_System_Files/Agent_Prompts/{role}-agent.md` as system prompts; see `context-router.md` |
| First time bootstrapping a project | Run `/bootstrap-project` with a filled `Docs/1_Inputs_Templates/project-context-brief.md` |
| The GDD doesn't exist yet | Fill `Docs/1_Inputs_Templates/project-context-brief.md` first, then run `/bootstrap-project` |
| You need to write a design spec yourself | Use the Game Design Agent's output format from `Docs/2_System_Files/Agent_Prompts/game-design-agent.md` |

---

## Quick Reference: Intake → Workflow → First Agent

| You Have | Workflow | First Agent |
|----------|----------|-------------|
| A feature idea | `/implement-feature` | 🎮 Game Designer |
| A bug report | `/fix-bug` | 🔀 Orchestrator → direct to implementation |
| Code that needs restructuring | `/refactor` | 🏗️ Architect (impact analysis) |
| A diff or PR to evaluate | `/code-review` | 👀 Reviewer |
| End of sprint | `/report` | 📋 PM Reporter |
| Empty project | `/bootstrap-project` | Fill `Docs/1_Inputs_Templates/project-context-brief.md` first |
