# Elemental auras and reaction refactor

Elemental auras live in `FighterState.ElementStatuses` (`ElementStatusContainer`). They never enter the combat `StatusContainer`. A presentation adapter creates neutral icon descriptions without granting actual statuses.

## Rules

- A full starting team, including reserves, must have the Genshin Impact trait. The same frozen team gate controls aura application and reactions, including delayed explosions and retaliation. Mixed teams still deal their authored damage.
- Pyro, Hydro, Electro, Cryo and Dendro apply automatically on elemental attack contact. Anemo and Geo only drive reactions. Evaded attacks do not contact the target.
- Each element has one aura per target, with no gameplay penalty, no application roll, no stacking and no refresh. Repeated hits do not change its source or remaining time.
- Auras last three target turn ends. Only that fighter's side advances the clock. Expiry and reaction consumption emit separate aura snapshots for playback and public projections.
- Traditional debuff immunity, cleansing, Weakpoint and debuff-sensitive passives operate on ordinary statuses. They do not count or remove auras. Ignite, Shock and Frost Bite remain ordinary debuffs, and their application conditions and snapshots run through the ordinary card sequence.
- Each attacking action makes one elemental contact per original target. A consumed aura cannot be reapplied by a later hit of that action. A subsequent action can apply it again.

## Reaction changes

| Reaction | Behavior |
| --- | --- |
| Melt / Vaporize | Existing direction and 2× / 1.5× factors apply to every direct damage hit of the action against the reacting target. AOE recipients retain their own reaction results. |
| Overload | 2× level-based Pyro damage to the target and occupied adjacent slots. Shields absorb this packet at 80% efficiency: a 100-point shield absorbs 80 damage. Overlapping AOE explosions hit each recipient once per action. |
| Electro-Charged | Two target turns of 1.5× level-based Electro damage per tick. Preserves both elemental auras and their independent clocks. Spreads through adjacent living Hydro targets on application and on ticks, with a Unity 3D Electrify particle effect that descends from the charged target, travels across a wet-ground sheen and rises into the eligible neighbor. Existing copies retain their source, strength and clock. New copies do not tick in the batch that created them. |
| Superconduct | 1.5× level-based Cryo damage to the target and occupied adjacent slots. Its two-turn debuff increases all non-elemental damage taken by 40%. Existing copies cannot refresh or retrigger. |
| Burning | Two target turns of Pyro damage at 3× the trigger actor's snapshotted ATK per tick, with up to three independent stacks. Keeps its existing damage basis and duration. |
| Shatter | An attack breaks Freeze and deals one 3× level-based non-elemental hit to that target. No attack-stat scaling, crit, block or extra adjacent targets. DOT ticks, natural thaw and cleanse do not trigger it. |
| Swirl | Keeps its existing attack scaling and source-preserving adjacent spread. Spreads a fresh neutral aura or causes the destination reaction; it never copies or manufactures an ordinary DOT debuff. |
| Crystallize | Keeps its existing barrier based on actual Geo HP and shield damage. Preserves the source aura. |

Overload, Electro-Charged, Superconduct and Shatter use `character level × 500`, then their reaction multipliers and the source's explicit DOT Damage Dealt bonuses. Burning retains its existing ATK snapshot, Pyro modifiers and independent stacks, with each tick multiplied by 3. Direct elemental attacks and Swirl do not inherit DOT bonuses. Freeze consumes auras on contact but applies its debuff only after the triggering action and its follow-up damage finish, preventing later hits of that action from shattering it. Freeze removes and blocks non-grey statuses, including neutral stances and healing, and prevents all HP recovery. Grey statuses remain exempt; their healing is still blocked.

Default rules use balance version `elemental-auras-v6`. Restored v2/v3/v4/v5 defaults upgrade to level × 500 and Shatter to 3× while preserving damage caps. Existing Charged and Burning statuses upgrade to 1.5× and 3× respectively, retaining their snapshots, stacks and duration. Overload, Electro-Charged, Superconduct and Shatter ignore enemy Defense and elemental Resistance while still applying enemy DOT damage reduction. Shatter's enum value is appended so older reaction IDs remain stable.

Level is copied from the encounter snapshot into each battle fighter and survives cloning and saves. Schema-one battles upgrade on a detached copy to schema two; existing debuffs remain debuffs and do not acquire inferred auras. Legacy card metadata converts to an ordinary elemental damage declaration, and any critical requirement stays on the ordinary debuff step.

## State and feedback

Aura entry requires eligible team, living target and elemental contact; application costs no resource beyond the authored action. The state exits on its third target turn end or reaction consumption. Only reactions consume the corresponding elemental state. Consecutive actions can apply a fresh aura after removal. Save/load, defeat, reserve slots, immunity, evade, simultaneous AOE, multi-hit actions, occupied-slot adjacency and interrupted feedback have explicit checks.

Application shows a colored element caption, pulse, audio cue and the supplied neutral status icon. Reaction spread has world-space lightning cores, particle sparks, wet-ground patches/ripples and reaction caption/audio. Its starting travel/fade durations are 0.5/0.35 seconds; the visual fixture checks ground traversal, eligible recipients and cleanup. If the three phases cannot be read, increase travel time; if action feedback feels slow, reduce it while retaining all three phases. The HUD and character inspector read auras separately from buffs and debuffs. Public forecasts and AI planning use the same neutral aura selection rules without private hands or random streams.

The five design checks are: clarity through predictable aura clocks and labels; motivation through choosing setup/trigger order; response through deterministic contact without a debuff roll; satisfaction through icon, pulse, text, spread and sound; fit through the existing turn, formation and reaction presentation conventions.

`CharacterStatUI` has a dedicated `ElementStatusPanel` beside `Buff/DebuffPanel` under `StatusLayoutSlot`. The buff/debuff row stays nearest the health bar; elemental icons occupy a separate row above it, with clearance that follows the ordinary grid's height. The row uses the supplied elemental icons and the shared icon skin, with its own icon instances, countdowns and cleanup. Empty aura rows hide and release their spacing. Initial display, playback updates and skip reconciliation deliver the two containers separately. Older HUD prefabs receive the separate row at runtime.

## Verification and playtest

Run `Tests/CombatPresentation/Run.ps1 -ElementalOnly`, `-ElementalAiOnly` and `-NeutralStatusOnly`. Run `Tests/CombatPresentation/RunElementalAuraVisualChecks.ps1` for the Unity HUD, art, content, caption, spread and interruption checks. The 3D screenshot capture slows the travel/fade to one-quarter speed to avoid skipping a phase during image encoding. It uses the existing disposable CombatTextProject fixture and leaves the open project scene untouched.

`RunNeutralStatusHudChecks.ps1` verifies the two visual containers, independent clearing, icon reuse, row separation, three-turn countdowns, restored snapshots and expiry. Its Unity layout capture is `Temp/NeutralStatusHudProject/Evidence/element-status-row.png`; Python with Pillow decodes the source icons into PNG fixture copies. Readability check: show both rows together, cleanse the ordinary debuffs, then consume or expire an aura; the observer should see only the appropriate row change, without icons jumping into the other group. Stress the layout with several ordinary status rows, all five auras and repeated playback refreshes.

The focused tests cover all aura elements, both reaction directions, both acting sides, 1/3/7/10-hit amplification, level/ATK independence, DOT bonus scaling, shield efficiency, no-refresh behavior, late Hydro spread, all non-elemental damage families, mixed-team delayed attacks, schema migration, encounter levels, Freeze recovery, projections and replay.

For a balance playtest, compare level 1, 10 and 90 reaction setups against the same targets, with and without a DOT bonus. Pass if ATK changes leave these three reactions unchanged, a 50% DOT bonus scales their damage by 1.5, each reaction has the documented recipient count, and no aura or active reaction refreshes. For encounter balance, compare two actions spent on a setup/trigger with two normal attacks at the same level. If reactions dominate every matchup, lower `ReactionDamagePerLevel`; if players cannot justify a setup action, increase it. Keep the user-specified clocks and percentages fixed.

For readability, let an observer identify the five neutral auras and the element consumed by a reaction, then repeat with a ten-hit AOE. Stress-test repeated contact, cleansing, evade, immunity, death and interrupted animation. For an abuse test, repeatedly attack an existing Charged/Superconduct target: neither output should extend. For skill expression, reverse the setup order and compare Melt/Reverse Melt results.

The broad pre-existing combat runner still stops at `MultiHitChecks.cs: First-hit status ran late`, an independently documented baseline failure. The broad description audit also reports formatting assertions; the dedicated aura visual checks verify the affected new content and presentation directly.
