using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatFoundationBuilder
    {
        // Starting authoring dimensions in layout-local metres. Camera evidence decides subsequent tuning.
        const float TerrainWest = -192, TerrainSouth = -128, TerrainSpan = 384, TerrainBase = -10;
        const float RiverLevel = -1.35f;
        static readonly float[] FallX = { -43f, -13f, 43f };
        static TerrainData referenceTerrainData;
        static float Smooth(float a, float b, float x) { return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a,b,x)); }
        static float RiverCenter(float x) { return 35 + 2.7f*Mathf.Sin(x*.042f) + 1.8f*Mathf.Cos(x*.071f); }
        static float RiverWidth(float x) { return 14.5f + 2.2f*Mathf.Sin(x*.057f+1); }
        static float CliffFront(float x, int tier)
        {
            return tier==0 ? RiverCenter(x)+RiverWidth(x)+7
                -7*Mathf.Exp(-Mathf.Pow((x+67)/25,2))+8*Mathf.Exp(-Mathf.Pow((x+8)/25,2))
                -4*Mathf.Exp(-Mathf.Pow((x-65)/20,2))+1.1f*Mathf.Sin(x*.084f)
                : 82-.20f*(x+20)+8*Mathf.Exp(-Mathf.Pow((x+55)/36,2))+2*Mathf.Sin(x*.061f);
        }
        static float ShelfHeight(float x) { return 17+8*Mathf.Exp(-Mathf.Pow((x+53)/48,2))-4*Smooth(25,120,x)+1.4f*Mathf.Sin(x*.052f); }
        static float ReferenceHeight(float x,float z)
        {
            var n=(Mathf.PerlinNoise(x*.023f+17,z*.027f+23)-.45f)*4;
            var low=n*Smooth(20,38,Mathf.Max(Mathf.Abs(x),Mathf.Abs(z)));
            low+=Smooth(90,143,Mathf.Abs(x))*6+Smooth(35,92,-z)*(8+3*Mathf.Sin(x*.032f));
            var center=RiverCenter(x);var width=z<center?RiverWidth(x):CliffFront(x,0)-1.8f-center;var bankDistance=Mathf.Abs(z-center);
            var basin=1-Smooth(width-1,width+4,bankDistance);
            low=Mathf.Lerp(low,-4.6f,basin);
            var front=CliffFront(x,0);var first=Smooth(front-1.3f,front+1.0f,z);
            var h=Mathf.Lerp(low,ShelfHeight(x)+n*.5f,first);
            var upper=Smooth(CliffFront(x,1)-1,CliffFront(x,1)+1.5f,z);
            h+=upper*(11+4*Mathf.Exp(-Mathf.Pow((x+60)/53,2)))*(1-Smooth(8,106,x));
            h+=Smooth(front+7,front+30,z)*(Mathf.PerlinNoise(x*.038f+13,z*.032f+5)-.4f)*5;
            h+=Smooth(122,182,z)*(2+3*Mathf.Sin(x*.027f+1));
            // Shallow watercourses physically reach each waterfall crest from the plateau.
            for(var i=0;i<FallX.Length;i++)
            {
                var dx=Mathf.Abs(x-FallX[i]);var half=i==1?4.4f:3.0f;
                var channel=(1-Smooth(half,half+2,dx))*Smooth(front-1,front+4,z)*(1-Smooth(front+17,front+27,z));
                h-=channel*1.4f;
            }
            // The current six fighter anchor elevations remain exact, including heightmap interpolation neighbours.
            if(Mathf.Abs(x)<=21&&Mathf.Abs(z)<=19)return 0;
            var protect=Smooth(21,27,Mathf.Abs(x))+Smooth(19,24,Mathf.Abs(z));
            if(z<24)h*=Mathf.Clamp01(protect);
            return h;
        }

        static void BuildLandscape(Transform root)
        {
            Make("CliffLight",new Color(.64f,.68f,.69f));
            Make("CliffWarm",new Color(.60f,.63f,.60f));
            Make("DistantBlue",new Color(.40f,.62f,.77f));
            Make("GrassCap",new Color(.50f,.68f,.235f));
            BuildReferenceTerrain(root);
            Mats["GrassCap"].color=Color.white;Mats["GrassCap"].mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Terrain/TG_Surface_0.asset");
            EditorUtility.SetDirty(Mats["GrassCap"]);
            var cliffs=Group("02 Integrated escarpments and grassy shelves",root);
            BuildReferenceEscarpment(cliffs,0,-184,184,105);
            BuildReferenceEscarpment(cliffs,1,-179,77,64);
            var water=Group("03 Winding basin and plateau-fed waterfalls - preview materials",root);
            ConfigureWaterPreview();BuildReferenceRiver(water);
            for(var i=0;i<FallX.Length;i++)BuildReferenceFall(water,FallX[i],i);
            BuildReferenceMountains(root);
            BuildReferenceDressing(root);
        }

        static void BuildReferenceTerrain(Transform root)
        {
            Directory.CreateDirectory(Art+"/Terrain");AssetDatabase.Refresh();
            var dataPath=Art+"/Terrain/TG_ReferenceLand.asset";
            var data=AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
            if(data==null){data=new TerrainData();AssetDatabase.CreateAsset(data,dataPath);}
            data.heightmapResolution=513;data.size=new Vector3(TerrainSpan,70,TerrainSpan);
            var heights=new float[513,513];
            for(var z=0;z<513;z++)for(var x=0;x<513;x++)
                heights[z,x]=(ReferenceHeight(TerrainWest+x/512f*TerrainSpan,TerrainSouth+z/512f*TerrainSpan)-TerrainBase)/70;
            data.SetHeights(0,0,heights);
            referenceTerrainData=data;
            const string vendor="Assets/Fantasy Skybox FREE/Scenes/Textures (Terrain)/";
            var layerNames=new[]{"TerrainGrass","TerrainDirt","TerrainRock"};var layers=new TerrainLayer[3];
            for(var i=0;i<3;i++)
            {
                var source=AssetDatabase.LoadAssetAtPath<TerrainLayer>(vendor+layerNames[i]+".terrainlayer");
                if(source==null)throw new InvalidOperationException("Installed terrain layer missing: "+layerNames[i]);
                var path=Art+"/Terrain/TG_"+layerNames[i]+".terrainlayer";
                layers[i]=AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if(layers[i]==null){layers[i]=UnityEngine.Object.Instantiate(source);AssetDatabase.CreateAsset(layers[i],path);}
                else EditorUtility.CopySerialized(source,layers[i]);
                layers[i].name="TG_"+layerNames[i];layers[i].tileSize=new Vector2(i==2?12:9,i==2?12:9);
                // Low contrast authoring textures establish coherent grass/soil values before WP04 surface art.
                layers[i].diffuseTexture=ReferenceGroundTexture(i);layers[i].normalMapTexture=null;layers[i].smoothness=0;
                EditorUtility.SetDirty(layers[i]);
            }
            data.terrainLayers=layers;data.alphamapResolution=256;var alpha=new float[256,256,3];
            for(var iz=0;iz<256;iz++)for(var ix=0;ix<256;ix++)
            {
                var x=TerrainWest+ix/255f*TerrainSpan;var z=TerrainSouth+iz/255f*TerrainSpan;
                var gradient=new Vector2(ReferenceHeight(x+1,z)-ReferenceHeight(x-1,z),ReferenceHeight(x,z+1)-ReferenceHeight(x,z-1)).magnitude*.5f;
                var stone=Smooth(.6f,2.1f,gradient)*.95f;
                var clearing=1-Smooth(.76f,1.13f,Mathf.Sqrt(x*x/(23*23)+z*z/(19*19)));
                var noise=Mathf.PerlinNoise(x*.14f+9,z*.14f+12);
                var pathX=4*Mathf.Sin(z*.064f)+2;
                var path=(1-Smooth(1.8f,4.6f,Mathf.Abs(x-pathX)))*Smooth(-26,-12,z)*(1-Smooth(16,24,z));
                var soil=Mathf.Max(path*.62f,clearing*Smooth(.40f,.69f,noise)*.58f);
                var bank=(1-Smooth(1,5,Mathf.Abs(Mathf.Abs(z-RiverCenter(x))-RiverWidth(x))))*.35f;
                soil=Mathf.Max(soil,bank)*(1-stone);
                alpha[iz,ix,0]=1-stone-soil;alpha[iz,ix,1]=soil;alpha[iz,ix,2]=stone;
            }
            data.SetAlphamaps(0,0,alpha);data.baseMapResolution=1024;EditorUtility.SetDirty(data);
            var go=Terrain.CreateTerrainGameObject(data);go.name="TG Editable TerrainData - connected meadow basin and terraces";
            go.transform.SetParent(root,false);go.transform.localPosition=new Vector3(TerrainWest,TerrainBase,TerrainSouth);
            var terrain=go.GetComponent<Terrain>();terrain.heightmapPixelError=3;terrain.basemapDistance=400;
            terrain.drawInstanced=true;terrain.Flush();
        }

        static Texture2D ReferenceGroundTexture(int layer)
        {
            var path=Art+"/Terrain/TG_Surface_"+layer+".asset";var tex=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if(tex==null){tex=new Texture2D(128,128,TextureFormat.RGB24,true);AssetDatabase.CreateAsset(tex,path);}
            var colors=new Color[128*128];
            var basis=layer==0?new Color(.50f,.68f,.235f):layer==1?new Color(.70f,.63f,.38f):new Color(.58f,.63f,.65f);
            for(var y=0;y<128;y++)for(var x=0;x<128;x++)
            {
                var n=Mathf.PerlinNoise(x*.0625f+layer*13,y*.0625f+5)*.10f+Mathf.PerlinNoise(x*.31f,y*.31f)*.035f-.065f;
                colors[y*128+x]=basis+new Color(n,n,n*.65f,0);
            }
            tex.SetPixels(colors);tex.wrapMode=TextureWrapMode.Repeat;tex.Apply(true,false);EditorUtility.SetDirty(tex);return tex;
        }

        static void BuildReferenceEscarpment(Transform parent,int tier,float left,float right,int count)
        {
            const int bands=4;var points=new Vector3[count+1,bands+1];
            for(var i=0;i<=count;i++)
            {
                var x=Mathf.Lerp(left,right,i/(float)count);var f=CliffFront(x,tier);
                var top=ReferenceHeight(x,f+5)+.14f;
                var bottom=tier==0?-3.3f:ReferenceHeight(x,f-4)-1.1f;
                for(var j=0;j<=bands;j++)
                {
                    var t=j/(float)bands;
                    var shift=j==0||j==bands?0:Mathf.Sin(i*2.41f+j*4.1f)*.25f;
                    points[i,j]=new Vector3(x+shift,Mathf.Lerp(bottom,top,t)+(j==0||j==bands?0:Mathf.Sin(i*.8f+j)*1.7f),
                        f-1.25f-Mathf.Sin(t*Mathf.PI)*(.35f+.3f*Mathf.Sin(i*.71f))+Mathf.Sin(i*1.31f)*.85f+Mathf.Sin(j*2+i)*.12f);
                }
            }
            var vertices=new List<Vector3>();var sets=new[]{new List<int>(),new List<int>(),new List<int>()};var rng=new System.Random(524+tier);
            Action<Vector3,Vector3,Vector3> facet=(a,b,c)=>
            {
                var k=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);
                var pick=rng.Next(30);var slot=pick<1?1:pick<3?2:0;sets[slot].AddRange(new[]{k,k+1,k+2});
            };
            for(var i=0;i<count;i++)for(var j=0;j<bands;j++)
            {
                if(tier==0&&FallNotch((points[i,bands].x+points[i+1,bands].x)*.5f,2.1f))continue;
                if((i+j)%2==0){facet(points[i,j],points[i,j+1],points[i+1,j]);facet(points[i+1,j],points[i,j+1],points[i+1,j+1]);}
                else {facet(points[i,j],points[i,j+1],points[i+1,j+1]);facet(points[i,j],points[i+1,j+1],points[i+1,j]);}
            }
            // Return faces are closed into the terrain, so inspection from either side has no paper-thin edges.
            for(var end=0;end<2;end++)
            {
                var i=end==0?0:count;
                for(var j=0;j<bands;j++)
                {
                    var a=points[i,j];var b=points[i,j+1];var c=b+Vector3.forward*8;var d=a+Vector3.forward*8;
                    if(end==0){facet(a,d,b);facet(d,c,b);}else{facet(a,b,d);facet(d,b,c);}
                }
            }
            var mesh=new Mesh{vertices=vertices.ToArray(),subMeshCount=3};for(var s=0;s<3;s++)mesh.SetTriangles(sets[s],s);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var go=MeshObject("Tier "+(tier+1)+" continuous broken stone faces",parent,mesh,"Rock",true);
            go.GetComponent<MeshRenderer>().sharedMaterials=new[]{Mats["Rock"],Mats["RockShade"],Mats["CliffLight"]};
            var caps=new List<Vector3>();var ct=new List<int>();
            for(var i=0;i<count;i++)
            {
                var a=points[i,bands];var b=points[i+1,bands];
                if(tier==0&&FallNotch((a.x+b.x)*.5f,2.1f))continue;
                var c=new Vector3(b.x,SampleReferenceTerrain(b.x,b.z+4.5f)-.12f,b.z+4.5f);
                var d=new Vector3(a.x,SampleReferenceTerrain(a.x,a.z+4.5f)-.12f,a.z+4.5f);
                AddFace(caps,ct,a,d,b);AddFace(caps,ct,b,d,c);
            }
            var capUV=new Vector2[caps.Count];for(var i=0;i<caps.Count;i++)capUV[i]=new Vector2((caps[i].x-TerrainWest)/9,(caps[i].z-TerrainSouth)/9);
            var cap=new Mesh{vertices=caps.ToArray(),uv=capUV,triangles=ct.ToArray()};cap.RecalculateNormals();
            MeshObject("Tier "+(tier+1)+" irregular grassy shelf transition",parent,cap,"GrassCap");
        }

        static float SampleReferenceTerrain(float x,float z)
        {
            return TerrainBase+referenceTerrainData.GetInterpolatedHeight((x-TerrainWest)/TerrainSpan,(z-TerrainSouth)/TerrainSpan);
        }

        static bool FallNotch(float x,float allowance)
        {
            for(var i=0;i<FallX.Length;i++)if(Mathf.Abs(x-FallX[i])<(i==1?4.3f:2.7f)+allowance)return true;
            return false;
        }

        static void ConfigureWaterPreview()
        {
            var shader=Shader.Find("Teyvat/Foundation Water Preview");
            if(shader==null)throw new InvalidOperationException("Foundation water preview shader not imported.");
            foreach(var key in new[]{"Water","Waterfall"})
            {
                var m=Mats[key];m.shader=shader;m.SetColor("_Color",key=="Water"?new Color(.055f,.49f,.64f):new Color(.32f,.74f,.86f));
                m.SetColor("_Shallow",new Color(.21f,.76f,.76f));m.SetColor("_Foam",new Color(.83f,.96f,.95f));m.SetFloat("_Mode",key=="Water"?0:1);
                EditorUtility.SetDirty(m);
            }
        }

        static void BuildReferenceRiver(Transform parent)
        {
            const int columns=193,rows=13;var v=new Vector3[columns*rows];var uv=new Vector2[v.Length];var tri=new List<int>();
            for(var i=0;i<columns;i++)for(var j=0;j<rows;j++)
            {
                var x=TerrainWest+i/(float)(columns-1)*TerrainSpan;var t=j/(float)(rows-1);var k=i*rows+j;
                var z=Mathf.Lerp(RiverCenter(x)-RiverWidth(x)-.2f,CliffFront(x,0)-1.8f,t);
                v[k]=new Vector3(x,RiverLevel,z);uv[k]=new Vector2(t,x*.055f);
                if(i<columns-1&&j<rows-1)tri.AddRange(new[]{k,k+1,k+rows,k+rows,k+1,k+rows+1});
            }
            var mesh=new Mesh{vertices=v,uv=uv,triangles=tri.ToArray()};mesh.RecalculateNormals();
            MeshObject("Irregular river basin - continuous bank to bank",parent,mesh,"Water");
            for(var i=0;i<23;i++)
            {
                var x=-113+i*10.1f;var side=i%2==0?-1:1;var z=RiverCenter(x)+side*(RiverWidth(x)-.8f);
                Rock("River shoreline outcrop "+i,parent,new Vector3(x,-1.7f,z),new Vector3(2.5f+i%3,1.8f+i%4*.42f,2.5f),i%4==0?"RockShade":"Rock",4100+i);
            }
        }

        static void BuildReferenceFall(Transform parent,float x,int seed)
        {
            var front=CliffFront(x,0);var top=ReferenceHeight(x,front+5)+.3f;var half=seed==1?4.3f:2.7f;
            const int rows=24,columns=9;var v=new Vector3[(rows+1)*columns];var uv=new Vector2[v.Length];var tri=new List<int>();
            for(var r=0;r<=rows;r++)for(var c=0;c<columns;c++)
            {
                var t=r/(float)rows;var s=c/(float)(columns-1);var k=r*columns+c;
                var y=top-(top-RiverLevel+.18f)*t;
                var px=x+(s*2-1)*half*(1+t*.15f)+Mathf.Sin(t*5+seed)*.35f;
                var z=CliffFront(px,0)-3.4f-2*Mathf.Sin(t*Mathf.PI*.5f)+.16f*Mathf.Sin(s*15+t*7);
                v[k]=new Vector3(px,y,z);uv[k]=new Vector2(s,t);
                if(r<rows&&c<columns-1)
                {
                    var a=k;var b=k+1;var d=k+columns;var e=d+1;
                    tri.AddRange(new[]{a,b,d,b,e,d}); // Front winding; preview shader uses Cull Off for reverse inspection.
                }
            }
            var mesh=new Mesh{vertices=v,uv=uv,triangles=tri.ToArray()};mesh.RecalculateNormals();
            MeshObject("Plateau-fed waterfall curtain "+seed,parent,mesh,"Waterfall");
            // A crest apron bridges the sculpted plateau channel and descending curtain.
            var p=new Vector3[22];var cuv=new Vector2[22];var ci=new List<int>();
            for(var row=0;row<=10;row++)for(var side=0;side<2;side++)
            {
                var t=row/10f;var px=x+Mathf.Sin(t*2.6f+seed)*t*.7f+(side*2-1)*Mathf.Lerp(half,half*.55f,Mathf.Sqrt(t));
                var z=Mathf.Lerp(CliffFront(px,0)-3.4f,front+22,t);var py=t<.34f?top:ReferenceHeight(px,z)+.18f;
                p[row*2+side]=new Vector3(px,py,z);cuv[row*2+side]=new Vector2(side,t*2);
                if(row==0)p[side]=v[side==0?0:columns-1];
                if(row<10&&side==0){var k=row*2;ci.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});}
            }
            var crest=new Mesh{vertices=p,uv=cuv,triangles=ci.ToArray()};crest.RecalculateNormals();
            MeshObject("Watercourse over physical crest "+seed,parent,crest,"Water");
            // Bury the channel termination beneath a source outcrop instead of leaving a rectangular water edge.
            Rock("Water source overhanging outcrop "+seed,parent,new Vector3(x,ReferenceHeight(x,front+21)-.35f,front+22),
                new Vector3(half*1.9f,2.1f,5.2f),"Rock",4650+seed);
            Ellipse("Plunge whitewater "+seed,parent,new Vector3(x,RiverLevel+.035f,front-7),half*1.8f,3.6f,"Waterfall");
            for(var i=0;i<7;i++)
            {
                var a=i*.93f;var rx=x+Mathf.Cos(a)*(half+1.3f);var z=front-6+Mathf.Sin(a)*3;
                Rock("Plunge island "+seed+"_"+i,parent,new Vector3(rx,-1.6f,z),new Vector3(1.4f+i%3,1+i%2,1.7f),i%3==0?"RockShade":"Rock",4400+seed*20+i);
            }
        }

        static void BuildReferenceMountains(Transform root)
        {
            var group=Group("06 Layered distant ridges - complete volumes",root);
            for(var layer=0;layer<3;layer++)
            {
                var v=new List<Vector3>();var t=new List<int>();
                for(var i=0;i<27;i++)
                {
                    var x=-250+i*20f;var z=188+layer*37+7*Mathf.Sin(i*.57f);
                    var h=31+layer*9+12*Mathf.Sin(i*.63f+layer)+7*Mathf.Sin(i*1.79f+layer*.6f);
                    v.Add(new Vector3(x,ReferenceHeight(Mathf.Clamp(x,-143,143),180)-5,z-20));v.Add(new Vector3(x,h,z));v.Add(new Vector3(x,12,z+38));
                    if(i<26){var k=i*3;t.AddRange(new[]{k,k+1,k+3,k+3,k+1,k+4,k+1,k+2,k+4,k+4,k+2,k+5});}
                }
                var mesh=new Mesh{vertices=v.ToArray(),triangles=t.ToArray()};mesh.RecalculateNormals();
                MeshObject("Blue atmospheric ridge layer "+layer,group,mesh,layer==0?"FarHill":"DistantBlue");
            }
        }
    }
}
