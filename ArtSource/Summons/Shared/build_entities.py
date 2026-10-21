"""Editable textured Generic summon rigs. Blender 5.1, deterministic explicit FBX exports.
Run: blender --background --factory-startup --python build_entities.py -- Guoba
Internal feather rows, fur markings, paw pads and gold engraving live in the atlas.
"""
import bpy, math, json, hashlib, shutil, sys
from pathlib import Path
from mathutils import Vector, Matrix
import numpy as np

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / 'ArtSource/Summons'
TILES = {'body':0, 'head':1, 'plain':2, 'ear':3, 'paw':4, 'dark':5,
         'wing':6, 'eye':7, 'gold':8, 'panel':9, 'pepper':10, 'green':11}
SIZE = 1024
FPS = 30
parts = []
bones = {}

def atlas(entity, folder):
    a = np.ones((SIZE,SIZE,4), dtype=np.float32)
    y,x = np.mgrid[0:256,0:256] / 255.0
    def tile(name, color):
        i=TILES[name]; row,col=divmod(i,4)
        p=a[row*256:(row+1)*256,col*256:(col+1)*256]
        p[:]=color
        return p
    def ellipse(p,c,r,color):
        d=((x-c[0])/r[0])**2+((y-c[1])/r[1])**2
        t=np.clip((1.03-d)*32,0,1)[...,None]
        p[:]=p*(1-t)+np.array(color)*t
    def line(p,x0,y0,x1,y1,w,color):
        dx=x1-x0;dy=y1-y0;t=np.clip(((x-x0)*dx+(y-y0)*dy)/(dx*dx+dy*dy+1e-8),0,1)
        d=np.sqrt((x-x0-t*dx)**2+(y-y0-t*dy)**2)
        mask=np.clip((w-d)*256,0,1)[...,None];p[:]=p*(1-mask)+np.array(color)*mask
    if entity=='Guoba':
        tan=(.97,.66,.25,1); brown=(.30,.16,.075,1); cream=(1,.96,.79,1)
        for name in TILES: tile(name,tan)
        p=tile('head',tan)
        p[:,:,:3]*=(.86+.14*y)[...,None]
        for side in [-1,1]:
            ellipse(p,(.5+side*.32,.34),(.13,.14),(.92,.43,.12,1))
            ellipse(p,(.5+side*.205,.47),(.17,.155),brown)
            ellipse(p,(.5+side*.205,.47),(.074,.089),cream)
            ellipse(p,(.5+side*.15,.71),(.04,.07),brown)
        ellipse(p,(.5,.75),(.115,.16),cream)
        ellipse(p,(.5,.65),(.085,.15),cream)
        tile('plain',tan); p=tile('body',tan)
        ellipse(p,(.5,.48),(.36,.44),brown);ellipse(p,(.5,.49),(.27,.41),cream)
        p=tile('ear',tan);ellipse(p,(.5,.5),(.48,.48),(.77,.40,.11,1));ellipse(p,(.5,.5),(.29,.31),(.89,.52,.18,1))
        p=tile('paw',cream);ellipse(p,(.5,.37),(.24,.23),brown)
        for u,v in [(.23,.63),(.40,.76),(.61,.76),(.78,.61)]: ellipse(p,(u,v),(.082,.105),brown)
        tile('dark',brown); tile('eye',cream)
        p=tile('pepper',(.9,.1,.045,1));p[:,:,:3]*=(.66+.34*y)[...,None]
        tile('green',(.21,.40,.08,1))
    else:
        for name in TILES: tile(name,(.14,.055,.34,1))
        for name in ['head','body','plain','wing']:
            p=tile(name,(.17,.075,.39,1))
            p[:,:,:3]*=(.65+.35*y)[...,None]
            for row in range(6):
                v=.10+row*.16
                for col in range(5):
                    u=.05+col*.23+(row%2)*.11
                    line(p,u-.08,v+.11,u,v,.008,(.25,.115,.45,1))
                    line(p,u,v,u+.10,v+.10,.008,(.25,.115,.45,1))
        p=tile('wing',(.23,.09,.52,1))
        band=((y>.32)&(y<.42))|((y>.58)&(y<.69))
        p[band]=(.57,.29,.9,1)
        line(p,.5,.02,.5,.99,.006,(.45,.24,.74,1))
        for v in np.arange(.12,.96,.08):
            line(p,.5,v,.03,v+.13,.003,(.32,.14,.60,1));line(p,.5,v,.97,v+.13,.003,(.32,.14,.60,1))
        tile('ear',(.39,.29,.55,1));tile('eye',(.87,.74,1,1));tile('dark',(.035,.018,.085,1))
    p=tile('gold',(.82,.49,.10,1))
    p[:,:,:3]*=(.75+.25*np.sin(y*math.pi))[...,None]
    for b in [.04,.1,.90,.96]: line(p,b,0,b,1,.009,(1,.83,.38,1))
    for v in np.arange(.12,1,.22):
        for u in [.29,.71]:
            r=np.sqrt((x-u)**2+(y-v)**2);ang=np.arctan2(y-v,x-u)
            mask=(np.abs(r-(.055+.014*ang))<.007)&(r<.105)
            p[mask]=(1,.82,.35,1)
    p=tile('panel',(.47,.39,.68,.20))
    p[:,:,:3] += (.06*np.cos(y*math.pi))[...,None]
    for u in [.02,.25,.5,.75,.98]: line(p,u,0,u,1,.005,(.93,.81,.56,.48))
    # Double circular emblem and four rotated Geo chevrons, painted rather than extruded.
    r=np.sqrt(((x-.5)*1.45)**2+(y-.54)**2)
    p[(abs(r-.29)<.009)|(abs(r-.32)<.004)]=(1,.91,.67,.82)
    diamond=np.abs((x-.5)*1.45)+np.abs(y-.54)
    p[(abs(diamond-.21)<.012)|(abs(diamond-.25)<.006)|(abs(diamond-.065)<.010)]=(1,.88,.53,.92)
    for sign in [-1,1]:
        line(p,.5+sign*.08,.54,.5+sign*.08,.54+sign*.12,.009,(1,.88,.53,.92))
    for side in [-1,1]:
        for j in range(4):
            u=.5+side*(.11+j*.105);v=.12+j*.012
            ellipse(p,(u,v),(.042,.016),(.96,.83,.57,.65))
            line(p,u-.06,v-.025,u+.06,v-.025,.004,(.96,.83,.57,.65))
    for v in [.035,.965]:line(p,0,v,1,v,.006,(1,.86,.54,.73))
    im=bpy.data.images.new(entity+'_BaseColor',width=SIZE,height=SIZE,alpha=True)
    im.pixels.foreach_set(a.ravel());im.filepath_raw=str(folder/(entity.lower()+'_basecolor.png'));im.file_format='PNG';im.save()
    mask=np.zeros_like(a);mask[:,: ,3]=1
    # R emissive, G opacity. Deliberately documented channels, no wind-map reuse.
    mask[:,:,1]=a[:,:,3]
    if entity=='Oz':
        i=TILES['eye'];row,col=divmod(i,4);mask[row*256:(row+1)*256,col*256:(col+1)*256,0]=1
    if entity=='JadeScreen':
        i=TILES['panel'];row,col=divmod(i,4);mask[row*256:(row+1)*256,col*256:(col+1)*256,0]=a[row*256:(row+1)*256,col*256:(col+1)*256,3]*.38
    im2=bpy.data.images.new(entity+'_Masks',width=SIZE,height=SIZE,alpha=True)
    im2.pixels.foreach_set(mask.ravel());im2.filepath_raw=str(folder/(entity.lower()+'_masks.png'));im2.file_format='PNG';im2.save()
    mat=bpy.data.materials.new(entity+'_Paint');mat.use_nodes=True
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=im
    bs=mat.node_tree.nodes.get('Principled BSDF');mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']);bs.inputs['Roughness'].default_value=.78
    return mat

def tileuv(u,v,tile):
    i=TILES[tile];row,col=divmod(i,4)
    u=min(1,max(0,u));v=min(1,max(0,v))
    # 3px inset prevents bleeding under bilinear/mip sampling.
    return (col+(3+u*249)/256)/4,(row+(3+v*249)/256)/4

def finish(obj, bone, tile='plain', uv=None):
    obj.data.materials.append(material)
    if uv:
        layer=obj.data.uv_layers.new(name='UVMap') if not obj.data.uv_layers else obj.data.uv_layers.active
        for poly in obj.data.polygons:
            for li in poly.loop_indices: layer.data[li].uv=tileuv(*uv(obj.data.vertices[obj.data.loops[li].vertex_index].co),tile)
    else:
        for d in obj.data.uv_layers.active.data: d.uv=tileuv(d.uv.x,d.uv.y,tile)
    for p in obj.data.polygons:p.use_smooth=True
    obj.vertex_groups.new(name=bone).add(list(range(len(obj.data.vertices))),1,'REPLACE')
    parts.append(obj)
    return obj

def ell(name, center, radius, bone, tile='plain', segments=24,rings=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,location=center)
    obj=bpy.context.object;obj.name=name
    # Apply geometry transform before armature binding; projected front gets a dedicated atlas island.
    for v in obj.data.vertices:v.co.x*=radius[0];v.co.y*=radius[1];v.co.z*=radius[2]
    def uv(co):return .5+co.x/radius[0]*.5,.5+co.z/radius[2]*.5
    finish(obj,bone,tile,uv)
    if tile in ['head','body','paw','ear']:
        layer=obj.data.uv_layers.active
        for p in obj.data.polygons:
            if sum(obj.data.vertices[i].co.y for i in p.vertices)>0:
                for li in p.loop_indices:layer.data[li].uv=tileuv(.5,.5,'plain')
    return obj

def mesh(name,verts,faces,bone,tile='plain',uv=None):
    data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update()
    obj=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(obj)
    if uv is None:
        data.uv_layers.new(name='UVMap')
        uv=lambda p:(p.x%1,p.z%1)
    return finish(obj,bone,tile,uv)

def box(name,center,scale,bone,tile='gold'):
    bpy.ops.mesh.primitive_cube_add(size=1,location=center);obj=bpy.context.object;obj.name=name
    for v in obj.data.vertices:v.co.x*=scale[0];v.co.y*=scale[1];v.co.z*=scale[2]
    bevel=obj.modifiers.new('Silhouette bevel','BEVEL');bevel.width=.025;bevel.segments=2
    bpy.context.view_layer.objects.active=obj;bpy.ops.object.modifier_apply(modifier=bevel.name)
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.cube_project(cube_size=1);bpy.ops.object.mode_set(mode='OBJECT')
    layer=obj.data.uv_layers.active
    for poly in obj.data.polygons:
        uvs=[layer.data[i].uv.copy() for i in poly.loop_indices]
        lo=[min(p[a] for p in uvs) for a in [0,1]];hi=[max(p[a] for p in uvs) for a in [0,1]]
        for li,uv in zip(poly.loop_indices,uvs):layer.data[li].uv=((uv.x-lo[0])/max(.0001,hi[0]-lo[0]),(uv.y-lo[1])/max(.0001,hi[1]-lo[1]))
    return finish(obj,bone,tile)

def tube(name,points,radii,bone,tile='plain',sides=10):
    verts=[];faces=[]
    for i,(p,r) in enumerate(zip(points,radii)):
        direction=Vector(points[min(len(points)-1,i+1)])-Vector(points[max(0,i-1)])
        direction.normalize();a=direction.cross(Vector((0,1,0)))
        if a.length<.01:a=direction.cross(Vector((1,0,0)))
        a.normalize();b=direction.cross(a).normalized()
        for j in range(sides):verts.append(Vector(p)+r*(a*math.cos(j*math.tau/sides)+b*math.sin(j*math.tau/sides)))
    for i in range(len(points)-1):
        for j in range(sides):faces.append((i*sides+j,i*sides+(j+1)%sides,(i+1)*sides+(j+1)%sides,(i+1)*sides+j))
    faces.extend([tuple(reversed(range(sides))),tuple((len(points)-1)*sides+j for j in range(sides))])
    return mesh(name,verts,faces,bone,tile)

def feather(name,start,end,width,bone,tile='wing'):
    start=Vector(start);end=Vector(end);d=end-start
    side=d.cross(Vector((0,1,0))).normalized()*width
    verts=[];rows=9
    for i in range(rows):
        t=i/(rows-1);c=start+d*t+Vector((0,-.04*math.sin(t*math.pi),.035*math.sin(t*math.pi)))
        w=max(.008,math.sin(t*math.pi)**.6)
        for s,depth in [(-1,0),(0,-.035),(1,0),(0,.035)]:verts.append(c+side*s*w+Vector((0,depth*w,0)))
    faces=[]
    for i in range(rows-1):
        for j in range(4):faces.append((i*4+j,i*4+(j+1)%4,(i+1)*4+(j+1)%4,(i+1)*4+j))
    faces.extend([(3,2,1,0),tuple((rows-1)*4+j for j in range(4))])
    obj=mesh(name,verts,faces,bone,tile,lambda p:(.5,.5))
    layer=obj.data.uv_layers.active
    for p in obj.data.polygons:
        for li in p.loop_indices:
            vi=obj.data.loops[li].vertex_index;layer.data[li].uv=tileuv([.02,.5,.98,.5][vi%4],(vi//4)/(rows-1),tile)
    return obj

def bone(name,head,tail,parent=None):bones[name]=(head,tail,parent)

def guoba():
    bone('Root',(0,0,0),(0,0,.15));bone('Body',(0,0,.35),(0,0,.95),'Root');bone('Head',(0,0,1.0),(0,0,1.5),'Body')
    bone('Jaw',(0,-.32,1.10),(0,-.42,1.02),'Head')
    ell('Round seated body',(0,0,.65),(.44,.32,.53),'Body','body')
    ell('Painted face',(0,-.025,1.27),(.48,.35,.43),'Head','head',32,24)
    ell('Cream muzzle',(0,-.315,1.09),(.26,.125,.155),'Head','eye')
    ell('Nose',(0,-.438,1.16),(.055,.028,.04),'Head','dark')
    ell('Mouth',(0,-.435,1.055),(.08,.018,.038),'Jaw','dark')
    for sign,label in [(-1,'L'),(1,'R')]:
        ell('Ear.'+label,(sign*.345,-.005,1.63),(.17,.12,.185),'Head','ear')
        bone('Arm.'+label,(sign*.32,0,.95),(sign*.39,-.1,.60),'Body')
        ell('Arm.'+label,(sign*.39,-.13,.79),(.14,.16,.28),'Arm.'+label,'dark')
        ell('Hand.'+label,(sign*.33,-.31,.70),(.14,.12,.15),'Arm.'+label,'eye')
        bone('Foot.'+label,(sign*.25,0,.32),(sign*.25,-.3,.15),'Root')
        ell('Foot.'+label,(sign*.28,-.20,.20),(.195,.23,.195),'Foot.'+label,'paw')
    tube('Held chili',[(-.49,-.36,.96),(-.38,-.43,.89),(-.03,-.45,.86),(.35,-.43,.90),(.62,-.41,1.00)], [.035,.105,.09,.055,.004],'Body','pepper',16)
    tube('Chili stem',[(-.49,-.36,.96),(-.56,-.35,1.02),(-.57,-.34,1.09)],[.04,.025,.008],'Body','green')
    ell('Tail',(0,.31,.37),(.15,.17,.17),'Body','plain')
    return {'Emit':(0,-.49,1.08),'Center':(0,0,.9),'Placement':(0,0,0)}

def oz():
    bone('Root',(0,0,0),(0,0,.2));bone('Body',(0,0,.45),(0,0,1.05),'Root');bone('Head',(0,-.02,1.07),(0,-.04,1.48),'Body')
    ell('Raven breast',(0,.03,.83),(.24,.23,.43),'Body','body')
    ell('Raven head',(0,-.055,1.25),(.225,.20,.245),'Head','head')
    mesh('Obsidian beak',[(-.10,-.21,1.31),(.10,-.21,1.31),(0,-.62,1.14),(0,-.22,1.19),(0,-.23,1.38)],[(0,4,2),(4,1,2),(1,3,2),(3,0,2),(0,3,1,4)],'Head','ear')
    for sign,label in [(-1,'L'),(1,'R')]:
        ell('Eye.'+label,(sign*.15,-.208,1.32),(.035,.022,.041),'Head','eye',16,10)
        ell('Pupil.'+label,(sign*.15,-.232,1.32),(.011,.007,.024),'Head','dark',12,8)
        feather('Crown.'+label,(sign*.13,.02,1.37),(sign*.24,.10,1.76),.043,'Head')
        bone('Wing.'+label,(sign*.19,.03,1.05),(sign*.88,.10,1.17),'Body')
        bone('WingTip.'+label,(sign*.88,.10,1.17),(sign*1.40,.24,1.12),'Wing.'+label)
        feather('Wing volume.'+label,(sign*.18,.03,1.04),(sign*1.43,.14,1.32),.28,'Wing.'+label)
        for j in range(7):
            sx=.32+j*.14;ex=.49+j*.17
            feather('Primary.%s.%02d'%(label,j),(sign*sx,.095,1.10+j*.017),(sign*ex,.18+j*.035,.40+j*.045),.11 if j<4 else .13,'Wing.'+label if j<3 else 'WingTip.'+label)
        bone('Leg.'+label,(sign*.10,.02,.51),(sign*.12,-.02,.18),'Body')
        tube('Leg.'+label,[(sign*.10,.02,.53),(sign*.12,.02,.31),(sign*.13,-.03,.20)],[.045,.028,.022],'Leg.'+label)
        for j in range(3):tube('Talon.%s.%s'%(label,j),[(sign*.13,-.02,.20),(sign*(.09+j*.04),-.12,.14),(sign*(.07+j*.055),-.18,.15)],[.022,.018,.004],'Leg.'+label)
    bone('Tail',(0,.18,.63),(0,.57,.25),'Body')
    for j in range(5):feather('Tail.%02d'%j,((j-2)*.025,.17,.62),((j-2)*.13,.65,.06+abs(j-2)*.06),.09,'Tail')
    return {'Emit':(0,-.61,1.2),'Center':(0,0,.9),'Placement':(0,0,0)}

def jade():
    bone('Root',(0,0,0),(0,0,.2));bone('Frame',(0,0,.2),(0,0,2.4),'Root');bone('Panel',(0,0,.3),(0,0,2.2),'Frame')
    for z,height in [(.29,.13),(2.30,.19),(2.51,.065)]:box('Gold crossbeam', (0,0,z),(3.85,.19,height),'Frame')
    for sign,label in [(-1,'L'),(1,'R')]:
        bone('Ornaments.'+label,(sign*1.8,0,.3),(sign*1.8,0,1.4),'Frame')
        box('Upright.'+label,(sign*1.79,0,1.3),(.13,.19,2.18),'Frame')
        for z in [.40,2.13]:
            box('Corner block.'+label,(sign*1.78,0,z),(.33,.26,.25),'Frame')
            # Major angular corner cutouts are silhouette geometry; engraving is painted.
            for offset in [-.13,.13]:box('Bracket', (sign*1.76+offset,0,z+.12),(.055,.27,.28),'Frame')
        for z,w in [(.07,.65),(.17,.49),(.33,.30)]:box('Foot.'+label,(sign*1.78,0,z),(w,.46,.11),'Frame')
        for z in [.70,1.25,1.80]:
            bpy.ops.mesh.primitive_cone_add(vertices=4,radius1=.16,radius2=0,depth=.23,location=(sign*1.96,0,z))
            obj=bpy.context.object;obj.name='Hanging Geo crystal';finish(obj,'Ornaments.'+label,'gold')
        points=[(sign*.15,0,2.49),(sign*.32,0,2.71),(sign*.57,0,2.68),(sign*.64,0,2.84)]
        tube('Crown scroll.'+label,points,[.04,.055,.035,.008],'Frame','gold')
    # Closed thin panel with explicit front/back UVs; no one-sided billboard illusion.
    obj=mesh('Translucent emblem panel',[(-1.7,-.027,.37),(1.7,-.027,.37),(1.7,-.027,2.2),(-1.7,-.027,2.2),
        (-1.7,.027,.37),(1.7,.027,.37),(1.7,.027,2.2),(-1.7,.027,2.2)],
        [(0,1,2,3),(7,6,5,4),(4,5,1,0),(3,2,6,7),(0,3,7,4),(1,5,6,2)],'Panel','panel',lambda p:((p.x+1.7)/3.4,(p.z-.37)/1.83))
    obj['transparent_panel']=True
    return {'Impact':(0,-.06,1.25),'Center':(0,0,1.3),'Placement':(0,0,0)}

def rig_and_clips(entity,sockets):
    arm=bpy.data.armatures.new('SummonSkeleton');rig=bpy.data.objects.new('Rig',arm);bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
    for name,(head,tail,parent) in bones.items():
        b=arm.edit_bones.new(name);b.head=head;b.tail=tail
        if parent:b.parent=arm.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    for obj in parts:
        obj.parent=rig;mod=obj.modifiers.new('Generic deformation','ARMATURE');mod.object=rig
    for name,position in sockets.items():
        obj=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(obj);obj.parent=rig
        obj.parent_type='BONE';obj.parent_bone='Head' if name=='Emit' and 'Head' in bones else 'Body' if name=='Center' and 'Body' in bones else 'Root'
        bpy.context.view_layer.update();obj.matrix_world=Matrix.Translation(position)
    durations={'Spawn':.8,'Idle':2.0,'Attack':1.25,'Despawn':.65} if entity!='JadeScreen' else {'Spawn':.9,'Idle':2.0,'AbsorbHit':.42,'Break':.85,'Despawn':.65}
    for name,duration in durations.items():
        action=bpy.data.actions.new(name);action.use_fake_user=True
        rig.animation_data_create();rig.animation_data.action=action
        count=round(duration*FPS)
        for f in range(count+1):
            t=f/count;wave=math.sin(t*math.tau);pulse=math.sin(t*math.pi)
            for p in rig.pose.bones:p.location=(0,0,0);p.rotation_mode='XYZ';p.rotation_euler=(0,0,0);p.scale=(1,1,1)
            root=rig.pose.bones['Root'];body=rig.pose.bones.get('Body')
            if name=='Spawn':
                factor=max(.001,min(1,t*1.5));root.scale=(factor,factor,factor);root.location.z=math.sin(t*math.pi)*.15
            elif name=='Despawn':root.scale=(max(.001,1-t),)*3;root.location.z=t*.12
            elif name=='Break':
                root.scale=(1+pulse*.08,1+pulse*.08,max(.001,1-t**2));root.rotation_euler.y=math.sin(t*math.pi*7)*.035
                for label,sign in [('L',-1),('R',1)]:rig.pose.bones['Ornaments.'+label].location.x=sign*t*.45
            elif name=='AbsorbHit':rig.pose.bones['Panel'].scale=(1+pulse*.04,1+pulse*.04,1+pulse*.04);root.rotation_euler.x=math.sin(t*math.pi*3)*.012
            if body:
                if entity=='Guoba':
                    body.scale=(1+.025*wave,1+.02*wave,1-.02*wave) if name=='Idle' else (1+.10*pulse,1+.12*pulse,1-.10*pulse)
                    rig.pose.bones['Head'].rotation_euler.x=wave*.035 if name=='Idle' else math.sin(t*math.pi*2)*.16
                    if name=='Attack':
                        rig.pose.bones['Jaw'].scale=(1,1+pulse*.7,1)
                        for label in ['L','R']:rig.pose.bones['Arm.'+label].rotation_euler.x=pulse*-.25
                else:
                    body.location.z=wave*.055 if name=='Idle' else pulse*.10
                    flap=wave*.18 if name=='Idle' else math.sin(t*math.pi*2)*.38
                    for label,sign in [('L',-1),('R',1)]:
                        rig.pose.bones['Wing.'+label].rotation_euler.y=sign*flap
                        rig.pose.bones['WingTip.'+label].rotation_euler.y=sign*flap*.7
                    rig.pose.bones['Head'].rotation_euler.x=wave*.025 if name=='Idle' else -pulse*.18
                    rig.pose.bones['Tail'].rotation_euler.x=wave*.065
            for p in rig.pose.bones:
                for prop in ['location','rotation_euler','scale']:p.keyframe_insert(data_path=prop,frame=f+1,group=p.name)
        action['duration']=duration
    rig.animation_data.action=bpy.data.actions['Idle']
    bpy.context.scene.frame_set(1)
    return rig,durations

def build(entity):
    global material,parts,bones
    parts=[];bones={}
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    for action in list(bpy.data.actions):bpy.data.actions.remove(action)
    src=SOURCE/entity;src.mkdir(parents=True,exist_ok=True);textures=src/'Textures';textures.mkdir(exist_ok=True)
    material=atlas(entity,textures)
    sockets={'Guoba':guoba,'Oz':oz,'JadeScreen':jade}[entity]()
    # One skinned draw per opaque entity, plus the independent translucent panel.
    # Joining preserves UV islands and named bone groups, not separate material copies.
    joined=[]
    groups=[(transparent,[obj for obj in parts if bool(obj.get('transparent_panel',False))==transparent]) for transparent in [False,True]]
    for transparent,group in groups:
        if not group:continue
        bpy.ops.object.select_all(action='DESELECT')
        for obj in group:obj.select_set(True)
        bpy.context.view_layer.objects.active=group[0]
        if len(group)>1:bpy.ops.object.join()
        obj=bpy.context.object;obj.name='Translucent emblem panel' if transparent else entity+' painted silhouette'
        obj.data.materials.clear();obj.data.materials.append(material)
        for p in obj.data.polygons:p.material_index=0
        joined.append(obj)
    parts=joined
    rig,durations=rig_and_clips(entity,sockets)
    bpy.context.scene.render.fps=FPS
    bpy.ops.wm.save_as_mainfile(filepath=str(src/(entity.lower()+'.blend')))
    bpy.ops.object.select_all(action='SELECT')
    fbx=src/(entity.lower()+'.fbx')
    bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH','ARMATURE','EMPTY'},
        axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL',use_space_transform=True,
        add_leaf_bones=False,armature_nodetype='NULL',bake_anim=True,bake_anim_use_all_actions=True,
        bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='STRIP',use_mesh_modifiers=True)
    dest=ROOT/'Assets/Project/Art/Summons'/entity
    (dest/'Models').mkdir(parents=True,exist_ok=True);(dest/'Textures').mkdir(exist_ok=True)
    shutil.copyfile(fbx,dest/'Models'/fbx.name)
    for path in textures.glob('*.png'):shutil.copyfile(path,dest/'Textures'/path.name)
    verts=[obj.matrix_world@v.co for obj in parts for v in obj.data.vertices]
    manifest={'entity':entity,'source':'Blender 5.1','fps':FPS,'units':'meters','clips':durations,
        'markers':{'Reveal':.28,'Emit':.48,'Impact':.10},'sockets':sockets,'bones':len(bones),
        'vertices':len(verts),'triangles':sum(sum(len(p.vertices)-2 for p in obj.data.polygons) for obj in parts),
        'boundsMin':[min(v[i] for v in verts) for i in range(3)],'boundsMax':[max(v[i] for v in verts) for i in range(3)],
        'fbxSha256':hashlib.sha256(fbx.read_bytes()).hexdigest(),'atlasSize':SIZE,'maskChannels':{'R':'emission','G':'opacity'},
        'budgetPolicy':'Starting values. Compare simultaneous set against scene without entities; reduce hidden geometry/passes/atlas if frame cost fails; enlarge visible markings if unreadable.',
        'backside':'Artist interpretation; no reference background or watermark in textures.'}
    (src/(entity.lower()+'.manifest.json')).write_text(json.dumps(manifest,indent=2))
    print('SUMMON_EXPORT '+json.dumps(manifest))

if __name__=='__main__':
    args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else ['Guoba']
    for entity in args:build(entity)
