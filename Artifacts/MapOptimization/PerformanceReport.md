# Native Windows map performance report

Generated 2026-10-19T15:27:32.003785+00:00. **Status: partial completion.**

The Teyvat draw-call work is measurable, but its GPU/frame cost remains a separate limit. The Osaka URP candidate retains scenery and working shaders at the reviewed positions; its original wet-pavement, diffuse and ambient appearance is not fully matched. [Osaka visual differences](OsakaVisualReview.md) prevent a zero-quality-change or pixel-identical claim.

## Measurement scope

AMD Ryzen 5 4600G APU / integrated AMD Radeon Graphics; 1920 × 1080; Direct3D 11; Unity 6000.3.4f1 Windows Development player. These are controlled **map-only** six-camera scenes, with no real battle simulation, HUD, combat VFX or representative final characters. The probe disables VSync, uses an uncapped target frame rate, warms each pose and records 90 requested frames. Results characterize this hardware/run, not a general Windows performance guarantee.

CPU main is a main-thread **wall-time** marker and includes waits. Estimated busy is FrameTiming `cpuMainThreadFrameTime − cpuMainThreadPresentWaitTime`; it can retain other GPU/render synchronization waits. Neither value isolates pure CPU computation. Main and render instruments remain separate; their samples must not be added together. GPU uses FrameTiming; frame wall time uses `Time.unscaledDeltaTime`. Unavailable values appear as N/A. Render-thread/GPU/frame P95 values were not recorded and are not inferred.

In the tables, B → A means Before → After. Draw/main/busy cells contain median / P95. Changes compare matching camera views, not pooled or recomputed percentiles.

## Teyvat Arena

Before and After use the Teyvat URP scene; After includes the final native renderer/culling configuration and depth-ordered meadow submissions.

| View | Draw calls B → A (median / P95) | Median draw Δ | Main wall ms B → A (median / P95) | Estimated busy ms B → A (median / P95) |
| --- | --- | --- | --- | --- |
| Planning | 3,120.00 / 3,120.00 → 1,891.00 / 1,891.00 | -39.4% | 72.68 / 96.15 → 65.62 / 75.59 | 71.86 / 96.07 → 65.61 / 73.97 |
| Reverse | 4,607.00 / 4,607.00 → 2,114.00 / 2,114.00 | -54.1% | 82.41 / 95.88 → 82.85 / 94.63 | 82.87 / 101.38 → 83.20 / 94.61 |
| Left | 3,937.00 / 3,937.00 → 1,888.00 / 1,888.00 | -52.0% | 69.75 / 79.44 → 74.94 / 88.59 | 69.81 / 79.42 → 74.77 / 88.33 |
| Right | 3,694.00 / 3,694.00 → 1,720.00 / 1,720.00 | -53.4% | 76.45 / 106.02 → 75.09 / 92.41 | 76.44 / 106.00 → 75.08 / 91.95 |
| Rear | 4,293.00 / 4,293.00 → 1,530.00 / 1,530.00 | -64.4% | 88.18 / 99.36 → 90.83 / 109.69 | 88.46 / 100.38 → 91.30 / 111.49 |
| Attack | 3,830.00 / 3,830.00 → 1,890.00 / 1,890.00 | -50.7% | 86.49 / 99.82 → 78.66 / 88.99 | 84.47 / 99.80 → 79.23 / 89.92 |

| View | Render thread ms B → A (median) | GPU ms B → A (median) | Frame wall ms B → A (median) | Batches B → A | SetPass B → A |
| --- | --- | --- | --- | --- | --- |
| Planning | 9.82 → 8.97 | 71.83 → 65.42 | 72.69 → 65.63 | 3,120.00 → 1,891.00 | 85.00 → 91.00 |
| Reverse | 13.08 → 11.83 | 82.23 → 82.53 | 82.47 → 82.90 | 4,607.00 → 2,114.00 | 86.00 → 85.00 |
| Left | 9.82 → 9.71 | 69.69 → 74.65 | 69.81 → 75.01 | 3,937.00 → 1,888.00 | 86.00 → 87.00 |
| Right | 11.32 → 9.90 | 76.24 → 74.83 | 76.49 → 75.10 | 3,694.00 → 1,720.00 | 86.00 → 83.00 |
| Rear | 10.66 → 10.30 | 88.04 → 90.60 | 88.23 → 90.87 | 4,293.00 → 1,530.00 | 84.00 → 84.00 |
| Attack | 8.87 → 8.25 | 86.46 → 78.90 | 86.56 → 78.69 | 3,830.00 → 1,890.00 | 86.00 → 88.00 |

After: median draws 1,530–2,114; 5/6 views have median draws ≤2,000 and 5/6 have P95 draws ≤2,000. Median GPU frame time is 65.42–90.60 ms. 6/6 views exceed 50 ms GPU; 6/6 exceed 50 ms frame wall time.

### Samples and instruments

R/FT below means ProfilerRecorder / FrameTiming sample counts. Main and render sources are shown explicitly. CSV and JSON preserve all per-instrument counts and independent recorder/timing statistics.

| View / run | Requested / frame | Draw / batch / SetPass | Main source (R/FT) | Busy | Render source (R/FT) | GPU / FT |
| --- | --- | --- | --- | --- | --- | --- |
| Planning / before | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |
| Planning / after | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 89 / 90 |
| Reverse / before | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |
| Reverse / after | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |
| Left / before | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |
| Left / after | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |
| Right / before | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |
| Right / after | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |
| Rear / before | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |
| Rear / after | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |
| Attack / before | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |
| Attack / after | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |

## Osaka

Before is the separately captured original Built-in Osaka player; After is the shared-URP candidate. Visual parity remains unresolved.

| View | Draw calls B → A (median / P95) | Median draw Δ | Main wall ms B → A (median / P95) | Estimated busy ms B → A (median / P95) |
| --- | --- | --- | --- | --- |
| Planning | 972.00 / 972.00 → 822.00 / 822.00 | -15.4% | 39.09 / 44.12 → 31.35 / 36.15 | 38.88 / 44.54 → 31.42 / 36.13 |
| Reverse | 556.00 / 556.00 → 418.00 / 418.00 | -24.8% | 35.53 / 44.28 → 28.19 / 31.37 | 36.04 / 44.27 → 28.17 / 31.36 |
| Left | 402.00 / 403.00 → 324.00 / 324.00 | -19.4% | 23.84 / 28.85 → 21.35 / 23.72 | 23.64 / 28.83 → 21.16 / 23.71 |
| Right | 407.00 / 407.00 → 323.00 / 323.00 | -20.6% | 25.14 / 34.39 → 21.29 / 27.45 | 24.98 / 34.37 → 21.21 / 27.43 |
| Rear | 369.00 / 369.00 → 286.00 / 286.00 | -22.5% | 23.54 / 29.54 → 19.83 / 22.85 | 23.93 / 29.81 → 19.82 / 22.84 |
| Attack | 748.00 / 748.00 → 641.00 / 641.00 | -14.3% | 38.79 / 50.60 → 29.77 / 36.69 | 39.07 / 50.58 → 29.75 / 36.67 |

| View | Render thread ms B → A (median) | GPU ms B → A (median) | Frame wall ms B → A (median) | Batches B → A | SetPass B → A |
| --- | --- | --- | --- | --- | --- |
| Planning | 2.73 → 1.52 | 38.69 → 31.30 | 39.09 → 31.37 | 708.00 → 805.00 | 619.00 → 24.00 |
| Reverse | 2.07 → 1.18 | 35.65 → 28.00 | 35.55 → 28.26 | 466.00 → 409.00 | 410.00 → 24.00 |
| Left | 1.51 → 0.99 | 23.72 → 21.09 | 23.87 → 21.39 | 294.00 → 316.00 | 254.00 → 23.00 |
| Right | 1.78 → 1.05 | 24.91 → 21.35 | 25.08 → 21.33 | 298.00 → 314.00 | 267.00 → 24.00 |
| Rear | 1.66 → 1.00 | 23.52 → 19.69 | 23.56 → 19.87 | 320.00 → 282.00 | 273.00 → 24.00 |
| Attack | 2.49 → 1.50 | 38.66 → 29.68 | 38.82 → 29.79 | 612.00 → 631.00 | 549.00 → 24.00 |

After: median draws 286–822; 6/6 views have median draws ≤2,000 and 6/6 have P95 draws ≤2,000. Median GPU frame time is 19.69–31.30 ms. 0/6 views exceed 50 ms GPU; 0/6 exceed 50 ms frame wall time.

### Samples and instruments

R/FT below means ProfilerRecorder / FrameTiming sample counts. Main and render sources are shown explicitly. CSV and JSON preserve all per-instrument counts and independent recorder/timing statistics.

| View / run | Requested / frame | Draw / batch / SetPass | Main source (R/FT) | Busy | Render source (R/FT) | GPU / FT |
| --- | --- | --- | --- | --- | --- | --- |
| Planning / before | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |
| Planning / after | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |
| Reverse / before | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |
| Reverse / after | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |
| Left / before | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |
| Left / after | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |
| Right / before | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |
| Right / after | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |
| Rear / before | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |
| Rear / after | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |
| Attack / before | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |
| Attack / after | 90 / 90 | 90 / 90 / 90 | ProfilerRecorder (90/90) | 90 | FrameTiming (0/90) | 90 / 90 |

## Acceptance and remaining work

The approximately 2,000-draw goal is a draw-submission goal, not an FPS or pure CPU-time guarantee. Teyvat's remaining high GPU/frame cost requires GPU-pass analysis; draw reductions alone do not close it. Do not report the 16.67 ms preferred / 50 ms maximum wall-time goals as passed when matching views exceed them.

Osaka appearance calibration remains open. The original Built-in baseline was captured separately; comparison with the URP candidate changes the rendering pipeline as well as the optimization path. Do not attribute its performance difference solely to culling/batching, or describe the migration as visually identical.

Fixed screenshots cannot prove motion, every occlusion transition or arbitrary production camera/actor/aspect envelopes. The final scene audit distinguishes the six benchmark positions, authored review cameras and dynamic runtime limits.

## Final player cleanup

The [final quit log](PlayerFinal.log) contains 0 ComputeBuffer GC-disposal warnings, 0 TempJob leak warnings and 0 Persistent leak warnings. Its MemoryLeaks summary still reports 599,628 residual allocated bytes, mostly under Manager. This is a scoped cleanup improvement and **not a zero-leak claim**; label totals do not establish which component owns each residual allocation. NativeOrdered and other intermediate diagnostic runs are excluded from the final performance tables.

## Supporting evidence

- [BoundsValidation.json](BoundsValidation.json): native GPU-driven renderer reads padded bounds while source mesh remains unchanged (pass reported).
- [FinalSceneAudit.json](FinalSceneAudit.json): saved cliff occluders, native rendering controls, tree wind padding, camera-volume scope and Teyvat screenshots (pass reported).
- [MeadowOrderValidation.json](MeadowOrderValidation.json): exact full mesh/material/submesh/LOD/matrix multiset at all six poses with ordering toggled (pass reported).
- [ContextToggleValidation.json](ContextToggleValidation.json): transient context enable/disable restores source materials, probes and quality (pass reported).
- [TrainingValidation.json](TrainingValidation.json): Training opens in Planning with supported actor materials (pass reported).
- [MenuReturnValidation.json](MenuReturnValidation.json): return to MainMenu with its UI and expected quality (pass reported).
- [PlayPassed.txt](../TeyvatGrassland/PlayableDungeon/PlayPassed.txt): earlier disposable Genshin dungeon encounter/card/result integration smoke; no account saves (pass reported).
- [OsakaVisualReview.md](OsakaVisualReview.md): six-view native Osaka visual comparison and outstanding material/lighting differences (see scoped review).
- [WindowsBuild.txt](WindowsBuild.txt): latest native benchmark player build result (see scoped review).
- [PlayerFinal.log](PlayerFinal.log): final Direct3D11 player startup, shutdown and qualified residual allocation record (see scoped review).

The dungeon smoke is historical integration evidence from the playable-arena task; it is not a new full-combat performance run after this renderer migration. Current transient context, Training and menu-return fixtures cover only their stated scopes. The final report does not supersede unresolved visual or GPU findings.

## Input provenance

| Run | Native GPU occlusion active | Source | SHA-256 |
| --- | --- | --- | --- |
| BattleTeyvat / Before | False | [Player/Native/BattleTeyvat_Before.json](Player/Native/BattleTeyvat_Before.json) | `c4ecd771180e90003468b98ffba2268b47d0aff9e86e9d99e6226aae787c769e` |
| Battle / Before | False | [Player/OsakaBaseline/Battle_Before.json](Player/OsakaBaseline/Battle_Before.json) | `95a4670edca587ca96afee8856830e90bb04308c48dcce7c1530fbf4f3501596` |
| BattleTeyvat / After | True | [Player/Final/BattleTeyvat_After.json](Player/Final/BattleTeyvat_After.json) | `1495ab679719f76d111d7db935da7cb9e2850981f1945a66bd22de6c880c27d5` |
| Battle / After | True | [Player/Final/Battle_After.json](Player/Final/Battle_After.json) | `e6e509aeb2ea7710532aeef7aaded53e3f250fd263299924e18d05aa450365d2` |
