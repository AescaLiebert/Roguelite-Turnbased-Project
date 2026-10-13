# Dungeon inspection and item rewards

Clicking any route platform opens a read-only inspector, including locked, completed and bypassed branches. It does not advance the run, pin a battle, bypass siblings, save a route choice, or consume randomness. The explicit Enter Stage action commits a battle. Choosing a Rest recovery or Boon commits that content node and applies its choice. Only reachable nodes can be entered; a committed encounter can be resumed.

The stage inspector shows its pinned enemy formation, active and SUB positions, constellation, primary and secondary stats, passive, every skill rank, the matching ultimate tier, available reward families, and elite/boss benefits. Information scrolls independently of the Enter button. Item information and inventory dialogs do not choose a route.

The account inventory now contains Diamonds, Coins, fourteen upgrade materials (nine Phase Crests from WIP-Phase to META and five Ascension Sigils from Launch to META), series Legacy Shards, series Relic Cores, Artifact Dust, and unique artifact instances. Series Challenge Boss is a separate Start→Boss mode and requires one specific series. Relic Cores drop only there and can fund Holy Relics or external systems; character upgrades use Coins, Phase Crests, Ascension Sigils and Legacy Shards. Artifact Dust funds the future artifact upgrade system. Diamonds remain in the existing Gacha wallet. No character-level balance curve, Holy Relic crafting, artifact equipment slots or random artifact stats are introduced by this material foundation.

Dungeon and stage previews show currencies and material families plus only the highest possible artifact tier, sorted by value and without drop quantities. Both currency frames are Gold; phase/sub-phase materials Blue; series materials Purple; Common/Uncommon/R/SR/SSR artifact frames Grey/Green/Blue/Purple/Gold. Click any sample for its icon, name, owned amount, drop rule and description. Granted reward tiles show actual quantities. Main Menu's Backpack provides Artifact, Material and Character Dupe grids without a preview pane; the header has separate Diamond and Coin channels.

Each victory creates a stable run/node receipt before account delivery. Wallet receipts prevent double payment after retries or a crash between the run save and the account save. Losing or abandoning preserves rewards already earned; unclaimed receipts stay saved for retry. There is no additional dungeon-clear reward. Legacy completion-bonus receipts are ignored on delivery; balances already earned remain intact.

## Starting reward values

These are initial tuning values, not final balance. The five UI levels map to internal legacy-compatible enemy bonuses 0/25/50/75/100 percent. Each level caps artifact rarity at Common/Uncommon/R/SR/SSR respectively. Currency is fixed per encounter type and level: Easy Battle gives 20 Diamonds / 600 Coins, increasing by 25% per level to 40 / 1,200 at Nightmare. Elite doubles and Boss quadruples those amounts.

| Guaranteed reward at Easy | Battle | Elite | Boss |
| --- | ---: | ---: | ---: |
| Diamonds | 20 | 40 | 80 |
| Coins | 600 | 1,200 | 2,400 |
| Selected Phase Crest | 1–2 | 2–4 | 4–8 |
| Selected Ascension Sigil | 1–2 | 2–4 | 4–8 |
| Artifact Dust | 1–2 | 2–4 | 4–8 |
| One enemy-series Legacy Shard | 1–2 | 2–4 | 4–8 |

Material quantities roll independently. A Battle rolls 1 through `level + 1` inclusive; Elite and Boss multiply by two and four. Nightmare Battle therefore guarantees 1–6 of each applicable material. One series is selected from the pinned enemy formation. Series Challenge Boss additionally guarantees its exclusive Relic Core in the same range.

Artifact instance drop chance is 25/37/50/62/75 percent across the five levels. Conditional rarity is uniformly sampled from Common through the level's cap (Nightmare: five equally likely tiers). These probabilities and ranges are prototype tuning choices. Rolls use a separate deterministic seed derived from run seed, node and reward purpose. Inspection and retries cannot reroll them, and route/combat RNG remains untouched.

Balance plan: compare ten regular runs with ten Series Challenge Boss encounters at Easy and Nightmare. Compare reward per win and per minute; every victory should advance upgrades and higher difficulty should improve artifact yield/rarity. If Coins gate every upgrade, raise Coin income or reduce costs once authored. If phase materials lose their farming purpose, adjust tier-specific recipes. If single-boss currency per minute dominates, tune its encounter multiplier or challenge while preserving exclusive Relic Cores. Validate again when upgrade costs and artifact stats are designed.

## Verification

Run `./Tests/CombatPresentation/Run.ps1 -DungeonRewardsOnly` for inspection immutability, explicit entry, victory/loss, reward replay, legacy save migration, independent material types, restricted resource use, artifact identity and difficulty behavior. Run **Fighting Allstar → Validate Dungeon Inspection and Rewards** in Unity for actual desktop, compact and portrait UI rendering. It uses an isolated preview inventory and writes screenshots and checks to `Artifacts/DungeonRewards`. Alternatively, `./Tests/CombatPresentation/RunDungeonRewardUiChecks.ps1` launches a separate hidden Unity batch fixture under `Temp/DungeonRewardUiProject`, with its own screenshots and verification file.

Play checks: inspect all branches repeatedly and open tooltips before entering; only the final Enter/content action should bypass siblings. Inspect a locked boss, complete an elite and select its Boon, recover at Rest, reload after a victory, lose/abandon after rewards, and clear the regular and Series Challenge modes. Confirm no duplicate rewards, unchanged enemy snapshots, correct series drops, accurate item quantities, and a visible Enter action at every supported screen size.

Current core verification: 3,312 focused assertions passed, including deterministic receipts, replay protection, all five rarity tiers, random guaranteed material ranges, fixed currency, and absence of clear payouts. The menu refactor renderer passed all nine Main Menu/Entry/Map checks at the three sizes. Its 39 captures and compilation/import log are in `Artifacts/MainMenuRefactor`. Economy values still require the gameplay balance pass above.
