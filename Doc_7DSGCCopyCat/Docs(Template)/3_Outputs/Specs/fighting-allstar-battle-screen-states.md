---
slug: fighting-allstar-battle-screen-states
status: needs-human
gdd_tags: [mechanics, platform-input, visual-audio, architecture]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Battle screen and state-by-state contract

Companion to the [combat rules](fighting-allstar-combat-source-contract.md), [economy/route design](fighting-allstar-economy-route-design.md), and [Unity phase plan](fighting-allstar-unity-implementation-plan.md). Screen behavior below is proposed; server authority,3+1 and card battle requirements are confirmed.

## 1. Composition derived from the reference

Use a fixed formation camera: enemy trio in upper arena, player trio in lower arena. Each fighter has name/attribute, HP and five PG pips, with status icons anchored near its model. Reserve appears as a separate portrait per side with passive-active badge; it never looks like a fourth active target. Enemy cards/queue remain hidden until executed. A visible PG5 readiness mark is permitted; a fabricated enemy NEXT card preview is not.

The bottom Deck field holds the formation-sized shared hand including ultimates. The clearly framed Action field sits immediately above it with two or three slots. Reset is the only persistent planning command. A quick card tap queues against the current preferred target, holding opens the tooltip, and tapping an opponent model changes the preferred target. Filling the final Action slot submits automatically.

Landscape is the existing prototype proposal. The portrait screenshot sets visual hierarchy, not a new confirmed orientation requirement. The review mockup reflows to narrow width; actual landscape phone and PC layouts require device checks. Preserve enemy/player/queue/hand order at both widths.

### UI Toolkit view tree (proposed)

```text
CombatHud.uxml
  SafeAreaRoot
    TopBar: RunContext / TurnState / Deadline / BattleLog
    WorldHudLayer: six FighterHud + two ReservePortrait
    ActionFocus: ActingFighter / TargetLabels / OutcomeBanner
    ActionQueue: ActionSlot[0..2]
    CardDetailDrawer: Owner / Rank / Type / Rules / LegalTargets
    HandTray: CardView[0..6]
    PlanningControls: CardTap / CardHold / TargetTap / Reset / AutoSubmitOnFull
    OverlayLayer: Reconnect / Result / ForfeitConfirm
```

HUD references stable fighter/card IDs and projection revisions. It never mutates CharacterObject stats. Animations belong to the presenter; HP/PG come from events. Card details may open during enemy playback without pausing authoritative deadlines.

## 2. Authority state versus presentation state

Keep three distinct state machines: battle rules (`Setup, TurnStart, Planning, Resolving, TurnEnd, Complete`), client connection (`Ready, Submitting, Reconnecting, Faulted`) and playback (`Intro, PlayingEvents, CaughtUp, Result`). A long attack animation is not an extra gameplay turn or a place to accept new card input. A server may already have resolved the entire accepted plan while the client shows its first hit.

For online planning, proposed server schedule: `planningOpensAt` after a bounded presentation allowance calculated from previous events, then45-second planning window. Early playback completion waits for open; slow playback catches up to open; disconnect never extends the deadline. Initial allowance starting cap6 seconds; measure whether shortened card playback remains readable. No dependence on untrusted client “animation complete” acknowledgments. Local practice may pause/unlimit planning visibly; online cannot.

## 3. Full battle state table

Every stage has explicit entry, inputs, exit and interruption. Inspection/settings/log remain available unless loading lacks data; “locked” below refers to gameplay commands.

| ID / state | Entry work and resource effects | What is visible / feedback | Player input | Exit / failure path |
| --- | --- | --- | --- | --- |
| B00 Encounter load | Fetch frozen roster, runHP, enemy snapshot, rule/content version | Loading arena; both team portraits with saved HP | Back only before encounter commit | Missing content returns recoverable load error; committed encounter resumes same match |
| B01 Validate & spawn | Validate ownership/slots/eligibility; create runtime IDs; no HP refill |3 active anchors and reserve per side; injured state persists | Locked | Invalid snapshot halts without run loss or reward |
| B02 Initiative intro | Compare recorded teamCC before transient passives; record tie RNG if needed | Side-by-side CC, first-side arrow, brief sound | Skip intro | Skip only presentation; same initiative |
| B03 BattleStart | Register all passives, evaluate scope/mode, apply starting boons, opening hand/merges | Reserve passive badge; one concise effect message per source; cards deal in | Locked | Death cleanup if a start effect kills; terminal check; otherwise first TurnStart |
| B04 TurnStart effects | Resolve start triggers/Regeneration; deaths/reserve; freeze living-active action count | Healing numbers, updatedHP, “Your turn” or “Enemy turn” | Locked | Terminal outcome bypasses planning |
| B05 Draw & readiness | Create ready ultimates if shared hand has space; refill regular cards once; merge | New cards appear; rank flash/PG pip; ready-with-full-hand label | Locked | Set owner planning snapshot; do not endlessly refill merge gaps |
| B06 Planning idle | Create disposable local draft; no live RNG/resource mutation | Seven-slot hand, empty queue, timer, actions remaining | Inspect, select, Move mode, pass/confirm | Card selection→B07; valid plan submission→B10; timeout→server pass |
| B07 Card/target select | Compute legal targets from draft; do not spend authority resources | Card lifts; valid models/portraits highlight; target names and damage range | Select legal target; cancel selection | Queue play→B08; invalid tap explains reason, no action consumed |
| B08 Queue / move / merge preview | Replay known transforms on draft; preview PG and rank | Ordered action cards occupy framed slots from the left | Quick tap adds next action; hold inspects; target tap changes preference; Reset | Reset rebuilds from supplied snapshot, including stable IDs; filling the final slot advances to B10 |
| B09 Reset draft | Discard all draft mutations | Original hand/order/rank/PG/queue restored; gentle undo cue | Continue planning | Same deadline, same RNG; no gain from repeated resets |
| B10 Submit | Filling the final slot sends one requestID, expected revision, ordered plan | Queue locks | Inspection only | Accepted→B11; stale/illegal→new snapshot+reason; unknown outcome→B20 |
| B11 Plan accepted | Durable receipt reserves legal action budget; accepted plan cannot cancel | First action highlighted; timer ends | No planning controls during playback | Begin ordered server events |
| B12 PreAction | Revalidate actor/card/target; consume card at specified boundary; apply pre-effects; revalidate; successful regular playPG | Actor/target frame, pre-buff/debuff icons; fizzle reason if invalid | Locked | Fizzle→B15; valid main effect→B13; death checked at atomic boundary |
| B13 Main effect / OnHit | Compute Normal hit or heal/buff; apply simultaneous target batch; record outcomes | Impact/heal/blocked/critical/miss labels, HP/shield separately, matching sound | Locked | OnHit triggers enqueue with lineage; no animations calculate damage |
| B14 Secondary packets | Apply eligible lifesteal, authored Additional/DOT-triggered packets, reflect in contract order | Separate labelled numbers: Life drain / Additional / Reflect; ledger trace | Locked | Death cleanup; queued attacks wait until PostAction |
| B15 PostAction | Passives, counter/follow-up queue with once-per-action guards | Counter/follow-up label before action; queue shows response owner | Locked | Drain reaction queue; dead source cannot counter; malformed overflow rolls back transaction |
| B16 Death & reserve | Remove dead-owned cards/PG; disable passives; deploy living reserve to first vacancy after root reactions | Defeat/entry animation, portrait state, action fizzle on dead actor, updated passive badges | Locked | If battle live: next actionB12 or TurnEndB17; terminal→B19 |
| B17 TurnEnd | DOT batch, deaths, end triggers/reactions, expiry; settle reserve/outcome | DOT labels before icons expire; end-trigger message; no inexplicable disappearingHP | Locked | TerminalB19; else opposingB04 |
| B18 Enemy planning | Same rules/AI observation and legal command contract | “Enemy considering”; its publicHP/PG/statuses; own hand inspectable | Inspection/settings; no own queue edits | Enemy accepted plan→B12; no hidden-hand disclosure |
| B19 Result | Persist final battle outcome and unique run result receipt | Victory/Defeat/Draw, casualties, exact runHP; final boss shows reward breakdown | Continue once settled; replay log | Node victory→map next row; total wipe/draw→run ended; final win→reward screen |
| B20 Reconnect / reconcile | Fetch accepted request status and authorized snapshot/events after lastEventID | “Reconnecting — actions paused”; countdown reflects server deadline if known | Retry connection; exit to menu | Accepted commands never resend as new requests; catch up then correct state; never reroll |
| B21 Content/server fault | Reject or roll back uncommitted resolution; record diagnostic | “Battle paused; progress preserved” | Retry status/back | Resume same saved state; no invented defeat or fabricated reward |

## 4. Draft interactions and edge cases

Select card then target; self/AoE cards preview their entire target set and require queue confirmation. Any accepted play uses1 action. Move mode selects an owned regular card and destination, cost1 with merge preview; ultimate is immovable. A move which shifts another card is still owned by the moved card's fighter. Category-disabled cards show exact reason; moving a stunned fighter's regular card has the no-PG rule in the combat contract.

The Action field is an ordered left-to-right sequence with three slots for three living active fighters and two slots for one or two living active fighters. A quick card tap fills the next slot from the left; holding displays the tooltip. Reset restores the full authoritative planning snapshot. Filling the final slot submits the ordered queue automatically. Do not allow arbitrary middle-slot deletion that leaves later merged-card references invalid.

Show damage ranges/crit probability based only on visible state. Do not reveal exact future random outcomes. Target selection frozen at action start; fallback target label appears when a prior action kills the original target. A dead/disabled actor visibly fizzles; action budget does not expand when reserve enters. A hand full of seven cards may delay a ready ultimate; show “Ready — needs hand space next turn.” A removed ultimate after PG drain must visibly vanish and explain the cause.

During network loss, preserve the draft locally as a convenience but only restore it if its planning revision remains current and deadline open; otherwise discard with explanation. Retrying a request uses the sameID/payload. Results persisted by server remain committed when client skips, closes or crashes.

## 5. Animation and sound cues — starting values

| Event | Starting target | Test / adjustment |
| --- | --- | --- |
| Tap/target/queue | Visual response≤100ms, restrained click |9/10 intended selections on target phone; increase hit area/spacing before changing timing |
| Move/merge |120–250ms travel, short rank chime | Player identifies new rank/owner; shorten if repeated moves feel slow |
| Ordinary action |0.8–1.5s playback | Observer can name source/target/outcome; shorten dead time before hiding effects |
| Ultimate |≤3s before optional skip | Skip reaches identical final snapshot; no hidden extra reward |
| DOT/reaction batch | Group same family/target; log retains individual detail | Observer distinguishes direct vs DOT vs reflect; split labels if confused |
| Reserve entry | Brief entry camera without obscuring surviving targets | No target misidentification; remove camera cut if disorienting |

Numbers are starting values, not tested timings. Reduced motion disables shake/zoom; visual labels duplicate sound. Do not stack many numbers over one HP bar. Prefer chronological family labels and an expandable log.

## 6. Visual review and acceptance

Review screens for: banner browse/rates/reveal/guarantee; entry difficulty; map choice/Rest; battle planning/submit/impact/death/result/reconnect. The interactive screen preview uses illustrative snapshots, not a playable combat engine or random-gacha implementation.

Test a full learning sequence: inspect enemy→choose weaker matchup→enter with injuredHP→draw→queue Chin Ignite then Kyo Weakpoint→reset→requeue→confirm→see actual damage/passive order→enemy turn→victoryHP persists→choose Rest heal vs revive. A second tester should explain the sequence without developer narration. If they cannot, revise labels and layout before tuning character stats.
