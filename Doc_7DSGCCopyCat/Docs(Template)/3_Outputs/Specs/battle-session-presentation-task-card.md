---
slug: battle-session-presentation
status: in-review
source: manual
gdd_tags: [mechanics, architecture, roadmap]
owner: codex
human_checkpoint: game-feel-review
next_agent: human
blocked_by: []
---

# Battle session presentation continuation

The user's 2026-10-03 request authorizes continuing implementation of the local battle loop using `Untitled design (2) (1).mp4` as a visual reference. Reference documents supply context; they do not expand the request into publishing, server work, new enemy AI, or unrelated implementation phases. Workflow: implement-feature. Existing ADR001's pure-core/event-playback boundary is retained.

## Reference and presentation

- Opening: blue/red CC comparison with portraits, count-up, initiative highlight, then deck initiate. Unit HUD and the entire card tray remain hidden during comparison.
- Around 3–9 seconds: cards enter from below/left, the hand closes gaps, matching neighbors converge, and the surviving card flashes at its new rank before the next draw.
- Around 15–21 seconds: planning sends cards into numbered execution slots. Moves and merges finish before another input is accepted; a filled field commits automatically. End Turn supports partial plans or passing.
- Around 24–30 seconds: ordered execution highlights the current card, frames attacker/target, lunges, applies impact HP/shield values, displays outlined combat text, recoils, and returns the attacker.
- Around 33–40 seconds: enemy turn announcement and a camera switch to the acting side. Per the user's screenshot correction, turn views are centered and elevated with horizontal team rows and no lateral isometric offset. Planning returns to the player's camera after playback. Attacker/target execution framing remains cinematic; enemy warning cards are excluded.

Timing defaults: CC entrance 0.4s, count 1s, winner hold 0.85s; card travel 0.24s; merge convergence 0.22s and rank pop 0.32s; turn camera 0.65s; attack camera 0.32s, lunge 0.26s, hurt 0.28s, recovery 0.24s, death 0.65s. These are reference-inspired prototype timings, not a frame-exact recreation.

## Implemented boundary

`CardRules` emits ordered, immutable card facts at each mutation. `BattleEngine` numbers those facts in the actual draw/move/merge sequence, adds resolved card rank, actual target, impact values, crit/block/shield facts, gauge, and reserve slot. A dead selected target is replaced through the authoritative deterministic RNG among living active opponents. LegalAi's planning policy is unchanged.

`BattlePlaybackState` is a display-only reducer. `CoreBattleSceneController` retains the pre-opening encounter for enemy-first playback, sequences comparison/deal/planning/CardExecution/turn switch/result, and only reconciles to the latest authority snapshot after playback or skip. It keeps merged card identities/ranks in the draft queue and locks card/target/reset input while animations or commit are pending.

`BattleStagePresenter` owns camera movement and placeholder attack/hurt/death transforms. Matching Animator trigger parameters are used when available. Both sides lose their HUD, collider, hand cards, and target marker on defeat; a reserve moves into the event's vacated slot. Victory/defeat/draw remains on screen until Continue hands the result back to the existing route flow.

The Toolkit HUD provides the CC lanes, turn banners, executing slot highlight, resolved-target description, rank captions, damage/critical/block/shield/readiness/defeat text, skip, and result overlay. The existing character art, card frames, reticle, and scene are reused. Empty skill artwork falls back to the fighter portrait without overwriting authored assets. No enemy search/strategy, run economy, or server authority changes are included.

## Validation and remaining review

- Shared core build passes. The dependency-free runner in `Tests/CombatPresentation/Run.ps1` passes 6,940 assertions across 64 complete seeded sessions.
- The actual Unity 6000.3.25f1 scene completes both player-first and enemy-first automated runs in the main repository with zero gameplay runtime errors. The harness exercises pointer drag/reset, filled action fields, execution, turn switch, and result. See `Logs/battle-main-preview.log` and `Logs/battle-main-enemy-preview.log`.
- Initial validation used an isolated copy because the original manifest referenced unavailable `com.unity.modules.physicscore2d@1.0.0`. After the user explicitly approved removing it, that entry was removed from `Packages/manifest.json` and `Packages/packages-lock.json`, and the main project now resolves packages and runs the scene. No package versions were changed. Unity's batch-mode Search indexing exception is unrelated to gameplay and is excluded from the gameplay error counter.
- Skip-to-authority checks passed for both CC and combat playback in the main repository (`Logs/battle-main-skip-preview.log`). Offscreen world/Toolkit renders are saved under `Logs/BattlePresentationEvidence`; those captures omit the uGUI world health bars.
- The preview menu uses disposable fixture stats and cannot abandon or settle the user's saved dungeon run.
- The corrected centered, elevated turn camera was visually checked in the planning capture, and the full automated battle passed with zero gameplay runtime errors (`Logs/battle-centered-camera-preview.log`).
- Manual touch/device testing and owner review of pacing, camera composition, and 7DSGC resemblance remain appropriate acceptance checks. Exact production character animation, VFX/audio, and a full authored heal/status/passive/reaction interpreter are not claimed by this change.

See `Tests/CombatPresentation/README.md` for the repeatable commands and visual checklist. No commit, PR, merge, or external team update was requested or performed.
