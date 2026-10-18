using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatFoundationBuilder
    {
        static Transform Group(string name,Transform parent)
        {
            var t=new GameObject(name).transform;
            if(parent!=null)t.SetParent(parent,false);
            return t;
        }

        static void Palette()
        {
            Make("Ground",new Color(.43f,.65f,.19f));Make("Meadow",new Color(.52f,.72f,.23f));
            Make("GrassCap",new Color(.45f,.68f,.21f));Make("Rock",new Color(.56f,.62f,.66f));
            Make("RockShade",new Color(.43f,.50f,.57f));Make("Trunk",new Color(.35f,.25f,.15f));
            Make("Leaves",new Color(.32f,.61f,.22f));Make("LeafLight",new Color(.54f,.73f,.24f));
            Make("Water",new Color(.16f,.66f,.74f));Make("Waterfall",new Color(.68f,.87f,.91f));
            Make("WhiteFlower",new Color(.96f,.92f,.65f));Make("PurpleFlower",new Color(.59f,.37f,.73f));
            Make("Player",new Color(.22f,.44f,.75f));Make("Enemy",new Color(.68f,.30f,.23f));
            Make("Skin",new Color(.89f,.76f,.57f));Make("FarHill",new Color(.36f,.57f,.62f));
            Make("Cloud",new Color(.96f,.95f,.89f));
        }

        static void Make(string key,Color color)
        {
            var path=Art+"/Materials/TG_Blockout_"+key+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
            m.color=color;m.SetFloat("_Glossiness",key=="Water"?.7f:.12f);m.SetFloat("_Metallic",0);
            EditorUtility.SetDirty(m);Mats[key]=m;
        }

        static GameObject Primitive(string name,PrimitiveType type,Transform parent,Vector3 pos,Vector3 scale,string mat,bool collision=false)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=pos;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=Mats[mat];
            if(!collision)Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static Mesh SaveMesh(Mesh mesh,string name)
        {
            var path=Art+"/Meshes/"+(meshSerial++).ToString("D3")+"_"+name+".asset";
            mesh.name=Path.GetFileNameWithoutExtension(path);var previous=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(previous==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
            // Native Mesh serialization alone can leave a previously rendered GPU buffer stale.
            // Use mesh setters on rebuild so saved data, scene rendering and collision agree.
            previous.Clear();previous.name=mesh.name;previous.indexFormat=mesh.indexFormat;previous.vertices=mesh.vertices;
            previous.subMeshCount=mesh.subMeshCount;
            for(var i=0;i<mesh.subMeshCount;i++)previous.SetTriangles(mesh.GetTriangles(i),i);
            previous.normals=mesh.normals;previous.uv=mesh.uv;
            if(mesh.colors.Length>0)previous.colors=mesh.colors;
            if(mesh.tangents.Length>0)previous.tangents=mesh.tangents;
            previous.RecalculateBounds();previous.UploadMeshData(false);EditorUtility.SetDirty(previous);
            Object.DestroyImmediate(mesh);return previous;
        }

        static GameObject MeshObject(string name,Transform parent,Mesh mesh,string mat,bool collision=false)
        {
            var go=Group(name,parent).gameObject;go.AddComponent<MeshFilter>().sharedMesh=SaveMesh(mesh,name.Replace(' ','_'));
            go.AddComponent<MeshRenderer>().sharedMaterial=Mats[mat];
            if(collision)go.AddComponent<MeshCollider>().sharedMesh=go.GetComponent<MeshFilter>().sharedMesh;
            return go;
        }

        static float Height(float x,float z)
        {
            // Starting layout dimensions. Keep both formation rows and camera clearance flat.
            if(Mathf.Abs(x)<21&&Mathf.Abs(z)<20)return 0;
            var edge=Mathf.Clamp01(Mathf.Max((Mathf.Abs(x)-18)/16,(Mathf.Abs(z)-17)/17));
            var h=(Mathf.PerlinNoise(x*.018f+4,z*.018f+6)*9-2)*edge;
            if(z>21&&z<57)h=Mathf.Lerp(h,-4,Mathf.Sin((z-21)/36*Mathf.PI));
            if(z>70)h+=Mathf.Clamp01((z-70)/25)*15;
            if(z< -32)h+=Mathf.Clamp01((-z-32)/46)*10;
            return h;
        }

        static void BuildLegacyLandscape(Transform root)
        {
            var ground=Group("01 Terrain and protected combat clearing",root);
            const int columns=49,rows=57;var vertices=new Vector3[columns*rows];var uv=new Vector2[vertices.Length];
            var triangles=new List<int>();
            for(var iz=0;iz<rows;iz++)for(var ix=0;ix<columns;ix++)
            {var i=iz*columns+ix;var x=(ix/(float)(columns-1)-.5f)*260;var z=(iz/(float)(rows-1)-.5f)*280;
                vertices[i]=new Vector3(x,Height(x,z),z);uv[i]=new Vector2(x*.05f,z*.05f);
                if(ix<columns-1&&iz<rows-1){triangles.AddRange(new[]{i,i+columns,i+1,i+1,i+columns,i+columns+1});}}
            var mesh=new Mesh{vertices=vertices,uv=uv,triangles=triangles.ToArray()};mesh.RecalculateNormals();
            MeshObject("Terrace and rolling land - height proxy",ground,mesh,"Ground",true);
            var floor=Primitive("Combat ground reference - collider only, matches anchor elevation",PrimitiveType.Cube,ground,new Vector3(0,-.15f,0),new Vector3(36,.3f,32),"Ground",true);
            floor.GetComponent<Renderer>().enabled=false;

            var water=Group("02 River basin and water surface proxy",root);
            Ellipse("River surface - material to replace in WP06",water,new Vector3(0,-1.2f,40),85,16,"Water");
            var cliffs=Group("03 Cliff silhouette and waterfall proxies",root);
            BuildCliffRange(cliffs);
            var waterfallX=new[]{-32f,4f,44f};var waterfallHeights=new[]{21f,25f,20f};
            for(var i=0;i<3;i++)
            {
                var x=waterfallX[i];var h=waterfallHeights[i];
                BuildWaterfall(cliffs,water,x,h,i);
            }

            var trees=Group("04 Tree canopy volume proxies - WP05 replaces",root);
            Tree(trees,new Vector3(-35,Height(-35,5),1),1.35f,0);
            for(var i=0;i<18;i++)
            {
                var side=i%2==0?-1:1;var x=side*(30+(i%5)*12);var z=-46+(i/2)*15;
                Tree(trees,new Vector3(x,Height(x,z),z),.43f+(i%3)*.1f,i+1);
            }
            var ruin=Group("05 Mossy arch and masonry proxies",root);
            Arch(ruin,new Vector3(25,Height(25,9),9),0,1);
            for(var i=0;i<6;i++)Rock("Border boulder "+i,ruin,new Vector3(i%2==0?-21:22,0,-12+i*5),new Vector3(3,2,2.8f),"Rock",i+100);

            var far=Group("06 Distant landmarks and full reverse scenery",root);
            Tower(far,new Vector3(-52,22,108));
            for(var i=0;i<9;i++)
            {
                var x=-194+i*48f;
                Peak(far,new Vector3(x,11+(i%3)*2,166+(i%2)*22),28+(i%3)*5,34+(i%4)*9,1300+i,"FarHill");
                Peak(far,new Vector3(x+16,8+(i%2)*3,206+(i%3)*13),34,42+(i%3)*8,1400+i,"FarHill");
            }
            BuildDistantBridge(far);
            var meadow=Group("07 Meadow and flower mass placeholders - WP04 replaces",root);
            for(var i=0;i<32;i++)
            {
                var angle=i/32f*Mathf.PI*2;var x=Mathf.Cos(angle)*23;var z=Mathf.Sin(angle)*19;
                var y=Height(x,z)+.1f;
                FlowerPatch(meadow,new Vector3(x,y,z),i,i%3==0?"PurpleFlower":"WhiteFlower");
            }
            BuildForegroundGrass(meadow);
            // Cloud masses are depth/sky blockout, not finished foliage or volumetric effects.
            var clouds=Group("08 Sky cloud volumes - atmosphere proxy",root);
            for(var i=0;i<9;i++)
            {
                var angle=i/9f*Mathf.PI*2;
                for(var j=0;j<3;j++)Primitive("Cloud mass",PrimitiveType.Sphere,clouds,
                    new Vector3(Mathf.Cos(angle)*230+j*14,76+j%2*8,Mathf.Sin(angle)*230),new Vector3(45,20,24),"Cloud");
            }
        }

        static void BuildCliffRange(Transform parent)
        {
            // A broken, connected escarpment reads as a landscape landmark from the combat camera.
            const int segments=28;var front=new Vector3[segments+1];var back=new Vector3[segments+1];
            var rng=new System.Random(41018);
            for(var i=0;i<=segments;i++)
            {
                var x=-112+i*8f;var top=22+Mathf.Sin(i*.61f)*2.1f+Mathf.Sin(i*1.37f)*1.25f+(float)rng.NextDouble()*1.4f;
                front[i]=new Vector3(x,top,63+Mathf.Sin(i*.47f)*1.8f);
                back[i]=new Vector3(x,top-2.2f,69+Mathf.Sin(i*.47f)*1.8f);
            }
            var verts=new List<Vector3>();var tri=new List<int>();
            for(var i=0;i<segments;i++)
            {
                var lowerLeft=new Vector3(front[i].x,-3,front[i].z);
                var lowerRight=new Vector3(front[i+1].x,-3,front[i+1].z);
                AddFace(verts,tri,lowerLeft,front[i],front[i+1]);
                AddFace(verts,tri,lowerLeft,front[i+1],lowerRight);
                AddFace(verts,tri,front[i],back[i],back[i+1]);
                AddFace(verts,tri,front[i],back[i+1],front[i+1]);
            }
            var face=new Mesh{vertices=verts.ToArray(),triangles=tri.ToArray()};face.RecalculateNormals();
            MeshObject("Continuous faceted waterfall escarpment",parent,face,"Rock");
            for(var i=0;i<segments;i++)
            {
                var width=7.6f;var p=(front[i]+front[i+1])*.5f;
                Rock("Broken cliff ledge rock "+i,parent,new Vector3(p.x,p.y-1.1f,p.z-1.2f),new Vector3(width*.48f,2.4f,2.5f),i%4==0?"RockShade":"Rock",500+i);
                if(i%2==0)
                    Rock("Escarpment fracture buttress "+i,parent,new Vector3(p.x,1.5f+Mathf.Sin(i*.8f)*2,58.4f),
                        new Vector3(2.4f+(i%3)*.65f,7.5f+(i%4)*1.5f,3.8f),i%4==0?"RockShade":"Rock",800+i);
                var cap=Primitive("Grass lip "+i,PrimitiveType.Cube,parent,new Vector3(p.x,p.y+.12f,p.z+1),new Vector3(width, .42f,5.8f),"GrassCap");
                cap.transform.localRotation=Quaternion.Euler(0,0,Mathf.Sin(i*.47f)*-2.5f);
            }
            for(var i=0;i<11;i++)
            {
                var x=-102+i*20f;var y=front[Mathf.Clamp(Mathf.RoundToInt((x+112)/8f),0,segments)].y;
                Rock("Cliff shoulder outcrop "+i,parent,new Vector3(x,y-5,61),new Vector3(8,9,6),"RockShade",700+i);
            }
        }

        static void AddFace(List<Vector3> vertices,List<int> triangles,Vector3 a,Vector3 b,Vector3 c)
        {
            var i=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(i);triangles.Add(i+1);triangles.Add(i+2);
        }

        static void BuildWaterfall(Transform cliffs,Transform water,float x,float height,int seed)
        {
            const int rows=8;var width=seed==1?5.2f:3.6f;var verts=new Vector3[(rows+1)*2];var uv=new Vector2[verts.Length];var tri=new int[rows*6];
            var rng=new System.Random(900+seed);
            for(var i=0;i<=rows;i++)
            {
                var t=i/(float)rows;var center=x+Mathf.Sin(t*5.8f+seed)*.52f;
                var half=width*(.88f+.12f*Mathf.Sin(t*13+seed));var y=Mathf.Lerp(height,-.9f,t);
                var z=56.3f+Mathf.Sin(t*8+seed)*.3f;
                verts[i*2]=new Vector3(center-half,y,z);verts[i*2+1]=new Vector3(center+half,y,z);
                uv[i*2]=new Vector2(0,t);uv[i*2+1]=new Vector2(1,t);
                if(i<rows){var p=i*2;var q=i*6;tri[q]=p;tri[q+1]=p+1;tri[q+2]=p+2;tri[q+3]=p+1;tri[q+4]=p+3;tri[q+5]=p+2;}
            }
            var mesh=new Mesh{vertices=verts,uv=uv,triangles=tri};mesh.RecalculateNormals();MeshObject("Sculpted waterfall "+seed,cliffs,mesh,"Waterfall");
            Ellipse("Pool foam ring "+seed,water,new Vector3(x,-1.13f,53.6f),width*1.55f,3.3f,"Waterfall");
            for(var i=0;i<7;i++)
            {
                var rx=((float)rng.NextDouble()-.5f)*width*2.6f;var rz=((float)rng.NextDouble()-.5f)*5.2f;
                Rock("Waterfall plunge boulder "+seed+"_"+i,water,new Vector3(x+rx,-1.35f,52+rz),new Vector3(.7f+(float)rng.NextDouble()*1.5f,.5f,1.2f),i%3==0?"RockShade":"Rock",1000+seed*20+i);
            }
            Rock("Waterfall crest outcrop "+seed,cliffs,new Vector3(x-4,height-.7f,61),new Vector3(5.8f,1.8f,7),"GrassCap",1100+seed);
        }

        static void FlowerPatch(Transform parent,Vector3 origin,int seed,string material)
        {
            var rng=new System.Random(1200+seed);var verts=new List<Vector3>();var tri=new List<int>();const int blooms=18;
            for(var i=0;i<blooms;i++)
            {
                var x=origin.x+((float)rng.NextDouble()-.5f)*5.5f;var z=origin.z+((float)rng.NextDouble()-.5f)*3.6f;
                var y=origin.y+(float)rng.NextDouble()*.35f;var radius=.12f+(float)rng.NextDouble()*.13f;
                var center=new Vector3(x,y+.32f+(float)rng.NextDouble()*.35f,z);
                for(var petal=0;petal<5;petal++)
                {
                    var a=petal*Mathf.PI*2/5;var tip=center+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius);
                    var mid=center+new Vector3(Mathf.Cos(a+.43f)*radius*.56f,.015f,Mathf.Sin(a+.43f)*radius*.56f);
                    AddFace(verts,tri,center,tip,mid);
                }
            }
            var mesh=new Mesh{vertices=verts.ToArray(),triangles=tri.ToArray()};mesh.RecalculateNormals();MeshObject("Flower cluster "+seed,parent,mesh,material);
        }

        static void BuildForegroundGrass(Transform parent)
        {
            var rng=new System.Random(1500);var verts=new List<Vector3>();var tri=new List<int>();
            for(var i=0;i<420;i++)
            {
                var side=i%2==0?-1:1;var x=side*(19+(float)rng.NextDouble()*8);var z=-27+(float)rng.NextDouble()*66;
                if(Mathf.Abs(z)<18&&Mathf.Abs(x)<22)continue;
                var y=Height(x,z)+.08f;var h=.28f+(float)rng.NextDouble()*.62f;var w=.035f+(float)rng.NextDouble()*.035f;
                var a=new Vector3(x-w,y,z);var b=new Vector3(x+w,y,z);var c=new Vector3(x+(float)rng.NextDouble()*.35f,y+h,z+(float)rng.NextDouble()*.35f);
                AddFace(verts,tri,a,b,c);
            }
            var mesh=new Mesh{vertices=verts.ToArray(),triangles=tri.ToArray()};mesh.RecalculateNormals();MeshObject("Foreground meadow blades",parent,mesh,"GrassCap");
        }

        static void Peak(Transform parent,Vector3 center,float radius,float height,int seed,string material)
        {
            const int sides=7;var rng=new System.Random(seed);var vertices=new List<Vector3>();var triangles=new List<int>();
            var baseRing=new Vector3[sides];var upperRing=new Vector3[sides];var peak=center+new Vector3(((float)rng.NextDouble()-.5f)*radius*.3f,height,((float)rng.NextDouble()-.5f)*radius*.22f);
            for(var i=0;i<sides;i++)
            {
                var a=i*Mathf.PI*2/sides;var r=radius*(.84f+(float)rng.NextDouble()*.28f);
                baseRing[i]=center+new Vector3(Mathf.Cos(a)*r,-center.y,Mathf.Sin(a)*r);
                upperRing[i]=center+new Vector3(Mathf.Cos(a)*r*.38f,height*.62f,Mathf.Sin(a)*r*.38f);
            }
            for(var i=0;i<sides;i++)
            {
                var n=(i+1)%sides;
                AddFace(vertices,triangles,baseRing[i],upperRing[i],baseRing[n]);
                AddFace(vertices,triangles,baseRing[n],upperRing[i],upperRing[n]);
                AddFace(vertices,triangles,upperRing[i],peak,upperRing[n]);
            }
            var mesh=new Mesh{vertices=vertices.ToArray(),triangles=triangles.ToArray()};mesh.RecalculateNormals();MeshObject("Distant mountain peak "+seed,parent,mesh,material);
        }

        static void BuildDistantBridge(Transform parent)
        {
            const float start=41,span=19,baseY=25,deckY=39;const int arches=5;
            Primitive("Distant aqueduct deck",PrimitiveType.Cube,parent,new Vector3(start+span*arches*.5f,deckY,131),new Vector3(span*arches+8,2.2f,5),"Rock");
            Primitive("Distant bridge parapet",PrimitiveType.Cube,parent,new Vector3(start+span*arches*.5f,deckY+2,131),new Vector3(span*arches+8,.75f,5.8f),"RockShade");
            for(var i=0;i<=arches;i++)
            {
                var x=start+i*span;
                Primitive("Aqueduct pier "+i,PrimitiveType.Cube,parent,new Vector3(x,(baseY+deckY)*.5f,131),new Vector3(2.6f,deckY-baseY,5.2f),"RockShade");
            }
            for(var i=0;i<arches;i++)
            {
                var x=start+span*(i+.5f);const int pieces=9;
                for(var j=0;j<pieces;j++)
                {
                    var a=Mathf.PI*j/(pieces-1);var block=Primitive("Bridge arch stone "+i+"_"+j,PrimitiveType.Cube,parent,
                        new Vector3(x+Mathf.Cos(a)*span*.5f,baseY+Mathf.Sin(a)*6.7f,131),new Vector3(span*.21f,1.8f,5.3f),j%3==0?"Rock":"RockShade");
                    block.transform.localRotation=Quaternion.Euler(0,0,Mathf.Cos(a)*-20);
                }
            }
        }

        static void Ellipse(string name,Transform parent,Vector3 pos,float rx,float rz,string mat)
        {
            const int count=48;var verts=new Vector3[count+1];var uv=new Vector2[count+1];var tri=new int[count*3];
            verts[0]=pos;uv[0]=Vector2.one*.5f;
            for(var i=0;i<count;i++){var a=i/(float)count*Mathf.PI*2;verts[i+1]=pos+new Vector3(Mathf.Cos(a)*rx,0,Mathf.Sin(a)*rz);uv[i+1]=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*.5f+Vector2.one*.5f;
                tri[i*3]=0;tri[i*3+1]=(i+1)%count+1;tri[i*3+2]=i+1;}
            var mesh=new Mesh{vertices=verts,uv=uv,triangles=tri};mesh.RecalculateNormals();MeshObject(name,parent,mesh,mat);
        }

        static void Rock(string name,Transform parent,Vector3 pos,Vector3 size,string mat,int seed)
        {
            var rng=new System.Random(seed);var verts=new List<Vector3>();var triangles=new List<int>();
            var ring=new Vector3[3,9];
            for(var h=0;h<3;h++)for(var i=0;i<8;i++)
            {
                var a=i/8f*Mathf.PI*2;var r=(.42f+(float)rng.NextDouble()*.1f)*(h==2?.83f:1);
                ring[h,i]=pos+new Vector3(Mathf.Cos(a)*size.x*r,h/2f*size.y,Mathf.Sin(a)*size.z*r);
            }
            for(var h=0;h<3;h++)ring[h,8]=ring[h,0];
            Action<Vector3,Vector3,Vector3> add=(a,b,c)=>{var n=verts.Count;verts.AddRange(new[]{a,b,c});triangles.AddRange(new[]{n,n+1,n+2});};
            for(var h=0;h<2;h++)for(var i=0;i<8;i++)
            {add(ring[h,i],ring[h+1,i],ring[h,i+1]);add(ring[h,i+1],ring[h+1,i],ring[h+1,i+1]);}
            for(var i=0;i<8;i++)add(pos+Vector3.up*size.y,ring[2,i+1],ring[2,i]);
            var mesh=new Mesh{vertices=verts.ToArray(),triangles=triangles.ToArray()};mesh.RecalculateNormals();MeshObject(name,parent,mesh,mat);
        }

        static void Tree(Transform parent,Vector3 pos,float scale,int seed)
        {
            var t=Group("Tree volume "+seed,parent);t.localPosition=pos;t.localScale=Vector3.one*scale;
            Primitive("Trunk proxy",PrimitiveType.Cylinder,t,new Vector3(0,4,0),new Vector3(1.7f,4,1.7f),"Trunk");
            for(var j=0;j<7;j++)
            {var a=j/7f*Mathf.PI*2;Primitive("Canopy clump proxy",PrimitiveType.Sphere,t,
                new Vector3(Mathf.Cos(a)*3.9f,9+j%3*1.5f,Mathf.Sin(a)*3.9f),new Vector3(8,6.5f,7),j%3==0?"LeafLight":"Leaves");}
        }

        static void Arch(Transform parent,Vector3 pos,float yaw,float scale)
        {
            var t=Group("Broken arch silhouette",parent);t.localPosition=pos;t.localRotation=Quaternion.Euler(0,yaw,0);t.localScale=Vector3.one*scale;
            Primitive("Left pier",PrimitiveType.Cube,t,new Vector3(-3,2,0),new Vector3(1.7f,4,2.2f),"Rock");
            Primitive("Right pier",PrimitiveType.Cube,t,new Vector3(3,2,0),new Vector3(1.7f,4,2.2f),"Rock");
            for(var i=0;i<7;i++)
            {var a=i/6f*Mathf.PI;var g=Primitive("Arch voussoir proxy",PrimitiveType.Cube,t,new Vector3(Mathf.Cos(a)*3,4+Mathf.Sin(a)*3,0),new Vector3(1.5f,1.7f,2.2f),i%3==0?"RockShade":"Rock");g.transform.localRotation=Quaternion.Euler(0,0,a*Mathf.Rad2Deg-90);}
        }

        static void Tower(Transform parent,Vector3 pos)
        {
            var t=Group("Distant tower silhouette",parent);t.localPosition=pos;
            Primitive("Tower body",PrimitiveType.Cube,t,new Vector3(0,11,0),new Vector3(6,22,6),"RockShade");
            Primitive("Tower upper ledge",PrimitiveType.Cube,t,new Vector3(0,21,0),new Vector3(8,1,8),"Rock");
            for(var i=0;i<4;i++)Primitive("Tower spire",PrimitiveType.Cube,t,new Vector3(i%2==0?-3:3,25,i<2?-3:3),new Vector3(1,9,1),"RockShade");
        }

        static void BuildActorsAndMarkers(Transform environment)
        {
            var root=Group("Review only - copied anchors and scale proxies",null);root.SetPositionAndRotation(layout.origin,layout.rotation);
            foreach(var a in layout.anchors)
            {
                var anchor=Group(a.name,root);anchor.localPosition=a.localPosition;
                var actor=Group("Proxy_"+a.name,anchor);var enemy=a.name.StartsWith("Enemy");
                actor.localRotation=Quaternion.Euler(0,enemy?180:0,0);
                Primitive("Body scale proxy",PrimitiveType.Capsule,actor,new Vector3(0,1.25f,0),new Vector3(.85f,1.15f,.65f),enemy?"Enemy":"Player");
                Primitive("Head scale proxy",PrimitiveType.Sphere,actor,new Vector3(0,2.65f,0),new Vector3(.65f,.7f,.65f),"Skin");
                Primitive("Facing indicator",PrimitiveType.Cube,actor,new Vector3(0,1.7f,.52f),new Vector3(.5f,.13f,.9f),"WhiteFlower");
            }
        }

        static void WriteMasks()
        {
            const int size=256;var texture=new Texture2D(size,size,TextureFormat.RGB24,false,true);
            var pixels=new Color[size*size];
            for(var y=0;y<size;y++)for(var x=0;x<size;x++)
            {
                var lx=(x/(float)(size-1)-.5f)*100;var lz=(y/(float)(size-1)-.5f)*100;
                var radius=Mathf.Sqrt(lx*lx/(23*23)+lz*lz/(19*19));
                var exclusion=Mathf.Abs(lx)<18&&Mathf.Abs(lz)<17?1f:0f;
                var density=Mathf.Clamp01(1-Mathf.Abs(radius-1)*3)*(1-exclusion);
                pixels[y*size+x]=new Color(density,exclusion,Mathf.Clamp01(1-Mathf.Abs(radius-1)*6));
            }
            texture.SetPixels(pixels);texture.Apply();var path=Art+"/TG_MeadowPlacementMask.png";
            File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.sRGBTexture=false;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            File.WriteAllText(Art+"/TG_MeadowPlacementMask.txt","Layout-local X/Z bounds [-50,50], PNG U follows +X, V follows +Z. Red: proposed border density. Green: combat exclusion. Blue: proposed flower mass. Apply layout origin/rotation; ground height is from TG_Layout.json. Starting mask: verify every registered camera before foliage production.\n");
        }

        [Serializable] sealed class CameraReview { public string name;public int visibleActors;public int occludedActors;public string[] obstructions; }
        [Serializable] sealed class ReviewReport { public string context;public int renderers;public long triangles;public int nativeTerrains;public int[] terrainHeightmapResolutions;public string gpu;public CameraReview[] cameras; }

        static void WriteReview(Scene scene,string output)
        {
            var transforms=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
            var actors=transforms.Where(t=>t.name.StartsWith("Proxy_")).ToArray();
            var colliders=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Collider>()).ToArray();
            var reviews=new List<CameraReview>();
            foreach(var cam in transforms.Select(t=>t.GetComponent<Camera>()).Where(c=>c!=null))
            {
                var problems=new List<string>();var visible=0;var occluded=0;
                foreach(var actor in actors)
                {
                    var p=actor.position+Vector3.up*1.6f;var v=cam.WorldToViewportPoint(p);
                    if(v.z>0&&v.x>=0&&v.x<=1&&v.y>=0&&v.y<=1)visible++;
                    var delta=p-cam.transform.position;var ray=new Ray(cam.transform.position,delta.normalized);
                    if(colliders.Any(c=>c.Raycast(ray,out var hit,delta.magnitude-.1f))){occluded++;problems.Add(actor.name+": collision geometry intersects line to torso");}
                }
                reviews.Add(new CameraReview{name=cam.name,visibleActors=visible,occludedActors=occluded,obstructions=problems.ToArray()});
            }
            var filters=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MeshFilter>()).Where(f=>f.sharedMesh!=null).ToArray();
            var terrains=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Terrain>()).Where(t=>t.terrainData!=null).ToArray();
            var report=new ReviewReport{context="Editor offscreen static proxy review. This summary checks existing scene colliders; use CameraEnvelopeReview for all visual modules and native terrain height samples. Triangle count covers MeshFilters only, excluding dynamically tessellated TerrainData. Not gameplay or player-build performance acceptance.",
                nativeTerrains=terrains.Length,terrainHeightmapResolutions=terrains.Select(t=>t.terrainData.heightmapResolution).ToArray(),
                renderers=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<Renderer>().Length),triangles=filters.Sum(f=>(long)f.sharedMesh.triangles.Length/3),gpu=SystemInfo.graphicsDeviceName,cameras=reviews.ToArray()};
            File.WriteAllText(output+"/CameraReview.json",JsonUtility.ToJson(report,true));
        }
    }
}
