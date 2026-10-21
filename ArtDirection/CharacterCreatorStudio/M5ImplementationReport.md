# M5 — Quick 3D Battle Lab

20 October 2026 · User-revised M5 scope: disposable runtime testing, without a scenario save/file workflow.

The **Battle Lab** workspace launches the existing 3D Training scene in Play Mode with a frozen, in-memory setup. It uses the same `CoreBattleSceneController`, production combat engine, card planner, enemy planner, FCT, battle-stage motion, hit reactions, SFX and character inspector as runtime combat. No new battle scene or playable character kit is installed.

## Modes

| Mode | Behavior |
| --- | --- |
| Real Battle | Normal HP, defeat, deck/action economy, resource costs, enemy AI, RNG streams, status clocks and DOT. Supports 1–4 fighters on each side; the fourth is reserve. |
| Training Battle | Existing 1v1 practice rules: no defeat, all skill ranks, free ultimate and one player action per turn. The chosen target passes its turn instead of attacking. Actual effects still resolve and committed contact facts drive presentation. |

Both modes are disposable. Lab results bypass dungeon/reward/result persistence. Account inventory bootstrap and file writes are suppressed while the lab context is active. Ordinary non-lab battle, training and account flows retain their existing behavior.

## Quick workflow

1. Open **Fighting Allstar → Character Creator Studio → Battle Lab**.
2. Choose Real Battle or Training Battle. Select actors/targets, level and tier. Actor 1 can use the current compiled draft without activating it in the game registry.
3. Optionally change opening HP, gauge, extra shield and a starting standard status. Starting periodic statuses can use an explicit captured-damage amount; `-1` uses the opposing lead's authored ATK as the lab snapshot basis. This is setup data, not a simulated triggering hit.
4. Optionally select a model/rig prefab and scale/yaw override. The override uses the prefab's existing Animator and runtime movement/reaction fallbacks. Registered characters may still use the generic shared actor prefab; the lab does not invent missing rig animation or VFX assets.
5. Launch the 3D lab and open Game View. Use the regular runtime hand/target planning, or choose a runtime hand card and target in the Studio's live controls. A quick card action commits the turn and, in Real Battle, lets the normal enemy AI respond.
6. Pause/resume, pass a turn to test status clocks, open the actual runtime character inspector, restart the same setup/seed, or stop the lab. Setup edits only affect the next restart. **New seed** changes the next run; restart alone preserves it.

The editor's Play Mode start-scene override preserves the previous scene setup, including its unsaved editor state, rather than saving or replacing the user's scene. Stopping restores the previous start-scene preference and clears the volatile handoff. A lab cannot replace an unrelated active Play Mode session.

## Evidence while testing

The live inspector shows authoritative HP, shield, gauge and statuses, battle/card RNG state and draw counts, turn/revision, presentation phase and recent damage/heal/status facts. Damage facts retain hit index/count, origin, amount and HP after resolution. The Game View overlay shows the mode, seed and last presented contact.

The lower **Trace** tab shows actual passive decisions from the live session, with jumps into locally authored rules. **State diff** compares the authoritative snapshot with the current 3D playback projection; these intentionally differ while committed events are still animating. Snapshots are copies, so inspection cannot mutate authority. **Problems** identifies the live runtime session rather than presenting stale fixture evidence.

An editor quick action is unavailable during opening/playback, while paused, or while the runtime inspector is open. Existing queued Game View cards must be committed/reset first. The engine still validates card readiness and target legality; the lab does not manufacture a missing Real Battle card. Restart/stop remain available when the battle ends.

## Architecture and scope

- `BattleLabSimulation` owns the cloned setup-to-production-state mapping. It sets levels before opening rules, captures passive traces from battle creation and retains deterministic combat/card streams.
- `BattleLabContext` holds volatile runtime setup and optional presentation overrides. It owns no save path.
- `StudioBattleLabBridge` transfers the frozen setup through Unity's in-memory `SessionState` across Play Mode domain reloads and restores the previous editor start-scene preference on exit.
- `CoreBattleSceneController.BattleLab` exposes narrow quick-action/inspection/snapshot APIs and the live overlay. The normal controller continues to own presentation and session submission.
- `LocalBattleSession` adds an optional passive-opponent policy used by lab practice; its default behavior is unchanged.
- The existing saved M3 acceptance fixtures remain available under **Legacy acceptance checks** for regression work. They are not required or created by M5's normal workflow.

This replaces the original broad M5 requirement for saved scenarios, arbitrary replay files, formula debugging and rewind with the requested quick lab. Those advanced tools are deferred. M6 still owns full rig/profile/timeline authoring. Mian remains a case study; no Mian kit is registered.

## Interaction and verification design

Clarity comes from explicit mode and frozen-setup labels plus the existing card/target telegraphs. Response uses normal planning/playback gates, with restart/stop as clean exits. Satisfaction uses the actual visual and audio pipeline, not fabricated damage cues. Fit comes from reusing the runtime's scene, camera, actor registry and combat vocabulary. The lab changes no balance values or animation timings.

State transitions are Edit setup → validated launch → opening → player planning → commit/3D playback → opponent/status resolution → planning or completion → restart/stop. Pause suspends Unity Play Mode. Restart cancels the old scene-owned coroutines/resources through normal scene teardown and creates a fresh battle. Stop returns to the previous editor setup. Source edits never retroactively change a live definition.

Acceptance covers malformed setup, deterministic restart, separate RNG streams, team/reserve mapping, normal enemy AI, idle practice targets, real DOT ticks, exact hit contacts, FCT/audio/motion, runtime inspector access, source/scene byte isolation and account/run file nonmutation. Stress tests include rapid action gating, request retries, stop/restart and mode changes; readability checks compare committed and presented state.

Artifacts: [core checks](../../Artifacts/CharacterCreatorStudio/M5/core-evidence.json), [live Play Mode checks](../../Artifacts/CharacterCreatorStudio/M5/live-evidence.json), and [final verification](../../Artifacts/CharacterCreatorStudio/M5/final-verification.json).

## Verified result

- **446 shared assertions pass**, including 36 new lab setup/session checks and the existing M0–M4 suite.
- **23 actual Play Mode assertions pass**: runtime prefab/renderer creation, practice deck, all four contact indices, FCT, impact audio, actor motion, pause/resume, actual inspector opening, deterministic restart, visual override isolation, Real Battle AI, DOT and RNG, editor restoration and source/account/run file nonmutation.
- **2,383 existing training assertions pass**, alongside the existing target reaction and frozen-queue checks in that regression entry point.
- **873 existing elemental/Genshin assertions pass**. These are the relevant regression subsets, not a claim that every unrelated repository suite passed.
- The live run records exact runtime/editor/presentation assembly hashes and observes active FCT, playing runtime impact audio and actor movement. The previous editor scene and its dirty state are restored, the lab context is cleared, and the Training/source character asset bytes are unchanged.

Background execution is enabled only while a lab scene is active, so the simulation keeps running when the Studio panel has focus. The previous setting is restored on scene teardown. A script reload that loses the managed authority session recreates the disposable lab from its frozen in-memory setup instead of leaving a stranded session.

## Captured UI

- [Battle Lab setup](../../Artifacts/CharacterCreatorStudio/M5/lab-setup.png): Real Battle setup with actor/target controls; advanced overrides remain folded until needed.
- [Live Studio controls](../../Artifacts/CharacterCreatorStudio/M5/lab-live.png): runtime hand, target, pause, pass and inspector controls alongside the running Training Battle.
- [Real Battle Game View](../../Artifacts/CharacterCreatorStudio/M5/real-battle.png): existing scene and runtime actor presentation.
- [Runtime character inspector](../../Artifacts/CharacterCreatorStudio/M5/runtime-inspector.png): actual Game View capture including the normal stats inspector.

The final verified editor state is stopped, with MainMenu restored and clean, no pending lab handoff, and the previous background-execution setting restored. Screenshot capture used the same assembly hashes as the passing native trial.
