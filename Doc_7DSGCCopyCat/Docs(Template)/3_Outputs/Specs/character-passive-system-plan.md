# Character Passive System — design and implementation plan

Status: architecture and eight-character runtime slice implemented; ready for review. Owner: Codex.
Request: 2026-10-04, design passives after Status Container / Card Effect; eight existing fighters may be migrated after architecture. Cassandra Origin is an explanatory example only.
Workflow: `/implement-feature`. The user authorized architecture followed by implementation in this request. No publishing or merge is included.

## 1. Contract and scope

The authoritative shared C# core owns passive resolution. Unity plays the resulting events. Character IDs belong in content authoring, never in battle branching. A passive is a collection of independent rules, not one character-specific callback.

The existing `PassiveSystem.CollectOperations` only collects operations; `BattleEngine` currently does not execute those collections. The status/card foundation provides useful types but does not yet implement every authored immunity, damage keyword, operation, or trigger. This plan must not treat those declarations as completed gameplay.

Source contract: `fighting-allstar-combat-source-contract.md`, especially sections covering stat groups, SUB eligibility, turn ordering and King. Existing decisions:

- SUB: living owner in active formation **or reserve**. On-Field: living active owner.
- King: +8 percentage points Pierce at **own team's TurnEnd**, maximum +40.
- Basic = ATK/DEF/MaxHP; AttackRelated = ATK/Pierce/CritChance/CritDamage; HPRelated = MaxHP/Recovery/Regeneration/Lifesteal.
- Flat stats use percent of base; rate stats use percentage points unless explicitly authored otherwise.
- MaxHP decreases clamp current HP, never kill through a stat change alone.

## 2. Rule model

| Part | Required meaning |
|---|---|
| Identity | Stable passive ID, rule ID, content version and provenance. Runtime keys also include fighter instance ID, so mirror teams never share counters. |
| Availability | Minimum/maximum constellation tier, allowed battle modes, owner presence and alive requirement. |
| Trigger | A committed combat fact or lifecycle boundary, separate from animation events. |
| Event filter | Actor/recipient relation to owner, card vs passive vs DOT origin, ultimate/category/rank, damage family, critical/block, actual deltas. |
| Condition | Explicit subject (owner, actor, event target, operation target), AND/OR/NOT, status recipe/tag/source, counters, HP/gauge and roster queries. |
| Target query | Relative to **effect owner**, with explicit include-self, active/reserve, attribute, series and trait filters. Trigger actor and trigger target are separate selectors. |
| Effect | Existing Card Effect / Status primitives; no separate passive damage or status formula. |
| Limits | Per battle/owner turn/root action/target/hit, cooldown, counter cap; explicit reset clock. |
| Lifetime | Aura while eligible, persistent counter, timed status, snapshot application, or scheduled next-boundary activation. |

Two rule kinds are necessary:

1. **Continuous contribution (aura):** derived from current eligible owners and current facts. Kyo's ATK depends on current Ignite stacks. Removing Ignite immediately changes the contribution. Do not repeatedly apply a timed buff at every query. Auras have owner/rule attribution and are not dispellable statuses unless explicitly authored as statuses.
2. **Reaction:** subscribes to a committed event, evaluates a condition, then executes ordered commands. King changes a persistent capped counter; Shingo responds to actual enemy gauge drain. Timed buffs produced by a reaction remain in the Status Container and follow their own lifetime.

## 3. Constellation contract

Freeze the selected C0–C6 tier in the battle snapshot. Passive gates are independent of ultimate coefficient tiers even when progression uses the same tier number.

- Unlock: rule has `MinimumTier = N`.
- Replacement: the earlier rule ends at `N-1`, upgraded rule starts at `N`. Do not run both versions accidentally.
- Additive upgrade: separate stable rule ID explicitly active alongside the old rule.
- Numeric upgrade: non-overlapping tier variants of a rule with the same gameplay purpose.
- Validate 0–6 bounds, empty ranges, duplicate IDs and unsupported payloads before battle.
- Existing eight sources specify no constellation changes to passives: author them at C0–C6. Do not invent unlocks.

## 4. Event and ordering contract

Events carry sequence, root action ID, parent event ID, owner/source ID, actor ID, target ID, turn side/index, origin, family, actual HP/shield/gauge deltas and hit result. A critical is a **resolved logical hit**, not an animation strike. A multi-target card may supply multiple hit facts; a once-per-card rule deduplicates on root action ID.

Battle initialization: freeze roster/mode/tier → build initial auras and MaxHP → battle-start reactions → opening deal → first team turn.

Team turn: enter side → boundary conditions/reactions → DOT/recovery/death/reserve reconciliation in documented order → recompute auras → draw/readiness → planning. Scheduling uses owner-team turn indices, never display `TurnNumber` alone.

Card: validate → consume → BeforeAction effects/reactions → emit attack presentation start → BeforeDamage packet preparation → commit logical hit(s) → lifesteal/Additional/reflect/reactions at authored windows → defeat cleanup → AfterAction → duration boundary. A "remove buff, inflict 400% ATK" BeforeAction removal is committed before pounce and before damage stats/policy are sampled.

Dispatch uses a deterministic bounded FIFO of facts, with stable owner-side/team-index/rule-ID order and authored command order. Newly generated facts append; never recursively call all subscribers on the C# stack. Snapshot eligible subscriptions per fact, then recheck owner/target validity before a command. Reaction-generated events preserve root lineage. Default reactive damage does not trigger card-only rules. A per-chain execution budget fails the cloned transaction rather than returning partially resolved combat.

Zero actual gauge drain emits no drain reaction. Shield loss and HP loss are separate. Lethal hits still have hit facts; defeat is emitted exactly once after the damage batch. Outcome finalization follows permitted death reactions. Dead-owner death triggers require explicit eligibility; ordinary passives stop when owner dies.

### Confirmed PG / ultimate lifecycle (2026-10-04)

The user confirmed King triggers at his team's turn end and Shingo restores one PG per PG actually drained by his non-ultimate card.

When drain leaves a fighter below 5 PG, immediately remove that fighter's pending Ultimate card from the Deck/hand and emit `CardRemoved` for playback. A queued reference to that removed card cannot execute. The Ultimate remains unavailable below 5 PG. Reaching 5 PG again restores readiness; the existing turn-refill rule inserts a new Ultimate when there is deck capacity, without duplicating an existing one. Reaching 5 is eligibility, not a mid-action random-card draw. Removal and any resulting merges are resolved in order; Shingo's refund uses the actual drain before those later gains.

## 5. Ownership, stacking, duration and derived stats

- Status source is the passive owner, even when an ally's attack caused it. Source ownership must survive DOT snapshots and source death.
- Recipe identity and source identity are separate. RefreshStronger preserves the stronger payload's source and refreshes duration to the greater remaining duration. Equal-strength refresh uses a documented stable tie rule. A weaker owner's application cannot steal attribution.
- AddStacks declares cap and duration model. Independent stacks declare independent durations/snapshots. Grey color, polarity, dispellability and behavior are independent fields.
- Auras from separate owners compose using explicit stat operations. Removing one owner removes only that owner's contribution. Aura loss does not cleanse timed statuses already granted by that owner.
- Start-of-battle MaxHP bonuses initialize full-health units at their effective maximum. Supplied carried HP remains absolute. Later MaxHP increases do not heal; decreases clamp to at least 1 for a living unit.
- Dynamic owner-stat auras (Mai) read a defined dependency layer: base + statuses + non-owner-stat auras. Do not recursively include stat-derived auras. Future cross-aura dependencies require an acyclic dependency graph, not iterative order-dependent evaluation.
- Team/series counts declare initial roster vs current living roster vs living active formation, plus include-self. Store initial roster facts when the text says game initialization.

## 6. Cassandra design walkthrough (no character implementation)

| Clause | Declarative decomposition |
|---|---|
| Initial team HP-related +5% per Soul Calibur member | BattleStarted; initial-roster series query; snapshot count; apply group modifier to eligible allies. Confirm self/reserve count from source. |
| Twilight buff for 2 turns on own turn entry | TeamTurnStarted; event side = owner side; apply grey status to living eligible allies. Its application cleanse is a command, not an aura that cleanses continuously. |
| Ally critical grants Punishment stack, max6 | Card-origin logical DamageResolved; critical + applicable allied actor; apply owned AddStacks status. Explicit granularity per target hit vs per card must be authored. |
| Full stacks grant Blessing next own turn | TeamTurnStarted checks owner Punishment count >=6. This naturally waits until the next own turn instead of granting on the sixth hit. Whether stacks are consumed, whether retriggering is allowed, and whether "full once reached" latches despite later removal require source decisions. |
| Twilight scaling by blue SC members | Compound attribute AND series query, declared roster snapshot/current scope; outgoing/incoming policy modifiers. Stun/petrify immunity uses typed status rules. |
| Punishment ignores crit defenses | Per-attack defender calculation view; never mutate enemy base stats or create a lasting debuff. Confirm per-stack values/caps. |
| Blessing MaxHP Additional damage | Status-scoped on-card attack rule; typed Additional packet based on target effective MaxHP; lineage prevents recursively triggering itself. |
| PvP-only enemy reduction and death clauses | Mode gate = PvP on each rule. AlliedDefeated increments counter capped3, enemy defeat heals eligible allies. Resolve both-team deaths deterministically. |
| Soul Calibur subpassive | Separate rule bundle with its own presence/trait requirements; never infer eligibility from localized display name. |
| Skill1 conditional taunt | Card operation condition checks owner's Blessing recipe; reuse same status condition primitive. |
| Bracketed ally-death protection | Separate death reaction. Constellation gate remains unspecified until source gives exact tier. |

Open Cassandra source details: exact unlock tiers; self inclusion in ally events; crit granularity; stack consumption; grey duration clock; count scope; tenacity charges; taunt targeting; additional-damage immunity precedence. None are guessed or added to the roster.

## 7. Eight-character migration

| Character | Source-backed rule | Implementation notes |
|---|---|---|
| Kyo94 | On-field self ATK +3% per Ignite on field; cap30% | Count living active units on both sides and real stacks, not status object count; derived contribution drops when stacks disappear. |
| Chin94 | SUB Green allies HP-related +20% | Attribute filter; MaxHP percent-of-base, three rates +2000 bp. |
| Kensou94 | SUB allies Recovery +40% | +4000 bp to total Recovery multiplier. |
| King94 | SUB own-team turn end Pierce +8%, cap40% | Owner counter0–5, aura +800 bp per counter; entering reserve does not reset. |
| Mai94 | On-field enemy Recovery reduction equal to own Regeneration | Dynamic owner-stat contribution; recompute after statuses/eligibility changes. |
| Shingo97 | Own non-ultimate enemy gauge drain restores own gauge | React to actual negative gauge delta, exclude ultimate and passive-origin changes. User confirmed 1:1 actual drained gauges. |
| Benimaru94 | SUB Red allies Attack-related +10% | ATK percent-of-base, three rates +1000 bp. |
| Athena94 | SUB women allies Attack-related +15% | `trait.women`, not names or art; ATK percent-of-base, three rates +1500 bp. |

Default allies includes eligible owner; aura recipients are living roster so reserve stats are ready on entry. These rules do not migrate the 24 card effects. Kyo and Shingo need actual Ignite/drain operations supplied by cards or other systems; existing placeholder card damage cannot manufacture those facts.

## 8. Implementation slices and file responsibilities

1. **Architecture (this document):** source mapping, ordering, gate semantics, Cassandra decomposition, acceptance cases.
2. **Eight-character runtime:** typed gate/filter/aura/reaction content; battle-owned counters; deterministic dispatch; stat contribution layer; actual gauge events; lifecycle integration; content authoring hookup. Authoring definitions remain inspectable C# data, frozen into character definitions at content load/export.
3. **General passive effect integration:** unify Card Effect execution, condition expressions, status-owned triggers, delayed commands, per-action limits, provenance and trace projection. Reject unsupported published content until implemented.
4. **Advanced kit capabilities:** typed immunity/tenacity/taunt, packet-local crit overrides, triggered Additional damage, simultaneous hit/death batches. Cassandra remains a design fixture until her authored data and unlocks are supplied.

| File/system | Responsibility |
|---|---|
| Content/PassiveRules.cs | Serializable rule definitions, gates, filters, validation, deep clone. |
| Content/StandardCharacterPassives.cs | Eight authored data bundles; no character checks in the resolver. |
| Combat/CharacterPassiveRuntime.cs | Aura refresh, counters, deterministic event reactions. |
| Combat/BattleModels.cs | Mode, passive runtime state, counters and cloned contributions. |
| Combat/StatusSystem.cs | Shared stat calculation includes passive contributions. |
| Combat/BattleEngine.cs | Emit committed lifecycle/gauge facts; refresh derived stats at mutation boundaries. |
| CombatContentSource / editor catalog builder | Attach authored bundles before validation and freeze; keep source descriptions. |
| Projection/playback | Effective MaxHP and actual gauge changes, never evaluate gameplay independently. |

## 9. Acceptance scenarios and review checklist

- Same snapshot/seed/plan produces identical counters, stats and event order; cloning cannot mutate the source snapshot.
- C0 below unlock, exact unlock tier, C6, replacement boundaries; invalid gate rejected. PvP rule absent in PvE.
- SUB active/reserve; On-Field reserve inactive; source death removes only its aura; deployment recomputes.
- Mirror owners have independent counters and status source IDs. Enemy action never changes which side "allies" means for an owner.
- Kyo: 0/1/10/11 Ignite stacks across teams, reserve excluded, cleanse/expiry immediately removes bonus.
- Chin: Green filter, rates in percentage points, MaxHP initialization/carry/clamp. Kensou: Recovery affects card heals, lifesteal and regeneration through shared healing calculation.
- King: own turns only, +800 per turn, cap4000, no duplicate boundary execution or reset on cloning.
- Mai: source Regeneration changes update enemy Recovery; source death clears; simultaneous Mai owners do not recurse.
- Shingo: 0 drain=0 gain; partial drain uses actual delta; gauge cap; ultimate excluded; ally/passive drain excluded; depleted ultimate readiness reconciled.
- Benimaru/Athena: exact attribute/trait filters; overlapping owners compose without replacing one another.
- Future Cassandra fixture: five vs six stacks, no same-action Blessing, next own-turn activation, PvP filtering, critical granularity, Additional loop prevention, simultaneous death behavior.
- BeforeAction strip precedes CardPlayed/pounce; presentation never drives triggers.

Runtime checks should exercise public BattleEngine entry points and compare cloned snapshots. A successful compile alone is not gameplay verification. No Cassandra runtime content is part of acceptance for this task.

## 10. Progress

- Architecture and eight-character source mapping: complete.
- Eight-character runtime implementation: complete. `PassiveDefinition.Auras` and `.Reactions` are executed; the older `.Triggers` collector API is not connected to execution and is rejected by passive validation if published.
- Implemented runtime vocabulary: availability gates, owner-relative attribute/trait/series target filters, constant/field-stack/counter/owner-stat scaling, BattleStarted/TeamTurnStarted/TeamTurnEnded/GaugeChanged facts, capped counter increments and gauge commands. The broader condition/operation/scheduling vocabulary in sections 2–6 is the follow-on architecture, not a claim of implemented Cassandra support.
- Catalog integration: loader attaches missing starter bundles; battle setup provides the same fallback for old starter catalogs with source text. Explicit authored bundles take precedence. The Unity exporter now writes those bundles with `phase-e-passives-v1`. The checked-in old JSON is preserved; its existing content hash describes the old JSON, not the compatibility overlay. Regenerate/version the export before using it as a network content-hash authority.
- Lifecycle integration: refresh on setup, action/utility mutations, expiry, death and reserve entry. Stat-derived auras sample the lower dependency layer. Passive owner IDs and event snapshots are remapped for run encounters.
- Healing/UI integration: card healing now observes recipient Recovery; HUD/projections use effective MaxHP; passive stat and gauge changes have playback facts. Leaving battle clamps temporary bonus HP to the frozen run MaxHP; carried HP entering battle remains absolute.
- Compile/review: shared Contracts/Core build succeeded with zero warnings/errors; Unity plugin assemblies rebuilt. Static diff whitespace check passed. Unity scene execution and gameplay assertions have not been run.
- Automated gameplay tests: not requested; acceptance scenarios documented above.

### Remaining follow-on work

The universal Card Effect executor, status-owned reactions, critical/hit/death event subscriptions, arbitrary conditions, delayed scheduling, per-action limits, and Cassandra's advanced effects remain slices 3–4. They are deliberately distinct from the completed eight-passive runtime slice. The existing eight cards retain their current implementation; Kyo and Shingo react when real Ignite/drain effects are supplied, and their new passives do not add those missing card effects automatically.
