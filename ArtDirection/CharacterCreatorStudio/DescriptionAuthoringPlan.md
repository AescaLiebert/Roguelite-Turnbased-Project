# Description Studio — structure, wording, rich text, and libraries

20 October 2026 · Original design plan; the supported M4 workspace is now implemented. See the [M4 implementation report](M4ImplementationReport.md) for delivered behavior, verification and runtime font limits. This plan extends the [Studio architecture](ArchitecturePlan.md), [UX plan](UXDesignPlan.md), and [implementation roadmap](ImplementationRoadmap.md), including later asset-dependent rich-text adapters.

## 1. Designer outcome

A designer writes skill, passive, resource, status, and glossary descriptions in one workspace; inserts approved wording/keywords/tags/colors/snippets; sees real rendered results; and checks how values change by rank, ultimate level, variant, locale, and game surface. Descriptions reference gameplay values instead of maintaining a second set of balance numbers.

Keep three distinct documents: player-facing authored description, generated mechanical summary, and original source/designer notes. Generated summaries explain supported rules for review; they must not silently overwrite curated player prose. The original Thai kit remains available unchanged as a source reference.

## 2. Current foundations and gaps

| Current evidence | Implication |
| --- | --- |
| `SkillCardSO.cs:105` and `UltimateCardSO.cs:33` expose TextArea description fields; CharacterObject stores passive source prose | Existing authoring is freeform text, not a typed description document |
| `CharacterObjectCatalogBuilder.cs:146–147,174–175` exports formatted Description and original SourceDescription for skills/ultimates | Preserve this distinction during migration; add a structured source reference |
| `CardDescriptionText.cs:26–112` normalizes prose and rewrites/generated damage clauses; `CardEffectText.cs:13–34` declines unsupported complex prose | Existing generators are useful import and summary adapters; do not apply legacy regex rewriting to newly compiled descriptions |
| `CombatTextCatalog.cs:24,95–104,139,149–197` stores terms/aliases and regex-based normalization, highlighting, glossary discovery | Seed a data-driven database from it; explicit references avoid ambiguous phrase matching |
| `PassiveDescriptionText.cs:24–37` retains authored text for complex reactions and limits its generation fallback | Full Mian prose cannot be reconstructed reliably by the current aura formatter |
| `BattleCharacterInspector.cs:186,335` uses TMP rich text; `CharacterDetailPopup.cs:174` uses a UI Toolkit Label | One renderer preview cannot prove all game consumers render identically |

These paths are under `Assets/Project/Runtime/Content/` or `Assets/Project/Runtime/Presentation/Combat/`, except `CharacterDetailPopup.cs`, which is under `Presentation/Navigation/`. Current tags generally pass through; there is no complete project-wide parser/validator/capability catalog for designer-authored rich text.

## 3. Separate the meanings of “tag”

| Concept | Example | Meaning |
| --- | --- | --- |
| Gameplay tag | `trait.woman`, `series.kof`, `ability.single-target` | Queried by combat rules; prose does not grant these tags |
| Rich-text tag | `<b>`, `<color=…>` | Renderer formatting syntax, with per-surface support |
| Description reference | `{{term:status.mian.face-mask}}` | Proposed authoring syntax for a stable vocabulary reference |
| Library classification | `resource`, `defensive`, `team`, `KOF` | Search metadata for descriptions/snippets; not automatically executable |

The editor presents these as separate insertion categories. Typing “Stun” or adding a Stun keyword reference does not make a skill apply Stun. Applied-status badges remain derived from compiled mechanics.

## 4. Description structure and source model

Recommended canonical data is a versioned description document with stable block/span IDs, stored without recursive Unity object graphs. Valid source text parses into this document; guided edits operate on the same representation. Preserve original source and diagnostics for incomplete/invalid drafts. The supported source syntax must round-trip without dropping prose, references, whitespace intent, or supported formatting.

| Record | Main fields |
| --- | --- |
| DescriptionDocument | ID, schema version, owner/ability/status ID, locale variants, block order, source text, review state |
| Block | Stable ID; paragraph/section/list item/condition note; semantic style; linked mechanic/rule IDs |
| Span | Literal text, emphasis, term reference, value binding, semantic style, line break, snippet instance |
| ValueBinding | Stable parameter ID, unit-aware format, selected upgrade context, source location |
| TextBranch | Explicit rank/variant/upgrade predicate over authored configuration; ordered content alternatives |
| LocalizedVariant | Locale, translated source/blocks, retained references, review state, explicit fallback |
| CompiledDescription | Surface render payload, plain text, semantic term/reference map, diagnostics and text revision |

Initial supported structure: paragraphs, section labels, ordered/unordered list items, emphasis, semantic colors, referenced terms, bound authored values, snippets, and configuration-based variants. Lists are document structure rendered by adapters, not an assumption that Unity supports HTML `<ul>` or `<li>` tags.

Text branches select the first matching predicate in declared priority/order. Require an explicit fallback or prove complete coverage for the finite supported configuration domain; otherwise compilation reports uncovered configurations. Report overlapping predicates with the winning branch, and test combined upgrade axes. Reordering branches is a semantic edit rather than an editor-only layout change.

Descriptions of live battle values are a later opt-in capability requiring a typed observation context and documented refresh/fallback behavior. Static player text resolves authored parameters; it must not consume battle RNG or evaluate gameplay actions to produce wording.

## 5. Authoring syntax and value bindings

The term/value/snippet/style source syntax is implemented in the supported M4 subset. Most designers insert tokens from pickers; source view exposes exactly what was inserted. Stable IDs survive renames and localization. The Mian IDs below are illustrative case-study examples, not registered game content.

```text
Gain {{value:param.mian.face-mask.gain|integer}} stack of
{{term:keyword.mian.face-mask}} (maximum
{{value:param.mian.face-mask.cap|integer}} stacks).

Recover {{value:param.mian.protection.heal|percent}} of the hit ally's
{{term:keyword.stat.max-health}}.
```

These proposed keyword IDs refer to glossary entries linked to the Face Mask resource and Max HP stat definitions. Inserting a glossary reference does not create an additional combat status or change either mechanic.

For a bound percentage, the format understands the parameter's unit: 2,000 basis points renders as 20%, not 2,000% or 0.2%. A cap renders as an integer. Duration bindings include their clock in the structured mechanical reference even if player wording simply says “2 turns.” Crit multiplication and added percentage points remain different typed values/phrases.

Markup view may accept an approved subset of Unity-style tags such as `<b>`, `<i>`, and `<color=#…>`; guided controls create equivalent typed spans. Semantic color roles and keyword references are preferred over repeated hex literals. Literal braces/angle brackets, token delimiters, newlines, and incomplete tags have explicit parser escaping rules. Tokens are recognized only outside literal spans and formatting attributes. No arbitrary paths, C# expressions, or script execution are permitted in bindings.

Example semantic result using the supplied Mian values:

> Gain 1 stack of **Face Mask**, up to 6 stacks. Recover **20%** of the hit ally's **Max HP**.

This demonstrates text binding, not a complete protection mechanic. The entire description also needs its eligible trigger, resource cost, target, duration, and frequency wording. Linking a paragraph to mechanic IDs supports review coverage; it does not prove arbitrary prose is accurate.

## 6. Keyword and wording database

Use one searchable library with distinct entry types, not one universal loose string dictionary.

| Entry type | Required data |
| --- | --- |
| Keyword/term | Stable ID, category, localized display names, aliases, short definition, detailed tooltip document, optional gameplay definition link, style role, optional icon, revision/deprecation |
| Wording phrase | Stable ID, localized pattern, typed placeholders, usage notes, mechanical semantic category |
| RichTextTag | Syntax, accepted attributes/value units, example, allowed nesting, supported renderer profiles, asset prerequisites, verification evidence, insert/wrap behavior |
| Style/color role | Stable ID, purpose, light/dark/surface mappings, emphasis, fallback, optional approved custom-color policy |
| Snippet | Stable ID/version, structured text template, typed inputs, dependencies, examples, locale variants, update policy |

Keyword categories include attack keyword, status, resource, stat/stat bundle, targeting, combat event, trait/series, and explanatory glossary term. Distinguish a mechanic-backed keyword from a purely explanatory word. Its existence in the database must never imply the combat feature is implemented.

Seed the library from CombatTextCatalog terms and aliases, stat labels, elemental vocabulary, StatusVisualData metadata, and existing style colors. Review duplicate names and conflicting meanings during import. For example, attack-keyword Shatter and elemental-reaction Shatter should have separate IDs and context-specific definitions, even if their localized display name is identical.

Each term shows where it is used in characters, statuses, descriptions, snippets, and tooltip documents. Rename changes labels, not identity. Deletion/deprecation offers an explicit migration for consumers. Aliases are locale-scoped discovery/import aids; they do not rewrite newly authored text invisibly. Automatic suggestions present candidates for confirmation, especially for Thai text without English word boundaries.

Glossary cross-reference cycles are allowed because related concepts can refer to one another. Never recursively inline-expand tooltip documents. Open references on demand with a bounded navigation history, deduplicate expansion within a view, and provide back/close controls usable without hover. Any optional inline expansion must detect its current reference path and stop at a link when it would revisit a term. Snippet execution remains acyclic.

Wording phrases encode meaningful distinctions: “before dealing damage,” “after damage resolves,” “once per action,” “for each target,” “actual HP lost,” “increase by percentage points,” “multiply chance,” “remove eligible debuffs,” and “consume layers.” These are reviewed templates backed by known semantics, not an unrestricted synonym generator.

## 7. Supported rich text and color behavior

Unity documents its [rich-text tag syntax](https://docs.unity3d.com/6000.3/Documentation/Manual/ui-systems/introduction-to-rich-text-tags.html) and [supported UI Toolkit tags](https://docs.unity3d.com/6000.3/Documentation/Manual/UIE-supported-tags.html). TextField itself does not display rich tags, so the initial editor pairs source/plain editing with separate rendered previews. Engine documentation is a candidate feature list; it is not proof that the game's current fonts, surfaces, and adapters support every feature.

Proposed rollout, subject to M0/M2 verification:

| Feature group | Initial Studio policy |
| --- | --- |
| Bold, italic, color, paragraph/line breaks | First slice candidate set; verify in Toolkit and TMP before marking supported |
| Underline, strike, bounded size, no-break, literal markup | Add after actual consumer checks; expose appropriate examples and constraints |
| Named styles, sprites/icons, font overrides | Require registered assets and a verified adapter for each target |
| Internal keyword links/tooltips | Semantic term references first; renderer-specific handlers and plain/glossary fallback |
| Alignment, spacing, indentation, absolute positions and widths | Advanced surface-specific features; unavailable where they break the layout contract |
| External hyperlinks or unknown HTML tags | Outside the initial gameplay-description feature set; preserve drafts and report unsupported output |

The tag browser shows **Documented**, **Verified for this target**, **Enabled**, **Requires asset**, or **Unsupported**, with the exact profile/version and an example rendered in that target. A supported-tag list is part of the shipped capability catalog and cannot be inferred solely from an Inspector checkbox. A tag that draws successfully may still lack interaction handlers, correct export, or expected layout behavior.

Each required surface has its own profile: Studio Toolkit preview, Character Hub/detail, battle card tooltip, passive inspector, status tooltip, and plain text. Profiles record actual renderer, font/text settings, supported spans/markup, width/typography contract, interaction support, and fallback rules. Inventory these consumers in M0 rather than assuming one file covers them all.

Color precedence: surface base → semantic block/condition role → term/value role → explicit authored span override. The preview explains an override and can show its role/hex. Existing keyword/stat/value/condition colors seed defaults; designers may choose approved palette entries or an explicit custom color where the target profile allows it. A color never determines gray-status immunity or any combat rule. Preserve readable labels and font emphasis so color is not the only meaning.

Compiler output escapes literal content per renderer and generates balanced supported tags. When assigning text dynamically, do not UXML-escape it a second time; UXML/XML serialization escaping and runtime rich-text/literal escaping are separate steps. Plain output comes from semantic nodes, not a regex that discards every angle-bracket sequence.

## 8. Snippets and shared wording

A snippet is a reusable description template, not a gameplay script. Useful examples are resource gain/cap, trigger/condition, heal amount/basis, duration, per-layer scaling, protection transaction, and upgrade note.

Required inputs have typed picker bindings: recipient term/query label, resource/keyword reference, amount parameter, duration parameter/clock, and optional configuration branch. Designers can inspect an expanded preview and every dependency. No missing input is silently replaced with a plausible number.

Default insertion creates a **linked instance pinned to an explicit snippet revision**. An update shows old/new text and all affected descriptions, with deliberate update selection. “Detach as local text” creates an editable local copy with provenance. Deleting a shared snippet cannot silently break pinned consumers; keep referenced revisions or migrate them explicitly. Snippets may call other snippets only through an acyclic dependency graph; enforce bounded expansion and reject cycles. Pinning the snippet fixes its template, not the gameplay parameter values or keyword labels resolved into a new bundle. Exact reproduction of prior rendered output requires the complete compiled bundle and its dependency revisions.

Translations may reorder sentence structure and inputs. Do not construct Thai/English grammar by concatenating translated fragments in English order. Parameter identity remains language-independent; unit/plural formatting and word order are locale-specific. Locale fallback is visible in preview and included in approval diagnostics.

## 9. Workspace and designer workflow

```text
Description  /  Passive: Face Mask           Locale [Thai / English]  [Validate]
┌─────────────────────┬──────────────────────────────────┬────────────────────────┐
│ LIBRARY             │ WRITE                            │ LIVE PREVIEW           │
│ Keywords & wording  │ [Bold] [Italic] [Color]           │ Surface [Battle TMP]   │
│ Gameplay tags       │ [Term] [Value] [Snippet] [Tag]    │ Rank / level / variant │
│ Rich-text tags      │                                  │ Width / font profile   │
│ Colors & styles     │ Paragraphs / source              │                        │
│ Snippets            │ Typed tokens + authored prose    │ Rendered player text   │
│ Bound parameters    │                                  │                        │
├─────────────────────┴──────────────────────────────────┴────────────────────────┤
│ Diagnostics | References | Mechanical summary | Plain output | Change impact    │
│ Selected token → parameter / mechanic / keyword / snippet revision              │
└───────────────────────────────────────────────────────────────────────────────┘
```

The writer is also reachable from a selected rule/status/skill. The library is contextual and collapsible. Its details show canonical ID, localized name, definition, preview, supported targets, and uses. “Insert” places a term/value at the caret; “Wrap selection” applies valid formatting; snippet insertion opens only its required inputs.

Source view displays literal tags and tokens. Structured controls avoid making designers memorize them. The live preview is a separate surface; clicking a semantic span can select its source/binding through generated source maps. Precise source-to-glyph selection is optional polish where a renderer cannot yet provide reliable hit testing; dependency navigation remains available from the inspector.

The preview can switch actual Toolkit/TMP adapters and compare required surfaces. A Toolkit label styled to resemble TMP is not renderer parity evidence. The mechanical summary stays beside the authored description for checking trigger, condition, target, ordering, cost, frequency, and lifetime. Show coverage as linked/unlinked mechanics, not a misleading “description is correct” score.

## 10. Compilation, hashes, and integration

```text
Authored description + locale + configuration
    → parse and resolve typed document
    → expand pinned snippets and term/parameter references
    → validate syntax, dependencies, units and surface capabilities
    → render adapter payload + plain text + semantic reference/source maps
    → preview and runtime description catalog
```

Description compilation uses the same parameter resolver as gameplay compilation, independent of presentation formatting. Text branches inspect authored configuration only. Binding a number to a mechanic does not execute that mechanic.

Track GameplayHash, TextHash, PresentationHash, and aggregate BundleHash in the proposed versioned format. A term rename or prose edit changes TextHash and the bundle while leaving GameplayHash and battle RNG behavior intact. Changes to gameplay parameters invalidate both affected gameplay and derived text. Revision-pinned runs retain their matching glossary/description catalog; shared database edits do not silently change an existing run.

For Studio-owned content, consumers receive compiled text/reference payloads and bypass legacy prose normalization/highlighting. Legacy mode retains its existing adapter until imported. Compatibility strings can be generated for old consumers, but they must not be passed through rewriting that changes them again. Migrate consumers explicitly and retain existing formatting regressions as fixtures.

No runtime code parses the player description to infer damage, status application, keyword behavior, or target queries. Likewise, description links to traits do not add those traits to characters.

## 11. Diagnostics and acceptance

Blocking output issues: malformed/unknown tokens, unresolved IDs, incompatible units/formats, required snippet inputs missing, recursive snippets, invalid/uncovered branch predicates, incompatible schema versions, or required-surface features/assets without an approved meaning-preserving fallback. Explicit profile-approved plain-text/emphasis fallbacks remain valid and are reported in the preview/output manifest; do not silently discard content or block an otherwise valid approved fallback. A draft remains saveable with diagnostics. Preview can show clearly marked placeholders; released required surfaces must receive valid output.

Review issues: unbound numeric prose, ambiguous imported term aliases, stale translations, missing mechanics links, visual overflow, unverified optional tags, and possible wording mismatch. Static checks cannot prove arbitrary prose is true; approved player text requires a designer review against the mechanical summary and selected scenarios.

Required tests and trials:

- Round-trip nested formatting, literal `<`/`>` and braces, newlines, incomplete tags, duplicate/copy/Undo, source spans, and Thai IME/composition without lost characters.
- Render the approved formatting set through real required Toolkit/TMP consumers with their actual fonts and text settings.
- Change Mian's bound Face Mask cap/heal parameter and verify selected rank/level/variant text updates without rewriting source prose or changing battle RNG.
- Keep Stun mention/removal separate from applied-status badges; disambiguate Pierce/Pierce Rate, Recovery Rate, and the two Shatter terms.
- Rename a keyword, deprecate an alias, change a linked snippet revision, and inspect consumers/diffs; pinned templates stay unchanged while bindings resolve against the selected bundle, and complete pinned bundles reproduce prior output.
- Check overlapping text-branch predicates, unmatched ranks, explicit fallbacks, and combined variant/upgrade axes; the winning branch and any coverage failure are visible.
- Verify Thai/English reordering, plural/unit formatting, missing translation fallback, long descriptions, missing glyphs, and nested tooltip cycles.
- Render compact and expanded player descriptions; all required information remains reachable if a surface truncates visible text.
- Let a designer discover a supported tag, insert a colored keyword and bound percentage, reuse a snippet, resolve a reported error, and explain what the text guarantees versus what requires manual review.

The definition of done is one coherent authoring pipeline with correct values, stable references, discoverable formatting support, verified output targets, and reviewable wording—not merely a rich-text textbox.
