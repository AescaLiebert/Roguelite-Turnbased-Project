# Character Creator Studio — UX and UI design plan

Status: proposed interface, not an implemented UXML. See [architecture](ArchitecturePlan.md) for authority and persistence contracts and [Mian](MianKitSpecification.md) for example content.

## 1. Design direction

Make the tool feel like a focused desktop creation workspace. The dominant task is reading and changing a kit, followed by proving what it does. A small character portrait provides identity; rules, parameters, targets, and evidence receive most of the space.

Use a **structured outline with nested rule blocks** as the default editor. A freeform node canvas makes simple reading and long conditional kits harder when wires cross. A dependency diagram can be an optional read-only view of modules and references. Both views refer to the same node IDs; there is no second authoring model.

The author should always know: which character and variant is open, whether edits are saved, which compiled revision is being simulated, and why the selected rule behaved as it did.

## 2. Application layout

```text
┌─────────────────────────────────────────────────────────────────────────────────┐
│ Character Creator Studio  /  [UE] Fire Dancing Mian     Draft saved   [Validate] │
│ [Character library ▾]     [Undo] [Redo]      [Run scenario]       [Apply to game] │
├───────────────────┬──────────────────────────────────────┬──────────────────────┤
│ KIT OUTLINE       │ Rules  /  Passive  /  Face Mask      │ RULE INSPECTOR       │
│                   │                                      │                      │
│ Overview          │ Protect a female KOF ally            │ Trigger timing       │
│ Identity & stats  │                                      │ Damage resolved      │
│ Skills            │ WHEN  Enemy deals damage             │                      │
│  Skill 1          │ TO    Ally matching ALL              │ Frequency            │
│   Base / EX       │       Series = KOF                   │ Per action + target  │
│  Skill 2          │       Trait = Woman                  │                      │
│  Ultimate         │ IF    Face Mask ≥ 1                  │ Context              │
│ Passive rules     │ DO    Spend Face Mask 1              │ Owner: Mian          │
│  Face Mask        │       If spend succeeds              │ Actor: Attacker      │
│  Impressions      │        Cleanse recovery block 1      │ Target: Hit ally     │
│  Mask Switch      │        Heal hit ally 20% max HP      │                      │
│ Resources/statuses│        Apply protection              │ Parameters           │
│ Presentation      │        Mark attacking enemy          │ [20%] [2 turns]      │
│ Scenarios         │                                      │                      │
│                   │ [Add condition] [Add action]         │ [View dependencies]  │
├───────────────────┴──────────────────────────────────────┴──────────────────────┤
│ TRACE / PROBLEMS / REFERENCES / DESCRIPTION                                      │
│ Enemy skill → Ally hit → Mask 2 → 1 → Cleanse → Heal blocked: another block left │
│ [Jump to rule]                 Scenario: Team defense · revision … · seed …      │
└─────────────────────────────────────────────────────────────────────────────────┘
```

The rule above is an illustrative authored choice, not a resolution of the kit's timing/frequency ambiguities. The inspector must show those decisions explicitly.

Use resizable split views. The left outline is navigation, the center is the work surface, the right inspector edits the selection, and the bottom dock contains evidence. Each pane has one clear scroll owner. Closing the inspector expands the document; opening the test lab replaces the center with a viewport and scenario details. Do not force the full rule editor and full 3D lab into competing miniature panels.

At narrower dock widths, collapse the outline to a drawer and show the inspector as a secondary page with a return breadcrumb. Preserve selection, scroll position, expanded groups, scenario, and unsaved draft state across pane changes. Desktop layout dimensions are starting values to determine with real kits, rather than fixed pixel requirements.

## 3. Navigation and information architecture

| Workspace | Main content |
| --- | --- |
| Overview | Identity, kit sentence summary, referenced mechanics, unresolved decisions, last relevant scenario results |
| Identity & stats | Series and independent tags, stat groups, level growth, separate upgrade dimensions, portrait/card art |
| Skills | Slot → variant → parameter table → action program, target contract, costs, combat tags |
| Passive rules | Event rules grouped by mechanic, continuous modifiers, threshold states, interception rules |
| Resources & statuses | Stack/layer semantics, source identity, duration clocks, removal permissions and local rules |
| Descriptions | Structured/source writer, bound parameters, rank/variant/locale/surface previews, keyword and formatting libraries |
| Presentation | Actor profile, sockets, animation map, skill cue timeline and reaction profiles |
| Scenarios | Teams and fixtures, control policies, scripted inputs, assertions, replay and comparison |

Use one character picker in application chrome. Search within the outline finds rules, statuses, parameters, and their references. Shared library entries can be opened from references without losing the current character. A breadcrumb shows when editing a shared mechanic and its affected characters.

New character flow: choose blank or a reusable character template, supply stable identity and traits, assign baseline stats, add skills/mechanics, bind an actor profile, and run a scenario. Imported legacy characters open with a compatibility report before conversion. Never claim an import is complete merely because the asset loaded.

## 4. Rule builder behavior

A rule has a title and generated sentence summary, followed by labeled blocks:

`When` → `Observe/select` → `If` → `Do` → optional `Else`, with `Frequency` and `Lifetime` always visible in the rule header or inspector.

Event rules, continuous modifiers, threshold transitions, and interceptors use distinct labels and small icons. Use subtle semantic accents with text labels; color never carries timing or validity on its own.

Conditions support explicit All, Any, and Not groups. Actions support sequences, branches, target loops, captured values, and module calls. Indentation lines and group labels expose scope. Collapsing a group preserves a useful summary such as “All: KOF, Woman, alive” or “If payment succeeds: cleanse, heal, protect, mark.”

The add menu is filtered by the current scope and actual engine capability. Selecting a node shows only applicable fields. Type-aware value pickers offer `Actual health damage` only where available and explain `Calculated damage` versus `HP + shield loss`. A named binding such as `Absorbed layers` remains visible where used.

Support keyboard add, duplicate, move, indent/outdent, enable/disable, copy/paste, and Undo/Redo, with menu equivalents for every drag action. Duplicating remaps node IDs and internal references. Moving a node out of a valid binding scope produces a repairable draft diagnostic; applying to game remains blocked until valid.

Common actions have compact inline fields; advanced details open in the inspector. A resource spend can expand to its success/failure branch. For `Heal 20%`, require the basis (`recipient Max HP`, for example). For `2 turns`, show the duration clock (`recipient turn end`, for example). For `+50% crit`, require whether it adds percentage points or multiplies chance.

**Readable example: three-layer absorption**

```text
When Mian's nonultimate single-target skill reaches [chosen phase]
  Capture Facial Impressions layers as AbsorbedLayers
  If AbsorbedLayers ≥ 1 → reward module Layer 1
  If AbsorbedLayers ≥ 2 → reward module Layer 2
  If AbsorbedLayers ≥ 3 → reward module Layer 3
  Remove captured mark with reason Absorb
```

That example depicts cumulative rewards. Selecting exact-tier rewards changes the branch structure and sentence; the Studio must not make this gameplay decision implicitly.

## 5. Parameter and variant editing

Keep one rule program with a parameter table for levels and ranks. Display inherited values and explicit overrides distinctly. Separate skill rank, ultimate level, awakening/constellation, and character level; an arbitrary “LV.MAX” must be mapped to a specific axis.

The variant editor shows Base → EX transition conditions, replacement priority, duration/use count, relock rule, and card handling. Authors choose whether variants are selected at card creation, queueing, or action execution. Preview both old and new hand/card identities, UI text, AI selection, and replay behavior.

Parameter edits show impact on dependent formulas and saved scenarios. Undefined source values appear as “Needs decision”; they are not filled with invented lower-rank numbers. Preserve the original Thai text alongside structured rules, and allow intentional localized wording separate from generated mechanical summaries.

### Description authoring workspace

Descriptions are a first-class workspace, with a full [Description Studio design](DescriptionAuthoringPlan.md). Place a contextual keyword/wording/tag/color/snippet library beside the source/guided writer and real renderer preview. Authors can insert a stable term reference, bind a typed gameplay parameter, wrap selected text with approved formatting, or reuse a versioned snippet without memorizing syntax.

The preview selects rank, upgrade level, Base/EX variant, locale, and game surface. Toolkit and TMP targets use their actual adapters, fonts, and capability profiles. A searchable tag reference shows syntax, examples, target support, and prerequisites. Gameplay tags, formatting tags, and glossary keywords remain distinct.

Keep player prose, generated mechanical summary, and original source/designer notes separate. Diagnostics jump to the relevant source span or mechanic. Shared keyword/style/snippet edits show affected consumers and revision differences. Parameter values update through bindings; merely writing a keyword never adds its gameplay behavior. The initial version uses source/plain editing plus separate rendered preview because Unity TextField does not display rich tags.

## 6. Simulation lab

```text
┌─────────────────────────────────────────────────────────────────────────────────┐
│ Mian  /  Scenarios  /  Three-layer absorption            Draft revision …         │
├───────────────────┬──────────────────────────────────────┬──────────────────────┤
│ SCENARIO          │                                      │ SELECTED ACTOR       │
│ Player team       │   A1   A2   A3      E1   E2   E3      │ Enemy E1             │
│ Enemy team        │                                      │ HP / shield / gauge  │
│ Active / reserve  │            3D battlefield            │ Impressions: 3       │
│ Control policies  │                                      │ Source: Mian         │
│ Initial state     │   Target set, slot and socket        │ Current modifiers    │
│ Scripted inputs   │   overlays available on demand      │ Pending outcomes     │
│ Assertions        │                                      │ [Why this target?]   │
├───────────────────┴──────────────────────────────────────┴──────────────────────┤
│ [Reset] [Run/Pause] [Step command] [Step event]          Seed […]  Speed […]      │
│ Root action → Primary hit → Absorb → Removal burst → Secondary hit               │
│ Trace  |  Damage breakdown  |  State diff  |  Assertions  |  Presentation cues    │
└─────────────────────────────────────────────────────────────────────────────────┘
```

Both teams can be manual, scripted, dummy-controlled, or AI-controlled. Presets cover low HP, full gauge, shielded, recovery-blocked, layered status, reflect, cap bypass, and missing targets. Overrides are scenario data, never edits to the actual character asset.

Logical target selection has a small battlefield slot diagram and a real target-set overlay. Selecting an actor shows candidate filters, exclusions, sorting, ties, and seeded random choice. Moving a model for visual framing does not change battlefield adjacency.

The trace is hierarchical: root action → target/hit → rule evaluation → operation → resulting facts. Skipped rules are available with reasons such as “Actor lacks Woman,” “Face Mask is 0,” “Already fired for this action/target,” or “Status protected from cleanse.” Selecting a trace row highlights the corresponding authoring node and state diff.

Expose a formula breakdown with named inputs, snapshots, additive/multiplicative stages, bypasses, rounding, cap application, shield loss, HP loss, and overkill. For a 40%-of-damage heal, show the exact damage field and computed recovery rather than merely displaying a final number.

Distinguish **Step command** (execute the real engine) from **Step event** (advance playback/inspection). The rule trace can move through recorded evaluations without pretending combat has paused before each mutation. Run invalidation is visible when a draft changes. A freeze/compare action keeps the prior result and reports differences in events, targets, resources, and damage.

## 7. Actor and damage-animation studio

Actor view: choose prefab or mesh/material fallback; inspect scale, facing/yaw correction, rig, Animator, bounds, health-bar origin, VFX sockets, projectile spawn/impact anchors, and rest location. Show incompatible rig/clip mappings as actionable problems. Use production actor creation through injected preview dependencies.

Ability view: a shared timeline with lanes for actor motion, **logical damage contacts**, cosmetic impacts, target reactions, VFX, audio, camera, and recovery. Logical contact markers are read-only references to combat definitions here. A visual preset must not silently alter target scope or gameplay hit count; the current DamageAttackDefinitionDrawer couples the two (`Assets/Project/Editor/DamageAttackDefinitionDrawer.cs:36–42`), which the Studio should remove through explicit separation.

Each skill selects authored animation timing or procedural choreography. This choice matters because current multihit procedural presentation disables Animator timing markers. Show cue binding status and deterministic fallback placement. A cinematic with extra visual slashes can map several effects to one logical hit, but cannot produce new combat hits.

Damage reaction profiles cover hit/flinch, heavy hit, block, evade, shield-only impact, shield break, additional damage, DOT, stun, cap reached, fatal save, healing, and defeat. Author priority, overlap/blending, interruption, return state, and motion limits. Test several targets reacting simultaneously and a target saved from defeat. Death presentation follows committed defeat rather than an animation-local HP check.

The viewport supports orbit, pan, frame selection, target socket/bounds overlays, slow playback, and restarting a cue. Rewind restores/replays a checkpoint. The lab must clean up hidden objects, textures, audio, and preview scenes on reload/close. Its default path never opens or edits Battle.unity.

## 8. Validation and editing states

| State | Visible behavior |
| --- | --- |
| Unsaved draft | Clear draft indicator; save/recovery available; apply uses the exact validated revision |
| Incomplete rule | Node-local explanation and Problems entry; draft can be saved |
| Unsupported runtime feature | Explicit “Not supported by this runtime” issue; unavailable for game application |
| Ambiguous kit wording | “Needs decision” entry linking source text and affected rules; author supplies semantic choice |
| Valid compilation, untested changes | Simulation runs; relevant scenario results marked stale |
| Missing visual binding | Fallback preview plus precise asset/cue issue; distinguish presentation warnings from required-binding errors |
| Shared mechanic changed | Impact list of characters, variants, and scenarios before applying |
| Failed application | Previous active content retained; changed/pending assets and recovery action shown |

Use separate labels for blocking errors, warnings, and unresolved design decisions. Do not use a green check based solely on schema validity. Show which scenarios ran against which revision, and which authoring nodes they exercised; unexecuted branches remain visible.

Undo/Redo, save, validation, and running a disposable scenario require no confirmation dialog. Applying a complete validated bundle shows its concrete diff and capability report. Avoid repeated modal approvals during editing. External conflicting edits open a merge/reload decision with both revisions preserved.

## 9. Visual system and usability checks

Proposed style: neutral layered surfaces; one selection accent; restrained semantic colors; sentence case; compact text labels; consistent inline field alignment; small radii; quiet separators; minimal animation. Maintain a clear distinction between selected, focused, disabled, inherited, and invalid states. Support both editor themes and readable Thai text with sufficient line height and a verified font fallback.

Choose spacing, type scale, pane widths, and density as design tokens in USS. Treat exact values as starting values to establish with real content and author tests; avoid scattering inline geometry through UXML. Keep author-visible names separate from stable IDs, which are available on demand in the inspector.

Keyboard navigation, visible focus, labeled icons, adjustable density/text scaling, non-color error cues, non-drag alternatives, and reduced motion are design requirements to verify in the vertical slice. Do not assume browser ARIA behavior or CSS features automatically work in Unity UI Toolkit; evaluate actual editor controls and assistive technology support before claiming accessibility conformance.

Evaluate the tool through the game-design filter: clarity comes from sentence summaries and causal traces; response from immediate reversible editing and fast reruns; satisfaction from a rule visibly working with consistent visual/audio feedback; fit from the existing card battle model and Thai content; motivation from reusable mechanics and saved verified scenarios.

## 10. Proposed UXML composition

One entry document, not one unmaintainable file:

```text
CharacterCreatorStudio.uxml
  StudioToolbar
  StudioWorkspaceSplit
    CharacterKitOutline
    DocumentHost
      CharacterOverviewView
      RuleEditorView
        RuleHeader / TriggerBlock / QueryBlock
        ConditionGroup / ActionGroup / ModuleCall
      ParameterTableView
      StatusResourceEditorView
      DescriptionStudioView
        DescriptionWriter / DescriptionSource / RendererPreview
        KeywordLibrary / WordingLibrary / RichTextTagReference
        SemanticStylePalette / SnippetLibrary / ValueBindingInspector
      SimulationLabView
      ActorPresentationView
    ContextInspector
  EvidenceDock
    ProblemsView / RuleTraceView / DamageBreakdownView
    StateDiffView / ReferencesView / DescriptionPreview
```

Reusable row templates and custom VisualElements render dynamic nested nodes from the document model. UXML supplies structure, USS supplies tokens and styling, view models supply selection and state, and editor application services execute edits. No combat decisions live in UXML callbacks. Use editor-only controls in the editor host; a potential future runtime host uses runtime equivalents.

Existing `CharacterHub.uxml` is a game-facing layout, not the Studio shell. Reuse card/status art and terminology where useful, while designing the Studio for authoring density rather than a large character showcase.

## 11. Prototype evaluation before implementation expands

Test the rule editor first with a triggered heal, Mian's Face Mask protection, and her three-layer absorption. Ask authors to read a collapsed rule, change its frequency, distinguish the attacking actor from the healed ally, find a blocked condition, and compare a replay after a parameter change. Any mistaken prediction is evidence to revise labels or scope visualization before adding a node canvas or decorative polish.

Then test the 3D lab with missing clips, zero available secondary targets, multiple simultaneous reactions, root motion, shield-only hits, and fatal interception. Passing means the core result is unchanged by animation speed, skipped cues, or disabled rendering, and the author can identify the source of each visible outcome.
