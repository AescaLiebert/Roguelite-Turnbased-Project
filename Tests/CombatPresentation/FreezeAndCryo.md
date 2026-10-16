# Freeze and Cryo presentation

Freeze is an enemy control state with a clean status slate. Applying it removes every Buff and Debuff, including their owned shield amounts and dependent children. Neutral statuses remain. Existing debuff immunity is checked before the purge. A rejected Freeze does not purge anything or start recovery.

While frozen, the fighter cannot use any card and cannot receive a Buff or Debuff, including immunity-bypassing applications and Freeze refreshes. Successful attack contact removes Freeze at the damage event, so later effects in the same attack can apply normally. Periodic damage does not break it. Duration expiry also thaws the fighter. The previous recovery duration is preserved as a hidden fighter clock: two completed target turns from application, with the applying turn skipped when required. Shattering or cleansing Freeze does not reset that clock. Clones and saves retain it; status lists, status counts and dispels never see it.

The status is shown as an actor-sized ice shell. The model pauses while frozen and briefly struggles at the start of its disabled turn. Thaw restores the previous animator speed and model pose. Frost Bite has drifting cold vapor, frost flakes and ice glaze. Glacial Waltz has three faceted icicles orbiting the actor with icy wakes. All three have separate entrance, sustained and exit phases. Repeated state snapshots preserve the loop; removal starts an exit; death, reserve removal and reset clear immediately. Reapplication can interrupt an unfinished exit. Cinematic suppression hides persistent effects and restores them without another entrance.

Player cards now use the stylesheet's black overlay, including the frame. The template no longer overrides its visibility. A shattering hit refreshes hand availability immediately.

## Player experience checks

| Component | Implementation and acceptance |
| --- | --- |
| Clarity | Ice shell, Freeze icon and black cards communicate disabled actions; no recovery buff icon is shown. |
| Response | The breaking hit thaws the target immediately; later status effects and card availability use the thawed state. |
| Satisfaction | Existing status sound/flash feedback accompanies the entrance; the shell and shards provide a distinct break, with a visible turn-start struggle. |
| Fit | Effects use the existing actor-owned procedural visual system and camera suppression. |
| Motivation | Freeze trades the target's existing positive and negative statuses for a temporary action lock; its hidden recovery prevents immediate repeated Freeze. |

## Starting values and adjustment plan

Entrance 0.5 s, exit 0.6 s and Glacial Waltz orbit 155 degrees/s are **starting values** exposed in `ActorVisualEffectSettings`. At the planning camera, every effect must remain recognizable, the three icicles must be individually visible over one revolution, and the exit must finish before lingering geometry looks like an active status. If entry/exit is hard to read, lengthen that phase; if it delays the feedback, shorten it. If the orbit obscures the actor, reduce opacity or increase clearance before changing its speed. No damage, status duration or combat balance values were retuned.

## Verification

- Run `Tests/CombatPresentation/Run.ps1 -ElementalOnly` for Freeze purge/blocking, real attack shatter and hit playback, hidden cooldown/save/clone, forecast branches, immunity rejection, periodic damage, skipped applying turns and max-HP buff removal.
- Run `Tests/CombatPresentation/Run.ps1 -NeutralStatusOnly` and `-ElementalAiOnly` for adjacent status and AI regressions.
- In Unity, use **Tools > Fighting Allstar > Preview Cryo Status Effects**. It renders the production Kaeya actor in an isolated preview and saves a contact sheet, animation frames, resolved player-card overlay and lifecycle checks under `Artifacts/CryoStatus`. It leaves the open scene untouched.

Manual playtest: give a new observer a Freeze and ask them to identify why the cards are black and which hit thaws the target. Stress-test repeated grants, attack combos, cleanse, expiry, death and camera transitions. For an abuse test, try to apply Freeze again during recovery and apply both friendly buffs and hostile debuffs while frozen. For a skill test, compare preserving an enemy's existing debuffs with intentionally clearing them through Freeze. At the planning camera, an observer must distinguish the ice prison, Frost Bite mist and orbiting icicles without opening a tooltip.

The broad combat runner currently stops at `MultiHitChecks.cs:145`, `First-hit status ran late.` The same failure was reproduced in an isolated source copy with this task's Freeze runtime changes reversed. It does not prevent the focused elemental, status, AI or Unity visual checks from passing.
