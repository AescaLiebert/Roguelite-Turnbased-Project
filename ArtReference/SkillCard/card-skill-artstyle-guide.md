# Card Skill Artstyle Guide

Revision: 2026-10-12 — action readability, simplified characters, thick silhouettes, and a black perimeter fade.

## Purpose and source priority

Create portrait skill-card art that communicates **what the character is doing and where the force is moving** at thumbnail size. The target is the graphic illustration language of the project's 7DSGC reference cards.

Primary style source: `Art Reference/SkillCard/` (23 inspected images). Character design sheets supply identity; they do not set the rendering detail. Generated Cassandra and Lumine cards are comparison material, not approved style references.

The rules below combine observed reference patterns with the user's chosen production requirements. Numerical budgets are proposed defaults for this project, not measurements or official 7DSGC specifications. Generate at the requested **2:3 portrait ratio**; the local references commonly use a 170 × 265 thumbnail format or scaled versions of it.

## The central rule

**Build the action from a few readable shapes.** The pose, weapon or hand, motion path, and category field work together as one composition. Character identity should survive through silhouette, hair mass, costume color blocks, and signature equipment.

Read order:

1. The action: slash, thrust, release, guard, empowerment, or restoration.
2. The body gesture and the direction of the force.
3. The character's broad identity and skill-specific equipment.
4. Any small detail that is still necessary after the first three reads succeed.

The face is not a mandatory focal point. Rear views, profiles, small faces, and partially hidden faces are valid. The acting hand or weapon must remain understandable. A large character can still work when its shapes are simple and its pose carries the action; making the character smaller alone does not fix excessive detail.

## What the current generated results reveal

| Result | Useful part | Correction needed |
| --- | --- | --- |
| Cassandra sword spin | Sweeping arc and turning pose describe a spin | Fine hair, facial polish, shield engraving, material shine, and multiple glowing ribbons compete with the body gesture. Simplify the figure and establish one dominant sweep. |
| Cassandra thunder force | Foreshortened sword creates a clear force direction | Glossy skin/metal, elaborate costume shading, and a prominent face make it read like character splash art. Give the sword thrust and lightning the clearest shapes. |
| Lumine v3 cards | Smaller figure and more room for the action | Both use a similar distant upper-right star and oversized white trail. Distinguish the sword's cutting arc from the palm's wind release; reduce fine character detail and strengthen the outer contour. |
| Lumine v2 wind card | Rear view connects the hand to the effect | Hair, costume folds, and polished light remain too elaborate. Preserve the gesture using broader flat shapes. |
| Lumine_SingleTargetSlash | Visible sword and diagonal effect | Portrait-like face emphasis, painterly lighting, and environmental fragments exceed the target detail budget. |
| Lumine_WindBladeDebuff | Hand-originating wind and rear angle | Excessive layered effects and character detail; a green-dominant field also conflicts with the requested purple Debuff category. Green wind is allowed as an accent inside a purple field. |
| Original Lumine red card | Strong diagonal pose | Thin, intricate interior lines and a face-centered composition still favor character presentation over the skill read. |

A successful correction changes the **rendering language and action grammar**, not just the amount of empty background.

## Observed reference patterns

| Local reference | What to study |
| --- | --- |
| `Skill_11.png` | Body bend, weapon motion, and a large graphic impact explain the move; broad costume fills and dark contours carry the figure. |
| `Skill_12.png` | A diagonal airborne pose and a few large cutting shapes; the face remains a minor part of the action. |
| `Skill_111.png` | Rear-view character inside circular motion; effect overlap supplies depth and the silhouette remains distinct. |
| `Skill_2.png` | Smaller figure with a clear gesture and expansive attack paths; identity does not depend on a detailed face. |
| `Skill_222.png` and `Skill_38.png` | A stance reads through bracing, crossed shapes, and controlled rings/rays instead of a completed hit. |
| `Skill_24.png` | The raised hand and orb define the skill; the blue category field stays visible around the effect. |
| `Skill_31.png` | Flat costume masses, strong outline, and layered water curves create volume and motion without realistic rendering. |
| `Skill_33.png` and `Skill_36.png` | A foreground caster faces a much larger effect; rear composition creates scale and depth. |
| `Skill_37.png` | A green restoration field with simple character shapes and a few sweeping light forms. |

The reference set varies in camera angle, character size, and effect placement. Avoid using the same corner impact, full-body lunge, or radial layout for every skill. Dark perimeter backgrounds recur, while bright effects can cross the crop. For this project's target, a soft black fade on all edges and corners is a required finishing treatment.

## Character rendering: a strict detail budget

### Shape and contour

- Use a thick, continuous black or near-black **outer character outline** that separates the entire figure from the field and effects.
- Make the silhouette contour visibly heavier than interior lines. A useful starting ratio is roughly 2–3 times the interior line weight; scale it to remain visible in the thumbnail.
- Define the torso, limbs, hair, skirt/cape, and equipment as large, clear masses.
- Preserve spaces between limbs and the torso where they explain the action. Separate the acting hand from the effect enough to show the release or grip.
- Use a small number of interior lines for joints, essential costume boundaries, and expression. Remove lines that disappear at card size.

### Color and shading

- Use nearly flat local colors with **one broad hard-edged shadow shape per major form** as the default.
- Add a second shadow or small highlight only when it explains an important overlap or orientation.
- Keep skin, cloth, hair, and metal graphic. Avoid smooth skin modeling, realistic metal reflections, fabric texture, and glossy material rendering.
- Organize hair into a few recognizable clumps. Do not render individual strands or numerous reflective streaks.
- Reduce ornaments to their iconic shape or color patch. Omit engraving, embroidery, filigree, small buckles, and repeated tiny decorative motifs.
- Render the face with a few readable marks when visible. Detailed irises, lips, eyelashes, blush, and beauty lighting are not part of the default budget.

The result should resemble a simplified animation drawing with solid fills and controlled cel shadows. Its depth comes from form and composition, not surface polish.

## Preserve depth with geometry

Use these methods before adding rendering detail:

1. **Foreshortening:** shorten the receding limb or weapon and enlarge the near end when appropriate.
2. **Overlap:** place a large effect behind the character and one purposeful piece in front, keeping the action source clear.
3. **Scale:** use a larger near shape and a smaller distant shape to show travel or space.
4. **Value separation:** distinguish figure, motion shape, and category field as three readable value groups.
5. **Broad cel shadows:** a single shadow plane can show a turned torso, a bent leg, or layered clothing.
6. **Negative space:** a gap around the weapon, hand, or body bend makes the gesture easier to understand.

Do not compensate for a weak pose with more glow, detail, particles, or costume folds. Keep effects crisp; use minimal bloom, if any.

## Design the action before the character details

Write a one-sentence action brief first: **[body movement] → [source] → [motion path] → [release/contact]**.

Then block the card using three shapes: the body gesture, the main action shape, and the background field. The image should communicate the skill at this stage.

### Motion grammar

| Action | Body and source | Main motion shape | Readability failure to avoid |
| --- | --- | --- | --- |
| Sword slash | Torso rotation, bent or extended arm, visibly oriented blade | Curved cutting arc aligned with the blade's path | A straight remote beam or distant star that reads as a thrust/projectile |
| Sword thrust | Braced or lunging torso, arm and blade aligned forward | Narrow forward wedge or lance | A decorative circular sweep that hides the thrust |
| Spin / sweep | Turning torso, opposing arm/leg directions, trailing cloth | One dominant circular or elliptical sweep | Several unrelated neon rings that obscure the body rotation |
| Palm wind release | Shoulder and reaching arm push away from the torso | A gust, compact wind crescent, or widening pressure shape visibly rooted at the palm | A floating ribbon with no clear connection to the hand, or the same generic slash used for a sword |
| Lightning force | Weapon or hand directs the force | A few large angular branches aligned to the strike | Many thin lightning threads that become noise |
| Stance | Braced feet, guarded arms, or a poised held gesture | A restrained ring, crossed guard, or rays around the pose | Treating every stance as an explosive attack |
| Buff / heal | A deliberate support gesture or visible receiving pose | Clean outward/upward radiance or a restoring loop | An indistinct white explosion with no visible source or recipient |

The main effect must connect logically to the hand, blade, staff, or body movement. The strongest light may sit at release or contact; a distant impact star is optional, not a universal requirement. Single-target actions should read as one concentrated strike or release rather than a broad scatter of impacts.

### Character size and placement

Choose size according to the action, not a fixed face-centered crop.

- For expansive ranged effects, start with a figure around 35–60% of the card height and give the effect room to travel.
- For melee gestures or stance cards, a larger figure can work when its simplified pose is the main readable shape.
- Use rear or three-quarter views when they explain force traveling away from the caster.
- Keep the character large enough to distinguish a sword grip from a palm release. If the figure becomes a postage stamp, enlarge it rather than adding tiny detail.
- Allow peripheral cloth and effects to leave the crop. Preserve the decisive gesture and equipment relationship.

These size ranges are composition starting points, not quotas.

## Background and effect construction

- Use an abstract type-colored field, broad rays, simple streaks, and purposeful empty space.
- Use one dominant action shape, with only a small number of subordinate motion accents.
- Build effects from large flat silhouettes: white/light core, one colored edge or body, and clear tapering shapes.
- Align speed marks, cloth flow, and effect travel with the same movement. Radial rays should converge at the action's meaningful source or contact, not automatically at the character's face.
- Keep the category hue visible across a substantial part of the field. Large white effects should not erase the color read.
- Project default: **no environment, rocks, rubble, destructive objects, material fragments, or gratuitous particles**. Some source cards contain debris, but it is not required for this target.
- Keep character silhouette edges darker and clearer than surrounding graphic marks.

## Required finish: black fade on every edge and corner

Apply a **soft black vignette** around the complete portrait. It must darken all four edges and all four corners, with the corners darkest and a gradual transition toward the action area.

Suggested starting settings for this project:

- Let the fade reach inward about 10–20% of the image width/height.
- Use moderate edge darkening and stronger corner darkening; roughly 25–50% black at the outer edge and 50–70% at the corners are initial tuning values, not source measurements.
- Retain saturated category color in the action area and recognizable color through the fade.
- Use an irregular or softly elliptical falloff if needed to protect the main action shape. Avoid a visible rectangular band.
- Effects may cross the perimeter, but soften their brightness near the crop so the vignette still reads. Preserve crisp shapes in the focal area.
- Keep the acting hand and important contact/release point far enough inside the fade to remain legible.

This finish contains the composition and directs attention toward the gesture and movement. It is a tonal fade, not a frame, border, black background replacement, or blur.

## Type colors

| Type | Dominant background family | Skill effect accents |
| --- | --- | --- |
| Attack | Red / crimson / scarlet | Retain the authored fire, sword, wind, lightning, or other effect colors |
| Debuff Attack / Debuff | Purple / violet / magenta | Retain the authored effect colors; a purple field does not require poison or corruption imagery |
| Stance | Yellow / gold / amber | Use controlled guard or readiness shapes |
| Buff | Blue / cobalt / azure | Use clear empowerment or protective shapes |
| Heal | Green / emerald / leaf green | Use restoration or cleansing shapes |

Choose the background from the **card category**, not from the element. A wind Debuff Attack can use green/cyan wind inside a purple field. A fire Buff can use orange flame inside blue. Apply the same black perimeter fade across all five families.

## Reference and generation workflow

1. Select 1–3 appropriate cards from `Art Reference/SkillCard/` for **rendering and action composition**. Label their role explicitly.
2. Supply the character sheet separately for **identity only**. Ask for a simplified interpretation of its design.
3. Specify the card category, action beat, body gesture, source, travel path, and camera angle.
4. Choose character scale based on the gesture and effect. Do not mandate a visible or detailed face.
5. Include the strict contour, flat-fill, one-shadow, and detail-reduction rules in every prompt.
6. Include the black edge-and-corner fade in every prompt.
7. Inspect the result near 170 × 265 display size. Reject detail or effects that compete with the action.

Consistency means repeating the rendering budget, contour treatment, type-color policy, and edge finish. It does not mean repeating the same pose, camera, character size, or upper-right impact layout.

### Simplified identity lock

Use a short lock such as: `[hair silhouette and color], [two or three costume color masses], [signature large accessory], [weapon/focus], [body proportions]`.

For Lumine: blonde bob with flower and trailing braid, white/pale-blue dress with a few gold shapes, slender sword, consistent proportions. Preserve those identifying shapes while simplifying small ornaments and surface lines. A design sheet is not an instruction to reproduce every decorative detail.

## Reusable generation prompt

> Create one finished 2:3 portrait skill-card illustration. Primary read: **[ACTION]**, immediately understandable at card-thumbnail size. Show **[CHARACTER + SIMPLIFIED IDENTITY LOCK]** at **[ACTION BEAT]**, with **[BODY GESTURE]** and a clear **[LINE OF ACTION]**. Camera: **[VIEW]**. Place/scale the figure to explain the skill; **[COMPOSITION NOTE]**. The action begins at **[HAND / BLADE / STAFF / BODY SOURCE]**, follows **[SPECIFIC MOTION PATH]**, and resolves at **[RELEASE OR CONTACT]**. Use one dominant **[EFFECT SHAPE]** with a few supporting marks. Prioritize the gesture, equipment relationship, and motion over facial or costume detail. Use the supplied 7DSGC cards as the rendering reference and the character sheet only for identity. Draw large simple shape masses, thick black outer character contours, sparse thinner interior lines, nearly flat local colors, and one broad hard cel shadow per major form. Simplify hair into clumps and ornaments into large color patches. Preserve depth through foreshortening, overlap, scale, negative space, and value separation. Use a **[TYPE COLOR]** dominant abstract field for **[CATEGORY]**, with **[ELEMENT COLOR]** as the secondary effect. Add broad directional rays and apply a soft black fade on all four edges and corners, darkest at the corners and fading inward toward the action. Edge-to-edge art with no frame, UI, text, logo, or watermark. No environment, rocks, rubble, destructive objects, debris, or gratuitous particles.

### Exclusions

> No character beauty portrait, face-centered showcase, ornate costume rendering, fine hair strands, engraving, embroidery, detailed irises, glossy skin, realistic metal, fabric texture, painterly shading, complex multi-tone material lighting, heavy bloom, numerous layered effect ribbons, disconnected effects, environmental debris, or repeated generic corner explosion. No extra limbs, duplicate weapons, malformed hands, panels, frame, UI, lettering, logo, or watermark.

Avoid prompt language that invites a different rendering target, such as `highly detailed`, `intricate`, `luxurious materials`, `glossy`, `cinematic volumetric lighting`, or `beautiful detailed face`.

## Two action examples

### Single-target sword slash — red Attack

> Lumine cuts across the frame with a clearly rotated torso and a visible sword aligned to one curved cutting path. Use a side/three-quarter action view, a simplified medium-sized figure, and a broad tapered slash that describes the blade's actual sweep. Preserve a gap around the grip and blade. Keep the face a few marks rather than a focal portrait. Use a red field, thick black silhouette contours, flat white/pale-blue costume blocks, minimal gold accents, one cel shadow, and a soft black fade at every edge and corner. No distant beam layout unless the skill explicitly launches a projectile; no rocks or fragments.

### Palm wind release — purple Debuff Attack

> Show Lumine from behind, braced and reaching into the card's depth. Make the shoulder, arm, and open palm read as the source of a broad cyan/green-white gust or wind crescent pushing away from her. Leave enough figure size to distinguish an open hand from a sword attack. Give the wind a clear pressure/release shape; avoid reusing the sword's exact arc or a floating decorative ribbon. Use a purple field, thick black silhouette contours, flat simplified costume masses, one cel shadow, and a soft black fade at every edge and corner. No rocks, fragments, or scenery.

Append the shared rendering and exclusion clauses above when using these briefs for generation.

## Acceptance checklist

- [ ] The skill can be named from its body gesture and main motion shape at thumbnail size.
- [ ] The source, direction, and release/contact are clear and physically connected.
- [ ] A slash, thrust, palm release, spin, and stance have distinct visual grammar.
- [ ] The character remains recognizable through broad shapes and colors.
- [ ] The outer character contour is visibly thick and stronger than interior detail.
- [ ] Hair, costume, skin, and equipment use large flat masses and sparse cel shadows.
- [ ] Depth survives through foreshortening, overlap, scale, and value grouping.
- [ ] Facial polish and ornaments do not compete with the action.
- [ ] One main effect dominates; supporting marks do not form competing focal points.
- [ ] Background hue matches the card category even when the element uses another color.
- [ ] All edges and corners have a soft black fade, with no hard border.
- [ ] No environment, destructive objects, rocks, rubble, or gratuitous debris.
- [ ] No frame, UI, text, logo, or watermark.

Reject a result when its first read is a polished character portrait, when the figure disappears into the effect, or when two different skills become the same generic glowing trail. Correct the pose, shapes, contour, and detail budget before adding polish.
