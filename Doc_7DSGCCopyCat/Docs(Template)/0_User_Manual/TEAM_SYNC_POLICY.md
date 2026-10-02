# Team Sync Policy

This policy defines how repository docs, Discord, and Notion work together.

## Source of Truth

The repository markdown docs are the canonical project memory:

- Game truth: `Docs/1_Inputs_Templates/GDD_Fighting_Allstar.md`
- Architecture truth: `Docs/3_Outputs/ADRs/`
- Work artifact truth: `Docs/3_Outputs/Specs/` and `Docs/3_Outputs/TestPlans/`
- Session/report truth: `Docs/3_Outputs/DevLog/` and `Docs/3_Outputs/PM_Reports/`

Notion is the team-facing planning and reading layer unless the project explicitly adds a Notion-to-markdown sync workflow.

Discord is the discussion and notification layer. Discord messages are not canonical until summarized into a task card, GDD update, ADR, DevLog, or issue.

## Intake Rules

| Source | AI Action | Required Output |
| --- | --- | --- |
| Discord idea or bug | Summarize and ask for/record approval | `Docs/3_Outputs/Specs/{slug}-task-card.md` or GitHub issue |
| Notion task | Convert key fields into repo artifact | `Docs/3_Outputs/Specs/{slug}-task-card.md` |
| GitHub issue | Treat as structured intake | `Docs/3_Outputs/Specs/{slug}-task-card.md` |
| Direct prompt | Convert into task card before multi-agent work | `Docs/3_Outputs/Specs/{slug}-task-card.md` |

## Notification Points

Discord or Notion updates may be posted after these events:

- Task card created and approved for work.
- Design spec ready for review.
- Architecture plan ready for review.
- PR opened.
- QA test plan ready.
- Sprint report generated.
- Blocker discovered.

Do not auto-post official project status until the team has approved the integration and channel/page targets.

## Human Approval Required

AI must pause before:

- Publishing official team status.
- Changing the GDD source of truth.
- Accepting architecture decisions.
- Changing CI/CD, build settings, dependencies, or secrets.
- Merging a PR.
- Declaring subjective game feel as complete.

## Recommended Notion Setup

Use Notion for visibility, not hidden truth:

- Database: Features / Bugs / Refactors
- Fields: `slug`, `status`, `owner`, `gdd_tags`, `repo_artifact`, `PR`, `blockers`
- Repo artifact links should point back to markdown docs or GitHub PRs.

## Recommended Discord Channels

- `#dev-log`: automated summaries and sprint report links
- `#review-needed`: design, architecture, PR, and QA checkpoints
- `#bugs`: bug intake before conversion to task cards
- `#builds`: CI/build notifications

## Conflict Rule

If Discord, Notion, and repo markdown disagree, repo markdown wins. Update Notion/Discord summaries after the repo artifact is corrected.
