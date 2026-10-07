# Training mode

Enter Main Menu → Training Mode. The popup lists the registry's characters regardless of ownership, using the shared character icon. Search by name or definition ID; filter by attribute, rarity or series; sort by name, rarity or ID. Draft fighters remain visible but cannot launch a battle. Choose C0–C6 before selecting a fighter.

Training.unity copies Battle.unity's environment and presentation references. TrainingContext builds fresh definitions from the selected CharacterObject and runtime-ready fighter.kyo98. No training path consumes LocalEncounterContext or writes inventory, dungeon progress, rewards or results. Restart discards the current round and recreates fresh fighters; Exit returns to Main Menu.

## Rules and design assumptions

- Training uses the same BattleEngine, PlanDraft, LocalBattleSession, playback, animations and passive/status resolution as battle.
- User-requested 1v1; one committed player action, then one dummy action. Status effects may block/fizzle either action under normal rules.
- All authored skill ranks plus the selected constellation's ultimate are dealt and refreshed every player turn. Ultimate PG cost is waived; other card restrictions still apply. Player PG otherwise changes normally so passives can be observed.
- Dummy inherits Kyo98's stats, skills, passive and visuals, but never gains PG or draws an ultimate. Its PG bar is hidden.
- Both fighters have infinite practice health: hits still show damage and statuses, but direct damage, reflected damage, and damage-over-time cannot lower their HP or trigger defeat. This keeps card/passive tests running through lethal-looking hits. Restart resets the fighters and Exit ends training.
- Assumption: the player always starts, making the selected card immediately testable. The regular battle turn limit does not apply.
- No admin commands are defined yet.

Clarity: popup and persistent practice banner state the altered rules. Response: regular draft reset/commit plus always-visible Restart/Exit. Satisfaction and fit: existing battle camera, hit feedback and animation playback. Motivation: freely inspect cards/passives without spending resources. Numeric tiers and card ranks use the existing game's ranges; action budget comes from the user request.

## Checks

Run `powershell -ExecutionPolicy Bypass -File Tests/CombatPresentation/Run.ps1 -TrainingOnly` for core checks: all ranks, free ultimate, one-action budget, dummy PG exclusion, response turn count, replenishment, serialization, retry idempotency, isolation and more than 40 turns. Core and Unity runtime sources compile. The full combat suite currently stops at MultiHitChecks: "Dead target received extra damage packets."

Manual Play Mode checklist (not yet performed):

1. New player/readability: open the popup, filter to a known fighter, clear filters, sort, choose C0 then C6, and confirm the selected character/ultimate and Kyo98 dummy appear. All cards should be visible and tooltips describe their ranks.
2. Skill/passive: play both skills at ranks 1–3 and an ultimate. Observe the same buffs, debuffs, counters and timing as battle; each committed action exposes only one dummy slot. Test stun/disable effects and self-target skills.
3. Stress: rapidly tap cards, reset a draft, restart/exit during intro, animation and inspector. Confirm controls remain usable, lethal-looking hits leave both fighters alive, and no lingering camera or effects survive scene exit.
4. Abuse/isolation: start with a saved dungeon and note inventory/currency; train with an unowned fighter, restart and exit. Saved dungeon, collection and currency must remain unchanged.
5. Boundaries: no search results, incomplete character data, missing Kyo98 registry entry, direct Training scene launch without a selection. Show a useful message and keep Exit available.
