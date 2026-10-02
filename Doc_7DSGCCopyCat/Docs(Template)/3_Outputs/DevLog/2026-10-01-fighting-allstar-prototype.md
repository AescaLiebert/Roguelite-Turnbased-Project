---
slug: fighting-allstar-prototype
status: needs-human
source: manual
gdd_tags: [identity, mechanics, prototype-truth, architecture, roadmap]
owner: orchestrator-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# DevLog: 2026-10-01 — Fighting Allstar design and architecture

## Goal
Create a filled GDD and a first-prototype architecture from the owner's gameplay requirements, using the existing documentation template and real Unity project evidence.

## Work
- Captured the user brief in `../../1_Inputs_Templates/project-context-brief.md` and created the bootstrap task card.
- Read the GDD template, project instructions, bootstrap workflow and handoff contract; resolved outdated paths through the numbered documentation hub.
- Inspected Unity/package/build scene settings, character/card data, team provider/spawning, battle state, inventory and gacha scripts.
- Delegated the GDD and architecture drafts to focused design/architecture agents; a QA agent prepared acceptance criteria and cross-document review.
- Consulted current official platform/service documentation for the architecture through the architecture agent.

## Confirmed Repository Findings
- Unity 6000.3.4f1, Built-in rendering, Input System 1.17.0 with Both handlers.
- Current UI uses uGUI/TMP; LeanTween sources exist. UI Toolkit is requested and the engine module is present.
- Five legacy scenes are enabled; requested MainMenu and Combat scenes are future targets.
- `IBattleDataProvider` is a local team-data seam containing Unity references, not an authoritative match protocol.
- Local battle, card and gacha prototypes exist. `PlayerCharacterRoster.cs` is empty; no deployed service or passing build was verified.
- Existing Kyo skill assets have zero-based serialized rank labels and percent-like multiplier values that conflict with script conventions. A future migration must validate both.

## Design and Architecture Decisions
The GDD and architecture plan record proposed rules separately from confirmed user intent. They cover team counterplay, cards/PG, reserve eligibility, generated dungeons, persistent ownership, ultimate progression, deterministic shared combat, and trusted online settlement.

The owner subsequently confirmed C0/6 through C6/6, linear stages with randomized opposing character teams, and Firebase third-party authentication. These replace the earlier provisional 1/6 progression and branching-map assumptions in all final drafts.

## Verification
All original GDD tags are present; local Markdown links, required metadata, fences and JSON examples passed structural checks. Observed prototype file references resolve to real files. Cross-document review corrected C0–C6 scaling, obsolete branching rules, damage rounding, passive scope, fizzle PG, private opening-hand events, deadline ordering, selector settlement and milestone order. No runtime tests, Unity playtest, Windows build, APK build or service deployment is claimed by this documentation task.

## Next Session
Review the proposed design and architecture, then implement the first dependency-ordered milestone in the architecture plan. Keep subjective game feel and service/dependency/build changes at their documented human checkpoints.

## Git
No commit or publication was requested. Existing untracked assets and documentation were present before this task.
