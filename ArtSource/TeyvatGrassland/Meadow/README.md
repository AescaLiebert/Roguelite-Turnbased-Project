# WP04 editable meadow kit

The eight species are ShortGrass, MeadowGrass, TallGrass, AccentGrass, Clover, DaisyWhite, FlowerGold and FlowerPurple. Each has LOD0/LOD1; the asymmetric axis marker is diagnostic only. The source contains 1,974 triangles including both LODs and the 84-triangle marker (LOD0 species total 1,462; LOD1 total 428). These counts describe the reusable kit, not the instanced scene.

Open `TG_MeadowKit.blend` in Blender 5.1.1. The objects are arranged on an authoring grid. Export temporarily restores all origins/transforms so Unity extracts raw meshes at ground contact; preview object locations are not placement data. Sources use metres and Blender Z up. The tested raw imported Unity basis is `(Blender X, Blender Z, Blender Y)`.

Regenerate from this repository with:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' --background --factory-startup --python 'ArtSource/TeyvatGrassland/Meadow/create_meadow_kit.py'
```

The generator imports the adjacent WP02 `../Shared/create_material_samples.py` Geometry helper, saves this folder's blend/FBX/manifest, and copies FBX/manifest to `Assets/Project/Art/Environment/TeyvatGrassland/Meadow/Models/`. Preserve `.meta` files when revising. It requires Blender's bundled FBX exporter; no external model service or downloaded vegetation is used.

UV0 and upward-biased custom normals are authored before export. Triangulation precedes custom normals; FBX scale1, FBX_SCALE_UNITS, -Z forward/Y up, bake-space-transform, LINEAR vertex colors, no exporter retriangulation/modifiers. Unity import uses scale1, baked axis conversion, Import normals, no compression and read/write for checks. RGBA is numerical R bend0root..1tip, G0, B stable per-element phase, A1 AO. Opacity is not vertex alpha.

Grass strips, clover leaves and flower petals are actual tapered geometry with white albedo, not alpha cards. No generated foliage bitmap or alpha mip/compression dependency exists. Material slots Grass/White/Gold/Purple map to lane material instances of the WP04 variant; every slot uses wind response .65 so petal/stem joins stay together. Grass LOD1 retains exact selected LOD0 blade roots/tips/phases; endpoint-subset assertions run in the generator. Flowers/clover have simpler authored distant representations.

The source manifest records the source/export hashes, bounds, triangle counts, channel ranges, normals and LOD strategy. It describes source generation; actual Unity acceptance is recorded in `Artifacts/TeyvatGrassland/WP04/r1`, including its cold Reload subfolder. See `ArtDirection/TeyvatGrasslandConcepts/WP04_Meadow.md` for the implemented distribution, user-directed short grass at fighter positions, validation and remaining work.
