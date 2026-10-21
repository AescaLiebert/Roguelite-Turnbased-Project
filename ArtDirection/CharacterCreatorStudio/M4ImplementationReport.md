# Character Creator Studio — M4 implementation

20 October 2026 · Description Studio, extending the verified M3 closure.

M4 adds a writer and wording library to the existing standalone UXML Studio. Mian is used only to demonstrate authored descriptions in an isolated trial. No Mian character, roster registration or production activation is created.

## Delivered packages

| Package | Implementation |
| --- | --- |
| M4-A Writer | Ordered paragraph/section/bullet/numbered/condition-note blocks; source editing; range-based Bold/Italic and library insertion; reorder/remove; invalid draft preservation; toolbar Undo/Redo. Original designer source and generated rule summary remain separate from player wording. |
| M4-B Profiles | Actual Toolkit Label and isolated TMP previews; authored level/rank/tier/Base/EX selection; adjustable width and compact/expanded layouts; Studio, character-detail, battle-card, passive and status surface contracts; selected font diagnostics and semantic plain output. |
| M4-C Vocabulary | Create/search/edit localized keyword labels, aliases, definitions, categories, gameplay links, styles and tooltip documents; stable IDs; deprecation; usage lists; referenced-entry deletion protection. Project vocabulary import separates attack Shatter from reaction Shatter. |
| M4-D Formatting | Searchable approved tag reference with verification state and insert/wrap actions; semantic color palette with bold/italic roles. Unknown tags remain invalid source rather than silently disappearing. |
| M4-E Snippets | Arbitrary required typed inputs; bilingual wording templates; pinned revisions; new editable revisions; visible old/new templates; explicit adoption of one selected instance; detach to local wording with persisted provenance; usage lists; missing-input and dependency-cycle rejection. |
| M4-F Integrity | Unit-aware authored parameters and resolved skill-field bindings; ordered level/rank/tier/variant text branches; explicit fallback and coverage checks; overlap diagnostics; locale fallback; mechanic coverage links; review receipts for exact text/dependency and gameplay revisions. |

## Open and use

Open **Fighting Allstar → Character Creator Studio → Descriptions**. Choose a block, write source, and use the inspector's Library selector to browse Keywords, Values, Snippets, Formatting, Branches or Review. The configuration foldout selects language, authored level/rank/tier and Base/EX. Configuration preview resolves definitions without simulating battle resource readiness or consuming random values.

Use **Import project vocabulary** to add the existing combat catalog, standard statuses, stat identities and the current draft's resources/statuses. It also seeds bilingual resource-gain, cleanse/recovery, per-layer scaling and before-damage wording. Import is idempotent and does not rewrite existing entries. The library belongs to the saved draft/bundle, so its exact revision remains recoverable with the document.

Keyword insertion does not apply a status or grant a gameplay tag. Gameplay definition links are validated separately from library categories. The two Shatter meanings retain distinct identities. Ambiguous localized names/aliases are diagnosed; labels can change without changing references.

The source editor supports the portable `b`, `i`, hex `color`, semantic style, value, keyword and snippet vocabulary already backed by the shared parser. Literal delimiters use backslash escapes. Sprites, font assets, external links, size and positioning markup remain explicitly unsupported until registered adapters and assets exist. Source/plain editing and rendered previews are separate; no rich-text editing behavior is claimed for TextField.

## Values and wording branches

Ordinary `{{value:parameter-id|integer}}` and `|percent` tokens read the shared authored parameter. Resolved bindings name a skill, optional primary/sequence effect identity, and a field such as coefficient, layer count or logical hit count. They use the production ability override resolver for the selected authored level, rank and tier. EX selection is explicit; live unlock/resource conditions remain in Scenario & actors. An absent effect or unknown configuration produces an error rather than substituting a base number.

Each block retains default source as its explicit fallback. Configuration branches are evaluated in declared order; the first match wins. The editor reports overlap and permits deliberate reordering. Turning off fallback requires complete coverage of level intervals, ranks 1–3, tiers 0–5 and variant categories. Invalid hidden branch source is rejected as well as selected source.

Locale variants preserve entire sentences and semantic references so Thai and English can reorder grammar independently. Creating a translation explicitly copies source for editing; fallback is visible and read-only before that action. All authored locales are checked by bundle compilation. Missing keyword/snippet translations report their fallback.

## Snippet lifecycle and review

Inserting a snippet requires every declared input. Value and keyword inputs use pickers; text inputs are escaped literal content. No default number or target is invented. Once a revision is used, editing requires a new revision. New templates leave pinned instances unchanged. Adoption shows before/after text and changes only the selected previous-revision instance after validating its existing inputs against the new revision. Changed input contracts require an explicit new insertion and bindings.

Detach preserves value/keyword references, writes local source and records its original linked token in the block. Source and provenance participate in the same Undo operation. Nested snippets must have an acyclic, bounded dependency graph. Glossary links may cycle because tooltips open on demand rather than recursively expanding; the viewer offers bounded Back and Close navigation.

Review records the selected configuration's compiled text hash and gameplay hash. Changes to prose, referenced labels/styles/templates/values or gameplay make the receipt stale. Coverage links are review bookkeeping; generated summaries and source notes do not automatically certify free prose. Runtime activation remains a later milestone.

## Persistence and isolation

The M4 fields are additive within the existing versioned description document and library envelopes. Existing M3 drafts deserialize with default empty branches/bindings/receipts and explicit fallback. Unknown schemas remain rejected by the existing parser/compiler gates. Full document duplication remaps binding and branch identities, local mechanic links, direct parameter references in library templates and tooltip documents, and keyword gameplay links; externally shared wording identities remain stable.

Previews use owned transient TMP atlases and preview scenes. They neither populate persistent game fonts nor modify the active scene or production character asset. Renderer evidence distinguishes verified formatting from missing glyphs in a selected runtime font. A missing Thai glyph is a release blocker for that runtime font/profile, not proof that the authored Thai source is corrupt. Choosing the Studio standard font previews the complete source without silently changing the production font.

## Verification artifacts

- [Shared core evidence](../../Artifacts/CharacterCreatorStudio/M4/core-evidence.json)
- [Actual UI authoring trial](../../Artifacts/CharacterCreatorStudio/M4/ui-author-trial.json)
- [Renderer/profile matrix](../../Artifacts/CharacterCreatorStudio/M4/Renderers/evidence.json)
- [Consolidated verification](../../Artifacts/CharacterCreatorStudio/M4/final-verification.json)
- [Writer and formatting reference](../../Artifacts/CharacterCreatorStudio/M4/writer.png), [rendered preview](../../Artifacts/CharacterCreatorStudio/M4/rendered-preview.png) and [attached Toolkit matrix](../../Artifacts/CharacterCreatorStudio/M4/Renderers/toolkit-matrix.png)

The UI trial authors case-study resource/layered-mark prose, binds recovery and caps, formats a range, repairs invalid markup and values, versions/adopts/detaches protection wording, tests Undo/Redo, branches and fallback, creates Thai prose, previews Base/EX resolved values, invalidates a review receipt and reopens the saved document. It uses ordinary widgets and real pointer events; document/library access is readback only.

M5 general battlefield authoring, M6 rigged actor/timeline authoring and M7–M8 roster migration and activation remain subsequent milestones.

M4 verification includes a UI-driven trial of 66 steps and 26 assertions. The shared suite adds 45 M4 assertions (410 total); the relevant elemental/Genshin regression subset passes 873 assertions. The separate renderer matrix passes 47 checks across five target surface contracts in English and Thai and explicitly records missing runtime-font glyphs; the Barlow Thai profiles remain blocked for release until the runtime font is replaced or given a verified fallback. Native duplication/reimport passes with both locales and resolved bindings retained, without modifying the source draft. The final UI trial's runtime/editor assembly hashes match the verified binaries, and the Unity compiler reports no C# errors.
