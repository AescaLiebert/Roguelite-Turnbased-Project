# RogueliteTurnbasedProject
A Rogue-lite / Turnbased / Card Gameplay / Character-Driven / Gacha / Inspired by 7DSGC and Other IP that are absolutely Free! and In-Game Service

## Code layout

- `Assets/FightingAllstar/` is the canonical Unity project: scenes, runtime presentation/adapters, content, art, prefabs, and editor tools.
- `Shared/FightingAllstar.Core/` contains the Unity-independent combat, run, economy, and content rules. `Shared/FightingAllstar.Contracts/` contains the DTOs/protocol shared with the server. These are source projects, not a second legacy implementation.
- `Assets/FightingAllstar/Plugins/` contains the compiled Unity assemblies built from `Shared/`. Edit the source projects and rebuild; do not edit or keep a second copy of Core source under `Assets`.
- `Server/` references those same source projects, so local Unity play and the future server use one rules implementation.

The prototype-era scripts that advanced turns through `GameManager`, `BattleManager`, and `TeamDataManager` were removed from the active project. `Battle` now has one authority: `CoreBattleSceneController` and its `IBattleSession`.
