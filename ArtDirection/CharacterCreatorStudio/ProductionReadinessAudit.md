# Character Creator Studio — actual character creation readiness audit

20 October 2026 · Verdict: **not ready for a complete designer-driven character creation and production asset workflow**.

**Bridge follow-up:** the subsequent [native bridge implementation](BridgeImplementationReport.md) addresses F1 (publisher/runtime activation), F2 (lossless SO adapters), F5 (runtime validation), the import restriction in F3, and explicit native wording materialization in F6. Blank creation, F4's full base-kit editing UI and dynamic description routing remain open. Findings below preserve the original pre-fix evidence; they describe that audited baseline.

**Native editor follow-up:** [kit authoring](NativeKitAuthoringReport.md) adds Identity & Stats, separate skill/ultimate/passive pages and direct Save into the existing five native assets. Joe96 is the actual authoring acceptance example. A separate draft asset is no longer required. F4's missing imported-base-kit editing is addressed for these controls; blank initialization, art replacement and refined complex-mechanic authoring remain open.

The existing game can load complete ScriptableObject character sets. The Studio has working draft, rule, description and simulation components, but it does not yet connect them into a lossless author → five source assets → registry → normal runtime loop. This audit concerns the current working tree, including unfinished local changes. M6 work was stopped at the user's request and is not treated as completed release evidence.

The user's `UltimateSO` and `PassiveSO` correspond to the actual classes `UltimateCardSO` and `PassiveDefinitionSO`. One normal character requires **five source assets**: one `CharacterObject`, two `SkillCardSO` assets, one `UltimateCardSO` and one `PassiveDefinitionSO`.

## Evidence and scope

- Inspected Studio entry points, document schema, compilers, editing pages, draft/test-bundle persistence, native SO schemas, registry synchronization, runtime catalog construction and wording consumers.
- Probed every registered runtime-ready character using the real catalog builder and Studio import service: **39 runtime-ready characters, 1 accepted Studio import, 38 rejected imports**. Only Kaeya imports through the current entry point.
- Ran the real production catalog validator: **0 errors** for the existing 39-character catalog. This establishes structural validity, not faithful implementation of each character's intended kit.
- Reproduced native SO serialization loss using transient objects and Unity's `EditorJsonUtility`, and compared the result with the shared JSON serializer.
- Reproduced malformed-character acceptance, duplicate identity retention, detached description output and loss of variant/override metadata through the fields available on `SkillCardSO`.
- Re-ran the existing shared harness: **466 assertions passed**, including 20 presentation-clock checks from the unfinished M6 branch. Passing scoped tests does not prove complete character creation.
- Compared **196 existing character/card/passive/registry files** before and after the native probes: all were byte-for-byte unchanged. No implementation code or production asset was edited for this audit. Scratch build output and audit evidence were written separately.

Evidence: [native probes and source hashes](../../Artifacts/CharacterCreatorStudio/ReadinessAudit/native-evidence.json), [shared harness](../../Artifacts/CharacterCreatorStudio/ReadinessAudit/shared-evidence.json).

## Findings, ordered by release impact

### F1 · P1 · There is no production output/activation bridge

`StudioDraftRepository.ApplyTestBundle` creates a `StudioTestBundleAsset` under the Editor-only draft directory. It stores compiled JSON and a local `UseStudioDefinition` switch. It does not create or update the five native source assets. The bundle review UI explicitly describes this limited contract.

The normal game loads `CharacterObjectRegistrySO`, then `CharacterObjectCatalogBuilder` reconstructs character definitions from native card/passive references. Its `Build` method replaces the catalog character list. There is no runtime consumer of `StudioTestBundleAsset` or its source-mode switch. Therefore, a rule can work in a compiled-draft Battle Lab session while normal runtime still uses the old source assets. The local test-bundle revert switch is not production rollback.

Sources: [test bundle creation](../../Assets/Project/Editor/CharacterCreatorStudio/StudioDraftAsset.cs:160), [editor-only bundle](../../Assets/Project/Editor/CharacterCreatorStudio/StudioTestBundleAsset.cs:8), [bundle UI](../../Assets/Project/Editor/CharacterCreatorStudio/CharacterCreatorStudioWindow.cs:328), [runtime rebuild](../../Assets/Project/Runtime/Content/CharacterObjectCatalogBuilder.cs:26), [runtime validation](../../Assets/Project/Runtime/Presentation/Combat/CombatContentSource.cs:12).

Required closure: implement an explicit, reviewable creation/update transaction for the five assets and their references, source ownership, safe runtime discovery, failure recovery and rollback. Place runtime assets outside Editor folders. Prove the result through the normal catalog loader with the Studio override disabled.

### F2 · P1 · Native SO storage cannot preserve important Studio mechanics

`PassiveDefinitionSO` persists a native serialized `PassiveDefinition`, while compiled M3 reactions store their action trees in properties marked `[field: NonSerialized]`. Nested condition children and status lifecycle reactions also use non-native-serialized properties to avoid Unity type-tree cycles.

The native probe found:

| Payload | Before serialization | After Unity serialization | After shared JSON serialization |
| --- | ---: | ---: | ---: |
| Root composed actions | 1 | **0** | 1 |
| Nested condition children | 1 | **0** | 1 |

The restored reaction retained `ExecutionVersion = 1` but failed runtime validation with “commands or composed actions required.” This is not a disk-reload test; it directly exercises the native serializer used by the source asset. It is sufficient to show that simply calling `SetDefinition(compiled.Passive)` is not a lossless persistence adapter.

Separately, `SkillDefinition` supports `Variants` and `ValueOverrides`, but `SkillCardSO` stores only card metadata, status visuals and rank effects. `CharacterObjectCatalogBuilder.CreateSkill` reconstructs ranks without these skill-level tables. A transient projection using the available SO rank fields reduced **1 variant / 1 override to 0 / 0**. This demonstrates a schema/adapter gap; no existing Studio exporter is being claimed or tested.

Sources: [native passive payload](../../Assets/Project/Runtime/Content/PassiveDefinitionSO.cs:9), [nonserialized action tree](../../Shared/FightingAllstar.Core/Content/PassiveActions.cs:20), [nested conditions](../../Shared/FightingAllstar.Core/Content/EffectRecipes.cs:385), [status lifecycle reactions](../../Shared/FightingAllstar.Core/Content/EffectRecipes.cs:325), [skill SO schema](../../Assets/Project/Runtime/Content/Cards/SkillCardSO.cs:7), [skill reconstruction](../../Assets/Project/Runtime/Content/CharacterObjectCatalogBuilder.cs:114), [compiled skill metadata](../../Shared/FightingAllstar.Core/Content/CombatContent.cs:492).

Required closure: choose a lossless, versioned source contract before building the exporter. A flat/JSON payload owned by the SO, with an explicit `CreateDefinition` adapter, can preserve recursive mechanics without reintroducing Unity serialization cycles. Avoid maintaining two unsynchronized authoritative copies. Add save/reimport/domain-reload/build-consumption tests for nested actions, All/Any/Not conditions, status reactions, EX readiness and level/rank/tier overrides.

### F3 · P1 · New/general character creation is still restricted to the Kaeya slice

The only import button is **Import opening gauge**. `StudioDraftRepository.Create` always calls `ImportOpeningGauge`, which rejects a character unless `Passive.Support.OpeningTeamGauge > 0`. The default document also locates Kaeya specifically. The live roster probe accepted only Kaeya and rejected Amber, Barbara, Lisa, Lumine and every other registered character.

There is no Studio new-character wizard or empty character creation document. The compiler requires `LegacyCharacter`. Duplicate remaps local authoring IDs but intentionally retains `SourceCharacterId` and `LegacyCharacter.Id`; it is a draft duplicate, not a new roster identity. Renaming the draft to an audit marker still compiled to “[Genshin Impact] Kaeya.”

Sources: [import precondition](../../Shared/FightingAllstar.Core/Studio/StudioDocumentService.cs:13), [creation service](../../Assets/Project/Editor/CharacterCreatorStudio/StudioDraftAsset.cs:39), [entry button](../../Assets/Project/Editor/CharacterCreatorStudio/CharacterCreatorStudioWindow.cs:106), [required frozen source](../../Shared/FightingAllstar.Core/Studio/StudioCompiler.cs:21), [duplicate behavior](../../Shared/FightingAllstar.Core/Studio/StudioDocumentService.cs:102).

Required closure: separate **New character**, **Import existing character**, **Duplicate as new character** and **Duplicate draft**. General import must not require a particular passive. New identities need collision checks for definition IDs and game-facing numeric IDs, plus correctly remapped local skill/passive/status references.

### F4 · P1 · Base character, skill and ultimate authoring is incomplete

The Studio schema carries a frozen `LegacyCharacter`; its compiler clones that snapshot and adds authored rules, policies, variants and value overrides. Current pages do not provide a complete editable identity/stats/growth/traits/art/card-reference model. The abilities page explicitly says imported base ranks remain frozen. Its skill picker only enumerates imported skills; there is no dedicated authoring page for the six ultimate tiers or a native SO picker/importer for independent skill, ultimate and passive assets.

The variant editor can express new variant effects, and overrides can change supported resolved values. Those are real capabilities. They do not replace authoring two base skills across three ranks, an ultimate across six tiers and all character metadata from a new document. An M6 cosmetic `$ultimate` binding does not implement gameplay ultimate authoring.

Sources: [document schema](../../Shared/FightingAllstar.Core/Studio/StudioModels.cs:150), [snapshot clone](../../Shared/FightingAllstar.Core/Studio/StudioCompiler.cs:86), [workspaces](../../Assets/Project/Editor/CharacterCreatorStudio/CharacterCreatorStudioWindow.Workspaces.cs:92), [base-rank restriction](../../Assets/Project/Editor/CharacterCreatorStudio/CharacterCreatorStudioWindow.PoliciesAbilities.cs:57), [skill-only compilation](../../Shared/FightingAllstar.Core/Studio/StudioCompiler.M3.cs:257), [native character fields](../../Assets/Project/Runtime/Content/CharacterObject.cs:96).

Required closure: add canonical editable identity/stat/growth/art sections, base skill rank/effect/sequence pages, C0–C5 ultimate authoring and complete passive source ownership. Preserve imported behavior as an explicit mode and show which fields are editable, inherited or still legacy-owned.

### F5 · P1 · “Validate” does not establish runtime character validity

`StudioCompiler` runs substantial rule, policy, variant, binding and description checks, but does not run the complete catalog/character validation path used by `CombatContentSource`. An in-memory imported draft containing attack `-100`, no rank entries on Skill 1 and no ultimate tiers compiled with `Success = true` and no diagnostics. The production content validator rejected the same compiled character for all three defects.

The missing-rank/tier corruption was injected into an in-memory draft for this test; it is not an editable base-rank UI path today. The negative-stat case is also relevant to imports from externally edited SOs. This finding limits the meaning of the current green Validate state and becomes a direct publishing risk once creation/export is added.

Sources: [Studio validation boundary](../../Shared/FightingAllstar.Core/Studio/StudioCompiler.cs:114), [successful result assignment](../../Shared/FightingAllstar.Core/Studio/StudioCompiler.cs:144), [full runtime checks](../../Shared/FightingAllstar.Core/Content/CombatContent.cs:614), [runtime load gate](../../Assets/Project/Runtime/Presentation/Combat/CombatContentSource.cs:15).

Required closure: distinguish draft validity, executable-preview validity and publishable-character validity. Publishing must validate a reconstructed candidate catalog against the actual recipes, registry identities, required ranks/tiers, native references and supported runtime schemas. Errors must identify the editable source field.

### F6 · P1 · Description authoring is not connected to shipped wording slots

The writer supports typed values, keywords, colors, snippets, locales and configuration previews. Its compiled text is returned as a separate `DescriptionCompileResult` and stored as separate JSON in a test bundle. It does not assign authored text to `CharacterObject` passive source wording, `CardRankData.description`, `UltimateLevelData.description` or a production description provider.

The probe compiled `AUDIT_STUDIO_WORDING_ONLY` successfully, while `compiled.Character.PassiveSource.Description` retained the original Kaeya prose. The runtime builder formats wording from native source fields, and the normal detail/inspector consumers read those runtime/native descriptions. Previewing the same text in a surface-shaped renderer does not install it into that consumer.

Resolved description bindings currently select from `LegacyCharacter.Skills`, not ultimate tiers. A complete description destination model is also needed: passive, each skill/rank, ultimate/tier, status tooltip and glossary, with locale/configuration behavior preserved.

Sources: [separate text compilation](../../Shared/FightingAllstar.Core/Studio/StudioCompiler.cs:125), [test-only description JSON](../../Assets/Project/Editor/CharacterCreatorStudio/StudioDraftAsset.cs:172), [skill-only bindings](../../Assets/Project/Editor/CharacterCreatorStudio/CharacterCreatorStudioWindow.DescriptionLibrary.cs:147), [native wording reconstruction](../../Assets/Project/Runtime/Content/CharacterObjectCatalogBuilder.cs:91), [runtime card consumer](../../Assets/Project/Runtime/Presentation/Combat/BattleCharacterInspector.cs:333).

Required closure: define per-slot text destinations and a runtime renderer/localization adapter, or materialize supported per-rank/per-tier text into native fields with explicit loss/compatibility rules. Verify actual runtime consumers after export. Existing M4 evidence records blocked Thai glyph profiles for the Barlow runtime font; that release issue needs fresh verification when the final runtime font contract is chosen.

## Cross-cutting production gaps

These are P2 integration gaps rather than additional claims of currently broken gameplay:

- **Existing registry sync is real.** `CharacterRegistrySync` indexes authored `CharacterObject` assets automatically. Registry discovery does not need to be reinvented. Creation still needs a staged, validated ready-state transition; registry presence alone does not prove a complete playable kit.
- **Ownership/conflict handling is missing for production edits.** Draft imports are deliberately frozen, and the test-bundle guard compares draft revision/hash. It does not compare the current native source with its import revision. A future exporter must detect source edits made since import and protect shared asset references. Existing content generators also need explicit ownership rules before Studio-owned assets can be safely regenerated.
- **Shared libraries are currently draft-owned.** The description/mechanic libraries are serialized inside each draft's `LibraryJson`. Local reuse and pinned snippets work; a project-wide library authority, cross-character dependency impact view and conflict-safe shared updates are not demonstrated.
- **No complete creation acceptance trial exists.** Current harnesses prove their scoped rule/text/simulation contracts. They do not create a new identity, author all five source assets, reopen them and load that identity through the normal game's catalog without a Studio override.
- **M6 is unfinished.** Its partial code remains in the working tree. A native console error in `BattlePlaybackDriver.FreezeClip` requests `Editor.AnimationUtility` for clip event editing. No M6 fix was made during this audit, and its earlier “delivered” wording must not be used as evidence of a closed release gate.

Sources: [registry synchronization](../../Assets/Project/Editor/CharacterRegistrySync.cs:25), [draft-local library storage](../../Assets/Project/Editor/CharacterCreatorStudio/StudioDraftAsset.cs:22), [draft-only apply guard](../../Assets/Project/Editor/CharacterCreatorStudio/StudioDraftAsset.cs:163), [M6 incomplete path](../../Assets/Project/Runtime/Presentation/Combat/BattlePlaybackDriver.cs:249).

## Readiness by source asset

| Source | Existing game loading | Studio authoring today | Ready for complete creation? |
| --- | --- | --- | --- |
| `CharacterObject` | Builds identity, metadata, stats, visuals and card/passive references | Frozen source import; no new identity or full metadata/stat editor/export | **No** |
| Two `SkillCardSO` assets | Reads three rank effects per skill | Frozen base ranks; editable compiled variants and selected upgrade values; no SO output; variants lack native storage | **No** |
| `UltimateCardSO` | Reads six C0–C5 effects | Preserved imported tiers; no complete gameplay tier editor, native output or resolved ultimate text binding | **No** |
| `PassiveDefinitionSO` | Reads existing native passive definitions | Generic rule/action/resource/status/policy authoring works in compiled JSON; native serialization loses composed actions/nested conditions | **No** |
| Wording/library data | Existing consumers read native/runtime descriptions | Rich writer and previews work; outputs are detached from production wording destinations | **No** |
| Battle Lab | Production engine and real 3D runtime presentation | Useful for testing frozen compiled drafts and registered actors | **Useful verification tool; does not publish a character** |

## Recommended closure order

1. **Settle the source persistence contract first.** Keep the requested SO workflow, but add a lossless versioned adapter for composed passives, nested conditions, variants, upgrade data and production wording. Specify ownership and migration. Prove SO save/reimport/reload fidelity before building more UI.
2. **Deliver general creation/import.** Create blank documents and import any valid registered character or selected source asset set. Add complete identity, base skill, ultimate, passive and description-slot authoring. Keep draft duplication distinct from new-character duplication.
3. **Build the source-asset transaction.** Preview exact asset creates/updates/reference changes, preserve GUIDs on intentional updates, protect shared assets, validate the candidate catalog and activate runtime-ready state last. Include recoverable partial failure and revert.
4. **Close the real runtime loop.** Load the new character through `CharacterObjectRegistrySO` and `CombatContentSource`, then verify normal selection/loadout/battle and actual description consumers. Disable the compiled-draft Battle Lab override for this gate.
5. **Only then resume presentation expansion and wider migration.** Use Mian as synthetic case-study acceptance, not a required playable roster entry.

## Definition of ready

A designer can create a fresh character ID, set identity/stats/traits/art, author both skills across ranks 1–3, author ultimate C0–C5, author a nested passive and localized descriptions, generate the five source assets and reload them with identical behavior. The normal runtime loads that ID without a Studio override; resolved skills, passive triggers and wording match the reviewed revision. Reopen/domain reload, duplicate-as-new, source conflicts, invalid references, partial export failure and rollback all preserve the previous working character. Completing this loop is the readiness gate; another preview-only milestone is not a substitute.
