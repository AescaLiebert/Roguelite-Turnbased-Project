"""Create editable WP02 sample meshes with Blender 5.1, without touching Unity settings.

Run only in a background Blender process, for example:
  blender.exe --background --factory-startup --python create_material_samples.py -- \
      --unity-fbx ".../Assets/Project/Art/Environment/TeyvatGrassland/Shared/Models/TG_MaterialSamples.fbx"

The optional first positional argument is also accepted as the Unity FBX destination.
Outputs next to this source (or --source-dir): TG_MaterialSamples.blend, .fbx and
.manifest.json. The blend arranges the six objects on an authoring grid. Export
temporarily places every object's ground/attachment origin at zero, so each mesh
can be extracted and placed independently by Unity. Geometry is authored in metres.

RGBA is numerical wind data, not visible albedo. CORNER/FLOAT_COLOR values are
exported with colors_type=LINEAR; SRGB would gamma-transform the mask numbers.
The manifest records source counts, bounds, masks and axis landmarks for the
required Unity round-trip check. No Unity import or rendering acceptance is claimed.
"""

from __future__ import annotations

import argparse
import hashlib
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


DEFAULT_SEED = 20261018
MASK_NAME = "TG_WindRGBA"
SOURCE_DIR = Path(__file__).resolve().parent
EXPECTED_NAMES = (
    "SM_GrassClump", "SM_LeafCluster", "SM_Trunk",
    "SM_FlowerCluster", "SM_Cliff", "SM_AxisMarker",
)


def unit(value):
    vector = Vector(value)
    return vector.normalized() if vector.length_squared > 1e-12 else Vector((0, 0, 1))


def rgba(red=0.0, green=0.0, blue=0.0):
    return (max(0.0, min(1.0, red)), max(0.0, min(1.0, green)),
            max(0.0, min(1.0, blue)), 1.0)


class Geometry:
    """Own vertex/loop data explicitly; no context-dependent modeling operators."""

    def __init__(self):
        self.vertices = []
        self.colors = []
        self.normals = []
        self.faces = []
        self.face_uvs = []
        self.face_materials = []
        self.face_smooth = []

    def vertex(self, position, color=(0, 0, 0, 1), normal=None):
        index = len(self.vertices)
        self.vertices.append(tuple(float(component) for component in position))
        self.colors.append(tuple(float(component) for component in color))
        self.normals.append(tuple(unit(normal)) if normal is not None else None)
        return index

    def face(self, indices, uvs=None, material=0, smooth=False):
        indices = tuple(indices)
        if len(set(indices)) != len(indices):
            raise ValueError("A generated face contains repeated vertex indices")
        if uvs is None:
            a, b, c = (Vector(self.vertices[index]) for index in indices[:3])
            normal = unit((b - a).cross(c - a))
            axis = max(range(3), key=lambda component: abs(normal[component]))
            axes = (1, 2) if axis == 0 else (0, 2) if axis == 1 else (0, 1)
            uvs = [(self.vertices[index][axes[0]], self.vertices[index][axes[1]])
                   for index in indices]
        self.faces.append(indices)
        self.face_uvs.append(tuple(uvs))
        self.face_materials.append(material)
        self.face_smooth.append(smooth)

    def ground(self):
        minimum = min(vertex[2] for vertex in self.vertices)
        self.vertices = [(x, y, z - minimum) for x, y, z in self.vertices]

    def object(self, name, materials, collection, mask_description, seed):
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(self.vertices, [], self.faces)
        mesh.update(calc_edges=True)
        if len(mesh.polygons) != len(self.faces):
            raise RuntimeError(f"Unexpected topology change in {name}")
        # Triangulate before authoring split normals. The bundled FBX exporter's
        # use_triangles option round-trips the mesh through BMesh after this point,
        # which drops the authored custom normals. Preserve original polygon UVs,
        # slots and broad face normals while constructing final triangles here.
        mesh.calc_loop_triangles()
        triangles, triangle_uvs, triangle_materials = [], [], []
        triangle_smooth, triangle_normals = [], []
        for triangle in mesh.loop_triangles:
            polygon_index = triangle.polygon_index
            polygon = mesh.polygons[polygon_index]
            triangles.append(tuple(triangle.vertices))
            triangle_uvs.append(tuple(self.face_uvs[polygon_index][loop - polygon.loop_start]
                                       for loop in triangle.loops))
            triangle_materials.append(self.face_materials[polygon_index])
            triangle_smooth.append(self.face_smooth[polygon_index])
            triangle_normals.append(tuple(self.normals[vertex] if self.normals[vertex] is not None
                                          else tuple(polygon.normal) for vertex in triangle.vertices))
        mesh.clear_geometry()
        mesh.from_pydata(self.vertices, [], triangles)
        mesh.update(calc_edges=True)
        for material in materials:
            mesh.materials.append(material)
        uv_layer = mesh.uv_layers.new(name="UV0")
        color_layer = mesh.color_attributes.new(name=MASK_NAME, type="FLOAT_COLOR", domain="CORNER")
        custom_normals = []
        for polygon, uvs, material, smooth, authored_normals in zip(
                mesh.polygons, triangle_uvs, triangle_materials, triangle_smooth, triangle_normals):
            polygon.material_index = material
            polygon.use_smooth = smooth
            for loop_index, uv, normal in zip(polygon.loop_indices, uvs, authored_normals):
                vertex_index = mesh.loops[loop_index].vertex_index
                uv_layer.data[loop_index].uv = uv
                color_layer.data[loop_index].color = self.colors[vertex_index]
                custom_normals.append(normal)
        mesh.color_attributes.active_color_index = 0
        mesh.color_attributes.render_color_index = 0
        # Blender 5.1's own FBX importer uses this API. No removed use_auto_smooth flag.
        mesh.normals_split_custom_set(custom_normals)
        mesh.update()
        mesh.calc_loop_triangles()
        obj = bpy.data.objects.new(name, mesh)
        collection.objects.link(obj)
        obj["TG_Seed"] = seed
        obj["TG_SourcePolygonsBeforeTriangulation"] = len(self.faces)
        obj["TG_Masks"] = mask_description
        obj["TG_Units"] = "metres; local Z up; ground/attachment origin at (0,0,0)"
        obj.color = materials[0].diffuse_color
        return obj


def material(name, color, roughness=0.8):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1.0)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get("Principled BSDF")
    shader.inputs["Base Color"].default_value = (*color, 1.0)
    shader.inputs["Roughness"].default_value = roughness
    shader.inputs["Metallic"].default_value = 0.0
    return mat


def add_tube(geometry, points, radii, material_index=0, phase=0.0,
             red_from_height=False, sides=8):
    points = [Vector(point) for point in points]
    rings = []
    distances = [0.0]
    for previous, current in zip(points, points[1:]):
        distances.append(distances[-1] + (current - previous).length)
    for index, (point, radius) in enumerate(zip(points, radii)):
        tangent = unit(points[min(index + 1, len(points) - 1)] - points[max(0, index - 1)])
        reference = Vector((0, 1, 0)) if abs(tangent.y) < 0.92 else Vector((1, 0, 0))
        right = unit(tangent.cross(reference))
        across = unit(tangent.cross(right))
        ring = []
        for side in range(sides):
            angle = side / sides * math.tau
            normal = right * math.cos(angle) + across * math.sin(angle)
            red = point.z / max(points[-1].z, 0.01) if red_from_height else 0.0
            ring.append(geometry.vertex(point + normal * radius, rgba(red, 0, phase), normal))
        rings.append(ring)
    for ring_index in range(len(rings) - 1):
        for side in range(sides):
            following = (side + 1) % sides
            u0, u1 = side / sides, (side + 1) / sides
            v0, v1 = distances[ring_index], distances[ring_index + 1]
            geometry.face((rings[ring_index][side], rings[ring_index][following],
                           rings[ring_index + 1][following], rings[ring_index + 1][side]),
                          ((u0, v0), (u1, v0), (u1, v1), (u0, v1)), material_index, True)
    # Separate cap vertices preserve hard end normals, without altering side normals.
    for ring_index, reverse in ((0, True), (len(rings) - 1, False)):
        center = geometry.vertex(points[ring_index], rgba(0 if reverse else (1 if red_from_height else 0), 0, phase))
        cap = [geometry.vertex(geometry.vertices[index], geometry.colors[index]) for index in rings[ring_index]]
        for side in range(sides):
            following = (side + 1) % sides
            indices = (center, cap[following], cap[side]) if reverse else (center, cap[side], cap[following])
            geometry.face(indices, material=material_index)


def add_blade(geometry, base, angle, height, width, lean, phase, material_index=0, segments=6):
    base = Vector(base)
    across = Vector((math.cos(angle), math.sin(angle), 0))
    bend = Vector((-math.sin(angle), math.cos(angle), 0))
    normal = unit(bend * 0.65 + Vector((0, 0, 0.76)))
    rings = []
    for row in range(segments):
        t = row / segments
        center = base + Vector((0, 0, height * t)) + bend * (lean * t * t)
        half_width = width * 0.5 * (1 - t) ** 0.65 * (0.65 + 0.35 * math.sin(t * math.pi))
        ring = []
        for column in range(3):
            point = center + across * ((column - 1) * half_width)
            if column == 1:
                point += bend * (0.012 * math.sin(t * math.pi))
            ring.append(geometry.vertex(point, rgba(t, 0, phase), normal))
        rings.append(ring)
    for row in range(segments - 1):
        for column in range(2):
            a, b = rings[row][column:column + 2]
            d, c = rings[row + 1][column:column + 2]
            u0, u1 = column / 2, (column + 1) / 2
            v0, v1 = row / segments, (row + 1) / segments
            geometry.face((a, b, c, d), ((u0, v0), (u1, v0), (u1, v1), (u0, v1)), material_index, True)
    tip = geometry.vertex(base + Vector((0, 0, height)) + bend * lean, rgba(1, 0, phase), normal)
    for column in range(2):
        geometry.face((rings[-1][column], rings[-1][column + 1], tip),
                      ((column / 2, (segments - 1) / segments),
                       ((column + 1) / 2, (segments - 1) / segments), (0.5, 1)), material_index, True)


def grass(seed):
    rng = random.Random(seed + 11)
    geometry = Geometry()
    for blade in range(10):
        angle = blade / 10 * math.tau + rng.uniform(-0.28, 0.28)
        radius = 0.06 + rng.random() * 0.17
        base = (math.cos(angle) * radius, math.sin(angle) * radius, 0)
        add_blade(geometry, base, angle + rng.uniform(-0.8, 0.8),
                  rng.uniform(0.38, 0.68), rng.uniform(0.045, 0.078),
                  rng.uniform(0.08, 0.24), rng.random())
    return geometry


def add_leaf(geometry, center, direction, normal, length, width, branch_weight, phase, material_index=0):
    center, normal = Vector(center), unit(normal)
    direction = unit(Vector(direction) - normal * Vector(direction).dot(normal))
    across = unit(normal.cross(direction))
    points = (center - direction * length * 0.5, center + across * width * 0.5,
              center + direction * length * 0.5, center - across * width * 0.5,
              center + normal * length * 0.055)
    weights = (0.2, 0.65, 1.0, 0.65, 0.6)
    indices = [geometry.vertex(point, rgba(red, branch_weight, phase), normal)
               for point, red in zip(points, weights)]
    uvs = ((0, 0.5), (0.5, 1), (1, 0.5), (0.5, 0), (0.5, 0.5))
    for side in range(4):
        following = (side + 1) % 4
        # Diamond order above is clockwise around the outward normal.
        geometry.face((indices[4], indices[following], indices[side]),
                      (uvs[4], uvs[following], uvs[side]), material_index, True)


def leaf_cluster(seed):
    """252 fine leaves in 84 coherent three-leaf sprays around seven soft lobes."""
    rng = random.Random(seed + 23)
    geometry = Geometry()
    lobes = [(Vector((0, 0, 0.40)), Vector((0.37, 0.31, 0.31)))]
    for lobe in range(6):
        angle = lobe / 6 * math.tau + 0.2
        lobes.append((Vector((math.cos(angle) * 0.29, math.sin(angle) * 0.24,
                              0.28 + (lobe % 3) * 0.065)), Vector((0.29, 0.25, 0.25))))
    for center, radius in lobes:
        for spray in range(12):
            height = 1 - 2 * (spray + 0.5) / 12
            angle = spray * 2.399963229728653 + rng.uniform(-0.3, 0.3)
            horizontal = math.sqrt(max(0, 1 - height * height))
            outward = Vector((math.cos(angle) * horizontal, math.sin(angle) * horizontal, height))
            position = center + Vector((outward.x * radius.x, outward.y * radius.y,
                                        outward.z * radius.z)) * rng.uniform(0.83, 1.06)
            # The full spray shares a gently outward/up-biased normal field.
            normal = unit((outward.x * 0.78, outward.y * 0.78, outward.z * 0.40 + 0.64))
            tangent = unit(normal.cross(Vector((0, 1, 0)) if abs(normal.y) < 0.9 else Vector((1, 0, 0))))
            bitangent = unit(normal.cross(tangent))
            phase = rng.random()
            branch_weight = 0.1 + 0.7 * ((height + 1) * 0.5)
            spin = rng.random() * math.tau
            for leaflet in range(3):
                leaf_angle = spin + (leaflet - 1) * 1.08
                direction = tangent * math.cos(leaf_angle) + bitangent * math.sin(leaf_angle)
                leaf_center = position + direction * (0.030 + leaflet * 0.008)
                add_leaf(geometry, leaf_center, direction, normal, rng.uniform(0.10, 0.16),
                         rng.uniform(0.045, 0.075), branch_weight, phase)
    geometry.ground()
    return geometry


def trunk(seed):
    geometry = Geometry()
    add_tube(geometry, ((0, 0, 0), (-0.045, 0.01, 0.38), (0.018, 0.035, 0.86),
                        (0.07, 0.055, 1.24), (0.04, 0.10, 1.48)),
             (0.105, 0.092, 0.067, 0.046, 0.025), sides=9)
    add_tube(geometry, ((0.0, 0.02, 0.66), (-0.23, 0.015, 0.92), (-0.46, 0.08, 1.18)),
             (0.059, 0.038, 0.015), sides=8)
    add_tube(geometry, ((0.045, 0.045, 0.99), (0.28, 0.03, 1.20), (0.43, -0.08, 1.37)),
             (0.045, 0.029, 0.011), sides=8)
    add_tube(geometry, ((0.025, 0.05, 1.10), (-0.02, 0.31, 1.32), (-0.10, 0.42, 1.43)),
             (0.036, 0.024, 0.010), sides=7)
    # Tubes join through overlapping branch bases; no hidden preview modifiers.
    geometry.ground()
    return geometry


def add_petal(geometry, center, angle, phase):
    direction = Vector((math.cos(angle), math.sin(angle), 0))
    across = Vector((-direction.y, direction.x, 0))
    center = Vector(center) + direction * 0.058
    crown = geometry.vertex(center + Vector((0, 0, 0.016)), rgba(1, 0, phase), (0, 0, 1))
    edge = []
    for side in range(10):
        a = side / 10 * math.tau
        point = center + direction * (math.cos(a) * 0.067) + across * (math.sin(a) * 0.031)
        point.z += 0.004 + 0.010 * math.cos(a)
        edge.append(geometry.vertex(point, rgba(1, 0, phase), (0, 0, 1)))
    for side in range(10):
        following = (side + 1) % 10
        a, b = side / 10 * math.tau, following / 10 * math.tau
        geometry.face((crown, edge[side], edge[following]),
                      ((0.5, 0.5), (0.5 + 0.5 * math.cos(a), 0.5 + 0.5 * math.sin(a)),
                       (0.5 + 0.5 * math.cos(b), 0.5 + 0.5 * math.sin(b))), 1, True)


def flowers(seed):
    rng = random.Random(seed + 37)
    geometry = Geometry()
    bases = ((-0.18, -0.07), (0.08, -0.14), (0.20, 0.10), (-0.07, 0.19), (0.0, 0.015))
    for flower, (x, y) in enumerate(bases):
        height, phase = rng.uniform(0.34, 0.54), rng.random()
        head = Vector((x + rng.uniform(-0.025, 0.025), y + rng.uniform(-0.025, 0.025), height))
        add_tube(geometry, ((x, y, 0), (x - 0.015, y + 0.015, height * 0.56), head),
                 (0.009, 0.006, 0.004), phase=phase, red_from_height=True, sides=5)
        for leaf in range(2):
            angle = flower * 1.9 + leaf * 2.5
            root = Vector((x, y, height * (0.32 + leaf * 0.17)))
            direction = unit((math.cos(angle), math.sin(angle), 0.45))
            normal = unit((-direction.x * 0.25, -direction.y * 0.25, 1))
            add_leaf(geometry, root + direction * 0.06, direction, normal, 0.15, 0.045, 0, phase)
        for petal in range(6):
            add_petal(geometry, head, petal / 6 * math.tau + flower * 0.35, phase)
        disk_center = geometry.vertex(head + Vector((0, 0, 0.021)), rgba(1, 0, phase), (0, 0, 1))
        ring = [geometry.vertex(head + Vector((math.cos(side / 10 * math.tau) * 0.026,
                                               math.sin(side / 10 * math.tau) * 0.026, 0.014)),
                                rgba(1, 0, phase), (0, 0, 1)) for side in range(10)]
        for side in range(10):
            geometry.face((disk_center, ring[side], ring[(side + 1) % 10]), material=2, smooth=True)
    geometry.ground()
    return geometry


def cliff(seed):
    geometry = Geometry()
    profile = ((-1.13, -0.55), (-0.35, -0.72), (0.62, -0.64), (1.14, -0.29),
               (1.02, 0.49), (0.28, 0.67), (-0.72, 0.61), (-1.17, 0.15))
    rings = []
    for row in range(3):
        ring = []
        for side, (x, y) in enumerate(profile):
            if row == 0:
                point = (x, y, 0)
            elif row == 1:
                point = (x * 0.96 + 0.055, y * 0.97 - 0.035,
                         1.04 + (0.10 if side in (1, 2, 6) else -0.04))
            else:
                point = (x * 0.85 - 0.06, y * 0.88 + 0.05,
                         2.12 + (0.13 if side in (0, 1, 7) else -0.07))
            ring.append(geometry.vertex(point, rgba()))
        rings.append(ring)
    for row in range(2):
        for side in range(8):
            following = (side + 1) % 8
            geometry.face((rings[row][side], rings[row][following], rings[row + 1][following], rings[row + 1][side]))
    geometry.face(tuple(reversed(rings[0])))
    geometry.face(tuple(rings[-1]))
    return geometry


def add_box(geometry, minimum, maximum, material_index=0):
    x0, y0, z0 = minimum
    x1, y1, z1 = maximum
    vertices = [geometry.vertex(point, rgba()) for point in (
        (x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
        (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1))]
    for face in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
                 (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        geometry.face(tuple(vertices[index] for index in face), material=material_index)


def add_stroke(geometry, start, end, material_index=2):
    start, end = Vector((*start, 0.085)), Vector((*end, 0.085))
    across = unit((-(end - start).y, (end - start).x, 0)) * 0.008
    lower = (start - across, end - across, end + across, start + across)
    indices = [geometry.vertex(point, rgba()) for point in lower]
    indices += [geometry.vertex(point + Vector((0, 0, 0.017)), rgba()) for point in lower]
    for face in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
                 (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)):
        geometry.face(tuple(indices[index] for index in face), material=material_index)


def axis_marker(seed):
    geometry = Geometry()
    # Native Blender -Y is the labelled forward arm; the FBX -Z/Y export basis
    # maps it to +Z before Unity's handedness/import conversion is checked.
    add_box(geometry, (0, -0.10, 0), (2, 0, 0.08), 0)
    add_box(geometry, (0, -1, 0), (0.10, 0, 0.08), 1)
    # Raised X and Z strokes make the two axis labels survive mesh-only export.
    add_stroke(geometry, (1.77, -0.083), (1.84, -0.017))
    add_stroke(geometry, (1.77, -0.017), (1.84, -0.083))
    add_stroke(geometry, (0.018, -0.80), (0.082, -0.80))
    add_stroke(geometry, (0.082, -0.80), (0.018, -0.88))
    add_stroke(geometry, (0.018, -0.88), (0.082, -0.88))
    return geometry


def bounds(points):
    minimum = [min(point[axis] for point in points) for axis in range(3)]
    maximum = [max(point[axis] for point in points) for axis in range(3)]
    return {"min": [round(value, 8) for value in minimum],
            "max": [round(value, 8) for value in maximum],
            "size": [round(high - low, 8) for low, high in zip(minimum, maximum)]}


def object_manifest(obj, conversion):
    mesh = obj.data
    mesh.calc_loop_triangles()
    positions = [tuple(vertex.co) for vertex in mesh.vertices]
    colors = [tuple(entry.color) for entry in mesh.color_attributes[MASK_NAME].data]
    normal_values = [tuple(entry.vector) for entry in mesh.corner_normals]
    uv_values = [tuple(entry.uv) for entry in mesh.uv_layers[0].data]
    color_min = [round(min(value[channel] for value in colors), 8) for channel in range(4)]
    color_max = [round(max(value[channel] for value in colors), 8) for channel in range(4)]
    return {
        "name": obj.name, "mesh_name": mesh.name,
        "vertices_before_fbx_seam_splitting": len(mesh.vertices),
        "polygons_before_triangulation": obj["TG_SourcePolygonsBeforeTriangulation"],
        "polygons_in_authored_triangulated_mesh": len(mesh.polygons),
        "expected_triangles_after_export": len(mesh.loop_triangles), "loops": len(mesh.loops),
        "bounds_blender_local_metres": bounds(positions),
        "bounds_export_axis_basis_metres": bounds([tuple(conversion @ Vector(point)) for point in positions]),
        "source_preview_location": list(obj.location),
        "fbx_export_input_transform": {"location": [0, 0, 0], "rotation_euler": [0, 0, 0], "scale": [1, 1, 1]},
        "uv0": {"name": "UV0", "min": [min(uv[axis] for uv in uv_values) for axis in range(2)],
                "max": [max(uv[axis] for uv in uv_values) for axis in range(2)]},
        "vertex_attribute": {"name": MASK_NAME, "domain": "CORNER", "type": "FLOAT_COLOR",
                             "rgba_min": color_min, "rgba_max": color_max, "meaning": obj["TG_Masks"]},
        "custom_split_normals": bool(mesh.has_custom_normals),
        "normal_component_min": [min(value[axis] for value in normal_values) for axis in range(3)],
        "normal_component_max": [max(value[axis] for value in normal_values) for axis in range(3)],
        "material_slots": [slot.name for slot in mesh.materials],
    }


def parse_arguments():
    arguments = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("unity_fbx", nargs="?", help="Optional destination FBX path inside Unity")
    parser.add_argument("--unity-fbx", dest="unity_fbx_option")
    parser.add_argument("--source-dir", type=Path, default=SOURCE_DIR)
    parser.add_argument("--seed", type=int, default=DEFAULT_SEED)
    args = parser.parse_args(arguments)
    default_target = SOURCE_DIR.parents[2] / "Assets/Project/Art/Environment/TeyvatGrassland/Shared/Models/TG_MaterialSamples.fbx"
    args.unity_fbx = Path(args.unity_fbx_option or args.unity_fbx or default_target).resolve()
    args.source_dir = args.source_dir.resolve()
    return args


def main():
    if not bpy.app.background:
        raise RuntimeError("Run this generator with Blender --background; it creates a fresh authoring scene.")
    if bpy.app.version < (4, 2, 0):
        raise RuntimeError("This generator requires Blender 4.2 or newer; the installed target is Blender 5.1.1.")
    args = parse_arguments()
    args.source_dir.mkdir(parents=True, exist_ok=True)
    args.unity_fbx.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    if addon_utils.enable("io_scene_fbx", default_set=False, persistent=False) is None:
        raise RuntimeError("Blender's bundled io_scene_fbx exporter is unavailable")
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.unit_settings.length_unit = "METERS"
    collection = bpy.data.collections.new("TG Material Samples - editable source geometry")
    scene.collection.children.link(collection)
    mats = {
        "grass": material("M_TG_GrassPreview", (0.31, 0.58, 0.12)),
        "leaf": material("M_TG_LeafPreview", (0.25, 0.49, 0.13)),
        "trunk": material("M_TG_TrunkPreview", (0.28, 0.17, 0.095)),
        "white": material("M_TG_FlowerWhite", (0.94, 0.91, 0.74)),
        "gold": material("M_TG_FlowerGold", (0.86, 0.55, 0.08)),
        "cliff": material("M_TG_CliffBlueGray", (0.43, 0.51, 0.58)),
        "axis_x": material("M_TG_AxisX", (0.86, 0.045, 0.025)),
        "axis_z": material("M_TG_AxisZ", (0.025, 0.22, 0.78)),
        "axis_label": material("M_TG_AxisLabels", (0.92, 0.92, 0.86)),
    }
    specifications = (
        ("SM_GrassClump", grass, [mats["grass"]], (-2.3, -1.3, 0),
         "R=bend 0 at roots/1 at tips; G=0; B=stable blade phase; A=1"),
        ("SM_LeafCluster", leaf_cluster, [mats["leaf"]], (0, -1.3, 0),
         "R=leaf flutter 0.2..1; G=branch sway within 0.1..0.8; B=stable spray phase; A=1"),
        ("SM_Trunk", trunk, [mats["trunk"]], (2.0, -1.3, 0),
         "R=0; G=0; B=0; A=1; rigid branching trunk"),
        ("SM_FlowerCluster", flowers, [mats["leaf"], mats["white"], mats["gold"]], (-2.3, 1.1, 0),
         "R=stem bend/root 0 and flower tips 1; G=0; B=stable flower phase; A=1"),
        ("SM_Cliff", cliff, [mats["cliff"]], (0.2, 1.4, 0),
         "R=0; G=0; B=0; A=1; stone masks unused"),
        ("SM_AxisMarker", axis_marker, [mats["axis_x"], mats["axis_z"], mats["axis_label"]], (2.6, 1.2, 0),
         "R=0; G=0; B=0; A=1; axis labels use material slots"),
    )
    objects = []
    for name, factory, materials, preview_location, mask_description in specifications:
        obj = factory(args.seed).object(name, materials, collection, mask_description, args.seed)
        obj.location = preview_location
        objects.append(obj)
    if tuple(obj.name for obj in objects) != EXPECTED_NAMES:
        raise RuntimeError("Generated object names differ from the import contract")
    conversion = axis_conversion(to_forward="-Z", to_up="Y").to_3x3()
    descriptions = [object_manifest(obj, conversion) for obj in objects]
    for description in descriptions:
        if abs(description["bounds_blender_local_metres"]["min"][2]) > 1e-5:
            raise RuntimeError(f"Ground/attachment origin mismatch in {description['name']}")
        if not description["custom_split_normals"]:
            raise RuntimeError(f"Custom normals missing in {description['name']}")
    source_fbx = args.source_dir / "TG_MaterialSamples.fbx"
    blend_path = args.source_dir / "TG_MaterialSamples.blend"
    manifest_path = args.source_dir / "TG_MaterialSamples.manifest.json"
    preview_locations = [obj.location.copy() for obj in objects]
    try:
        bpy.ops.object.select_all(action="DESELECT")
        for obj in objects:
            obj.location = (0, 0, 0)
            obj.rotation_euler = (0, 0, 0)
            obj.scale = (1, 1, 1)
            obj.select_set(True)
        bpy.context.view_layer.objects.active = objects[0]
        # All input transforms are already identity. Avoid another mesh operation
        # after split-normal authoring; axis conversion is baked by the exporter.
        bpy.ops.export_scene.fbx(
            filepath=str(source_fbx), use_selection=True, object_types={"MESH"},
            global_scale=1.0, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
            # Static mesh-only export: bake conversion into raw mesh data because the
            # Unity lab extracts Mesh subassets without the FBX prefab node transforms.
            axis_forward="-Z", axis_up="Y", use_space_transform=True, bake_space_transform=True,
            use_mesh_modifiers=False, use_triangles=False, mesh_smooth_type="OFF", use_tspace=True,
            colors_type="LINEAR", prioritize_active_color=True,
            use_custom_props=True, bake_anim=False, path_mode="AUTO", embed_textures=False,
        )
    finally:
        for obj, location in zip(objects, preview_locations):
            obj.location = location
    if source_fbx.resolve() != args.unity_fbx:
        shutil.copy2(source_fbx, args.unity_fbx)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), compress=True)
    manifest = {
        "schema": "TG_WP02_MaterialSamples_v1", "seed": args.seed,
        "generator": str(Path(__file__).resolve()),
        "generator_sha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        "blender_version": bpy.app.version_string, "units": "metres", "source_up": "+Z",
        "source_blend": str(blend_path), "source_fbx": str(source_fbx), "unity_fbx": str(args.unity_fbx),
        "fbx_sha256": hashlib.sha256(source_fbx.read_bytes()).hexdigest(),
        "export": {"scale": 1.0, "apply_unit_scale": True, "apply_scale_options": "FBX_SCALE_UNITS",
                   "axis_forward": "-Z", "axis_up": "Y", "bake_space_transform": True,
                   "triangulated": True, "triangulation_stage": "Explicit loop triangles before custom split normals; exporter triangulation disabled",
                   "use_triangles": False, "use_mesh_modifiers": False,
                   "colors_type": "LINEAR", "custom_normals": True,
                   "UV0": True, "mesh_objects_only": True},
        "authoring_layout": "Source objects are arranged on a preview grid; FBX input transforms are identity and each mesh has its own ground/attachment origin. The -Z-forward/Y-up export conversion is baked into static raw mesh data so Unity can extract Mesh subassets without depending on FBX prefab-node rotations.",
        "reproducibility": "Geometry, UVs, masks, normals and counts are deterministic for this seed and generator. FBX/blend metadata timestamps may change file bytes.",
        "unity_import_required": {"globalScale": 1.0, "bakeAxisConversion": True,
                                  "normals": "Import", "tangents": "Import or deliberate matching recalculation",
                                  "readWriteForInitialVerification": True,
                                  "materials": "Remap by the recorded slot names; Blender preview materials are not final shaders"},
        "unity_verification": "Check the six mesh names, metre bounds, triangle counts, RGBA numerical ranges, normals/UVs and raised X/Z axis labels after importing. FBX/Unity can split or weld vertices at seams; compare triangle counts and geometry, not raw vertex counts alone. Axis basis/handedness must be confirmed from the marker before accepting the preset.",
        "axis_marker": {"source_X_arm_end_metres": [2, 0, 0],
                        "source_labelled_Z_arm_end_metres": [0, -1, 0],
                        "long_short_ratio": 2.0, "raised_labels": ["X", "Z"],
                        "native_bounds_metres": {"X": 2, "Y": 1, "Z_height": 0.102}},
        "objects": descriptions,
        "total_expected_triangles": sum(item["expected_triangles_after_export"] for item in descriptions),
    }
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(json.dumps({"status": "generated", "blend": str(blend_path), "fbx": str(args.unity_fbx),
                      "manifest": str(manifest_path), "objects": len(objects),
                      "triangles": manifest["total_expected_triangles"]}))


if __name__ == "__main__":
    main()
