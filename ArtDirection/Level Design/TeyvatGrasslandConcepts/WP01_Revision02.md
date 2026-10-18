# WP01 — Revision 2 Terrain and Composition

Updated 2026-10-18. Target: Windows PC. **Status: implemented and ready as a static layout foundation.** The saved scene, generated diagnostics and final multi-angle captures were reviewed. Finished art, actual animated combat and player-build performance remain later acceptance work.

## Reference and purpose

Use the supplied four-angle diorama as the landform reference, together with the existing empty meadow, planning battle and over-the-shoulder attack concepts. The new reference clarifies the depth of the upper plateaus, connected cliff shoulders, river basin and landmark placement. It does not calibrate exact world dimensions or camera lenses.

The revision 1 view reads as a thin upright cliff wall with repeated protruding blocks. Revision 2 should read as a continuous landscape: a quiet foreground battle meadow, turquoise basin, broad broken cliffs, deep grassy shelves and distant landmarks. Review the geometry from multiple sides as well as the battle cameras.

## Saved outputs

| Output | Location |
| --- | --- |
| Separate revision 2 scene | `Assets/Project/Scenes/EnvironmentReview/TeyvatGrassland/TG_LayoutReview_r2.unity` |
| Review art and editable terrain | `Assets/Project/Art/Environment/TeyvatGrassland/FoundationR2/` |
| Environment-only prefab | `Assets/Project/Art/Environment/TeyvatGrassland/FoundationR2/TG_FoundationLayout.prefab` |
| Native TerrainData | `Assets/Project/Art/Environment/TeyvatGrassland/FoundationR2/Terrain/TG_ReferenceLand.asset` |
| Source anchor and camera registry | `Assets/Project/Art/Environment/TeyvatGrassland/FoundationR2/TG_Layout.json` |
| Meadow/exclusion mask | `Assets/Project/Art/Environment/TeyvatGrassland/FoundationR2/TG_MeadowPlacementMask.png` and adjacent `.txt` |
| Skybox material copy | `Assets/Project/Art/Environment/TeyvatGrassland/FoundationR2/Materials/TG_FantasyDaySky.mat` |
| Captures and machine-readable validation | `Artifacts/TeyvatGrassland/WP01/Revision02/BuiltInGamma/` |

Rebuild with `Fighting Allstar > Teyvat Foundation > Build WP01 Reference Revision 2`. Revision 1 assets and evidence remain available for before/after comparison. Captures load the saved scene alone so sky and scene lighting are applied consistently, refuse dirty open scenes and restore the saved editor scene setup afterward.

**Rebuilding overwrites the generated TerrainData heights and splat weights.** Before manual sculpting, duplicate the scene and TerrainData assets, relink both Terrain and TerrainCollider references, and use that separate copy. Duplicate shared layers too if changing their properties rather than only painting terrain.

## Landform revision

| Element | Implemented foundation | Capture review |
| --- | --- | --- |
| Combat meadow | Native editable TerrainData, with a protected flat center and smooth transition into surrounding land | Retain anchor height, unobstructed fighter silhouettes and clear camera movement |
| Upper plateaus | Substantial grassy shelves extending behind and above the cliff face, with a higher left mass | Terrain reads as connected land from front, oblique and side views |
| Cliffs | Integrated faceted meshes with irregular broad planes, setbacks and shoulders | Avoid regular standalone pillars, thin cube lips and open side/rear gaps |
| Basin | Carved lower land and irregular river banks, with rock islands and plunge areas | Water sits in a basin instead of appearing as a straight floating strip |
| Waterfalls | Three visible volumes emerging through upper shelf channels into the basin | Crest, descent and impact remain visible from the reference-facing review views |
| Cathedral | Silhouette seated on the higher left plateau | Landmark belongs to the terrain and clears the surrounding ridge |
| Aqueduct | Distant arched landmark connected into land | Both ends and piers have a convincing terrain relationship |
| Side and rear landscape | Connected terrain and closed cliff volumes beyond the main view | Oblique and reverse cameras reveal complete geometry |
| Sky | Review clone of installed Fantasy FS003 Day Sunless | Remove primitive cloud masses; check sky orientation and the scene sun together |

The installed terrain layers and skybox material were cloned into the review asset folder. Terrain heights, cliff meshes, waterfall volumes and landmark transforms remain editable. TerrainData supplies the continuous heightfield; steep cliff planes remain separate geometry where required by the reference silhouette. Native terrain authoring is 384 by 384 m with a 70 m vertical range, local base -10 m, a 513 heightmap, a 256 splat map and three cloned layers. These are starting authoring dimensions/resolutions, not accepted runtime budgets.

The foreground tree, right ruin and flower/grass masses establish composition only. Final foliage meshes and shading are later work. The reference's display plinth does not require converting the playable scene into a floating island.

## Preserved gameplay baseline

- Copy the same six source fighter anchors and derive the same arena frame from the source scene.
- Preserve the flat combat ground at the copied anchor elevation.
- Preserve the exact player and enemy planning-camera poses, focus formulas and source field of view.
- Retain the distinction between exact planning cameras and static attack/contact/portrait proxies.
- Four modeling angles are registered as `ReferenceFrontLeft`, `ReferenceFrontRight`, `ReferenceSide` and `ReferenceRear`; their poses are saved in the revision 2 registry. These are additional review shots rather than gameplay-camera replacements.
- Keep the existing Battle scene, gameplay camera code and main project rendering pipeline outside this layout revision.

Additional camera poses, landscape dimensions, proxy dimensions and sampling resolution are **starting values**. Test them by comparing the main masses against the references and checking the registered camera views. Move scenery outward or revise terrain when it obstructs a fighter or camera; do not move the copied fighter anchors to conceal the problem. Adjust a review camera only when its purpose and baseline distinction remain explicit.

## Generated validation and visual review

The validation source now supports deduplicated native TerrainCollider instances alongside temporary mesh collision. This prevents TerrainData from being omitted from actor-view and sampled camera-path obstruction checks.

Read-only ground diagnostics sample TerrainData at each copied anchor inside the terrain's bounds, transform the sampled surface to world height, and report signed terrain-minus-anchor error. The **starting tolerance is 0.02 m**. Every anchor needs terrain coverage; every overlapping terrain sample must fall within that tolerance. If a check fails, correct terrain coverage/height and recapture. This is a foundation diagnostic, not acceptance of runtime grounding or animated combat.

| Check | Observed result |
| --- | --- |
| Revision 2 script compilation and scene generation | Scene generated; editor compilation completed with zero errors |
| Source anchor and camera baseline preservation | Source Battle/camera hashes match the r2 record; all copied anchors and exact planning shots match revision 1 |
| Terrain coverage and sampled height at all copied anchors | All six covered; each sampled signed error is +0.0003051758 m, within the 0.02 m starting tolerance |
| Both exact planning views contain all six conservative actor bounds | Pass at the recorded 16:9 capture aspect |
| Static actor-torso obstruction review, including terrain | Zero detected environment obstructions across 17 registered views |
| Sampled planning/attack/contact camera-path obstruction review | Eight paths with 33 samples each; zero blocked-center samples out of 264 |
| Three falls visible with crest and basin connection | Visible in the reference-facing overview and comparison views; source-channel art remains coarse |
| Reference-facing massing, connected shelf depth and landmarks | Improved uneven terraces, taller left cathedral plateau and grounded aqueduct are visible; no final-art match claimed |
| Four modeling angles and reverse landscape coverage | Reviewed front-left, front-right, side and rear captures; no blocking foundation geometry gap identified |
| Main project renderer and source asset preservation | Built-in/Gamma retained; installed source layers/skybox remain separate from review copies |
| Revision 2 isolated URP rendering trial | Not rerun; revision 1 URP evidence is not revision 2 validation |

Evidence in the capture folder includes `CameraEnvelopeReview.json`, `CameraReview.json` and `RevisionVerification.json`, plus all 17 clean camera images. Exact planning cameras retain their original framing; static close-action shots intentionally do not keep all six actors in frame. No detected environment occlusion in those shots is distinct from a requirement to show every actor.

The final visual pass inspected the landscape overview, reference planning comparison, front-left, front-right, side and rear views. Visible cliffs now read as connected terraces rather than the revision 1 wall with repeated protruding blocks. All three falls are visible. Extended facades cover the exposed native steep faces; shelf joins meet the terrain without the earlier floating-strip gap. Remaining bright blend lines, abstract proxy canopies/stone, simplified headwaters and coarse bridge support mounds are handoff refinements below.

The simple summary reports 86,540 MeshFilter triangles and 839 renderers. Native TerrainData's dynamically generated geometry is excluded from that triangle total. These are editor scene observations, not GPU/CPU frame-time measurements or player-build acceptance.

Sampling does not establish continuous camera clearance or replace actual animation playback, variable attack pivots, moving actor bounds, shake, both orbit directions and action-specific FOV fitting. Ground interpolation checks do not replace runtime grounding/collision tests.

## Scope boundary and handoff

WP01 revision 2 supplies a closer terrain/layout foundation. Finished ground/rock shaders, ground-to-grass color blending, canopy shading, wind, vegetation density, production water, foam, mist, final exposure and atmosphere remain WP02–WP08. Current colors and preview water do not constitute final material acceptance.

The preview water does animate procedural ripples and waterfall flow, but its shader is Built-in-only. WP02 must port and inspect it in URP before using it as a shared production material. No temporal-quality or performance acceptance is inferred from still captures.

WP03 should refine broad cliff fractures, grass-cap/terrain blend lines, ground variation and cathedral/aqueduct meshes, including the coarse bridge support mounds. WP06 should integrate longer source headwaters, crest/channel shapes, bank transitions, foam and mist. WP05 replaces abstract proxy canopies; WP04 develops the actual meadow. Sky choice and orientation establish a useful backdrop, while final sky exposure/light matching remains WP08.

The main project remains on Built-in/Gamma. WP00's URP Forward + Linear choice applies to the isolated material-development direction; this revision does not migrate the game renderer. The export-trial source allowlist includes the new reference terrain, dressing and sky partial classes so a later isolated trial can include them; that trial has not been rerun for revision 2.

The foundation is ready for the next material/asset stages against the recorded static evidence. Animated combat playback and a measured Windows PC player build remain pending, including minimum hardware and frame-rate goals. This does not accept reference-level final art or silently carry revision 1's URP trial forward.
