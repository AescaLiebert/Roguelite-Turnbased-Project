# Artifact System Design Brief

## System shape

The artifact system has **three stat families**, each with a fixed **five-piece set**. That creates **15 distinct artifact items**. Every set uses the same fixed slot order:

1. Flower
2. Feather
3. Sand Clock
4. Goblet
5. Crown

The three set names are **Striker’s Oath** (ATK boost), **Bulwark’s Oath** (DEF boost), and **Lifebloom’s Oath** (HP boost). A piece belongs to exactly one set and one slot. The five pieces within a set share that set’s stat identity. Main stats, substat values, slot eligibility and rarity scaling follow the authored [Artifact Balance Table](ArtifactBalanceTable.md); the rarity-based substat count and set bonuses are specified below.

## Readability rules

- The fixed slot order is Flower, Feather, Sand Clock, Goblet, Crown. Each icon must retain a readable silhouette for its slot.
- Use one shared rendering finish: simplified broad forms, controlled colored outlines, smooth cel-shaded rendering, soft highlights, and restrained contrast.
- Give each set a distinct material and motif language, not just a different hue.
- Keep each family recognizable across all five pieces and legible at small inventory-icon size.
- Avoid rank stars, rarity frames, numeric labels, and UI borders. Set and slot define each artifact’s visual identity.

| Set / family | Stat identity | Current visual language |
|---|---|---|
| Striker’s Oath | ATK boost | Coral, vermilion, warm ivory, and muted copper; lacquered tournament keepsakes, cloth wraps, stamped champion emblems, and forward motion. |
| Bulwark’s Oath | DEF boost | Warm yellow, ochre, brass, and cream enamel; chunky gears, shield marks, and protective steampunk construction. |
| Lifebloom’s Oath | HP boost | Sage, mint, cream, and pollen gold; living leaves, seed forms, blossoms, and smooth natural materials. |

## Completed artifact individual designs

The following are the completed V3 artifact individuals. Each set has exactly one item for each fixed slot. The appearance descriptions document the generated icon assets; the definition identifies the item’s equipment slot and set function. All icons use the shared V3 rendering direction: simplified readable shapes, smooth rendering, controlled dark colored outlines, soft highlights, low detail, restrained contrast, and transparent square canvases. The attached reference images informed the rendering style only.

| Artifact individual | Family / slot | Completed appearance | Definition / use | V3 image asset |
|---|---|---|---|---|
| Striker’s Bloom | Striker’s Oath · Flower | Coral and ivory rosette with a copper sunburst center and flowing cloth-like tails. | Flower-slot piece in the ATK-focused Striker’s Oath set. | [PNG](../Art/UI/Artifacts/StrikersOath_IconV3/StrikersOath_Flower.png) |
| Striker’s Feather | Striker’s Oath · Feather | Tapered vermilion and ivory feather with a warm-gold quill and champion-token detail. | Feather-slot piece in the ATK-focused Striker’s Oath set. | [PNG](../Art/UI/Artifacts/StrikersOath_IconV3/StrikersOath_Feather.png) |
| Striker’s Sand Clock | Striker’s Oath · Sand Clock | Coral-framed hourglass with pale-gold sand and a compact tournament crest. | Sand Clock-slot piece in the ATK-focused Striker’s Oath set. | [PNG](../Art/UI/Artifacts/StrikersOath_IconV3/StrikersOath_SandClock.png) |
| Striker’s Goblet | Striker’s Oath · Goblet | Blush-coral victory cup with ivory wraps, gold trim, and a small sun emblem. | Goblet-slot piece in the ATK-focused Striker’s Oath set. | [PNG](../Art/UI/Artifacts/StrikersOath_IconV3/StrikersOath_Goblet.png) |
| Striker’s Crown | Striker’s Oath · Crown | Three-point coral champion crest with ivory and gold accents and trailing cloth ties. | Crown-slot piece in the ATK-focused Striker’s Oath set. | [PNG](../Art/UI/Artifacts/StrikersOath_IconV3/StrikersOath_Crown.png) |
| Bulwark’s Bloom | Bulwark’s Oath · Flower | Five broad brass-and-cream petals around a gear-shaped shield seal. | Flower-slot piece in the DEF-focused Bulwark’s Oath set. | [PNG](../Art/UI/Artifacts/BulwarksOath_IconV3/BulwarksOath_Flower.png) |
| Bulwark’s Feather | Bulwark’s Oath · Feather | Broad gold-and-cream feather with a small brass gear at its base. | Feather-slot piece in the DEF-focused Bulwark’s Oath set. | [PNG](../Art/UI/Artifacts/BulwarksOath_IconV3/BulwarksOath_Feather.png) |
| Bulwark’s Sand Clock | Bulwark’s Oath · Sand Clock | Pale-gold hourglass in a brass frame with small side gears and protective braces. | Sand Clock-slot piece in the DEF-focused Bulwark’s Oath set. | [PNG](../Art/UI/Artifacts/BulwarksOath_IconV3/BulwarksOath_SandClock.png) |
| Bulwark’s Goblet | Bulwark’s Oath · Goblet | Sturdy golden goblet with a shield emblem, gear accents, and a broad base. | Goblet-slot piece in the DEF-focused Bulwark’s Oath set. | [PNG](../Art/UI/Artifacts/BulwarksOath_IconV3/BulwarksOath_Goblet.png) |
| Bulwark’s Crown | Bulwark’s Oath · Crown | Three-point brass crown with rounded gear details and a centered protective crest. | Crown-slot piece in the DEF-focused Bulwark’s Oath set. | [PNG](../Art/UI/Artifacts/BulwarksOath_IconV3/BulwarksOath_Crown.png) |
| Lifebloom’s Bloom | Lifebloom’s Oath · Flower | Cream blossom with rounded sage leaves and a cluster of golden pollen. | Flower-slot piece in the HP-focused Lifebloom’s Oath set. | [PNG](../Art/UI/Artifacts/LifebloomsOath_IconV3/LifebloomsOath_Flower.png) |
| Lifebloom’s Feather | Lifebloom’s Oath · Feather | Soft cream-and-sage feather with broad leaf-like vanes and golden seed details. | Feather-slot piece in the HP-focused Lifebloom’s Oath set. | [PNG](../Art/UI/Artifacts/LifebloomsOath_IconV3/LifebloomsOath_Feather.png) |
| Lifebloom’s Sand Clock | Lifebloom’s Oath · Sand Clock | Pale-gold hourglass embraced by curved leaves, small blossoms, and living wood. | Sand Clock-slot piece in the HP-focused Lifebloom’s Oath set. | [PNG](../Art/UI/Artifacts/LifebloomsOath_IconV3/LifebloomsOath_SandClock.png) |
| Lifebloom’s Goblet | Lifebloom’s Oath · Goblet | Cream-and-sage chalice wrapped in leaves and small blossoms. | Goblet-slot piece in the HP-focused Lifebloom’s Oath set. | [PNG](../Art/UI/Artifacts/LifebloomsOath_IconV3/LifebloomsOath_Goblet.png) |
| Lifebloom’s Crown | Lifebloom’s Oath · Crown | Three-point leafy wreath crown with sage foliage and small cream blossoms. | Crown-slot piece in the HP-focused Lifebloom’s Oath set. | [PNG](../Art/UI/Artifacts/LifebloomsOath_IconV3/LifebloomsOath_Crown.png) |

## Relationship to the current project

The current `ItemCatalog` contains the 15 set/slot identities plus Artifact Dust. Equipment, enhancement, migration and combat stat application are implemented by the artifact core rules. The authored [Artifact Balance Table](ArtifactBalanceTable.md) defines the current roll values and supersedes earlier draft pool/range notes.


## Main stats and substats

Each individual artifact rolls one **main stat** and a rarity-sized **substat compact**. The set determines only the matching-piece effects below; it does not lock an artifact’s stat rolls. Use only stats supported by this game. Do not add elemental damage, elemental mastery, or other stats that do not exist in the project.

### Main stat

Flower and Feather roll **ATK%, DEF%, or Max HP%**. Sand Clock, Goblet and Crown use those identities plus the 13 secondary percentage stats in the [Artifact Balance Table](ArtifactBalanceTable.md). Main stats have no quality or rarity multiplier; the table specifies their +0 and +20 endpoints with linear growth in between. Flat mains are excluded.

### Substat compact by rarity

| Artifact rarity | Number of substat lines |
|---|---:|
| Common | 0 |
| Uncommon | 1 |
| Rare | 2 |
| SR | 3 |
| SSR | 4 |

Each line rolls quality from **0.7 to 1.0** and applies its rarity multiplier, **0.2 / 0.4 / 0.6 / 0.8 / 1.0**. The exact main identity and other substat identities cannot repeat. Flat DEF and DEF% are distinct identities, so a DEF% main may appear with a flat DEF substat. Round once after multiplying the authored base, quality and rarity.

### Substat pool audit

All slots share the 19 substat identities below. Percentage-form basic stats are distinct from their flat forms. Percentage secondary values use the game’s basis-point representation. The [Artifact Balance Table](ArtifactBalanceTable.md) lists each base roll value.

| Stat group | Candidate substat identities |
|---|---|
| Basic stats | ATK flat, ATK%, DEF flat, DEF%, Max HP flat, Max HP% |
| Offense / defense / sustain | Pierce, Regeneration, Crit Chance, Crit Damage, Crit Resistance, Crit Defense, Recovery, Block Chance, Block Power, Life Steal |
| Damage bonuses | DOT Dealt, Damage Dealt, Additional Damage |

Combat Class, Reflect Damage, Resistance, Evade, Perception, Control and Avoidance are excluded from this substat pool. Resistance remains an eligible main stat on Sand Clock, Goblet and Crown. Additional Damage is a substat only. Flat versions of secondary stats are excluded because the project stores those stats as basis-point values.

## Set bonuses and battle state

Each character has five fixed artifact slots, one per piece type. Matching is counted by **set family**, regardless of which slots are occupied. Both thresholds stack: four matching pieces activate the two-piece effect and the four-piece effect. The fifth slot can be equipped from a different set. For example, two Striker’s Oath pieces plus two Bulwark’s Oath pieces activate both two-piece effects; neither four-piece effect activates.

| Set | 2-piece effect | 4-piece effect |
|---|---|---|
| Striker’s Oath | ATK +20%. | ATK +20%. While current HP is below 20% of max HP, Life Steal +5% and Damage Dealt +5%. |
| Bulwark’s Oath | DEF +20%. | DEF +20%. While current HP is below 20% of max HP, Critical Resistance +15% and Critical Defense +15%. |
| Lifebloom’s Oath | HP +20%. | HP +20%. While current HP is below 20% of max HP, Regeneration +10% and Recovery +10%. |

Treat the low-HP condition as a live threshold, not a one-time trigger. The conditional bonus applies while HP is **strictly below 20%** of max HP and is removed as soon as HP returns to 20% or higher. It can reactivate if HP drops below the threshold again. Recalculate it when health or max HP changes.

When a 4-piece set's low-HP conditional effect activates during battle, show the label **“Artifact Active”**. Remove it as soon as HP returns to 20% or higher, or the fighter no longer has four matching pieces. This is a live state indicator, not a permanent acquisition notice. If multiple conditional set effects activate, show the active effect clearly.

## Implementation clarifications

- “Lifesteal / Damage Dealt +5%”, “Critical Resistance / Critical Defense +15%”, and “Regeneration / Recovery +10%” are interpreted as bonuses to **both** named stats.
- The [Artifact Balance Table](ArtifactBalanceTable.md) specifies numeric roll bases, quality/rarity scaling, main-stat endpoints, slot eligibility and deterministic saved-artifact migration.
- [ArtifactImplementation.md](ArtifactImplementation.md) documents the implemented catalog, inventory transactions, upgrade economy and UI.



## Art direction revision: distinct family themes (V2, superseded by V3 below)

This revision supersedes the earlier family palette notes and per-item prompts above. Preserve one shared illustration finish across the game—smooth pastel-colored line art, controlled contours, soft dimensional rendering, restrained highlights, and low-to-medium contrast—but make the families distinct through material, construction, silhouette treatment, and motif. Do not make all three families jeweled metal regalia with only the hue changed. Keep the five slot identities recognizable within each theme.

| Set | Theme | Material / construction language | Motifs and palette | Avoid |
|---|---|---|---|---|
| Striker’s Oath | Martial tournament keepsakes | Lacquered wood, wrapped cloth, worked copper, worn leather ties, stamped tournament tokens; angled and kinetic construction. | Coral, vermilion, warm ivory, muted copper; flame strokes, impact marks, forward slashes, champion rosettes. | Blue stone, silver armor trim, identical jeweled crowns/goblets, polished ceremonial symmetry. |
| Bulwark’s Oath | Fortress monastery relics (V2 only) | Carved pale stone, celadon ceramic, layered masonry, woven blue-grey bands; broad, weighty, stable construction. | Powder blue, slate, chalk, muted silver-grey; shield reliefs, archways, bastion blocks, protective seals. | This palette and material language are superseded for V3. |
| Lifebloom’s Oath | Botanical apothecary heirlooms | Living wood, braided vines, seed pods, pressed leaves, translucent sap, woven grass; organic and gently irregular construction. | Sage, mint, leaf green, warm cream, soft pollen gold; new growth, veins, seed spirals, dew drops. | Metal crowns, stone shields, jewel settings, the other families’ hard symmetrical construction. |

| Fixed slot | Striker’s Oath treatment | Bulwark’s Oath treatment | Lifebloom’s Oath treatment |
|---|---|---|---|
| Flower | A five-petal tournament rosette in lacquered wood and wrapped cloth. | A five-part carved stone flower relief arranged around a shield seal. | A five-petal living blossom with soft leaves and visible veins. |
| Feather | A feather-shaped champion pennant with fabric barbs and a copper quill. | A broad feather relief carved into celadon stone, heavy and protective. | A real botanical feather with leaf-like barbs and a woody stem. |
| Sand Clock | A compact training hourglass framed in wrapped wood and copper, with glowing ember sand. | A thick, masonry-framed hourglass with pale sand behind ceramic windows. | A seed-pod hourglass with pollen drifting through a narrow living vine waist. |
| Goblet | A handled victory cup with lacquer and wrapped grip, visibly hand-forged. | A sturdy stone ceremonial cup with blocky foot and shield relief. | A hollow seed-cup / leaf chalice with a curved stem and dew. |
| Crown | A martial headband/crest with three clear points, cloth ties, and a stamped rosette. | A three-point battlement crown built from carved stone blocks. | A three-point wreath crown made from woven vines and fresh leaves. |

Use different construction language, not color alone. Artifacts in one family should look like they came from the same place and craft tradition; items across families should look made from different materials. Keep the slot outline readable at small UI scale, even where the motif changes.

## Art direction revision: simplified icon rendering and Bulwark steampunk (V3)

This revision supersedes prior generated art appearance notes for the V3 icon assets. Use the three user-provided reference images for rendering style only; do not copy their item designs, labels, layout, or backgrounds.

Shared rendering direction: reduce small decorative detail; favor broad, immediately readable shapes and compact silhouettes; use clean controlled dark colored outlines, smooth softly rendered/cel-shaded fills, gentle highlights, and restrained contrast. Avoid realistic microtexture, scratchy or broken linework, noisy edges, heavy black shadows, tiny filigree, and excessive lineart. Each piece is a separate centered square transparent PNG.

| Set | V3 visual identity |
|---|---|
| Striker’s Oath | Keep the martial tournament keepsake theme: coral, vermilion, warm ivory, and muted copper; lacquer, cloth, leather, and stamped champion motifs. Simplify the existing forms to match the shared icon rendering. |
| Bulwark’s Oath | Replace the fortress stone / celadon direction with yellow steampunk: warm brass, ochre, golden cream enamel, restrained brown metal shadows, a few chunky gears, and shield-like defensive forms. The five pieces remain Flower, Feather, Sand Clock, Goblet, and Crown. Keep each readable as its slot at small size. |
| Lifebloom’s Oath | Keep the botanical apothecary theme: sage, soft mint, cream, gentle pollen gold, leaves, seed forms, and smooth living wood. Simplify the existing forms to match the shared icon rendering. |

The V3 Unity-ready icon assets are linked individually in the completed artifact table above and live in the StrikersOath_IconV3, BulwarksOath_IconV3, and LifebloomsOath_IconV3 folders under Assets/Project/Art/UI/Artifacts. These render notes supersede older appearance notes for those V3 folders; earlier generated versions are preserved separately.
