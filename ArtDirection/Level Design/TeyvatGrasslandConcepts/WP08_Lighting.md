# WP08 — reference daylight lighting and atmosphere r2

2026-10-19. Windows PC art target. **R2 replaces the warm r1 treatment following the user's new reference direction.** The first two new references establish clear daylight: cyan-blue sky, near-white sun, luminous yellow-green grass and distinctly cool tree/cliff shadows. The third informs layered depth and selective warm rims, not a gold wash across the stage.

**R2 full build/bakes, independent saved-scene reload, actual Play Mode and package verification passed.** Historical [Revision01 evidence](../../../Artifacts/TeyvatGrassland/WP08/Revision01/) is retained separately. The current results below come from Revision02. This is not an exact-reference or final Genshin-parity claim.

## Outputs and ownership

| Deliverable | Location |
| --- | --- |
| Owned materials, palette field, shader copies, profiles, pipeline and probes | [Lighting](../../../Assets/Project/Art/Environment/TeyvatGrassland/Lighting/) |
| Profile/scene owner | `Lighting/Runtime/TeyvatLightingProfile.cs`, `TeyvatLightingRig.cs` |
| Target and comparison profiles | `Lighting/Profiles/TG_ReferenceDay.asset`, `TG_Neutral.asset` |
| Environment prefab | `Lighting/Prefabs/TG_LitBattleStage_r2.prefab` |
| Complete review scene | `Assets/Project/Scenes/EnvironmentReview/TeyvatGrassland/TG_LightingReview_r2.unity` |
| Builder and rock-binding review | [TeyvatLightingLab.cs](../../../Assets/Project/Editor/TeyvatLightingLab.cs), [TeyvatLightingRock.cs](../../../Assets/Project/Editor/TeyvatLightingRock.cs) |
| Isolated launcher | [RunTeyvatLightingLab.ps1](../../../Tests/Environment/RunTeyvatLightingLab.ps1) |
| Current evidence and eventual handoff | [WP08/Revision02](../../../Artifacts/TeyvatGrassland/WP08/Revision02/) |

The builder opens the accepted WP07 master and saves a separate review. Materials are copied under Lighting. Surface, foliage, meadow and native-terrain shading use **Lighting-owned shader copies** and their common include; earlier Shared, Trees, Meadow, Water and Integration assets remain inputs. Copied water/effect materials retain their earlier shader dependencies.

The owned world-color texture now preserves input hue, saturation and value instead of r1's desaturation/value lift. A Lighting-owned meadow data asset remaps materials while copying canonical seed, meshes, placement matrices and cell bounds. The translation-only `TeyvatStageBinding` retains its transient runtime data. Checks confirm all seventeen original camera poses and zero placement-matrix error for 27,737 clumps. The protected-input hash check confirms 3,727 files unchanged, including the accepted terrain data/control maps and gameplay settings.

The environment prefab contains the remapped stage. **The scene owns the sun, lighting rig, global Volume, probes and actor fixtures outside that prefab.** Dropping the environment prefab into another scene does not reproduce the complete look or bake inputs.

The asset package excludes project settings and package manifest. Import into a separate **Unity 6000.3.4f1 / URP 17.3** project, select **Linear**, assign `Lighting/Settings/TG_LightingURP.asset` in both active Graphics and Quality settings, reopen and open `TG_LightingReview_r2.unity`. Preserve imported metadata/GUID relationships and dependencies. The launcher automates setup only in the isolated art host; source regeneration expects accepted earlier repository lanes.

## Clear daylight, sky and materials

`TeyvatLightingRig` applies sun, ambient colors, deterministic analytic ambient SH, linear fog, skybox and Volume before relevant cameras render. It publishes owned shading controls and reuses the single `TeyvatMaterialGlobals` clock. Established foliage deformation, opacity and depth behavior retain that timing.

R1 lit visible cliff walls from near the battle camera, reducing cap-versus-wall separation. R2 puts the sun behind-left of the player-facing view: grass caps/crowns receive the white key while front-facing cliffs retain cool blue-gray form. It removes exposure lift and saturation reduction rather than compensating for flat values with more global brightness.

ReferenceDay adds **analytic procedural cloud shadows** over world X/Z, using two noise scales and `_TG_Wind.w`. This modulates the key; it is not volumetric clouds, a simulated cloud layer or a shadow map derived from the panorama. Neutral disables modulation. The environment helper supports the main directional light and SH ambient, not arbitrary local lights, detailed lightmaps or URP/Lit reflections.

`Teyvat/URP/PaintedSky` reuses **Fantasy Skybox FREE FS003 Day Sunless**. A chroma mask separates cloud from clear sky while retaining source cloud color/value modelling. The owned PNG is a byte-for-byte FS003 copy, with mipmaps, trilinear sampling and uncompressed import. Palette inputs explicitly convert to linear. No generated/repainted sky image is used. Analytic sun direction follows the light; glow is zero, with a restrained disc. Cloud drift starts at zero and, if enabled, shares the clock.

Both profiles use **Neutral tonemapping, zero post-exposure and zero saturation adjustment**, without depth of field/motion blur. The Forward renderer explicitly references installed URP post-process resources, enables HDR/soft main-light shadows and copies opaque depth **AfterOpaques** for water. Scene-color sampling stays disabled; captures use MSAA.

These are **current source starting values**, to compare in matched planning/attack/water views:

| Control | Neutral / ReferenceDay starting values and review |
| --- | --- |
| Key | Elevation 55° / 52°, azimuth 145°; white / `(1,1,.99)`, intensity 1.15 / 1.70. Check both team directions and cap/wall separation before tuning. |
| Grade/bloom | Exposure 0, saturation 0; contrast 0 / +6. Bloom threshold 1.4, intensity 0 / .035. Preserve foam/aura shape; exposure cannot repair material color. |
| Ambient/shadow | Sky `(.41,.64,.84)`, equator `(.27,.44,.51)`, ground `(.19,.29,.25)`; linear ambient floor `(.08,.105,.13)`. LightStyle ambient/key/shadow-floor/transmission `(.85,1.10,.16,1.15)`. Keep cool readable shadows without turning all stone navy. |
| Haze | Neutral off; ReferenceDay start 140 m/end 650 m, color `(.48,.72,.86)`. Distant ridges should lose contrast before nearby cliffs; do not hide contacts. |
| Cloud shadow | World scale .021, drift .018, strength 0 / .16. Compare open sunlit and shaded patches rather than uniformly dimming the field. |
| Shadow maps | Distance 150 m, resolution 2048, four cascades. Check contact, bias and moving-camera transitions. |
| Foliage | Grass base `(.54,.79,.30)`, tip multiplier `(1.10,1.06,.92)`, wrap .26/transmission .17, world-color roots. Leaf copies use cooler undersides and wrap .23. Do not lift grass to compensate for dark stone. |
| Sky | Rotation 212°, exposure 1, glow 0, sun radius .32°. Check cloud placement and modelling; panorama detail remains an asset qualification. |

The third reference's warm foliage/rims and layered distance inform depth; alternate landforms do not replace the accepted stage composition.

## Painted cliff texture correction

The original [TG_RockPainted.png](../../../Assets/Project/Art/Environment/TeyvatGrassland/Shared/Textures/TG_RockPainted.png) was already assigned to mesh rocks and native terrain layer2. It appeared nearly bare because the mesh shared-style branch ignored local `_TextureStrength` and retained weak shared paint .35; RockShade followed a separate weak .35 path. Front lighting further compressed form. Native terrain already used world triplanar mapping.

R2 adds [TG_ReferenceRock.hlsl](../../../Assets/Project/Art/Environment/TeyvatGrassland/Lighting/Shaders/Terrain/TG_ReferenceRock.hlsl), shared by Lighting mesh rock and native layer2. It normalizes the texture's measured linear mean `(.17163,.18891,.21022)` around one before applying the profile tint. Relative painted contrast is 1.20, clamped .50–1.65. This is albedo variation, not baked AO or invented normals.

Current binding evidence identifies **two Lighting-owned materials across 347 mesh material slots**, plus native terrain's existing layer2. Meshes select `_ReferenceRock=1`, local texture strength 1 and world triplanar mapping. Shared paint strength is **.85**; world scale **.055 UV/metre**, approximately **18.18 m per tile**; RockShade multiplier .88. Current profile rock tint `(.64,.72,.78)` converts to linear through shared globals.

No TerrainData, heights, layer weights, TerrainLayer, original texture or earlier-lane material is changed. Ground/soil retain accepted control maps and world-color response. [ReferenceRockBindings.json](../../../Artifacts/TeyvatGrassland/WP08/Revision02/ReferenceRockBindings.json) records bindings. Matched `RockTexture/*_PaintedLit`, `*_FlatTintLit` and `*_PaintedAlbedo` captures distinguish texture from light; they are diagnostics, not exact-match scores.

## Actor, aura and bake qualifications

Current Lumine/Amber definitions reference the shared **cube placeholder prefab**, without detailed mesh overrides. Fixtures reconstruct it at **1.5 × 3 × 1.5 m**, face both team directions and ground renderer bounds using the production offset rule. Source-derived **URP/Lit review materials** preserve portrait texture/color mapping; gameplay assets are not converted. `ActorFixtureProvenance.json` records sources and positions. These textured cubes cannot establish detailed skin, hair, costume, rigs or animation lighting.

The aura uses actual [UltimateReadyAura](../../../Assets/Project/Runtime/Presentation/Combat/UltimateReadyAura.cs) and its [resource shader](../../../Assets/Resources/UltimateReadyAura.shader), with explicit seeds and fixed evaluation. It tests an additive unlit effect under exposure/bloom/transparency, not probe-responsive lighting or a combat playthrough. Radius .85 m, height 3 m and evaluation .8 s are fixture starting values.

Full mode requests **45 legacy probes** on a 5 × 3 × 3 arena lattice. Progressive CPU baking uses an approximate opaque URP/Lit ground slab and up to **eight nearby boulder proxies**. Stylized terrain/background, foliage, grass, water and effects do not contribute to that GI bake; proxies disable afterward and stay outside the environment prefab. This approximates actor indirect light, not full-stage lightmaps, APV or canopy bounce.

A **128-resolution HDR cubemap**, generated by Unity's bake and assigned to the probe's serialized **Custom** slot, supplies static URP/Lit actor reflections. Its capture includes scene renderers with actors and instanced grass temporarily disabled. There is no runtime reflection camera or moving-content update. **Environment/water shaders do not sample it**; water keeps an artistic sky-color Fresnel approximation. Neutral/ReferenceDay are profile comparisons, not independent full bakes; full mode bakes ReferenceDay. R2 bake and saved-scene persistence checks passed.

## Reproduce and verify

Use isolated **`E:/Program Files/Unity/RogueliteTurnbasedProject/.utmp/TeyvatWorldLab`**, Unity 6000.3.4f1 / URP 17.3 Forward / Linear. Main gameplay remains **Built-in/Gamma** with its controller, settings and source materials unchanged.

Close the isolated editor. From repository root:

```powershell
& 'Tests/Environment/RunTeyvatLightingLab.ps1' -SampleOnly
```

The launcher returns while a hidden Unity process works. Wait for process completion and current marker, then inspect samples. **SampleOnly skips baking and cannot establish full acceptance/persistence.** Run the following sequentially, waiting for each process:

```powershell
& 'Tests/Environment/RunTeyvatLightingLab.ps1'
& 'Tests/Environment/RunTeyvatLightingLab.ps1' -VerifySaved
& 'Tests/Environment/RunTeyvatLightingLab.ps1' -Playback
```

Default builds/bakes/captures/checks. VerifySaved opens saved R2 without rebuilding/rebaking, repeats review and exports. Playback uses actual Play Mode on that saved host. Default/SampleOnly stages repository inputs; saved-review/Playback/ExportOnly retain the generated snapshot instead of copying potentially stale main assets over it.

After current-revision passes, synchronize/finalize:

```powershell
python Tests/Environment/finalize_teyvat_lighting.py copy-back
python Tests/Environment/finalize_teyvat_lighting.py images
python Tests/Environment/finalize_teyvat_lighting.py motion
python Tests/Environment/finalize_teyvat_lighting.py bundle
```

The finalizer requires full/cold/Play passes and bakes, compares package asset/metadata payloads with synchronized sources, checks local HLSL include closure and hashes protected inputs. It produces [TG_WP08_Lighting.unitypackage](../../../Artifacts/TeyvatGrassland/WP08/Revision02/TG_WP08_Lighting.unitypackage), [TG_WP08_EditableSource.zip](../../../Artifacts/TeyvatGrassland/WP08/Revision02/TG_WP08_EditableSource.zip) and [HandoffManifest.json](../../../Artifacts/TeyvatGrassland/WP08/Revision02/HandoffManifest.json). R2 finalization passed.

## Verified R2 results

| Evidence | Current revision status |
| --- | --- |
| [Full lighting validation](../../../Artifacts/TeyvatGrassland/WP08/Revision02/LightingValidation.json) / [saved reload](../../../Artifacts/TeyvatGrassland/WP08/Revision02/Reload/LightingValidation.json) | PASS: 17 original poses, six grounded source-derived cube actors, 27,737 unchanged meadow placements, 29 owned material copies, zero missing scripts/shader errors, one clock/lighting rig and retained rock-texture bindings. |
| [Probe bake](../../../Artifacts/TeyvatGrassland/WP08/Revision02/ProbeBake.json) / [reflection](../../../Artifacts/TeyvatGrassland/WP08/Revision02/ReflectionBake.json) | PASS: 45 finite legacy probes and 128-resolution HDR cubemap; both survive reopening. Approximate actor indirect light, not full-stage lightmaps. |
| [Rock binding evidence](../../../Artifacts/TeyvatGrassland/WP08/Revision02/ReferenceRockBindings.json) / [rendered comparisons](../../../Artifacts/TeyvatGrassland/WP08/Revision02/ImageReview.json) | PASS: texture-on/off changes 53.68% of WaterSample pixels and 23.16% of Overview pixels by more than one RGB unit. Mean RGB differences are 2.9382 and 1.2298 respectively. This verifies a visible texture contribution, not reference-style parity. |
| [Rendered post-process test](../../../Artifacts/TeyvatGrassland/WP08/Revision02/Reload/PostProcess/PixelValidation.json) | PASS: actual-source aura at fixed camera/time, post on/off difference 5.4832 mean RGB units out of 255. It proves rendering changes, not a bloom-only measurement. |
| Matched captures | 84 in each full/cold run: 35 Neutral, 35 ReferenceDay, four clean Beauty, four actual-aura and six rock comparisons. All four Beauty views are pixel-identical after reopening. Actor-containing views differ by up to 1.3539 mean RGB units; their warm/cold response is not claimed bit-identical. Use Reload for saved-scene appearance. |
| [Actual Play Mode](../../../Artifacts/TeyvatGrassland/WP08/Revision02/Playback/PlaybackValidation.json) | PASS after domain reload: 24 water and 24 moving-tour frames; advancing shared clock, grass submissions, one transient meadow binding and retained cubemap. The aura was tested separately at fixed time; this is not a combat playthrough. |
| [Preservation](../../../Artifacts/TeyvatGrassland/WP08/Revision02/PreservationCheck.json) / [package inventory](../../../Artifacts/TeyvatGrassland/WP08/Revision02/PackageContents.json) | PASS: 3,727 protected files unchanged, 155 packaged asset payloads and metadata matching synchronized sources, 27 local shader includes verified. Editable source archive/manifest produced. |

Review the [saved overview](../../../Artifacts/TeyvatGrassland/WP08/Revision02/Reload/Beauty/TG_EnvironmentOverview.png), [close cliff texture view](../../../Artifacts/TeyvatGrassland/WP08/Revision02/Reload/Beauty/TG_WaterSample.png), [water clip](../../../Artifacts/TeyvatGrassland/WP08/Revision02/TG_WP08_WaterPlayMode.mp4) and [tour clip](../../../Artifacts/TeyvatGrassland/WP08/Revision02/TG_WP08_TourPlayMode.mp4). Clips encode 24 frames at 6 fps; they are not timing measurements. Read current `COMPLETE.txt` / `FAILED.txt` with reports; an earlier sample marker is not a full pass.

Broad cliff bands, sparse/small canopies, uniform tall-grass coverage, waterfall shapes and panorama detail still differ from references. Light cannot repair asset shape/paint limitations. Detailed characters and continuous combat/VFX remain unverified. Encoded frame rates or Editor capture time are not player FPS. Standalone performance on a named minimum Windows PC and final combat-camera qualification remain WP09 work.


