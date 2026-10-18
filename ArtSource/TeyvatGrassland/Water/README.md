# WP06 editable water source

Open `TG_WaterKit.blend` in Blender5.1.1. [create_water_kit.py](create_water_kit.py), seed606731, creates deterministic surfaces using the adjacent [Shared geometry helper](../Shared/create_material_samples.py). [TG_WaterPlan.json](TG_WaterPlan.json) is a frozen copy of accepted R3 layout data. The generator also reads canonical R3 height samples to trace the real shoreline; it does not alter that heightfield.

The kit has41 objects /45,034 source triangles including the asymmetric axis marker. All surfaces are in full arena-local metres, with pivots at the arena origin. Unity applies the accepted arena translation once. This is a composed water module, not a collection of zero-centered generic waterfalls. The blend retains individually editable river, cascades, pools and effect meshes. Blender preview materials do not reproduce the Unity production shader.

The river uses a192×32-cell grid along the unchanged bank curves, at local Y−1.35. Three cascade sheets use100×24 cells each, following six-point R3 courses. V is normalized cumulative physical arc distance, increasing downstream; U0..1 crosses the width. Elliptic catch pools, radial impact/ripple patches and sparse island wakes use radial UV0..1. Crossed mist quads use UV0..1. The marching-square shoreline strip uses world XZ-scaled UVs; its shader selects the shoreline mode rather than interpreting them as radial patches.

Effects encode stable phase in vertex R and opacity in A; G/B are zero. This is not the foliage-wind schema. The surfaces are rigid and shaders animate patterns from the scene's existing common `_TG_Wind.w` clock. The production shader is [Water/Shaders](../../../Assets/Project/Art/Environment/TeyvatGrassland/Water/Shaders/); it uses deterministic procedural noise, no raster texture or image-generation dependency.

Export uses metric scale1,−Z forward/Y up, baked axis conversion, LINEAR vertex colors and explicit triangles before split normals. Do not enable exporter retriangulation or recalculate imported normals. The validated raw Unity basis is `(BlenderX, BlenderZ, BlenderY)`. Unity extracts Mesh subassets without applying FBX prefab-node rotations. Normals and UVs are authored before export; Water-owned meshes have initial read/write access for validation.

Regenerate from the repository root:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' --background --factory-startup --python 'ArtSource/TeyvatGrassland/Water/create_water_kit.py'
```

The generator writes its blend/FBX/manifest, copies FBX/manifest into Unity Water/Models and freezes the plan into Water/Data and this source directory. The manifest stores generator/plan/FBX hashes, bounds, topology counts, UV0 and normal ranges. Successful source generation alone is not a Unity appearance pass. Save manual edits before regenerating; `.meta` files remain with existing Unity assets.

Use [RunTeyvatWaterLab.ps1](../../../Tests/Environment/RunTeyvatWaterLab.ps1) for representative sample, full assembly and independent cold verification. [The WP06 handoff](../../../ArtDirection/TeyvatGrasslandConcepts/WP06_Water.md) records production properties, depth-copy scheduling, fallback, sorting fixtures, evidence and limits. The full module has40 renderers; `TG_WaterFallback.prefab` retains10 water surfaces with constant depth and reduced procedural detail. Final combat integration, cliff-contact dressing and player performance remain required.
