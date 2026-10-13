# Neutral statuses

The status HUD sorts Grey before Normal. Within either color, Buff and Neutral statuses share the same floor in application order, followed by Debuffs on their own floor. Application order is stable within each group, including stance parents followed by their children. Empty groups occupy no space, and departing icons immediately leave the grid while their exit animation finishes.

Stance and healing statuses are Neutral. Older recipes that serialize these behaviors as Buff migrate when cloned or applied. Explicit Neutral polarity survives catalog loading even when the recipe ID contains `buff`, `debuff`, or `increase`. Stance children always produce Neutral recipes. Ordinary stat buffs, debuffs, and Grey status rules keep their existing behavior.

Neutral statuses do not trigger Rupture, add Amplify damage, satisfy Buff/Debuff counts, or get removed by Buff removal or Debuff cleansing. They retain their mechanical effects: healing, immunity, evade, taunt and counters. Removing or expiring the parent stance still removes its children; duration clocks and activation-turn protection are unchanged.

This change prioritizes clarity: icon position and attack results must agree on classification. Existing application, expiry, healing and counter feedback remains in use. No damage or timing values are tuned.

Run the focused combat checks with `./Tests/CombatPresentation/Run.ps1 -NeutralStatusOnly`. Run `./Tests/CombatPresentation/RunNeutralStatusHudChecks.ps1` for the isolated Unity Play Mode fixture; its result is saved under `Temp/NeutralStatusHudProject/Evidence/neutral-status-hud.txt`.

Manual checks in battle:

- Apply a Buff, a stance with children, healing, and a Debuff in different orders. Verify Grey first, Buff and Neutral together in application order, and Debuff after them in each color group.
- Use Rupture against an actor with only Neutral statuses, then add a real Buff. Only the real Buff should enable Rupture. Repeat with Amplify on the attacker.
- Refresh and expire a stance repeatedly. Children stay with the Neutral group, disappear with their parent, and leave no empty grid cells.
- Use Buff removal and Debuff cleansing. Neutral statuses survive both, while Cancel Stance removes the stance and all its children.
