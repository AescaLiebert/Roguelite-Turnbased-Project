# Painted rock albedo source

- Generated: 2026-10-18, using the built-in `image_gen.imagegen` tool through the imagegen skill.
- Asset: `Assets/Project/Art/Environment/TeyvatGrassland/Shared/Textures/TG_RockPainted.png`.
- Original generated output: `C:/Users/Apricha/.codex/generated_images/01a14da7-ed74-7d42-a310-13a73f3b3ef5/exec-83e91065-b0e8-466e-899e-fbce2f3b9fd1.png`.
- The project asset is an exact copy of the generated PNG. No resize, recoloring, image editing, edge correction, or derived normal-map generation was performed.
- Reference provenance: two user-supplied Genshin Impact screenshots were inspected for the broad cool painted stone palette and vertical strata: `codex-clipboard-b342c83b-f00d-42d3-8a04-8b5bfcff7ade.png` and `codex-clipboard-23197263-f5ae-4bb6-b5b1-6dc7027956a1.png`. They guided the verbal art-direction specification; their pixels were not copied or supplied as an image-edit target.
- Intended use: original stylized rock diffuse/albedo, interpreted as sRGB color, with Repeat wrapping when tiling. This file is not a normal, AO, roughness, or lighting map.
- Review status: generated candidate, visually inspected. Tile-edge continuity remains unverified until a repeated-tile and in-engine material review. The generated dark fractures also require neutral-light review to determine whether their appearance is too cavity-like or high contrast at the intended world scale. Do not claim the texture is production-approved or perfectly seamless yet.

## Exact generation prompt

Use case: stylized-concept.
Asset type: ONE seamless square 2D game material ALBEDO texture, fully opaque, edge-to-edge, intended for a Unity stylized fantasy grassland cliff.
Reference role: the two inspected user screenshots guide ONLY the broad painterly blue-gray stone color and quiet vertical strata. Create an original material pattern; do not reproduce their scene, geometry, trees, grass, sky, framing, UI, or text.
Primary request: an original tileable painterly blue-gray/slate sandstone ROCK ALBEDO map. Straight orthographic texture view, flat unlit color information only. Medium slate gray-blue overall, somewhat neutral and low saturation so the Unity material can tint it lighter. Broad soft vertical painted brush strata and broad gently varied areas of cool stone color, a few very restrained thin fractures and occasional subtle grey-beige mineral patches. Large scale hand-painted anime fantasy game surface, smooth matte diffuse coloration, very low fine noise, subdued contrast. Quiet secondary detail supports broad designed cliff geometry.
Composition: uniform material density over the entire square. Pattern must continue seamlessly from left edge to right edge AND top edge to bottom edge. No border, frame, vignette, central motif, horizon, perspective, object silhouette or isolated rock. No photographic surface scans. Color variation is pigment/albedo variation, never directional illumination.
Lighting: NONE. Do not bake light, cast shadows, ambient occlusion, cavity darkness, highlights, reflections, specular glints, bevel shading, bottom darkening, top brightening or gradients of illumination into this albedo. No rendered 3D mesh or relief demonstration. No grass, moss, foliage, water, letters, labels, watermark, game UI. No texture atlas or extra maps. Deliver one finished square albedo image.
