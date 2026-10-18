"""WP06 editable surfaces in the accepted R3 arena-local metre frame.

No external textures, generated images, physics or second animation clock.
Curve control points come unchanged from R3. Fine mesh variation is deterministic.
UV U crosses each fall and V increases by physical arc length downstream.
"""
from __future__ import annotations
import hashlib,importlib.util,json,math,random,shutil,sys
from pathlib import Path
import addon_utils,bpy
from mathutils import Vector
from bpy_extras.io_utils import axis_conversion
HERE=Path(__file__).resolve().parent;ROOT=HERE.parents[2]
spec=importlib.util.spec_from_file_location('tg_water_helper',HERE.parent/'Shared/create_material_samples.py')
h=importlib.util.module_from_spec(spec);sys.modules[spec.name]=h;spec.loader.exec_module(h)
PLAN=ROOT/'Assets/Project/Art/Environment/TeyvatGrassland/LayoutR3/Data/TG_LayoutPlan.json'
HEIGHT=ROOT/'Assets/Project/Art/Environment/TeyvatGrassland/LayoutR3/Data/TG_LayoutTerrainSource.json'
SEED=606731;objects=[];records=[]

def point(p):return (p[0],p[2],p[1])
def surface(name,positions,uv,tri,slot='River',colors=None):
    g=h.Geometry()
    for i,p in enumerate(positions):g.vertex(point(p),colors[i] if colors else (0,0,0,1),None)
    # Reflection of Y/Z reverses handedness. Reverse each polygon before export.
    for indices in tri:g.face(tuple(reversed(indices)),tuple(uv[i] for i in reversed(indices)),0,True)
    obj=g.object(name,[mats[slot]],collection,'R=stable effect phase; G/B=0; A=patch opacity. Water geometry rigid; shared clock animates shader.',SEED)
    objects.append(obj);d=h.object_manifest(obj,axis_conversion(to_forward='-Z',to_up='Y').to_3x3())
    d.update(role=slot,bounds_unity_local_metres=h.bounds(positions),unity_normal_min=[d['normal_component_min'][k] for k in (0,2,1)],unity_normal_max=[d['normal_component_max'][k] for k in (0,2,1)])
    records.append(d)

def grid(name,rows,cols,fn,slot):
    p=[];uv=[];tri=[]
    for i in range(rows+1):
        for j in range(cols+1):
            pos,tex=fn(i/rows,j/cols);p.append(pos);uv.append(tex)
            if i<rows and j<cols:
                k=i*(cols+1)+j;tri.extend(((k,k+1,k+cols+2),(k,k+cols+2,k+cols+1)))
    surface(name,p,uv,tri,slot)

def disk(name,center,rx,rz,slot='River',phase=.3,opacity=1):
    p=[center];uv=[(.5,.5)];tri=[];colors=[(phase,0,0,opacity)]
    rings=6;segments=64
    for ring in range(1,rings+1):
        r=ring/rings
        for j in range(segments):
            a=j/segments*math.tau;variation=1+.045*math.sin(a*5+phase*6)+.025*math.cos(a*9)
            x=math.cos(a)*r*variation;z=math.sin(a)*r*variation
            p.append((center[0]+x*rx,center[1],center[2]+z*rz));uv.append((x*.5+.5,z*.5+.5));colors.append((phase,0,0,opacity))
    for j in range(segments):tri.append((0,1+j,1+(j+1)%segments))
    for ring in range(rings-1):
        a=1+ring*segments;b=a+segments
        for j in range(segments):k=(j+1)%segments;tri.extend(((a+j,b+j,b+k),(a+j,b+k,a+k)))
    surface(name,p,uv,tri,slot,colors)

def card(name,center,width,height,yaw,phase,opacity=.6):
    a=math.radians(yaw);right=Vector((math.cos(a),0,math.sin(a)));up=Vector((0,1,0));c=Vector(center)
    positions=[tuple(c+right*width*x+up*height*y) for x,y in ((-.5,-.5),(.5,-.5),(.5,.5),(-.5,.5))]
    surface(name,positions,[(0,0),(1,0),(1,1),(0,1)],[(0,1,2),(0,2,3)],'Mist',[(phase,0,0,opacity)]*4)

def fall(f):
    points=[Vector(p) for p in f['path']];lengths=[0]
    for a,b in zip(points,points[1:]):lengths.append(lengths[-1]+(b-a).length)
    rows=100;cols=24;p=[];uv=[];tri=[]
    for row in range(rows+1):
        distance=lengths[-1]*row/rows;segment=min(len(points)-2,next((j for j in range(len(points)-1) if distance<=lengths[j+1]),len(points)-2))
        t=(distance-lengths[segment])/(lengths[segment+1]-lengths[segment]);center=points[segment].lerp(points[segment+1],t)
        # Keep the canonical center course; slight mesh bow/edge irregularity
        # breaks a ruler-straight curtain without moving the terrain or anchors.
        for col in range(cols+1):
            u=col/cols;v=row/rows;half=f['half_width']*(1+.07*math.sin(v*17+.6)+.04*math.sin(v*41))
            y=center.y+.13;z=center.z-.13-.08*math.sin(u*math.pi)*math.sin(v*math.pi)
            p.append((center.x+(u*2-1)*half,y,z));uv.append((u,v))
            if row<rows and col<cols:
                k=row*(cols+1)+col;tri.extend(((k,k+1,k+cols+2),(k,k+cols+2,k+cols+1)))
    surface('SM_WP06_Cascade_'+f['name'],p,uv,tri,'Waterfall')
    for index in range(2):
        q=points[2 if index==0 else -1].copy();q.y=points[2].y+.12 if index==0 else plan['water_level']+.045
        disk('SM_WP06_CatchPool_'+f['name']+'_'+str(index),tuple(q),f['half_width']+1.0,1.8,'Pool')
        disk('SM_WP06_ImpactFoam_'+f['name']+'_'+str(index),tuple(q+Vector((0,.045,-.2))),f['half_width']+1.8,2.5,'Foam',.2+index*.4,.95)
        disk('SM_WP06_RippleFan_'+f['name']+'_'+str(index),tuple(q+Vector((.6,.075,-2.3))),f['half_width']+3.8,4.2,'Foam',.6+index*.13,.45)
        for j in range(2):
            card('SM_WP06_Mist_'+f['name']+'_'+str(index)+'_'+str(j),tuple(q+Vector(((-1 if j==0 else 1)*f['half_width']*.28,1.3,-.65))),
                 f['half_width']*2.0,3.6,(-18 if j==0 else 30),.15+j*.43,.55 if index==0 else .45)

def shore_contours(height):
    n=height['resolution'];px,py,pz=height['position'];sx,sy,sz=height['size'];values=height['heights_local_metres'];level=plan['water_level'];p=[];uv=[];tri=[]
    # Marching-square contour at the real native shoreline, with a short strip
    # extending onto the wet side. It is masked irregularly, not a white ring.
    def sample(x,z):return values[z*n+x]
    for iz in range(n-1):
        z=pz+iz*sz/(n-1)
        if z<17 or z>77:continue
        for ix in range(n-1):
            x=px+ix*sx/(n-1)
            if abs(x)>140:continue
            coords=[(ix,iz),(ix+1,iz),(ix+1,iz+1),(ix,iz+1)];hits=[]
            for a,b in zip(coords,coords[1:]+coords[:1]):
                ha,hb=sample(*a),sample(*b)
                if (ha-level)*(hb-level)<0:
                    t=(level-ha)/(hb-ha);hx=px+(a[0]+(b[0]-a[0])*t)*sx/(n-1);hz=pz+(a[1]+(b[1]-a[1])*t)*sz/(n-1);hits.append(Vector((hx,level+.04,hz)))
            if len(hits)!=2:continue
            a,b=hits;dx=sample(ix+1,iz)-sample(ix,iz);dz=sample(ix,iz+1)-sample(ix,iz);wet=h.unit((-dx,0,-dz))
            k=len(p)
            for q in (a,b,b+wet*.55,a+wet*.55):p.append(tuple(q));uv.append((q.x*.21,q.z*.21))
            tri.extend(((k,k+1,k+2),(k,k+2,k+3)))
    # Shore strip uses world-coordinate contact breakup rather than radial foam.
    surface('SM_WP06_ShoreFoam',p,uv,tri,'Shore',[(.35,0,0,.65)]*len(p))

def main():
    global plan,mats,collection
    plan=json.loads(PLAN.read_text());height=json.loads(HEIGHT.read_text())
    bpy.ops.wm.read_factory_settings(use_empty=True);addon_utils.enable('io_scene_fbx',default_set=False)
    bpy.context.scene.unit_settings.system='METRIC';bpy.context.scene.unit_settings.scale_length=1
    collection=bpy.data.collections.new('WP06 water surfaces, canonical local layout');bpy.context.scene.collection.children.link(collection)
    mats={k:h.material(k,c) for k,c in {'River':(.08,.52,.64),'Waterfall':(.50,.85,.89),'Pool':(.14,.67,.70),'Foam':(.86,.95,.93),'Mist':(.78,.91,.94),'Shore':(.77,.91,.88)}.items()}
    def river(t,s):
        x=-192+t*384;near=23+2.8*math.sin(x*.037)+math.sin(x*.13)-3
        far=57+9*math.exp(-((x+43)/20)**2)+10*math.exp(-((x-43)/24)**2)+1
        z=near+(far-near)*s
        return ((x,plan['water_level'],z),(x*.065,z*.065))
    grid('SM_WP06_River',192,32,river,'River')
    for f in plan['waterfalls']:fall(f)
    shore_contours(height)
    for island in plan['islands']:
        # Sparse downstream wakes are asymmetric; no full white island ring.
        disk('SM_WP06_IslandWake_'+island['name'],(island['x']+island['width']*.53,plan['water_level']+.07,island['z']-1.2),island['width']*.35,island['depth']*.34,'Foam',.19,.32)
    marker=h.axis_marker(SEED).object('SM_WP06_AxisMarker',list(mats.values())[:3],collection,'Rigid axis diagnostic',SEED);objects.append(marker)
    d=h.object_manifest(marker,axis_conversion(to_forward='-Z',to_up='Y').to_3x3());d.update(role='Diagnostic',bounds_unity_local_metres=h.bounds([(v.co.x,v.co.z,v.co.y) for v in marker.data.vertices]));records.append(d)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];fbx=HERE/'TG_WaterKit.fbx'
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH'},global_scale=1,apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS',axis_forward='-Z',axis_up='Y',use_space_transform=True,bake_space_transform=True,
        use_mesh_modifiers=False,use_triangles=False,mesh_smooth_type='OFF',use_tspace=True,colors_type='LINEAR',prioritize_active_color=True,use_custom_props=True,bake_anim=False)
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'TG_WaterKit.blend'),compress=True)
    manifest={'schema':'TG_WP06_WaterKit_v1','seed':SEED,'blender_version':bpy.app.version_string,'generator_sha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
              'plan_sha256':hashlib.sha256(PLAN.read_bytes()).hexdigest(),'fbx_sha256':hashlib.sha256(fbx.read_bytes()).hexdigest(),
              'coordinates':'Metres, full R3 arena-local surfaces; Unity applies arena origin exactly once. RawUnity=(BlenderX,BlenderZ,BlenderY).',
              'flow_uv':'Cascade U0..1 across width; V0..1 cumulative physical arc distance increasing source to impact. River UV worldmetres*.065. Foam radialpatchUV0..1, shorestripworldXZ*.21; effects vertexR stablephase,Aopacity, GB0.',
              'objects':records,'triangles':sum(d['expected_triangles_after_export'] for d in records)}
    side=HERE/'TG_WaterKit.manifest.json';side.write_text(json.dumps(manifest,indent=2)+'\n')
    target=ROOT/'Assets/Project/Art/Environment/TeyvatGrassland/Water/Models';target.mkdir(parents=True,exist_ok=True)
    for p in (fbx,side):shutil.copy2(p,target/p.name)
    data=ROOT/'Assets/Project/Art/Environment/TeyvatGrassland/Water/Data';data.mkdir(parents=True,exist_ok=True);shutil.copy2(PLAN,HERE/'TG_WaterPlan.json');shutil.copy2(PLAN,data/'TG_WaterPlan.json')
    print(json.dumps({'objects':len(objects),'triangles':manifest['triangles'],'fbx':str(fbx)}))

if __name__=='__main__':main()
