# Artifact Upgrade System Brief

## Purpose and scope

This brief defines how an individual artifact advances from **+0 to +20** and how it can be protected from being consumed as upgrade material. It builds on the [Artifact System Design Brief](ArtifactSystemDesignBrief.md): three five-piece sets, rarity-based substat compacts, and main/substat identities defined there. “Ascend” is the current working term for the artifact’s +level.

## Upgrade levels and stat growth

- A new artifact starts at **+0**.
- The maximum upgrade level is **+20**.
- Each successful level increase raises the artifact’s **main stat**. It always improves, regardless of whether the upgrade level is also a substat milestone.
- At positive levels divisible by six—**+6, +12, and +18**—one randomly selected existing substat line receives an increase.
- The milestone increases an existing line; it does not add another line. The artifact’s substat count remains set by its rarity: Common 0, Uncommon 1, Rare 2, SR 3, SSR 4.
- The substat increase uses its authored base multiplied by an independent **0.7–1.0** quality roll and **rarity / 5**, flooring once. Main-stat growth uses the +0/+20 endpoints in the [Artifact Balance Table](ArtifactBalanceTable.md), with no quality or rarity multiplier.
- No substat milestone occurs at +20. +20 is the cap.

For Common artifacts, which have no substat lines, the +6 / +12 / +18 substat event has no eligible line and is skipped; the main stat still increases at each level.

## Upgrade materials

Two material sources can contribute to artifact upgrades:

1. **Artifact Dust**, a dedicated upgrade material.
2. **Another artifact**, used as upgrade fodder.

When used as fodder, the source artifact is consumed. A favorite artifact is never eligible as fodder. The player may still equip a favorite or upgrade it using valid materials; favorite protects it only from being consumed to upgrade another artifact.

Both material sources can be combined in one transaction. Existing starting economy values are listed in [ArtifactImplementation.md](ArtifactImplementation.md): Dust gives 100 EXP, Coins cost 2 per usable EXP, upgraded fodder returns 80% of invested EXP plus its base value, and cap overflow is disclosed and discarded. The UI shows the target artifact, current and draft level, expected stat increases, and exact materials consumed before confirmation.

## Favourite lock

Artifacts can be marked **Favourite**. Favourite is a persistent protection state on that artifact instance. A favorite artifact:

- cannot be selected or consumed as upgrade fodder;
- remains eligible for equipment and target upgrades;
- can be unfavourited by its owner before it becomes fodder.

Show a clear favorite marker in inventory and artifact detail views. When selecting fodder, exclude favorites by default and enforce the lock if one is selected through another path. If the player tries to consume a non-favorite artifact, identify the exact source item and confirm its consumption.

## Stat identity and roll rules

The main-stat eligibility, all 19 substat bases, and quality/rarity formula are defined in the [Artifact Balance Table](ArtifactBalanceTable.md). Main-stat upgrades apply at every level. Only the +6 / +12 / +18 milestones increase a substat line. Combat Class and Reflect Damage are excluded from the artifact pool. A compact cannot repeat its exact main identity or another substat identity; flat DEF and DEF% remain distinct identities. Random line selection and quality are stable for each artifact/milestone, so changing draft materials cannot reroll an outcome. Confirmation revalidates the complete reviewed result before consuming resources.

## Example progression

| Level reached | Main stat | Substat event |
|---:|---|---|
| +0 | Initial main-stat roll | Initial rarity-defined substat compact is present |
| +1 to +5 | Increases every level | None |
| +6 | Increases | One random existing substat line increases, if any line exists |
| +7 to +11 | Increases every level | None |
| +12 | Increases | One random existing substat line increases, if any line exists |
| +13 to +17 | Increases every level | None |
| +18 | Increases | One random existing substat line increases, if any line exists |
| +19 to +20 | Increases every level | None; +20 is the cap |

## Current implementation

Enhancement succeeds when the review remains valid and its costs can be saved. Eligible substat lines are selected uniformly at each milestone; a line may be selected more than once. The [Artifact Balance Table](ArtifactBalanceTable.md) records the authored values and saved-artifact migration. [ArtifactImplementation.md](ArtifactImplementation.md) describes the remaining starting economy values and UI behavior.
