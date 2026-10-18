# WP00 / WP01 Foundation Status

Updated 2026-10-18. Target: Windows PC, as confirmed by the user.

**WP01 revision 2 is ready as a static layout foundation.** The four-angle terrain reference guided the connected terrain/cliff revision. Generated reports and multi-angle captures were reviewed; finished art and runtime acceptance remain later work. [Revision 2 results and handoff](WP01_Revision02.md).

| Work | Status | Evidence |
| --- | --- | --- |
| Rendering baseline and visual brief | Implemented | [Visual brief](WP00_VisualBrief.md), baseline captures and hardware report |
| Isolated URP/Gamma/Linear comparison | Completed | [Rendering decision](WP00_RenderingDecision.md), trial completion markers, captures and material probes |
| Rendering foundation for WP02 | Selected | URP 17.3 Forward + Linear for the isolated next-stage material sample |
| Whole-game URP migration | Pending compatibility/playback work | Main game remains Built-in/Gamma |
| Revision 1 layout and anchors | Implemented, retained as baseline | Separate original review scene, prefab and `TG_Layout.json` |
| Revision 1 camera registry and static review | Passed for revision 1 only | 13 views; both exact planning views contain six full actor bounds; eight sampled sweeps have no detected center-view obstruction |
| Revision 2 terrain and cliff composition | Implemented; foundation reviewed | Editable TerrainData, connected cliffs, irregular basin, three falls, cathedral plateau and grounded aqueduct; 17 captures |
| Revision 2 skybox and terrain-layer copies | Implemented | Review copies of Fantasy FS003 Day Sunless and three terrain layers; installed source assets remain separate |
| Revision 2 camera and terrain validation | Static diagnostics pass | 17 views without detected environment obstruction; both exact planning views contain six full actor bounds; 264 clear path samples; all six anchor ground errors +0.0003051758 m |
| Foliage placement/exclusion handoff | Revision 2 saved; final distribution pending | Mask PNG and channel/bounds documentation under FoundationR2; future foliage must recheck terrain and cameras |
| Revision 2 URP validation | Not rerun | Current revision captures use Built-in/Gamma; preview water shader needs a WP02 URP port |
| Finished models, grass, leaves and water | Planned for subsequent packages | WP02–WP06 |
| Actual combat-animation camera playback | Pending integration | Static proxy shots do not establish runtime framing |
| Player-build performance acceptance | Pending | Windows PC chosen; minimum hardware and frame-rate goal still to be finalized |

## Review scene

Open `Assets/Project/Scenes/EnvironmentReview/TeyvatGrassland/TG_LayoutReview_r2.unity`, with its own art under `Assets/Project/Art/Environment/TeyvatGrassland/FoundationR2/`. Evidence is saved under `Artifacts/TeyvatGrassland/WP01/Revision02/BuiltInGamma/`. The scene generated and the editor compiled without errors. Use `Fighting Allstar > Teyvat Foundation > Build WP01 Reference Revision 2` to regenerate it; duplicate and relink TerrainData first if preserving manual sculpt changes.

The retained revision 1 scene is `Assets/Project/Scenes/EnvironmentReview/TeyvatGrassland/TG_LayoutReview.unity`. Its `Review cameras` root contains the exact planning cameras and comparison/clearance views. Both review revisions are separate from the Battle/Osaka gameplay scene. Revision 2 preserves the copied source anchors, flat combat ground and exact baseline planning formulas.

The isolated URP project contains its own copy of that scene and the tested renderer/material configuration. Do not interpret the main review scene's placeholder Standard materials as final URP shader assets.

## Retained revision 1 captures

- Main current-renderer planning: `Artifacts/TeyvatGrassland/WP01/BuiltInGamma/TG_PlayerPlanning.png`.
- Main lower-angle composition comparison: `Artifacts/TeyvatGrassland/WP01/BuiltInGamma/TG_ReferencePlanningCandidate.png`.
- URP/Linear planning: `Artifacts/TeyvatGrassland/WP00/RenderingTrial/URP_Linear/TG_PlayerPlanning.png`.
- URP/Linear composition comparison: `Artifacts/TeyvatGrassland/WP00/RenderingTrial/URP_Linear/TG_ReferencePlanningCandidate.png`.
- Whole-stage modeling overview: `Artifacts/TeyvatGrassland/WP01/BuiltInGamma/TG_EnvironmentOverview.png`.

## Current revision 2 evidence

- Battle-camera composition: `Artifacts/TeyvatGrassland/WP01/Revision02/BuiltInGamma/TG_ReferencePlanningCandidate.png`.
- Whole-stage overview: `Artifacts/TeyvatGrassland/WP01/Revision02/BuiltInGamma/TG_EnvironmentOverview.png`.
- Modeling views: `TG_ReferenceFrontLeft.png`, `TG_ReferenceFrontRight.png`, `TG_ReferenceSide.png`, `TG_ReferenceRear.png` in the same evidence folder.
- Detailed diagnostics: `CameraEnvelopeReview.json`; source-preservation summary: `RevisionVerification.json` in the same folder.

## Scope and verification

This work establishes the 3D layout and data interfaces. Revision 2 improves the actual landscape massing and editable terrain against the reference; finished materials, grass, tree foliage, production water, mist and final lighting remain later work packages. Numeric landscape dimensions, additional camera poses and review tolerances remain starting values with camera/terrain checks.

Revision 2 records matching source Battle/camera hashes, copied anchors and exact planning shots matching revision 1, both exact planning views passing conservative actor-bounds checks, all six anchor terrain heights within the starting 0.02 m diagnostic tolerance, zero detected torso obstructions across 17 views, and zero blocked-center samples across eight paths with 33 samples each. These static reports do not establish animation playback or continuous camera clearance.

Final front/side/rear captures show the extended faceted terrain and three visible falls. Remaining cap/terrain blend lines, abstract canopy and stone shapes, short headwater-channel geometry, aqueduct support mounds and sky/exposure matching belong in the next art passes. The current preview water has procedural animated ripples/flow, but it uses a Built-in-only shader and has not been accepted in URP.

The scene reports 86,540 MeshFilter triangles (excluding native TerrainData) and 839 renderers. These are editor scene observations, not runtime budget or player-build acceptance. The native terrain is 384 by 384 m, with a 70 m vertical range, local base -10 m, a 513 heightmap and three cloned layers. These authoring choices remain starting values, not universal performance targets.

The main project retains its existing Built-in/Gamma rendering configuration. WP00's selected URP/Linear foundation remains the isolated next-stage material direction; this layout revision does not perform a whole-project renderer conversion.

See [layout/camera review](WP01_LayoutAndCameras.md) for exact versus proxy camera distinctions, test limitations and the handoff to each asset lane. The next authorized work here was WP00–WP01; no full WP02 material-production work has been started.
