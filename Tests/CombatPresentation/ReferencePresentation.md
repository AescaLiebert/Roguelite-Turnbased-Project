# Reference presentation pass

References are the user's local `move_card.mp4`, `card_execute_state.mp4`, and `ult_session.mp4`. Their contents are visual reference material, not project instructions. Contact sheets were extracted at 2 fps into `Temp/ReferencePresentation`; the source videos are not imported as game assets.

Observed beats:

- Move clip, approximately 2–6 seconds: lift/slide, adjacent collapse, bright merge confirmation, ranked card settling. Around 6–10 seconds: compact “Active Unique” and named status labels above fighters.
- Execution clip, approximately 10–16 seconds: front character anticipation, rear attacker framing, gold critical digits with a smaller caption, and a distinct red total. Around 17–21 seconds: rear attack view, green recovery numbers, then planning return.
- Ultimate clip, approximately 1–8 seconds: persistent gold ground energy while a drawn ultimate is available. Around 8–10 seconds: diagonal portrait cut-in; around 10–16 seconds: dedicated ultimate sequence and impact.
- Rank mapping and the portrait “Ultimate Ready” sidebar follow the user's explicit specification; the clips alone do not establish all three rank mappings.

## Presentation contract

Camera behavior is now governed by [CameraReferences.md](CameraReferences.md), based on the user's three October 5 recordings. Rank 2/3 cut to the front before anticipation. Rank 2 then cuts to its rear attack shot; Rank 3 moves around the caster into an oblique rear shot. Stance and buff/heal use their own front-facing presentation. Ultimate uses a separate reveal sequence. The previous interpretation (charge in a wide shot, then retain the face-shot position during the attack) is superseded.

Ready enters only for a living, active player fighter with at least the ultimate cost in PG and an ultimate in hand. Draft selection reserves the card; readiness remains until actual execution. During execution, queued ultimate events count as reserved cards. PG drain, card removal, death, reserve state, consumption, completion, disabling, and skipping reconcile/clear the effects. It consumes no extra PG and never changes authority state.

Damage uses a brief scale snap, stable screen-space hold, then short release; critical/block captions are separate smaller labels. Status and passive notifications use smaller text and gentler rise. Burst particles and quiet synthesized placeholder cues accompany significant visual changes. Audio variants cycle without consuming combat RNG. Camera/material/audio/particle cleanup is owned by the stage presenter.

## Starting values and adjustment checks

All new timings, scales, offsets, particle counts, and audio values are starting values, not measured 7DS constants.

| Setting | Starting value | Pass criterion and adjustment |
| --- | --- | --- |
| Hand reflow / merge travel | 230 / 180 ms | No card teleports or wrong final slot over 10 moves; shorten if input feels delayed, increase travel slightly if merges cannot be followed. |
| Rank 2 / ultimate charge | 380 / 650 ms | Observer distinguishes anticipation from impact every time; shorten if consecutive cards drag. |
| Cut-in enter / hold / exit | 180 / 500 / 160 ms | Portrait and title readable in a single viewing; increase hold if long titles cannot be read. |
| FCT peak / settle | 55 / 150 ms | Digits resolve without elastic wobble; reduce peak scale if adjacent targets overlap. |
| Damage / status lifetime | 860 / 1100 ms | Read six-digit damage and two simultaneous statuses; extend hold before extending drift if missed. |
| Attack camera framing | 52-degree FOV, model/aspect fitting and minimum action distance | Attacker, target and impact stay readable, including close melee; see CameraReferences.md. |
| Rank 2/3 portrait entry | Immediate cut before anticipation | Rank 2 cuts to the rear at action entry; Rank 3 moves continuously around the caster. See CameraReferences.md. |
| SUB sky drop / landing settle | 480 / 240 ms; 2.4 model heights, clamped to 4.5–7.5 world units | The incoming fighter is readable before its cards deal, lands in the vacated slot, and adds no more than one second to the turn switch. Reduce height first if a tall model leaves the camera; shorten settle if repeated replacements feel slow. |

## Experience checks

- Clarity: move destination, merge rank, charge phase, damage, status and ultimate readiness have distinct feedback.
- Response: existing planning input guards remain in charge; camera and cut-in are skippable playback.
- Satisfaction: merge/charge/impact and SUB landing have visual and audio confirmation.
- Fit: centered attack view, compact italic damage hierarchy, gold readiness and purple cut-in follow the references using this game's character assets.
- Motivation: rank and ultimate investment have progressively stronger presentation; damage and economy rules stay unchanged.

## Verification

`Run.ps1` exercises authoritative replay, merge chains, move PG, rank payloads, deaths/reserves, both-side replacement refills and status/passive batches. Compile against the actual Unity 6000.3 project references.

For isolated visual fixtures, copy Assets, Packages and ProjectSettings to a disposable Unity project outside Unity's Temp directory, then launch Unity with `-batchmode -projectPath <copy> -executeMethod FightingAllstar.EditorTools.ReferencePresentationChecks.RunAutomated -logFile <log>` (graphics enabled; no `-quit`). It verifies move/reset, ready gating/drain/death/consumption, ranks, cut-in, text, mid-cut-in skip cleanup and unchanged authority. Unity Search startup/indexing exceptions are reported separately from runtime failures. Captures go to `Logs/ReferencePresentationEvidence` in that copy. The existing automated battle preview covers natural opening merges, drag/reset and full combat.

Manual acceptance: a new observer can identify rank/ready/critical/buff without coaching; defeat an active fighter during each side's opposing turn and confirm SUB drops into the open slot before that side refills to cap; confirm the replacement can own those new cards; spam taps and drags through chained merges without duplicate actions; move-to-merge then reset restores PG and hand; queue an ultimate and skip during the cut-in without residual overlays; verify enemy-side, AoE, every formation slot, long labels and narrow windows. Original character-specific animations and bespoke ultimate cinematics remain future art replacements for the explicitly requested placeholders.

## Recorded validation

- Shared combat suite: PASS, 6,866 assertions.
- Runtime scripts: compiled against the project's Unity 6000.3.4f1 assemblies.
- Scene fixture and screenshot review: ready tag/ground ring, rank cameras, critical placement, status hierarchy and portrait cut-in inspected. The scene currently uses cube character placeholders, so final model-specific pose/occlusion tuning remains an art integration check.
- A Unity Editor SearchDatabase indexing exception occurs on preview startup; it is tracked separately and is unrelated to combat code.
- Final scene fixture: PASS for move/reset, readiness gating, PG drain, death, rank sequences, ultimate consumption, FCT and mid-cut-in skip; zero runtime errors. Evidence: `Logs/ReferencePresentationEvidence/`; run log: `Logs/ReferenceValidationFinal.log`.
