---
slug: genshin-element-system
status: approved
source: manual
gdd_tags: [mechanics, elemental-reactions, actor-camera, ai, architecture]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Task card: elemental reaction GDD and implementation plan

## Goal and authorization

Owner request, 2026-10-15: record the selected Genshin-inspired design in the GDD and produce an implementation plan. This task authorizes documentation and architecture proposals. Runtime implementation, character assets, balance migration, builds, branches, commits, PRs, and publishing are outside this request.

The owner confirmed simplified turn-based adaptation, full Genshin roster including SUB, all-stack consumption, source preservation for Swirl/Crystallize, and terminal Overload damage. Detailed proposed rules must retain their status rather than being promoted to confirmed facts.

Follow-up, 2026-10-15: the owner approved the documented plan with “LGTM,” supplied five Genshin kits, and authorized generated missing stats. Additional confirmed clarifications cover R3/C0, target icons, Kaeya's C5 scope, safe Bunny cleanse, Bunny/Waltz reaction contacts, Amber's Explosion category and Barbara's Dissolve meaning. Current follow-up scope remains documentation/stat authoring, not runtime implementation.

## Canonical context

- [GDD: elemental reactions](../../1_Inputs_Templates/GDD_Fighting_Allstar.md#genshin-inspired-elemental-reactions), `@tag:elemental-reactions`.
- Existing `@tag:mechanics`, `@tag:actor-camera`, `@tag:ai`, and `@tag:architecture` remain governing boundaries.
- Historical input: [design audit](../../../../Artifacts/GenshinElementSystem/DesignAudit.md). The new GDD section supersedes its pending questions where the owner has answered them.

## Workflow and outputs

`/implement-feature`, documentation/design/architecture planning stages only, executed by one agent. The owner's current request authorizes making the proposed plan concrete and reviewable. Architecture and feel remain review subjects before future runtime work; this does not require another confirmation to write these documents.

- [Design spec](genshin-element-system-design-spec.md): player flow, feedback, and GDD traceability.
- [Implementation plan](genshin-element-system-arch-plan.md): target contracts, affected files, ordered slices, dependencies, and completion gates.
- [Test plan](../TestPlans/genshin-element-system-test-plan.md): planned functional, abuse, replay, privacy, and readability checks.
- [ADR-002 draft](../ADRs/002-elemental-reaction-resolution.md): shared-core contact resolution proposal.
- [DevLog](../DevLog/2026-10-15-genshin-element-system-design.md): documentation outcome and verification.
- [Genshin roster](CharacterData/genshin-character-roster.md): five supplied kits, generated complete stat profiles, rank/constellation proposals, and explicit missing source fields.

## Acceptance

- [x] Canonical GDD distinguishes confirmed choices, proposed baseline, observed support, and unresolved tuning.
- [x] Directional reactions, stack consumption/exceptions, contact timing, propagation, and hit-batch behavior are documented.
- [x] Plan identifies authoring, shared authority, projection, preview, presentation, AI, and integration work.
- [x] Every implementation slice has dependency and exit criteria; unresolved choices affect only dependent slices.
- [x] Tests cover both sides, multi-hit/AOE, all-stack sacrifice, finite chains, immunity, reserve/death boundaries, and hidden information.
- [x] Documentation links, tag registration, required metadata, fences, and whitespace are checked before handoff.

## Handoff

Historical authorization: “LGTM, implementation Slice A-C /goal” on 2026-10-15 approved A–C. The owner subsequently explicitly requested D–F, Genshin-style FCT and shared DOT corrections. See [D–F task/results](genshin-element-system-df-task-card.md), which supersedes the documentation-only scope for those slices. G/H acceptance and AI work remain separate.

Documentation deliverables are complete. The owner subsequently approved the baseline architecture/design plan; no runtime implementation or final balance is claimed. Later typed kit extensions are proposed details, with confirmed kit choices identified explicitly. Unanswered GDD decisions E-01–E-08 and roster K-01–K-05 still need recorded answers before affected production rules are enabled; settled choices do not require reconfirmation.
