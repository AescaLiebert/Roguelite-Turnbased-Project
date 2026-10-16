---
slug: genshin-element-system-df-evidence
status: approved
gdd_tags: [elemental-reactions, genshin-roster, mechanics, actor-camera, architecture]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Elemental D–F implementation evidence

Implemented under the owner's D–F goal, including the explicit global DOT and presentation changes. Canonical rules are in the [GDD](../../Doc_7DSGCCopyCat/Docs(Template)/1_Inputs_Templates/GDD_Fighting_Allstar.md); detailed kits and starting values are in the [roster](../../Doc_7DSGCCopyCat/Docs(Template)/3_Outputs/Specs/CharacterData/genshin-character-roster.md).

Subsequent [G–H delivery](ImplementationGH.md) adds enemy planning, the Practice launch repair and expanded audio/cleanup checks. Its evidence supersedes the later-work and verification status below for those areas.

## Delivered

- Stable formation-slot Swirl, preserving its source and spreading fresh single-stack setup. Sources are captured before consumption; consumed sources cannot emit later, newly spread setup cannot join that snapshot, and Overload remains terminal.
- Superconduct physical-only vulnerability; two-target-turn Charged/Burning; Frozen plus one recovery turn; accumulating two-turn Crystallize capped at **100% effective Max HP** and based on actual HP-plus-shield damage, excluding overkill.
- Public symbolic queue forecast with consumed stacks, preserved setup, target positions and spread destinations. Critical/status-dependent follow-ups are possible branches; immunity and the 64-branch limit are visible. No private hand/RNG fields are inputs.
- Reusable explosion, reserve-Swirl, attack-cadence, target-setup, healing and gauge capabilities; typed natural-expiry and retaliation attacks. Bunny/Waltz can react and cannot form generic reflect/retaliation loops.
- Five authored Unity kits, ten skills with R1–R3 and five ultimates with C0–C5 under [GenshinImpact](../../Assets/Project/Data/Character/GenshinImpact). Stats/lower ranks are generated starting values. Existing placeholder art is used; no banner/acquisition release is implied.
- Global Shock/Poison **45%**, Bleed **70%**, owned by [StandardDotRules](../../Shared/FightingAllstar.Core/Content/StandardDotRules.cs), including old snapshots. Frost Bite retains 64% and its generation reduction lasts with the status. Orb fractions carry, replay and render as partial pips; reaching five/spending the ultimate clears the fraction. Clamped drains and actual-gauge-loss refunds retain exact fractional amounts, including replay.
- Conductive cash-out and after-attack setup require a contact in the current action. An evaded target retains its setup; earlier events with a reused card ID cannot qualify that action or contribute old healing damage.
- Every floating caption uses local HYWenHei `zh-cn` and normal weight. Element color survives crit/block. Colored contacts converge, then display the reaction with a matching impact tint. Existing pooling/skip cleanup remains in use.

## Verification

| Check | Result |
| --- | --- |
| Focused elemental suite | **440 assertions passed:** 264 A–C, 89 expansion/forecast/DOT, 87 starter-kit assertions. |
| Existing status/stance/immunity group | **101 assertions passed.** |
| Dungeon/combat/progression/save group | **9879 assertions passed.** |
| Legacy Shock damage snapshots | Passed: 45% of actual single-target/AOE HP damage rather than ATK or the old inline coefficient. |
| Persisted global DOT audit | **48 inline recipes audited; 0 repairs needed** on the final Unity pass. |
| Character/card description audit | **4031 assertions passed; 39 characters, 468 ranks/tiers.** [Description audit](../CombatText/Review.md). |
| Persisted Unity content | Five force-reimported kits validate/export 30 skill ranks and 30 ultimate tiers; Barbara's Max-HP heal executes after Unity serialization. |
| Shared core/server | Release builds succeeded. Unity runtime compilation checked; existing unrelated deprecation warnings remain. |
| Visual feedback | **24 assertions at D–F; 28 in the subsequent G–H pass**, including added audio/interruption checks. Four animation checkpoints plus public forecast and fractional-pip fixtures rendered and inspected. [Current visual result](Feedback/result.txt). |
| Unrestricted pre-existing harness | Still stops at `MultiHitChecks.cs:145`, **First-hit status ran late**. A–C work reproduced this in an isolated pre-change baseline; the current rerun has the same failure. |

## Rendered review

![Elemental contact convergence](Feedback/01-elemental-contact.png)
![Reaction impact and seven damage colors](Feedback/02-reaction-impact.png)
![Public reaction forecast](Feedback/05-public-forecast.png)
![Fractional orb](Feedback/06-fractional-gauge.png)

## Practical limits

This is a verified playable prototype, not final balance approval. Swirl/Superconduct coefficients, Burning's three-stack cap, generated stats and several kit batching/snapshot conventions remain documented starting policies. Human stress/skill/readability/abuse sessions, Android runtime acceptance and combo-aware enemy AI are later work. Forecasts condition on successful contacts and surviving targets; their symbolic branch cap is explicit. Existing unrelated workspace edits were retained; no commit, PR, merge or publication was performed.
