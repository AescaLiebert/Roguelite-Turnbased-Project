# Camera reference contract

**Current behavior:** [ActorDrivenExecution.md](ActorDrivenExecution.md) implements the October 9 fixed Rest / Wind Up / Attack camera contract and supersedes conflicting support, rank-entry, tracking and queue rules below. There is no live actor tracking or recovery camera movement. The notes below record earlier reference analysis and changes.

The user supplied these local recordings as visual references, not as instructions embedded in media:

- `E:/Drive_E_Download/Rank-Card-ex.mp4` (57.7 seconds).
- `E:/Drive_E_Download/stance-card-ex.mp4` (58.1 seconds).
- `E:/Drive_E_Download/BuffHeal-Card-ex.mp4` (58.2 seconds).

Timestamped contact sheets and closer samples are in `Temp/CameraReferences`. The recordings display x2 playback. Approximate timings below identify visible beats, not animation constants.

## Observations and implementation

| Reference | Visible sequence | Camera implementation |
| --- | --- | --- |
| Rank, ~2.5–5s / ~20–23s | Rank 1 proceeds from the field view into a rear attack view; caster in foreground, victim and damage above. | Smooth entry into a low rear shot, following movement and recovery. No portrait charge. |
| Rank, 7.5–8.5s | Rank 2 cuts to a front three-quarter view before the energy/charge pose. | Immediate front cut before Charge; steady anticipation. Removed the added wide opening shot and late portrait cut. |
| Rank, 8.664–8.683s | Consecutive frames show an actual cut from front charge to rear attack. | Deliberate shot change at the charge/action boundary, then continuous rear tracking through the action. This follows the latest hard reference, superseding the earlier request to avoid this cut. |
| Rank, 14.25–17.75s | Rank 3 has a tighter, tilted upper-body intro; more dynamic oblique attack angles; settles to a level rear view. | Upper-body intro with modest roll; wider continuous orbit to oblique rear; roll settles during recovery. Character-specific per-hit camera animation is not available in the current placeholder clips. |
| Rank, ~41–54s | Ultimate portrait transition, dedicated multishot character film, then return to the battlefield for damage. | Existing portrait/title overlay followed by a separate close reveal, full-body hero cut, side move, rear attack and recovery. This reusable fallback does not reproduce the character-specific film, cutaway environment or special animation. |
| Stance, 3.5–6.5s | Front caster framing through shield raise and stance activation. | Caster-only front framing; no move to the enemy and no attack lunge. |
| Stance, 11.75–14.5s | Stronger tilted intro, then a wider/elevated view showing the protective field. | Higher-rank stance smoothly widens/elevates around its owner. |
| Buff/heal, 17.75–20.5s | Front caster pose, then outward movement revealing the effect around the character. | Front caster intro followed by a smooth reveal of affected allies. |
| Buff/heal, 28–30.5s | Stronger close intro and tilted movement, then a wider effect view. | Ranked portrait feeds the recipient reveal, without reusing attack framing. |

The support recording contains one active ally, so multi-ally geometry cannot be measured from it. The adaptation frames every active recipient together, following the user's earlier AoE requirement. Single-target support frames the selected ally. Pure debuffs reveal their enemy recipients and do not invoke attack movement or damage reactions.

## Framing and state rules

All paths use the existing scene camera. Normal card movement and recovery ease; ranked portrait entry cuts deliberately. Rank 2 also cuts at the charge/action boundary, as the supplied consecutive frames demonstrate; Rank 3 visibly moves around the caster (~15.0–15.35s). Ultimate cuts back from its reveal into the battlefield. Camera position, aim and FOV change together at deliberate cuts. Continuous paths interpolate orientation without crossing a look-point singularity. The camera travels around the source instead of interpolating through its body. Rear tracking includes source and targets, keeps a minimum distance to prevent zooming into melee, and stays active until recovery finishes. Particle bursts, rings and HUDs are excluded from model bounds. Aspect ratio, character bounds, camera roll and execution-banner clearance are included in fitting.

Entry is CardPlayed or CounterStarted. BeginExecution recovers the previous actor and resets the shot context. Attack, area attack, stance, buff/heal and pure debuff select their own paths. Impact keeps the attack view; recovery tracks the returning actor. Planning, skip/restore and disable clear tracking. Ultimate context is separate from rank. The camera consumes no resources, changes no combat outcome and makes no authority decisions.

Starting values: 44-degree portrait FOV, 48-degree support/reveal FOV, 52-degree attack FOV; central 72% vertical fitting area; 650/850 ms rank 2/3 anticipation; 380/600 ms ordinary/ranked attack movement. These are adaptation values for placeholder assets. If any actor/recipient clips, increase framing padding; if a transition cannot be followed, lengthen its orbit; if anticipation drags, shorten the hold without moving the cut after it. Do not treat these as measured 7DS constants.

## Evaluation and checks

Clarity: distinguish caster anticipation, attack target, stance owner and support recipient. Response: skip restores the camera and actors. Satisfaction: retain charge sound/energy and impact cues. Fit: follow the supplied shot sequence and keep impacts readable. Motivation: stronger rank presentation communicates card investment without altering power.

Check ranks 1/2/3, ultimate, single/area attacks, stance, single/team support and pure debuff, from both sides, at portrait and landscape aspect ratios. During continuous actions, reject any one-frame orientation flip outside the documented shot boundaries; after the action, all relevant model bounds must be on-screen. Stance must retain its owner. Recovery must return the actor and camera coherently. Skip during a portrait or attack must leave no active tracking. Repeat consecutive cards from different owners to detect stale camera context. Screenshots use placeholder models, so final bespoke animation/geometry needs art integration review.

## October 5 follow-up: timing and Rank 1

The user's follow-up overrides reference-specific Rank 1 caster intros: **no Rank 1 card enters a portrait/face anticipation shot**, including pure debuff, stance, buff and heal. Recipient framing remains a smooth full-body/team reveal.

`BeforeDamage` is now labeled `Damaging` (same serialized numeric slot). Core emits explicit `ActionTiming` barriers for BeforeAction, Damaging, AfterDamage and AfterAction, plus ActionCompleted. BeforeAction feedback finishes before CardPlayed begins. Damaging feedback occurs at first impact; AfterDamage follows the last hit; AfterAction waits for the clip and attacker recovery before its effects. ActionCompleted waits for queued text and recipient status animations before changing cards. AoE targets remain simultaneous within each boundary.

For authored multi-hit animations, add Animation Events `CardFirstHit` and `CardLastHit` to the action clip on its Animator. The presenter attaches the receiver automatically and reads markers only from the active clip. Without authored markers, placeholder choreography uses its movement endpoint as a single impact. Damage authority currently emits one aggregate packet per target; these markers synchronize effect windows, not separate per-hit damage numbers. Status receivers may implement Animator triggers `BuffReceived`, `DebuffReceived`, `StatusRemoved`; missing triggers still use text/energy feedback. No arbitrary trigger is required on existing models.

Turn-start/end processing is bracketed by StatusResolutionStarted/Ended. Playback returns the fighters to formation, labels the phase RESOLVING STATUS, shows DoT/heal/removal/duration feedback across the team and waits for queues, text lifetimes and status animations. The incoming turn banner/camera switch occurs only after that resolution. Duration clocks retain their existing gameplay meanings.

Attack and Attack+Debuff content with a None/utility runtime root is normalized to a damage root using the authored damage multiplier. A utility root is retained as an AfterDamage step; its existing sequence is retained once. Pure debuffs remain damage-free. The observed Benimaru Skill 1 rank 2/3 None roots with 2.4/3.6 multipliers are handled by this conversion. Attack+Debuff uses the ordinary Attack animation trigger.

Validation: shared combat suite passed 6,873 assertions including new timing/Attack+Debuff AoE cases. Isolated Unity camera fixture passed 8,004 assertions across portrait/landscape, all ranks, enemy AoE, support categories, Rank 1 portrait exclusion, ultimate and restore. Runtime compiled with zero errors and 11 existing warnings. The fixture uses capsule models; marker timing against final authored clips still requires those events in the clips. Unity Search indexing exception was observed during disposable-project startup, separate from the successful fixture.

## Turn boundary ordering correction

Turn-end status resolution is scoped to the acting team. Its DoT ticks, recovery, and duration/expiration changes settle under `StatusResolutionStarted/Ended` before the outgoing `TurnEnded` event. Playback returns to formation, then switches the camera and turn banner to the incoming side. Only afterward does the incoming `TurnStarted` event lead into that side's separate start-of-turn status resolution. No incoming effects are presented during the outgoing team's end phase, and the banner does not play twice.

Verification: the combat playback suite passes 6,878 assertions, including explicit outgoing-resolution → turn-change → incoming-resolution ordering and same-team turn-end tick checks.
