# WP04 meadow module

`Scenes/TG_MeadowReview.unity` is the isolated URP17.3 Forward/Linear review on WP03 terrain and geometry. It includes inherited tree/water/light context; those are not WP04 final assets. The main game remains Built-in/Gamma. Installing/importing these assets does not migrate it.

`Prefabs/TG_Meadow.prefab` contains the renderer and `Data/TG_MeadowPlacement.asset`; no cameras, lights, collision or independent wind clock. Supply the one scene-level `TeyvatMaterialGlobals` owner using the accepted WP03 world field/bounds. A null prefab globals reference resolves that same-scene owner. Remove the WP02 owner's optional sample grass instances to avoid double submissions. The renderer safely skips Built-in rendering.

Placement matrices and bounds are baked in WORLD coordinates for the accepted WP01 origin. Moving the prefab transform does not move its data. Regenerate or deliberately transform/revalidate the data when relocating the stage. The eight individual meshes remain reusable. After editing matrices, phases or mesh bounds in place at runtime, call `InvalidatePlacementCache()`; changing data/species/cell-array references invalidates automatically.

User direction on 2026-10-18 permits grass on the battlefield, including character positions. The original WP01 mask asset is retained; its green region now selects clustered short growth rather than bare ground. `Data/TG_MeadowDensityPolicy.json` records this override. Tall accents/flowers stay outside. `Terrain/TG_MeadowGround.asset` is a WP04 clone with softer soil/grass control weights in the clearing and exactly unchanged WP01 heights; the accepted WP03 asset is preserved.

`Runtime/TeyvatMeadowRenderer.cs` submits multiple species/material submeshes in pooled groups of at most500. Twelve-metre placement cells and full transformed bounds are saved; runtime caches placements/bounds and applies camera/layer filtering, frustum culling and a conservative3m margin for nearby shadow casters. It does not provide occlusion culling or gameplay collision.

Starting distance treatment: stable per-instance LOD thresholds spread across30–50m, smooth root-preserving geometry shrink70–105m, skip when scale≤.01. `Shaders/TG_MeadowFoliage.shader` preserves the WP02 property/mask/color/wind interface and shares TG_Common; it additionally fades wind by the same distance factor using `_TG_MeadowViewPosition`. Forward, ShadowCaster and DepthOnly share the one deformation function. Keep shader70/105 constants paired with runtime values. No Shared shader was changed. No DepthNormals/motion-vector pass, SSAO/TAA or actor-bending implementation is provided.

Materials use geometry silhouettes, double-sided shading, instancing and the shared world root-color field. Mesh assets store conservative expanded animated bounds. Wind response is .65 on every slot, including flower stems/petals. Camera, density, color and distance values are review starting values; adjust against captures and the intended PC build.

From the repository, close the isolated art host and run `Tests/Environment/RunTeyvatMeadowLab.ps1`. It copies the owned assets plus accepted dependencies to `.utmp/TeyvatWorldLab`, reuses that isolated cache, builds this distinct scene and writes WP04 evidence. `-VerifySaved` opens the saved result in a new process without rebuilding/copying/configuring rendering. Main Battle, WP01/WP03 and project rendering settings are not written by the launcher.

Full handoff: `ArtDirection/TeyvatGrasslandConcepts/WP04_Meadow.md`. Editable source: `ArtSource/TeyvatGrassland/Meadow/`. Validation/evidence: `Artifacts/TeyvatGrassland/WP04/r1/`. Final integrated combat, trees, water, lighting and named-minimum-PC player performance remain subsequent work packages.
