# Reports

PM reports are generated here from DevLog entries and git log.

## How to Generate

### Manual Raw Data

```bash
git log --since="2 weeks ago" --pretty=format:"- %s (%h, %an, %ar)" --no-merges > sprint_report_raw.txt
```

### AI-Formatted Report

```bash
python Docs(Template)/scripts/format_sprint_report.py \
  --input sprint_report_raw.txt \
  --template Docs(Template)/Reports/_TEMPLATE.md \
  --output "Docs(Template)/Reports/$(date +%Y-%m-%d)-sprint-report.md"
```

Set `OPENAI_API_KEY` or `AI_API_KEY` before running. For a local path check without an API key, add `--allow-fallback`.

### Automated Monday Run

Use `Docs(Template)/.github/workflows/sprint-report.yml`. It runs every Monday, writes an AI-formatted report, and opens a PR.

## Naming Convention

`{YYYY-MM-DD}-sprint-report.md`
