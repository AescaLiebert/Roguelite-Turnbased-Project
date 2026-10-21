# Character Creator Studio — M3 A–F audit

20 October 2026. Scope: the roadmap's M3 acceptance contract, implementation, saved verification evidence, live EditorWindow and supplied interface concept. The concept is a UX reference; its illustrative Mian settings do not authorize implementing a playable kit.

**Historical audit:** the implementation following this audit is recorded in the [M3 closure report](M3ClosureReport.md), with fixes, current scope and fresh acceptance evidence. The findings below describe the pre-closure revision and are retained for traceability.

## Verdict

**All six packages have real engineering implementation. M3 is not fully closed as a designer-facing Studio.** The current result is a working rule-authoring prototype over a substantial combat runtime. Calling it a finished Studio, or treating every A–F capability as an integrated designer workflow, overstates the evidence.

The earlier “M3 is implemented” statement should be read as engineering coverage, not completion of the UX design or every authoring acceptance gate. The two confirmed validation defects below also prevent an unqualified completion claim. Existing passing checks remain useful; they did not exercise these boundaries.

This audit changes documentation and records evidence. It makes no runtime, production-content or draft changes.

## Package coverage

The roadmap requires each group to have capability registration, editor fields, text bindings/summary support, trace output and semantic fixtures. A runtime class plus a field picker is insufficient for that whole contract.

| Package | Implemented engineering | Designer-facing coverage and remaining gap | Assessment |
| --- | --- | --- | --- |
| A — Composition | Flat action/module tables, All/Any/Not conditions, sequences, branches, bounded loops, value/context bindings and module expansion. Compiler rejects cycles and missing node/parameter references. | Authoring works through nested forms. Binding names and many content identities are raw strings; summaries expose enum names/IDs. No contextual binding inventory or compact semantic rule view. Missing target-binding references escape validation (finding 2). | Substantial implementation; authoring incomplete. |
| B — Execution controls | Production lifecycle dispatch, v0/v1 compatibility, phase/priority ordering, receipts, cooldowns, origin filtering and shared root budgets. Tests cover ordering, limits, compatibility and rejected transactions. | Controls exist in an advanced foldout and a header summary shows phase/frequency/cooldown. No selected-rule context inspector; availability is collapsed. Action traces have event ranges but lack the observed context and branch explanations promised by the concept (finding 5). The suite's phase-order check does not itself prove every combination of retaliation, reflection, lifesteal and authored post-action work. | Core controls implemented; full authoring/ordering acceptance remains incomplete. |
| C — Resources/status lifecycle | Atomic spend/transfer, caps, exact/partial owned layer consumption, frozen removal reasons/snapshots and runtime break/source-death handling. Independent protection and absorption fixtures exist. | Resource keys and caps are repeated on operations. Inline status editor exposes stack identity and duration clock, but not `BreakRule`, status-local `Reactions`, or a central reusable resource/status definition workflow (finding 4). The existing native fixture builds the complex program in C#, then edits its cost in UI. | Runtime implemented; status/resource authoring partial. |
| D — Queries/history/transitions | Filter/sort/sample/exclude/store/reuse, logical adjacency, active RNG, owner-turn card ledger and threshold entry/exit. | Selector controls exist, including explicit anchors. Stored selections use unchecked string names; an undefined reuse binding silently selects nothing. History is an enum condition rather than an explained authoring block. | Runtime implemented; binding reliability and authoring incomplete. |
| E — Interception/policies | Incoming per-contact HP cap, synchronous fatal saves, atomic costs, gauge guards, explicit True profile and bypass precedence. Broad real-damage-path fixtures exist. | These policies have actual editable controls and bound cap/cost/heal values. They remain hidden at the bottom of the Rules page; there is no focused interceptor view or designer-authored fatal/cap scenario trial. General damage inspection belongs to M5. | Strongest coverage with B; primitives delivered, integrated workflow not fully accepted. |
| F — Variants/upgrades | Stable card identity, runtime readiness/unlock/relock/cost, resolution-time selection, independent level/rank/tier and bound overrides. | Variants clone an imported skill's ranks. UI edits existing primary/sequence fields but cannot add/remove/reorder sequence steps or author the status payload when changing effect kind. Variant payload validation has confirmed holes (finding 1). Base skills stay frozen; this is not a full skill editor. | Partial authoring and validation; not complete. |

Relevant sources: [action editor](../../Assets/Project/Editor/CharacterCreatorStudio/CharacterCreatorStudioWindow.Actions.cs), [execution/condition controls](../../Assets/Project/Editor/CharacterCreatorStudio/CharacterCreatorStudioWindow.Rules.cs), [policy/variant editor](../../Assets/Project/Editor/CharacterCreatorStudio/CharacterCreatorStudioWindow.PoliciesAbilities.cs), [M3 compiler](../../Shared/FightingAllstar.Core/Studio/StudioCompiler.M3.cs), [runtime executor](../../Shared/FightingAllstar.Core/Combat/PassiveExecution.cs), [variant runtime](../../Shared/FightingAllstar.Core/Combat/AbilityVariantSystem.cs).

## Findings in priority order

### 1. High — Invalid variant payloads pass “Validate”

`DrawVariantOperation` exposes every `EffectKind` and unrestricted coefficient fields (PoliciesAbilities.cs, lines 148–166). It offers no status recipe editor when choosing ApplyStatus. `AbilityVariantValidator.Validate` checks readiness, rank presence and overrides, but does not validate the rank effect payload (AbilityVariants.cs, lines 53–75).

Two fresh in-memory reproductions using the frozen Kaeya import returned **Success=true and zero diagnostics**:

- Set the cloned variant's primary effects to ApplyStatus with `StatusRecipe=null`.
- Set the cloned variant's damage coefficients to `-10000`.

Evidence: [variant-validation.json](../../Artifacts/CharacterCreatorStudio/M3Audit/variant-validation.json). These are reachable edits in the present form. Validation must reject them with a source link. A valid alternate skill also needs complete operation payload editing and sequence add/remove/reorder, rather than cloning whatever sequence happened to exist in the imported base skill.

### 2. High — Undefined target bindings silently suppress actions

`ValidateQuery` validates enum/filter/bounds and a lowest-layer recipe; it does not resolve `ReuseBinding`/`ExcludeBinding` against producers or execution scope (StudioCompiler.M3.cs, lines 131–141). `TargetQuerySystem.Resolve` returns an empty set when a nonempty reuse name is absent (lines 19–29).

A fresh compiled loop with `ReuseBinding="selection.never.created"` passed validation with **zero diagnostics**, visited **zero targets**, and emitted `ForEachTarget: completed.` rather than a missing-binding explanation. Evidence: [target-binding-validation.json](../../Artifacts/CharacterCreatorStudio/M3Audit/target-binding-validation.json).

The editor needs named, scoped target/value references; validation must distinguish an undefined reference from a valid selection that legitimately contains no living targets. Required missing bindings should fail explicitly. Exclusion bindings need equivalent checking because a typo can allow extra recipients.

### 3. High acceptance gap — Existing integration evidence does not demonstrate authoring A–F through the editor

`StudioM3Checks.CreateExampleDraft` calls `AddCaseStudyMechanics`, which constructs the test program directly in C# (StudioM3Checks.cs, lines 22–84). Its 15 checks prove important persistence/runtime contracts. The [designer workflow evidence](../../Artifacts/CharacterCreatorStudio/M3/designer-workflow.json) proves editing one parameter through the real UI, saving/reopening, matching wording and exact replay. It does not prove creating the protection module, typed target reuse, status lifetime or EX program from the editor.

The 333 shared assertions, 873 combat regression assertions and native checks should remain labeled engineering evidence. Close M3 with UI-authored synthetic examples for each package, including invalid edits and repairs, with save/reopen/Undo/replay. No playable Mian content is needed.

### 4. Medium — Resource/status lifecycle exists beyond what the editor can author

The inline status form (Actions.cs, lines 201–234) edits identity, clock, stacks, tags, two behavior switches and modifiers. The runtime model separately supports `StatusRecipeDefinition.BreakRule` and status-local `Reactions` (EffectRecipes.cs, lines 298 and 326). Those lifecycle choices have no corresponding editor controls. The document also has no resource/status definition table; custom recipes live inside operations, and the non-inline recipe picker is limited to standard recipes.

A designer cannot discover or centrally maintain the complete lifetime/ownership behavior promised for C. Add a resource/status workspace or focused inspector with stable references, supported break policies, source lifetime and local event rules; clearly label the unsupported remainder.

### 5. Medium — Execution evidence omits the context needed to explain composed actions

Action trace creation stores `ConditionsPassed`, event ranges and a generic completion/failure sentence (PassiveExecution.cs, lines 465–472). Unlike top-level condition tracing, it does not attach the nested action's condition observations. The scenario list renders action and rule entries as FIRED/SKIPPED (Scenario.cs, lines 53–64); a zero-target loop consequently appears skipped while its reason says completed.

The selected-rule UI needs resolved Owner/Event actor/Event target/Operation target, receipt scope, selected targets, cost before/after and selected branch. Those explanations close A–D's M3 trace contract. The larger hierarchical battle debugger and formula breakdown remain M5 work.

### 6. Medium product gap — The shell does not implement the concept's work-surface model

The current left outline lists authored rules only (Window.cs, lines 184–191). The right pane is a global parameter form (Rules.cs, lines 65–79), while the center contains the detailed settings for every nested node. Policies, modules and variants are foldouts appended to that page. The evidence dock is a Problems/status list, with the actual trace in the Scenario workspace. Pane widths/heights are fixed in USS; there is no resizable split-view or responsive inspector treatment.

The supplied concept instead uses kit navigation, compact semantic WHEN/TO/IF/DO blocks, a selected-rule inspector, explicit context bindings and a separate evidence dock. This is an information-architecture gap, not a color/theme issue. [Live current window](../../Artifacts/CharacterCreatorStudio/M3Audit/studio-current.png) confirms the difference.

## Why it feels like a draft tool

The entry path is still “Import opening gauge.” `StudioDocumentService.ImportOpeningGauge` rejects sources without that support capability (lines 13–17), and the compiler requires a frozen imported character. There is no blank-character/template flow, identity/stats workspace or editable complete base kit. That explains the product impression; it is broader than an isolated M3 runtime defect.

The concept's status is also intentionally different: its “Draft saved” label means a persistent editable authoring document. Draft persistence is a correct part of the final Studio. What is missing is the complete authoring workflow around that document.

Do not count the following planned work as accidental M3 omissions: the full wording/keyword workflow is M4; arbitrary two-team scenarios and damage debugger are M5; rigged actor/timeline authoring is M6; remaining roster migration and production activation/recovery are M7–M8. The concept's Apply to game button therefore should not be represented as delivered by the current editor-only test-bundle button.

## Recommended closure order

1. Fix variant payload validation and binding-reference/scope validation. Preserve the existing runtime and saved document contracts.
2. Complete the M3 authoring paths: variant sequence composer, applicable payload controls, lifecycle fields and contextual typed selection. Reuse the action editor where possible instead of maintaining two incomplete operation editors.
3. Implement the concept's rule workspace: readable semantic cards in the center; selected-rule/node inspector; named context/bindings; frequency/lifetime summary; rule/resource/variant navigation; focused Problems/Trace evidence. Use synthetic mechanics.
4. Complete UI author trials for A–F with invalid-state repair, persistence and deterministic replay. Tie the resulting evidence to the exact document/runtime revision.
5. Continue M4–M8 with the existing boundaries. Develop the new-character/base-kit flow as an explicit product work item rather than treating the opening-gauge importer as a general creator.

## Audit method and limits

Reviewed the roadmap, UX plan, implementation, test sources and saved M3 results; inspected the live Unity window; ran the two isolated boundary probes above. The probes used cloned definitions and wrote only audit artifacts. The full previously passing suites were not rerun for this read/audit task. Their assertion counts are historical supporting evidence, not a new claim that every editor capability passed an author trial. This is an A–F coverage and workflow audit, not a proof of every combat combination.
