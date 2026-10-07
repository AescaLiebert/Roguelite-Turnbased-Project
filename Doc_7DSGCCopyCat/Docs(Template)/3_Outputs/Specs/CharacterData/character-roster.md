---
slug: fighting-allstar-character-roster
status: needs-human
gdd_tags: [roster, tuning]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---
# Fighting Allstar — actual WIP character drafts

29 source characters. Source IDs are CSV IDs, not legacy Unity asset IDs. Every skill listing in the guide is Rank 3 (PDF p.7). Original spellings/text remain in [source-catalog.json](source-catalog.json). [Structured drafts](wip-character-drafts.json) preserve provenance per stat and rank. These are authoring drafts, not runtime definitions.

## Generation rules and test gates

- **Starting values:** R1/R2 percentage magnitudes are one-third/two-thirds of R3, rounded to two decimals. Integer turn/gauge/Ignite counts use ceiling with minimum one. Non-numeric effects remain available; keyword multipliers (Weakpoint ×3, etc.) do not scale with card rank. R3 stays verbatim. This implements the guide’s approximate one-third suggestion; it is not a claim of exact 7DSGC scaling.
- **Starting values:** C0 is the source ultimate baseline; C1–C6 add 5% of baseline ATK-scaled ultimate damage/healing per tier, non-compounding; Kensou’s primary percent-HP ultimate heal follows the same scaling. Other effects remain unchanged except explicit Kyo +1 Ignite/tier and Robert +7 percentage points/tier. There are seven states and six upgrades. No invented C6 passive unlocks.
- **Starting values:** missing stats use explicit role profiles for Shingo97, Andy94, Brian94, Lucky94 and Heavy D94. Existing numbers, including surprising CC values and identical Joe variants, are preserved. Hidden stats use guide p.6 defaults.
- Microtests: compare all ranks at equal card/action investment; R3 must improve payload without making R1 useless. If move/merge always wins, reduce R2/R3 damage or effect duration. If generated low-rank crowd control dominates, gate it to R2 rather than silently altering the source R3.
- Run 100 paired seeds per intended matchup, swapping first side: initial soft-counter target 55–65%, all-round team target at most 60%. These are diagnostic starting targets, not measured results. If rarity/stats dominate, narrow generated stats first and separately evaluate source stat normalization.
- Compare C0 and C6 mixed teams; reject progression that removes access to a counter at C0. If Kyo’s eight Ignites overwhelms defense, tune the proposed stack cap/mitigation cap before adding new power.
- All Holy Relics are retained but disabled for the first prototype. Missing relic means unspecified, not permission to invent a permanent bonus. Character-specific ambiguities below must become explicit typed effects before that character enters a runtime allowlist.

## Roster index

| CSV ID | Character | Rarity | Attribute | ATK / DEF / HP | Stats |
| --- | --- | --- | --- | --- | --- |
| 1 | Kusanagi Kyo 94 | SR | Green | 290 / 320 / 4700 | Source |
| 2 | Kusanagi Kyo 98 | SSR | Red | 540 / 290 / 6600 | Source |
| 3 | Mai Shiranui 94 | SR | Green | 175 / 190 / 5000 | Source |
| 4 | Mai Shiranui 95 | SSR | Red | 590 / 375 / 5500 | Source |
| 5 | King 94 | SR | Red | 300 / 250 / 3650 | Source |
| 6 | Yagami Iori 95 | SSR | Blue | 560 / 260 / 6100 | Source |
| 7 | Benimaru 94 | R | Red | 178 / 110 / 2721 | Source |
| 8 | Benimaru 99 | SSR | Green | 420 / 240 / 5800 | Source |
| 9 | Goro Daimon 94 | R | Blue | 125 / 260 / 3190 | Source |
| 10 | Ibuki Shingo 97 | SR | Blue | 300 / 230 / 4400 | Generated starting values |
| 11 | Asumiya Athena 94 | SSR | Yellow | 500 / 370 / 5900 | Source |
| 12 | Sie Kensou 94 | R | Blue | 260 / 120 / 3200 | Source |
| 13 | Chin Gentsai 94 | R | Green | 320 / 120 / 4400 | Source |
| 14 | Terry Bogard 96 | SSR | Blue | 420 / 450 / 6000 | Source |
| 15 | Joe Higashi 94 | R | Red | 250 / 200 / 4500 | Source |
| 16 | Joe Higashi 96 | R | Blue | 250 / 200 / 4500 | Source |
| 17 | Andy Bogard 94 | R | Yellow | 310 / 130 / 3600 | Generated starting values |
| 18 | Leona Heidern 96 | SSR | Green | 560 / 290 / 5500 | Source |
| 19 | Clark Still 94 | SR | Blue | 400 / 120 / 3000 | Source |
| 20 | Ralf Jone 94 | R | Green | 150 / 300 / 5100 | Source |
| 21 | Ryo Sakazaki 94 | SR | Yellow | 300 / 290 / 5500 | Source |
| 22 | Yuri Sakazaki 94 | SR | Green | 250 / 130 / 3675 | Source |
| 23 | Yuri Sakazaki 98 | SSR | Blue | 420 / 300 / 6300 | Source |
| 24 | Robert Garcia 94 | SSR | Blue | 620 / 290 / 5900 | Source |
| 25 | Choi Bouge 94 | R | Red | 260 / 120 / 3200 | Source |
| 26 | Chang Koehan 94 | R | Green | 130 / 300 / 3400 | Source |
| 27 | Brian 94 | R | Blue | 260 / 250 / 4300 | Generated starting values |
| 28 | Lucky 94 | R | Green | 230 / 170 / 3900 | Generated starting values |
| 29 | Heavy D! 94 | SR | Red | 390 / 180 / 4100 | Generated starting values |

## 1. Kusanagi Kyo 94

`fighter.kyo94` · SR · Green · Japan, Fire Elements, Sacred Treasure

**Passive (On-Field; Own):** Increase own attack power by 3% for each Ignites effect on the field. (Maximum 30%)

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=2794; Attack=290; Defense=320; Health=4700; Pierce_Rate=20; Resistance=5; Regeneration=5; Critical_Chance=30; Critical_Damage=152; Critical_Resistance=0; Critical_Defense=15; Recovery_Rate=112; Block_Chance=0; Block_Power=20; Lifesteal=5; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Attack

- **R1 (starting value):** Inflict Weakpoint damage equal to 100% of ATK
- **R2 (starting value):** Inflict Weakpoint damage equal to 200% of ATK
- **R3 (source):** Inflict Weakpoint damage equal to 300% of ATK
### Skill 2 — Single-Target / Attack

- **R1 (starting value):** Inflict Shatter damage equal to 150% of ATK
- **R2 (starting value):** Inflict Shatter damage equal to 300% of ATK
- **R3 (source):** Inflict Shatter damage equal to 450% of ATK

### Ultimate constellation sequence

- **C0/5:** Inflicts 500% of ATK, applies 2 Ignites effects.
- **C1/5:** Inflicts 525% of ATK, applies 3 Ignites effects.
- **C2/5:** Inflicts 550% of ATK, applies 4 Ignites effects.
- **C3/5:** Inflicts 575% of ATK, applies 5 Ignites effects.
- **C4/5:** Inflicts 600% of ATK, applies 6 Ignites effects.
- **C5/5:** Inflicts 625% of ATK, applies 7 Ignites effects.

**Holy Relic (disabled):** Randomly applies 3 Ignites effects to the enemy every turn for 3 turns

**Review:** Ignite ultimate duration missing: propose 3 owner turns. Relic disabled until unlock design exists.

## 2. Kusanagi Kyo 98

`fighter.kyo98` · SSR · Red · Japan, Fire Elements, Sacred Treasure

**Passive (On-Field; All):** For each Ignites effect on enemy, Decrease allies's Damage Received by 3%

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=3840; Attack=540; Defense=290; Health=6600; Pierce_Rate=30; Resistance=15; Regeneration=0; Critical_Chance=40; Critical_Damage=140; Critical_Resistance=15; Critical_Defense=30; Recovery_Rate=110; Block_Chance=7; Block_Power=20; Lifesteal=7; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Attack

- **R1 (starting value):** Inflict Co-Destruction damage equal to 141.67% of ATK
- **R2 (starting value):** Inflict Co-Destruction damage equal to 283.33% of ATK
- **R3 (source):** Inflict Co-Destruction damage equal to 425% of ATK
### Skill 2 — AOE / Attack

- **R1 (starting value):** Inflict Blaze damage equal to 83.33% of ATK
- **R2 (starting value):** Inflict Blaze damage equal to 166.67% of ATK
- **R3 (source):** Inflict Blaze damage equal to 250% of ATK

### Ultimate constellation sequence

- **C0/5:** Inflicts 500% of ATK, applies 2 Ignites effects.
- **C1/5:** Inflicts 525% of ATK, applies 3 Ignites effects.
- **C2/5:** Inflicts 550% of ATK, applies 4 Ignites effects.
- **C3/5:** Inflicts 575% of ATK, applies 5 Ignites effects.
- **C4/5:** Inflicts 600% of ATK, applies 6 Ignites effects.
- **C5/5:** Inflicts 625% of ATK, applies 7 Ignites effects.

**Holy Relic (disabled):** Randomly applies 3 Ignites effects to the enemy every turn for 3 turns

**Review:** Uncapped team mitigation: propose 30% cap as a starting value. Ignite ultimate duration proposed 3 turns.

## 3. Mai Shiranui 94

`fighter.mai94` · SR · Green · Women, Fire Elements

**Passive (On-Field; All):** Decrease enemy's Recovery Rate equal to own Regeneration

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=2790; Attack=175; Defense=190; Health=5000; Pierce_Rate=30; Resistance=20; Regeneration=10; Critical_Chance=10; Critical_Damage=110; Critical_Resistance=0; Critical_Defense=20; Recovery_Rate=110; Block_Chance=30; Block_Power=40; Lifesteal=13; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Attack

- **R1 (starting value):** Inflict Rupture damage of 133.33% ATK
- **R2 (starting value):** Inflict Rupture damage of 266.67% ATK
- **R3 (source):** Inflict Rupture damage of 400% ATK
### Skill 2 — AOE / Debuff

- **R1 (starting value):** Inflicts 91.67% of ATK, Disable enemy from using recovery cards for 1 turns.
- **R2 (starting value):** Inflicts 183.33% of ATK, Disable enemy from using recovery cards for 2 turns.
- **R3 (source):** Inflicts 275% of ATK, Disable enemy from using recovery cards for 2 turns.

### Ultimate constellation sequence

- **C0/5:** Inflicts 375% of ATK, increase own card rank
- **C1/5:** Inflicts 393.75% of ATK, increase own card rank
- **C2/5:** Inflicts 412.5% of ATK, increase own card rank
- **C3/5:** Inflicts 431.25% of ATK, increase own card rank
- **C4/5:** Inflicts 450% of ATK, increase own card rank
- **C5/5:** Inflicts 468.75% of ATK, increase own card rank

**Holy Relic (disabled):** Increase own Resistance by 80%

**Review:** Card 2 description disables Recovery; effect tag says Attack. Draft follows description.

## 4. Mai Shiranui 95

`fighter.mai95` · SSR · Red · Women, Fire Elements

**Passive (On-Field; All):** When own attacks an Ignites enemy, Attack increases by 20% and Damage Dealt increases by 20%

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=3790; Attack=590; Defense=375; Health=5500; Pierce_Rate=20; Resistance=5; Regeneration=50; Critical_Chance=75; Critical_Damage=140; Critical_Resistance=25; Critical_Defense=10; Recovery_Rate=135; Block_Chance=20; Block_Power=5; Lifesteal=10; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Debuff

- **R1 (starting value):** Inflicts damage equal to 120% of ATK. Removes buff and applies 1 Ignites to enemy for 1 turns
- **R2 (starting value):** Inflicts damage equal to 240% of ATK. Removes buff and applies 2 Ignites to enemy for 2 turns
- **R3 (source):** Inflicts damage equal to 360% of ATK. Removes buff and applies 3 Ignites to enemy for 3 turns
### Skill 2 — AOE / Attack

- **R1 (starting value):** Inflicts 125% of ATK
- **R2 (starting value):** Inflicts 250% of ATK
- **R3 (source):** Inflicts 375% of ATK

### Ultimate constellation sequence

- **C0/5:** Inflicts 375% of ATK, increase own card rank
- **C1/5:** Inflicts 393.75% of ATK, increase own card rank
- **C2/5:** Inflicts 412.5% of ATK, increase own card rank
- **C3/5:** Inflicts 431.25% of ATK, increase own card rank
- **C4/5:** Inflicts 450% of ATK, increase own card rank
- **C5/5:** Inflicts 468.75% of ATK, increase own card rank

**Holy Relic (disabled):** Increase own Resistance by 80%

**Review:** Passive scope All conflicts with own-attacks wording: draft self-only, current action, non-stacking.

## 5. King 94

`fighter.king94` · SR · Red · Women

**Passive (SUB; All):** Increase the alies's Pierce Rate by 8% at the end of every turn. (Maximum 40%)

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=3100; Attack=300; Defense=250; Health=3650; Pierce_Rate=40; Resistance=0; Regeneration=20; Critical_Chance=0; Critical_Damage=110; Critical_Resistance=15; Critical_Defense=20; Recovery_Rate=120; Block_Chance=0; Block_Power=0; Lifesteal=2; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Attack

- **R1 (starting value):** Inflict Pierce damage equal to 150% of ATK
- **R2 (starting value):** Inflict Pierce damage equal to 300% of ATK
- **R3 (source):** Inflict Pierce damage equal to 450% of ATK
### Skill 2 — AOE / Debuff

- **R1 (starting value):** Inflict damage equal to 125% of ATK, applies Poison to enemy for 1 turns
- **R2 (starting value):** Inflict damage equal to 250% of ATK, applies Poison to enemy for 2 turns
- **R3 (source):** Inflict damage equal to 375% of ATK, applies Poison to enemy for 3 turns

### Ultimate constellation sequence

- **C0/5:** Inflict damage equal to 630% of ATK, Depletes enemy ult gauge by 3 gauges
- **C1/5:** Inflict damage equal to 661.5% of ATK, Depletes enemy ult gauge by 3 gauges
- **C2/5:** Inflict damage equal to 693% of ATK, Depletes enemy ult gauge by 3 gauges
- **C3/5:** Inflict damage equal to 724.5% of ATK, Depletes enemy ult gauge by 3 gauges
- **C4/5:** Inflict damage equal to 756% of ATK, Depletes enemy ult gauge by 3 gauges
- **C5/5:** Inflict damage equal to 787.5% of ATK, Depletes enemy ult gauge by 3 gauges

**Holy Relic (disabled):** Increase own Pierce Rate by 80%


## 6. Yagami Iori 95

`fighter.iori95` · SSR · Blue · Orochi, Fire Elements, Japan, Sacred Treasure

**Passive (On-Field; Own):** Increases Damage Dealt by each Ignites on enemy by 20% per Ignites. When the enemy team, including Kyo, deals 5% of their Max HP as additional damage

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=4105; Attack=560; Defense=260; Health=6100; Pierce_Rate=5; Resistance=40; Regeneration=25; Critical_Chance=65; Critical_Damage=165; Critical_Resistance=30; Critical_Defense=35; Recovery_Rate=110; Block_Chance=0; Block_Power=0; Lifesteal=20; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Attack

- **R1 (starting value):** Inflicts 166.67% of ATK and Increases Lifesteal by 10% for 1 turns.
- **R2 (starting value):** Inflicts 333.33% of ATK and Increases Lifesteal by 20% for 2 turns.
- **R3 (source):** Inflicts 500% of ATK and Increases Lifesteal by 30% for 2 turns.
### Skill 2 — AOE / Debuff

- **R1 (starting value):** Inflicts 93.33% ATK and applies 1 Ignites for 1 turns.
- **R2 (starting value):** Inflicts 186.67% ATK and applies 2 Ignites for 2 turns.
- **R3 (source):** Inflicts 280% ATK and applies 2 Ignites for 3 turns.

### Ultimate constellation sequence

- **C0/5:** Inflicts 400% of ATK and Extort 50% of the enemy's attack and defense for 2 turns.
- **C1/5:** Inflicts 420% of ATK and Extort 50% of the enemy's attack and defense for 2 turns.
- **C2/5:** Inflicts 440% of ATK and Extort 50% of the enemy's attack and defense for 2 turns.
- **C3/5:** Inflicts 460% of ATK and Extort 50% of the enemy's attack and defense for 2 turns.
- **C4/5:** Inflicts 480% of ATK and Extort 50% of the enemy's attack and defense for 2 turns.
- **C5/5:** Inflicts 500% of ATK and Extort 50% of the enemy's attack and defense for 2 turns.

**Holy Relic (disabled):** Not supplied.

**Review:** Second passive sentence has no reliable trigger/target: retain verbatim, do not execute that clause.

## 7. Benimaru 94

`fighter.benimaru94` · R · Red · Japan, Chinese

**Passive (SUB; Red attribute):** Increase Red attribute allies Attack-related stats by 10%

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=2368; Attack=178; Defense=110; Health=2721; Pierce_Rate=5; Resistance=15; Regeneration=20; Critical_Chance=0; Critical_Damage=110; Critical_Resistance=0; Critical_Defense=15; Recovery_Rate=110; Block_Chance=12; Block_Power=25; Lifesteal=6; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Debuff

- **R1 (starting value):** Inflict damage equal to 120% of ATK, Cancel Stance and prevent enemy to use stance for 1 turns.
- **R2 (starting value):** Inflict damage equal to 240% of ATK, Cancel Stance and prevent enemy to use stance for 2 turns.
- **R3 (source):** Inflict damage equal to 360% of ATK, Cancel Stance and prevent enemy to use stance for 2 turns.
### Skill 2 — Single-Target / Debuff

- **R1 (starting value):** Inflict damage equal to 150% of ATK, Depletes enemy ult gauge by 1 gauges
- **R2 (starting value):** Inflict damage equal to 300% of ATK, Depletes enemy ult gauge by 2 gauges
- **R3 (source):** Inflict damage equal to 450% of ATK, Depletes enemy ult gauge by 3 gauges

### Ultimate constellation sequence

- **C0/5:** Cancel Buff and Stance on enemy, Inflict damage equal to 500% of ATK then Paralyze for 1 turn(s)
- **C1/5:** Cancel Buff and Stance on enemy, Inflict damage equal to 525% of ATK then Paralyze for 1 turn(s)
- **C2/5:** Cancel Buff and Stance on enemy, Inflict damage equal to 550% of ATK then Paralyze for 1 turn(s)
- **C3/5:** Cancel Buff and Stance on enemy, Inflict damage equal to 575% of ATK then Paralyze for 1 turn(s)
- **C4/5:** Cancel Buff and Stance on enemy, Inflict damage equal to 600% of ATK then Paralyze for 1 turn(s)
- **C5/5:** Cancel Buff and Stance on enemy, Inflict damage equal to 625% of ATK then Paralyze for 1 turn(s)

**Holy Relic (disabled):** When attacking, apply 1 Shock to target(s) for 3 turns


## 8. Benimaru 99

`fighter.benimaru99` · SSR · Green · Japan, Chinese

**Passive (On-Field; Women):** Reduces the Avoidance rate of enemy Women Trait by 20%

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=3668; Attack=420; Defense=240; Health=5800; Pierce_Rate=5; Resistance=40; Regeneration=30; Critical_Chance=10; Critical_Damage=150; Critical_Resistance=0; Critical_Defense=40; Recovery_Rate=150; Block_Chance=20; Block_Power=25; Lifesteal=6; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Debuff

- **R1 (starting value):** Cancels Stances on enemy and inflicts damage equal to 83.33% of Attack, then Stuns for 1 turns
- **R2 (starting value):** Cancels Stances on enemy and inflicts damage equal to 166.67% of Attack, then Stuns for 2 turns
- **R3 (source):** Cancels Stances on enemy and inflicts damage equal to 250% of Attack, then Stuns for 2 turns
### Skill 2 — Single-Target / Buff

- **R1 (starting value):** Taunts enemies and increases HP-related stats by 6.67% When attacked by a Women trait, reduces the Attacker’ Defense by 16.67% for 1 turns
- **R2 (starting value):** Taunts enemies and increases HP-related stats by 13.33% When attacked by a Women trait, reduces the Attacker’ Defense by 33.33% for 2 turns
- **R3 (source):** Taunts enemies and increases HP-related stats by 20% When attacked by a Women trait, reduces the Attacker’ Defense by 50% for 3 turns

### Ultimate constellation sequence

- **C0/5:** If the target is a female fighter, paralyzes for 1 turn and Inflict damage equal to 350% of ATK, applying "Shock" debuff for 3 turns
- **C1/5:** If the target is a female fighter, paralyzes for 1 turn and Inflict damage equal to 367.5% of ATK, applying "Shock" debuff for 3 turns
- **C2/5:** If the target is a female fighter, paralyzes for 1 turn and Inflict damage equal to 385% of ATK, applying "Shock" debuff for 3 turns
- **C3/5:** If the target is a female fighter, paralyzes for 1 turn and Inflict damage equal to 402.5% of ATK, applying "Shock" debuff for 3 turns
- **C4/5:** If the target is a female fighter, paralyzes for 1 turn and Inflict damage equal to 420% of ATK, applying "Shock" debuff for 3 turns
- **C5/5:** If the target is a female fighter, paralyzes for 1 turn and Inflict damage equal to 437.5% of ATK, applying "Shock" debuff for 3 turns

**Holy Relic (disabled):** Increase allies's Avoidance Rate by 40%

**Review:** Skill 1 says Stun but tag says disable stance: follow Stun description. Skill 2 Self target; propose 2-turn duration. Ultimate female condition applies to Paralyze; damage/Shock hit all enemies (proposal).

## 9. Goro Daimon 94

`fighter.goro94` · R · Blue · Japan

**Passive (SUB, PVP-Only; All):** Reduces damage taken by the team in PvP by 10%

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=2250; Attack=125; Defense=260; Health=3190; Pierce_Rate=0; Resistance=30; Regeneration=0; Critical_Chance=10; Critical_Damage=126; Critical_Resistance=25; Critical_Defense=25; Recovery_Rate=100; Block_Chance=0; Block_Power=0; Lifesteal=0; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — AOE / Debuff

- **R1 (starting value):** Reduces enemy's defense by 20%
- **R2 (starting value):** Reduces enemy's defense by 40%
- **R3 (source):** Reduces enemy's defense by 60%
### Skill 2 — Single-Target / Attack

- **R1 (starting value):** Inflict damage equal to 133.33% of ATK and Removes buff and stance
- **R2 (starting value):** Inflict damage equal to 266.67% of ATK and Removes buff and stance
- **R3 (source):** Inflict damage equal to 400% of ATK and Removes buff and stance

### Ultimate constellation sequence

- **C0/5:** Inflict damage equal to 600% of ATK
- **C1/5:** Inflict damage equal to 630% of ATK
- **C2/5:** Inflict damage equal to 660% of ATK
- **C3/5:** Inflict damage equal to 690% of ATK
- **C4/5:** Inflict damage equal to 720% of ATK
- **C5/5:** Inflict damage equal to 750% of ATK

**Holy Relic (disabled):** Increases own Damage Dealt by 25%

**Review:** PVP-only passive is inactive in dungeons; do not silently activate it. Skill 1 duration proposed 2 turns.

## 10. Ibuki Shingo 97

`fighter.shingo97` · SR · Blue · Japan

**Passive (On-Field; Own):** Upon Decreasing enemy's reduced ult gauge will increase fighter's ult gauge (excluding Ultimate Card Skill)

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=2850; Attack=300; Defense=230; Health=4400; Pierce_Rate=15; Resistance=15; Regeneration=10; Critical_Chance=25; Critical_Damage=140; Critical_Resistance=15; Critical_Defense=20; Recovery_Rate=115; Block_Chance=10; Block_Power=20; Lifesteal=5; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Attack

- **R1 (starting value):** Inflict damage equal to 150% of ATK, Depletes enemy ult gauge by 1 gauges
- **R2 (starting value):** Inflict damage equal to 300% of ATK, Depletes enemy ult gauge by 2 gauges
- **R3 (source):** Inflict damage equal to 450% of ATK, Depletes enemy ult gauge by 3 gauges
### Skill 2 — Single-Target / Debuff

- **R1 (starting value):** Inflict damage equal to 116.67% of ATK, decrease defense power 20% 1 turns
- **R2 (starting value):** Inflict damage equal to 233.33% of ATK, decrease defense power 40% 2 turns
- **R3 (source):** Inflict damage equal to 350% of ATK, decrease defense power 60% 2 turns

### Ultimate constellation sequence

- **C0/5:** Inflict damage equal to 550% of ATK and Decrease enemy's Resistance 50% 2 turns Decrease ult gauge 5 gauges
- **C1/5:** Inflict damage equal to 577.5% of ATK and Decrease enemy's Resistance 50% 2 turns Decrease ult gauge 5 gauges
- **C2/5:** Inflict damage equal to 605% of ATK and Decrease enemy's Resistance 50% 2 turns Decrease ult gauge 5 gauges
- **C3/5:** Inflict damage equal to 632.5% of ATK and Decrease enemy's Resistance 50% 2 turns Decrease ult gauge 5 gauges
- **C4/5:** Inflict damage equal to 660% of ATK and Decrease enemy's Resistance 50% 2 turns Decrease ult gauge 5 gauges
- **C5/5:** Inflict damage equal to 687.5% of ATK and Decrease enemy's Resistance 50% 2 turns Decrease ult gauge 5 gauges

**Holy Relic (disabled):** Not supplied.

**Review:** All stats generated. PG gain proposed equal to actual PG removed, once per card; excludes ultimate.

## 11. Asumiya Athena 94

`fighter.athena94` · SSR · Yellow · Women, Chinese, Psycho Soldier

**Passive (SUB; Women):** Increase Women allies's Attack-related stats by 15%

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=3501; Attack=500; Defense=370; Health=5900; Pierce_Rate=20; Resistance=20; Regeneration=50; Critical_Chance=15; Critical_Damage=130; Critical_Resistance=40; Critical_Defense=30; Recovery_Rate=140; Block_Chance=0; Block_Power=0; Lifesteal=0; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Attack

- **R1 (starting value):** Inflict damage equal to 133.33% of ATK, Decrease enemy recovery rate by 26.67% 1 turns
- **R2 (starting value):** Inflict damage equal to 266.67% of ATK, Decrease enemy recovery rate by 53.33% 2 turns
- **R3 (source):** Inflict damage equal to 400% of ATK, Decrease enemy recovery rate by 80% 2 turns
### Skill 2 — AOE / Heal

- **R1 (starting value):** Heal the entire team for 108.33% of own ATK and apply a Rejuvenates to alias (healing effect that restores 20% of the recovered HP for 1 turns)
- **R2 (starting value):** Heal the entire team for 216.67% of own ATK and apply a Rejuvenates to alias (healing effect that restores 40% of the recovered HP for 2 turns)
- **R3 (source):** Heal the entire team for 325% of own ATK and apply a Rejuvenates to alias (healing effect that restores 60% of the recovered HP for 2 turns)

### Ultimate constellation sequence

- **C0/5:** Heal the entire team for 300% of the own ATK, cleanse all Debuff effects from “female fighters”, Inflict damage equal to 375% of ATK
- **C1/5:** Heal the entire team for 315% of the own ATK, cleanse all Debuff effects from “female fighters”, Inflict damage equal to 393.75% of ATK
- **C2/5:** Heal the entire team for 330% of the own ATK, cleanse all Debuff effects from “female fighters”, Inflict damage equal to 412.5% of ATK
- **C3/5:** Heal the entire team for 345% of the own ATK, cleanse all Debuff effects from “female fighters”, Inflict damage equal to 431.25% of ATK
- **C4/5:** Heal the entire team for 360% of the own ATK, cleanse all Debuff effects from “female fighters”, Inflict damage equal to 450% of ATK
- **C5/5:** Heal the entire team for 375% of the own ATK, cleanse all Debuff effects from “female fighters”, Inflict damage equal to 468.75% of ATK

**Holy Relic (disabled):** Not supplied.

**Review:** Ultimate is composite: heal all allies, cleanse Women allies, damage all enemies. Separate target queries.

## 12. Sie Kensou 94

`fighter.kensou94` · R · Blue · Chinese, Psycho Soldier

**Passive (SUB; All):** Increases allies's Recovery Rate by 40%

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=2761; Attack=260; Defense=120; Health=3200; Pierce_Rate=30; Resistance=0; Regeneration=30; Critical_Chance=40; Critical_Damage=130; Critical_Resistance=0; Critical_Defense=0; Recovery_Rate=135; Block_Chance=0; Block_Power=0; Lifesteal=20; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Attack

- **R1 (starting value):** Inflicts Sever damage equal to 150% of ATK
- **R2 (starting value):** Inflicts Sever damage equal to 300% of ATK
- **R3 (source):** Inflicts Sever damage equal to 450% of ATK
### Skill 2 — Single-Target / Heal

- **R1 (starting value):** Heals diminished HP of all allies by 16.67% and Removes Debuffs
- **R2 (starting value):** Heals diminished HP of all allies by 33.33% and Removes Debuffs
- **R3 (source):** Heals diminished HP of all allies by 50% and Removes Debuffs

### Ultimate constellation sequence

- **C0/5:** Restores 50% HP to one ally, Increases Their Basic Stats by 25%, and Grants a Debuff Immunity for 3 turns
- **C1/5:** Restores 52.5% HP to one ally, Increases Their Basic Stats by 25%, and Grants a Debuff Immunity for 3 turns
- **C2/5:** Restores 55% HP to one ally, Increases Their Basic Stats by 25%, and Grants a Debuff Immunity for 3 turns
- **C3/5:** Restores 57.5% HP to one ally, Increases Their Basic Stats by 25%, and Grants a Debuff Immunity for 3 turns
- **C4/5:** Restores 60% HP to one ally, Increases Their Basic Stats by 25%, and Grants a Debuff Immunity for 3 turns
- **C5/5:** Restores 62.5% HP to one ally, Increases Their Basic Stats by 25%, and Grants a Debuff Immunity for 3 turns

**Holy Relic (disabled):** Not supplied.

**Review:** Skill 2 says Single target but description all allies: follow all allies. Ultimate target field Heal is a category; propose SelectedAlly. Ultimate stat buff duration proposed 3 turns.

## 13. Chin Gentsai 94

`fighter.chin94` · R · Green · Psycho Soldier, Chinese

**Passive (SUB; Green attribute):** Increases Green Attribute Team's  HP-related stats by 20%

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=2630; Attack=320; Defense=120; Health=4400; Pierce_Rate=10; Resistance=10; Regeneration=5; Critical_Chance=10; Critical_Damage=150; Critical_Resistance=20; Critical_Defense=10; Recovery_Rate=100; Block_Chance=0; Block_Power=0; Lifesteal=0; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Attack

- **R1 (starting value):** Inflicts damage equal to 150% of ATK applies Ignite enemy for 1 turns
- **R2 (starting value):** Inflicts damage equal to 300% of ATK applies Ignite enemy for 2 turns
- **R3 (source):** Inflicts damage equal to 450% of ATK applies Ignite enemy for 3 turns
### Skill 2 — Single-Target / Debuff

- **R1 (starting value):** Inflicts damage equal to 120% of ATK Removes Buff and applies 1 ignite to enemy for 1 turns
- **R2 (starting value):** Inflicts damage equal to 240% of ATK Removes Buff and applies 2 ignite to enemy for 2 turns
- **R3 (source):** Inflicts damage equal to 360% of ATK Removes Buff and applies 3 ignite to enemy for 3 turns

### Ultimate constellation sequence

- **C0/5:** Inflicts Weakpoint damage equal to 210% of ATK
- **C1/5:** Inflicts Weakpoint damage equal to 220.5% of ATK
- **C2/5:** Inflicts Weakpoint damage equal to 231% of ATK
- **C3/5:** Inflicts Weakpoint damage equal to 241.5% of ATK
- **C4/5:** Inflicts Weakpoint damage equal to 252% of ATK
- **C5/5:** Inflicts Weakpoint damage equal to 262.5% of ATK

**Holy Relic (disabled):** Not supplied.


## 14. Terry Bogard 96

`fighter.terry96` · SSR · Blue · Fatal Fury, American

**Passive (On-Field; All):** Reduces enemy Pierce Rate and Attack by 6% at the start of own turn, up to (30% maximum)

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=3524; Attack=420; Defense=450; Health=6000; Pierce_Rate=5; Resistance=50; Regeneration=10; Critical_Chance=10; Critical_Damage=120; Critical_Resistance=60; Critical_Defense=0; Recovery_Rate=160; Block_Chance=5; Block_Power=25; Lifesteal=10; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Attack

- **R1 (starting value):** Deals damage equal to 120% of ATK and reduces enemy Critical Chance by 26.67% for 1 turns
- **R2 (starting value):** Deals damage equal to 240% of ATK and reduces enemy Critical Chance by 53.33% for 2 turns
- **R3 (source):** Deals damage equal to 360% of ATK and reduces enemy Critical Chance by 80% for 2 turns
### Skill 2 — AOE / Debuff

- **R1 (starting value):** Reduces enemy defense-related stats  by 13.33% for 1 turns
- **R2 (starting value):** Reduces enemy defense-related stats  by 26.67% for 2 turns
- **R3 (source):** Reduces enemy defense-related stats  by 40% for 3 turns

### Ultimate constellation sequence

- **C0/5:** Inflict Charge damage equal to 450% of ATK
- **C1/5:** Inflict Charge damage equal to 472.5% of ATK
- **C2/5:** Inflict Charge damage equal to 495% of ATK
- **C3/5:** Inflict Charge damage equal to 517.5% of ATK
- **C4/5:** Inflict Charge damage equal to 540% of ATK
- **C5/5:** Inflict Charge damage equal to 562.5% of ATK

**Holy Relic (disabled):** Not supplied.


## 15. Joe Higashi 94

`fighter.joe94` · R · Red · Fatal Fury

**Passive (SUB; All):** Increases allies's Damage Dealt in PvP by 15%

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=2750; Attack=250; Defense=200; Health=4500; Pierce_Rate=5; Resistance=0; Regeneration=10; Critical_Chance=15; Critical_Damage=120; Critical_Resistance=0; Critical_Defense=30; Recovery_Rate=130; Block_Chance=30; Block_Power=20; Lifesteal=0; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — AOE / Debuff

- **R1 (starting value):** Inflicts damage equal to 70% of ATK, applies bleeding to enemy for 1 turns
- **R2 (starting value):** Inflicts damage equal to 140% of ATK, applies bleeding to enemy for 2 turns
- **R3 (source):** Inflicts damage equal to 210% of ATK, applies bleeding to enemy for 3 turns
### Skill 2 — AOE / Debuff

- **R1 (starting value):** Inflicts damage equal to 60% of ATK,  Disable enemy from use Healing cards for 1 turns
- **R2 (starting value):** Inflicts damage equal to 120% of ATK,  Disable enemy from use Healing cards for 2 turns
- **R3 (source):** Inflicts damage equal to 180% of ATK,  Disable enemy from use Healing cards for 2 turns

### Ultimate constellation sequence

- **C0/5:** Inflict Rupture damage equal to 500% of ATK
- **C1/5:** Inflict Rupture damage equal to 525% of ATK
- **C2/5:** Inflict Rupture damage equal to 550% of ATK
- **C3/5:** Inflict Rupture damage equal to 575% of ATK
- **C4/5:** Inflict Rupture damage equal to 600% of ATK
- **C5/5:** Inflict Rupture damage equal to 625% of ATK

**Holy Relic (disabled):** Not supplied.

**Review:** Passive text specifies PvP despite missing mode tag: normalize mode to PvPOnly.

## 16. Joe Higashi 96

`fighter.joe96` · R · Blue · Fatal Fury

**Passive (SUB; Blue Attributes ):** Increases Blue attribute allies Defense by 60%

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=2750; Attack=250; Defense=200; Health=4500; Pierce_Rate=5; Resistance=0; Regeneration=10; Critical_Chance=15; Critical_Damage=120; Critical_Resistance=0; Critical_Defense=30; Recovery_Rate=130; Block_Chance=30; Block_Power=20; Lifesteal=0; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — AOE / Debuff

- **R1 (starting value):** Inflicts damage equal to 70% of ATK applies bleeding to enemy for 1 turns
- **R2 (starting value):** Inflicts damage equal to 140% of ATK applies bleeding to enemy for 2 turns
- **R3 (source):** Inflicts damage equal to 210% of ATK applies bleeding to enemy for 3 turns
### Skill 2 — Single-Target / Attack

- **R1 (starting value):** Inflict Rupture damage of 133.33% ATK
- **R2 (starting value):** Inflict Rupture damage of 266.67% ATK
- **R3 (source):** Inflict Rupture damage of 400% ATK

### Ultimate constellation sequence

- **C0/5:** Inflict Rupture damage equal to 500% of ATK
- **C1/5:** Inflict Rupture damage equal to 525% of ATK
- **C2/5:** Inflict Rupture damage equal to 550% of ATK
- **C3/5:** Inflict Rupture damage equal to 575% of ATK
- **C4/5:** Inflict Rupture damage equal to 600% of ATK
- **C5/5:** Inflict Rupture damage equal to 625% of ATK

**Holy Relic (disabled):** Not supplied.


## 17. Andy Bogard 94

`fighter.andy94` · R · Yellow · Fatal Fury, American

**Passive (On-Field; Own):** If self not damaged in that turn, gains 1 point ult gauge to self

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=2650; Attack=310; Defense=130; Health=3600; Pierce_Rate=30; Resistance=5; Regeneration=10; Critical_Chance=20; Critical_Damage=135; Critical_Resistance=5; Critical_Defense=10; Recovery_Rate=110; Block_Chance=5; Block_Power=20; Lifesteal=5; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — AOE / Buff

- **R1 (starting value):** Increases allies's Pierce Rate by 20% for 1 turns
- **R2 (starting value):** Increases allies's Pierce Rate by 40% for 2 turns
- **R3 (source):** Increases allies's Pierce Rate by 60% for 3 turns
### Skill 2 — AOE / Debuff

- **R1 (starting value):** Inflict Explodes damage equal to 133.33% of ATK (applies Explosion to enemy)
- **R2 (starting value):** Inflict Explodes damage equal to 266.67% of ATK (applies Explosion to enemy)
- **R3 (source):** Inflict Explodes damage equal to 400% of ATK (applies Explosion to enemy)

### Ultimate constellation sequence

- **C0/5:** Inflict 225% of ATK, with a 40% chance to stun the enemy for 1 turn
- **C1/5:** Inflict 236.25% of ATK, with a 40% chance to stun the enemy for 1 turn
- **C2/5:** Inflict 247.5% of ATK, with a 40% chance to stun the enemy for 1 turn
- **C3/5:** Inflict 258.75% of ATK, with a 40% chance to stun the enemy for 1 turn
- **C4/5:** Inflict 270% of ATK, with a 40% chance to stun the enemy for 1 turn
- **C5/5:** Inflict 281.25% of ATK, with a 40% chance to stun the enemy for 1 turn

**Holy Relic (disabled):** Not supplied.

**Review:** All stats generated. Explosion delay missing: propose 2 affected-owner turn ends. Passive observes own side turn-end damage ledger.

## 18. Leona Heidern 96

`fighter.leona96` · SSR · Green · Orochi, Ikari, Women

**Passive (SUB; Own):** Increases own lifesteal by 15%

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=4000; Attack=560; Defense=290; Health=5500; Pierce_Rate=25; Resistance=25; Regeneration=20; Critical_Chance=25; Critical_Damage=160; Critical_Resistance=25; Critical_Defense=25; Recovery_Rate=100; Block_Chance=0; Block_Power=0; Lifesteal=10; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Debuff

- **R1 (starting value):** Inflict damage equal to 125% of ATK applies bleed debuff on the enemy for 1 turns
- **R2 (starting value):** Inflict damage equal to 250% of ATK applies bleed debuff on the enemy for 2 turns
- **R3 (source):** Inflict damage equal to 375% of ATK applies bleed debuff on the enemy for 3 turns
### Skill 2 — AOE / Buff

- **R1 (starting value):** Increases allies' Attack-related stats by 10% for 1 turns
- **R2 (starting value):** Increases allies' Attack-related stats by 20% for 2 turns
- **R3 (source):** Increases allies' Attack-related stats by 30% for 3 turns

### Ultimate constellation sequence

- **C0/5:** Inflicts damage equal to 500% of ATK, Increases damage dealt by 30% when attacking enemies with the lowest, Recovers own HP by 8% upon killing
- **C1/5:** Inflicts damage equal to 525% of ATK, Increases damage dealt by 30% when attacking enemies with the lowest, Recovers own HP by 8% upon killing
- **C2/5:** Inflicts damage equal to 550% of ATK, Increases damage dealt by 30% when attacking enemies with the lowest, Recovers own HP by 8% upon killing
- **C3/5:** Inflicts damage equal to 575% of ATK, Increases damage dealt by 30% when attacking enemies with the lowest, Recovers own HP by 8% upon killing
- **C4/5:** Inflicts damage equal to 600% of ATK, Increases damage dealt by 30% when attacking enemies with the lowest, Recovers own HP by 8% upon killing
- **C5/5:** Inflicts damage equal to 625% of ATK, Increases damage dealt by 30% when attacking enemies with the lowest, Recovers own HP by 8% upon killing

**Holy Relic (disabled):** Not supplied.

**Review:** SUB self Lifesteal is ineffective while benched if reserve-only: propose SUB means active-or-reserve. Ultimate lowest what is incomplete: propose lowest current HP percentage, ties all qualify.

## 19. Clark Still 94

`fighter.clark94` · SR · Blue · Ikari

**Passive (SUB; Blue Attributes ):** Increases Blue Attributes allies's Attack-related stats by 10%

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=3300; Attack=400; Defense=120; Health=3000; Pierce_Rate=30; Resistance=0; Regeneration=15; Critical_Chance=35; Critical_Damage=160; Critical_Resistance=0; Critical_Defense=10; Recovery_Rate=100; Block_Chance=0; Block_Power=0; Lifesteal=10; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Attack

- **R1 (starting value):** Inflict Sever damage equal to 150% of  ATK
- **R2 (starting value):** Inflict Sever damage equal to 300% of  ATK
- **R3 (source):** Inflict Sever damage equal to 450% of  ATK
### Skill 2 — Single-Target / Attack

- **R1 (starting value):** Inflict Rupture damage equal to 133.33% of ATK
- **R2 (starting value):** Inflict Rupture damage equal to 266.67% of ATK
- **R3 (source):** Inflict Rupture damage equal to 400% of ATK

### Ultimate constellation sequence

- **C0/5:** Inflict Shatter damage equal to 500% of ATK
- **C1/5:** Inflict Shatter damage equal to 525% of ATK
- **C2/5:** Inflict Shatter damage equal to 550% of ATK
- **C3/5:** Inflict Shatter damage equal to 575% of ATK
- **C4/5:** Inflict Shatter damage equal to 600% of ATK
- **C5/5:** Inflict Shatter damage equal to 625% of ATK

**Holy Relic (disabled):** Not supplied.


## 20. Ralf Jone 94

`fighter.ralf94` · R · Green · Ikari

**Passive (SUB; All):** Increases allies's Ultimate Card damage by 40%

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=2520; Attack=150; Defense=300; Health=5100; Pierce_Rate=0; Resistance=15; Regeneration=0; Critical_Chance=5; Critical_Damage=120; Critical_Resistance=20; Critical_Defense=20; Recovery_Rate=110; Block_Chance=5; Block_Power=0; Lifesteal=5; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Attack

- **R1 (starting value):** Inflict Detonate damage equal to 133.33% of ATK
- **R2 (starting value):** Inflict Detonate damage equal to 266.67% of ATK
- **R3 (source):** Inflict Detonate damage equal to 400% of ATK
### Skill 2 — AOE / Attack

- **R1 (starting value):** Inflict damage equal to 83.33% of ATK, Depletes enemy ult gauge by 1 gauges
- **R2 (starting value):** Inflict damage equal to 166.67% of ATK, Depletes enemy ult gauge by 2 gauges
- **R3 (source):** Inflict damage equal to 250% of ATK, Depletes enemy ult gauge by 2 gauges

### Ultimate constellation sequence

- **C0/5:** Inflict damage equal to 800% of ATK
- **C1/5:** Inflict damage equal to 840% of ATK
- **C2/5:** Inflict damage equal to 880% of ATK
- **C3/5:** Inflict damage equal to 920% of ATK
- **C4/5:** Inflict damage equal to 960% of ATK
- **C5/5:** Inflict damage equal to 1000% of ATK

**Holy Relic (disabled):** Not supplied.


## 21. Ryo Sakazaki 94

`fighter.ryo94` · SR · Yellow · Art of Fighting

**Passive (On-Field; Own):** Reflects 15% of damage received

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=2550; Attack=300; Defense=290; Health=5500; Pierce_Rate=5; Resistance=0; Regeneration=30; Critical_Chance=5; Critical_Damage=130; Critical_Resistance=0; Critical_Defense=30; Recovery_Rate=105; Block_Chance=25; Block_Power=20; Lifesteal=0; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Attack

- **R1 (starting value):** Inflict damage equal to 166.67% of ATK, Fills increases own ult gauge by 1 gauges
- **R2 (starting value):** Inflict damage equal to 333.33% of ATK, Fills increases own ult gauge by 2 gauges
- **R3 (source):** Inflict damage equal to 500% of ATK, Fills increases own ult gauge by 2 gauges
### Skill 2 — Single-Target / Stance

- **R1 (starting value):** Assume a stance, grants a buff debuff Immunity and Evasion After the turn ends, restores HP by 26.67% of the damage received, for 1 turn
- **R2 (starting value):** Assume a stance, grants a buff debuff Immunity and Evasion After the turn ends, restores HP by 53.33% of the damage received, for 1 turn
- **R3 (source):** Assume a stance, grants a buff debuff Immunity and Evasion After the turn ends, restores HP by 80% of the damage received, for 1 turn

### Ultimate constellation sequence

- **C0/5:** Inflict damage equal to 300% of ATK, reduces enemy's attack-related stats by 40% for 2 turns
- **C1/5:** Inflict damage equal to 315% of ATK, reduces enemy's attack-related stats by 40% for 2 turns
- **C2/5:** Inflict damage equal to 330% of ATK, reduces enemy's attack-related stats by 40% for 2 turns
- **C3/5:** Inflict damage equal to 345% of ATK, reduces enemy's attack-related stats by 40% for 2 turns
- **C4/5:** Inflict damage equal to 360% of ATK, reduces enemy's attack-related stats by 40% for 2 turns
- **C5/5:** Inflict damage equal to 375% of ATK, reduces enemy's attack-related stats by 40% for 2 turns

**Holy Relic (disabled):** Increases resistance, critical resistance, and critical defense by 4% per gauge are gained, and an additional 40% when the gauge is full

**Review:** Stance Evade magnitude absent: propose guaranteed evade of next hostile damaging card; still permits later damage. Relic gauge gain means current gauge, not lifetime gains (proposal).

## 22. Yuri Sakazaki 94

`fighter.yuri94` · SR · Green · Art of Fighting, Women

**Passive (SUB; Green attribute):** Increases Green attribute team attack-related stats by 10%

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=2820; Attack=250; Defense=130; Health=3675; Pierce_Rate=10; Resistance=10; Regeneration=20; Critical_Chance=35; Critical_Damage=150; Critical_Resistance=0; Critical_Defense=0; Recovery_Rate=130; Block_Chance=15; Block_Power=30; Lifesteal=9; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — AOE / Debuff

- **R1 (starting value):** Inflict damage equal to 60% of ATK and disable enemy from using attack cards for 1 turns
- **R2 (starting value):** Inflict damage equal to 120% of ATK and disable enemy from using attack cards for 2 turns
- **R3 (source):** Inflict damage equal to 180% of ATK and disable enemy from using attack cards for 2 turns
### Skill 2 — AOE / Buff

- **R1 (starting value):** Increases the team's attack by 20% for 1 turns
- **R2 (starting value):** Increases the team's attack by 40% for 2 turns
- **R3 (source):** Increases the team's attack by 60% for 3 turns

### Ultimate constellation sequence

- **C0/5:** Inflict damage equal to 375% of ATK and applies Shock debuff for 4 turns
- **C1/5:** Inflict damage equal to 393.75% of ATK and applies Shock debuff for 4 turns
- **C2/5:** Inflict damage equal to 412.5% of ATK and applies Shock debuff for 4 turns
- **C3/5:** Inflict damage equal to 431.25% of ATK and applies Shock debuff for 4 turns
- **C4/5:** Inflict damage equal to 450% of ATK and applies Shock debuff for 4 turns
- **C5/5:** Inflict damage equal to 468.75% of ATK and applies Shock debuff for 4 turns

**Holy Relic (disabled):** When a "Art of Fighting Team" attacks, restores HP by 6%

**Review:** Relic team predicate/heal recipient ambiguous; retain disabled pending explicit definition.

## 23. Yuri Sakazaki 98

`fighter.yuri98` · SSR · Blue · Art of Fighting, Women

**Passive (SUB; All):** When an enemy is defeated, Decreasethe gauge of all enemies by 2 gauges

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=3619; Attack=420; Defense=300; Health=6300; Pierce_Rate=5; Resistance=10; Regeneration=0; Critical_Chance=30; Critical_Damage=140; Critical_Resistance=10; Critical_Defense=20; Recovery_Rate=110; Block_Chance=15; Block_Power=30; Lifesteal=9; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Attack

- **R1 (starting value):** Inflict damage equal to 150% of ATK, Depletes enemy ult gauge by 1 gauges
- **R2 (starting value):** Inflict damage equal to 300% of ATK, Depletes enemy ult gauge by 2 gauges
- **R3 (source):** Inflict damage equal to 450% of ATK, Depletes enemy ult gauge by 3 gauges
### Skill 2 — AOE / Debuff

- **R1 (starting value):** Inflict damage equal to 60% of ATK and disable enemy from using attack cards for 1 turns
- **R2 (starting value):** Inflict damage equal to 120% of ATK and disable enemy from using attack cards for 2 turns
- **R3 (source):** Inflict damage equal to 180% of ATK and disable enemy from using attack cards for 2 turns

### Ultimate constellation sequence

- **C0/5:** Inflict damage equal to 375% of ATK and applies Shock debuff for 4 turns
- **C1/5:** Inflict damage equal to 393.75% of ATK and applies Shock debuff for 4 turns
- **C2/5:** Inflict damage equal to 412.5% of ATK and applies Shock debuff for 4 turns
- **C3/5:** Inflict damage equal to 431.25% of ATK and applies Shock debuff for 4 turns
- **C4/5:** Inflict damage equal to 450% of ATK and applies Shock debuff for 4 turns
- **C5/5:** Inflict damage equal to 468.75% of ATK and applies Shock debuff for 4 turns

**Holy Relic (disabled):** When a "Art of Fighting Team" attacks, restores HP by 6%

**Review:** On enemy defeat PG drain can cascade: once per root action starting value. Relic remains disabled.

## 24. Robert Garcia 94

`fighter.robert94` · SSR · Blue · Art of Fighting

**Passive (On-Field; Own):** Increases Critical Chance equal to his own critical Resistance

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=4130; Attack=620; Defense=290; Health=5900; Pierce_Rate=35; Resistance=5; Regeneration=10; Critical_Chance=20; Critical_Damage=190; Critical_Resistance=40; Critical_Defense=15; Recovery_Rate=120; Block_Chance=10; Block_Power=10; Lifesteal=6; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Attack

- **R1 (starting value):** Inflicts Spike damage equal to 133.33% of ATK
- **R2 (starting value):** Inflicts Spike damage equal to 266.67% of ATK
- **R3 (source):** Inflicts Spike damage equal to 400% of ATK
### Skill 2 — Single-Target / Attack

- **R1 (starting value):** Inflicts damage equal to 150% of ATK and increases own critical damage by 26.67% for 1 turns
- **R2 (starting value):** Inflicts damage equal to 300% of ATK and increases own critical damage by 53.33% for 2 turns
- **R3 (source):** Inflicts damage equal to 450% of ATK and increases own critical damage by 80% for 2 turns

### Ultimate constellation sequence

- **C0/5:** Inflicts damage equal to 400% of ATK and increases team's Critical Chance by 7% for 3 turns
- **C1/5:** Inflicts damage equal to 420% of ATK and increases team's Critical Chance by 14% for 3 turns
- **C2/5:** Inflicts damage equal to 440% of ATK and increases team's Critical Chance by 21% for 3 turns
- **C3/5:** Inflicts damage equal to 460% of ATK and increases team's Critical Chance by 28% for 3 turns
- **C4/5:** Inflicts damage equal to 480% of ATK and increases team's Critical Chance by 35% for 3 turns
- **C5/5:** Inflicts damage equal to 500% of ATK and increases team's Critical Chance by 42% for 3 turns

**Holy Relic (disabled):** When landing a critical hit, ignores 30% of the enemy's critical defense

**Review:** Explicit +7 percentage points per ultimate level retained across C0..C6. Relic ignore30% uses multiplicative removal of target CritDefense.

## 25. Choi Bouge 94

`fighter.choi94` · R · Red · Korea Justice

**Passive (SUB; Women):** Reduces the attack-related stats of Women Trait enemy by 10%

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=2761; Attack=260; Defense=120; Health=3200; Pierce_Rate=30; Resistance=0; Regeneration=30; Critical_Chance=40; Critical_Damage=130; Critical_Resistance=0; Critical_Defense=0; Recovery_Rate=135; Block_Chance=0; Block_Power=0; Lifesteal=20; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — AOE / Debuff

- **R1 (starting value):** Inflicts damage equal to 125% of ATK and causes Bleed to enemy for 1 turns
- **R2 (starting value):** Inflicts damage equal to 250% of ATK and causes Bleed to enemy for 2 turns
- **R3 (source):** Inflicts damage equal to 375% of ATK and causes Bleed to enemy for 3 turns
### Skill 2 — AOE / Debuff

- **R1 (starting value):** Inflicts damage equal to 93.33% of ATK and causes Infect to enemy for 1 turns
- **R2 (starting value):** Inflicts damage equal to 186.67% of ATK and causes Infect to enemy for 2 turns
- **R3 (source):** Inflicts damage equal to 280% of ATK and causes Infect to enemy for 2 turns

### Ultimate constellation sequence

- **C0/5:** Inflict damage equal to 375% of ATK and Disable enemy from using attack cards for 2 turns
- **C1/5:** Inflict damage equal to 393.75% of ATK and Disable enemy from using attack cards for 2 turns
- **C2/5:** Inflict damage equal to 412.5% of ATK and Disable enemy from using attack cards for 2 turns
- **C3/5:** Inflict damage equal to 431.25% of ATK and Disable enemy from using attack cards for 2 turns
- **C4/5:** Inflict damage equal to 450% of ATK and Disable enemy from using attack cards for 2 turns
- **C5/5:** Inflict damage equal to 468.75% of ATK and Disable enemy from using attack cards for 2 turns

**Holy Relic (disabled):** Not supplied.


## 26. Chang Koehan 94

`fighter.chang94` · R · Green · Korea Justice

**Passive (SUB; Green attribute):** Increases the defense of the “Green attribute team” by 60%

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=2371; Attack=130; Defense=300; Health=3400; Pierce_Rate=0; Resistance=10; Regeneration=0; Critical_Chance=20; Critical_Damage=110; Critical_Resistance=10; Critical_Defense=10; Recovery_Rate=110; Block_Chance=5; Block_Power=50; Lifesteal=5; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Debuff

- **R1 (starting value):** Inflicts damage equal to 83.33% of ATK, then Stuns for 1 turns
- **R2 (starting value):** Inflicts damage equal to 166.67% of ATK, then Stuns for 2 turns
- **R3 (source):** Inflicts damage equal to 250% of ATK, then Stuns for 2 turns
### Skill 2 — AOE / Debuff

- **R1 (starting value):** Inflicts damage equal to 60% of ATK and has an 26.67% chance to Stuns for 1 turn
- **R2 (starting value):** Inflicts damage equal to 120% of ATK and has an 53.33% chance to Stuns for 1 turn
- **R3 (source):** Inflicts damage equal to 180% of ATK and has an 80% chance to Stuns for 1 turn

### Ultimate constellation sequence

- **C0/5:** Inflicts Shatter damage equal to 425% of ATK
- **C1/5:** Inflicts Shatter damage equal to 446.25% of ATK
- **C2/5:** Inflicts Shatter damage equal to 467.5% of ATK
- **C3/5:** Inflicts Shatter damage equal to 488.75% of ATK
- **C4/5:** Inflicts Shatter damage equal to 510% of ATK
- **C5/5:** Inflicts Shatter damage equal to 531.25% of ATK

**Holy Relic (disabled):** Not supplied.


## 27. Brian 94

`fighter.brian94` · R · Blue · American

**Passive (SUB; All):** Increases the allies Block Chance by 40%

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=2700; Attack=260; Defense=250; Health=4300; Pierce_Rate=10; Resistance=20; Regeneration=15; Critical_Chance=10; Critical_Damage=120; Critical_Resistance=15; Critical_Defense=20; Recovery_Rate=120; Block_Chance=20; Block_Power=30; Lifesteal=5; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Attack

- **R1 (starting value):** Inflict Rupture damage of 133.33% ATK
- **R2 (starting value):** Inflict Rupture damage of 266.67% ATK
- **R3 (source):** Inflict Rupture damage of 400% ATK
### Skill 2 — AOE / Buff

- **R1 (starting value):** Creates a barrier around all allies equal to 125% of Attack for 1 turns
- **R2 (starting value):** Creates a barrier around all allies equal to 250% of Attack for 2 turns
- **R3 (source):** Creates a barrier around all allies equal to 375% of Attack for 2 turns

### Ultimate constellation sequence

- **C0/5:** Inflict Detonate damage equal to 450% of ATK
- **C1/5:** Inflict Detonate damage equal to 472.5% of ATK
- **C2/5:** Inflict Detonate damage equal to 495% of ATK
- **C3/5:** Inflict Detonate damage equal to 517.5% of ATK
- **C4/5:** Inflict Detonate damage equal to 540% of ATK
- **C5/5:** Inflict Detonate damage equal to 562.5% of ATK

**Holy Relic (disabled):** Not supplied.

**Review:** All stats generated; Barrier duration source 2 turns.

## 28. Lucky 94

`fighter.lucky94` · R · Green · American

**Passive (On-Field; Own):** Increases own Control Rate by 18%

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=2600; Attack=230; Defense=170; Health=3900; Pierce_Rate=10; Resistance=15; Regeneration=10; Critical_Chance=10; Critical_Damage=120; Critical_Resistance=10; Critical_Defense=15; Recovery_Rate=115; Block_Chance=10; Block_Power=20; Lifesteal=5; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — AOE / Debuff

- **R1 (starting value):** Inflicts damage equal to 41.67% of ATK, has a 8.33% chance to Disables everything including Ultimate Moves except for Debuff Card 1 turns
- **R2 (starting value):** Inflicts damage equal to 83.33% of ATK, has a 16.67% chance to Disables everything including Ultimate Moves except for Debuff Card 2 turns
- **R3 (source):** Inflicts damage equal to 125% of ATK, has a 25% chance to Disables everything including Ultimate Moves except for Debuff Card 2 turns
### Skill 2 — AOE / Debuff

- **R1 (starting value):** Inflicts damage equal to 65% of ATK and has a 16.67% chance to Remove Buffs and Stun the enemy for 1 turns
- **R2 (starting value):** Inflicts damage equal to 130% of ATK and has a 33.33% chance to Remove Buffs and Stun the enemy for 2 turns
- **R3 (source):** Inflicts damage equal to 195% of ATK and has a 50% chance to Remove Buffs and Stun the enemy for 3 turns

### Ultimate constellation sequence

- **C0/5:** Inflicts damage equal to 250% of ATK and has a 40% chance to Stun the enemy for 1 turn
- **C1/5:** Inflicts damage equal to 262.5% of ATK and has a 40% chance to Stun the enemy for 1 turn
- **C2/5:** Inflicts damage equal to 275% of ATK and has a 40% chance to Stun the enemy for 1 turn
- **C3/5:** Inflicts damage equal to 287.5% of ATK and has a 40% chance to Stun the enemy for 1 turn
- **C4/5:** Inflicts damage equal to 300% of ATK and has a 40% chance to Stun the enemy for 1 turn
- **C5/5:** Inflicts damage equal to 312.5% of ATK and has a 40% chance to Stun the enemy for 1 turn

**Holy Relic (disabled):** Not supplied.

**Review:** All stats generated. Skill 2 shared 50% proc gates both removal and stun (proposal).

## 29. Heavy D! 94

`fighter.heavyd94` · SR · Red · American

**Passive (On-Field; All):** Reduces enemy's Critical Chance by half of his own Critical Resistance

**Stats** (points for CC/ATK/DEF/HP, percent for the remainder): Class_Combat=3000; Attack=390; Defense=180; Health=4100; Pierce_Rate=20; Resistance=10; Regeneration=10; Critical_Chance=30; Critical_Damage=160; Critical_Resistance=40; Critical_Defense=15; Recovery_Rate=110; Block_Chance=5; Block_Power=20; Lifesteal=5; Avoidance_Rate=0; Evade_Rate=0; Control_Rate=100; Perception_Rate=100

### Skill 1 — Single-Target / Attack

- **R1 (starting value):** Inflicts damage equal to 133.33% of ATK, Increases own Critical Chance by 16.67% for 1 turns
- **R2 (starting value):** Inflicts damage equal to 266.67% of ATK, Increases own Critical Chance by 33.33% for 2 turns
- **R3 (source):** Inflicts damage equal to 400% of ATK, Increases own Critical Chance by 50% for 2 turns
### Skill 2 — Single-Target / Attack

- **R1 (starting value):** Inflicts damage equal to 200% of ATK
- **R2 (starting value):** Inflicts damage equal to 400% of ATK
- **R3 (source):** Inflicts damage equal to 600% of ATK

### Ultimate constellation sequence

- **C0/5:** Inflicts Breakthrough damage equal to 550% of ATK
- **C1/5:** Inflicts Breakthrough damage equal to 577.5% of ATK
- **C2/5:** Inflicts Breakthrough damage equal to 605% of ATK
- **C3/5:** Inflicts Breakthrough damage equal to 632.5% of ATK
- **C4/5:** Inflicts Breakthrough damage equal to 660% of ATK
- **C5/5:** Inflicts Breakthrough damage equal to 687.5% of ATK

**Holy Relic (disabled):** Not supplied.

**Review:** All stats generated. Passive halves own CritResistance value to form enemy CritChance reduction.
