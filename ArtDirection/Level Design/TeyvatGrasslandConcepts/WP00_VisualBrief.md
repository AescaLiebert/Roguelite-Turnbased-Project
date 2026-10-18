# WP00 — Visual Foundation Brief

Prepared 2026-10-18 for the supplied grassland battle concepts. Target platform confirmed by the user: Windows PC.

## Visual priorities

Preserve the image's hierarchy: a quiet walkable meadow center, lush flower/grass frame, soft tree canopy on the left, mossy arch on the right, turquoise water behind the encounter, broad gray waterfall cliffs and blue distant landmarks.

The main art-direction reference is `01-environment-overview.png`; `02-battle-planning-camera.png` defines the desired team composition and `03-attack-camera.png` defines the close action mood. The generated views are visual references, not a calibrated 3D reconstruction. The accepted 3D layout controls shared spatial decisions.

| Element | Target | Reject or revise |
| --- | --- | --- |
| Meadow center | Luminous green/yellow, short, readable coverage | Dense blades hiding feet, flat neon color, hard artificial arena border |
| Meadow border | Layered grass lengths and deliberate white/yellow/purple flower patches | Uniform random scatter or sharp ground/grass color seams |
| Trees | Connected rounded canopy masses with soft lighting variation | Disconnected cards, noisy leaf density or black undersides |
| Cliffs | Broad cool-gray planes, angular ledges, grassy tops | Cube stacking as final art or fine noise dominating silhouette |
| Water | Turquoise shallows, deeper cool regions, restrained highlights and foam | Flat cyan fill as final water or mirror reflections overwhelming actors |
| Waterfalls | Directional motion, broken flow, grounded impact foam/mist | Solid white bars as final art or transparency sorting errors |
| Ruins | Simple readable silhouette, cohesive stone/moss palette | Unrelated texture/style detail or excessive foreground occlusion |
| Depth | Clear foreground/middle/background separation, distant blue haze | Fog hiding the encounter or equally sharp/saturated distant assets |
| Light | Warm afternoon sun with cool, readable shade | Crushed foliage shadows, clipped character highlights or blanket yellow grading |

## Baseline lighting and image comparison

The foundation scene includes one directional sun, an explicitly configured ambient gradient, procedural sky and distance fog. These are repeatable starting settings for comparison; they do not close WP08 lighting. Post-processing is disabled for the baseline so material issues remain visible.

Use the registered camera positions, fixed aspect and capture dimensions, stable exposure and consistent wind state when comparing future asset changes. Review under neutral light as well as the target afternoon light during WP02. Diagnose geometry and material behavior before using grading, bloom or fog to adjust the image.

## Platform and measurements

The product target is Windows PC. The current authoring system reports an AMD Ryzen 5 4600G, integrated AMD Radeon graphics, approximately 16 GB system RAM and approximately 8 GB reported graphics memory. Reported integrated graphics memory is not equivalent to dedicated VRAM.

Use this machine as the first comparison baseline. A final minimum supported PC specification and frame-rate target are still product decisions. Until those are recorded, shader/density/texture settings remain provisional and no player-build performance acceptance is claimed.

For the rendering trial, HDR, multisample anti-aliasing, shadow settings and scene-depth support are starting settings. Check image edges, shadow continuity, water inputs and target-device frame time before retaining them. Opaque scene-copy/refraction and extra reflection cameras remain opt-in measured costs.

## Player-experience checks

The visual target must preserve target clarity and current input response. Grass, foam and mist should remain subordinate to attack feedback. The map adds no new combat rules or progression state.

Check both teams' planning views, attacks, portraits, wide actions and transitions. Use actual combat models and effects before calling the environment integrated. Existing Genshin roster entries currently point to the common placeholder prefab; their material probes do not validate detailed future character meshes, rigs or hair rendering.

## Package responsibility

The foundation stage proves rendering setup and spatial composition. WP02 proves the coherent surface/material look on a small finished sample. WP03–WP06 supply authored model, foliage and water assets. WP07–WP09 own assembly, final lighting, runtime review and delivery. Replacing the blockout with authored assets is part of that plan.
