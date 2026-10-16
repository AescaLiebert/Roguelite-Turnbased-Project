---
slug: genshin-element-system-df
status: approved
source: manual
gdd_tags: [elemental-reactions, genshin-roster, mechanics, actor-camera, architecture]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Elemental D–F implementation task

Owner authorized D–F, local `zh-cn` Genshin-style FCT, colored contact→reaction visualization, and the global database correction: Shock/Poison 45%, Bleed 70%. Workflow: `/implement-feature`, single-agent implementation; preserve existing workspace edits. This extends the already approved architecture and does not reopen its approval checkpoint.

Canonical rules: [GDD elemental reactions](../../1_Inputs_Templates/GDD_Fighting_Allstar.md#genshin-inspired-elemental-reactions) and [five starter kits](CharacterData/genshin-character-roster.md).

Owner's follow-up answers: Crystallize cap 100% Max HP; accepted Freeze/recovery, physical Superconduct and two-turn reaction ticks; Dissolve R1=1 PG/1 turn, R2=2 PG/1 turn, R3=2 PG/2 turns; Bunny applies ordinary Ignite; gauge modifiers change generated orb amounts with fractional carry; Lumine ultimate uses Shatter.

Delivered scope: formation-aware bounded Swirl, remaining reaction outputs, public symbolic queue forecast, event-driven colored feedback, shared DOT normalization for old/new statuses, complete five-character shared/Unity content, and focused/legacy/editor checks. See [implementation evidence](../../../../Artifacts/GenshinElementSystem/ImplementationDF.md).

Starting values and policies remain explicit: 100% ATK Swirl/Superconduct, Burning cap three, generated stats/lower ranks, Conductive per-stack DEF reduction, owned damage-heal batching, snapshot moments and active-provider scopes. Final human feel/balance review and Android validation belong to later acceptance. AI combo valuation is slice G; acquisition/banner publication and new character artwork are outside this task.
