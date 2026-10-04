---
slug: fighting-allstar-unity-implementation
status: needs-human
gdd_tags: [architecture, roadmap, mechanics, items, dungeons]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Unity implementation phase plan

This is the concrete plan requested after the screen/economy design. It extends [shared-core architecture](fighting-allstar-prototype-arch-plan.md) and [ADR001](../ADRs/001-authoritative-combat-core.md). No runtime code/scenes/packages were changed by this task. The next implementation can begin with phaseA's pure rule/content slice; production gacha publish waits for the explicit guarantee policy decision. Screen mockups are design references, not Unity assets.

## 1. Inspected starting point

Repository Unity6000.3.4f1; InputSystem1.17.0, uGUI2.0.0 and UIElements module present; LeanTween source exists. The prototype `BattleManager`, `GameManager`, `TeamDataManager`, and `CardDeckManager` mixed battle mutation with presentation; these scripts and their old test scenes have now been removed. The active Battle scene uses the Core controller and local battle session. The Gacha/Loadout views still use Unity presenters over the local account service.

## 2. Dependency boundary and proposed folders

```text
Shared/FightingAllstar.Core/              pure C#, netstandard2.1 proposal
  Content/  Battle/  Cards/  Effects/  AI/  Dungeon/  Economy/  Replay/
Shared/FightingAllstar.Contracts/         serializable commands/projections/events
Server/FightingAllstar.Server/            ASP.NET Core authoritative host
  Auth/ Sessions/ Runs/ Wallet/ Summons/ Persistence/
Assets/FightingAllstar/
  Plugins/                               built Core + Contracts assemblies
  Runtime/App/                           composition, scene router, session context
  Runtime/Adapters/                      local/remote sessions, Unity web requests
  Runtime/Presentation/Combat/            views, playback, camera, animation bridge
  Runtime/Presentation/Menu/              roster, banners, map, rest, results
  UI/UXML/                               MainMenu, CombatHud and reusable views
  UI/USS/                                typography, controls, card states, layout
  Content/Authoring/                      ScriptableObjects for authoring only
  Content/Generated/                      validated pinned catalog + presentation map
  Editor/                                import validator/exporter/migration tools
  Tests/EditMode/  Tests/PlayMode/
  Scenes/MainMenu.unity  Scenes/Combat.unity
```

Current layout follows one Unity product root: `Assets/FightingAllstar/` contains `Art/`, `Prefabs/`, `Runtime/`, `Scenes/`, `UI/`, `Content/`, and `Editor/`. `Shared/` is the portable source boundary consumed by Unity through compiled `Plugins/` assemblies and by the server through project references. It is deliberately shared rule code, not an old gameplay folder; do not copy its source under `Assets`.

Core imports no UnityEngine/Firebase/HTTP/time API. Contracts contain DTOs, no SDK types. Unity presentation references Core/Contracts through adapters; server references the same source projects. Proposed assembly definitions: `FA.App`, `FA.Adapters`, `FA.Presentation`, `FA.Authoring`, `FA.Editor`, `FA.Tests`. Compile core once into Unity plugin; do not also copy its source into Assets. Validate IL2CPP/AOT serialization in phaseA; use explicit typed serializers/registrations, no runtime reflection-heavy effect activation.

## 3. Composition and class ownership

| Module | Owns | Main API / boundary |
| --- | --- | --- |
| ContentCatalog / ContentValidator | Immutable published definitions, aliases, constraints | Validate→hash→pin; reject runtimeReady=false drafts |
| BattleEngine | All rule states, card legality, packet resolution, reactions, death | Resolve(snapshot,command,content)→nextState+orderedEvents |
| PlanDraft | Reversible known transforms, target previews | BuildDraft(snapshot); Apply; UndoLast; Reset |
| CardRules | Seven slots, merges, PG/readiness/ultimate lifecycle | Shared by validator, preview and AI |
| DamageResolver / StatusSystem | Typed families, exact arithmetic, snapshotDOT, clocks | No animation callbacks in calculation |
| LocalBattleSession | Disposable authoritative simulation for development | Same IBattleSession contract as remote; no online economy rights |
| RemoteBattleSession | Authenticated commands, revision errors, reconnect | Submit sameID; fetch snapshot/event cursor; never calculate trusted reward |
| BattlePresenter / EventPlayback | Fighter/Card views, camera/audio, HP displays | Consume ordered events; skip/catchup to final projection |
| RunService / RouteGenerator | Forward graph, reachable selection, runHP, Rest/boons | ChooseNode(expectedRevision), ApplyBattleResult(receipt), Rest(choice) |
| BannerService / SummonService | Published pool, debit, draws, progress, entitlements | Atomic summon and durable receipt; server-only RNG |
| AuthAdapter | Provider sign-in, refresh, FirebaseUID session | Platform-specific native/browser flow behind common interface |
| MainMenuPresenter | Navigation, selected roster/banner/node, server-backed wallet | No purchase/HP mutations in view code |

`GameSessionContext` survives scene transitions and stores IDs/projections, not mutable CharacterObject instances. MainMenu composition creates profile/banner/route services. Combat composition resolves matchID and constructs presenters. Direct-launch Combat in Editor uses a clearly labelled local fixture; missing session online returns to Resume, never generates a random match.

## 4. Scene and UI mapping

MainMenu root: AppComposition, SceneRouter, UIDocument/MainMenuShell, AudioRoot. Views: Login, Home, Roster, Formation, Banner, Rates, SummonResults, GuaranteeChoice, DungeonEntry, RouteMap, NodePreview, Rest, RunResult. Overlay host owns Confirm/Loading/Error. Route walking uses a marker along precomputed path within the map view; it is not an extra exploration scene.

Combat root: CombatComposition, ArenaRoot with3+1 anchors each side, CameraRig, FighterViewRegistry, CombatHud UIDocument, EventPlayback, Audio/VFX pool. FighterView owns animator/model only. World HUD screen positions use camera projection with UI Toolkit coordinate conversion. Verify viewport/safe-area conversion with actual PanelSettings before implementing all HUDs.

Reuse portrait/model/animation mappings via `PresentationCatalog`. Keep skill art keys separate from executable effect IDs. LeanTween can animate scene transforms through one adapter; UI Toolkit transitions or a dedicated style tween adapter drive VisualElements. Existing RectTransform helpers are not assumed compatible with VisualElement. No dependency/package change is needed merely to document this boundary.

## 5. Minimum durable records

| Record | Required fields / invariant |
| --- | --- |
| CharacterDefinition | stableID, seriesID, traits, rarity, sourceID, stats, skill/passive refs, contentVersion |
| BannerRevision | ID/revision, seriesID, active interval, bucket rates/weights, prices160/1600, guaranteePolicyID/group, featuredIDs |
| SummonReceipt | uid, requestID, canonical payload hash, bannerRevision, count, debit, ordered outcomes, conversions, progressBefore/After, granted entitlements |
| GuaranteeProgress | uid+group, progress0..299, policyVersion; separate earned choice records |
| RunState | runID/uid, revision, seed/generatorVersion, contentHash, profileID, difficulty0..100, reward quote, node graph, currentNode, selectedPath, frozen roster, runHP/defeat per fighter, boons, status |
| NodeState | ID,row,type,outgoingIDs, teamSnapshot if combat, stateLocked/available/chosen/completed/bypassed |
| BattleSnapshot | matchID/runID/nodeID, revision, formations,HP/PG/statuses, hands, rule state, RNG counters, deadline schedule |
| RunSettlement | unique runID result, completion reward, first-clear entitlement, wallet receipt; retries cannot pay again |

Never accept client-ownedHP, difficulty-at-payout, gacha outcome or “I won.” Every command includes expected revision and idempotencyID. Node travel/Rest/result application use compare-and-swap transactions. Cross-service battle→run→wallet operations use durable result receipts/outbox and idempotent consumers; do not pretend a client animation makes several database writes atomic.

## 6. Server command sketches

```text
GET  /content/banners                  published banner revisions and disclosure
POST /summons                         requestID, bannerID, expectedRevision, count1|10
GET  /summons/receipts/{requestID}      recover unknown outcome
POST /guarantees/{entitlementID}/claim requestID, selectedCharacterID
POST /runs                            profileID, loadoutRevision, difficulty, requestID
POST /runs/{id}/choose-node            nodeID, expectedRevision, requestID
POST /runs/{id}/rest                   healAll|reviveOne, fighterID?, revision, requestID
POST /runs/{id}/choose-boon            offerID, boonID, revision, requestID
GET  /runs/{id}                        resume authorized projection
POST /battles/{id}/plans               turnRevision, orderedActions, requestID
GET  /battles/{id}/events              afterEventID, authorized side only
```

Summon transaction validates auth/banner revision/balance, chooses deterministic immutable receipt once using secure server entropy, and atomically debits/grants/progresses. Firestore transaction retries must not reroll externally observed results: precompute outcomes bound to request/entropy and immutable pool, then reuse on transaction retries while validating current economic state; reconcile concurrent ownership conversions at commit. Same requestID with different payload is rejected. If banner closes before a successful transaction, reject without debit; once committed reveal remains recoverable.

Difficulty is frozen at run creation; multiply basic stats once from original frozen enemy build. UI previews and server resolve read identical snapshot. RunHP applies battle result once; zero remains zero; Rest action consumes node once. Partial casualties lead to fewer active slots with living reserve auto-fill. One active run/account is a starting simplification; new run requires abandoning or completing the current one.

## 7. Implementation phases and gates

| Phase | Concrete work / deliverable | Required evidence before next phase |
| --- | --- | --- |
| A — Rules and content foundation (P0-A) | Contracts/Core projects, catalog schema, four kits Kyo/Chin/Kensou/King, source aliases/series metadata, deterministic arithmetic/RNG, command/event IDs | Damage-family vectors,58 sourceR3 preservation at authoring boundary, explicit four-kitAST completeness, Windows/server/Android core parity spike; no unresolved runtime operations |
| B — Local card battle (P0-B) | Symmetric formations, shared hand/PG, preview/reset/commit, turn/reaction/death state machine, LocalBattleSession, baseline legalAI | Seeded mirror battle completes; reset RNG isolation; full-hand ultimate; partial casualty/reserve; replay identical. Presentation may still be primitive |
| C — Combat screen (P0-B) | Combat scene, UXML/USS, camera/anchors, fighter/card views, event-driven playback, reconnect mock/error/result states | All B00–B21 screens; tap and mouse planning; skip leaves same state; no legacy manager writes new HP/PG; novice observer explains a turn |
| D — Route and persistent runHP (P0-C) | MainMenu entry/map/Rest, nine-row generator, difficulty snapshots, selectable enemy alternatives, HP carryover | Reachable boss+Rest path for fixed seed suite; skipped nodes locked; no auto-heal; Rest idempotent;0/100% scale exact; injured resume works |
| E — Eight kits and local economy (P0-C) | Add Mai/Shingo/Benimaru/Athena; KOF banner and rate disclosure, wallet/receipt fake, selector/duplicate/C0–C6 UI; profile restrictions | Rates4/36/60 and160/1600 exact;295+10 guarantee fixture for chosen policy; no unowned5-pull guarantee; source/runtime readiness clear; full local loop |
| F — Authoritative services (P0-D) | Host/shared core, Firebase third-party auth adapters, Cloudflare gateway, persistence/idempotency, private projections, resume; replace fakes via composition | Real authenticated onlinePvE Windows/APK; concurrent spend and replay tests; lost receipt recovery; no client result acceptance; private two-seat battle parity |
| G — Expert AI and delivery (P0-E) | Search/heuristics, actual roster matchup corpus, route/economy telemetry, platform polish | Forced-win/legal/fairness fixtures; paired-side benchmark; device performance; complete run→reward→summon→upgrade loop; owner feel review |

Phases are dependency order, not duration promises. PhaseA/B need no live hosting or production gacha decision. PhaseE needs a selected guaranteePolicyID before final acceptance; visual draft can retain the proposed milestone meanwhile. F requires approved host/provider settings/credentials through normal project configuration; no secret belongs in content or client code. Build/dependency changes are explicit implementation tasks, not already authorized edits hidden in this document.

### First implementation ticket: FA-001

Create pure `StatBlock`, `DamagePacket`, `DamagePolicy`, `DamageResult` and `DamageResolver`, plus explicit unit/keyword normalization and golden fixtures from the combat contract. Add stable definition IDs and a complete Kyo94 test definition (both3-rank skills, passive and7-tier ultimate) with generated-field provenance. Deliver a deterministic headless Normal/True/DOT/Additional/Destructive calculation log and unit fixtures, then import the core into an isolated Unity test scene without changing legacy gameplay scenes. Exit: all golden values match; immutable content cannot be mutated by battle state; one Windows/Android IL2CPP portability check recorded before broader implementation.

## 8. Legacy migration matrix

| Existing | New owner / migration |
| --- | --- |
| BattleManager/GameManager | Replace authority with BattleEngine/session; retire from new Combat scene; retain legacy scenes |
| Unit.ReceiveDamage and animation callbacks | BattleEngine calculates; FighterView displays; remove write path in new composition |
| TeamDataManager/IBattleDataProvider | LoadoutSnapshot/session adapter; preserve model spawning knowledge, no online authority assumed |
| CardDeckManager/RuntimeCard | CardRules+PlanDraft+HandPresenter; stable card IDs rather than object references |
| CharacterObject | Authoring/presentation only; immutable exported definition vs persistent owned state |
| GachaManager/GachaRate | Banner/Summon services; reuse reveal assets after converting outcomes to receipt-driven playback |
| InventoryObject/PlayerCharacterRoster | Profile projection and server inventory; do not save account state in ScriptableObject assets |
| BattleUIController/UnitHUD | UI Toolkit presenters bound to projections; migrate layout intentionally |

No giant rewrite of legacy scripts in place. One authority per new scene. Maintain a migration checklist of GUIDs/prefabs before scene changes; do not rename files casually. Run regression only on boundaries touched by that phase, then stop and move to its next gate.

## 9. Ready-to-build checklist

- Design references: economy-route, battle-screen-states, actual-source combat contract and external character drafts are linked from GDD.
- Chosen configuration includes source version and explicit proposal IDs; no runtime interpretation of English CSV.
- PhaseA can be implemented without waiting on all29 kits, production authentication or image assets.
- Review-sensitive choices remain visible: guarantee reset/carryover, Rest/revive amount, reward slope/base, True/Destructive interactions and SUB scope.
- Required tests are listed per phase. Documentation validation does not count as a Unity build or gameplay test.

## 10. Battle-session continuation — 2026-10-03

The requested Phase C battle presentation slice is implemented in the current `Assets/Project` layout. See [battle-session continuation](battle-session-presentation-task-card.md) for reference timing, ownership, validation, and remaining game-feel review.

Delivered: hidden HUD during CC comparison; ordered opening deal and interleaved rank merges; animated draft moves, card-to-slot travel, reset and auto-commit; explicit CardExecution presentation with ordered slot UI; turn and attacker/target cameras; damage/critical/block/shield/readiness combat text; placeholder attack/hurt/death; reserve deployment; seeded random retargeting after death; skip-to-authority; persistent result screen and existing route return. Enemy AI decisions remain unchanged.

Core validation passes 6,940 assertions. Automated Unity scene runs finish both initiative paths with zero gameplay runtime errors in the main repository. With the user's explicit approval, the unavailable `com.unity.modules.physicscore2d` entry was removed from the manifest and lock file, resolving the original launch blocker. Use **Fighting Allstar → Preview Battle → Player First / Enemy First** for a disposable local demonstration.

This advances the battle-session slice of Phase C. It does not mark the complete A–G roadmap, production character effects, reconnect/remote UI, device acceptance, or owner game-feel approval complete.
