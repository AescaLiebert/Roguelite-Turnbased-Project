---
slug: genshin-character-roster
status: draft
source: manual
gdd_tags: [genshin-roster, elemental-reactions, roster, tuning]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Genshin starter roster: five supplied kits and generated stats

Date: 2026-10-15. Canonical context: [GDD `@tag:genshin-roster`](../../../1_Inputs_Templates/GDD_Fighting_Allstar.md#genshin-starter-roster) and [elemental reactions](../../../1_Inputs_Templates/GDD_Fighting_Allstar.md#genshin-inspired-elemental-reactions). Implementation: [elemental plan](../genshin-element-system-arch-plan.md). D–F now authors all five kits in shared content and Unity assets under `Assets/Project/Data/Character/GenshinImpact`; the character registry includes them. Character art uses the existing placeholder fallback. No summon-banner publication is implied.

## Source, confirmed interpretations, and provenance

The owner supplied Amber, Kaeya, Lisa, Barbara, and Anemo Lumine's kits in this conversation. Rarity, attribute colors, listed coefficients/effects, and the common Gnosis clause are supplied data. All base stats below are generated **Starting values**, as requested; they are not official Genshin stats or playtested Fighting Allstar balance.

Owner-confirmed clarifications:

- Listed regular skills are Rank 3; listed ultimates are C0/5. Kaeya's team-wide Glacial Waltz becomes available at C5/5.
- 🗡️ targets one selected enemy; 🏹 targets all active enemies. These icons do not by themselves define close/ranged motion.
- Baron Bunny detonates on natural expiry only. Cleanse removes it safely.
- Baron Bunny's delayed Pyro attack and Glacial Waltz's Cryo retaliation may trigger reactions as explicit authored triggered attacks. Ordinary DOT ticks/reflected damage remain terminal.
- Amber doubles Baron Bunny, Overload, and explicitly Explosion-tagged damage. Other reactions are not doubled merely because they are reactions.
- Barbara's keyword is **Dissolve / บั่นทอน**: R1 drains 1 PG for 1 target turn; R2 drains 2 PG for 1 turn; R3 drains 2 PG for 2 turns, always at the affected enemy's turn end.
- Bunny's follow-up is ordinary Ignite (+10% damage taken), not a new burn DOT. It retains the source four-turn R3 duration.
- Frost Bite's −50% gauge generation changes the amount gained to 0.5 orb per ordinary action while Frost Bite lasts. +50% generation produces 1.5 orbs; fractions carry between actions. Explicit authored gauge grants/drains remain whole-orb operations. Spending an ultimate or reaching five orbs clears the remainder.
- Lumine's บดขยี้ is the existing **Shatter** attack keyword.

Implementation starting policies retain the documented recommendations: active providers unless SUB is explicitly stated; opening gauge once; one Waltz retaliation per living protected victim per hostile attacking card using actual HP lost; retaliation cannot trigger another retaliation or generic reflect. Barbara chooses another living active teammate by exact HP ratio, with formation-order tie-break, and pays once per owned action or periodic batch from actual HP damage. Melody snapshots Barbara's effective Max HP. Conductive is a permanent grey debuff, reduces DEF by 20% per stack, and is consumed before Lisa adds a new after-attack stack. These are prototype policies to evaluate in human balance/feel tests, not claims of final balance.

The owner approved the earlier elemental plan with “LGTM”; this roster extends it. Newly proposed kit interpretations and generated values are not retroactively source facts. Missing source fields remain visible below.

## Identity and proposed traits

| Proposed definition ID | Supplied rarity / attribute | Element | Proposed role | Proposed region trait |
| --- | --- | --- | --- | --- |
| `fighter.amber` | R / Red | Pyro | Delayed explosion setup; team explosion amplifier | Mondstadt |
| `fighter.kaeya` | SR / Blue | Cryo | Gauge disruption; reactive Cryo protection | Mondstadt |
| `fighter.lisa` | SR / Purple | Electro | Shock/Conductive setup and burst conversion | Mondstadt |
| `fighter.barbara` | SR / Blue | Hydro | Team sustain; Hydro reaction driver | Mondstadt |
| `fighter.lumine-anemo` | SSR / Green | Anemo | Spreading and Swirl support; useful SUB slot | Unassigned |

All five receive the supplied Genshin trait. Proposed canonical IDs: `series.genshin-impact`, `trait.genshin-impact`, and `trait.region.mondstadt`. Normalize the supplied “Monstadt” spelling as an input alias for the displayed Mondstadt trait. Region assignment is a proposed gameplay grouping; do not infer additional nationality/gender/weapon traits or assign original CSV row IDs.

Lumine is not automatically included in Barbara's region buff. This gives team selection a visible regional tradeoff while allowing the full Genshin requirement. No kit is Geo or Dendro, so these five cannot by themselves validate Crystallize or Dendro reactions; separate fixtures remain necessary.

## Generated level-one stats

All values in this table are **Starting values** at level 1, C0/5, unequipped, before passive contributions. Points apply to CC/ATK/DEF/HP; other rows are percentages. Crit Damage and Recovery are total multipliers: 150% = 1.50 and 110% = 1.10. Avoidance 100% is the current runtime's neutral divisor; it does not mean guaranteed evasion.

| Stat | Amber | Kaeya | Lisa | Barbara | Anemo Lumine |
| --- | ---: | ---: | ---: | ---: | ---: |
| Combat Class, authored baseline | 2700 | 3150 | 3100 | 3200 | 3500 |
| ATK | 280 | 350 | 390 | 230 | 420 |
| DEF | 150 | 260 | 180 | 240 | 300 |
| Max HP | 3600 | 4800 | 4200 | 5400 | 5600 |
| Pierce | 20% | 25% | 30% | 10% | 20% |
| Resistance | 10% | 15% | 10% | 20% | 15% |
| Regeneration | 5% | 5% | 5% | 5% | 5% |
| Crit Chance | 20% | 25% | 30% | 10% | 25% |
| Crit Damage | 150% | 150% | 150% | 150% | 150% |
| Crit Resistance | 5% | 10% | 5% | 15% | 10% |
| Crit Defense | 15% | 20% | 15% | 25% | 20% |
| Recovery | 110% | 110% | 110% | 110% | 110% |
| Block Chance | 5% | 10% | 5% | 10% | 10% |
| Block Power | 20% | 25% | 20% | 25% | 25% |
| Lifesteal | 0% | 0% | 0% | 0% | 0% |
| Evade | 0% | 0% | 0% | 0% | 0% |
| Perception | 100% | 100% | 100% | 100% | 100% |
| Control | 100% | 100% | 100% | 100% | 100% |
| Avoidance | 100% | 100% | 100% | 100% | 100% |
| Generic reflected damage | 0% | 0% | 0% | 0% | 0% |
| Element-specific resistance | 0% each | 0% each | 0% each | 0% each | 0% each |

Zero generic reflect is deliberate: Glacial Waltz is its own owned triggered attack, not a permanent reflected-damage stat. Zero Lifesteal keeps ordinary self-lifesteal separate from Barbara's ally heal and Waltz's authored healing.

Proposed **Starting growth profile:** ATK/DEF/HP each add 2135 basis points of their level-one value per level, matching the inspected current King94/WIP baseline profile rather than the smaller fallback class default. This is linear, not compound growth. Use the existing progression/CC calibration system and level cap; do not independently scale secondary stats or authored CC. Element resistance starts neutral and does not grow automatically. Adopt the existing [progression](../../../../../Assets/Project/UI/CharacterProgression.md) and [CC calibration](../../../../../Assets/Project/UI/CombatClassFormula.md) implementations, without changing their formulas.

ASSUMPTION: Comparisons at equal progression should preserve current combat's basic-stat scale rather than create a separate Genshin stat tier.
IMPACT: The table gives distinct roles while borrowing a current roster growth profile.
IF WRONG: Generated fighters could be consistently too weak/strong against the current roster, or Barbara's MaxHP scaling could dominate.
VALIDATE: Run the stat/growth and matchup checks below before publishing these profiles.

### Stat microtests and adjustment directions

- Compare level 1, intermediate progression, and max-level snapshots at identical gear/constellation. Pass if monotonic growth/CC follows existing calibration and the team does not gain an advantage solely through a different growth formula. If relative power diverges across levels, adjust the Genshin growth profile before changing source skills.
- Compare Lisa's burst, Kaeya's disruption/survival, Amber's explosion contribution, Barbara's healing, and Lumine's spread using mirrored sides and normalized attributes. Pass when each has a useful authored situation and a counterexample where another role is better. If Lisa/Amber overshadow all choices, lower generated offensive stats before altering supplied coefficients.
- Evaluate Barbara with and without her region aura, including self eligibility. If her HP, team healing, Melody Loop, and damage-based heal make incoming pressure irrelevant, reduce generated HP/DEF first and record the result; do not silently weaken the source percentages.
- Test all five bench choices at equal progression. Amber's low rarity must not remove her team role, and Lumine's SUB aura must not make her automatically optimal in every encounter. If a bench choice always wins, investigate aura scope and combat opportunities before changing rarity/stat gaps.
- Inspect crit/block/status outcomes independently. If Lisa cannot demonstrate her conditional Shock, tune her generated Crit Chance; if setup fails for unrelated accuracy reasons, verify the neutral Control/Avoidance import first.

These are required playtests, not passed results or claimed win-rate benchmarks.

## Rank-generation convention

Supplied R3 values stay intact. **Starting values:** R1/R2 damage/healing percentages use one-third/two-thirds of R3, rounded to two decimals; integer durations/gauge changes use ceiling with a minimum of one. Explicit keyword multipliers and passive-provided effects do not scale automatically with card rank. This follows the existing roster-draft convention; it does not certify lower-rank balance. For any separately displayed triggered payload below, its generated rank scaling is stated explicitly.

Test equal card/action investment at all ranks. Pass if lower ranks retain their intended setup/response role and merging is a situational decision. If repeated low-rank control wins every comparison, gate or shorten the generated rider before altering R3. If merging always wins, reduce generated rank advantages or improve low-rank utility.

## Amber — R / Red / Pyro

**Supplied passive:** double the team's explosion damage. Confirmed category: Baron Bunny, Overload, and explicitly Explosion-tagged packets. Proposed provider scope is On-Field because the source does not mark SUB. Apply the modifier once per eligible packet even if it has both reaction and Explosion metadata. Do not double Swirl, Melt/Vaporize's entire hit, Burning ticks, or generic reflected damage unless independently authored as Explosion.

| Ability | Supplied R3 / C0 contract | Generated R1 / R2 starting values |
| --- | --- | --- |
| Skill 1, AOE AttackDebuff | Normal damage 250% ATK; apply Baron Bunny for 1 target turn. On natural expiry, a Pyro attack deals 150% Amber ATK and applies ordinary Ignite for 4 turns unless that contact reacts. | Root hit 83.33% / 166.67%; expiry attack 50% / 100%; marker 1 / 1 turn; Ignite 2 / 3 turns. |
| Skill 2, single-target Attack | Normal damage 300% ATK with Weakpoint / จุดอ่อน. | 100% / 200% ATK; preserve the existing Weakpoint keyword rule. |
| Ultimate, AOE Attack | Pyro damage 300% ATK. | No regular card ranks; see constellation draft. |

Baron Bunny is a delayed marker, **not Pyro reaction fuel**. Its natural-expiry reason emits a distinct triggered attack with no card cost/PG gain; cleansing safely cancels it. Proposed: consumption, replacement, encounter cleanup, and target defeat are cancellation reasons too, not expiry. The marker cannot be consumed merely because it is a debuff/DOT-like object.

The expiry attack may trigger a reaction at its own damage contact. Its Explosion modifier is applied once; a resulting Overload is a separate Explosion packet and receives the modifier once independently. The resulting Overload remains terminal. Do not run Baron Bunny through the generic damage-dealt-based Explosion recipe: its supplied basis is Amber ATK.

**K-01 update:** the owner explicitly selected ordinary Ignite. Implementation snapshots Amber ATK when the marker is applied; natural expiry may still resolve if Amber has since died, using that owned snapshot, but her living-active explosion aura is then unavailable. Cleanse/replacement/target defeat/encounter cleanup cancel the old marker. A reacting contact consumes its rider opportunity rather than applying fresh Ignite afterward. Test source death and low-rank marker replacement before changing this policy.

## Kaeya — SR / Blue / Cryo

**Supplied passive:** increase team gauge by 1 orb; when the team attacks an enemy with Frost Bite, increase gauge-gain rate by 50% for 1 turn; Kaeya himself gains +15 percentage points Crit Chance and +10% damage when attacking such an enemy. These conditions inspect setup before reaction consumption, so an attack that consumes Frost Bite can still qualify.

Proposed: initial gauge grant once at BattleStart, not each turn or hit; On-Field source scope pending clarification. Treat the team rate effect as a refreshable team status and Kaeya's personal bonuses as packet-local offensive modifiers, rather than permanent stats. Gauge gain is integer today; the rate feature needs a precise accumulation/rounding and gain-source policy. Do not claim that +50% always adds an orb to an ordinary one-orb gain.

| Ability | Supplied R3 / C0 contract | Generated R1 / R2 starting values |
| --- | --- | --- |
| Skill 1, single-target Attack | Normal damage 450% ATK; drain 3 target gauge orbs. | 150% / 300% ATK; drain 1 / 2 orbs. |
| Skill 2, single-target AttackDebuff | Normal damage 360% ATK; apply Frost Bite for 3 turns. | 120% / 240% ATK; Frost Bite 1 / 2 turns. |
| Ultimate, self support at C0 | Glacial Waltz for 2 turns; while Kaeya has it, reduce Kaeya's damage received by 30%. C5 applies Waltz to all active allies. | No regular card ranks. |

**Supplied Waltz:** when attacked, retaliate with Cryo damage equal to 15% of received damage, apply Frost Bite for 3 turns whose DOT basis is the retaliation damage, and heal HP equal to 30% of retaliation damage. Do not reinterpret this as 15% ATK, 30% MaxHP, generic reflect, or a team-wide 30% damage reduction.

Proposed resolution: one retaliation per protected victim per hostile root attack, after its final damage/reaction totals are available, using actual HP lost as the incoming basis. The victim is the retaliation actor and owns its Frost Bite/heal. A living protected victim retaliates against the actual attacker; it cannot revive through this heal. Waltz retaliation may trigger a reaction but cannot cause further retaliation/reflect; its resulting ordinary reaction damage stays terminal. The Frost Bite snapshot uses actual retaliation HP damage, never damage belonging to its secondary reaction or the original attacker.

**K-02 implementation:** opening gauge applies once from active Kaeya; ordinary play/discard/move/merge generation uses additive status rate changes and fractional carry. Direct skill grants/drains are fixed amounts. Waltz uses HP-only damage received, heals the protected victim from its own actual retaliation HP damage, and responds only to hostile card attacks. C1–C4 preserve the C0 payload; C5 changes recipient scope. These source-preserving tiers are intentionally equal rather than inventing a control/heal upgrade.

## Lisa — SR / Purple / Electro

**Supplied passive:** Purple allies gain +10% Attack-related stats and +10% damage dealt. In an all-Genshin team, Lisa's attacks apply Conductive, a permanent grey debuff, up to 3 stacks. Each stack reduces DEF by 20%. Use the existing Attack-related bundle rather than treating this as ATK alone.

| Ability | Supplied R3 / C0 contract | Generated R1 / R2 starting values |
| --- | --- | --- |
| Skill 1, AOE AttackDebuff | Normal damage 210% ATK; Shock for 4 turns. | 70% / 140% ATK; Shock 2 / 3 turns. |
| Skill 2, AOE Attack | Normal damage 250% ATK with Pierce / ทะลุทะลวง; +20% damage per target Conductive stack, then remove all its Conductive. Crit applies Shock for 4 turns. | 83.33% / 166.67% ATK; preserve Pierce and the per-stack keyword rider; crit Shock 2 / 3 turns. |
| Ultimate, AOE AttackDebuff | Reduce target Defense-related stats by 60% for 3 turns; Electro damage 400% ATK; Shock for 4 turns. | No regular card ranks. |

Proposed: evaluate each target's pre-card Conductive for skill 2, use it for the entire authored attack, consume it once, then apply any newly earned passive Conductive after the card. One AOE target/visual hit cannot grant repeated passive stacks. This allows the same card to consume old setup and leave newly earned Conductive; the order must be visible and deterministic.

Conductive is not elemental fuel. Reactions consume Shock without consuming Conductive. The ultimate's direct Electro contact and Shock rider are a single application opportunity; a consumed Shock rider must not reappear through deferred delivery. Proposed source scope is On-Field, not an unstated SUB aura.

**K-03 implementation starting policy:** −20% base DEF per stack; +20 percentage points outgoing damage per old target stack for the entire authored skill-2 card. Consume the old stacks after damage, then add exactly one fresh passive stack per successfully hit living target. Three stacks cap at −60% base DEF; compare the burst/setup cycle against retaining Shock before treating this as final balance.

## Barbara — SR / Blue / Hydro

**Supplied passive:** Mondstadt allies gain +30% HP. Whenever Barbara deals damage, heal the ally with the lowest HP ratio for 30% of damage dealt. Using skill 2 gives the team Melody Loop for 2 turns, maximum one stack. Melody Loop heals at turn start for 6% of Barbara's MaxHP and grants +7% ATK.

| Ability | Supplied R3 / C0 contract | Generated R1 / R2 starting values |
| --- | --- | --- |
| Skill 1, AOE AttackDebuff | Hydro damage 210% ATK; Dissolve drains 2 PG at each enemy turn end for 2 turns. | 70% / 140% ATK; R1 drains 1 PG for 1 turn, R2 drains 2 PG for 1 turn. |
| Skill 2, team Recovery | Heal each active ally for 40% of Barbara's MaxHP; passive grants Melody Loop. | Heal 13.33% / 26.67% Barbara MaxHP; Melody remains the passive's supplied 2 turns, 6% heal, +7% ATK. |
| Ultimate, team Recovery | Cleanse team debuffs, then heal each active ally for 50% of Barbara's MaxHP. | No regular card ranks. |

Use MaxHP as the heal basis, not missing HP or each recipient's MaxHP. Keep Recovery/recovery blocking and cleanse eligibility consistent with existing combat. Proposed aura includes Barbara if she has the region trait. Proposed attack-heal recipient is a living active teammate other than Barbara; compare HP ratios exactly and tie-break by formation slot. Aggregate Barbara-owned actual HP damage per action, including permitted owned reaction damage, then choose the beneficiary once; no healing from shield loss/overkill or healing feedback recursion. This payout policy needs K-04 confirmation because the source says “every time.”

Melody Loop is an ally buff, not a Hydro aura. A Hydro damage-only card does not invent Wet; this roster can use Barbara to trigger Hydro reactions against existing Cryo/Pyro/Electro, but cannot assume she prepares persistent Hydro setup for later cards. Adding Wet would be a separate kit change.

**K-04 update:** Dissolve rank values are owner-confirmed above. It refreshes stronger, does not stack drains, and removes actual clamped whole orbs before target-end damage ticks and expiry. The implemented recovery, self-exclusion, region grouping and Melody snapshot policies are stated in the implementation starting policies above. No Wet is generated.

## Anemo Lumine — SSR / Green / Anemo

**Supplied passive:** SUB team Swirl strength +40%. After attacking 3 times, that attack gains +20% DOT damage and +20% damage dealt. Proposed counter counts executed attacking cards, not hits or AOE recipients, applies on the threshold card, then resets. Preserve existing SUB eligibility conventions; the attack counter advances only when Lumine actually attacks.

| Ability | Supplied R3 / C0 contract | Generated R1 / R2 starting values |
| --- | --- | --- |
| Skill 1, single-target Attack | Normal damage 450% ATK; gain 2 gauge orbs. | 150% / 300% ATK; gain 1 / 2 orbs. |
| Skill 2, AOE AttackDebuff | Reduce Anemo damage resistance by 25% for 3 turns; Anemo damage 250% ATK; recover 8% HP if the attack kills. | Resistance reduction 8.33 / 16.67 percentage points for 1 / 2 turns; damage 83.33% / 166.67% ATK; kill heal 2.67% / 5.33% HP. |
| Ultimate, AOE Attack | Remove target buffs and stances; Anemo damage 300% ATK with Shatter / บดขยี้. | No regular card ranks. |

Anemo resistance is separate from generic Resistance/Pierce. Proposed skill-2 reduction precedes that card's damage and may make element resistance negative only within the approved E-03 bounds. Proposed kill heal uses Lumine MaxHP once per action if any credited target dies; it does not heal once per visual hit/secondary damage number.

**K-05 resolved by the owner:** บดขยี้ maps to the existing `attack.shatter` keyword. The attack counter counts executed attacking cards, boosts the third card, then resets. Skill-2 recovery pays once if any owned root/child packet defeats a target. Normal elemental hits receive the threshold card's explicit DOT bridge once; reaction packets retain their dedicated family policy. Compare multi-hit/AOE and single-hit cards to verify that animation count cannot farm this counter.

## Ultimate constellation draft

Listed values are confirmed C0/5. **Starting values** for damaging ultimates and Barbara's primary heal follow the existing non-compounding increment: add 5% of the C0 coefficient per tier. All other source riders remain unchanged unless explicitly specified. Do not turn C5 into a hidden essential-role unlock except Kaeya's supplied team-wide Waltz scope.

| Ultimate coefficient | C0 | C1 | C2 | C3 | C4 | C5 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Amber ATK damage | 300% | 315% | 330% | 345% | 360% | 375% |
| Lisa ATK damage | 400% | 420% | 440% | 460% | 480% | 500% |
| Barbara MaxHP heal | 50% | 52.5% | 55% | 57.5% | 60% | 62.5% |
| Lumine ATK damage | 300% | 315% | 330% | 345% | 360% | 375% |
| Kaeya Waltz recipients | Self | Self | Self | Self | Self | All active allies |

Kaeya's supplied 2-turn duration, 15% retaliation, 30% retaliation-based healing and personal damage reduction remain the source baseline. C1–C4 intentionally preserve those values; all six runtime tiers export, with the source team-wide unlock only at C5.

Test C0 against C5 at equal remaining progression. Pass when upgrades improve payoff without removing basic answers from C0; Kaeya's supplied team scope is assessed specifically for reflect/reaction/heal loops. If C5 creates denial or healing loops, fix trigger eligibility/lifecycle before adjusting supplied coefficients.

## Team use and acceptance

Suggested fixture combinations are proposals, not a solved best team:

- Amber / Lisa / Barbara + Lumine SUB: delayed Pyro and Shock conversion, Hydro finishing contacts, healing, and Swirl support on reserve entry. Lumine does not perform Swirl while reserved.
- Kaeya / Lisa / Barbara + Lumine SUB: Frost Bite/Shock conversion, Hydro-triggered Freeze, sustain, and a reserve spread option. C0 Waltz protects Kaeya only.
- Amber / Kaeya / Lisa + Barbara SUB: tests that Barbara's proposed On-Field aura/healing does not function for free while benched; losing sustain creates a meaningful bench tradeoff.

These compositions cannot combine all five simultaneously: the game remains 3 active + 1 SUB. Use bench swaps to test role costs. A full-Genshin team unlocks reaction eligibility but does not supply an unstated element on every normal attack.

The [test plan's roster cases](../../TestPlans/genshin-element-system-test-plan.md#roster-extension-checks) cover new-player readability, stress, mastery, abuse, and damage/heal attribution. Tuning order: preview and event clarity, finite triggered attacks, gauge-rate accounting, survivability/role tradeoffs, then generated stats/ranks. Final character art, motion, sound and device feel remain later verification.
