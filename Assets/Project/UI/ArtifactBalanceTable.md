# Artifact main and substat table

This is the authored artifact balance table used by `ArtifactRules`. “Leather” in the supplied slot list is interpreted as the existing **Feather** slot; Sand and Circlet use the existing **Sand Clock** and **Crown** slots. The minimum main value is the value at **+0** and the maximum is the value at **+20**, with linear growth between those levels. Main stats have no quality or rarity multiplier.

## Main stats: Flower and Feather

Every main stat is a percentage. Both slots draw from these three identities only.

| Main stat | +0 | +20 |
|---|---:|---:|
| HP% | 7.0% | 46.6% |
| DEF% | 8.7% | 58.3% |
| ATK% | 7.0% | 46.6% |

## Main stats: Sand Clock, Goblet and Crown

Each of these slots draws from the same full pool below.

| Main stat | +0 | +20 | Combat stat |
|---|---:|---:|---|
| HP% | 7.0% | 46.6% | Max Health percentage |
| DEF% | 8.7% | 58.3% | Defense percentage |
| ATK% | 7.0% | 46.6% | Attack percentage |
| Pierce | 8.7% | 58.3% | Pierce |
| Resistance | 4.7% | 31.1% | Resistance |
| Regenerate | 4.7% | 31.1% | Regeneration |
| Crit Rate | 4.7% | 31.1% | Critical Chance |
| Crit Damage | 9.3% | 62.2% | Critical Damage |
| Crit Resistance | 4.7% | 31.1% | Critical Resistance |
| Crit Defense | 9.3% | 62.2% | Critical Defense |
| Recovery Rate | 4.7% | 31.1% | Recovery |
| Lifesteal | 4.7% | 31.1% | Life Steal |
| Block Chance | 4.7% | 31.1% | Block Chance |
| Block Power | 9.3% | 62.2% | Block Power |
| DOT Dealt | 7.0% | 46.6% | Damage Over Time Dealt |
| Damage Dealt | 7.0% | 46.6% | Global Damage Dealt |

## Shared substat pool: all five slots

The listed base is the maximum value of **one** SSR quality roll. Initial lines and each earned milestone use this same table. Resistance is absent from the substat pool. Additional Damage Dealt is a substat only.

| Substat | Base value per roll | Stored base |
|---|---:|---:|
| HP flat | 298 | 298 |
| ATK flat | 20 | 20 |
| DEF flat | 23 | 23 |
| HP% | 5.83% | 583 bp |
| ATK% | 5.83% | 583 bp |
| DEF% | 7.29% | 729 bp |
| Pierce | 5.12% | 512 bp |
| Regenerate | 3.89% | 389 bp |
| Crit Rate | 3.89% | 389 bp |
| Crit Damage | 7.77% | 777 bp |
| Crit Resistance | 3.89% | 389 bp |
| Crit Defense | 7.77% | 777 bp |
| Recovery Rate | 3.89% | 389 bp |
| Lifesteal | 3.89% | 389 bp |
| Block Chance | 3.89% | 389 bp |
| Block Power | 7.77% | 777 bp |
| DOT Dealt | 5.12% | 512 bp |
| Damage Dealt | 5.12% | 512 bp |
| Additional Damage Dealt | 5.12% | 512 bp |

One percent is stored as 100 basis points. Basic percentage lines add a percentage of the unequipped basic stat. Secondary rates add percentage points to the corresponding combat rate.

## Quality and rarity

Each initial line and milestone gain rolls quality independently from **0.7 through 1.0**, inclusive. Quality uses a 0.0001 step so the stored calculation remains deterministic. Multiply the authored base, the quality roll and the rarity multiplier, then floor once to the integer storage unit:

`substat roll = floor(base × quality × rarity multiplier)`

| Rarity | Stars | Rarity multiplier | Initial substat count |
|---|---:|---:|---:|
| Common | 1 | 0.2 | 0 |
| Uncommon | 2 | 0.4 | 1 |
| Rare | 3 | 0.6 | 2 |
| SR | 4 | 0.8 | 3 |
| SSR | 5 | 1.0 | 4 |

For example, one SSR ATK% roll is `floor(583 × quality)`, producing 408–583 bp (4.08%–5.83%). One Uncommon flat ATK roll is `floor(20 × quality × 0.4)`, producing 5–8 ATK. Rarity scaling applies to substat rolls only; the listed main-stat endpoints are the same across rarities.

## Identity and enhancement rules

- An artifact draws one allowed main stat. Flat main stats are absent from this table.
- A substat cannot repeat the exact main identity or another substat identity. Flat ATK and ATK% are distinct identities.
- At +6, +12 and +18, randomly choose one existing substat and add one independently rolled gain. The same line can improve more than once. Common artifacts have no substats, so they skip these events.
- +20 is the cap and has no additional substat milestone.
- Each artifact seed fixes its quality rolls and milestone choices. Drafting, changing materials, canceling, reopening, and splitting an enhancement preserve the same outcome for each earned milestone.
- The reviewed values and costs are revalidated before materials are spent. Persisting a failed upgrade restores the previous inventory.

DOT Dealt affects Damage Over Time and the existing DOT-to-elemental-reaction bonus bridge. Additional Damage affects only the Additional family. Global Damage Dealt applies once through the correct branch for each damage family. These are separate stats; DOT and Additional are never treated as interchangeable names.

## Existing saved artifacts

Schema 2 updates saved artifacts to this table once. The migration retains instance/source identity, set, slot, seed, level, EXP, equipped owner and Favourite protection. Existing flat basic mains convert to their corresponding basic percentage track. Eligible main and substat identities remain; removed identities (including Combat Class, Reflect Damage, and Resistance substats), duplicates and exact main/substat overlaps are deterministically replaced from the allowed pool. Substat values are rebuilt using the persisted seed, authored quality and rarity formula, and every earned +6/+12/+18 gain. Normalizing a current-schema artifact preserves its valid rolls.

Combat Class and Reflect Damage still exist for other game systems; they are excluded from the artifact roll table. Existing active dungeon snapshots remain frozen; new account equipment snapshots use the updated table.

## Verification

Run `dotnet run --project Tests/ArtifactEnhancement/ArtifactEnhancementChecks.csproj -c Release` from the project root. The checks cover all main endpoints/slots/rarities, all 19 substat bases, quality limits and integer rounding, generated pool coverage and exclusion, migration, actual stat application, independent DOT/Additional/global damage effects, committed roll persistence, review validation, and resource protection.
