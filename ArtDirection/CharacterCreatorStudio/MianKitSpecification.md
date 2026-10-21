# Fire Dancing Mian — rule design reference

This is a structured interpretation of the supplied Thai kit, not a balance revision or an implemented character. All numeric combat values below come from that kit. Where timing, targeting, duration, or scope is unspecified, it remains an explicit authoring decision.

Mian is a case study only. M3 validates reusable capabilities through synthetic fixtures; it does not install this kit, create a playable character, or resolve the kit's ambiguities into production defaults.

## 1. The logic of the kit

Mian is a team-protection and resource-conversion character. Female KOF allies feed Face Mask; Face Mask protects those allies and marks attackers. Mian consumes layered marks for sustain, buffs, resources, and Skill 1 EX access. Mask Switch rewards waiting/turn progression, increases offense, changes protection around its threshold, and funds fatal saves. The ultimate builds and redistributes marks, then converts all enemy marks into enhanced detonations.

The central loop is therefore:

```text
Female KOF team activity → Face Mask
Enemy pressure → spend Face Mask → ally recovery/protection + enemy Impressions
Mian single-target skills → consume Impressions → tier rewards + EX access
Turns / layer rewards → Mask Switch → threshold offense + fatal-save resource
Ultimate → add/redistribute Impressions → enhanced all-enemy detonation
```

This kit combines event rules, continuous auras, state transitions, history, and interception. A flat list of passive stat fields cannot describe its ordering reliably.

## 2. Required named data

| Item | Proposed kind | Source-specified properties | Unresolved properties |
| --- | --- | --- | --- |
| Qualifying ally | Target query | Ally; KOF; woman | Self/reserves/living/initial roster policy |
| Face Mask | Resource with status-style display | Gain 1; cap 6; spend 1 for protection | Owner/shared scope; aura scaling; origin and proc frequency |
| Mask Switch | Resource with permanent battle display | Opening grant; +1 own turn; conditional +2; threshold 5; fatal spend 3 | Opening quantity; cap; threshold rearm; persistence after defeat |
| Facial Impressions | Layered gray enemy status | Permanent; max 3; attack capability −20%; explosion on removal | Per-layer stat scaling; removal protections/reasons; source identity |
| Internal Power / เสริมกำลังภายใน | Buff module | Attack +20%; damage and ultimate damage +30% | Duration; stacking; additive or category contribution |
| Skill 1 EX | Conditional variant of slot 1 | Unlocked by three-layer absorption | One-use/permanent/timed; relock; already-held card behavior |
| Card-use history | Turn ledger | Needed for extra Mask Switch gain | Temporal window; whether move, ultimate, disabled/fizzled card, or absent owner counts |
| Level parameters | Independent parameter axis | Several values change at LV.MAX | Which progression system/axis defines LV.MAX |

Store the English/Thai naming variants as aliases until the designer confirms that “Face Mask Impressions” in Skill 2 means Facial Impressions.

## 3. Passive modules

| ID | Type / trigger | Conditions | Actions or continuous contribution |
| --- | --- | --- | --- |
| P01 Roster offense | Derived modifier | Qualifying team count | Attack capability +12% per counted female KOF fighter; recipient and stat bundle unresolved |
| P02 Opening mask | Battle started | Eligible owner | Grant permanent Mask Switch; initial amount unresolved |
| P03 Face generation | Qualifying ally deals damage; own turn starts | Eligible provider; selected damage origin/frequency | Add Face Mask 1, capped at 6 |
| P04 Team protection | Enemy attack event at chosen phase | Victim qualifies; Face Mask ≥1 | Atomic spend 1; remove one recovery-prevention debuff; heal victim 20% HP; stackable single-target damage reduction −15% for 2 turns; add attacker Impression layer |
| P05 Repeat impression | Escalation policy for marked enemy's later attacks | Authored repeat-attack eligibility; mark exists | Increase by 1, capped at 3; coalesce with P04 mark application so the same attack does not accidentally add two layers; dependency on spending unresolved |
| P06 Face aura | Continuous | Face Mask active | Team DOT received −30%; basic stats +5%; per-stack versus flat unresolved |
| P07 Impression penalty | Continuous | Status present on enemy | Attack capability −20%; flat versus per layer unresolved |
| P08 Removal burst | Impression removed | Eligible removal reason | DOT/additional burst based on Mian attack: 300%; 500% at maximum level |
| P09 Absorption | Mian nonultimate single-target skill at chosen phase | Target has Impressions eligible under the authored ownership policy | Capture old layers, apply chosen tier rewards, remove captured status with reason Absorb |
| P10 Mask offense | Continuous | Mask Switch count | Damage dealt +10% per stack |
| P11 Mask turn gain | Own turn starts | Owner eligible | +1 Mask Switch; +2 if no card used in the authored history window; previous own turn is a proposed interpretation requiring confirmation |
| P12 High-mask entry | Transition into Mask Switch ≥5 | Authored rearm/entry policy | Cleanse debuffs; increase applicable card rank by 1 |
| P13 High-mask aura | Continuous | Mask Switch ≥5 | Ultimate damage +200%; normal skill damage +100% at maximum level |
| P14 Full gauge guard | Continuous | Mask Switch ≥5 AND gauge 5 | Gauge-loss immunity; damage-cap bypass condition scope unresolved |
| P15 Low-mask guard | Continuous | Mask Switch <5 | Incoming damage −20% |
| P16 Incoming cap | Damage policy/interception | Eligible incoming damage | Limit to 49% own Max HP; when cap activates, additional-damage immunity for 1 turn |
| P17 Debuff scaling | Outgoing damage modifier | Target debuff count | Damage dealt +7% per counted debuff |
| P18 Critical growth | Eligible attack damage outcome | Critical | Permanent attack +5%, stackable |
| P19 Noncritical recovery | Eligible attack damage outcome | Noncritical | Recover 40% of the selected damage measure |
| P20 Fatal save | BeforeDefeat interceptor | At least 3 Mask Switch; eligible lethal event | Spend 3; prevent defeat; cleanse all debuffs; restore/recover 50% HP; crit chance, crit damage, pierce +50%, stackable |

“Permanent” is interpreted as a battle-lifetime property for this proposal, pending confirmation. No effect should silently persist into account progression.

## 4. Layer absorption program

Capture the status instance, source, and layers before removal. Damage scaling and rewards read this captured value if the chosen timing consumes the mark before contact. Removing first and querying live layers afterward would incorrectly return zero.

| Layer reward | Source-specified effects |
| --- | --- |
| 1 | Heal 18% HP; final damage received −13% for 2 turns |
| 2 | Remove enemy stance; all stats +15%; additional-damage immunity for 3 turns |
| 3 | +1 Mask Switch and +1 Face Mask; absorb 1 gauge point from the authored marked-enemy set; adjacent splash for 30% of the referenced destructive/killing damage; unlock Skill 1 EX |

The phrase “ดาเมจพิฆาตนั้น” needs an explicit formula/reference choice. Do not silently equate it to the engine's Destructive damage family or assume a kill requirement. Rewards may be cumulative (1+2+3) or exact-tier; the source does not settle this.

For each removal, P08 must know the reason and previous layers. If absorption also detonates the mark, the ordinary burst fires exactly once. If absorption suppresses it, that policy must be visible. The ultimate's enhanced explosion replaces the ordinary removal formula for that operation; it must not cause both bursts unless explicitly authored.

## 5. Skill programs

### Skill 1

1. Bind the selected primary target and capture relevant Impression layers at the chosen snapshot phase.
2. Apply evasion +70% and ultimate damage received −50%, both for 2 turns, at an explicitly authored phase.
3. Resolve 500% attack with damage increase +50% per captured Impression layer.
4. Roll 40% stun for 1 turn at the selected contact/outcome phase.
5. Select one random other living enemy and apply splash equal to 50% of the named primary damage result.
6. Run the absorption module according to its chosen timing and eligibility.

The ordering above is a program outline. The source does not fully define before/after-hit timing; the final asset must choose it.

### Skill 1 EX

1. Set the attack's reflection policy; the source states the attack ignores reflection rate.
2. Give Internal Power to self and one random eligible ally, using a distinct selection policy.
3. Resolve 500% attack, increased by +50% per Mask Switch at the selected snapshot.
4. Absorb 5 gauge from the enemy using the authored transfer semantics; grant self gauge-loss immunity for 2 turns.
5. Select and capture one random other enemy; splash 50% of the named primary damage result.
6. Apply additional damage equal to 40% Max HP to the primary and captured secondary targets. Whose Max HP is unresolved.
7. Apply variant usage/relock policy and eligible shared passive rules.

The secondary target set must be reused by both splash and Max-HP damage. Two independent random draws would change the supplied “both targets” relationship.

### Skill 2

Apply four burn stacks for 3 turns; resolve 130% attack with doubled crit rate and +25% damage per target Facial Impressions layer. Target shape is unspecified. If it is an area ability, each target uses its own layer snapshot. “Double crit rate” must multiply the relevant chance at a specified stage, not add 100 percentage points or guarantee a crit.

### Ultimate

1. Validate costs/targets and choose whether high-mask/full-gauge eligibility is snapshotted before gauge payment or evaluated afterward.
2. Cleanse self; apply damage dealt, crit chance, and crit damage +80%. Durations are unspecified.
3. At maximum level, give female KOF allies reflection immunity and accuracy for 2 turns; the accuracy amount is unspecified.
4. Apply 1 Impression layer, or 3 at maximum level, with authored overflow distribution to enemies with the fewest layers.
5. Add 1 Mask Switch, or 3 at maximum level.
6. Resolve 500% attack with +50% damage per Mask Switch at the declared snapshot.
7. If the authored ultimate success condition is satisfied, capture every eligible enemy Impression instance and remove them with reason UltimateDetonate.
8. Each removal uses the enhanced formula and true policy below, retaining the correct source and old layer count.

Formula expressed from the supplied wording, pending confirmation of coefficient stacking:

```text
Normal level: Mian ATK × (300% + 200% × removed layers)
Maximum level: Mian ATK × (500% + 500% × removed layers)
Damage policy: supplied true-damage behavior, including +20% outgoing damage
Bypass: shield, defense, enemy damage reduction, final damage reduction
Origin/tags: triggered Impression detonation; retain authored DOT/additional semantics
```

Those coefficients are a base expression, not a guarantee of the final number after the chosen policy's other stages. The current engine's True family adds 30%, so it cannot be selected unchanged to represent this wording. DOT/Additional immunity and cap interactions must also be resolved explicitly.

## 6. Decisions required before approving Mian content

These are design questions for the future authoring asset, not a request to stop the architecture work.

| Topic | Required choice | Why it changes execution |
| --- | --- | --- |
| Roster synergy | Beneficiaries; active/living/reserve/initial roster; self inclusion; attack-capability bundle | Changes aura count, targets, and source lifetime |
| Resource identity | Per Mian or shared team; opening quantity; Mask Switch cap | Multiple providers and fatal-save availability |
| Face generation | Hit versus action/target; HP/shield/zero damage; DOT/splash/reflect origins | Prevents uncontrolled gain on multihit/area chains |
| Team protection | Attack intent/contact/damage/completion; before or after damage; spend frequency | Determines whether the triggering hit is reduced and whether lethal allies can be healed |
| Repeat layers | Independent of Face Mask payment or part of protection; eligible victim; coalescing with P04 | Marked attackers might escalate with zero masks or when attacking a nonqualifying ally; avoid accidental +2 on one attack |
| Stack modifiers | Face aura and Impression penalty flat or per stack/layer | Materially different stat scaling |
| Absorption | Capture phase; cumulative or exact rewards; removal burst enabled; stance removal timing | Controls same-hit scaling, crit benefit, and double explosions |
| Gray status | Polarity/display versus removal protection; own-source versus shared identity | Cleanse behavior and multiple-Mian ownership |
| Threshold entry | Once per battle, each crossing, or each gain above threshold; rearm behavior | Avoids repeated cleanse/rank-up during stat recomputation |
| Full gauge | Which effects require full gauge; snapshot before or after ultimate cost | Cap bypass could disappear on ultimate payment |
| Incoming cap | Per hit/action; HP/pre-shield basis; family coverage; cap bypass precedence | Especially important for DOT, Additional, and true packets |
| Cap reached | Equal to cap versus actually truncated; zero damage; immunity timing | Prevents unexpected repeated cap-trigger protection |
| Counts and damage | Debuff instances/recipes/layers; calculated versus actual HP/shield loss | Affects +7%, splash, healing, and overkill |
| Layer-3 gauge | Current marked target versus every marked enemy; recipient; actual transfer | Determines which resources move and how many points Mian receives |
| No-card history | Previous own turn, current turn, or another window | Changes when the additional 2 Mask Switch are earned |
| Fatal save | Restore-to versus heal-by 50%; recovery block; repeat limits; execution bypass; buff lifetime | Defines a valid pre-defeat state transition |
| EX variant | Unlock duration/uses, relock, held cards, selection timing | Must stay consistent with card UI, AI, and replay |
| EX damage | Whose Max HP; gauge transfer actual/fixed; reflect immunity versus bypass | Defines actual outputs and nested protection |
| Ultimate outcome | Success on hit/resolve/completion; miss/immune/interrupted/target death | Controls whether global detonation occurs |
| Layer overflow | Fill remainder versus full reallocation; repeated selection; ties; all enemies capped | Determines where 3 maximum-level layers go |
| Unknown durations | Internal Power, all-stats buff, +80% ultimate buffs, fatal-save growth | Missing duration must not silently become permanent |
| Upgrade axis | Which LV.MAX and which unspecified lower levels/ranks | Prevents accidental mapping to constellation tiers |

## 7. State-transition contracts

| State/mechanic | Entry | Exit / interruption | Chaining and cost | Edge behavior |
| --- | --- | --- | --- | --- |
| Team protection | Qualifying event and successful Face spend | Ordered protection transaction completes | Costs 1; emits cleanse/heal/buff/mark facts | No free effects at zero; define dead victim and simultaneous AOE handling |
| High Mask | Threshold predicate becomes true | Count falls below 5 or provider becomes ineligible | Continuous modifiers plus separate entry actions | Rearm and source defeat/reserve policies required |
| EX available | Three-layer reward grants access | Authored use/expiry/relock rule | Same skill slot, explicit variant resolution phase | Define queued/held card transformation and cancel behavior |
| Fatal interception | Proposed lethal result and successful spend 3 | Survives into valid state, or interception fails | Cleanse/recovery/modifiers before defeat commit | No temporary defeat/reserve substitution on success; define execute bypass |
| Mark detonation | Eligible exact removal operation | Removed instance cannot detonate again | Capture old layers and context, emit a separate damage operation | Dead targets/source, multiple owners, and ordinary/enhanced burst precedence |

## 8. Saved acceptance scenarios

| Scenario | Required evidence |
| --- | --- |
| Face Mask 0 versus 1 | Payment gates the transaction; cleanse precedes recovery; another remaining recovery block is respected |
| Female KOF and excluded ally | Eligibility is correctly composed, including self/reserve policy |
| Multihit AOE against two eligible allies | Exact authored spending/gain count per action, target, or hit; no accidental duplication |
| Marked enemy's second attack | Correct single mark escalation, including payment success/failure and nonqualifying victims; no unintended P04+P05 double increment |
| Face cap and stack aura | Add at 6 clamps correctly; aura uses chosen flat/per-stack semantics |
| Absorb 1, 2, and 3 layers | Damage snapshot, reward tiers, stance removal, gauge transfer, and EX unlock match authored policy |
| Removal reason matrix | Cleanse, Absorb, UltimateDetonate, Expire, Replace, and DeathCleanup fire the correct burst once or suppress it |
| Ultimate overflow | A partly full target and lowest-layer ties allocate deterministically; all-capped case terminates |
| Ultimate mass detonation | Every enemy uses its captured layers, source, level coefficients, and temporary true policy; no duplicate base burst |
| Mask 4→5→2→5 | Continuous high/low modifiers and entry/rearm behavior match the chosen policy |
| Full gauge ultimate | Cost timing does not silently change intended cap bypass or gauge protection |
| Lethal hit at 2 versus 3 Masks | Failed payment has no partial rescue; successful rescue emits no committed defeat or reserve entry |
| Below/equal/above 49% cap | Chosen boundary semantics hold for multi-hit, shields, DOT, Additional, true, and cap-bypassing attacks |
| Mixed critical/noncritical multihit | Growth and healing follow the selected frequency and damage field, including zero and overkill |
| Only one enemy / no eligible ally | Empty random selections use explicit behavior; no accidental self or primary retarget |
| Two Mians / source defeated | Resource/status ownership, aura lifetime, and detonation source are consistent |
| Reaction chain stress | Splash, reflect, removal bursts, growth, and Face generation terminate by origin policies and root-wide limits |
| Persistence and deterministic replay | Save/reimport/reopen keeps nested rules; same input/RNG/hash produces matching facts and outcomes |
| Visual independence | Skipped animation, absent clip, different speed, and headless mode produce identical combat results |

Readability acceptance: an author can explain a skipped rule from its trigger, values, target query, payment, immunity, and proc receipt. A player-facing result should also have readable status changes and appropriate visual/audio confirmation without an unreadable flood of simultaneous effects.

No balance tuning is proposed here. Resolve semantics and event order first; then evaluate whether the resulting power loop creates the intended team-building and timing decisions.
