# Battle map optimization standard

The shared map system reduces submission overhead and hidden geometry while retaining the authored scenery, textures, material response, shadow quality and LOD thresholds. The requested draw-call and CPU budgets are acceptance targets, not a claim that every camera or combat encounter already meets them. Measurements and visual comparisons belong in [Artifacts/MapOptimization](../../../Artifacts/MapOptimization).

The current implementation uses Unity's native GPU Resident Drawer. The completed **Native** and **NativeOrdered** Windows runs are interim evidence: Teyvat's draw calls fell substantially, but its integrated-GPU frame time still exceeds the 50 ms target. Osaka's URP visual parity also remains unresolved. The final verification run and report will supersede these interim results; earlier manual-instancing experiments are not proof that the native implementation meets the budget.

## Current rendering system

The project uses **URP as its rendering standard**. The installed Unity 6 player-build validation rejects a mixed Built-in/URP quality configuration, so the project default and previously unassigned quality tiers now reference the shared URP pipeline. Existing explicit URP references remain valid. The project color space is unchanged.

`BattleTeyvat` and Osaka's `Battle` use the **Map Standard URP** quality tier through `MapRenderContext`. Renderer **0** handles terrain, and renderer **1** handles the street and its Osaka glow feature. The context configures scene cameras and restores the previous quality tier when the map unloads. Menus retain their existing UI shaders and layout under URP. `Training` also has a context with `adaptSceneStandardMaterials` enabled, which creates transient compatible copies of its Standard scene materials.

The shared pipeline enables **GPU Resident Drawer → InstancedDrawing** and its camera GPU occlusion culling. Native `MeshRenderer` and `LODGroup` ownership remains with Unity. `smallMeshScreenPercentage` is **0**, so this standard does not obtain a lower counter by dropping small scenery. BatchRendererGroup shader variant stripping is **KeepAll** to retain the native instancing variants in players.

`MapOptimizationSetup` applies a shared `MapOptimizationPolicy`, enables baked camera occlusion and retains its eligible runtime static-batching path. It preserves native per-renderer visibility rather than collapsing the map into one giant culling bound. Baked visibility and native GPU occlusion are separate systems; verify the actual rendered result rather than assuming either feature removes all hidden submissions.

`MapWindBoundsPadding` expands windy renderers' `localBounds` at runtime using a world-space padding converted through each renderer's inverse matrix. It restores the previous renderer bounds when disabled. Authored mesh assets, `LODGroup.size` and LOD transitions remain unchanged.

The Teyvat surface and foliage shaders have six DOTS pragma includes across their forward, shadow and depth passes. Those additions enable the native path without changing shading math, material values or texture sampling.

`MapLODInstancer` remains an optional stationary-mesh fallback, but the builder **disables it for the current native candidate**. Never enable it alongside GPU Resident Drawer. Its spatial cells, cached matrices and manual LOD selection describe the fallback only, not the active native architecture.

The existing Teyvat meadow renderer remains its own instanced grass system. Avoid creating a second owner for those placements.

The meadow's `frontToBack` option orders visible cells and draw groups to reduce redundant opaque overdraw. It preserves the selected placements, matrices, materials and authored LOD/fade decisions. This ordering is measured in the NativeOrdered run rather than assumed to solve the GPU budget.

## Fidelity and eligibility

- Preserve source textures, texture import settings, material values, shader behavior, render scale, shadow resolution and authored LOD transitions. Osaka uses separate compatible URP material copies; source art assets remain available for comparison.
- Assign only stationary decorative objects to cached instancing or static batching. Keep actors, moving props, particles, interaction objects and gameplay colliders outside those lists.
- Verify shader and renderer eligibility for the native drawer; unsupported content must retain a working native rendering path. Keep the DOTS/BRG variants required by that path in the player.
- For the optional manual fallback, crossfade LODs, lightmapped renderers, custom property blocks, additional vertex streams, transparent materials, unsupported probe modes and materials without enabled instancing stay on their normal renderer path. Its cached matrices, bounds and probes require `MapLODInstancer.Rebuild()` after source changes.
- Manual procedural draws do not automatically consume baked Umbra visibility. Preserve offscreen shadow casters, and review reflection blending because a cell's bounds can differ from individual renderer bounds. These are fallback-specific considerations; the active native drawer keeps Unity's renderer and LOD ownership.
- The Standard-material adapter preserves supported metallic/specular, texture and transparency settings in transient URP Lit copies; custom shaders still require their own compatibility review. Shader conversion must pass a visual comparison rather than being accepted solely because it renders without errors.

Review all six battle-facing camera directions and transitions after an optimization. A lower counter with missing shadows, loading shaders, missing foliage, exposed edges or changed shading is a failed result.

## Rebuild the two production maps

1. Save open scene edits and leave Play Mode.
2. Run **Fighting Allstar → Stage → Optimize Battle Maps**.
3. Let both background occlusion bakes finish. The builder configures the maps, saves the generated review scenes and restores the previous scene setup.
4. Inspect the setup and bake evidence, then compare the rendered maps. Confirm that the scene references populated `OcclusionCullingData` with the matching baked scene GUID. A bake request starting or finishing alone does not prove usable visibility data; the current Editor's native data-size query can report zero despite a populated saved PVS asset.

Rebake after changing occluding geometry, static renderer membership, transforms or the camera volume. Use solid terrain, cliffs and architecture as occluders; foliage and transparent water should not become opaque occlusion walls. The policy controls smallest occluder, smallest hole, backface threshold and the camera volume. Check the full action-camera envelope, not just the planning position.

Teyvat's occluder selection explicitly includes the LOD0 renderers of its **27 `SM_R3_Cliff_*` groups**. Selection follows the LOD group name, so the WP07 replacement meshes remain included. Other LOD renderers are not made occluders indiscriminately. Verify this membership after replacing or renaming cliff groups.

The builder preserves the first **Before** benchmark scenes instead of silently replacing the baseline on every rebuild. When intentionally establishing a new baseline, archive the old evidence and review its source state first.

## Add a future map

Create a scene-owned `MapRenderContext` with the shared URP pipeline, **Map Standard URP** tier, compatible actor shader and the appropriate renderer index. Assign that map's lighting owner and camera distance requirements explicitly. Use `adaptSceneStandardMaterials` only where legacy Standard scene materials need transient conversion; otherwise author compatible URP materials directly. Keep one rendering owner per loaded map lifetime; overlapping owners sharing a quality tier need an ownership review. Do not reintroduce Built-in quality tiers into the production URP build.

Add `MapOptimizationSetup` to the stationary stage root and assign a `MapOptimizationPolicy`. Keep compatible native renderers and authored LOD groups for GPU Resident Drawer; populate `staticRenderers` only for the supported static-batching path. Add `MapWindBoundsPadding` for shader-deformed foliage and assign its source LOD groups. Set padding from the actual maximum wind displacement, then check silhouette and shadow stability at camera edges.

Use `MapLODInstancer` only in a separately validated fallback configuration with the native drawer disabled. Assign its explicit sources and policy cell size, and do not lower authored LOD thresholds to meet a counter. Keep the normal renderer path for unsupported inputs.

Mark native occludees and suitable solid occluders, author the camera occlusion volume, bake the scene, and register its normal production route. The existing **Optimize Battle Maps** builder currently targets Teyvat and Osaka; extend its explicit map configuration and benchmark list when adopting another map. Adding a component alone does not add a future map to that builder.

## Before/After measurement

The generated review scenes use the same six poses for both variants: **Planning, Reverse, Left, Right, Rear and Attack**. Compare matching poses on the same machine, resolution and build configuration. Teyvat's wind clock is fixed for the comparison. The Windows benchmark requests a 1920×1080 window, disables VSync/frame-rate limiting for measurement, and warms each pose before collecting samples. Screenshots accompany the measurements so the rendering workload can be checked visually.

While the Editor is idle, `Temp/map-optimization-profile.request` starts the Editor diagnostic sequence. `Temp/map-optimization-player.request` builds the shared-URP Windows benchmark at `Artifacts/MapOptimization/Windows/MapBenchmark.exe`. It measures Teyvat Before and both maps After. The original Osaka Before scene is deliberately skipped when URP is active, because timing its incompatible Built-in materials would measure the wrong image.

For Osaka's original reference, `Temp/map-optimization-baseline.request` builds `Artifacts/MapOptimization/WindowsBaseline/MapBenchmark.exe` containing only Osaka Before. This separate build temporarily clears the default and quality pipeline references to use Built-in; the builder restores those project settings in `finally`. Run both players on the same hardware and compare their matching Osaka poses. Scene chaining proceeds only when the destination is included in the player, so the single-scene baseline can finish independently.

Generated JSON and screenshots are written under `Artifacts/MapOptimization/Editor` or `Artifacts/MapOptimization/Player`. Keep the correct Before/After and pipeline identities with exported evidence. The benchmark does not run account progression or award rewards.

### Interim Windows results

These ranges cover six completed camera poses at **1920×1080** on the **Ryzen 5 4600G / AMD Radeon integrated graphics**. They are ranges of per-pose medians, not a pooled median. See the raw [Native](../../../Artifacts/MapOptimization/Player/Native) and [NativeOrdered](../../../Artifacts/MapOptimization/Player/NativeOrdered) results for sample counts, tails and screenshots.

| Map / run | Draw calls | Estimated CPU busy | GPU | Frame time |
|---|---:|---:|---:|---:|
| Teyvat / Native | 1,520–2,107 | 72.6–92.1 ms | 72.3–91.5 ms | 72.6–91.4 ms |
| Teyvat / NativeOrdered | 1,530–2,114 | 66.3–90.1 ms | 65.7–89.4 ms | 66.0–89.7 ms |
| Osaka / NativeOrdered | 286–822 | 21.1–40.3 ms | 21.3–40.3 ms | 21.6–40.1 ms |

**Teyvat does not pass the requested 50 ms ceiling on this integrated GPU**, and some poses remain above 2,000 draw calls. Its estimated CPU busy follows GPU/frame time and must not be described as pure CPU submission work. Lower draw calls have not eliminated the GPU bottleneck. Osaka's timings fall below that ceiling in this map-only run, but the migration still needs visual parity with its Built-in reference; performance alone does not accept changed lighting or material appearance.

NativeOrdered changes ordering rather than density or authored LOD quality. Cross-run differences also include machine timing variation; use matched per-pose comparisons and the final rerun rather than treating differences between range endpoints as a measured speedup.

`MapBenchmarkVariantFilter` is active only while `MapOptimizationLab.BenchmarkBuild` is true. It removes Terrain DOTS variants because these scenes use native Unity Terrain instancing rather than terrain-shader `MeshRenderer` draws, and removes unused non-cluster stock URP Lit forward DOTS variants from these Forward+ benchmark scenes. This scoped build-time filter does not change shader math or runtime quality. Shipping builds do not activate this filter; do not apply its scene-specific assumptions to a full-game build.

Use the actual resolution and hardware recorded in the result. Check valid sample counts and instrument source fields before interpreting a value; an unavailable metric reports `-1`, not a successful zero. Recorder and FrameTiming render-thread samples have separate medians. Existing main/render aggregate fields use an available recorder with a named FrameTiming fallback.

- **Draw calls, batches and SetPass calls** describe different submission/state costs. Shader batching can reduce CPU state overhead without making all three counters fall equally. Instancer submission counters are diagnostic and do not replace the player-wide draw-call measurement.
- **CPU main elapsed** includes waits. **CPU busy** is an estimate from FrameTiming main-thread time minus present wait; it is not processor utilization and can still contain other stalls. Compare matching instruments and report median and tail behavior where available.
- **Render-thread time** and **GPU time** identify different bottlenecks. CPU and GPU work overlap, so their times should not be added as if they were sequential work. Unavailable GPU timing must remain explicitly unavailable.
- **Frame time** represents the resulting cadence and can be limited by either side. An Editor run includes Editor overhead and is diagnostic; use the Windows player to assess the target budget.

These benchmark scenes contain the map, lighting and camera workload. They exclude battle simulation, actors, combat UI and effects. Passing a map-only budget does not certify the full combat budget: finish with real dungeon battles, action cameras, effects, targeting and renderer-restoration checks, then profile representative combat on the target Windows hardware.

## Native renderer shutdown

[PlayerNativeOrdered.log](../../../Artifacts/MapOptimization/PlayerNativeOrdered.log) records buffer-finalizer warnings and native allocation reports at player shutdown. Diagnostic stacks identify GPU Resident Drawer / `InstanceDataSystem` allocations. This is observed quit-time teardown behavior; the evidence does not establish a steadily accumulating gameplay leak or a leak-free player.

`MapNativeRendererLifetime` is a localized Windows-player shutdown candidate awaiting the final dedicated player test. It registers once for `Application.quitting`, temporarily selects Disabled on the active URP asset, calls the public `IGPUResidentRenderPipeline.ReinitializeGPUResidentDrawer()` lifecycle hook, and restores the asset mode in `finally`. The installed package disposes the previous drawer before evaluating the disabled settings. It performs no per-frame work, package patching, reflection or project-setting saves. `GPUResidentDrawer.ReinitializeIfNeeded()` is deliberately not used because its body is Editor-only in this package. Do not claim the workaround resolves shutdown warnings until the final player log confirms it; remove it if that test shows unsafe teardown behavior.
