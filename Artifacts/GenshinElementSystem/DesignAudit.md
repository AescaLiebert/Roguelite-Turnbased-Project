# Genshin-inspired elemental combat: design audit

Date: 2026-10-15. Status: design proposal; no gameplay implementation or playtest performed.

**Follow-up, 2026-10-15:** the owner accepted the Swirl/Crystallize source-preservation exceptions and terminal Overload recommendation, then requested canonical GDD documentation and an implementation plan. Current design truth is [GDD `@tag:elemental-reactions`](../../Doc_7DSGCCopyCat/Docs(Template)/1_Inputs_Templates/GDD_Fighting_Allstar.md#genshin-inspired-elemental-reactions); see its linked implementation/test plans. Pending language below records the earlier audit state and does not reopen those confirmed decisions.

## Verdict and confirmed direction

The system is technically feasible in this project. It is not yet a complete implementation specification: reaction direction, contact timing, consumption, propagation, and damage policies need explicit contracts.

The intended experience is to prepare enemies with elemental debuffs, order character cards to convert that setup into reactions, and use formation position for spreading effects. This fits the existing team/card combat better than reproducing real-time elemental application timers.

Confirmed by the user during this audit:

- Preserve Genshin's feel while simplifying for turn-based combat.
- Require the entire team, including SUB, to have the Genshin Impact trait.
- Reactions remove ALL matching debuff stacks, rather than a single stack or a separate aura.

Consequently, this proposal uses the existing debuffs as elemental setup. A separate aura that preserves the original debuff is not the selected design. All recommendations below are provisional unless listed as confirmed above. Values in the original proposal remain user-proposed and unvalidated; they are not balance findings.

## Player goal and design identity

The player should be able to answer: which enemy should I prepare, which card should trigger the reaction, and is triggering now worth losing the remaining debuff value?

ASSUMPTION: Preserving the existing card-order and formation-based battle loop is part of the desired adaptation.
IMPACT: Recommendations translate application timing into authored contacts and action limits rather than real-time rotations.
IF WRONG: The proposed simplifications would not deliver a real-time switching/rotation experience.
VALIDATE: Review the worked Swirl example and the setup-versus-conversion playtest before implementation.

Consuming every matching stack creates an intentional conversion economy. A reaction can sacrifice future Shock ticks, Ignite vulnerability, or Frost Bite suppression for immediate damage or control. More stacks should not automatically mean a stronger reaction unless a skill explicitly says so. Otherwise stacking DOTs, exploding them, and rebuilding them could multiply both existing and new rewards.

The payoff must sometimes justify consumption without making reaction use compulsory in every situation. A damage race may favor immediate conversion; an enemy about to take its turn may favor retaining its DOT. Show the consumed stacks and remaining duration in the preview so this is a decision the player can make.

## What the current game supports

Evidence is from the current working files; existing local changes were left untouched.

| Existing system | Assessment |
| --- | --- |
| `Shared/FightingAllstar.Core/Combat/StatusSystem.cs` | Timed instances, several stacking policies, source ownership, damage snapshots, status removal, permanent duration, and barrier capacity already exist. |
| `Shared/FightingAllstar.Core/Combat/BattleEngine.cs` | Has per-hit damage resolution, action timing, ordered events, status application checks, formation slots, reserve deployment, and deferred defeat handling. These are useful foundations, not an existing elemental resolver. |
| `Shared/FightingAllstar.Core/Combat/DamageResolver.cs` | Normal and DOT calculations deliberately differ. An element must not silently inherit every behavior of the DOT family. |
| `Shared/FightingAllstar.Core/Content/CombatContent.cs` | Characters have traits, series, and attributes. Genshin element must be separate from the current attribute/color advantage system. There is no seven-element damage model here. |
| `Shared/FightingAllstar.Core/Content/EffectRecipes.cs` | Supports status clocks, snapshot scaling, and accumulating barrier policies. A barrier based on actual Geo damage still needs an explicit path for that value. |
| `Shared/FightingAllstar.Core/Combat/PlanDraft.cs` | Previews hand, card movement, targeting, and gauge changes. It does not simulate enemy damage and reaction state through a queued combo. A reaction forecast requires additional work. |
| `Shared/FightingAllstar.Core/Combat/AI/UtilityScorer.cs` | Damage scoring is currently an estimate using attack and rank. It will not automatically understand setup, debuff sacrifice, or reaction order. |

The shared combat core should own elemental rules so local play and server resolution agree. Unity presentation should show the resulting events rather than decide reactions.

Content discrepancies to resolve during later implementation:

- The default Shock recipe in `StandardEffectDatabase.cs` uses 36% of triggering health damage, while the supplied text calls the old value 45%. This is a baseline/source discrepancy, not authorization to change it.
- Ignite has a DOT-related tag but is a stat vulnerability recipe, not a periodic tick. A generic DOT tag is therefore unsuitable as the sole elemental eligibility test.
- Internal Flame, Lightning, Frost Bite, Wet, and Nature require explicit canonical recipes/mappings. This audit did not establish full runtime support for those named effects.
- Element-specific resistance, Lumine's wind resistance reduction, and Frost Bite's gauge-gain penalty need support beyond simply labeling a damage number with an element.

## Reference corrections versus deliberate adaptations

The reaction table should always be written as **existing target element + incoming trigger element**. The incoming event owns the reaction. The original table does not label its axes, making its Melt entries ambiguous, and its Hydro-triggered Vaporize value differs from the reference.

For the classic Genshin reaction model:

| Existing element | Incoming element | Reaction | Reference multiplier |
| --- | --- | --- | --- |
| Cryo | Pyro | Melt | 2x |
| Pyro | Cryo | Reverse Melt | 1.5x |
| Pyro | Hydro | Vaporize | 2x |
| Hydro | Pyro | Reverse Vaporize | 1.5x |

These are reference values, not validated balance values for this game's defense and card formulas. Source: [KQM Amplifying Reactions](https://library.keqingmains.com/combat-mechanics/elemental-effects/amplifying-reactions).

Swirl and Crystallize use Pyro, Hydro, Electro, or Cryo as their elemental partners. Remove the original table's Anemo/Geo/Dendro self/cross reactions. Genshin consumes elemental gauge, so a surviving aura after Swirl/Crystallize is not an unconditional preservation rule. This game's proposed preservation can be a deliberate exception. Source: [KQM Elemental Gauge Theory](https://library.keqingmains.com/combat-mechanics/elemental-effects/elemental-gauge-theory).

Genshin Overload is Pyro, its explosion does not apply elemental gauge, and Superconduct's debuff concerns Physical resistance. An Overload-to-Melt chain, variable-element Overload, and universal damage vulnerability are custom adaptations. They are implementable, but should be chosen deliberately. Source: [KQM Transformative Reactions](https://library.keqingmains.com/combat-mechanics/elemental-effects/transformative-reactions).

This is a scoped adaptation of the supplied classic reaction set, not a claim to reproduce the complete current Genshin system. Dendro combinations beyond Burning remain outside the initial scope.

## Recommended system rules

### Team activation

Evaluate the confirmed full-team trait condition from the starting roster, including SUB. Store the result per side for the encounter. Do not accidentally unlock reactions when a non-Genshin fighter dies, or disable a qualifying team when its reserve deploys. Display the rule's enabled/disabled state during team selection.

Use a stable trait identifier rather than comparing translated display names. Repeated Gnosis descriptions on multiple characters should enable the rule once, not duplicate its resolution. Still to decide: whether a Gnosis passive must be present/unlocked in addition to the full-team trait condition.

Proposed ownership policy: only eligible sources can trigger reactions. Existing mapped debuffs on the enemy are usable setup regardless of their original applier; the current trigger owns the resulting reaction. This matters if reflected effects, borrowed statuses, or future mixed-source encounters exist.

### Element identity and debuff mapping

Keep three separate concepts in content:

- Damage family: the calculation policy, such as Normal or DOT.
- Element: Pyro, Hydro, Electro, Cryo, Dendro, Anemo, Geo, or none.
- Reaction application: whether this specific contact may trigger/apply an element.

Use an explicit registry of eligible status recipes: Ignite/Internal Flame to Pyro, Shock/Lightning to Electro, Frost Bite to Cryo, Wet to Hydro, and Nature to Dendro. Confirm the alternate names against their actual authored effects. Bleed and Poison must not become elemental solely because they are DOTs.

An elemental damage hit may react with existing setup without leaving a new debuff, preserving the supplied rule. This also means a Hydro damage-only skill cannot prepare Wet for a later Electro card; the skill must explicitly apply Wet if that is its intended role.

Pure non-damaging status application may prepare an element but does not trigger a reaction under the user's damage-contact requirement. Existing combinations do not react merely because both statuses are present. End-turn DOT ticks do not start reactions in the recommended initial design.

### Contact timing and consumption

Resolve reactions as part of an eligible damage contact, inside the action. Determining an amplifying reaction before calculating that hit is necessary to modify its damage; this is internal calculation, not a separate before-action trigger.

Recommended sequence:

1. Resolve the real target, including taunt, and determine whether the hit connects.
2. Determine the incoming element and whether any attached elemental debuff is accepted. Do not consume statuses for an evaded hit or rejected status application.
3. Inspect eligible statuses currently on that target and select the reaction.
4. Calculate the hit, including amplification when applicable. Record current-contact modifiers consistently; consumed Ignite should stop benefiting later contacts.
5. Commit consumption and reaction outcomes at this contact. For consuming reactions, remove every eligible status instance mapped to the participating elements on that target. Leave unrelated debuffs intact.
6. Resolve explicitly allowed Swirl children before continuing to the next root hit/contact. Apply ordinary non-consumed status riders at their authored timing.

If a damaging card delivers Shock and Shock is used as its reaction trigger, do not consume it and then add the same triggering Shock back in the ordinary after-damage step. Conversely, separate later authored applications can create fresh setup if their text says so. This needs an explicit delivered-and-consumed result in the eventual implementation.

If a target already has several incompatible elements, choose a documented reaction priority; never derive it from the incidental order of status entries. Reaction priority is still open. The preview must show the selected result.

Proposed contact policy: shield absorption still counts as contact; complete evasion does not. Element damage can react even when its optional debuff rider is resisted, whereas a normal hit relying on an incoming Shock rider cannot use a rejected Shock. Crowd-control immunity can prevent Freeze without preventing the underlying elemental hit. These policies need design sign-off before code.

### Stack consumption and exceptions

For Melt, Vaporize, Overload, Superconduct, Electro-Charged, Freeze, and Burning, consume all eligible stacks of the participating elements on the reacting target, consistent with the confirmed answer. A damage-only trigger has no incoming status to remove.

The supplied Swirl and Crystallize rules explicitly preserve the source debuff. Treat that as an exception for the draft, pending confirmation that the user's all-stack answer also intends these exceptions. At a Swirl destination, a consuming reaction still removes all matching destination stacks.

Do not copy an original DOT's snapshotted damage or full stack pile when spreading its element. Swirl delivers a fresh elemental contact owned by the Anemo character. If it finds no reacting element, any newly applied mapped status needs its own authored potency, duration, and snapshot rule.

### Damage and reaction identity

Recommended initial policies:

| Result | Calculation and identity |
| --- | --- |
| Ordinary elemental skill | Existing Normal damage formula, with element-specific modifiers/resistance. Retains normal crit, block, cap, and shield rules. |
| Melt/Vaporize | Amplify the eligible triggering hit; do not create a duplicate damage hit. Apply at a documented formula stage, before the final cap and shield allocation. |
| Swirl/Overload/Superconduct instant damage | Dedicated reaction policy; elemental identity without automatically applying another element. Initially non-critical, without repeated normal attack keywords. |
| Charged/Burning periodic damage | Explicit reaction DOT recipe, attributed to the trigger, with chosen snapshot and clock rules. Its ticks cannot recursively react. |
| Crystallize | Barrier creation, with an explicit basis for the Geo damage used and no damage double count. |

Do not run the Normal result through the DOT resolver a second time. If existing DOT bonuses must also increase ordinary elemental damage, author that as a precise modifier bridge, counted once. Decide separately whether DOT damage-received bonuses apply; the proposed wording does not settle this.

Lumine's proposed normal damage bonus, DOT bonus, and Swirl bonus could otherwise all amplify the same result unintentionally. Preserve her intended synergy through explicit modifier eligibility, not inheritance of two entire formulas. A distinct reaction bonus also gives future characters a reaction-support identity without making ATK/crit buffs improve every system identically.

Using trigger ATK for separate reaction damage is a reasonable adaptation if the goal is to reuse current stats. It is not the reference Genshin scaling model. Its coefficients need comparative playtests, not copied values.

### Propagation, formation, and action limits

Recommended simplification: **Swirl can deliver an element and cause a secondary reaction; ordinary reaction explosions and periodic ticks cannot apply elements.** This provides deliberate combo spreading without general recursive explosions.

In the supplied scenario, Anemo on Pyro at Pos1 spreads Pyro to Shock at Pos2 and causes Overload. Its explosion damages nearby enemies, including the Cryo target at Pos3, but does not Melt that target. The Cryo setup survives for another eligible application.

Retaining Overload-to-Melt is also technically possible, but requires a custom propagation table, ownership rules, repeat prevention, and a predictable termination condition for every child event. It increases both UI and balance scope.

Use formation slots for adjacency, never current animation positions. Only living active enemies are spread targets; reserves are excluded. Proposed taunt interpretation: taunt redirects the initial selected target; splash remains centered on the actual hit target and affects its eligible neighbors. The original phrase 'does not apply if got Taunt' needs clarification if it instead means taunt cancels all spreading.

For AOE Anemo, capture each root target's eligible source element at that hit's start, then resolve contacts/spreads in visible slot order. A debuff consumed by an earlier chain cannot later be reused from the snapshot. This makes the supplied AOE example's unconditional later Electro/Cryo spreading invalid whenever those source statuses have already been removed. A different snapshot rule is possible but would allow a consumed element to act again and must be taught explicitly.

Keep the supplied Swirl-per-enemy-per-action limit as the initial application limit, counting all visual hits of the same card under the same root action. For other reactions, a provisional starting rule is one occurrence of each reaction type per target per root action. Test multi-hit and single-hit cards with equivalent authored power: fail if extra visual hits alone grant extra conversion opportunities; adjust authored application points or limits if they do.

The proposed 'Overload once per action' is ambiguous for AOE. Decide between a single explosion centered on a chosen reacting enemy, or local explosions with a shared per-recipient damage limit. The latter is recommended for consistent area coverage. Aura consumption and explosion damage limits must be separate: suppressing duplicate damage must not silently leave arbitrary targets' elements unconsumed.

### Reaction-specific risks

- **Freeze:** Losing an entire target turn can be much stronger than a short real-time freeze. Define its exact expiry boundary, damage-break behavior, cleansing, existing queued-card behavior, boss handling, and a recovery opportunity before immediate re-freezing. Use the existing card-disable semantics deliberately; do not introduce a hidden second rule for card spending.
- **Superconduct:** The user's universal damage vulnerability makes this a general damage-support reaction. Restricting its benefit to non-elemental/physical damage would create a distinct team role. Either is valid, but the current proposed amount and any stacking must be tested against Ignite and defense reduction.
- **Electro-Charged:** Define whether Charged deals immediate damage, ticks at target turn end, or both, its duration, and refresh/stack policy. '100% trigger ATK' alone does not specify total payoff.
- **Burning:** Define snapshot ownership, stack limits, tick order, and whether Burning counts as future Pyro setup. Recommended initial scope: a reaction DOT that does not supply another element. Endless stacking is not a complete rule.
- **Crystallize:** Permanent accumulating shields reward stalling, especially if Geo repeatedly uses preserved setup. Recommend a maximum accumulated capacity plus a duration or an encounter boundary. Keep the supplied damage-based shield basis only after defining whether it uses calculated damage, shield-plus-health loss, or health loss alone. Overkill and multi-target totals must not accidentally multiply it.
- **Wet/Nature:** State whether reduction means percentage points or a multiplier. In current code, Avoidance affects debuff application, not attack evasion; Control affects the target's outgoing debuff applications. Nature therefore reduces the afflicted enemy's control reliability rather than making it easier to control that enemy. Confirm this is intentional.

## Character-kit audit

### Anemo Lumine

The support identity is coherent: help the team exploit prepared enemies and improve Swirl. Define whether the 'after attacking' counter counts cards, damaging hits, or targets. Recommend executed attacking cards, so AOE/multi-hit animation choices do not accelerate the passive. Define whether the threshold bonus applies on the threshold attack or afterward, how it resets, and which damage categories receive each bonus.

Her wind-resistance reduction needs an elemental resistance stat distinct from generic Resistance, which currently participates in the pierce calculation. On her ultimate, order buff/stance removal, damage, Swirl, defeat, and the existing crush/hit-reaction behavior explicitly. Reaction generation must not reuse or override the existing physical hit-reaction enum.

### Lisa

Conductive gives Lisa a setup-and-cash-out identity alongside Shock. Keep it as a separate named status so reactions consume Shock without automatically consuming Conductive.

Her skill that increases damage per Conductive stack and then removes stacks needs a clear snapshot/consumption point, particularly for multi-hit damage. Clarify whether defense reduction is per stack or a shared status effect and whether stacks affect all allies' damage. Her ultimate defense-related reduction must specify its stat bundle and relationship to Conductive.

An ultimate that deals Electro damage and applies Shock must not use both deliveries as two independent reactions unless explicitly authored. Conditional crit-applied Shock is evaluated from that card's eligible contact and must follow the same accepted/rejected/consumed rules as ordinary Shock.

## Five-component evaluation

| Component | Finding | Required evidence |
| --- | --- | --- |
| Clarity | Biggest weakness: hidden consumption, ambiguous incoming direction, and AOE source order. | Players can predict the selected reaction, affected neighbors, and lost debuffs before committing. |
| Motivation | Strong potential: convert remaining DOT/control value into immediate payoff. | Players sometimes choose to retain setup and sometimes spend it, for explainable tactical reasons. |
| Response | Card order and target selection provide agency if outcomes are forecast. | Reordering a planned combo updates its reaction forecast without committing battle state or consuming random rolls. |
| Satisfaction | Named reactions can make team cooperation legible. | Use a distinct visual cue and sound, plus readable reaction names; group repeated secondary numbers to avoid noise. |
| Fit | Strong fit for faction teams and card sequencing. | Swirl, burst conversion, control, and shielding support distinguishable plans rather than all becoming interchangeable bonus damage. |

## State contract and edge cases

| Property | Draft contract |
| --- | --- |
| Entry | Eligible team/source; authored damage contact or accepted damaging status delivery; compatible target setup; application budget available. |
| Exit | Contact and its permitted child events settle; temporary statuses follow their named clocks. |
| Interruption | Evade prevents contact. Status rejection prevents a status-dependent trigger. Defeat, cleanse, and dispel remove their explicitly owned states. |
| Chaining | Swirl application may trigger a reaction. Other reaction damage and ticks are terminal in the recommended first version. |
| Cost | Consume all eligible participating status stacks except the draft Swirl/Crystallize preservation exceptions. No additional card action is spent by reaction child events. |
| Ownership | Incoming source owns reaction damage, child effects, and snapshots. Existing debuff owners lose only the consumed setup. |
| Boundaries | Define shield contact, immunity, zero health before deferred defeat, simultaneous lethal AOE, cannot-die states, dead sources, taunt, empty slots, reserve entry, cleanse, and encounter reset. |

The engine currently defers fighter defeat until the action boundary. Proposed rule: finish ordinary authored hit presentation as it does today, but do not generate fresh reactions or elemental setup on targets whose HP has already reached zero. Reaction deaths must join the same pending-defeat process, including secondary targets absent from the original card target list. Finalize reserve entry only at the established battle boundary, not midway through a spreading effect.

## Validation plan

These are planned checks, not completed tests. The prediction threshold below is a starting acceptance target rather than a balance benchmark.

| Scenario | Pass/fail and response |
| --- | --- |
| New player/readability | After a short guided example, predict at least 8 of 10 prepared outcomes, including consumed stacks. If missed, simplify priority/chain rules and improve preview before tuning damage. |
| Stack sacrifice | Compare triggering now versus retaining DOTs across different enemy turn timings. Fail if either choice always dominates across intended encounter roles; adjust reaction payoff, setup access, or status value. |
| Direction | Reverse Pyro/Cryo and Pyro/Hydro card order. Correct reaction and amplification appear; unrelated stacks remain. |
| Stress/multi-hit | Compare equivalent single-hit, multi-hit, and AOE cards. Limits follow authored elemental opportunities, not animation slice count. |
| Swirl topology | Run the user's Pos1/Pos2/Pos3 examples, empty slots, and reversed slots. Outcome follows the selected source-snapshot and live-consumption rules exactly. |
| Abuse | Try repeated Geo on preserved setup, repeated Freeze, Burning refreshes, and reaction-boost passives. Fail on unbounded accumulation, denial without a recovery opportunity, or unintended recursive feedback. |
| Contact rejection | Test shields, evasion, debuff immunity, CC immunity, and rejected conditional Shock. Consume only what the chosen policy allows. |
| Activation | Mixed starting roster stays ineligible after non-Genshin deaths; qualifying reserve deployment preserves activation; repeated providers do not duplicate reactions. |
| Authority/replay | Same initial state and actions reproduce the same outcome and reaction event order; preview leaves the live state/RNG unchanged. |
| Skill expression/AI | Compare deliberate setup/trigger sequences against arbitrary order. Combo-aware AI must value the sequence and the debuff value it sacrifices. |

Tuning order: make card ordering responsive and previewable; remove rule ambiguity; verify consumption and finite chains; test control/shield counterplay; then tune damage, duration, and faction-team payoff.

## Implementation plan after design decisions

1. Lock a directional reaction table, status registry, consumption exceptions, contact policy, damage modifier policy, and chain rules. Produce worked examples with before/after status lists.
2. Add element identity and explicit application metadata to shared content/state, preserving existing attribute colors and damage families. Add authoritative reaction events and source attribution.
3. Build a small deterministic vertical slice: setup into direct amplification, Shock/Pyro conversion, and Lumine Swirl into a secondary reaction. Keep content fixtures distinct from final character balance.
4. Add consumed-stack forecasting, reaction feedback, and formation targeting clarity. Expand preview beyond the current hand/gauge draft rather than claiming it already simulates combat.
5. Resolve Lisa/Lumine passive timing and remaining reaction-specific control/shield/DOT contracts. Extend AI scoring and candidate ordering for elemental setup and conversion cost.
6. Run the planned abuse, replay, and player-readability checks. Expand Dendro and optional custom propagation only after the smaller system demonstrates understandable tactical choices.

Next decisions to discuss: preservation exceptions for Swirl/Crystallize; terminal versus chaining Overload; non-damaging preparation; multi-element priority; Charged timing; Freeze recovery; Crystallize limits; and whether a Gnosis provider is additionally required.
