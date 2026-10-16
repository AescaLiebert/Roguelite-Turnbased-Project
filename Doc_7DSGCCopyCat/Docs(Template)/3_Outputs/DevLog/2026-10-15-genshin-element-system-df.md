---
slug: genshin-element-system-df-log
status: approved
source: manual
gdd_tags: [elemental-reactions, genshin-roster, mechanics, actor-camera, architecture]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# DevLog: 2026-10-15 — Elemental D–F, feedback and DOT correction

The owner authorized D–F and specified the local font/colored reaction feedback and database-owned DOT values. Follow-up answers replaced the proposed Geo cap with 100% Max HP, defined all Dissolve ranks, corrected Bunny's rider to ordinary Ignite, clarified fractional orb generation and mapped Lumine's ultimate to Shatter.

Implemented formation-aware Swirl with captured-source cancellation, remaining outputs/clocks, public symbolic queue forecasting, shared fractional gauge generation/replay/pips, and event-driven colored feedback. Reusable support and typed triggered attacks author the five kits without combat resolver branches on character IDs. Unity assets are registry-indexed; new artwork and summon-banner publication were not requested.

`StandardDotRules` normalizes Shock/Poison to 45% and Bleed to 70% for cloned content, applications and old snapshots. Authoring migration audits inline recipes; the glossary uses the same constants. The legacy fixed-damage Bleed fixture now supplies a triggering-damage snapshot under the newly authorized rule.

Unity round-trip review caught materialized empty optional payloads. Cloning treats empty elemental applications, triggered attacks, support and heal values as absent, preserving meaningful authored values. Persisted Barbara healing is exercised after force-reimport; formatting preserves existing status display names instead of leaking localization IDs.

Final integration preserves exact fractional PG loss/refunds in live state and replay. Support processing uses only current-action contacts and damage: evaded Conductive targets keep their setup, and historical events with a reused card ID cannot qualify a new action. These paths have dedicated regressions.

See [D–F evidence](../../../../Artifacts/GenshinElementSystem/ImplementationDF.md). Focused gameplay, dungeon/save, description, server/core, Unity export and rendered feedback checks passed. The unrestricted harness still reaches its pre-existing first-hit timing assertion; no entirely green full-suite claim is made. Android, human balance/feel and slice-G AI remain future work.
