# Character Creator Studio

The [skill effect ownership implementation](SkillEffectOwnershipImplementation.md) adds shared conditions and status reactions to skill/ultimate authoring, executes damage follow-ups, and migrates Taki, Billy97 and Benimaru99 to their owning skills/statuses. Innate passive rules retain PassiveSO ownership.

The [passive centralization implementation](PassiveCentralizationImplementationReport.md) adds Passive Overview, shared imported/new event authoring, live WHEN/WHO summaries, scoped native ownership and fixed current-hit calculation modifiers. Its [audit](PassiveCentralizationAudit.md) remains the historical rationale. Dedicated Genshin support/snapshot migration is explicitly incomplete and remains visible as compatibility mechanics.

For choosing between passive auras, trigger rules and imported support, see the [Passive authoring guide](PassiveAuthoringGuide.md), including Kaeya's current ownership, migration limits and corrected preview layout.

20 October 2026 · M0–M5 scoped delivery; M6 paused/incomplete · Unity 6000.3.4f1

The [native kit editor](NativeKitAuthoringReport.md) now opens and saves a **CharacterObject and its four data SOs directly**, without a required draft asset. The kit navigator exposes Overview, Identity & Stats, Skill 1, Skill 2, Ultimate and Passive. Joe96 has been authored and saved through these pages. Blank initialization, art replacement and dynamic runtime wording routing remain open; full Studio completion is not claimed.

The Studio should let an author build, understand, simulate, and present a complete character from one workspace. Its main editing model is a **readable rule tree**, backed by a deterministic combat rules runtime. Its main reliability feature is a trace that explains both why a rule fired and why it did not.

The M0–M2 implementation now includes an independent UXML EditorWindow, durable drafts, a parameter-bound description writer, a production-engine Kaeya opening-gauge scenario and isolated actor/text previews. The standard Editor font is the existing Thai-capable `Resources/UI/zh-cn.ttf`. The [implementation report](ImplementationReport.md) separates implemented scope from current verification evidence; the remaining design documents describe the wider system through M8.

Open the tool in Unity with **Fighting Allstar → Character Creator Studio**. The report links real-engine parity, save/reload/Undo, domain-reload recovery, actor checks and verified Thai Toolkit/TMP preview evidence.

M3 adds reusable ordered action programs, atomic resources and layer consumption, execution controls, anchored target queries, history/threshold predicates, interception policies and ability variants. See the [M3 implementation report](M3ImplementationReport.md) for semantics and evidence. Mian remains a case study and is not installed as a playable character.

The subsequent [M3 A–F audit](M3Audit.md) distinguishes engineering coverage from designer acceptance. Its [closure implementation](M3ClosureReport.md) adds the semantic rule workspace, contextual inspector, shared resource/status libraries, complete variant sequence controls and fresh UI authoring evidence. The full product through M8 remains planned.

Read the roadmap first, then use the detailed plans as implementation references:

1. [Implementation roadmap](ImplementationRoadmap.md): ordered work packages, dependencies, parallel streams, milestones, and acceptance gates.
2. [Architecture plan](ArchitecturePlan.md): current limitations, data model, event ordering, damage policies, migration, and delivery gates.
3. [UX and UI design plan](UXDesignPlan.md): application layout, rule authoring, simulation, actor animation, validation, and UXML composition.
4. [Description Studio plan](DescriptionAuthoringPlan.md): description structure, live previews, parameter bindings, keyword/tag/color/snippet databases, and renderer support.
5. [Mian kit specification](MianKitSpecification.md): the supplied Thai kit translated into rules, unresolved semantics, and acceptance scenarios.
6. [Implementation report](ImplementationReport.md): M0–M2 files, supported scope, checks, evidence and remaining boundaries.
7. [M3 A–F audit](M3Audit.md): current coverage, reproduced validation defects, concept comparison and required authoring closure.

[Interface concept](StudioConcept.png) shows the proposed rule workspace. Its rule settings and trace are illustrative design content, not a screenshot of implemented behavior.

![Character Creator Studio interface concept](StudioConcept.png)

## Recommended decisions

| Area | Recommendation |
| --- | --- |
| Host | A dedicated Unity EditorWindow, usable docked or as a separate window, with one root UXML and reusable UXML sections |
| Primary authoring | Trigger → target query → nested conditions → ordered actions; add declarative modifiers for continuous effects |
| Storage | Versioned authoring assets with stable IDs and flat node tables; compile into Unity-independent runtime definitions |
| Runtime | Extend the existing shared combat core with generic rules and authoritative lifecycle hooks |
| Preview | The production rules engine drives a disposable simulation; production playback consumes its events |
| 3D presentation | Per-model actor profiles plus per-ability presentation cues, separate from damage calculation |
| Description authoring | A first-class writer with parameter-bound values, semantic terms, approved rich text, real renderer previews, and versioned snippets |
| Wording database | Separate keyword/glossary, gameplay-tag, rich-text capability, semantic style/color, and snippet libraries with usage/dependency navigation |
| Migration | One active source of truth per character; preserve legacy assets until behavioral parity is demonstrated |

## What the audit changes about the premise

The initial source audit confirmed the concern about Genshin support fields, while also finding generic triggers, conditions, commands, else branches and ordered skill sequences. Several declared execution controls were not enforced at that baseline; M3 implements explicit version-one limits, cooldowns and origin controls. Several Genshin passives still use a separate `CombatSupportDefinition` capability bag with dedicated resolver paths; the sampled Lisa, Barbara, and Lumine passive assets had empty `Reactions` lists.

The central problem is therefore **incomplete composition and lifecycle coverage**, compounded by authoring and serialization limitations. Replacing SerializeField alone would not solve it: fields remain a reasonable way to persist data. The Studio needs a durable schema, generic operations, complete timing contracts, and an explanation of execution.

Mian is a useful reference because her kit combines team filters, mutable resources, layered marks, removal reactions, thresholds, historical conditions, lethal interception, skill variants, and unusual damage policies. These mechanics inform independent synthetic acceptance fixtures for the runtime and Editor. Her wording contains ambiguities that remain visible; no complete Mian game kit or roster entry is required.

## Intended author experience

Select a character, open a mechanic, read its sentence summary, inspect its nested rule, and run a saved scenario. Clicking a simulator event highlights the exact rule, condition values, resource changes, damage formula, target choice, and animation cue responsible. Authors can reuse an existing mechanic without copying its implementation or editing combat code.

The proposed visual direction is a restrained desktop creation tool: neutral surfaces, a clear hierarchy, compact labeled controls, a structured outline, a contextual inspector, and a collapsible evidence dock. Description authoring pairs source/guided editing with actual UI Toolkit and TMP previews and a searchable vocabulary/formatting library. The complex part should be the kit being expressed, not finding where to edit it.

M4 adds the Description Studio writer, searchable vocabulary/formatting library, typed pinned snippets, localized configuration branches, resolved-value previews and exact-revision review. See the [M4 report](M4ImplementationReport.md) for usage, evidence and renderer font limits.


M5 adds the [Quick 3D Battle Lab](M5ImplementationReport.md): volatile Real Battle and Training Battle sessions using the runtime battle scene, with actor/target setup, optional model overrides and live evidence. Scenario files are not required.

M6's [Actor and presentation lab](M6ImplementationReport.md) remains partial and paused at the user's request. Its scoped checks do not establish milestone completion. The subsequent [native bridge fix](BridgeImplementationReport.md) connects supported Studio edits to actual runtime SO packages.

