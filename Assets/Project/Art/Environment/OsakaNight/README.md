# Osaka Night — Battle stage handoff

The KOF reference suggests lively 1990s Osaka nightlife: a yellow/red hero arcade, warm glazed interiors, cool teal pavement/shadows and a violet skyline. Sidewalk crowds and urban details make the street feel inhabited. This stage translates that mood into original storefront graphics, wet surfaces, restrained steam and neon glow. Complete 3D architecture surrounds all four arena sides for planning, attack and portrait cameras. Shops, cabinets, stairs, bikes and furniture have modeled depth; the sixteen posed spectators are static geometry, not animated NPCs.

## Layout and gameplay

Coordinates are local to `Osaka Night • Walking Mall`. The fighting midpoint is **(1.96, 0, 0)** locally and approximately **(-0.1663, 1.8876, -10.1019)** in Battle world space; root translation is **(-2.1263, 1.8876, -10.1019)**. The builder derives placement and floor height from the existing hero/enemy middle anchors.

Keep local combat **X [-6, 10], Z [-7, 7]** clear. Props occupy raised outer promenades; ropes and glowing bollards frame the crowd. Decoration has **no colliders**, preserving combat picking/raycasts. The builder disables the original `Land` floor.

## Authoring and runtime assets

Open `Assets/Project/Scenes/Battle.unity` in **Edit Mode** and run **Fighting Allstar → Stage → Rebuild Osaka Night Battle**. Regeneration replaces the stage, materials, meshes, reflection capture and lighting; preserve manual variations first.

- `Prefabs/OsakaNightAuthoring.prefab`: the individual modeled parts for inspection and direct editing.
- `Prefabs/OsakaNightStage.prefab`: runtime meshes combined locally by material. Its 4,678 original primitive objects are omitted after batching; their complete editable geometry remains in `OsakaNightAuthoring.prefab`.
- `Materials/`, `Meshes/`, `Textures/`: ordinary editable Unity assets. Geometry/layout sources are `Assets/Project/Editor/OsakaBattleStageBuilder.cs`, `OsakaStreetProps.cs` and `OsakaStageMeshBatching.cs`.
- `Tools/Art/GenerateOsakaNightTextures.py`: repeatable Pillow/NumPy source for the twelve original signage and surface textures. `SoftSteam.png` is generated separately by the stage builder.

Uses Unity's **Built-in pipeline**, Standard surfaces, custom `OsakaNight/Neon` and `OsakaNight/Sky` shaders, and main-camera `OsakaNightAtmosphere` with `Hidden/OsakaNight/Glow`. The reflection probe/wet surfaces use saved 128px HDR cubemap `OsakaNightReflection.asset`; rebuild to recapture surroundings.

## Starting lighting values

Artistic starting values; tune while checking character readability.

| Element | Starting values |
|---|---|
| Cool directional key | Intensity 0.76; rotation (48, -32, 0); soft-shadow strength 0.65 |
| Amber combat fill | Spot intensity 2.8; range 32; angle 105° |
| Teal combat rim | Spot intensity 2.4; range 30; angle 108° |
| Arcade warm lights | Intensity 4.5; range 14 |
| Violet distance fog | Exponential Squared; density 0.009 |
| Reflection | Intensity 0.85 |
| Camera glow | Strength 0.22; threshold 1.05; HDR enabled |

## Verification after edits

Review **both planning sides** at height 13.5 and distance 17, then attack/portrait cameras for clipping, obscured faces and fighter silhouettes. Pass: all active fighters are visible against the street, no building cuts through the combat view, and the target raycast reaches each enemy. If silhouettes fail, raise the broad combat fill before increasing sign exposure; if architecture clips, move its frontage farther from the arena. Check for Console errors, persistent meshes and assigned materials after reopening Battle.

Final saved geometry: 301,364 triangles and 248 active renderers, combined from 4,678 individual source mesh renderers. The optimized stage prefab dropped from 13.7 MB to 1.1 MB and from 5,141 to 463 GameObjects after its duplicate hidden source pieces were removed; the full model remains editable in the authoring prefab. Two service-vent steam systems now use brighter, larger wisps and face toward the open arena. Battle was saved with the optimized prefab; structural checks found no remaining disabled source renderers. The floor matches the unchanged formation anchors exactly. Both planning directions were visually reviewed; the live battle preview and all three enemy target raycasts passed. The half-turn camera now carries its orientation along the selected spatial arc: 22 intermediate samples across both directions stayed aimed at arena focus (minimum forward alignment 0.9999999), with exact rotation endpoints. Existing combat fighters retain their original placeholder art. Screenshots and the sweep check result are in `Artifacts/OsakaNight/`. Repeat the checks after regeneration or manual art changes.
