---
slug: fighting-allstar-phase-e-playable-refactor
status: in-progress
source: manual
gdd_tags: [core-loop, mechanics, economy, inventory, architecture, roadmap]
owner: codex
human_checkpoint: satisfied-by-current-chat
next_agent: human
blocked_by: []
---

# Task Card: Make the Phase A–E local game playable

## Player-Facing Goal

Open the Unity project in Play Mode and play the local Phase A–E loop: banner, owned roster/formation, dungeon route, deterministic-core battle, run settlement, and return to the roster/economy.

## Source

- Requested by the project owner in this chat.
- The owner clarified that the previous implementation only connected Phase E assets to the legacy battle flow, and authorized refactoring/removing obsolete legacy gameplay into the designated structure.

## GDD / Architecture References

- `fighting-allstar-unity-implementation-plan.md` phases A–E and legacy migration matrix.
- `fighting-allstar-prototype-arch-plan.md` shared core and presentation boundary.
- ADR-001: the Core owns rules/state; Unity scenes present snapshots and submit commands.

## Type

- [x] Feature and architecture refactor

## Scope

- Move Fighting Allstar app code/content/scenes into `Assets/FightingAllstar/` with stable Unity GUIDs.
- Publish a validated runtime catalog for the first eight authored fighters; keep the remaining source records authoring-only.
- Use one account-scoped local profile for wallet, ownership, progression, and formation; preserve a future authenticated-subject binding seam.
- Compose the existing Phase E economy, route engine, `BattleEngine`, and `CombatHudController` into a single playable local loop.
- Set MainMenu as the editor/build entry scene and retain the existing BattleScene art assets where useful.
- Retire legacy gameplay authority only after the Core-backed replacement has a working scene path.

## Acceptance Criteria

- [ ] Unity Play Mode starts at MainMenu without manual scene setup.
- [ ] New local profile receives the documented starter team and can set a legal 3+1 formation.
- [ ] The banner can summon from the published eight-character catalog; rates, price, selector, duplicates, and C0–C6 progression use one saved profile.
- [ ] The player can start a run, choose reachable route nodes, complete a Core battle with planning/undo/commit, carry run HP, and claim completion rewards once.
- [ ] The new combat authority is `FightingAllstar.Core`; Unity presentation does not calculate or mutate battle rules.
- [ ] The active scenes/scripts/assets are under `Assets/FightingAllstar/`; old authority scripts are retired after references are migrated.
- [ ] Landscape remains locked.

## Validation

- Unity compilation, scene/prefab reference inspection, and one editor Play Mode walk-through are required before calling the loop playable.
- No online authentication/provider, server, or production entitlement is implied by this local build.

## Checkpoint

The owner explicitly authorized the architecture refactor and legacy gameplay removal in the current chat. The prior ADR remains the governing Core/presentation boundary.

## Local flow steering

The owner asked to defer Firebase Authentication and focus on seeing the actual battle and dungeon flow. Keep the editor flow on the existing local guest profile; do not make sign-in a prerequisite for local runs. The local battle scene now advances an opponent-first opening turn before enabling player planning and plays the resulting Core events after the formation intro. This is a gameplay-path change only; it does not certify the full loop as visually verified.
