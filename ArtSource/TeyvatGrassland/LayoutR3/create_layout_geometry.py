"""Authored WP01 revision-3 cliff/terrace/island geometry for background Blender.

The root-owned canonical layout plan and sampled heightfield are the spatial
authority. This independent generator never rewrites prior WP01/WP03 geometry.
All exported vertices already use the accepted local arena frame; Unity applies
only the saved arena origin. Grass, trees, water shaders and runtime are separate.
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
import bmesh
import bpy
from bpy_extras.io_utils import axis_conversion
from mathutils import Vector


HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]
HELPER = HERE.parent / "Shared/create_material_samples.py"
spec = importlib.util.spec_from_file_location("tg_layout_r3_geometry_helper", HELPER)
helper = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = helper
spec.loader.exec_module(helper)
SLOTS = ["Ground", "Soil", "Rock", "RockShade", "Moss"]
SEED = 20261018
OBJECTS = []
RECORDS = []


def lerp(a, b, t):
    return a + (b - a) * t


def smooth(a, b, value):
    t = max(0, min(1, (value - a) / (b - a)))
    return t * t * (3 - 2 * t)


def to_blender(point):
    return point[0], point[2], point[1]


def interpolate(points, x):
    """Linear feature interpolation keeps broad designed breaks, not random bumps."""
    if x <= points[0][0]:
        return points[0][1]
    for first, second in zip(points, points[1:]):
        if x <= second[0]:
            return lerp(first[1], second[1], (x - first[0]) / (second[0] - first[0]))
    return points[-1][1]


class Sculpt:
    def __init__(self):
        self.geometry = helper.Geometry()

    def face(self, points, slot=2, normal=None, smooth_face=False, uv_scale=.15):
        points = list(points)
        indices = [self.geometry.vertex(to_blender(p), normal=to_blender(normal) if normal is not None else None)
                   for p in points]
        self.geometry.face(indices[::-1], material=slot, smooth=smooth_face)
        # Explicit metre-scaled UV projection; shared triplanar materials can
        # replace it, but UV0 remains complete for normal export/asset inspection.
        self.geometry.face_uvs[-1] = tuple((uv[0] * uv_scale, uv[1] * uv_scale) for uv in self.geometry.face_uvs[-1])

    def grid_face(self, a, b, c, d, slot=2, alternate=False, normal=None):
        if alternate:
            self.face([a, b, d], slot, normal)
            self.face([b, c, d], slot, normal)
        else:
            self.face([a, b, c], slot, normal)
            self.face([a, c, d], slot, normal)

    def finish(self, name, role, lod, collision_for="", feature=None):
        # Recalculate only outward topology orientation before assigning custom
        # split normals. The final normals are never recalculated by FBX export.
        unique, vertices, faces = {}, [], []
        for face in self.geometry.faces:
            ids = []
            for index in face:
                key = tuple(round(c, 6) for c in self.geometry.vertices[index])
                if key not in unique:
                    unique[key] = len(vertices)
                    vertices.append(key)
                ids.append(unique[key])
            faces.append(ids)
        orientation_mesh = bpy.data.meshes.new("temporary_R3_orientation")
        orientation_mesh.from_pydata(vertices, [], faces)
        orientation_mesh.update()
        bm = bmesh.new()
        bm.from_mesh(orientation_mesh)
        bm.faces.ensure_lookup_table()
        bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
        for index, face in enumerate(self.geometry.faces):
            a, b, c = [Vector(self.geometry.vertices[k]) for k in face[:3]]
            if (b - a).cross(c - a).dot(bm.faces[index].normal) < 0:
                self.geometry.faces[index] = tuple(reversed(face))
                self.geometry.face_uvs[index] = tuple(reversed(self.geometry.face_uvs[index]))
        bm.free()
        bpy.data.meshes.remove(orientation_mesh)
        obj = self.geometry.object(name, MATERIALS, COLLECTION, "R0 G0 B0 A1; rigid authored landscape", SEED)
        obj["TG_Role"] = role
        obj["TG_LOD"] = lod
        obj["TG_CollisionFor"] = collision_for
        record = helper.object_manifest(obj, CONVERSION)
        world_vertices = [(p.co.x, p.co.z, p.co.y) for p in obj.data.vertices]
        normal_values = [(n.vector.x, n.vector.z, n.vector.y) for n in obj.data.corner_normals]
        record.update(role=role, lod=lod, collision_for=collision_for, feature=feature or "",
                      bounds_unity_local_metres=helper.bounds(world_vertices),
                      unity_normal_min=[min(n[i] for n in normal_values) for i in range(3)],
                      unity_normal_max=[max(n[i] for n in normal_values) for i in range(3)])
        edges = {}
        for poly in obj.data.polygons:
            points = [tuple(round(c, 5) for c in obj.data.vertices[index].co) for index in poly.vertices]
            for a, b in zip(points, points[1:] + points[:1]):
                key = tuple(sorted((a, b)))
                edges[key] = edges.get(key, 0) + 1
        record["welded_boundary_edges"] = sum(count == 1 for count in edges.values())
        record["welded_nonmanifold_edges"] = sum(count > 2 for count in edges.values())
        record["degenerate_triangles"] = sum(tri.area < 1e-9 for tri in obj.data.loop_triangles)
        if record["welded_boundary_edges"] or record["degenerate_triangles"]:
            raise RuntimeError(name + " contains an open boundary or degenerate triangle")
        OBJECTS.append(obj)
        RECORDS.append(record)
        return obj


def split_rock(sculpt, center, radii, seed, sides=10, moss=False):
    """Low broad rock with a slanted fracture crown and flared submerged/contact foot."""
    rng = random.Random(seed)
    levels = [(0, .82), (.18, 1), (.52, .93), (.83, .63), (1, .38)]
    rings = []
    phase = rng.uniform(-.2, .2)
    slope_x = rng.uniform(-.18, .18)
    slope_z = rng.uniform(-.13, .13)
    for level, radius in levels:
        ring = []
        for index in range(sides):
            angle = phase + index / sides * math.tau
            # Correlated corners are the same up each column: designed planes.
            factor = 1 + .1 * math.sin(index * 2.17 + seed)
            x = math.cos(angle) * radii[0] * radius * factor
            z = math.sin(angle) * radii[2] * radius * factor
            y = level * radii[1] + (x * slope_x + z * slope_z) * math.sin(level * math.pi * .5)
            ring.append((center[0] + x + level * radii[0] * .13, center[1] + y,
                         center[2] + z - level * radii[2] * .11))
        rings.append(ring)
    sculpt.face(rings[0][::-1], 2)
    for row in range(len(rings) - 1):
        for index in range(sides):
            next_index = (index + 1) % sides
            sculpt.face([rings[row][index], rings[row][next_index], rings[row + 1][next_index], rings[row + 1][index]],
                        3 if (index + seed) % 11 == 0 else 2)
    sculpt.face(rings[-1], 4 if moss else 2)


def arguments():
    values = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--plan", type=Path, default=HERE / "TG_LayoutPlan.json")
    parser.add_argument("--heightfield", type=Path, default=HERE / "TG_LayoutTerrainSource.json")
    parser.add_argument("--unity-fbx", type=Path, default=ROOT / "Assets/Project/Art/Environment/TeyvatGrassland/LayoutR3/Models/TG_LayoutGeometry.fbx")
    return parser.parse_args(values)


def make_cliff(a,b,tier,serial,lod):
    sculpt=Sculpt();count=max(2,math.ceil((b-a)/(1.6 if lod==0 else 4.5)))
    rings=[]
    for i in range(count+1):
        x=lerp(a,b,i/count);f=plan_source.front(x,tier);top=plan_source.height(x,f+4.6)
        base=plan_source.height(x,f-8) - .25
        if tier==1:base=plan_source.height(x,f-8)-.8
        span=max(3,top-base);fracture=2.4*math.sin(x*.17+tier)+.9*math.sin(x*.39)
        ledge=span*.095*math.sin(x*.105+tier*2.4)
        # Broad, correlated fracture columns and two genuinely deep lower ledges.
        profile=[(f-11,-7),(f-11,base-.5),(f-9+fracture,base+span*.17+ledge),(f-6+fracture,base+span*.17+ledge),
                 (f-5+fracture*.5,base+span*.37+ledge*.7),(f-1+fracture*.5,base+span*.37+ledge*.7),(f+1.3+fracture*.25,base+span*.77),(f+3.8,top-.14),
                 (f+7,plan_source.height(x,f+7)+.03),(f+15,plan_source.height(x,f+15)+.03),(f+23,plan_source.height(x,f+23)+.03),(f+23,-7)]
        rings.append([(x+(math.sin(x*.15)*1.25 if 2<=j<=6 else 0),y,z) for j,(z,y) in enumerate(profile)])
    for i in range(count):
        for row in range(len(rings[0])):
            j=(row+1)%len(rings[0]);ground=row in (2,4,7,8,9)
            slot=4 if ground else 3 if (i+serial+row)%13==0 else 2
            sculpt.grid_face(rings[i][row],rings[i+1][row],rings[i+1][j],rings[i][j],slot,(i+row)%2,normal=(0,1,0) if ground else None)
    sculpt.face(rings[0][::-1],2);sculpt.face(rings[-1],2)
    return sculpt.finish(f'SM_R3_Cliff_T{tier+1}_{serial:02d}_LOD{lod}','visual',lod,feature='primary escarpment / grassy setbacks')

def island_rim(item,index,lod):
    sculpt=Sculpt();n=32 if lod==0 else 14;rings=[];angle=math.radians(item['rotation'])
    for radius,offset in [(1.34,-2.4),(1.05,-.2),(.82,.08),(.54,.04)]:
        ring=[]
        for i in range(n):
            t=i/n*math.tau;rad=radius*(1+.055*math.sin(t*3+index)+.035*math.sin(t*7+index))
            u=math.cos(t)*item['width']*.5*rad;v=math.sin(t)*item['depth']*.5*rad
            x=item['x']+u*math.cos(angle)-v*math.sin(angle);z=item['z']+u*math.sin(angle)+v*math.cos(angle)
            y=plan_source.height(x,z)+offset if radius<1.1 else -3.8
            ring.append((x,y,z))
        rings.append(ring)
    for row in range(3):
        for i in range(n):
            j=(i+1)%n;sculpt.grid_face(rings[row][i],rings[row][j],rings[row+1][j],rings[row+1][i],4 if row==2 else 2,(i+row)%2,normal=(0,1,0) if row==2 else None)
    sculpt.face(rings[-1],4,normal=(0,1,0));sculpt.face(rings[0][::-1],2)
    return sculpt.finish(f'SM_R3_Island_{index:02d}_LOD{lod}','visual',lod,feature=item['name'])

def main():
    global MATERIALS,COLLECTION,CONVERSION,plan_source
    args=arguments();pspec=importlib.util.spec_from_file_location('tg_r3_plan',HERE/'create_layout_plan.py');plan_source=importlib.util.module_from_spec(pspec);pspec.loader.exec_module(plan_source)
    plan=json.loads(args.plan.read_text())
    bpy.ops.wm.read_factory_settings(use_empty=True);addon_utils.enable('io_scene_fbx',default_set=False,persistent=False)
    bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
    COLLECTION=bpy.data.collections.new('WP01 R3 editable terrain composition');bpy.context.scene.collection.children.link(COLLECTION)
    MATERIALS=[helper.material(n,c) for n,c in zip(SLOTS,[(.43,.64,.22),(.64,.56,.37),(.52,.59,.63),(.39,.47,.55),(.45,.65,.24)])]
    CONVERSION=axis_conversion(to_forward='-Z',to_up='Y').to_3x3()
    for tier,intervals in [(0,[(-146,-47),(-39,-18),(-6,38),(48,146)]),(1,[(-140,132)])]:
        serial=0
        for a,b in intervals:
            count=math.ceil((b-a)/21)
            for j in range(count):
                for lod in (0,1):make_cliff(lerp(a,b,j/count),lerp(a,b,(j+1)/count),tier,serial,lod)
                serial+=1
    for i,item in enumerate(plan['islands']):
        for lod in (0,1):island_rim(item,i,lod)
    # Cascade landing shelves and toe talus have unequal heights/depths. Pools
    # sit on these closed rock supports; channels pass through the cliff gaps.
    for fi,fall in enumerate(plan['waterfalls']):
        path=fall['path'];mid=path[2]
        for lod in (0,1):
            s=Sculpt();split_rock(s,(mid[0],mid[1]-3.2,mid[2]-1.1),(fall['half_width']+2.6,3.1,4.3),700+fi,14 if lod==0 else 8,False)
            split_rock(s,(path[-2][0]+2.4,-2.2,path[-2][2]),(fall['half_width']+1.1,3.1,3.0),720+fi,12 if lod==0 else 7,True)
            s.finish(f'SM_R3_CascadeLanding_{fi:02d}_LOD{lod}','visual',lod,feature=fall['name']+' landing shelf and talus')
        rng=random.Random(900+fi)
        for j in range(9):
            x=fall['x']+rng.choice([-1,1])*rng.uniform(fall['half_width']+1,10);z=fall['path'][-1][2]+rng.uniform(-4,9);y=plan_source.height(x,z)-.3
            s=Sculpt();split_rock(s,(x,y,z),(rng.uniform(1,3.2),rng.uniform(1,2.8),rng.uniform(1,3.3)),1200+fi*20+j,10,True)
            s.finish(f'SM_R3_FallTalus_{fi}_{j:02d}_LOD0','visual',0,feature='waterfall toe breakup')
    groups=[(-32,-4,5),(-35,11,4),(34,12,4),(-30,-30,4),(31,-28,4),(-55,25,4),(57,27,4)]
    serial=0
    for gi,(cx,cz,count) in enumerate(groups):
        rng=random.Random(3100+gi)
        for j in range(count):
            x=cx+rng.uniform(-5,5);z=cz+rng.uniform(-4,4);scale=1.25 if j==0 else rng.uniform(.25,.65)
            for lod in (0,1):
                s=Sculpt();split_rock(s,(x,plan_source.height(x,z)-.45,z),(3.8*scale,3.3*scale,3.2*scale),3500+serial,14 if lod==0 else 7,True)
                s.finish(f'SM_R3_Boulder_{serial:02d}_LOD{lod}','visual',lod,feature='primary-secondary-tertiary prop cluster')
            serial+=1
    # Smaller stone accents follow curved banks instead of an arena necklace.
    for i in range(30):
        x=-103+i*7.0;z=plan_source.nearbank(x)+(-1.8 if i%3 else 1.2);s=Sculpt()
        split_rock(s,(x,plan_source.height(x,z)-.5,z),(1.2+i%4*.37,.8+i%3*.4,1.1+i%3*.24),5000+i,9,False)
        s.finish(f'SM_R3_ShoreStone_{i:02d}_LOD0','visual',0,feature='bank contact')
    marker=helper.axis_marker(SEED).object('SM_R3_AxisMarker',MATERIALS[:3],COLLECTION,'Rigid data A1',SEED)
    OBJECTS.append(marker);d=helper.object_manifest(marker,CONVERSION);d.update(role='diagnostic',lod=0,collision_for='',bounds_unity_local_metres=helper.bounds([(v.co.x,v.co.z,v.co.y) for v in marker.data.vertices]));norm=[(n.vector.x,n.vector.z,n.vector.y) for n in marker.data.corner_normals];d.update(unity_normal_min=[min(n[i] for n in norm) for i in range(3)],unity_normal_max=[max(n[i] for n in norm) for i in range(3)],welded_boundary_edges=0,welded_nonmanifold_edges=0);RECORDS.append(d)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in OBJECTS:obj.select_set(True)
    bpy.context.view_layer.objects.active=OBJECTS[0];fbx=HERE/'TG_LayoutGeometry.fbx';args.unity_fbx.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_space_transform=True,bake_space_transform=True,use_mesh_modifiers=False,use_triangles=False,mesh_smooth_type='OFF',use_tspace=True,colors_type='LINEAR',prioritize_active_color=True,use_custom_props=True,bake_anim=False)
    shutil.copy2(fbx,args.unity_fbx)
    # Editable native-ground equivalent is visible in Blender but deliberately
    # excluded from the FBX: Unity uses the identical sampled TerrainData.
    ground=Sculpt();n=128
    for iz in range(n):
        for ix in range(n):
            x=-192+ix*3;z=-128+iz*3
            ground.face([(x,plan_source.height(x,z),z),(x+3,plan_source.height(x+3,z),z),(x+3,plan_source.height(x+3,z+3),z+3),(x,plan_source.height(x,z+3),z+3)],0,normal=(0,1,0),smooth_face=True)
    ref=ground.geometry.object('SOURCE_R3_native_ground_editable_reference',MATERIALS,COLLECTION,'Reference only; excluded from FBX',SEED);ref['TG_Role']='Editable source reference; Unity uses canonical sampled TerrainData'
    for obj in OBJECTS:
        if obj.name.endswith('_LOD1') or 'AxisMarker' in obj.name:obj.hide_set(True);obj.hide_render=True
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'TG_LayoutGeometry.blend'),compress=True)
    manifest=dict(schema='TG_LayoutR3_Geometry_v1',seed=SEED,units='metres',blender_version=bpy.app.version_string,fbx_sha256=hashlib.sha256(fbx.read_bytes()).hexdigest(),plan_sha256=hashlib.sha256(args.plan.read_bytes()).hexdigest(),objects=RECORDS,total_expected_triangles=sum(d['expected_triangles_after_export'] for d in RECORDS),waterfalls=plan['waterfalls'],islands=plan['islands'])
    path=HERE/'TG_LayoutGeometry.manifest.json';path.write_text(json.dumps(manifest,indent=2));shutil.copy2(path,args.unity_fbx.with_suffix('.manifest.json'))
    print(json.dumps(dict(meshes=len(RECORDS),triangles=manifest['total_expected_triangles'],fbx=str(args.unity_fbx))))
if __name__ == "__main__":main()
