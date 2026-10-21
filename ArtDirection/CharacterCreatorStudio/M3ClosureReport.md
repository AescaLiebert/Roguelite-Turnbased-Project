# Character Creator Studio — M3 closure

20 October 2026 · Unity 6000.3.4f1 · Closure of the [M3 A–F audit](M3Audit.md).

The Rules workspace now separates a readable rule document from the selected-node inspector. The implementation includes the missing validation, shared-definition authoring and production trace context, with a UI-driven acceptance trial. Mian remains a case study; the trial creates a separate synthetic draft from the supported Kaeya import.

## Audit findings addressed

| Finding | Delivered closure | Evidence |
| --- | --- | --- |
| Invalid alternate effects passed Validate | Primary and sequence effects receive payload validation: kind/target/scaling, coefficients, logical contacts, status recipe/layers/chance/duration and healing basis. Diagnostics identify the variant. | Shared boundary assertions; native trial introduces a negative EX coefficient, locates the variant error and repairs it. |
| Missing saved-target binding silently selected nothing | Definite-assignment analysis walks sequences, guards, branches, loops and module calls. Both target reuse and exclusion require an available producer. A valid empty selection is distinct from an undefined reference. Direct runtime calls reject missing required bindings. | Shared checks cover reordered producers, single/both branches, zero-iteration loops and explicit runtime failure; native trial renames and repairs a producer. |
| Fixtures were authored in C# rather than through the editor | A separate trial uses ordinary UI Toolkit fields and real PointerDown/PointerUp events. It reads documents to locate IDs; it does not construct or mutate their graphs directly. | 93 successful steps and 21 assertions in [UI author trial](../../Artifacts/CharacterCreatorStudio/M3Closure/ui-author-trial.json). |
| Incomplete resource/status authoring | Schema3 adds shared resource and status tables. Counter/transfer caps derive from their shared resource. Status authoring includes identity, stacking, duration/source-break policy and status-owned event rules using the same flat action/condition editor. | Shared compilation, duplication, actual instance/removal callback and migration checks; native author trial. |
| Action traces lacked observed context | Traces retain immutable actor/event/operation identities, selected targets, nested condition observations, value bindings, branch and resource before/after. Zero-target loops report No targets. The dock links back to the source and selected inspector. | Shared snapshot/payment checks and native EX payment 4→2 / absorption count 3 assertions. |
| Form shell did not follow the concept's editing model | Kit outline, semantic WHEN/TO/IF/DO cards, selected-rule/node inspector, explicit frequency/presence, contextual typed pickers, dedicated parameter/module/resource/status/variant workspaces and a persistent Problems/Trace/State diff dock. Pane sizes use UI Toolkit split views with saved view data. | Live native screenshots and the author trial. |

## Authoring behavior

Click a rule to read its trigger, relation filters, nested conditions and ordered actions in the center. Click a card to edit that node in the inspector. Containers expose success/failure or ordered children. Rule settings keep frequency and owner presence visible; event filters and ordering are available in the advanced section. Actions and conditions can be duplicated without sharing their private graph; module calls intentionally refer to the shared module.

The Resources & statuses workspace manages named resources, shared recipes and interception policies. A resource definition cannot be removed through the UI while its actions/policies/variants still reference it. A shared status can own local event rules; these compile into the status instance rather than a global character reaction. Compilation rejects cyclic shared-status local-program dependencies. Existing inline and standard recipes remain supported.

The variant composer adds, removes and reorders sequence steps for each rank. It exposes the applicable status, recovery or damage payload rather than presenting unrelated numbers. Level, card rank and tier remain independent. Value overrides continue to bind typed parameters.

Binding pickers show values or target sets available at that point in the current rule. Values produced on only one conditional path or inside a possibly empty loop are not promised to later siblings. Modules are checked in their call context. Invalid drafts remain saveable and diagnostics link to the affected source. A binding edit cannot silently change an exclusion into a broader selection.

## Acceptance scenarios and replay

The existing fixed opening-skill scenario is joined by two bounded M3 presets: IncomingCap and FatalSave. They exercise one synthetic incoming lethal contact against the authored defender. These are explicit scenario overrides on cloned definitions, not source edits or a general battlefield editor.

The cap is checked against the committed HP loss of that contact. Ordinary later turn recovery can change final HP; it does not invalidate the contact cap. The fatal-save trial verifies survival and the exact three-charge payment. Scenario assets now save the run's actual immutable input plan, including an opponent command, rather than reconstructing a player opening command.

## Persistence and compatibility

New documents use schema3. Explicit migrations 1→2→3 clone the original source and add the new tables. Reading/recovery does not rewrite the saved source until an edit/save. Existing version-zero imports keep their execution semantics. Unknown future schemas remain blocked with source retained.

Whole-document duplication remaps library IDs, status ownership, lifecycle filters, resource references, module/action/condition IDs, parameters and variant/override links. External standard content identities remain stable. The runtime models remain Unity independent; editor-only panels and fixtures stay under the Editor folder.

## Verification

- **365 shared assertions pass**: the previous 333 plus 32 closure checks. [Core evidence](../../Artifacts/CharacterCreatorStudio/M3Closure/core-evidence.json).
- **873 existing elemental/Genshin regression assertions pass.** This is the relevant existing subset, not a claim that every unrelated repository suite passed.
- **93 UI authoring steps and 21 assertions pass**, including module creation/call, resource threshold, owned absorption/bound burst, selector repair, status-local rule, EX sequence/reordering/repair, independent level override, toolbar Undo/Redo, save/reopen, cap/fatal outcomes and exact replay. [Trial implementation](../../Assets/Project/Editor/CharacterCreatorStudio/StudioClosureAuthoringTrial.cs).
- Native asset duplication/reimport succeeds without changing the source draft; five existing actor/TMP/Thai-font lifecycle checks pass. [Native evidence](../../Artifacts/CharacterCreatorStudio/M3Closure/native-evidence.json).

The consolidated [verification index](../../Artifacts/CharacterCreatorStudio/M3Closure/final-verification.json) records the final checks and captures. The trial's draft/scenario asset paths are recorded in its evidence for direct inspection.

Native captures: [rule authoring](../../Artifacts/CharacterCreatorStudio/M3Closure/studio-rules.png), [observed execution inspector](../../Artifacts/CharacterCreatorStudio/M3Closure/studio-inspector.png), [shared definitions](../../Artifacts/CharacterCreatorStudio/M3Closure/studio-resources.png) and [variant composer](../../Artifacts/CharacterCreatorStudio/M3Closure/studio-variants.png). Viewing the variant workspace preserves the in-memory document; the final trial's runtime/editor assembly hashes match the verified binaries.

## Scope after closure

This closes the audited M3 rule-authoring gaps for the supported vocabulary and synthetic acceptance paths. The original concept remains a product direction, not a promise that all later workspaces are finished. Full wording/vocabulary workflows remain M4; arbitrary two-team battlefield authoring and the formula debugger remain M5; rigged profiles/timelines remain M6; roster migration and production activation/recovery remain M7–M8. Blank-character creation and complete base-kit editing require their own explicit product work; the supported import remains visible and limited.
