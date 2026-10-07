---
slug: fighting-allstar-prototype
status: needs-human
gdd_tags: [mechanics, core-loop, items, enemies, roster, dungeons, ai, tuning, platform-input, architecture, roadmap]
owner: qa-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Fighting Allstar — First Prototype Test Plan

**Planning status:** These are proposed acceptance tests for the architecture draft. No gameplay tests, builds, security tests, network tests, device profiling, or benchmarks have been executed for this documentation task. Passing this document review does not imply that the prototype passes its tests.

**Design authority:** [GDD](../../1_Inputs_Templates/GDD_Fighting_Allstar.md), especially `@tag:mechanics`, `@tag:core-loop`, `@tag:items`, `@tag:enemies`, `@tag:platform-input`, and `@tag:roadmap`.

**Technical authority:** [Prototype architecture](../Specs/fighting-allstar-prototype-arch-plan.md), [task card](../Specs/fighting-allstar-prototype-task-card.md), and [combat ADR](../ADRs/001-authoritative-combat-core.md). If a proposed rule changes, update its fixtures and expected trace before implementing it; the test plan must not become a competing source of mechanics.

## 1. Test strategy and evidence

| Test area | Priority | Planned method | Platforms |
|---|---|---|---|
| Card planning, combat state, triggers, death, reserve, and RNG | P0 | Pure domain unit/property tests and replay fixtures | Unity test runner and standalone server runner |
| Server authority, privacy, retries, reconnect, deadlines, and settlement | P0 | Integration tests with fault injection | Server plus Windows and Android clients |
| Dungeon eligibility, generated run validity, persistent roster and rewards | P0 | Content validator, seeded generator tests, end-to-end runs | Server, Windows, Android |
| AI legal actions and hidden information boundary | P0 | Observation-contract and deterministic decision tests | Local and online authority |
| Tactical AI, synergy, and counterplay | P1 | Curated scenarios, paired seeded matches, observed playtests | Local harness, Windows, Android |
| MainMenu/Combat flow, input, readability, presentation, performance | P1 | PlayMode and manual device tests, profiler captures | Windows build and Android APK |

P0 failures include incorrect battle outcomes, impossible progression, hidden information exposure, invalid authority transitions, duplicate rewards, corrupted ownership, and crashes. P1 failures include required prototype feature gaps, unreadable combat feedback, input failures, and failure to meet agreed performance targets. Cosmetic polish outside the prototype's accepted scope does not hold a gate.

Each executed case must record: case ID; build and commit; platform/device/OS; content/rules version and hash; seed; initial state; command IDs and turn revisions; expected and actual results; event trace; state hash at each completed action/turn; and pass/fail. Keep the full authoritative replay in restricted test storage; player-visible logs must preserve private-hand boundaries.

## 2. Finite acceptance gates

| Gate | Required evidence | Exit condition |
|---|---|---|
| A — Core combat | Rule fixtures and deterministic replay suite for both sides | All P0 combat cases pass; no divergent hash, illegal hand, duplicated actor/card, or unbounded reaction chain |
| B — Complete local run | Starter onboarding, dungeon restrictions, generated encounters, AI, loss/win settlement simulation | A repeatable MainMenu → dungeon → Combat → result → MainMenu loop preserves the roster and grants the specified win reward once |
| C — Authoritative online prototype | Real authenticated server flow, durable sessions and settlement, two-client combat, fault tests | Server owns rules and progression; all online P0 cases pass on Windows and Android; local completion alone cannot satisfy this gate |
| D — Prototype acceptance | Eight-fighter roster, three dungeon profiles, required synergy and AI cases, platform matrix | All scoped P0/P1 cases pass or a specific P1 waiver is accepted by the owner; no unresolved P0; gameplay feel receives owner review |

Recommended sample sizes in this plan are **starting acceptance targets**, not measured reliability or balance claims. They may be adjusted before execution when device class and test infrastructure are chosen. Record the final agreed sample sizes alongside results.

## 3. Combat and content fixtures

GDD references: `@tag:mechanics` and the linked actual-source combat contract, `@tag:roster`, and the T-CARDS/T-PG/T-DAMAGE/T-STATUS rows in `@tag:tuning`. Each case is P0. Execute each side-sensitive fixture once with either team owning the turn. Use different fighter and card instance IDs even when their display names match. Fixture coefficients and RNG outcomes must be pinned; do not rely on an incidental random crit to make an expected result pass.

| ID | Given | When | Then |
|---|---|---|---|
| C01 — Setup and first turn | Both sides have three living active fighters with two skills and one living reserve | Create a battle, including a fixture where the seventh opening card merges | Each side begins with the seeded random card on the left followed by the six known rank-1 cards ordered Position 1, Position 2, Position 3; reserve skills are absent; every opening merge grants its owner 1 PG and emits that updated value; the recorded CC result/seeded tie break determines the first side |
| C02 — Draw once, merge once | A regular hand has five distinct cards, with the next two recorded draws being two equal rank-1 A cards that match no existing card | Resolve owner TurnStart | Exactly two cards are drawn, then merge to A rank 2; the final hand has six cards; no extra draw fills the merge gap; the other side's hand/RNG counter is unchanged |
| C03 — Paid move and PG | A stable hand is `[A1, X1, A1]`, A's PG is below the cap, and two actions remain | Move the right A beside the left A, then play the deterministic merged result ID | The move spends one action; A gains one movement PG and one merge PG, capped at five; the merged A2 is playable in the next action; consumed IDs cannot be reused |
| C04 — Merge chain | A stable hand is `[A1, X1, A1, A2]` and X belongs to a different fighter | Move X to the end | Left-to-right merge/restart creates A2 then A3 at the left position; X gains one move PG and A gains two merge PG; only one action is spent; the result is identical on replay |
| C05 — Play closes a gap | The C04 hand is stable and the X play has no disruptive effect | Play X, then queue the merged A result | Card removal closes the gap and creates the same A3 chain; X gets only its successful regular-play PG, A receives actual merge PG, and later actions reference the stable resulting card ID |
| C06 — Illegal transforms | The hand includes equal labels from different owners, two rank-3 copies, and a pending ultimate | Attempt cross-owner merging, rank-4 merging, moving an ultimate, out-of-range movement, and a no-op movement | These transformations are rejected without spending actions, changing PG, or advancing RNG; legal adjacent same-owner/skill/rank merges still work |
| C07 — Reset is exact | A saved planning projection has an ordered hand, PG, pending ultimate, and three free actions | Preview multiple moves/merges/plays, inspect damage ranges, then Reset; repeat 20 times and commit the original plan | Every reset restores card identities/order/ranks, PG, targets, pending cards, and budget to the snapshot; authoritative state and all live RNG counters remain unchanged until commit; no future roll is revealed |
| C08 — Frozen action budget | Test one, two, and three living active fighters at owner TurnStart; a reserve is alive | During a committed turn defeat an actor and deploy the reserve at the safe boundary | Budgets start at two/two/three and remain frozen; dead actor actions fizzle without PG/effect; reserve entry adds no actions or immediate skill cards; only future draws include it |
| C09 — Target revalidation | Earlier actions kill a selected enemy or ally; an enemy taunt exists | Begin later queued actions | Hostile target retargets lowest valid slot respecting taunt; invalid friendly target fizzles; no eligible hostile target means fizzle |
| C10 — Ultimate delivery and capacity | Ready fighter at PG5, seven occupied shared slots | Advance owner TurnStart, then repeat with one empty slot | Full hand retains readiness without an eighth card; with space create exactly one ultimate before regular refill; repeated checks cannot duplicate it |
| C11 — Ultimate drain and use | Pending ultimate at PG5 | Drain, recharge, advance turn, execute; separately apply Attack disable and Stun | Drain removes pending card; recharge cannot generate midturn; valid ultimate consumes card/PG; Attack disable leaves ultimate usable, Stun prevents its play; death removes all owner cards/PG |
| C12 — Damage vectors | Pinned source-contract inputs and outcome rolls | Run all six family and crit/block/keyword fixtures | Match source-contract golden vectors, exclusive crit/block, bypass/cap/shield flags; zero damage cannot heal; reductions never reprocess aggregated packets |
| C13 — Lifesteal, reflect, and death | A damaged attacker hits a low-HP defender with a shield and bounded reflect | Resolve the damage batch | Lifesteal uses actual direct HP removed, excludes shield absorption/overkill, applies RecoveryRate, and caps at missing HP; the GDD lifesteal/additional/reflect order decides the subsequent death checkpoint; animation speed never changes who survives |
| C14 — Status clocks and sources | Poison stacks snapshot actual Normal HP loss; Ignite has no tick | Change ATK, kill source, cleanse and advance both owner clocks | Poison keeps stored magnitude; owner-turn application does not tick immediately; enemy-turn application ticks next target end; Ignite never generates periodic HP damage |
| C15 — Trigger trace and scope | ActiveOnly, ReserveOnly, TeamRoster, and explicit OnDeath passives have equal-priority competing triggers | Resolve setup, a normal hit, a cleanse/heal, a death, and reserve entry | Only applicable scopes activate; non-death passive evaluation stops on death; the trace follows the canonical priority/side/slot/source/sequence ordering; once-per-action/turn rules reset only on their defined clock |
| C16 — Reactions are bounded | Both teams have retaliation, follow-up, and reflect opportunities | Execute a root attack, then a deliberately malformed chain exceeding the content/runtime budget | Legal counters/follow-ups occur at PostAction only for surviving valid sources and grant no normal play PG; reflect cannot reflect itself; generic retaliation cannot recurse; invalid content is rejected or the uncommitted transaction rolls back with an error, never a fabricated win |
| C17 — Simultaneous deaths and reserve | An area/reflect batch can kill several active fighters on both sides; vary reserve survival on each side | Resolve the root action and all its queued effects | Simultaneous batch health changes apply before death decisions; owned cards/ultimates are removed; each eligible reserve enters the lowest vacant slot only after the queue drains; both entries occur before win/loss/draw evaluation |
| C18 — Presentation hit split | One authoritative card result, displayed as three animation hits | Skip, interrupt or accelerate presentation | Visual portions sum to the same damage; no repeated DEF subtraction or authoritative interruption from animation. Explicit mechanical multi-hit remains separate future content requiring its own contract |
| C19 — End of turn and terminal state | DOT can kill both formations, an end trigger can queue a legal reaction, and a separate fixture reaches the battle cap | Resolve TurnEnd | DOT batch, death handling, eligible end effects/reactions, expiry, reserve entry, and outcome follow the GDD order; a draw/cap failure grants no dungeon victory reward and preserves ownership; terminal battles reject new actions |
| C20 — Catalog and legacy migration | The eight-fighter catalog plus legacy Kyo rank indices 0/1/2 and a serialized potency of 180 | Export into canonical ranks 1/2/3 and basis-point coefficients; submit missing IDs, duplicate ranks, cycles, unknown nodes, impossible hit counts, overflow, or out-of-range progression | The importer maps each source convention explicitly and produces reviewed golden vectors; it never guesses whether 180 is 180%, 180×, or 1.8; invalid data fails before battle; all C0–C6 records resolve |
| C21 — Replay parity | The same pinned catalog, setup, RNG key/streams, and committed command log | Replay 100 fixed seeds for both side assignments on standalone server, Windows, and Android core runners; vary playback frame rate/skip/fast-forward | Every event/state hash at completed actions and turns agrees; both private hands, reserve state, gauges, durations, and RNG counters agree internally; recipient hashes reveal only authorized projections |
| C22 — Death merge consumes a queued ID | A legal plan names an existing A1 card for a later action; another fighter's intervening card separates two A1 copies | The earlier action kills that intervening fighter and its card removal merges both A1 copies | Cleanup creates one stable A2 and grants one merge PG; the later action naming the consumed A1 fizzles without substituting A2, extra PG, or a hidden automatic rank upgrade |
| C23 — PreAction interruption | A regular card is initially legal but a PreAction effect can disable its living actor | Resolve the PreAction window, then compare to a successful regular attack later interrupted by reflect | A PreAction fizzle receives no ordinary play PG; successful PreAction grants play PG before card effects; subsequent interruption does not retroactively undo that grant, although death cleanup removes the dead fighter's gauge |

C12 uses the [actual-source damage vectors](../Specs/fighting-allstar-combat-source-contract.md): ordinary195; crit with total1.5 minus CritDefense.15 gives263; failed crit plus25% block gives146. Never expect crit+block together. The Pierce/Resistance distinguishing vector is280 (not the old255).

For C13, a263 hit against shield100 and HP80 removes80 HP. Lifesteal50%, total Recovery100%, casterHP5 heals40 before reflect15% of80 removes12, leaving33. Overkill and shield do not produce lifesteal.

C21 is a proposed 200-run corpus per runtime; seeded coverage supplements the named boundary fixtures and does not replace them.

## 4. Dungeon, roster, and progression cases

GDD references: `@tag:core-loop`, `@tag:items`, `@tag:enemies`, and `@tag:roadmap`. All cases are P0 except the marked balance case.

| ID | Given | When | Then |
|---|---|---|---|
| D01 — Fresh-account solvability | Kyo94 C0 account | Complete tutorial, claim Chin94/Kensou94/King94, clear Open Circuit, select Mai94 or Athena94 | Starter enters Open Circuit and Green Accord; first-clear selection supplies second Women fighter for Women Exhibition; no luck or circular dungeon prerequisite |
| D02 — Restriction truth | One eligible and one ineligible loadout per dungeon profile, including an invalid reserve | Preview entry in MainMenu, submit the loadout, and tamper with the same request | UI and server agree on the exact restriction reason; every required active/reserve slot is checked; invalid teams cannot enter by bypassing the menu |
| D03 — Generated stage validity | Three pinned dungeon profiles, an eligible roster, and 100 fixed seeds per profile | Generate each linear run twice and validate every stage and its opposing 3+1 character team | The same inputs produce the same ordered stages/opponents/rewards; every stage uses legal fighters from the shared catalog and their ordinary skills/passives; forward choices are validated, no backward edge or invented monster-only rule appears; the final stage is reachable within the finite stage count; illegal generation selects the documented deterministic fallback |
| D04 — Loss persistence | A populated owned roster and inventory plus an in-progress run with temporary rewards/modifiers | Lose by combat, exhaust the run's valid continuation, or forfeit under the documented policy | Owned fighters and permanent upgrades persist; only run state changes as specified; no victory Diamonds appear; returning to MainMenu remains possible |
| D05 — Victory and acquisition | A legal completed dungeon win, its earned Diamond reward, and a legal gacha request | Settle the win, perform a pull, reload, and retry both requests | Currency and the resulting owned fighter/duplicate conversion persist once; the pull result is stable across retries and cannot reroll after disconnect |
| D06 — Constellation boundaries | Fighters at each of the seven displayed states C0/6, C1/6, C2/6, C3/6, C4/6, C5/6, and C6/6 | Apply six legal upgrades from C0, retry each conversion, then acquire a duplicate at C6 or request C7 | Exactly six upgrades reach C6/6; cumulative modifiers and source per-tier riders apply once; the cap cannot overflow; capped duplicates give the specified Collection Token; no owned fighter disappears |
| D07 / P1 — Restriction value | Equal-power accounts with a narrow and broad roster drawn from the eight prototype fighters | Compare entry choices across all three profiles | Roster breadth creates the documented team-building choices; entry requirements and rewards are visible before commitment; the starter's guaranteed progression route is preserved |
| D08 — First-clear selector authority | An account has just completed its first Open Circuit win and another has already claimed that first-clear reward | Retry settlement under new request IDs, complete another run, submit concurrent selector claims, and request an already-owned or noncatalog selection | The first-clear entitlement is unique per account/profile and cannot be farmed through new run IDs; one legal unowned fighter is granted once; invalid selections do not consume the entitlement; any unspent permanent selector survives later loss |

D03 samples 300 seeds in total as a starting acceptance target. This checks structural validity, repeatability, and content references; it does not prove that every seed is strategically balanced or winnable. Curated fresh-account and counterplay fixtures supply that separate evidence.

Stage transitions now persist exact remainingHP, including0; no automatic heal/revive. Reset hand/PG/combat statuses, retain boons; reordering cannot import an external fighter. Partial casualties permit reduced active teams. Rest once either heals living40%runMaxHP or revives one30%. Final boss victory closes the run; full wipe/draw ends without completion payout. Chosen/bypassed node state and immutable enemy snapshots survive reconnect.

For D08, an already-owned selection is rejected while unowned pool members remain. When the whole pool is owned, one selected fighter's crest is the valid fallback; at C6 it converts to one Collection Token. Verify the fallback and entitlement consumption in the same transaction. For D06, ultimate magnitude multipliers at C0 through C6 are 1.00, 1.05, 1.10, 1.15, 1.20, 1.25 and 1.30; discrete values do not scale fractionally; Kyo Ignite count is2+tier and Robert critical-chance buff is7+7*tier percentage points. Kensou primary HP heal is50% at C0 and65% at C6.

## 5. AI and multiple meta triangle cases

GDD references: `@tag:ai`, `@tag:roster`, `@tag:mechanics` attribute and meta rules, and T-AI/T-META in `@tag:tuning`. These targets require implementation and measured results before they can support an Expert AI label.

| ID / Priority | Given | When | Then |
|---|---|---|---|
| A01 / P0 — Legal authority path | Normal, full-hand, sealed, one-survivor, pending-ultimate, and expensive-reaction states | Ask every AI tier to select a plan, including a search-budget exhaustion case | Every output validates under the same card/action/target rules as a player; a legal baseline/pass fallback exists; no private rule override or extra action is granted |
| A02 / P0 — Information fairness | Identical AI observation/history/search seed, but permuted hidden enemy hands and future combat streams | Run the planner with the same fixed candidate budget and wall-clock guard disabled or sufficiently ample for the fixture | Decisions and evaluated scores are identical; live RNG counters remain unchanged; no hidden card ID, future roll, live key, or player draft is reachable through the observation object |
| A03 / P1 — Twenty tactical fixtures | Two fixtures each: lethal, survival, cleanse-before-heal, Ignite-before-Weakpoint, Rupture versus buff, merge economy, PG denial, taunt targeting, reserve support, counter avoidance | Evaluate pinned baseline/Expert plans | Expert meets documented tactical objectives and legality; retain failed fixtures, show concise decision reasons |
| A04 / P1 — Expert versus baseline | Equal builds/progression, pinned lineups/seeds, identical information, and both initiative assignments | Run 100 paired-seed lineup trials with sides swapped, totaling 200 matches | Expert meets the GDD starting target of at least 65% wins; report wins/losses/draws, side split, elapsed time, and confidence interval; a draw is not silently counted as a win |
| A05 / P1 — Search budget | The twenty fixtures and an adversarial full hand with all legal targets, merge choices, and reactions | Measure on the named reference server tier | Search honors the architecture's starting bound of 2,000 candidates or 250 ms and T-AI's p95 goal; the selected command is persisted, so replay does not rerun a potentially different timed search |
| M01 / P1 — Counter triangles | C0 equal-progression builds, with strategy, effect counters, and attribute varied separately before combining them | Run 100 paired-seed trials per directed strategy matchup, totaling 200 matches per matchup, then the approved mixed-team set | Intended strategy counters are assessed against the proposed 55–65% range, any lineup over 60% overall is flagged, and matched-team first-side wins are checked against 45–55%; publish sample sizes and uncertainty rather than declaring universal balance |
| M02 / P1 — Human counterplay | Players can inspect skills, enabled reserve passives, and all visible statuses/modifiers | Play the guaranteed team plus Burst, Sustain, and Attrition/control fixtures, including unfavorable attribute and effect-counter matchups | Testers can identify the threat, a costly response, and the consequence of their sequence; essential cleanse/guard/anti-stall answers work at C0; no single acquisition or C6 upgrade is needed to understand or answer the teaching encounter |

For M01, use the GDD's full proposed roster and preserve a baseline snapshot of each team. A perfect mirror with CC tied tests initiative/arithmetic bias; it cannot by itself establish a healthy metagame. Numerical ranges are diagnostic starting gates and require human interpretation alongside M02.

## 6. Online authority and persistence cases

These are P0. Run the mutation cases against authenticated test accounts and an isolated test environment. A rejection must leave the authoritative state and RNG position unchanged.

| ID | Given | When | Then |
|---|---|---|---|
| N01 — Identity and ownership | Two accounts, separate rosters, and one active session per account | An account submits another account's session, fighter instance, inventory item, or reconnect token; repeat with absent/expired credentials | The server rejects unauthorized access and mutation; neither account's private hand or inventory is disclosed |
| N02 — Forged combat inputs | A valid battle snapshot and turn revision | Submit edited ATK/HP/gauge/CC, an invented or enemy-owned card, a dead actor, a reserve attack, an illegal target, extra actions, altered rules hash, or client-selected combat RNG | The server derives trusted values, validates the entire submitted plan, and rejects illegal requests without partially applying them |
| N03 — Replay and conflict | A valid accepted plan with an idempotency key | Retry the same request before/after its acknowledgement; reuse the key with a changed payload; submit the prior turn's revision | An identical retry returns the same accepted result once; conflicting reuse and stale revisions are rejected; no extra turn, damage, draw, or gauge occurs |
| N04 — Concurrent commitment | Two connections for the same player see the same turn revision | Both commit different plans concurrently | At most one plan wins the authoritative compare-and-commit; the other receives the current revision and a recoverable rejection |
| N05 — Deadline race | A turn has a server deadline and the plan/timeout compete for the same state revision | Attempt acceptance immediately before, exactly at, and after the deadline; also let an early HTTP request stall until its successful compare-and-swap attempt occurs after the deadline | Only a successful transaction attempt with trusted `acceptedAt < deadline` accepts the plan; exactly-at/late attempts reject; early network arrival alone grants no exception; at most one plan or TimeoutPass commits and reconnect/device time cannot extend it |
| N06 — Reconnect windows | A battle is at each of planning, accepted commitment, action presentation, and settlement | Drop connectivity, terminate the client, reconnect, then resend the last request | The server's revision and private projection restore the battle; an accepted action is never replayed as a new command; local animation progress cannot change the outcome |
| N07 — Privacy on the wire | Two clients and an AI share a battle with distinct private hands | Inspect snapshots, reconnect payloads, logs delivered to clients, errors, and AI observation objects | Each recipient receives only the information authorized by the GDD/architecture; hidden opponent card IDs, future draws, and secret RNG state are absent |
| N08 — Durable battle recovery | A recorded initial state, accepted command, and persisted checkpoint/event boundary | Terminate the server before commit, after commit before reply, and during subsequent event delivery | Recovery follows the documented persistence boundary; accepted work appears exactly once, unaccepted work can be safely retried, and state hashes agree with the uninterrupted replay |
| N09 — Win settlement fault matrix | A legitimate final encounter win and its immutable reward identifier | Fail before creating settlement intent, after intent but before inventory transaction, after transaction before acknowledgement, and during retry | The durable process eventually grants the exact authorized reward once; a committed win cannot be lost to acknowledgement failure; client repetition cannot grant twice |
| N10 — Economy concurrency | An account has enough Diamonds for one pull and receives one earned reward | Concurrently request two pulls, retry the reward, reconnect from another device, and repeat a duplicate-character conversion | Transactions preserve a nonnegative balance and one outcome per request; no duplicated grant, lost owned fighter, or excess constellation level results |
| N11 — Run and content validation | A run is pinned to a content version and an eligible locked loadout | Alter fighter IDs, restriction tags, level/constellation, room index, encounter result, or content version between requests | The server validates persisted run state and content; it rejects unearned progression or incompatible content; a client cannot reroll an active encounter by reconnecting |
| N12 — Third-party authentication | The approved third-party provider is configured for the Android and Windows adapters | Sign in, cancel sign-in, expire/refresh credentials, restart, and sign in to the same linked account on the other platform; replay or substitute another browser callback | Firebase resolves the intended UID consistently; canceled/expired or unbound callbacks do not create an authorized session; provider credentials and refresh material stay out of logs/build assets; account switching cannot expose the previous account's hand/roster |

N08 and N09 need controlled failure points in the eventual service implementation. A unit test of the retry function alone is insufficient evidence for process termination and recovery behavior.

## 7. Platform and presentation cases

| ID / Priority | Given | When | Then |
|---|---|---|---|
| U01 / P1 — Scene loop | A clean installation and an authenticated test account | Complete onboarding and enter/exit Combat through win, loss, reconnect, and return to MainMenu | There is one active session/presenter; no previous hand, queued action, fighter model, tooltip, or event subscription leaks into the next battle |
| U02 / P1 — Equivalent input | The same hand and legal plan on Windows and Android | Select cards, reorder/move, inspect ranks/statuses, change targets, reset, and commit using mouse versus touch | Both inputs produce the same legal command plan; scroll/drag/tap conflicts do not spend an action or commit accidentally |
| U03 / P0 — Rapid and interrupted input | A planning phase, pending commit, and imminent deadline | Double-tap commit, drag while target changes, rotate/background the device if supported, or disconnect during transition | No duplicated command or UI exception occurs; inputs after commitment cannot mutate accepted state; recovery shows the authority's current phase |
| U04 / P1 — Event playback | A multi-hit attack with crit/block, reflect, healing, additional damage, death, and a reaction in its approved trace | Play at normal speed, fast speed, low frame rate, skip/reconnect, and with one cosmetic asset missing | Presentation consumes the same resolved events; HP/gauge/outcome agree at every reconciliation point; missing visuals have a readable fallback |
| U05 / P1 — Readability | All prototype card/status types and three attributes appear | Inspect them at supported resolutions, safe areas, and minimum planned screen size | Text, ranks, costs, targets, status duration, restrictions, and gauge are readable; color is accompanied by symbol/text; the action preview explains rejected moves |
| U06 / P0 — Shippable test builds | Approved Windows and Android build configurations and pinned content | Install fresh, launch, authenticate, complete a run, close, reopen, and reconnect | Both the Windows executable and physical-device APK complete the authoritative flow; Editor success alone is insufficient; missing service/configuration reports a recoverable error |

Required starting device matrix: one Windows PC; one agreed Android device at the minimum supported class; and one representative Android device above that class. Device models, OS/API levels, graphics API, connection quality, and thermal conditions belong in the execution record. The plan does not claim a supported minimum device until those choices are made and tested.

## 8. Performance measurement plan

Use release-equivalent builds for end-user performance and a separate development/profiler capture to locate costs. Measure a five-minute representative battle loop after warm-up and a 30-minute repeated MainMenu/Combat session for retained-object/memory growth. Include one maximum reaction fixture and a full hand with status tooltips. Capture p95 frame time, hitch counts, total memory, scene load time, allocations, AI decision time, server turn resolution time, and request/reconnect latency.

The following are initial project policy targets, **not measured results**: stable 30 FPS on the chosen minimum Android device and 60 FPS on the chosen Windows PC; scene load under 3 seconds on Android and 2 seconds on PC; total memory below 512 MB on Android and 1 GB on PC; draw calls below 50/100 respectively; and 0 bytes of routine per-frame managed allocation during a warmed-up idle/planning frame. Measure discrete content loading, turn resolution, and UI refresh allocations separately rather than concealing them in the frame average. Any change to these targets must be documented with measured evidence and owner agreement.

Network and AI performance gates use the architecture's eventual pinned budgets. A local test runner's fast execution does not establish hosted service latency or phone performance.

## 9. Regression and bug reporting

After a rule/content change, rerun the directly affected fixtures and replay their recorded seeds. After an authority/protocol change, rerun N01–N12 and one complete run on each platform. After a presentation/input change, rerun U01–U06 plus a replay hash comparison. Escalate to the full matrix only when the changed dependency, failure, or required gate warrants it.

Regression focus from the inspected legacy code: clearing the action queue between battles; cancellation preserving cards; hand/refill isolation for both teams; removal of gameplay use of `UnityEngine.Random`; reserve-owned passives and cards; and animation-independent HP/death resolution. These are risk areas identified by source inspection, not test results.

```text
BUG: concise observable failure
CASE / PRIORITY: case ID and P0/P1
BUILD / CONTENT: commit, build, rules version/hash
PLATFORM: device, OS, graphics API, network condition
REPRO: initial fixture or account/run, seed, exact commands and timing
EXPECTED: linked GDD tag/rule and expected event/state
ACTUAL: observed event/state and first divergent sequence/hash
FREQUENCY: failures / attempts
EVIDENCE: restricted replay, relevant logs, screenshot/video
IMPACT: battle correctness, progression, privacy, crash, or usability
```

Do not put credentials, service keys, or unrelated player data in bug reports. Attach only the test records needed to reproduce the failure.


## 10. Source-data authoring validation

- Verify29 unique stable character IDs, two skills each,174 rank entries,203 C0–C6 entries, and19 stat fields each (15 CSV fields plus4 guide defaults).
- Verify all58 rank3 descriptions and all supplied numeric stats equal source; exactly five missing stat blocks are generated and marked. SHA-256 verifies originals unchanged.
- Reject runtime import of draft JSON: runtimeReady=false. Only typed, reviewed, complete allowlisted effects can publish. Unknown keyword arguments, contradictory targets or unresolved trigger clauses must fail export.
- Verify CSV IDs do not overwrite legacy asset IDs; KyoCSV1 differs from legacy0; Athena/Shingo variants need identity review.
- Test source defaults/aliases, PvP-only Goro/Joe inactive in PvE, SUB aura removal on death and activation after entry, and no duplicate stat accrual.
- Source semantics review remains separate from runtime tests. No C01–M02 or build test was run during documentation generation.


## 11. Revised economy, map and screen gates

Authority: [economy and route design](../Specs/fighting-allstar-economy-route-design.md), [battle screen states](../Specs/fighting-allstar-battle-screen-states.md), [Unity phases](../Specs/fighting-allstar-unity-implementation-plan.md). Earlier fixed-stage/equal-pool assumptions are superseded.

| ID | Fixture | Expected |
| --- | --- | --- |
| E01 | Eight-kit KOF pool |4/36/60 buckets; Athena4%, fourSR9%each, threeR20%each; no cross-series fighter |
| E02 |159/160 and1599/1600 wallet boundaries | Exact160/1600 debit or no change;10 pulls no discount/no automaticSR |
| E03 | Proposed milestone295+10, earlySSR, duplicate retries | Progress5, one choice entitlement; earlySSR no reset; retry same receipt. Replace fixture if owner chooses hard pity |
| E04 | Banner changes while submitted / expired selector pool | Published revision validated at commit; earned selector retains frozen choices; no reroll or lost entitlement |
| R01 | Two next-row options | Inspect free; choose once; other node bypassed; no backtrack/reward farming |
| R02 | Battle ends with livingHP and one0 | ExactHP carried; no40% automatic heal; living reserve fills vacancy; Rest heal does not revive |
| R03 | Revive/Heal Rest concurrent requests | One choice committed; revive30% for one OR heal40%living; repeated call no extraHP |
| R04 | Difficulty0/100 and reconnect | Enemy basic stats1×/2×; substats unchanged; quoted320/640 proposed reward; value locked throughout run |
| R05 | Seeded generation / malformed edges | Reachable boss and declaredRest route, row-increasing edges, valid team snapshots; fallback deterministic |
| V01 | B00–B21 state walkthrough | Inputs enabled only in allowed states; reset exact; fizzle/reconnect/result visible; no animation-controlled authority |
| V02 | Slow playback at planning open | Catchup to authoritative state; planning deadline unchanged; no advantage from delaying animation acknowledgment |

Economy/run/visual fixtures are specifications awaiting implementation. Documentation checks do not satisfy them.
