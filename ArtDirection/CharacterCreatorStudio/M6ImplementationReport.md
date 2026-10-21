# M6 — Actor and presentation lab

20 October 2026 · Extends the revised M5 Quick Battle Lab. Mian remains a case study; no playable kit is added.

**Status: paused/incomplete.** Work was interrupted before final closure and then stopped at the user's request. A native error remains in `BattlePlaybackDriver.FreezeClip`: Unity requests `Editor.AnimationUtility` when editing clip events. The checks below are scoped observations, not a completed release gate. See the [actual creation readiness audit](ProductionReadinessAudit.md); its source-persistence and runtime publication findings take priority.

## Designer workflow

Open **Fighting Allstar → Character Creator Studio → Actor lab**. Select or create an Actor Profile and Ability Profile. New ability profiles link to the selected actor profile. Choose the compiled actor's skill ID and card rank, then **Record selected ability**. This records an actual production-engine action in memory without entering Play Mode or saving a scenario.

The isolated viewport supports play, pause, reset, previous/next contact, absolute timeline scrubbing and contact-row jumps. It samples copied clips and displays committed contact IDs, event IDs, damage and HP projections. **Capture latest Battle Lab action** imports the most recent committed action from a live session. The viewport displays one source and one recipient; the timeline retains all recipients' facts. Use Battle Lab's full formation to inspect multi-target staging.

Edit profile fields in the right-hand inspector. **Rebuild visual preview** captures the edited visual configuration; **Save profile assets** persists the reusable profiles. Gameplay draft saving is separate. **Test these profiles in Battle Lab** transfers the selected actor/target profiles into the disposable setup. Launch/restart and open Game View to audition runtime clip playback, contact FX, impact audio, FCT and character inspection.

## Supported profile data

- Actor: model prefab, visual scale/yaw, Animator/controller path, role-to-clip/name mappings, bounds, approach distance, visual root motion policy, named socket paths/offsets, reaction clips/fallback capabilities and linked ability profiles.
- Ability: stable skill ID (or `$ultimate`), optional variant ID with base-profile fallback, windup/action/recovery durations and clips, normalized `contact:1` through `contact:10` offsets, recipient socket FX, cue lifetime, audio/volume, camera offset/FOV and recovery.
- Contact ordinals bind to committed logical hits. Extra markers report diagnostics; missing markers derive timing from the actual hit count. Area recipients share a contact ordinal but retain separate event IDs and committed amounts.

Missing model/clip/socket paths have deterministic fallbacks. Runtime Animator events and copied clip events are suppressed. Clip-local root motion affects only the visual hierarchy; it cannot move a combat target or decide damage. Scrubbing is silent; runtime cue audio is auditioned in Battle Lab. Profiles do not supply gameplay commands.

## Architecture and isolation

`PresentationTimeline` is a pure shared-core projection of cloned committed facts. It preserves event order, target identity, amount, hit count and HP after resolution. Marker edits can retime visuals but cannot reverse fact order. Reads return fact copies. Final duration includes the last reaction's settling time.

`BattlePlaybackDriver` receives actor objects, frozen profile copies, camera, audio source and effects root through its constructor. It has no battle engine, account service, scene lookup or private-field reflection dependency. It owns copied animation clips with events removed, contact FX, visual clocks and cleanup. Actor creation reuses `BattleFighterView` with explicit formation rotation. Missing sockets resolve to the profile's fallback impact position.

`StudioPresentationViewport` owns the preview scene, lights, camera, actors and editor clock. Rebuilding, detaching, closing the Studio, assembly reload or editor quit disposes the driver and preview scene. Absolute sampling restores the rig's base transforms before applying clips and reactions, so backwards scrubbing and repeat samples are deterministic.

Battle Lab composes the same driver with real actor views and a runtime audio source. The existing controller still submits plans, reduces committed facts, shows FCT/HUD and owns battle state. Profiled actions use the extracted driver's contact clock; unprofiled actions retain the existing stage presentation. A committed defeat takes precedence over reactions. A lethal-prevention event without committed defeat never hides the actor. Overlapping reactions retain the stronger active response.

Profiles are opt-in Battle Lab presentation overrides. This does not activate a new production character source; bundle activation remains M8 work. Verification uses a clearly named synthetic rig/clip fixture because the current registry characters use generic actor assets and the project contains no production AnimatorController assets. Humanoid retargeting/art quality require supplied rig/animation assets and are not claimed by the synthetic fixture.

## Interaction and timing checks

Clarity: committed contacts, authored markers and fallbacks are explicit. Response: pause, scrub, reset and stop are available without replaying combat. Satisfaction: the live path retains real FX, FCT and audio. Fit: existing stage/card presentation remains the default; the lab supplies opt-in visual profiles. Motivation is designer iteration, with durable reusable profiles and disposable battle sessions.

States: unloaded → recorded/frozen → sampled/playing → paused/completed → reset/rebuild/dispose. Live actions enter only through validated normal card submission. Committed playback can be skipped/stopped; cancellation removes FX, restores transforms/camera and releases owned clips. Pausing Play Mode freezes the profile clock. There is no gameplay resource cost for previewing; runtime resource costs remain engine-owned.

Starting presentation values: 0.2s windup, 1.2s action, 0.3s recovery, 0.5s reaction settling; default approach 1 unit. They are editable visual starting values, not balance changes. Micro-test: every committed contact must appear in order, final projection must equal authority, and reset/rebuild must leave no active clock/effect. If contacts feel compressed, lengthen action timing; if the last response is cut, lengthen the visual recovery/settling interval. Camera cues are explicit opt-in shots and restore the injected home camera on completion/cancel.

Acceptance includes first-time workflow discovery, rapid play/reset/scrub/rebuild, missing clips/sockets, unmatched/invalid markers, exact repeat samples, overlapping reactions, fatal saves versus defeat, source edits during frozen playback, Play Mode pause/resume, source/scene isolation and repeated viewport disposal. Presentation tuning must never change engine facts or either RNG stream.

## Verification artifacts

- [Shared timeline and existing Studio checks](../../Artifacts/CharacterCreatorStudio/M6/core-evidence.json)
- [Native isolated viewport checks](../../Artifacts/CharacterCreatorStudio/M6/native-preview-evidence.json)
- [Live profile Battle Lab checks](../../Artifacts/CharacterCreatorStudio/M6/live-profile-evidence.json)
- [Authored clip viewport](../../Artifacts/CharacterCreatorStudio/M6/authored-viewport.png)
- [Live profile battle](../../Artifacts/CharacterCreatorStudio/M6/live-profile-battle.png)

The shared harness passes **466 assertions**, including 20 new immutable timeline checks. Native isolated-preview acceptance passes **28 assertions**, including actual ultimate recording and variant binding fallback. Profiled 3D Battle Lab acceptance passes **14 checks**, covering authored clip/FX/audio, pause, exact damage/HP/RNG parity, final projection and cleanup. The standard M5 live regression passes **23 checks**, and the existing training regression passes **2,383 assertions** plus target-reaction/frozen-queue checks.

The final consolidated verification artifact was not completed before interruption. [Actor lab workspace](../../Artifacts/CharacterCreatorStudio/M6/actor-lab.png) captures the partial Studio UI. Existing checks cover their documented subset, not final release closure, every unrelated repository suite or production humanoid retargeting/art quality.
