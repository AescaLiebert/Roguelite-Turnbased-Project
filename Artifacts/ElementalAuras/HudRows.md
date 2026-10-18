# Separate CharacterStatUI elemental row

`CharacterStatUI/UnitUI/StatusLayoutSlot` contains independent `Buff/DebuffPanel` and `ElementStatusPanel` objects. The buff/debuff row stays nearest the health bar, and elemental auras occupy a separate row above it. The aura row clears the current buff/debuff grid height plus its spacing, so it remains above the ordinary grid as that grid grows. Each row has its own icon instances, updates and cleanup. Empty elemental rows hide and release their spacing. Both initial display and event playback send the two collections separately.

Verification on 2026-10-17:

- Production runtime compiled successfully; five existing warnings, no errors.
- Unity fixture passed 26 assertions covering ordinary status ordering, independent aura rows, icon reuse, non-refreshing three-turn clocks, restored snapshots, inspector duration, independent clearing and expiry.
- Prefab structure checked for unique object IDs, separate sibling panels and a five-icon elemental grid.
- Visually inspected the [layout capture](character-stat-element-row.png), rendered through the production HUD icon helpers using the supplied icon art. This is an isolated layout fixture rather than a full battle capture.

Run `Tests/CombatPresentation/RunNeutralStatusHudChecks.ps1` to reproduce. Python with Pillow converts fixture copies of the supplied WebP-encoded `.png` files to PNG bytes for the runtime capture loader.
