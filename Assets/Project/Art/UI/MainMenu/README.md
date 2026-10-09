# MainMenu reference implementation

Reference: `Doc_7DSGCCopyCat/Docs(Template)/3_Outputs/Reports/visual-design/fighting-allstar-main-menu-mockup.png`.

The UXML contains native labels and buttons. MainMenuView scales its 1672 × 941 reference canvas uniformly and centers it inside the device safe area. Other aspect ratios receive dark letterboxing so no controls or portraits are cropped.

`ArenaBackground.png` is a clean plate made with the built-in image-generation tool. It preserves the reference fighter and arena while removing interface overlays. `Assets/Project/Resources/UI/MainMenu/ReferenceAtlas.png` is an unchanged copy of the reference, sampled directly for the logo, route thumbnail, portrait artwork, and small decorative graphics. Mesh UV regions select artwork; names, roles, wallet, constellation, and actions are separate live UI.

Kyo is the deliberately featured fighter from the reference. The account's Kyo constellation appears in his caption. The four cards show saved account formation when available; otherwise they show the owned starter lineup preview, explicitly labeled STARTER LINEUP. The project currently chooses run formation in dungeon entry/loadout. No inventory, formation, or progression rules were changed.

MainMenu retains the existing actions: battle → Combat dungeon entry, summon → Scene-Gacha, training → existing fighter picker. Account balance and constellation replace illustrative mockup values; unavailable inventory displays an em dash. The reference's C0/5 was updated to the project's six-tier constellation display.

Fonts: Barlow Condensed ExtraBold Italic and SemiBold Italic, distributed under the accompanying SIL Open Font License. Sources:

- https://raw.githubusercontent.com/google/fonts/main/ofl/barlowcondensed/BarlowCondensed-ExtraBoldItalic.ttf
- https://raw.githubusercontent.com/google/fonts/main/ofl/barlowcondensed/BarlowCondensed-SemiBoldItalic.ttf
- https://raw.githubusercontent.com/google/fonts/main/ofl/barlowcondensed/OFL.txt

## Clean-plate generation prompt

Use case: precise-object-edit. Input image is the edit target. Create a clean production background plate for this exact game main menu at the same 16:9 composition. Remove ALL foreground UI overlays, logo, top navigation bar, currency/profile, headline and season and tagline text, route preview card, red CTA button, all four bottom-left portrait cards, fighter caption, summon/practice buttons. Inpaint those regions with the underlying arena environment or fighter clothing as appropriate. KEEP THE MAIN LARGE RED-AND-BLACK JACKET MALE FIGHTER EXACTLY THE SAME face, pose, scale, clothing, position, and silhouette, and keep all visible arena architecture, sunset, flags, banners, spectators, lighting, palette, dark left side and orange/purple right side exactly as source. Do not add new people, do not recompose. The main fighter stays around x=900-1460 on the right, left 45% is dark arena background space. No interface frames, no readable text anywhere. This is an asset extraction/removal task, preserve original artwork as closely as possible.

## Visual and interaction checks

Compare Play Mode against the source at 1920 × 1080, 1280 × 720, and 960 × 540. Pass when all three actions, four cards, caption and account controls remain within the safe composition; text remains readable and no card overlaps an action. Check a wider aspect ratio for intentional side letterboxing. Submit Practice and close the picker; submit Summon and verify Scene-Gacha; submit dungeon entry and verify Combat. Check the Unity Console for compilation, UXML, USS, and runtime errors.

Design checks: the red route action retains first priority; roles use written ACTIVE/RESERVE labels; the account values reflect inventory; hover, pressed and keyboard focus provide visible response; the graphics match the tournament identity. No new game state transition or balance value was introduced.
