# Artifact upgrade presentation verification

Run `Tests/CombatPresentation/RunArtifactImprovementsUiChecks.ps1 -ArtifactUpgrade` from the repository root. The script starts a separate hidden Unity 6000.3.4f1 process under `Temp/ArtifactUpgradeUiProject`, copies the current runtime/UI and Core binaries, and runs `ArtifactUpgradePresentationChecks.Run`. Keep the returned process ID when polling its status. Do not start another fixture process against that same project while it is running.

The run writes native Unity PNG frames and `Verification.txt` under `Temp/ArtifactUpgradeUiProject/Artifacts/ArtifactUpgrade`. Its account subject contains a fresh random ID; it deletes that account save after verification. It renders both 1920×1080 and 1280×720. It pumps the real panel update loop and waits between observations, so fade, slide, scale and experience motion remain active in the captured evidence.

## Requirement and evidence map

| Requested behavior | Automated evidence |
| --- | --- |
| Bigger artifact slots on Fighter detail | `ArtifactTab` frame and actual frame/viewport bounds at both sizes |
| Workshop shows the current level without `/20` | `WorkshopBaseline` has `+5`; `UpgradeSuccess` has `+6` |
| Draft level scales up then eases back | `DraftFeedbackEnter` and `DraftFeedbackSettled`; projected level scale sampled over time |
| Separate before/after stat values, yellow result, green rising arrow | `DraftFeedbackEnter` and `DraftFeedbackRising`; separate labels, resolved colors and timed arrow position; committed arrow leaves the stat number clear |
| Horizontal non-wrapping material group | `HorizontalMaterialTray`; resolved flex direction/wrap and every material tile's row position |
| No separate Materials button | Absence of `enhance-add`; empty material slots still open the shared picker |
| Drafting does not spend or alter equipment | Live inventory JSON and saved account file remain unchanged while drafting |
| Shared simple lookup in equipment and workshop | `EquipmentPicker` and `MaterialPicker`; same six sort choices, no search/filter fields, selected rarity first, reversible Base EXP order |
| Lookup fades and slides from the left | `EquipmentPickerEnter` and `MaterialPickerEnter`; opacity and negative horizontal translation sampled during entry |
| Workshop lookup overlays artwork without shifting controls | Absolute host/panel placement and identical workshop control-column bounds before and after lookup opens |
| Committed experience visibly fills over time | `UpgradeExperienceStart` and `UpgradeExperienceFilling`; different bar values across timed frames, commit disabled while filling |
| Milestone improves a random affix and updates the current header | Isolated +5→+6 transaction has one improved affix; success header retains the current committed level with a visible highlight. Random sampling rules require the Core regression checks as well. |
| All stat feedback remains readable at both sizes | Every main/substat row fits its stat viewport at baseline, draft and success; the improved affix pulse/emitter is observed inside that viewport |
| Exact reviewed costs/materials and persistence | One reviewed source disappears, coins match quoted cost, target owner/favourite survive, reload preserves result |
| Upgrade effects finish cleanly | `UpgradeSettled`; normal scale restored and temporary sparkle elements removed |
| Exit motion completes | `ArtifactTabAfterUpgrade`; workshop detached after its exit interval |
| Interruptions preserve screen state | Inline opacity/translation/scale and enabled state survive pending Hide→Show and five rapid close/reopen actions; no stale completion callback runs; `KitAfterPickerExit` confirms a delayed inventory exit cannot reveal artifact slots on another tab |
| Keyboard Escape follows actual focus | A pooled Escape event is sent through the panel's real focused element; a fresh workshop closes, confirmation cancels without spending, and immediate replacements keep their focus after the old exit completes |

The fixture does not prove whole-game animation coverage. That requirement additionally needs an audit of screen/modal entry and exit call sites and rendered checks in combat and infrastructure screens. A pass for these artifact views must not be reported as proof of that broader scope.

## Reference interpretation

The five supplied reference frames show a current enhancement level beside a yellow projected level, a main stat split into before and after columns, a green upward improvement arrow, a final level banner with a flash/sparkle on the improved stat, and an inventory whose primary control is a single Base EXP/star sorting dropdown. They also show empty material slots as the affordance for opening lookup. The fixture checks these distinct states rather than collapsing draft and committed presentation into one screenshot.

## Player experience and interruption scenarios

The player goal is to compare equipment, select expendable material and understand both the projected outcome and the committed result. Clarity comes from separate current/projected numbers and protected material exclusions. Response comes from a persistent shared lookup, a horizontally scrollable tray and a guarded commit. Satisfaction comes from the experience fill, level pulse, improved affix sparkle and existing success cue. Fit comes from the shared artifact tiles, rarity frames and restrained screen motion.

Draft entry requires an owned target below its maximum level and at least one eligible source. Draft exit is clearing the tray, closing the picker/workshop, changing account or committing reviewed material. Confirmation may cancel without spending. A successful commit consumes its reviewed material and quoted coins once, then presents the committed result. While the experience animation runs, repeated commit input is ignored. Closing/detaching or switching account must cancel scheduled feedback. Drafting, failed validation and a cancelled confirmation must leave inventory unchanged.

Manual checks after automated verification:

1. Ask a new player to add material through an empty tile, explain current versus projected level, then cancel confirmation. They should identify the current level and protected pieces without instructions.
2. Repeatedly select/remove material, clear, reverse sorting and close/reopen the picker during entry motion. Controls should stay responsive, and old arrows/particles should disappear.
3. Fill the 15-slot tray, attempt a sixteenth, remove one and replace it. Scroll the full material row at both supported sizes; no tile should wrap or become vertically clipped.
4. Commit through a substat milestone and observe the experience bar, updated current header and the specific improved stat. A second immediate commit should consume nothing.
5. Test +20, insufficient coins, equipped/favourite sources, inventory changes during confirmation, and closing/account change during the fill. No stale confirmation or feedback should alter another account.
6. Have an observer identify which stat improved from the animation alone. If the pulse is unreadable, tune its hold/decay after preserving input responsiveness and separate before/after numbers.

Starting values currently used by the implementation: shared entry 320 ms, exit 180 ms, lookup dialog entry 600 ms with 28 px leftward starting offset, item entry 450 ms with a 28 ms stagger capped after twelve items, and value pulse 520 ms. The draft arrow uses 700 ms. Committed experience uses 900 ms plus 150 ms per gained level, bounded at 3600 ms, followed by each affix reveal and a short success hold. These values are tuning candidates rather than benchmark claims. Test five rapid close/reopen actions, then have an observer identify the changed stat in three upgrades. Pass when every reopened panel remains responsive and each changed stat is correctly identified; shorten entry/stagger if controls feel delayed, or extend the reveal hold if the result is missed. Preserve the current/projected value separation when changing timing.

## Current result

2026-10-17: the final isolated Unity run passed **464 assertions** at 1920×1080 and 1280×720. The report and native frames are in `Artifacts/ArtifactUpgrade`. The run covers shared upward panel motion, lookup's retained left slide, an absolute lookup host/panel, unchanged workshop control-column bounds, current/projected values, visible affix effects, horizontal material capacity, exact consumption/persistence, Escape focus routing and rapid close/reopen behavior. Representative baseline, draft, lookup and success renders were inspected. The separate broader UI regression run passed **378 assertions**; its evidence remains in `Artifacts/ArtifactImprovements`.
