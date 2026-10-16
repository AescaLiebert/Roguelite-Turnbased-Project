---
slug: elemental-reaction-resolution
status: approved
source: manual
gdd_tags: [elemental-reactions, mechanics, architecture]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# ADR-002: Shared-core elemental contact resolution with bounded child events

Date: 2026-10-15. Status: owner approved the documented baseline direction with “LGTM” and subsequently authorized implemented slices A–C. Specific unanswered policies and later kit extensions retain their explicit proposed status.

Design: [GDD `@tag:elemental-reactions`](../../1_Inputs_Templates/GDD_Fighting_Allstar.md#genshin-inspired-elemental-reactions). Detailed proposal: [implementation plan](../Specs/genshin-element-system-arch-plan.md). Extends the shared-authority boundary described by [ADR-001](001-authoritative-combat-core.md) without changing its historical approval status.

## Context

The selected design uses existing debuff stacks as reaction fuel, preserves source fuel for Swirl/Crystallize, and prevents Overload from generating another reaction. Resolution must occur during damage contacts and support AOE, multiple visual hits, deferred defeat, replay, and recipient privacy. The current core provides related status and damage foundations but has no complete elemental resolver or queued-combo forecast.

## Proposed decision

- Keep element identity separate from damage family, attribute, and physical hit reaction.
- Evaluate reaction selection/consumption intents in a pure shared rule function; let the combat authority commit damage, exact instance removal, output statuses, and ordered events.
- Use explicit root/parent/source lineage and a bounded per-action child queue. Only permitted origins can apply another element; terminal damage cannot reenter application. The owner later explicitly allowed authored Baron Bunny expiry attacks and Glacial Waltz retaliations alongside Swirl. Ordinary DOT/reflect remains terminal.
- Preserve simultaneous root-hit inputs. Drain ordered children before the next root hit; check captured Swirl source instances against live consumption.
- Share selection semantics with symbolic preview/AI through permitted facts. Do not execute private RNG or hidden opponent state to produce a local exact forecast.
- Version content, state, and public projection changes; default omitted element/application data to neutral legacy behavior.

Specific class/API names, resistance/formula stages, and unresolved GDD decisions remain proposed details in the plan. This ADR does not authorize new dependencies, runtime migration, or deployment.

## Alternatives

G adoption update: expected-value AI evaluation receives a detached observed-board snapshot, its own cards and independent synthetic random streams. Opponent cards, event history and live combat/card entropy are excluded at the planning boundary. Ordered candidates retain ordinary alternatives; consuming DOT/control has an explicit opportunity cost. Local player forecasts continue to be symbolic. [G–H evidence](../../../../Artifacts/GenshinElementSystem/ImplementationGH.md) records privacy and retention checks. This extends the approved permitted-facts decision without a new service or package.

| Alternative | Benefit | Reason not selected for the baseline |
| --- | --- | --- |
| Separate aura preserving original debuffs | Avoids losing DOT value | Conflicts with the owner's confirmed all-stack consumption model. |
| Scan all status pairs after every mutation | Small initial hook | Violates damage-contact timing; makes coexisting setup react automatically and risks repeated consumption. |
| Recursive ordinary damage callbacks | Reuses card execution paths | Can reapply keywords/passives, lose finite-chain guarantees, and complicate ordering/defeat. |
| Unity/VFX-triggered resolution | Easy visual synchronization | Violates shared authority and makes rules depend on playback timing. |
| Full authoritative simulation for local preview | Exact outcomes with full state | Risks private RNG/state leakage; current draft does not implement such combat simulation. |

## Consequences and adoption evidence

The proposal creates explicit authoring, validation, projection, and forecasting work, but makes consumption and finite propagation inspectable. It preserves one source of combat truth and allows legacy content to remain neutral. Separate reaction-policy and element-resistance stages require careful regression coverage.

Implementation requires a task covering the selected slice and answers to its dependent decisions; the approved architecture direction need not be reapproved. Required evidence: all-stack atomicity, no rider reapplication, finite Swirl chains, terminal Overload, simultaneous root parity, deterministic replay/resume, projection privacy, and non-elemental regression preservation. Later kits also require typed removal causes, allowlisted triggered-attack origins and anti-retaliation guards. The initial documentation sessions provided no runtime evidence; [A–C results](../../../../Artifacts/GenshinElementSystem/ImplementationAC.md) now establish the implemented subset. Swirl and later-slice evidence remain outstanding.
