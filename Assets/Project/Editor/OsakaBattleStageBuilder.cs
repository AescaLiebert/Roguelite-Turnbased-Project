using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FightingAllstar.EditorTools
{
    /// <summary>Authored modular 3D Osaka street. Generated objects, materials and meshes are saved assets.</summary>
    public static partial class OsakaBattleStageBuilder
    {
        private const string Art = "Assets/Project/Art/Environment/OsakaNight";
        private const string ScenePath = "Assets/Project/Scenes/Battle.unity";
        private const string RootName = "Osaka Night • Walking Mall";
        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        private static readonly Vector3 Center = new Vector3(1.96f, 0, 0);
        private static int _meshSerial;

        [MenuItem("Fighting Allstar/Stage/Rebuild Osaka Night Battle")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build the stage in Edit Mode.");
            if (SceneManager.GetActiveScene().path != ScenePath)
                throw new InvalidOperationException("Open Battle.unity before rebuilding its stage.");
            EnsureFolders();
            Materials.Clear(); Meshes.Clear(); _meshSerial = 0;
            ImportTextures();
            var existing = GameObject.Find(RootName);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
            var oldFloor = GameObject.Find("Land");
            if (oldFloor != null) oldFloor.SetActive(false);
            var player = GameObject.Find("CharHeroPosition2").transform.position;
            var enemy = GameObject.Find("EnemyHeroPosition2").transform.position;
            var center = (player + enemy) * .5f;
            var root = Group(RootName, null, center - Center);
            Palette();
            BuildGround(root);
            BuildArchitecture(root);
            BuildStreetProps(root);
            BuildSkyline(root);
            BuildLighting(root);
            BuildSteam(root);
            ConfigureCamera(center);
            BakeReflection(root);
            BatchStageMeshes(root);
            PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, Art + "/Prefabs/OsakaNightStage.prefab", InteractionMode.AutomatedAction);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            MapOptimizationLab.ApplyStandardToOpenMap();
            Selection.activeGameObject = root.gameObject;
            var sv = SceneView.lastActiveSceneView;
            if (sv != null) sv.LookAt(center + Vector3.up * 3,
                Quaternion.Euler(19, -18, 0), 36, false, true);
            Debug.Log("OSAKA STAGE COMPLETE: " + root.GetComponentsInChildren<Renderer>().Count(r => r.enabled) +
                " renderers. Existing combat anchors preserved. Floor elevation " + center.y);
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(Art + "/Materials");
            Directory.CreateDirectory(Art + "/Meshes");
            Directory.CreateDirectory(Art + "/Prefabs");
        }

        private static void ImportTextures()
        {
            foreach (var path in Directory.GetFiles(Art + "/Textures", "*.png"))
            {
                var assetPath = path.Replace('\\', '/');
                AssetDatabase.ImportAsset(assetPath);
                var imp = (TextureImporter)AssetImporter.GetAtPath(assetPath);
                imp.textureType = path.Contains("Normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
                imp.wrapMode = path.Contains("Normal") || path.Contains("Asphalt") || path.Contains("Pavers") || path.Contains("Concrete")
                    ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                imp.maxTextureSize = 2048;
                imp.anisoLevel = 8;
                imp.mipmapEnabled = true;
                imp.SaveAndReimport();
            }
        }

        private static void Palette()
        {
            Make("Asphalt", new Color(.80f,.90f,.90f), .80f, .24f, "Asphalt", "AsphaltNormal", new Vector2(12,16));
            Make("Paver", new Color(.70f,.74f,.72f), .36f, .06f, "Pavers", "PaversNormal", new Vector2(3,5));
            Make("Concrete", new Color(.34f,.38f,.39f), .22f, 0, "Concrete", null, new Vector2(2,2));
            Make("Red", new Color(.48f,.045f,.035f), .45f, .12f);
            Make("Cream", new Color(.60f,.57f,.44f), .25f, 0);
            Make("Metal", new Color(.12f,.16f,.17f), .62f, .75f);
            Make("Dark", new Color(.028f,.041f,.045f), .36f, .08f);
            Make("Wood", new Color(.27f,.135f,.07f), .25f, 0);
            Make("Green", new Color(.10f,.19f,.10f), .13f, 0);
            Make("Skin", new Color(.53f,.36f,.23f), .14f, 0);
            Make("ClothBlue", new Color(.08f,.14f,.22f), .12f, 0);
            Make("ClothOchre", new Color(.42f,.29f,.09f), .12f, 0);
            Make("ClothBurgundy", new Color(.28f,.055f,.065f), .12f, 0);
            Make("WarmGlow", new Color(1,.70f,.32f), .35f, 0, emission: new Color(2.5f,1.7f,.70f));
            Make("CyanGlow", new Color(.16f,.75f,.85f), .35f, 0, emission: new Color(.25f,1.45f,1.8f));
            Make("PinkGlow", new Color(.80f,.15f,.36f), .35f, 0, emission: new Color(1.7f,.20f,.48f));
            Make("AmberGlow", new Color(.95f,.45f,.06f), .35f, 0, emission: new Color(2.1f,.75f,.08f));
            var glass = Make("Glass", new Color(.13f,.27f,.27f,.17f), .92f, .14f);
            glass.SetFloat("_Mode",3); glass.SetInt("_SrcBlend",(int)BlendMode.One);
            glass.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha); glass.SetInt("_ZWrite",0);
            glass.DisableKeyword("_ALPHATEST_ON"); glass.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            glass.renderQueue=3000; EditorUtility.SetDirty(glass);
        }

        private static Material Make(string key, Color color, float smooth, float metal,
            string texture = null, string normal = null, Vector2? tiling = null, Color? emission = null)
        {
            var path = Art + "/Materials/" + key + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Standard")); AssetDatabase.CreateAsset(mat,path); }
            mat.shader=Shader.Find(emission.HasValue ? "OsakaNight/Neon" : "Standard"); mat.color=color;
            mat.SetFloat("_Glossiness",smooth); mat.SetFloat("_Metallic",metal);
            if (texture != null) mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/"+texture+".png");
            if (tiling.HasValue) mat.mainTextureScale=tiling.Value;
            if (normal != null)
            {
                mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Art+"/Textures/"+normal+".png"));
                mat.SetFloat("_BumpScale", .32f); mat.EnableKeyword("_NORMALMAP");
            }
            if (emission.HasValue)
            { mat.SetFloat("_Glow",1.65f); }
            mat.enableInstancing=true; Materials[key]=mat; EditorUtility.SetDirty(mat); return mat;
        }

        private static Material M(string key) { return Materials[key]; }

        private static Transform Group(string name, Transform parent, Vector3 position = default(Vector3))
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false);
            go.transform.localPosition=position; return go.transform;
        }

        private static GameObject Primitive(string name, Transform parent, Vector3 p, Vector3 scale, Material mat, PrimitiveType type)
        {
            var go=GameObject.CreatePrimitive(type); go.name=name; go.transform.SetParent(parent,false);
            go.transform.localPosition=p; go.transform.localScale=scale;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            var r=go.GetComponent<Renderer>(); r.sharedMaterial=mat;
            r.shadowCastingMode=mat.shader.name=="OsakaNight/Neon" ? ShadowCastingMode.Off : ShadowCastingMode.On;
            r.receiveShadows=true; go.isStatic=true; return go;
        }
        private static GameObject Box(string n,Transform t,Vector3 p,Vector3 s,Material m) { return Primitive(n,t,p,s,m,PrimitiveType.Cube); }
        private static GameObject Cylinder(string n,Transform t,Vector3 p,Vector3 s,Material m) { return Primitive(n,t,p,s,m,PrimitiveType.Cylinder); }
        private static GameObject Sphere(string n,Transform t,Vector3 p,Vector3 s,Material m) { return Primitive(n,t,p,s,m,PrimitiveType.Sphere); }
        private static GameObject Tube(string n,Transform t,Vector3 a,Vector3 b,float radius,Material m)
        {
            var d=b-a; var go=Cylinder(n,t,(a+b)*.5f,new Vector3(radius*2,d.magnitude*.5f,radius*2),m);
            go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,d.normalized); return go;
        }

        // Quad normal faces -Z, giving correct readable lettering from the street.
        private static GameObject Sign(string n,Transform t,Vector3 p,Vector2 size,string textureName,float yaw=0)
        {
            var key="Sign_"+textureName;
            Material mat;
            if (!Materials.TryGetValue(key,out mat))
            {
                mat=Make(key,Color.white,.3f,0,textureName,null,null,new Color(1.05f,1.05f,1.05f));
                mat.SetFloat("_Glow",1.5f);
            }
            var go=Primitive(n,t,p,new Vector3(size.x,size.y,1),mat,PrimitiveType.Quad);
            go.transform.localRotation=Quaternion.Euler(0,yaw,0);
            return go;
        }

        private static void BuildGround(Transform root)
        {
            var g=Group("01 • Wet walking mall / clear combat floor",root);
            Box("Continuous street slab (combat elevation)",g,Center+new Vector3(0,-.12f,0),new Vector3(70,.24f,76),M("Asphalt"));
            foreach (var z in new[] {-12.3f,12.3f})
            {
                Box("Raised paver promenade",g,new Vector3(1.96f,.12f,z>0 ? 14.55f : -15.05f),new Vector3(38,.24f,z>0 ? 9.6f : 10.6f),M("Paver"));
                Box("Stone curb",g,new Vector3(1.96f,.115f,z>0 ? 9.66f : -9.66f),new Vector3(38,.23f,.22f),M("Cream"));
                for(var x=-16f;x<22;x+=2f)
                {
                    Box("Curb joint",g,new Vector3(x,.232f,z>0 ? 9.66f : -9.66f),new Vector3(.028f,.004f,.22f),M("Dark"));
                    Box("Drain surround",g,new Vector3(x,.008f,z>0 ? 9.3f : -9.3f),new Vector3(.75f,.016f,.26f),M("Metal"));
                    for(var j=0;j<7;j++) Box("Drain slit",g,new Vector3(x-.28f+j*.09f,.02f,z>0 ? 9.3f : -9.3f),new Vector3(.034f,.006f,.2f),M("Dark"));
                }
            }
            foreach(var x in new[] {-12.4f,16.32f})
            {
                Box("Side promenade",g,new Vector3(x,.12f,0),new Vector3(4.8f,.24f,19.2f),M("Paver"));
                Box("Side curb",g,new Vector3(x<0 ? -9.88f : 13.8f,.12f,0),new Vector3(.22f,.24f,19.2f),M("Cream"));
            }
            for(var z=-8;z<=8;z+=4)
            {
                Box("Subtle pavement expansion seam",g,new Vector3(1.96f,.003f,z),new Vector3(23.5f,.004f,.018f),M("Dark"));
                for(var x=-8;x<14;x+=4) Box("Stone joint",g,new Vector3(x,.003f,z+2),new Vector3(.018f,.004f,4),M("Dark"));
            }
            // Street marks are deliberately confined to outer ends; nothing obscures attack cues.
            foreach(var z in new[] {-8.1f,8.1f})
                for(var x=-6;x<11;x+=2)
                    Box("Faded pedestrian stripe",g,new Vector3(x,.007f,z),new Vector3(1,.009f,.34f),M("Cream"));
            var cover=Cylinder("Cast iron service cover",g,new Vector3(9.8f,.007f,6.6f),new Vector3(1.2f,.009f,1.2f),M("Metal"));
            for(var i=0;i<6;i++) Box("Cover rib",g,new Vector3(9.45f+i*.14f,.02f,6.6f),new Vector3(.035f,.012f,.78f),M("Dark"));
        }

        private static void BuildArchitecture(Transform root)
        {
            var g=Group("02 • Shopfront architecture",root);
            BuildArcade(g);
            Shop(g,"Ramen • warm corner kitchen",new Vector3(-13.3f,.24f,19.5f),7.8f,10.4f,0,"Ramen",M("Cream"),M("Red"));
            Shop(g,"Karaoke • electric blue tower",new Vector3(17.9f,.24f,19.5f),7.8f,15.2f,0,"Karaoke",M("Concrete"),M("CyanGlow"));
            Shop(g,"South • covered market",new Vector3(1.96f,.24f,-20.5f),18,8.4f,180,"Market",M("Concrete"),M("Green"));
            Shop(g,"Southwest coffee house",new Vector3(-13.8f,.24f,-20.5f),9,10,180,"Coffee",M("Cream"),M("Wood"));
            Shop(g,"Southeast retro games",new Vector3(18.6f,.24f,-20.5f),9,11.6f,180,"ArcadeHero",M("Red"),M("Dark"));
            Shop(g,"West • intimate cafe",new Vector3(-15.7f,.24f,-.2f),12,8.8f,-90,"Coffee",M("Cream"),M("Wood"));
            Shop(g,"East • noodle bar",new Vector3(19.62f,.24f,.4f),12,9.8f,90,"Ramen",M("Concrete"),M("Red"));
            // Separate side passage beyond the hero facade gives depth in shoulder-height shots.
            for(var i=0;i<4;i++)
            {
                Shop(g,"East alley shop "+i,new Vector3(29,.24f,21+i*8),7,8+i*1.2f,90,i%2==0?"Market":"Coffee",M("Concrete"),M("Dark"));
                Shop(g,"West alley shop "+i,new Vector3(-25,.24f,21+i*8),7,9+i,-90,i%2==0?"Ramen":"Karaoke",M("Cream"),M("Red"));
            }
        }

        private static void BuildArcade(Transform parent)
        {
            var a=Group("NEO GEO LAND • hero arcade",parent,new Vector3(1.96f,.24f,19.5f));
            Box("Rear masonry",a,new Vector3(0,5.7f,6),new Vector3(19.8f,11.4f,.5f),M("Concrete"));
            Box("Floor • interior terrazzo",a,new Vector3(0,.02f,3),new Vector3(20,.08f,6),M("Cream"));
            Box("Roof cornice",a,new Vector3(0,11.35f,3),new Vector3(21,.42f,6.8f),M("Metal"));
            Box("Sign fascia",a,new Vector3(0,8.65f,.23f),new Vector3(20.3f,4.6f,.55f),M("Red"));
            Box("Black recessed sign mount",a,new Vector3(0,8.7f,-.09f),new Vector3(17.4f,3.9f,.28f),M("Dark"));
            Sign("Hero illuminated sign",a,new Vector3(0,8.7f,-.252f),new Vector2(16.8f,3.6f),"ArcadeHero");
            foreach(var y in new[] {6.81f,10.59f})
                Tube("Yellow neon border tube",a,new Vector3(-8.6f,y,-.33f),new Vector3(8.6f,y,-.33f),.022f,M("AmberGlow"));
            foreach(var x in new[] {-8.6f,8.6f})
                Tube("Vertical neon border tube",a,new Vector3(x,6.81f,-.33f),new Vector3(x,10.59f,-.33f),.022f,M("AmberGlow"));
            foreach(var x in new[] {-9.9f,-5.8f,0f,5.8f,9.9f})
            {
                Cylinder("Red monumental entrance column",a,new Vector3(x,3.35f,.02f),new Vector3(.68f,3.35f,.68f),M("Red"));
                Cylinder("Column brass foot",a,new Vector3(x,.14f,.02f),new Vector3(.82f,.14f,.82f),M("Metal"));
                Box("Column uplight lens",a,new Vector3(x,.1f,-.46f),new Vector3(.38f,.07f,.24f),M("AmberGlow"));
            }
            for(var x=-9f;x<=9f;x+=1.5f)
            {
                Box("Window mullion",a,new Vector3(x,3.25f,.12f),new Vector3(.07f,6.5f,.1f),M("Metal"));
                Box("Tinted glass pane",a,new Vector3(x+.72f,3.15f,.16f),new Vector3(1.35f,6.1f,.028f),M("Glass"));
            }
            foreach(var y in new[] {.38f,3.1f,6.35f}) Box("Horizontal glazing rail",a,new Vector3(0,y,.09f),new Vector3(19.7f,.1f,.13f),M("Metal"));
            Box("Entrance transom",a,new Vector3(0,2.95f,-.08f),new Vector3(2.7f,.1f,.14f),M("Metal"));
            foreach(var x in new[] {-.62f,.62f}) Tube("Brushed entry door handle",a,new Vector3(x,.9f,-.14f),new Vector3(x,1.62f,-.14f),.04f,M("Cream"));
            Box("Warm ceiling soffit",a,new Vector3(0,6.8f,3),new Vector3(20,.2f,6),M("Cream"));
            for(var x=-8f;x<=8f;x+=4)
            {
                Box("Interior fluorescent strip",a,new Vector3(x,6.68f,3),new Vector3(2.7f,.055f,.2f),M("WarmGlow"));
                Box("Second floor slab",a,new Vector3(x,4.5f,4.8f),new Vector3(3.8f,.18f,2.7f),M("Cream"));
                Tube("Gallery front guard rail",a,new Vector3(x-1.8f,5.5f,3.5f),new Vector3(x+1.8f,5.5f,3.5f),.035f,M("Metal"));
            }
            for(var i=0;i<8;i++) Cabinet(a,new Vector3(-8+i*2.25f,0,4.95f),i);
            for(var i=0;i<5;i++)
            {
                var c=Group("Foreground arcade row "+i,a,new Vector3(-7.8f+i*3.8f,0,2.05f));
                c.localRotation=Quaternion.Euler(0,12,0); Cabinet(c,Vector3.zero,i+8);
            }
            // An actual staircase, visible through the left display window.
            for(var i=0;i<11;i++) Box("Stair tread "+i,a,new Vector3(-8.6f,.18f+i*.35f,1.7f+i*.30f),new Vector3(1.2f,.36f,.38f),M("Cream"));
            Tube("Stair handrail",a,new Vector3(-9.3f,1.1f,1.6f),new Vector3(-9.3f,4.7f,4.9f),.04f,M("Metal"));
            foreach(var x in new[] {-10.7f,10.7f})
            {
                Box("Projecting blade sign case",a,new Vector3(x,5.8f,-.45f),new Vector3(1.6f,4.7f,.32f),M("Red"));
                Sign("Vertical game sign",a,new Vector3(x,5.8f,-.63f),new Vector2(1.4f,4.45f),"VerticalArcade");
            }
            Point("Arcade amber interior",a,new Vector3(-5,3.5f,-1.4f),new Color(1,.66f,.29f),4.5f,14);
            Point("Arcade amber interior east",a,new Vector3(5,3.5f,-1.4f),new Color(1,.70f,.37f),4.5f,14);
            Point("Cyan arcade threshold",a,new Vector3(0,2,-.6f),new Color(.20f,.75f,.82f),1.2f,8);
            Point("Soft arcade interior ceiling",a,new Vector3(0,5,3.6f),new Color(1,.78f,.45f),2.8f,11);
        }

        private static void Cabinet(Transform parent,Vector3 position,int i)
        {
            var c=Group("3D arcade cabinet "+i,parent,position);
            var profile=new[] {new Vector2(-.55f,0),new Vector2(.45f,0),new Vector2(.45f,2.05f),new Vector2(.20f,2.15f),new Vector2(-.35f,2.15f),new Vector2(-.55f,1.8f),new Vector2(-.32f,1.05f),new Vector2(-.68f,.92f)};
            Extrude("Cabinet sculpted shell",c,profile,.82f,M(i%3==0?"Red":"Cream"));
            var screen=Box("Recessed monitor",c,new Vector3(0,1.47f,-.36f),new Vector3(.67f,.59f,.08f),M("Dark"));
            screen.transform.localRotation=Quaternion.Euler(-16,0,0);
            var pixels=Box("CRT glow",c,new Vector3(0,1.48f,-.412f),new Vector3(.52f,.4f,.012f),M(i%3==0?"PinkGlow":"CyanGlow"));
            pixels.transform.localRotation=screen.transform.localRotation;
            for(var k=0;k<3;k++) Box("CRT abstract game pixel",c,new Vector3(-.12f+k*.1f,1.4f+k*.07f,-.44f),new Vector3(.075f,.08f,.015f),M("WarmGlow"));
            Box("Illuminated cabinet marquee",c,new Vector3(0,1.98f,-.43f),new Vector3(.67f,.18f,.09f),M("AmberGlow"));
            Box("Control deck",c,new Vector3(0,1.02f,-.49f),new Vector3(.76f,.1f,.36f),M("Dark"));
            Tube("Joystick stem",c,new Vector3(-.20f,1.07f,-.50f),new Vector3(-.20f,1.19f,-.50f),.022f,M("Metal"));
            Sphere("Joystick ball",c,new Vector3(-.20f,1.20f,-.50f),Vector3.one*.1f,M("Red"));
            for(var j=0;j<3;j++) Cylinder("Action button",c,new Vector3(.03f+j*.10f,1.08f,-.54f),new Vector3(.065f,.018f,.065f),M(j==0?"Red":"CyanGlow"));
            Box("Coin plate",c,new Vector3(0,.55f,-.61f),new Vector3(.22f,.22f,.03f),M("Metal"));
        }

        private static void Shop(Transform parent,string name,Vector3 p,float width,float height,float yaw,string sign,Material wall,Material accent)
        {
            var g=Group(name,parent,p);g.localRotation=Quaternion.Euler(0,yaw,0);
            Box("Rear wall",g,new Vector3(0,height*.5f,4.5f),new Vector3(width,height,.5f),wall);
            foreach(var x in new[] {-width*.5f,width*.5f}) Box("Party wall",g,new Vector3(x,height*.5f,2.2f),new Vector3(.35f,height,4.6f),wall);
            Box("Store interior floor",g,new Vector3(0,.035f,2.25f),new Vector3(width,.07f,4.5f),M("Paver"));
            Box("Upper facade",g,new Vector3(0,(height+4.7f)*.5f,.12f),new Vector3(width,height-4.7f,.45f),wall);
            Box("Cornice edge",g,new Vector3(0,height,1.9f),new Vector3(width+.3f,.22f,4.7f),M("Metal"));
            Box("Store sign surround",g,new Vector3(0,4.05f,-.14f),new Vector3(width-.35f,1.55f,.26f),accent);
            var vertical=sign=="Karaoke";
            Sign("Illuminated shop identity",g,new Vector3(0,4.1f,-.285f),new Vector2(width-.65f,1.32f),vertical?"Market":sign);
            for(var x=-width*.5f+.45f;x<width*.5f;x+=1.35f)
            {
                Box("Storefront frame",g,new Vector3(x,1.8f,.08f),new Vector3(.055f,3.6f,.09f),M("Metal"));
                Box("Storefront glass",g,new Vector3(x+.62f,1.8f,.12f),new Vector3(1.18f,3.3f,.022f),M("Glass"));
            }
            Box("Interior counter",g,new Vector3(0,.62f,2.7f),new Vector3(width*.72f,1.24f,.8f),M("Wood"));
            Box("Counter polished top",g,new Vector3(0,1.29f,2.7f),new Vector3(width*.76f,.1f,1),M("Dark"));
            for(var x=-width*.30f;x<=width*.3f;x+=1.5f)
            {
                Cylinder("Bar stool stem",g,new Vector3(x,.36f,1.45f),new Vector3(.085f,.36f,.085f),M("Metal"));
                Cylinder("Bar stool cushion",g,new Vector3(x,.76f,1.45f),new Vector3(.48f,.07f,.48f),M("Red"));
                Tube("Pendant cable",g,new Vector3(x,3.35f,2.5f),new Vector3(x,2.65f,2.5f),.012f,M("Metal"));
                Sphere("Warm pendant shade",g,new Vector3(x,2.65f,2.5f),new Vector3(.34f,.18f,.34f),M("WarmGlow"));
            }
            for(var y=6.1f;y<height-.5f;y+=2.3f)
                for(var x=-width*.5f+1.1f;x<width*.5f-.5f;x+=2.2f)
                {
                    Box("Upper window inset",g,new Vector3(x,y,-.14f),new Vector3(1.4f,1.5f,.15f),M("Metal"));
                    Box("Apartment glass",g,new Vector3(x,y,-.23f),new Vector3(1.2f,1.3f,.035f),((int)(x*3+y)%3==0)?M("WarmGlow"):M("Dark"));
                    Box("Window cross bar",g,new Vector3(x,y,-.26f),new Vector3(.05f,1.35f,.035f),M("Metal"));
                    Box("Window sill",g,new Vector3(x,y-.75f,-.29f),new Vector3(1.6f,.12f,.35f),wall);
                }
            for(var x=-width*.45f;x<=width*.45f;x+=1.2f)
                Box("Striped fabric awning",g,new Vector3(x,3.25f,-.50f),new Vector3(1.2f,.10f,1.15f),((int)Mathf.Round((x+width*.45f)/1.2f)%2==0)?accent:M("Cream")).transform.localRotation=Quaternion.Euler(-10,0,0);
            Tube("Drainpipe",g,new Vector3(width*.48f,.1f,-.18f),new Vector3(width*.48f,height,-.18f),.055f,M("Metal"));
            Box("Roof AC compressor",g,new Vector3(-width*.26f,height+.35f,2.5f),new Vector3(1.3f,.7f,.9f),M("Concrete"));
            for(var j=0;j<5;j++) Box("Compressor louver",g,new Vector3(-width*.26f,height+.12f+j*.1f,2.03f),new Vector3(1.1f,.035f,.05f),M("Metal"));
            if(vertical)
            {
                Box("Tall karaoke sign case",g,new Vector3(width*.36f,9,-.7f),new Vector3(2.7f,6.7f,.4f),M("Dark"));
                Sign("Tall blue karaoke sign",g,new Vector3(width*.36f,9,-.92f),new Vector2(2.5f,6.4f),"Karaoke");
            }
            Point("Shop warm spill",g,new Vector3(0,2.6f,.15f),new Color(1,.65f,.33f),1.8f,8);
        }

        private static void Extrude(string name,Transform parent,Vector2[] profile,float width,Material mat)
        {
            // Convex side profile in Z/Y; duplicated triangle vertices yield deliberate flat facets.
            var verts=new List<Vector3>();var tris=new List<int>();
            Action<Vector3,Vector3,Vector3> tri=(a,b,c)=>{var n=verts.Count;verts.Add(a);verts.Add(b);verts.Add(c);tris.Add(n);tris.Add(n+1);tris.Add(n+2);};
            Func<int,float,Vector3> v=(i,x)=>new Vector3(x,profile[i].y,profile[i].x);
            for(var i=1;i<profile.Length-1;i++)
            { tri(v(0,-width*.5f),v(i+1,-width*.5f),v(i,-width*.5f));tri(v(0,width*.5f),v(i,width*.5f),v(i+1,width*.5f)); }
            for(var i=0;i<profile.Length;i++)
            {var j=(i+1)%profile.Length;tri(v(i,-width*.5f),v(j,-width*.5f),v(j,width*.5f));tri(v(i,-width*.5f),v(j,width*.5f),v(i,width*.5f));}
            var mesh=new Mesh {name="Osaka sculpted profile"};mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            var path=Art+"/Meshes/Profile_"+(_meshSerial++)+".asset";
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old!=null){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial=mat;go.isStatic=true;
        }

        private static void BuildSkyline(Transform root)
        {
            var g=Group("04 • Layered Osaka skyline / alley depth",root);
            var random=new System.Random(1994);
            for(var i=0;i<38;i++)
            {
                var angle=i*Mathf.PI*2/38;var radius=41+(float)random.NextDouble()*17;
                var p=Center+new Vector3(Mathf.Cos(angle)*radius,0,Mathf.Sin(angle)*radius);
                var h=12+(float)random.NextDouble()*24;var w=4+(float)random.NextDouble()*7;
                var b=Group("Distant city block "+i,g,p);b.localRotation=Quaternion.Euler(0,-angle*Mathf.Rad2Deg+90,0);
                Box("Tower mass",b,new Vector3(0,h*.5f,0),new Vector3(w,h,6),M("Dark"));
                Box("Roof plant",b,new Vector3(0,h+.5f,.4f),new Vector3(w*.65f,1,3),M("Concrete"));
                for(var y=2f;y<h-1;y+=2.5f)
                    for(var x=-w*.5f+.7f;x<w*.5f-.3f;x+=1.5f)
                        if(random.NextDouble()>.4) Box("Distant occupied window",b,new Vector3(x,y,-3.015f),new Vector3(.6f,1,.025f),random.NextDouble()>.25?M("WarmGlow"):M("CyanGlow"));
                if(i%5==0)
                {
                    Tube("Rooftop antenna",b,new Vector3(0,h,0),new Vector3(0,h+4,0),.055f,M("Metal"));
                    Sphere("Aircraft marker",b,new Vector3(0,h+4,0),Vector3.one*.12f,M("PinkGlow"));
                }
            }
            // Long service cables frame the upper alley view, never cross the playable court.
            foreach(var x in new[] {-23f,26.9f})
                for(var z=12;z<50;z+=12)
                {
                    Tube("Service pole",g,new Vector3(x,0,z),new Vector3(x,11,z),.10f,M("Metal"));
                    for(var k=0;k<12;k++)
                    {
                        float t=k/12f,t2=(k+1)/12f;
                        Tube("Sagging power line",g,new Vector3(x,10.5f-Mathf.Sin(t*Mathf.PI)*.7f,z+t*12),new Vector3(x,10.5f-Mathf.Sin(t2*Mathf.PI)*.7f,z+t2*12),.018f,M("Dark"));
                    }
                }
        }

        private static Light Point(string name,Transform parent,Vector3 p,Color color,float intensity,float range)
        {
            var g=Group(name,parent,p);var l=g.gameObject.AddComponent<Light>();l.type=LightType.Point;l.color=color;
            l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;l.renderMode=LightRenderMode.Auto;return l;
        }

        private static void BuildLighting(Transform root)
        {
            var g=Group("05 • Night lighting",root);
            var sun=GameObject.Find("Directional Light");
            if(sun==null)sun=new GameObject("Directional Light",typeof(Light));
            var key=sun.GetComponent<Light>();key.type=LightType.Directional;key.color=new Color(.44f,.61f,.79f);
            key.intensity=.76f;key.shadows=LightShadows.Soft;key.shadowStrength=.65f;key.shadowBias=.05f;
            sun.transform.rotation=Quaternion.Euler(48,-32,0);
            var fill=Group("Broad amber combat fill",g,new Vector3(-7,12,-4));
            var l=fill.gameObject.AddComponent<Light>();l.type=LightType.Spot;l.color=new Color(1,.73f,.49f);
            l.intensity=2.8f;l.range=32;l.spotAngle=105;l.shadows=LightShadows.Soft;l.shadowStrength=.4f;l.renderMode=LightRenderMode.ForcePixel;
            fill.rotation=Quaternion.LookRotation(root.TransformPoint(Center)-fill.position);
            var rim=Group("Broad teal combat rim",g,new Vector3(10.5f,11,5));
            var r=rim.gameObject.AddComponent<Light>();r.type=LightType.Spot;r.color=new Color(.35f,.77f,.79f);
            r.intensity=2.4f;r.range=30;r.spotAngle=108;r.shadows=LightShadows.None;r.renderMode=LightRenderMode.ForcePixel;
            rim.rotation=Quaternion.LookRotation(root.TransformPoint(Center+Vector3.up)-rim.position);
            foreach(var z in new[] {-12f,12f})
                foreach(var x in new[] {-9f,12.9f})
                {
                    var p=Group("Pedestrian lamp",g,new Vector3(x,0,z));
                    Cylinder("Lamp pole",p,new Vector3(0,2.4f,0),new Vector3(.14f,2.4f,.14f),M("Metal"));
                    Cylinder("Pole base",p,new Vector3(0,.3f,0),new Vector3(.35f,.3f,.35f),M("Metal"));
                    Tube("Lamp arm",p,new Vector3(0,4.7f,0),new Vector3(.75f,4.7f,0),.055f,M("Metal"));
                    Box("Lamp housing",p,new Vector3(.75f,4.63f,0),new Vector3(.7f,.15f,.4f),M("Metal"));
                    Box("Warm lamp lens",p,new Vector3(.75f,4.54f,0),new Vector3(.6f,.025f,.32f),M("WarmGlow"));
                    Point("Warm pavement pool",p,new Vector3(.75f,4.4f,0),new Color(1,.71f,.39f),2.0f,8);
                }
            var sky=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/OsakaSky.mat");
            if(sky==null){sky=new Material(Shader.Find("OsakaNight/Sky"));AssetDatabase.CreateAsset(sky,Art+"/Materials/OsakaSky.mat");}
            RenderSettings.skybox=sky;RenderSettings.sun=key;RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.21f,.29f,.36f);RenderSettings.ambientEquatorColor=new Color(.13f,.21f,.23f);
            RenderSettings.ambientGroundColor=new Color(.11f,.12f,.16f);RenderSettings.ambientIntensity=1;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogColor=new Color(.054f,.048f,.105f);RenderSettings.fogDensity=.009f;
            RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.reflectionIntensity=.85f;
        }

        private static void ConfigureCamera(Vector3 center)
        {
            var cam=Camera.main;if(cam==null)throw new InvalidOperationException("Battle needs Main Camera.");
            cam.transform.position=center+new Vector3(0,13.5f,-17);
            cam.transform.rotation=Quaternion.LookRotation(center+Vector3.up*1.3f-cam.transform.position);
            cam.clearFlags=CameraClearFlags.Skybox;cam.backgroundColor=new Color(.025f,.018f,.065f);cam.allowHDR=true;cam.farClipPlane=140;
            var glow=cam.GetComponent<OsakaNightAtmosphere>();if(glow==null)glow=cam.gameObject.AddComponent<OsakaNightAtmosphere>();
            var so=new SerializedObject(glow);so.FindProperty("glowShader").objectReferenceValue=Shader.Find("Hidden/OsakaNight/Glow");so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void BakeReflection(Transform root)
        {
            var go=new GameObject("Temporary reflection capture");var cam=go.AddComponent<Camera>();cam.enabled=false;
            cam.transform.position=root.TransformPoint(Center+new Vector3(0,2.5f,0));cam.clearFlags=CameraClearFlags.Skybox;cam.farClipPlane=100;cam.allowHDR=true;
            var cube=new Cubemap(128,TextureFormat.RGBAHalf,true);cube.name="Osaka wet street reflections";
            if(!cam.RenderToCubemap(cube))throw new InvalidOperationException("Reflection capture failed.");
            cube.Apply(true,false);var path=Art+"/OsakaNightReflection.asset";var old=AssetDatabase.LoadAssetAtPath<Cubemap>(path);
            if(old!=null){EditorUtility.CopySerialized(cube,old);UnityEngine.Object.DestroyImmediate(cube);cube=old;}else AssetDatabase.CreateAsset(cube,path);
            UnityEngine.Object.DestroyImmediate(go);RenderSettings.customReflectionTexture=cube;
            var probe=Group("Street reflection probe • saved cubemap",root,Center+Vector3.up*3).gameObject.AddComponent<ReflectionProbe>();
            probe.mode=ReflectionProbeMode.Custom;probe.customBakedTexture=cube;probe.boxProjection=true;probe.size=new Vector3(45,20,45);probe.intensity=.85f;probe.blendDistance=6;
        }

        private static void BuildSteam(Transform root)
        {
            var texPath=Art+"/Textures/SoftSteam.png";
            if(!File.Exists(texPath))
            {
                var tex=new Texture2D(64,64,TextureFormat.RGBA32,false);
                for(var y=0;y<64;y++)for(var x=0;x<64;x++)
                {var d=Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f))/31.5f;tex.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-d),2)));}
                tex.Apply();File.WriteAllBytes(texPath,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(texPath);
            }
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/SoftSteam.mat");
            if(mat==null){mat=new Material(Shader.Find("Particles/Standard Unlit"));AssetDatabase.CreateAsset(mat,Art+"/Materials/SoftSteam.mat");}
            mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);mat.SetFloat("_Mode",2);mat.SetColor("_Color",new Color(.72f,.84f,.95f,.72f));
            mat.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);mat.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);mat.SetInt("_ZWrite",0);mat.renderQueue=3100;
            mat.SetOverrideTag("RenderType","Transparent");mat.EnableKeyword("_ALPHABLEND_ON");EditorUtility.SetDirty(mat);
            foreach(var p in new[] {new Vector3(-10,.26f,9.3f),new Vector3(14,.26f,-9.3f)})
            {
                var g=Group("Quiet service vent steam",root,p);var ps=g.gameObject.AddComponent<ParticleSystem>();ps.Stop();
                var main=ps.main;main.startLifetime=4.5f;main.startSpeed=.42f;main.startSize=new ParticleSystem.MinMaxCurve(.48f,.95f);
                main.startColor=new Color(.82f,.9f,1f,.62f);main.maxParticles=36;main.simulationSpace=ParticleSystemSimulationSpace.World;
                var em=ps.emission;em.rateOverTime=6;
                var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=12;shape.radius=.18f;
                // Aim both plumes toward the open arena instead of sending one into its wall.
                g.localRotation=Quaternion.Euler(p.z>0 ? -90 : 90,0,0);
                var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.35f),new Keyframe(1,1.8f)));
                var color=ps.colorOverLifetime;color.enabled=true;
                var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.78f,.22f),new GradientAlphaKey(.48f,.72f),new GradientAlphaKey(0,1)});color.color=gradient;
                ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=mat;ps.Play();
            }
        }

        public static void Capture(string path,Vector3 position,Vector3 lookAt,int width=1600,int height=900)
        {
            var cam=Camera.main;var oldPosition=cam.transform.position;var oldRotation=cam.transform.rotation;var target=cam.targetTexture;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGBHalf);Texture2D tex=null;
            try
            {
                cam.transform.position=position;cam.transform.LookAt(lookAt);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
                tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,tex.EncodeToPNG());
            }
            finally { cam.transform.SetPositionAndRotation(oldPosition,oldRotation);cam.targetTexture=target;RenderTexture.active=active;if(tex!=null)UnityEngine.Object.DestroyImmediate(tex);RenderTexture.ReleaseTemporary(rt); }
        }

        public static object ValidateStage()
        {
            var root=GameObject.Find(RootName);var center=(GameObject.Find("CharHeroPosition2").transform.position+GameObject.Find("EnemyHeroPosition2").transform.position)*.5f;
            var renderers=root.GetComponentsInChildren<Renderer>().Where(r=>r.enabled).ToArray();
            var filters=root.GetComponentsInChildren<MeshFilter>().Where(m=>m.GetComponent<Renderer>()!=null && m.GetComponent<Renderer>().enabled).ToArray();
            return new {center=center.ToString(),decorColliders=root.GetComponentsInChildren<Collider>().Length,
                missingMaterials=renderers.Count(r=>r.sharedMaterials.Any(m=>m==null || m.shader==null)),
                nonPersistentMeshes=filters.Count(m=>m.sharedMesh==null || !EditorUtility.IsPersistent(m.sharedMesh)),
                rendererCount=renderers.Length,meshTriangles=filters.Sum(m=>m.sharedMesh.triangles.Length/3),
                floorDelta=Mathf.Abs(center.y-root.transform.position.y),glow=Camera.main.GetComponent<OsakaNightAtmosphere>()!=null};
        }
    }
}
