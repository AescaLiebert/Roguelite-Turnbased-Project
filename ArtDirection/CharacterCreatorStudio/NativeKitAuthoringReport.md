# Character Creator Studio — native kit authoring

20 October 2026 · Supersedes the separate-draft workflow for normal character editing

The Studio now opens a **CharacterObject and its four referenced data SOs directly**. Save character updates that existing five-asset set. A separate StudioDraft asset is not required or created. The preceding [bridge report](BridgeImplementationReport.md) describes the earlier publishing-first workflow; direct editing is now the default.

## Kit navigator and pages

- **Overview:** identity/stat summary, both skills with all rank summaries, ultimate tier scaling, passive summary and links into each editor.
- **Identity & stats:** display name, family/series/role/attribute/rarity, traits, level-one stats, secondary stats, derived Combat Class settings and progression growth. Stable native IDs are preserved on Save; Save as new character assigns another identity.
- **Skill 1 / Skill 2:** independent ranks 1–3, base and per-rank category/target, primary effect, ordered follow-up effects, sequence reordering/removal/addition, logical hit count and per-rank player/source wording.
- **Ultimate:** independent C0–C5 category, effect program and wording.
- **Passive:** player wording/source metadata, continuous auras with target/availability filters and effective stat modifiers, base reactions/policies, and links to the existing trigger → condition → action rule workspace.

Mechanics appear before collapsed wording/reference panels on skill pages. Less common nested conditions, status recipes, batches and policy fields use typed, lazy controls in advanced foldouts; authors do not have to edit JSON. Existing parameter, resource/status, variant/upgrade, description, reusable module and Battle Lab workspaces remain available.

Imported modifier enum mirrors are resolved when displayed, and an edit clears the stale mirror. This applies to passive auras and status modifiers inside skill effects. Choosing Defense therefore changes the stat combat actually uses.

## Saved ownership

The existing CharacterObject owns identity, stats and small embedded Studio rule/writer/library metadata. The two SkillCardSO assets, UltimateCardSO and PassiveDefinitionSO own their complete canonical runtime payloads. Recursive payloads retain the versioned serialization adapter delivered by the bridge.

An in-memory ScriptableObject buffer supplies Undo, preview and validation. It has no asset path. The implementation reuses the older draft envelope class internally and keeps old draft assets readable for compatibility; the normal UI no longer asks designers to select or import a separate draft. Embedded authoring metadata contains **no duplicated frozen or authored CharacterDefinition snapshots**. Reopening reconstructs base kit data from the native assets and removes generated additions before recompilation, preventing duplicate rules on repeated saves.

Schema 4 distinguishes editable base-kit ownership from the frozen comparison source in memory. Schema 1–3 drafts migrate without silently changing their behavior. Native wording now participates in the text and bundle hashes while remaining excluded from the gameplay hash.

## Save behavior

**Open character → choose a kit page → edit → Validate/Battle Lab → Save character.** The native asset Inspectors also provide **Open in Character Creator Studio**, selecting the corresponding kit page.

Save requires a valid reconstructed character and stable Edit Mode. It checks native source changes and rejects data assets shared by another character. A failed write restores the preceding five files. Successful saves preserve all five GUIDs and participate in Unity Undo/Redo. Pending edit recovery uses Library; it is an editing journal, not another content asset. Native files remain the runtime source of truth.

Save as new character retains the reviewed native publisher for creating another identity. Old draft/test-bundle workflows remain compatibility paths. M6 remains paused.

The native character path survives Editor/Play Mode reloads independently of the temporary buffer. Pending edits recover after returning to Edit Mode; the battle can still load the saved native SOs independently of those edits.

## Joe96 acceptance

Joe96 was authored through the actual Studio controls and saved into his existing five native assets, retaining identity `fighter.joe96`, numeric ID 1316 and all five GUIDs. No second roster character or new draft asset was created.

| Section | Saved behavior |
| --- | --- |
| Object | ATK 250, DEF 200, HP 4,500; existing growth and native presentation retained |
| Skill 1 | All-enemy 120/120/180% damage, two hits; rank-dependent Disable Debuff / Disable Buff steps retained |
| Skill 2 | Single-enemy 200/360/450% damage, four hits; team Block Chance and rank-three Defense protection retained |
| Ultimate C0–C5 | 500/530/560/590/620/650%; imported Rupture recipe retained |
| Passive | Blue allies gain 60% Defense |

Rank-three Skill 2 player wording now shows **60%**, replacing the imported generated phrase **6000**. This is a wording correction, not a balance change. The pre-conversion native files are retained in [BeforeNativeAuthoring](../../Artifacts/CharacterCreatorStudio/KitAuthoring/Joe96/BeforeNativeAuthoring).

Evidence: [12 actual Joe96 native authoring checks](../../Artifacts/CharacterCreatorStudio/KitAuthoring/Joe96/native-authoring.json), [18 disposable direct-save checks](../../Artifacts/CharacterCreatorStudio/KitAuthoring/native-evidence.json), [491 shared assertions](../../Artifacts/CharacterCreatorStudio/KitAuthoring/shared-evidence.json), and [26 publisher regression checks](../../Artifacts/CharacterCreatorStudio/Bridge/native-evidence.json). The direct-save fixture verifies actual widget callbacks, all five native writes, GUID preservation, reopen, rule deduplication, partial-write recovery, retry and Undo/Redo.

[Nine Play Mode checks](../../Artifacts/CharacterCreatorStudio/KitAuthoring/Joe96/live-evidence.json) verify actual 3D actors, the native passive (DEF 200 → 320), rank-three Skill 2's four damage contacts and team protection (DEF 440, Block Chance 90%). They also verify that an unsaved ATK 251 edit survives entering/leaving Play Mode while the battle independently uses saved ATK 250. The verification edit is cleared afterward. This checks committed gameplay and session recovery; it does not assert a complete animation cycle or FCT timing.

![Actual Joe96 kit overview](../../Artifacts/CharacterCreatorStudio/KitAuthoring/Joe96/overview.png)

![Actual Skill 2 rank editor](../../Artifacts/CharacterCreatorStudio/KitAuthoring/Joe96/skill2.png)

## Boundaries

This closes the missing kit navigation and editing of imported base kits demonstrated by Joe96. Blank character initialization and creation of missing native component assets still need a dedicated workflow. Art/model/status visual replacement remains inherited from the existing assets, rather than editable from these kit pages. Dynamic runtime locale/level/variant wording routing and a project-wide wording library remain separate work. The advanced schema controls are functional coverage, not a claim that every complex mechanic has a polished dedicated editor.
