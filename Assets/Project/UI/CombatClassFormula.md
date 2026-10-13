# Stat-derived Combat Class

## Starting values

CC retains each fighter’s authored level-one value. The increment now comes from stat gains instead of CombatClassBp.

| Gain | Weight |
|---|---:|
| One HP point | 0.2 |
| One ATK point | 1.0 |
| One DEF point | 1.5 |
| One percentage point of any secondary stat | 100 |

These are proposed starting weights, not a claim about Seven Deadly Sins’ exact formula. Percentage points use the game’s basis points: 100 BP is one percentage point. Neutral secondary baselines cancel when calculating gains.

Let W(stats) = 0.2 HP + ATK + 1.5 DEF + 100 × sum(secondary percentage points).

For each fighter, calculate their unequipped level-90 basic stats using the authored HP/ATK/DEF growth profile. Then:

CC = baseCC + round[9 × baseCC × (W(current) − W(base)) / (W(level90) − W(base))] + explicit CC artifact bonuses.

Integer score units and decimal arithmetic make rounding deterministic; results clamp to 0…Int32.MaxValue. With positive stat growth, naked level-one CC remains unchanged and naked level-90 CC is exactly 10× base. Gear can take CC above that endpoint. Fighters with no stat growth gain no CC from levels; extra stats still contribute using the raw starting weights. Legacy direct-CC artifact rolls remain valid without making CC count itself in W.

## Integration

Character sheets, next-level previews, practice fighters and new dungeon formation snapshots use CharacterProgression.StatsAtLevel plus ArtifactRules.ApplyStats. Artifact flat/percentage/secondary rolls and active permanent set bonuses update CC. Enemy difficulty changes and dungeon stat rewards also recalculate from their stat gains. Temporary in-battle statuses do not rewrite the recorded encounter power. Existing frozen run snapshots without calibration metadata keep their recorded CC; newly formed fighters carry flat calibration fields through cloning and serialization.

CombatClassBp survives only as an ignored legacy JSON property; it is no longer a Unity-serialized growth control. The permanent character stats and their growth rates remain individually authored.

## Evidence

`Artifacts/UnifiedItemAndCC/CCReview.csv` reviews all 29 current authored fighters across levels 1–90. It covers monotonic progression, the 10.000 endpoint ratio, stat-weight ordering, gear contribution, dungeon boost contribution, serialized calibration, zero-growth behavior and saturation. The runner is isolated from the player account.

## Tuning review

Starting acceptance target: all un-geared authored level-90 fighters remain near 10× base, and an equal raw stat-point gain follows HP < ATK < DEF; a secondary percentage-point gain has the highest weight. If percent-stat gear overwhelms build choice, lower the secondary weight; if HP dominates equivalent flat additions, lower HP weight. Keep the endpoint calibration when tuning so progression pacing stays readable. Playtesting for desirable build balance is still required.

Clarity: CC follows visible stats. Response: gear and stat rewards update immediately. Motivation: defensive and secondary-stat builds contribute to power. Fit: the existing four-stat sheet shows the derived value. Satisfaction: upgrade comparisons show the actual calculated gain.
