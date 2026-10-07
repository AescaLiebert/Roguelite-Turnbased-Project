# Card kinds and stance authoring

SkillCardSO rank data now controls gameplay and targeting; UltimateLevelData has the same Skill Type field (existing ultimates default to Attack). Rank merges refresh the kind and target scope. Buff/Heal Single opens the active-ally picker; AOE targets all active allies. Debuff is damage-free regardless of multiplier. Damaging debuffs use DebuffAtk. Stance always targets self. The existing damaging Debuff assets were migrated to DebuffAtk based on their explicit nonzero multipliers.

## Status-driven stance

For the two supplied examples, use the SkillCardSO context menu: `Stance Examples/Immunity, Evade, 80% Recovery` or `Stance Examples/Taunt with Ignite Counter`. These are explicit, undoable authoring operations; they replace stance configuration for each rank.

Set Skill Type = Stance and author the rank's Stance recipe. It is the parent buff, with its own ID, duration and clock. Add recipes under Stance Children. Each child appears as a buff and is owned by that particular parent instance. Dispelling, explicitly removing, replacing, or expiring the parent removes its effects. Normal-color buffs can be cleansed; grey statuses cannot. Explicit RemoveStance/Cancel Stance removes stances. Existing unrelated buffs remain intact.

The activation turn does not consume TargetTurnEnd stance duration. TargetTurnStart stances recover before their final expiry. Use:

- DebuffImmunity for the immunity child.
- EvadeAttacks for a guaranteed evade child (no damage or on-hit effects).
- RecoverDamageTakenBp = 8000 for the requested 80% of actual HP damage received. Damage to shields is excluded; ordinary attacks, counters, and DOT HP loss are included. Recovery consumes the accumulated amount at the next own turn, before expiry, and cannot revive a defeated fighter.
- Taunt for the taunt child. Single-enemy cards must choose a living active taunter. If several taunters exist, any can be selected; invalidated execution targets use the first active taunter in formation order. AOE keeps its whole-team targets.
- Enable CounterEnabled, then set CounterEffect, CounterCategory and CounterTarget for a counter child. For the Ignite example, choose Debuff / SelectedEnemy, with an ApplyStatus counter operation, StatusRecipeId = `status.debuff.ignite`, and a two-turn override. No damage multiplier is used. The recipient is the original attacker even if another enemy taunts.

Counters run once per eligible counter status after the entire attacking card, before the next queued card. Dead or dispelled stance users cannot counter. Pure debuffs, heals, buffs, and stances cannot trigger counters. Counters do not recursively trigger counter chains. Counter targets/scopes support the same damage and utility execution paths as cards.

Assign StatusVisualData directly on an authored card effect to bind custom/generated status IDs, or add visuals to SkillCardSO/PassiveDefinitionSO Status Visuals using matching recipe IDs. These references also work outside Resources. Stance children display the parent's remaining lifetime. The cyan stance aura follows the live status state; it clears on removal and death. Counter UI shows the stance user's portrait and name.

## Playback and state rules

Authority remains deterministic and event ordered. Consecutive effect feedback within a card/turn window is displayed in one frame, including interleaved damage, healing, status changes and passive notices. Card starts, counters, deaths, turn transitions and hand operations form boundaries. Status event snapshots preserve custom recipes and parent-child membership instead of reconstructing effects from a built-in ID list.

Card costs retain existing action-budget/PG rules. Cancelling the ally picker spends nothing. Reserves and defeated allies are excluded. A selected ally that dies before resolution causes the action to fizzle. Enemy AI chooses valid allies for support and respects taunt for single-enemy cards.

## Verification and playtest

Run `Tests/CombatPresentation/Run.ps1` for deterministic engine tests. The isolated Unity ReferencePresentationChecks runner captures the ally picker, simultaneous team recovery, stance aura and counter cue in addition to its existing presentation cases. It must be launched in a disposable project copy because the runner exits the editor.

Readability/new-player: tap a single heal and identify its recipient before committing; cancel it and confirm no action was spent. Watch AOE heal/passive notices and confirm all affected fighters show feedback together.

Stress: merge ranks that change Single to AOE; target a dying ally; kill or dispel a counter user during AOE; use two taunters; skip during counter presentation. No stuck popup, orphan buff, duplicate team effect, or extra counter is acceptable.

Skill/abuse: remove stance before an attack and verify immunity, evade and counter disappear without removing unrelated buffs. Try pure debuffs against counter stance: no counter. Try repeated counter stances: no recursive chain. Confirm a one-turn recovery stance restores only 80% of actual received HP damage once.

Clarity: ally picker, status badges and named counter portrait identify ownership. Response: cancel is free and playback barriers prevent queued cards overlapping counters. Satisfaction: support pulses/cues and counter animation accompany outcomes. Fit: existing camera, UI styling and animator triggers are reused. Motivation: target choice, stance removal and timing affect the next turn.

Starting value: animation watchdog is 8 seconds, used only to prevent a malformed animator from locking playback. Test with the longest authored action and missing/looping action states. Pass: normal clips complete before continuation and malformed clips cannot hang. Increase only if a valid authored clip exceeds the watchdog; repair looping action transitions if fallback is reached. Existing presentation timings are otherwise reused.

## Verified result

The engine suite passes 6,842 assertions, including the new card-kind and stance cases. Runtime sources compile against Unity 6000.3.4f1 with zero errors. Isolated Unity interaction/visual checks pass with zero runtime errors and captures for ally selection, team healing, stance, and counter presentation. The imported project still reports its recursive passive-condition serialization warnings and a Unity Search startup exception separately from runtime results. Current scene character models are placeholders; animation triggers reuse supplied Animator controllers when available, with procedural presentation as fallback.

Starting value: support-camera framing uses a 25% margin around target bounds. Visual test: all recipients and their feedback remain visible with three active allies; reduce the margin if the team is unnecessarily small, increase it if a supplied model clips. Evidence is saved in Logs/CardKindsEvidence. Final isolated preview: PASS, zero runtime errors; no new stance/counter serialization warnings.
