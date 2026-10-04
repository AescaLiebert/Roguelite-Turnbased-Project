---
slug: character-passive-system
status: ready-review
source: manual
owner: Codex
human_checkpoint: review
blocked_by: []
workflow: /implement-feature
---

# Character Passive System

## Request and scope

Design and plan a systematic passive architecture with constellation-gated abilities. After architecture, implement the eight existing character passives. Cassandra Origin is a design example only.

## Source of truth

- [Design and implementation plan](character-passive-system-plan.md)
- [Combat source contract](fighting-allstar-combat-source-contract.md): pure shared authority, SUB presence, stat units/groups, King timing, lifecycle ordering.
- `Assets/Project/Content/Generated/phase-e-catalog.json`: eight passive source descriptions and existing C0–C6 ultimate data.
- `Assets/Project/Content/WipCharacterCatalog.json`: preserved source catalog.

## Acceptance

- [x] Document explicit triggers, conditions, ownership, modes, duration, stacking, lineage and constellation unlock/replacement semantics.
- [x] Decompose Cassandra into independent rules without implementing her.
- [x] Implement supported gates, continuous contributions and deterministic counter/gauge reactions for the eight source passives.
- [x] Wire battle lifecycle, effective stats, event playback and content export/load.
- [x] Preserve existing unrelated changes and card-effect scope.
- [x] Compile shared core and rebuild Unity plugin assemblies.
- [ ] Run gameplay acceptance scenarios in plan section 9 when requested.

## Checkpoints

The user explicitly authorized design followed by the eight-character implementation. Review this concrete result and its documented follow-on slices before broader passive content authoring. No commit, merge, publication, dependency or CI changes are included.
