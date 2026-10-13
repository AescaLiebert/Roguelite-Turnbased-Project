# Main Menu refactor review

The Unity 6000.3.4f1 fixture compiled the current runtime and editor sources and rendered Main Menu, Dungeon Entry and Dungeon Route Map at 1920×1080, 1280×720 and 768×1024. All nine screen checks passed. Thirty-nine captures include active/empty menus, three Backpack tabs, quick tooltips, Easy/Nightmare reward samples and fighter previews. No fixture account, formation, run or scene was saved.

## Requirements checked

- Shared UXML/USS references resolve to their current Resources paths and GUIDs. XML and relative/project references were checked across 27 documents/styles. Unity reported no UXML/USS import or runtime compilation errors. The shared shell selector syntax is repaired; actual backgrounds and primary button colors were checked in the renderer.
- Translucent header with readable Diamond, Coin and account channels; balances update on inventory change. Desktop logo is no longer clipped.
- Backpack is a separate Home action. Artifact, Material and Character Dupe tabs use boxes, quantity labels and quick tooltips. Five rarity instances remain separate; phase materials are Blue and series materials Purple. Character Dupe reads existing surplus tokens. Browsing, repeated open/close and Escape leave fixture inventory unchanged.
- Dungeon card shows the bound run's dungeon name, arena picture, current stage and difficulty. Active runs show Continue Dungeon; absent or completed/defeated/abandoned runs hide and disable the card. Runtime binding reads only the current account's saved run. The existing shared dungeon arena picture is reused; distinct art per series is not authored.
- Difficulty is 1–5, Easy through Nightmare. Front-page samples show one highest artifact tier, sorted by configured value priority alongside currencies/materials. Tooltips state the drop rules. Fighter Preview and Enter Dungeon remain within their fitted panel; controls and reward labels do not overlap.
- Both currencies use Gold frames. Common/Uncommon/R/SR/SSR use Grey/Green/Blue/Purple/Gold. Phase/sub-phase materials use Blue; series materials use Purple.
- Battle/Elite/Boss victory receipts grant fixed currencies, independent material ranges and optional unique artifact instances. No separate clear receipt is generated or delivered. Existing R/SR/SSR enum values and legacy save fields are retained. Already earned balances are preserved.

## Evidence and limits

`Verification.txt` contains renderer checks. `UnityRender.log` contains compilation/import output. Images were visually reviewed for menu/header readability, Backpack grids and compact tooltips, rarity colors, dungeon control spacing and action visibility.

Core reward verification passed 3,312 focused assertions. See `Tests/CombatPresentation/DungeonRewards.md` for starting amounts, probabilities and the future balance plan. Item icons are provisional procedural symbols; artifact equipment/stats remain a future system. The current Unity MCP connection was unavailable, so verification used a separate hidden Unity fixture rather than interaction with the user's open Editor.
