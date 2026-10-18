# WP03 world source

The approved battle concept and [four-angle layout](../../../ArtDirection/TeyvatGrasslandConcepts/04-terrain-layout-multiview.png) define composition. The earlier real-game screenshots define shader/texture language only. Preserve the open combat meadow, river and three stepped falls, left tree area, right arch, hilltop cathedral and distant aqueduct. Do not reinterpret the surface references as a new island/city layout.

`TG_WP01TerrainSource.json` is an export of all 513 × 513 actual height samples from the repo's WP01 r2 TerrainData, with its local position and dimensions. The generator bilinearly samples that source for cap and landmark contacts. It does not substitute a Python approximation of Unity's Perlin noise. The unchanged native TerrainData remains the Unity ground; the Blender source contains a non-exported reference surface.

`create_world_geometry.py` generates an editable `.blend`, explicit `.fbx`, and a machine-readable `.manifest.json`. It imports the WP02 geometry/export helper, so retain `../Shared/create_material_samples.py`. All runtime mesh coordinates are baked in the accepted layout-local frame, in metres, with identity object transforms. The source swaps Unity `(x,y,z)` to Blender `(x,z,y)` and establishes outward closed-volume face orientation before triangulation, UVs and custom split normals. Unity's raw imported mesh conversion is independently checked with an asymmetric axis marker.

Run from the repository root:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' --background --factory-startup --python 'ArtSource/TeyvatGrassland/World/create_world_geometry.py'
```

This overwrites the generated `.blend`, `.fbx` and manifest. Save manual sculpt changes separately or incorporate them into a versioned generator before rebuilding. The explicit FBX is copied to the World/Models folder; `.blend` files stay outside Unity Assets.

Names `SM_*_LOD0` / `_LOD1` are paired representations, `COL_*` are simplified static collision meshes, and `SM_WorldAxisMarker` is diagnostic only. Native TerrainCollider provides continuous ground collision. Source caps/backs/ends/bottoms form closed volumes; separate masonry stones deliberately overlap and are not a Boolean union. The manifest records welded boundary counts and import statistics. LOD screen thresholds and collision simplification are starting representations, requiring camera and runtime validation rather than an assumed universal budget.

The reusable Unity world prefab contains geometry, collision and the cloned native ground only. Water uses WP02 review context, and trees use clearly named WP01 silhouettes in the review scene. Full meadow/tree/water art belongs to WP04–WP06; final lighting and gameplay integration belong to subsequent packages.
