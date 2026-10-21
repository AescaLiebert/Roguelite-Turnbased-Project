# Character Creator Studio — M0–M2 implementation report

20 October 2026 · Unity 6000.3.4f1 · Evidence is scoped to M0–M2.

The implementation provides the first complete opening-gauge authoring slice, using Kaeya's registered source definition. It does not implement the M3–M8 roadmap or the complete Mian kit. Open **Fighting Allstar → Character Creator Studio** in Unity.

## Implemented scope

| Milestone | Current implementation |
| --- | --- |
| M0 | [Capability audit](../../Artifacts/CharacterCreatorStudio/M0/CapabilityAudit.md), frozen Kaeya definition, explicit supported/runtime-only/proposed capability registry and separate renderer profiles |
| M1 | Independent [root UXML](../../Assets/Project/Editor/CharacterCreatorStudio/UI/CharacterCreatorStudio.uxml), flat-ID rule document, versioned JSON draft assets, explicit save, Undo/Redo, duplication/remapping, diagnostics, atomic pending journal and domain-reload recovery |
| M2 | Generic BattleStarted → nested eligibility → active-team gauge command; shared mechanic/text parameter; literal source and real Toolkit/TMP previews; seeded BattleEngine scenario, fired/skipped trace, saved exact replay, committed-event actor playback and reversible editor-only test activation |

The rule pickers expose BattleStarted, All/Any/Not, the selected simple condition leaves and active-team gauge adjustment. They do not expose declared runtime settings whose complete behavior has not been implemented. Other imported character behavior remains in the frozen legacy definition.

Workspace tabs reset their scroll position after layout. Trace links select and scroll to the relevant condition. Long description source and keyword fields use the pane width; compact displayed hashes show the digest instead of the common schema prefix.

The document picker is synchronized when a draft is imported, duplicated or loaded. Actor target selection prefers the committed damage target over earlier opening utility recipients, so the real enemy reaction remains visible.

Production character ownership remains unchanged. Test activation writes an editor-only bundle with an explicit frozen-legacy fallback; it does not register the draft as production content. The gameplay/text/presentation hashes and combined bundle identity are separate.

## Description writer

The canonical document stores locale blocks, stable block IDs and exact source text, including invalid drafts and Thai combining characters. Its derived flat span table provides UTF-16 diagnostic/source ranges compatible with TextField selection indices. Compilation never passes curated Studio prose through the legacy English regex rewrite path.

The first description slice supports:

- Paragraphs, sections, bullet and ordered-list blocks; `<b>`, `<i>` and validated hex `<color>` formatting; explicit literal escaping.
- Typed parameter bindings, with Integer, basis-point percent, scalar and turn formats in the description contract. The M2 gameplay UI creates integer gauge/count parameters; the shared schema also defines BasisPoints for typed validation.
- Editable localized keyword labels/definitions, semantic style colors and pinned snippet revisions with typed inputs.
- Missing-reference, malformed-tag, unit/type, missing-translation and snippet-cycle/depth/size diagnostics; dependencies, semantic references and source maps.
- Plain text and shared portable rich-text output for actual Toolkit and TMP preview adapters. Unknown markup is preserved visibly with an activation-blocking diagnostic.

The seed sentence uses one bound gauge parameter, the Power Gauge glossary entry, a reusable pinned opening-gauge snippet and semantic color/emphasis. Prose edits and glossary wording do not execute combat rules or consume battle RNG. A keyword reference cannot grant a status or mechanic.

The Studio Editor uses the existing `Assets/Project/Resources/UI/zh-cn.ttf` font, including source fields and Toolkit labels. Default TMP preview uses an owned transient atlas from the same font. The explicit BattleInspector profile retains the game's Barlow source for meaningful comparison. Font switching preserves the authored text and disposes the previous preview atlas.

The renderer report passes all 20 checks. In the default zh-cn profile, the tested Thai text exactly matches semantic plain output, has no missing requested codepoints and produces 31 visible Thai glyphs. The persistent Editor/game font sources remain unchanged, and switching profiles preserves text and restores glyph coverage. The explicit Barlow comparison profile reports its 19 missing Thai codepoints accurately. Recorded visual review confirms the attached Toolkit and actual TMP captures.

## Evidence currently on disk

[Final verification](../../Artifacts/CharacterCreatorStudio/FinalVerification.json) records the completed M0–M2 gate against the current compiled Studio and live legacy-parity run.

| Requirement | Evidence and scope |
| --- | --- |
| Real-engine legacy parity | [Core evidence](../../Artifacts/CharacterCreatorStudio/M2/core-evidence.json): 71 core assertions; seed 12345; equal Studio/legacy state hashes; fired and skipped nested traces; four committed damage facts |
| Save/reimport/reopen, Undo, activation safety | [Unity authoring evidence](../../Artifacts/CharacterCreatorStudio/M1/authoring-evidence.json): 24 passing checks, no failures; includes draft persistence, reference repair, journal recovery, exact saved scenario replay, legacy reversion, same-frame Save/Undo recovery and production-source nonmutation |
| Same-frame Save/Undo recovery | [Focused regression evidence](../../Artifacts/CharacterCreatorStudio/M1/same-frame-undo-after-fix.json): six passing checks; Save flushes the pending Unity Undo diff before updating the disk baseline, preventing false saved state and recovery conflicts |
| Actual assembly reload | [Domain-reload evidence](../../Artifacts/CharacterCreatorStudio/M1/domain-reload-evidence.json): preserved GUID, pending payload, unsaved state, description workspace and Thai pending text across successful compilation |
| Visible designer workflow | [Workflow evidence](../../Artifacts/CharacterCreatorStudio/M2/designer-workflow.json): parameter/prose edits through UI, same draft reopened, matching rerun, three changed-mechanic differences and exact saved replay |
| Portable text rendering | [Version-2 renderer evidence](../../Artifacts/CharacterCreatorStudio/DescriptionRendererEvidence/evidence.json): 20 passing checks, no errors; actual TMP glyph style/color/plain output, attached Toolkit measurement and screenshot review, Thai zh-cn coverage and unchanged font sources |
| Core description contracts | [Description checks](../../Tests/CharacterCreatorStudio/DescriptionChecks.cs) invoked by the [Studio harness](../../Tests/CharacterCreatorStudio/Program.cs); preserve Thai/invalid drafts and cover bindings, snippets, localization, source maps, hashes and remapping |
| Actor/text fixture lifecycle | [Preview test results](../../Artifacts/CharacterCreatorStudio/M2/preview-checks.json): 5/5 passing tests from [Unity preview checks](../../Assets/Project/Editor/CharacterCreatorStudio/StudioPreviewChecks.cs), including deterministic committed actor playback, damage-target selection after opening utility facts, missing-clip fallback, actual TMP formatting, Thai output and font-switch cleanup |
| Draft picker consistency | [Picker check](../../Artifacts/CharacterCreatorStudio/M2/draft-picker-check.json): switching and returning to a draft updates the visible picker to the actual edited asset, with successful compilation and legacy parity |

Source and evidence links are relative to this repository. The [Toolkit window capture](../../Artifacts/CharacterCreatorStudio/DescriptionRendererEvidence/portable-subset-toolkit-window.png) and [default Thai TMP capture](../../Artifacts/CharacterCreatorStudio/DescriptionRendererEvidence/thai-editor-standard.png) show actual rendered output, rather than a mockup.

Final workspace captures: [Rules](../../Artifacts/CharacterCreatorStudio/M2/studio-rules.png), [Descriptions](../../Artifacts/CharacterCreatorStudio/M2/studio-descriptions.png), [Scenario and actors](../../Artifacts/CharacterCreatorStudio/M2/studio-scenario.png). The [committed actor playback captures](../../Artifacts/CharacterCreatorStudio/M2/CompletedSlice/actor-reaction.png) use the registered source's existing artwork cube prefab. This source has no authored action/reaction clips, so the preview demonstrates its deterministic movement/reaction fallback. Rigged character animation and the full timeline remain M6 work.

The shared build and Studio harness pass 71 core assertions plus 51 description assertions. The existing elemental/Genshin regression subset also passed 873 assertions; the unrelated broader combat suite is outside this verification claim.

## Verification boundaries

The supplied Thai sample and portable tag subset are verified for the named profiles. This does not certify every Unicode glyph, advanced rich tag or runtime tooltip consumer. The explicit BattleInspector missing-glyph result remains a profile-specific limitation; changing the Editor standard does not replace the game's production font assets.

The final completion audit also checks the visible Editor workflow and its saved outputs. Advanced targeting, resources/lifecycle, Mian interception/EX rules, the full wording database, general battlefield lab and full actor timeline remain in later milestones.
