# WP02 native terrain surface

`Teyvat/URP/TerrainGrassland` is a narrowly modified copy of URP 17.3.0 Terrain/Lit, taken from the project's installed `com.unity.render-pipelines.universal@d10049dfa479` package. Unity Technologies owns the original source; the accompanying `LICENSE.md` reproduces its Unity Companion License notice. Package files remain unmodified.

The shader keeps Unity's terrain property names, control-map binding, terrain patch instancing, native normals, hole clipping, shadow casting, depth, depth normals and selection. The local pass file adds world position to `SplatmapMix`. It multiplies **layer 0's diffuse RGB** by `TGColorField(positionWS)` before native layer blending. **Layer 2 is rock**: geometric world normals select triplanar projections and `TGStyleRock` applies the same global tint, paint strength and world scale as mesh cliffs. Layers 1 and 3 keep ordinary diffuse sampling. No final lit color, shadow or fog is multiplied by the field.

The accepted **Forward path** uses the common stylized `TGShade` lighting with grass wrap 0.4 and transmission 0.22. The inherited deferred/GBuffer path still uses PBR and is unsupported for this contract. Use the material lab's Forward renderer; enabling a different rendering path requires new comparisons.

The common functions come from `../TG_Common.hlsl`. The scene's `TeyvatMaterialGlobals` component owns the color texture/bounds and `_TG_RockStyle` / `_TG_RockWorldScale`. It explicitly converts rock tint to Linear RGB. The field texture imports as sRGB; hardware converts texels to Linear before filtering. Use white layer-0 diffuse/remap for exact root-albedo parity. Additional detail or tint deliberately changes that equality.

## Lab constraints

- Use at most four TerrainLayers. The WP02 terrain uses three. Add-pass terrain layers are inherited Unity shaders and do not sample the project's shared field.
- Set every lab terrain's `basemapDistance` to `100000`, above the review scene's far clip distance. This deliberately retains native splat rendering throughout the review. The inherited far basemap shaders do not contain the world field. Production basemap support needs a separately reviewed generator before reducing this distance.
- The review uses realtime lighting. The inherited Meta baking path does not sample the world color field, so this package does **not** claim field-correct baked GI or terrain basemaps.
- Holes are not used by this scene. Native URP hole clipping is retained in the relevant passes.
- Keep the installed URP 17.3 package while using these forked passes. When upgrading URP, compare this fork with its new TerrainLit source, then repeat native TerrainData, shadow, depth and color-field checks.

## Verification

Confirm the Unity shader import reports no errors, the terrain has its own TerrainData and a TerrainCollider, terrain instancing is enabled, the layer count stays at three, and the basemap distance is above the camera far clip. A color-field on/off comparison should alter grass albedo while exposed soil and rock retain their authored colors. Check the ground/grass color transition under a common shadow as well as sunlight; a post-lighting tint cannot pass this comparison reliably.
