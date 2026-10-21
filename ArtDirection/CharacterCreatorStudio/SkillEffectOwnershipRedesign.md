# Skill effect ownership and conditional sequences

21 October 2026 · Design proposal · Runtime and native assets unchanged

## Decision

SkillCardSO and UltimateCardSO own everything granted by that ability, including conditional follow-ups and reactions granted by temporary statuses. PassiveDefinitionSO owns the character's innate continuous rules and event reactions.

Reuse the passive workflow's condition builder, target selectors, action controls, validation and trace. Use composition of shared rule data and editor components; do not derive SkillCardSO from PassiveDefinitionSO. An event trigger describes when something happens; it does not decide whether the mechanic is a character passive.

The player should find every rank-dependent mechanic in that skill's description and see feedback attributed to the skill or its granted status.

## Findings in the current project

| Existing implementation | Consequence |
| --- | --- |
| `EffectOperationDefinition.Conditions` is inherited by root effects and sequence operations. | Conditional status steps already have a data slot; adding another independent condition system would duplicate it. |
| `EffectConditionDefinition` and `CardEffectSystem.ConditionsPass` support subjects, traits and All/Any/Not. | The condition evaluator can be shared with the passive editor. |
| `DrawAbilityOperation` exposes conditions, partly through generic controls and damage foldouts. | The gap includes a readable authoring workflow, not just missing serialized fields. |
| `ApplyCardEffectSteps` explicitly skips `EffectKind.Damage`. | A visible damage sequence step is not currently sufficient to execute Taki's bonus. |
| Card damage uses the attacker's stat for `StatScaling.MaxHealth`; `DamageEffectRecipe` separately supports `DamageStatSource.Target`. | Moving Taki's declaration without adapting execution would change its damage source. |
| Utility sequence condition calls construct contexts with default category/rank values. | Predicates such as CardRankAtLeast cannot reliably use the actual card in those calls. |
| `StatusRecipeDefinition.Reactions` exists; `CharacterPassiveRuntime.ReactionBindings` also binds active status reactions. | Temporary skill reactions have a runtime home already. |
| Playback displays `Active Unique` for presented `PassiveTriggered` events. | Event origin needs to distinguish an innate passive from a skill/status reaction. |

These findings are based on current working-tree code and assets, including ongoing local changes. They are not a completed behavior test.

## Ownership rules

| Behavior | Authoring owner | Runtime lifetime |
| --- | --- | --- |
| Innate team aura, battle-start benefit, character-wide reaction | PassiveDefinitionSO | Existing passive availability and execution rules |
| Damage, status, heal or other operation during a card action | That rank/tier's effect program | Current card action |
| Reaction granted by a stance, buff or debuff | Status recipe granted by that rank/tier | Applied status instance |
| Ultimate-specific conditional effect | That ultimate tier's effect program or granted status | Action or status instance, respectively |

Changing rank or ultimate tier chooses the corresponding complete effect definition. Do not move progression values back into a character passive and attempt to reconstruct which skill granted them later.

## The three cases

### Taki: additional damage during Skill 2

Skill 2's selected rank owns the main attack and an AfterDamage bonus packet against each original attack recipient that qualified for the bonus.

The current generator declares target-Max-HP scaling, Additional damage family, no crit and no block. Preserve those declarations when moving ownership. The existing coefficient is a migration input, not a new balance recommendation.

The bonus executes after the primary hit sequence, rather than replaying that sequence's choreography. It needs its own damage policy and packet identity. Setting `Scaling = MaxHealth` on an ordinary damage step is insufficient because the current card path reads the attacker's Max HP.

Keep Taki's innate stat/attack rules in PassiveDefinitionSO. The skill-specific reaction currently relies on category/rank filters rather than an exact Skill 2 identity; moving it into Skill 2 removes that accidental coupling.

### Billy97: Ignite while Burning Guard is active

Skill 2 applies its rank's Burning Guard status. That status owns:

```text
WHEN: an eligible enemy attack resolves against the status holder
IF: the holder still has this status instance
DO: apply Ignite to the attacking enemy
```

This is a delayed status reaction, not a sequence step that waits after the cast. When the stance ends or is removed, its reaction is no longer available. Billy's innate low-health Crit rule remains in PassiveDefinitionSO.

The current native passive contains `guard-burn` without an explicit stance condition. Its skill grants Burning Guard without a status reaction. Migration must therefore distinguish preserving the intended skill text from preserving every existing event: removing out-of-stance Ignite is an intended behavior correction and needs an explicit regression case.

### Benimaru99: conditional reaction granted by Skill 2

The current skill text grants a Defense-reduction response at higher ranks when attacked by an enemy with the Women trait. This should be authored inside the granted stance/buff:

```text
WHEN: an eligible enemy attack resolves against the status holder
IF: the attacking enemy has trait.women
DO: apply the selected rank's Defense reduction to that attacker
```

The condition subject is the event actor, and the action recipient is that same attacker. Using the skill's cast target or status holder here would check/debuff the wrong fighter.

The existing passive copies potency from `status.buff.apply-debuff`; the selected rank's granted status should contain or snapshot the same authored potency. Keep Benimaru's innate Women-related aura in PassiveDefinitionSO.

## Shared authoring workflow

Keep Skill 1, Skill 2 and Ultimate pages, with their existing rank/tier selectors and native asset ownership. Each page exposes:

```text
ACTION FLOW
  Main action
  Follow-up action
    WHEN  After damage
    WHO   Original attack recipients
    IF    All / Any / Not conditions, or Always
    DO    Apply status / Additional damage / Heal / existing supported action

GRANTED STATUS
  Duration / stacking / removal rules
  Continuous modifiers
  Reactions while active
    WHEN  Incoming enemy attack resolves
    WHO   Status holder / attacking enemy
    IF    Shared condition builder
    DO    Shared supported actions
```

Use one shared WHEN → WHO → IF → DO editor with an ownership breadcrumb, for example `Skill 2 / Rank 3 / Guard / Reaction`. Passive pages open that same editor with a passive owner. A shared overview may list all rules, but navigation and saving must retain the actual owning skill/status.

Show a readable condition summary on every action. Keep raw enum fields out of the normal workflow. Reuse existing flat condition-node storage and compilation for nested groups; Unity's native condition list is an implicit AND, and its nonserialized recursive Children property cannot be used as a new native serialized tree.

Rank/tier values live in their existing complete definitions. Add explicit Copy from rank/tier and comparison controls if needed; defer a second runtime inheritance/override system. Wording, preview and simulation must read the selected canonical definition.

## Runtime changes required

1. **Pass an explicit action context.** Include skill identity, card identity, rank, ultimate tier, category, caster, selected target, original resolved recipients and current phase. Reuse it for every sequence condition instead of reconstructing partial contexts from event history. Include recipient-specific hit outcomes; later bonus packets must not change the primary attack's crit/hit facts.
2. **Execute supported damage follow-ups.** Add a packet-based additional-damage route alongside the existing utility sequence route. Reuse `DamageEffectRecipe` and the shared damage resolver for stat source, family and crit/block policy. Preserve ordinary primary-attack execution and shared animation settings; an additional packet must not inherit another full attack's hit count. A new optional recipe on a sequence damage operation is one compatible adapter; do not maintain independently editable recipe and legacy damage values for the same operation.
3. **Expose existing status reactions.** Compile shared rule-editor output into the granted recipe's Reactions collection, with ownership scoped to ability/rank-or-tier/status/rule. Generalize names at the editor boundary now; broad renaming of existing passive runtime types is unnecessary for this change.
4. **Separate origin from event dispatch.** Add or extend structured event provenance to identify Skill, SkillGrantedStatus and CharacterPassive, with granting ability/rank/tier, effect ID and status instance where relevant. Reuse common reaction execution, but let playback choose the correct feedback from provenance. Renaming the floating label alone would leave ownership wrong.
5. **Extend validation and persistence.** Reject unsupported action/condition contexts before save; preserve native GUIDs, Undo, rollback, scoped reconstruction and single ownership in the existing five-asset workflow. Generated rules must not reappear on reopen or execute alongside their old passive copies.

### Condition and execution semantics

- IF belongs to the operation or status reaction it controls. A false follow-up condition skips that follow-up and does not cancel the card or consume another resource.
- Evaluate each recipient independently. An area attack's qualifying target must not make every recipient pass a trait, status or crit condition.
- Conditions read current state immediately before their operation by default; ordered status changes may affect later conditions. Action-start snapshots require an explicit authored capability and validation, not an implicit cache.
- Hit-dependent effects require a successful primary contact; full evade does not qualify. Zero HP damage caused by a shield is not equivalent to evade. Any exception must be explicit.
- A crit-dependent rule must identify its scope, such as current contact or any primary hit against this recipient. Do not offer current-hit predicates before a hit result exists.
- Shared batch chance stays a recipient-scoped roll with existing chance/stat rules. Do not reroll it independently for each child or alter RNG order silently during migration.
- Trigger frequency belongs to execution controls. Preserve authored per-hit/final-hit/action limits; migration must not silently convert an imported rule to a different frequency.
- Additional/status damage must not recursively count as a new eligible card attack. Reactive chains use existing origin filters, scoped limits and dispatcher safeguards.
- Status application failure creates no reactive capability. Unavailable event predicates fail validation rather than being accepted with empty/default context.

### Status lifecycle

| Property | Proposed contract |
| --- | --- |
| Entry | Skill successfully applies the granted status; snapshot its selected rank/tier and authored payload. |
| Exit | Existing expiry, removal, replacement, defeat and break rules end that instance's availability. |
| Interruptibility | Stance removal cancels future stance reactions. In-flight reactions resolve against the eligible instance under existing dispatcher rules. |
| Chaining | A qualifying event may execute the status's supported action program; generated status/damage events retain origin and recursion guards. |
| Resource cost | The cast uses the current card/gauge rules. A failed IF adds no cost; reactive costs exist only if explicitly authored and supported. |
| Recast | Keep the status's stacking/refresh policy. Define which rank's payload wins when refreshing; proposed behavior is replacement by the newly applied rank payload when refresh succeeds. Verify this before migration. |
| Identity | Track both status holder and granting fighter. In self-stance cases they match; shared editor labels must not assume they always match. |
| Pending defeat | Preserve the primary multi-hit action boundary. Define whether additional packets may resolve against pending-defeat recipients, and prevent new debuffs on defeated targets. Do not infer this from IsAlive alone. |

## Presentation and player experience

- Skill descriptions include immediate bonuses and granted reactions at the selected rank/tier. Status tooltips repeat the active reaction and its remaining lifetime.
- Immediate additional damage uses its damage feedback; a granted-status response uses the status/skill cue. `Active Unique` remains reserved for actual character-passive activation.
- Preserve damage/status visual and audio cues. A reaction's cause should be visible without requiring another generic activation pop-up.
- The Battle Lab trace explains owner, rank/tier, target, condition result, application failure and execution limit. A skipped condition should be inspectable without flooding combat text.

| Design filter | Acceptance goal |
| --- | --- |
| Clarity | Skill text predicts its bonus and temporary reaction; no misleading passive announcement. |
| Motivation | Rank/tier selection exposes the corresponding mechanical improvement. |
| Response | Casting, reaction timing and status removal have deterministic effects on availability. |
| Satisfaction | Bonus damage/status responses keep coherent visual and audio feedback. |
| Fit | Temporary stance responses behave as part of the stance in this turn-based combat system. |

## Migration and validation plan

Implement in this order: shared ownership/context → damage follow-up execution → shared skill/status condition editor → provenance/presentation → targeted native migration. Update generators as well as saved assets so rebuilding content cannot restore the old passive wiring.

Use isolated fixtures first. Migrate the three example kits individually, preserving their existing numbers and rank/tier data. Remove each old skill-specific passive rule in the same change that installs its replacement; retain innate passive rules. Extend parity checks to all ranks/tiers and utility skills. Record intentional changes, including Billy's out-of-stance behavior, separately from unintended differences in state, receipts, events or RNG.

Required scenarios:

- **Taki:** Skill 2 resolves its target-Max-HP bonus with the correct family/policy after the primary hits. Skill 1 and Ultimate do not receive it. Cover multi-hit, area recipients, evade, shield-only contact, pending defeat and Additional-damage immunity.
- **Billy:** Ignite occurs only while the granting guard is active, targets the attacker, and stops after expiry/removal. Test repeated hits, recast and innate low-health Crit independently.
- **Benimaru:** Compare every rank. Women attackers receive the selected rank's reduction when the granting status permits it; other attackers do not. Test removal, refresh from a different rank and immunity.
- **Progression:** Ultimate-granted reactions use the casting tier's payload through the status lifetime. No lookup into a subsequently changed character tier/rank is required.
- **Persistence:** Save/reopen/Undo/Redo retains grouped conditions, status reactions, rank payloads and GUIDs. Repeated saves do not duplicate or resurrect removed rules.
- **New player/readability:** A player and an observer can explain the damage/status result from the skill text and active status without being told about PassiveSO.
- **Stress/abuse:** Repeated casts, simultaneous recipients and reactive damage cannot multiply activations, leak after removal or cause unbounded reaction chains.

Tune ownership, timing, subject selection and feedback before considering balance changes. This proposal adds no new damage, duration, chance or economy values.

## Assumptions to validate

ASSUMPTION: The current skill descriptions express the intended ownership and broad behavior of these three mechanics.

IMPACT: Migration may correct existing behavior that accidentally runs outside the granting skill/status.

IF WRONG: Moving rules could remove an intentionally innate mechanic, or change a deliberate per-hit/final-hit response.

VALIDATE: Compare each rank's authored source wording against an isolated Battle Lab scenario; record trigger frequency, refresh payload and pending-defeat semantics before migrating live assets.

## Code anchors

- [SkillCardSO and rank payloads](../../Assets/Project/Runtime/Content/Cards/SkillCardSO.cs)
- [UltimateCardSO and tier payloads](../../Assets/Project/Runtime/Content/Cards/UltimateCardSO.cs)
- [Shared effects and sequences](../../Shared/FightingAllstar.Core/Content/CombatContent.cs)
- [Conditions and damage/status recipes](../../Shared/FightingAllstar.Core/Content/EffectRecipes.cs)
- [Ability editor](../../Assets/Project/Editor/CharacterCreatorStudio/CharacterCreatorStudioWindow.AbilityEditor.cs)
- [Battle sequence execution](../../Shared/FightingAllstar.Core/Combat/BattleEngine.cs)
- [Shared condition evaluation](../../Shared/FightingAllstar.Core/Combat/CardEffectSystem.cs)
- [Passive and status reaction bindings](../../Shared/FightingAllstar.Core/Combat/CharacterPassiveRuntime.cs)
- [Playback labels](../../Assets/Project/Runtime/Presentation/Combat/CoreBattleSceneController.Playback.cs)
