# M0 — capability and contract baseline

20 October 2026 · Project version 6000.3.4f1 · The implementation evidence is scoped to M0–M2.

## Selected migration slice

The registered source is `fighter.kaeya`, at `Assets/Project/Data/Character/WIP_Phase/WIP_GenshinImpact/kaeya.asset`. Its current Unity-exported definition was saved in `KaeyaLegacyDefinition.json` before Studio implementation. The source's `Support.OpeningTeamGauge` is migrated into a generic BattleStarted reaction. Its other support capabilities, skills, ranks, ultimates, stats and traits remain cloned from the frozen source.

This choice tests actual event-driven authoring and parameter/description binding while remaining directly comparable to the existing opening behavior. The baseline is one gauge point for living active allies from an eligible active provider. Reserves do not receive that opening grant. The fixture records explicit combat and card RNG through BattleEngine's existing snapshots.

## Runtime authoring surface

The executable inventory is `StudioCapabilities.All`, backed by the shared compiler's validation rather than Inspector field presence. The M2 pickers expose:

| Area | Enabled subset | Evidence |
| --- | --- | --- |
| Trigger | BattleStarted | Emitted by BattleEngine.Create into CharacterPassiveRuntime |
| Logic | All, Any, Not; flat IDs compiled into runtime recursive conditions | Actual CardEffectSystem evaluation, short-circuit and nested failure checks |
| Leaf conditions | Always, owner gauge at least, owner health at most, matching roster count | Parameter-unit/domain validation plus real engine scenarios |
| Action | Change living active ally gauge | Generic ExecuteEffect → ChangePowerGauge |
| Values | Integer count and basis-point parameters | One resolver used by gameplay and description bindings |
| Provider gate | Existing living presence/mode/tier contract | Compiler refuses defeated-owner configuration |

Declared general cooldowns, activation counts, and reaction-origin controls are unavailable because the existing generic dispatcher does not enforce their complete contracts. Other runtime events and imported support features remain runtime-only in this editor slice. Atomic resource spending, removal contexts, arbitrary target queries, fatal interception, and EX variants remain later milestones. The compiler rejects unknown events, unsupported nodes, bad units, cycles and dangling references; no picker promises these future capabilities.

Trace capture is an optional observer in the production dispatcher and condition evaluator. It records rule decisions, evaluated condition values and short-circuit coverage, without producing combat events or drawing RNG. Passive gauge actions use passive provenance rather than incorrectly identifying themselves as a player card action.

## Persistence and source ownership

- The editor-only StudioDraftAsset stores a versioned JSON envelope. Authored conditions use flat node IDs; Unity does not traverse recursively embedded runtime types.
- Source document, original character definition, description source, locale blocks and keyword/snippet/style references are preserved. Invalid text drafts remain saveable.
- Unity Undo records the canonical envelope. An atomic journal under Library/CharacterCreatorStudio preserves pending drafts across reload. Recovery detects a conflicting saved base revision.
- Schema version 1 is implemented. Unknown versions are rejected without overwriting source or the last working test bundle; no speculative migration is attempted.
- Compiled gameplay/text/presentation hashes are separate; the aggregate bundle pins their combination. The existing production ContentHash remains unchanged by this editor-only integration.
- A reversible editor-only test bundle selects Studio or its frozen legacy definition. Production registry ownership is not switched at M2.

## Description and rendering inventory

| Surface | Existing consumer / renderer | M2 status |
| --- | --- | --- |
| Studio source editing | UI Toolkit TextField | Literal source editing; rich styling belongs in the preview |
| Studio preview | Actual attached UI Toolkit Label using the existing `Resources/UI/zh-cn.ttf` font | Portable subset verified through attached measurements and recorded captured visual inspection |
| Standard Editor TMP preview | Actual TextMeshProUGUI using an owned transient atlas generated from `Resources/UI/zh-cn.ttf` | Default profile; tested Thai parsed/plain equality, no missing codepoints, 31 visible Thai glyphs and unchanged source font |
| Battle-inspector-style preview | Actual TextMeshProUGUI using the game Barlow font source/settings in an owned transient atlas | Explicit comparison profile; parsed glyph/style/color checks plus rendered captures |
| Character detail | CharacterDetailPopup, rich UI Toolkit Label | Existing consumer inventoried; full production document API migration is later |
| Battle passive/card inspector | BattleCharacterInspector, TMP richText enabled | Renderer/family exercised by isolated real component; production consumer rewiring is later |
| Character Hub | CharacterHubView sets formatted Label text | Existing mixed legacy formatter path retained |
| Route details | DungeonRouteMapController creates Labels from formatted text | Different highlighting/glossary path inventoried; not falsely certified as the same output |

The compiler's first subset is bold, italic, hex color, structural/newline text, and escaped literal angles. Other rich tags remain unavailable rather than inferred from renderer support. The exact renderer verification, font coverage and screenshots are in the separate DescriptionRendererEvidence output. Thai source persistence is distinct from font coverage: missing glyphs must be reported, not silently called supported.

The standard font for the Editor shell, source fields, controls, attached Toolkit preview and default TMP preview is the existing `Assets/Project/Resources/UI/zh-cn.ttf`. The source file is reused; no persistent font asset is generated or edited. TMP owns a transient atlas, and switching the preview font releases that atlas before creating its replacement. Choosing the explicit BattleInspector profile does not change the Editor's source-field font.

The Barlow-profile renderer evidence confirms bold/italic/color and escaped literals, but reports 19 missing Thai codepoints and replacement squares. That result belongs to the runtime-font comparison profile. It does not describe the Editor standard. The current version-2 renderer report passes all 20 checks: the tested Thai text matches semantic plain output exactly with no missing codepoints and 31 visible Thai glyphs in the zh-cn profile. Source-font JSON is unchanged after generation, capture and switching both profiles. The attached Toolkit screenshot and zh-cn TMP capture have recorded visual review. The report records these outcomes separately as `EditorThaiGlyphCoverage` and `ThaiGlyphCoverage`, and pins the standard-font asset dependency hash. A stale version-1 report cannot certify the updated Editor font profile.

Current migration inputs are CombatTextCatalog's hardcoded English terms/aliases, CardEffectText operation templates, CardDescriptionText/PassiveDescriptionText adapters, CombatDetailFormatter colors and StatusVisualData metadata. New Studio text uses typed references and does not pass through the legacy English regex rewriting. Keyword mention does not apply a status, and description text never determines combat behavior.

## Mian semantic decisions

The decision register remains in ArtDirection/CharacterCreatorStudio/MianKitSpecification.md. No unresolved Mian timing, cap precedence, resource ownership, duration, upgrade or absorption decision is silently implemented by the Kaeya slice. Those decisions become prerequisites when their corresponding M3/M7 mechanic is implemented.

## Evidence boundaries

The .NET contract suite establishes core semantics and frozen-state parity. StudioAuthoringChecks uses actual AssetDatabase save/reimport, Unity Undo, journal recovery, activation/reversion, exact saved-scenario replay and production-source nonmutation. StudioPreviewChecks verifies actual actor/text fixtures, fallback behavior and cleanup. Description renderer evidence separately records real TMP glyphs and a live Toolkit panel capture.

No single green test or renderer checkbox establishes complete future Studio support. Milestone completion must also inspect the editor's visible author workflow and its resulting files.
