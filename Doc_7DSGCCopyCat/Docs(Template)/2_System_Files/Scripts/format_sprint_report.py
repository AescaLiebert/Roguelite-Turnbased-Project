"""
AI sprint report formatter.
 
Formats a raw sprint data file into Docs/1_Inputs_Templates/PM_Report_Template.md using an AI
provider. The generated markdown is normalized to ASCII so CI logs and repo
reports remain portable across shells and platforms.
 
Usage:
    python Docs/2_System_Files/Scripts/format_sprint_report.py \
        --input Docs/3_Outputs/PM_Reports/raw_log.md \
        --template Docs/1_Inputs_Templates/PM_Report_Template.md \
        --output "Docs/3_Outputs/PM_Reports/YYYY-MM-DD-sprint-report.md"
 
Environment:
    OPENAI_API_KEY or AI_API_KEY
    OPENAI_MODEL, optional, defaults to gpt-4.1-mini
"""

from __future__ import annotations

import argparse
import datetime as dt
import json
import os
import re
import sys
import unicodedata
import urllib.error
import urllib.request
from pathlib import Path


DEFAULT_MODEL = "gpt-4.1-mini"
OPENAI_RESPONSES_URL = "https://api.openai.com/v1/responses"


def read_file(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def write_file(path: Path, content: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content, encoding="utf-8", newline="\n")


def ascii_safe(text: str) -> str:
    replacements = {
        "\u2013": "-",
        "\u2014": "-",
        "\u2018": "'",
        "\u2019": "'",
        "\u201c": '"',
        "\u201d": '"',
        "\u2026": "...",
        "\u2192": "->",
        "\u2191": "up",
        "\u2193": "down",
        "\u2194": "<->",
        "\ufeff": "",
    }
    for old, new in replacements.items():
        text = text.replace(old, new)
    normalized = unicodedata.normalize("NFKD", text)
    return normalized.encode("ascii", "ignore").decode("ascii")


def strip_code_fences(text: str) -> str:
    cleaned = text.strip()
    if cleaned.startswith("```"):
        cleaned = re.sub(r"^```[a-zA-Z0-9_-]*\s*", "", cleaned)
        cleaned = re.sub(r"\s*```$", "", cleaned)
    return cleaned.strip()


def extract_response_text(payload: dict) -> str:
    text = payload.get("output_text")
    if isinstance(text, str) and text.strip():
        return text

    parts: list[str] = []
    for item in payload.get("output", []):
        for content in item.get("content", []):
            if content.get("type") in {"output_text", "text"}:
                value = content.get("text")
                if isinstance(value, str):
                    parts.append(value)
    return "\n".join(parts).strip()


def report_period(today: dt.date) -> tuple[str, str]:
    end_date = today.isoformat()
    start_date = (today - dt.timedelta(days=6)).isoformat()
    return start_date, end_date


def build_prompt(raw_log: str, template: str, today: dt.date) -> str:
    start_date, end_date = report_period(today)
    return f"""Create a stakeholder-friendly sprint report from the raw sprint data.

Rules:
- Follow the template structure exactly.
- Use only facts present in the raw sprint data.
- If a section has no evidence, write "None documented."
- Keep the output markdown ASCII-only.
- Do not wrap the report in code fences.
- Use report date {end_date}.
- Use period {start_date} -> {end_date}.
- Keep GDD references as "Not documented" unless the raw data contains a tag.

Template:
{template}

Raw sprint data:
{raw_log}
"""


def format_with_openai(raw_log: str, template: str, model: str, api_key: str) -> str:
    prompt = build_prompt(raw_log, template, dt.date.today())
    body = {
        "model": model,
        "input": [
            {
                "role": "system",
                "content": (
                    "You are a PM report agent. Produce concise markdown sprint "
                    "reports from git logs and DevLog excerpts."
                ),
            },
            {"role": "user", "content": prompt},
        ],
    }
    request = urllib.request.Request(
        OPENAI_RESPONSES_URL,
        data=json.dumps(body).encode("utf-8"),
        headers={
            "Authorization": f"Bearer {api_key}",
            "Content-Type": "application/json",
        },
        method="POST",
    )

    try:
        with urllib.request.urlopen(request, timeout=120) as response:
            payload = json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"OpenAI request failed with HTTP {exc.code}: {detail}") from exc
    except urllib.error.URLError as exc:
        raise RuntimeError(f"OpenAI request failed: {exc.reason}") from exc

    output = extract_response_text(payload)
    if not output:
        raise RuntimeError("OpenAI response did not contain report text.")
    return output


def deterministic_report(raw_log: str, today: dt.date) -> str:
    start_date, end_date = report_period(today)
    return f"""# Sprint Report: {end_date} - AI Formatting Unavailable

## Period
{start_date} -> {end_date}

## Summary
AI formatting was not run. The raw sprint data is preserved below for manual review.

---

## Completed Features
None documented.

## Bug Fixes
None documented.

## Refactors
None documented.

---

## Metrics
None documented.

## Risk Assessment
AI formatting did not run, so risk assessment was not generated.

## Blockers
- [ ] AI formatter not configured - owner: project maintainer

## Next Sprint Plan
- [ ] Review raw sprint data and define priorities.

---

## Raw Sprint Data
{raw_log.strip()}
"""


def format_report(raw_log: str, template: str, provider: str, model: str, allow_fallback: bool) -> str:
    api_key = os.environ.get("OPENAI_API_KEY") or os.environ.get("AI_API_KEY")
    if provider != "openai":
        raise ValueError(f"Unsupported provider: {provider}")

    if api_key:
        return format_with_openai(raw_log, template, model, api_key)

    if allow_fallback:
        print("WARNING: No OPENAI_API_KEY or AI_API_KEY found; writing fallback report.", file=sys.stderr)
        return deterministic_report(raw_log, dt.date.today())

    raise RuntimeError("Missing OPENAI_API_KEY or AI_API_KEY for AI sprint report formatting.")


def main() -> int:
    parser = argparse.ArgumentParser(description="Format a sprint report using AI")
    parser.add_argument("--input", required=True, type=Path, help="Path to raw sprint data")
    parser.add_argument("--template", required=True, type=Path, help="Path to report template")
    parser.add_argument("--output", required=True, type=Path, help="Output path for formatted report")
    parser.add_argument("--provider", default=os.environ.get("AI_PROVIDER", "openai"), help="AI provider")
    parser.add_argument("--model", default=os.environ.get("OPENAI_MODEL", DEFAULT_MODEL), help="AI model")
    parser.add_argument(
        "--allow-fallback",
        action="store_true",
        help="Write a deterministic placeholder report when no API key is configured",
    )
    args = parser.parse_args()

    raw_log = read_file(args.input)
    template = read_file(args.template)
    formatted = format_report(raw_log, template, args.provider, args.model, args.allow_fallback)
    formatted = ascii_safe(strip_code_fences(formatted)).rstrip() + "\n"
    write_file(args.output, formatted)
    print(f"Report saved to {args.output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
