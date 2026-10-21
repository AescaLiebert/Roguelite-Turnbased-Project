# Anemo status icons

Generated with the built-in image_gen tool using existing project PNGs as visual references. Final PNGs are in `Assets/Project/Art/UI/Stats_Icon/`.

- `icon_debuff_anemo_res_down.png`: red debuff tile, white Anemo emblem, yellow downward arrow.
- `icon_buff_anemo_dmg_up.png`: blue buff tile, white Anemo emblem, cyan upward arrow.

The icon files are new art assets and are not assigned to gameplay status data.

## Reference images

- `Assets/Project/Art/UI/Elemental_Icon/Element_Anemo.png`
- `Assets/Project/Art/UI/Stats_Icon/icon_buff_cc_def_dwon_01.png`
- `Assets/Project/Art/UI/Stats_Icon/icon_dmg_add_per.png`
- `Assets/Project/Art/UI/Stats_Icon/icon_buff_atk_add.png`

## Generation prompts

### anemo_res_down

```text
Use case: stylized-concept.
Asset type: ONE square raster status icon for a turn-based Unity RPG, matching the supplied project PNG references. References are STYLE AND SUBJECT REFERENCES only; do not modify source files.
Style/medium: exceptionally clean FLAT 2D graphic with hard crisp antialiased edges, a very small palette of strictly UNIFORM SOLID COLORS. Large simple silhouette easily readable at 32px and 64px. No bevels, no gradients, no texture, no highlights, no shadows, no lighting, no glow, no 3D or fake depth.
Subject: one large WHITE Anemo wind crest, faithfully based on the distinctive winged / three-swirling-curls Anemo emblem in image 1. Remove its green circular medallion and all its shading; retain the identifiable wind-crest shape in pure white. Use the Anemo crest as the dominant hero, filling approximately 68 percent of the square. Generous thick strokes and open negative spaces.
Composition: front-on, exactly square status tile almost filling the canvas, about 4 percent transparent exterior margin, thin dark outer keyline, simple unshaded single-color inset square border. One large central emblem with one bold small directional arrow at upper right matching the existing status-icon corner-marker grammar. Arrow must remain secondary to the Anemo emblem. Treat emblem and marker as one coherent icon.
Constraints: exactly one finished icon filling the image, transparent outside the square tile only, no captions, no text, no letters, no numbers, no watermark. No mockup, no sprite sheet, no side-by-side options, no decorative particles, no weapon, no extra shield, no separate green element badge. Enforce FLAT fills and NO BEVEL, especially on border and arrow.
Primary request: generate the DEBUFF "Anemo RES Down". Image 1 defines the Anemo shape, image 2 defines the red debuff tile and its small downward corner arrow, image 3 defines the simple white-symbol readability.
Palette: flat red tile fill #D71920, flat light salmon-red border #F66B63, dark maroon keyline #71151A, white emblem #FFFFFF, single solid golden-yellow DOWN ARROW #FFE04B outlined in the same dark maroon. The arrow MUST clearly point DOWN. No red-green medallion; the white Anemo emblem sits directly on red.
The output should look like a new member of this existing icon set but with every surface completely flat and uniform.
```

### anemo_dmg_up

```text
Use case: stylized-concept.
Asset type: ONE square raster status icon for a turn-based Unity RPG, matching the supplied project PNG references. References are STYLE AND SUBJECT REFERENCES only; do not modify source files.
Style/medium: exceptionally clean FLAT 2D graphic with hard crisp antialiased edges, a very small palette of strictly UNIFORM SOLID COLORS. Large simple silhouette easily readable at 32px and 64px. No bevels, no gradients, no texture, no highlights, no shadows, no lighting, no glow, no 3D or fake depth.
Subject: one large WHITE Anemo wind crest, faithfully based on the distinctive winged / three-swirling-curls Anemo emblem in image 1. Remove its green circular medallion and all its shading; retain the identifiable wind-crest shape in pure white. Use the Anemo crest as the dominant hero, filling approximately 68 percent of the square. Generous thick strokes and open negative spaces.
Composition: front-on, exactly square status tile almost filling the canvas, about 4 percent transparent exterior margin, thin dark outer keyline, simple unshaded single-color inset square border. One large central emblem with one bold small directional arrow at upper right matching the existing status-icon corner-marker grammar. Arrow must remain secondary to the Anemo emblem. Treat emblem and marker as one coherent icon.
Constraints: exactly one finished icon filling the image, transparent outside the square tile only, no captions, no text, no letters, no numbers, no watermark. No mockup, no sprite sheet, no side-by-side options, no decorative particles, no weapon, no extra shield, no separate green element badge. Enforce FLAT fills and NO BEVEL, especially on border and arrow.
Primary request: generate the BUFF "Anemo DMG Up". Image 1 defines the Anemo shape, image 2 defines the blue damage-buff tile and simple white-symbol readability, image 3 defines the blue buff square and its small cyan corner marker.
Palette: flat blue tile fill #1566D8, flat light blue border #62B8FF, dark navy keyline #103A78, white emblem #FFFFFF, single solid bright cyan UP ARROW #4DE9FF outlined in the same dark navy. Replace the plus corner marker convention with an equally simple upward arrow. The arrow MUST clearly point UP. No green medallion; the white Anemo emblem sits directly on blue.
The output should look like a new member of this existing icon set but with every surface completely flat and uniform.
```

## Final refinement prompts

### anemo_res_down_flat

```text
Use case: precise-object-edit. EDIT TARGET: supplied icon image.
Change ONLY the surface color treatment to a perfectly FLAT, crisp, minimalist game UI icon. Preserve the square frame layout, recognizable large white Anemo crest and arrow direction/placement. Remove absolutely ALL mottling, cloudy shading, light patches, soft shadows, gradients, surface grain, noise and edge artifacts from every region. Render each region as a completely even flat solid-color fill, like a vector icon exported to PNG. White emblem must be pure solid white. All dark outlines must be one solid dark color. Frame must be a single light flat color stripe, no beveled edge. Large unmistakable wind emblem, no new symbols, no text. Clean transparent outside margin, with no stray pixels or speckles. This is a simple low-color UI asset, not an illustration of a physical badge.
This is Anemo RES DOWN: solid deep red interior, solid salmon border, solid dark maroon outlines, solid white emblem, solid golden-yellow downward arrow. Uniform solid red across the entire interior with no tonal variation.
```

### anemo_dmg_up_flat

```text
Use case: precise-object-edit. EDIT TARGET: supplied icon image.
Change ONLY the surface color treatment to a perfectly FLAT, crisp, minimalist game UI icon. Preserve the square frame layout, recognizable large white Anemo crest and arrow direction/placement. Remove absolutely ALL mottling, cloudy shading, light patches, soft shadows, gradients, surface grain, noise and edge artifacts from every region. Render each region as a completely even flat solid-color fill, like a vector icon exported to PNG. White emblem must be pure solid white. All dark outlines must be one solid dark color. Frame must be a single light flat color stripe, no beveled edge. Large unmistakable wind emblem, no new symbols, no text. Clean transparent outside margin, with no stray pixels or speckles. This is a simple low-color UI asset, not an illustration of a physical badge.
This is Anemo DMG UP: solid royal blue interior, solid light blue border, solid dark navy outlines, solid white emblem, solid bright cyan upward arrow. Uniform solid blue across the entire interior with no tonal variation. Remove the erroneous little white diamond within the crest's bottom negative-space diamond, leaving that diamond-shaped hole entirely blue.
```
+
## Buff cleanup and color revision

Edited `Assets/Project/Art/UI/Stats_Icon/icon_buff_anemo_dmg_up.png` with the existing buff as the edit target and `Element_Anemo.png` as the green-color reference.

```text
Use case: precise-object-edit.
Image 1 is the EDIT TARGET: the blue Anemo damage buff status icon. Image 2 is a REFERENCE for the exact mint-green Anemo symbol color.
Change only the center Anemo emblem from white to a single, uniform solid mint green matching Image 2 (approximately #A7F5CD). Preserve its existing shape and size. Keep the blue square, cyan upward arrow, navy outlines, light-blue flat border, layout, and transparent outside area unchanged.
Clean the icon surface: remove all dark smudges, black specks, blue blotches, stray marks, and stray pixels outside the tile. Interior blue must read as smooth solid flat blue; do not add texture, gradients, highlights, bevels, soft shadows, glow, grain, noise or any decoration. Keep all fills flat, crisp, and uniform. Preserve the current border and arrow geometry exactly; add no new symbol or text.
Output one clean flat 2D game status icon, square canvas, true transparency outside the square tile.
```
+

## Debuff emblem recolor

Updated `Assets/Project/Art/UI/Stats_Icon/icon_debuff_anemo_res_down.png` with the green Anemo emblem, retaining the red debuff tile and yellow down arrow.

```text
Precise single-color edit. Image 1 is the source status icon to edit. Change ONLY the large white Anemo symbol in the center into the same pale mint green as the Anemo symbol in Image 2. Use a uniform flat green close to #A7F5CD. Preserve its exact silhouette. Keep every other part of Image 1 exactly as shown: the red interior, red frame, maroon outlines, yellow down arrow, transparent margin, and overall layout. Do not redraw or reinterpret anything. Do not add, remove or move shapes. No new marks. No new shading, gradients, texture, blur, smudges or noise.
```

