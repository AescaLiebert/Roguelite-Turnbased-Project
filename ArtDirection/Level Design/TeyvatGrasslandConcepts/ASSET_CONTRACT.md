# Shared Asset and Integration Contract

Status: [WP01 R3](WP01_LayoutR3.md) establishes the current spatial frame and tall-grass policy. [WP02 shared material contract r1](WP02_MaterialContract.md) defines the implemented shader, import, vertex-channel, wind/color and instancing interfaces for WP03–WP06. [WP05 trees](WP05_Trees.md) now supplies the Trees lane without changing those shared interfaces. See [the WP02 handoff](WP02_MaterialLab.md) for scope, evidence and remaining work. Folder locations for later production lanes below remain planned.

[WP06 water r1](WP06_Water.md) adds Water-owned production/effect shaders and compatible material instances without editing Shared shader sources. Its versioned extension retains the WP02 water properties, adds a pool mode/quality/sky tint and records Water-specific effect phase/opacity channels. Depth-copy scheduling and the one shared clock remain unchanged. It uses sky-color Fresnel, no reflected scene geometry/refraction, and supplies a depth-independent reduced-effect prefab.

[WP07 integration r1](WP07_Integration.md) owns the complete stage prefab/master review scene and Integration-local contact meshes, native terrain/meadow derivatives and water effects. Earlier lane inputs remain unchanged. Its stage binding supports translation only with native Terrain; one globals owner lives within the complete stage, with review cameras/lights outside it. Runtime collision is native terrain only; detailed backdrop meshes remain visual scenery.

[WP08 reference daylight r2](WP08_Lighting.md) owns Lighting-local shader/material/palette copies, sky import, profiles, pipeline settings, environment prefab and a separate lighting review scene. One scene lighting rig supplies the near-white back-left key, analytic SH ambient, fog and shared-clock cloud shadows. Native terrain and mesh cliffs share the normalized TG_RockPainted texture response with coarse world projection; its .85 shared paint amount and material strength are both active. Existing TerrainData/control maps/layers remain inputs. The scene owns the sun/Volume, 45 approximate legacy actor probes, explicit static HDR reflection cubemap and current-project actor fixtures outside the environment prefab. Custom environment/water shaders do not sample that reflection probe or use full baked lightmaps. The main gameplay renderer remains Built-in/Gamma; the lighting scene is reviewed in the isolated URP 17.3/Linear host.

## Ownership and folders

Use this separation so work packages can proceed without editing each other's assets:

| Scope | Proposed location | Owner |
| --- | --- | --- |
| Editable art sources | `ArtSource/TeyvatGrassland/<discipline>/` | Respective asset author |
| Ground, cliff and ruin exports | `Assets/Project/Art/Environment/TeyvatGrassland/World/` | WP03 |
| Grass and flower exports/prefabs | `Assets/Project/Art/Environment/TeyvatGrassland/Meadow/` | WP04 |
| Tree exports/prefabs | `Assets/Project/Art/Environment/TeyvatGrassland/Trees/` | WP05 |
| River and waterfall assets | `Assets/Project/Art/Environment/TeyvatGrassland/Water/` | WP06 |
| Shared shader families and color/wind data | `Assets/Project/Art/Environment/TeyvatGrassland/Shared/` | WP02 lead; reviewed updates thereafter |
| Final environment prefab | `Assets/Project/Art/Environment/TeyvatGrassland/Prefabs/` | Integrator |
| Review scenes | `Assets/Project/Scenes/EnvironmentReview/TeyvatGrassland/` | Each lane's uniquely named sample; master owned by integrator |
| Lighting configuration | `Assets/Project/Art/Environment/TeyvatGrassland/Lighting/` | WP08 |
| Captures and measurements | `Artifacts/TeyvatGrassland/<package>/<revision>/` | Package owner |
| Briefs and decisions | This concept-art directory | Integrator |

Keep `.blend` source outside Unity's imported asset folders and export explicitly. Preserve Unity `.meta` files when revising imported assets. Do not modify existing Osaka asset identifiers, shared character materials or project settings from an asset lane. Review scenes must have distinct paths.

## Coordinate and export rules

- Use a metric authoring convention: one Unity unit represents one meter. This is a unit definition, not a gameplay tuning value. Verify scale using an actual project character.
- Keep Blender's normal authoring axes; use an agreed export preset for Unity conversion. Validate with a labeled orientation sample in WP02 instead of blindly rotating every asset by hand.
- Define arena origin, forward direction and combat ground height in the accepted layout. Store transforms relative to that frame. Do not assume the existing battle anchors are at world zero.
- Use sensible placement pivots: ground contact for rocks and trees, contact base for ruins, and documented origins for terrain tiles and water surfaces.
- Final exports must not depend on unsaved modifiers, unavailable external textures or procedural Blender shaders to render correctly in Unity. Preserve editable source graphs and export evaluated mesh data through a tested route.
- FBX is the proposed common mesh handoff. An alternative format is acceptable after a representative import proves units, normals, UVs, vertex colors and material slots. Do not change formats mid-lane without updating the sample.
- Repeated vegetation remains reusable clump/tree assets with placement data or Unity distribution settings. Do not flatten the full meadow into a single baked mesh.
- Export only the intended assets. Test cameras, preview lights and duplicate hidden objects are not final mesh content.

## Mesh, UV, vertex and mask conventions

| Data | Proposed meaning | Verification |
| --- | --- | --- |
| Primary UV set | Albedo/opacity and general surface textures | Test with shared checker and actual texture |
| Secondary UV set | Reserved for lightmaps on assets that need them | Check overlap/padding in the chosen bake workflow |
| Grass/flower vertex red | Bend weight: dark roots, brighter tips | Confirm wind leaves roots attached |
| Tree vertex red | Leaf/flutter deformation weight | Compare leaf motion with trunk stability |
| Tree vertex green | Branch-sway weight | Check branch joins for tearing |
| Tree vertex blue | Stable phase variation | Confirm no frame-to-frame reseeding |
| Vertex alpha | Foliage ambient-occlusion multiplier; ordinary vertices use 1 | A is not opacity; use base-map alpha for opacity |
| Optional extra tree pivot data | Only if the selected branch-wind method requires it | Document exact encoding before authoring the kit |
| Ground placement masks | Separate density/species/exclusion data | Overlay mask in layout preview |
| World color field | Shared ground/grass variation over the layout bounds | Compare boundary colors from battle cameras |

These vertex-channel assignments are local project conventions. Shader code and artist exports must agree on them. If a more advanced wind method needs extra data, accept its data contract and example first. Avoid packing unrelated ground masks into a foliage channel simply because that channel appears unused on another mesh type.

Keep hard/soft edges and custom normals intentional. Verify any foliage normal transfer through the chosen Unity importer settings; automatic normal recalculation can invalidate the canopy look. Normal and silhouette quality take precedence over an arbitrary demand for all-quad runtime topology.

## Material families and responsibilities

| Family | Required controls | Key limit |
| --- | --- | --- |
| Ground | Grass/soil blend, broad color variation, edge-transition masks | Do not bake the final sun shadow into albedo |
| Rock | Broad base/tint variation, controlled surface normals, grass/moss blend where appropriate | Avoid high-frequency procedural noise dominating the planes |
| Trunk | Cohesive bark color and restrained normal detail | Match canopy scale and palette |
| Grass | Root/tip tint, world color field, root-fixed wind, received-light/shadow response | Keep blade fill cost and animated bounds measurable |
| Leaf | Canopy tint, intentional normals, controlled backlighting, clipping where needed, wind masks | Verify backsides and shadow pass |
| Flower | Cohesive petal/leaf tint, optional opacity, restrained wind | Flowers remain subordinate to fighters in battle framing |
| River | Shallow/deep colors, surface motion, foam, highlights, reflection policy | Depth input/refraction behavior depends on renderer |
| Waterfall | Directional flow, breakup, opacity/foam, contact transition | Keep foam and mist layering stable |

Blender procedural material graphs are source-authoring tools. Export mesh data and texture maps explicitly; implement the final runtime appearance in the selected Unity pipeline.

Color/albedo textures use the appropriate sRGB import. Masks, normals and other numerical textures use linear/data interpretation; normal maps need the corresponding Unity texture type. Use intentional alpha import, mip settings and compression for foliage. Do not declare all maps interchangeable or pack new channels without recording the packing.

The shader owner defines actual property identifiers in the sample; other lanes make material instances without renaming the shared interface. Shader Graph may cover ordinary material authoring; custom lighting, specialized instancing or wind may need HLSL/custom functions. Pick based on the accepted sample, not a requirement that everything fit inside one tool.

## Shared wind and color interface

Provide one scene-level wind source with direction, strength and time behavior; each material has its own response and local variation. Establish the coordinate convention once. Foliage authors do not each implement an unrelated global wind clock.

Provide one world color-field asset plus arena bounds/transform data. Both ground and grass sample the same field. Seed and distribution data must be saved so a material change does not randomly move the flower patches.

Visible rendering and shadow/depth passes must agree on clipping and vertex deformation. Expand animated bounds appropriately so moving tips are not culled. Evaluate the instancing path with the actual shader and lighting combination.

## Distance, collision and lighting

- Each asset declares its intended foreground, middle or background role, relevant camera views and distance representation. Choose LOD thresholds by recorded camera tests.
- Separate static collision from detailed visual meshes. Foliage and distant scenery generally do not need gameplay collision; preserve the existing movement/ground contract.
- Record which meshes can participate in light baking, which move through shader wind, and how characters receive ambient light. Do not assume static shadow bakes update with wind.
- Water modules declare depth-texture, opaque-texture, probe, extra-camera and particle dependencies explicitly.
- A work-package sample may include preview lighting, but the exported production module must allow the master scene to own final sun, sky, exposure and fog.

## Handoff manifest

Every accepted module supplies:

- Package/revision, owner, accepted contract revision and dependencies.
- Source files, export preset, runtime assets, sample scene and regeneration steps.
- Texture packing, vertex-channel meanings, normals/import settings and material dependencies.
- Current mesh/texture statistics and measured rendering impact in the review scene; device and build/editor context labeled.
- Camera-matched neutral-light and target-light captures, with wind-on/off examples for vegetation.
- Seed/mask data and any external tool/service provenance needed to reproduce or replace the source.
- Known visual/runtime limitations and any planned fallback.

The integrator accepts or returns a module using the shared review checklist. A lane may revise its own assets; changes to shared data, layout, lighting or rendering settings go through the corresponding owner and a new contract revision.
