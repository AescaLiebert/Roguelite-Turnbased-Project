# Passive centralization implementation

20 October 2026 · Unity 6000.3.4f1 · M6 remains paused

The Studio now has a **Passive Overview** and one event-rule authoring route. It continues to save the existing CharacterObject, two SkillCardSO assets, UltimateCardSO and PassiveDefinitionSO. No separate StudioDraft asset is required. The transient editing buffer and embedded flat authoring metadata support Undo and reconstruction.

## Delivered

- Overview inventories event/calculation rules, continuous modifiers, interception policies and individual compatibility support clauses. Kaeya shows opening Gauge, conditional Crit/damage and conditional Gauge acceleration separately. Selecting an event opens the shared WHEN → WHO → IF → DO editor; selecting a continuous rule edits its persistent modifiers and filters.
- Representable native reactions import into the same flat rules/conditions/actions as new rules. Import preserves negation, else commands, execution version, effect IDs and original dispatch order, including disable/save/re-enable. Capability preflight retains unsupported declarations with a visible reason. The competing raw base-reactions editor and unused legacy rule screen are removed.
- WHEN and WHO have unique selection IDs and distinct inspectors. Pure view projections drive initial and immediate summaries; trigger, event filters, action recipients, execution controls and continuous availability refresh after edits. Source-type wording is explicitly metadata. Preview availability comes from enabled executable mechanics, including Swirl's separate gate.
- Opening rules now retain their configured filters, limits and cooldowns through compilation and native save/reopen. Version-zero frequency/ordering fields are disabled with an explanation; existing receipt and dormant lifecycle semantics are retained.
- Native reconstruction removes generated entries only from their owning collection and scope. An unrelated native skill variant with the same ID as a passive rule survives save/reopen. Imported ownership tombstones prevent removed rules from reappearing.
- BeforeDamage authoring supports synchronous fixed local Attack, Crit Chance, damage and Crit Defense modifiers, including base-stat scaling. Compiler validation rejects state-changing programs, unresolved critical/block predicates and unsupported else calculations. The new current-hit Crit command does not modify persistent actor stats.
- The passive icon remains above the wording editor at the top right, with native portrait fallback. Availability text uses the available preview width.

Semantic import/removal lives in `StudioPassiveEditing`; compilation remains in `StudioCompiler`; pure summaries/inventory live in `StudioPassiveView`; inventory rendering lives in `StudioPassiveOverviewPresenter`; the native persistence adapter delegates ownership reconstruction to the semantic service. Schema 5 adds import provenance, native ordering, negation and else-program references while preserving older source data.

## Verification

| Check | Result / evidence |
| --- | --- |
| Shared Studio suite | **524 assertions**, including 33 centralization checks; [shared evidence](../../Artifacts/CharacterCreatorStudio/PassiveCentralization/shared-evidence.json) |
| Native roster import | **39/39 compile**, 22 reactions editable through the common representation, six preserved compatibility reactions; [inventory](../../Artifacts/CharacterCreatorStudio/PassiveCentralization/roster-import.json) |
| Native battle parity | **234 cases pass**: 39 characters × two seeds × base configuration, max rank/tier, reserve placement. Compares authoritative state, cards, statuses, receipts, events and RNG; utility skills are included. [Evidence](../../Artifacts/CharacterCreatorStudio/PassiveCentralization/roster-parity.json) |
| Actual attached-panel UI and persistence | **18 checks pass**, including immediate WHEN/WHO/availability updates, contextual selection, disabled legacy fields, collision preservation, repeated save/removal/reopen and original 195 native asset files unchanged. [Evidence](../../Artifacts/CharacterCreatorStudio/PassiveCentralization/native-ui-evidence.json) |
| Existing native kit editor | **18 checks pass**, including five-asset save, stable GUIDs, Undo/Redo and injected save failure/retry. [Evidence](../../Artifacts/CharacterCreatorStudio/KitAuthoring/native-evidence.json) |
| Existing native publishing bridge | **26 checks pass**, including conflicts, rollback, normal runtime discovery and 196 original source files unchanged. [Evidence](../../Artifacts/CharacterCreatorStudio/Bridge/native-evidence.json) |
| Existing elemental/Genshin regression | **873 assertions pass**; [output](../../Artifacts/CharacterCreatorStudio/PassiveCentralization/elemental-regression.txt) |
| Existing Training regression | **2383 assertions pass**; [output](../../Artifacts/CharacterCreatorStudio/PassiveCentralization/training-regression.txt) |

Core and editor compile without C# errors. Disposable test packages are removed, the existing roster is not rewritten, and the active scene/Play Mode remain unchanged. Historical M6 assertions remain in the shared harness; passing them does not resume or close M6.

Actual Unity captures: [Passive Overview](../../Artifacts/CharacterCreatorStudio/PassiveCentralization/passive-overview.png), [shared rule editor](../../Artifacts/CharacterCreatorStudio/PassiveCentralization/shared-rule-editor.png). The open editing session may contain pending designer edits; these captures do not imply those edits were saved to the live character.

## Remaining migration boundary

Audit closure steps 1–4 are implemented. Step 5 has fixed current-hit calculation coverage, but **action-start eligibility snapshots and dedicated support/forecast hooks are not generalized**. Step 6 therefore remains gated for those clauses. The five existing Genshin support definitions continue executing their existing engine behavior, shown as individual read-only compatibility mechanics with explicit per-clause removal. The six remaining native compatibility reactions use unsupported StealStats operations or availability gates.

Continuous rules retain their existing availability/roster filters and scaling semantics; arbitrary nested event predicates are not added to the aura evaluator. Current-hit calculation rules are not equivalent to Kaeya's action-start target snapshot, and a successful first-skill parity matrix does not prove every possible kit interaction. No support clause is silently replaced, no live character is bulk migrated, and no playable Mian kit is installed.

Use **Event rule** for new supported event behavior, **Continuous rule** for persistent modifiers, and **BeforeDamage** for supported fixed hit modifiers. Existing compatibility clauses remain authoritative until an explicit equivalent migration is available; do not duplicate their behavior in another rule.
