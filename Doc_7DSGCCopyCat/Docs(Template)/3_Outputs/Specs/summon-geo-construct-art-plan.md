---
slug: summon-geo-construct
status: draft
source: manual
gdd_tags: [mechanics, actor-camera, architecture]
owner: codex
human_checkpoint: required
next_agent: human
blocked_by: []
---

# Guoba, Oz, and Jade Screen art production plan

Date: 2026-10-21. Planning only: no Blender script, model, texture, animation, or prefab is generated in this delivery. The supplied Oz, Guoba, and Jade Screen images are the visual references. Preserve a named reference copy with the production source when modeling begins.

## Visual direction

Build recognizable three-dimensional silhouettes, with internal decoration in textures. Guoba must retain rounded volume and Oz must read from rear/side views; neither becomes a flat character billboard. Jade Screen uses a real framed volume and a simple decorated panel surface.

| Entity | Geometry for silhouette and deformation | Texture detail | Motion and effects |
| --- | --- | --- | --- |
| Guoba | Rounded body/head, ears, muzzle volume, paws, short limbs; separate pepper only if used visibly | Eye patches, facial markings, belly patch, paw pads/toes, ear interiors, fur color edges, restrained baked shading | Grounded seated idle, squash/anticipation, inhale and fire breath, return to seat; flame is VFX |
| Oz | Torso/head/beak, long wing contour, major separated outer feather shapes, tail and readable talons | Interior feather rows, purple value bands, fine feather pattern, eye and glow masks | Hover/perch idle, wing anticipation, electrical charge/bolt, recoil/recover; electricity is VFX |
| Jade Screen | Thick perimeter frame, feet/corner ornaments, major cutouts; separate low-complexity panel | Gold scrollwork, Geo emblem, fine inlay, vertical divisions, decorative opacity and emission | Frame assembly/reveal, subtle idle shimmer, absorb pulse, capacity-break fragments/dissolve, quiet expiry |

Do not model repeated feather grooves, fur lines, gold engraving, or the screen's emblem as dense geometry. Use geometry where it changes the outer contour, moves independently, or produces important depth at the actual camera distance. Use alpha cutout sparingly for feather edges; keep the bird's body and major wings volumetric.

## Verified tooling and import constraints

- Blender executable exists at `C:/Program Files/Blender Foundation/Blender 5.1/blender.exe`.
- Existing source pattern: `ArtSource/TeyvatGrassland/Trees/create_tree_kit.py` builds deterministic geometry, saves `.blend`, exports explicit FBX, and emits bounds/count/hash manifests.
- That exporter is static (`MESH`, no baked animation). It does not prove a skinned model/armature preset. Prove one rigged sample before producing the batch.
- `ArtDirection/Level Design/TeyvatGrasslandConcepts/ASSET_CONTRACT.md` separates editable `.blend` source from Unity imports and calls for explicit export/import verification and `.meta` preservation.
- Current graphics settings select `Assets/Project/Art/Environment/MapStandard/Settings/MapStandardURP.asset`; package data contains URP 17.3.0 and project color space is Gamma. Older Built-in notes are stale. Match the current renderer; this feature does not migrate graphics settings.
- Existing `ActorPresentationProfile` and `AbilityPresentationProfile` supply useful socket, bounds, clip, and contact-marker conventions. `BattlePlaybackDriver` strips AnimationEvents and disables root motion for profiled playback.

## Proposed source and runtime layout

```text
ArtSource/Summons/
  Shared/                         reusable mesh, UV, rig, validation, export helpers
  References/                     the three supplied image references
  Guoba/
    create_guoba.py
    guoba_shape_data.json
    guoba.blend
    guoba.fbx
    guoba.manifest.json
    Textures/
    README.md
  Oz/                             corresponding source bundle
  JadeScreen/                     corresponding source bundle

Assets/Project/Art/Summons/<Entity>/
  Models/
  Textures/
  Materials/
  Animation/
  Prefabs/

Artifacts/Summons/<revision>/<Entity>/
  source-and-import-report.json
  gameplay-camera-captures/
  animation-loop-and-contact-evidence/
```

Keep scripts/source outside `Assets` and import explicit exports. Source regeneration writes only that entity's owned output paths and preserves established Unity asset GUIDs. Do not delete shared materials or recreate unrelated folders.

## Blender script stages

1. Read named shape, UV, palette, rig, clip, and export settings. Start from a known source scene or replace only the owned entity collection. Output must be deterministic for the same inputs.
2. Build low-complexity editable forms using profiles/curves and controlled primitives; retain useful joint topology. Apply/validate source transforms before skinning, not as an unproved export workaround.
3. Create purposeful UV islands. Give Guoba's face/paw details and Oz's outer wing bands enough area to survive the battle camera. Keep asymmetric markings separate where mirroring would damage the reference.
4. Generate or load named UV atlases. Scripted image painting can place broad color zones and masks; allow a paint-over stage for faces, feather gradients, and ornate panel motifs. Internal detail remains editable texture data.
5. Create a non-humanoid rig, bind weights, and add socket empties. Keep source controls editable, then bake the export rig/actions. Jade Screen can use rigid hierarchy animation instead of skinning where appropriate.
6. Author named clips with anticipation, readable emission/contact, and recovery. Store visual markers in the manifest/profile. Blender markers are authoring metadata only.
7. Validate topology, normals, degenerate faces, UVs, units, negative scale, weight coverage, bounds, texture references, action names, socket names, root drift, and loop continuity.
8. Save `.blend`, export the explicitly selected rig/meshes/actions, write the manifest and preview renders, then import a sample into Unity. Confirm axis/scale/bind pose/clip separation before fixing the production preset.
9. Regenerate once and compare manifests and Unity GUIDs. A reproducible source should rebuild without duplicating meshes/materials/actions or changing unrelated files.

Planned batch entry follows the existing background Blender workflow (`--background --factory-startup --python`), with per-entity configuration and an explicit export destination. Exact Blender API/export flags belong in the proved implementation script; do not copy static environment axis/bake settings onto an untested armature.

## Rig and clip contract

| Entity | Rig requirements | Required clips | Sockets / markers |
| --- | --- | --- | --- |
| Guoba | Root, body, head/jaw, limbs sufficient for seated motion and breath; stable ground pivot | `Spawn`, seamless `Idle`, `Attack`, `Despawn` | Mouth `Emit`, body `Center`, ground `Placement`; Reveal/Emit/Complete |
| Oz | Root/body/head/tail, wing chains and major outer feathers; visible wingtip clearance | `Spawn`, seamless hovering `Idle`, `Attack`, `Despawn` | Beak/chest `Emit`, body `Center`, hover `Placement`; Reveal/Emit/Complete |
| Jade Screen | Rigid root/frame/panel/ornament transforms; static geometry where no movement is needed | `Spawn`, subtle `Idle`, `AbsorbHit`, `Break`, `Despawn` | Panel `Impact`, `Center`, ground `Placement`; Reveal/Impact/Complete |

`Attack` includes windup/emission/recovery; the presentation profile can segment it if the existing ability workflow benefits. `AbsorbHit` may be a material/VFX pulse layered over idle rather than a full skeletal animation. Capacity depletion uses `Break`; natural expiry and owner defeat use their appropriate removal visual. A visual break never creates the gameplay break.

Prove Unity Generic/non-humanoid rig import for Guoba and Oz. Import `Idle` as looping and Spawn/Attack/AbsorbHit/Break/Despawn as one-shot clips where present. Export only intended actions; verify that source NLA tracks or control-rig actions do not create unwanted clips. Validate animated skinned-mesh culling bounds and presentation bounds across the full wing flap/attack poses, especially Oz's widest wingspan, rather than accepting bind-pose bounds alone.

No animation uses authoritative root motion. Emission sockets follow the animated rig, while the entity root stays at its assigned anchor. Clips and sound cues can be shortened/skipped without changing duration, damage, or absorption. Clip absence has a pose/VFX fallback and a bounded completion path.

## Textures and materials

Each entity has a BaseColor atlas and documented masks for emission/opacity and any useful material variation. Add normal maps only where they visibly improve the result. Base color imports as color, masks as data, and normals as normal maps; document each mask channel instead of inheriting environment wind-map meanings.

Guoba favors broad painted color and soft shading. Oz uses large purple bands and controlled emission so feather detail survives distance. Jade Screen separates opaque frame from translucent panel, letting sorting and occlusion be tuned independently. Prefer an existing compatible project material style for the first proof, then add only the shader features needed for the reference.

The supplied images are references, not ready UV atlases. Their backgrounds, lighting, occluded surfaces, perspective, and watermarks should not become model textures. Create clean atlas detail and inspect unlit/material previews as well as the battle lighting.

Establish triangle count, atlas size, bone count, clip sampling, material passes, and particle budgets after the first sample. Any chosen budget is a **Starting value**, recorded in the manifest with a micro-test: render the intended simultaneous entity set on the target hardware and compare against the same scene without entities. Pass only if silhouettes/markings are readable and measured frame/memory use meets the project's agreed budget. If performance fails, reduce hidden geometry, material passes, and texture size first; if readability fails, increase only the visible silhouette or texture region at fault. Do not describe unmeasured values as standards.

## Unity prefab and placement

```text
BattleEntityRoot                   stable instance/view identity, world placement
  Model                           scale/yaw correction above animated bindings
    Rig                           binding root / Animator
      Meshes
      Skeleton                    where needed
      Sockets
  VisualEffects
  SelectionBounds                 inspection bounds, no combat target collider
```

Guoba and Jade Screen resolve their cast/team placement and detach to fixed ground anchors. Oz uses a stable owner-formation hover anchor so the actor's card lunge does not drag the bird. Per-animation placement offsets and reveal timing live in the presentation profile. Models face the enemy formation through the stable root, independently of imported clip yaw.

Use the existing battle-owned hierarchy section and session lifecycle; avoid a permanent global summon manager. The profile maps logical entity definition to prefab, correction, bounds, placement, clips, VFX and audio. A grey status portrait and category/lock symbols accompany the 3D asset.

## Production order and visual gates

First prove Guoba with a textured rig, seamless idle and one attack, plus a minimal Jade frame/panel material proof to expose transparency risk early. Then finalize Guoba, Oz's wing deformation, and Jade Screen ornament textures and break presentation. Validate each stage in the game before polishing source renders.

- Capture planning view, owner rear attack view, opposing-team view, and close inspection. Guoba's markings and Oz's bird silhouette must remain recognizable.
- Check grounded pivots, mirrored team facing, socket direction, wing clipping, and no owner-lunge inheritance.
- Scrub every clip and loop seam; test repeated attacks, death during appearance/action, and full-wing poses near camera edges without mesh culling.
- Verify Jade panel transparency against allies, incoming projectiles, other transparent effects, and another screen; check frame and panel separately.
- Test Skip, fast playback, replay rebuild, missing clip/prefab, repeated import, and encounter exit with no orphan audio/VFX/material instances.
- Review visuals with their status/owner labels and action/absorb/break sounds. The model alone is not the whole readability test.

Final approval is based on gameplay-camera evidence and animation feel, alongside source/import validation. The three reference images do not establish unseen back-side details; those remain documented artistic interpretations for review.
