# Teyvat surrounding scenery revision

## Intent

Replace the sparse left, right and rear middle/farland with a connected landscape. The supplied great-tree valley, snowy mountain and coastal-island images guide the silhouette hierarchy and environmental story. The original arena, forward river/islands/cascades and combat anchors remain the composition's centre.

- **Left — Great Tree sanctuary:** a large oak rises above layered root terraces, surrounded by smaller woodland and weathered pilgrim markers. Trees and rock groups continue up the west river gate and high pasture.
- **Right — Tidewatch coast:** woodland frames an open valley towards sea stacks and a ruined beacon. Islands have different silhouettes and heights; the outer ocean connects the river landscape to a continuous water horizon.
- **Rear — Frost Crown:** broadleaf copses become conifer foothills, then an asymmetric snowy massif. Foreground rocks, two shoulder ridges and distant spurs give the reverse combat view distinct depth layers.
- **Forward cliff sides:** denser terrace groves and rock groups extend the original waterfall composition towards both edges. Waterfall channels stay open.

The former very wide blue ridge strips were removed from the new stage because they read as vertical walls from side/reverse angles and obscured the landmarks. Their replacement is shaped terrain and a northern ridge beyond the cathedral. A coastal land apron continues beyond the native terrain border, with submerged seabed under the outer ocean.

## Assets and rebuild

- Production stage: `Assets/Project/Art/Environment/TeyvatGrassland/Surroundings/Prefabs/TG_SurroundedBattleStage.prefab`.
- Production scene: `Assets/Project/Scenes/BattleTeyvat.unity`.
- Scenery review: `Assets/Project/Scenes/EnvironmentReview/TeyvatGrassland/TG_SurroundingsReview.unity`. Play this review scene to activate its own renderer. It contains the 17 original camera poses and eight additional direction/landmark studies.
- Builder menu: **Fighting Allstar → Stage → Build Teyvat Surrounding Scenery**. Save open edits first. It regenerates the added scenery from seed194731 and updates the dungeon scene. The existing playable-dungeon builder also uses the new stage.

Geometry, materials and placements are saved assets. There is no runtime procedural generation. Added trees use the existing three-level tree kit and WP08 material copies. Rock kits are recentered from their baked arena coordinates and retain their individual rock/shade/moss slots. The sea has an opening around the original native terrain footprint, avoiding a second transparent layer inside the existing river basin. Added scenery has no gameplay colliders.

Starting dimensions and density were tuned against the directional studies and battle camera captures. They are composition values, not a performance budget. The playable Teyvat camera now draws to1600m so it can display the new skyline; the previous140m limit clipped the farland. Original Osaka camera settings are retained.

## Verification

Evidence lives in `Artifacts/TeyvatGrassland/Surroundings`:

- `Before` and `After`: matching renders from17 authored cameras plus eight directional/landmark studies.
- `Comparison.png`: left/right/rear comparison, before on the left and revised on the right.
- `After/NewSceneryCameraSweep.json`: eight orbit legs ×33samples, testing six actor-head sightlines against all added opaque renderer bounds and checking native camera-ground clearance. The apron is checked by its referenced vertices outside the30m keepout instead of its deliberately hollow overall bounds. This is conservative geometric evidence, supplemented by the real combat smoke.
- `Build.json`: final deterministic placement counts.
- `PreservationBefore.json` / `CompletionAudit.json`: original battle scene, global rendering/color-space settings, accepted lit source prefab and canonical meadow preservation checks, plus requirement-level evidence.
- `CombatSmoke.txt`: the real battle controller's planning, card execution, completion, in-memory dungeon-result application and renderer-restoration test. Test fixtures never write account progress or rewards.

The scenery pass preserves the main project's Gamma lighting and original grass placement data. It improves layout and silhouette density; it does not certify a Windows player build or frame-time budget. Distant geometry remains deliberately simple and uses existing distance haze. The review camera studies are evidence for this arena's views, rather than a claim of an explorable open world.
