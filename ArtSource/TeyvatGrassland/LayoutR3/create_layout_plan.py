"""Canonical R3 terrain sample data; Unity imports these exact heights.
Starting dimensions are judged through retained battle cameras, not a budget.
"""
from pathlib import Path
import json, math
HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
def smooth(a,b,x):
    t=max(0,min(1,(x-a)/(b-a)));return t*t*(3-2*t)
def gauss(x,z,cx,cz,rx,rz):return math.exp(-((x-cx)/rx)**2-((z-cz)/rz)**2)
def front(x,tier=0):
    return (78+10*math.sin(x*.019+.35)+4*math.sin(x*.073)) if tier==0 else (110+7*math.sin(x*.025+1.7))
def tierheight(x,tier=0):
    return (14+8*math.exp(-((x+24)/40)**2)+4*math.exp(-((x+80)/31)**2)-8*math.exp(-((x-22)/12)**2)) if tier==0 else (13+10*math.exp(-((x+65)/42)**2))
def nearbank(x):return 23+2.8*math.sin(x*.037)+1.0*math.sin(x*.13)
def farbank(x):return 57+9*math.exp(-((x+43)/20)**2)+10*math.exp(-((x-43)/24)**2)
ISLANDS=[dict(name='Heron',x=-45,z=37,width=12,depth=7,top=.7,rotation=-18),dict(name='Willow',x=-12,z=43,width=15,depth=8,top=1.1,rotation=22),dict(name='Split',x=30,z=40,width=10,depth=6,top=.5,rotation=-25),dict(name='LeftCascade',x=-42,z=55,width=8,depth=5,top=2.5,rotation=15),dict(name='RightCascade',x=43,z=53,width=13,depth=8,top=1.7,rotation=-13)]
def height(x,z):
    # Unequal surrounding mounds and lowered camera corridor. Anchors at±5Z
    # remain exactly y0 before native terrain quantization.
    base=.35+.32*math.sin(x*.055+z*.038)+.23*math.sin(z*.11-x*.026)
    base+=3.4*gauss(x,z,-31,-4,14,17)+2.8*gauss(x,z,34,10,16,16)+2.6*gauss(x,z,-32,-32,22,13)+3.3*gauss(x,z,29,-38,17,18)
    base+=2.1*gauss(x,z,-66,2,25,24)+1.8*gauss(x,z,73,-4,28,26)
    base+=smooth(-43,-103,z)*(5+6*gauss(x,z,-35,-100,30,25)+8*gauss(x,z,51,-103,28,30)+2.5*math.sin(x*.034))
    core=smooth(10,21,abs(x))*smooth(7,19,abs(z))
    # A low smoothly bounded corridor spans all retained attack/contact views.
    protect=(1-smooth(12,22,abs(x)))*(1-smooth(9,20,abs(z)))
    base*=1-protect
    n=nearbank(x);f=farbank(x)
    river=smooth(n-6,n+2,z)*(1-smooth(f-1,f+9,z))
    base=base*(1-river)+(-3.8+.35*math.sin(x*.09))*river
    # Rounded banks behind the water, then broken low foothill benches.
    toe=3.0*gauss(x,z,-61,66,25,14)+4.8*gauss(x,z,5,72,19,13)+2.5*gauss(x,z,62,74,24,14)
    base+=toe*(1-river)
    t1=tierheight(x)*smooth(front(x)+.4,front(x)+4.5,z)
    t2=tierheight(x,1)*smooth(front(x,1)+.6,front(x,1)+5,z)
    base+=t1+t2
    base+=8*smooth(145,210,z)+4*math.sin(x*.021)*smooth(145,190,z)
    # Flat channels are not cut through island tops; low banks taper naturally.
    for island in ISLANDS:
        a=math.radians(island['rotation']);dx=x-island['x'];dz=z-island['z'];u=dx*math.cos(a)+dz*math.sin(a);v=-dx*math.sin(a)+dz*math.cos(a)
        radius=math.sqrt((u/(island['width']*.5))**2+(v/(island['depth']*.5))**2)
        shape=1-smooth(.56,1.2,radius)
        if shape>0:base=max(base,-3.5+(island['top']+3.5)*shape+.12*math.sin(u*.55)*shape)
    return base
def main():
    falls=[]
    for name,x,half in [('Left',-43,2.5),('Hero',-12,4.4),('Right',43,3.2)]:
        crestz=front(x)+4.5;crest=height(x,crestz)+.06
        # Real cascade geometry: main drop -> landing pool -> second spill -> toe.
        midz=crestz-9;basez=min(farbank(x)-2,midz-10);lowz=basez+3
        path=[[x,crest,crestz+2],[x,crest,crestz],[x+.5,6.8 if name=='Hero' else 4.2,midz],[x+.7,6.5 if name=='Hero' else 3.9,midz-3],[x-.2,1.1,lowz],[x-.4,-1.27,basez]]
        falls.append(dict(name=name,x=x,half_width=half,front_z=crestz,crest_y=crest,path=path,path_flat=[v for p in path for v in p]))
    plan=dict(schema='TG_LayoutR3_Plan_v1',revision='WP01-r3',seed=20261021,units='metres',origin=json.loads((ROOT/'Assets/Project/Art/Environment/TeyvatGrassland/FoundationR2/TG_Layout.json').read_text())['origin'],water_level=-1.35,terrain_position=[-192,-12,-128],terrain_size=[384,90,384],resolution=513,islands=ISLANDS,waterfalls=falls,notes='User-authorized layout redesign outside retained six anchors and battle camera corridor. Three unequal recessed cascade courses, five separated river islands, broken cliff toes/grass terraces. Old WP01/WP03/WP04 assets retained as prior revisions. Grass1.2-1.8m relative3mproxy.')
    (HERE/'TG_LayoutPlan.json').write_text(json.dumps(plan,indent=2))
    n=plan['resolution'];heights=[[height(-192+x*384/(n-1),-128+z*384/(n-1)) for x in range(n)] for z in range(n)]
    (HERE/'TG_LayoutTerrainSource.json').write_text(json.dumps(dict(schema='TG_LayoutR3_ExactHeightSamples_v1',position=plan['terrain_position'],size=plan['terrain_size'],resolution=n,heights_local_metres=[h for row in heights for h in row]),separators=(',',':')))
    dest=ROOT/'Assets/Project/Art/Environment/TeyvatGrassland/LayoutR3/Data';dest.mkdir(parents=True,exist_ok=True)
    for name in ('TG_LayoutPlan.json','TG_LayoutTerrainSource.json'):(dest/name).write_bytes((HERE/name).read_bytes())
    registry=json.loads((ROOT/'Assets/Project/Art/Environment/TeyvatGrassland/FoundationR2/TG_Layout.json').read_text());registry.update(revision='WP01-r3',status='Revised surrounding topography, river islands, cascade ledges and grass height per user. Six actor anchors and17 original cameras retained; use LayoutR3 plan/native terrain/geometry rather than r2 surface.',grassHeightRatio=[.4,.6],spatialPlan='TG_LayoutPlan.json')
    (dest/'TG_Layout.json').write_text(json.dumps(registry,indent=2))
    print(json.dumps(dict(terrainMin=min(map(min,heights)),terrainMax=max(map(max,heights)),waterfalls=falls)))
if __name__=='__main__':main()
