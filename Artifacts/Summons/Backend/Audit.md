# Summon backend self-audit

Date: 2026-10-21. Requested by the owner after initial implementation. Scope: approved backend rules, the saved Fischl/Xiangling/Ningguang packages, lifecycle/ownership, damage/trigger bookkeeping, content and snapshot validation, replay, and shared-core regressions. Visual production remains the next phase.

## Findings and fixes

| Finding | Reproduction / impact | Fix |
| --- | --- | --- |
| Generated status riders incorrectly reported card origin | A card-only StatusApplied passive counted Guoba's autonomous Ignite applications | Generated attack riders now pass `cardOrigin: false`; ordinary card status delivery retains its existing semantics |
| Opening replay retained entity aura bonuses before creation | A battle-start entity granting Attack remained absent in the pre-opening display while its Attack bonus was already active | Pre-opening reconstruction suppresses contributions and activation keys from removed entity instances; replay restores them from authority facts |
| New summon validation missed ordinary cards | An invalid lifetime on a base skill, or an unknown schema in an ordinary Ultimate sequence, escaped the validator used at battle creation/submission | New entity/source-layer contracts now cover ordinary ranks, Ultimate tiers, and their sequence operations, alongside existing variant validation |
| Saved structural invariants were incomplete | A linked marker could load with a Permanent clock or no owner-death break rule; invalid occurrence values were accepted | Snapshot checks enforce canonical identity, marker recipe/clock/death/stacking/source rules, finite lifetime bounds, positive scheduling bounds, unique links, and no live entities in a completed battle. Detached fighter creation is rejected. The plan boundary rejects malformed entity state atomically |
| Generated AoE healing recounted earlier recipients | A summon owner with a damage-healing support capability healed from the first target again when processing the next target | Generated attack healing sums only damage facts committed by that contact and its own children, instead of rescanning the entire action history |
| Allied Oz kills credited the triggering card's caster | An ally's Hydro hit triggered Electro-Charged; Fischl's Oz delivered the lethal follow-up, but the ally's defeat passive received credit | Deferred defeat resolution inspects the latest positive lethal contact. Entity kills retain their owner, generated action origin, parent contact, and entity metadata; card kills retain their original attribution |
| Entity AoE resolved reaction children before later root targets | Guoba's first target triggered Overload and killed its neighbour before Guoba's own root contact reached that captured target | Entity actions now commit all root contacts first, then process their captured reaction/status consequences and finalize deferred Freeze effects and defeats. Shared construct capacity still spends in stable target order |

The new regressions first exposed the generated-status error, then the missing snapshot invariants, and separately reproduced the Oz defeat-credit error. All are passing after correction. `Tests/Summons/AuditChecks.cs` adds focused cases for each finding; the original integration cases remain in place.

## Verification

- Focused summon suite: **251 assertions passed** (229 before this audit).
- Shared Core/Contracts Release build: passed with **zero warnings and errors**.
- Existing Studio regression suites: passed, including lifecycle, composition, immutable context, interception, authoring, Battle Lab, and timeline cases.
- Existing artifact regression suite: **35,122 assertions passed**.
- Targeted character migration: `--check` reports **0 of 15** native package files differ; no character source/balance migration was needed for the audit fixes.
- Final Unity import and isolated native-character battle: passed. Both opening screens spawned; Oz/Guoba and their reaction follow-ups resolved; shared screen absorbed 1,584 damage; the resulting Planning snapshot round-tripped with three surviving entities and 135 facts. [JSON evidence](AuditUnityBattle.json).
- Unity console after final import/simulation: **zero errors**. The verification did not modify the active scene or control Play mode.

No balance, renderer, dependencies, scene layout, models, or animation assets were changed by the audit. This report records the paths inspected and the reproduced defects; it is not a claim that every possible future content combination has been exhaustively tested.

## Follow-up audit

The continued backend review reproduced one additional defect: Guoba's built-in Ignite rider applied even when defense reduced its formula damage to zero. Its separate resistance-down rider and elemental aura already required a positive contact, so the same attack could report Endure while still applying Ignite. The regression failed before the fix. Generated elemental attacks now require `hitLanded` before delivering the built-in status rider. A positive contact fully absorbed by a construct still delivers both Guoba riders, preserving barrier-contact behavior.

Added coverage also verifies that a summon created by a turn-end passive waits until the next eligible boundary, retains its full lifetime, and performs its final attack before expiry. A four-turn replay fixture compares both teams' complete entity snapshots and structural marker states after every turn, including absorption, duration changes, and removal.

- Focused suite: **291 assertions passed**, up from 251.
- Studio regression: all 14 reported suites passed.
- Artifact regression: **35,122 assertions passed**.
- Targeted native-package migration check: **0 of 15** files differ.
- Official Core/Contracts Release build: **zero warnings and errors**; updated Unity plugin binaries deployed.
- Unity native-asset simulation after reimport: both opening screens, five entity actions, 1,584 damage absorbed, and three entities retained through snapshot serialization. Native Guoba's endured contact applied zero Ignite; its fully construct-absorbed 2,473-damage contact applied two Ignite layers with zero HP loss. [JSON evidence](FollowupAuditUnityBattle.json). No Play mode or scene changes were required.
- Unity compilation completed; the console has no errors. Existing unrelated obsolete-API, unused-field, and reference-comparison warnings remain in presentation/example scripts.

The three approved character kits and balance values remain unchanged. This is backend verification; model and animation production remains separate.
