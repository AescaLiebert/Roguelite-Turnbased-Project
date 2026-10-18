# WP05 — tree and canopy lane r1

2026-10-18. Windows PC art target. **Tree/canopy r1 is implemented and passes the recorded import, integration, sampled camera/wind and cold saved-scene checks.** WP05 replaces the R3 tree placeholders with authored assets and a separate canopy review scene. Further art polish and moving-combat review remain; this handoff does not claim finished Genshin visual parity or a player performance pass.

## Assets and composition

| Deliverable | Location |
| --- | --- |
| Editable Blender source, generator, FBX and manifest | [ArtSource/TeyvatGrassland/Trees](../../ArtSource/TeyvatGrassland/Trees/) |
| Imported models, animated mesh copies, materials and prefabs | [Trees](../../Assets/Project/Art/Environment/TeyvatGrassland/Trees/) |
| Independent assembled review scene | `Trees/Scenes/TG_CanopyReview_r1.unity` |
| Representative small sample | `Trees/Scenes/TG_CanopySample_r1.unity` |
| Repeatable isolated launcher | [RunTeyvatTreesLab.ps1](../../Tests/Environment/RunTeyvatTreesLab.ps1) |
| Evidence and exported asset package | [WP05/Revision01](../../Artifacts/TeyvatGrassland/WP05/Revision01/), [TG_WP05_Trees.unitypackage](../../Artifacts/TeyvatGrassland/WP05/Revision01/TG_WP05_Trees.unitypackage) |

Six species have **three authored LODs each**: HeroOak, MeadowOak, IslandTree, WindPine, YoungPine and Shrub. The editable kit uses metre-scale source heights of 12, 8, 6, 8, 6 and 1.8 m respectively. These are source starting values; scene trees are uniformly scaled to their accepted R3 height guides. Eighteen vegetation meshes plus an asymmetric diagnostic marker form the imported kit: **121,590 source triangles**, including every alternate LOD and the marker. The six species once each total 85,296 triangles at LOD0, 23,472 at LOD1 and 12,738 at LOD2. These kit totals are not visible-frame costs or accepted performance budgets.

All **120 R3 trunk X/Z positions and target heights** are retained from [TG_TreeComposition.json](../../Assets/Project/Art/Environment/TeyvatGrassland/LayoutR3/Data/TG_TreeComposition.json). Hero/perimeter/island roles select their corresponding broadleaf species. Seeded terrace and reverse groves mix broadleaf and conifers using location, rather than the previous repeating index rule. Yaw varies reproducibly. Trees sit with a small intended root burial; the assembled placement record is `Trees/Data/TG_TreePlacement.json`.

The assembled scene adds **70 shrubs** as root-cluster accents, not replacement grass or an extra meadow system. Their roots sample the unchanged native R3 terrain, rejecting water and the central actor region. The R3 native landscape, meadow, six fighter anchors and seventeen registered cameras remain inputs to the separate review scene. Larger tree crowns still require integrated art review around the hero-tree frame, ruin arch and island/fall sightlines.

## Geometry, shading and wind

[tree_shapes.py](../../ArtSource/TeyvatGrassland/Trees/tree_shapes.py) supplies the editable deterministic branch/crown records. The hero has four unequal crooked primary limbs with secondary forks and uneven crown groups; three interior bridge crowns connect its upper canopy while retaining lower branch windows. MeadowOak stays upright and rounded, while IslandTree leans. Pines use unequal vertical gaps, missing radial arms and fuller flattened bough groups beneath a tapered leader. The shrub has three unequal overlapping groups.

Broadleaf **LOD0 and LOD1 have no closed canopy-core faces**: small pointed folded geometric leaves and curved alpha-clipped spray cards supply their foliage. LOD2 adds a deeply recessed core at **0.45** of the lobe envelope to support distant volume. Pines retain **0.85** geometric cores, tiered bough volumes and geometric needles; they do not use the broadleaf spray texture. Higher detail starts from 60 small geometric leaves per broadleaf lobe and 150 per pine lobe, plus **32 curved sprays per broadleaf lobe**.

[TG_LeafSprayAlbedoOpacity.png](../../ArtSource/TeyvatGrassland/Trees/Textures/TG_LeafSprayAlbedoOpacity.png) is an original RGBA spray generated with the built-in image tool and copied unchanged into the source and Unity texture folders. It is also packed into the editable `.blend`. Pale neutral leaves receive green tint in the material. [Provenance.json](../../ArtSource/TeyvatGrassland/Trees/Textures/Provenance.json) records the exact prompt, source image and usage. Import uses sRGB color, input alpha, transparency-aware edges, trilinear mipmaps with alpha-coverage preservation at **0.60**, Clamp wrap, maximum size **1024** and uncompressed review storage. The actual imported texture is **1024 × 683 with 11 mip levels**. Compression, small-mip silhouette quality and overdraw still need player-target review.

Leaf custom normals come from the analytical ellipsoid envelope, softened toward up. Spray geometry orients around the actual outward lobe direction; its separate lighting normals retain that soft upward bias. This avoids using lighting-normal bias to flatten all cards into one geometric direction. Import the custom normals; do not recalculate them. Core/geometric-leaf materials use the shared `Teyvat/URP/Foliage` interface with Leaf family, opaque clipping disabled and **Cull Off**. Broadleaf spray materials use the same double-sided shader with alpha clipping enabled and cutoff **0.60**. Bark remains opaque with **backface culling**, no transmission and a lower diffuse wrap. Warm upper-leaf and cool interior tints are material inputs; final lighting belongs to WP08.

Numerical vertex RGBA supplies R=0 on wood/core and geometric-leaf attachments, rising toward leaf tips for flutter; spray R is low in the card center and higher at outer edges. G is continuous height-based sway on wood, cores and foliage; B is the stable tree phase; vertex A is one and texture alpha supplies spray opacity. All slots use response **0.65** with the scene's one `TeyvatMaterialGlobals` clock. The unchanged `TG_Common.hlsl` function drives Forward, ShadowCaster and DepthOnly; those passes also share spray alpha clipping. It is a shader displacement approximation, not skeletal branches or a per-branch pivot simulation; normals are not bent with that displacement.

Animated bounds are expanded in dedicated Mesh assets so they survive scene serialization. Renderer-only bounds overrides are not the persistence route. Each species keeps identical clump/branch records, root pivot and source scale across LODs. LOD1 keeps a subset of small geometric leaves and half the broadleaf sprays; LOD2 removes those small leaves, retains one quarter of the sprays and adds the recessed broadleaf cores. Pine cores and branch tubes simplify with distance. Unity LOD screen-relative thresholds **0.12 / 0.045 / 0.002**, with no crossfade, are starting review values. Inspect silhouette changes, branch exposure and final culling through moving battle cameras before accepting them.

## Review and reproduction

Use Unity **6000.3.4f1**, **URP 17.3 Forward / Linear**, in the isolated art project **`E:/Program Files/Unity/RogueliteTurnbasedProject/.utmp/TeyvatWorldLab`**. Open `Assets/Project/Art/Environment/TeyvatGrassland/Trees/Scenes/TG_CanopyReview_r1.unity`. The main game remains Built-in/Gamma; this package does not migrate its renderer or gameplay scene.

From the repository root:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' --background --factory-startup --python 'ArtSource/TeyvatGrassland/Trees/create_tree_kit.py'
& 'Tests/Environment/RunTeyvatTreesLab.ps1' -SampleOnly
& 'Tests/Environment/RunTeyvatTreesLab.ps1'
```

Run these as separate stages, waiting for each Unity process to finish before the next. `-SampleOnly` renders representative hero/pine/shrub views for early art inspection; it skips assembled-stage validation and is not an acceptance result. The default mode builds the species prefabs and the distinct assembled review scene. The launcher refuses an already-open isolated host and starts a hidden batch process. Preserve manual source changes and `.meta` files before regenerating; the generator replaces its Blender/FBX/manifest outputs and copies the import files into Trees/Models.

Read the latest `COMPLETE.txt` or `FAILED.txt` and `TreeValidation.json` together with the images. After a successful assembled build, run:

```powershell
& 'Tests/Environment/RunTeyvatTreesLab.ps1' -VerifySaved
```

The cold process opens the saved scene without rebuilding, copying assets or changing its renderer. Successful cold output belongs in `Revision01/Reload`; it exports `Revision01/TG_WP05_Trees.unitypackage` with referenced asset dependencies. A representative sample marker or an older report is not proof that the current stage passed. Importing the package elsewhere requires the matching URP/Linear setup and scene-level shared material globals.

## Technical evidence and remaining work

The current [TreeValidation.json](../../Artifacts/TeyvatGrassland/WP05/Revision01/TreeValidation.json), [TreeIntegration.json](../../Artifacts/TeyvatGrassland/WP05/Revision01/TreeIntegration.json) and their independent [Reload](../../Artifacts/TeyvatGrassland/WP05/Revision01/Reload/) counterparts pass. The final warm marker was written at **21:11:45**, and the cold marker at **21:13:33 on 2026-10-18 (Asia/Bangkok)**.

| Check | Recorded result and scope |
| --- | --- |
| Import | All 19 raw meshes pass FBX hash, metre/axis bounds, triangle counts, UV0, numerical RGBA and custom-normal range checks. Range checks do not prove per-loop correspondence. |
| Placement/integration | All 120 R3 trunk X/Z anchors retained; 70 shrub groups. Maximum target-height error is 0.0000002384 m; maximum root-origin ground error is 0.040001 m. Buttress-tip contact on slopes remains an art/set-dressing check. |
| Cameras | All seventeen original camera poses/FOVs retained; head-ray and conservative camera-bound checks pass. Eight paths / 264 samples pass. Tests use actual undeformed LOD0 triangles and stored animated bounds, not complete animated characters. |
| Wind/buffers | Zero escaped sampled wind vertices and zero sampled root displacement. Checked animated vertex/normal/color/topology streams match the raw imported meshes in both processes. The builder explicitly copies vertex/normal/color/UV/submesh data to avoid stale native mesh contents. |
| Rendering/persistence | One shared globals owner; supported scene shaders, zero missing scripts, retained R3 terrain/meadow references and valid texture/mip settings. A fresh process reopens the saved scene without rebuilding and repeats the checks successfully. |

Neutral and afternoon captures, underside/reverse sample views, wind times and forced LOD views support canopy art review. [CanopyCost.json](../../Artifacts/TeyvatGrassland/WP05/Revision01/CanopyCost.json) records six alternating on/off measurements after warmup on **AMD Ryzen 5 4600G / AMD Radeon(TM) Graphics**. At 1600 × 900 with 4× MSAA, the medians are **112.01 ms with canopy / 96.63 ms without**. Each measurement includes **two offscreen render submissions and synchronous CPU image readback**. These noisy Editor observations are neither GPU timings nor player FPS; the difference is not an isolated GPU canopy cost or minimum-PC certification.

Water/cascades remain context for **WP06**; final atmosphere and character lighting remain **WP08**. Actual characters, animation, attack effects, camera transitions and composition integration require WP07 review. Fine-leaf shimmer, wind/depth/shadow agreement, automatic LOD changes and standalone performance on a named minimum Windows PC remain unaccepted until tested. SSAO, motion vectors and TAA are not supplied by the shared foliage shader. The hero trunk is partly concealed by the retained foreground boulder; final rock/root dressing belongs to that integration pass.

## Packaged handoff

The delivered Unity package contains **108 assets** with the review scene and its dependencies. It excludes gameplay scenes and project/package settings. The editable source archive contains **17 files**, including the packed Blender source, FBX, generators, original spray PNG/prompt provenance, import builder/validation and frozen layout guides. [PackageContents.json](../../Artifacts/TeyvatGrassland/WP05/Revision01/PackageContents.json) records the exported inventory and hash; [HandoffManifest.json](../../Artifacts/TeyvatGrassland/WP05/Revision01/HandoffManifest.json) records owned-file hashes. [PreservationCheck.json](../../Artifacts/TeyvatGrassland/WP05/Revision01/PreservationCheck.json) confirms **296 protected main, R3, Shared and Meadow files** match their starting hashes.
