# Artifact Upgrade System Brief

## Purpose and scope

This brief defines how an individual artifact advances from **+0 to +20** and how it can be protected from being consumed as upgrade material. It builds on the [Artifact System Design Brief](ArtifactSystemDesignBrief.md): three five-piece sets, rarity-based substat compacts, and main/substat identities defined there. “Ascend” is the current working term for the artifact’s +level.

## Upgrade levels and stat growth

- A new artifact starts at **+0**.
- The maximum upgrade level is **+20**.
- Each successful level increase raises the artifact’s **main stat**. It always improves, regardless of whether the upgrade level is also a substat milestone.
- At positive levels divisible by six—**+6, +12, and +18**—one randomly selected existing substat line receives an increase.
- The milestone increases an existing line; it does not add another line. The artifact’s substat count remains set by its rarity: Common 0, Uncommon 1, Rare 2, SR 3, SSR 4.
- The substat increase is a separate quality roll for that line, subject to the line’s allowed value range. Main-stat growth has its own level-based minimum/maximum values and no quality roll.
- No substat milestone occurs at +20. +20 is the cap.

For Common artifacts, which have no substat lines, the +6 / +12 / +18 substat event has no eligible line and is skipped; the main stat still increases at each level. Whether Common items should have any substitute milestone reward is not defined here.

## Upgrade materials

Two material sources can contribute to artifact upgrades:

1. **Artifact Dust**, a dedicated upgrade material.
2. **Another artifact**, used as upgrade fodder.

When used as fodder, the source artifact is consumed. A favorite artifact is never eligible as fodder. The player may still equip a favorite or upgrade it using valid materials; favorite protects it only from being consumed to upgrade another artifact.

The Dust and fodder costs per level, upgrade points granted by each artifact, rarity multipliers, overflow handling, and whether both sources can be combined in a single upgrade action remain open economy decisions. The UI should show the target artifact, current and next level, expected main-stat increase, any substat milestone at that level, and the exact materials consumed before confirmation.

## Favourite lock

Artifacts can be marked **Favourite**. Favourite is a persistent protection state on that artifact instance. A favorite artifact:

- cannot be selected or consumed as upgrade fodder;
- remains eligible for equipment and target upgrades;
- can be unfavourited by its owner before it becomes fodder.

Show a clear favorite marker in inventory and artifact detail views. When selecting fodder, exclude favorites by default and enforce the lock if one is selected through another path. If the player tries to consume a non-favorite artifact, identify the exact source item and confirm its consumption.

## Stat identity and roll rules

The main-stat family, rarity-based substat count, no-duplicate-substat rule, and stat pool are defined in the parent artifact brief. Main-stat upgrades apply at every level. Only the +6 / +12 / +18 milestones increase a substat line. **Combat Class and Reflect Damage are Special Stats**: they are valid substat lines, but not main stats. A single compact cannot contain duplicate identities; flat DEF and DEF% remain distinct identities and may both appear.

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

## Out of scope / tuning still needed

- Dust and fodder costs, fodder conversion, upgrade success guarantees, and excess-material handling.
- Main-stat min/max growth by stat, slot, rarity, and artifact level.
- Substat quality ranges and the size of a milestone increase by stat, rarity, and artifact level.
- Random selection weights when an artifact has multiple substat lines.
- Any replacement reward for Common artifacts at +6, +12, or +18.

This is a design brief only. It does not change item definitions, inventory storage, UI, or upgrade behavior in code.
