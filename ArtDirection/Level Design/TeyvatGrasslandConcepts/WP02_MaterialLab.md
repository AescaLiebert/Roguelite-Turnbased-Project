# WP02 — shared materials and sample r1

Delivered 2026-10-18 for the Windows PC art pipeline. WP02 supplies the common materials and a small Unity material lab for the separate geometry, meadow, tree and water work packages. The lab contains simple authored sample meshes and six capsule scale proxies; it is not the finished Teyvat battle map.

The supplied real-game screenshots guide the warm grass tips, green canopy, quiet cool stone and turquoise water. Terrain and grass now share one ground-color field. Native terrain rock and mesh cliffs share world-projected paint without stretching on vertical faces. The canopy has controlled backlighting and a cool side tint; river color responds to actual opaque basin depth. The Fantasy FS003 Day Sunless sky is retained as a project asset dependency.

## Open and review

The tested art workspace is **`E:/Program Files/Unity/RogueliteTurnbasedProject/.utmp/TeyvatMaterialLab`**. Add that folder as an existing project in Unity Hub and open it with **Unity 6000.3.4f1**. Its renderer is **URP 17.3.0 / Forward / Linear**. Open `Assets/Project/Art/Environment/TeyvatGrassland/Shared/Scenes/TG_MaterialLab.unity`; the saved planning camera is enabled, and wind runs in Edit and Play modes.

The durable asset copy is in the main project's [Shared folder](../../Assets/Project/Art/Environment/TeyvatGrassland/Shared/). The main game still uses **Built-in/Gamma**. Its renderer, battle scene and WP01 layouts were not migrated by this package. View these URP materials in the isolated art workspace; opening their scene under Built-in does not provide the tested appearance.

An [asset handoff package](../../Artifacts/TeyvatGrassland/WP02/r1/TG_WP02_MaterialLab.unitypackage) includes the sample assets and referenced sky texture, without main project settings or gameplay scenes. For another art project, install the matching URP version, import the package, select Linear color space, assign `Shared/Settings/TG_MaterialURP.asset` to the project's active graphics/quality pipeline, and reopen the project before reviewing. The package is an asset handoff; the launcher below reproduces the tested project configuration.

## What is ready for the separate lanes

| Lane | Reusable WP02 foundation | Next art work |
| --- | --- | --- |
| WP03 world | `Teyvat/URP/Surface`, native `TerrainGrassland`, shared rock tint/scale/paint and a sample cliff | Sculpt the reference's cliff silhouettes, terrace/banks, ruins and skyline; provide collision and distance versions. |
| WP04 meadow | Grass/flower family, ground color field, root-fixed masks, deterministic wind and explicit instanced placement | Author varied clumps/flowers, painted density/exclusion masks and distance coverage; soften distribution boundaries. |
| WP05 trees | Trunk/leaf materials, imported custom normals, cool canopy side tint and serialized animated bounds | Build full rounded hero/midground canopies, branches, leaf shapes and distance versions. |
| WP06 water | Scene-depth color/foam interface, common animation clock, directional waterfall UVs and fixed-depth fallback | Break up waterfall silhouettes/streaks, refine foam/highlights and add restrained mist; test combat-effect transparency. |

Use [material contract r1](WP02_MaterialContract.md) for exact shader properties, channel encodings, exports and rendering dependencies. Keep the shared layout and interfaces stable while the four lanes build their own assets. The reference-matched WP01 r2 remains the spatial source; the material lab does not replace its complete landscape layout.

## Files and provenance

- Editable meshes and repeatable authoring source: [ArtSource/TeyvatGrassland/Shared](../../ArtSource/TeyvatGrassland/Shared/), including `TG_MaterialSamples.blend`, its generator, FBX and manifest.
- Unity sample meshes/materials/shaders/runtime/TerrainData: [Shared](../../Assets/Project/Art/Environment/TeyvatGrassland/Shared/).
- Actual Unity renders: [Afternoon](../../Artifacts/TeyvatGrassland/WP02/r1/Afternoon/) and [Neutral](../../Artifacts/TeyvatGrassland/WP02/r1/Neutral/), each with 11 planning, attack and close material views at 1600 × 900 without UI.
- Wind/water time comparisons: [Motion](../../Artifacts/TeyvatGrassland/WP02/r1/Motion/). GPU diagnostic renders are in [Diagnostics](../../Artifacts/TeyvatGrassland/WP02/r1/Diagnostics/).
- Original painted rock albedo was created using the built-in image-generation tool. [Exact generation prompt and provenance](../../ArtSource/TeyvatGrassland/Shared/RockTexturePrompt.md); [saved PNG](../../Assets/Project/Art/Environment/TeyvatGrassland/Shared/Textures/TG_RockPainted.png). It is used at restrained strength; arbitrary close-range seamless tiling is not claimed.
- Generated asset hashes and the copyback preservation check: [HandoffManifest.json](../../Artifacts/TeyvatGrassland/WP02/r1/HandoffManifest.json).

## Verification and practical limits

The [material report](../../Artifacts/TeyvatGrassland/WP02/r1/MaterialLabReport.json) records the actual Unity version, GPU, rendering context, shader status and checks. Evidence is editor/offscreen rendering on **AMD Radeon(TM) Graphics**; capture-loop time is not gameplay frame time.

| Check | Result / scope |
| --- | --- |
| Blender → FBX → Unity | All six source meshes pass metre/axis bounds, topology, UV0, numerical masks and imported-normal checks; 2,124 source triangles. Range checks do not establish per-loop correspondence. |
| Ground/grass albedo | Actual native terrain and grass-root GPU samples differ by at most **0.00001857** in Linear RGB; maximum error against the correctly filtered CPU field reference is **0.00047113**, below the unchanged 0.003 starting tolerance. Lit appearance is also reviewed in both lighting setups. |
| Water depth | Actual water shader measures **0.200044 / 1.000049 / 3.000066 m** for 0.2 / 1 / 3 m test beds. Constant fallback returns 2 m. The renderer copies opaque depth **AfterOpaques** so transparent water can read it. |
| Wind and persistence | Root displacement is zero at the sampled times. Animated sample vertices remain within recorded bounds. Dedicated expanded Grass/Leaf/Flower mesh assets retain those bounds after serialization. |
| Actual instanced coverage | A separate GPU coverage rig exercises all **4,200** placements through the runtime submission route. See [coverage evidence](../../Artifacts/TeyvatGrassland/WP02/r1/InstancingCoverageValidation.json). Submissions use at most **500** instances each: nine API calls for 4,200 placements. These are not final hardware-draw or performance budgets. |
| Cold reload | A separate Unity process opens the saved scene without building it or reconfiguring its pipeline; [saved-scene report](../../Artifacts/TeyvatGrassland/WP02/r1/Reload/SavedSceneReport.json) records retained references, matrices, cameras, bounds and actual rendering. |

The sample uses realtime Forward lighting, a maximum of four terrain layers, and disables the inherited far-terrain basemap transition. SSAO, TAA/motion vectors, baked GI, water refraction/reflection cameras and transparent combat-effect sorting are not accepted features of this revision. These need explicit implementation and tests if a later lane adopts them.

Remaining appearance work is visible in the captures: broad simple cliffs, uniformly placed grass, simplified canopy geometry, regular waterfall streaks and repeated river highlights. Their production assets belong to WP03–WP06. WP07 integrates those assets into WP01 r2, WP08 finishes atmosphere/character lighting, and WP09 establishes real combat/player-build performance on named PC hardware. WP02 does not claim final reference parity or a standalone performance pass.

## Rebuild

Run from `E:/Program Files/Unity/RogueliteTurnbasedProject` in PowerShell:

```powershell
& 'Tests/Environment/RunTeyvatMaterialLab.ps1'
```

The launcher starts a hidden Unity batch process and prints its evidence path. Wait for the process to finish; `Artifacts/TeyvatGrassland/WP02/r1/COMPLETE.txt` means its checks passed. `FAILED.txt` and the JSON reports describe a failed run. It uses installed package-cache dependencies, so the main project must have completed package resolution. No Tripo/Meshy connection is needed for this sample.

After a completed build, verify the saved scene in a new process:

```powershell
& 'Tests/Environment/RunTeyvatMaterialLab.ps1' -VerifySaved
```

Check `r1/Reload/COMPLETE.txt` and `SavedSceneReport.json`. Do not run either command while that isolated project is open in another Unity process. Rebuilding replaces generated lab assets and captures; make art revisions in the owning source folders and version the handoff first.

The `.utmp` workspace is disposable. The main Shared assets, editable sources, launcher and editor builder/diagnostics reproduce it. The existing generated asset copy and package are delivered snapshots; after later source revisions, rebuild, validate, and export a new snapshot rather than assuming they update automatically.

Grass matrices, color bounds and draw bounds are saved in **world coordinates** around the WP01 origin. Moving only the lab root does not relocate them. Recompute those values when deliberately relocating the sample; the current layout rotation is identity.
