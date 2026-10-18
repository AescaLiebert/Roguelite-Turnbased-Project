# WP04 — meadow and flower lane r1

2026-10-18. Windows PC art target. This package supplies authored grass, ground cover and flowers, a reproducible distribution, and a separate Unity review scene around the accepted WP01/WP03 landscape. **WP04 r1 passes its static checks and an independent cold saved-scene verification.** The appearance remains an art work in progress and does not claim Genshin reference parity. Final performance acceptance remains provisional until the named minimum-PC player test.

## User direction and preserved layout

The user's latest direction permits grass throughout the battlefield, including all six character positions, and asks for denser clustered cover. This overrides the earlier interpretation of the WP01 green mask as a bare-ground exclusion. Green now selects **short growth**, with a 0.35 m undeformed height cap as a review starting value. Taller border grass, accents and flowers stay outside that region; camera clearance and fighter torso visibility still constrain placement. Adjust height and density against planning and attack captures if short cover hides feet, distracts from attacks, or looks too sparse.

The original `FoundationR2/TG_MeadowPlacementMask.png`, six anchors, camera poses, world origin and terrain heights remain unchanged. `Meadow/Data/TG_MeadowDensityPolicy.json` records the user override instead of rewriting the accepted mask. The battle and four-angle concepts still define composition; the real-game screenshots guide foliage and surface treatment.

WP04 uses its own `Meadow/Terrain/TG_MeadowGround.asset`. This independent TerrainData softens grass/soil control weights in the clearing, with a blend confined to local `|X| < 26 m`, `|Z| < 24 m`. The builder first persists fresh native data, then explicitly copies resolutions, size, layers, height/hole arrays and accepted WP03 layer weights before adjusting them. Native control weights are checked immediately after writing and again after cold reload. Outside that region, the surface check allows only the recorded control-texture quantization tolerance. Height preservation is checked independently against every WP01 height sample. The original World terrain asset and World review scene remain separate inputs.

## Assets and integration

| Deliverable | Location |
| --- | --- |
| Editable Blender scene, generator, FBX and source manifest | [ArtSource/TeyvatGrassland/Meadow](../../ArtSource/TeyvatGrassland/Meadow/) |
| Unity models, animated meshes, materials, shader, runtime and placement data | [Meadow](../../Assets/Project/Art/Environment/TeyvatGrassland/Meadow/) |
| Reusable meadow module | `Meadow/Prefabs/TG_Meadow.prefab` |
| Separate review scene and native ground clone | `Meadow/Scenes/TG_MeadowReview.unity`, `Meadow/Terrain/TG_MeadowGround.asset` |
| Build and cold-reload launcher | [RunTeyvatMeadowLab.ps1](../../Tests/Environment/RunTeyvatMeadowLab.ps1) |
| Captures, JSON checks and handoff package | [WP04/r1](../../Artifacts/TeyvatGrassland/WP04/r1/) |

The kit contains **eight species**, each with two authored detail levels: ShortGrass, MeadowGrass, TallGrass, AccentGrass, Clover, DaisyWhite, FlowerGold and FlowerPurple. Sixteen vegetation meshes plus one diagnostic axis marker contain **1,974 source triangles**: 1,462 vegetation LOD0, 428 LOD1 and 84 marker triangles. These are reusable source counts, not the instanced scene's triangle count or a performance budget. Grass LOD1 keeps selected LOD0 blade roots, tips and phases; endpoint-subset assertions run during generation.

Tapered blades, clover leaves, stems and petals use actual geometry silhouettes, without foliage alpha bitmaps. UV0, upward-biased custom normals and numerical RGBA are authored and imported: R is root-to-tip bend, G is zero, B is a stable element phase and A is one. Grass roots sample the accepted shared world-color field; white, gold and purple petal slots preserve their own colors. **All meadow slots use wind response 0.65**, including stems and petals, so a material boundary does not tear a connected flower apart.

The lane-owned `Teyvat/URP/MeadowFoliage` shader keeps the shared material interface and includes `TG_Common.hlsl`. Its scoped addition reduces wind with the same distance fade as the geometry. Forward, ShadowCaster and DepthOnly use one deformation function. There is no foliage bitmap mip/compression dependency; geometric edge shimmer still needs visual review. DepthNormals, motion vectors, SSAO, TAA and actor bending are outside this revision.

`TeyvatMeadowRenderer` reads persisted seeded matrices from twelve-metre cells, caches placement bounds, culls cells, and submits species/material submeshes in pooled groups of at most **500 instances**. Saved cells include both LODs, full matrix rotation/scale/mesh-center transforms and independent world wind padding. LOD thresholds are distributed from **30–50 m**; root-preserving geometry shrink and matching wind fade run from **70–105 m**. These are starting distances for camera review, not accepted universal settings.

The prefab contains the meadow renderer and placement data, without cameras, lights, collision or a second wind clock. Supply the scene's one `TeyvatMaterialGlobals` owner and the accepted world color field/bounds. A null prefab globals reference resolves that same-scene owner. Remove optional WP02 sample-grass submissions when integrating, to avoid drawing two grass systems. The runtime skips Built-in rendering.

Matrices and cell bounds use **world coordinates** at the accepted WP01 origin. Moving the prefab transform does not relocate the grass. Regenerate or deliberately transform and revalidate the data when relocating a stage. After changing matrices, phases or mesh bounds in place, call `InvalidatePlacementCache()`; replacing data/species/cell-array references invalidates automatically. See the module and source READMEs for import and export details.

## Open, reproduce and inspect

Open **`E:/Program Files/Unity/RogueliteTurnbasedProject/.utmp/TeyvatWorldLab`** in Unity Hub with **Unity 6000.3.4f1**, then open `Assets/Project/Art/Environment/TeyvatGrassland/Meadow/Scenes/TG_MeadowReview.unity`. WP04 reuses this isolated cached art host with **URP 17.3 / Forward / Linear** while saving its own scene. The main game remains Built-in/Gamma; importing the module does not migrate the project's renderer.

From the repository root, regenerate the editable kit if needed, then build the Unity review:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' --background --factory-startup --python 'ArtSource/TeyvatGrassland/Meadow/create_meadow_kit.py'
& 'Tests/Environment/RunTeyvatMeadowLab.ps1'
```

The launcher returns while a hidden Unity process runs. Close that isolated project before starting another process. Wait for the process to finish; read the current `r1/COMPLETE.txt` or `FAILED.txt` together with the reports, rather than relying on old captures. Rebuilding replaces generated assets and evidence; preserve manual source revisions and `.meta` files first. The generator uses the adjacent WP02 geometry helper and Blender's bundled FBX exporter.

After a successful build, verify persistence without rebuilding or copying assets:

```powershell
& 'Tests/Environment/RunTeyvatMeadowLab.ps1' -VerifySaved
```

The cold run opens the saved scene in another process. Its output is `r1/Reload/`; a successful run exports `r1/TG_WP04_Meadow.unitypackage` with referenced asset dependencies. The package contains assets, not a main-project pipeline migration. Install the matching URP version and Linear/accepted renderer settings before using it in another art project. Durable source, Unity assets and the launcher reproduce the disposable `.utmp` workspace.

## Verification and remaining work

The current [MeadowLabReport.json](../../Artifacts/TeyvatGrassland/WP04/r1/MeadowLabReport.json) and `COMPLETE.txt` were written at **19:19 on 2026-10-18 (Asia/Bangkok)**. Import, placement, terrain surface, wind, camera envelope, actual GPU instancing coverage, shaders and missing-script checks pass in Unity 6000.3.4f1 on AMD Radeon(TM) Graphics. A warm build alone does not establish saved-scene persistence.

| Check | Observed current result and scope |
| --- | --- |
| Distribution | 25,521 placements in 240 cells; eight species. Zero detected tall-combat-cover, invalid-species, cell-bound or torso/camera-clearance violations. |
| Ground contact | All placement origins pass the 0.055 m starting tolerance, including intentional 0.025 m burial. 14,976 sampled individual roots pass the starting contact range −0.13 to +0.08 m; greatest sampled burial is 0.02875 m. This samples up to 64 placements per species, not every blade root. |
| Native surface | Independent WP04 data; 470 control samples changed, zero outside-region violations and zero measured outside change. Minimum combat grass weight is 0.85098. All native height samples match WP01; six anchors retain +0.00030518 m signed error. |
| Camera envelope | Original nineteen views have zero detected actor-center obstruction; both exact planning views contain all six conservative actor bounds. Eight paths / 264 samples pass. Meadow bounds are checked separately against saved torso rays and camera volumes. |
| Wind and instancing | Sampled roots stay fixed and tested animated vertices remain inside persisted bounds. Actual GPU route covers all 7,200 submesh sample points at each LOD for 3,600 placements, with zero misses, 18 API submissions per LOD and maximum group size 500. |
| Editor cost observation | At 1600 × 900 with the accepted 4× MSAA project setting, synchronized offscreen render/readback median is 12.93 ms without meadow and 32.24 ms with meadow. The measured planning view submits 13,311 instances, 48 API calls and 1,241,379 triangles; median prepare/submission CPU counters are 6.07/0.72 ms. These include Editor/driver/readback overhead and are not player FPS, hardware draw counts or an accepted budget. |
| Cold persistence | Independent new process opens the saved scene without Build or pipeline reconfiguration: all checks pass, 25,521 placements, 240 cells, 21 cameras and zero missing scripts. Native surface, root contacts, cell bounds, camera paths, wind and actual GPU coverage pass again; `Reload/COMPLETE.txt` confirms this run. |

Review `Afternoon/` and `Neutral/` planning, attack, reverse and modeling captures, plus `TG_MeadowClose` and `TG_MeadowFlowers`. The two botanical cameras frame roots and petals and are outside the original nineteen fighter-camera checks. `Wind/`, `Temporal/` and `Distance/` provide fixed-time, small camera-motion and retreat comparisons. Static screenshots support art review but do not prove continuous shimmer-free motion.

Main Battle, WP01 and WP03 review scenes, the original mask/World terrain and main rendering settings retain their pre-WP04 hashes; see `PreservationCheck.json`. The Unity package and editable-source archive are inventoried in `HandoffManifest.json`. The source archive requires the accepted WP01–WP03 Unity inputs from this repository or delivered dependencies; it is not a standalone Unity project.

The context trees, water and lighting remain WP01/WP02 assets for WP05, WP06 and WP08. This scene uses static actor proxies. Actual moving combat, attack pivots, shake, effects, continuous camera clearance and final module composition remain WP07 work. Editor render/readback timing is an observation on the reported device, not player FPS. A named minimum-PC target and standalone player performance acceptance remain WP09 dependencies.
