# Shared UI presentation motion

The player can open, inspect, dismiss and reopen UI without waiting for an entry animation. An exit rejects duplicate interaction immediately, then fades and slides before hiding or removing its content. Opening again cancels that pending exit. All motion uses unscaled time so menus continue responding while combat is paused. Scene roots remain stationary; scene changes use a black overlay that fades to black before loading and fades back out after the new scene is ready.

## Presentation rules and starting values

`ScreenMotion` uses cubic ease out, a short upward slide and a gentle scale toward the authored geometry. Starting values are 320 ms entry, 180 ms exit, 16 px entry travel, 10 px exit travel and 1.5% entry scale offset. These are tuning values, not a balance or timing standard. Panel entry adds a brief gold sparkle; modals place it on their content; Canvas entry adds a subtle warm flash. Per-item `Reveal` only fades/slides/scales so a staggered grid does not emit many overlapping particle effects. Stat pulses use a larger scale that settles over 520 ms.

Artifact lookup is the deliberate exception: it retains the requested slide from the left, with a fading grid. Its workshop host and docked panel are absolute overlays inside the artwork column; opening lookup leaves the workshop control column's position and size unchanged.

Clarity: content remains readable after motion settles and original opacity/geometry are restored. Response: entry keeps input live; dismissal disables duplicate interaction; rapid reopening cancels stale completion. Satisfaction: fade, motion, scale and a small sparkle/flash provide distinct visual feedback. Fit: short restrained movement keeps ordinary UI quieter than existing combat and summon cinematics. Motivation: the underlying equipment, reward and upgrade outcomes remain persistent; animation callbacks do not decide whether a reward is saved.

State transitions: hidden/detached → entering → visible → exiting → hidden/detached. Enter and pulse may replace pending motion. Detached Toolkit elements cancel scheduled callbacks and restore their saved presentation state. Canvas targets destroyed during motion stop updating. Exit completion only fires for the current, attached presentation. Scene navigation has one pending scene transition at a time.

## Source coverage

| Surface | Integration |
| --- | --- |
| Scene transitions | Persistent black overlay fades to black in 180 ms, stays above both scenes during loading, then fades out in 320 ms. |
| Scene UI Toolkit roots and root Canvases | No root fade, scale or slide. Only panels inside scenes use panel motion. |
| Menu → Summon / Dungeon / Training | FrontEnd keeps asynchronous preload, then fades the black overlay to cover before activation. |
| Other scene navigation | Loader, local economy, dungeon entry/map/flow, formation, training and battle return paths call `ScreenMotion.LoadScene`. |
| Collection ↔ Home ↔ Fighter Details | Shared entry/exit, including outgoing flex pages leaving layout during exit. |
| Fighter detail tabs and artifact stage | Shared entry/exit; outgoing pages are removed from flex layout during their fade. |
| Fighter additional stats / skill / passive dialogs | Shared modal entry/sparkle and delayed dismissal with focus restoration. |
| Backpack | Shared modal entry and dismissal; unsubscribes immediately when closing. |
| Item and artifact tooltips | Shared modal entry, cancel-safe dismissal and focus restoration. |
| Training character picker | Shared modal entry/dismissal; scene selection uses shared scene exit. |
| Summon details / selectors | Shared modal entry; content stays rendered until exit completes. |
| Summon presentation | Existing cinematic timeline retained; outer overlay entry/dismissal uses shared motion. Disposal remains immediate cleanup. |
| Dungeon configuration / enemy details / victory reward dialogs | Shared modal entry and delayed dismissal. Reward acknowledgment remains saved before dismissing. |
| Same scene dungeon entry ↔ route map | Root documents switch immediately without scene-wide animation. |
| Formation fighter inventory | Canvas entry/flash and delayed exit; reopening cancels stale deactivation. |
| Battle ally picker | Shared modal entry and dismissal. |
| Battle character inspector and nested card popup | Canvas fade, slide, scale and flash on entry; delayed exit. |
| Battle held-card tooltip and battle result | Shared modal entry/dismissal. Existing battle completion scene return uses scene exit. |
| Legacy battle CC opening | Shared Canvas content entry/exit and portrait entry; counting and holds use unscaled time. |

The existing battle hand, card execution, ultimate cut-in, turn/combat-class banners, floating combat numbers and status feedback keep their specialized motion. Inline content visibility changes such as an empty-state label, missing portrait, selected badge, coin icon or authoring-time initialization are not separate navigation surfaces. Editor-only previews and teardown paths remove objects immediately.

## Verification

Run `Tests/CombatPresentation/RunArtifactImprovementsUiChecks.ps1 -MotionChecks` to create a disposable project at `Temp/ScreenMotionUiProject`. It never opens or saves the main project's scenes or account files. The Play mode fixture covers Toolkit and Canvas panel motion while `Time.timeScale == 0`, root stability, same-scene dungeon document switching, black-overlay fade timing/input blocking, and a real asynchronous scene A → B → A roundtrip using one persistent driver. PNG frames and the assertion report are saved to that project's `Artifacts/ScreenMotion` folder.

The artifact presentation fixture separately exercises shared Toolkit reopening while validating the full workshop flow at 1920×1080 and 1280×720. Its source checks do not establish full visual coverage of every non-artifact screen.

Manual smoke pass: open each surface in the coverage table, dismiss it immediately, reopen before exit ends, and repeat rapidly. Confirm no blank blocker, stale focus jump, stacked tab layout, duplicate resource transaction or stopped animation while paused. New player check: infer opening/closing and confirmation state from the visible content. Stress/abuse check: five rapid reversals and repeated confirm inputs must produce one action. Readability check: watch a scene transition and one reward popup without controlling it; content must remain legible after settling. If input feels delayed, reduce travel/duration before adding stronger effects; if the transition is unreadable, lengthen entry while retaining immediate input.

The previous isolated Play mode run passed **42 assertions** before the black-overlay transition policy changed. The fixture has been updated for the new behavior but has not been rerun. The updated code compiles cleanly. A source coverage table is not a claim that every listed screen has been visually inspected.
