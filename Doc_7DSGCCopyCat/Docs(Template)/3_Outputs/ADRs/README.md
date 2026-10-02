# Architecture Decision Records Index

> Last updated: {YYYY-MM-DD}

## Dependency Order

```mermaid
graph TD
    ADR001["ADR-001: {First Decision}"]
    ADR002["ADR-002: {Second Decision}"]
    ADR003["ADR-003: {Third Decision}"]
    ADR001 --> ADR002
    ADR002 --> ADR003
```

_Update this diagram as ADRs are added. Show which decisions depend on others._

## ADR Registry

| # | Title | Status | Phase | Current Note |
|---|-------|--------|-------|-------------|
| [001](001-{kebab-title}.md) | {Title} | **Proposed** / Accepted / Deprecated | {Phase} | {status note} |

## Phase Mapping

| GDD Phase | ADRs | Current Status |
|-----------|------|----------------|
| **Phase 1** — Stabilize Prototype | ADR-{NNN} | {status} |
| **Phase 2** — Split Core Runtime | ADR-{NNN} | {status} |
| **Phase 3** — Data-Drive Content | ADR-{NNN} | {status} |

## How to Use

1. **Before making an architecture decision** → Check this index for existing decisions
2. **To create a new ADR** → Copy `_TEMPLATE.md` → Name it `{NNN}-{kebab-case-title}.md`
3. **After creating** → Add a row to the registry table above
4. **When superseding** → Update the old ADR's status to `Superseded by ADR-{NNN}`
