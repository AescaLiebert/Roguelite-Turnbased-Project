---
slug: status-container-card-effect
status: ready-review
gdd_tags: [mechanics, architecture, cards, statuses]
owner: codex
human_checkpoint: requested-by-owner
blocked_by: []
---

# Status Container and Card Effect implementation plan

## Goal

Build the reusable combat-effect foundation before character passives are authored. The system must represent the supplied Attack Effect, Debuff Effect, and future Buff Effect databases without parsing prose during battle. The current eight runtime character kits remain on their existing data and are not migrated by this slice.

## Source findings

The supplied Google Sheet contains:

- 28 attack keywords. They affect different stages: local calculation views (Charge, Shatter, Breakthrough), card multipliers (Rupture, Blaze, Weakpoint), gauges and removal, and separate Additional/DOT packets (Absorb Energy, DOT Burst, DOT Explosion, Abyss).
- 72 debuff records. A status can combine behaviors: periodic damage plus stat changes, a disable plus a break-on-damage rule, or a stat modifier plus source healing.
- Card-category disables for Attack, Debuff, Buff, Recovery, Stance, Ultimate, rank 2/3, and card effects.
- A Buff tab that is currently WIP. Buffs therefore use the same recipe schema with positive polarity instead of a second runtime implementation.

## Decisions

1. **Recipe versus instance.** A recipe is immutable catalog data. A runtime status instance stores source fighter, target, application order, root action, strength, duration, stack count, and snapshots.
2. **Identity.** `RecipeId` is the normal stacking key. Recipes may opt into `RecipeAndSource` when each applier must own a separate copy.
3. **Default stacking.** The default is `RefreshStronger`: keep the stronger payload, keep its ownership/snapshot, and refresh to the longer remaining duration. Equal strength keeps the older instance for deterministic ordering.
4. **Explicit stack policies.** Snapshot DOTs may use `IndependentStacks`; capped counters use `AddStacks`; exceptional effects may use `ReplaceAlways` or `RefreshDuration`.
5. **Duration clocks.** Duration declares the clock explicitly: target TurnStart, target TurnEnd, source TurnEnd, action count, or permanent. Applying during the clock owner's current turn cannot consume a turn immediately.
6. **Composite statuses.** One recipe may contain stat modifiers, periodic damage, disable flags, tags, and removal/break rules. It still counts as one status instance unless its stack policy creates independent instances.
7. **Ordered card effects.** A card recipe contains stable operations with `BeforeAction`, `BeforeDamage`, `Damage`, `AfterDamage`, and `AfterAction` windows. Operations declare target, conditions, damage family, and payload.
8. **Ownership-aware conditions.** Trigger context contains actor, target, passive owner, source status instance, card category/rank, damage family, critical/block result, actual HP/shield loss, and root action ID.
9. **No prose execution.** Sheet wording is source material. Published recipes use enums, IDs, basis points, flags, and explicit formulas.

## Runtime flow

```text
Card/Passive recipe
  -> collect operations for the current window
  -> evaluate typed conditions against trigger context
  -> resolve targets
  -> apply status through StatusContainer
       -> find identity key
       -> apply recipe stacking policy
       -> preserve source and snapshots
  -> calculate effective stats/damage policy from active instances
  -> emit ordered battle events carrying cause and instance IDs
  -> advance the declared status clocks
```

## Deliverables in this slice

- Typed status recipes for stat, DOT, disable, and composite behavior.
- A runtime `StatusContainer` with deterministic apply, refresh, stack, query, remove, and expiry behavior.
- Source ownership and DOT snapshot records.
- Card category disable checks.
- Ordered card-effect recipes, typed conditions, target scopes, and damage-family payloads.
- Passive trigger contexts that distinguish effect owner, acting fighter, and target; support owner-applied-status checks; and can scale effects from actual HP damage.
- A versioned standard effect database containing all 28 attack keyword IDs, executable known pre-damage formulas, core DOT/disable recipes, and parameterized buff/debuff stat groups.
- Catalog validation for IDs, references, stack limits, durations, and operation ordering.
- Compatibility adapters for the existing prototype effect fields.

## Deferred

- Migrating the eight existing character kits.
- Authoring Shermie XV or any other new fighter.
- Final Buff database content while the sheet tab remains WIP.
- Full passive dispatch and unresolved attack rows whose sheet formula still uses `X`/`XX%`. Those records remain `RuntimeReady=false` until exact values are authored.

## Acceptance criteria

- Same recipe with weaker potency retains the stronger payload and extends duration when longer.
- Same recipe with stronger potency replaces payload, ownership, and snapshot while preserving deterministic instance order.
- Independent DOT stacks retain distinct source snapshots.
- `RecipeAndSource` keeps separate instances for different appliers.
- Cleanse/dispel respects polarity and dispellable flags.
- Stat calculation consumes every active modifier once per status instance/stack rule.
- Card disable queries distinguish category, ultimate, rank, and card-effect suppression.
- Core compiles without Unity dependencies.
