"""WP03 world sculpt, authored in metres against the unchanged WP01 r2 terrain.

Blender 5.1 background entry point. Layout references are the approved battle
concept and four-angle diorama. Real-game screenshots guide surface language only.
Native TerrainData remains the continuous Unity ground; its exported height samples
drive visible cliff caps, landmark contacts and the editable Blender reference mesh.
"""
from __future__ import annotations
import sys, math, json, hashlib, shutil, importlib.util
from pathlib import Path
import bpy, addon_utils, bmesh
from mathutils import Vector
from bpy_extras.io_utils import axis_conversion

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
spec=importlib.util.spec_from_file_location('tg_sample_source',HERE.parent/'Shared/create_material_samples.py')
helper=importlib.util.module_from_spec(spec);sys.modules[spec.name]=helper;spec.loader.exec_module(helper)
HEIGHT=json.loads((HERE/'TG_WP01TerrainSource.json').read_text(encoding='utf-8-sig'))
SEED=20261018
SLOTS=['Ground','Soil','Rock','RockShade','Moss','DistantBlue','Roof','Window']
objects=[];records=[]

def h(x,z):
    n=HEIGHT['resolution']; px=max(0,min(n-1,(x+192)/384*(n-1)));pz=max(0,min(n-1,(z+128)/384*(n-1)))
    ix=min(n-2,int(px));iz=min(n-2,int(pz));tx=px-ix;tz=pz-iz;v=HEIGHT['heights']
    a=v[iz*n+ix]*(1-tx)+v[iz*n+ix+1]*tx;b=v[(iz+1)*n+ix]*(1-tx)+v[(iz+1)*n+ix+1]*tx
    return -10+(a*(1-tz)+b*tz)*70
def center(x):return 35+2.7*math.sin(x*.042)+1.8*math.cos(x*.071)
def width(x):return 14.5+2.2*math.sin(x*.057+1)
def front(x,tier=0):
    if tier==0:return center(x)+width(x)+7-7*math.exp(-((x+67)/25)**2)+8*math.exp(-((x+8)/25)**2)-4*math.exp(-((x-65)/20)**2)+1.1*math.sin(x*.084)
    return 82-.20*(x+20)+8*math.exp(-((x+55)/36)**2)+2*math.sin(x*.061)
def bn(p):return (p[0],p[2],p[1])

class Sculpt:
    def __init__(self):self.g=helper.Geometry()
    def face(self,points,slot=2,normal=None,smooth=False):
        # The Unity-local -> Blender coordinate swap is a reflection. Reverse the
        # source loop so Blender's outward normals survive the proven FBX preset.
        points=list(points); ids=[self.g.vertex(bn(p),normal=bn(normal) if normal is not None else None) for p in points]
        self.g.face(ids[::-1],material=slot,smooth=smooth)
    def box(self,c,s,slot=2,bevel=0):
        x,y,z=c;dx,dy,dz=[v*.5 for v in s]
        p=[(x+a*dx,y+b*dy,z+c*dz) for a,b,c in [(-1,-1,-1),(1,-1,-1),(1,-1,1),(-1,-1,1),(-1,1,-1),(1,1,-1),(1,1,1),(-1,1,1)]]
        for ids in [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]:self.face([p[i] for i in ids],slot)
    def object(self,name,role='visual',lod=0,collision=None):
        # Establish outward volume normals before UV/custom-normal authoring.
        # Faces use independent corner vertices, so weld a temporary topology copy
        # for orientation only, then transfer just the loop-direction decision.
        unique={};verts=[];faces=[]
        for face in self.g.faces:
            indices=[]
            for index in face:
                p=tuple(round(v,7) for v in self.g.vertices[index])
                if p not in unique:unique[p]=len(verts);verts.append(p)
                indices.append(unique[p])
            faces.append(indices)
        temp=bpy.data.meshes.new('temporary volume orientation');temp.from_pydata(verts,[],faces);temp.update()
        bm=bmesh.new();bm.from_mesh(temp);bm.faces.ensure_lookup_table();bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        for i,face in enumerate(self.g.faces):
            a,b,c=[Vector(self.g.vertices[k]) for k in face[:3]]
            if (b-a).cross(c-a).dot(bm.faces[i].normal)<0:
                self.g.faces[i]=tuple(reversed(face));self.g.face_uvs[i]=tuple(reversed(self.g.face_uvs[i]))
        bm.free();bpy.data.meshes.remove(temp)
        obj=self.g.object(name,mats,collection,'R=0 G=0 B=0 A=1; rigid WP03 world geometry',SEED)
        obj['TG_Role']=role;obj['TG_LOD']=lod;objects.append(obj)
        d=helper.object_manifest(obj,conversion)
        verts=[(v.co.x,v.co.z,v.co.y) for v in obj.data.vertices]
        d['bounds_unity_local_metres']=helper.bounds(verts)
        normals=[(n.vector.x,n.vector.z,n.vector.y) for n in obj.data.corner_normals]
        d['unity_normal_min']=[min(v[i] for v in normals) for i in range(3)];d['unity_normal_max']=[max(v[i] for v in normals) for i in range(3)]
        d.update(role=role,lod=lod,collision_for=collision or '',material_slots=SLOTS)
        # Manifold proof on the authored topology after coincident positions are welded.
        # This is a mesh-volume check, not Boolean union of deliberately overlapping stones.
        edges={}
        for poly in obj.data.polygons:
            ids=list(poly.vertices)
            for i in range(len(ids)):
                a=tuple(round(v,5) for v in obj.data.vertices[ids[i]].co);b=tuple(round(v,5) for v in obj.data.vertices[ids[(i+1)%len(ids)]].co)
                key=tuple(sorted((a,b)));edges[key]=edges.get(key,0)+1
        d['welded_boundary_edges']=sum(v==1 for v in edges.values())
        d['welded_nonmanifold_edges']=sum(v>2 for v in edges.values())
        records.append(d);return obj

def escarpment(a,b,tier,step,name,lod=0,collision=False):
    s=Sculpt();n=max(1,math.ceil((b-a)/step));bands=[0,.28,.63,1];grid=[]
    for i in range(n+1):
        x=a+(b-a)*i/n;f=front(x,tier);top=h(x,f+5)+.08;bottom=-4.0 if tier==0 else h(x,f-5)-1.8
        row=[]
        for j,t in enumerate(bands):
            # Broad fixed longitudinal changes, not independent per-triangle noise.
            shoulder=(.65+.55*math.sin(x*.105+1.2))*math.sin(t*math.pi)
            z=f-2.1-shoulder-.8*math.sin(x*.39+.1)*(1-t*.35)
            y=bottom+(top-bottom)*t+(math.sin(x*.23+j*1.6)*1.4 if j in (1,2) else 0)
            lateral=math.sin(x*.19+j*1.1)*.85 if j in (1,2) else 0
            row.append((x+lateral,y,z))
        grid.append(row)
    for i in range(n):
        for j in range(3):
            slot=2 if (i//3+j+tier)%8 else 3
            s.face([grid[i][j],grid[i+1][j],grid[i+1][j+1],grid[i][j+1]],slot)
        # Cap has real depth and shares its back edge with the sampled TerrainData.
        aa=grid[i][-1];bb=grid[i+1][-1];cc=(bb[0],h(bb[0],front(bb[0],tier)+7)-.06,front(bb[0],tier)+7);dd=(aa[0],h(aa[0],front(aa[0],tier)+7)-.06,front(aa[0],tier)+7)
        # Close rear and underside well inside the continuous terrain volume.
        lowa=(aa[0],grid[i][0][1]-.5,dd[2]);lowb=(bb[0],grid[i+1][0][1]-.5,cc[2])
        s.face([aa,bb,cc,dd],0);s.face([dd,cc,lowb,lowa],2)
        s.face([grid[i][0],lowa,lowb,grid[i+1][0]],2)
    for i,reverse in [(0,True),(n,False)]:
        row=grid[i];x=row[0][0];backz=front(x,tier)+7;poly=row+[(x,h(x,backz)-.06,backz),(x,row[0][1]-.5,backz)]
        if reverse:poly=poly[::-1]
        s.face(poly,2)
    return s.object(name,'collision' if collision else 'visual',lod,collision)

def rock(name,c,scale,seed,lod=0,collision=False):
    s=Sculpt();rings=[];sides=7 if lod==0 else 5
    for y,r in [(0,.82),(.24,1),(.74,.77),(1,.31)]:
        ring=[]
        for i in range(sides):
            angle=2*math.pi*i/sides+.15;rr=r*(1+.10*math.sin(i*2.1+seed))
            ring.append((c[0]+math.cos(angle)*scale[0]*rr,c[1]+y*scale[1]+(.06*math.sin(i*1.7+seed) if y else 0),c[2]+math.sin(angle)*scale[2]*rr))
        rings.append(ring)
    s.face(rings[0][::-1],2)
    for r in range(3):
        for i in range(sides):s.face([rings[r][i],rings[r][(i+1)%sides],rings[r+1][(i+1)%sides],rings[r+1][i]],2 if (i+seed)%6 else 3)
    s.face(rings[-1],4 if seed%4==0 else 2)
    return s.object(name,'collision' if collision else 'visual',lod,collision)

def arch(s,c,rx,ry,thick,depth,count,slot=2,angle=0):
    ca=math.cos(angle);sa=math.sin(angle)
    def p(x,y,z):return(c[0]+x*ca+z*sa,c[1]+y,c[2]-x*sa+z*ca)
    for i in range(count):
        a=math.pi*i/count+.003;b=math.pi*(i+1)/count-.003
        ring=[((rx-thick/2)*math.cos(a),(ry-thick/2)*math.sin(a)),((rx+thick/2)*math.cos(a),(ry+thick/2)*math.sin(a)),((rx+thick/2)*math.cos(b),(ry+thick/2)*math.sin(b)),((rx-thick/2)*math.cos(b),(ry-thick/2)*math.sin(b))]
        f=[p(x,y,-depth/2) for x,y in ring];r=[p(x,y,depth/2) for x,y in ring]
        s.face(f,slot);s.face(r[::-1],slot)
        for k in range(4):s.face([f[k],r[k],r[(k+1)%4],f[(k+1)%4]],slot)

def ruin(lod,collision=False):
    s=Sculpt();x,z=26,8;base=h(x,z)-.18
    # Unequal courses and a damaged crown retain the concept's real arch opening.
    for side in [-1,1]:
        for i in range(3):s.box((x+side*3,base+.73+i*1.44,z),(1.54-(i%2)*.1,1.42,2.25),2 if i%2 else 3)
        s.box((x+side*3,base+4.55,z),(1.95,.42,2.4),2)
    arch(s,(x,base+4.65,z),3.0,3.2,1.05,2.2,13 if lod==0 else 7)
    s.box((x+4.55,base+.6,z-1.9),(1.9,1.5,1.7),2)
    s.box((x-.7,base+7.85,z),(.9,.16,1.8),4)
    return s.object('COL_WP03_RuinArch' if collision else 'SM_WP03_RuinArch_LOD'+str(lod),
                    role='collision' if collision else 'visual',lod=lod,
                    collision='SM_WP03_RuinArch' if collision else None)

def spire(s,c,r,height,sides=6):
    p=[(c[0]+r*math.cos(i*2*math.pi/sides),c[1],c[2]+r*math.sin(i*2*math.pi/sides)) for i in range(sides)]
    s.face(p[::-1],6)
    for i in range(sides):s.face([p[i],p[(i+1)%sides],(c[0],c[1]+height,c[2])],6)

def cathedral(lod):
    s=Sculpt();x,z=-50,106;base=h(x,z)-.45
    s.box((x,base+.5,z+2),(17,1.6,25),3)
    # Nave shell, steep solid gable and projecting buttresses.
    s.box((x,base+5.6,z+4),(10.7,10,17),2)
    s.box((x,base+4.5,z+7),(17,8,4.8),2)
    a=(x-6,base+10.5,z-5);b=(x+6,base+10.5,z-5);c=(x+6,base+10.5,z+13);d=(x-6,base+10.5,z+13);e=(x,base+16,z-5);f=(x,base+16,z+13)
    for q in [[a,d,f,e],[b,e,f,c],[a,e,b],[d,c,f],[a,b,c,d]]:s.face(q,6)
    for side in [-1,1]:
        for i in range(4 if lod==0 else 3):
            zz=z-2+i*4.2;s.box((x+side*5.8,base+4.3,zz),(1,8.3,1.4),3)
            s.box((x+side*5.4,base+6,zz+1.8),(.08,3.7,.95),7)
    tz=z-7.2;s.box((x,base+8,tz),(7.6,16,7.6),2)
    s.box((x,base+16.4,tz),(8.6,.85,8.6),3)
    # Four corner shafts leave belfry voids visible through the tower.
    for dx in [-3.25,3.25]:
        for dz in [-3.25,3.25]:
            s.box((x+dx,base+20.5,tz+dz),(1.1,7.5,1.1),2)
            s.box((x+dx,base+25.8,tz+dz),(1.25,3.2,1.25),3);spire(s,(x+dx,base+27.3,tz+dz),.95,5.8)
    for side in range(4):
        angle=side*math.pi/2;cx=x-3.25*math.sin(angle);cz=tz-3.25*math.cos(angle)
        arch(s,(cx,base+20.2,cz),2.8,3.2,.6,.9,9 if lod==0 else 5,2,angle)
    s.box((x,base+24.4,tz),(8.1,.8,8.1),2);spire(s,(x,base+24.8,tz),3.1,11.2)
    s.box((x,base+3.8,tz-3.83),(2.6,5.7,.08),7)
    arch(s,(x,base+6.4,tz-3.9),1.6,2.3,.5,.4,9 if lod==0 else 5)
    return s.object('SM_WP03_Cathedral_LOD'+str(lod),lod=lod)

def aqueduct(lod):
    s=Sculpt();start,end,z=25,110,125;span=17;deck=max(h(start+i*span,z) for i in range(6))+15.5
    for i in range(6):
        x=start+i*span;ground=h(x,z)-.65
        s.box((x,(ground+deck-2)/2,z),(2.8,deck-2-ground,4.2),3)
        s.box((x,deck-7.5,z),(3.5,.6,4.8),2)
    for i in range(5):
        x=start+span*(i+.5)
        arch(s,(x,deck-8,z),span/2,8,1.35,4.2,15 if lod==0 else 7)
    s.box(((start+end)/2,deck+.5,z),(end-start+5,1.5,4.8),2)
    s.box(((start+end)/2,deck+1.6,z-2.2),(end-start+5,.75,.55),3)
    s.box(((start+end)/2,deck+1.6,z+2.2),(end-start+5,.75,.55),3)
    return s.object('SM_WP03_Aqueduct_LOD'+str(lod),lod=lod)

def ridge(name,z,y,seed,lod):
    s=Sculpt();n=40 if lod==0 else 20;frontrow=[];backrow=[]
    for i in range(n+1):
        x=-600+1200*i/n;fade=max(0,min(1,(600-abs(x))/180))
        peak=(y+12*math.sin(x*.022+seed)+7*math.sin(x*.051+seed*.7)+28*math.exp(-((x+95)/45)**2)+18*math.exp(-((x-105)/38)**2))*fade
        frontrow.append((x,peak,z+14*math.sin(x*.012)));backrow.append((x,peak*.42,z+75))
    for i in range(n):
        a=frontrow[i];b=frontrow[i+1];c=backrow[i+1];d=backrow[i]
        s.face([a,b,c,d],5);s.face([(a[0],-12,a[2]),(b[0],-12,b[2]),b,a],5)
        s.face([d,c,(c[0],-12,c[2]),(d[0],-12,d[2])],5)
        s.face([(a[0],-12,a[2]),(d[0],-12,d[2]),(c[0],-12,c[2]),(b[0],-12,b[2])],5)
    for i,rev in [(0,False),(n,True)]:
        a=frontrow[i];b=backrow[i];q=[a,b,(b[0],-12,b[2]),(a[0],-12,a[2])];s.face(q[::-1] if rev else q,5)
    return s.object(name+'_LOD'+str(lod),lod=lod)

def main():
    global mats,collection,conversion
    if not bpy.app.background:raise RuntimeError('Use Blender --background; this creates an authored source scene.')
    bpy.ops.wm.read_factory_settings(use_empty=True);addon_utils.enable('io_scene_fbx',default_set=False,persistent=False)
    bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
    collection=bpy.data.collections.new('WP03 world - layout-local editable sculpt');bpy.context.scene.collection.children.link(collection)
    colors=[(.38,.61,.17),(.65,.55,.34),(.48,.55,.59),(.38,.45,.50),(.40,.60,.20),(.27,.49,.67),(.25,.33,.41),(.10,.18,.23)]
    mats=[helper.material(n,c) for n,c in zip(SLOTS,colors)];conversion=axis_conversion(to_forward='-Z',to_up='Y').to_3x3()
    intervals=[(-184,-47),(-39,-19),(-7,39),(47,184)]
    for tier,segments in [(0,intervals),(1,[(-179,77)])]:
        serial=0
        for a,b in segments:
            count=max(1,math.ceil((b-a)/25))
            for j in range(count):
                aa=a+(b-a)*j/count;bb=a+(b-a)*(j+1)/count
                base=f'SM_Escarpment_Tier{tier+1}_{serial:02d}'
                for lod,step in [(0,3.2),(1,6.4)]:escarpment(aa,bb,tier,step,base+'_LOD'+str(lod),lod)
                escarpment(aa,bb,tier,8.0,base.replace('SM_','COL_'),1,base)
                serial+=1
    rockpos=[(-36,4,1.35),(-31,-3,1.05),(-28,6,.7),(-36,11,.82),(-41,-1,.63),(-28,-8,.52),(31,8,1.08),(34,15,.87),(26,16,.6),(38,5,.48),(-28,-26,1.4),(-23,-29,.63),(-31,-24,.49),(26,-28,1.17),(31,-24,.55)]
    for i,(x,z,size) in enumerate(rockpos):
        c=(x,h(x,z)-.24,z);scale=(3.4*size,2.6*size,2.8*size);base=f'SM_MeadowRock_{i:02d}'
        for lod in [0,1]:rock(base+'_LOD'+str(lod),c,scale,i+901,lod)
        rock(base.replace('SM_','COL_'),c,scale,i+901,1,base)
    for i in range(22):
        x=-113+i*10.6;z=center(x)+(-1 if i%2==0 else 1)*(width(x)-.8)
        rock(f'SM_BankRock_{i:02d}_LOD0',(x,h(x,z)-.3,z),(2.0+i%3*.5,1.4+i%4*.45,1.9),4100+i)
    for lod in [0,1]:ruin(lod);cathedral(lod);aqueduct(lod)
    ruin(1,True)
    for i in range(3):
        for lod in [0,1]:ridge(f'SM_DistantRidge_{i:02d}',190+i*85,48+i*14,11+i,lod)
    # Reuse the proven asymmetric marker to establish raw import axes independently.
    marker=helper.axis_marker(SEED).object('SM_WorldAxisMarker',[mats[2],mats[3],mats[1]],collection,'Rigid axis marker A1',SEED)
    objects.append(marker);d=helper.object_manifest(marker,conversion);d.update(role='diagnostic',lod=0,collision_for='')
    verts=[(v.co.x,v.co.z,v.co.y) for v in marker.data.vertices];d['bounds_unity_local_metres']=helper.bounds(verts)
    norms=[(v.vector.x,v.vector.z,v.vector.y) for v in marker.data.corner_normals];d['unity_normal_min']=[min(v[i] for v in norms) for i in range(3)];d['unity_normal_max']=[max(v[i] for v in norms) for i in range(3)];records.append(d)
    # Editable terrain reference is in the blend but deliberately excluded from FBX.
    ref=Sculpt();n=128
    for iz in range(n):
        for ix in range(n):
            x=-192+ix*3;z=-128+iz*3
            ref.face([(x,h(x,z),z),(x,h(x,z+3),z+3),(x+3,h(x+3,z+3),z+3),(x+3,h(x+3,z),z)],0)
    refobj=ref.g.object('SOURCE_WP01_TerrainData_surface_reference',mats,collection,'Not exported; unchanged native TerrainData source',SEED)
    refobj['TG_Role']='Blender reference only; Unity uses native TerrainData';refobj.hide_render=True
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:obj.select_set(True)
    bpy.context.view_layer.objects.active=objects[0]
    source=HERE/'TG_WorldGeometry.fbx';dest=ROOT/'Assets/Project/Art/Environment/TeyvatGrassland/World/Models/TG_WorldGeometry.fbx';dest.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=str(source),use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_space_transform=True,bake_space_transform=True,use_mesh_modifiers=False,use_triangles=False,mesh_smooth_type='OFF',use_tspace=True,colors_type='LINEAR',prioritize_active_color=True,use_custom_props=True,bake_anim=False,path_mode='AUTO',embed_textures=False)
    shutil.copy2(source,dest)
    for obj in objects:
        if obj.name.startswith('COL_') or obj.name.endswith('_LOD1') or 'AxisMarker' in obj.name:obj.hide_set(True);obj.hide_render=True
    # Authoring view remains in actual layout coordinates; no transform bake after normals.
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'TG_WorldGeometry.blend'),compress=True)
    falls=[]
    for i,x in enumerate([-43,-13,43]):falls.append(dict(x=x,front_z=front(x),crest_y=h(x,front(x)+5)+.3,half_width=4.3 if i==1 else 2.7))
    manifest=dict(schema='TG_WP03_WorldGeometry_v1',seed=SEED,units='metres',blender_version=bpy.app.version_string,terrain_source_sha256=hashlib.sha256((HERE/'TG_WP01TerrainSource.json').read_bytes()).hexdigest(),fbx_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),generator_sha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),axis='Unity raw=(Blender X, Blender Z, Blender Y); geometry authored layout-local, reflection loops reversed before custom normals. Independently validate axis marker.',material_slot_names=SLOTS,waterfalls=falls,river_level=-1.35,objects=records,total_expected_triangles=sum(o['expected_triangles_after_export'] for o in records),layout_reference='WP01 r2 + approved battle concept and four-angle diorama. Earlier real-game screenshots are material references only.',native_terrain='Keep the original WP01 TerrainData heightfield as the continuous ground; source-reference mesh is excluded from FBX.',limits='LOD1 and collision shapes are authored starting representations. Validate camera silhouettes, collider grounding and transition distances before runtime acceptance.')
    path=HERE/'TG_WorldGeometry.manifest.json';path.write_text(json.dumps(manifest,indent=2));shutil.copy2(path,dest.with_suffix('.manifest.json'))
    print(json.dumps(dict(status='generated',fbx=str(dest),objects=len(records),triangles=manifest['total_expected_triangles'],waterfalls=falls)))

if __name__=='__main__':main()
