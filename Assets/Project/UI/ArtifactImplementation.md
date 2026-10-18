# Artifact equipment and enhancement

Implemented from [Artifact System Design Brief](ArtifactSystemDesignBrief.md) and [Artifact Upgrade System Brief](ArtifactUpgradeSystemBrief.md).

## Player flow

- Characters → owned fighter → Artifacts. Five named slots surround the character artwork; the sixth position reserves Holy Relics, which remain unavailable under the existing game scope.
- Select an equipped slot to inspect its individual rolls and set effects. Select an empty slot or Change to insert the searchable artifact inventory into the current screen's left character area. The existing tabs and artifact preview remain in place; selecting a tile updates that same preview, where Equip commits it. The bottom of the left inventory contains a compact character preview with the selected fighter and all five current artifact slots; selecting one filters the collection. Closing inventory restores the character artwork. Inventory tiles show the owning fighter, including the selected character; front-tab equipment and the selected-character artifact preview omit repeated ownership details. Unequip retains the inspected artifact, artwork, stat rows and scroll view, changes the action to Equip, and updates live set/ownership sections.
- Equip a backpack piece to replace the matching slot and return its previous piece to the backpack. Equip another fighter's piece to transfer it into an empty slot, or exchange both fighters' pieces when the destination is occupied. The button reads **SWAP & EQUIP** for an exchange. Both owners are saved in one transaction and a save failure restores the complete previous inventory.
- Set effects keep their two-piece and four-piece descriptions. A checkmark and colored text show a matching-piece threshold is met; unmet effects are grey. Loadout summaries use compact threshold markers without activation-status sentences. Inspection uses the viewed fighter’s current equipment; standalone tooltips use the artifact’s equipped owner.
- Enhance opens the workshop. Artifact Dust and eligible artifact instances can be combined. The projected level, main and substat values, XP, Coins, milestones, and cap overflow are shown before consumption.
- An empty material tile opens the shared sorted artifact grid docked on the left of the workshop, keeping the enhancement preview and controls available beside it. The horizontal tray has 15 slots. Both manual selection and Auto Add respect this capacity. Multiple selection and separate Inspect controls are supported. Clicking a selected material in the workshop removes it. Clear resets the selection.
- Auto Add selects the lowest rarity and least upgraded eligible artifacts toward the next level, up to 15 pieces. Its maximum rarity setting is saved per account. Review and confirmation identify every consumed source instance; favourite, equipped, and target artifacts are excluded and also rejected by core rules.
- Dungeon victories display each obtained artifact separately in a horizontal scroll strip without wrapping. Select a tile for a compact stat and set tooltip. Backpack browsing exposes search, oath, slot, rarity, and favourite filters. Artifact tiles use rarity stars and frame color rather than item names. Favourite pieces carry a lock marker. Equipped item tiles show the owning fighter’s avatar, with the fighter’s name available on inspection and the avatar tooltip.

## Rules and persistence

There are 15 identities: three oaths × Flower, Feather, Sand Clock, Goblet, Crown. Rarity belongs to each instance. Dungeon acquisition uses the stage-specific pair in [DungeonArtifactBaskets.md](DungeonArtifactBaskets.md). Existing generic rarity items migrate deterministically without losing their instance or acquisition identifiers.

Every artifact has one percentage main stat from its slot's authored pool. Flower and Feather use basic percentages; Sand Clock, Goblet and Crown also admit the authored secondary rates. Common / Uncommon / R / SR / SSR have 0 / 1 / 2 / 3 / 4 distinct substat identities from the shared 19-line pool. The exact main identity cannot repeat as a substat; flat and percentage versions of a basic stat are distinct. Main-stat growth has no quality or rarity multiplier. Substat rolls multiply their authored base by quality 0.7–1.0 and rarity / 5, flooring once. [ArtifactBalanceTable.md](ArtifactBalanceTable.md) contains the complete authored table, damage-stat mapping and schema-2 migration. Inspection and enhancement preview never advance gameplay RNG.

Enhancement is guaranteed from +0 through +20. The main stat improves at every level. +6, +12, and +18 each improve one existing substat; Common skips those events, and +20 has no milestone. XP carries between levels. Excess at +20 is displayed before confirmation and discarded; Coins are charged for usable XP.

Favourite is persistent and blocks every fodder path. A favourite may still be equipped or enhanced. Equipped artifacts are also protected from consumption. A piece cannot silently move from another fighter: unequip it there first.

Equipment stats and matching-piece effects enter character stats, practice, and dungeon snapshots. Two matching pieces add 20% to their oath's basic stat; four add a second 20%. Four-piece low-HP effects apply only while current HP is strictly below 20% of live maximum HP. Both named conditional stats apply; Striker also raises outgoing damage. Battle HUDs display **Artifact Active** with the active oath and remove it immediately when its condition ends.

Dungeon fighters retain the equipment snapshot from entry. Later account equipment changes apply to a new run or a fighter's first entry, preserving active-run stats. The same artifact instance cannot be frozen onto two different fighters in one run.

Equipment, favourite, and enhancement changes clone inventory state and use the existing atomic account save. Save failure restores the previous state. Receipt grants remain idempotent. Malformed legacy inventories repair invalid equipment references, duplicate slots, invalid stat lines, negative stacks, and overflowing duplicate balances.

## Authored stats and starting economy values

The stat table is authored in [ArtifactBalanceTable.md](ArtifactBalanceTable.md). Economy and acquisition distribution remain **starting values**, kept together with the authored rules in `Shared/FightingAllstar.Core/Content/ArtifactRules.cs`:

| Rule | Starting value |
|---|---|
| Main eligibility | Flower/Feather: HP%, DEF%, ATK%; Sand Clock/Goblet/Crown: those plus 13 authored secondary rates |
| Set / slot / main / quality selection | Two-set dungeon basket, uniform eligible set / slot / main / quality draws; rarity follows dungeon difficulty |
| Main +0 → +20, every rarity | HP/ATK/DOT Dealt/Damage Dealt 7% → 46.6%; DEF/Pierce 8.7% → 58.3%; other endpoints in the authored table |
| Main scaling | Linear level growth; no quality or rarity multiplier |
| Initial and milestone substat rolls | Authored base × quality (0.7–1.0 inclusive) × rarity / 5, floor once; same 19-identity pool on all slots |
| XP for next level from level L | rarity × (100 + 30 × L) |
| Artifact Dust | 100 XP each |
| Fresh fodder XP | Common 200; Uncommon 400; R 800; SR 1400; SSR 2200 |
| Upgraded fodder recovery | 80% of invested XP plus fresh fodder XP |
| Coins | 2 per usable XP |

### Suggested tuning review (not executed)

- Economy: SSR +0 with five Dust reaches +1 for 1,000 Coins. Check whether successive dungeon sessions sustain visible progress without spending protected equipment. If progression stalls, reduce XP growth or increase Dust rewards; if +20 is trivial, increase the XP slope.
- Readability: a new player should identify the selected slot, the equipped owner from their avatar, matching-piece thresholds from checkmarks and color, the main stat, projected level, and exact consumption before confirmation. If any are missed, strengthen labels and visual grouping before tuning costs.
- Input stress: changing filters, favouriting an inspected artifact, removing selected fodder, closing with Escape, and clicking Enhance repeatedly should leave one clear state and one committed transaction.
- Protection: manual and Auto Add selections should never include favourites, equipped pieces, or the target. Changed inventory should invalidate protected sources before confirmation.
- Battle threshold: compare 19%, exactly 20%, healing above 20%, and changed maximum HP. The conditional stats and Artifact Active indicator should agree at every transition.

## UI assets

`Resources/UI/ArtifactLoadout.uxml` and `ArtifactWorkshop.uxml` provide editable UI Toolkit structure; `Artifacts.uss` supplies layout and responsive styles. `ArtifactArt.asset` references all 15 authored V3 icons. Shared artifact tiles and tooltips are rendered by `ArtifactItemView`, using the existing backpack rarity frames.

Native UI review captures are stored in `Artifacts/ArtifactSystem`: equipment, selected-slot detail, enhancement preview, consumption confirmation, docked material picker, backpack, reward strip, and reward tooltip at the supported landscape sizes 1920×1080 and 1280×720. No account files or live scenes are modified by the capture fixture. These are visual captures; gameplay and persistence test suites were not run.

## Item tooltip panels

Item and instance-artifact tooltips share a rarity-colored name bar, an icon/type/star preview panel, and a parchment content panel. Artifact set names lead separate 2-piece and 4-piece effect lines; active effects show a checkmark and green text, inactive effects remain grey. Instance tooltips retain rolled stats, level, equipped owner, and favourite protection. Dungeon basket tooltips show both possible sets without inventing rolled stats. Artifact reward preview boxes omit the always-zero level footer.

The game targets landscape. Subsequent layout review uses 1920×1080 and 1280×720 only; prior portrait captures are historical.

Instance tooltip refinement: the +level badge follows the name in the title bar; the main-stat name and value sit in the colored preview panel beside the icon. Only substats, set effects, ownership and favourite controls appear in the parchment content panel. Set-basket previews have neither an instance level nor rolled stats.

Dungeon set previews now show both eligible Flowers separately; each tooltip presents its own set effects and the shared drop basket. All artifact level and generic item quantity footers use the same centered white treatment.

## CharacterHub artifact composition

ArtifactLoadout contains the tooltip-style inspector and no duplicate identity header or portrait. Its six authored slot buttons join the existing CharacterHub hero at runtime. The active-set overview renders only permanent bonuses from activated sets and hides when none are active. The preview contains three equipment actions: Change, Equip/Unequip, Enhance. Inventory, fighter details and enhancement Back actions are managed by AccountHeader’s navigation stack. Closing the inventory during a tab change preserves the new tab's slot visibility.

## Unified item geometry

Standard item frames use the shared 1.2× geometry (115.2×139.2), with a square sprite area and separate pale quantity/level footer. Fighter artifact slots are enlarged further to 156×188 frames, or 124×150 in compact layouts. The compact workshop's material tray uses 92×112 frames so all current and projected stat rows remain visible. Hero art and tooltip inspection remain larger previews.

Enhancement always exposes 15 material slots in one horizontal, non-wrapping scroll row. Empty slots open the same lookup used by fighter equipment. Selected tiles remove their material; there is no separate Materials button. Both lookup paths use one Base EXP / 1–5 Stars sort dropdown and a direction button; a selected equipment slot scopes equipment results. There are no search, set, rarity or slot filter controls inside this shared lookup.

## Draft and committed presentation

The header shows the current +level, with a separate yellow projected level during drafting. Main and changed substat values occupy separate before/after elements; projected values are yellow with green arrows that fade and rise. Affix choices and gains come from the authored table and artifact seed, so previewing or changing materials does not reroll an earned milestone. No inventory changes occur during drafting.

A reviewed commit validates and saves the exact transaction once, then fills EXP across level boundaries. The level updates with a pulse; a blue current-level header and sparkle introduce success. Each earned random affix gain is revealed on its own row with a number pulse, sparkle and a green rising arrow. Compact layout and scroll targeting keep that row visible. Closing, detaching or changing accounts cancels presentation callbacks. The persisted result does not depend on an animation completing.

The lookup enters with a left slide and fade, and its grid fades in with a stagger. Shared screen motion provides fade, ease-out slide, scale and sparkle/flash across infrastructure and combat UI. See `Tests/CombatPresentation/ArtifactUpgradePresentation.md` and `ScreenMotion.md` for the current verification scope, starting values and interaction checks. Historical captures above predate this refinement.
