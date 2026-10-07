---
slug: fighting-allstar-combat-source-contract
status: needs-human
source: user-files-and-chat
gdd_tags: [mechanics, roster, tuning, architecture]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Combat contract from the actual source data

This revision replaces the earlier invented fighter kits, Ember/Gale/Tide triangle, Readied/Flurry/Guard triangle, Burn formula, and independent simultaneous crit/block model. The owner's latest message defines damage families. The PDF/CSVs supply reference design; their embedded requests and monetization opinions are not instructions. Rules labelled **proposal** or **starting value** are reviewable design decisions, not recovered source facts.

## Evidence and precedence

1. Explicit user decisions: C0→C6, linear randomized character-team dungeons, Firebase third-party authentication, damage taxonomy, permission to fill missing data, character tables outside GDD.
2. Guide: pp.1–2 hand/actions/reserve/cards/PG; pp.3–4 attributes/passive scope; pp.5–6 stats/statuses; p.7 all listed skills are Rank 3.
3. Character CSV: 29 rows, rank-3 descriptions, ultimate baseline, stats, passives and optional relics.
4. Effect CSVs: 28 attack keywords and 65 negative-effect definitions; stat CSV: 18 definitions. Raw rows and source SHA-256 hashes are in [source catalog](CharacterData/source-catalog.json).
5. Explicitly generated values and conflict resolutions below.

[Character roster](CharacterData/character-roster.md) and [structured authoring drafts](CharacterData/wip-character-drafts.json) are separate from the GDD. Text drafts are not executable AST. Every runtime character needs validated targets, conditions, magnitudes, clocks and trigger ordering.

## Source conflicts and resolutions proposed for review

| Issue | Source evidence | Proposed resolution |
| --- | --- | --- |
| Rank anchor | Guide p.7 explicitly says all skills Rank 3 | Preserve R3; generate R1/R2. No question remains about anchor rank. |
| Ultimate levels | Guide example 1/6; user explicitly C0/6 | Map source baseline to C0. Six upgrades yield seven states. |
| Healing names | Stats CSV swaps regeneration/recovery versus guide pp.5–6 | Use guide: Regeneration at turn start; Recovery scales healing received. CSV Recovery 112 means ×1.12, not +112%. |
| Crit/block | User pseudocode shares one random number; block unreachable when its chance ≤ crit chance | Preserve mutually exclusive outcomes, use a conditional second roll for block only after crit fails. CritDefense subtracts from total crit multiplier. |
| Final subtraction | `reduction - damage` becomes negative for ordinary hits | `max(0, damage - flatReduction)` or multiplicative percent reduction, with explicit unit. Damage never heals. |
| Packet aggregation | Applying generic final reduction to DOT/additional contradicts latest taxonomy | Resolve each packet family separately, then sum actual losses for display. Never mitigate the sum again. |
| True Damage | +30%, defense/shield/reduction bypass; crit/resistance behavior unstated | Normal enchantment; +0.30 outgoing increase; bypass DEF and Resistance and incoming generic reductions, preserve crit resistance/defense and block. These last interactions are proposals. |
| Destructive | HP −1% instant death, “damage restricted” undefined | Explicit bypass of caps, immunity and survival/HP-floor restrictions. Other defense/reduction/shield rules remain unless separately tagged; execution when post-shield HP would be <1. Record death cause and −1% sentinel separately from clamped HP. Revival suppression is not assumed. |
| SUB | Guide says SUB works in reserve, not that it stops on entry | Map SUB to living roster (active or reserve), On-Field to active only; retain separate mode/trait filters. Makes Leona's self-Lifesteal meaningful after entry. |
| Card category | Several Debuff cards also deal damage | Category controls disables/UI; typed effects control behavior. No assumption that all Debuff cards are damage-free. |
| Mai94 / Beni99 / Kensou | Tags disagree with explicit card descriptions | Mai94 disables Recovery; Beni99 skill1 Stuns; Kensou skill2 heals all allies. Preserve original values/text in source catalog. |
| Amplify / Clash | Amplify formula references enemy buff despite self description; Clash formula >4 versus prose ≥4 | Count own eligible blue buffs; Clash threshold ≥4. |
| Ignite | CSV type DOT but effect only increases damage taken | Keep DOT-family tag for keyword counting; no periodic HP damage. +10 percentage points received-damage per stack. |
| Abyss / Fracture | CSV output/type labels conflict with timing | Typed `DamageFamily` and `TriggerWindow` are independent. Abyss is periodic DOT at enemy TurnStart; action-timed Fracture uses AdditionalDOT. |
| Guide PvP focus / later P2W roadmap | Reference guide mentions PvP and power escalation | Current product remains dungeon-first with synergy priority. Do not infer mandatory pay-to-win or PvP launch requirements. |

## Rates, stats and keyword evaluation

Source percentages become basis points once at import: 20%=2000 bp, 152% CritDamage=15200 bp total multiplier, 112% Recovery=11200 bp total multiplier. Formulas below use normalized ratios (20%=0.20). ATK/DEF/HP/CC are points. Do not divide ratios by 100 again.

Guide defaults: Avoidance=0%, Evade=0%, Control=100%, Perception=100%. Alias map: Evasion→Evade; Penetration→Perception (never Pierce); CrowdControl→Control; Regenerate→Regeneration. Proposal: hit probability `clamp(Perception - Evade,0,1)`; status probability `clamp(authoredProcChance * max(0,Control - Avoidance),0,1)`. First roll attack hit, then damage outcomes, then eligible debuffs. Pure debuff cards use status accuracy only. Immunity checks precede the status roll; grey immunity bypass is a separate authored flag.

Stat groups: Basic=ATK/DEF/HP; AttackRelated=ATK/Pierce/CritChance/CritDamage; DefenseRelated=DEF/Resistance/CritResistance/CritDefense; HPRelated=MaxHP/Recovery/Regeneration/Lifesteal. Block/Evade/Perception/Control/Avoidance are special; generic “all stats” excludes special stats per CSV. Flat stats receive multiplicative percent buffs; rate stats receive percentage-point changes unless an effect explicitly specifies multiplication. MaxHP reductions clamp current HP but cannot kill through stat adjustment alone (proposal).

Keyword modifications precede base damage: Charge sets effective DEF=0; Shatter sets Resistance=0; Pierce triples attacker Pierce; Sever triples CritChance before opposing resistance; Spike doubles total CritDamage; Breakthrough zeros target DEF/Resistance/CritResistance/CritDefense. These alter local calculation views, not persistent stats.

Card factors: Weakpoint ×3 if any debuff; Rupture ×2 if eligible buff; Blaze `1+.25*targetIgnites`; Co-Destruction `1+.20*targetDebuffStacks`; Detonate `1+.20*targetPG`; Clash `1+.20*ownPG`; Flood `1+.8*currentHP/MaxHP`; PowerStrike `1+targetResistance`; Cleave `1+.75*max(0,CritDamage-1)` and cannot crit; Quell `1+.5*ownStances`; Amplify `1+.3*ownEligibleBlueBuffs`; SecretTechnique `1+.2*ownFighterCardsInHand`. Counting includes individual stack instances, except a multi-stat status remains one status. Grey buffs do not count for Amplify. The word “additional” in a keyword description does not turn these Normal multipliers into Additional packets.

## Normal damage formula

Proposed corrected implementation of the user's staged formula, in exact rational arithmetic:

```text
base = selectedScalingStat * cardCoefficient
contract = max(0, base * max(0, 1 + effectivePierce - effectiveResistance) - effectiveDEF)
cardDamage = contract * keywordFactor
outFactor = max(0, 1 + dealtIncrease - dealtDecrease + (trueEnchant ? .30 : 0))
inFactor  = max(0, 1 + receivedIncrease - (trueEnchant ? 0 : receivedDecrease))
normal = cardDamage * outFactor * inFactor * attributeFactor
critChance = clamp(effectiveCritChance - effectiveCritResistance, 0, 1)
if critRoll < critChance:
    normal *= max(1, effectiveCritDamage - effectiveCritDefense)
else:
    blockChance = clamp(blockChanceStat - effectivePierce, 0, 1)
    if blockRoll < blockChance:
        normal *= 1 - clamp(blockPower, 0, 1)
normal *= variance
if not trueEnchant:
    normal = max(0, normal * (1-clamp(finalReductionRate,0,1)) - finalReductionFlat)
packet = floor(normal)
apply allowed cap / damage-immunity / survival rules
apply shield unless bypassed; subtract remaining packet from HP
```

The chosen block rule is a **proposal**, not literal source pseudocode. At 30% crit and 20% conditional block it yields 30% crit, 14% block, 56% ordinary. CritDefense is not ignored; critical damage never falls below ordinary damage. `variance=1` for golden tests; **starting value** 0.95–1.05 for normal gameplay only. If randomness obscures tactical predictions, narrow it toward 1.

True enchantment sets effective DEF and Resistance to zero in this draft and bypasses shield and generic reductions. Caps/immunity/survival remain because the user reserves restriction bypass for Destructive. Block/CritDefense are outcome mechanics here, not generic reduction; review this explicit choice before runtime implementation. Attribute factor affects Normal (including True), not DOT/Additional, unless an effect explicitly opts in.

## Damage-family matrix

| Family | Timing / magnitude | Defense / generic reduction | Damage cap | Shield | Specific reduction | Crit/block/lifesteal |
| --- | --- | --- | --- | --- | --- | --- |
| Normal | Card action; scaling stat×coefficient | Applies | Applies | Applies | Normal modifiers | Yes / exclusive / actual HP loss |
| DOT | Authored periodic window; stored magnitude | Bypass | Bypass | Applies (proposal) | DOT only | No / no / no |
| Additional | Action/attack trigger; authored magnitude | Bypass | Bypass | Applies (proposal) | Additional only | No / no / no by default |
| True-enchanted Normal | Normal with +30% outgoing | Bypass as above | Applies | Bypass | No generic reduction | Normal outcomes (proposal) |
| Destructive | Authored passive/skill magnitude | Explicit normal mitigations if configured | Bypass | Applies unless separately bypassed | Explicit authored policy | No by default; lethal execution |
| Additional DOT | Action/attack trigger; authored DOT magnitude | Bypass | Bypass | Applies (proposal) | DOT only | No / no / no |

Damage caps and damage restrictions are different: DOT/Additional bypass a maximum damage cap, but do not automatically bypass immunity or survive-at-1 effects. Destructive explicitly can. Give every effect an explicit flags record; do not infer all bypasses from an English keyword.

DOT/Additional formula: `floor(max(0, authoredMagnitude) * max(0,1+familyDealtIncrease-familyDealtDecrease) * max(0,1+familyReceivedIncrease-familyReceivedDecrease))`. Generic Ignite received increase does not amplify these packets unless specifically authored as family-specific. Do not reapply the causing card multiplier, DEF, variance or attribute. Ordinary effects cannot heal from negative damage.

## Status and event semantics

- **Starting values:** Ignite cap 10 per target; Kyo98 passive reduction cap 30%; King accrues 8 percentage points at his team's TurnEnd to source cap40. Test 10-stack cases; if mitigation or focus burst eliminates responses, lower the proposed cap, not source descriptions silently.
- Bleed/Shock/Poison store respectively 33%/36%/45% of the causing card's actual Normal HP loss to that target, excluding shield loss, overkill, DOT and Additional. This definition of “damage dealt” is proposed. Store one stack per application with source, snapshot magnitude, tick window and remaining turns. Zero-HP-loss attacks can still apply a zero-potency status but do not fabricate a damage tick.
- DOT default ticks at affected fighter's owner TurnEnd, then duration decreases. Status applied during that owner's turn does not tick/decrement immediately. Explosion starting delay=2 owner ends, once; Abyss explicitly uses TurnStart instead. Source death or later ATK changes do not alter stored magnitude.
- Normal-color statuses can be removed according to polarity; Grey statuses are not cleanseable. Cleanse eligibility is derived from `StatusColor` (Normal = cleanseable, Grey = not cleanseable); debuff immunity and expiry remain independent. Prototype ordinary statuses are Normal. Cleanse removes all eligible debuffs when description says “Removes Debuffs”; buff removal never cleanses enemies' debuffs.
- Starting value: unspecified status duration=2 owner turns; Ignite ultimate duration=3. Shields use larger of remaining/new amount, refresh duration, do not add. Taunt redirects legal hostile single-target actions to the taunter; AoE unaffected. On multiple taunts, most recently applied wins (proposal).
- Stun/Paralyze/Seal prevent that fighter's card plays, including ultimate. Team may move its regular cards at normal action cost but gains no PG for moving a disabled fighter's card (proposal). Category disables match that category; an Attack disable does not disable a Debuff-category damage card. Skills/passives specify immunity and refresh; no invented universal control-immunity grace period.
- TurnStart: start triggers/Regeneration → deaths/reserve → action budget → ultimates/draw → planning. Proposed regeneration is `MaxHP*Regeneration*Recovery`, healing capped at missingHP, Infect blocks it. All recovery uses total Recovery multiplier, never `1+Recovery`. Zero HP cannot receive normal healing.
- PreAction validates targets, consumes card, handles pre-effects and PG; resolve direct hit batches simultaneously per target set. Apply direct lifesteal from actual HP removed (scaled by source Recovery), then authored Additional packets, reflect, death cleanup. Do not allow a zero-HP actor to heal before cleanup. Per card, sum rendered hit portions to the single card result; decorative multi-hit animation does not subtract DEF repeatedly.
- Apply after-damage debuffs after the causing damage unless description expressly orders removal/control before damage. Snapshot DOT at that application. Atomic AoE damage completes before death-driven triggers, then counters/follow-ups after root action. Reactions are lineage-tagged, no recursive reflect, no extra normal play PG. Starting safety limit128 effects aborts a broken transaction, never awards a win.
- Destructive execute produces `hp=0`, `deathCause=Destructive`, `displayHpRatio=-.01` for the requested sentinel. Snapshot negative display value must never be treated as spendable/mutable HP. Simultaneous elimination of both full rosters is draw; dungeon draw ends run with no win reward.

## Hand, ultimate and attribute corrections

Guide hand maximum is **7 cards**, not seven plus a separate ultimate strip. Owner decision: the ordered Deck field capacity is `min(7, total formation size + 3)`, giving six for three fighters and seven for a full three-active-plus-SUB roster. At battle initialization deal exactly two R1 skills per active fighter from Position 1 toward Position 3. Random-fill any remaining opening capacity from living active fighters by inserting on the left; later random TurnStart draws also enter from the left. A SUB increases capacity but contributes no cards while reserved. During planning, a quick card tap moves it into the next Action slot against the preferred target, while holding shows its tooltip. Reset restores exact pre-plan card identities/order/ranks. Filling the Action field commits automatically, consumes queued cards, and compacts unplayed cards from index 0. No draws occur during planning. At full hand, retain ultimate readiness without generating an eighth card. Ultimate can occupy a slot but cannot merge or move; one pending ultimate/fighter. Drain below5 removes it, death removes all owner cards. Source PG requirement=5. Starting gains: regular play1, paid move1, merge1, including merges caused by opening and TurnStart draws. Draw once then settle merges; do not recursively refill merge gaps.

Action budget = living active count at TurnStart, maximum3, minimum1 only while battle remains live. Reserve is not a fourth action. Freeze budget for the turn; death fizzles affected queued actions and reserve enters after root action/reactions. A living reserve with no active allies enters before terminal outcome. Tutorial partial teams follow the same count.

Source attribute cycle: **Red→Green→Yellow→Blue→Red**, +20% advantageous Normal damage, −20% disadvantaged; opposite pairs/same color neutral (proposal for unspecified pairs). It is a four-way cycle, not a triangle. Light team received-damage −10% and Darkness team dealt-damage +10% do not stack per attribute; no WIP row uses them. Proposal: living roster aura including reserve; modifiers Normal-only unless explicitly authored otherwise. Do not replace this with a three-color system to satisfy the phrase “meta triangle.”

## Team synergy and prototype implementation order

Meta triangles are hypotheses tested through card decisions, with no automatic strategy-tag multiplier:

1. **Ignite/setup burst → recovery sustain → PG denial → setup burst.** Kyo/Chin pressure HP; Kensou cleanses and recovers; Shingo/King delay high-value ultimates. Sustain beats low-damage denial only if denial sacrifices lethal pressure. These relationships require paired tests; names alone do not establish balance.
2. **Buffed offense → raw damage/defense team → dispel/Rupture team → buffed offense.** Yuri/Leona spend actions on buffs; Terry/Chang/Brian buy time; Mai/Chin remove buffs and Rupture punishes them. Dispel teams should pay damage/action cost against unbuffed teams. Do not assert a universal triangle until tests support it.

P0-B first four: **Kyo94, Chin94, Kensou94 / King94**. Two-team mirrors and curated enemy permutations use these same definitions. Holy Relics off. P0-C expands to eight by adding **Mai94, Shingo97, Benimaru94, Athena94**; unlock full Red/Green/Blue/Yellow coverage and anti-heal/category disable interactions. Remaining 21 are catalog backlog; source completeness is not a claim that 29 runtime kits exist.

Proposed guaranteed onboarding grants Chin94/Kensou94/King94 after Kyo tutorial. Foundation accepts all; Green Accord requires ≥2 Green across roster (Kyo+Chin satisfy); Women Exhibition requires ≥2 Women (King+Mai or Athena). First foundation clear gives an unowned selector; Mai or Athena guarantees access without gacha luck. Each restriction applies equally to generated enemy teams; maintain legal fallback teams with complete slots and no duplicate definition IDs. Variant family exclusion is deferred; definition duplicates forbidden.

## Acceptance vectors and review gates

All vectors use variance1, neutral attribute, no absent modifiers. These are specifications, not executed battle tests.

**Early balance risk:** generated low-rank coefficients plus the source flat DEF can produce near-zero damage, while source Regeneration reaches50% and Recovery reaches160%. Preserve those source values for traceability, but test full-health regeneration/cleanse loops before claiming the first roster is playable. Compare damage per action to healing per owner turn. If starter battles stall, first propose a documented Regeneration scaling rule or lower flat-defense contribution and rerun golden vectors; do not quietly overwrite supplied stats. CC also needs calibration: source sums are initiative inputs, not a proven measure of team strength.

| Fixture | Expected |
| --- | --- |
| ATK150, coefficient1.5, Pierce0, Resistance0, DEF30 | Normal195 |
| Same, CritDamage1.5, CritDefense.15, successful crit | floor(195×1.35)=263; no block |
| Same, failed crit, successful block, BlockPower.25 | floor(195×.75)=146 |
| ATK100, coefficient3, Pierce.2, Resistance.1, DEF50 | 280; old defense-coefficient formula would give255 |
| Same, Shatter | 310 |
| Same, Charge | 330 |
| Additional100, generic reduction90%, cap10, Additional reduction20% | Packet80, not8 or10; then shield/immunity/survival according to flags |
| DOT100, DOT reduction25%, generic reduction90% | Packet75 |
| True: ATK100×3, Pierce.2, DEF50, Resistance.1, shield100, no crit/block | floor(360×1.30)=468 HP-directed damage; shield unchanged |
| Normal100 HP loss, Poison | Stored tick45; later ATK change/source death does not change it |
| Ignite only | Received-damage increase10%; periodic HP tick0 |
| 100 shield +80 HP vs263 Normal, Lifesteal50%, Recovery1, casterHP5; reflect15%actualHP loss | Shield100, HP loss80, heal40 then reflect12; casterHP33 |
| Destructive against survive-at1, post-shield lethal | HP0, explicit execution event, display sentinel−1%; ordinary damage obeys survival |

Validate all 174 rank rows, 203 constellation rows, source rank3 equality, unique stable IDs, five generated stat blocks, and source file hashes. Runtime tests later must cover status clocks, typed immunity/cap differences, exclusive crit/block probabilities, reset RNG isolation, 7-card ultimate pressure, mode-filtered reserve passives, and restricted dungeon fallback solvability. First player test: players predict which cleanse, drain or setup sequence wins; if they cannot, improve status previews and action explanations before adjusting numerical power.
