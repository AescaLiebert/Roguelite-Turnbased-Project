---
slug: genshin-presentation-revision
status: approved
gdd_tags: [elemental-reactions, mechanics, actor-camera, architecture]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Dungeon catalog and fighter-specific combat text revision

Owner's latest instructions supersede the earlier universal FCT, prediction UI and Genshin shortcut design.

## Delivered behavior

- Dungeon route validation includes the standard versioned elemental rules, status recipes and attack recipes. The reported `RouteGenerator.ValidateProfile → RecommendedCombatClass → DungeonEntryController.Refresh` exception came from validating the full character list without that rule catalog. Shared starter definitions now carry the same stable source IDs used by their Unity exports.
- Reaction predictions are removed from card details. Actual reaction captions, colors and impact feedback continue to play from resolved events. The AI's internal planning model is retained.
- Practice uses **Series → Genshin Impact**. The separate “Genshin Impact · 5” shortcut is removed. All Fighters resets ordinary filters, and all five saved characters remain selectable.
- Genshin FCT is selected from the source fighter's `trait.genshin-impact`, independent of full-team reaction eligibility. A Genshin attack/heal uses it against any recipient; a different-series source uses its existing presentation even against a Genshin recipient. Owner-local status captions use that fighter when no separate source is supplied.
- Genshin numbers use local HYWenHei-85W, elemental color, fade-in, a shrinking entrance and a decelerating upward slide, then fade out. The font importer now names the actual embedded family; the former `HYWenHei` name produced fallback glyphs in rendered evidence. Criticals start larger and show no CRITICAL/CRITCAL kicker. **BLOCK** remains explicit. Other fighters retain Archivo Black, their configured font weight/colors and their existing motion/critical cue.
- Pool release resets font, outline and color overrides so alternating sources cannot inherit the other style. Skip/disable still clears captions and reaction audio.

## Starting animation values and review

Owner-provided screenshots supersede the earlier envelope. Starting values: 50 ms fade-in, 180 ms shrink, normal entrance scale 1.18, critical 1.7, reaction 1.08; 48 px upward travel with exponential deceleration and a subtle 6 px lateral drift; numeric lifetime 1.15 s and reaction lifetime 1.2 s, with fading in the final 30%. These are tuning values, not a claim of frame-exact reproduction from still images. Genshin numbers omit grouping separators; font sizes are normal 36 px, critical 64 px, healing 28 px and reaction 24 px. Reaction sizing is applied after the legacy settings, preventing its previous override. A compact fan replaces the regular staircase layout; reactions use a shared impact anchor above the damage cluster and do not advance the hit lane. Element outlines are thin and darker than their fill, rather than heavy black. Overload's presentation name is “Overloaded.”

Micro test: render normal, critical and blocked hits together at entrance, settle and late fade, plus the owner's Melt, Overloaded, Superconduct and clustered Hydro/physical examples. Pass when entrance scale decreases monotonically, late upward velocity is lower, both fades are visible, captions remain smaller than damage, individual packets remain readable, and neither critical word appears on Genshin captions. If criticals obscure neighbors, reduce overlap or initial scale; if the entrance is too brief, lengthen fade/shrink slightly. Review AOE density before changing lifetime. Human feel and Android runtime acceptance remain pending.

## Verification

Previously completed gameplay checks: **9665 dungeon/combat/save assertions**, **440 elemental/kit assertions**, **393 Unity Practice/dungeon-entry checks** (105 authored card plays) and **4031 description assertions**. Shared/server Release builds succeeded. This screenshot revision is verified separately by the rendered FCT/audio/cleanup fixture; gameplay rules did not change. The captured style comparison is inspected for Genshin critical-word removal and retained BLOCK, with the other-series presentation shown alongside it.

- Dungeon regression includes elemental definitions outside the selected enemy series and exercises both route generation and recommended combat class.
- The Unity Practice fixture loads all 39 actual saved assets, generates a dungeon preview/route, selects all five Genshin fighters through Series and plays 105 authored cards without inventory changes.
- Rendered feedback checks include mixed-roster source selection, Genshin/legacy pooled-item reuse, font/color separation, critical-word removal, BLOCK preservation, animation curves, no prediction panel, audio and interrupted cleanup.
- Current result files: [Practice/dungeon entry](Practice/result.txt), [combat text](Feedback/result.txt). [Series picker](Practice/01-genshin-picker.png), [style comparison](Feedback/05-exclusive-style-comparison.png).
- Screenshot revision evidence: [reference specimens](Feedback/07-reference-specimens.png), [motion preview](Feedback/08-reference-motion.gif). The preview samples the production pool styling and motion envelope at 30 frames per second; production controller playback is checked separately in the same fixture. Still references establish appearance, not exact motion timing.
- This revision passed **40 rendered presentation assertions** and the production Unity console reports no errors. The disposable preview editor also logged a Unity Search startup indexing exception; the render/check routine completed successfully. Overloaded's damage packet retains the reaction's red identity, while ordinary Pyro hits use orange.

The unrestricted combat runner's previously reproduced first-hit timing assertion is tracked separately. Android runtime testing remains pending by owner choice.
