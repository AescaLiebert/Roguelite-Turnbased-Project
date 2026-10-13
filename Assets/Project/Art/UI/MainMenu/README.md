# MainMenu reference implementation

Reference: `Doc_7DSGCCopyCat/Docs(Template)/3_Outputs/Reports/visual-design/fighting-allstar-main-menu-mockup.png`.

The screen uses UI Toolkit flex layout with a shared account header and bottom-aligned route, Summon, and Practice actions. `Fixed1920PanelSettings` retains Constant Pixel Size at scale 1: 1920 × 1080 is the reference size, and other viewports reflow without scaling the screen. The featured fighter remains in the background plate. There is no roster preview.

`ArenaBackground.png` preserves the reference fighter and arena while removing interface overlays. `Assets/Project/Resources/UI/MainMenu/ReferenceAtlas.png` is an unchanged copy of the reference, sampled for the shared logo/avatar and small route/action illustrations. Buttons, labels, borders, hover and focus states are native UXML/USS. MainMenu retains battle → Combat dungeon entry, summon → Scene-Gacha, and training → the existing fighter picker. The shared header displays the live inventory balance.

Fonts: Barlow Condensed ExtraBold Italic and SemiBold Italic, distributed under the accompanying SIL Open Font License. Sources:

- https://raw.githubusercontent.com/google/fonts/main/ofl/barlowcondensed/BarlowCondensed-ExtraBoldItalic.ttf
- https://raw.githubusercontent.com/google/fonts/main/ofl/barlowcondensed/BarlowCondensed-SemiBoldItalic.ttf
- https://raw.githubusercontent.com/google/fonts/main/ofl/barlowcondensed/OFL.txt

## Clean-plate generation prompt

Use case: precise-object-edit. Input image is the edit target. Create a clean production background plate for this exact game main menu at the same 16:9 composition. Remove ALL foreground UI overlays, logo, top navigation bar, currency/profile, headline and season and tagline text, route preview card, red CTA button, all four bottom-left portrait cards, fighter caption, summon/practice buttons. Inpaint those regions with the underlying arena environment or fighter clothing as appropriate. KEEP THE MAIN LARGE RED-AND-BLACK JACKET MALE FIGHTER EXACTLY THE SAME face, pose, scale, clothing, position, and silhouette, and keep all visible arena architecture, sunset, flags, banners, spectators, lighting, palette, dark left side and orange/purple right side exactly as source. Do not add new people, do not recompose. The main fighter stays around x=900-1460 on the right, left 45% is dark arena background space. No interface frames, no readable text anywhere. This is an asset extraction/removal task, preserve original artwork as closely as possible.

## Visual and interaction checks

Use `Fighting Allstar > Validate Shared Screen Layouts` for real UI Toolkit captures at 1920 × 1080, 1280 × 720, and 768 × 1024. Results appear in `Artifacts/ScreenLayouts`. In Play Mode, check Practice open/close, Summon navigation, dungeon entry, repeated clicks, and keyboard focus. See `Assets/Project/UI/README.md` for the shared layout, character grid, and animation conventions.
