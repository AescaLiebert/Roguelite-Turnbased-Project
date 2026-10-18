# WP06 water and waterfalls r1

2026-10-18. Windows PC, Unity6000.3.4f1 / URP17.3 Forward / Linear art lane. This package replaces the R3 water placeholders in a separate scene derived from WP05. It retains the accepted battle layout, five river islands, three six-point cascade courses, native terrain, trees and tall meadow. It does not migrate the main Built-in/Gamma game.

## Deliverables

| Item | Location |
| --- | --- |
| Editable Blender source, generator, FBX and source manifest | [ArtSource/TeyvatGrassland/Water](../../ArtSource/TeyvatGrassland/Water/) |
| Imported surfaces, shaders, materials and reusable prefabs | [Water assets](../../Assets/Project/Art/Environment/TeyvatGrassland/Water/) |
| Representative sample | `Water/Scenes/TG_WaterSample_r1.unity` |
| Assembled review | `Water/Scenes/TG_WaterReview_r1.unity` |
| Isolated build/reload launcher | [RunTeyvatWaterLab.ps1](../../Tests/Environment/RunTeyvatWaterLab.ps1) |
| Captures, checks and packages | [WP06/Revision01](../../Artifacts/TeyvatGrassland/WP06/Revision01/) |

The source contains41 mesh objects /45,034 triangles including its diagnostic marker. The full module has40 renderers: one river, three cascade sheets, six catch pools, twelve impact/ripple patches, twelve mist patches, five sparse island wakes and a shoreline strip. The lower-cost prefab has10 renderers: river, three cascades and six pools. Counts describe this module, not an industry budget or final frame cost.

## Surfaces and appearance

The river follows the unchanged curved R3 near/far banks at local Y−1.35m. Opaque native terrain and authored islands hide its interior portions and provide sampled underwater depth. Five islands remain separated by visible channels. A marching-square contour of the canonical native heightfield supplies a narrow foam strip on the wet side of the real waterline. The shader breaks up this strip; island wakes are sparse downstream accents rather than uniform rings.

Each cascade uses the six accepted R3 path points. U runs0..1 across its width; V runs0..1 downstream by cumulative physical arc distance. Subdivided sheets add small static edge-width variation and a shallow bow, keeping source and impact endpoints. Two smaller elliptic catch pools sit at the intermediate ledge and river impact. Separate soft-edged radial foam patches, downstream ripple fans and restrained crossed mist patches soften those transitions. Mist is procedural mesh shading, not a particle simulation or camera-facing billboard system.

Waterfall shading uses warped noise at unequal scales for variable blue/white flow, softer irregular sides and crest/impact emphasis. The river uses depth tint, broad quieter variation, broken surface accents and a Fresnel sky tint. No bitmap textures or external model service are needed; no game textures were extracted. The editable blend includes preview materials, while the production appearance is implemented by the Unity shaders.

## Rendering contract

The lane owns `Teyvat/URP/WaterProduction`, `Teyvat/URP/WaterEffects` and `TG_WaterProcedural.hlsl`; the Shared shaders remain unchanged. Both use the existing `_TG_Wind.w` time from the scene's single `TeyvatMaterialGlobals`. There is no second animation controller. They use ordinary alpha blending, `ZWrite Off`, `ZTest LEqual`, `Cull Off`, fog and instancing support. Water does not cast shadows. WaterProduction receives the main light's shadow; foam/mist are softly tinted unlit effects with fog.

WaterProduction retains the WP02 identifiers: `_ShallowColor`, `_DeepColor`, `_FoamColor`, `_DepthRange`, `_FoamDistance`, `_FlowDirection`, `_FlowSpeed`, `_FlowStrength`, `_UseSceneDepth`, `_FallbackDepth`, `_Mode`, `_Opacity` and `_DebugDepth`. Its owned extension adds `_SkyColor`, `_ReflectionStrength`, `_FoamStrength`, `_RippleScale`, `_EdgeSoftness` and `_Quality`. `_Mode`0 is river,1 waterfall and2 an edge-faded pool. `_DebugDepth`0 is normal output,1 normalized thickness,2 raw depth,3 scene eye depth/40 and4 water eye depth/40.

WaterEffects exposes `_BaseColor`, `_Opacity`, `_FlowSpeed`, `_DepthFade`, `_UseSceneDepth`, `_EffectMode`, `_NoiseScale`, `_Softness` and `_Quality`. Modes0/1/2/3 are radial foam, streak, mist and world-space shoreline strip. Vertex R is a stable patch phase and A is patch opacity; G/B are unused. This is Water-owned mesh data, not the Shared foliage-wind channel contract.

**Reflection method:** artist-controlled sky-color Fresnel only. It does not reflect scene geometry, sample a probe or launch an additional camera. **Refraction method:** none. Transparent blending reveals the opaque bed without distorting scene color. The opaque texture remains disabled. River depth mode requires an opaque depth texture available before transparent water: the accepted renderer uses `CopyDepthMode.AfterOpaques` and the review cameras request depth. The shader handles perspective/orthographic linearization and empty-sky depth. Waterfalls use fallback thickness because the cliff behind the curtain is not water thickness.

The production queues are river3000, waterfall3001, pools3002, foam3005 and mist3010. The sorting fixture uses representative purple transparent effect patches at3020. This favors effect readability for that fixture; fixed queues do not solve arbitrary interpenetrating transparent geometry. Opaque terrain, trees and actor proxies still occlude through normal depth testing. Actual gameplay VFX/shaders must be checked during WP07.

`TG_WaterFallback.prefab` omits foam, shoreline strips, wakes and mist, sets `_Quality=0` and `_UseSceneDepth=0`, and uses constant thickness. It requires no depth texture for its own materials and preserves the main water silhouette. It loses real shoreline/contact depth behavior. It does not change the scene's pipeline settings or turn off other systems' depth requests.

## Tuning and reproduction

All art numbers are starting values, not standards. Current river depth range5.4m and contact band.56m should be judged at the island-channel and grazing cameras: if shallow shelves disappear, increase range or lighten shallow tint; if foam blankets channels, narrow the band or reduce foam strength. Cascade flow.32 and river flow.65 use shader-specific coordinates; review the fixed-time sequence for apparent downstream motion, reduce speed if it flickers and increase if it reads static. Mist opacity.40 is multiplied by patch opacity and soft noise; reduce if it erases islands/actors, increase cautiously if impact depth is lost. These camera tests govern acceptance rather than the values alone.

From the repository root, run each stage separately and wait for its process to finish:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' --background --factory-startup --python 'ArtSource/TeyvatGrassland/Water/create_water_kit.py'
& 'Tests/Environment/RunTeyvatWaterLab.ps1' -SampleOnly
& 'Tests/Environment/RunTeyvatWaterLab.ps1'
& 'Tests/Environment/RunTeyvatWaterLab.ps1' -VerifySaved
```

The launcher reuses the closed isolated `.utmp/TeyvatWorldLab`, checks its process/lock, copies dependencies incrementally without deletion and starts hidden batch Unity. It never opens the main gameplay project in batch. Source regeneration overwrites its own generated blend/FBX/manifest and copies the import files to Water; preserve manual sculpt revisions and existing `.meta` files. Cold verification opens the saved review scene without rebuilding or reconfiguring the pipeline, repeats checks and exports the Unity package. Read the current completion marker and reports together.

## Evidence and remaining work

The current [WaterValidation.json](../../Artifacts/TeyvatGrassland/WP06/Revision01/WaterValidation.json) and independent [cold reload](../../Artifacts/TeyvatGrassland/WP06/Revision01/Reload/WaterValidation.json) pass. Both processes retain all17 original camera poses/FOVs,120 trees,70 shrubs, the R3 terrain/meadow references and one shared global owner. Water level error is zero; the six native-ground anchor samples have maximum height error0.00054932m. All41 imported meshes match the source hash, triangle counts, metre bounds, UV/normal/color streams and normal ranges. All three descending101-row cascade sheets retain source/impact endpoints within0.000020m and downstream V direction; all six catch pools are present at their specified levels. No runtime water colliders, old water placeholders, missing scripts, shader errors or extra reflection cameras remain in the review.

GPU fixtures use actual production water on known0.2/1/3m opaque beds in perspective and orthographic cameras, with linear floating-point readback; they also check a fixed-depth fallback. Both projections pass in warm and cold runs. Maximum warm bed-depth error is **0.00006581m perspective /0.000001148m orthographic**, below the unchanged0.1m equivalent starting tolerance. See [perspective evidence](../../Artifacts/TeyvatGrassland/WP06/Revision01/Perspective/WaterDepthValidation.json) and [orthographic evidence](../../Artifacts/TeyvatGrassland/WP06/Revision01/Orthographic/WaterDepthValidation.json). These fixtures establish depth math, not fluid simulation or reference-image parity.

There are28 neutral and28 afternoon views, eight fixed common-time flow frames, grazing/reverse/island views and four representative transparent-effect captures. The fixture confirms readable purple effects in the chosen queue order and opaque terrain occlusion; it is not an automated proof of arbitrary transparent overlap. The reverse acting-side shot naturally faces away from the river.

[WaterCost.json](../../Artifacts/TeyvatGrassland/WP06/Revision01/WaterCost.json) alternates full/fallback/no-water in the Editor on **AMD Ryzen5 4600G / AMD Radeon(TM) Graphics**. Six observations after warmup include two offscreen render submissions plus synchronous readback at1600×900/4×MSAA. Medians are **82.224ms full /89.343ms fallback /81.641ms water off**. The variance does not establish a fallback speed improvement or isolated GPU water cost. These are not player frame times or minimum-PC certification; the fallback's verified reduction is40→10 renderers and removal of effect/depth dependencies.

The retained terrain is still a production blockout in several close waterfall/rock intersections. Integration should refine those contacts and final foam composition together; this lane does not silently recut accepted cliffs or move vegetation. Final sky/lighting, actor/VFX playback, automatic transparent overlap and a standalone Windows performance pass remain WP07–WP09 work. The result is an editable water increment toward the reference, not a claim of Genshin-quality parity.

## Packaged handoff

[TG_WP06_Water.unitypackage](../../Artifacts/TeyvatGrassland/WP06/Revision01/TG_WP06_Water.unitypackage) contains109 assets including the review scene and referenced dependencies. It excludes the gameplay Battle scene, ProjectSettings and Packages. [Editable source archive](../../Artifacts/TeyvatGrassland/WP06/Revision01/TG_WP06_Water_EditableSource.zip) contains20 files, including Blender/FBX, generator/helper, shaders, builder/validators, frozen layout/height data and documentation. [PackageContents.json](../../Artifacts/TeyvatGrassland/WP06/Revision01/PackageContents.json) records inventory/hash, and [HandoffManifest.json](../../Artifacts/TeyvatGrassland/WP06/Revision01/HandoffManifest.json) records owned source/asset hashes. [PreservationCheck.json](../../Artifacts/TeyvatGrassland/WP06/Revision01/PreservationCheck.json) confirms all **382 protected main, R3, Shared, Meadow and Trees files** match their starting hashes.
