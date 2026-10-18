# WP00 — Rendering Decision and Evidence

Recorded 2026-10-18. Target platform confirmed by the user: Windows PC.

## Decision

Use **URP 17.3.0, Forward rendering, Linear color space** as the foundation for the next Teyvat material-development sample. The isolated foundation comparison ran successfully. Retune light, ambient contribution and surface colors together in WP02; the existing Gamma palette is not an approved Linear palette.

The main gameplay project remains on its existing Built-in/Gamma rendering configuration. Installing URP alone did not activate it. This decision accepts a foundation for look development; it does not declare the entire game migrated or its existing effects compatible.

The isolated project is `.utmp/TeyvatRenderingTrial/`. Its main copied layout is `Assets/Project/Scenes/EnvironmentReview/TeyvatGrassland/TG_LayoutReview.unity`. It contains the actual trial URP renderer/pipeline assets under `Assets/Project/Art/Environment/TeyvatGrassland/Foundation/Settings/` and trial-only converted blockout material copies.

The foundation review scene in the main project remains usable with its current Built-in materials. WP02 should develop the final shader/material families against the accepted URP setup; replacing the foundation materials is expected. Before activating URP across the main game, validate or port the current stage, character materials, UI/world effects and camera rendering through full playback.

## Verified baseline

| Property | Observed value |
| --- | --- |
| Editor | Unity 6000.3.4f1 |
| Installed URP package | 17.3.0 |
| Main active renderer | Built-in |
| Main color space | Gamma |
| Main quality level | Ultra at baseline capture |
| Initial comparison system | AMD Ryzen 5 4600G, AMD Radeon integrated graphics, approximately 16 GB system memory |
| Product platform | Windows PC |

The reported approximately 8 GB graphics memory is a driver-reported value for integrated graphics; it is not a dedicated-VRAM specification. Minimum supported PC hardware and frame-rate goals remain undecided, so player-build performance acceptance is pending.

Baseline scene captures are in `Artifacts/TeyvatGrassland/WP00/Baseline/`. They were rendered from a preview copy of the saved Battle scene, including a capture using the actual current planning position/focus formula. They do not overwrite the open user scene.

## Comparison performed

The same exported foundation scene was rendered in a separate Unity process under:

- Built-in/Gamma.
- URP/Gamma with trial-only blockout material mapping.
- URP/Linear with the same mapped materials and target-light setup.

Each configuration has the registered camera set and static camera-envelope report. The URP configurations additionally have original/candidate probes for representative material copies. Evidence is in `Artifacts/TeyvatGrassland/WP00/RenderingTrial/`.

The fresh trial initially waited on remote package resolution. The repeatable launcher now references the exact already-installed package sources in the main project's package cache, including transitive dependencies. It excludes the Unity MCP connector from the trial and does not edit those cached source packages.

The trial completed and exited. Visual inspection found an incorrect all-blue first Linear camera frame after the in-process color-space change. The capture helper now warms the pipeline before retaining a frame; the entire Linear camera set was recaptured in a fresh process, and the corrected planning image was inspected. The discarded image is retained only under `RenderingTrial/Diagnostics/` for diagnosis. The precise internal cause of the first-frame issue was not established.

## Material compatibility results

| Representative sample | URP probe result | Consequence |
| --- | --- | --- |
| Lumine, Amber and Kyo material copies using Standard | Original copies render error-pink; simple URP/Lit mappings remove error-pink in both color spaces | Convert/test actual runtime character material instances before main-game migration |
| Actor Cryo, Actor Status Particles and Actor Visual Effects shaders | No error-pink in the simple sphere probes | Keep full effect playback, opacity, vertex data, sorting and depth behavior pending |
| Ultimate Ready Aura shader | No error-pink in the simple probe | Validate real aura geometry, blending and combat layering separately |
| Osaka Neon and hidden Glow shader | No error-pink in the simple probes | Verify the real scene/effect rendering path; a sphere probe is not a post-effect or Osaka scene parity test |

The Standard candidate mapping copies base color, base texture and smoothness to URP/Lit. It is a diagnostic mapping, not a complete general-purpose material converter. No original character material was converted in the main project.

The current Genshin roster uses the shared placeholder prefab and material references. These tests do not establish appearance or performance for detailed future character meshes, hair, rigs or animation.

## Starting rendering settings

| Setting | Trial value | Validation and adjustment before production |
| --- | --- | --- |
| Renderer | URP Forward | Check actual shader/effect requirements and player-build GPU cost before introducing extra passes |
| Color space | Linear | Recalibrate neutral/afternoon materials and lighting together in WP02 |
| HDR | Enabled | Retain when exposure/highlight handling warrants its measured cost |
| Pipeline MSAA | 4 samples | Starting value; compare real Game View/player edges and GPU cost before accepting |
| Shadow distance | 80 world units | Starting value; inspect cliff/tree shadow loss and raise or reduce from measured visual/cost needs |
| Shadow cascades | 2 | Starting value; review near/far transitions in both team views |
| Scene depth texture | Enabled | Starting feature provision for later water tests; measure and scope to actual need |
| Opaque texture | Disabled | Enable only if the selected water/refraction implementation needs it |
| Post-processing | Disabled | Keep baseline comparisons legible; final mood belongs to material/lighting development |

Capture images use fixed offscreen targets. Their render-texture sampling does not independently establish the effective pipeline-MSAA quality in the player. Re-evaluate anti-aliasing in the real display path. There is no real-time reflection camera, volumetric fog claim, finished water shader or foliage system in this foundation trial.

## Handoff and remaining work

The rendering foundation is ready for WP02's small finished material sample. Use the same layout, camera registry and visual brief for comparisons. Introduce one representative rock, ground/grass transition, canopy, flower group and water/waterfall segment before increasing asset count.

Still pending: neutral/afternoon material calibration, detailed actor/effect playback in URP, final whole-game migration, target-PC player-build profiling, and final runtime camera/animation validation. Those are explicit follow-on checks, not claimed completed by these static foundation captures.

## Reproduction

In the main Unity Editor, run `Fighting Allstar > Teyvat Foundation > Build WP00-WP01 Review`, then `Export Isolated Trial Inputs`. Run `Tests/Environment/RunTeyvatRenderingTrial.ps1` from the project workspace. The launcher starts a hidden batch editor for the isolated dependency project and records its output/log location. The trial refuses to configure rendering if the source gameplay Battle scene is present in its project.

For an already completed trial, `TeyvatFoundationBuilder.RunIsolatedLinearReview` can repeat only the Linear camera capture in that isolated project. The launcher/export and camera-capture helpers are versioned with the foundation code; package reflection for renderer creation is specifically checked against the installed URP 17.3 API.
