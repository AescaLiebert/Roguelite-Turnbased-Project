---
slug: fighting-allstar-prototype
status: needs-human
source: manual
gdd_tags: [identity, core-loop, mechanics, architecture, roadmap]
owner: orchestrator-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Task Card: Fighting Allstar design and first prototype architecture

## Player-Facing Goal
Design a 3D card battle RPG inspired by Grand Cross, centered on 3 active fighters plus 1 reserve per side, team synergy, several interacting counterplay triangles, and repeatable dungeon runs that grow a persistent roster.

## Source
- Requested by the project owner in this chat on 2026-10-01.
- User supplied identity, progression loop, proposed data structure, and battle lifecycle.
- This task authorizes concrete design and architecture drafts together. It does not request runtime implementation or deployment.

## Type
- [x] Design and architecture documentation / project bootstrap.

## Scope
- Filled GDD using every applicable section/tag in `../../1_Inputs_Templates/GDD_TEMPLATE.md`.
- First prototype architecture, finite scope, implementation order, risks, and acceptance criteria.
- Reconcile mutable battle state with immutable content and persistent player ownership.
- Define server authority, local AI testing, stage generation, card planning/reset, RNG, triggered effects, death, reserve entry, ultimate progression, and rewards.
- Inspect actual repository before describing existing features.
- Produce supporting context, proposed ADR, test plan, and session DevLog.

## Files To Inspect First
- `Assets/Script/Gameplay/BattleManager.cs`, `GameManager.cs`, `Unit.cs`, `TeamDataManager.cs`, `Interfaces/IBattleDataProvider.cs`.
- `Assets/Script/Card/CardDeckManager.cs`, `RuntimeCard.cs`, `SkillCardSO.cs`, `UltimateCardSO.cs`.
- `Assets/Script/Character/CharacterObject.cs`, `Assets/Script/Inventory/PlayerCharacterRoster.cs`, `Assets/Script/Gacha/GachaManager.cs`.
- `Packages/manifest.json`, `ProjectSettings/ProjectVersion.txt`, `ProjectSettings/EditorBuildSettings.asset`.

## Out of Scope
Code changes, scene changes, asset generation, package installation, deployment, paid economy launch, multiplayer launch, and build settings edits.

## Acceptance Criteria
- [x] GDD preserves all explicit user requirements and labels proposed rules and unknowns.
- [x] Multiple meta triangles explain mechanisms, counterplay, mixed teams, and balance tests.
- [x] Prototype plan defines a complete dungeon-to-reward-to-roster loop and 3+1 battles.
- [x] Architecture maps the current repo to target ownership and defines authoritative online validation.
- [x] Platform/service capabilities checked against official documentation where necessary.
- [x] No template placeholders or unsupported claims that planned features already work.
- [x] Local links, GDD tags, phase scope, and numerical proposals checked for consistency.

## Router Decision
Workflow: `/bootstrap-project`.
Current artifact: this task card.
Next agents: game-design-agent for GDD; architect-agent for repository audit and architecture draft; QA review after both.
Human checkpoint: review completed design and architecture before implementation. User explicitly requested both drafts in this task, so the two drafts may be developed together.
Actual documentation root: `Doc_7DSGCCopyCat/Docs(Template)/`; older paths in AGENTS.md resolve to the numbered directories documented by the hub.

## Required Outputs
- `../../1_Inputs_Templates/GDD_Fighting_Allstar.md`
- `fighting-allstar-prototype-arch-plan.md`
- `../TestPlans/fighting-allstar-prototype-test-plan.md`
- `../ADRs/001-authoritative-combat-core.md`

## Confirmed Follow-up Choices
- Ownership starts at C0/6; six upgrades reach C6/6.
- Dungeons are linear stage sequences with randomized opposing character teams, using the same character catalog and rules rather than a separate NPC enemy system.
- Firebase third-party authentication provides user identity on mobile and PC through suitable platform adapters.

Exact card/PG rules, balance values, provider selection and added C# hosting remain proposed design/architecture choices for review.

## Completion Evidence
GDD, architecture plan, proposed ADR, test plan, context brief, derived stack and DevLog are saved. All original GDD template tags are present. Local Markdown links, required artifact metadata, fenced examples and JSON syntax passed document checks; current-prototype file references exist. The final review corrected progression arithmetic, obsolete branching text, trigger/rounding/privacy differences, first-clear settlement, and milestone ordering. Runtime tests and PC/APK builds are future implementation gates, not results of this task.
