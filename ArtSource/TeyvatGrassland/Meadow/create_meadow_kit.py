"""Author the editable WP04 meadow clump kit in background Blender.

The native Blender Z-up meshes use metre units and ground-contact origins.
Export follows the proven WP02 baked FBX route: raw imported Unity coordinates
are Blender (X,Z,Y). All wind channels are numerical LINEAR RGBA data.
This script only writes this kit's Blender/FBX/manifest and matching Unity copies.
"""

from __future__ import annotations

import argparse
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import random
import shutil
import sys

import addon_utils
import bpy
from bpy_extras.io_utils import axis_conversion
from mathutils import Vector


SOURCE_DIR = Path(__file__).resolve().parent
ROOT = SOURCE_DIR.parents[2]
HELPER_PATH = SOURCE_DIR.parent / "Shared/create_material_samples.py"
helper_spec = importlib.util.spec_from_file_location("tg_wp02_geometry", HELPER_PATH)
helper = importlib.util.module_from_spec(helper_spec)
sys.modules[helper_spec.name] = helper
helper_spec.loader.exec_module(helper)
Geometry = helper.Geometry
rgba = helper.rgba
unit = helper.unit
SEED = 20261018


def blade(geometry, base, angle, height, width, lean, phase, rows=4, material=0):
    """Two-column curved strip with a fine point and authored upward normals."""
    base = Vector(base)
    side = Vector((math.cos(angle), math.sin(angle), 0))
    bend = Vector((-math.sin(angle), math.cos(angle), 0))
    normal = unit(Vector((0, 0, .90)) + bend * .43)
    pairs = []
    for row in range(rows):
        t = row / rows
        center = base + Vector((0, 0, height * t)) + bend * lean * (t * t)
        half = width * .5 * (1 - t) ** .7 * (.65 + .35 * math.sin(t * math.pi))
        pairs.append([geometry.vertex(center - side * half, rgba(t, 0, phase), normal),
                      geometry.vertex(center + side * half, rgba(t, 0, phase), normal)])
    for row in range(rows - 1):
        geometry.face((pairs[row][0], pairs[row][1], pairs[row + 1][1], pairs[row + 1][0]),
                      ((0, row / rows), (1, row / rows), (1, (row + 1) / rows), (0, (row + 1) / rows)), material, True)
    tip = geometry.vertex(base + Vector((0, 0, height)) + bend * lean, rgba(1, 0, phase), normal)
    geometry.face((pairs[-1][0], pairs[-1][1], tip), ((0, (rows - 1) / rows), (1, (rows - 1) / rows), (.5, 1)), material, True)


def grass_species(seed, species, lod):
    geometry = Geometry()
    config = {
        "ShortGrass": (.15, .048, .30, 16),
        "MeadowGrass": (.55, .060, .42, 22),
        "TallGrass": (.85, .070, .50, 18),
        "AccentGrass": (1.15, .080, .48, 10),
    }
    height, width, radius, count = config[species]
    rng = random.Random(seed + list(config).index(species) * 107)
    # Generate complete source blade records once. LOD1 keeps exact source bases,
    # heights, orientations, lean and phase rather than reseeding a new clump.
    records = []
    per_blade_radius = []
    full_geometry = Geometry()
    for index in range(count):
        angle = index / count * math.tau + rng.uniform(-.2, .2)
        radial = math.sqrt(rng.random()) * radius * .72
        base = (math.cos(angle) * radial, math.sin(angle) * radial, 0)
        h = height * rng.uniform(.70, 1.0)
        if index == 0:
            h = height
        record = (base, angle + rng.uniform(-.85, .85), h,
                  width * rng.uniform(.75, 1.05), height * rng.uniform(.15, .35), rng.random())
        records.append(record)
        first_vertex = len(full_geometry.vertices)
        blade(full_geometry, *record, rows=4)
        per_blade_radius.append(max(math.hypot(point[0], point[1]) for point in full_geometry.vertices[first_vertex:]))
    # A first-N selection follows this stable spatial priority. It retains the
    # tallest source blade and footprint-defining source blade, then fills the
    # widest angular gaps. LOD0 geometry/shape remains the original full clump.
    priority = [0]
    outermost = max(range(count), key=lambda i: per_blade_radius[i])
    if outermost not in priority:
        priority.append(outermost)
    while len(priority) < count:
        remaining = [i for i in range(count) if i not in priority]
        following = max(remaining, key=lambda i: min(min(abs(i - j), count - abs(i - j)) for j in priority))
        priority.append(following)
    selected = list(range(count)) if lod == 0 else priority[:max(4, count // 3)]
    for index in selected:
        blade(geometry, *records[index], rows=4 if lod == 0 else 3)
    # Identical scale for both LODs is essential: normalizing the subset's own
    # footprint would move the retained roots and tips during the transition.
    footprint = max(per_blade_radius)
    scale_xy = max(.3, min(.6, footprint)) / footprint
    geometry.vertices = [(x * scale_xy, y * scale_xy, z) for x, y, z in geometry.vertices]
    geometry.normals = [tuple(unit((n[0] / scale_xy, n[1] / scale_xy, n[2]))) if n is not None else None for n in geometry.normals]
    geometry.meadow_source_blade_indices = selected
    return geometry


def stem(geometry, base, height, angle, phase, lod, lean=.035):
    base = Vector(base)
    bend = Vector((math.cos(angle), math.sin(angle), 0))
    points = [base + Vector((0, 0, height * t)) + bend * lean * t * t for t in (0, .48, 1)]
    sides = 5 if lod == 0 else 3
    helper.add_tube(geometry, points, (.009, .007, .0035), 0, phase, True, sides)
    return points[-1], bend


def stem_leaf(geometry, attach, direction, length, width, height_ratio, phase, material=0):
    attach = Vector(attach)
    direction = unit((direction[0], direction[1], .38))
    across = unit(Vector((0, 0, 1)).cross(direction))
    normal = unit(Vector((0, 0, 1)) + direction * .18)
    center = attach + direction * length * .55
    tip = attach + direction * length
    indices = [geometry.vertex(attach, rgba(height_ratio, 0, phase), normal),
               geometry.vertex(center - across * width * .5, rgba(min(1, height_ratio + .12), 0, phase), normal),
               geometry.vertex(tip, rgba(min(1, height_ratio + .2), 0, phase), normal),
               geometry.vertex(center + across * width * .5, rgba(min(1, height_ratio + .12), 0, phase), normal)]
    geometry.face(indices, ((.5, 0), (0, .55), (.5, 1), (1, .55)), material, True)


def disk(geometry, center, radius, material, phase, sides=8):
    center = Vector(center)
    middle = geometry.vertex(center + Vector((0, 0, radius * .22)), rgba(1, 0, phase), (0, 0, 1))
    ring = [geometry.vertex(center + Vector((math.cos(i / sides * math.tau) * radius,
                                             math.sin(i / sides * math.tau) * radius, 0)),
                            rgba(1, 0, phase), (0, 0, 1)) for i in range(sides)]
    for i in range(sides):
        geometry.face((middle, ring[i], ring[(i + 1) % sides]),
                      ((.5, .5), ((math.cos(i / sides * math.tau) + 1) * .5, (math.sin(i / sides * math.tau) + 1) * .5),
                       ((math.cos((i + 1) / sides * math.tau) + 1) * .5, (math.sin((i + 1) / sides * math.tau) + 1) * .5)), material, True)


def petals(geometry, center, radius, count, material, phase, angle_offset=0, cup=.015):
    center = Vector(center)
    for i in range(count):
        angle = i / count * math.tau + angle_offset
        radial = Vector((math.cos(angle), math.sin(angle), 0))
        side = Vector((-math.sin(angle), math.cos(angle), 0))
        normal = unit(Vector((0, 0, 1)) + radial * .15)
        points = (center + radial * radius * .18,
                  center + radial * radius * .65 - side * radius * .27 + Vector((0, 0, cup)),
                  center + radial * radius + Vector((0, 0, cup * .2)),
                  center + radial * radius * .65 + side * radius * .27 + Vector((0, 0, cup)))
        indices = [geometry.vertex(point, rgba(1, 0, phase), normal) for point in points]
        geometry.face(indices, ((.5, 0), (0, .6), (.5, 1), (1, .6)), material, True)


def clover(seed, lod):
    geometry = Geometry()
    rng = random.Random(seed + 417)
    clusters = 7 if lod == 0 else 3
    for i in range(clusters):
        angle = i / clusters * math.tau + rng.uniform(-.2, .2)
        radius = math.sqrt(rng.random()) * .28
        h = rng.uniform(.11, .22)
        top, bend = stem(geometry, (math.cos(angle) * radius, math.sin(angle) * radius, 0), h, angle, rng.random(), lod, .025)
        phase = (i * .41 + rng.random() * .15) % 1
        for leaf in range(3):
            a = angle + leaf / 3 * math.tau
            radial = Vector((math.cos(a), math.sin(a), .1))
            side = Vector((-math.sin(a), math.cos(a), 0))
            tip = top + radial * .125
            # Heart-like doubled lobe silhouette, no opacity texture.
            points = (top, top + radial * .075 - side * .05, tip - side * .025,
                      tip - radial * .014, tip + side * .025, top + radial * .075 + side * .05)
            ids = [geometry.vertex(point, rgba(.5 + .5 * index / 5, 0, phase), (0, 0, 1)) for index, point in enumerate(points)]
            geometry.face(ids, ((.5, 0), (0, .45), (.2, 1), (.5, .84), (.8, 1), (1, .45)), 0, True)
    return geometry


def flower_species(seed, species, lod):
    geometry = Geometry()
    rng = random.Random(seed + {"DaisyWhite": 511, "FlowerGold": 631, "FlowerPurple": 751}[species])
    count = (3 if species != "FlowerPurple" else 4) if lod == 0 else 2
    for index in range(count):
        angle = index / count * math.tau + rng.uniform(-.25, .25)
        radial = .08 + math.sqrt(rng.random()) * .22
        base = Vector((math.cos(angle) * radial, math.sin(angle) * radial, 0))
        height = {"DaisyWhite": .55, "FlowerGold": .43, "FlowerPurple": .65}[species] * rng.uniform(.82, 1)
        phase = (index * .41 + rng.random() * .15) % 1
        top, bend = stem(geometry, base, height, angle, phase, lod, .045)
        for leaf in range(2 if lod == 0 else 1):
            attach_height = height * (.32 + leaf * .2)
            attach = base + Vector((0, 0, attach_height)) + bend * .045 * (attach_height / height) ** 2
            direction = (math.cos(angle + leaf * math.pi + .7), math.sin(angle + leaf * math.pi + .7), 0)
            stem_leaf(geometry, attach, direction, .15 if species != "FlowerPurple" else .11, .055, attach_height / height, phase)
        if species == "DaisyWhite":
            petals(geometry, top, .13, 8 if lod == 0 else 5, 1, phase, angle, .014)
            disk(geometry, top + Vector((0, 0, .006)), .036, 2, phase, 8 if lod == 0 else 5)
        elif species == "FlowerGold":
            petals(geometry, top, .105, 6 if lod == 0 else 4, 1, phase, angle, .025)
            disk(geometry, top + Vector((0, 0, .006)), .025, 1, phase, 6 if lod == 0 else 4)
        else:
            tiers = 4 if lod == 0 else 3
            for tier in range(tiers):
                center = top - Vector((0, 0, .15 - tier * .043))
                petals(geometry, center, .075 - tier * .009, 5 if lod == 0 else 3, 1, phase,
                       angle + tier * .52, .022)
                disk(geometry, center + Vector((0, 0, .008)), .013, 2, phase, 5 if lod == 0 else 3)
    return geometry


def arguments():
    values = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--seed", type=int, default=SEED)
    parser.add_argument("--source-dir", type=Path, default=SOURCE_DIR)
    parser.add_argument("--unity-fbx", type=Path, default=ROOT / "Assets/Project/Art/Environment/TeyvatGrassland/Meadow/Models/TG_MeadowKit.fbx")
    return parser.parse_args(values)


def main():
    if not bpy.app.background:
        raise RuntimeError("Run only in background Blender; the generator starts a fresh authoring scene.")
    args = arguments()
    args.source_dir = args.source_dir.resolve()
    args.unity_fbx = args.unity_fbx.resolve()
    args.source_dir.mkdir(parents=True, exist_ok=True)
    args.unity_fbx.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if addon_utils.enable("io_scene_fbx", default_set=False, persistent=False) is None:
        raise RuntimeError("Bundled FBX exporter is unavailable")
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    scene.unit_settings.length_unit = "METERS"
    collection = bpy.data.collections.new("TG Meadow Kit - editable species and authored LODs")
    scene.collection.children.link(collection)
    mats = {name: helper.material(name, color) for name, color in {
        "Grass": (.31, .55, .12), "White": (.94, .93, .78),
        "Gold": (.94, .65, .10), "Purple": (.48, .20, .62),
        "AxisX": (.85, .06, .02), "AxisZ": (.03, .24, .75), "AxisLabel": (.92, .92, .86)}.items()}
    objects = []
    metadata = {}
    species_list = ("ShortGrass", "MeadowGrass", "TallGrass", "AccentGrass", "Clover", "DaisyWhite", "FlowerGold", "FlowerPurple")
    for species_index, species in enumerate(species_list):
        for lod in (0, 1):
            name = "SM_" + species + "_LOD" + str(lod)
            if species.endswith("Grass"):
                geo = grass_species(args.seed, species, lod)
                slots = [mats["Grass"]]
            elif species == "Clover":
                geo = clover(args.seed, lod)
                slots = [mats["Grass"]]
            else:
                geo = flower_species(args.seed, species, lod)
                slots = [mats["Grass"], mats["Gold"]] if species == "FlowerGold" else [mats["Grass"], mats["White" if species == "DaisyWhite" else "Purple"], mats["Gold"]]
            # Keep the authored clumps inside the requested 0.3..0.6 m radius.
            footprint = max(math.hypot(point[0], point[1]) for point in geo.vertices)
            if not species.endswith("Grass") and (footprint > .6 or footprint < .3):
                scale_xy = max(.3, min(.6, footprint)) / footprint
                geo.vertices = [(x * scale_xy, y * scale_xy, z) for x, y, z in geo.vertices]
                geo.normals = [tuple(unit((n[0] / scale_xy, n[1] / scale_xy, n[2]))) if n is not None else None for n in geo.normals]
            # Bent stem ring normals can put a root corner a fraction below Z0;
            # establish the common contact plane before writing final mesh data.
            geo.ground()
            obj = geo.object(name, slots, collection, "R=height/bend 0 roots to 1 tips; G=0; B=stable element phase; A=1 numerical data", args.seed)
            obj.location = (species_index * 1.7, lod * 2.2, 0)
            obj["TG_Species"] = species
            obj["TG_LOD"] = lod
            obj["TG_Role"] = "Short readable combat edge" if species == "ShortGrass" else "Meadow accent" if species.startswith("Flower") or species == "DaisyWhite" else "Border foliage"
            objects.append(obj)
            metadata[name] = {"species": species, "lod": lod, "role": obj["TG_Role"], "collision": False,
                              "alpha_bitmap_required": False, "material_slot_roles": [slot.name for slot in slots],
                              "source_blade_indices": getattr(geo, "meadow_source_blade_indices", []),
                              "lod_strategy": "Exact stable subset of full LOD0 blade records; tallest/outermost retained; shared footprint scale; fewer strip rows" if species.endswith("Grass") else "Reduced flower/stem or clover count"}
    marker = helper.axis_marker(args.seed).object("SM_MeadowAxisMarker", [mats["AxisX"], mats["AxisZ"], mats["AxisLabel"]], collection,
        "R=0; G=0; B=0; A=1; raised X/Z labels", args.seed)
    marker.location = (0, -2.2, 0)
    objects.append(marker)
    metadata[marker.name] = {"species": "AxisMarker", "lod": -1, "role": "Import units/orientation proof only; omit from stage", "collision": False}

    # Source endpoint proof: every retained LOD1 grass root and tip has the same
    # position and phase as its LOD0 record. Intermediate strip rows may differ.
    object_by_name = {obj.name: obj for obj in objects}
    for species in species_list[:4]:
        endpoints = []
        for lod in (0, 1):
            mesh = object_by_name["SM_" + species + "_LOD" + str(lod)].data
            color_data = mesh.color_attributes[helper.MASK_NAME].data
            endpoints.append({tuple(round(component, 6) for component in (*mesh.vertices[loop.vertex_index].co, color_data[index].color[0], color_data[index].color[2]))
                              for index, loop in enumerate(mesh.loops)
                              if color_data[index].color[0] < .00001 or color_data[index].color[0] > .99999})
        if not endpoints[1].issubset(endpoints[0]):
            raise RuntimeError(species + " LOD1 moved a source grass root/tip or changed its phase")

    conversion = axis_conversion(to_forward="-Z", to_up="Y").to_3x3()
    descriptions = []
    for obj in objects:
        description = helper.object_manifest(obj, conversion)
        description.update(metadata[obj.name])
        description["expected_unity_bounds_metres"] = helper.bounds([(v.co.x, v.co.z, v.co.y) for v in obj.data.vertices])
        description["footprint_radius_metres"] = max(math.hypot(v.co.x, v.co.y) for v in obj.data.vertices)
        description["expected_unity_normal_min"] = [min((n.vector.x, n.vector.z, n.vector.y)[axis] for n in obj.data.corner_normals) for axis in range(3)]
        description["expected_unity_normal_max"] = [max((n.vector.x, n.vector.z, n.vector.y)[axis] for n in obj.data.corner_normals) for axis in range(3)]
        if abs(description["bounds_blender_local_metres"]["min"][2]) > .00001:
            raise RuntimeError(obj.name + " is not grounded at native Z0")
        if not description["custom_split_normals"]:
            raise RuntimeError(obj.name + " is missing custom normals")
        descriptions.append(description)

    source_fbx = args.source_dir / "TG_MeadowKit.fbx"
    blend = args.source_dir / "TG_MeadowKit.blend"
    manifest_path = args.source_dir / "TG_MeadowKit.manifest.json"
    locations = [obj.location.copy() for obj in objects]
    try:
        bpy.ops.object.select_all(action="DESELECT")
        for obj in objects:
            obj.location = (0, 0, 0)
            obj.rotation_euler = (0, 0, 0)
            obj.scale = (1, 1, 1)
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        bpy.ops.export_scene.fbx(filepath=str(source_fbx), use_selection=True, object_types={"MESH"},
            global_scale=1, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
            axis_forward="-Z", axis_up="Y", use_space_transform=True, bake_space_transform=True,
            use_mesh_modifiers=False, use_triangles=False, mesh_smooth_type="OFF", use_tspace=True,
            colors_type="LINEAR", prioritize_active_color=True, use_custom_props=True,
            bake_anim=False, path_mode="AUTO", embed_textures=False)
    finally:
        for obj, location in zip(objects, locations):
            obj.location = location
    shutil.copy2(source_fbx, args.unity_fbx)
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), compress=True)
    manifest = {
        "schema": "TG_WP04_MeadowKit_v1", "seed": args.seed, "blender_version": bpy.app.version_string,
        "units": "metres", "source_up": "+Z", "generator": str(Path(__file__).resolve()),
        "generator_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        "geometry_helper": str(HELPER_PATH), "geometry_helper_sha256": hashlib.sha256(HELPER_PATH.read_bytes()).hexdigest(),
        "source_blend": str(blend), "source_fbx": str(source_fbx), "unity_fbx": str(args.unity_fbx),
        "fbx_sha256": hashlib.sha256(source_fbx.read_bytes()).hexdigest(),
        "export": {"scale": 1, "axis_forward": "-Z", "axis_up": "Y", "bake_space_transform": True,
            "triangulation_stage": "Explicit loop triangles before custom split normals through WP02 Geometry.object",
            "use_triangles": False, "use_mesh_modifiers": False, "colors_type": "LINEAR", "UV0": True,
            "custom_normals": True, "mesh_objects_only": True},
        "coordinates": "Author Blender Z-up, root Z0. Proven WP02 raw imported Unity coordinates=(BlenderX,BlenderZ,BlenderY). No prefab-node transform dependency; all export inputs identity.",
        "unity_import_required": {"globalScale": 1, "bakeAxisConversion": True, "normals": "Import", "meshCompression": "Off", "readWriteForValidation": True,
            "material_remap": "Grass->shared Grass, White/Gold/Purple->shared Flower family with matching petal tint; Axis slots diagnostic only"},
        "mask_contract": "R height/bend0root..1tip; G0; B stable per-element phase; A1. Numerical LINEAR data, not color; A is AO not opacity.",
        "normals": "Grass and leaf-like polygons have deliberate upward-biased custom normals; stems use tube normals. Triangulate before authoring, never recompute at import.",
        "grass_lod_source_endpoint_subset_verified": True,
        "authoring_layout": "Species in X columns, LOD0/LOD1 in Y rows for editable inspection; export restores every origin to zero and imports independently.",
        "starting_values_and_test": "Blade heights short.15/meadow.55/tall.85/accent1.15m and widths~.045-.085m are starting values. Test matched battle and attack cameras; lower height/density if fighters obscure, widen or adjust density if coverage breaks, tune LOD transition by captured silhouette stability.",
        "limitations": "Geometry kit only; Unity import/shading/wind/mips/LOD thresholds/distribution/camera and performance validation remain pending. No grass-card alpha texture, vegetation collision, trees or water assets are included.",
        "objects": descriptions, "mesh_count": len(objects),
        "total_expected_triangles": sum(d["expected_triangles_after_export"] for d in descriptions),
        "lod0_expected_triangles": sum(d["expected_triangles_after_export"] for d in descriptions if d["lod"] == 0),
        "lod1_expected_triangles": sum(d["expected_triangles_after_export"] for d in descriptions if d["lod"] == 1),
    }
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    shutil.copy2(manifest_path, args.unity_fbx.with_suffix(".manifest.json"))
    print(json.dumps({"status": "generated", "objects": len(objects), "triangles": manifest["total_expected_triangles"],
                      "lod0_triangles": manifest["lod0_expected_triangles"], "lod1_triangles": manifest["lod1_expected_triangles"],
                      "blend": str(blend), "unity_fbx": str(args.unity_fbx), "manifest": str(manifest_path)}))


if __name__ == "__main__":
    main()
