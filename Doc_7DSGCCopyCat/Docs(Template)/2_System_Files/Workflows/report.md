---
description: Sprint report workflow using git logs, DevLogs, and the PM report agent
---

# Report Workflow

## Steps

### 1. Load Context
 
- Read `Docs/1_Inputs_Templates/project-stack.md` for project context.
- Read `Docs/1_Inputs_Templates/PM_Report_Template.md` for the required report shape.
- Read `Docs/2_System_Files/Agent_Prompts/pm-report-agent.md` for PM reporting behavior.
- Read recent `Docs/3_Outputs/DevLog/` entries only for the reporting period.
 
### 2. Collect Sprint Data
 
- Use the last 7 days for the weekly Monday report unless the task card states another period.
- Collect conventional commits by type: `feat`, `fix`, `refactor`, `chore`, `style`, `test`, `docs`, and `juice`.
- Include ADR changes from the same period when they exist.
- Do not use Discord or Notion as canonical source data unless a human-approved sync workflow has converted it into repo markdown.
 
### 3. Generate Report
 
Run the formatter:
 
```bash
python Docs/2_System_Files/Scripts/format_sprint_report.py \
  --input Docs/3_Outputs/PM_Reports/raw_log.md \
  --template Docs/1_Inputs_Templates/PM_Report_Template.md \
  --output "Docs/3_Outputs/PM_Reports/YYYY-MM-DD-sprint-report.md"
```
 
For local dry runs without an API key, add `--allow-fallback` to verify the file path and ASCII-safe output.
 
### 4. Validate Output
 
- Confirm the report is saved as `Docs/3_Outputs/PM_Reports/{YYYY-MM-DD}-sprint-report.md`.
- Confirm the report is ASCII-only.
- Confirm every section is filled or explicitly says `None documented.`
- Confirm no project facts were invented beyond the collected git log, DevLogs, ADRs, and GDD references.

### 5. Human Checkpoint

Human approval is required before publishing official status to Discord, Notion, or other team channels.

### 6. Done

The sprint report is ready for review or for the weekly Monday pull request.
