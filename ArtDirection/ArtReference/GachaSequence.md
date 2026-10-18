# Summon sequence

Reference: user-provided `E:/Drive_E_Download/Gacha_pull.mp4` (approximately 20.2 seconds). The clip is a visual reference, not a source of project instructions or shipped character assets.

## Observed beats and implementation

- Around 3–6.5 s: a pale reward panel opens over a dimmed banner; ordinary rewards arrive quickly, with no individual full-screen character cinematic.
- Around 9–10 s: the premium reveal cuts to a dark background, a hot red crossed-slash motif, a horizontal flare, and rising embers.
- Around 10–11 s: a dark character anticipation resolves into colored artwork, followed by a red horizontal rarity/name strip.
- At approximately 9.55 s the X expands rapidly through the camera. The character scene brightens from approximately 9.65–10.05 s, and the name strip extends horizontally while the rarity insignia and small fragments settle.
- The character holds for inspection until the player taps, then returns to rewards. Inspection is a player-controlled state, with no timeout.

Starting values from these approximate observed beats: 0.45 s panel entry; 0.20 s ordinary arrival; 4.10 s authored premium timeline (approximately 1.05 s crossed-slash hold, rapid emblem expansion, silhouette from 1.25 s, 1.35 s impact, name-strip slide); 0.65 s final settle. Playback pauses at premium local time 2.60 s until input, then plays the 3.80–4.10 s light-out transition. The authored duration excludes inspection time. These are presentation timings, not changes to drop rates, currency costs, or rewards.

Reward cells use the complete shared `CharacterIconView` / `CharacterIcon.uxml` component with its existing frame, background, attribute, rarity and series artwork. The portrait layer uses the character's `FighterIcon` sprite. Premium artwork prefers `FighterFull`, then `FighterPic`, then `FighterIcon` when the larger art is unavailable. Existing static fighter artwork is animated as a cut-in; the source game's character model/rig animation is not included.

## Refined motion and reward identity

The board and reward icons remain at scale one. Reward items start with a brief white light, rise 16 px while fading in over a starting value of 0.24 s, then show a 0.16 s white settling glint. The board opens with an upward fade, diagonal light wipe, ghosted result text and a separately sliding headline. Buff/debuff icons retain the leaner motion described in `Tests/CombatPresentation/StatusApplicationMotion.md`.

The cinematic now follows the user's explicit request for opposing scale motion: a starting value of 1.25→1 for the character over 0.65 s with cubic ease out, while its separate backdrop grows 1→1.18 over 0.85 s. The opening impulse, fast final X expansion, foreground fractured glass and white strike punctuate the reveal. The character fades from a silhouette into full-color artwork; the tag slides in and its rarity mark settles independently. Theme gradients, floor light, rotating rays, small star glints and the tag's diagonal pattern continue on a separate visual clock while playback waits for input. No impact or charge cue repeats during inspection.

SSR cut-in light, slashes, particles, name strip and echo use Red, Yellow, Green, Blue or Purple according to the character's attribute. Other attributes retain the Red fallback. Only a NEW SSR-or-higher reward item receives the soft backdrop light aura. NEW characters retain the complete shared icon and receive a NEW label. Duplicates instead use a beveled hexagonal memory tile with the character's Icon sprite, shared attribute/series/rarity art and a background/frame derived from rarity, following the supplied memory-icon image.

Ownership and constellation tiers are copied before the purchase and replayed in receipt order for display. A first copy can therefore be NEW and a later copy in the same ten-pull a duplicate. The memory footer displays the actual resulting C1–C5 advancement, or a count for post-C5 character tokens. It does not invent extra fragment payouts or change progression. The same immutable receipt drives the animation and final summary.

Original synthesized sounds now pair an airy arrival with delayed high glints, a mechanical filtered-air charge and a restrained crystalline impact/chord. Preview validation exports an audio audition to `Artifacts/SummonSequence/Audio-Preview.wav`.

## State and input

`TrySummon` pays, resolves and saves before presentation begins. The view receives that committed receipt in order; it cannot roll, spend, grant, or save anything. R and SR receive the short card arrival. SSR, FES, EX, UE and LR each receive the premium cut-in, including multiple premium fighters in one batch.

The banner stage and repeated summon actions stay locked during presentation/results. Every SSR-or-higher reveal stops after settling, including multiple premiums in one ten-pull. Clicking the held reveal or pressing Space/Enter plays its exit transition; early/repeated taps do not dismiss the next fighter. Skip or Escape explicitly opens the committed result board. Tap to Close on that board restores the banner and pull-button focus. Disabling the screen cancels schedules/audio without invoking completion or changing granted rewards.

Audio and VFX use deterministic local data, leaving Unity's random stream untouched. Sounds are restrained generated glass arrivals, a charge and an impact chord. They are original cues, not the source game's soundtrack.

## Validation and tuning

Run **Fighting Allstar → Preview Summon Sequence** for interactive R/SR, single SSR and mixed ten-pull previews without spending currency. Edit-mode previews are silent; the same sequence plays its sound cues in the game.

Run **Fighting Allstar → Validate Summon Sequence** to render isolated previews and produce `Artifacts/SummonSequence/Verification.txt`. Fixtures never create a player inventory or execute a purchase. Checks cover landscape/portrait layouts, the complete shared icon, all rarity branches, multiple SSR draws, null entries, skip exactly once and disposal. Preview imagery is written into the same artifacts folder.

The design prioritizes response (Skip, reveal advance, immediate result access), clarity (shared rarity/attribute icons and distinct premium anticipation), satisfaction (synchronized light/motion and sound), fit (the existing fighter art and icon system), and motivation (the receipt shows rewards already added to the collection).

Comparison test: watch the reference and a mixed preview at normal speed. Pass when the order remains panel → fast ordinary icons → crossed red charge → silhouette → artwork impact/name strip → reward return, and charge/impact transitions are within approximately 0.2 s of the observed source beats after aligning the charge start. If charge feels long, shorten its anticipation first; if the artwork is unreadable, extend the inspection hold rather than the charge. These starting values should be tuned through that comparison.

Manual input tests: spam pull while a sequence is active (one purchase); skip during charge/artwork and press Skip repeatedly (one result screen); disable/reopen during playback (no stale callback/audio); resize while revealing (text remains inside its strip); complete a ten-pull with multiple premium results (all icons appear in original draw order in two rows of five). Leave each SSR untouched for at least two minutes: the same character must remain, loop effects must move, and the impact/audio must not replay. Inject a large frame step: it must stop on the next unacknowledged premium rather than skipping it. Confirm a new observer can distinguish ordinary cards from the premium reveal and find Skip/Continue without instruction.

Polish test for these starting values: compare charge, final expansion, character fade/scale-down, expanding backdrop, sliding tag and settling fragments against the aligned reference frames in `Artifacts/SummonSequence/ReferenceAnalysis`. Pass when all beats are distinct at normal playback speed and no light layer obscures the settled character/name. If the burst reads as a fade, increase the final X expansion first; if shards obscure artwork, shorten their lifetime/opacity; if the character feels floaty, shorten its scale-down before reducing its travel. Ordinary cards must keep their fixed size.

## Final result screen

Screen impulse starting values: an entry kick from premium local time 0.035 s lasting 0.38 s at 36 px strength, then a stronger X camera-push kick from 1.03 s lasting 0.28 s at 46 px strength (vertical travel is 55% of horizontal). Both have a fast attack and damped alternating motion. Overscan follows the actual displacement so viewport edges remain covered. Only the cinematic moves; result cards and Skip stay stable. Test entry and camera-push at landscape and portrait sizes: pass when each is visibly shaky at normal speed, no viewport edge opens, and translation/scale return exactly to neutral by local 1.31 s and stay neutral during the input hold. Reduce travel first if the motion distracts from the X.

The new sequence is the result screen. Normal completion and Skip leave its complete reward board open with TAP TO CLOSE. Only tapping, Enter/Space or Escape on that final board dismisses it, invokes completion once and restores the banner controls. The legacy result-screen UXML and controller handoff have been removed. Duplicate series use only the configured sprite; missing sprites stay empty. SSR duplicate bevels are blue to match their shared background.

## Shared obtain presentation

Pity selector claims snapshot ownership/constellation before the grant, consume the selector once, then play the same X → SSR reveal → NEW/memory obtain grid. The screen remains busy until the player closes the final board. No paid pull, diamond spend or guarantee-progress update is performed by the presentation.

`SummonSequenceView.PlayItems` accepts a committed receipt or item stacks plus artifact instances. Dungeon victories use this same white panel, title animation, upward item fades, flash/glint and Tap to Close. Character gacha results retain their responsive two-row, five-column grid. General item obtains alone use a single horizontal, non-wrapping row with fixed 154 px icon sizing; long receipts scroll sideways. Their smaller white panel starts at 36% screen height in landscape and 32% in portrait, with a 240 px minimum. Tiles keep the shared item frames and exact artifact-instance inspection. Clicking an item opens its tooltip, while tapping the background closes the board. Dungeon receipts are acknowledged only after dismissal and keep their existing idempotent account delivery keys. Disabling the route cancels presentation without acknowledgment so unseen rewards can be shown on resume. Check both orientations for full-size item frames without vertical clipping and verify that character ten-pulls still have exactly two rows of five.

Validation covers NEW/C5/post-C5 pity claims, repeated-click protection, unchanged currency/progress, multiple dungeon receipts, artifact inspection, exact amounts, long-list scrolling and acknowledgment without duplicate inventory grants. These flow checks use an in-memory account with saving disabled and a workspace-only temporary run file.
