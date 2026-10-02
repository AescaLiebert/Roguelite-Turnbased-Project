---
description: Bootstrap project context — Human fills brief, AI generates GDD-ready docs, all workflows get concrete context
---

# Bootstrap Project Workflow

> **When to use:** First time setting up this documentation system for a new project.
> This is **Step 0** — run this BEFORE any other workflow.
>
> **Token budget:** This workflow is designed to be run ONCE. After it completes, all other workflows load only the `@tag:` section they need from the filled GDD (~200-500 tokens per call instead of ~8,700).

## Why This Workflow Exists

The template system ships with `{placeholder}` values everywhere. Until those are filled:
- Agent prompts reference `project-stack.md` → but it's empty → agents hallucinate context
- Workflows load `GDD_Fighting_Allstar.md` → but it's a template → no real file paths, no real systems
- Token budget is wasted on loading placeholder text

**This workflow solves all of that in one pass.**

---

## The Flow

```
Human fills Context Brief (15-30 min)
    └──► AI reads Brief + GDD_TEMPLATE.md
           └──► AI generates: 1_Inputs_Templates/GDD_Fighting_Allstar.md (filled, with @tag: sections)
                  └──► AI derives: 1_Inputs_Templates/project-stack.md (compact bootstrap)
                         └──► All agent workflows now have real context
                                └──► Every @tag: section maps to real file paths
```

---

## Steps

### 1. Fill the Project Context Brief (👤 Human — 15-30 min)

Open `Docs/1_Inputs_Templates/project-context-brief.md` and fill in every section:

| Section | What To Write | Why It Matters |
|---------|--------------|----------------|
| §1 Identity | Name, genre, platform, tone | Sets @tag:identity and @tag:platform-input |
| §2 Tech Stack | Engine, pipeline, input, camera | Sets @tag:tech-stack |
| §3 Scenes | Build Settings scene list | Sets @tag:scene-roles |
| §4 File Map | Every major script path + state | **Most important** — this is how agents know WHICH files to read and which to skip |
| §5 Architecture | Current wiring, known debt | Sets @tag:prototype-truth |
| §6 Design Boundaries | Pillars, hard rules, arch rules | Sets @tag:guardrails |
| §7 Commit Scopes | System names for commits | Configures all agent workflows |

> [!IMPORTANT]
> **Keep it concise.** The brief should be ~500-800 tokens total. AI will expand it into the full GDD. Don't write essays — write facts.

### 2. Generate GDD (🤖 AI — Automated)

Read the filled `project-context-brief.md` and `1_Inputs_Templates/GDD_TEMPLATE.md`.

For each `@tag:` section in the template, fill in the `{placeholder}` values using the brief:

| GDD `@tag:` | Filled From Brief § |
|-------------|-------------------|
| `@tag:identity` | §1 Identity |
| `@tag:visual-audio` | §1 Tone keywords + §6 Pillars |
| `@tag:platform-input` | §1 Primary/Secondary Platform |
| `@tag:core-loop` | §6 Pillars (AI drafts, human reviews) |
| `@tag:tech-stack` | §2 Tech Stack |
| `@tag:prototype-truth` | §4 File Map + §5 Architecture |
| `@tag:scene-roles` | §3 Scenes |
| `@tag:system-ownership` | §4 File Map (system → script path) |
| `@tag:architecture` | §5 Architecture + §6 Architecture Rules |
| `@tag:guardrails` | §6 Hard Rules |
| `@tag:roadmap` | AI drafts based on §5 Known Debt + §4 States |

**Save to:** `Docs/1_Inputs_Templates/GDD_Fighting_Allstar.md`

> [!WARNING]
> **Sections the AI cannot fill from the brief alone:**
> - `@tag:items` — Gameplay content items (leave as TODO or human fills later)
> - `@tag:enemies` — Enemy/threat designs (leave as TODO or human fills later)
> - `@tag:mechanics` — Detailed system mechanics (leave as TODO or human fills later)
> - `@tag:inspirations` — Design inspirations (leave as TODO or human fills later)
>
> These are **game design decisions** that require human creative input. Mark them with `<!-- TODO: Human fill -->`.

### 3. Generate project-stack.md (🤖 AI — Automated)

Derive `Docs/1_Inputs_Templates/project-stack.md` from the filled GDD. This file must be:
- **Under 400 tokens** — it's loaded by EVERY agent call as the universal bootstrap
- **Concrete, not templated** — real project name, real file paths, real system states
- **A pointer, not a copy** — reference `@tag:` sections for details, don't duplicate content

The `project-stack.md` structure stays the same but every `{placeholder}` is now a real value.

### 4. Validate Context Completeness (🤖 AI — Automated)

Check that the generated GDD and project-stack are internally consistent:

- [ ] Every `@tag:` section in GDD has at least one concrete value (no remaining `{placeholder}`)
- [ ] Every script path in `@tag:prototype-truth` actually describes a real file the dev confirmed
- [ ] Every scene in `@tag:scene-roles` matches the Build Settings list from the brief
- [ ] `project-stack.md` is under 400 tokens
- [ ] `project-stack.md` references GDD `@tag:` sections, not inline copies
- [ ] Commit scopes in `project-stack.md` match §7 from the brief

Report any gaps: "The following @tag: sections still need human input: [list]"

### 5. Human Review (👤 Human — ⚠️ CHECKPOINT)

Human reviews the generated `GDD_Fighting_Allstar.md` and `project-stack.md`:

- [ ] Identity is accurate
- [ ] File paths are correct (no typos in `Assets/...` paths)
- [ ] Experience pillars reflect the actual design intent
- [ ] Architecture description matches how the project really works today
- [ ] Roadmap phases make sense for the project's current state
- [ ] TODO sections identified for later creative fill-in

**If rejected:** Human edits the Context Brief and re-runs from Step 2.

### 6. Done

The project documentation is now **GDD-ready for development**:

```
✅ 1_Inputs_Templates/GDD_Fighting_Allstar.md                    — Filled with real project context
✅ 1_Inputs_Templates/project-stack.md          — Compact 400-token bootstrap
✅ All @tag: sections                           — Point to real file paths and systems
✅ All agent workflows                          — Can now reference @tag: sections with real data
```

---

## How This Saves Tokens

### Before Bootstrap (Generic Template)
```
Every agent call loads:
  project-stack.md (placeholders)     → 400 tokens of nothing
  GDD_Fighting_Allstar.md (placeholders)              → 2,000 tokens of nothing
  Agent prompt                       → 700 tokens
  RULES_AND_POLICY.md (full)         → 2,500 tokens (most irrelevant)
  ─────────────────────────────────
  TOTAL: ~5,600 tokens, 0% useful context
```

### After Bootstrap (Filled GDD)
```
Every agent call loads:
  project-stack.md (filled)           → 400 tokens of REAL context
  GDD @tag:{relevant-section} only   → 200-500 tokens of REAL context
  Agent prompt                       → 700 tokens
  ─────────────────────────────────
  TOTAL: ~1,300-1,600 tokens, 100% useful context
  SAVINGS: ~72% token reduction per agent call
```

### The Key Insight

**The GDD becomes a context index, not a context blob.**

Agents never load the full GDD. They load `project-stack.md` (the index) and then the specific `@tag:` section they need (the page). This is the same as how a database index works — you don't read the whole table, you read the index then fetch the row.

---

## Anti-Patterns

> [!CAUTION]
> - **Don't skip the brief.** Filling GDD_Fighting_Allstar.md directly is slower and produces inconsistent results.
> - **Don't over-write the brief.** Keep it factual and short. The AI expands it.
> - **Don't manually fill project-stack.md.** It's a derived file — always regenerate from GDD.
> - **Don't run other workflows before this one.** They'll reference empty templates and produce garbage output.
> - **Don't re-run this workflow for small updates.** If one system changes, edit the GDD `@tag:` section directly. Only re-run bootstrap for major project resets.
