# Dungeon reward layout review — 2026-10-11

Native Unity UI Toolkit captures use an isolated preview fixture and detached sample inventory/receipt. They do not write the live player account or run automated tests.

## Reviewed layouts

- 1920 × 1080: six preview boxes fit one row; no preview ScrollView; Fighter Preview remains visible beside the heading.
- 1280 × 720: the six boxes and heading action fit without clipping or scrolling.
- 768 × 1024: the six boxes fit one row in the portrait panel.
- Granted rewards: larger tiles remain in a horizontal ScrollView, with no wrapping. The mixed-quality sample receipt places Gold before Purple before Blue before Green before Grey; artifacts use their instance rarity.
- Basket tooltip: displays both eligible sets, all-five-slot eligibility, maximum stars, chance, and Dust bonus; constrained to the screen.

## Build status

Shared core Release build completed with zero warnings and zero errors. Main Unity editor refresh/compilation completed; console returned no error or warning entries and editor state was idle.

Gameplay drop distribution and receipt replay tests were not run. The basket and sorting rules were reviewed in source; the capture is visual evidence, not a statistical drop validation.

## Captures

EntryEasy, EntryNightmare, BasketTooltip, and GrantedRewards each have desktop, compact, and portrait PNGs in this directory.

## Tooltip update — landscape only

Reviewed the updated preview and tooltip layouts at 1920×1080 and 1280×720. No portrait captures were generated in this update; existing portrait images are historical.

- Artifact preview tiles show icon and stars without a +0 footer.
- Shared cards use three panels: rarity name bar, icon/type/stars preview, and parchment content.
- Basket tooltips show both eligible sets with separate 2-piece/4-piece lines. Instance tooltips show level, main stat, substats, set effects, and favourite control.
- CurrencyTooltip and ArtifactTooltip captures demonstrate ordinary-item and instance-artifact cards.
- Unity compilation completed with no console errors. Existing compiler deprecation/unused-field/reference-comparison and serialization-depth warnings remain elsewhere in the project.

No automated tests or live-account writes were performed.
