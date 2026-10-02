---
slug: fighting-allstar-stack
status: needs-human
gdd_tags: [identity, tech-stack, prototype-truth, architecture, roadmap]
owner: orchestrator-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Fighting Allstar — Project Stack

Design: [GDD_Fighting_Allstar.md](GDD_Fighting_Allstar.md); load relevant tags.

- Online 3D card roguelite; PC/Android; 3 active + 1 reserve per side.
- Confirmed: Kyo94; linear dungeons vs randomized character teams; persistent ownership; C0/6–C6/6; Firebase third-party login.
- Current: Unity 6000.3.4f1, Built-in, Input System 1.17/Both, uGUI/TMP, LeanTween source.
- Prototypes: `Assets/Script/Gameplay/`, `Card/`, `Character/`, `Gacha/`; roster empty, online authority absent.
- Target: UI Toolkit, MainMenu/Combat, shared C# core, Firebase/Firestore, Cloudflare gateway, C# host; UnityMCP tooling.
- Separate content/ownership/match state. Server owns RNG/results/rewards. Animation consumes events. Local practice grants no online rewards.

Read `@tag:prototype-truth`, `@tag:mechanics`, `@tag:dungeons`, `@tag:ai`, `@tag:architecture`, `@tag:roadmap`.

Plan: `../3_Outputs/Specs/fighting-allstar-prototype-arch-plan.md`; ADR-001 and matching TestPlans/DevLog.

Scopes: combat, cards, characters, ai, dungeon, inventory, economy, network, ui, audio, scene, build, docs.
