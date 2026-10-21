# Osaka Windows visual review

Reviewed 2026-10-19. Compared all six 1920 x 1080 Windows captures in `Player/Native/Battle_After_*.png` with matching captures in `Player/OsakaBaseline/Battle_Before_*.png`. These are the native URP candidate and the separate Built-in baseline; old Editor loading-fallback captures were not used.

**Result: scenery and shader availability pass at the sampled camera positions; original lighting/material appearance is not yet fully matched.** Do not describe this comparison as pixel-identical or as verified zero visual change.

| View | Observed comparison |
| --- | --- |
| Planning | Buildings, crowd, trees, benches, bicycles, signs, barriers, and arcade interiors remain visible. The candidate has a substantially brighter cyan-white pavement reflection at lower right, plus brighter small warm highlights at the opposite curb and near edge. The diffuse street and crowd-side pavement appear darker. Glass remains transparent, though its reflected/interior light balance changes. |
| Reverse | Scenery silhouettes and sign lettering are retained. The candidate has a much brighter cyan-white highlight at lower left around the service cover and a brighter warm pool at lower right. Pavement/facades have deeper dark areas and different shadow contrast. |
| Left | Buildings, trees, benches, signs, and sky are retained. Foreground shop facades and sidewalk are darker in the candidate; shadow patterns change. Emissive cyan/yellow windows and signs remain recognizable. |
| Right | Scenery is retained. Shop walls and window surrounds, especially the large building behind the ramen sign and the red arcade building, are darker. The baseline has more ambient facade readability and a different warm pavement pool. |
| Rear | Skyline, signs, trees, benches, ropes, and star sky remain present. The candidate has deeper dark patches across middle/foreground paving; the baseline has a lighter, more continuous paving value and different long shadows. |
| Attack | Arcade facade, cabinets, crowd, bikes, and trees remain visible. The candidate has darker street/facades and a changed balance between cyan highlights and the broad warm road reflection. Pavement normal detail is still visible; no obvious texture-resolution loss is apparent. |

No cyan loading material, magenta error shader, obvious missing prop, or large culling hole is visible in these six captures. Neon hue/lettering and the purple star sky broadly retain their appearance. This is evidence for these fixed views only; it cannot exclude transient culling/LOD popping during camera motion or animation.

## Remaining quality issue

The wet-pavement specular hotspots and darker diffuse/ambient surfaces are material visual differences. They block an unconditional claim that the original approved Osaka appearance was preserved. Screenshot evidence alone does not isolate the cause: URP light attenuation/BRDF, reflection/probe response, and ambient/shadow configuration should be checked separately.

Calibrate the Osaka lit-surface response against Planning and Reverse first, then recheck all six views. Preserve the authored textures and normal detail, glass transparency, neon/glow, and sky. A global exposure increase alone would also brighten the already strong signs/reflections and is unlikely to resolve both the darker facades and brighter pavement hotspots.

No source assets, scene settings, or Unity state were changed by this review.
