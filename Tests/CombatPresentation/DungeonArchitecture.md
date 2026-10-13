# Dungeon generation and run state

`RunState.Generation` owns the dungeon seed, generator version, route and pinned enemy formations. It contains no account inventory, player formation or battle random stream. Reward receipts and stat blessings belong to progression; their rolls may use the dungeon seed. Player fighter history and the four selected inventory ids are saved separately in the run. Old flat Seed/Nodes/GeneratorVersion JSON remains readable, and existing routes are never regenerated during migration.

Production battles create fresh, independent combat and card-draw streams through `BattleEntropy`. Crit, block, evade, target selection and status chance use battle-local randomness. A battle snapshot can still replay its own decisions for validation. Explicit test/training entropy remains available.

Each fighter's first deployment records its resolved stats and HP in run history. Benching/replacing fighters does not erase that history. A defeated fighter has zero HP, is removed from every formation position, cannot be assigned, and appears last in the collection with a black portrait overlay and cross. Defeat is permanent within this run. Rest heals living fighters by the existing 40% of Max HP; it cannot revive.

A lost/drawn encounter keeps its selected stage and the run active, even after a full wipe. Retry creates a new battle identity and opens team preparation. Players may replace the team with eligible owned characters or explicitly abandon. A victory advances a normal stage, keeps an Elite selected until its stat reward is claimed, or completes a Boss run. Each victory generates one idempotent reward receipt. The grant popup displays actual quantities after returning from battle; acknowledgment persists independently of delivery, so restart retries delivery and presents unseen receipts without paying twice.

Boon/Elite rewards increase one random primary stat (ATK, DEF or Max HP) by a random percentage for living fighters and later recruits. Claiming is atomic and cannot stack through request replay. Max HP increases also add that HP amount to living fighters. Defeated fighters remain unchanged. **Starting value: 3–8%.** Play ten matched routes at each difficulty: compare a blessing branch to the battle branch. Pass if the blessing is understandable and useful without consistently deciding the following encounter alone. Lower the upper bound if blessings dominate; raise the lower bound if players consistently avoid them. This range is provisional balance, not a benchmark.

## State transitions and feedback

| State/action | Entry | Exit and edge cases |
| --- | --- | --- |
| Prepare team | A selected encounter | Unique owned eligible living fighters, at least one active; no currency cost. Assignment cannot revive, reset wounded HP or modify the account formation. |
| Battle | Prepared encounter | Victory or loss/draw result; each attempt has a unique identity. Stale receipts cannot overwrite retries. Benched fighters cannot be changed by a battle result. |
| Defeated fighter | Battle result HP is zero | Unavailable until completion/abandonment; permanent inventory ownership is retained. Rest and swapping cannot cancel defeat. |
| Rest | Reachable/selected Rest stage | Heal living fighters once, or Continue if none are injured. Full HP, empty history and all-defeated history do not trap progression. |
| Stat blessing | Reachable/selected Boon, or victorious Elite | Claim one saved roll then unlock successors; no choice reroll or repeated claim. |
| Reward popup | Unacknowledged victory receipt | Continue acknowledges after a successful save; closing/restarting before acknowledgment shows it again. Delivery failures retain receipts. |

Clarity: previews show recovery and the exact rolled stat amount. Motivation: HP, availability, boosts and rewards persist. Response: inspection is read-only; explicit actions commit choices. Satisfaction: victory popup, rarity-framed grants and panel motion complement existing battle victory audio. Fit: previews reuse the condensed fonts, dark panels, gold accents and existing healing/diamond artwork. Defeat uses portrait/cross plus a textual reason, not color alone.

## Verification

`Tests/CombatPresentation/Run.ps1 -DungeonArchitectureOnly` checks generation independence, both battle streams, deterministic explicit replay, roster swapping, bench HP, permanent defeat, automatic unequip, replacements, battle identities, reward idempotency, Rest, boon persistence and legacy JSON.

`Tests/CombatPresentation/RunDungeonRewardUiChecks.ps1` runs an isolated Unity renderer and checks Entry/Map/Loadout at 1920×1080, 1280×720 and 768×1024. Additional captures cover Rest, Boon, the victory grant popup and defeated-character collection. It does not save real profiles or scenes.

Verified on 2026-10-10: 3,386 focused architecture/reward/policy assertions passed; all nine Unity layout checks passed. Core build completed with no errors or warnings. Rendered previews and the Unity verification report are retained in `Artifacts/DungeonArchitecture`.

Play-mode scenarios:

1. New player/readability: inspect Rest/Boon before entry. Predict recovery/boost from the card, then verify the resulting stats. An observer should identify unavailable characters from the cross and label.
2. Skill: start the same route twice; enemy formations/rewards match while opening draws and combat chance outcomes can vary. Compare decisions with the independent draws.
3. Stress: repeatedly click enter, claim and continue; navigate away/restart with a pending popup. No double reward, boon stacking or branch skipping.
4. Abuse: wound and bench a character, swap in another, then bring the wounded character back. Repeat after defeat. HP must persist and defeated fighters must remain blocked, including at Rest.
5. Boundary: lose with everyone defeated, reopen/restart the run, bring a new eligible fighter or abandon. No automatic exit. Lose an Elite/Boss and retry; no boon or victory reward before winning. Win with one casualty and verify it is unequipped.

Full combat-suite limitation: the current broader suite stops at `MultiHitChecks.cs` with “First-hit status ran late.” That test resolves direct BattleEngine fixtures and does not use the dungeon architecture changed here.

The checked-in generated `Assembly-CSharp.csproj` also points to stale Unity/package paths and cannot build directly with dotnet. Unity's isolated fixture compiles the actual runtime sources against the installed Editor instead.
