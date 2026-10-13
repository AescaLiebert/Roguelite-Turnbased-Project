---
slug: fighting-allstar-actor-camera-gdd
status: needs-human
source: manual
gdd_tags: [visual-audio, actor-camera, mechanics, architecture]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Task Card: Actor and camera contract in the GDD

## Player-Facing Goal

Future fighters and card animations preserve the owner's agreed preparation, action, recovery, camera framing, and queue behavior.

## Source

- Direct owner request on 2026-10-09: write the implemented actor/camera state architecture into the GDD for future consistency.
- Owner-confirmed revisions in the same conversation: type/rank camera rules, support exceptions, two-thirds-body portraits, and moving ultimate Rest lead-in.
- Existing playback contracts and presentation code provide implementation evidence, not authority for new design decisions.

## GDD Reference

- `@tag:visual-audio`: readable event-driven presentation and skippable cinematics.
- `@tag:mechanics`: recorded combat resolution; animation never recalculates combat outcomes.
- `@tag:architecture`: pure combat authority separated from Unity presentation.
- Add `@tag:actor-camera` as the canonical, owner-confirmed state contract.

## Type and Scope

Documentation of existing behavior; no new player-facing feature or runtime architecture decision.

- Update `1_Inputs_Templates/GDD_Fighting_Allstar.md` and link its existing visual/audio and architecture sections to the new contract.
- Link `Tests/CombatPresentation/ActorDrivenExecution.md` back to the canonical GDD section.
- Record the session in a DevLog.
- Out of scope: runtime changes, card/SO/scene assets, balance, dependencies, branch/commit/PR creation, and team publishing.

## Acceptance Criteria

- [x] Actor and camera states have explicit entry/exit, owner, queue, and interruption rules.
- [x] All rank/target exceptions match the owner's latest instructions, especially Rank 2 support versus Rank 3 self support.
- [x] Captured target focus, full-formation AoE, held reactions, first/last-hit barriers, and ultimate lead-in are unambiguous.
- [x] Timing values are labeled starting values with playtest/adjustment guidance; hard transition rules remain separate.
- [x] Source links, GDD tags, and local paths resolve; unrelated edits are preserved.

## Human Checkpoints

The owner authorized documenting the already-agreed behavior. This does not approve the rest of the draft GDD or certify final-art feel. New behavior, subjective feel changes, architecture changes, merge, and publishing still require their own owner review.

## Router Decision

Workflow: `/implement-feature`, documentation/design stage only. The canonical GDD is the requested design artifact; implementation/branch/PR stages are not authorized or needed. No new ADR: this session records an existing presentation boundary rather than deciding a new one.

Output: [GDD @tag:actor-camera](../../1_Inputs_Templates/GDD_Fighting_Allstar.md#actor-and-camera-state-architecture). Documentation checks passed: 26 local links resolved, fences balanced, tag/anchor present, and diff whitespace clean. Wording is ready for human review; no further implementation handoff was started.
