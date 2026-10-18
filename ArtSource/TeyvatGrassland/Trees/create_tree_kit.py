"""WP05 deterministic, editable geometric canopy kit. Run in background Blender.

Hybrid geometric canopy supports with alpha-clipped broadleaf sprays.
Leaf normals are analytical ellipsoid-envelope transfer plus a soft upward bias.
All LODs share root pivots, primary branches and identical clump records.
"""
from __future__ import annotations
import hashlib, importlib.util, json, math, random, shutil, sys
from pathlib import Path
import addon_utils, bpy
from mathutils import Vector
from bpy_extras.io_utils import axis_conversion

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
spec=importlib.util.spec_from_file_location('tg_tree_helper',HERE.parent/'Shared/create_material_samples.py')
h=importlib.util.module_from_spec(spec);sys.modules[spec.name]=h;spec.loader.exec_module(h)
SEED=510731
CONFIGS=[('HeroOak',12.0,1.0),('MeadowOak',8.0,.84),('IslandTree',6.0,.78),
         ('WindPine',8.0,1.0),('YoungPine',6.0,.78),('Shrub',1.8,1.0)]

def mask(p,height,red=0):
    return h.rgba(red, .55*max(0,min(1,p[2]/height))**1.6, .41)

def tube(g,points,radii,height,lod):
    start=len(g.vertices)
    h.add_tube(g,points,radii,0,.41,False,8 if lod==0 else 6 if lod==1 else 5)
    for i in range(start,len(g.vertices)):g.colors[i]=mask(g.vertices[i],height)

def clump(g,c,r,height,lod,seed,slot=1,pine=False):
    """Closed scalloped support volume with edge leaves, no flat card rectangles."""
    rng=random.Random(seed);c=Vector(c);r=Vector(r)
    rows,cols=((7,14),(5,12),(4,10))[lod]
    rings=[]
    def surface(theta,phi):
        n=Vector((math.sin(theta)*math.cos(phi),math.sin(theta)*math.sin(phi),math.cos(theta)))
        scallop=1+.065*math.sin(phi*5+.5)*math.sin(theta)**2+.035*math.cos(phi*9+theta*4)
        p=c+Vector((n.x*r.x,n.y*r.y,n.z*r.z))*scallop
        normal=h.unit(Vector((n.x/r.x,n.y/r.y,n.z/r.z))*.55+Vector((0,0,.70)))
        return p,normal
    # The closed core supports volume but sits inside the leafy surface. Keeping
    # it at the outer radius made the first sample read as smooth green balls.
    core=.85 if pine else .45
    top=g.vertex(c+Vector((0,0,r.z*core)),mask(c+Vector((0,0,r.z*core)),height),Vector((0,0,1)))
    for row in range(1,rows):
        theta=row/rows*math.pi;ring=[]
        for col in range(cols):
            p,n=surface(theta,col/cols*math.tau);p=c+(p-c)*core
            ring.append(g.vertex(p,mask(p,height),n))
        rings.append(ring)
    bottom=g.vertex(c-Vector((0,0,r.z*core)),mask(c-Vector((0,0,r.z*core)),height),Vector((0,0,1)))
    for j in range(cols):
        k=(j+1)%cols
        if pine or lod==2:
            g.face((top,rings[0][j],rings[0][k]),material=slot,smooth=True)
            for a,b in zip(rings,rings[1:]):g.face((a[j],b[j],b[k],a[k]),material=slot,smooth=True)
            g.face((rings[-1][j],bottom,rings[-1][k]),material=slot,smooth=True)
    # Stable source records: LOD1 retains a subset, rather than reseeding leaves.
    count=60 if not pine else 150
    for j in range(count):
        theta=math.acos(rng.uniform(-.70,.98));phi=j*2.399963+rng.uniform(-.3,.3)
        p,n=surface(theta,phi);out=h.unit(p-c)
        length=(.17 if not pine else .30)*rng.uniform(.8,1.35)*max(.5,height/8)**.35
        width=length*(.53 if not pine else .30)
        direction=h.unit(out+Vector((math.cos(phi+.6),math.sin(phi+.6),.2))*.4)
        side=h.unit(direction.cross(Vector((0,0,1))))
        if lod==2 or (lod==1 and j%4!=0):continue
        # Geometric pointed leaf with a folded center. At the attachment R=0,
        # same G/B/function as its underlying clump; flutter grows toward tip.
        points=[p-out*.035,p+direction*length*.45-side*width,
                p+direction*length,p+direction*length*.45+side*width,
                p+direction*length*.50+n*length*.13]
        ids=[g.vertex(q,mask(q,height,(0,.45,1,.45,.5)[i]),n) for i,q in enumerate(points)]
        if lod==0:
            for a,b in ((0,1),(1,2),(2,3),(3,0)):g.face((ids[a],ids[b],ids[4]),material=slot,smooth=True)
        else:g.face((ids[0],ids[1],ids[2],ids[3]),material=slot,smooth=True)
    if pine:return
    # Curved tangent sprays, not crossed full-tree billboards. Every LOD retains
    # the same spatial records; texture cutout gives individual leaf silhouettes.
    for j in range(32):
        theta=math.acos(.96-1.92*((j+.5)/32));phi=j*2.399963
        p,n=surface(theta,phi);p=c+(p-c)*.84
        if lod==1 and j%2:continue
        if lod==2 and j%4:continue
        outward=h.unit(p-c)
        tangent=h.unit(Vector((-math.sin(phi),math.cos(phi),.16*math.sin(phi*2))))
        tangent=h.unit(tangent-outward*tangent.dot(outward))
        vertical=h.unit(outward.cross(tangent))
        span=max(r.x,r.y)*1.32*(.85+.25*math.sin(j*1.73)**2);length=span/1.5
        ids=[]
        for row in range(3):
            for col in range(3):
                u=col/2;v=row/2
                q=p+tangent*(u-.5)*span+vertical*(v-.5)*length+outward*(.10*span*(1-(u*2-1)**2))
                red=.1+.8*max(abs(u*2-1),abs(v*2-1))
                ids.append(g.vertex(q,mask(q,height,red),n))
        for row in range(2):
            for col in range(2):
                a=row*3+col;g.face((ids[a],ids[a+1],ids[a+4],ids[a+3]),
                    ((col/2,row/2),((col+1)/2,row/2),((col+1)/2,(row+1)/2),(col/2,(row+1)/2)),3+(slot==2),True)

sys.path.insert(0,str(HERE))
from tree_shapes import records

def build(name,height,width,lod):
    g=h.Geometry();branches,crowns=records(name,height,width)
    for p,r in branches:tube(g,p,r,height,lod)
    for c,r,seed,slot in crowns:clump(g,c,r,height,lod,seed+SEED,slot,'Pine' in name)
    # One scale is derived from LOD0 geometry and reused for every LOD. Heights
    # mean actual authored top, rather than trunk height or unsaved node scale.
    return g

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True);addon_utils.enable('io_scene_fbx',default_set=False)
    bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
    collection=bpy.data.collections.new('WP05 editable trees, LODs aligned per species');bpy.context.scene.collection.children.link(collection)
    mats=[h.material('Bark',(.31,.19,.10)),h.material('Leaf',(.27,.51,.12)),h.material('LeafLight',(.39,.60,.16)),
          h.material('LeafSpray',(.27,.51,.12)),h.material('LeafSprayLight',(.39,.60,.16))]
    image=bpy.data.images.load(str(HERE/'Textures/TG_LeafSprayAlbedoOpacity.png'))
    image.pack();image.filepath='//Textures/TG_LeafSprayAlbedoOpacity.png'
    for mat in mats[3:]:
        nodes=mat.node_tree.nodes;links=mat.node_tree.links;principled=nodes.get('Principled BSDF')
        texture=nodes.new('ShaderNodeTexImage');texture.image=image
        tint=nodes.new('ShaderNodeMixRGB');tint.blend_type='MULTIPLY';tint.inputs[0].default_value=1
        tint.inputs[2].default_value=mat.diffuse_color
        links.new(texture.outputs['Color'],tint.inputs[1]);links.new(tint.outputs[0],principled.inputs['Base Color'])
        threshold=nodes.new('ShaderNodeMath');threshold.operation='GREATER_THAN';threshold.inputs[1].default_value=.60
        links.new(texture.outputs['Alpha'],threshold.inputs[0]);links.new(threshold.outputs[0],principled.inputs['Alpha'])
        mat.surface_render_method='DITHERED'
    objects=[];descriptions=[]
    for index,(name,height,width) in enumerate(CONFIGS):
        baseline=build(name,height,width,0);top=max(p[2] for p in baseline.vertices);factor=height/top
        for lod in range(3):
            g=baseline if lod==0 else build(name,height,width,lod)
            g.vertices=[(x*factor,y*factor,max(0,z*factor)) for x,y,z in g.vertices]
            obj=g.object('SM_TG_'+name+'_LOD'+str(lod),mats,collection,
                         'R=0 wood/core/leaf attachment, 0..1 leaf flutter; G=.55*(height/speciesHeight)^1.6 continuous on wood and foliage; B=.41 stable phase; A=1',SEED)
            obj.location=(index*17,lod*19,0);objects.append(obj)
            d=h.object_manifest(obj,axis_conversion(to_forward='-Z',to_up='Y').to_3x3())
            d.update(species=name,lod=lod,height=height,
                     bounds_unity_local_metres=h.bounds([(v[0],v[2],v[1]) for v in g.vertices]),
                     unity_normal_min=[d['normal_component_min'][k] for k in (0,2,1)],
                     unity_normal_max=[d['normal_component_max'][k] for k in (0,2,1)])
            descriptions.append(d)
    marker=h.axis_marker(SEED).object('SM_TG_TreeAxisMarker',mats,collection,'Rigid orientation proof',SEED);marker.location=(0,-5,0);objects.append(marker)
    d=h.object_manifest(marker,axis_conversion(to_forward='-Z',to_up='Y').to_3x3());d.update(species='diagnostic',lod=0,bounds_unity_local_metres=h.bounds([(v.co.x,v.co.z,v.co.y) for v in marker.data.vertices]));descriptions.append(d)
    locations=[o.location.copy() for o in objects];bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.location=(0,0,0);o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    fbx=HERE/'TG_TreeKit.fbx'
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},global_scale=1,
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',
        use_space_transform=True,bake_space_transform=True,use_mesh_modifiers=False,use_triangles=False,
        mesh_smooth_type='OFF',use_tspace=True,colors_type='LINEAR',prioritize_active_color=True,use_custom_props=True,bake_anim=False)
    for o,p in zip(objects,locations):o.location=p
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'TG_TreeKit.blend'),compress=True)
    data={'schema':'TG_WP05_Trees_v1','seed':SEED,'blender_version':bpy.app.version_string,
          'generator_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
          'shape_source_sha256':hashlib.sha256((HERE/'tree_shapes.py').read_bytes()).hexdigest(),
          'fbx_sha256':hashlib.sha256(fbx.read_bytes()).hexdigest(),'objects':descriptions,
          'geometry_policy':'Inset closed scalloped canopy support volumes, pointed folded geometric leaves and curved alpha-clipped broadleaf sprays. Pines use geometry only. No baked sun direction.',
          'normal_policy':'Analytical ellipsoid envelope normals, softened upward. Explicit triangle normals before FBX. Import normals; do not recalculate.',
          'lod_policy':'Same clump/branch records and scale per species; reduce surface subdivisions, retain every fourth geometric source leaf in LOD1; broadleaf sprays keep every second record in LOD1/every fourth in LOD2. Pines LOD2 use the closed branch envelopes.'}
    manifest=HERE/'TG_TreeKit.manifest.json';manifest.write_text(json.dumps(data,indent=2)+'\n')
    target=ROOT/'Assets/Project/Art/Environment/TeyvatGrassland/Trees/Models';target.mkdir(parents=True,exist_ok=True)
    shutil.copy2(fbx,target/fbx.name);shutil.copy2(manifest,target/manifest.name)
    print(json.dumps({'objects':len(objects),'triangles':sum(d['expected_triangles_after_export'] for d in descriptions),'output':str(fbx)}))

if __name__=='__main__':main()
