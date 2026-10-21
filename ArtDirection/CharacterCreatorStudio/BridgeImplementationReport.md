# Character Creator Studio — native asset bridge

20 October 2026 · Implemented and verified · M6 remains paused

**Workflow update:** [native kit authoring](NativeKitAuthoringReport.md) now opens and saves the existing CharacterObject and four data SOs directly. A separate draft asset is no longer required. This report retains the earlier publisher/compatibility workflow and its evidence.

The Studio can now publish an imported, edited character as five native runtime assets and load it through the normal game registry. This closes the missing persistence/runtime bridge identified in the [production creation audit](ProductionReadinessAudit.md). It does **not** complete the blank-character creator or the base kit editing UI.

## Designer workflow

1. Open **Fighting Allstar → Character Creator Studio** and **Import character**. All 39 registered characters now import and compile; opening-gauge migration remains available where applicable.
2. Edit the supported rules, parameters, variants and description in the draft.
3. Open **Review & publish character**. In **Publish to native character assets**, enter a new `fighter.*` ID, unique positive numeric ID and display name.
4. Leave wording on **Preserve**, or choose Passive, Skill 1, Skill 2 or Ultimate as the destination for the current writer.
5. Choose **Review native asset changes**, inspect the five paths, then **Publish reviewed character package**. Editing the draft or source invalidates the reviewed revision.
6. Subsequent publishes can update this draft's owned package. **Rollback last native publish** restores its previous version or removes the newly created package.

New packages live under `Assets/Project/Data/Character/Studio/<character-id-without-fighter-prefix>/` and contain `Character.asset`, `Skill1.asset`, `Skill2.asset`, `Ultimate.asset` and `Passive.asset`. The publisher synchronizes the normal registry and validates the resulting catalog. A Studio test bundle or Battle Lab draft override is not required to use the published character.

Publishing a legacy import creates a separate identity and preserves its original source assets. Updating an existing legacy package in place is intentionally outside this workflow. Update mode is restricted to the owning draft's published package.

## Persistence and runtime contract

- Studio-owned SOs use versioned canonical JSON payloads, preserving recursive passive actions, nested conditions and lifecycle callbacks, full skill ranks, variants and value overrides, ultimate tiers and complete character metadata. Unknown schema versions fail explicitly.
- The normal `CharacterObjectCatalogBuilder` hydrates these payloads. Existing native card consumers resolve complete effects through `GetRankData` and `GetLevelData`; native Inspector fields are read-only mirrors for owned assets.
- Unowned legacy SOs keep their existing reconstruction path. The legacy passive setter refuses to replace a Studio-owned definition.
- Studio compilation now runs the full content validator in addition to its authoring checks. Native publishing validates both the adapter roundtrip and the candidate production catalog, including identity collisions and required ranks/tiers.

## Review, conflicts and recovery

Review writes no runtime package. Publication rechecks the exact draft revision, compiled bundle, source fingerprint and candidate definition. Source fingerprints include the reconstructed definition and native source objects. Updates and rollback reject changed disk files and unsaved package edits.

The transaction backs up the five owned assets before writing, saves the CharacterObject after its dependencies, reimports the package, checks the reconstructed gameplay definition, validates runtime discovery and saves a receipt on the draft. Injected partial-create and partial-update failures are covered by native checks. Updates preserve all five GUIDs; rollback restores previous bytes while retaining existing identities.

Recovery manifests are local to `Library/CharacterCreatorStudio/PublishBackups`. **Deleting Library removes this rollback history.** This is a local recovery mechanism, not the durable release/recovery workflow planned for M8. Interrupted-process recovery and project-wide activation history remain separate rollout work.

## Description boundary

The selected writer destination is materialized into native passive wording, all three skill ranks or all six ultimate tiers as appropriate. The publication uses the document's default locale and level 1, resolving each rank/tier separately. Preserve leaves existing wording intact.

This connects authored rich text to normal runtime consumers. It does not add dynamic runtime locale, character-level or Base/EX description routing, multiple independent writer documents for every wording slot, or a project-wide wording database. Those remain follow-up work.

## Verification

| Check | Result | Evidence |
| --- | --- | --- |
| Shared Studio suite | 476 assertions, including 10 bridge checks | [Shared evidence](../../Artifacts/CharacterCreatorStudio/Bridge/shared-evidence.json) |
| Native create/update/reimport/recovery/rollback | 26 checks; all 39 roster imports compile | [Native evidence](../../Artifacts/CharacterCreatorStudio/Bridge/native-evidence.json) |
| Actual Studio buttons → native publish → domain reload → real 3D battle | 13 checks; native passive, damage, FCT/audio and authored runtime inspector wording | [Live evidence](../../Artifacts/CharacterCreatorStudio/Bridge/live-evidence.json) |
| Elemental regression | 873 assertions | [Regression output](../../Artifacts/CharacterCreatorStudio/Bridge/elemental-regression.txt) |
| Training regression | 2,383 assertions plus reaction/queue checks | [Regression output](../../Artifacts/CharacterCreatorStudio/Bridge/training-regression.txt) |

The live trial uses the normal runtime catalog without a compiled-draft override. It creates and then rolls back a disposable verification character. All 196 existing roster/card/passive/registry files remain byte-for-byte unchanged. The final editor state is stopped, MainMenu is clean, the registry contains the original 39 characters, and volatile Battle Lab context is cleared. No Mian kit is installed.

![Actual native package review](../../Artifacts/CharacterCreatorStudio/Bridge/publish-review.png)

![Actual published character in the runtime inspector](../../Artifacts/CharacterCreatorStudio/Bridge/runtime-inspector.png)

The runtime screenshot shows the inspector's visible identity/stat section. The separate live check asserts authored wording in its actual TMP components; that text is below the visible fold.

For repeatable checks, run `StudioBridgeChecks.Run()` from stable Edit Mode, then `StudioBridgeLiveChecks.Start()` and inspect its `Status`. The live check crosses Play Mode domain reloads and restores the original scene and draft. See the [test instructions](../../Tests/CharacterCreatorStudio/README.md).

## Remaining creation work

The original audit's missing publisher, recursive native persistence, generic import restriction and incomplete compile validation are addressed. Native wording installation is addressed for the explicit materialization contract above.

Complete designer-driven creation still needs blank character initialization, editable canonical identity/stats/growth/art, complete base skill rank/effect authoring, six-tier ultimate authoring, and the remaining description routing/library work. A new published identity currently starts from an imported frozen base kit. This bridge makes supported edits usable in the actual game; it does not establish full Studio production readiness or resume M6.
