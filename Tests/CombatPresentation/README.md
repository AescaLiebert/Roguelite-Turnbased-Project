# Battle presentation checks

Run `./Tests/CombatPresentation/Run.ps1` from PowerShell with .NET 10 installed. This creates a temporary console runner under `Temp/`, builds the shared core using its existing project, and checks event playback against authority across 64 complete seeded sessions. It covers interleaved opening merges, multi-rank chains, draft reset, merged execution rank, enemy-first playback, reserve/death, ultimates, deep-cloned events, run fighter ID remapping, and seeded random retargeting.

In Unity, choose **Fighting Allstar → Preview Battle → Player First** or **Enemy First**. These use the real battle scene, UI, catalog, and character art with reduced fixture HP for a short session. They do not load/save inventory or a dungeon run. Continue/Abandon exits the preview. Ordinary dungeon entry still uses the normal run handoff.

For an automated scene smoke test, launch Unity with `-batchmode -projectPath <project> -executeMethod FightingAllstar.EditorTools.BattlePresentationPreview.RunAutomated -logFile <log>`. Do not pass `-quit`; the test exits after reaching the result screen. Add `-battleEnemyFirst` for enemy initiative, or `-battleSkipOpening -battleSkipPlayback` to check skip reconciliation. Use a graphics device for offscreen world/Toolkit screenshots. Evidence is written to `Logs/BattlePresentationEvidence/` (uGUI world health bars are not included in these offscreen captures).

Manual visual checks:

- CC lanes enter, count up, highlight the winning side, and exit while UnitUI and the card tray are hidden.
- Each draw lands before its merge; rank-up settles before drawing the replacement. Input stays locked.
- Tap sends the actual ranked card to an ordered slot. Drag slides cards, merges adjacent matches, and costs one action. Reset restores the original hand and PG.
- The full field auto-commits; End Turn also allows a partial plan or pass. Rapid taps cannot queue during animation/commit.
- Execution highlights slots in order and displays the resolved target. Camera frames attacker and target; HP changes on impact; critical/block/shield/ultimate-ready text is readable.
- Both sides recoil and collapse on death; their HUD/target/collider/cards disappear. Reserve enters the vacated slot. Later actions retarget only living active opponents.
- Turn camera switches sides using a centered elevated view: both team rows stay horizontal, with no sideways isometric offset. It returns to player planning after execution. Enemy warning cards are intentionally absent. Victory/defeat/draw stays visible until Continue.
- Skip animation during CC, dealing, impact, or death settles to the same authority state and restores input/camera.
- Check 16:9 and narrow windows; pointer cancel/hold/drag and touch target selection should not produce extra actions.

Scope: presentation consumes the existing damage resolver and actual readiness/shield/death facts. This change does not implement the catalog's currently unsupported healing, buff/debuff duration, reaction, or passive rule families, and does not change LegalAi's decision policy.
