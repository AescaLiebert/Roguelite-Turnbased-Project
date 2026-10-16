---
slug: genshin-element-system-design
status: approved
source: manual
gdd_tags: [elemental-reactions, mechanics, visual-audio, actor-camera, platform-input]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Elemental reactions: player experience specification

Implementation update: D–F now includes a public queue forecast, local HYWenHei FCT, colored elemental contact convergence and reaction captions, remaining outputs, five starter kits and fractional gauge feedback. Current scope, owner corrections and executed checks are in [D–F evidence](../../../../Artifacts/GenshinElementSystem/ImplementationDF.md). The interaction/feel goals below remain human acceptance criteria; automated render checks do not establish final balance or player comprehension. Earlier future-tense implementation status is historical.

Date: 2026-10-15. Design reference: [GDD `@tag:elemental-reactions`](../../1_Inputs_Templates/GDD_Fighting_Allstar.md#genshin-inspired-elemental-reactions). Owner approved the documented plan with “LGTM.” This is a derived interaction specification; rules and tuning live in the GDD. Runtime slices A–F are implemented; human feel acceptance remains open.

## Player experience

A qualifying Genshin team turns familiar debuffs into a shared setup resource. The player can retain a valuable Shock or use a Pyro contact to consume its stacks for Overload. Lumine spreads setup between formation slots; Geo converts preserved setup into bounded protection. The player sees what is spent before choosing a combo, and recognizes its payoff at the impact.

Confirmed: full roster including SUB; all matching stacks consumed by consuming reactions; Swirl/Crystallize preserve their source; Overload cannot trigger another reaction. Detailed policy and values retain the status shown in the GDD.

## Interaction flow

1. Team selection explains whether the entire starting roster satisfies the Genshin trait requirement, including the SUB portrait.
2. During planning, elemental symbols identify eligible setup separately from ordinary DOTs and Conductive.
3. A quick card tap uses the current queue behavior. Holding opens its details; selecting a target shows the real root target and affected formation neighbors.
4. The forecast shows the expected reaction path and every consumed/preserved stack with remaining duration. Conditional crit/application branches are identified as conditional.
5. Every queued change updates subsequent forecasts. Information must be visible before the last slot automatically commits; no extra confirmation step is introduced.
6. Reset restores the disposable draft. Accepted playback displays recorded hit/reaction/status events in their authoritative order.

PC provides the same decisions and information through existing input equivalents. Hover may supplement the details but cannot be the only way to access them. Neither interface reveals future rolls or an opponent's private cards.

## Feedback and presentation

| Trigger | Visual | Audio | Timing |
| --- | --- | --- | --- |
| Qualifying roster | Gnosis/reaction availability indicator with a readable reason if unavailable | Existing selection cue | Local team validation |
| Planned conversion | Reaction name, incoming/setup symbols, affected slots, consumed/preserved stacks | Existing card/target selection cue | Before commitment; changes with the draft |
| Elemental hit | Element symbol/color plus ordinary hit and full resolved amount | Existing hit cue with elemental identity where available | Recorded contact |
| Reaction | Distinct reaction label and effect; grouped secondary damage remains attributable | Distinct reaction accent layered over impact | Corresponding reaction/child events |
| Consumption | Remove recorded instance icons; keep unrelated and preserved setup visible | Short conversion cue, grouped with reaction | Committed status events |
| Shield/control output | Barrier capacity or Frozen status plus expiry/recovery explanation | Shield or control accent | Recorded output event |

Use restrained particles and symbol/name/color redundancy. The initial functional slice needs visual and audio confirmation before camera polish. Preserve `@tag:actor-camera`: caster-owned framing, root hit barriers, child reaction readiness, and queue cleanup. Do not create additional camera states or make rule execution wait for a VFX callback. Existing full-amount floating-text rules apply to reaction damage too.

No new numeric animation, particle, shake, hitstop, or haptic budgets are specified here. Reuse existing presentation timing until a recorded owner playtest justifies a change. Movement buffering and coyote time do not apply to this combat feature.

### Supplied-kit extension

The [five-character roster](CharacterData/genshin-character-roster.md) adds confirmed authored Bunny/Waltz reaction contacts. Display Bunny's expiry countdown and safe-cleanse cancellation, Waltz's owner/retaliation/recovery, Amber's exact Explosion scope, and Dissolve's end-of-target-turn gauge loss. Keep ordinary reflected damage and DOT visually distinct from those attacks. C0 Kaeya shows self-only Waltz; C5 previews all active recipients. Future-contact forecasts are conditional on survival, cleanse, source eligibility and unresolved snapshot policy; they cannot promise an unavoidable detonation or future target.

## Edge cases and alignment

- Taunt previews the actual root target and its neighbors under the proposed GDD policy.
- Empty/dead slots and reserves do not silently change spatial meaning; temporary hit displacement never changes adjacency.
- Evasion/rejected application reports the correct failed condition and does not falsely show consumed setup.
- A conditional forecast is not presented as a guaranteed Freeze or crit-applied Shock.
- Reset/input repetition cannot produce live consumption, extra PG, or a new RNG outcome.
- Slow playback, skipped cinematics, reconnect, and low frame rate preserve the same event sequence and final HUD state.

This fulfills `@tag:mechanics` card sequencing, `@tag:platform-input` equivalent access, `@tag:visual-audio` readable feedback, and `@tag:actor-camera` presentation ownership. Faction specialization deliberately constrains team construction while retaining the wider game's mixed-team alternatives.

## Playtest and tuning priority

Use the [test plan](../TestPlans/genshin-element-system-test-plan.md) for beginner prediction, stress, mastery, abuse, and observer readability. First check whether the player can change a plan and predict consumption; then check satisfaction and role distinctions; only then tune reaction damage/duration. No new feature playtest has been performed in this documentation task.
