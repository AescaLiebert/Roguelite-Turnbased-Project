---
slug: authoritative-combat-core
status: needs-human
gdd_tags: [mechanics, architecture, roadmap]
owner: architect-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# ADR-001: Shared deterministic C# combat core with authoritative online services

| Field | Value |
|---|---|
| Status | **Proposed** |
| Date | 2026-10-01 |
| Author | architect-agent |
| GDD Section | [GDD](../../1_Inputs_Templates/GDD_Fighting_Allstar.md): `@tag:mechanics`, `@tag:architecture`, `@tag:roadmap` |

## Context

Fighting Allstar needs identical 3+1 card rules for local AI development, online dungeon play and later PvP, while keeping currency, ownership, draws and results authoritative. The current Unity scripts mix combat state, random rolls, animation delays and presentation; `IBattleDataProvider` supplies local teams but does not provide authoritative sessions. Firebase and Cloudflare are requested services, but the project has no verified online combat service or persistent economy implementation.

## Decision

Propose a pure C# library in `Shared/FightingAllstar.Core/`, targeting `netstandard2.1`, built as a Unity managed plugin and referenced by an ASP.NET Core server. Unity's documented managed-plugin compatibility supports .NET Standard; a .NET Core-targeted server assembly is not automatically a compatible Unity plugin. [Unity .NET profile support](https://docs.unity3d.com/6000.0/Documentation/Manual/dotnet-profile-support.html)

The core owns battle rules, typed effect evaluation, card transforms, seeded RNG, replay and recipient projections. It accepts plain content/state/commands and returns next state/events. It cannot reference Unity objects, wall clocks, animation timing, database clients or network APIs. Immutable authored content, persistent owned progression and mutable battle state are different models. Effects use an allowlisted bounded AST and validated catalog IDs, never arbitrary scripts.

Honor the confirmed progression and dungeon contracts: `constellationTier` is integer 0–6, with a complete C0 kit and six upgrades to C6; the catalog has seven cumulative build states. Dungeons advance through a forward graph of selectable nodes with server-generated opposing character teams. Persist immutable enemy TeamSnapshots, currentNode/chosenPath, difficulty and runHP. Resume cannot reroll, revisit bypassed alternatives, heal for free or change difficulty; Rest is a committed once-only node action.

`LocalBattleSession` and `RemoteBattleSession` implement one `IBattleSession` contract. Local practice has disposable state; online progression is accepted only from trusted server results. The online `MatchService` validates Firebase identity, participant membership, command IDs, revisions, actions and deadlines, executes the shared engine, and persists the snapshot plus receipt before acknowledging. Clients receive recipient-specific snapshots/events; opponent hands, drafts and RNG never cross that boundary. AI sees only its permitted observation and submits the same legal command type as players.

Proposed added compute host: an ASP.NET Core container on Google Cloud Run, behind a Cloudflare HTTPS gateway. Firebase supplies identity and Firestore storage. Cloudflare Workers' documented languages/Wasm do not establish that a normal managed C# library can run unchanged there. Cloudflare Containers is a credible alternative that needs a hosting spike; its arbitrary-runtime container capability is separate from Workers language compatibility. The host choice is proposed, not provisioned. [Workers languages](https://developers.cloudflare.com/workers/languages/), [Cloudflare Containers](https://developers.cloudflare.com/containers/), [Cloud Run .NET quickstart](https://docs.cloud.google.com/run/docs/quickstarts/build-and-deploy/deploy-dotnet-service)

Use Firebase's server token verification and C# Firestore client with explicit server authorization and IAM. Firestore server libraries bypass client Security Rules. Battle completion, run completion and economic settlement form a durable retryable chain; unique grant IDs and transactions prevent duplicate victory rewards, summons or upgrades. [Firebase ID token verification](https://firebase.google.com/docs/auth/admin/verify-id-tokens), [Firestore server libraries](https://firebase.google.com/docs/firestore/client/libraries), [Firestore transactions](https://firebase.google.com/docs/firestore/manage-data/transactions)

Use third-party authentication through platform identity adapters. Android obtains a native provider credential and exchanges it through Firebase Unity Auth. Windows opens a hosted Firebase web provider flow in the system browser; a proposed backend broker binds verified Firebase identity to a short-lived one-use code and native verifier, then returns a custom token for Firebase Auth REST exchange/refresh. The broker is application integration work, not an automatic Firebase Unity feature. The exact first provider is undecided; Google is the proposed initial integration. [Firebase Unity provider sign-in](https://firebase.google.com/docs/auth/unity/google-signin), [Firebase web provider sign-in](https://firebase.google.com/docs/auth/web/google-signin), [Firebase custom tokens](https://firebase.google.com/docs/auth/admin/create-custom-tokens), [Firebase Auth REST](https://firebase.google.com/docs/reference/rest/auth)

Firebase Unity desktop SDK support is documented for development workflows, so it is not assumed to be a shipping Windows dependency. Both platforms use Firebase ID tokens for the same backend APIs and preserve one account UID across linked providers. [Firebase Unity desktop support](https://firebase.google.com/docs/unity/setup)

## Alternatives considered

| Option | Advantages | Costs / reason not selected as default |
|---|---|---|
| Shared pure C# core + container service | Same rules in local tests/server; fast headless simulations; minimal Unity dependence | Requires service hosting, persistence contracts, content export and cross-runtime tests. Selected proposal. |
| Dedicated Unity server build | Reuses Unity-bound content and code paths; useful if engine physics were authoritative | More runtime/deployment overhead and slower AI simulation; current battle logic does not need engine physics. Could be revisited if engine simulation becomes essential. |
| C# client + rewritten TypeScript Workers combat | Direct Workers integration and one edge deployment | Two battle implementations create rule drift and duplicate testing; unsuitable for this prototype's local/online parity priority. |
| C# via Wasm inside Workers | Potentially preserves some shared logic | Managed runtime, APIs, performance and toolchain compatibility require a separate proof; not assumed. |
| Cloudflare Containers for the same C# service | Keeps requested provider footprint; supports arbitrary runtimes | Validate availability, lifecycle, storage integration, latency and cost first; viable alternative host, not a different core. |
| Client calculates results; database stores them | Quick initial implementation | Cannot establish trustworthy battle outcomes or economy. Rejected for online progression. |

## Consequences

### Positive

- Local practice, online AI and private-room PvP execute one versioned set of combat rules.
- Reset, replay, AI search and regression testing operate without scene objects or animation timing.
- Hidden information, retries, two-device races and reconnect behavior have explicit contracts.
- Content creators retain ScriptableObject authoring while the server consumes validated pure data.

### Trade-offs

- The existing battle scripts require separation of state from presentation; this is more work than extending a single manager.
- Catalog export, integer arithmetic, deterministic RNG and serialization require parity tests on server, Windows and Android IL2CPP.
- Cloud hosting and Firestore operations have costs that must be measured; no cost estimate or deployment is approved by this ADR.
- A prototype offline harness cannot grant online progress; full online acceptance remains a required final milestone.
- Server libraries' privileged access makes application authorization and IAM part of the implementation, not something client Rules can supply alone.

### Migration

1. Export stable character/card IDs and source stats from `CharacterObject`, `SkillCardSO` and `UltimateCardSO`; preserve existing visual asset GUIDs.
2. Create core fixtures for seeded opening hands, merges, PG, damage, statuses, reactions, death and reserve entry before scene migration.
3. Replace `RuntimeCard` object references and `Guid.NewGuid()` with deterministic runtime IDs; move card logic out of `CardDeckManager`.
4. Convert `Unit`/`ActionScript` into presentation adapters and remove rule advancement from animation delays; disable legacy `GameManager` authority in the new Combat scene.
5. Split `TeamDataManager` into formation mapping, session setup and fighter view spawning; adapt its useful provider seam to plain DTOs.
6. Replace SO-based player inventory and client `GachaManager` rolls with profile/economy API receipts.
7. Add remote transport, private projections, durable command/result settlement, and restart tests; prove the full online loop in Windows and APK builds.

Migration is incremental through a new scene composition, with one authoritative loop active at a time. This ADR changes no code, packages, credentials, cloud resources or build settings. Acceptance of the design precedes a separate implementation task.

## Related

- [Prototype architecture plan](../Specs/fighting-allstar-prototype-arch-plan.md)
- [Prototype test plan](../TestPlans/fighting-allstar-prototype-test-plan.md)
- [Task card](../Specs/fighting-allstar-prototype-task-card.md)
- Existing scripts: `Assets/Script/Gameplay/BattleManager.cs`, `GameManager.cs`, `Unit.cs`, `ActionScript.cs`, `TeamDataManager.cs`, `Interfaces/IBattleDataProvider.cs`; `Assets/Script/Card/CardDeckManager.cs`, `RuntimeCard.cs`; `Assets/Script/Character/CharacterObject.cs`; `Assets/Script/Inventory/InventoryObject.cs`; `Assets/Script/Gacha/GachaManager.cs`.
