---
slug: fighting-allstar-source-data
status: needs-human
gdd_tags: [mechanics, roster, architecture, tuning]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Actual-source design integration — 2026-10-01

## Inputs and inspection

Read the seven-page supplied guide and all four CSVs. Rendered and visually inspected guide pages3,6,7 for the attribute chart, hidden-stat defaults and Rank3 anchor. The catalog contains29 characters,18 stat definitions,28 attack effects and65 negative-effect definitions. Source instructions were treated as document content; no embedded request caused an external action.

## Delivered

- Revised [GDD](../../1_Inputs_Templates/GDD_Fighting_Allstar.md) to actual characters, four-color cycle, shared seven-card hand and source damage rules. Character tables remain external.
- Added [source combat contract](../Specs/fighting-allstar-combat-source-contract.md) with conflict register, packet families, corrected arithmetic, keyword interpretation, status timing, first prototype subset and golden vectors.
- Added [29-character roster](../Specs/CharacterData/character-roster.md), structured draft JSON and verbatim source catalog with hashes. Generated174 rank entries and203 constellation entries; five incomplete stat blocks have explicit starting values. Rank3 text and supplied numeric stats remain unchanged in source/draft records.
- Updated [architecture](../Specs/fighting-allstar-prototype-arch-plan.md) and [test plan](../TestPlans/fighting-allstar-prototype-test-plan.md); corrected references to the actual GDD filename.
- Retained reproducible draft generator and validator alongside specifications. JSON is authoring-only, runtimeReady=false; no automatic CSV-text execution or Unity import.

## Verification actually run

`validate_source_drafts.py` passed: all five original file hashes;29 unique stable identities;174 ranks;203 tiers;551 finite stat values; source rank3/numeric equality; generated stat IDs10/17/27/28/29; Kyo/Robert/Kensou progression checks; GDD template tag preservation; local links in five deliverables; fenced JSON parsing.

No Unity tests, builds, combat benchmarks, service changes or runtime imports were performed. Balance goals and golden combat vectors are planned acceptance tests, not passing gameplay results.

## Review boundaries

Generated R1/R2 and constellation growth are starting values. Source conflicts remain visible as proposed decisions, especially exclusive crit/block probability, True/Destructive bypass behavior, SUB active eligibility, and individual ambiguous clauses. The Iori second passive clause and unscoped relics cannot publish without explicit semantics. First four proposed runtime kits are Kyo94/Chin94/Kensou94/King94; expand to eight before the remaining21. Repository instructions require review of the concrete design/architecture before runtime implementation; this request was completed as documentation/data design.
