# Character inventory / upgrade completion audit

The current runtime, UI, shared art references, 29 fighter assets, test runner and Core DLL match the compiled fixture byte-for-byte. See `VerifiedSources.sha256` (49 files). This audit uses the final `Verification.txt` and native Unity captures rather than earlier failed iterations.

| Requested requirement | Current evidence | Result |
| --- | --- | --- |
| Remove inventory preview; grid click enters inspector | CharacterInventory.uxml contains only filters and grid; all three sizes invoke the actual character card callback and assert inspector shown / inventory hidden. `CharacterInventory-*.png` | Complete |
| Two Character Kit panels | CharacterHub.uxml has one level/basic-stat panel and one kit icon panel. Tests verify four basic stats and three cards plus passive. `CharacterKit-*.png` | Complete |
| Level label, basic stats and Details with every stat | Owned level/cap and resolved CC/HP/ATK/DEF bind to the first panel. Details uses the full StatId enumeration; tests assert every stat row. `CharacterStats-*.png` | Complete |
| Standard card/passive icons; click opens detail popup | Uses the actual BattleCard template and same bronze/gold frames/type icons. Both skills, ultimate and passive callbacks are exercised. R3 and C5 previews, rich text, status badges and shared CombatDetailFormatter are verified. `CharacterCardTooltip`, `CharacterUltimateTooltip`, `CharacterPassiveTooltip` captures | Complete |
| Upgrade tab with leveling resource and standard limit-break item grid | Real Upgrade tab binds Coin tile and four cost tiles using ItemRewardView, quantities and tooltips. At the cap, Limit Break is first and its action stays within the viewport. `CharacterUpgrade-*.png`, `CharacterLevelUp-*.png` | Complete |
| Every level costs increasing Coins | Tests verify strictly increasing cost from current level 1 through 89 and exact coin deduction for every committed level. Core starting formula documented in CharacterProgression.md | Complete |
| Limit breaks at multiples of ten using matching phase/sub-phase/series materials plus Coins | All 45 phase/sub-phase identities and all 29 fighter identities validated. Every cap unlock 10→20 through 80→90 checks exact deductions and refuses ordinary leveling before the break. Each missing resource refuses without mutation | Complete |
| Iori95 requires WIP-Phase / META / KOF | Authored ID 1406, series.kof. Runtime costs and captures show WIP-Phase Prototype Crest, META Apex Sigil, KOF Tournament Insignia and Coins | Complete |
| Limit break raises capacity by ten without increasing stats | Every break asserts unchanged level and full resolved StatBlock, and cap +10. Account reload confirms persisted cap | Complete |
| Maximum level 90 | All levels exercised; both service actions reject at 90 without changing state. Both UI buttons disabled. `CharacterMaxLevel-*.png` | Complete |
| Level only CC / ATK / DEF / HP; different authored growth per fighter | All 29 serialized LevelGrowth profiles are present and distinct. Tests resolve every level for every fighter, check the four growing stats and exact equality of every secondary stat, with immutable base content | Complete |
| Progression affects gameplay | Formation manager uses PlayerInventoryService.CreateRunFighterSeed with owned-level stats; practice snapshots use the same growth resolver. Actual adapter outputs are compared with upgraded stats, including starting HP | Complete |
| Material names, descriptions and appearance | ItemCatalog supplies phase/sub-phase/series names, usage/farming descriptions, tint and appearance descriptions. ItemRewardView draws faceted crests, radial sigils and monogrammed series medallions. Definitions and all family identities checked; final material captures visually reviewed | Complete |
| Save integrity and clean navigation | Exact deductions, unknown ownership, early break, shortages, failed-write rollback of memory and unchanged disk, split material stacks, repeated disk reload, Back/Escape and filter retention verified. Actual main editor recompiled and reported zero console errors | Complete |

## Final verification

- Core build: zero warnings / errors.
- Unity 6000.3.4f1 isolated native UI + persisted transaction verification: **55,594 assertions passed**.
- Thirty native captures: ten states at 1920×1080, 1280×720 and 768×1024.
- Shared UXML/USS reference audit: 62 references, zero missing paths or mismatched GUIDs.
- Main editor after final refresh: not compiling; zero error console entries.
- The forced disk-write failure in UnityVerification.log is expected and proves rollback; it targets a unique disposable fixture account. This account is removed by the runner. No user account or scene was saved by the fixture.

Economy prices and growth profiles are explicitly labeled starting values with a tuning/playtest plan in CharacterProgression.md. Human economy/readability playtesting remains a tuning activity; the requested UI and progression mechanics are implemented and verified.
