# Passive architecture and UI audit

20 October 2026 · Current working tree · Audit and proposed closure; no implementation changes in this audit

Follow-up: the [centralization implementation report](PassiveCentralizationImplementationReport.md) records delivered correctness, ownership, overview and shared-authoring work, current-hit calculation support, verification and remaining support/forecast migration boundaries. Findings below describe the pre-implementation audit snapshot.

**Verdict: centralization is warranted.** The native save bridge works, but passive authoring still exposes implementation history as several competing editing paths. The most serious issue is not appearance: some editable settings are discarded by compilation, and reconstruction infers ownership from unscoped string IDs. Rename the entry point **Passive Overview**, then converge all event-driven behavior on one rule editor and one compile contract.

Centralize the authoring model, ownership, conditions, selectors, summaries and diagnostics. Keep continuous modifiers and calculation interception as explicitly typed semantics; translating every aura into an ordinary combat event would introduce incorrect application, stacking and removal behavior.

## Scope and evidence

- Read the native passive schemas, Studio document/compiler/import services, native save/reopen adapter, rule and kit editors, summary refresh code, runtime dispatcher, aura evaluator, support hooks and passive preview.
- Inspected all **39 registered characters**: **32** have auras, **14** have reactions, **5** have configured compatibility support, **28** native reactions use execution version zero, **1** uses version one, and **3** characters contain embedded Studio authoring metadata. These categories overlap.
- The normal production catalog validator returns **zero structural errors**. That does not establish field-to-runtime parity or completed migration.
- Reproduced ignored compatibility-rule settings and stale event-filter summaries through actual UI controls. A composed version-one rule was checked separately to distinguish display failure from compile failure.
- Reproduced an ownership collision with a synthetic, compiler-valid in-memory definition; no native file was written.
- Compared **196 native roster/card/passive/registry files** during the UI probes: unchanged. No runtime/editor source code was modified for this audit. Evidence files and this report were added.

Evidence: [native UI probes](../../Artifacts/CharacterCreatorStudio/PassiveArchitectureAudit/native-evidence.json), [roster census](../../Artifacts/CharacterCreatorStudio/PassiveArchitectureAudit/census.json), [synthetic ownership probe](../../Artifacts/CharacterCreatorStudio/PassiveArchitectureAudit/ownership-probe.json).

## Findings

### F1 · P1 · Editable execution settings can be discarded while compilation succeeds

In the imported Kaeya opening rule, the inspector accepted Actor relation = Enemies, Event target relation = Self, Activation scope = Battle and Cooldown = 3. Compilation still succeeded, producing **Any, Any, None and 0**, respectively, with `ExecutionVersion = 0`.

The compiler creates a fresh `PassiveReactionDefinition` when `ActionRootId` is empty, instead of cloning `Settings`. It copies only selected fields and emits the special opening-gauge command. The contextual inspector nevertheless exposes the general execution controls. Runtime version-zero handling also bypasses the version-one execution controls. This is a silent contract violation, not merely stale text.

Sources: [special compile path](../../Shared/FightingAllstar.Core/Studio/StudioCompiler.cs:94), [execution controls](../../Assets/Project/Editor/CharacterCreatorStudio/CharacterCreatorStudioWindow.Rules.cs:82), [contextual inspector](../../Assets/Project/Editor/CharacterCreatorStudio/CharacterCreatorStudioWindow.Workspaces.cs:185), [versioned runtime controls](../../Shared/FightingAllstar.Core/Combat/PassiveExecution.cs:342).

Required closure: normalize migrated opening rules to the same version-one action structure as new rules, with explicit parity tests. Until normalized, disable unsupported controls and explain their compatibility status. No enabled field may disappear from the compiled definition without a blocking diagnostic.

### F2 · P1 · Native reconstruction can delete unrelated content after an ID collision

`RemoveCompiledAdditions` collects IDs of Studio rules, policies, variants and overrides, then walks every JSON array in the entire character and removes any object whose `Id` matches. It does not check the object's type, owning skill, source or expected path.

A synthetic native skill variant was assigned the same ID as the opening passive rule. The Studio compiler accepted the combined character. Reconstruction then reduced native skill variants from **1 to 0** while trying to remove generated passive additions. This is a demonstrated in-memory reconstruction defect, not a claim that an existing roster variant has already been lost. Normal generated namespaces reduce likelihood but do not establish safe ownership.

Source: [ownership reconstruction](../../Assets/Project/Editor/CharacterCreatorStudio/StudioProductionBridge.Direct.cs:47).

Required closure: use typed, owner-scoped provenance records: generated character reactions, generated status reactions, generated policies, skill-owned variants and their overrides. Remove only the exact recorded object in its expected collection. Detect collisions before save and verify that reopen preserves unrelated native data. Keep existing five SOs; another draft SO is not needed.

### F3 · P2 · Rule summaries are stale and “TO” conflates filters with recipients

In a composed version-one rule, changing Actor relation from Any to Enemies correctly changed the compiled reaction to Enemies, but the visible card still read **Actor: Any · event target: Any**.

`RefreshSemanticSummaries` updates action, condition, resource, status and policy text plus rule names; it does not refresh rule event/filter cards or the execution subtitle. WHEN and TO are constructed once, and share the same `card.<ruleId>` and `summary.<ruleId>` names. Their selection identity also sends both to the same undifferentiated rule inspector.

The TO card describes who caused/received the event. It does **not** describe action recipients. The probe displayed Any/Any while the actual action recipient was Self. An event target filter, a target query and an action recipient are separate concepts, but their presentation currently blurs them.

Qualification to the reported WHEN symptom: changing a supported trigger on a composed rule to BeforeAction **does** rebuild WHEN to “Before Action” and compiles BeforeAction. WHEN is therefore not universally frozen. However, it omits execution filters, availability and frequency, while neighboring summaries can remain stale. Changing the compatibility opening rule's trigger without conversion instead produces `trigger.legacy-slice`.

Sources: [card construction and duplicate names](../../Assets/Project/Editor/CharacterCreatorStudio/CharacterCreatorStudioWindow.Workspaces.cs:128), [partial refresh](../../Assets/Project/Editor/CharacterCreatorStudio/CharacterCreatorStudioWindow.Workspaces.cs:298), [edit refresh path](../../Assets/Project/Editor/CharacterCreatorStudio/CharacterCreatorStudioWindow.cs:227).

Required closure: one derived RuleViewModel, unique `(rule ID, section)` identities, and typed summary functions used for initial rendering and updates. Split **WHEN** (event), **WHO** (event actor/target filters), **IF** (predicates), and **DO** (actions with explicit recipients). Put availability and frequency in a compact persistent strip. Recipients belong with each action; a nested action program can have several different recipients.

### F4 · P2 · Imported event behavior has a second authoring language

The Passive page exposes native `Reactions` through the generic recursive schema editor, while Trigger rules edits flat Studio rules/conditions/actions. Both become `PassiveDefinition.Reactions`, but only the second path gets the readable rule cards, selection inspector and Studio node diagnostics. Support adds a third source of event/calculation behavior. The overview does not yet provide a single selectable inventory covering all these sources.

This is why the UI feels detached despite a shared runtime container. A rename alone will not resolve it. The current compatibility explanation is useful, but a designer still has to understand native reactions, Studio rules, support fields, ownership flags and execution versions to decide where an effect belongs.

Sources: [separate passive editors](../../Assets/Project/Editor/CharacterCreatorStudio/CharacterCreatorStudioWindow.Kit.cs:581), [flat authoring model](../../Shared/FightingAllstar.Core/Studio/StudioModels.cs:35), [compiled composition](../../Shared/FightingAllstar.Core/Studio/StudioCompiler.cs:88).

Required closure: import every representable native event reaction into one rule-authoring representation. Show unsupported imports in that same inventory with explicit read-only compatibility status and a reason. Do not keep an unrestricted parallel raw reaction editor as the main authoring route. Continuous auras remain a typed mechanic with shared availability/filter/condition controls.

### F5 · P2 · Displayed availability can contradict executable availability

Source type and restriction are editable prose/metadata fields. Preview badges use source-type metadata, while runtime availability comes from support presence and individual aura/rule gates. Kaeya demonstrates the contradiction: source type SUB produces a reserve badge, but support and the migrated opening rule use LivingActive.

This is especially confusing in an overview: it looks as though changing the displayed source type configures when the passive works. It does not rewrite the support or rule gates. Mixed availability across multiple mechanics also cannot truthfully be reduced to one unconditional passive-wide badge.

Sources: [metadata controls](../../Assets/Project/Editor/CharacterCreatorStudio/CharacterCreatorStudioWindow.Kit.cs:592), [preview badges](../../Assets/Project/Editor/CharacterCreatorStudio/CharacterCreatorStudioWindow.NativeDescriptions.cs:107), [runtime gate](../../Shared/FightingAllstar.Core/Combat/CharacterPassiveRuntime.cs:74), [compatibility presence](../../Shared/FightingAllstar.Core/Combat/BattleEngine.CombatSupport.cs:9).

Required closure: derive execution badges from authoritative mechanic gates. Label authored prose as prose. Use a passive-wide default availability with explicit per-mechanic overrides only if the compiler resolves both consistently. Show “Mixed availability” when appropriate; mismatches should be diagnostics, not hidden assumptions.

### F6 · P2 · Runtime support cannot yet be represented completely by the preferred editor

The five Genshin support definitions still execute through dedicated battle-start, action-preparation and hit-calculation hooks. The general authoring catalog excludes BeforeDamage and lacks a current-hit Crit Chance modifier command. The opening-gauge import is a special migration rather than a complete support adapter.

For example, Kaeya's target-qualified Crit/damage bonus snapshots qualifying targets during action preparation and modifies each hit's calculation. A temporary whole-actor Crit buff or a DamageResolved rule is not equivalent. Gauge acceleration also affects planning forecasts, so replacing only authoritative execution would leave forecasts inconsistent.

Sources: [support hooks](../../Shared/FightingAllstar.Core/Combat/BattleEngine.CombatSupport.cs:38), [authoring trigger allow-list](../../Shared/FightingAllstar.Core/Studio/StudioAuthoringCatalog.cs:12), [command vocabulary](../../Shared/FightingAllstar.Core/Content/PassiveRules.cs:14), [planning support](../../Shared/FightingAllstar.Core/Combat/PlanDraft.cs:171).

Required closure: define typed calculation modifiers with exact timing, eligibility snapshot, per-target scope and precedence, then share their evaluation between runtime and forecast. Migrate support clause by clause only after equivalence tests. A centralized UI must clearly disclose capabilities that remain unsupported.

## Recommended centralized architecture

Preserve the existing **CharacterObject + two SkillCardSO + UltimateCardSO + PassiveDefinitionSO** persistence model. Use one embedded passive authoring document and one compiler contract; the in-memory editing buffer remains an Editor concern.

| Component | Responsibility |
| --- | --- |
| Passive authoring document | Stable mechanic IDs, typed mechanics, shared gates/queries/conditions/action programs, explicit provenance |
| Import/migration adapter | Translate supported native declarations into that document; retain unsupported declarations with visible compatibility state |
| Editing service | Add/remove/duplicate/reorder mechanics and nodes, Undo, validation and dependency invalidation; UI does not manipulate compiler internals |
| Passive compiler | Validate capabilities and references, lower every enabled mechanic, emit typed runtime content and source maps |
| Summary/view-model builder | Pure projections for overview rows, WHEN/WHO/IF/DO, availability, recipients, execution limits and diagnostics |
| Native persistence adapter | Write/reconstruct the same five assets losslessly using typed ownership; no whole-character ID sweeping |
| Existing runtime evaluators | Execute committed-event rules, continuous modifiers and calculation/interception policies with their distinct semantics |

Use explicit mechanic kinds:

- **Event rule:** event → event filters → predicates → ordered actions.
- **Continuous rule:** availability + recipient query + predicates → persistent modifiers, recomputed and removed by the aura evaluator.
- **Calculation/interception rule:** a supported calculation stage + scope + predicates → typed calculation modifiers/policy.
- **Compatibility mechanic:** preserved executable declaration that cannot yet be translated; read-only in normal authoring, with a migration explanation.

Continuous rules can share most controls with event rules, but need not pretend to have a one-shot battle trigger. This preserves reliable reserve entry/exit, defeat, stat recalculation and removal semantics. Shared conditions must define which context subjects exist for each mechanic kind; unavailable event subjects should be rejected.

Do not centralize by creating a larger EditorWindow partial class or a single universal reflection-based payload editor. Separate semantic editing, compilation, presentation and persistence behind the responsibilities above. Reuse the existing runtime and deterministic fixtures where they already meet the contract.

## Proposed UI

**Passive Overview** is the permanent entry point. It contains player wording/preview and a complete mechanic list, with a name, plain-language behavior summary, kind, availability, supported/compatibility state and diagnostics. Every imported event reaction and authored event rule opens the same rule editor. The overview does not expose a competing raw trigger editor.

The selected rule editor presents:

1. Name, enabled state, owner availability and frequency.
2. **WHEN:** event or calculation stage; continuous rules instead show “While available.”
3. **WHO:** event actor/target filters where applicable.
4. **IF:** nested conditions with readable ALL/ANY/NOT groups.
5. **DO:** ordered effects; each displays its actual recipients, values and success/failure branch.

Clicking a section selects that section's inspector. Editing a setting updates its card, overview summary and diagnostics immediately from the same view model. Unsupported choices are disabled with a concrete reason. Advanced execution options stay available without hiding basic event/recipient controls under a generic advanced foldout.

For Kaeya, the overview would list opening Gauge, Frost Bite Gauge acceleration, and Frost Bite Crit/damage separately. His team elemental qualification should be shown as a referenced engine capability rather than duplicated as another passive effect. Existing SUB-versus-active behavior must be resolved as a deliberate kit decision, not silently altered during migration.

## Ordered implementation closure

| Step | Work | Acceptance |
| --- | --- | --- |
| 1 · Correctness | Normalize opening-rule settings or block unsupported edits; fix live section summaries and unique selection identities | An enabled setting survives compile/save/reopen; changes refresh WHEN/WHO/DO and execution strip immediately |
| 2 · Ownership | Replace unscoped ID reconstruction with typed source maps and collision validation | Synthetic colliding native variant survives reopen; generated rules are not duplicated; unrelated native assets remain unchanged |
| 3 · Passive Overview | One inventory across native/authored mechanics, authoritative availability badges, contextual navigation | A designer finds and edits/removes every event mechanic without choosing between two authoring systems |
| 4 · Shared authoring | Normalize representable imported reactions; shared condition/query/action controls and capability diagnostics | Imported and newly authored equivalent rules render, edit, validate and execute identically |
| 5 · Calculation coverage | Add typed target-specific calculation modifiers and forecast integration | Kaeya support clauses pass seeded multi-target, per-hit, crit, gauge, reserve and forecast equivalence tests |
| 6 · Migration closure | Remove compatibility only after per-character parity and native reload checks | No duplicate behavior, unsupported editable fields or silent declaration loss; five-asset persistence retained |

Required tests include actual-widget edits without navigation/rebuild, section selection, disabled-field explanations, defaults versus overrides, mixed availability, compilation versus runtime receipts, remove/re-add, duplicate identities, Undo/Redo, repeated save/reopen, Play Mode recovery, missing assets and failure rollback. Existing scoped shared assertions remain useful, but they did not catch the enabled-field mismatch reproduced here.

## Audit boundary

This turn supplies the audit and a concrete centralization plan. It does not rename or refactor the current Studio, migrate characters, change Kaeya's gates, or resume M6. Readiness remains **functional native editing with significant passive authoring/ownership closure required**.
