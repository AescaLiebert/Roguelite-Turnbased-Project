# Fighting Allstar UX/UI Style Guide

**Status:** Design direction for prototype review  
**Scope:** Main Menu first; reusable direction for roster, dungeon, battle, summon, and results UI  
**Project basis:** Fighting Allstar GDD, especially `@tag:identity`, `@tag:visual-audio`, `@tag:platform-input`, `@tag:core-loop`, `@tag:scene-roles`, and `@tag:system-ownership`.

> This is a project-specific direction informed by reference-game analysis. It is not a reproduction of Genshin Impact, NIKKE, or any fighting game's interface, art, typography, or logos. Treat all visual values below as proposed starting points until reviewed in the game and on target devices.

## 1. Product experience

Fighting Allstar is a character-and-team-driven tactical card RPG with a tournament expedition frame. The menu should make a player feel that their fighters are ready for a bout, while clearly showing the next meaningful choice: prepare a team, enter a dungeon, or grow the roster.

The visual voice combines:

- **World invitation:** a cinematic scene and a clear destination, adapted from the sense of place and character-led party fantasy in Genshin Impact.
- **Squad command:** a visible team, mission status, recruit path, and account progress, adapted from NIKKE's commander-and-squad framing.
- **Fight-night impact:** strong match typography, compact meters, sharp dividers, hit-confirm accents, and decisive calls to action, adapted from fighting-game presentation.
- **Tactical calm:** restrained decoration around information that affects decisions. During battle, clarity and response take priority over spectacle, consistent with the GDD.

The result should feel like **a premium fight-card for a tactical expedition**: modern, confident, character-forward, and readable at a glance.

## 2. Reference research and design translation

| Reference | Official material reviewed | Observed product emphasis | Translation for Fighting Allstar |
| --- | --- | --- | --- |
| Genshin Impact | [Official game overview](https://genshin.hoyoverse.com/en/game); [official HoYoWiki announcement](https://genshin.hoyoverse.com/en/news/detail/104249) | The game overview leads with a world to explore, party building, distinctive characters, and elemental mastery. HoYoWiki organizes persistent reference material into character, equipment, enemy, material, and tutorial archives. | Give the menu an atmospheric stage and one featured fighter; keep team prep and the route one glance away. Give roster and rules knowledge a persistent home instead of hiding it in event art. |
| GODDESS OF VICTORY: NIKKE | [Official site](https://nikke-en.com/indexm.html); [official anniversary report](https://nikke-en.com/events/annualreport/) | The official report speaks to the player as Commander and makes squad membership, campaign battles, recruitment, training, and accumulated progress part of a personal service record. | Present the player as team lead. Show the active formation, the next mission, and one clear roster-growth path together. Let collection history and progression feel owned by the player. |
| Guilty Gear -Strive- | [Arc System Works official game page](https://www.arcsystemworks.com/game/guilty-gear-strive/) | Arc System Works describes high-energy presentation and responsive play, while explaining discrete fight resources and counterplay such as Tension, Burst, Overdrives, and Counter Blitz. It also presents training and ranked play as distinct modes. | Borrow the *language* of combat: meter segments, rank stamps, action words, and sharp hit accents. Keep combat resources visually distinct and use practice as a separate, low-pressure destination. |

The translation above is a design inference from official product descriptions and screenshots/materials, not a claim that those games use identical layouts or that their interface rules are universal best practice. The style guide takes only the qualities that fit Fighting Allstar's documented loop.

## 3. UX principles

### 3.1 Show the next decision first

On the Main Menu, the dungeon entry is the primary action. The current route card should answer: what is this run, why can I enter, what team format does it use, and what do I get for completing it? Summon and Training are secondary. Settings and account information stay available but quiet.

### 3.2 Show ownership and risk honestly

Distinguish permanent account state from temporary run state. Owned fighters, permanent currency, and constellation progress belong to the account. Route progress, run HP, and boons belong to the current attempt. A loss must never look like it removed owned fighters or permanent inventory.

### 3.3 Make team synergy visible

Show three active fighters and one reserve as a single formation. Provide compact role, attribute, and eligibility cues. Do not imply that rarity alone determines team strength. When a dungeon is restricted, explain who qualifies and how the player can obtain an eligible fighter before they commit.

### 3.4 Keep tactical cause and effect legible

Battle cards, targets, action order, move/merge cost, PG, health, shields, statuses, and opponent intent need separate visual treatments. A selected action should produce immediate preview feedback; a committed outcome should be visually distinguishable from a local draft.

### 3.5 Give spectacle a job

Reserve the strongest contrast, motion, and screen space for a meaningful state change: entering a route, drawing a fighter, confirming an ultimate, winning, or losing. Keep idle menus composed and battle planning stable. Honor the GDD's reduced-motion preference and never communicate information by color or animation alone.

## 4. Visual identity

### 4.1 Palette

Use a mostly dark, cool foundation with a warm arena accent. The palette should support both anime character art and compact tactical text.

| Token | Starting color | Use |
| --- | --- | --- |
| Ink / stage | `#0C111D` | Main background, immersive scenes, low-noise panels |
| Slate / raised surface | `#171F2D` | Cards, team panels, modal surfaces |
| Frost / primary text | `#F2F3F5` | Main headings and primary information |
| Mist / secondary text | `#AAB3C1` | Supporting text and labels |
| Signal red | `#C84638` | Primary action, danger, hit-confirm accent |
| Arena gold | `#E9BE7B` | Reward, rank, featured fighter, selected emphasis |
| Counter teal | `#64B9B0` | Positive state, defense, safe confirmation |
| Warning amber | `#E7A74E` | Caution, expiring effect, resource shortage |

These are proposed tokens, not lore rules. Use each color with a label, icon, shape, or position cue. Do not make character attributes depend on color alone; the GDD requires both symbol and written name.

### 4.2 Shape and surface language

- Use clean rectangles and restrained corner radii for most controls.
- Use one or two purposeful diagonal cuts or angled rules in tournament headers, fighter cards, and primary buttons to suggest a fight poster.
- Keep deep shadows and glass effects subtle. Prefer tonal layers, thin rules, and clear spacing to many stacked containers.
- Use a restrained frame treatment around portrait cards: quality and selected state should not compete with character art.
- Avoid ornamental borders around every element, excessive gold filigree, and high-frequency background patterns behind text.

### 4.3 Typography

- Use a legible sans-serif for all functional UI. Use a condensed or extended display face only for short headlines, fighter names, and event titles.
- Headlines may use uppercase and wider tracking. Sentence case is preferred for instructions and explanatory copy.
- Numeric combat values should use tabular figures where the chosen font supports them. Pair the number with a stable label or icon.
- Never compress a label until it becomes hard to read. Let supporting copy wrap or move to a detail view.
- Use bundled or licensed fonts only after checking platform availability and project redistribution rights.

### 4.4 Character art

- Let one fighter own the Main Menu hero area. Use the player's current leader or a deliberate default; never silently show a random roster member as if selected by the player.
- Protect faces, hands, and signature silhouettes from text and buttons. Use scrims or quiet fields behind copy instead of covering the character.
- Support the existing art system: full character cuts for hero displays, portraits/icons for squad slots, and authored fallback art if a runtime asset is absent.
- Maintain a clean fallback state when the account has no selected leader, no image, or a slow catalog load.

### 4.5 3D battle-stage art direction

The battle takes place in a **real 3D environment** with a fixed, readable formation camera. Use an elevated, top-down three-quarter view from behind the player formation, looking across the arena toward the opponents. Treat the player's screenshot as a reference for that camera, the outdoor courtyard, and the relative spacing of both three-fighter formations; it does not define Fighting Allstar's final character art or HUD.

- Render fighters as smooth, stylized anime 3D models with soft pastel materials and gentle cel-shading. Keep facial features, hair, and clothing forms clean and simplified.
- Use minimal linework: prefer soft edge shading and silhouette contrast over dark outlines around every feature. Avoid noisy fabric textures, tiny costume ornament, and gritty realism.
- Keep the environment fully three-dimensional, with a clear ground plane, perspective depth, soft natural light, and restrained background motion. The arena should read as a place, not a flat illustration.
- Use light stone, foliage greens, sky blue, warm cream, and muted coral for the scene. Keep character palettes distinct enough that fighter silhouettes and target state remain readable.
- Let the tactical HUD sit on top of the world with stable high-contrast panels. Do not darken or cover the whole arena to make the HUD legible; use localized scrims behind text and meters.
- Preserve the fixed 3-active-plus-reserve formations and keep health, PG, statuses, card queue, and target feedback readable over the bright environment. Follow reduced-motion and no-hidden-information rules in the GDD.

## 5. Layout and hierarchy

### 5.1 Main Menu at a glance

Recommended desktop/landscape composition:

1. **Top utility bar:** game mark, current section, currency, player profile, settings.
2. **Hero stage:** featured fighter art on one side; short season/circuit label, headline, and route card on the other.
3. **Primary action:** a single strong `ENTER DUNGEON` or `CONTINUE RUN` button. The label must match the actual state and destination.
4. **Formation strip:** three active fighter portraits plus one reserve, clearly separated.
5. **Secondary actions:** Summon and Training. Roster, mailbox, or other future systems should appear only when they have a real destination and useful state.
6. **State-aware route details:** available route, eligibility, current run stage, and reward preview. Do not show stale mock data as account state.

For mobile landscape, preserve the same reading order but reduce nonessential top-bar content, allow the fighter art to crop behind a dark scrim, and keep the primary action and team summary within comfortable reach. For narrow portrait screens, stack hero and route content and use a compact expandable account strip rather than shrinking the desktop composition.

### 5.2 Information priority by screen

| Screen | First priority | Second priority | Keep quiet |
| --- | --- | --- | --- |
| Main Menu | Continue/enter route | Current team and account resources | Ambient lore, seasonal decoration |
| Dungeon entry/map | Eligibility and reachable next stage | Enemy tendencies, difficulty, rewards, run length | Collection promotion |
| Formation / roster | Fighter role and team coverage | Progression, attributes, passives, filters | Rarity animation |
| Battle planning | Legal action, target, action order, cost | HP/PG/status and visible enemy plan | Decorative character info |
| Battle playback | Resolved event and affected fighters | Cause, amount, status, counter | Input controls that cannot be used |
| Summon | Cost and banner details before confirmation | Guarantee progress and results | Decorative reveal effects before result |
| Result | Win/loss and persistent receipt | Key events, earned rewards, next step | Unclaimed cosmetic celebration |

## 6. Components and states

### 6.1 Buttons

- **Primary:** one per view, filled Signal red or a contextually approved high-contrast treatment; verb-first label.
- **Secondary:** outlined or raised Slate, with quieter label and icon.
- **Tertiary:** text or icon-only only for common utility actions with a tooltip/accessible name.
- **Disabled:** visibly unavailable and accompanied by the reason and, when relevant, a route to resolve it.
- **Loading/submitting:** block duplicate submits, show progress, preserve the user's chosen intent, and announce whether authority accepted the action.

Use stable hit areas that work for touch and mouse. **Starting value:** a 44-by-44 logical-pixel minimum for common touch targets. Validate on the smallest supported phone and at 200% text scaling; pass if intended adjacent controls can be tapped without activating a neighbor and no essential text is clipped. If it fails, increase the target, increase separation, or reflow the row.

### 6.2 Fighter cards and squad slots

Each fighter slot should support portrait, name, role/attribute cue, active/reserve status, and selected state. Use a corner number or slot label to convey position. Selected state should use at least two cues (for example border plus check mark). Reserve state should be explicit and visually distinct without implying inferiority.

### 6.3 Route cards

Show route name, open/locked status, stage length or current progress, eligibility rule, reward preview, and the next action. Use `Continue Run` only when a valid persisted run exists. If no run exists, show the always-available foundation route first. Difficulty, roster restriction, and reward should remain separate pieces of information.

### 6.4 Combat HUD vocabulary

- **Health:** continuous bar plus numeric value when it affects an immediate decision.
- **PG / ultimate gauge:** visibly segmented or notched to show meaningful thresholds; avoid decorative gradients that obscure filled amount.
- **Action queue:** ordered slots with the acting fighter and target state. Movement, merge, and reset previews must be visibly reversible before commit.
- **Counterplay:** clear word/icon labels such as `BLOCK`, `COUNTER`, `CLEANSE`, or `RESERVE IN` tied to the source and affected target.
- **Status:** icon plus short name on inspection; color may reinforce but never replace the text or symbol.
- **Event feedback:** distinguish damage, healing, blocked recovery, shields, and zero-effective recovery as specified by the GDD.

### 6.5 Modal and overlay behavior

Keep one decision in focus. Dim and disable the underlying screen, trap keyboard focus, support Escape/back where appropriate, and return focus to the opening control after dismissal. Confirmation dialogs should summarize the cost and irreversible/committed result in plain language. Never use a cinematic overlay to conceal a server rejection or unsettled reward.

## 7. Motion, audio, and response

- Use short, directional transitions to show where a panel came from and where focus moved.
- Use one restrained accent motion for featured content; do not animate currency, all cards, and navigation simultaneously.
- Card selection: lift/brighten and reveal target eligibility immediately.
- Merge: show the two source cards converging and preview the resulting rank/PG before submission.
- Commit: show a clear locked/accepted state, then hand off to playback.
- Result: reveal outcome and persistent rewards before optional celebratory flourish.
- Reduced-motion mode removes shake, aggressive zoom, large parallax, and flashing. Preserve static labels, contrast, and state transitions.
- Pair important actions with a visual change and a distinct sound cue. Provide text/caption equivalents for audio meaning; haptics remain optional.

**Starting motion values:** use a restrained 120–220 ms range for routine menu transitions and 180–320 ms for major route or result reveals. Treat these as prototype starting values, not sourced standards. Review at 30, 60, and 120 FPS with reduced motion on and off. Pass if button feedback is immediate, the next stable screen is clear without waiting for animation, and no action appears to change authority before confirmation; if not, shorten, remove, or simplify the motion.

## 8. Accessibility and platform behavior

- Design for mobile landscape touch first and mirror the same information and verbs on PC.
- Support safe areas, controller/keyboard focus, mouse hover, and touch without requiring hover-only information.
- Use readable contrast, explicit focus rings, scalable copy, and icons with text labels when meaning is not obvious.
- Never rely on color, sound, animation, or vibration as the only indicator of an important state.
- Respect system reduced-motion preferences and the GDD setting.
- Keep battle inputs stable during playback: inspection may remain available, but controls that cannot affect the committed turn should communicate that state.
- Test tutorial and error copy with a fresh player; use clear next steps rather than internal system terminology.

## 9. Content voice

Use concise, confident, action-led copy. Let fighter names and authored world flavor carry personality, while rules remain literal.

| Prefer | Avoid |
| --- | --- |
| `ENTER DUNGEON` | `GO!!!` |
| `3 active · 1 reserve` | `Your ultimate squad is ready!` |
| `Locked: requires 3 eligible fighters` | `You can't go there` |
| `Run progress is saved` | `Don't worry, everything is fine` |
| `Can't Recovery` (specified combat feedback) | Rephrasing a canonical combat state inconsistently |

## 10. Token starting set

The measurements below are **starting values for a 1920 × 1080 reference frame**, not final platform specifications. Test the same screens at 1280 × 720, common mobile landscape sizes, safe-area insets, and enlarged text. Pass if route, formation, primary action, currency, and battle decisions remain readable and reachable; when a pass condition fails, reflow before reducing text size.

| Token | Starting value | Intended use |
| --- | --- | --- |
| Spacing | 4 / 8 / 12 / 16 / 24 / 32 / 48 px | Consistent spacing increments |
| Main safe gutter | 5% viewport width, capped after device review | Frame edges and top utility bar |
| Primary headline | 44–56 px | Short menu headline, no more than a few lines |
| Section heading | 18–24 px | Route and panel headings |
| Body text | 14–18 px | Instructions and descriptive copy |
| Utility label | 10–12 px | Currency, status, navigation; must remain legible |
| Card corner | 2–8 px | Panels and controls; prefer clean, restrained geometry |
| Rule width | 1 px | Dividers and quiet borders |

For mobile and dense tactical screens, prioritize accessible text size and reflow over matching these desktop pixel values.

## 11. Main Menu design specification

### Visual concept

The generated images below are direction-setting concepts, not captures of Unity scenes or claims that every pictured control is implemented. Character art, account balances, levels, enemy stats, reward quantities, route counts, and any other sample values are illustrative unless they match an explicitly confirmed GDD rule. Bind every action and account value to live state before adopting the exact copy.

![Fighting Allstar main menu concept](../Reports/visual-design/fighting-allstar-main-menu-mockup.png)

| Screen concept | Image |
| --- | --- |
| Gacha / summon banner | [View PNG](../Reports/visual-design/fighting-allstar-gacha-banner-mockup.png) |
| Choose dungeon and difficulty | [View PNG](../Reports/visual-design/fighting-allstar-dungeon-selection-mockup.png) |
| Dungeon map and stage inspection | [View PNG](../Reports/visual-design/fighting-allstar-dungeon-map-stage-inspection-mockup.png) |
| 3D in-battle stage — top-view camera and light pastel models | [View PNG](../Reports/visual-design/fighting-allstar-3d-battle-stage-topview-mockup.png) |

### Player questions answered without opening another panel

1. What is the next recommended activity?
2. Can I enter it with my current team?
3. What stays with me if I lose?
4. What does my current squad look like?
5. Where can I grow my collection or practice?

### Suggested content anatomy

- **Top-left:** compact Fighting Allstar mark and active page.
- **Top-right:** Diamonds, profile/account status, settings.
- **Hero scene:** a single owned/selected fighter with room for a clear route headline.
- **Route module:** Open Circuit or Continue Run, eligibility/open status, progress, short reward cue.
- **Primary action:** `ENTER OPEN CIRCUIT` when no run exists; `CONTINUE RUN` when a persisted run is resumable.
- **Formation band:** 3 active slots and 1 reserve with portraits and compact role cues.
- **Secondary actions:** Summon and Training; keep them subordinate to route entry.

### Empty/loading/error states

- **Catalog loading:** retain the hero silhouette or neutral backdrop; show a small loading state in the formation region; keep only valid actions enabled.
- **No selected leader:** display a neutral team/arena visual with `Choose a fighter` and a real route to roster selection.
- **Inventory unavailable:** show a recoverable message; do not display a fabricated Diamonds balance.
- **Route ineligible:** explain the missing eligibility and offer the guaranteed acquisition path from the GDD.
- **Offline/reconnect:** preserve the current local screen and show connection state; do not imply a run was abandoned or rerolled.

### Current prototype wiring note

The existing `FrontEndController` binds `battle`, `summon`, `training`, `wallet`, and `hero-art`. Keep those names when refining the current UXML unless the controller is changed in the same work. The screen visual should not imply roster editing, settings, or route selection is live unless those controls are actually wired. The current GDD target places MainMenu, roster/build inspection, team selection, between-room map, summon/results, and settings under the long-term hub role; treat unimplemented sections as planned work, not current behavior.

## 12. Review checklist

### Clarity

- Is the primary action obvious within a quick glance?
- Can a new player tell active fighters from reserve?
- Are route restrictions, rewards, and progress distinct?
- Can a viewer explain why a card action was legal, costly, blocked, or resolved?

### Motivation

- Does route entry connect to roster growth and a visible reward loop?
- Does the result state explain what persisted and what ended with the run?
- Does a failed run suggest a meaningful team or sequencing adjustment?

### Response

- Does selection show immediate feedback?
- Does Reset restore the draft without changing authority?
- Can buttons be activated rapidly without duplicate commands?
- Does committed playback block only actions that cannot affect the resolved turn?

### Satisfaction and fit

- Do sound and visual cues agree for card snap, merge, ultimate, counter, win, and loss?
- Is reduced motion respected?
- Does the screen feel like a fighting roster entering a tactical tournament, not a generic RPG dashboard?

### Usability scenarios

Review with a fresh player, a returning player with an active run, a player whose squad fails an eligibility rule, a player with low Diamonds, and a player using enlarged text or reduced motion. Record device, resolution, active state, failed expectation, and the next UI change. These are proposed review scenarios, not completed tests.

## 13. Sources

- HoYoverse, [Genshin Impact — official game overview](https://genshin.hoyoverse.com/en/game).
- HoYoverse, [The Genshin Impact HoYoWiki Has Launched](https://genshin.hoyoverse.com/en/news/detail/104249).
- Proxima Beta / SHIFT UP, [GODDESS OF VICTORY: NIKKE official site](https://nikke-en.com/indexm.html).
- Proxima Beta / SHIFT UP, [NIKKE 1st Anniversary report](https://nikke-en.com/events/annualreport/).
- Arc System Works, [Guilty Gear -Strive- official game page](https://www.arcsystemworks.com/game/guilty-gear-strive/).
- Fighting Allstar, [Game Design Document](../1_Inputs_Templates/GDD_Fighting_Allstar.md).
