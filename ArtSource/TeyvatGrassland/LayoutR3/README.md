# WP01 R3 editable geography

Run `create_layout_plan.py` with Python first, then run `create_layout_geometry.py` with Blender 5.1.1 in background mode. The plan generator writes `TG_LayoutPlan.json` and the exact 513×513 local-metre height sample array; these are copied into Unity LayoutR3/Data. The geometry generator imports the adjacent WP02 Geometry helper, creates closed cliff/terrace/island/rock modules, exports FBX and writes an import manifest.

`TG_LayoutGeometry.blend` is editable source, including a native-ground reference excluded from the FBX. Unity uses native TerrainData for that ground. Other exported geometry is already in arena-local coordinates, with root origin zero and the accepted scene origin applied once in Unity. Grass and trees are assembled in the saved Unity scene rather than baked into the FBX.

Export preset: metres, scale1, FBX_SCALE_UNITS, −Z forward/Y up, bake-space-transform, mesh only, LINEAR vertex data, no exporter retriangulation or modifier evaluation. Triangulate before custom split normals. Raw Unity mesh basis is `(Blender X, Blender Z, Blender Y)`. Import scale1/baked axis conversion/Import normals/no compression/readable for diagnostics. R/G/B0 and A1 are rigid material data, not visible color or opacity.

Revisions preserve original source/Unity assets under FoundationR2, World and Meadow. These R3 files intentionally replace the surrounding height/layout constraints while retaining six fighter anchors and seventeen camera poses. All new spatial inputs are in the R3 plan and layout registry; downstream packages should use those instead of the older world heightfield or waterfall coordinates.

Full handoff: `ArtDirection/TeyvatGrasslandConcepts/WP01_LayoutR3.md`. Build/reload launcher: `Tests/Environment/RunTeyvatLayoutR3Lab.ps1`. Evidence: `Artifacts/TeyvatGrassland/WP01/Revision03/`. The source archive requires this repository's accepted WP01/WP02/WP03/WP04 dependencies; it is not a standalone Unity project.
