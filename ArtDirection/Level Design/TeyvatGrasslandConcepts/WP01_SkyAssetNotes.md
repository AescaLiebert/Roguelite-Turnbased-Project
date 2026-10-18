# WP01 reference sky and terrain asset notes

The revised layout uses a project-owned copy of `Assets/Fantasy Skybox FREE/Panoramics/FS003/FS003_Day_Sunless.mat`, saved as `Assets/Project/Art/Environment/TeyvatGrassland/Foundation/Materials/TG_FantasyDaySky.mat` when the builder runs. The installed material and its texture stay unchanged. FS003 was chosen after visually comparing the installed FS002, FS003, FS013, and FS017 day panoramas: its rich blue sky and large white cumulus clouds are closest to the bright grassland references. The sunless version permits the scene's directional light to define the sunlight without a second, contradictory sun disc.

This is a `Skybox/Panoramic` material using the installed 2048 x 1024 texture. Exposure starts at 1 and neutral skybox tint at 0.5; skybox tint is not the same scale as an ordinary material's base color. The texture is a background and does not generate volumetric clouds, animated cloud shadows, or localized light shafts. Sky rotation, sunlight, and fog need checking together in the battle and reverse cameras.

The selected material can be used by the Built-in and URP review variants. The helper configures only the active review scene's sky, ambient colors, and distance fog; it does not change the render pipeline or project color space. Keep the camera clear flags set to Skybox. The sky uses LDR PNG artwork, so exposing it cannot recover HDR sun intensity.

## Installed terrain layers

All are under `Assets/Fantasy Skybox FREE/Scenes/Textures (Terrain)/`:

| Layer | Existing texture / setup | Use in WP01 |
| --- | --- | --- |
| `TerrainGrass.terrainlayer` | `Texture_Grass_Diffuse.png`, matching normal; 5 m tiling, normal scale 0.2 | Painted grass base for the clearing and upper terraces. It is olive/yellow green and already contains blade detail; avoid excessive tiling scale or additional high-frequency noise. |
| `TerrainDirt.terrainlayer` | `Texture_Dirt_Diffuse.png`, matching normal; 5 m tiling, normal scale 1 | Warm, pale ochre arena wear and paths; blend irregularly into grass instead of forming a uniform ring. Reduce normal strength in a project-owned copy if it competes with character readability. |
| `TerrainRock.terrainlayer` | `Texture_Rock_Diffuse.png`, `Texure_Rock_Normal.png`; 10 m tiling, normal scale 1 | Painterly grey/cream rocky slopes. The normal filename intentionally contains the package's `Texure` spelling. Mesh cliffs are still needed for vertical faces and overhangs. |

The terrain textures are 2048 x 2048. Diffuse maps are imported as sRGB; normals are imported as normal maps with sRGB disabled. All three layers have no mask map, metallic 0, and smoothness 0. Duplicate layers into the project-owned foundation directory before changing normal strength or tiling; avoid altering the package assets. TerrainLayer assets do not contain a shader. The Terrain object's material must match the active rendering pipeline; a Built-in terrain shader is not a substitute for URP Terrain/Lit.

`Assets/SkySeries Freebie` also contains HDR cubemap materials such as `FluffballDay.mat`, `CasualDay.mat`, `CloudedSunGlow.mat`, and `HighFantasy.mat`, using `Skybox/Cubemap`. They are available alternatives but are not required for this revision. If reusing their HDR textures with a new skybox material, retain the cube texture shape expected by those materials rather than treating the imported cube as a 2D panorama.
