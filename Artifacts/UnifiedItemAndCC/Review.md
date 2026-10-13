# Unified items and stat-derived CC

## Completion evidence

- Same portrait: LayoutMeasurements.csv records the same Kit/Artifact image bounds at 1920×1080 (960×996) and 1280×720 (640×636). Both use the existing 512×512 character sprite. The artifact-only 64% width override is removed.
- Shared geometry: all 214 measured item frames are 96×116. Real item icons have square 92×92 content bounds; footers begin at the sprite bottom, with zero overlap. Frame borders account for the difference between the nominal 96px image section and its interior. Pale footers use 72% alpha.
- Covered roles: equipment slots, required materials, both dungeon previews, victory rewards, backpack artifacts/materials, selected enhancement items, the picker and consumption confirmation. Larger hero/inspection art is not a backpack tile.
- Empty enhancement row has one + tile. Three selected artifacts produce four tiles (three materials plus the add tile), without a fixed-capacity slot array. The row is visible in the docked controls.
- CCReview.csv covers all 29 authored fighters across levels 1–90. All naked endpoint ratios are 10.000. Weight ordering, artifact gain, stat-reward gain, calibration serialization, zero-growth behavior, legacy snapshots and saturation passed. CombatClassBp has no effect on calculated CC.

## Scope and verification

24 native UI Toolkit captures cover 12 states at 1920×1080 and 1280×720. No portrait review was performed. Rendering uses an isolated preview project and detached sample equipment; it never equips, consumes, upgrades or saves the live account. Numerical checks run in a separate .NET review process. Core build and isolated Unity compilation completed without C# errors. Existing project warnings remain; live-editor verification was unavailable. CC weights are starting balance values and still need gameplay tuning.
