# WP03 — world geometry r1

2026-10-18. Windows PC art target. **Status: world geometry r1 implemented and validated as a static asset handoff.** This package refines the world around the accepted WP01 r2 layout using the WP02 material interfaces. Numerical and multi-camera review results are recorded beside the generated Unity captures; final meadow/tree/water art, lighting, actual battle integration and player performance remain later packages.

## Reference priority

The [battle concept](02-battle-planning-camera.png), [environment concept](01-environment-overview.png) and [four-angle terrain reference](04-terrain-layout-multiview.png) define composition. Preserve the open foreground clearing, left tree area, right ruined arch, river and three waterfall positions, substantial stepped grassy shelves, left hilltop cathedral, right aqueduct and distant mountains.

The earlier real-game coastal cliff/city screenshots guide **shader and texture language only**. They do not replace this layout or call for new islands, windmills, a city or extreme projecting coastal landforms. The diorama's display plinth is a modeling reference, not a requirement to float the playable world in space.

## Delivered assets

| Asset | Location |
| --- | --- |
| Editable Blender sculpt, explicit FBX, generator and manifest | [ArtSource/TeyvatGrassland/World](../../ArtSource/TeyvatGrassland/World/) |
| Actual WP01 TerrainData height export used for joins | [TG_WP01TerrainSource.json](../../ArtSource/TeyvatGrassland/World/TG_WP01TerrainSource.json) |
| Unity-ready world models/materials/terrain | [World](../../Assets/Project/Art/Environment/TeyvatGrassland/World/) |
| Geometry-only reusable prefab | `World/Prefabs/TG_WorldGeometry.prefab` |
| Separate review scene | `World/Scenes/TG_WorldReview.unity` |
| Actual Unity images, diagnostics and handoff package | [WP03/r1](../../Artifacts/TeyvatGrassland/WP03/r1/) |
| Repeatable isolated review launcher | [RunTeyvatWorldLab.ps1](../../Tests/Environment/RunTeyvatWorldLab.ps1) |

The Blender source contains closed cliff volumes with broad fracture planes, grassy caps, side returns, rear faces and bottoms. Simplified static collision is separate from visible geometry. Paired detail levels are authored for cliff chunks, foreground boulders, ruin, cathedral, aqueduct and mountain ridges. These representations and screen-relative transitions are starting values; inspect their silhouettes before choosing final runtime distances.

The native WP01 r2 TerrainData is cloned into World assets with **unchanged height samples**. It retains the open meadow, connected basin and upper/reverse slopes. New cliff caps and landmark contacts sample an export of the actual 513 × 513 heightfield; Python noise is not substituted for Unity's terrain. Blender includes the sampled surface as an editable reference, excluded from the FBX so Unity does not draw a duplicate ground mesh.

The world prefab contains geometry, collision and the cloned ground. Scene-level material globals, cameras, lights, six capsule actor proxies, WP02 water and WP01 tree silhouettes remain outside that prefab. Trees are context for the existing composition, not a completed WP05 tree kit. The water uses the accepted WP02 shader over the basin and at the three existing fall positions; WP06 still owns breakup, foam, source channels, mist and transparency integration.

## Rendering and review

Open the existing-project folder **`E:/Program Files/Unity/RogueliteTurnbasedProject/.utmp/TeyvatWorldLab`** in Unity Hub with Unity **6000.3.4f1**. Open `Assets/Project/Art/Environment/TeyvatGrassland/World/Scenes/TG_WorldReview.unity`. This isolated art workspace uses **URP 17.3 / Forward / Linear**, matching WP02. The main game renderer and gameplay scenes are outside this change.

Review the original 17 saved camera poses, plus `SculptClose` and `ShelfDepth`. The source planning poses and all six anchor transforms are copied unchanged from WP01 r2. Neutral and afternoon captures separate surface defects from lighting. Forced LOD1 overview/side captures provide a first silhouette comparison; automatic thresholds still require integrated camera playback.

Rebuild from the repo root:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' --background --factory-startup --python 'ArtSource/TeyvatGrassland/World/create_world_geometry.py'
& 'Tests/Environment/RunTeyvatWorldLab.ps1'
```

The launcher returns while a hidden isolated Unity process runs. Wait for its process to finish and read `Artifacts/TeyvatGrassland/WP03/r1/COMPLETE.txt` or `FAILED.txt` together with the JSON evidence. After a successful build, a separate cold reload is available:

```powershell
& 'Tests/Environment/RunTeyvatWorldLab.ps1' -VerifySaved
```

Do not run against an isolated world lab that is already open. Rebuilding replaces generated source/Unity assets and captures; preserve manual sculpt revisions before running it. The disposable `.utmp` project can be regenerated from the durable source and Unity assets. After changes, validate and export a new delivered snapshot rather than assuming a previous package updates itself.

## Acceptance evidence and limits

Read `WorldLabReport.json`, `ImportValidation.json`, `CameraEnvelopeReview.json` and the corresponding `Reload` reports in the evidence folder. Import checks compare the actual FBX hash, metre/axis bounds, triangle counts, UV0, numerical RGBA and custom normal ranges with the Blender manifest. An asymmetric marker checks the raw mesh coordinate conversion without depending on FBX prefab-node transforms. Source welded boundary-edge checks verify closure of the authored volume components; separate masonry stones remain overlapping pieces, not a Boolean union.

| Check | Observed r1 result |
| --- | --- |
| Authored source/import | 159 meshes: 116 visual representations, 42 collision meshes and one diagnostic marker; import checks pass. |
| Detail levels | 47 paired LOD groups; forced lower-detail planning/overview/side views captured. Starting transitions still need integrated runtime tuning. |
| Mesh counts | 13,860 source triangles including alternate LODs and collision; 7,914 in visual LOD0. Native terrain and context vegetation are excluded from these source counts. These are asset statistics, not a performance budget. |
| Native ground | All 263,169 height samples match WP01 exactly. All six anchors have +0.00030518 m signed terrain error, within the unchanged 0.02 m starting tolerance. |
| Camera envelope | All 19 review views have zero detected actor-center obstruction; both exact planning views contain all six conservative actor bounds. Eight paths / 264 sampled positions have zero detected center obstruction. |
| Persistence | A separate cold process reopens the saved scene and repeats import, geometry/camera/ground and shader checks successfully. |
| Main source preservation | Battle, WP01 r2 scene, WP01 TerrainData and main graphics/quality/player settings retain their pre-task hashes. See `HandoffManifest.json`. |

The shared runtime has one scoped integration fix: publish color/rock/water globals for the same-scene camera even when no instanced grass is assigned. This prevents the world-only scene from losing its field before WP04. The grass submission schema is unchanged; the existing WP02 cold saved-scene / 4,200-instance GPU coverage diagnostic was rerun against that runtime.

Visual review covered neutral and afternoon planning, close cliff, both modeling sides, rear, reverse attack and lower-detail views. The cliff bands now have broader diagonal plane changes and closed returns, while the reference's stepped composition remains. The current tree silhouettes, regular water pattern, waterfall curtains, restrained surface detail and lighting are still visibly simplified. Those context limitations must not be presented as final reference parity.

Camera checks project conservative actor bounds in both planning views and test actor-center sightlines against the actual LOD0 sculpt plus native TerrainCollider. Eight sampled camera paths cover the same planning/attack/contact envelope as WP01. Ground checks compare all copied anchors with a 0.02 m starting tolerance and compare every native height sample exactly with WP01. If a check fails, adjust scenery or its joins rather than moving the accepted anchors/cameras.

These are static editor checks on the reported device. They do not establish continuous camera clearance, attack-pivot motion, animation, shake, character lighting, transparent combat-effect sorting or a player-build frame budget. The final level still needs WP04–WP06 asset work, WP07 integration, WP08 lighting and WP09 runtime review.
