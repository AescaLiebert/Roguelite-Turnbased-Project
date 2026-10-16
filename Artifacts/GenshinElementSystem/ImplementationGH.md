---
slug: genshin-element-system-gh-evidence
status: approved
gdd_tags: [elemental-reactions, mechanics, ai, architecture, actor-camera]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# G–H implementation and Practice repair

G is implemented. H's automated Windows/editor integration checks are executed; Android runtime acceptance is **pending at the owner's request**, and human feel/balance acceptance remains open. This report extends [D–F evidence](ImplementationDF.md).

**Subsequent owner revision:** [current presentation/catalog revision](PresentationRevision.md) removes prediction UI and the separate Genshin shortcut, uses the Series dropdown, fixes full-catalog dungeon validation and limits the new FCT to Genshin source fighters. Historical screenshots/counts below describe the earlier G–H delivery.

## Practice failure and fix

All five fighters were present and runtime-ready in the 39-character registry. The actual launch failure was `TrainingContext.Prepare`: it built a validation catalog without elemental rules/status recipes. Validation then rejected the authored elemental skills. Practice now supplies the standard versioned rules and recipes before validating the selected fighter and dummy.

The picker now has a **GENSHIN IMPACT · 5** shortcut, a readable series filter, visible scrolling, and an All Fighters reset. Both shortcuts clear conflicting filters. Constellation choices follow authored tiers; a stale/programmatic C6 request is clamped to C5. Existing 1v1, infinite HP, free ultimate, no-reward and inventory-isolation rules remain active.

![Five selectable Genshin fighters](Practice/01-genshin-picker.png)
![Barbara search](Practice/02-barbara-search.png)

## Enemy planning

- `AiPlanningObservation` copies the acting side's hand and observed fighters. It excludes opponent cards, event history and live combat/card RNG streams. Planning never changes the authority snapshot.
- Ordered elemental candidates are considered alongside ordinary plans. Pair examination stops at 512 combinations; at most half the candidate budget is reserved for elemental plans. An eight-plan outcome shortlist keeps conversion and ordinary options when available; its configurable hard range is 2–16.
- The combat authority evaluates those detached plans against three independent synthetic sample streams. Scores use actual HP/shield harm, healing/self-harm, defeats, new protection and the signed change in retained DOT/control. Consuming a strong existing Shock snapshot can therefore lose to a physical attack.
- Starting valuation: future DOT horizon up to four ticks, capped by visible victim HP/shield; residual DOT/control discounted by 60%; shield changes valued at 50%; full-card disable valued against visible ATK; gauge denial valued at half the equivalent disabled-turn fraction. These are bounded heuristics for testing, rather than exact opponent-turn predictions. Periodic damage resistance/cap changes, future healing/buff synergies and rare proc probabilities warrant further tuning.
- Invalid plans cannot win the selector fallback. Existing non-elemental tactical tests remain supported. Diagnostic explanations report expected contact damage and retained DOT/control/protection.
- Public forecasts now carry conditional pure status preparation into subsequent contacts. Preparation itself never produces an automatic reaction.

Reaction captions also play distinct local cues at the recorded impact, reusing the existing presentation audio source and cleanup. The cue is a prototype sound; human audio/feel approval remains open.

## Executed checks

| Check | Evidence |
| --- | --- |
| G–H plus existing tactical AI | **543 assertions:** 19 ordering/retention/privacy/forecast checks, 504 integrated save/replay/privacy checks across 24 turns, 20 existing tactical AI assertions. [Raw result](VerificationGH.txt). |
| A–F feature suite | **440 assertions:** 264 A–C, 89 expansion/DOT/forecast, 87 kit assertions. |
| Practice | **391 checks**, all five saved Unity assets, C0/C5 and stale C6 clamp, **105 authored card plays**, filters/search and inventory isolation. [Result](Practice/result.txt). |
| Existing status group | **101 assertions passed.** |
| Existing Practice/training group | **2377 assertions passed.** |
| Dungeon/combat/save group | **9663 assertions passed** on this run; the count varies with battle length after intentional AI choices change. |
| Character descriptions | **4031 assertions; 39 characters, 468 ranks/tiers.** [Result](../CombatText/Review.md). |
| Presentation | **28 checks**: contact/caption frames, forecast, fractional pip, distinct audio generation, audio stop and interrupted-effect cleanup. [Result](Feedback/result.txt). |
| Shared/server/Unity | Shared and server Release builds; production Unity runtime compilation and saved-kit export checks. |
| Android | Android module/SDK available; `adb devices -l` found no device. Owner explicitly chose to leave runtime testing pending. No Android runtime or APK-release claim. |
| Unrestricted harness | The previously reproduced first-hit timing failure remains a separate baseline limitation; this is not an entirely green full-suite claim. |

Planning timing is recorded in the raw desktop fixture result. It includes the configured bounded search and is not an Android benchmark. Further performance tuning should preserve both conversion and retention coverage before reducing sample/search depth. The isolated visual editor has also reproduced Unity's internal SearchDatabase startup exception; its fixture completed, while production-editor console checks are recorded separately.

## Human acceptance script

1. New player/readability: open Practice, select the Genshin shortcut, find all five, launch a fighter and explain the full-team reaction condition. Predict setup/trigger outcomes in ten prepared situations; starting target is eight correct. If prediction fails, improve wording/preview before increasing damage.
2. Skill: compare reversed setup/trigger orders at equal progression, then a valuable Shock case where retaining DOT wins. Explain the spent setup and why the enemy chose its sequence.
3. Stress/abuse: rapidly reset/reorder, restart or exit during contact and impact; repeat Freeze/recovery and Geo accumulation. Confirm finite output, capped shields, restored controls and no lingering sound/effects.
4. Observer/audio: distinguish ordinary elemental damage, contact, reaction and immune output by text, color and sound. Check crowding during AOE and reduce cue overlap if the sequence becomes unclear.
5. Android: on a connected target, repeat taps/holds/cancel, narrow-screen forecasts, skip/resume and sound/cleanup checks. Record device/build, latency and readability. This step is pending.

No acquisition pool, summon banner, official artwork, production build setting, dependency, deployment or commit was changed as part of G–H.
