# Teyvat Grassland Battle Concepts

Current layout revision: [WP01 R3 terrain composition redesign](WP01_LayoutR3.md). This user-directed revision adds river islands, layered cascade shelves, deeper cliff benches, sloping perimeter terrain and taller grass at 40–60% of the review actor height. It supersedes the older surrounding-land and short-grass constraints; prior assets remain available as historical revisions.

Production workflow added 2026-10-18: [Production pipeline](PRODUCTION_PIPELINE.md), [separate work-package briefs](WORK_PACKAGES.md), [asset contract](ASSET_CONTRACT.md), and [review checklist](REVIEW_CHECKLIST.md). These documents specify future map production; they do not claim the map is implemented.

WP00/WP01 implementation started: [visual brief](WP00_VisualBrief.md) and [layout/camera evidence](WP01_LayoutAndCameras.md). A separate Unity foundation scene, layout prefab, camera records, placement mask and static review captures now exist. These are blockout assets, with final art and materials assigned to subsequent packages.

Foundation results: [WP00/WP01 status](WP00_WP01_Status.md) and [rendering decision](WP00_RenderingDecision.md). The isolated URP comparison is complete; the main game retains its current renderer pending migration validation.

WP02 results: [shared material lab and handoff](WP02_MaterialLab.md), [implemented material contract r1](WP02_MaterialContract.md). Blender-authored sample meshes, native TerrainData, grass/leaf/flower wind, matching terrain/mesh stone, and depth-based turquoise water now have an isolated URP/Linear review scene. These shared interfaces support the separate WP03–WP06 art lanes; they do not represent the finished map.

WP03 results: [world geometry handoff](WP03_WorldGeometry.md). The accepted battle concept and [four-angle terrain reference](04-terrain-layout-multiview.png) define layout; real-game screenshots guide materials only. Editable Blender world modules and a separate Unity world review preserve the native WP01 terrain heights, anchors and cameras.

WP04 results: [meadow handoff](WP04_Meadow.md). Editable grass/flower meshes, seeded clustered placement, shared wind/color and a separate Unity meadow review. The user's 2026-10-18 direction permits short grass throughout the battlefield and at character positions; taller grass and flower patches remain concentrated around the edges. This supersedes the earlier bare-center interpretation of the green mask while preserving its original asset and the accepted terrain heights/layout.

WP05 results: [tree and canopy handoff](WP05_Trees.md). Six editable species with three distance levels replace all 120 R3 tree placeholders, with 70 root-cluster shrubs, a new canopy review scene, shared wind and neutral/afternoon captures. The R3 tall-grass policy remains current. Source, imported data, camera paths and independent saved-scene reload pass the recorded technical checks; final set dressing, water, lighting and player performance remain later work.

WP06 results: [water and waterfall handoff](WP06_Water.md). The new water review replaces the R3 water placeholders with depth-tinted turquoise river water, three irregular flowing cascades, six catch pools, contact/impact foam and restrained mesh mist. An independent cold scene check and perspective/orthographic GPU depth fixtures pass. Full40-renderer and reduced10-renderer prefabs are delivered; final cliff-contact dressing, actual combat VFX/lighting and player performance remain integration work.

Generated with the built-in image-generation tool on 2026-10-12, using the user's four Genshin Impact reference images. These are raster visual-development concepts for subsequent Blender modeling and Unity implementation, not screenshots of an implemented scene. Camera compositions are guided by BattleStagePresenter planning and attack camera logic; image generation does not provide exact camera calibration. Combatants illustrate scale and framing.

## Deliverables

- 01-environment-overview.png: environment and modeling reference.
- 02-battle-planning-camera.png: elevated view behind the allied row.
- 03-attack-camera.png: close view behind the attacker.

All images omit UI, logos and text. Generated views are approximate visual references rather than a dimensionally validated 3D reconstruction.

## Final prompts

### Environment overview

Use case: stylized-concept.
Asset type: high-end 3D game environment concept for modeling a Unity turn-based battle stage.
Input images 1-4 are HARD visual references, not edit targets. Match their Genshin Impact Teyvat / Mondstadt visual language very closely: luminous emerald and lime meadows, angular sculpted cool-gray cliffs, softly shaded full tree canopies, turquoise water, white-yellow wildflowers with scattered purple flowers, distant blue atmospheric mountains and cumulus clouds. Create a new cohesive landscape using these references.
Primary request: a beautiful believable real-time stylized 3D battle arena in a Teyvat grassland, shown as a single wide landscape image, no collage. Environment overview from a slightly elevated oblique viewpoint with horizon and expansive background visible, good for understanding how to model the level.
Arena geography to keep consistent for later alternate camera views: a gently oval, naturally worn short-grass combat clearing on a meadow terrace; tall flowing grass and flowers bordering rather than filling the combat floor. Beyond its far edge a shallow turquoise stream runs along the base of a wide stepped gray cliff wall with three waterfall ribbons. On the clearing's left boundary a large rounded leafy tree and small angular boulders. On the right boundary a low mossy broken stone arch and purple flower patch. Beyond the cliff a slender distant weathered stone tower on a grassy ridge, distant rolling mountains. The clearing is spacious, contiguous and walkable; smooth grass transitions, no artificial arena ring or tiled platform.
Style: polished Genshin-like in-engine 3D environment with designed faceted rocks, painterly clean albedo surfaces, fine grass blades, lush layered foliage, coherent physically plausible scale. Beautiful warm late-afternoon sunlight, cool teal-blue soft shadows, clear blue sky with cream clouds, restrained bloom, distant haze. Rich color without blown highlights or plastic material. Visually ambitious yet plausible to construct in Blender and render in Unity. Readable shapes and no exaggerated perspective.
Environment only in this overview, no characters.
Constraints: absolutely NO UI, HUD, minimap, health bars, menus, cards, icons, typography, title, logo, watermark, annotation, labels, border, editor gizmos, wireframe or text. Do not copy UI or logos from references.

### Battle planning camera

Use case: stylized-concept.
Asset type: 3D game environment and battle-camera concept.
Input image: the approved generated arena overview, used as a STRICT environment, art style, lighting and geography reference.
Generate a new single wide landscape concept frame of THIS SAME arena from a turn-based battle planning camera, with combatants present, absolutely no UI.
Preserve the same luminous Genshin Impact Teyvat grassland look, large rounded tree and boulders on arena left, mossy broken arch and purple flowers on right, broad gray stepped cliff and turquoise waterfall-fed water beyond the far edge, distant Gothic stone tower, warm afternoon sunlight, creamy clouds and distant blue mountains. Preserve the relative geography; only change camera and add staged fighters.
Camera composition closely guided by the Unity project: centered behind the allied row, roughly 17 meters back and 13.5 meters above the arena center, looking down toward the center at a moderate downward pitch. Perspective camera, not isometric, no diagonal sideways orbit. Include the complete playable clearing and enough cliff/waterfall/horizon for the landscape to remain beautiful. It should feel like a playable turn-based battle shot rather than a scenic postcard.
Six small anime-stylized 3D combatants with grounded contact shadows: three allies in the near row viewed from behind, facing three opponents in the far row with ample clear ground between rows. Allies: central blond swordsman in ivory/navy clothing with a short cape, left companion in forest-green light armor, right companion in cream and pale-blue mage clothing. Opponents: three humanoid bandits in muted dark rust and charcoal tunics, faces toward camera, distinct stances. All six complete bodies visible, natural human scale against trees and cliffs, evenly spaced enough to read individual silhouettes, unobstructed legs. Combat floor short grass, taller grass and flowers only at perimeter. Staged ready-to-fight moment; no big effects.
Rendering: cohesive polished real-time Genshin-like 3D environment, detailed grass border, designed faceted rocks, soft stylized trees, restrained bloom. Avoid photorealism and flat 2D painting.
Constraints: NO UI, HUD, minimap, health bars, menus, turn indicators, cards, icons, text, labels, logo, watermark, borders, editor gizmos, selection circles, grid, annotations, collage.

### Attack camera

Use case: stylized-concept.
Asset type: in-combat attack-camera concept of a 3D Genshin Impact Teyvat grassland battle stage.
Input image 1: strict environment reference of approved arena. Input image 2: strict environment and combatant identity reference, showing the battle planning view. Generate a NEW camera angle of that SAME arena and same combatants, not a collage.
Primary request: cinematic actual-fight view matching the project's attack camera composition, behind and slightly beside the central blond swordsman, near shoulder level, about 52-degree field of view. Attacker in the lower-left foreground seen from behind in three-quarter view, his ivory cape and navy outfit clearly recognizable from image 2. He lunges forward across the short-grass clearing, sword extended with a restrained pale gold motion arc. The central dark-rust/charcoal bandit is ahead in center-right midground, fully visible from head to feet, reacting in a defensive fighting stance with a curved blade. Two remaining enemies are further behind near their established row; allies may be out of frame naturally. Natural anatomy and physically coherent sword interaction. Combat clear and energetic but environment remains the primary art-direction subject.
Preserve the arena's geography and light: short grass clearing, taller lush grass and white-yellow and purple flowers confined to borders, turquoise water beyond the enemy row, wide faceted gray cliff wall with waterfalls, soft green trees, mossy arch on the right boundary, giant leafy tree on left boundary, distant stone tower ridge and blue mountains. The lower camera makes the waterfall cliffs feel tall and dramatic. Bright warm late-afternoon sunlight and cool shadows, clear blue sky and cream clouds. Atmospheric depth, grass blades and coherent designed rock forms, high-end stylized real-time 3D rendering matching Genshin Impact. Slight foreshortening but no ultra-wide distortion. Keep the defender unobstructed by attacker; clear ground between them. Sharp environment details with only restrained motion blur on sword trail. No excessive magic explosion obscuring level design.
Constraints: absolutely NO UI, HUD, minimap, health bars, names, menus, cards, buttons, icons, text, title, logos, watermark, labels, border, annotation, grid, editor gizmos. No blood or gore. Single wide landscape image.

