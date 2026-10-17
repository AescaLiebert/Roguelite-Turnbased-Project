# Elemental refactor verification

- Shared core build: passed, zero warnings and errors.
- Combat and authored Genshin kits: **522 assertions passed**.
- AI ordering, legality, save/resume, privacy and replay: **734 assertions passed**, including 24 integrated turns.
- Adjacent neutral status, immunity, stance, healing and Weakpoint regressions: **101 assertions passed**.
- Existing neutral HUD fixture: **13 assertions passed**.
- Updated Unity aura and 3D feedback fixture: **36 assertions passed**. Checks include supplied neutral icons in the actual HUD, authored content, application caption colors, world-space particles, wet-ground meshes, downward discharge, ground travel, rise into an eligible neighbor, exclusion of dry targets, completion cleanup and interrupted cleanup.

The Unity preview uses placeholder capsules to make the 3D path easy to see. Capture travel/fade runs at quarter speed so image encoding cannot skip a phase; production defaults remain 0.5 seconds of travel and 0.35 seconds of fade. See `01-auras-and-electrify.png` for the ground phase and `02-ground-lightning.png` for the completed path.

The full combat suite still stops at its documented baseline `First-hit status ran late` assertion. The broad card description audit also reports 14 formatting-idempotence failures for Lumine, Benimaru 99 and Brian; its report is retained in `DescriptionAudit.md`. The focused gameplay and Unity checks above pass.

Rules, migration behavior and playtest steps: `Tests/CombatPresentation/ElementalAuras.md`.

Follow-up: Freeze now settles after the complete triggering action, including its follow-up damage; checks cover both sides and 1/3/10 hits. Default reaction level scaling is now ×100, including migration of saved ×10 defaults. Custom reaction configurations are preserved.
