---
slug: genshin-element-system-tests
status: draft
source: manual
gdd_tags: [elemental-reactions, mechanics, ai, actor-camera, architecture]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Elemental system: acceptance test plan

Date: 2026-10-15. Canonical contract: [GDD `@tag:elemental-reactions`](../../1_Inputs_Templates/GDD_Fighting_Allstar.md#genshin-inspired-elemental-reactions). Sequence and implementation dependencies: [architecture plan](../Specs/genshin-element-system-arch-plan.md).

**Execution update, 2026-10-15:** A–C shared-core checks now pass (264 assertions), with relevant status, Additional damage, dungeon/combat, server and Unity compilation checks. See [implementation evidence](../../../../Artifacts/GenshinElementSystem/ImplementationAC.md). The full broad harness reproduces a pre-existing first-hit timing failure in an isolated pre-change baseline. Rows involving Swirl, other output reactions, complete kits, forecasts, AI, visuals, Android or player feel remain planned/not run; a partly covered row is not a complete feature pass.

## Fixture requirements

**G–H execution update:** [G–H evidence](../../../../Artifacts/GenshinElementSystem/ImplementationGH.md) records the implemented enemy-planning and Practice repair, 543 G–H/tactical AI assertions, 391 Unity Practice checks including 105 authored card plays, 28 presentation/audio/cleanup checks and related regressions. Conditional pure preparation is now included in subsequent-contact forecasts. Android runtime testing remains pending by explicit owner choice; human predictions/feel and long balance sessions are not automatically marked passed.

**D–F execution update:** [current evidence](../../../../Artifacts/GenshinElementSystem/ImplementationDF.md) supersedes the future-only status above for D–F. Passed: 440 focused elemental/forecast/DOT/kit assertions (264 A–C + 89 expansion + 87 kits), 9879 dungeon/combat/save assertions, 101 status regressions, the dedicated legacy Shock snapshot check, 4031 description assertions across 39 characters and 468 ranks/tiers, persisted Unity-kit export/healing, server/core builds and 24 rendered feedback checks. Regression coverage includes exact fractional drain/refund replay and retaining Conductive on an evaded target despite a prior matching card ID. AI combo valuation, Android runtime testing, long balance sessions and human readability/feel approval remain unrun. The unrestricted harness still stops at the known baseline `First-hit status ran late` assertion.

Use isolated, versioned fixture content: qualifying and nonqualifying starting rosters, explicit trait IDs, neutral stats, deterministic or forced acceptance outcomes, eligible aliases from different sources, ordinary unrelated DOTs, and controlled formation slots. Fixture values are not production balance approvals. Test both acting sides and swap mirrored formation positions where relevant.

For every contact, assert pre-state, selected reaction/source, consumed instance IDs, preserved statuses, resulting HP/shield/control, application/receipt budgets, event parent/root IDs, and final projection. Do not assert only floating-text strings or method call counts. Every consuming reaction must remove the intended matching instances and no unrelated instance.

## Functional and regression matrix

| ID | Scenario | Expected evidence |
| --- | --- | --- |
| ER-01 | Starting roster includes a non-Genshin SUB; that fighter later dies | Reactions remain disabled; no accidental unlock from living-roster counts. |
| ER-02 | Qualifying roster deploys reserve; several fighters describe Gnosis | Enablement remains stable; no duplicate provider-triggered reactions. E-01 determines any additional passive gate. |
| ER-03 | Reverse Pyro/Cryo and Pyro/Hydro contact order | Correct directional amplification is applied to the eligible hit once; reference/starting multipliers match configured rules. |
| ER-04 | Target has several Pyro aliases/stacks from different owners plus Shock and Poison | Overload consumes every eligible Pyro/Electro instance while Poison and other unrelated statuses remain. |
| ER-05 | Elemental damage-only hit versus explicit elemental debuff delivery | Both can trigger under their policy; damage-only hit leaves no invented debuff on an unprepared target. |
| ER-06 | Direct Electro hit also applies Shock | One eligible contact, no duplicate reaction, no consumed Shock reappearing through the same deferred rider. |
| ER-07 | Pure utility preparation and coexisting elemental debuffs | Application alone triggers no reaction; later damaging application checks its incoming element only. |
| ER-08 | Equivalent single-hit, multi-hit, and AOE cards | Visual slices do not add application opportunities; action budgets reset only at their defined boundary. |
| ER-09 | Swirl/Crystallize on eligible source | Source setup survives; consuming reactions at a Swirl destination remove all matching destination fuel. |
| ER-10 | Single-target Anemo on Pyro Pos1, Shock Pos2, Cryo Pos3 | Swirl→Overload resolves; terminal explosion damages neighbors without Melt; Pos3's Cryo survives that explosion. |
| ER-11 | AOE Anemo on the same formation | Root batch inputs remain simultaneous; consumed Pos2 Shock cannot later emit; newly delivered statuses cannot join the root snapshot. |
| ER-12 | Multiple adjacent Overload triggers | Valid local fuel consumption remains recorded; overlapping victims receive only the configured shared damage budget. |
| ER-13 | Taunt, empty slots, temporary visual displacement, and reserve targets | Actual root target and formation adjacency follow the GDD; no compacted-list or Transform-based spread. |
| ER-14 | Several coexisting elements in different status insertion orders | E-02's authored priority gives the same selected reaction and consumption; no incidental list-order dependence. |
| ER-15 | Evasion, shields, zero-effective damage, status rejection, CC immunity | Only eligible contacts consume fuel. Cached acceptance is reused. Immune-output consumption follows E-04 rather than a partial transaction. |
| ER-16 | Reaction kills a secondary victim; simultaneous lethal roots; cannot-die target | Secondary deaths join the same pending collection; no fresh setup on zero-HP targets, premature reserve entry, duplicate defeat, or invalid healing. |
| ER-17 | Existing Ignite/passive contributions disappear on consumption | Correct contact retains its snapshotted modifiers; later contacts refresh contributions; unrelated cards/traits keep their existing behavior. |
| ER-18 | DOT bridge, reaction bonuses, crit/block, DEF/pierce, resistance, caps, rounding | Configured E-03 category/stage matrix applies each modifier once; neutral/absent element preserves legacy results. |
| ER-19 | Charged/Burning ticks, source death, cleanse, refresh, duration end | E-06 clocks/snapshots/stack policy hold; no reaction starts from a tick and no stale source-object dependency appears. |
| ER-20 | Repeated Freeze and queued disabled cards; boss immunity | E-04 expiry/recovery and existing card spending/fizzle rules remain explicit; repeated application cannot bypass the recovery opportunity. |
| ER-21 | Repeated Crystallize, overkill, shields, multi-target Geo, encounter end | E-08 basis/cap/expiry is respected; action totals are not paid repeatedly per victim; no unbounded accumulation. |
| ER-22 | Lumine counter and Lisa Conductive/crit-Shock interactions | Authored card/hit granularity holds; Conductive is not reaction fuel; consumption timing and bonus categories match validated kit definitions. |
| ER-23 | Reset, undo, reorder, and final-slot auto-commit | Public forecast updates in time; no live status/RNG/PG mutation; consumed setup does not remain available in later forecast steps. |
| ER-24 | Conditional crit/application, hidden opponent hand/RNG, reconnect | Forecast reports uncertainty and exposes only permitted facts; recipient projection and resumed HUD reproduce the event cursor. |
| ER-25 | Identical snapshots/actions, duplicate command, serialization round-trip | Same outcome and event order; no duplicate reaction on retry; eligibility and any resumable action budgets survive appropriately. |
| ER-26 | Non-elemental content and disabled elemental feature | Existing headless combat cases preserve their results; unsupported schema/content is rejected explicitly rather than guessed. |

E-04/E-05/E-06/E-07/E-08 dependent outputs cannot pass through placeholder expectations: record actual owner-approved/configured decisions before marking their rows passed.

## Player and AI scenarios

- **New player:** after a guided setup/trigger example, predict reaction, recipients, and lost stacks in prepared situations. **Starting acceptance target:** at least 8 of 10 correct predictions. If below target, improve previews/priority or simplify propagation before increasing damage.
- **Skill:** compare deliberate card ordering against arbitrary ordering in equal-investment states. Pass when players can explain a better sequence and when retaining a DOT is preferable in an authored counterexample; fail if every situation rewards the same immediate conversion.
- **Abuse:** repeatedly prepare/convert, refresh Burning, freeze the same enemy, and farm Geo barriers. Fail on unbounded shield/stack loops, uncontrolled child events, or denial without the chosen recovery opportunity. Fix lifecycle/budget rules before numerical tuning.
- **Readability:** an observer identifies the trigger, consumed setup, and terminal explosion without developer narration. Fail if reaction damage is mistaken for another card or a physical knockback effect; adjust labels/impact grouping/audio.
- **AI:** prepare public states where a setup/trigger combo is useful and where sacrificing long-duration DOT is harmful. Pass when candidate generation includes both options and scoring reflects the state without private information. Do not use win rate alone as proof of correct reasoning.

## Presentation and platforms

On Windows and Android, verify quick-tap queueing, hold details, target/splash indicators, pre-commit forecast visibility, reset, full resolved damage text, and final status HUD. Record slow playback, cinematic skip, frame-rate variation, and interrupted/resumed playback. Expected outcome: identical authoritative events and final state, correct root/child readiness, no camera-state changes, and readable visual plus audio confirmation.

## Roster extension checks

Source: [five supplied kits and generated stats](../Specs/CharacterData/genshin-character-roster.md), [GDD `@tag:genshin-roster`](../../1_Inputs_Templates/GDD_Fighting_Allstar.md#genshin-starter-roster). These checks were added after owner approval of the baseline plan. They remain **not run**.

| ID | Scenario | Expected evidence |
| --- | --- | --- |
| GR-01 | Import all five draft stat profiles and rank tables | Supplied R3/C0 values/rarity/colors remain intact; generated numbers have provenance; neutral Avoidance is 100%/10000 bp; element is distinct from attribute and target icon. |
| GR-02 | Compare level-one/intermediate/max-level profiles and different bench choices | Existing linear growth/CC calibration remains intact; no accidental fallback-growth mismatch, extra team slot, or free On-Field passive from reserve. Apply the roster's stat adjustment plan if a role always dominates. |
| GR-03 | Bunny natural expiry versus cleanse/replacement/defeat/consumption/reset | Only natural expiry schedules an attack under the selected lifecycle; safe cleanse emits no explosion. No duplicate attack on replay/resume. Marker itself is not elemental fuel. |
| GR-04 | AOE Bunny markers expire together against mixed elemental setup | Explicit triggered Pyro contacts can react; confirmed terminal Overload cannot chain. Proposed shared expiry-group budget limits overlapping Overload, and Explosion metadata alone does not create extra splash. |
| GR-05 | Amber buffs Bunny, Overload, another tagged Explosion, and untagged Swirl/Burning | Each eligible packet is doubled once; combined reaction/Explosion tags cannot multiply the same Amber effect twice; unrelated reactions are unaffected. |
| GR-06 | Kaeya initial team gauge and +50% gain-rate status | K-02 timing/rounding/gain-source choices are asserted, including cap, fractional carry if adopted, clone/reset and actual gauge delta. No per-target/multi-hit extra passive grant. |
| GR-07 | Waltz at C0/C5, several protected recipients, repeated incoming hits | Source duration/15% retaliation/30% recovery preserved; C0 self-only, C5 all active allies; Kaeya's personal 30% reduction is not copied onto teammates. No death-triggered revival unless separately authored. |
| GR-08 | Waltz hits Frost Bite-compatible setup and another Waltz/reflect target | Authored Cryo retaliation may react and snapshot its own Frost Bite, but retaliation/reflect descendants cannot bounce indefinitely. Ordinary reflect and DOT remain terminal. |
| GR-09 | Lisa AOE with different preexisting Conductive counts and conditional crits | K-03 target-local snapshot/consumption order; no multiple passive stacks from visual hits; shared application opportunities; Conductive survives elemental-fuel consumption unless skill 2 explicitly spends it. |
| GR-10 | Barbara lowest-HP-ratio heal and Melody with HP aura, shield hits, overkill and source death | K-04 beneficiary/aggregation/snapshot choices; deterministic ties, actual basis, recovery blocking and no healing recursion. MaxHP basis is Barbara's, not each recipient's. |
| GR-11 | Dissolve at target TurnEnd, expiration/cleanse, low PG and ready ultimate | Actual clamped gauge loss occurs at the authored boundary; correct ultimate invalidation and status order. Amount/duration/stacking must be supplied before this row passes; no invented Depletes behavior. |
| GR-12 | Barbara Hydro skill and Melody with unprepared/prepared enemies | Hydro can trigger a reaction; no unstated Wet on unprepared targets and no Hydro aura from Melody Loop. |
| GR-13 | Lumine threshold attack, AOE/multi-hit, reserve support, kill attribution | K-05 granularity/reset and heal-basis choices; source bonuses apply once per category. Unspecified บดขยี้ cannot silently become Shatter or physical hit reaction. |
| GR-14 | Generated lower ranks and C0/C5 matches | All source R3/C0 payloads preserved; generated rank changes follow the documented rule. Incomplete Kaeya C1–C4 progression cannot silently enter a production allowlist. |
| GR-15 | Off-turn forecast/playback/reconnect | Expiry, cancellation, retaliation and gauge drain use recorded origins/causes/lineage. Future prediction is conditional; UI does not rerun authoritative damage during playback. |

Run beginner/readability, stress, deliberate-order, abuse and observer sessions from the existing player scenarios using these kits. In addition, test region-buff membership and whether the three-active-plus-one-SUB bench tradeoff remains understandable. Record E/K decision IDs and content version with results.

## Evidence and completion

Run focused new headless checks, then the affected existing combat/status/multi-hit/privacy regressions through the inspected harness. Run visual/platform checks after projection/playback changes. Store failures and fixes with fixture/content versions. A passing documentation check is not a passing gameplay check; a C# build is not an Android or owner-feel pass.
