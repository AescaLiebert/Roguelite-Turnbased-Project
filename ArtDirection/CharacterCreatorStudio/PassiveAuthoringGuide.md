# Passive authoring: auras, trigger rules and imported compatibility

20 October 2026

There is one compiled `PassiveDefinition`, saved in the character's `PassiveDefinitionSO`. **Passive Overview** inventories all its mechanics. Imported and newly authored event rules open the same WHEN → WHO → IF → DO editor. Continuous modifiers and calculation/interception policies retain their distinct runtime semantics. See the [centralization implementation](PassiveCentralizationImplementationReport.md).

| Behavior | Use now | Migration status |
| --- | --- | --- |
| Continuous stat modifiers while an owner/roster condition holds | Continuous rules in Passive Overview | Supported availability/filter/scaling mechanism; arbitrary event predicates are not aura conditions |
| Battle/turn/action/status event → conditions → ordered actions | Trigger rules | Preferred path for new event behavior; compiles to runtime reactions |
| Existing native reaction payloads | Select their Overview entry | Representable reactions import into the shared rule editor; unsupported declarations remain read-only with a reason |
| Fixed current-hit Attack, Crit, damage or Crit Defense changes | BeforeDamage calculation rule | Synchronous local modifiers; state-changing actions and action-start snapshots are unsupported |
| Existing `CombatSupportDefinition` hooks | Individual compatibility entries in Overview | Still executable; opening Gauge is automatically represented as a rule, other clauses retain dedicated runtime/forecast behavior |

A permanent navigation page cannot be removed as if it were a mechanic. Select and remove the specific continuous rule, event rule or compatibility entry. Removing one support clause preserves its other clauses. Changes reach native assets after Save character. Do not recreate an effect in a rule while its original compatibility clause remains active. Imported version-zero rules retain native dispatch/receipt behavior; unavailable advanced frequency controls are disabled with an explanation.

WHEN selects the event or calculation stage. WHO filters the event actor and event target; each DO action chooses its own recipients. IF supports nested ALL/ANY/NOT predicates and explicit negation. Changing controls refreshes their summaries immediately. Availability and frequency remain visible independently of the selected section.

## Kaeya, based on the current native SO and runtime

| Clause | Current owner | Recommendation |
| --- | --- | --- |
| Start battle: +1 Gauge to living active allies | `studio.opening-team-gauge`, `BattleStarted`, `AllAllies` | Edit the migrated rule. Compiler sets support `OpeningTeamGauge` to zero to prevent duplicate execution. |
| Ally attacks a target that already has Frost Bite: +50% Gauge generation for one turn | Support `AttackerGaugeAgainstRecipeId` / `AttackerGaugeGainBp = 5000` | Retain compatibility until a parity migration covers the action-start test, per-action application, duration and planning forecast. |
| Kaeya attacks a qualifying target: +15 percentage points Crit Chance, +10% outgoing damage | Support `BonusAgainstRecipeId`, `BonusCritChanceBp = 1500`, `BonusDamageBp = 1000` | Retain compatibility. This snapshots target eligibility at action preparation and changes the damage calculation per hit. |
| Whole team, including reserve, qualifies for Genshin elemental reactions | Team `ElementalReactionRules.Qualifies` checks every fighter's Genshin trait | Do not recreate as a Kaeya aura or per-hit rule; it is a team engine rule. |

BeforeDamage and a current-hit Crit Chance command are now supported. They evaluate current-hit predicates, which still differs from Kaeya's action-start target snapshot. His Gauge acceleration also affects planning forecasts. Retain those compatibility clauses until an equivalent snapshot/forecast migration exists; applying a temporary whole-actor Crit buff changes multi-target behavior. New designs may use the supported fixed current-hit semantics deliberately.

There is also an existing presence mismatch: Kaeya's source wording says **SUB**, while executable support uses `LivingActive`, and the imported opening rule copies that gate. The preview now reports authoritative availability rather than source-type metadata. If reserve-only operation is intended, make and verify that deliberate gameplay change. Centralization does not silently change the live kit.

## Preview correction and verification

The passive preview now places its icon at the top right of the preview, above the wording editor, in both the Passive page and Description Studio. The native Kaeya asset has no dedicated passive art; the character icon is the fallback, with an explanatory tooltip. Fallback uses Unity's null comparison so missing serialized Sprite references cannot suppress a valid portrait.

Historical icon/fallback evidence remains in [Passive Clarity](../../Artifacts/CharacterCreatorStudio/PassiveClarity/native-evidence.json). Current centralization verification adds [18 native UI checks](../../Artifacts/CharacterCreatorStudio/PassiveCentralization/native-ui-evidence.json) and [234 native battle parity cases](../../Artifacts/CharacterCreatorStudio/PassiveCentralization/roster-parity.json). The implementation report separates fixed-hit support from remaining snapshot/forecast migration.

Relevant source: [compiler composition](../../Shared/FightingAllstar.Core/Studio/StudioCompiler.cs), [opening migration](../../Shared/FightingAllstar.Core/Studio/StudioDocumentService.cs), [support execution](../../Shared/FightingAllstar.Core/Combat/BattleEngine.CombatSupport.cs), [planning forecast](../../Shared/FightingAllstar.Core/Combat/PlanDraft.cs), [team elemental eligibility](../../Shared/FightingAllstar.Core/Combat/ElementalReactionRules.cs).
