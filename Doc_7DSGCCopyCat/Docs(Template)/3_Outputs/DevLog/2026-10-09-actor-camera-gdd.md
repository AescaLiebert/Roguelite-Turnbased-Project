---
slug: fighting-allstar-actor-camera-gdd-log
status: needs-human
source: manual
gdd_tags: [visual-audio, actor-camera, mechanics, architecture]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# DevLog: 2026-10-09 — Actor/camera state contract in the GDD

## Goal

Record the owner's implemented and agreed presentation rules in the canonical GDD so future card/character work cannot fall back to contradictory historical camera behavior.

## What I Did

- Added `@tag:actor-camera`: ownership boundary, actor phases/exit barriers, three camera states, complete rank/target matrix, snapshot focus, multi-hit/reaction readiness, ultimate lead-in, queue/cleanup rules and acceptance checks.
- Updated the GDD's revision status, generic visual/audio camera description, mechanics/architecture cross-links and contributor guardrail without approving unrelated proposed sections.
- Linked implementation/fixture notes back to the GDD and preserved unrelated runtime, card asset and UI-design edits.
- Checked Markdown structure, tag registration, relative links and diff whitespace: 26 existing/new local links resolved, code fences balanced, canonical tag/anchor present, and no whitespace errors. No runtime/build/scene/SO changes were made in this documentation session.

## Key Decisions

- The GDD owns behavior; test notes contain supporting implementation/history, not a parallel source of design truth.
- Rank 2 Buff/Heal face WindUp cuts to its exact Rank 1 Action camera for self/selected-ally/team scopes. Rank 3 self holds face through Action; Rank 3 team remains in rear-team view.
- Offensive camera follows the caster, never displaced targets; target focus and whole-formation AoE are captured positions. Recovery and next action await readiness.
- Ultimate visibly moves to Rest even after support, then pauses before the portrait overlay. Wider upper-two-thirds portraits and skippable cleanup preserve readability/response.
- No new runtime architecture decision was made; this records existing owner-confirmed behavior, so no additional ADR or implementation work was introduced.

## Verification and Feel Notes

Documentation-only checks are not a new gameplay test pass. The existing actor polish and ultimate/HUD fixture evidence is linked from the contract. Final-art framing, anticipation and recovery still require owner playtesting. Timings remain labeled starting values with adjustment directions; state order/cuts/barriers are required rules.

## Next Session

- Human review of the new GDD wording; unrelated proposed GDD sections retain their original review status.
- Future presentation changes cite `@tag:actor-camera`, update the matrix first with owner approval, and rerun the relevant isolated fixture plus final-art checks.

## Git Commit Summary

Suggested only; no commit, branch, PR or publishing was performed:

```text
docs(combat): codify actor and camera execution states in GDD
```
