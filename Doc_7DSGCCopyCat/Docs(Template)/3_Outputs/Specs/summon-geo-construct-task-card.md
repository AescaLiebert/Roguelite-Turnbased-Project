---
slug: summon-geo-construct
status: approved
source: manual
gdd_tags: [mechanics, architecture, actor-camera, elemental-reactions, platform-input]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Summon and Geo Construct planning package

Date: 2026-10-21. Scope of this delivery: architecture, game design, art production planning, and validation planning. Runtime implementation and model production are future work.

Update 2026-10-21: owner approved the planning package and requested backend implementation for Fischl, Xiangling, and Ningguang, with visuals deferred. The shared backend and targeted character packages are implemented and verified; see `Artifacts/Summons/Backend/Implementation.md` and ADR-003. The original planning scope below is retained as history. Visual production is still pending.

## Player-facing goal

An actor can create a visible battlefield entity with its own model, idle loop, and action animation. A grey, non-dispellable owner status identifies that entity and its remaining duration. The entity disappears when its owner dies. Geo Constructs share this lifecycle and have a distinct category; damage absorption is an optional capability.

## Source and decisions

- Requested by Apricha in this chat; the latest instruction explicitly requests architectural and game design plans now.
- Supplied images are visual references: Oz, Guoba, and Jade Screen. They are reference material, not instructions.
- Confirmed in this chat: damage exceeding Jade Screen's remaining capacity reaches the protected ally.
- Existing roster generator and character data provide migration evidence, not approval of new balance decisions.

## Deliverables

- [Game design](summon-geo-construct-design-spec.md): lifecycle, turn timing, targeting, interception, feedback, and assumptions.
- [Architecture](summon-geo-construct-arch-plan.md): shared-core ownership, data contracts, events, migration, and implementation slices.
- [Blender and art plan](summon-geo-construct-art-plan.md): three entities, scripted mesh/UV/rig/clip production, texture detail, and Unity assembly.
- [Test plan](../TestPlans/summon-geo-construct-test-plan.md): executable acceptance scenarios for implementation and playtests.

## GDD alignment

- `@tag:mechanics`: preserve card queues, source combat formulas, status clocks, and deterministic resolution.
- `@tag:architecture`: "The core should run without Unity scene objects." Definitions, state, events, and views remain separate.
- `@tag:actor-camera`: preserve actor-owned execution phases and captured target focus; autonomous entities need explicit presentation support.
- `@tag:elemental-reactions`: preserve elemental identity, contact timing, bounded child events, and origin permissions.
- `@tag:platform-input`: landscape mobile inspection first; PC exposes the same facts and decision verbs.

## Scope

Feature planning covers a reusable `Summon` battle effect, category `GeoConstruct`, owner-linked structural statuses, duration, deterministic triggered actions, optional shared absorption, three presentation definitions, and a reproducible Blender production workflow.

The three initial definitions are Guoba, Oz, and Jade Screen. Pyronado and other existing periodic effects retain their current behavior until separately migrated. Entity targeting, independent initiative, summon card hands, navigation, physical projectile blocking, construct resonance, and new economy systems are outside this first slice.

## Acceptance criteria for this planning delivery

- [x] Separate confirmed user requirements, observed code, and proposed rules.
- [x] Define spawn, idle, action, expiry, owner death, replacement, battle completion, and cleanup.
- [x] Define overflow, damage accounting, multiple targets, and non-absorbing constructs.
- [x] Map the proposal to actual `Shared/` and `Assets/Project/` paths.
- [x] Specify modeling, texture, rig, animation, and prefab deliverables for all three entities.
- [x] Supply implementation order and a test plan; do not claim unrun tests or completed models.

## Routing and review

Workflow: `/implement-feature`, planning stages only. Both design and architecture drafts were directly requested, so they are delivered together for review. No design or architecture is marked approved. Review the design assumptions before starting implementation; record accepted direction in a task update and an ADR at that point. This package does not change existing ADR approval states or the GDD.

Useful first implementation reads: `BattleEngine.cs`, `StatusSystem.cs`, `DamageResolver.cs`, `CombatPolicySystem.cs`, `CharacterPassiveRuntime.cs`, `BattleModels.cs`, `BuildPreReleaseLaunchRoster.cs`, and the architecture plan's affected-file table.
