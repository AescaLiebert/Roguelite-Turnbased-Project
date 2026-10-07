# Damage attacks and animation templates

In a Skill Card rank or Ultimate Card level, expand **Runtime Effect**, choose **Kind: Damage**, then use **Damage Animation**. Choose a template or set **Hit Count (1–10)** and **Range (Close / Long)**. Selecting a preset also sets the effect's target to Single Enemy or All Enemies. This uses the existing card asset; no Animator clips or new character rig are required. The same controls are available for damage counters.

The sixteen reusable procedural templates are selected by hit-count family, range, and target scope. AOE still uses its area template when only one enemy survives. Barrage counts are starting presets and remain editable.

| Hits | Close / Single | Close / AOE | Long / Single | Long / AOE |
| --- | --- | --- | --- | --- |
| 1 | Vault Cleaver: airborne overhead slash | Seismic Landing: jump into ground burst | Comet Lance: charge and heavy bolt | Thunderstorm: descending lightning columns |
| 2 | Cross Cut: alternating diagonal cuts | Twin Crescent: opposing sweeping arcs | Twin Fang: paired curved bolts | Crosswind: two fans of wind blades |
| 3 | Rising Dragon: slash, rising cut, finisher | Cyclone Breaker: three rotating sweeps | Trinity Volley: stepped launch heights | Stormfront: three waves across the group |
| 4–10 | Phantom Rush (6): alternating rush cuts | Blade Tempest (5): spinning area slashes | Meteor Barrage (8): alternating bolts | Starfall Rain (10): alternating overhead rain |

## Resolution contract

- Existing/absent attack settings resolve one close-range hit. Non-damage cards ignore these settings. Old event recordings without hit metadata retain legacy animation playback.
- Damage, coefficient and magnitude describe the **whole card**, not each hit. Each hit independently evaluates the full damage formula with its own crit/block rolls, then takes `floor(total * hit / count) - floor(total * (hit - 1) / count)`. Matching rolls/stats therefore preserve the exact integer total, including defense, pierce, flat reductions and caps. Differing rolls and reactive stat changes can change the final sum.
- Each hit consumes the target's current shield/HP. An AOE strike resolves all living original recipients before the next strike. Defeated recipients receive no more packets; the remainder never retargets to reserves. When everyone is defeated, the sequence ends early.
- `Damaging` occurs once at the first visual impact, before its damage. `AfterDamage` occurs once after the last actual hit. `BeforeAction` and `AfterAction` remain once per action. Follow-up effects use accumulated damage, including damage-based status potency. Legacy gauge drain remains once per action.
- Per-hit damage reactions can observe each hit; reflect, stance-counter activation and lifesteal retain their existing end-of-action aggregation. Damage counters can themselves have multiple hits, but do not recursively counter.
- Every `HitStarted` is a playback barrier. The event carries the hit index/count and surviving targets. Each damage event carries separate crit/block, shield and HP facts; the presentation never re-rolls combat.
- Generated templates own their impact timing. Existing `CardFirstHit` / `CardLastHit` Animator events still serve legacy playback; they do not override procedural template impacts.

## State and feedback

Entry: a validated card/counter begins during locked execution. Card cost and gauge payment happen once. Intermediate hits cannot queue an extra player action. Exit: last actual hit, follow-up windows, then recovery to the remembered formation pose. Skip/disable stops presentation and restores poses/camera; authority already contains the complete deterministic result. Ranged attackers stay at their formation position; melee stops in front of the nearest target plane. Camera follows the existing bounded attack framing and returns through the existing planning flow.

Clarity: the card description exposes hit count/range; every strike has its own number and crit/block feedback. Motivation: the same card investment now produces visible independent outcomes. Response: planning lock and skip remain intact. Satisfaction: alternating trails/projectiles, recoil, impact audio and a heavier final strike. Fit: existing models, rank/ultimate introductions, camera and status presentation are reused.

## Starting values and playtest

Starting values: heavy windup 0.42 s, combo strikes 0.20 s, finisher 0.23 s; ranged flight 0.12 s (heavy 0.26 s). Barrages with 4–10 hits share a 0.72 s total strike window, so every extra hit shortens its animation, projectile, recoil, and number beat. The attacker approaches once before that window and recovers once afterward. These are tuning values, not balance claims.

Timing test: compare first impact to last impact for a 4-hit and 10-hit barrage with the same range/target scope. Pass: both strike windows finish within about 0.9 s while each result is visible. If dense, lengthen the total window in small steps; if action flow feels slow, shorten the shared window without adding per-hit movement.

Automated core checks: `./Tests/CombatPresentation/Run.ps1`. Coverage includes full-formula conservation for all damage families and 1–10 hits, shields/caps, AOE ordering, independent rolls, death, replay/projection, serialization/cloning and support-card isolation.

Automated visual smoke: after rebuilding Core, run `./Tests/CombatPresentation/RunMultiHitVisual.ps1`. It launches a separate minimal Unity project under `Temp/MultiHitVisualProject`, exercises all sixteen templates on capsules, checks range travel, target framing, recovery and skip, and saves PNG evidence there. It does not open or save the active battle scene.

Manual checks with real character models:

1. New player/readability: inspect a card, predict hit count/range, then count the displayed impacts. Pass: visible impacts and HP changes match, AOE numbers appear together per strike. If dense, lengthen barrage separation or shorten trail lifetime.
2. Stress: play ten-hit AOE, kill one target early, use a shielded target, evade stance and crit/block mixtures. Pass: no dead-target packets, missing survivors, duplicate follow-up statuses or early reserve entry.
3. Timing: put distinct status effects in Damaging and AfterDamage. Pass: first status occurs once at strike one, last status once after the final strike. Confirm damage-based statuses use the whole sequence.
4. Response/abuse: spam input and skip during approach, projectile flight, middle hit and finisher. Pass: one card/gauge cost, no extra actions, authority/display agree, poses and camera restore.
5. Skill/fit: compare single heavy attacks and a long barrage on small and large models, both sides, narrow and wide windows. Pass: targets and feedback remain visible, melee bodies do not overlap, ranged actor stays home. If occluded, increase separation/framing padding; if slow, reduce recovery/spacing before reducing readability.

Human evaluation of animation feel on the production rigs remains a playtest task.
