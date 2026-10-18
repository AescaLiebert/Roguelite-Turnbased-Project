"""WP07 integration-owned contact corrections; preserves accepted lane sources.

Cut shallow, closed rock channels below the accepted watercourse, export only
changed rock meshes, and record the exact replacement mesh names. Blender metre
coordinates are X/right, Y/forward, Z/up; Unity imports X/right,Y/up,Z/forward.
"""
from pathlib import Path
import hashlib, importlib.util, json, math, shutil, sys
import addon_utils, bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from bpy_extras.io_utils import axis_conversion

HERE=Path(__file__).resolve().parent; ROOT=HERE.parents[2]
R3=HERE.parent/'LayoutR3'
spec=importlib.util.spec_from_file_location('contact_helper',HERE.parent/'Shared/create_material_samples.py')
h=importlib.util.module_from_spec(spec);sys.modules[spec.name]=h;spec.loader.exec_module(h)
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

def cutter(f):
    points=f['path']; w=f['half_width']*1.16+.45; verts=[];faces=[]
    # The lower bed follows the whole accepted stepped course. It removes
    # occluding caps while retaining the shelf shoulders and toe rocks.
    for x,y,z in points:
        verts.extend([(x-w,z,y-.34),(x+w,z,y-.34),(x+w,z,85),(x-w,z,85)])
    for i in range(len(points)-1):
        a=i*4;b=a+4
        for j in range(4):faces.append((a+j,b+j,b+(j+1)%4,a+(j+1)%4))
    faces.extend([(3,2,1,0),tuple((len(points)-1)*4+j for j in range(4))])
    mesh=bpy.data.meshes.new('closed water channel cutter');mesh.from_pydata(verts,[],faces);mesh.update()
    # Boolean cut faces explicitly inherit stone, not the target's slot-zero
    # grass surface. All slots use the same names/order as the R3 source.
    for name in ('Ground','Soil','Rock','RockShade','Moss'):mesh.materials.append(bpy.data.materials[name])
    for poly in mesh.polygons:poly.material_index=2
    obj=bpy.data.objects.new('CUTTER_'+f['name'],mesh);bpy.context.collection.objects.link(obj)
    # Recalculate outward winding independently of the authoring convention.
    import bmesh
    bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
    return obj

def bounds(obj):
    p=[obj.matrix_world@Vector(c) for c in obj.bound_box]
    return [min(v[i] for v in p) for i in range(3)],[max(v[i] for v in p) for i in range(3)]
def overlaps(a,b):
    aa,ab=bounds(a);ba,bb=bounds(b)
    return all(aa[i]<bb[i] and ab[i]>ba[i] for i in range(3))

def main():
    plan=json.loads((R3/'TG_LayoutPlan.json').read_text())
    bpy.ops.wm.open_mainfile(filepath=str(R3/'TG_LayoutGeometry.blend'))
    addon_utils.enable('io_scene_fbx',default_set=False)
    original=[o for o in list(bpy.data.objects) if o.type=='MESH' and o.name.startswith('SM_R3_') and 'AxisMarker' not in o.name]
    cutters=[cutter(f) for f in plan['waterfalls']];changed=[];records=[]
    for obj in original:
        relevant=[c for c in cutters if overlaps(obj,c)]
        if not relevant:continue
        obj.hide_set(False);obj.hide_render=False;bpy.context.view_layer.objects.active=obj
        old_name=obj.name;old_count=len(obj.data.polygons);old_volume=sum(v.co.length for v in obj.data.vertices)
        for cut in relevant:
            modifier=obj.modifiers.new('WP07 water-contact trough','BOOLEAN');modifier.operation='DIFFERENCE';modifier.solver='EXACT';modifier.object=cut
            bpy.ops.object.modifier_apply(modifier=modifier.name)
        if len(obj.data.polygons)==old_count and abs(sum(v.co.length for v in obj.data.vertices)-old_volume)<.00001:continue
        obj.name='SM_WP07_'+old_name.removeprefix('SM_R3_');obj.data.name=obj.name
        for index,material in enumerate(obj.data.materials):
            if material is None:obj.data.materials[index]=next(m for m in bpy.data.materials if m.name=='Rock')
        # Explicit evaluated export after boolean. Preserve material slot names.
        obj.data.calc_loop_triangles();changed.append(obj)
        record=h.object_manifest(obj,axis_conversion(to_forward='-Z',to_up='Y').to_3x3())
        # Boolean n-gons may contain re-entrant corners; the evaluated Blender
        # tessellation, rather than a triangle-fan formula, is authoritative.
        record['expected_triangles_after_export']=len(obj.data.loop_triangles)
        record.update(replaces=old_name,slots=[s.material.name if s.material else '' for s in obj.material_slots],
                      bounds_unity_local_metres=h.bounds([(v.co.x,v.co.z,v.co.y) for v in obj.data.vertices]))
        records.append(record)
    for obj in list(bpy.data.objects):
        if obj not in changed:bpy.data.objects.remove(obj,do_unlink=True)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in changed:obj.hide_set(False);obj.select_set(True)
    bpy.context.view_layer.objects.active=changed[0]
    fbx=HERE/'TG_StageContacts.fbx'
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_space_transform=True,bake_space_transform=True,use_mesh_modifiers=False,use_triangles=True,mesh_smooth_type='OFF',use_tspace=True,colors_type='LINEAR',prioritize_active_color=True,use_custom_props=True,bake_anim=False)
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'TG_StageContacts.blend'),compress=True)
    manifest={'schema':'TG_WP07_Contacts_v1','generator_sha256':sha(Path(__file__)),'source_blend_sha256':sha(R3/'TG_LayoutGeometry.blend'),'fbx_sha256':sha(fbx),'clearance_below_water_metres':.34,'water_channel_width':'half_width*1.16+.45 each side','objects':records}
    side=HERE/'TG_StageContacts.manifest.json';side.write_text(json.dumps(manifest,indent=2)+'\n')
    target=ROOT/'Assets/Project/Art/Environment/TeyvatGrassland/Integration/Models';target.mkdir(parents=True,exist_ok=True)
    for p in (fbx,side):shutil.copy2(p,target/p.name)
    print(json.dumps({'changed_meshes':len(records),'fbx':str(fbx)}))
if __name__=='__main__':main()
