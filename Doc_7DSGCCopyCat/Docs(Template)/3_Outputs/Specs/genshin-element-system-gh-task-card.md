---
slug: genshin-element-system-gh
status: approved
source: manual
gdd_tags: [elemental-reactions, mechanics, ai, architecture, actor-camera]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Elemental G–H and Practice integration

Owner requested “Implementation G-H” and reported missing Genshin fighters in Practice. Workflow: `/implement-feature`, single-agent execution of the already approved architecture. Preserve unrelated workspace edits; no publication or dependency/build-setting changes.

GDD intent: “converting debuffs competes with retaining their value” and “do not reveal future rolls, opponent cards, or hidden stats.” G adds bounded prepare/trigger candidates, order-sensitive outcome evaluation and the opportunity cost of consumed DOT/control. H verifies integration, export, replay/privacy, presentation cleanup and platform availability. Human comprehension/balance and physical-device acceptance must be recorded honestly.

Planning receives a detached observation with its own hand and visible fighters; opponent hands, event history and live random streams are excluded. Combat evaluation uses three fixed independent sample streams, never the encounter's future rolls. Existing non-elemental scoring remains supported. Starting policies: retain at most half the candidate budget for elemental pairs, examine at most 512 pair/target combinations, independently evaluate an eight-plan shortlist (half conversion/half ordinary when available; configurable within 2–16), and discount residual DOT/control by 60%. Test equal-progression setup/trigger wins, valuable-DOT preservation, immunity/taunt, and hand/RNG invariance. If conversion dominates every fixture, raise retained-value discount; if useful combos lose, inspect candidate coverage before tuning damage. The separate shortlist bounds synchronous planning cost without making private RNG a shortcut.

Practice audit: all five assets are runtime-ready in the current 39-character registry. The launch test reproduced rejection of elemental cards because TrainingContext built its validation catalog without elemental rules/statuses. Supply the standard versioned catalog, expose the available fighters clearly, and clamp constellation to authored tiers (C0–C5). The picker also has a hidden scrollbar and raw series IDs; improve discovery and test each authored asset through the actual picker/context. Preserve its existing 1v1, free-ultimate, no-save rules.

Five-component goals: clear series selection and explainable combo choices; immediate filtering/reset; existing visual contact plus an audible reaction cue; formation/retained-value identity; safe unrestricted practice. Entry is an eligible planning turn; completion, fizzle, defeat or budget exhaustion ends a plan. Preview/evaluation cannot change live state. Practice closes/restarts/exits through existing controls and cannot grant rewards. New-player, stress, skill, abuse and observer scripts are part of H evidence; human results are not inferred from automation.

Delivery: [G–H evidence](../../../../Artifacts/GenshinElementSystem/ImplementationGH.md). G and automated H checks implemented. Owner explicitly selected “Record Android runtime testing as pending.” Human acceptance is still open. Starting valuation details and tuning directions are recorded in the canonical GDD; reaction audio reuses the existing local cue source and its cleanup.

Latest authorized revision: [dungeon/presentation revision](../../../../Artifacts/GenshinElementSystem/PresentationRevision.md) removes player prediction UI and the separate Genshin shortcut, retains Series selection, fixes dungeon catalog validation and scopes the requested shrinking/fading/slowing-rise FCT to Genshin source fighters. This supersedes earlier universal-FCT and shortcut instructions.
