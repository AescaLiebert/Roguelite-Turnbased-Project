# Artifact equipment and enhancement

Implemented from [Artifact System Design Brief](ArtifactSystemDesignBrief.md) and [Artifact Upgrade System Brief](ArtifactUpgradeSystemBrief.md).

## Player flow

- Characters → owned fighter → Artifacts. Five named slots surround the character artwork; the sixth position reserves Holy Relics, which remain unavailable under the existing game scope.
- Select an equipped slot to inspect its individual rolls and set effects. Select an empty slot or Change to open a searchable collection filtered by slot, oath, and rarity. Selection previews the item; Equip commits it. Equipping another piece in the same slot returns the previous piece to the backpack.
- Set effects keep their two-piece and four-piece descriptions. A checkmark and colored text show a matching-piece threshold is met; unmet effects are grey. Loadout summaries use compact threshold markers without activation-status sentences. Inspection uses the viewed fighter’s current equipment; standalone tooltips use the artifact’s equipped owner.
- Enhance opens the workshop. Artifact Dust and eligible artifact instances can be combined. The projected level, main and substat values, XP, Coins, milestones, and cap overflow are shown before consumption.
- Materials opens a filtered backpack grid docked on the left of the workshop, keeping the enhancement preview and controls available beside it. Multiple selection and separate Inspect controls are supported. Clicking a selected material in the workshop removes it. Clear resets the selection.
- Auto Add fills enough XP for the next level, using Dust first, then the lowest rarity and least upgraded eligible artifacts. Its maximum rarity setting is saved per account. Review and confirmation identify every consumed source instance; favourite, equipped, and target artifacts are excluded and also rejected by core rules.
- Dungeon victories display each obtained artifact separately in a horizontal scroll strip without wrapping. Select a tile for a compact stat and set tooltip. Backpack browsing exposes search, oath, slot, rarity, and favourite filters. Artifact tiles use rarity stars and frame color rather than item names. Favourite pieces carry a lock marker. Equipped item tiles show the owning fighter’s avatar, with the fighter’s name available on inspection and the avatar tooltip.

## Rules and persistence

There are 15 identities: three oaths × Flower, Feather, Sand Clock, Goblet, Crown. Rarity belongs to each instance. Dungeon acquisition uses the stage-specific pair in [DungeonArtifactBaskets.md](DungeonArtifactBaskets.md). Existing generic rarity items migrate deterministically without losing their instance or acquisition identifiers.

Every artifact has one basic main stat, flat or percent. Common / Uncommon / R / SR / SSR have 0 / 1 / 2 / 3 / 4 distinct substat identities. The 23-line vocabulary includes Combat Class and Reflect Damage. Flat and percentage versions of a basic stat are distinct. Main-stat growth is deterministic; substat quality is rolled from a persisted seed. Inspection and enhancement preview never advance gameplay RNG.

Enhancement is guaranteed from +0 through +20. The main stat improves at every level. +6, +12, and +18 each improve one existing substat; Common skips those events, and +20 has no milestone. XP carries between levels. Excess at +20 is displayed before confirmation and discarded; Coins are charged for usable XP.

Favourite is persistent and blocks every fodder path. A favourite may still be equipped or enhanced. Equipped artifacts are also protected from consumption. A piece cannot silently move from another fighter: unequip it there first.

Equipment stats and matching-piece effects enter character stats, practice, and dungeon snapshots. Two matching pieces add 20% to their oath's basic stat; four add a second 20%. Four-piece low-HP effects apply only while current HP is strictly below 20% of live maximum HP. Both named conditional stats apply; Striker also raises outgoing damage. Battle HUDs display **Artifact Active** with the active oath and remove it immediately when its condition ends.

Dungeon fighters retain the equipment snapshot from entry. Later account equipment changes apply to a new run or a fighter's first entry, preserving active-run stats. The same artifact instance cannot be frozen onto two different fighters in one run.

Equipment, favourite, and enhancement changes clone inventory state and use the existing atomic account save. Save failure restores the previous state. Receipt grants remain idempotent. Malformed legacy inventories repair invalid equipment references, duplicate slots, invalid stat lines, negative stacks, and overflowing duplicate balances.

## Starting tuning values

The briefs leave the economy, ranges, slot eligibility, and acquisition distribution open. These are **starting values**, kept together in `Shared/FightingAllstar.Core/Content/ArtifactRules.cs`:

| Rule | Starting value |
|---|---|
| Main eligibility | All six basic forms on every slot |
| Set / slot / main / quality selection | Two-set dungeon basket, uniform eligible set / slot / main / quality draws; rarity follows dungeon difficulty |
| SSR main +0 → +20 | ATK 60 → 500; DEF 50 → 400; HP 500 → 3500; basic percent 6% → 36% |
| Rarity scaling | Above ranges multiplied by rarity / 5; linear main growth |
| SSR initial and milestone substat rolls | Percent 100–400 bp; ATK 8–25; DEF 6–20; HP 75–200; CC 20–80 |
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

Native UI review captures are stored in `Artifacts/ArtifactSystem`: equipment, selected-slot detail, enhancement preview, consumption confirmation, docked material picker, backpack, reward strip, and reward tooltip at 1920×1080, 1280×720, and 768×1024. The material picker sits on the left at landscape sizes and stacks above the preview in portrait. No account files or live scenes are modified by the capture fixture. These are visual captures; gameplay and persistence test suites were not run.

## Item tooltip panels

Item and instance-artifact tooltips share a rarity-colored name bar, an icon/type/star preview panel, and a parchment content panel. Artifact set names lead separate 2-piece and 4-piece effect lines; active effects show a checkmark and green text, inactive effects remain grey. Instance tooltips retain rolled stats, level, equipped owner, and favourite protection. Dungeon basket tooltips show both possible sets without inventing rolled stats. Artifact reward preview boxes omit the always-zero level footer.

The game targets landscape. Subsequent layout review uses 1920×1080 and 1280×720 only; prior portrait captures are historical.

Instance tooltip refinement: the +level badge follows the name in the title bar; the main-stat name and value sit in the colored preview panel beside the icon. Only substats, set effects, ownership and favourite controls appear in the parchment content panel. Set-basket previews have neither an instance level nor rolled stats.

Dungeon set previews now show both eligible Flowers separately; each tooltip presents its own set effects and the shared drop basket. All artifact level and generic item quantity footers use the same centered white treatment.

## CharacterHub artifact composition

ArtifactLoadout contains the shared character identity header and tooltip-style inspector. Its six authored slot buttons join the existing CharacterHub hero at runtime; it no longer owns a portrait. The active-set overview renders only permanent bonuses from activated sets and hides when none are active. The preview contains three equipment actions: Change, Equip/Unequip, Enhance. The separate equipment dock is removed. Inventory, fighter details and enhancement Back actions are managed by AccountHeader’s navigation stack. New landscape captures are in `Artifacts/ArtifactHubRefinement`.

## Unified item geometry

Every item tile uses a 96×116 rarity frame: a square sprite area followed by a 20px absolute footer. The pale footer has 72% background alpha and does not cover the sprite. The shared builder controls geometry across backpack, dungeon preview/rewards, equipped slots, upgrade requirements, material picker/selection and consumption review. Hero art and tooltip inspection remain larger previews. CharacterHub’s existing 512×512 portrait keeps the same bounds on Kit and Artifacts. Enhancement selection is a flexible row: empty shows one + tile; selected items append dynamically with one add tile. The selected row is docked with the enhancement controls and stays visible.
