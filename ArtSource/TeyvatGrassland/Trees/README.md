# WP05 editable tree kit

Open `TG_TreeKit.blend` in Blender 5.1.1. The deterministic generator is [create_tree_kit.py](create_tree_kit.py), seed **510731**. It imports [tree_shapes.py](tree_shapes.py) for branch/crown records and the adjacent WP02 [geometry helper](../Shared/create_material_samples.py). Meshes are authored procedurally in Blender; broadleaf sprays use an original image generated with the built-in image tool. No external model service or downloaded mesh is used.

| Species | Source height, metres | Intended role |
| --- | --- | --- |
| HeroOak | 12 | Broad foreground tree with crooked trunk and buttress roots |
| MeadowOak | 8 | Perimeter and mixed-grove broadleaf |
| IslandTree | 6 | Smaller spreading river-island tree |
| WindPine | 8 | Layered upper/reverse-grove conifer |
| YoungPine | 6 | Smaller conifer variation |
| Shrub | 1.8 | Sparse root-cluster accent |

These are source starting values, not scene placement requirements. The Unity builder scales the five tree species to the R3 registry's target heights and adds smaller shrubs separately. All species have LOD0/LOD1/LOD2, plus one diagnostic axis marker: nineteen imported mesh objects and **121,590 source triangles** total. Counting all six species once each gives LOD0 **85,296**, LOD1 **23,472**, LOD2 **12,738**; the marker adds 84. These are kit totals including alternate representations, not visible-frame triangle counts. All LODs share primary branch/clump records and one scale derived from the actual LOD0 top. Roots sit at zero. The saved blend arranges objects in columns/LOD rows for editing; export temporarily returns their origins to zero, so that authoring grid is not placement data.

`tree_shapes.py` provides crooked forked hero limbs with unequal crown groups and three interior bridge crowns, a rounded upright meadow oak, a leaning island tree, broken-height fuller flattened pine boughs and a soft uneven shrub. The bridge crowns connect the hero's upper canopy while preserving lower branch windows. The module returns records only; edit `create_tree_kit.py` for mesh/card construction. Every LOD uses those same shape records.

Broadleaf LOD0/LOD1 use **no closed canopy-core faces**. Each lobe starts with 60 small folded geometric leaves and **32 curved 3×3-vertex spray cards**. LOD1 retains every fourth small leaf and every second spray. LOD2 removes the small leaves, retains every fourth spray and adds a recessed closed core at **0.45** of the envelope. Branch tube resolution also reduces. Pines use **0.85** geometric bough cores and 150 geometric needle leaves per high-detail lobe without the broadleaf atlas; their LOD2 uses simpler closed bough envelopes. Fine silhouettes and lower-detail transitions still require Unity capture review.

[TG_LeafSprayAlbedoOpacity.png](Textures/TG_LeafSprayAlbedoOpacity.png) contains pale neutral leaves and transparent gaps, intended for green material tint. The generated PNG is copied unchanged into the Unity Trees/Textures folder and packed into the editable blend. [Provenance.json](Textures/Provenance.json) records its exact image-generation prompt, source and usage; regenerate the texture separately from the mesh script if a new artwork version is needed. The Blender generator does not invoke image generation.

Leaf lighting normals use analytical ellipsoid-envelope normals plus a soft upward bias. Spray construction uses the actual outward geometric direction for tangent/card orientation, separately from its upward-biased lighting normals. UV0 and custom split normals are authored before FBX export. Do not recalculate imported normals. Numerical linear RGBA means:

- **R:** zero on wood/core/geometric-leaf attachments, up to one at geometric tips; spray centers have low R and outer edges higher R.
- **G:** continuous `0.55 × (height / speciesHeight)^1.6` sway on wood and foliage.
- **B:** stable phase 0.41.
- **A:** one; spray opacity comes from the texture's alpha, not vertex A.

The shared Unity shader uses the same world displacement function on all slots at response 0.65. Root R0/G0 vertices stay fixed. This approximates continuous whole-tree sway with tip flutter; it does not encode branch pivot/axis animation or bend shading normals. Forward, ShadowCaster and DepthOnly share deformation and the spray alpha test.

Regenerate from `E:/Program Files/Unity/RogueliteTurnbasedProject`:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' --background --factory-startup --python 'ArtSource/TeyvatGrassland/Trees/create_tree_kit.py'
```

The script replaces this folder's blend/FBX/manifest and copies FBX/manifest into [Unity Trees/Models](../../../Assets/Project/Art/Environment/TeyvatGrassland/Trees/Models/). Save manual sculpt revisions before regeneration and retain Unity `.meta` files. Source units are metres, Blender Z up; the proven raw imported Unity basis is `(Blender X, Blender Z, Blender Y)`.

FBX settings use scale 1, unit application/`FBX_SCALE_UNITS`, −Z forward/Y up, baked axis conversion, explicit triangulation before custom normals, no exporter retriangulation/modifiers, and LINEAR vertex colors. Unity uses scale 1, baked axis conversion, Import normals/tangents, no mesh compression and read/write for validation. The manifest records FBX SHA256, expected triangles, actual bounds, UV0, channel ranges and normal ranges. Those are source provenance; successful source generation alone does not validate the imported Unity appearance.

The Unity builder maps Bark/Leaf/LeafLight/LeafSpray/LeafSprayLight by actual imported FBX slot names, selecting cooler geometric needle materials for pines. Do not assume the exporter preserves a fixed submesh slot order. Core/geometric-leaf materials remain opaque and double-sided; spray materials use Cull Off and alpha cutoff **0.60**; bark stays opaque with backface culling. The spray texture imports as sRGB RGBA with input alpha/transparency-aware edges, trilinear mipmaps, preserved mip coverage at **0.60**, Clamp wrap, anisotropy 2, maximum size **1024** and uncompressed review storage. Mip silhouette quality and final compression/overdraw remain review work.

Dedicated animated mesh copies preserve bounds. The builder explicitly copies vertex/normal/color/UV/submesh buffers; `TreeIntegration.json` compares actual raw and persisted animated data through `animatedBuffersMatch` to detect stale native mesh contents. Unity LODGroup thresholds **0.12 / 0.045 / 0.002** are screen-relative starting values with no crossfade. Keep source geometry, shape records, texture and the corresponding material/wind contract together when revising.

Build or review using [RunTeyvatTreesLab.ps1](../../../Tests/Environment/RunTeyvatTreesLab.ps1), in the isolated URP/Linear art host. The current warm and independent cold [TreeValidation/TreeIntegration reports](../../../Artifacts/TeyvatGrassland/WP05/Revision01/) pass: 19 imported meshes, 120 tree anchors, 70 shrubs, seventeen original cameras, 264 path samples and one wind owner. The actual Unity texture is 1024 × 683 with 11 mips; persisted animated buffers match. Root checks concern root origins/sampled vertices, not every buttress tip on uneven ground.

The full [WP05 handoff](../../../ArtDirection/TeyvatGrasslandConcepts/WP05_Trees.md) records reproduction, Editor cost observations and remaining art/runtime work. Final fine-leaf polish, moving combat, lighting and named-minimum-PC player performance remain outstanding; technical acceptance does not claim final Genshin parity.
