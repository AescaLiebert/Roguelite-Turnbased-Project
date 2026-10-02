# Agent Entry Point

Use this file as the always-loaded project instruction layer for Codex, Antigravity, and other coding agents.

Do not paste the whole documentation stack into chat. Load docs on demand using the routing rules below.

## Project Docs Location

The reusable agent workflow system lives in:

- `Docs(Template)/README.md`
- `Docs(Template)/RULES_AND_POLICY.md`
- `Docs(Template)/TEAM_SYNC_POLICY.md`
- `Docs(Template)/AgentPrompts/`
- `Docs(Template)/.agent/workflows/`
- `Docs(Template)/Handoff-Contracts/`

If this template has been copied into a real project as `Docs/`, prefer `Docs/` over `Docs(Template)/`.

## First Rule

For multi-agent work, route before coding:

1. Read `Docs(Template)/AgentPrompts/orchestrator-agent.md`.
2. Read `Docs(Template)/Handoff-Contracts/README.md`.
3. Create or validate a task card from `Docs(Template)/Handoff-Contracts/task-card-template.md`.
4. Select exactly one workflow from `Docs(Template)/.agent/workflows/`.
5. Stop at human checkpoints for design feel, architecture, PR merge, team publishing, CI/build settings, dependencies, secrets, or destructive operations.

## Context Loading Policy

Load only what the current task needs:

- Universal context: `Docs(Template)/GDD/AI-Context/project-stack.md`
- Current task: `Docs(Template)/Specs/{slug}-task-card.md`
- Design truth: relevant `@tag:` section from `Docs(Template)/GDD/GDD.md`, if it exists
- Workflow: one file from `Docs(Template)/.agent/workflows/`
- Role prompt: one file from `Docs(Template)/AgentPrompts/`
- Rules: only relevant sections of `Docs(Template)/RULES_AND_POLICY.md`

Avoid loading:

- All agent prompts at once
- The full GDD when one `@tag:` section is enough
- Entire `Docs(Template)/Specs/`, `TestPlans/`, `ADRs/`, or `Reports/`
- Historical docs marked superseded

## Common Commands

When the user asks for:

- New feature: route to `/implement-feature`
- Bug fix: route to `/fix-bug`
- Refactor: route to `/refactor`
- Review: route to `/code-review`
- Report or sprint summary: route to PM/report workflow
- New project setup: route to `/bootstrap-project`

## Team Tools

Discord and Notion are not canonical memory by default.

Follow `Docs(Template)/TEAM_SYNC_POLICY.md`:

- Repo markdown is the source of truth.
- Notion is a planning/readability layer unless a sync workflow is explicitly approved.
- Discord is discussion/notification until summarized into a task card, issue, GDD update, ADR, DevLog, or report.

## Safety

Never invent project facts. If the GDD, task card, project stack, or inspected files do not document something, say what is missing and ask for the smallest useful clarification.

Never auto-merge, publish official team status, modify CI/build/dependency/secret configuration, or perform destructive operations without explicit human approval.
