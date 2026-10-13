# Dungeon enemy progression and action passives

Implemented behavior:

- Entry filters include earlier released fighters, but remove phase/sub-phase cutoffs that cannot supply four distinct ready enemies. Ownership does not hide release filters. Existing `series.sc` Soul Calibur assets match `series.soulcalibur` policies. Soul Calibur starts at Pre-Release / Late.
- Easy battles use minions with half basic and secondary stats, half CC, no fighter passive, and simple attacks. Crit Damage retains its neutral 100% floor and halves its bonus. Easy elites use elite minions with the same half-stat baseline and stronger attacks. Easy bosses use unmodified level-one fighter kits.
- Starting balance values: Normal starts at level 15 with two Uncommon artifacts; Hard at 35 with four R artifacts; Extreme at 60 with five SR artifacts; Nightmare at 90 with five SSR artifacts. Each loadout uses unique slots of one set. Starting artifact enhancement levels are +0/+6/+12/+20 respectively. Row and elite/boss progression adds levels and enhancement up to their existing maximums. Easy stays at level one.
- Nightmare always has level 90, a five-piece SSR set at +20 including earned substat milestones, and random C0–C5 even for formations with an authored constellation override.
- Enemy upgrades, loadouts, levels and kinds are frozen with the route and survive clone/save/load. New routes use generator version 4. Existing routes keep their saved enemies.
- Recommended team CC is the highest enemy team CC across three deterministic route samples, including bosses. It is an estimate for preparation, not a promise of victory or first turn against every possible randomly generated team.
- Loadout stats use the same level/artifact calculation as battle; previously entered fighters use their frozen run stats. Team CC includes the reserve, compares with the selected saved enemy team, and uses green for player-first including ties, red otherwise. HP text shows max HP; a fill shows remaining HP. The loadout header now adapts to smaller viewports.
- Card damage facts aggregate all hits per recipient and retain any-hit crit/block facts. Reactions resolve after card status effects, so a stance removed by that card cannot enable its stance-dependent reaction. Each reaction command activates once per owner/action. Explicit RootActionTarget reactions retain one execution per recipient. Counters use the same boundary. Buffs gained from an attack do not amplify subsequent hits of that same attack.

## Verification

Run `Tests/CombatPresentation/Run.ps1 -DungeonEnemiesOnly` for deterministic progression, availability, alias compatibility, persistence, minion stats/kit, Nightmare equipment, multihit/AOE crit buffs, incoming-attack stacks, same-action stance removal, mixed crit facts, playback, additional damage and dungeon policy checks.

Unity menu: **Fighting Allstar / Validation / Dungeon Enemies and Loadout**. This isolates ownership in a preview scene, renders 1920×1080, 1280×720 and 768×1024, verifies real Soul Calibur cutoffs, every recommendation, upgraded/frozen stats, injury bars, red/green CC including ties, legacy uGUI filled Image fallback and viewport bounds. Evidence is in `Artifacts/DungeonEnemyRefactor`. It does not save accounts, runs or scenes.

The complete existing suite stops at `MultiHitChecks: First-hit status ran late`; the dungeon reward group stops at `Regular preview must show possible series materials once and exclude premium materials`. Both also fail when run against the unchanged HEAD core and checks in an isolated baseline. The older broad UI fixture stops at its reward quantity assertion. Focused checks avoid those unrelated legacy assertions.

## Balance playtest

All progression numbers above are starting values. Use fresh level-one teams without artifacts for Easy, and teams matching the listed progression for higher difficulties. Try ten runs per difficulty with at least two different formations. Starting pass criteria: win the first Easy battle in at least eight runs, and win the Easy boss in at least five; higher difficulty teams at their recommended CC should win at least five bosses. Check that players can read CC and remaining HP before entry. If Easy opening wins fall below eight, lower minion attack coefficients; if opening wins meet the criterion but bosses fail, lower the Easy boss team size or tune source fighter encounter selection. If matching higher-difficulty teams fail the boss criterion, lower starting enemy levels or artifact enhancement before changing player progression. If encounters are consistently trivial, raise those same values gradually and repeat.
