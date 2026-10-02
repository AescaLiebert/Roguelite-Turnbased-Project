# {Project Name} — Game Design Document

## Purpose

This document is the shared north star for AI coders, senior engineers, technical designers, and future contributors working on **{Project Name}**.

It has two equally important jobs:

1. Preserve the intended identity of the game.
2. Give the current prototype a clean technical direction without pretending that the clean architecture already exists.

Read this as a **design-led execution charter**:

- The **game fantasy, emotional tone, and player loop are authoritative**.
- The **current codebase is prototype truth**, not final architecture.
- Any refactor must protect the intended player experience while improving maintainability.

---

<!-- @tag:identity -->
## Project Identity

| Field | Direction |
| --- | --- |
| Working Title | **{Project Name}** |
| Genre | **{genre description}** |
| Core Fantasy | {one-sentence player fantasy} |
| Player Promise | {what the player will do and feel} |
| Platform Priority | **{primary platform}**, with **{secondary platform}** support |
| Camera / Play Plane | **{camera style}** |
| Tone | **{emotional tone keywords}** |
| Story Intent | {story hook in one sentence} |

### Experience Pillars

1. **{Pillar Name}**
   {Description of what this pillar means for the player experience.}

2. **{Pillar Name}**
   {Description.}

3. **{Pillar Name}**
   {Description.}

4. **{Pillar Name}**
   {Description.}

5. **{Pillar Name}**
   {Description.}

---

<!-- @tag:visual-audio -->
## Visual, Mood, and Audio Direction

### Mood Board Keywords

- {keyword}
- {keyword}
- {keyword}

### Art Direction
{Describe the art style, character design, environment style, contrast rules.}

### Audio Direction
{Describe the sound design philosophy — is sound informational? atmospheric? both? What role does silence play?}

---

<!-- @tag:platform-input -->
## Platform and Input Philosophy

### Primary Rule
**{Primary platform} is the authoritative input model.** {Secondary platform} support is a mapped port layer, not a separate game design.

### {Primary Platform} Controls
- {Input 1}: {action}
- {Input 2}: {action}
- {Input 3}: {action}

### {Secondary Platform} Port Rules
- Mirror the same verbs and timing windows.
- Do not redesign encounters around {secondary platform} precision.

---

<!-- @tag:core-loop -->
## Core Game Loop

The fundamental loop is:

1. {Step 1}
2. {Step 2}
3. {Step 3}
4. {Step 4}
5. {Step 5}

### Progression Cadence
{How does difficulty/content ramp? What are the pacing beats?}

### Failure / Recovery Pattern
{How does the player fail? How do they recover? What emotional curve should failure follow?}

---

<!-- @tag:items -->
## Canonical Gameplay Content — Items

| Item | Role | Intended Effect |
| --- | --- | --- |
| {Item Name} | {role} | {mechanical effect} |
| {Item Name} | {role} | {mechanical effect} |

---

<!-- @tag:enemies -->
## Canonical Gameplay Content — Enemies and Threats

### {Enemy Name}

**Role:** {one-sentence role description}

**Design intent**
- {behavior 1}
- {behavior 2}
- {behavior 3}

**Counterplay**
- {how the player deals with this threat}

---

<!-- @tag:mechanics -->
## System Mechanics

### {Mechanic Name}
- {How it works}
- {Player interaction model}
- {Risk/reward dynamics}

---

<!-- @tag:inspirations -->
## Inspirations and Design Boundaries

### Primary Inspirations
- **{Game/Media 1}**
- **{Game/Media 2}**

### What To Borrow
- {design element}

### What Not To Copy Blindly
- {anti-pattern for this project}

---

<!-- @tag:tech-stack -->
## Actual Project Stack and Repo Reality

> This section reflects the repository **as it exists today**.

### Technical Stack

| Area | Current Repo Truth |
| --- | --- |
| Engine | **{engine and version}** |
| Render Pipeline | **{pipeline}** |
| Gameplay Dimension | **{2D / 3D}** |
| Input | **{input system}** |
| Camera | **{camera system}** |
| UI | **{UI framework}** |
| Networking | **{networking solution or "None"}** |
| Build Scenes | {list of scenes in build settings} |

---

<!-- @tag:prototype-truth -->
## Current Prototype Truth

> This is the most important reality check for future contributors.

**The current implementation already contains usable gameplay ideas, but it is still prototype-grade.**

### Existing Runtime Shape
{Describe the actual current architecture — singletons, scene wiring, state management.}

### What Exists Today

#### {System Name}
Current script: `{path}`

What it currently does:
- {behavior 1}
- {behavior 2}

### Current Prototype Debt
- {technical debt item 1}
- {technical debt item 2}
- {technical debt item 3}

---

<!-- @tag:scene-roles -->
## Canonical Future Scene Roles

| Scene Role | Purpose |
| --- | --- |
| `{SceneName}` | {purpose} |
| `{SceneName}` | {purpose} |

---

<!-- @tag:system-ownership -->
## Canonical Future System Ownership

| System | Ownership |
| --- | --- |
| `{SystemName}` | {responsibility} |
| `{SystemName}` | {responsibility} |

### Hard Rule
No future refactor should dump new features back into a single global MonoBehaviour just because it is faster in the short term.

---

<!-- @tag:architecture -->
## Target Architecture Direction

### Architecture Principles

1. **Game design authority first**
   The mood, pace, pressure, and {primary platform}-first loop are more important than neat code alone.

2. **Data-driven content**
   {Content types} should move toward authorable data assets rather than hardcoded scene logic.

3. **Thin scene wiring**
   Scenes should assemble references and presentation. They should not permanently own progression logic.

4. **State separated from presentation**
   UI, animation, sound, and VFX should react to gameplay state instead of defining it.

5. **Explicit module boundaries**
   {List of systems} should be individually testable or replaceable.

### Recommended Folder Direction
_(See `RULES_AND_POLICY.md` §3 for the standard folder structure.)_

### Data-Driven Content Targets
- `{Name}Definition` — {what it owns}
- `{Name}Definition` — {what it owns}

---

<!-- @tag:guardrails -->
## Contributor Guardrails

### Preserve These Non-Negotiables
- {design constraint 1}
- {design constraint 2}

### You May Refactor Aggressively
- {area open for refactor}

### Do Not Do These
- {anti-pattern 1}
- {anti-pattern 2}

### Naming Policy
_(See `RULES_AND_POLICY.md` §2 for the standard naming conventions.)_

### AI Safety Rule
Every future implementation must clearly separate **current implementation** from **target architecture**. Do not hallucinate completed architecture.

---

<!-- @tag:roadmap -->
## Phased Refactor Roadmap

### Phase 1 — Stabilize the Prototype
**Goal:** Make the current game loop less fragile without changing the design identity.
- {priority 1}
- {priority 2}

### Phase 2 — Split Core Runtime Systems
**Goal:** Break the prototype monolith into clear system owners.
- {priority 1}
- {priority 2}

### Phase 3 — Data-Drive Content
**Goal:** Make content authorable without rewriting runtime code.
- {priority 1}
- {priority 2}

### Phase 4 — Vertical Slice
**Goal:** Turn the core loop into a production-worthy slice.
- {priority 1}
- {priority 2}

### Phase 5 — Expand Content and Port Readiness
**Goal:** Scale content while preserving {primary platform}-first usability.
- {priority 1}
- {priority 2}

---

## Final Instruction To Contributors

When working on **{Project Name}**, do not optimize only for clean code and do not optimize only for vibes.

The correct target is:

- **emotionally faithful {genre} design**
- **{primary platform}-first readable gameplay**
- **data-driven scalable architecture**
- **clear ownership instead of prototype sprawl**

If a choice improves architecture but weakens {core emotion}, it is the wrong choice.
If a choice preserves mood but makes the code impossible to scale, it is also the wrong choice.

Build toward both.
