# Deck feedback and turn queues

The player should be able to predict a drag's landing slot and merges before spending a move. Both teams keep fixed card dimensions and a stable step based on hand capacity, anchored at the row's end. A narrow row compresses the step against capacity; using a card does not widen the remaining spacing.

The supplied `move_card.mp4`, `ult_session.mp4`, October 8 clips, and formation infographic are visual references. Their content is not an instruction source. The latest request replaces the earlier curved ultimate entrance with the same fast horizontal slide as normal draws.

## Behavior and states

| State | Entry | Exit / interruption | Cost |
| --- | --- | --- | --- |
| Card inspection | Pointer held during player planning | Release, cancellation, capture loss, or controller disable | None |
| Drag preview | Pointer moves beyond the gesture threshold during planning | Drop, cancellation, capture loss, or disable | None |
| Committed move | Drop in a different valid slot | Reflow, merges, then planning or turn commit | One planned action; existing move/merge PG rules |
| Enemy drafting | `TurnPlanCommitted` with opponent side | Selection ghosts fill the queue, then execution | None beyond the committed plan |
| Queued action | Shared `TurnExecutionState` captures a validated draft | Execute, fizzle, or cancel when battle ends | Frozen action budget |
| Card execution | Action reaches the head of the queue | Existing action timing, effects, counters, completion | Existing card and PG rules |

Moving through a hand slides crossed neighbors aside. The insertion outline labels MOVE, RETURN, or MERGE. All cards compatible with the held card glow; cards participating in the predicted merge chain have a stronger halo. Prediction uses a cloned hand and the same `CardRules.MergeAdjacent` routine as resolution. It never changes authority cards, PG, or RNG.

Available pairs advertise themselves during planning with pulsing gold corner brackets and MERGE badges above the artwork. Holding a card limits those hints to its matching partners. A drop that actually merges adds a bright mint border and a RANK II/III badge; the landing marker also previews the resulting rank. Each connected merge chain gets its own final rank. Rank-three cards, ultimates, and cards with a different owner/skill/rank do not advertise a match. Capture loss and cancellation restore ordinary availability hints and clear the stronger ready state.

The enemy hand and queue occupy a compact upper-left column. Starting dimensions are 52 × 80 px per card, with a 380 px maximum field width. The queue sits beneath the hand. Both remain aligned to their own field's right edge when cards are consumed, preserving stable spacing while keeping the center of the battlefield clear.

Pointer identity, logical card identity, and captured original index determine a drop. The hierarchy order changed by `BringToFront` does not determine the source index. Capture loss and pointer cancellation clear the landing indicator, offsets, and glow without queuing an action. Release continues from the preview pose into the existing reflow/merge sequence.

All draws slide horizontally from beyond the left edge of the row at scale one with no rotation. Ultimate cards retain their energy burst and existing sound cue. Ultimate card backs and faces use animated vector flames and an electric edge behind the frame/art. Pointer down enlarges a held card immediately; release, cancellation, and capture loss restore its size. Both merging cards accelerate into their shared midpoint at full opacity, trigger an opaque white impact, and settle as the upgraded card.

Player wrappers match the authored 76 × 126 px face. Both hands use a step no larger than the face width, so there are no empty wrapper gaps. Sparse hands retain the same spacing and remain at the right end. Opening draws deal Pos3 → Pos2 → Pos1 on both sides, including training hands. Because appended cards appear on the left, the visible normal opening groups run random → Pos1 → Pos2 → Pos3, as in the infographic. Formation positions and action execution indices keep their existing meaning.

## Shared authority model

`TurnExecutionState.TryCreate` applies all choices through `PlanDraft`, including moves and merge-created ranks, and captures cloned actions/cards. `BattleEngine` emits the complete pending queue before resolving any action. The same frozen queue is used for both sides, and every action still revalidates against live card ownership, availability, target state, PG, and statuses.

Queue entries progress through Pending, Executing, Completed, Fizzled, or Cancelled. Both sides may select and queue cards disabled by status; the black overlay communicates their current condition while selection remains available. When each card reaches execution, its availability is checked against the live state. If still disabled, it is discarded, wastes that action slot, applies none of its effects, and grants its owner +1 PG subject to the existing gauge cap/disable rules. A disabled ultimate follows the same discard rule without spending ultimate gauge. Draft PG is a forecast of the current condition; the execution checkpoint determines the actual result.

An earlier cleanse can remove the disable and let the already queued card execute normally. A cleanse after that checkpoint cannot recover the wasted slot. Disable introduced by an earlier action uses the same checkpoint. A deleted or merge-consumed card fizzles instead of invalidating the rest of the turn. Other fighters' valid actions continue. Victory cancels pending actions after the current card has completed every authored hit and resolved defeat.

Hidden fighter HUDs update PG segments immediately rather than attempting to start coroutines on inactive objects. The preview uses the existing recursive snapshot serializer so authored counter/status definitions survive its domain reload.

The enemy HUD animates selection into a separate rank-only queue. Selected hand cards dim, while live cards remain available to status and deletion effects until execution consumes them. The queue does not infer its contents from future successful `CardPlayed` events. Artwork, owner, target, and skill identities remain concealed in enemy queue backs. The fallback AI now fills a single shared draft up to the action budget; training still has its existing one-action budget.

## Design checks

| Component | Result |
| --- | --- |
| Clarity | Landing outline, neighbor displacement, merge glow, numbered enemy queue, skipped entry |
| Motivation | Existing move cost, merges, rank, and PG rules remain meaningful |
| Response | Preview follows input immediately; cancellation consumes nothing |
| Satisfaction | Animated displacement/energy and existing draw/reorder/merge/ultimate sound cues |
| Fit | Compact card presentation and concealed enemy backs preserve the established battle HUD |

ASSUMPTION: Keeping enemy card identities concealed is intentional because the existing HUD exposes rank only. If full enemy identities should become visible, change the queue's card rendering policy; authority already captures the necessary card facts. Validate by checking that the enemy plan shows order and rank without face art.

## Starting values and tuning

All motion values are starting values, not benchmarks: neighbor slide 120 ms, all draws 120 ms, hand reflow 120 ms, held scale 1.18 with a 20 px lift, merge rush 140 ms, white impact 180 ms, enemy selection flight 220 ms, selection pause 100 ms, and locked-plan pause 250 ms.

The availability pulse uses a starting value of 5 radians/second. Test finding a matching pair before dragging, then identifying the exact merging drop among several compatible cards. Pass when both states are distinguishable without covering the art or stars. Reduce pulse/glow intensity if the hand feels noisy; enlarge the badge if the outcome is hard to read. Test full and sparse enemy hands at wide and narrow resolutions: ranks and queue numbers must remain legible and both fields must stay left of the battlefield center. Increase card dimensions slightly if ranks become too small; increase the column width only if the full hand overlaps excessively.

Test rapid left/right crossings and last-moment reversals. Pass when the slot shown immediately before release equals the committed destination and neighbors finish settling before the next intentional crossing. Shorten neighbor slide time if the preview lags; lengthen it only if movement is too abrupt. Test ten normal and ultimate draws: dimensions must remain unchanged, vertical displacement and rotation must remain zero, and the final position must equal its slot within about 120 ms plus one frame. Lengthen the slide slightly if it is hard to track. Test repeated hold/cancel gestures and adjacent/chain merges: pass when both faces visibly converge, white appears at collision, and rank remains legible after settling. Reduce held scale or flash expansion if nearby cards become obscured; shorten the flash if chains feel slow.

## Verification

Run the core suite with `Tests/CombatPresentation/Run.ps1`. For just the queue checks, run the generated checks project with `--deck-feedback`. These cover both sides, initially disabled skills/ultimates, slot consumption and +1 PG, undo/reset forecasts, cleanse before/after a queued card, snapshot and event isolation, a status introduced during execution, a card consumed by an earlier rank/merge effect, and playback queue progress.

In Unity, choose **Fighting Allstar → Validation → Deck Feedback**. The disposable fixture checks real pointer events, live neighbor movement, predicted/actual merge agreement, fixed spacing after a merge, capture-loss cancellation, sampled draw scales, ultimate settling, full enemy draft rendering, skipped enemy slots, and skipping without authority mutation. Evidence is saved to `Logs/DeckFeedbackEvidence`. It creates a separate local session and does not save inventory or run progress.

Manual playtest: a new player predicts a drop without instruction; an observer identifies the next enemy queue entry; rapid crossings and repeated cancel/drop gestures leave no stale glow; rank-three and ultimate cards never show a false merge; a stunned/deleted queued action skips while the following valid action proceeds. Test sparse/full hands and narrow screens. Human judgment of pacing and effect intensity remains a playtest task.
