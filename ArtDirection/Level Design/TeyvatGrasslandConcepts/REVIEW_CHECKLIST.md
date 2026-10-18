# Visual, Combat and Runtime Review

Status: acceptance procedure for future implementation. No checks below have been executed against a built Teyvat stage yet.

## Capture procedure

Use the accepted camera records, fixed viewport/aspect ratio, renderer settings, light direction, exposure and wind state. Record those settings with each capture set. Capture neutral-light and target-afternoon versions of material samples. Capture planning and attack views with the actual project characters on the integrated stage.

Use the same camera pose for before/after comparisons. The generated concepts are composition targets, not exact lens/geometry calibration. Align the main masses before judging small surface details. Keep clean art-direction captures free of UI; test overlays separately in the real game.

Capture player planning, enemy planning, rear attack, near-contact attack, character portrait, multi-target action, ultimate and the camera movement between shots. Include both ends of the stage and the worst observed occlusion/overdraw views. Store short motion evidence for foliage, waterfalls, water highlights and camera transitions; still images cannot prove temporal quality.

## Visual comparison scorecard

Mark each row Pass, Revise or Pending, attach the relevant captures and explain revisions in concrete terms. Do not combine these into an unsupported similarity percentage.

| Criterion | Pass condition | Likely owner when it fails |
| --- | --- | --- |
| Composition | Clearing, left canopy, right ruin, waterfall cliffs and distant landmarks retain the reference hierarchy | WP01 / WP07 |
| Scale and silhouette | Characters, grass, arch, canopy and cliff planes share a coherent scale; no primitive-looking hero shapes | WP03 / WP05 |
| Ground/grass transition | Root hues and ground coverage blend without a conspicuous border or abrupt distance ring | WP02 / WP04 |
| Meadow richness | Flower patches and varied grass frame the arena while the center remains quiet | WP04 / WP07 |
| Canopy volume | Trees read as soft connected masses in light and shadow, without dominant card edges or black backs | WP02 / WP05 |
| Rock character | Broad designed planes and ledges dominate over fine noise; grass caps sit convincingly | WP03 |
| Water | Turquoise depth, restrained glints, directional flow and bank contact read across registered cameras | WP06 |
| Waterfall | Flow, breakup, impact foam and mist feel integrated rather than stacked opaque sheets | WP06 |
| Light and color | Warm light, cool readable shadows and clear atmospheric depth match the intended mood | WP08 |
| Temporal stability | Foliage, highlights, shadows and distance transitions avoid distracting flicker, popping or culling | Owning lane / WP09 |
| Reverse and close views | Opposing planning view and close attacks show complete geometry and acceptable detail | WP01 / WP07 |

When the image looks wrong, diagnose layout/scale first, then silhouettes, material behavior, light and atmosphere, water/effects, and finally small dressing. A color grade does not fix incorrect canopy normals.

## Combat and player-experience checks

Active design domains are camera and combat presentation. No new combat state machine is proposed by this environment workflow.

| Design filter | Application to this map | Review evidence |
| --- | --- | --- |
| Clarity | Fighters, targets, feet, weapon motion and hit feedback remain distinguishable from flowers, foam and foliage | Full action playback from both acting sides |
| Response | New scenery introduces no camera blockage or performance hitch that makes existing actions feel delayed | Transition playback and target-device frame-time record |
| Satisfaction | Water/foliage movement enrich the scene while character impacts remain dominant | Weak/strong action comparison with normal game audio |
| Fit | Rock, grass, trees, water and lighting belong to the same bright stylized world | Shared neutral/afternoon comparisons |
| Motivation | The stage supports the encounter's identity; it introduces no new rewards or progression rules | Encounter-context review; no economy change required |

The environment does not add new combat feedback rules. When reviewing existing hit feedback, retain visual and audio confirmation; ambient water/wind should not obscure them.

- New-observer test: show normal battle playback without explaining the environment. The observer can identify the acting fighter and target without foliage/effects misleading them. Record failures and the specific obstruction.
- Stress test: run the heaviest representative multi-target effects with water, mist, grass, wind and shadows active; rapidly traverse available camera transitions. Look for sorting errors, bounds culling and frame-time spikes.
- Action-readability test: compare distinct existing actions from close and planning cameras. The environment preserves the differences visible in the baseline stage.
- Boundary/abuse test: exercise knockbacks, actor extremes and available camera excursions. The environment cannot push actors onto a wrong ground surface, expose a scenery hole, or create camera trapping that changes the encounter.
- Occlusion test: pause throughout camera transitions, not only at endpoints. No grass clump, trunk, cliff or ruin hides the selected target or fills the camera unexpectedly.
- Skill-test applicability: this map introduces no new player mastery mechanic. Verify that existing timing/target feedback remains perceivable instead of inventing an unrelated skill system.

## Runtime evidence

Record actual CPU/GPU frame times, spikes, mesh/texture memory, visible geometry/draw activity, shadow work, foliage overdraw, water transparency cost and any extra reflection-camera cost. Include build type, hardware, display resolution, rendering settings, camera and action. Separate editor measurements from player-build results.

Any chosen numeric budget or artistic density is a **starting value** until it is tested. Each such entry uses this form:

| Setting | Starting value | Device and view | Pass/fail condition | Adjustment if it fails |
| --- | --- | --- | --- | --- |
| Populate during implementation | Record actual selected setting | Named hardware and repeatable camera/action | Recorded project goal plus visual comparison | Change the measured bottleneck and recapture |

Do not fill this with invented universal limits. If target hardware or frame-rate expectations are unknown, mark performance acceptance Pending. Establish provisional measurements and keep the visual work moving.

When optimizing, measure first and change the relevant factor: coverage/overdraw, shadow settings, instance route, material samples, transparent layers, extra-camera passes, texture residency or geometry. Keep before/after images so a performance improvement does not silently remove the intended look.

## Handoff and completion

- Editable Blender and texture sources open with their required dependencies.
- Mesh exports preserve scale, orientation, UVs, vertex masks and intended normals in Unity.
- Prefabs/materials reference stable assets and include required `.meta` files.
- Stage modules have no accidental preview lights, cameras or gameplay-controller duplicates.
- Procedural generation reproduces the accepted distribution using saved masks, settings and seeds.
- The final prefab preserves combat anchors, ground height and current battle behavior.
- Clean camera captures and motion evidence cover the registered view set.
- The named target-device player build meets recorded runtime goals, or the pending decision is explicitly stated.
- Known limitations, package versions, reproduction steps and fallback settings are included.

An accepted visual sample closes look development for that sample; it does not imply that the full stage, reverse views or player-build performance have passed. Mark each milestone separately.
