---
slug: fighting-allstar-gdd
status: needs-human
source: manual
gdd_tags: [identity, visual-audio, platform-input, core-loop, items, enemies, mechanics, roster, dungeons, ai, tuning, inspirations, tech-stack, prototype-truth, scene-roles, system-ownership, architecture, guardrails, roadmap]
owner: game-design-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Fighting Allstar — Game Design Document

Design draft • 2026-10-01 • First prototype and full-game direction

## Purpose and decision status

**Screen/economy revision:** see [banners and route design](../3_Outputs/Specs/fighting-allstar-economy-route-design.md), [battle screen states](../3_Outputs/Specs/fighting-allstar-battle-screen-states.md) and [Unity implementation phase plan](../3_Outputs/Specs/fighting-allstar-unity-implementation-plan.md). Owner-confirmed rates/costs, forward route choices, persistent HP, Rest and entry difficulty supersede earlier defaults.300-pull selector semantics remain proposed pending clarification.

**Actual-source revision (2026-10-01):** incorporates the supplied guide, 29 WIP characters and effect catalogs. Character tables are external. Conflict decisions and generated values remain reviewable proposals; no runtime kits are implemented by this document.

Fighting Allstar is a 3D card battle RPG about assembling a team whose fighters create opportunities for one another, then adapting that team to a dungeon and its opponents. Its signature is several overlapping counterplay relationships: a strong team has weaknesses, and the player can answer those weaknesses through roster selection, card sequencing, and run boon choices.

**Confirmed** means stated by the owner in the 2026-10-01 request. **Observed** means inspected in this repository; it does not mean verified in a running build. **Proposed** means a concrete recommendation prepared for review. All mechanics, content, scope quantities, timing, economy, and tuning below are **Proposed** unless explicitly marked Confirmed or Observed. They are not claims about the exact rules of Grand Cross. This draft preserves its card battle inspiration while specifying Fighting Allstar's own rules.

The completed documents are review inputs. Runtime implementation, dependency changes, scene/build changes, and deployment are later work. This GDD is the design reference; [the architecture plan](../3_Outputs/Specs/fighting-allstar-prototype-arch-plan.md) describes the implementation boundary and [the test plan](../3_Outputs/TestPlans/fighting-allstar-prototype-test-plan.md) defines verification. There is no claim that the new design is already implemented.

**Confirmed owner clarifications:** constellation starts at **C0/6** and advances through six upgrades to **C6/6**. Dungeons have **linear stages with randomized opposing character teams drawn from the same shared character catalog**. Opponents use ordinary character skills, passives, progression snapshots, and combat rules. Account sign-in uses **Firebase Authentication with third-party providers**. The specific providers and platform integration remain implementation choices for review.

<!-- @tag:identity -->
## Project identity

| Field | Direction and status |
| --- | --- |
| Title | **Fighting Allstar** — Confirmed |
| Genre | **3D tactical card turn based game / roguelite dungeon / character and team driven RPG** — Confirmed |
| Formation | **3 active fighters + 1 reserve against 3 active fighters + 1 reserve** — Confirmed |
| Platforms | **Mobile and PC**, with PC build and Android APK — Confirmed |
| Play model | **Server authoritative online**, with a switchable local AI test backend — Confirmed |
| Core fantasy | Build a fighting team, read its opponent, and win through coordinated card decisions — Proposed expression of the user's priorities |
| Player promise | A larger roster opens more plans and dungeon options; a practiced player can turn an awkward hand into a good turn |
| Camera / play plane | Fixed 3D formations in an arena; camera presents card actions; movement through space is not a combat input |
| Tone | Energetic tournament spectacle, clear tactical information, rivals with readable strengths |
| Story intent | A tournament expedition brings fighters through themed challenge gates; detailed narrative is later content |

### Experience pillars

1. **The team is the build.** A fighter supplies damage, protection, setup, or recovery that another fighter can use. No single fighter should simultaneously provide the best damage, defense, cleanse, and gauge economy.
2. **Several ways to gain an advantage.** Strategy, effect counters, and attributes create separate soft counter relationships. Mixed teams trade peak payoff for wider answers.
3. **Every card action has a cost.** Playing, saving, moving, and merging compete for the same turn budget. A powerful hand still requires a good sequence.
4. **Readable intelligence.** Enemy choices express a plan the player can infer from visible buffs, gauges, roles, and past actions. Difficulty comes from better decisions and authored encounters.
5. **A failed run still belongs to the player.** Defeat ends the attempt, while owned fighters and permanent inventory remain. Restrictions invite preparation rather than destroy investments.

### Design assumptions and consequences

| Assumption | Impact | If wrong | Quick validation |
| --- | --- | --- | --- |
| Mobile touch is the primary interaction model; Windows mirrors it | Cards and targets must work without hover or precision dragging | PC-first expectations may demand different density and shortcuts | Review the same turn on a phone and desktop mockup |
| Each linear stage loads an authored 3D arena and a validated randomized character team | A complete generated run can reuse arena assets while varying tactical matchups | Variety may feel insufficient if team changes do not change decisions | Compare repeated runs with different team seeds and record changed player plans |
| Permanent duplicates fund the six confirmed constellation upgrades | Collection remains persistent and upgrade costs are easy to explain | Too many required duplicate pulls can delay meaningful progression | Test C0 utility and acquisition pacing before final economy tuning |
| Counterplay matters more than collection rarity | Normalized tests and useful starter roles remain necessary | A rarity-first power fantasy may conflict with fair team variety | Playtest starter teams against new combinations at equal progression |

<!-- @tag:visual-audio -->
## Visual, mood, and audio direction

Use stylized 3D fighters with distinct silhouettes and readable stance changes. Prototype art can be capsules, available placeholder models, and simple particles. Gameplay signs must remain legible before visual polish: target outline, health, shield, PG, taunt, Ignite, and harmful status icons. Attributes use a symbol and written name as well as color.

The planning camera shows both formations and the action queue. On confirmation it may frame the acting fighter and targets, then returns to the same planning angle. A camera cut never hides a target-selection change, a reserve entry, or a counter. A reduced-motion setting disables shake, aggressive zoom, and flashes. Ultimate cinematics are skippable presentation; simulation is unaffected by skip, playback speed, or low frame rate.

Sound carries information: a card snap confirms a selection, a distinct chime confirms a merge, a deeper cue announces an ultimate, and contrasting impact sounds distinguish ordinary, critical, blocked, and absorbed damage. Music shifts between preparation, battle pressure, and result screens. Captions and visual cues carry any meaning conveyed by sound. Haptics are optional.

### Combat interaction and feedback specification

| Trigger | What the player sees | What the player hears | Timing rule |
| --- | --- | --- | --- |
| Select a card | Card lifts, eligible targets highlight, action cost previews | Short card click | Immediate local draft feedback; timing budget T-UI |
| Move / merge | Slot destination appears; merge animation shows rank and PG preview | Slide / ascending chime | Draft remains reversible until confirm |
| Reset | Original authoritative hand and resources return | Soft undo cue | No reward, RNG, or PG mutation |
| Final Action slot filled | Queue locks and automatically submits | Clear transition into playback | Only server acceptance starts authoritative playback |
| Hit | Target flashes; damage label includes crit/block; health updates at cue | Matching impact | Cues read already resolved events; T-PACE |
| Heal / lifesteal | Green heal number and source-to-owner trace; caps clearly | Recovery cue | At the recorded heal event, never a second client calculation |
| Guard / counter | Shield or stance icon activates; counter source is framed | Guard ring / response hit | Counter label precedes the response animation |
| Death / reserve entry | Portrait grays out, cards disappear, reserve enters its slot | Defeat accent / entry cue | On corresponding event boundaries |
| Win / loss | Result names decisive effects and persistent rewards | Victory / defeat stinger | Claim status distinguished from visual celebration |

Juice starts with restrained hit flash, impact audio, and short card movement. Screen shake and haptics default off in the earliest functional slice until the player can read an entire turn. Their amplitude and duration require device playtesting; no unvalidated camera value is a dependency of battle logic. Jump timing, coyote time, world movement buffering, and collision response are not applicable to this combat model.

<!-- @tag:platform-input -->
## Platform and input philosophy

**Proposed: mobile landscape touch is the primary input model.** PC uses the same decision verbs and rules. Neither device grants more decision time or more combat information.

| Intent | Mobile | PC |
| --- | --- | --- |
| Inspect fighter or card | Tap inspect icon / long press card | Right click or inspect button; hover may show the same information |
| Queue a play | Tap a card to queue it against the preferred living target; tap an opponent model to change that target | Click a card to queue it; click an opponent model to change the preferred target |
| Move a card | Enter Move mode, tap source, tap destination; drag is optional | Move mode or drag |
| Choose enemy | Tap the opponent model | Click the opponent model |
| Reset draft | Visible Reset button | Same button; Escape may cancel current selection |
| Commit turn | Filling the Action field commits automatically | Filling the Action field commits automatically |
| Review stages | Tap the current or upcoming stage in the linear progress list | Click a stage entry |

The battle surface keeps only immediate decisions visible: card art/rank, ordered Action slots, Reset, and the authored UnitUI above fighters. Holding a card opens its compact skill tooltip; a quick tap queues it. Validation, playback, and diagnostic messages use the Unity log during the prototype. Filling every Action slot commits and begins playback automatically.

Touch cancellation and Reset affect only the unsubmitted draft. Input during playback can open inspection but cannot insert combat actions. Double taps, stale UI bindings, and duplicated network replies must not duplicate commands. Modal dialogs stop underlying touch propagation. Safe-area layouts and text scaling are validated on selected Android devices before calling the mobile interface complete.

<!-- @tag:core-loop -->
## Core game loop

**Confirmed loop:** receive starter **Kyo94**, enter linear dungeons, face randomly generated character teams, win Diamonds, use Diamonds for character gacha, expand the roster, and use eligible fighters in restricted dungeons. Defeat preserves owned characters and permanent inventory. Ultimate/constellation progression advances from **C0/6 through C6/6**.

**Proposed complete session:**

1. Sign in through a configured third-party Firebase provider, then inspect an eligible dungeon's restrictions, enemy tendencies, stage count, and victory reward.
2. Assemble three active fighters and a reserve; inspect enabled passives and missing role coverage.
3. Advance through the dungeon's forward map rows, choosing a reachable encounter and bypassing its alternatives; inspect the generated opposing team before committing.
4. Win battles by managing cards, PG, target priorities, and reserve timing; choose a temporary team boon at the scheduled reward checkpoints.
5. Defeat the final opposing team; the server settles the run and credits Diamonds exactly once.
6. Return to MainMenu, summon or improve a fighter, revise the team, and enter a new eligible dungeon.

### Onboarding and roster access

The first tutorial starts with Kyo94 alone and explicitly uses reduced active-team rules. It teaches targeting and a regular card, then movement/merging and PG. The tutorial is not a full 3+1 battle. Guaranteed tutorial grants then add **Chin94, Kensou94, and King94**, creating a complete starter team without depending on gacha luck. A subsequent practice encounter teaches reserve entry with a protected, scripted lesson; ordinary matches never script the result.

Every account always has an unrestricted foundation dungeon that the starter roster can enter. Restricted dungeons are optional selections until the account owns enough eligible fighters. Eligibility is checked against all selected slots, including reserve. An unavailable dungeon explains which owned fighters qualify and a guaranteed acquisition path for any needed role. **The first Open Circuit completion grants an unowned-fighter selector. Choosing Mai94 or Athena94 opens Women Exhibition alongside King94.** Choosing another fighter is allowed; guaranteed onboarding and the first-clear selector preserve an accessible progression route; ordinary gacha is not an unowned-character guarantee. A player must never need to win a locked dungeon to acquire its first eligible team.

An eligibility rule is an authored predicate over attributes, traits, or named exclusions. The generator cannot invent new account restrictions. The prototype proves both an immediately eligible profile and a stricter profile unlocked by acquiring another role in @tag:dungeons. Difficulty, roster restrictions, and rewards are shown separately; rarity is not a hidden eligibility gate.

### What survives a run

| State | Win | Loss / surrender |
| --- | --- | --- |
| Owned fighters, permanent inventory, constellation progression, preexisting Diamonds | Preserved | Preserved |
| Dungeon victory Diamonds | Credited once after validated victory | Not awarded |
| Run boons, current stage, run HP | Discarded when run closes | Discarded when run closes |
| Discovery/tutorial completion already committed | Preserved | Preserved |
| Unclaimed victory receipt | Claim can be retried idempotently | Cannot be converted from a loss |

Leaving the app suspends presentation and reconnects to the existing server run. It does not reroll a hand, opponent, map, summon, or reward. A committed battle turn resumes from a snapshot/event cursor. A disconnected player follows the timeout rule. A server fault returns a recoverable error and is not recorded as a player defeat.

### Session pacing

The desired arc is **prepare → fight → breathe → adapt → next character team → final test → grow roster**. Battle and session duration targets are starting values in @tag:tuning. The stage list shows the run's length, recovery rule, boon checkpoints, and final elite character-team battle before entry. The final team changes tactical pressure through coordinated kits and permitted progression, using the same character rules as the player. The result screen names key triggers and lets the player inspect a concise battle log to understand a loss.

<!-- @tag:items -->
## Canonical gameplay content — items and progression

| Item / resource | Role | Prototype rule |
| --- | --- | --- |
| Diamonds | Permanent character acquisition currency — Confirmed | Awarded on dungeon completion; server owns wallet and spend ledger |
| Duplicate crest | Character-specific upgrade material — Proposed | A duplicate adds a crest to that character; it does not create an independently fieldable copy |
| First-clear fighter selector | Guaranteed roster-breadth milestone — Proposed | First Open Circuit clear grants one unowned pool member of the player's choice; Mai94 or Athena94 opens Women Exhibition |
| Training credit | Overflow material — Proposed | Duplicates of a completed character convert to generic Collection Token; use is collection catch-up later, not an undefined paid sink |
| Run boon | Temporary team modifier — Proposed | Chosen during the run; cleared on completion/failure; never modifies permanent content assets |
| Stage recovery | Automatic recovery after a stage victory — Proposed | Restores a defined portion of run HP and returns fallen fighters for the next battle; cannot revive midbattle |
| Character entry | Permanent collection ownership | Stable identity, acquired timestamp, constellation, and progression; no fighters removed by defeat |

### Gacha and progression starting model

Banners are separated by series using explicit seriesId and matching series trait (KOF, Tekken, DOA, Street Fighter, etc.). Confirmed ordinary rates:4% aggregate featuredSSR,36%SR,60%R. One pull costs160 Diamonds; ten costs1600, no discount. Each featuredSSR receives its share of the4% bucket. No five-pull unowned guarantee or ten-pullSR guarantee applies.

Confirmed featured guarantee threshold300 pulls. Proposed review policy: every300 paid pulls earns a featured selector; earlySSR does not reset progress, and progress stays in the same series guarantee group. This reset/selector policy is not yet owner-confirmed. Rates, individual weights, carryover and duplicate outcomes are disclosed before spending. The first-clear roster selector is separate from this guarantee. Published banner pools contain only implemented character definitions.

Server atomically debits/grants and stores an immutable summon receipt; retries use the same requestID and return the same results. Reveals/skips cannot spend or grant again. Six character-specific crests upgrade C0 throughC6; proposed capped duplicates become Collection Tokens with no prototype exchange. Full states, receipt recovery and economy starting values are in the linked economy design.

**Confirmed constellation model:** ownership is **C0/6**, followed by **C1/6, C2/6, C3/6, C4/6, C5/6, C6/6**. This is six upgrades and seven displayed states. A constellation changes ultimate parameters and a visible ultimate-related synergy; it does not unlock a fighter's essential role. The initial eight-fighter balance test runs all fighters at C0/6. Mandatory cleanse, guard, or anti-stall answers cannot require duplicates.

The separate roster provides explicit C0–C6 drafts. Default generated ultimate ATK potency grows non-compoundingly by5% of baseline per upgrade; source Kyo Ignite and Robert critical-chance riders keep their stated per-level steps. No universal invented C6 unlock. Relics and equipment are deferred.

<!-- @tag:enemies -->
## Canonical gameplay content — opposing character teams

Every dungeon encounter uses the same versioned character catalog, skills and battle rules as the player. Generator selects legal 3+1 character teams, not separate NPC monsters. Enemy roster/attributes/CC and difficulty are visible before entry; private hand and future RNG remain private.

Proposed encounter plans include Kyo/Chin Ignite setup, Kensou/Athena recovery, and Shingo/King PG denial. Preserve supporting traits and active/reserve passive scope when generating. Elite final teams improve synergy/decision quality with disclosed progression snapshots. Do not silently inflate stats or activate PvP-only passives in PvE. Initial four-character fixtures may mirror both sides; eight-kit generation expands variety.

<!-- @tag:mechanics -->
## System mechanics

The detailed [actual-source combat contract](../3_Outputs/Specs/fighting-allstar-combat-source-contract.md) is part of this design revision. It owns formulas, keyword timing, status clocks, packet bypass flags and source conflicts. Earlier invented combat rules are replaced by the following.

### Team build and initiative

Three active fighters and one reserve; no duplicate definition IDs within a team. Different variants are allowed for the prototype; family restrictions are future rules. Validate ownership, dungeon eligibility and C0–C6; freeze the versioned loadout. Proposed initiative sums authored Class_Combat across the selected roster, including reserve, before transient passives; a server-recorded coin flip breaks ties. CC is an estimate, not the AI evaluation. Existing source CC is retained; there is no invented HP+ATK formula overriding it.

### Cards, planning and PG

- Two skills per fighter, R1/R2/R3. The ordered Deck-field capacity is `min(7, total formation size + 3)`: a three-fighter roster holds six cards; three active fighters plus one SUB hold seven. At battle initialization, deal two R1 skill cards per active fighter from Position 1 toward Position 3. With three fighters and no random capacity this yields Position 1 at indices 0–1, Position 2 at 2–3, and Position 3 at 4–5. Random opening cards enter from the left and shift that known block right; a full 3+1 roster therefore has its random seventh card at index 0. SUB raises capacity but does not enter the card-owner pool while reserved.
- A quick card tap moves it from the ordered Deck field into the next Action slot against the preferred living target; holding the card displays its tooltip without queuing it. Reset restores the pre-plan Deck field exactly, including card identities, ranks, and indices. Filling every Action slot automatically commits and consumes the queued cards; unplayed Deck-field cards retain their order and compact from index 0. At each later owner TurnStart, add ready ultimates as space allows, then insert randomly drawn R1 skills from living active fighters at the left side until formation-based capacity is reached. The opening positional deal is not repeated after Turn 1. Do not draw during planning.
- Adjacent identical fighter/skill/rank cards merge left-to-right up to R3. Settle after a draw batch; no repeated draw/merge refill loop. Move and play each cost one action; automatic merge costs none.
- The action field has three ordered slots when three active fighters are alive and two slots when one or two active fighters are alive. The first queued action always occupies the leftmost slot. Freeze this budget at turn start; a reserve does not add a slot during the current turn.
- PG cap5; proposed play/move/merge gain1 each, opening merges0. No-op moves, pass, reset, rejected plans and ultimate use award no normal PG. At next owner TurnStart, generate one ready ultimate per active fighter in slot order as space permits; then regular refill. Full hand retains readiness. Ultimates cannot move/merge. Drain below5 removes a pending ultimate; valid ultimate spends all PG.
- Client planning is disposable. Reset restores card IDs/order/ranks, PG and action budget from the authoritative snapshot. Drafts do not advance live RNG or reveal random future draws. Filling the final Action slot submits one versioned ordered plan, validated atomically; accepted plans cannot be undone.
- On execution, revalidate actor and target. Dead/disabled actors fizzle. Invalid enemy target retargets lowest valid enemy slot, respecting taunt; invalid ally target fizzles. No free substitute card after an unexpected merge. Passing is always available. Timeout commits passes; reconnect does not reset deadline.

### Attributes and interacting meta relationships

Source cycle **Red→Green→Yellow→Blue→Red**: advantage +20%, disadvantage −20% Normal damage. Opposite pairs neutral in this draft. Light/Darkness carry nonstacking team modifiers from the guide but have no WIP fighters. Traits use actual catalog tags (Women, Japan, Fire Elements, Psycho Soldier, etc.), independently of roles/attributes.

Proposed strategy triangles: **setup burst→recovery sustain→PG denial→setup burst**; **buffed offense→raw damage/defense→dispel/Rupture→buffed offense**. These are matchup hypotheses based on actual card costs, not hidden bonuses or guaranteed outcomes. Attribute is a four-way cycle. Paired-side tests must establish whether these soft counters actually emerge.

### Damage and status rules

Normal base uses `(scalingStat × cardCoefficient) × (1 + Pierce − Resistance) − DEF`, clamped nonnegative before keyword/outgoing/incoming/outcome factors. Normal, DOT, Additional, True-enchanted Normal, Destructive and Additional DOT have separate mitigation contracts. Critical and block are exclusive proposed outcomes; CritDamage is a total multiplier. Never subtract damage from reduction or reduce all packet families together.

Ignite raises damage received, with no periodic HP tick. Poison/Bleed/Shock snapshot damage dealt and tick at their authored owner window. Regeneration heals at TurnStart; Recovery is a total healing multiplier (112%=1.12). Infect blocks recovery. Source stats, missing-value proposals and full rank tables live outside the GDD.

All passives register; On-Field runs only while active, SUB is proposed to remain eligible active or reserve. Mode restrictions are separate: Goro94 and Joe94 PvP effects do not apply in dungeons. Blue dispellability, grey immunity bypass and expiry are separate flags. Stun/Paralyze/Seal and category-specific disables use explicit legality checks. Death disables ordinary passives; authored OnDeath triggers are exceptions.

### Resolution state machine

Validate teams → BattleStart/passives/opening cards → TurnStart/triggers/heal/deaths/reserve/budget/draw → Planning → PreAction → Hit batches → Additional/reflect/deaths → PostAction/counters/follow-ups/reserve → next queued action → TurnEnd/DOT/end triggers/expiry → opposing TurnStart. Terminal result is checked at defined batch boundaries; a remaining living reserve enters before a team is declared defeated. Both rosters eliminated together is a draw.

Batch damage is simultaneous for its target set. Lifesteal uses actual HP removed, excluding shield/overkill; zero-HP fighters cannot heal. Reactions use stable priority/side/slot/source order and lineage guards; no recursive reflect. Destructive death records a separate −1% display sentinel with actual HP clamped0. Animation plays recorded results and never controls state. Turn cap40 is a starting value; reaching it is a draw, not a fabricated winner.

<!-- @tag:roster -->
## Character content and team synergy

The actual WIP catalog contains **29 characters**. Full character tables are intentionally external: [roster and generated ranks/constellations](../3_Outputs/Specs/CharacterData/character-roster.md), [structured authoring drafts](../3_Outputs/Specs/CharacterData/wip-character-drafts.json), and [unmodified source rows](../3_Outputs/Specs/CharacterData/source-catalog.json).

First four proposed runtime kits: **Kyo94, Chin94, Kensou94, King94**. First complete eight-kit slice adds **Mai94, Shingo97, Benimaru94, Athena94**. Remaining21 are planned catalog content, not required to prove the first loop. Missing stats are generated only for the five incomplete source characters; all 29 receive draft R1/R2 and C0–C6 entries. Holy Relics are preserved but disabled initially.

Kyo94 benefits from Chin's Ignite setup, Kensou supplies recovery/cleanse, and reserve King builds Pierce support before entering with Poison and drain. Moving King active trades immediate access to those cards against another active role. Mai supplies anti-recovery/Rupture; Shingo supplies gauge denial; Athena supplies Women support and healing; Benimaru supports Red stats and stance disruption. Team inspection explains active reserve effects and trait/mode mismatches.

This source roster replaces the earlier invented archetypes. No source character is relabelled with another fighter's kit. Character family, source CSV ID, stable definition ID and legacy Unity asset ID remain separate identifiers.

<!-- @tag:dungeons -->
## Generated dungeon stages

Confirmed forward linear map with reachable alternatives: choose which enemy character team to fight and which to avoid. Each accepted move goes to the next row, skipped nodes cannot be revisited, and enemy snapshots cannot reroll. Reference map supplies connected node/floor presentation. [Detailed route contract](../3_Outputs/Specs/fighting-allstar-economy-route-design.md) specifies every state and recovery edge case.

Three proposed profiles remain: Open Circuit (any legal roster), Green Accord (at least2 Green across roster), Women Exhibition (at least2 Women). Starter Kyo/Chin qualifies Green; King plus first-clear Mai or Athena selection qualifies Women. Enemy generation uses the same catalog/restrictions and complete validated teams.

Starting one-floor template:9 rows, Start→Battle choice→Battle choice→Rest/Elite→Boon→Battle choice→Rest/Battle→Elite choice→Boss. Most choice rows have2 candidates. Later compose multiple floors; P0 proves one complete selectable floor. Validate reachable boss, offered Rest access, supported effects and deterministic fallback after8 generation attempts.

Run HP persists exactly through victorious battles, including defeated fighters at0. No automatic postbattle healing or revival. Combat healing persists; cards/PG/temporary statuses reset between encounters. Living reserve fills vacancies; fewer survivors means fewer active units/actions. Total wipe ends run without loss of owned characters. Rest proposed choices: heal living allies40%runMaxHP OR revive one defeated fighter at30%, once per Rest node. No external roster swaps during a run.

Entry difficulty0..100% is frozen: enemy ATK/DEF/MaxHP multiplied by1+d/100, reaching2× at100%. Substats/skill coefficients/PG remain unchanged. Proposed completion reward320×(1+d/100) Diamonds; grant only on final victory, once. First-clear/grants are not multiplied. Display exact stats/reward before entry; reward slope/base are starting values.

Run boons remain temporary: proposed Lingering Venom +25%Poison/Bleed/Shock; Restorative Rhythm60%casterATK shield after first cleanse/owner turn; Mirror Sigil15%actual directHP-loss reflect for first enemy root action after ownerTurnStart; Opening Plan+1startPG to the lowest-slot active player fighter only when the player takes their first turn second. Boon nodes/elite victories offer3 distinct unowned compatible choices. No recursive self-trigger. These starting values require tests for meaningful choices and readability.

<!-- @tag:ai -->
## Tactical AI and measurable intelligence

“Genius AI” means **demonstrable tactical competence under fair information**, not a promise of unbeatable or human-level general intelligence. The target opponent finds strong sequences, protects its win condition, responds to visible threats, and adjusts when its plan becomes invalid.

The AI receives its own hand, its own private state, public enemy units/statuses/PG, visible run modifiers, and observed action history. It cannot inspect the enemy hand/draft, future card queue, undisclosed server seed, or pending random outcomes. Candidate simulation uses expected values or independently sampled hidden possibilities. The live random stream is never advanced while searching.

| Competency | Required observable behavior |
| --- | --- |
| Lethal | Finds a legal immediate defeat when the curated fixture offers one |
| Survival | Saves a threatened key fighter when survival has higher expected value than superficial damage |
| Sequence | Cleanses Infect before healing; applies Ignite before Weakpoint; drains PG before an ultimate window |
| Hand economy | Considers moving/merging when its rank or PG value exceeds the lost play |
| Ultimate control | Drains a ready enemy when that denies a dangerous next turn; spends its own ultimate when delay risks denial |
| Target selection | Respects Guard; attacks the role that sustains the enemy plan; avoids overkill and useless damage into a shield |
| Reserve planning | Values both eligible reserve passives and the replacement fighter's entry role |
| Information fairness | Chooses identically for identical visible states when only the hidden enemy hand changes, given the same search seed and deterministic candidate/node budget with wall-clock stopping disabled |
| Explanation | Debug trace identifies a short reason such as lethal, prevent lethal, deny ultimate, or secure cleanse |

A deterministic rules-based baseline is implemented first. The stronger AI searches legal action sequences with pruning/beam search and evaluates probable opponent replies within T-AI limits. Evaluation considers win/loss first, then surviving roles, HP/shields, statuses, hand value, PG, and tempo. Its weights are content-versioned. A difficulty tier changes lookahead/budget and permissible deliberate mistakes; it does not grant secret stats. Production may stop search at a wall-clock budget, which can change the selected action under different load; the accepted plan is stored for replay. Determinism/fairness fixtures use fixed work budgets. Before labeling a tier Expert, it must pass the fixture suite and win-rate targets in T-AI against the baseline over balanced lineups.

<!-- @tag:tuning -->
## Tuning register — source values and starting hypotheses

Source numerical anchors: hand7; PG5; source rank3 and baseline stats; attribute±20%; effect-keyword coefficients. These are preserved, not claimed to be balanced. [Combat contract](../3_Outputs/Specs/fighting-allstar-combat-source-contract.md) and [character roster](../3_Outputs/Specs/CharacterData/character-roster.md) contain generation rules and conflict proposals.

| Key | Starting value / source | Microtest and adjustment |
| --- | --- | --- |
| T-CARDS / T-PG | Seven shared slots; max3 actions; PG5; play/move/merge1 | Full hand and drain fixtures; adjust generation scheduling if the player cannot explain readiness |
| T-TIME / T-PACE | Planning45s; cap40 owner turns; battle2–4min; run12–25min | Timed new-player run; simplify reading/playback before cutting thinking time |
| T-UI / T-DEVICE | Feedback≤100ms; Android30FPS / PC60FPS | Record target devices; 9/10 intended selections; enlarge targets before changing rules |
| T-DAMAGE / T-STATUS | Source-contract formulas, variance95–105%; default unspecified duration2, Ignite cap10 | Golden vectors and cap/control abuse tests; narrow variance/stack duration if prediction or agency fails |
| T-BASE / T-SKILL / T-CONST | Actual source stat/rank3 anchors; generated ranks and C0–C6 outside GDD | Compare action efficiency and C0 counter access; reduce generated progression if it erases counterplay |
| T-ATTR | Source cycle±20%; neutral1; Light/Darkness10% aura | Paired attribute swaps; report domination before proposing a source-rule change |
| T-CC | Sum authored Class_Combat; tie recorded random | Matched teams first-side win target45–55%; revise initiative if advantage persists |
| T-ECON | Confirmed160/1600 pulls;4%featuredSSR/36%SR/60%R;300-pull featured guarantee. Proposed selector milestone,320 base completion Diamonds and1600 one-time onboarding grant | Receipt, rate and boundary tests; measure runs/time per chosen featured; improve deterministic acquisition if restrictions stall |
| T-RUN / T-RECOVER / T-BOON | Proposed9-row floor with forward choices; persistentHP; Rest40%living heal OR30%single revive; difficulty0..100%; linked route specification | Validate all routes, no automatic heal, partial casualties and once-only Rest; tune rest/reward based on complete-run tests |
| T-AI | 20 forced-choice fixtures; ≥65% versus baseline over200 paired-side games; beam32; one enemy-turn lookahead; ≤250ms p95 server | Improve legality/heuristics before deeper search; record accepted plan for replay |
| T-META | 100 paired seeds per matchup; desired soft counter55–65%; avoid all-round team>60% | Confidence intervals plus human playtests; fix universal utility/initiative before blanket stat growth |
| T-REACTION | Safety128 effects/root action; no recursion | Boundary/cascade fixtures; simplify triggers before raising execution limit |

No gameplay benchmark or balance target has been measured in this documentation pass. Source rarity power differences must be measured separately from synergy with both actual-stat and normalized-stat test cohorts.

<!-- @tag:inspirations -->
## Inspirations and design boundaries

**Confirmed inspirations:** The Seven Deadly Sins: Grand Cross for the 3+1 card battle foundation; Genshin Impact for the idea of staged constellation-like late character progression. The user's requested title and Kyo94 starter are retained as project direction. This document does not assert ownership, availability, or final shipping rights for any character artwork or name; prototype content references remain replaceable assets.

Borrow the decisions: team roles, ranked cards, useful movement and merges, gauge pressure, reserve timing, and dramatic character actions. Make Fighting Allstar's own team systems, generated dungeon structure, and readable opponent planning explicit. A collection game should not require a rare pull to understand the tutorial or enter the only available dungeon. Production monetization, crossover content licensing, and live-service scheduling are later product decisions.

<!-- @tag:tech-stack -->
## Actual project stack and requested target

| Area | Observed repository state | Target / status |
| --- | --- | --- |
| Engine | Unity **6000.3.4f1** | Retain unless a separate migration is justified |
| Rendering | Built-in; GraphicsSettings custom pipeline unassigned | Stylized 3D arenas; no pipeline migration in this task |
| Input | Input System **1.17.0** and Both input handlers configured | Shared mobile/PC action model |
| UI | Existing uGUI / TextMeshPro scripts; uielements module present; no project UXML/USS found in inspection | **UI Toolkit** requested; new planning/menu presentation follows a migration plan |
| Tweening | LeanTween source in Assets/LeanTween | Requested “LeaTween” interpreted as **LeanTween** |
| Character data | ScriptableObject-based prototype | Authoring definitions exported into versioned runtime content |
| Services | No Firebase/Cloudflare battle authority implementation found in inspected code | **Firebase + Cloudflare** requested; service responsibilities in architecture plan |
| Battle host | No existing authoritative host verified | Proposed separate .NET/C# host, such as Cloud Run behind Cloudflare; hosting addition needs owner review |
| UnityMCP | Active project integration unverified | Requested development/editor tooling, not a runtime combat authority |
| Scenes | Five enabled existing scene files | Requested canonical MainMenu / Combat are planned replacements |

The architecture must account for desktop deployment constraints instead of assuming the Firebase Unity SDK alone provides a production Windows backend. Official service/platform evidence and host options are recorded in the architecture plan. Authentication, database, edge routing, and authoritative simulation are different responsibilities; specifying Firebase and Cloudflare does not by itself implement turn validation.

<!-- @tag:prototype-truth -->
## Current prototype truth

Observed by targeted source inspection on 2026-10-01; no Unity play session or build was run for this documentation task.

| Existing file | What is observed | Gap against this GDD |
| --- | --- | --- |
| `Assets/Script/Gameplay/BattleManager.cs` | Battle introduction, player card initialization, CC-related presentation | Does not establish symmetric server-owned 3+1 turn resolution |
| `Assets/Script/Gameplay/GameManager.cs` | Legacy individual-unit turns and random enemy skill choice | Conflicts with shared team card planning and tactical opponent search |
| `Assets/Script/Gameplay/TeamDataManager.cs` | Singleton local team transfer, scene spawning and reserve logic | Needs symmetric formation state and authoritative snapshots |
| `Assets/Script/Gameplay/Interfaces/IBattleDataProvider.cs` | Team retrieval through lists containing SO references | A useful seam, but insufficient for authoritative commands/events/reconnect |
| `Assets/Script/Gameplay/Unit.cs` | HP/PG/damage/death alongside animation/presentation behavior | Combat results and turn advancement depend on scene-level behavior |
| `Assets/Script/Character/CharacterObject.cs` | Character identity, visuals, stats, serialized progression in one SO | Separate immutable definition, player ownership, and battle instance |
| `Assets/Script/Card/CardDeckManager.cs` | Hand/action queue and reordering | Merge/reset rules and server acceptance not found in inspected code |
| `Assets/Script/Card/RuntimeCard.cs` | Runtime card representation | Stable authoritative identity and deterministic planning rules needed |
| `Assets/Script/Card/SkillCardSO.cs`, `UltimateCardSO.cs` | Card authoring assets | Typed, versioned effect model and complete rank/ultimate lifecycle needed |
| `Assets/Script/Inventory/PlayerCharacterRoster.cs` | Empty file | Persistent roster is a planned system |
| `Assets/Script/Inventory/InventoryObject.cs` | Local SO inventory list; name-based deduplication | Stable IDs, constellation material, and server persistence needed |
| `Assets/Script/Gacha/GachaManager.cs` | Local Unity random summoning | Authoritative wallet, transaction, and guarantee logic needed |

No project-owned test suite or project assembly definitions were found in the inspected paths. These statements are not a broad claim that every scene is broken; they establish what source inspection supports. Existing art, card prefabs, and useful visual effects can be adapted after dependencies are inspected. The first architecture milestone measures current behavior before replacing scene wiring.

<!-- @tag:scene-roles -->
## Canonical future scene roles

| Target scene | Responsibility |
| --- | --- |
| **MainMenu** | Authentication state, roster/build inspection, team selection, dungeon map between rooms, summon/result overlays, settings |
| **Combat** | Arena composition, both formations, card planning HUD, authoritative event playback, reconnect overlay, battle summary |

These are the two **Confirmed target names**, not existing scene files. Current enabled scenes are `Assets/Scenes/Scene-CharacterLoadOut.unity`, `Scene-CharacterProfile.unity`, `Scene-Gacha.unity`, `Scene-BattleGame.unity`, and `Scene-Test.unity`. Their consolidation is future implementation. Test fixtures may exist outside production scene selection; no additional canonical runtime scene is required for every menu panel or dungeon room.

<!-- @tag:system-ownership -->
## Canonical future system ownership

| System | Owns | Must not own |
| --- | --- | --- |
| Content catalog | Validated character, card, status, passive, dungeon, boon, and tuning definitions | Player HP, acquired copies, UI objects |
| Player profile / inventory | Owned fighter IDs, progression, currencies, tutorial flags, receipts | Mutable battle internals or client-declared wins |
| Loadout validator | Ownership, slot rules, eligibility, snapshot construction | Animation timing |
| Battle core | Deterministic state transitions, legality, RNG, effect queue, deaths, outcome | Unity GameObjects, Firebase calls, tween callbacks |
| Plan draft | Reversible local preview of a supplied turn snapshot | Durable authority or future random outcomes |
| Battle session service | Command authorization, concurrency, deadline, persistence, reconnect projection | Trusting client damage calculations |
| AI controller | Legal plans from permitted observations | Hidden enemy state or modifications to the live RNG stream |
| Dungeon service | Seeded forward route graph, room eligibility, persistent run state and completion | Generating arbitrary unvalidated effect code |
| Economy service | Idempotent reward/claim/summon/upgrade transactions | Using a reveal animation as proof of spending |
| Presentation | Models, camera, UI Toolkit, audio, animation, VFX from events | Deciding damage, death, or turn ownership |
| Local test backend | Same battle interface and rules core with test fixtures | Writing fabricated results into online accounts |

<!-- @tag:architecture -->
## Target architecture direction

Architecture serves reversible planning, deterministic battles, and clear online authority. The detailed contracts, migration plan, host options, and ownership boundaries live in [the first prototype architecture plan](../3_Outputs/Specs/fighting-allstar-prototype-arch-plan.md). This section supplies the design invariants that plan must preserve.

### Three separate forms of character data

| Data | Proposed fields | Lifetime |
| --- | --- | --- |
| **CharacterDefinition** | Stable character ID, display/localization keys, rarity label, attribute, traits/search tags, base stat block, two regular skill IDs, ultimate ID, passive IDs, presentation reference | Immutable within a content version |
| **OwnedCharacter** | Account ID, character ID, acquisition record, constellation level, duplicate crests, selected permanent progression | Persistent account data |
| **BattleFighterState** | Fighter instance ID, side/slot/reserve state, resolved stats, HP, shield, PG, status instances, cooldown/trigger counters, alive state | Mutable within one battle snapshot |

The owner's NoSQL/AST idea is retained as **typed declarative effect data**, not executable strings. Use stable IDs with explicit width/type rather than an 8-bit character ID that caps the catalog. Names are display data rather than database keys. Search metadata and trait references are indexed separately from combat execution.

A skill contains rank definitions and typed targeting/effect lists. A passive contains a declared scope, trigger, bounded conditions, and typed consequences. “Card type” selects UI/category constraints, while the effect schema describes behavior; do not make arbitrary JSON fields appear depending on a string value with no validator. An ultimate reads its progression definition, not an integer field on a shared mutable asset. Unsupported conditions/effects are rejected at import and on version mismatch.

### Authority and determinism contract

Both local AI tests and online clients use a battle-session interface with create, snapshot, submit-plan, resume/events, and result operations. Swapping only TeamData is insufficient: online play must move all command validation, RNG, and outcome creation to the authority. A client receives a redacted view appropriate to its account; an AI receives the same information boundary as a player controlling that side.

The core should run without Unity scene objects. Content exports, commands, and event logs are versioned. RNG seeds are server-owned and replayable but not exposed during a live match. Animation events present recorded impact times and never call authoritative damage logic. Committing a plan produces an atomic state revision and ordered events. Duplicate commands return their prior result. Concurrency, reward claims, summons, and constellation upgrades require transaction identifiers and conflict handling.

The local mode is a development/practice mode with separate storage. It can grant test fighters and change seeds but cannot claim online Diamonds. The final prototype is not called server authoritative until an online PvE run and a minimal private two-client match pass the security and reconnect acceptance tests. Public matchmaking, ranking, guilds, and social systems are later scope.

<!-- @tag:guardrails -->
## Contributor guardrails and player experience checks

### Non-negotiable user requirements

- Fighting Allstar; 3D tactical turn based cards; mobile/PC and PC/APK outputs.
- Full 3+1 formations, team synergy, multiple meta triangles, and strong tactical opponents.
- Starter Kyo94, dungeons, victory Diamonds, character gacha, and persistent ownership on loss.
- Restricted dungeons that give roster breadth value; ultimate/constellation progression ends at C6/6.
- MainMenu and Combat target scenes; Unity, UI Toolkit, LeanTween, UnityMCP tooling, Firebase, Cloudflare.
- Card movement/merge/reset, PG/ultimates, statuses, critical/block, additional damage, lifesteal, counters/follow-ups, DOT, and reserve-aware passive registration.
- Online outcomes derive from server execution; local AI tests can swap backend without changing combat rules.

### Five-component evaluation

| Component | Required player experience | Prototype check |
| --- | --- | --- |
| Clarity | Predict legal targets, action cost, status expiry, and why a reaction happened | Observe a turn without explanation; tester recounts the decisive effect |
| Motivation | Victory opens roster choices; failure suggests a revised plan | Tester selects a different team/card sequence after a loss rather than only seeking higher stats |
| Response | Draft selection/movement/reset is immediate and reliable | Touch stress test and reconnect recovery preserve intentional choices |
| Satisfaction | A planned merge, cleanse, or coordinated hit reads as earned success | Visual and audio feedback agree; log confirms the tactic caused the outcome |
| Fit | Fighters express their role through cards, stance, and animation | Kyo visibly responds to Ignite setup; heal, dispel and drain have distinct cues |

When these conflict, prioritize **Response → Clarity → Satisfaction → Fit → Motivation** for the interaction under review. Investigate unclear targeting and unresponsive planning before changing damage or reward values.

### Risks and abuse cases

| Risk | Required design response |
| --- | --- |
| Reset/move PG farming | Draft never writes authority; move cost and no-op validation; same snapshot on reset |
| Roster restriction softlock | Permanent unrestricted route plus guaranteed onboarding four and visible acquisition path |
| A single universal team | Orthogonal roles/attributes, explicit opportunity costs, mirrored matchup testing |
| Reaction explosion | Bounded triggers, lineage filters, fail-closed transaction error, readable queue |
| Hidden AI cheating | Observation projection tests and hidden-state permutation fixtures |
| Disconnect/reroll exploitation | Same run/battle/seed, persisted deadline and idempotent commands |
| Client reward or gacha fabrication | Server transaction receipts; test backend has no online reward authority |
| Low frame rate changes outcomes | Fixed rules core and event-driven presentation; playback can catch up |
| Prototype scope expands into live service | Complete small content loop first; no paid banners/ranked/guild dependency |

### Playtest script and review checkpoint

1. **New player:** start with Kyo94, play a safe card, perform a guided merge, inspect PG, acquire guaranteed allies, assemble 3+1, and finish Open Circuit. Check whether the player can explain reserve availability and why a move used an action.
2. **Stress:** rapidly tap cards/targets/reset, resize UI, reconnect during planning and playback, exhaust legal actions, kill a queued actor, and fill the shared hand. Confirm no duplicated command, lost selection, or unreadable state.
3. **Skill:** repeat an equal-stat encounter with the same visible opening. The second attempt should improve through sequencing, targeting, or team choice. Compare deliberate merge and immediate-play alternatives.
4. **Abuse:** retry a winning claim, summon, and upgrade; replay stale commands; alter client stats; reconnect to seek a different draw; repeatedly trigger reflect. Expected behavior is stable results and explicit rejections.
5. **Readability:** a fresh observer watches a counter, shield, lifesteal, DOT death, and reserve entry, then identifies cause and order from the HUD/log without developer narration.

These are planned tests, not completed playtest results. Human review is required for design feel after a playable slice exists. Record device, team, seed, content version, failed expectation, and observed event sequence. Tune in this order: interaction reliability, target/status clarity, sequence correctness, tactical opportunity costs, team matchups, progression pace, presentation polish.

<!-- @tag:roadmap -->
## First prototype scope and phased roadmap

The prototype is a complete small version of the loop, not the full collection/live-service game. Content counts and milestones below are **proposed starting scope**. They may be cut only with an explicit GDD revision that preserves a playable end-to-end result and the online acceptance gate.

| Phase | Player-visible outcome | Completion evidence |
| --- | --- | --- |
| **P0-A — Rules and portability** | A reproducible full 3+1 test battle with four distinct archetypes available on both sides | Headless replay/legality tests, versioned data and early Windows/Android core parity |
| **P0-B — Playable card battle** | Touch/mouse plan, move, merge, reset, confirm, PG, ultimate, reserve entry; readable 3D playback and a legal baseline AI | MainMenu/Combat wiring, local backend and manual combat checklist |
| **P0-C — Dungeon and roster loop** | Eight kits, Kyo tutorial, guaranteed allies, three linear dungeon profiles, randomized character teams, four reusable boons, first-clear selector, Diamonds, gacha, C0–C6 | Fresh profile completes dungeon-to-summon-to-revised-team loop; selector unlocks Women Exhibition; loss retains holdings; generation and reward fixtures pass |
| **P0-D — Online authority** | Firebase third-party login, online PvE, reconnect and private-room two-client 3+1 battle on PC/APK | Trusted outcomes, private projections, duplicate-request/settlement tests and restart recovery |
| **P0-E — AI and delivery acceptance** | Expert search, demonstrated team counterplay, finished prototype UI and tested Windows/APK artifacts | Tactical/fairness fixtures, paired matchup report, measured search/device budgets and owner game-feel review |

Final prototype acceptance requires all phases. Local-only success is a useful milestone, not proof of online completion. The private-room match verifies both player seats and symmetric authority; public matchmaking, ladders, monetized gacha, anti-cheat operations at scale, and season balancing are later releases.

### Full-game expansion after the prototype

Expand the roster and arena library after the eight-fighter matchup report demonstrates several viable plans. Add longer stage sequences, stronger character-team challenges, new restrictions, more progression choices, collection catch-up, and cosmetic identity. Evaluate production account operations, content delivery, availability, live balance, matchmaking, and monetization using measured player behavior. The full vision needs a separate production plan, content budget, and service targets.

### Ready for review, not yet implemented

This GDD defines the requested battle lifecycle, a finite prototype roster, C0–C6 progression, linear generated character-team encounters, fair tactical AI, and acceptance criteria. Review priorities are the300-pull guarantee policy, Rest/reward starting values, detailed battle states and phaseA implementation ticket. Architecture changes should make those choices reproducible and understandable to the player.
