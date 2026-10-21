# Playable Teyvat dungeon

The Genshin Impact dungeon (`genshin-impact`, profile series `series.genshin-impact`) now loads `BattleTeyvat`. Other dungeon profiles retain `Battle`. Scene selection follows the dungeon profile, including reconstructed saved-run profiles, rather than the fighter selected by the player.

## Play

1. Open `Assets/Project/Scenes/MainMenu.unity` and enter Play Mode.
2. Choose **Enter Dungeon**, then **Genshin Impact**. If continuing an existing run, its existing dungeon profile determines the arena.
3. Choose a cutoff that includes your owned Genshin fighters. The currently authored definitions 1411–1415 require **WIP-Phase / META** or a later cutoff.
4. Start the run, choose a battle node, select your owned formation, and press Play in loadout.
5. Use the normal card combat controls. The existing result/return flow remains in charge of run progress and rewards.

Opening `BattleTeyvat` directly does not supply a dungeon encounter. Use the dungeon flow above for normal play.

## Integration

- The dedicated scene retains the production battle controller, UI, fighter anchors, targeting and action cameras. It references the WP08 revision 2 stage, foliage, water, lighting rig, 45 baked probes and custom reflection.
- The surrounding-scenery revision wraps that accepted stage with the Great Tree valley, Tidewatch coast and Frost Crown reverse view. The playable builder now uses `Surroundings/Prefabs/TG_SurroundedBattleStage.prefab`; see `Surroundings.md` for its camera comparisons and rebuild workflow.
- `DungeonBattleScene.ForProfile` selects the arena. Loadout uses it after preparing and saving the encounter.
- `TeyvatDungeonArena` uses the shared `MapRenderContext` and **Map Standard URP** tier, with terrain renderer **0**. Osaka uses the same pipeline with street renderer **1** and its glow feature. The context restores the previous quality tier when unloaded. The project default and previously unassigned quality tiers now use URP to satisfy the installed Unity 6 player-build validation; menus keep their existing UI shaders under URP. Platform quality selections are retained, and `BattleTeyvat` remains enabled in build settings.
- Existing Standard actor materials receive transient URP Lit copies when fighters spawn. Source fighter materials are untouched. The adapter preserves supported metallic/specular, texture and transparency settings; custom shaders require their own compatibility review. `Training` uses the same context to adapt its Standard scene materials.
- Rebuild through **Fighting Allstar → Stage → Build Playable Teyvat Dungeon**, after saving open scene edits. The builder copies the production battle scene and accepted art references rather than editing the original battle scene. After stage or layout changes, run **Fighting Allstar → Stage → Optimize Battle Maps** to refresh the map setup, occlusion bake and performance review scenes; see [MapOptimization.md](MapOptimization.md).

## Verified

`Assets/Project/Editor/TeyvatDungeonValidation.cs` runs a disposable generated Genshin encounter with the real battle controller. It checks the resumed-profile routing policy, URP activation, 45 baked probes, 27,737 bound meadow placements, six active fighters plus hidden reserve, supported actor shaders and the card hand. It queues real actions through planning, execution and completion and applies the battle result to the in-memory run. Its renderer handoff check now expects Osaka's shared URP context to take over after Teyvat unloads.

The smoke run uses temporary high fighter stats to shorten verification. It does not write account inventory, rewards or run saves. It does not automate the entry/loadout pointer interactions or the persisted reward screen. Production combat stats and save logic are unchanged.

Evidence is in `Artifacts/TeyvatGrassland/PlayableDungeon`: `BuildPassed.txt`, `PlayPassed.txt`, `Verification.json`, and three combat screenshots. To repeat the isolated smoke, create `Temp/check-teyvat-dungeon.request` while the Editor is idle; it changes the open scene and enters/exits Play Mode.

## Visual context and performance evidence

The main project remains **Gamma**; the isolated WP08 art preview was **Linear**. Use the current production URP scenes for visual acceptance rather than assuming pixel-identical output between those rendering environments. Fighter visuals remain the currently authored placeholders.

The shared Windows URP benchmark and separate Built-in Osaka baseline are described in [MapOptimization.md](MapOptimization.md). Their evidence and final performance report are being assembled in [Artifacts/MapOptimization](../../../Artifacts/MapOptimization); this document does not yet assert measured budget results. The six-camera benchmark measures the map workload, not full combat. Keep the gameplay smoke evidence above separate from the pending Windows budget assessment, and validate representative battles with actors, UI and effects before claiming a full combat performance budget.
