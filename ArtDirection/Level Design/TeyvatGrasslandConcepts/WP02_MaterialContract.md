# WP02 shared material contract — accepted sample r1

Recorded 2026-10-18. **The r1 shared material sample and interfaces are accepted for the downstream WP03–WP06 asset lanes.** The final isolated batch passes imports, wind/bounds, GPU water-depth and GPU ground/grass-color diagnostics; all four shaders are supported with no reported shader errors. A separate cold-process saved-scene check also passes. This accepts the documented URP/Linear sample and its constraints; final map quality, exact reference parity, actual combat integration and standalone player performance remain production work.

The selected rendering target is Unity 6000.3.4f1, URP 17.3.0, Forward rendering and Linear color space, for Windows PC. The isolated material lab is `.utmp/TeyvatMaterialLab`, with its scene at `Assets/Project/Art/Environment/TeyvatGrassland/Shared/Scenes/TG_MaterialLab.unity` and evidence at `Artifacts/TeyvatGrassland/WP02/r1`. The main game's Built-in/Gamma renderer is preserved. These URP shader families are not drop-in replacements for that current renderer.

The new user screenshots refine the art direction: warm luminous grass, deep green foliage and quiet coarse blue-grey stone, with broad designed cliff planes and restrained vertical painted strata. Keep the rock texture secondary to the authored geometry. Neutral-light captures must reveal defects before afternoon lighting is tuned.

## Ownership and source locations

Shared shaders, textures, materials, the sample scene and the runtime controller live under `Assets/Project/Art/Environment/TeyvatGrassland/Shared/`. WP02 owns their property identifiers and data conventions. WP03–WP06 produce compatible mesh/texture/material instances; the integrator owns master layout, lighting and scene-level configuration. Contract changes need a versioned handoff to affected lanes.

The shared source is `Shaders/TG_Common.hlsl`, `Shaders/TG_Surface.shader`, `Shaders/TG_Foliage.shader`, `Shaders/TG_Water.shader`, `Shaders/Terrain/TG_TerrainGrassland.shader`, `Shaders/Terrain/TG_TerrainLitPasses.hlsl`, and `Runtime/TeyvatMaterialGlobals.cs`. The runtime component's actual type is `FightingAllstar.EnvironmentArt.TeyvatMaterialGlobals`; keep the `EnvironmentArt` namespace rather than introducing an `Environment` namespace that would shadow `System.Environment`. Terrain source is derived from the installed Unity URP 17.3.0 terrain implementation; retain its attribution/license material when distributing the source.

## Coordinates, texture imports and mesh data

One Unity unit represents one metre. Asset pivots sit at their documented contact base. Export an explicit model file; keep `.blend` and other editable art sources outside Unity's imported Assets directory. The current Blender 5.1.1 sample FBX contains six meshes and 2,124 source triangles. [Main-project import evidence](../../Artifacts/TeyvatGrassland/WP02/r1/MainImportCheck/ImportValidation.json) reports all six passing, matching the source FBX hash, metre bounds, expected triangles/material slots, UV0, numerical RGBA ranges and custom-normal range checks. This is a specific sample result, not a general guarantee for future asset exports.

The actual export preset uses scale 1, `apply_unit_scale=True`, `apply_scale_options='FBX_SCALE_UNITS'`, `axis_forward='-Z'`, `axis_up='Y'`, `bake_space_transform=True` and `colors_type='LINEAR'`. Triangulate explicitly **before assigning custom split normals**. Export with `use_triangles=False` and `use_mesh_modifiers=False` to preserve those final normals instead of retriangulating through the exporter. Export only intended mesh objects. The `.blend`, generator, FBX and source manifest live under `ArtSource/TeyvatGrassland/Shared/`; Unity imports the copied FBX and sidecar under `Shared/Models/`.

The asymmetric axis marker establishes one consistent source-basis mapping for all six imported meshes: Unity raw mesh X sign +1, Y sign +1 and Z sign −1 relative to the manifest's FBX −Z-forward/Y-up basis. Retain that demonstrated mapping in diagnostics rather than manually flipping individual assets. The import uses globalScale 1, baked axis conversion, Import normals and initial read/write access. The custom-normal export correction is validated in the main-project diagnostic. Range/topology checks still do not prove per-loop correspondence or final shading; visually inspect the raised X/Z axis labels and canopy lighting in the isolated sample.

Import authored custom normals rather than recalculating them. Preserve UV0, material slots and vertex color channels. UV0 carries surface albedo/opacity and directional water coordinates. UV1 is reserved for an eventual lightmap workflow; this realtime lab does not validate baked lighting. Color values used as bend/phase/AO data are numerical linear data, not sRGB color. The active shaders do not require tangent-space normal maps.

| Vertex channel | Meaning and required source values | Verification |
| --- | --- | --- |
| R | Grass/flower bend weight; leaf flutter/tint weight. Roots use 0, tips approach 1. | Wind leaves rooted vertices stationary when G is also 0; grass color transition follows R. |
| G | Branch-sway weight. Grass roots should also use 0. | Trunk/branch joins stay connected; a root with R0 but G>0 can still move. |
| B | Stable phase offset, mapped to one full phase cycle. | Repeated instances have stable variation; no frame-to-frame reseeding. |
| A | Ambient-occlusion multiplier applied to the foliage's shaded color. Ordinary unoccluded vertices must use 1. | White vertex alpha does not darken the asset; alpha 0 makes its shaded color black. A is not opacity. |

The current common deformation accepts a family argument but uses the same equation for every family. Per-asset RGB masks and `_WindResponse` determine motion. No encoded per-branch pivot/axis, trunk bending or skeletal foliage animation is provided by this revision. Current normals are transformed from the imported mesh and are not reoriented by the wind deformation; test this approximation at the intended amplitude.

Color/albedo textures, including the world color field and painted rock, are imported as sRGB color. Mask/weight textures use linear data interpretation. Alpha opacity is sampled from the base map alpha and multiplied by material base alpha. Normal maps, if later added, require a declared shader interface and Unity Normal Map import. Do not repurpose a channel or add a packed map silently.

## Scene globals: one owner per loaded lab scene

`TeyvatMaterialGlobals` publishes the following actual global identifiers. Do not create independent wind clocks in individual material lanes.

| Global | Actual layout / behavior |
| --- | --- |
| `_TG_ColorField` | Shared color texture, interpreted as sRGB and sampled by the shader in Linear space. Sampling uses mip level 0. |
| `_TG_ColorBounds` | `(minWorldX, minWorldZ, inverseWorldSpanX, inverseWorldSpanZ)`. UV = saturate((worldXZ - minimum) × inverse span). |
| `_TG_ColorFieldEnabled` | 1 when the controller has a color texture, otherwise 0. Disabled sampling returns white. |
| `_TG_Wind` | `(normalizedWorldDirectionX, normalizedWorldDirectionZ, strength, timeSeconds)`. The first two components are a world-X/Z direction, despite occupying vector X/Y. |
| `_TG_DebugAlbedo` | Diagnostic global: values greater than 0.5 return the actual terrain/opaque/foliage albedo without ordinary lighting/fog. Restore its previous value after each test; normal rendering uses 0. |
| `_TG_RockStyle` | `(linearRockTintR, linearRockTintG, linearRockTintB, paintStrength)`. The runtime explicitly converts the authored `rockTint` using `rockTint.linear` before publishing it. |
| `_TG_RockWorldScale` | Shared world-coordinate texture multiplier used by terrain rock layer 2 and mesh rock with shared style enabled. |

The color bounds are world-axis-aligned; there is no layout-rotation matrix in this implementation. For a translated or rotated arena, supply a field/bounds arrangement appropriate to the world placement or revise this contract before reusing layout-local data. Current accepted R2 layout rotation is identity. The color field clamps at its bounds, so inspect the outer edges rather than assuming automatic periodic extension.

The controller runs in Edit and Play modes. `fixedTime`/`fixedSeconds` hold the common clock for paired captures; otherwise the clock uses `Time.realtimeSinceStartup`. Water animation currently uses the same `_TG_Wind.w` time. Turning foliage strength to zero does not stop water; use fixed time to freeze both. Globals remain shader-global state, so one controller must own them; additive scenes with competing controllers are outside the accepted sample scope.

WP03 integration correction: the same-scene camera callback publishes these globals **before** checking whether instanced grass is assigned. Ground, mesh-rock and water therefore retain their shared inputs in a world-only scene before WP04 supplies vegetation. The property schema and grass submission route remain unchanged. Explicit world-review captures and a cold reload check this use case.

Controller source defaults are `colorBounds=(-28,-18,1/56,1/66)`, wind direction `(0.93,0.36)`, strength `0.4`, and grass bounds centered at `(0,1,6)` with size `(54,7,52)`. Shared rock defaults are authored sRGB tint `(0.64,0.72,0.76)`, paint strength `0.35` and world scale `0.11`. The rock style formula is `lerp(white, sampledPaint, saturate(paintStrength)) * linearTint`; it intentionally suppresses texture contrast around a controlled base color. These are starting sample values. Verify field/rock alignment with diagnostic patches and mesh/terrain transitions, and roots/tips and displaced bounds at maximum wind; enlarge/reposition bounds and retune values when visible tests reveal mismatch, noisy planes or clipping.

## Surface family: `Teyvat/URP/Surface`

Ground mesh, rock, trunk and simple opaque world surfaces share this material family. Forward, ShadowCaster and DepthOnly passes exist. It uses authored normals, the main directional light, spherical-harmonic ambient, stylized diffuse response, received main-light shadows and fog. It does not implement normal maps, local-light accumulation, baked-lightmap response, gloss controls or a Meta pass.

| Property | Actual meaning | Starting default |
| --- | --- | --- |
| `_BaseMap` | Painted RGB albedo; alpha is unused | white |
| `_BaseColor` | Multiplicative RGB tint | white |
| `_UseColorField` | Multiply albedo by shared world color | 0 |
| `_MossColor` | Upward grass/moss RGB tint | (0.4,0.61,0.18) |
| `_MossAmount` | Upward tint blend weight | 0 |
| `_MossSlope` | Beginning of normal-Y blend transition | 0.65 |
| `_WorldScale` | World-coordinate texture multiplier for triplanar mapping | 0.14 |
| `_Mapping` | 0 = UV0; 1 = world triplanar | 0 |
| `_Wrap` | Diffuse wrap response | 0.08 |
| `_TextureStrength` | Blend ordinary sampled paint toward white before `_BaseColor` tint | 1 |
| `_UseSharedRockStyle` | Blend ordinary paint/tint to global rock style; for triplanar mapping also blend local scale to global rock scale | 0 |

For exact ground-field albedo, use white `_BaseMap`, white `_BaseColor` and `_UseColorField=1`. Grass roots at full field weight then have the same unlit color. Other ground texture/tint multiplication deliberately changes that equality. The surface shader's moss color also multiplies the field. Tune albedo under neutral light first; tune wrap separately to inspect lit continuity.

With `_UseSharedRockStyle=1`, `_TG_RockStyle` supplies the base tint and paint strength in place of `_BaseColor` and `_TextureStrength`; `_TG_RockWorldScale` supplies the world scale for triplanar mapping. Keep `_Mapping=1` on mesh rocks intended to align with the terrain's world-projected rock pattern. The common global must be active during previews, otherwise the shared-style material cannot infer a useful fallback tint. Partial style weights blend both appearance routes and their world-scale selection.

### Native TerrainData route

`Teyvat/URP/TerrainGrassland` retains the terrain-engine layer, normal, smoothness, metalness, hole and control-map interfaces from URP Terrain/Lit. Terrain layer index **0 is grass** and its sampled RGB is multiplied by the same world color field before native splat blending and lighting. Layer **2 is rock**: geometric world normals choose triplanar projections of that layer's texture, with `_TG_RockWorldScale` and `TGStyleRock` matching the mesh-rock style. Layer 1 remains dirt; the sample's layer 3 is available only under the same documented interface. The sample contract permits **at most four terrain layers**. Reordering grass or rock silently breaks these conventions.

The lab keeps `basemapDistance=100000` to avoid switching into the unmodified far-basemap shading path. This large distance is a realtime sample workaround, not a final production budget. Validate the visible full lab bounds and all review distances; a future far-terrain solution must preserve the field or explicitly blend to an accepted representation. Do not enable extra terrain layers or a basemap transition and assume parity.

Use a neutral/white grass layer when proving exact albedo matching. The terrain's **Forward path now calls the same `TGShade` lighting function** as foliage, with fixed wrap 0.4 and transmission 0.22 to match the lab grass instances. Native layer blending and interpolated/per-pixel terrain normals remain in use. The inherited GBuffer/deferred path remains PBR and is **unsupported by this material contract**; use the selected Forward renderer. No baked terrain/foliage lighting is accepted in this lab. Record neutral/afternoon, sun/shade and attack-distance comparisons even with shared albedo and lighting, because geometry normals, masks and real coverage can still reveal boundaries.

## Foliage family: `Teyvat/URP/Foliage`

Opaque, alpha-tested rendering uses `ZWrite On`, material-selected culling and alpha-to-coverage. Forward, ShadowCaster and DepthOnly use the same vertex deformation and texture-alpha clip function. Instance IDs are propagated through all passes. Backface normals are flipped intentionally, then their world Y is made positive and the normal renormalized to retain an upward blade/canopy bias on both sides. A custom DepthNormals pass is absent: keep SSAO disabled in the sample; enabling SSAO or another depth-normal consumer requires a matching deformed/clipped pass and new tests. TAA and motion-vector effects remain off; 2×/4× MSAA are sample quality comparisons, not accepted final settings.

| Property | Actual meaning | Starting default |
| --- | --- | --- |
| `_BaseMap` | RGB albedo and alpha opacity | white |
| `_BaseColor` | RGB tint and opacity-alpha multiplier | (0.62,0.82,0.35,1) |
| `_RootColor` | Lower grass/leaf RGB tint | white |
| `_TipColor` | Upper grass/leaf RGB tint | white |
| `_CanopyShadeColor` | Leaf-family side tint, fading toward white for upward/downward authored normals | (0.55,0.80,0.75,1) |
| `_UseColorField` | Grass root blend to world field | 1 |
| `_WindResponse` | Material multiplier for common displacement | 1 |
| `_Wrap` | Diffuse wrap | 0.35 |
| `_Transmission` | Controlled view-dependent backlighting | 0.25 |
| `_Cutoff` | Texture alpha × base alpha clip threshold | 0.45 |
| `_AlphaClip` | Clip enabled when greater than 0.5 | 1 |
| `_Cull` | Unity CullMode: 0 off, 1 front, 2 back | 0 |
| `_Family` | 0 grass, 1 leaf, 2 flower | 0 |

Grass interpolates from the root color to base-map × base-color × tip-tint using vertex R. At `_UseColorField=1` and R0, unlit root color is exactly the shared field; it samples the undeformed position to prevent color swimming. Leaf color is base-map × base-color × lerp(root-tint,tip-tint,R), multiplied by `lerp(_CanopyShadeColor, white, smoothstep(0.2,0.88,abs(authoredWorldNormalY)))` to cool the canopy sides. Flowers keep base-map × base-color petal hue. Vertex A multiplies the ordinary lit result for all three families; debug-albedo mode bypasses that AO multiplication and fog. A is not additional texture opacity.

`_Wrap`, `_Transmission`, `_WindResponse`, tint and clip defaults are starting values. Compare neutral/afternoon front and back views, fixed-time wind on/off, alpha mip edges and cast shadows. Reduce transmission if leaves become luminous silhouettes; change clipping/mips/coverage when edges shimmer or disappear; adjust masks/amplitude before enlarging a model to hide root motion.

### Selected grass submission route

The runtime uses explicit `Graphics.RenderMeshInstanced` calls from `RenderPipelineManager.beginCameraRendering`, scoped to cameras in the controller's scene. Supply one reusable `grassMesh`, one instancing-enabled `grassMaterial` and saved `Matrix4x4[] grassInstances`. The code divides submissions into groups of **500 maximum** and exposes `submittedInstances`, `submittedBatches` and `submissionFrames` for diagnostics. The selected mesh's animated local bounds and the controller's `grassBounds` must cover maximum deformation.

Persist deformation bounds in dedicated serialized Mesh assets rather than relying on `Renderer.localBounds` overrides, which do not survive the saved-scene handoff. The sample stores `TG_GrassClump_AnimatedBounds`, `TG_LeafCluster_AnimatedBounds` and `TG_FlowerCluster_AnimatedBounds` under `Shared/Meshes/`. Their bounds are expanded from the imported source meshes; the originals retain their source data. Current starting size expansions are `(1.2,0.3,1.2)` for grass and `(1.2,0.35,1.2)` for leaf/flower. These expand the overall bounds size, not each side by that full amount. The cold-process saved-scene check confirms wind/bounds validation still passes after reopening without regeneration.

This is the chosen sample route, not a claim that thousands of separate GameObjects batch automatically. The chosen 500-instance ceiling stays below the [documented Unity 6.3 default limit of 511](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Graphics.RenderMeshInstanced.html) with both transform matrices, and matches the installed shader fallback array size. No `assumeuniformscaling` restriction is imposed on future placement matrices. A dedicated GPU coverage microtest checks all 4,200 grid placements through this actual runtime callback, including submission boundaries; see `InstancingCoverageValidation.json`. Runtime timing and full-scene culling remain separate checks. `submittedBatches` counts API submissions, not shadow passes or guaranteed hardware draw counts. There is no per-instance light-probe/lightmap contract, density-streaming system or completed LOD implementation yet.

## River and waterfall: `Teyvat/URP/Water`

The shared water material uses transparent alpha blending, `ZWrite Off`, `Cull Off`, animated procedural color/foam patterns and distance fog. It samples opaque scene depth for approximate eye-space thickness. There is no refraction, opaque-texture sampling, reflection probe lookup, planar-reflection camera, screen-space reflection, normal-map interface or physical refraction model in this revision. Its restrained sparkle is a procedural pattern, not an actual reflected light source. The opaque texture stays disabled; scene depth is required for the depth mode. The selected `UniversalRendererData.copyDepthMode` must be **`AfterOpaques`**, with depth requested by the pipeline/camera: copying depth only after transparents leaves the required opaque depth unavailable when this water pass samples it. Keep that scheduling dependency with the material handoff.

| Property | Actual meaning | Starting default |
| --- | --- | --- |
| `_ShallowColor` | Shallow RGBA | (0.15,0.66,0.63,0.72) |
| `_DeepColor` | Deep RGBA | (0.045,0.27,0.43,0.9) |
| `_FoamColor` | Procedural foam RGBA | (0.85,0.96,0.91,1) |
| `_DepthRange` | Eye-space thickness for deep-color blend, metres | 4 |
| `_FoamDistance` | Eye-space thickness for bank-contact foam, metres | 0.35 |
| `_FlowSpeed` | Pattern UV displacement per second | 0.24 |
| `_FlowDirection` | Pattern displacement direction in UV XY | (0,1,0,0) |
| `_FlowStrength` | River ripple color variation amount | 0.15 |
| `_UseSceneDepth` | Blend between depth fallback and sampled thickness | 1 |
| `_FallbackDepth` | Constant eye-space thickness fallback, metres | 2 |
| `_Mode` | 0 river, 1 waterfall | 0 |
| `_Opacity` | Overall alpha multiplier | 1 |
| `_DebugDepth` | Hidden diagnostic: output clamped thickness / `_DepthRange` as opaque grayscale, before ordinary shading | 0 |

Sampled thickness is `max(0, LinearEyeDepth(sceneDepth)-surfaceEyeDepth)`. It is view-ray distance, not vertical water depth, so grazing and elevated camera tests are required. The river needs real underlying opaque basin geometry. Waterfall mode replaces bank foam with animated streak/impact patterns; the impact band uses UV V near 1. Mesh UV direction must agree: the current WP01 curtain V increases downward, while its crest apron V increases toward the source. Assign flow directions deliberately rather than applying one unexamined default to both.

Fallback mode uses a constant depth; it does not preserve real shoreline contact. Transparent waterfalls, mist and combat effects are absent from opaque depth and need sorting tests. The current material does not cast a physical water shadow. Tune opacity/foam to prevent a flat white curtain, then judge movement through timed captures or playback rather than a single still.

The depth microtest enables `_DebugDepth` only on temporary material clones, renders known opaque beds through the actual water shader into a Linear floating-point target, and compares the readback with expected fractions. Ground/grass albedo parity uses the separate `_TG_DebugAlbedo` global and actual native terrain/grass shaders. Both GPU microtests pass in the final isolated batch. Diagnostic modes are not production appearance settings.

## Painted-rock texture provenance

`Shared/Textures/TG_RockPainted.png` is an original 1,254 × 1,254 image generated with the built-in image tool using the two supplied reference screenshots as verbal palette/strata guidance. The project PNG is an exact copy of the generated output, without image editing or resize, imported as sRGB with Repeat wrapping. [Exact prompt and provenance](../../ArtSource/TeyvatGrassland/Shared/RockTexturePrompt.md) are stored under `ArtSource/TeyvatGrassland/Shared/`. Use it as albedo, not AO/normal/roughness. A numerical pixel observation found mean opposite-edge RGB differences of 0.01367 left/right and 0.01322 top/bottom, versus an interior adjacent-pixel mean of 0.00853, on a normalized 0–1 channel scale. These statistics are observations, not seam approval: repeated-tile and neutral-light camera review must judge whether the mismatch and painted fractures are visible at the selected world scale. A generated prompt asking for seamless edges does not establish that the delivered tile is seamless.

## Accepted r1 evidence

| Check | Observed result | Evidence |
| --- | --- | --- |
| Final isolated sample | URP/Linear; imports, wind, GPU depth and GPU color checks pass; all four shaders supported, no reported shader errors | [MaterialLabReport.json](../../Artifacts/TeyvatGrassland/WP02/r1/MaterialLabReport.json) |
| Ground/grass GPU albedo | Maximum terrain/grass RGB difference 0.00001856685; largest GPU-versus-reference channel difference 0.00047113, below the unchanged 0.003 starting tolerance | [ColorFieldValidation.json](../../Artifacts/TeyvatGrassland/WP02/r1/ColorFieldValidation.json) |
| Water GPU depth | Known bed depths produce increasing sampled thickness and the constant-depth fallback passes | [WaterDepthValidation.json](../../Artifacts/TeyvatGrassland/WP02/r1/WaterDepthValidation.json) |
| Imported authored sample | All six FBX meshes pass source/hash, geometry, UV, mask and normal-range diagnostics | [ImportValidation.json](../../Artifacts/TeyvatGrassland/WP02/r1/ImportValidation.json) |
| Deformed bounds | Sample vertices and instanced placements pass the diagnostic at its fixed test times | [WindBoundsValidation.json](../../Artifacts/TeyvatGrassland/WP02/r1/WindBoundsValidation.json) |
| Actual GPU instanced coverage | All 4,200 separated grid placements visible, nine runtime submissions at 500 maximum per call, no missing indices and a clean black background | [InstancingCoverageValidation.json](../../Artifacts/TeyvatGrassland/WP02/r1/InstancingCoverageValidation.json) |
| Cold saved-scene handoff | Opens without Build or pipeline reconfiguration in a separate Unity editor process: URP/Linear retained, 4,200 saved matrices and submitted instances, nine API submissions, 11 cameras, zero missing scripts, wind/bounds pass | [Reload/SavedSceneReport.json](../../Artifacts/TeyvatGrassland/WP02/r1/Reload/SavedSceneReport.json) |

The CPU color reference decodes each contributing sRGB texel to Linear before bilinear interpolation at the GPU texel-center convention. It does not apply `.linear` to an already-interpolated sRGB value. This matches hardware sample ordering; the diagnostic tolerance was not widened to obtain the pass. The GPU comparison is a flat layer-zero/root-albedo proof, not a claim that every lit grass/terrain boundary or future painted layer will match automatically.

The cold reload demonstrates serialized scene/material/mesh/global data remains usable without a fresh builder run. Its nine submissions are explicit API submissions, not a measured final hardware-draw or frame-rate budget. All evidence here is Unity editor/offscreen evidence; the capture loop's elapsed milliseconds are not standalone gameplay frame time.

## Downstream and final-production checks

Preserve the accepted interfaces above while replacing sample assets. Repeat the relevant checks whenever a lane changes mesh masks, normals, materials, wind amplitude, rendering features, texture mapping or camera coverage. The following checks remain part of asset and final-map acceptance; the accepted shared sample does not waive them.

1. **Compile/import:** no shader errors in URP17.3/Linear; actual imported source sample preserves metre scale, UV0, normals, RGB weights and alpha1. Record importer settings and source/export version.
2. **Ground/grass field:** diagnostic samples confirm world bounds and root albedo equality; native terrain and mesh-ground variants receive matched neutral/afternoon and sun/shade captures through planning and attack cameras. Inspect field bounds and seams.
3. **Wind/pass consistency:** fixed common times compare wind off/on; inspect roots, branch joins, phase stability, Forward/ShadowCaster/DepthOnly silhouettes and maximum animated bounds. Record any normal-rotation approximation.
4. **Canopy/flowers:** front/back light, cool shade and attack distance preserve canopy volume without black backs, obvious card edges or unreadable petals. Compare custom-normal import against a diagnostic normals view.
5. **Rock/trunk:** broad geometry survives texture application without fine-noise dominance; a repeated tile has no prominent seam. No directional sunlight is accepted in albedo.
6. **Water:** known shallow/deep basin samples give increasing depth tint; bank intersections are tested at elevated/grazing and both-team views. Freeze/common-time captures show directional waterfall movement. Layer representative transparent combat effects and mist; record sorting limitations and constant-depth fallback behavior.
7. **Submission/runtime:** observe the actual instanced draw route, counts, shadows, bounds and CPU/GPU behavior on named hardware. Compare wind off/on and grass disabled/enabled. Editor/offscreen numbers are labelled; a Windows player build remains necessary for final performance acceptance.
8. **Preservation:** the sample regenerates in the isolated project; main Battle scene, main render settings and installed vendor assets retain their recorded hashes/configuration. Sample cameras/lights stay out of reusable environment modules.

All numeric defaults and sample dimensions here are starting values. Their acceptance condition is the relevant microtest above; adjust the value in the direction that fixes the recorded defect, recapture and record the resulting setting. No triangle count, batch size, MSAA choice, texture scale, opacity, wind strength or draw distance is an industry-standard production budget. Named minimum PC hardware and frame-rate/display goals remain a final performance dependency.

WP02 r1 is accepted as the **shared rendering/material sample for asset production**. Keep its limits explicit: Forward URP17.3/Linear, at most four native terrain layers with grass0/rock2 conventions, realtime shading and disabled far basemap, one shared global controller, no custom foliage DepthNormals/motion-vector pass, no SSAO/TAA, and no refraction/reflection-camera or fully tested transparent-combat sorting contract. The stylized imported sample is a material baseline; complete production trees/cliffs/meadow/water assets, final-map reference matching, combat playback and standalone PC performance acceptance remain subsequent packages. Read the latest evidence report and run markers together when regenerating this sample.
