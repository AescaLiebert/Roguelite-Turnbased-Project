---
slug: "{kebab-case-task}"
status: draft
source: github|discord|notion|manual
gdd_tags:
  - "{tag-name}"
owner: "{human-or-agent}"
human_checkpoint: required
next_agent: orchestrator-agent
blocked_by: []
---

# Task Card: {Readable Task Name}

## Player-Facing Goal

What should the player experience when this is done?

## Source

- Origin: GitHub issue / Discord thread / Notion page / manual prompt
- Link: {url-or-reference}
- Requested by: {name}

## GDD Reference

- `@tag:{tag-name}` - {specific relevant line or summary}

## Type

- [ ] Feature
- [ ] Bug fix
- [ ] Refactor
- [ ] Code review
- [ ] Report / PM update
- [ ] Tooling / CI

## Scope

### Systems Affected

- {system}

### Files To Inspect First

- `Assets/...`
- `Docs/...`

### Out of Scope

- {explicit non-goal}

## Acceptance Criteria

- [ ] {criterion}
- [ ] Works on primary platform input model
- [ ] Does not break existing: {system}
- [ ] Follows `RULES_AND_POLICY.md`

## Human Checkpoints

- [ ] Design/game-feel approval
- [ ] Architecture approval
- [ ] PR review before merge
- [ ] Team status publish approval

## Router Decision

Recommended workflow: `/implement-feature` | `/fix-bug` | `/refactor` | `/code-review` | `/report`

Next artifact:

- `Docs/Specs/{slug}-design-spec.md`
