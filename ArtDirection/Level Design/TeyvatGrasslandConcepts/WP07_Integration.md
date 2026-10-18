# WP07 — integrated grassland stage r1

2026-10-18–19. Windows PC art target. **Integrated, with passing warm/cold structural, depth, camera-envelope and actual Play Mode review.** This package assembles the accepted R3 landscape, tall meadow, WP05 trees and WP06 water into one reusable stage, with local waterfall-contact corrections and new foam/spray treatment. Actual combat integration, final reference parity and player performance remain later reviews.

## Stage and owned changes

| Deliverable | Location |
| --- | --- |
| Editable contact-correction source, FBX and manifest | [ArtSource/TeyvatGrassland/Integration](../../ArtSource/TeyvatGrassland/Integration/) |
| Integration meshes, materials, terrain, shaders and runtime binding | [Integration](../../Assets/Project/Art/Environment/TeyvatGrassland/Integration/) |
| Reusable complete environment prefab | `Assets/Project/Art/Environment/TeyvatGrassland/Prefabs/TG_BattleStage_r1.prefab` |
| Separate master review scene | `Assets/Project/Scenes/EnvironmentReview/TeyvatGrassland/TG_StageReview_r1.unity` |
| Isolated build/cold-reload launcher | [RunTeyvatStageLab.ps1](../../Tests/Environment/RunTeyvatStageLab.ps1) |
| Captures, diagnostic reports and eventual asset package | [WP07/Revision01](../../Artifacts/TeyvatGrassland/WP07/Revision01/) |

The source manifest contains **15 replacement meshes / 3,058 source triangles**, covering affected landing shelves, cliff pieces and talus at both available detail levels. The Blender generator opens the accepted R3 blend and subtracts a closed channel cutter following each existing watercourse. It exports only changed rocks, with exact source-to-replacement names; the older lane assets remain inputs. Cuts retain the shoulders while recessing the bed 0.34 m below the canonical course points, with width `half_width × 1.16 + 0.45 m` on each side. WP06 ribbons retain their 0.13 m surface offset, so this is not a 0.34 m physical film thickness. These are contact-review starting values, not erosion simulation.

An Integration-owned TerrainData lowers 109 native height samples beneath the same three courses, starting at 0.36 m clearance. Accepted R3 TerrainData remains unchanged. Six fighter anchor heights and seventeen camera poses are preserved, along with 120 trees and 70 shrubs. The stage adds 81 seeded flower accents. Its meadow derivative excludes 119 tufts from waterfall channels, retaining 27,737 placements and the battlefield grass policy, including actor positions. The original R3 meadow asset remains unchanged.

## Water contact, foam and animation

The Integration lane owns three shaders plus [TG_WaterProcedural.hlsl](../../Assets/Project/Art/Environment/TeyvatGrassland/Integration/Shaders/TG_WaterProcedural.hlsl). They reuse the shared helpers without editing older Shared/Water shaders:

| Shader | Actual interface and use |
| --- | --- |
| `Teyvat/URP/IntegratedWater` | Retains shallow/deep/foam colors, depth range, flow, fallback and opacity controls. Adds per-fall `_FlowLength` and `_FlowWidth` so broken turquoise/white patterns use physical course dimensions. River/pool shading reads opaque depth; falling curtains use their artist fallback thickness. |
| `Teyvat/URP/ImpactFoam` | Normalized patch UV, vertex R phase and A opacity. `_EffectMode` selects impact, downstream fragments or mist. Concentrated asymmetric white patches at the back/+V edge feed sparse fragments toward −V, replacing ring-shaped impacts. Queue 3005; mist materials use 3010. |
| `Teyvat/URP/ImpactSpray` | Shared-clock ballistic droplets with per-emitter velocity/lifetime and phase/size. Camera-facing quads fade in/out over their lifetime; queue 3015. |

All use the scene's **one `_TG_Wind.w` clock**. Foam and spray are transparent, double-sided and depth-tested without depth writes. Optional opaque-depth fading softens intersections; it does not make overlapping transparent surfaces write depth or solve arbitrary combat-effect sorting. Retained shoreline strips still depend on the WP06 `WaterEffects` shader. Opaque bed/rock geometry and the accepted URP depth copy remain dependencies.

Six additional foam-fringe meshes create a broken vertical white foot at the landings: UV V runs bottom to top in effect mode 3, with animated uneven tips. Their 1,920 triangles join the horizontal impact patches and spray.

The spray geometry has **six meshes / 920 emitters**: two landings per course, with 200 emitters per hero landing and 130 per side-fall landing. Each emitter contributes four vertices and two triangles, with movement performed in the vertex shader. Initial upward velocity is 1.8–5 m/s, lifetime 0.55–1.10 s and world half-size 0.045–0.11 m; these are starting art values. Check that droplets separate from the white impact mass, travel outward/downstream, fade before looping and remain inside saved bounds. Reduce size, density or opacity if the hero base becomes a white curtain; raise velocity/size only if motion disappears at the actual attack/planning distance.

Waterfall `_FlowSpeed` starts at 1.9; the shader's along-course pattern uses a factor of 4, approximately 7.6 m/s of pattern travel. This is a stylized animation rate, not simulated water velocity. Foam/mist have their own rate multipliers on the same clock. Review fixed-time frames and a moving sequence across crest, catch shelf, lower spill and base: features should descend coherently without apparent upward travel, whole-sheet flicker or abrupt material joins.

The baseline uses no scene-color texture, reflection/refraction camera or fluid simulation. `_SkyColor`/Fresnel is an artistic sky-tint approximation; transparency can reveal the opaque riverbed through the surface. Neither effect is an actual reflected/refracted scene image.

## Prefab placement and review scene

`TG_BattleStage_r1` contains the environment, native ground, meadow, trees, water, effects, dressing and one material-globals owner. Review cameras, lights and capsule actors stay outside it. `TeyvatStageBinding` supports **translation only**: keep its world rotation identity and world scale one, including parent transforms, because native Unity Terrain constrains the stage.

The binding creates an owned, unsaved copy of canonical meadow placement data, translates its matrices/cell bounds from the authored origin and shifts world-color bounds consistently. Mesh/material references stay shared; the canonical asset is not rewritten. Repeated binding reuses that copy rather than accumulating offsets. Saving serializes the canonical reference; enabling/reloading recreates the transient copy. Disabling/destroying restores the canonical reference and releases owned data. Rotation/scaling is outside the contract and reports a warning.

`TeyvatStageTour` is a **review-only** camera component in the master scene, excluded from the prefab. In Play mode it interpolates the saved review shots. It is not the battle camera controller, does not move real characters and does not establish runtime attack/ultimate-camera acceptance.

Neutral and afternoon lighting retain the WP06 review baseline. Final atmosphere and character lighting remain WP08 work. Open the isolated **`.utmp/TeyvatWorldLab`** project in Unity 6000.3.4f1 with URP 17.3 Forward/Linear, then open the master review scene. The main game remains Built-in/Gamma; this package does not migrate its renderer or modify battle rules.

## Reproduction and verified review

From the repository root, with the isolated Unity host closed:

```powershell
& 'C:/Program Files/Blender Foundation/Blender 5.1/blender.exe' --background --factory-startup --python 'ArtSource/TeyvatGrassland/Integration/create_stage_contact.py'
& 'Tests/Environment/RunTeyvatStageLab.ps1' -SampleOnly
& 'Tests/Environment/RunTeyvatStageLab.ps1'
```

Run these sequentially, waiting for each Unity process to finish. The source generator replaces its blend/FBX/manifest and imported contact files. Preserve manual revisions and `.meta` files first. The default run builds the stage, contact studies, deterministic flow frames and transition evidence. `-SampleOnly` reduces capture scope; it is not a substitute for the full motion/camera review.

After a successful full run:

```powershell
& 'Tests/Environment/RunTeyvatStageLab.ps1' -VerifySaved
```

Use `-Playback` for the separate saved-scene actual Play Mode run. Read `COMPLETE.txt`/`FAILED.txt`, `StageValidation.json`, `PrefabValidation.json`, the depth reports, `CameraSweep.json` and `Playback/PlaybackValidation.json` together. The cold process opens the saved scene without rebuilding, repeats checks and exports `TG_WP07_IntegratedStage.unitypackage`. It includes WP06 review-scene dependencies used as rebuild inputs. Full regeneration expects the repository's accepted lane folders and installed Unity/URP versions; the prefab can be imported into an equivalently configured URP project.

Contact probes check imported curtain-core vertices against corrected native ground and closed opaque rocks, excluding intentional edge/end joins. They do not prove every triangle pair is disjoint. Front/side/base studies and motion frames were visually inspected. Actual animated combat and arbitrary overlapping transparent effects still require a real-game review. Fine-leaf/foam polish, WP08 lighting and standalone performance on a named minimum PC remain outstanding.

## Verification evidence

| Check | Recorded result |
| --- | --- |
| Water/cliff contact | 7,153 imported curtain-core vertex probes; zero inside tested rocks or below native ground, warm and cold. |
| Combat floor/cameras | Six anchors retained, maximum height difference 0.00054932 m; all 17 registered camera transforms/FOV retained. |
| Camera envelope | Eight orbit legs / 264 positions; zero ground or conservative opaque proxy-head bounds violations. Thirty-two transition captures. |
| Standalone prefab | Empty-scene instantiation with one globals/meadow/binding, no review camera/light/tour/actor and no missing materials/scripts. |
| Translation/lifetime | All 27,737 matrices and 272 cell bounds match after XYZ translation and return, with zero numerical error. Clone lifecycle and canonical fingerprints pass. |
| Depth | Actual river-shader GPU checks pass for perspective and orthographic cameras, including fixed fallback. Maximum errors 0.00006581 m / 0.00000115 m. |
| Actual Play Mode | Saved-scene domain reload, 24 water + 24 tour frames; shared clock advances 3.8443 to 13.3707 s, grass submits, one clone/clock. Review capsules/controller. |
| Motion | 60 fixed-time frames encoded at 15 fps, plus Play Mode sequences encoded at 6 fps for viewing. Encoding rate is not runtime FPS. |

[Waterfall motion](../../Artifacts/TeyvatGrassland/WP07/Revision01/TG_WaterfallMotion.mp4), [live Play Mode water](../../Artifacts/TeyvatGrassland/WP07/Revision01/TG_WaterfallPlayMode.mp4), [Play Mode camera tour](../../Artifacts/TeyvatGrassland/WP07/Revision01/TG_StageTourPlayMode.mp4). These are Unity renders with no UI or composited concept repaint.

The assembly has 52 water/effect renderers and 48,710 source triangles before culling. This inventory is not a GPU-time or overdraw measurement. Runtime collision is the native TerrainCollider; visual cliffs, rocks, ruins, foliage and water are noncolliding scenery. An explorable/climbable world needs separately authored collision.

The main game remains Built-in/Gamma. WP08 owns final lighting/atmosphere and character/VFX appearance; WP09 owns actual combat/controller integration and named-PC performance. This integrated art stage does not establish final Genshin texture/lighting parity. See the [preservation report](../../Artifacts/TeyvatGrassland/WP07/Revision01/PreservationCheck.json), [package inventory](../../Artifacts/TeyvatGrassland/WP07/Revision01/PackageContents.json) and [reload image comparison](../../Artifacts/TeyvatGrassland/WP07/Revision01/VisualReloadComparison.json).

Delivery: 139 assets in the Unity package, plus 22 editable-source files. Preservation fingerprints match for all 3,664 protected prior-art/gameplay/settings files. Of 35 warm/cold camera comparisons, 29 are pixel-identical; the other six differ by at most 0.00000255 mean RGB units on the 0–255 scale.
