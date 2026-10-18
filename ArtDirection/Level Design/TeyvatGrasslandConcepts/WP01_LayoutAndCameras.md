# WP01 — Layout and Camera Review

Updated 2026-10-18. **Revision 2 is implemented and ready as a static layout foundation.** Its terrain, camera and multi-angle review is recorded in [Revision 2 results and handoff](WP01_Revision02.md). Finished art and runtime combat acceptance remain later work.

Revision 1 was implemented as a replaceable 3D foundation blockout and passed its static proxy review. Those results below describe revision 1 only; they do not accept the revised landscape, finished art, or actual combat-animation playback.

## Revision 2 files and scope

The revision 2 outputs are saved at:

- Scene: `Assets/Project/Scenes/EnvironmentReview/TeyvatGrassland/TG_LayoutReview_r2.unity`.
- Separate review art: `Assets/Project/Art/Environment/TeyvatGrassland/FoundationR2/`.
- Environment-only prefab: `Assets/Project/Art/Environment/TeyvatGrassland/FoundationR2/TG_FoundationLayout.prefab`.
- Editable terrain: `Assets/Project/Art/Environment/TeyvatGrassland/FoundationR2/Terrain/TG_ReferenceLand.asset`.
- Layout and camera registry: `Assets/Project/Art/Environment/TeyvatGrassland/FoundationR2/TG_Layout.json`.
- Captures and machine-readable validation: `Artifacts/TeyvatGrassland/WP01/Revision02/BuiltInGamma/`.
- Rebuild menu: `Fighting Allstar > Teyvat Foundation > Build WP01 Reference Revision 2`.

Revision 2 uses editable native TerrainData and three cloned terrain layers for the meadow, basin, plateaus and surrounding land. Integrated faceted meshes provide the steep cliff faces, with an irregular river basin and three visible waterfall volumes. A higher left plateau supports the cathedral landmark; the aqueduct connects into land. A clone of the installed Fantasy `FS003 Day Sunless` skybox replaces primitive cloud volumes. Source skybox and terrain-layer assets remain separate from the review copies.

The protected flat battle region, six copied source anchors and both exact planning-camera formulas remain the baseline. Four additional modeling angles (`ReferenceFrontLeft`, `ReferenceFrontRight`, `ReferenceSide`, `ReferenceRear`) review connected cliff depth, shorelines, side shoulders and rear geometry. These are review cameras; their poses and landscape dimensions are starting values, tuned through capture and clearance checks. The main project rendering pipeline remains Built-in/Gamma.

The validation includes native TerrainCollider surfaces and read-only TerrainData height samples at copied anchors. All six anchors have coverage and a sampled signed terrain-minus-anchor error of **+0.0003051758 m**, within the **0.02 m starting diagnostic tolerance**. All 17 registered views have zero detected environment obstruction to actor torsos; both exact planning views contain all six complete conservative actor bounds. Eight sampled paths, each with 33 samples, have zero blocked-center samples. These static diagnostics do not establish gameplay grounding, continuous collision freedom or animation acceptance.

The source Battle scene and camera hashes match the revision 2 record; copied anchors and exact planning shots match revision 1. The editor compiled the revision without errors. The isolated URP trial was **not rerun for revision 2**. Preview water has procedural animated ripples/flow using a Built-in-only shader; WP02 must port and review it for URP.

Rebuilding regenerates TerrainData heights and splat weights. To keep manual sculpt work, duplicate the review scene and its TerrainData assets, relink the Terrain/TerrainCollider references, and work on that separate copy before rebuilding. Capture loads the saved review scene alone, refuses dirty open scenes, and restores the saved editor scene setup.

Remaining art refinement includes abstract canopy/stone shapes, cliff detail and cap/terrain blending, source headwaters, aqueduct support mounds, final water and foliage shaders, and sky/exposure matching. These are documented handoff items, not claims of reference-level final art.

## Revision 1 files (retained baseline)

- Scene: `Assets/Project/Scenes/EnvironmentReview/TeyvatGrassland/TG_LayoutReview.unity`.
- Environment-only foundation prefab: `Assets/Project/Art/Environment/TeyvatGrassland/Foundation/TG_FoundationLayout.prefab`.
- Source layout and camera registry: `Assets/Project/Art/Environment/TeyvatGrassland/Foundation/TG_Layout.json`.
- Placement/exclusion mask: `Assets/Project/Art/Environment/TeyvatGrassland/Foundation/TG_MeadowPlacementMask.png`, with adjacent channel/bounds documentation.
- Captures and machine-readable validation: `Artifacts/TeyvatGrassland/WP01/BuiltInGamma/`.
- Historical build menu: `Fighting Allstar > Teyvat Foundation > Build WP00-WP01 Review`. The current builder targets revision 2; retained revision 1 files are comparison evidence, not a promise that this menu recreates revision 1.

Paths in this report are relative to the project root. This is a separate review stage; it does not replace the existing Osaka battle scene.

## Coordinate source and layout

Read the saved Battle scene through an isolated Unity preview scene. Copy the six named hero anchors without moving them. Derive the environment frame from the midpoint and direction between `CharHeroPosition2` and `EnemyHeroPosition2`.

Observed source values:

| Item | Value |
| --- | --- |
| Arena origin | X -0.1663017, Y 1.8876038, Z -10.1018934 |
| Arena forward | World +Z in the current scene |
| Ground reference | Y 1.8876038, matching the existing stage/anchor elevation |
| Player row | Local Z -5 |
| Enemy row | Local Z +5 |
| Slot X positions | Local X -5.59, 0, +5.25 |

These are observations from the current project, not proposed balance numbers. Source scene and camera source hashes are recorded in `TG_Layout.json` so future changes can be detected.

Starting layout values include the clearing extent, three-unit proxy height, river/cliff placement and landmark dimensions. These are review-scale values rather than final production dimensions. Test them with the registered cameras and actual actor bounds; enlarge the protected region or move an obstruction outward if a camera/actor fails clearance. Do not change the existing actor anchors to hide an environment error.

The revision 1 blockout contains a flat fighting area, rolling surrounding terrain, a shallow river basin and opaque turquoise surface proxy, separate cliff and waterfall ribbon volumes, a left hero canopy, right masonry arch, border rocks/flower mass markers, distant tower/bridge and reverse-side land. The terrain mesh and hidden ground reference supply collision for that review. Decorative geometry remains modular. Revision 2 replaces the wall-like cliff profile and shallow shelf arrangement with the connected terrain described above.

No finished foliage meshes, foliage shaders, animated water, mist simulation or final lighting are claimed. The simple canopy spheres, flat flower masses and waterfall bars deliberately reserve those shapes for WP03–WP06.

## Revision 1 registered cameras (revision 2 preserves the gameplay baseline)

| View | Basis | Current role |
| --- | --- | --- |
| PlayerPlanning | Exact current planning position/focus formula, source FOV | Gameplay baseline |
| EnemyPlanning | Exact opposite-side planning formula | Reverse gameplay baseline |
| ReferencePlanningCandidate | Optional lower-angle, landscape-oriented starting value | Composition comparison only |
| ReversePlanningCandidate | Opposite-side lower-angle starting value | Reverse composition comparison |
| EnvironmentOverview | Oblique whole-stage starting value | Modeling/layout review |
| PlayerAttack / EnemyAttack | Rear/shoulder formula samples using proxy scale and current attack FOV | Attack clearance review |
| PlayerContact / EnemyContact | Open shoulder-angle starting values near contact | Close action clearance review |
| PlayerPortrait / EnemyPortrait | Close portrait clearance proxies | Close scenery/detail review |
| WideMultiTarget | Broad action envelope starting value | Multi-target/ultimate clearance proxy |
| ReverseOverview | Oblique view of reverse landscape | Coverage review |

The baseline planning camera is high and steep. Its image shows less skyline than the generated concept. The optional comparison cameras explore that difference while retaining the existing gameplay camera unchanged. They are not accepted camera replacements. Revisit framing with real models and the real battle overlay before changing camera code.

Attack/contact/portrait/wide views are static proxies, not exact runtime action captures. Current action framing adapts to actor and target bounds, motion, pivots and roll. Those factors must be checked during actual integration.

## Revision 1 verification performed

- Both baseline planning views show all six complete conservative actor bounds at the recorded widescreen aspect.
- The two optional planning comparisons also show all six actor bounds.
- All registered static views have no detected environment obstruction along the ray to any actor torso.
- Sampled spherical transitions between planning, attack and contact views have no detected obstruction along the view to arena center.
- Captures were inspected for the baseline planning views, lower-angle comparison and attack view.
- A visible coplanar floor/color seam discovered during capture review was removed; the ground reference now provides collision without a second rendered floor.

The revision 1 `CameraEnvelopeReview.json` checks temporary mesh colliders for every enabled visual module, projects conservative actor bounds, and samples the current spherical orbit-position formula. It checks geometry beyond the gameplay colliders. `CameraReview.json` provides a simpler scene/ground summary. Revision 2 extends the envelope report with native terrain collision and per-anchor terrain-height diagnostics; its generated results are summarized above and in the revision 2 review record.

The sweep sampling count is a starting validation resolution. Increase it until obstruction findings stabilize when the layout changes. These checks do not prove continuous collision freedom or replace playback with real animations, moving target bounds, camera shake, actual pivots and both orbit directions.

## Handoff to the next packages

WP02 uses this layout and representative meshes to prove ground/grass color matching, canopy shading and water behavior. WP03 replaces terrain/cliff/ruin proxies while preserving the agreed frame. WP04 consumes the saved mask; red is border density, green is combat exclusion and blue is a proposed flower mass. WP05 replaces canopy volumes. WP06 replaces the water/ribbon proxies and can adjust basin shape through the integrator.

The environment prefab excludes the review cameras and fighter proxies. The scene includes those review helpers separately. The main scene/prefab integration remains WP07.
