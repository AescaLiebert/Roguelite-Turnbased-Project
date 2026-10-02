# Rules & Policy — AI-Assisted Game Development

> **Purpose:** Standardize how AI agents (IDE copilots, CLI tools, multi-agent pipelines) interact with this project. Copy into your AI tool's system prompt, `.cursorrules`, `CLAUDE.md`, or equivalent.

---

## 1. Context Policy — GDD as Single Source of Truth

### Rule
All `.md` files in this project get context **ONLY** from `1_Inputs_Templates/GDD_Fighting_Allstar.md` and its `@tag:` markers. AI agents must not invent context, assume features, or reference external knowledge about the game.

### Tag System
The GDD uses `@tag:section-name` markers to enable precise cross-referencing:

```markdown
<!-- @tag:core-loop -->
## Core Game Loop
...

<!-- @tag:enemies -->
## Enemies and Threats
...
```

Other documents reference GDD sections with:
```
GDD Section: @tag:core-loop
```

### Context Hierarchy
```
1_Inputs_Templates/GDD_Fighting_Allstar.md      ← Authoritative source of truth
  ├── 0_User_Manual/            ← Policies, cookbooks, rules
  ├── 1_Inputs_Templates/       ← Derived stack, context brief, templates
  ├── 2_System_Files/           ← Agent roles, workflows, scripts, contracts
  └── 3_Outputs/                ← Specs, ADRs, DevLogs, PM reports
```

> [!IMPORTANT]
> If a fact is not in the GDD, it is not a project fact. AI agents must say "this is not documented in the GDD" rather than guessing.

### Bootstrap Exception

`1_Inputs_Templates/project-context-brief.md` is allowed as an input only during `/bootstrap-project`.
After bootstrap, `1_Inputs_Templates/GDD_Fighting_Allstar.md` and `1_Inputs_Templates/project-stack.md` become the source of truth.
Do not use the brief as a parallel truth source for normal feature, bugfix, refactor, review, QA, or report work.

---

## 2. Output Rules — Code & Documentation Style

### Naming Conventions
| Element | Convention | Example |
|---------|-----------|---------|
| C# Class | PascalCase | `PlayerMovement`, `InventoryManager` |
| C# Method | PascalCase | `TakeDamage()`, `GetCurrentFloor()` |
| C# Field (private) | camelCase with `_` prefix | `_currentHealth`, `_isHidden` |
| C# Field (public/serialized) | camelCase | `moveSpeed`, `maxHealth` |
| C# Interface | `I` prefix + PascalCase | `IInteractable`, `IDamageable` |
| ScriptableObject | PascalCase + `Definition` suffix | `ItemDefinition`, `EnemyDefinition` |
| Folder | PascalCase | `Scripts/`, `Gameplay/`, `Core/` |
| Scene | PascalCase | `MainMenu.unity`, `FloorRuntime.unity` |
| Commit | Conventional commits | `feat(player): add sprint mechanic` |

### Code Response Format
When writing code, AI agents must:
1. **State which file** is being created or modified (full path)
2. **State the GDD reference** that motivates the change
3. **Separate current state from target state** — never hallucinate completed architecture
4. **Include only relevant code** — no boilerplate dumps or unrelated files
5. **Add comments only for WHY**, never for WHAT

### Documentation Response Format
- Use markdown with proper heading hierarchy
- Use tables for structured comparisons
- Use mermaid diagrams for architecture/flow visualization
- Use `> [!NOTE]`, `> [!WARNING]`, `> [!CAUTION]` for callouts
- Keep bullet points concise — one line per point

---

## 3. File & Folder Organization

### Project Folder Structure (Target)
```
Assets/_Project/
  Core/                  # Bootstrap, game flow, save/profile, services
    Bootstrap/
    GameFlow/
    SaveProfile/
    Services/
  Gameplay/              # Runtime gameplay systems
    Player/
    Interaction/
    Inventory/
    {GameSpecificSystem}/
  Content/               # ScriptableObject definitions and authored data
    Items/
    Enemies/
    Levels/
    Encounters/
  UI/                    # All UI logic and prefabs
    HUD/
    Menus/
    Shop/
  Audio/
  Art/
  Tools/                 # Editor tools, debug utilities
  Tests/                 # Edit-mode and play-mode tests

Docs/                    # Outside Unity Assets, version-controlled
  0_User_Manual/         # Guides, prompt cookbook, context router, rules
  1_Inputs_Templates/    # Configuration brief, stack, GDD template
  2_System_Files/        # Agent prompts, workflows, contracts, scripts
  3_Outputs/             # Active specs, ADRs, test plans, dev logs, reports
```

### File Naming Rules
| Type | Pattern | Example |
|------|---------|---------|
| MonoBehaviour | `{SystemName}.cs` | `PlayerMovement.cs` |
| ScriptableObject | `{Name}Definition.cs` | `ItemDefinition.cs` |
| Interface | `I{Name}.cs` | `IInteractable.cs` |
| Editor Script | `{Name}Editor.cs` | `ItemDefinitionEditor.cs` |
| ADR | `{NNN}-{kebab-case-title}.md` | `001-docs-structure.md` |
| DevLog | `{YYYY-MM-DD}-{kebab-case}.md` | `2026-05-11-phase1-cleanup.md` |

---

## 4. In-Scene Hierarchy Organization

### Separator Convention
Use **empty GameObjects** as visual separators in the hierarchy. Name them with `=` padding to create clear visual sections:

```
=======System=======
  EventSystem
  AudioListener
  InputManager
=======Manager=======
  GameFlowManager
  SessionManager
  SceneLoader
=======Camera=======
  MainCamera
  CinemachineVCam
=======Lighting=======
  GlobalLight2D
  AmbientLight
=======Environment=======
  Background
  Foreground
  Tilemap_Ground
  Tilemap_Walls
=======Player=======
  Player
=======Enemies=======
  EnemySpawner
  (runtime spawned enemies)
=======UI=======
  Canvas_HUD
  Canvas_Menus
  Canvas_Overlay
=======Debug=======
  DebugConsole
  TestSpawner
```

### Separator Rules
1. **No parenting** — separators are sorting markers, not parent objects
2. **Disable all components** — separator objects should have no components (or just a disabled `MonoBehaviour` tag script)
3. **Static flag** — mark separators as `EditorOnly` or strip them in builds
4. **Order matters** — follow the convention: System → Manager → Camera → Lighting → Environment → Player → Enemies → UI → Debug
5. **Scene-specific sections** — add custom sections as needed (e.g., `=======Interactables=======`, `=======VFX=======`)

### Prefab Hierarchy
Inside prefabs, use logical grouping with actual parenting:
```
Player (root)
  ├── Model/
  │   ├── Sprite
  │   └── Animator
  ├── Collision/
  │   ├── BodyCollider
  │   └── InteractionTrigger
  ├── Audio/
  │   ├── FootstepSource
  │   └── VoiceSource
  └── FX/
      ├── DamageFlash
      └── HealParticle
```

---

## 5. Game Optimization Policy

### Performance Budgets
| Metric | Mobile Target | PC Target |
|--------|--------------|-----------|
| Frame rate | 30 FPS stable | 60 FPS stable |
| Draw calls | < 50 per frame | < 100 per frame |
| Memory | < 512 MB | < 1 GB |
| Scene load time | < 3 seconds | < 2 seconds |
| GC allocations | 0 per frame in gameplay | 0 per frame in gameplay |

### Code Optimization Rules
1. **No `Update()` polling** when events or coroutines suffice
2. **Cache component references** — never use `GetComponent<T>()` in `Update()`
3. **Use object pooling** for frequently spawned/destroyed objects (bullets, particles, enemies)
4. **Avoid string operations** in hot paths — use `StringComparison.Ordinal`, `StringBuilder`, or pre-hashed values
5. **Use `CompareTag()`** instead of `gameObject.tag ==`
6. **Minimize `Find*()` calls** — use direct references, events, or service locators
7. **Profile before optimizing** — never optimize without data from Unity Profiler

### Art & Asset Optimization Rules
1. **Texture atlasing** — combine small sprites into atlases
2. **Appropriate texture sizes** — mobile sprites rarely need > 1024x1024
3. **Audio compression** — use Vorbis for music, ADPCM for short SFX
4. **Prefab variants** — use variants instead of duplicating prefabs
5. **Addressables or AssetBundles** for large content sets that can be loaded on demand

### Scene Optimization Rules
1. **Minimize root-level objects** — use the separator convention above, but keep total root object count reasonable
2. **Use static batching** for non-moving environment art
3. **Culling** — use sorting layers and camera bounds to avoid rendering off-screen objects
4. **Additive scenes** for large game worlds — don't put everything in one scene

### Build Optimization Rules
1. **Strip unused engine modules** in Player Settings
2. **IL2CPP** for production builds (better performance than Mono)
3. **Managed code stripping** — set to Medium or High
4. **Compress build** — LZ4 for faster load, LZ4HC for smaller size

---

## 6. Agent Behavior Rules

### Must Do
- ✅ Always reference the GDD section that motivates a change
- ✅ Explicitly state whether code is **current implementation** or **target architecture**
- ✅ Write ADRs for any significant architecture decisions
- ✅ Write DevLog entries after each work session
- ✅ Follow existing patterns in the codebase before introducing new ones
- ✅ Keep changes small and incremental — one system per PR

### Must Not Do
- ❌ Never auto-merge to main — always require human approval
- ❌ Never skip the spec/design phase — code-first leads to architectural debt
- ❌ Never grow a god-object just because it's faster short-term
- ❌ Never hallucinate completed architecture — say what exists vs. what's planned
- ❌ Never fully automate game feel — always keep human-in-the-loop for subjective quality
- ❌ Never use one mega-prompt — specialized agents with focused system prompts outperform generalists
- ❌ Never feed the entire repo to an agent — use targeted file selection or RAG

### Error Handling
When an AI agent encounters ambiguity:
1. State the ambiguity explicitly
2. Reference what the GDD says (or that it's silent on the topic)
3. Propose 2-3 options with trade-offs
4. Wait for human decision — do not proceed with assumptions

### Orchestration Rules

For multi-agent work:
1. Start from a task card that follows `Docs/2_System_Files/Handoff_Contracts/README.md`
2. Use `Docs/2_System_Files/Agent_Prompts/orchestrator-agent.md` to route ambiguous work before selecting a workflow
3. Stop when an artifact has `status: needs-human`, `status: blocked`, or `human_checkpoint: required`
4. Treat Discord and Notion discussion as intake only until converted into a tracked repo artifact
5. Use `Docs/0_User_Manual/TEAM_SYNC_POLICY.md` before posting status to team channels or Notion

---

## 7. Safety Guardrails

### Code Safety
- All gameplay state changes must be traceable (events, logs, or state machines)
- No silent side effects — if a method changes state outside its scope, document it
- Destructive operations (delete, reset, overwrite) require explicit confirmation
- No `PlayerPrefs` for critical game state — use proper save systems

### AI Safety
- AI-generated code must be reviewed before merging
- AI must not generate content that contradicts the GDD's tone, theme, or design pillars
- AI must not introduce dependencies without documenting them in an ADR
- AI must not modify build settings, CI/CD pipelines, or deployment configs without human approval
- AI must not publish official Discord or Notion status without human approval and a link to the repo artifact or PR

---

## 8. Token Budget Policy — AI Context Loading

### Per-Agent Context Budget

| Agent Role | Max Context | Loading Strategy |
|-----------|-------------|-----------------|
| Orchestrator | ~2,000 tokens | `Docs/1_Inputs_Templates/project-stack.md` + task card/current artifact + workflow index |
| 🎮 Game Design | ~4,000 tokens | `Docs/1_Inputs_Templates/project-stack.md` + GDD `@tag:` section + agent prompt |
| 🏗️ Architect | ~6,000 tokens | `Docs/1_Inputs_Templates/project-stack.md` + GDD `@tag:` section + relevant ADRs + agent prompt |
| 💻 Implementer | ~8,000 tokens | Specs + affected source files + agent prompt |
| 👀 Reviewer | ~4,000 tokens | Git diff + agent prompt (not full files) |
| 🧪 QA | ~4,000 tokens | Design spec + implementation summary + agent prompt |
| 📋 PM Reporter | ~2,000 tokens | Git log + report template + agent prompt |

### Context Loading Rules

1. **Always load `project-stack.md` first** (~400 tokens) — this is the universal bootstrap (should be loaded from `Docs/1_Inputs_Templates/project-stack.md`)
2. **Load the task card or current artifact** for the current slug (from `Docs/3_Outputs/Specs/`)
3. **Load only the `@tag:` section** of GDD relevant to the task (from `Docs/1_Inputs_Templates/GDD_Fighting_Allstar.md`) — never the full GDD
4. **Load the agent prompt for the CURRENT role only** — not all agent prompts (load from `Docs/2_System_Files/Agent_Prompts/`)
5. **Load only the specific ADRs** referenced by the task — not the entire `Docs/3_Outputs/ADRs/` directory
6. **For implementation,** load only the files listed in the architecture spec's "Affected Systems"
7. **For reviews,** load the diff — only load full files if the diff is insufficient to understand context
8. **Prefer references over inline content** — say "follow §4" rather than copying §4 into the prompt

### Anti-Patterns

- ❌ Loading the full GDD (7,000+ tokens) when only one `@tag:` section is relevant
- ❌ Loading all agent prompts into one session (only one agent role is active at a time)
- ❌ Loading `RULES_AND_POLICY.md` in full (10,000+ tokens) — load only the relevant §sections (from `Docs/0_User_Manual/RULES_AND_POLICY.md`)
- ❌ Duplicating conventions inline instead of referencing the canonical source

---

## Quick Reference Card

```
📋 Before coding    → Read GDD section + relevant ADRs
🏗️ Before deciding  → Check existing ADRs, write new one if needed
📝 After session    → Write DevLog entry → commit message
📊 Weekly           → DevLogs feed into PM reports
🎮 Game feel        → ALWAYS human-in-the-loop
🔒 Merge            → ALWAYS human approval
💰 Token budget     → Load ONLY what the current agent needs (§8)
```
