# Character inventory and progression

## Player flow

The inventory is a wrapping, scrollable grid of owned fighters. Selecting a card opens that fighter's inspector. Back and Escape return to the same search, filters and scroll position. The Character Kit page contains a level/basic-stat panel and an icon-only kit panel. Details opens all stats; the two skills, ultimate and passive open dismissible detail popups. Card details support R1–R3 and C0–C5 inspection and use the battle tooltip formatter, colors, keyword glossary and status badges.

## Progression rules

- New fighters start at level 1, cap 10. Level 90 is the final level.
- Leveling spends only Coins, shown as the leveling resource item. It increases CC, ATK, DEF and HP; secondary stats stay unchanged.
- At levels 10, 20, 30, 40, 50, 60, 70 and 80, leveling stops until limit break. Limit break raises the cap by 10 without changing level or stats.
- Limit break spends Coins plus exactly the fighter's own phase crest, sub-phase sigil and ordinary series insignia. Premium series relic materials cannot substitute.
- Fighter category IDs encode phase in the thousands digit and sub-phase in the hundreds digit. Iori95 (`1406`, `series.kof`) consumes WIP-Phase Prototype Crest, META Apex Sigil and KOF Tournament Insignia.
- Both transactions recheck ownership, balances and cap at activation. All deductions and progression are persisted in one account save; failed saves restore resources, level and cap.
- Old account records without a cap receive the smallest multiple of ten containing their saved level. Out-of-range legacy levels clamp to 1–90. Constellation progression stays separate.
- Dungeon formation seeds and practice battle snapshots resolve owned-level stats. A run already in progress keeps its recorded stats; upgrades apply when forming the next roster.

## Starting values — economy and growth

These are editable prototype values, not final economy balance:

| Action | Starting cost |
| --- | --- |
| Level L → L+1 | `100 + 25 × L²` Coins |
| Cap C → C+10 | `5,000 × (C/10)²` Coins |
| Limit break phase material | `5 × (C/10)` |
| Limit break sub-phase material | `3 × (C/10)` |
| Limit break series material | `2 × (C/10)` |

Each CharacterObject has a serialized `LevelGrowth` profile for AttackBp, DefenseBp and HealthBp. Each is basis points of the level-one stat added per level, with deterministic rounding. CC no longer has a separately authored rate: it derives from weighted stat gains and is calibrated to 10× level-one CC at level 90 before equipment. See [CombatClassFormula.md](CombatClassFormula.md) for the starting weights, formula, snapshot behavior and 29-fighter numerical review. Secondary stats do not increase from levels but contribute to CC when permanent equipment changes them.

Assumption: Coins are the leveling material; no additional XP consumable is required. The initial cap is 10. If either rule changes, update costs and the displayed resource grid together.

## Material art direction

The shared ItemRewardView renders these vector icons in the backpack, reward lists and Upgrade grid; no texture import is required. Names, descriptions, icon tint and appearance text live in ItemCatalog. Stable inventory IDs are unchanged.

| Family | Names / identity | Appearance |
| --- | --- | --- |
| Phase 1–9 | Prototype, Dawn, Release, Ember, Tempest, Prism, Vanguard, Eclipse, Apex Crests, each prefixed by its phase | Faceted five-sided shield, bright central core, 1–9 engraved release marks; blue, mint, gold, ember, periwinkle, orchid, green, violet and rose tints |
| Sub-phase 0–4 | Launch Origin, Begin Spark, Middle Resonance, Late Horizon, META Apex Sigils | Diamond seal surrounded by an orbital ring and 4–8 rays; mint, amber, blue, lavender and pink |
| Series | KOF Tournament Insignia; Tekken Iron Fist Token; Soul Calibur Soul Fragment; other series Legacy Insignia | Violet hexagonal medallion with gold series monogram |

Descriptions explain which fighters consume each item and which dungeon victories award it. Upgrade tiles show owned / required amounts; shortages use red text and borders. Item tooltips repeat the requirement and display inventory amounts.

## State and feedback

One upgrade view shows CC, ATK, DEF and HP in aligned rows. Below cap it previews next-level stats, Coin cost, and the Level Up action. At the cap it switches to Limit Break, reveals the phase/sub-phase/series material boxes, and compares current and next caps; stats stay unchanged. Limit Break is enabled only with every required resource. At level 90 the view shows Max Level and disables its single action. Successful actions update level/stat/cap labels and a result message; resource counters update through the account inventory event. Popup dismissal restores focus, and Escape closes a popup before the inspector.

## Tuning and playtest plan

1. New player/readability: have five players enter a fighter, open a skill and explain the two Upgrade actions. Starting pass metric: four of five correctly identify that limit break raises capacity rather than stats. If they fail, improve action labels and cost grouping before changing prices.
2. Economy: record dungeon victories and Coins/materials earned from level 1 to 20. Starting target: the first break requires 3–6 relevant victories. If above six, reduce material requirements first; if below three, increase requirements. Track later breaks independently rather than assuming linear play time.
3. Stress/abuse: rapid clicks cannot skip a cap, spend unavailable resources or pass level 90. A forced save failure keeps the previous account state. Reopen/reload preserves the result.
4. Fighter identity: compare growth profiles across attackers, tanks and supports at levels 1, 10, 50 and 90. If they converge undesirably, tune the individual four-stat profiles; never scale secondary stats to compensate.
5. Navigation: inspect a fighter from a filtered/scrolled grid at desktop and compact landscape sizes. Popups and Back must remain visible and restore the same collection view.

The five design checks are addressed by explicit costs and cap labels (clarity), immediate stat/resource updates (response), increasing investment and fighter choice (motivation), visible level/cap confirmation (satisfaction), and shared battle/item visuals (fit).

## Verification

Run `Tests/CombatPresentation/RunCharacterUpgradeUiChecks.ps1` with Unity 6000.3.4f1. It uses an isolated project and unique disposable fixture account, exercises all fighters' level 1–90 growth scope, all 45 phase/sub-phase identities, exact resource deductions, every cap unlock, disk reload, failed-save rollback, actual UI callbacks and native UI Toolkit renders at 1920×1080, 1280×720 and 768×1024. Evidence is written under the fixture project's `Artifacts/CharacterUpgrade` and copied to the main project's artifact folder after review.

The current visual review is landscape only. Native reference captures in `Artifacts/UpgradeReference` cover level-up, capped limit break, insufficient materials, final level, and instance artifact tooltips. These captures use an isolated preview inventory and do not execute gameplay tests or write the player account.

## Shared composition

Character Kit and Upgrade use `BasicStatPreview` for aligned CC, ATK, DEF, HP rows. Upgrade adds next-level values only while leveling. Its preview stays at the top and its material/coin/action group anchors to the bottom of the fitted vertical content. Required material tiles sort by frame quality and use the shared centered white amount footer. The account header Back action returns from fighter details to the character inventory, restoring the header’s previous navigation action on close.

Character Inventory has no local Back button. AccountHeader manages nested Back actions so closing fighter details restores Inventory’s Back-to-Home action; closing enhancement restores fighter navigation.
