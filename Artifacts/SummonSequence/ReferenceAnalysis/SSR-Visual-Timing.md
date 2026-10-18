# SSR cinematic reference inspection

Source: the user's `E:/Drive_E_Download/Gacha_pull.mp4`. Visual reference only; on-screen Thai copy and reward notifications are not implementation instructions.

Decoded with the installed Python 3.13 OpenCV package. Dimensions are 1064 × 480; 606 frames at approximately 30.003 fps. Times below are frame-index/fps and resolve changes to roughly one frame (33 ms). The source has a wider aspect ratio than the project previews.

## Frame timing

Zero for the premium segment is source frame 253, approximately 8.433 s.

| Source time | Relative time | Observed beat |
| --- | --- | --- |
| 8.399 s / f252 | before zero | Ordinary reward board is still completely visible. |
| 8.433 s / f253 | 0.000 s | Hard cut to a charcoal upper field and broad peach/red light gradient from the bottom; the X is not visible yet. |
| 8.466 s / f254 | 0.033 s | Large angular white/coral crossed blades appear almost immediately, accompanied by a horizontal flare and a dense burst of yellow/red triangular shards and tiny star glints. |
| 8.533–8.633 s | 0.100–0.200 s | Centered SSR notification text becomes readable. The emblem relaxes slightly from its initial size; shard density begins falling. |
| 8.733–9.499 s | 0.300–1.066 s | Stable emblem hold, horizontal red beam across its crossing, fine upright light spikes and sparse drift/glints. The lower edge remains strongly illuminated. |
| 9.532 s / f286 | 1.100 s | Center text begins disappearing, crossing flare brightens. |
| 9.566–9.632 s / f287–289 | 1.133–1.200 s | Very fast emblem push toward the camera: X grows from medium to larger than the whole screen in about three frames. This is a graphic transition, rather than a character/reward-card size bounce. |
| 9.666–9.699 s / f290–291 | 1.233–1.266 s | Enlarged X disappears through a dark crossfade. The new scene has diagonal charcoal facets, thin silver diagonal streaks and narrow vertical spindle glints. |
| 9.766 s / f293 | 1.333 s | Character is a faint dark ghost in the new scene. The leading coral wedge of the bottom strip enters from the far left. |
| 9.799–9.966 s / f294–299 | 1.366–1.533 s | Character brightens gradually from dark grey into readable color. The coral strip continues extending to the right. A large translucent SSR+ mark appears near the lower left and rapidly settles into a smaller sharp badge. |
| 10.032–10.199 s / f301–306 | 1.599–1.766 s | Rarity badge throws a short bright glint and triangular coral fragments; shared-style attribute/series icons and name become readable. Character art is now fully lit. |
| 10.266–10.399 s / f308–312 | 1.833–1.966 s | Strip finishes extending nearly full width with a slanted right end and two/three black diagonal hatch marks. Layout settles. |
| 10.499–10.699 s / f315–321 | 2.066–2.266 s | Small description copy fades in below the name strip. A soft warm halo and thin rainbow/iridescent ring develop behind the figure. |
| roughly 10.7–16.6 s | inspection | Character holds with gentle model motion and restrained background glints. This several-second wait includes inspection time and should not be required before the user's advance input. |

## Visual details that materially affect the match

- The opening X is an opaque, designed pair of broad angular blade plates, with inset broken dark lines, long parallel edge trims, four small dots on the lower-left blade and three hatch marks near the upper-right end. It is not primarily two thin luminous lines.
- Emblem proportions during the hold are approximately one third of viewport width and three fifths of viewport height. The source's ultrawide aspect makes that vertically prominent.
- The entire bottom third is an airy peach-white emission gradient. The strong horizontal red flare crosses the emblem at mid-height; fine vertical waveform-like light lines sit behind it. The charcoal top remains quiet.
- Particles are mainly distinct flat triangle shards and small four-point sparkles; they are brightest/densest on the first emblem frame and become sparse through the hold. They do not resemble continuous heavy fire or a storm of long embers.
- The character scene uses a separate dark faceted background. A few thin angled silver beams and narrow upright glints give depth, and the glow shifts behind the character rather than filling the whole viewport.
- There is no full-screen white impact frame during the SSR-to-character transition in these 30 fps samples. Readability comes from the dark-to-color reveal, expanding coral strip and brief badge glint. A large opaque white flash would depart from this footage.
- The strip sits at approximately 74–83% of viewport height and grows left to right. The character remains behind it. Rarity settles near the left sixth; small square attribute/series icons precede the name. The final right end has a clipped diagonal and black slash details.
- Preserve the user's separate restriction on reward arrivals: this reference's very fast X transition does not imply scaling the reward icons or character art.

## Contact sheets and tools

- `SSR-Opening-30fps-contact.jpg`: frame-by-frame emblem opening.
- `SSR-Transition-30fps-contact.jpg`: frame-by-frame emblem expansion, character appearance and strip extension.
- `SSR-Hold-30fps-contact.jpg`: strip settling and description/halo arrival.
- `SSR-HighCadence-contact.jpg`: 0.1 s cadence overview from 8.0 to 10.7 s.
- `Overview-contact.jpg`: whole clip at 0.5 s cadence.
- Individual source-sized JPEG frames and the extraction script live alongside this report. No runtime code was changed.

Installed FFmpeg binary, if needed for later recording/transcoding: `C:/Users/Apricha/AppData/Local/Programs/Python/Python313/Lib/site-packages/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe`.
