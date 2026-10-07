# Dungeon flow refactor

## Behavior

- MainMenu starts the build. Battle opens Combat (dungeon selection); Summon opens Scene-Gacha.
- Gacha has a banner, rates, earned selectors, and a separate result grid. All draws use PlayerInventoryService and the authored runtime-ready KOF assets. Existing 4% SSR / 36% SR / 60% R rates and 160/1600 costs are retained. Each rarity is distributed evenly across its currently configured fighters.
- Dungeon selection presents a horizontal series-card ScrollView. Clicking a card opens a configuration modal with phase 1–9, sub-phase 0–4, difficulty and live reward preview. Cancel, Escape, and clicking the backdrop close it without starting a run. Only authored series appear (currently KOF).
- Dungeon selection filters exactly by series, phase and sub-phase. CharacterObject.ID supplies the four-digit category; SourceId remains provenance. Empty categories cannot fall back to unrelated enemies. Four distinct eligible enemy definitions and one owned eligible fighter are needed.
- Entry creates a fresh seed, route, and four-member enemy snapshots. The map opens at the bottom. A route choice reveals its enemy formation; Prepare team opens loadout; Play opens Battle.
- Run formation changes autosave in the account-scoped run file. Reopening loadout restores them. CharacterLoadOut filters owned characters through the active dungeon policy without locking committed characters to the run.
- Completion grants the existing idempotent run reward and clears the run; defeat and abandonment clear it too. Permanent ownership and currency remain. Legacy account-level formation fields are discarded on load and omitted from meaningful persistence.
- Existing runs retain their pinned enemies; start a fresh run to use changed authored combat data. Legacy runs can still be abandoned from the map.
- CharacterRegistrySync updates asset references when authored character assets change. It never regenerates character, card, or passive configuration.

## State and feedback

Choose category → saved route → selected node → run formation → battle → battle result → route / terminal state. Back from loadout preserves the run; leaving a banner spends nothing. Invalid categories, missing ownership, insufficient Diamonds, and invalid formations have visible reasons. Button disabling prevents duplicate navigation and summon submission. Existing battle audio is retained; this refactor adds no new economy or audio balance.

The scene fade is a **starting value of 0.2 seconds**, measured in unscaled time. Check ten rapid scene transitions at low and high frame rates: each click should navigate once with no frozen overlay. Shorten the fade if it feels delayed; lengthen only if the transition visibly flashes.

## Automated verification

- Run Tests/CombatPresentation/Run.ps1: includes 45-category matching, clone isolation, series rejection, deterministic seed replay, changed-seed variation, distinct three-active/one-reserve teams, no empty-category fallback, and map-first entry.
- Unity menu: **Fighting Allstar → Validate Dungeon Flow**. Validates current authored assets, generates a KOF 10XX route, and checks required controls in the four UXML trees. Report: Temp/dungeon-flow-validation.txt.

## Play-mode acceptance

1. New player: open MainMenu, use Summon, read rates, draw once and ten times on a test profile, close results, choose dungeon. Verify wallet, collection and selector progress agree after reopening.
2. Clarity: WIP-Phase/Launch shows 10XX candidates. Pre-Release/Launch shows 20XX and is unavailable until authored content supports it. All nine phases and five sub-phases remain selectable.
3. Route: confirm Start is at the bottom and Boss at the top; paths connect only legal successors. Preview every active/reserve enemy before entering.
4. Save: assign and reorder four owned fighters, return to the route and reopen loadout. Restart play mode during the run and repeat. The same seed, enemies and slots must return.
5. Loadout adjustments: win an encounter with wounds or a defeated fighter. Reopen loadout: all eligible owned fighters remain available through the dungeon policy filter without locking committed characters to the formation.
6. Abuse/stress: rapidly click navigation, draw and route actions. No double spend, duplicate navigation, route skip, team reroll, free healing or duplicate completion reward.
7. Terminal: abandon, lose, and complete separate test runs. Reopen loadout and start a new run: no old formation should appear, while owned fighters and currency remain.
8. Readability: inspect MainMenu, banner, ten-result grid, dungeon filters and map at 1280×720 and 1920×1080. Confirm no clipping, unreadable labels or invisible route controls.

Visual references inform the banner rail/hero/actions/result composition and the narrow, bottom-up staggered labyrinth platforms. The implementation uses the project's existing character and UI art.
