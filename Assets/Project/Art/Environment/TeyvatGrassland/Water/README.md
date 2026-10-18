# Teyvat WP06 water r1

Review `Scenes/TG_WaterReview_r1.unity` in the isolated URP17.3/Linear art host. The main gameplay project remains Built-in/Gamma. These shaders require URP; importing the package does not migrate the renderer.

`Prefabs/TG_WaterComposition.prefab` has40 water/effect renderers in R3 arena-local coordinates. Place it at the accepted arena origin alongside the R3 world and WP05 trees; remove the previous R3 water meshes. Do not instantiate both water prefabs together. `TG_WaterFallback.prefab` has10 water surfaces with constant depth and reduced procedural detail, without foam/mist/wakes. Neither prefab includes cameras, lights, collision or a global clock owner.

The full river needs an opaque depth texture copied after opaques. No opaque-color texture, extra reflection camera, physical refraction or geometry-reflection feature is used. One existing `FightingAllstar.EnvironmentArt.TeyvatMaterialGlobals` must publish the common `_TG_Wind.w` time. Production queues favor representative combat-effect readability but do not solve arbitrary intersecting transparency.

See `ArtDirection/TeyvatGrasslandConcepts/WP06_Water.md` and `ArtSource/TeyvatGrassland/Water/README.md` in the repository for properties, deterministic source regeneration, validation, preservation and remaining integration limits. Current technical evidence is under `Artifacts/TeyvatGrassland/WP06/Revision01`.
