using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace FightingAllstar.EditorTools
{
    public static partial class TeyvatFoundationBuilder
    {
        const string TrialMarker="TG_ISOLATED_RENDERING_TRIAL.txt";
        static double trialNext;
        static int trialPhase;
        static string trialOutput;
        static UniversalRenderPipelineAsset trialAsset;

        [MenuItem("Fighting Allstar/Teyvat Foundation/Export Isolated Trial Inputs")]
        public static void ExportTrialInputs()
        {
            var all=AssetDatabase.FindAssets("",new[]{Art}).Select(AssetDatabase.GUIDToAssetPath).Where(File.Exists).ToList();
            all.Add(ScenePath);
            var editorSources=new[]{"Assets/Project/Editor/TeyvatFoundationBuilder.cs","Assets/Project/Editor/TeyvatFoundationGeometry.cs",
                "Assets/Project/Editor/TeyvatFoundationTrial.cs","Assets/Project/Editor/TeyvatFoundationValidation.cs",
                "Assets/Project/Editor/TeyvatReferenceTerrain.cs","Assets/Project/Editor/TeyvatReferenceSky.cs","Assets/Project/Editor/TeyvatReferenceDressing.cs"};
            all.AddRange(editorSources);
            if(File.Exists("Assets/UniversalRenderPipelineGlobalSettings.asset"))all.Add("Assets/UniversalRenderPipelineGlobalSettings.asset");
            all.AddRange(AssetDatabase.GetDependencies(all.ToArray(),true).Where(p=>p.StartsWith("Assets/")&&File.Exists(p)));
            all=all.Distinct().OrderBy(p=>p).ToList();
            if(all.Any(p=>p.EndsWith(".cs")&&!editorSources.Contains(p)))
                throw new InvalidOperationException("Trial dependency unexpectedly contains a gameplay script; inspect before export.");
            Directory.CreateDirectory(Evidence+"/WP00");
            File.WriteAllLines(Evidence+"/WP00/TrialInputs.txt",all);
            Debug.Log("Trial dependency manifest: "+all.Count+" files; source main-project assets remain untouched.");
        }

        /// <summary>Entry point for a second, hidden Unity process. Refuses to configure the main project.</summary>
        public static void RunIsolatedTrial()
        {
            var root=Path.GetDirectoryName(Application.dataPath);
            if(!File.Exists(Path.Combine(root,TrialMarker))||File.Exists(BattlePath))
                throw new InvalidOperationException("Rendering trial must run in the dedicated exported trial project, never the gameplay project.");
            trialOutput=File.ReadAllText(Path.Combine(root,TrialMarker)).Trim();
            Directory.CreateDirectory(trialOutput);
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            PlayerSettings.colorSpace=ColorSpace.Gamma;
            QualitySettings.renderPipeline=null;GraphicsSettings.defaultRenderPipeline=null;
            trialPhase=0;trialNext=EditorApplication.timeSinceStartup+4;
            EditorApplication.update+=TrialTick;
        }

        public static void RunIsolatedLinearReview()
        {
            var root=Path.GetDirectoryName(Application.dataPath);
            if(!File.Exists(Path.Combine(root,TrialMarker))||File.Exists(BattlePath))
                throw new InvalidOperationException("Linear capture must run in the isolated trial project.");
            if(PlayerSettings.colorSpace!=ColorSpace.Linear)throw new InvalidOperationException("Run the full trial first to establish Linear settings.");
            trialOutput=File.ReadAllText(Path.Combine(root,TrialMarker)).Trim();
            trialAsset=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Art+"/Settings/TG_TrialURP.asset");
            GraphicsSettings.defaultRenderPipeline=trialAsset;QualitySettings.renderPipeline=trialAsset;
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            trialNext=EditorApplication.timeSinceStartup+5;
            EditorApplication.update+=FinishLinearReview;
        }

        static void FinishLinearReview()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<trialNext)return;
            EditorApplication.update-=FinishLinearReview;
            try
            {
                CaptureSavedScene(ScenePath,trialOutput+"/URP_Linear",true);
                File.WriteAllText(trialOutput+"/LINEAR_REVIEW_COMPLETE.txt","Linear camera set recaptured after cold-process restart with retained-frame warmup.\n");
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);File.WriteAllText(trialOutput+"/FAILED.txt",e.ToString());EditorApplication.Exit(1);}
        }

        static void TrialTick()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<trialNext)return;
            try
            {
                if(trialPhase==0)
                {
                    CaptureSavedScene(ScenePath,trialOutput+"/BuiltInGamma",false);
                    ConfigureTrialPipeline();
                    trialPhase=1;trialNext=EditorApplication.timeSinceStartup+5;
                }
                else if(trialPhase==1)
                {
                    CaptureCompatibility(trialOutput+"/URP_Gamma");
                    ConvertTrialMaterials();
                    EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
                    CaptureSavedScene(ScenePath,trialOutput+"/URP_Gamma",true);
                    PlayerSettings.colorSpace=ColorSpace.Linear;
                    AssetDatabase.SaveAssets();
                    trialPhase=2;trialNext=EditorApplication.timeSinceStartup+5;
                }
                else
                {
                    EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
                    CaptureSavedScene(ScenePath,trialOutput+"/URP_Linear",true);
                    CaptureCompatibility(trialOutput+"/URP_Linear");
                    WriteTrialSettings();
                    EditorApplication.update-=TrialTick;
                    File.WriteAllText(trialOutput+"/COMPLETE.txt","Isolated Built-in/Gamma, URP/Gamma, URP/Linear captures completed. Source gameplay project unchanged.\n");
                    EditorApplication.Exit(0);
                }
            }
            catch(Exception e)
            {
                EditorApplication.update-=TrialTick;Debug.LogException(e);
                File.WriteAllText(trialOutput+"/FAILED.txt",e.ToString());EditorApplication.Exit(1);
            }
        }

        static void ConfigureTrialPipeline()
        {
            Directory.CreateDirectory(Art+"/Settings");AssetDatabase.Refresh();
            var method=typeof(UniversalRenderPipelineAsset).GetMethod("CreateRendererAsset",BindingFlags.Static|BindingFlags.NonPublic);
            if(method==null)throw new MissingMethodException("URP 17.3 CreateRendererAsset could not be found.");
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Art+"/Settings/TG_TrialURP_Renderer.asset");
            if(renderer==null)
            {
                var parameters=method.GetParameters();
                renderer=(UniversalRendererData)method.Invoke(null,new object[]{Art+"/Settings/TG_TrialURP.asset",Enum.Parse(parameters[1].ParameterType,"UniversalRenderer"),true,"Renderer"});
            }
            renderer.renderingMode=RenderingMode.Forward;
            trialAsset=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Art+"/Settings/TG_TrialURP.asset");
            if(trialAsset==null){trialAsset=UniversalRenderPipelineAsset.Create(renderer);AssetDatabase.CreateAsset(trialAsset,Art+"/Settings/TG_TrialURP.asset");}
            trialAsset.supportsHDR=true;trialAsset.msaaSampleCount=4;trialAsset.shadowDistance=80;
            trialAsset.shadowCascadeCount=2;trialAsset.supportsCameraDepthTexture=true;trialAsset.supportsCameraOpaqueTexture=false;
            GraphicsSettings.defaultRenderPipeline=trialAsset;QualitySettings.renderPipeline=trialAsset;
            var globalType=typeof(UniversalRenderPipelineAsset).Assembly.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings");
            var ensure=globalType==null?null:globalType.GetMethod("Ensure",BindingFlags.Static|BindingFlags.NonPublic);
            if(ensure!=null)ensure.Invoke(null,new object[]{true});
            EditorUtility.SetDirty(trialAsset);AssetDatabase.SaveAssets();
        }

        static void ConvertTrialMaterials()
        {
            foreach(var path in AssetDatabase.FindAssets("t:Material",new[]{Art}).Select(AssetDatabase.GUIDToAssetPath))
            {
                if(path.Contains("/Compatibility/"))continue;
                var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(m.shader.name!="Standard")continue;
                var color=m.color;var texture=m.mainTexture;var smooth=m.GetFloat("_Glossiness");
                m.shader=Shader.Find("Universal Render Pipeline/Lit");m.SetColor("_BaseColor",color);m.SetTexture("_BaseMap",texture);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",0);
                EditorUtility.SetDirty(m);
            }
            AssetDatabase.SaveAssets();
        }

        [Serializable] sealed class CompatibilitySample {public string material;public string shader;public string candidateShader;public float originalMagentaFraction;public float candidateMagentaFraction;public string note;}
        [Serializable] sealed class CompatibilityReport {public string colorSpace;public string context;public CompatibilitySample[] samples;}

        static void CaptureCompatibility(string output)
        {
            Directory.CreateDirectory(output);
            var saved=SceneManager.GetActiveScene();
            var preview=EditorSceneManager.NewPreviewScene();
            var records=new List<CompatibilitySample>();
            try
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);SceneManager.MoveGameObjectToScene(go,preview);go.transform.localScale=Vector3.one*3;
                var colors=new Color[go.GetComponent<MeshFilter>().sharedMesh.vertexCount];for(var i=0;i<colors.Length;i++)colors[i]=new Color(.3f,.8f,1,1);
                var mesh=Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);mesh.colors=colors;go.GetComponent<MeshFilter>().sharedMesh=mesh;
                var camera=new GameObject("Compatibility camera").AddComponent<Camera>();SceneManager.MoveGameObjectToScene(camera.gameObject,preview);
                camera.scene=preview;camera.transform.position=new Vector3(0,0,-6);camera.fieldOfView=40;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.18f,.18f,.18f);
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
                var sun=new GameObject("Compatibility light").AddComponent<Light>();SceneManager.MoveGameObjectToScene(sun.gameObject,preview);sun.type=LightType.Directional;sun.intensity=1;sun.transform.rotation=Quaternion.Euler(45,-25,0);
                foreach(var path in AssetDatabase.FindAssets("t:Material",new[]{Art+"/Compatibility"}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>Path.GetFileName(p).StartsWith("Source_")))
                {
                    var original=AssetDatabase.LoadAssetAtPath<Material>(path);go.GetComponent<Renderer>().sharedMaterial=original;
                    var name=Path.GetFileNameWithoutExtension(path);var magenta=Probe(camera,output+"/"+name+".png");
                    var candidate=new Material(original);
                    var note="Original custom shader retained; absence of error-pink does not prove parity of lighting, blending, depth or animation.";
                    if(original.shader.name=="Standard")
                    {
                        candidate.shader=Shader.Find("Universal Render Pipeline/Lit");candidate.SetColor("_BaseColor",original.color);candidate.SetTexture("_BaseMap",original.mainTexture);
                        candidate.SetFloat("_Smoothness",original.GetFloat("_Glossiness"));note="Trial-only Standard to URP/Lit mapping of color/base map/smoothness; full material conversion not claimed.";
                    }
                    go.GetComponent<Renderer>().sharedMaterial=candidate;
                    var candidateMagenta=Probe(camera,output+"/Candidate_"+name+".png");
                    records.Add(new CompatibilitySample{material=path,shader=original.shader.name,candidateShader=candidate.shader.name,originalMagentaFraction=magenta,candidateMagentaFraction=candidateMagenta,note=note});
                    Object.DestroyImmediate(candidate);
                }
                Object.DestroyImmediate(mesh);
            }
            finally {EditorSceneManager.ClosePreviewScene(preview);if(saved.IsValid()&&saved.isLoaded)SceneManager.SetActiveScene(saved);}
            File.WriteAllText(output+"/Compatibility.json",JsonUtility.ToJson(new CompatibilityReport{colorSpace=PlayerSettings.colorSpace.ToString(),context="Isolated representative material sphere probes; fraction of image with error-pink-like pixels. This tests render visibility, not full gameplay/effect behavior.",samples=records.ToArray()},true));
        }

        static float Probe(Camera camera,string path)
        {
            var target=RenderTexture.GetTemporary(384,384,24,RenderTextureFormat.ARGB32);var old=RenderTexture.active;
            var texture=new Texture2D(384,384,TextureFormat.RGB24,false);
            try
            {
                camera.aspect=1;RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
                RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,384,384),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());
                var pixels=texture.GetPixels32();return pixels.Count(c=>c.r>180&&c.b>180&&c.g<75)/(float)pixels.Length;
            }
            finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(texture);}
        }

        static void WriteTrialSettings()
        {
            var text="Unity "+Application.unityVersion+"; URP 17.3.0; Forward renderer; final color space "+PlayerSettings.colorSpace+
                "; HDR enabled; MSAA 4; shadow distance 80; cascades 2; depth texture enabled; opaque texture disabled; no post-processing.\n"+
                "These are starting values for foundation comparison, not final budgets. Validate edge quality, missing shadows, GPU cost and depth features on the named target device before accepting them. Reduce MSAA/shadow distance or enable opaque texture only if measured need justifies it.\n"+
                "This isolated minimal dependency project tests geometry and representative material copies, not complete gameplay, UI or player-build timing.\n";
            File.WriteAllText(trialOutput+"/Settings.txt",text);
        }
    }
}
