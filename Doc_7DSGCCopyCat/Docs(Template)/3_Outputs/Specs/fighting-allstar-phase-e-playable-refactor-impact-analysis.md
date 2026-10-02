# Phase A–E playable refactor impact analysis

## Refactor goal

Make the Core-backed Phase A–E local game playable from one editor/build entry scene, then retire the old gameplay authority while preserving reusable presentation assets.

## Current truth observed

- The pure C# Core already contains seeded cards/RNG, damage resolution, `BattleEngine`, local session/AI, route generation, run HP, rest, and local economy rules.
- The Unity Core presentation scripts live in `Assets/Project/FightingAllstar/Runtime`, not the designated `Assets/FightingAllstar/` path.
- The economy scene reads the 29-entry WIP catalog and intentionally labels all fighters draft-only. Its banner validator rejects runtime-ready content.
- The dungeon scene has working entry/map/combat presenters, but its serialized roster is empty and its content source points at the WIP catalog, causing the reported 29-draft rejection.
- The Core combat HUD creates its own hardcoded local battle during `Awake` when a content source is assigned; route startup and standalone startup can race or diverge.
- The legacy Gacha/LoadOut/Battle scripts use a second inventory and battle authority. They do not submit commands to `BattleEngine`.
- Build settings begin at the legacy LoadOut scene and omit the Core dungeon/economy scenes.

## Responsibility migration

| Concern | Current owner(s) | Target owner | Migration action |
|---|---|---|---|
| Published fighter/card data | WIP JSON plus legacy `CharacterObject` assets | Validated `ContentCatalog` in `Assets/FightingAllstar/Content/Generated` | Generate first-eight runtime catalog with explicit effect provenance; preserve all 29 source rows separately |
| Wallet, roster, guarantee, formation | `PlayerInventoryService` and `LocalEconomyState` split | One account-scoped local profile/economy adapter over Core-owned records | Unify storage, seed starters, bind subject ID seam |
| Summon and progression rules | Legacy Gacha scripts and Core `LocalEconomyService` | Core economy service | Keep Core receipt flow; remove legacy roll path from active scenes |
| Dungeon run and run HP | Core `DungeonRunEngine`, scene composition has blank roster | Core run state and route presenter | Build roster from saved formation, persist and resume same profile |
| Battle state, cards, turns, damage | Legacy `GameManager`/`Unit` plus unused Core `BattleEngine` | Core `BattleEngine` / `IBattleSession` | Route only Core snapshot/commands through Combat UI; disable old manager authority in shipped scene |
| Battle visuals/UI | Legacy uGUI scene and Core UI Toolkit HUD | Thin presentation adapter using authored assets where available | Use one authority per active scene; avoid a second rules simulation |
| App navigation | Legacy direct scene loads; isolated prototype scenes | MainMenu → Combat composition | Set build/editor entry scenes and add return navigation |

## Blast radius and safety

- Unity scenes/prefabs/assets use GUID references. Move files together with `.meta` files and keep GUIDs; do not rewrite serialized IDs.
- Preserve original artwork, animations, prefabs, and legacy scenes until the Core-backed loop is playable and scene references are reviewed.
- The existing git working tree already contains broad uncommitted moves/deletions. Avoid checkout/reset/clean operations and do not overwrite unrelated user work.
- `FightingAllstar.Core.csproj` writes to the intended `Assets/FightingAllstar/Plugins` path. Rebuild it after any Core changes and inspect the Unity console before using changed types.
- Offline receipts/profile data are not server-authoritative entitlements. Keep the UI clearly marked as local practice until an authenticated service exists.

## Sequence

1. Establish canonical `Assets/FightingAllstar/` paths and publish the validated eight-character catalog.
2. Unify starter ownership/formation with the Core economy store and make the banner plus formation screen enter the existing route scene.
3. Remove hardcoded roster and independent HUD battle bootstrap; make route encounter the single Core session source.
4. Set canonical MainMenu/Combat scenes in build settings; add return path and verify one full Play Mode loop.
5. Only after that review the legacy scripts for retirement. Do not delete art or source data.

## Gate

The owner approved the architecture and legacy gameplay refactor in this chat. Destructive removal remains sequenced after replacement and serialized-reference verification.
