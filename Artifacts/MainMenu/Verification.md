# MainMenu verification — 2026-10-09

- Unity 6000.3.4f1 compiled the changed C# and imported UXML/USS without errors.
- Inspected final Play Mode screenshots at 1920×1080, 1280×720, 960×540 and 1920×820.
- All three actions, all four portraits, wallet and fighter caption remain visible. The wide view preserves the composition with side letterboxing.
- At 960×540, the route button measures 396.54×48.78 screen pixels; secondary actions measure 181.34×64.27.
- Runtime panel picking resolves each action's center to its Button; decorative graphics do not intercept input.
- Submitted Practice through the UI event system: existing fighter picker appeared with 29 fighters and its Close button returned to MainMenu.
- Submitted Summon through the UI event system: reached Scene-Gacha.
- Submitted Enter Open Circuit through the UI event system: reached Combat dungeon entry.
- Final console query returned no errors or warnings.
- Removed temporary review resolutions, restored 1920×1080 and exited Play Mode. MainMenu remains the active edit-mode scene.

The screenshot uses the account's existing Diamonds balance and Kyo constellation, so these differ from the mockup's illustrative values. The hero background is an image-generation clean plate; native text is a close font/layout recreation. Asset provenance and the exact generation prompt are in Assets/Project/Art/UI/MainMenu/README.md.
