---
slug: fighting-allstar-context
status: needs-human
source: manual
gdd_tags: [identity, tech-stack, prototype-truth, guardrails]
owner: orchestrator-agent
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Fighting Allstar — Project Context Brief

Bootstrap input captured from the owner's 2026-10-01 request and source inspection. User requirements are confirmed intent; recommendations remain draft until reviewed. GDD_Fighting_Allstar.md becomes the design reference after bootstrap.

## 1. Identity
| Field | Direction |
| --- | --- |
| Title | Fighting Allstar |
| Genre | 3D tactical card turn based RPG / roguelite dungeon / character and team driven |
| Platforms | Mobile and PC; prototype deliverables Windows PC and Android APK |
| Play | Server authoritative online; interchangeable local AI testing backend |
| Fantasy | Assemble synergistic fighter teams and adapt them to dungeon restrictions and opposing strategies |
| Priorities | Team synergy; multiple interacting meta triangles; 3 active + 1 reserve on each side |
| Presentation | Proposed readable 3D arena with fixed formations and a tactical card HUD |

## 2. Tech Stack
| Area | Current inspection / requested target |
| --- | --- |
| Engine | Unity 6000.3.4f1 |
| Rendering | Built-in; no custom render pipeline assigned |
| Input | Input System 1.17.0; Both input handlers configured |
| UI | Existing uGUI/TMP; UI Toolkit requested, uielements module present |
| Tween | LeanTween source under Assets/LeanTween |
| Services | Firebase third-party authentication and Cloudflare requested; battle server hosting to be proposed |
| UnityMCP | Requested development tooling; active integration unverified |

## 3. Scenes
Enabled: `Assets/Scenes/Scene-CharacterLoadOut.unity`, `Scene-CharacterProfile.unity`, `Scene-Gacha.unity`, `Scene-BattleGame.unity`, `Scene-Test.unity`.
Requested future roles: `MainMenu` and `Combat`; these are not current scene files.

## 4. Core Systems — File Map
| System | Path | State |
| --- | --- | --- |
| Battle setup | `Assets/Script/Gameplay/BattleManager.cs` | Player card initialization and CC introduction |
| Legacy turns | `Assets/Script/Gameplay/GameManager.cs` | Individual turns; random enemy skill |
| Team transfer | `Assets/Script/Gameplay/TeamDataManager.cs` | Singleton, local SO transfer, spawning and reserve logic |
| Backend seam | `Assets/Script/Gameplay/Interfaces/IBattleDataProvider.cs` | Team-data retrieval only |
| Character data | `Assets/Script/Character/CharacterObject.cs` | SO mixing stats, visuals and progression |
| Cards | `Assets/Script/Card/CardDeckManager.cs` | Hand/action queue and reorder; merge/reset not found |
| Battle state | `Assets/Script/Gameplay/Unit.cs` | HP/PG/damage/death mixed with presentation |
| Ownership | `Assets/Script/Inventory/PlayerCharacterRoster.cs` | Empty file |
| Gacha | `Assets/Script/Gacha/GachaManager.cs` | Local Unity Random rolls |

## 5. Architecture Reality
Scene components combine rules, Unity references and presentation. Current team retrieval does not establish server authority. No Firebase/Cloudflare implementation or dedicated project test suite was found in inspected scripts/manifest. Source inspection does not verify a running build.

## 6. Design Boundaries
- Start with Kyo94; dungeon victories award Diamonds for character gacha.
- Losing preserves owned characters and permanent inventory.
- Dungeon eligibility restrictions give wider rosters value.
- Characters start at C0/6 and have six constellation upgrades through C6/6; upgrades affect the ultimate and related kit behavior.
- Dungeons consist of linear stages with randomized opposing character teams drawn from the character catalog, using the same character combat rules.
- Card movement/merging, PG, ultimates, statuses, critical/block, additional damage, lifesteal, counters, follow-ups and DOT are requested mechanics.
- All fighters, including reserve, register passives; activation conditions must be explicit.
- Proposed onboarding, exact rules, content quantities and economy are reviewable design choices.
- Target separates content, ownership and match state. Server validates online outcomes; animation presents them.

## 7. Commit Scopes
Proposed: `combat`, `cards`, `characters`, `ai`, `dungeon`, `inventory`, `economy`, `network`, `ui`, `audio`, `scene`, `build`, `docs`.

## Latest detailed-design revision

Series banners4%featuredSSR/36%SR/60%R,160/1600 Diamonds,300-pull featured guarantee. Reset/selector behavior remains proposed. Forward selectable route map replaces fixed encounters; exact runHP persists; Rest heals/revives, no automatic recovery. Entry difficulty0..100% doubles enemy basic stats at maximum. See economy-route, battle-screen-states and Unity implementation plan in3_Outputs/Specs.
