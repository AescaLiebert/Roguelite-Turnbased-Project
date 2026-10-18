using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FightingAllstar.EditorTools
{
    /// <summary>WP00/WP01 only: repeatable, isolated camera/layout review. Never converts live project materials.</summary>
    public static partial class TeyvatFoundationBuilder
    {
        public const string Art = "Assets/Project/Art/Environment/TeyvatGrassland/FoundationR2";
        public const string ScenePath = "Assets/Project/Scenes/EnvironmentReview/TeyvatGrassland/TG_LayoutReview_r2.unity";
        public const string Evidence = "Artifacts/TeyvatGrassland";
        public const string ReferenceEvidence = Evidence + "/WP01/Revision02/BuiltInGamma";
        public const string LayoutPath = Art + "/TG_Layout.json";
        const string BattlePath = "Assets/Project/Scenes/Battle.unity";
        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
        static Scene buildScene;
        static LayoutData layout;
        static int meshSerial;

        [Serializable] public sealed class AnchorRecord { public string name; public Vector3 worldPosition; public Vector3 localPosition; }
        [Serializable] public sealed class ShotRecord { public string name; public Vector3 position; public Quaternion rotation; public float fov; public string basis; }
        [Serializable] public sealed class LayoutData
        {
            public string revision = "WP01-r2";
            public string status = "Review blockout; dimensions are starting values, not final art.";
            public Vector3 origin; public Quaternion rotation; public float groundY;
            public float clearingHalfWidth = 17; public float clearingHalfLength = 15;
            public float actorProxyHeight = 3; public float captureAspect = 16f / 9f;
            public AnchorRecord[] anchors; public ShotRecord[] shots;
            public string sourceScene; public string sourceSceneSha256;
            public string cameraSourceSha256; public float sourceCameraFov;
        }

        [MenuItem("Fighting Allstar/Teyvat Foundation/Build WP00-WP01 Review")]
        public static void Build()
        {
            BuildInternal(true);
        }

        [MenuItem("Fighting Allstar/Teyvat Foundation/Build WP01 Reference Revision 2")]
        public static void BuildReferenceRevision()
        {
            BuildInternal(false);
        }

        static void BuildInternal(bool captureHistoricalBaseline)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before building the review.");
            var originalActive = SceneManager.GetActiveScene();
            var source = EditorSceneManager.OpenPreviewScene(BattlePath);
            try
            {
                layout = ReadLayout(source);
                Directory.CreateDirectory(Art + "/Materials");
                Directory.CreateDirectory(Art + "/Meshes");
                Directory.CreateDirectory(Art + "/Compatibility");
                Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
                Directory.CreateDirectory(Evidence + "/WP00/Baseline");
                Directory.CreateDirectory(ReferenceEvidence);
                AssetDatabase.Refresh();
                if(captureHistoricalBaseline) { CaptureSourceBaseline(source); SaveCompatibilitySamples(); }
            }
            finally { EditorSceneManager.ClosePreviewScene(source); }

            buildScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(buildScene);
                meshSerial = 0; Mats.Clear(); Palette();
                var environment = Group("TG Foundation - replaceable layout modules", null);
                environment.SetPositionAndRotation(layout.origin, layout.rotation);
                BuildLandscape(environment);
                BuildActorsAndMarkers(environment);
                CreateCameras(); ConfigureLighting(false); ConfigureReferenceSky();
                WriteMasks();
                PrefabUtility.SaveAsPrefabAsset(environment.gameObject, Art + "/TG_FoundationLayout.prefab");
                File.WriteAllText(LayoutPath, JsonUtility.ToJson(layout, true));
                AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
                if (!EditorSceneManager.SaveScene(buildScene, ScenePath)) throw new IOException("Could not save layout scene.");
            }
            finally
            {
                if (originalActive.IsValid() && originalActive.isLoaded) SceneManager.SetActiveScene(originalActive);
                EditorSceneManager.CloseScene(buildScene, true);
            }
            CaptureSavedScene(ScenePath, ReferenceEvidence, false);
            if(captureHistoricalBaseline) WriteBaselineReport();
            Debug.Log("TEYVAT FOUNDATION READY: " + ScenePath + ". Source Battle scene and rendering settings preserved.");
        }

        static LayoutData ReadLayout(Scene source)
        {
            var transforms = source.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var player = transforms.First(t => t.name == "CharHeroPosition2");
            var enemy = transforms.First(t => t.name == "EnemyHeroPosition2");
            var forward = Vector3.ProjectOnPlane(enemy.position - player.position, Vector3.up).normalized;
            var result = new LayoutData { origin = (player.position + enemy.position) * .5f,
                rotation = Quaternion.LookRotation(forward), groundY = player.position.y, sourceScene = BattlePath,
                sourceSceneSha256 = Hash(BattlePath), cameraSourceSha256 = Hash("Assets/Project/Runtime/Presentation/Combat/BattleStagePresenter.Camera.cs") };
            var sourceCamera = transforms.Select(t => t.GetComponent<Camera>()).FirstOrDefault(c => c != null);
            result.sourceCameraFov = sourceCamera != null ? sourceCamera.fieldOfView : 60;
            result.anchors = transforms.Where(t => t.name.StartsWith("CharHeroPosition") || t.name.StartsWith("EnemyHeroPosition"))
                .OrderBy(t => t.name).Select(t => new AnchorRecord { name=t.name, worldPosition=t.position,
                    localPosition=Quaternion.Inverse(result.rotation)*(t.position-result.origin) }).ToArray();
            var shots = new List<ShotRecord>();
            AddShot(shots, result, "PlayerPlanning", new Vector3(0,13.5f,-17), new Vector3(0,1.3f,0), result.sourceCameraFov,
                "Exact current BattleStagePresenter.Initialize planning formula; source anchors copied.");
            AddShot(shots, result, "EnemyPlanning", new Vector3(0,13.5f,17), new Vector3(0,1.3f,0), result.sourceCameraFov,
                "Exact planning formula rotated 180 degrees as in PlanningShot.");
            AddShot(shots, result, "ReferencePlanningCandidate", new Vector3(0,9,-31), new Vector3(0,12,42), 58,
                "Starting value: optional landscape-oriented comparison, not applied to gameplay. Pass if six complete actors and cliff skyline remain visible; increase height/FOV if actors clip.");
            AddShot(shots, result, "ReversePlanningCandidate", new Vector3(0,10,24), new Vector3(0,3,-10), 52,
                "Starting value: reverse comparison of optional lower planning view; gameplay is unchanged.");
            AddShot(shots, result, "EnvironmentOverview", new Vector3(26,22,-42), new Vector3(0,16,42), 56,
                "Starting value: art review only; require all landmark masses in frame.");
            AddShot(shots, result, "PlayerAttack", new Vector3(1.35f,3.9f,-10.4f), new Vector3(0,2.1f,5), 52,
                "Proxy-height sample of rear/shoulder AttackShot: source in near row, target in far row. Not runtime playback.");
            AddShot(shots, result, "EnemyAttack", new Vector3(-1.35f,3.9f,10.4f), new Vector3(0,2.1f,-5), 52,
                "Reverse rear/shoulder sample; target visibility and reverse background check.");
            AddShot(shots, result, "PlayerContact", new Vector3(5.1f,3.9f,-3.4f), new Vector3(0,2.1f,5), 52,
                "Starting value: near-contact open shoulder-angle clearance; not calibrated animation framing.");
            AddShot(shots, result, "EnemyContact", new Vector3(-5.1f,3.9f,3.4f), new Vector3(0,2.1f,-5), 52,
                "Starting value: reverse near-contact clearance.");
            AddShot(shots, result, "PlayerPortrait", new Vector3(2.7f,3,1), new Vector3(0,2,-5), 48,
                "Starting value: close portrait clearance proxy. Actual actor-bound fitting remains runtime validation.");
            AddShot(shots, result, "EnemyPortrait", new Vector3(-2.7f,3,-1), new Vector3(0,2,5), 48,
                "Starting value: reverse portrait clearance proxy.");
            AddShot(shots, result, "WideMultiTarget", new Vector3(14,8,-15), new Vector3(0,1.8f,4), 56,
                "Starting value: broad multi-target/ultimate clearance envelope, not a claim of actual ultimate playback.");
            AddShot(shots, result, "ReverseOverview", new Vector3(-27,19,35), new Vector3(0,5,-25), 56,
                "Starting value: review full 3D reverse scenery.");
            AddShot(shots, result, "ReferenceFrontLeft", new Vector3(-108,85,-117), new Vector3(0,17,60), 55,
                "Starting value: multi-side landform review; inspect terrace connections, basin and left plateau.");
            AddShot(shots, result, "ReferenceFrontRight", new Vector3(115,78,-99), new Vector3(0,17,60), 55,
                "Starting value: inspect opposite cliff returns and grounded landmarks.");
            AddShot(shots, result, "ReferenceSide", new Vector3(144,85,41), new Vector3(0,19,65), 57,
                "Starting value: inspect depth, shelf setbacks and waterfall crests.");
            AddShot(shots, result, "ReferenceRear", new Vector3(83,122,203), new Vector3(0,17,65), 58,
                "Starting value: inspect continuous upper plateau, reverse slopes and landmark grounding.");
            result.shots = shots.ToArray(); return result;
        }

        static void AddShot(List<ShotRecord> shots, LayoutData data, string name, Vector3 pos, Vector3 focus, float fov, string basis)
        {
            var wp = data.origin + data.rotation * pos;
            shots.Add(new ShotRecord { name=name,position=wp,rotation=Quaternion.LookRotation(data.origin+data.rotation*focus-wp),fov=fov,basis=basis });
        }

        static void CaptureSourceBaseline(Scene source)
        {
            var cam=source.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).FirstOrDefault();
            if(cam==null) return;
            cam.scene=source;
            var originalPosition=cam.transform.position;var originalRotation=cam.transform.rotation;
            try
            {
                Capture(cam, Evidence+"/WP00/Baseline/BattleSavedCamera.png");
                var planning=layout.shots.First(s=>s.name=="PlayerPlanning");
                cam.transform.SetPositionAndRotation(planning.position,planning.rotation);
                Capture(cam,Evidence+"/WP00/Baseline/BattlePlanningCamera.png");
            }
            finally {cam.transform.SetPositionAndRotation(originalPosition,originalRotation);}
        }

        static void CreateCameras()
        {
            var parent=Group("Review cameras - proxies, see TG_Layout.json",null);
            foreach(var shot in layout.shots)
            {
                var go=Group("TG_"+shot.name,parent).gameObject;
                go.transform.SetPositionAndRotation(shot.position,shot.rotation);
                var c=go.AddComponent<Camera>();c.fieldOfView=shot.fov;c.nearClipPlane=.1f;c.farClipPlane=650;
                c.allowHDR=true;c.allowMSAA=true;c.enabled=shot.name=="PlayerPlanning";
                if(c.enabled) go.tag="MainCamera";
                var data=go.AddComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=false;
            }
        }

        static void ConfigureLighting(bool neutral)
        {
            var sun=new GameObject("TG Afternoon Sun").AddComponent<Light>();
            sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(neutral?55:39,neutral?-35:-48,0);
            sun.color=neutral?Color.white:new Color(1,.91f,.73f);sun.intensity=neutral?1:1.15f;
            sun.shadows=LightShadows.Soft;sun.shadowBias=.035f;RenderSettings.sun=sun;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.62f,.76f,.92f);
            RenderSettings.ambientEquatorColor=new Color(.39f,.48f,.53f);
            RenderSettings.ambientGroundColor=new Color(.28f,.34f,.22f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;
            RenderSettings.fogColor=new Color(.59f,.77f,.91f);RenderSettings.fogStartDistance=95;RenderSettings.fogEndDistance=460;
            var sky=AssetDatabase.LoadAssetAtPath<Material>(Art+"/Materials/TG_PreviewSky.mat");
            if(sky==null) {sky=new Material(Shader.Find("Skybox/Procedural"));AssetDatabase.CreateAsset(sky,Art+"/Materials/TG_PreviewSky.mat");}
            sky.SetColor("_SkyTint",new Color(.47f,.67f,.88f));sky.SetFloat("_AtmosphereThickness",.65f);sky.SetFloat("_Exposure",1.1f);
            RenderSettings.skybox=sky;
        }

        static void SaveCompatibilitySamples()
        {
            var samples=new[]{"Assets/Project/Art/Character/3D-Material/gi-lumine_anemo.mat","Assets/Project/Art/Character/3D-Material/gi-amber.mat",
                "Assets/Project/Art/Character/3D-Material/kyo94.mat","Assets/Project/Art/Environment/OsakaNight/OsakaNeon.shader",
                "Assets/Project/Art/Environment/OsakaNight/OsakaGlow.shader","Assets/Resources/ActorVisualEffects.shader",
                "Assets/Resources/ActorStatusParticles.shader","Assets/Resources/ActorCryo.shader","Assets/Resources/UltimateReadyAura.shader"};
            var records=new List<string>();
            foreach(var path in samples)
            {
                var original=AssetDatabase.LoadAssetAtPath<Material>(path);
                var shader=AssetDatabase.LoadAssetAtPath<Shader>(path);
                if(original==null&&shader==null) continue;
                var dest=Art+"/Compatibility/Source_"+Path.GetFileNameWithoutExtension(path)+".mat";
                var sample=AssetDatabase.LoadAssetAtPath<Material>(dest);
                if(sample==null) {sample=original!=null?new Material(original):new Material(shader);AssetDatabase.CreateAsset(sample,dest);}
                if(sample.HasProperty("_Color")&&original==null)sample.SetColor("_Color",new Color(.3f,.85f,1,1));
                records.Add(path+" | "+sample.shader.name+" | original copy; conversion unverified until trial");
            }
            File.WriteAllLines(Evidence+"/WP00/Baseline/MaterialInventory.txt",records);
        }

        public static void CaptureSavedScene(string scenePath,string output,bool urp)
        {
            Directory.CreateDirectory(output);
            // Preview scenes omit the saved RenderSettings; ordinary additive scenes share renderers
            // and lights. Capture the saved scene alone, and restore the clean editor scene setup.
            var setup=EditorSceneManager.GetSceneManagerSetup();
            for(var i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Capture requires saved scenes so the current editor setup can be restored without losing edits.");
            var preview=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Single);
            try
            {
                SceneManager.SetActiveScene(preview);
                var cameras=preview.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).ToArray();
                foreach(var c in cameras) {c.scene=preview;Capture(c,output+"/"+c.name+".png");}
                WriteReview(preview,output);
                ValidateVisualEnvelope(preview,output);
            }
            finally
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        static void Capture(Camera camera,string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var old=RenderTexture.active;var previousTarget=camera.targetTexture;
            var target=RenderTexture.GetTemporary(1600,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default);
            var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try
            {
                camera.aspect=16f/9f;
                if(GraphicsSettings.currentRenderPipeline==null) {camera.targetTexture=target;camera.Render();}
                else
                {
                    // Warm material/pipeline state after an editor color-space reload before retaining evidence.
                    RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest {destination=target});
                    RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest {destination=target});
                }
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
                File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            finally {camera.targetTexture=previousTarget;RenderTexture.active=old;Object.DestroyImmediate(pixels);RenderTexture.ReleaseTemporary(target);}
        }

        static void WriteBaselineReport()
        {
            var report="Unity "+Application.unityVersion+"\nActive pipeline: "+(GraphicsSettings.currentRenderPipeline==null?"Built-in":GraphicsSettings.currentRenderPipeline.name)+
                "\nColor space: "+PlayerSettings.colorSpace+"\nQuality: "+QualitySettings.names[QualitySettings.GetQualityLevel()]+
                "\nGPU: "+SystemInfo.graphicsDeviceName+"\nReported graphics memory MB: "+SystemInfo.graphicsMemorySize+
                "\nCPU: "+SystemInfo.processorType+"\nSystem memory MB: "+SystemInfo.systemMemorySize+
                "\nMeasurements are editor/offscreen evidence, not player-build performance acceptance.\n"+
                "All new layout dimensions are starting values. Validate with CameraReview.json; widen clearing or move modules outward if actors/cameras are obstructed.\n";
            File.WriteAllText(Evidence+"/WP00/Baseline/Environment.txt",report);
        }

        static string Hash(string path)
        {
            using(var hash=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();
        }
    }
}
