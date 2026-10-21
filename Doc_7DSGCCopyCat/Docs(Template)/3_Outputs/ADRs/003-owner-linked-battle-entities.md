---
slug: owner-linked-battle-entities
status: approved
source: manual
gdd_tags: [mechanics, architecture, elemental-reactions]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# ADR-003: Owner-linked battle entities and shared damage interception

Date: 2026-10-21. Accepted direction: Apricha approved the Summon / Geo Construct plans with "LGTM" and requested backend implementation for Fischl, Xiangling, and Ningguang, explicitly deferring visuals.

## Decision

- The shared core owns lightweight battle entities, separate from fighters and economy summons. `GeoConstruct` is a category; positive authored capacity enables shared interception independently of category.
- A reciprocal grey structural owner marker is the sole lifetime authority. Gameplay dispels/layer consumption cannot remove it. Owner defeat, expiry, replacement, depletion, and battle completion are typed lifecycle causes.
- Typed inline entity definitions are cloned into the battle instance. Generic Summon operations work from both card effects and passive operations, with backend authoring validation. Native Unity assets persist the graph through existing Studio JSON; the inline Summon properties are excluded from Unity field traversal to avoid serializer depth overflow.
- Deterministic owner-side turn-end jobs run after existing DOT/healing and turn-end passive notification, before lifetime expiry. Both-side activation is explicit content for Pyronado. Animation callbacks do not execute rules.
- Completed integer damage is immutable `FormulaDamage`. Construct absorption precedes personal shield allocation and HP caps/survival. Every existing damage commit route uses the common incoming-damage policy seam. The owner confirmed that excess damage reaches the protected ally.
- Public entity views and immutable event snapshots support recipient projection, replay, save/load, and detached AI observations. Private hands and live entropy remain excluded from AI/public boundaries.

## Alternatives and consequences

A personal barrier per ally copies capacity and cannot implement one shared team pool. Using `FighterState` would introduce unwanted hand/formation/initiative behavior. A Unity object manager would leave the shared authority unable to reproduce the result. A separate entity timer would let the owner status and object disagree. These alternatives were rejected in favor of the linked model.

The damage allocation seam changes a central combat path and requires cap/shield/lethal-save regression checks. Historical snapshots with no entity list remain empty; new source character packages explicitly opt into typed definitions. Existing `BypassShield` does not bypass constructs; the separate `BypassConstruct` policy must be authored explicitly.

Fischl/Oz, Xiangling/Guoba/Pyronado, and Ningguang/Jade Screen use the same generic model. Source ambiguities and retained interpretations are recorded in the native metadata review notes and backend implementation report. Visual entity presentation remains the next phase and is not accepted by backend tests.

Related: [architecture](../Specs/summon-geo-construct-arch-plan.md), [design](../Specs/summon-geo-construct-design-spec.md), ADR-001's shared authority boundary, and ADR-002's bounded elemental contact direction. This ADR does not alter their historical approval status.
