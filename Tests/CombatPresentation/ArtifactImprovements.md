# Artifact and character screen verification

Run `RunArtifactImprovementsUiChecks.ps1` to compile and render a separate Unity project with a unique disposable account. It exercises the public inventory service and real UI controls at 1920×1080 and 1280×720 landscape resolutions. Evidence is saved under `Temp/ArtifactImprovementsUiProject/Artifacts/ArtifactImprovements`.

The player goal is to compare and rearrange equipment in the same character screen. The inventory docks into its existing left character area; the existing artifact preview and tabs stay in place. Inventory stays open after selection, Equip, Swap & Equip and Unequip. A centered character preview at the bottom of the inventory shows the selected fighter and all five slots, sized to fit the landscape width. Closing inventory restores the character artwork. Equipment exchanges use the artifact's matching slot, preserve all instances and favourites, consume no currency, and persist both owners together. An occupied destination swaps; an empty destination transfers. Repeating Equip on the same artifact is a no-op. Invalid ownership and failed saves leave the prior equipment intact.

The artifact tab omits the duplicate identity panel. Inventory tiles show ownership for every equipped artifact, including the selected fighter's pieces. The front hero, compact character strip and own-character artifact preview omit repeated ownership details. Unequip retains the selected artifact and the same name/artwork panels, stat rows and scroll view; only actions and live set/ownership sections change. The inventory preview centers the fighter and five enlarged slots. Other owners remain visible when an exchange needs context. Additional Stats shows only stats beyond the four on the front page. Kit card wrappers follow the authored 1461:2650 frame ratio and skill-type badge inset. Shared item frames and footers are enlarged by the requested 1.2× across equipment, materials, backpack and rewards. Limit-break requirements use the same standard item tile and a single non-wrapping row as the upgrade view. Scroll views keep touch/wheel scrolling and hide both scrollbar tracks.

## Feedback and interaction rules

CC feedback starts only after the inspected fighter's resolved CC actually changes. Opening or switching fighters establishes a baseline; previewing, favouriting and wallet-only changes do not animate. A green rising arrow and signed gain accompany increases; a red falling arrow and signed loss accompany decreases. The overlay ignores pointer input. Its count always targets the latest committed CC, including consecutive changes; a rapid reversal to the burst's starting value dismisses it. Close, detach and account changes cancel stale feedback.

Starting animation values: 180 ms ease-out entry, 650 ms count, 1100 ms hold, 350 ms exit. Micro test: make five rapid swaps, then unequip and level up; the displayed count must settle at resolved CC every time, show the correct sign, and allow the next equipment click throughout. If feedback is hard to read, increase hold by 200 ms; if it obscures equipment, move the overlay before extending the duration.

## Player experience checks

- Clarity: the swap label, other owner's details, persistent strip, signed delta and directional arrows explain the result before and after committing.
- Motivation: swaps affect persisted equipment and resolved fighter stats immediately.
- Response: selecting previews without committing; equipment actions keep inventory open; rapid actions retarget the count without blocking input.
- Satisfaction: updated slots and set effects accompany the animated CC count, directional change and a quiet rising or falling audio cue. Consecutive changes replace the cue instead of layering sounds.
- Fit: the inventory uses the existing character screen's left area and artifact preview, with the same rarity frames and character icon system.

Manual playtest: have a new player equip and transfer a piece, then exchange two occupied slots without instructions. They should predict which slot changes and locate all equipped artifacts without closing inventory. Try keyboard selection, Escape, header Back, switching filters and repeated fast clicks. Fill all 15 material slots, reject a sixteenth, remove one and replace it; inspect exact consumption before confirming. At each supported size, verify the inventory shows a whole tile row and Equip/Enhance controls remain inside the viewport. Observe changes to set thresholds, enhancement of an equipped piece, level up, and a loss from unequipping. No amount should animate for browsing or failed transactions.

## Verified result

2026-10-13: Unity 6000.3.4f1 passed 264 assertions at 1920×1080 and 1280×720. The landscape fixture checks the skill badge inset, a centered enlarged character summary that fits the inventory, standard one-row limit-break materials, and hidden scrollbar tracks across artifact, upgrade, backpack and dungeon reward views. Existing checks also cover equipment swaps, persistence and rollback, owner indicators, retained unequip preview, 15-slot enhancement, CC feedback and reward tile bounds. Screenshots and the verification report are stored in `Artifacts/ArtifactImprovements`. Portrait layouts were not tested.
