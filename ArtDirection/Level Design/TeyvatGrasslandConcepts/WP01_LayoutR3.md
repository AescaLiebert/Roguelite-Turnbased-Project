# WP01 revision 3 — terrain composition redesign

2026-10-18. This is the user-requested replacement for the earlier surrounding-land layout. It follows the battle concept and cutaway reference: battle meadow → sloping bank → river/islands → waterfall landing shelves → unequal cliff terraces → distant landmarks. It also replaces WP04's short-grass policy with grass at 40–60% of the actor proxy height. Earlier WP01 r2, WP03 and WP04 assets remain available as prior revisions.

## Spatial decisions

The six fighter anchors, arena origin and seventeen registered camera poses stay unchanged. The floor immediately around them remains compatible with their ground height. Outside that corridor, the native terrain is redesigned rather than held equal to the old heightfield: unequal perimeter mounds, lowered river banks, a broad basin, five island tops, a low middle valley, upper setbacks and reverse-view hills.

All dimensions below are **starting art values**, measured in the local arena frame. The microtest is the camera-matched Unity review: islands must read as separate landforms with water between them; ledges must have visible depth; lower terrain and tall grass must preserve fighter head visibility. If a landform merges visually, adjust its footprint, separation or height before adding small surface noise. These are not an industry budget or a final performance target.

| Island | Local X/Z | Approximate full footprint | Native top above battle datum |
| --- | --- | --- | --- |
| Heron | −45 / 37 | 12 × 7 m | 0.7 m |
| Willow | −12 / 43 | 15 × 8 m | 1.1 m |
| Split | 30 / 40 | 10 × 6 m | 0.5 m |
| LeftCascade | −42 / 55 | 8 × 5 m | 2.5 m |
| RightCascade | 43 / 53 | 13 × 8 m | 1.7 m |

Island outlines rotate independently, have broader grass tops, irregular stone shore rims and submerged flared feet. River level is local Y −1.35 m. The banks widen near the rear islets so those landforms remain surrounded by water. Larger stones at the falls and smaller bank stones provide a size hierarchy without filling the channel with equal-sized props.

The main escarpment no longer presents one straight wall. Its front recesses and projects across X, the left and central landforms rise above the right shoulder, and a low valley separates the middle/right masses. Each closed cliff module has sloped feet, two lower benches, fractured main planes, a grass lip and a deep upper setback. Ledge heights and fracture columns vary together rather than using fine random bumps. A second, higher terrace carries the cathedral and aqueduct.

The three waterfalls have different widths and crest levels. Their six-point courses descend through an upper fall, catch shelf/pool, lower spill and river contact. Central crest is about 23.43 m, left 22.74 m and right 14.76 m above the arena datum. These values and all path points are in `LayoutR3/Data/TG_LayoutPlan.json`. Closed landing rock supports and toe talus sit beside the courses; WP06 still owns polished water breakup, foam, mist and erosion treatment.

Foreground rocks form primary/secondary/small clusters around the left tree, right ruin and near slopes. The hero tree frames arena left; smaller groups reinforce the ruin side and islands. Mixed broadleaf/conifer groves occupy terrace and reverse-hill clusters. `TG_TreeComposition.json` stores their placement guides. These are layout tree representations, not the final WP05 tree kit.

## Tall grass and combat readability

Grass grows throughout the battlefield, including around character positions. Its actual transformed LOD0 geometry is measured against the **3 m review proxy**, giving approximately **1.2–1.8 m** grass. Growth close to actors favors the lower end; broad border and slope patches favor the upper end. Clover is understory and flowers are accents, so not every plant has the same height.

The height request supersedes the older 0.35 m combat-grass cap. The old bare-ground mask is not reapplied. Seeded clustered distribution uses the highest actual native/mesh surface and rejects water, rock faces and root gaps. Camera/head clearance replaces the previous low torso-line assumption. Static capsules are scale proxies: animation, real character proportions, attack trails and skill effects still require WP07/WP09 testing.

This is a visual/layout change; it does not change combat state, controls or game rules. The game-design priorities here are clarity (heads, action targets and camera paths) and fit (a lush Teyvat-inspired meadow). Grass density is useful only where it improves the image while preserving those conditions.

## Files and reproduction

- [Editable Blender source and plan generators](../../ArtSource/TeyvatGrassland/LayoutR3/).
- [Unity layout assets](../../Assets/Project/Art/Environment/TeyvatGrassland/LayoutR3/), including the native TerrainData, geometry, water-course meshes, meadow, materials and prefab.
- Review scene: `Assets/Project/Art/Environment/TeyvatGrassland/LayoutR3/Scenes/TG_LayoutReview_r3.unity`.
- [Evidence and handoff](../../Artifacts/TeyvatGrassland/WP01/Revision03/).
- [Isolated Unity launcher](../../Tests/Environment/RunTeyvatLayoutR3Lab.ps1).

From the repository root:

```powershell
python 'ArtSource/TeyvatGrassland/LayoutR3/create_layout_plan.py'
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' --background --factory-startup --python 'ArtSource/TeyvatGrassland/LayoutR3/create_layout_geometry.py'
& 'Tests/Environment/RunTeyvatLayoutR3Lab.ps1'
```

The launcher starts a hidden process in the isolated `.utmp/TeyvatWorldLab` art host. Wait for its current `COMPLETE.txt`/`FAILED.txt` and reports; do not start another editor on that host while it is running. A fresh `-VerifySaved` process opens the saved scene without rebuilding or reconfiguring rendering. The main game remains Built-in/Gamma; the review uses accepted URP 17.3 Forward/Linear, Unity 6000.3.4f1. Open that isolated project in Unity Hub to inspect the scene with its intended renderer.

Blender uses metres, native Z up and the proven raw Unity basis `(Blender X, Blender Z, Blender Y)`. FBX exports only the authored modules, with explicit triangulation before custom normals, UV0 and rigid numerical RGBA. The `.blend` also contains an editable native-ground reference, excluded from FBX to avoid drawing duplicate ground in Unity. The canonical terrain samples drive Unity exactly within native quantization. Unity source uses the existing eight-species WP04 kit and its shared wind/color interface; no second wind clock or global shader change is introduced.

The layout prefab contains world-coordinate meadow data. Moving only its Transform does not relocate those matrices; regenerate or deliberately transform/revalidate placement data when relocating the stage. Scene-level wind, lighting and cameras remain the master scene's responsibility. The new terrain asset is allocated before its control maps are written, avoiding transient native terrain-copy issues encountered in WP04.

## Review status and limits

Read `LayoutR3Report.json`, `CompositionValidation.json`, `ImportValidation.json`, `HeightRatioReport.json`, `CameraEnvelopeReview.json` and the independent `Reload/` results together. The handoff inventory records final files and preservation hashes. Main Battle, renderer settings and older stage assets are outside this redesign.

| Review item | Recorded revised-layout result |
| --- | --- |
| Native landscape | Independent 513 × 513 TerrainData; all samples match the canonical R3 source within 0.004 m native quantization tolerance. Six original anchor heights pass their 0.02 m starting tolerance. |
| Island separation | Five raised island centers; all twelve exterior bed probes around each are below the water level. |
| Waterfall hierarchy | Three distinct descending six-point courses, with upper fall, intermediate landing, lower spill and river contact; endpoint height within 0.08 m of river level. |
| Cliff/stone source | 186 imported meshes, 26,888 FBX triangles including alternate representations and marker; 20,216 visual LOD0 triangles. Native ground, assembled trees and instanced grass are additional. Source closure/import checks pass. |
| Meadow | 27,856 saved placements. Measured grass geometry is 1.223–1.800 m, averaging 1.635 m against 3 m actor proxies. Root gaps/burial stay within the stated sampled starting tolerances. |
| Cameras and bounds | Seventeen original camera poses retained; twenty review cameras total. Both planning views contain all six actor bounds. Eight paths / 264 samples pass static world and grass-head clearance. No sampled animated grass vertices escape stored bounds. |

Capture folders contain neutral and afternoon versions of every review camera. Shader/bounds, missing-script and composition results are in the JSON reports. Performance remains unaccepted: taller, overlapping geometry increases workload, and no named minimum-PC player target has been supplied. The R3 images and API counters must not be presented as a frame-rate guarantee.

The retained exact planning camera looks steeply down and cannot show the complete distant skyline. Its checks concern the meadow, river and fighter framing. Use `ReferencePlanningCandidate`, attack views and the island/cascade/terrace studies for the full landscape composition. Seventeen original poses are retained; the three added study cameras are for terrain inspection.

This revision supplies substantially revised topology and placement, not finished Genshin visual parity. Water patterns, sky, final tree assets, detailed surface painting and lighting remain visibly provisional. The source includes authored lower detail levels; automatic transitions and tall-grass shimmer require moving-camera player review. Static ray/bounds checks and editor screenshots are not a standalone Windows performance acceptance or an animated combat test.
