# Fighter and dungeon UI composition

## Implemented

- Shared `BasicStatPreview` renders CC, ATK, DEF and HP in Character Kit, Upgrade, and enemy inspection. Level-up comparisons add next values to those same rows.
- Upgrade uses a fitted vertical flex container with the stat preview at the top and material/coin/action controls at the bottom.
- Required material boxes sort by frame quality. Owned/required quantities occupy the shared centered white item footer.
- AccountHeader Back returns from fighter details to the character inventory and restores the header’s earlier action when the hub closes.
- Generic item quantities and artifact levels share `ItemRewardView.AddQuantityFooter`, including inventory and granted rewards.
- Dungeon previews show one Flower for each eligible set, without zero-level footers. Seven boxes remain in a fitted, non-wrapping row; earned loot keeps its horizontal ScrollView.
- Stage Detail has larger enemy cards, team CC, compact stage status and reward icons. Selecting an enemy opens an absolute popup for snapshot stats, passive, skills and ultimate. Inline Enemy Info and Last Victory Rewards are removed.

## Native landscape review

20 UI Toolkit captures at 1920×1080 and 1280×720 cover CharacterKit, LevelUp, LimitBreak, MaxLevel, DungeonEntry, StageDetail, StageNightmare, EnemyPopup, GrantedRewards and Backpack. The final compact review confirms that kit cards and stage reward boxes fit inside the viewport. The Nightmare capture is a maximum-rarity preview stress sample with detached encounter data, not a gameplay difficulty simulation.

The capture uses an isolated Unity project with an in-memory preview inventory. It never upgrades the live account, chooses a live route or grants player rewards. No automated gameplay tests were run. Core Release build: zero warnings/errors. Isolated Unity compilation completed without C# errors; existing project deprecation and serialization-depth warnings remain. Live-editor console verification was unavailable.
