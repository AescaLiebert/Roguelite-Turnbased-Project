# Separate Work-Package Briefs

Spatial revision: [WP01 R3](WP01_LayoutR3.md) is the new user-directed layout for downstream work. Use its native ground, island footprints, waterfall courses and40–60% actor-height grass policy. Older WP01 r2/WP03/WP04 handoffs retain their original evidence but no longer define the revised surrounding terrain or grass-height cap.

These are implementation assignments for separate work sessions. Current results: WP00 rendering trial recorded, [WP01 layout R3](WP01_LayoutR3.md) implemented, [WP02 shared material sample r1](WP02_MaterialLab.md) delivered, [WP03 world geometry r1](WP03_WorldGeometry.md) implemented, [WP04 meadow r1](WP04_Meadow.md) extended by R3's tall-grass policy, [WP05 tree/canopy r1](WP05_Trees.md) implemented, and [WP06 water r1](WP06_Water.md) implemented with technical review evidence. [WP07 integration r1](WP07_Integration.md) is assembled with passing contact, prefab, camera and Play Mode reviews. [WP08 reference daylight r2](WP08_Lighting.md) now supplies clear daylight, the owned sky/palette, visible shared cliff paint, fixed exposure and scoped actor probes, with passing saved-scene and Play Mode checks. WP09 real-combat/player review and further art refinement remain; the final map is not complete. Read [the pipeline](PRODUCTION_PIPELINE.md) and [the asset contract](ASSET_CONTRACT.md) before starting a package.

Use separate scoped work sessions to reduce context and iteration load. Concurrent work is suitable after dependencies are accepted and ownership is clear; no requirement exists to run everything at once. A source-file handoff also works when Blender and Unity operate in different environments.

## Common instruction for every assignment

Build only the assigned TeyvatGrassland scope using the supplied concept images and current accepted layout/material sample. Inspect current files before editing and preserve unrelated work. Work against the selected renderer and recorded contract; do not choose a new pipeline independently. Deliver editable source, Unity-ready assets, a uniquely named sample scene, reference-matched captures, import/regeneration instructions and measured limitations. Finish a representative sample before expanding the kit. Resolve shared-interface changes with the integrator instead of silently adapting another package. Treat concepts as visual targets and the accepted blockout as spatial truth.

## WP00 — Rendering and visual foundation

**Needs:** concept images and current project baseline.

**Assignment:** inspect the current renderer, color space, character shaders, Osaka effects and target-device needs. In an isolated project copy, compare a URP/Linear candidate against the current rendering baseline with representative actors and scene assets. Record versions, compatibility work, visual differences and runtime impact. Establish the chosen renderer, preview light, target afternoon light, capture settings and a visual brief. Do not apply a project-wide migration to the main checkout as a side effect of the trial.

**Owns:** trial project and decision records. Proposed result: `RenderingDecision.md` and baseline capture directory.

**Complete when:** a reproducible rendering choice and compatibility path exist, with target-device assumptions clearly recorded. Renderer-specific shader production follows that choice.

## WP01 — Stage layout and camera envelope

**Needs:** WP00 decision and current battle anchors/camera behavior.

**Assignment:** create a simple fully 3D blockout of the meadow, stream basin, cliffs, waterfalls, left hero tree, right ruin, distant tower/bridge and reverse-side landscape. Preserve combat floor height and anchors. Record actual player/enemy planning views, close attack shots, portraits, multi-target shots and ultimate camera paths. Mark fighter movement, camera clearance and vegetation exclusion regions. Save shared layout transforms and masks.

**Owns:** layout sample and camera records. Proposed result: `TG_LayoutReview.unity` and `CameraSet.md`.

**Complete when:** camera motion and both acting-side views have no major occlusion or exposed scenery gaps. Integrator accepts the shared layout before full asset production.

## WP02 — Shared materials and finished sample

**Needs:** WP00 and WP01.

**Assignment:** create the shared ground, rock, trunk, grass, leaf, flower, river and waterfall material interfaces, with simple representative authored meshes. Prove the look on a small coherent sample of meadow edge, tree canopy, cliff, water and waterfall, viewed through battle and attack cameras. Verify foliage vertex masks, imported normals, shared wind, world color field and selected instancing route. Document actual shader property names, texture packing and required rendering features.

**Owns:** Shared assets and material-lab scene. Proposed result: `TG_MaterialLab.unity`, accepted contract revision and sample captures.

**Complete when:** neutral and target-light comparisons pass, shader inputs survive export/import, and the sample's runtime behavior is understood. This unlocks WP03–WP06.

## WP03 — World geometry lane

**Needs:** WP01 layout and WP02 sample/contract.

**Assignment:** build the authored terrace mesh, river basin/banks, cliff modules, waterfall openings, rocks, mossy arch, ruins and simplified distant landmarks in Blender. Keep the reference's broad faceted cliff planes and layered skyline. Use scripts for repeatable variations and exports while reviewing silhouettes manually or through captures. Deliver UVs, intentional normals, material masks, suitable collision and distance variants. Validate the kit in Unity before completing all variations.

**Owns:** World assets and matching source folder. Does not own master scene, combat anchors or shared shaders.

**Complete when:** pieces replace the blockout cleanly, water has underlying depth geometry, both camera directions are covered and all modules meet the asset contract.

## WP04 — Grass and flower lane

**Needs:** WP02 grass/flower shader sample, layout masks and shared wind/color interface.

**Assignment:** author reusable grass clumps, accent blades, meadow ground cover and white/yellow/purple flower clusters. Implement the accepted Unity instancing/distribution route using seeded density/species/exclusion masks. Match ground hue at roots, create deliberate border density, preserve a short readable combat center, and test clipping, mip behavior, animated bounds, shadow deformation and transitions to distant coverage. Add optional actor bending only after the base sample passes.

**Owns:** Meadow assets and meadow sample. Does not move the layout's exclusion zones or change global wind independently.

**Complete when:** actual combat shots show lush borders and clear fighters, without prominent shimmer, floating roots, abrupt distance rings or excessive measured vegetation cost.

## WP05 — Tree and canopy lane

**Needs:** WP02 trunk/leaf materials and accepted tree silhouette/location from layout.

**Assignment:** author the hero tree plus reusable middle/background tree variants. Design trunks, primary branches and layered leaf clumps to match the reference's soft full canopy. Verify normal-transfer/import behavior, intentional backside lighting, alpha clipping where needed, and branch/leaf wind masks. Prepare suitable distance versions and document the chosen lighting/shadow behavior for wind-deformed assets. Test in both neutral and afternoon light.

**Owns:** Trees assets and canopy sample. Does not edit the master lighting or shared wind schema.

**Complete when:** canopy volumes read clearly without harsh card artifacts or dark backs, and no camera transition enters a trunk or dense canopy volume.

## WP06 — Water and waterfall lane

**Needs:** WP02 water interfaces, WP01 flow/basin layout and representative WP03 basin geometry or accepted proxy.

**Assignment:** author river surface and waterfall ribbons with directional UV flow. Implement the selected renderer's shallow/deep tint, foam, surface detail, highlights and declared reflection/refraction method. Assemble waterfall breakup, bank contact, impact foam and restrained mist using the chosen effects tools. Test linearized depth calculations, grazing angles, both team cameras, and sorting with transparent combat effects. Deliver a lower-cost fallback for any extra-camera or heavy transparency technique.

**Owns:** Water assets and water sample. Does not silently enable global rendering features; records dependencies for the integrator.

**Complete when:** water is recognizably turquoise and layered, waterfall flow is readable, transparent ordering is stable in representative shots, and measured cost supports the target.

## WP07 — Integration and set dressing

**Needs:** accepted layout/material contract and usable increments from WP03–WP06.

**Assignment:** own the final environment prefab and master review scene. Replace blockout modules incrementally, integrate accepted lane outputs, regenerate placement from the saved masks/seeds, preserve actor anchors/ground height and check all cameras after each import. Dress flowers, rocks and ruins to match the concept's composition while completing reverse-side scenery. Keep sample-scene cameras/lights out of final modules.

**Owns:** master scene and final stage prefab; resolves cross-package interface/layout changes.

**Complete when:** the complete stage plays through camera transitions with coherent scale, palette, silhouettes, collision and module dependencies.

## WP08 — Lighting and atmospheric finish

**Delivered r2:** [reference daylight handoff](WP08_Lighting.md). Technical checks cover the existing seventeen camera poses, actual current cube actor materials, actual-source aura fixture, 45 baked legacy probes, a persistent static reflection cubemap, post-process pixel output and moving water/grass in Play Mode. Detailed character shading, full combat and named-device performance remain unverified. Existing asset shapes and sky panorama detail still limit reference similarity.

**Needs:** integrated stage and WP00/WP02 lighting baseline.

**Assignment:** refine warm afternoon sun, cool ambient shadows, sky/cloud appearance, distance haze, exposure, restrained bloom and tone mapping in Unity. Validate actual character and combat-effect appearance. Configure supported actor ambient/probe lighting, suitable static bakes and reflection probes. Evaluate cinematic depth of field separately from the baseline clean captures. Return material/normal defects to the relevant lane rather than hiding them with grading.

**Owns:** Lighting assets and final scene lighting through coordination with integrator.

**Complete when:** clean camera comparisons show stable exposure, readable fighters and the supplied reference's luminous meadow and atmospheric depth.

## WP09 — Runtime review and delivery

**Needs:** integrated lit stage and named target hardware/display goals.

**Assignment:** execute [the review checklist](REVIEW_CHECKLIST.md) through real combat and a player build. Record camera-matched captures, performance/memory evidence, worst views and remaining issues. Diagnose actual bottlenecks and send concrete changes to the owning lane. Verify a clean reimport/regeneration path and dependencies. Package editable art, exports, shaders/materials, prefabs/scenes, seeds/settings, versions, measurements and limitations.

**Owns:** review evidence and final acceptance report; stage changes remain coordinated with their owners.

**Complete when:** all required visual/gameplay checks pass and the named target-device budget is met. If hardware remains unspecified, visual acceptance may be reported separately and performance acceptance stays pending.

## Scheduling and handoff rules

Start WP00, then WP01, then WP02. After that, WP03–WP06 can progress independently within their folders. Start integration with the first accepted lane increments; maintain the same camera review set. Finish WP08 and WP09 on the integrated stage and loop back to the owning lane when a defect is found.

Use this short handoff format at the end of each work session:

> Package/revision; completed files and editable sources; sample scene; contract/layout version; captures; import/regeneration steps; runtime measurements and context; unresolved issues; next required dependency.

Only the integrator merges changes to the master scene, shared layout and scene-level configuration. This keeps separate heavy work from producing competing stage assemblies.
