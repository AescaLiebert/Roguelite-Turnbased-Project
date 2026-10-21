using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using FightingAllstar.EnvironmentArt;
using FightingAllstar.Rendering;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

namespace FightingAllstar.EditorTools
{
    [InitializeOnLoad]
    public static class MapOptimizationLab
    {
        const string Root="Assets/Project/Art/Environment/MapStandard";
        const string Scenes="Assets/Project/Scenes/PerformanceReview";
        const string Evidence="Artifacts/MapOptimization";
        public const string Tier="Map Standard URP";
        static int bakeIndex;
        static bool baking;
        internal static bool BenchmarkBuild { get; private set; }
        static SceneSetup[] previous;
        static readonly string[] Maps={"BattleTeyvat","Battle"};
        static MapOptimizationLab(){EditorApplication.update+=Tick;}
        static void Tick()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(EditorApplication.isPlaying)
            {
                if(SessionState.GetBool("MapOptimization.Profiling",false))
                {
                    EditorApplication.QueuePlayerLoopUpdate();
                    if(SessionState.GetBool("MapOptimization.ProfileComplete",false))EditorApplication.isPlaying=false;
                }
                return;
            }
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            if(SessionState.GetBool("MapOptimization.Profiling",false))
            {
                SessionState.SetBool("MapOptimization.Profiling",false);SessionState.SetBool("MapOptimization.ProfileComplete",false);
                PlayerSettings.runInBackground=SessionState.GetBool("MapOptimization.Background",false);
                var settings=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];EditorUtility.SetDirty(settings);AssetDatabase.SaveAssetIfDirty(settings);
                var benchmarks=new HashSet<string>(BenchmarkScenes());EditorBuildSettings.scenes=EditorBuildSettings.scenes.Where(s=>!benchmarks.Contains(s.path)).ToArray();
                EditorSceneManager.OpenScene("Assets/Project/Scenes/MainMenu.unity");
            }
            try
            {
                if(baking){PollBake();return;}
                if(Request("build"))Build();
                else if(Request("profile"))Profile();
                else if(Request("player"))BuildPlayer();
                else if(Request("baseline"))BuildPlayer(true);
            }
            catch(Exception e){baking=false;Directory.CreateDirectory(Evidence);File.WriteAllText(Evidence+"/Failed.txt",e.ToString());Debug.LogException(e);if(previous!=null){EditorSceneManager.RestoreSceneManagerSetup(previous);previous=null;}}
        }
        static bool Request(string name)
        {var path="Temp/map-optimization-"+name+".request";if(!File.Exists(path))return false;File.Delete(path);return true;}
        [MenuItem("Fighting Allstar/Stage/Optimize Battle Maps")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new Exception("Save open scene edits first.");
            Directory.CreateDirectory(Root+"/Settings");Directory.CreateDirectory(Root+"/Materials/Osaka");Directory.CreateDirectory(Scenes);Directory.CreateDirectory(Evidence);AssetDatabase.Refresh();
            previous=EditorSceneManager.GetSceneManagerSetup();
            foreach(var name in Maps)if(!File.Exists(Scenes+"/MapProfile_"+name+"_Before.unity"))MakeBenchmark(name,"Before");
            var pipeline=BuildPipelineAsset();RegisterTier(pipeline);StandardizeProject(pipeline);
            var policy=AssetDatabase.LoadAssetAtPath<MapOptimizationPolicy>(Root+"/Settings/BattleMaps.asset");
            if(!policy){policy=ScriptableObject.CreateInstance<MapOptimizationPolicy>();AssetDatabase.CreateAsset(policy,Root+"/Settings/BattleMaps.asset");}
            EditorUtility.SetDirty(policy);
            foreach(var name in Maps)ConfigureMap(name,pipeline,policy);
            bakeIndex=0;baking=true;StartBake();
        }
        static UniversalRenderPipelineAsset BuildPipelineAsset()
        {
            var source="Assets/Project/Art/Environment/TeyvatGrassland/Lighting/Settings/TG_LightingURP.asset";
            var pipeline=Copy<UniversalRenderPipelineAsset>(source,Root+"/Settings/MapStandardURP.asset");
            var so=new SerializedObject(pipeline);var sourceRenderer=so.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue;
            var a=Copy<UniversalRendererData>(AssetDatabase.GetAssetPath(sourceRenderer),Root+"/Settings/MapTerrainRenderer.asset");
            var b=Copy<UniversalRendererData>(AssetDatabase.GetAssetPath(sourceRenderer),Root+"/Settings/MapStreetRenderer.asset");
            foreach(var renderer in new[]{a,b})
            {
                var r=new SerializedObject(renderer);r.FindProperty("m_RenderingMode").intValue=2; // Installed URP: ForwardPlus.
                r.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(renderer);
            }
            var glow=b.rendererFeatures.OfType<OsakaGlowRendererFeature>().FirstOrDefault();
            if(glow==null){glow=ScriptableObject.CreateInstance<OsakaGlowRendererFeature>();glow.name="Osaka exact glow";AssetDatabase.AddObjectToAsset(glow,b);b.rendererFeatures.Add(glow);}
            glow.shader=Shader.Find("Hidden/OsakaNight/URP/Glow");glow.SetActive(true);EditorUtility.SetDirty(glow);EditorUtility.SetDirty(b);
            var list=so.FindProperty("m_RendererDataList");list.arraySize=2;list.GetArrayElementAtIndex(0).objectReferenceValue=a;list.GetArrayElementAtIndex(1).objectReferenceValue=b;
            so.FindProperty("m_AdditionalLightShadowsSupported").boolValue=true;
            so.FindProperty("m_AdditionalLightsShadowmapResolution").intValue=2048;
            so.ApplyModifiedPropertiesWithoutUndo();
            pipeline.gpuResidentDrawerMode=GPUResidentDrawerMode.InstancedDrawing;
            pipeline.gpuResidentDrawerEnableOcclusionCullingInCameras=true;pipeline.smallMeshScreenPercentage=0;
            var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            graphics.FindProperty("m_BrgStripping").intValue=(int)BatchRendererGroupStrippingMode.KeepAll;graphics.ApplyModifiedPropertiesWithoutUndo();
            pipeline.name="Map Standard URP";EditorUtility.SetDirty(pipeline);AssetDatabase.SaveAssets();return pipeline;
        }
        static T Copy<T>(string source,string destination) where T:Object
        {var asset=AssetDatabase.LoadAssetAtPath<T>(destination);if(!asset){AssetDatabase.CopyAsset(source,destination);asset=AssetDatabase.LoadAssetAtPath<T>(destination);}return asset;}
        static void RegisterTier(UniversalRenderPipelineAsset pipeline)
        {
            var defaults=new Dictionary<string,int>();var reading=false;
            foreach(var line in File.ReadAllLines("ProjectSettings/QualitySettings.asset"))
            {
                if(line.StartsWith("  m_PerPlatformDefaultQuality:")){reading=true;continue;}if(!reading)continue;if(!line.StartsWith("    "))break;
                var pair=line.Trim().Split(':');if(pair.Length==2&&int.TryParse(pair[1].Trim(),out var value))defaults[pair[0]]=value;
            }
            var q=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);var levels=q.FindProperty("m_QualitySettings");var index=-1;
            for(var i=0;i<levels.arraySize;i++)if(levels.GetArrayElementAtIndex(i).FindPropertyRelative("name").stringValue==Tier)index=i;
            if(index<0){index=levels.arraySize;levels.InsertArrayElementAtIndex(index);}
            var p=levels.GetArrayElementAtIndex(index);p.FindPropertyRelative("name").stringValue=Tier;p.FindPropertyRelative("customRenderPipeline").objectReferenceValue=pipeline;p.FindPropertyRelative("excludedTargetPlatforms").ClearArray();
            if(defaults.Count>0)
            {
                var d=q.FindProperty("m_PerPlatformDefaultQuality");d.arraySize=defaults.Count;var j=0;
                foreach(var pair in defaults){var item=d.GetArrayElementAtIndex(j++);item.FindPropertyRelative("first").stringValue=pair.Key;item.FindPropertyRelative("second").intValue=pair.Value;}
            }
            q.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(q.targetObject);AssetDatabase.SaveAssetIfDirty(q.targetObject);
        }
        static void StandardizeProject(UniversalRenderPipelineAsset pipeline)
        {
            GraphicsSettings.defaultRenderPipeline=pipeline;
            var graphics=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0];EditorUtility.SetDirty(graphics);
            var quality=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);var levels=quality.FindProperty("m_QualitySettings");
            for(var i=0;i<levels.arraySize;i++)
            {var property=levels.GetArrayElementAtIndex(i).FindPropertyRelative("customRenderPipeline");if(property.objectReferenceValue==null)property.objectReferenceValue=pipeline;}
            quality.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(quality.targetObject);AssetDatabase.SaveAssets();
            var previousSetup=EditorSceneManager.GetSceneManagerSetup();
            var training=EditorSceneManager.OpenScene("Assets/Project/Scenes/Training.unity");
            var context=Object.FindFirstObjectByType<MapRenderContext>();if(!context)context=new GameObject("Shared map rendering").AddComponent<MapRenderContext>();
            context.pipeline=pipeline;context.actorShader=Shader.Find("Universal Render Pipeline/Lit");context.qualityTier=Tier;context.rendererIndex=0;context.postProcessing=false;context.adaptSceneStandardMaterials=true;
            EditorSceneManager.MarkSceneDirty(training);EditorSceneManager.SaveScene(training);EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
        }
        static void ConfigureMap(string name,UniversalRenderPipelineAsset pipeline,MapOptimizationPolicy policy)
        {
            var scene=EditorSceneManager.OpenScene("Assets/Project/Scenes/"+name+".unity");
            ConfigureOpenMap(scene,pipeline,policy);
        }
        public static bool ApplyStandardToOpenMap()
        {
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root+"/Settings/MapStandardURP.asset");
            var policy=AssetDatabase.LoadAssetAtPath<MapOptimizationPolicy>(Root+"/Settings/BattleMaps.asset");
            if(!pipeline||!policy)return false;
            RegisterTier(pipeline);ConfigureOpenMap(SceneManager.GetActiveScene(),pipeline,policy);
            Debug.Log("Map rendering standard reapplied. Re-bake occlusion after changing map geometry.");return true;
        }
        static void ConfigureOpenMap(Scene scene,UniversalRenderPipelineAsset pipeline,MapOptimizationPolicy policy)
        {
            var name=scene.name;
            var stage=name=="BattleTeyvat"?Object.FindFirstObjectByType<TeyvatStageBinding>().gameObject:GameObject.Find("Osaka Night • Walking Mall");
            MapRenderContext context;
            if(name=="BattleTeyvat")context=Object.FindFirstObjectByType<TeyvatDungeonArena>();
            else
            {
                context=Object.FindFirstObjectByType<MapRenderContext>();if(!context)context=new GameObject("Shared map rendering").AddComponent<MapRenderContext>();
                ConvertOsaka(stage);var sky=RenderSettings.skybox;if(sky)RenderSettings.skybox=ConvertMaterial(sky);
                foreach(var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(l=>l.gameObject.scene==scene))
                {
                    light.GetUniversalAdditionalLightData().usePipelineSettings=false;
                    var authored=PrefabUtility.GetCorrespondingObjectFromOriginalSource(light);
                    if(authored&&(light.type==LightType.Point||light.type==LightType.Spot))
                    {
                        // Built-in's normalized range attenuation and URP's inverse
                        // square attenuation use different intensity scales.
                        light.intensity=authored.intensity*authored.range*authored.range*.03f;
                        if(light.type==LightType.Spot)light.innerSpotAngle=light.spotAngle*.8f;
                    }
                }
            }
            context.pipeline=pipeline;context.qualityTier=Tier;context.rendererIndex=name=="Battle"?1:0;context.actorShader=Shader.Find("Universal Render Pipeline/Lit");context.farClipMinimum=name=="BattleTeyvat"?1600:0;context.postProcessing=true;
            var camera=Camera.main;camera.useOcclusionCulling=true;var additional=camera.GetUniversalAdditionalCameraData();additional.SetRenderer(context.rendererIndex);additional.renderPostProcessing=true;additional.requiresDepthTexture=true;
            var setup=stage.GetComponent<MapOptimizationSetup>();if(!setup)setup=stage.AddComponent<MapOptimizationSetup>();setup.policy=policy;
            var lods=stage.GetComponentsInChildren<LODGroup>(true);var lodRenderers=new HashSet<Renderer>(lods.SelectMany(l=>l.GetLODs()).SelectMany(l=>l.renderers));
            var cliffOccluders=new HashSet<Renderer>(lods.Where(l=>l.name.StartsWith("SM_R3_Cliff_")).SelectMany(l=>l.GetLODs().Take(1)).SelectMany(l=>l.renderers));
            setup.staticRenderers=stage.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.enabled&&r.gameObject.activeInHierarchy&&!lodRenderers.Contains(r)&&r.sharedMaterials.All(m=>m&&m.renderQueue<2450)&&!r.HasPropertyBlock()).ToArray();
            foreach(var r in stage.GetComponentsInChildren<MeshRenderer>(true))
            {
                var flags=GameObjectUtility.GetStaticEditorFlags(r.gameObject);flags&=~(StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic|StaticEditorFlags.BatchingStatic);
                if(r.enabled&&r.gameObject.activeInHierarchy)
                {
                    flags|=StaticEditorFlags.OccludeeStatic;
                    if(!lodRenderers.Contains(r)&&r.sharedMaterials.All(m=>m&&m.renderQueue<2450)){flags|=StaticEditorFlags.OccluderStatic|StaticEditorFlags.BatchingStatic;}
                    if(cliffOccluders.Contains(r)&&r.sharedMaterials.All(m=>m&&m.renderQueue<2450))flags|=StaticEditorFlags.OccluderStatic;
                }
                GameObjectUtility.SetStaticEditorFlags(r.gameObject,flags);
            }
            if(name=="BattleTeyvat")
            {
                var instancer=stage.GetComponent<MapLODInstancer>();if(!instancer)instancer=stage.AddComponent<MapLODInstancer>();
                instancer.sources=lods.Where(l=>l.GetComponentsInChildren<MeshFilter>(true).Any(f=>f.sharedMesh&&f.sharedMesh.name.StartsWith("SM_TG_"))).ToArray();
                instancer.cellSize=policy.spatialCellMetres;
                instancer.enabled=false; // Native GPU Resident Drawer owns these renderers and their LODs.
                var padding=stage.GetComponent<MapWindBoundsPadding>();if(!padding)padding=stage.AddComponent<MapWindBoundsPadding>();padding.sources=instancer.sources;
                foreach(var meadow in Object.FindObjectsByType<TeyvatMeadowRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(m=>m.gameObject.scene==scene))
                {meadow.frontToBack=true;EditorUtility.SetDirty(meadow);PrefabUtility.RecordPrefabInstancePropertyModifications(meadow);}
            }
            var area=stage.GetComponent<OcclusionArea>();if(!area)area=stage.AddComponent<OcclusionArea>();area.center=new Vector3(0,20,0);area.size=policy.cameraVolumeSize;
            var binding=stage.GetComponent<TeyvatStageBinding>();if(binding)binding.meadow.data=binding.canonicalMeadowData;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);if(binding)binding.ApplyBinding();
            File.WriteAllText(Evidence+"/"+name+"_Setup.txt","Native static candidates="+setup.staticRenderers.Length+"; source LOD groups="+lods.Length+"; shared URP="+AssetDatabase.GetAssetPath(pipeline)+"; renderer="+context.rendererIndex+". No texture import, material quality, shadow resolution, render scale or authored LOD thresholds reduced.");
        }
        static readonly Dictionary<Material,Material> converted=new Dictionary<Material,Material>();
        static void ConvertOsaka(GameObject root)
        {
            converted.Clear();foreach(var r in root.GetComponentsInChildren<Renderer>(true))r.sharedMaterials=r.sharedMaterials.Select(ConvertMaterial).ToArray();
            foreach(var atmosphere in Object.FindObjectsByType<OsakaNightAtmosphere>(FindObjectsInactive.Include,FindObjectsSortMode.None))atmosphere.enabled=false;
        }
        static Material ConvertMaterial(Material source)
        {
            if(!source)return null;
            if(AssetDatabase.GetAssetPath(source).StartsWith(Root+"/Materials/Osaka/"))
            {
                var filename=Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(source));var suffix=filename.Substring(filename.LastIndexOf('_')+1);
                var guid=AssetDatabase.FindAssets("t:Material",new[]{"Assets/Project/Art/Environment/OsakaNight/Materials"}).FirstOrDefault(g=>g.StartsWith(suffix));
                return guid!=null?ConvertMaterial(AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid))):source;
            }
            if(converted.TryGetValue(source,out var found))return found;
            var path=Root+"/Materials/Osaka/"+source.name+"_"+AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source)).Substring(0,8)+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(source);AssetDatabase.CreateAsset(m,path);}else EditorUtility.CopySerialized(source,m);
            m.name=source.name+" URP";var shader=source.shader.name;
            if(shader=="OsakaNight/Neon")m.shader=Shader.Find("OsakaNight/URP/Neon");
            else if(shader=="OsakaNight/Sky")m.shader=Shader.Find("OsakaNight/URP/Sky");
            else if(shader=="Standard"||shader=="Standard (Specular setup)")
            {
                m.shader=Shader.Find("Universal Render Pipeline/Lit");m.shaderKeywords=Array.Empty<string>();
                m.SetTexture("_BaseMap",source.mainTexture);m.SetTextureScale("_BaseMap",source.mainTextureScale);m.SetTextureOffset("_BaseMap",source.mainTextureOffset);m.SetColor("_BaseColor",source.color);
                m.SetFloat("_Metallic",source.GetFloat("_Metallic"));m.SetFloat("_Smoothness",source.GetFloat("_Glossiness"));
                foreach(var p in new[]{"_BumpMap","_MetallicGlossMap","_OcclusionMap","_EmissionMap"})if(source.HasProperty(p))m.SetTexture(p,source.GetTexture(p));
                foreach(var p in new[]{"_BumpScale","_OcclusionStrength","_Cutoff"})if(source.HasProperty(p))m.SetFloat(p,source.GetFloat(p));
                if(source.GetTexture("_BumpMap"))m.EnableKeyword("_NORMALMAP");if(source.GetTexture("_MetallicGlossMap"))m.EnableKeyword("_METALLICSPECGLOSSMAP");if(source.GetTexture("_OcclusionMap"))m.EnableKeyword("_OCCLUSIONMAP");
                m.SetColor("_EmissionColor",source.GetColor("_EmissionColor"));if(source.IsKeywordEnabled("_EMISSION"))m.EnableKeyword("_EMISSION");
                var mode=(int)source.GetFloat("_Mode");if(mode==1){m.SetFloat("_AlphaClip",1);m.EnableKeyword("_ALPHATEST_ON");m.renderQueue=2450;}
                if(mode>=2){m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_BlendModePreserveSpecular",mode==3?1:0);m.SetFloat("_SrcBlend",mode==3?1:5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");if(mode==3)m.EnableKeyword("_ALPHAPREMULTIPLY_ON");m.renderQueue=3000;m.SetOverrideTag("RenderType","Transparent");}
            }
            else if(shader=="Particles/Standard Unlit")
            {
                m.shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");m.SetTexture("_BaseMap",source.mainTexture);m.SetColor("_BaseColor",source.color);m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetFloat("_QueueOffset",source.renderQueue-3000);m.renderQueue=source.renderQueue;
            }
            m.enableInstancing=true;EditorUtility.SetDirty(m);converted[source]=m;return m;
        }
        static void StartBake()
        {
            var scene=EditorSceneManager.OpenScene("Assets/Project/Scenes/"+Maps[bakeIndex]+".unity");
            var policy=AssetDatabase.LoadAssetAtPath<MapOptimizationPolicy>(Root+"/Settings/BattleMaps.asset");
            StaticOcclusionCulling.smallestOccluder=policy.smallestOccluder;StaticOcclusionCulling.smallestHole=policy.smallestHole;StaticOcclusionCulling.backfaceThreshold=policy.backfaceThreshold;
            EditorSceneManager.SaveScene(scene);
            if(!StaticOcclusionCulling.GenerateInBackground())throw new Exception("Occlusion bake did not start for "+scene.name);
            File.WriteAllText(Evidence+"/BakeStatus.txt","Baking "+scene.name);
        }
        static void PollBake()
        {
            if(StaticOcclusionCulling.isRunning)return;
            var scene=SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            var header=File.ReadAllText(scene.path);var guid=System.Text.RegularExpressions.Regex.Match(header,@"m_OcclusionCullingData: \{fileID: \d+, guid: ([a-f0-9]+)").Groups[1].Value;
            var path=AssetDatabase.GUIDToAssetPath(guid);var pvs=File.Exists(path)?System.Text.RegularExpressions.Regex.Match(File.ReadAllText(path),@"m_PVSData: ([a-fA-F0-9]+)").Groups[1].Value.Length/2:0;
            if(pvs<=0)throw new Exception("Occlusion produced no persisted PVS for "+scene.name);
            File.WriteAllText(Evidence+"/"+Maps[bakeIndex]+"_Occlusion.txt","Verified persisted PVS bytes="+pvs+"; asset="+path+"; file bytes="+new FileInfo(path).Length+". Native umbraDataSize counter reports "+StaticOcclusionCulling.umbraDataSize+" on this Editor and is not used as proof. Exact occluder geometry, no added target-picking colliders.");
            bakeIndex++;if(bakeIndex<Maps.Length){StartBake();return;}
            foreach(var name in Maps)MakeBenchmark(name,"After");baking=false;
            File.WriteAllText(Evidence+"/BuildPassed.txt","Both production maps configured; native occlusion baked; Before/After profile scenes saved.");
            if(File.Exists(Evidence+"/Failed.txt"))File.Delete(Evidence+"/Failed.txt");
            EditorSceneManager.RestoreSceneManagerSetup(previous);previous=null;
        }
        static MapPerformanceProbe.Pose Pose(string name,Vector3 p,Vector3 target,float fov)
        {return new MapPerformanceProbe.Pose{name=name,position=p,rotation=Quaternion.LookRotation(target-p),fov=fov};}
        static void MakeBenchmark(string name,string variant)
        {
            var scene=EditorSceneManager.OpenScene("Assets/Project/Scenes/"+name+".unity");
            EditorSceneManager.SaveScene(scene,Scenes+"/MapProfile_"+name+"_"+variant+".unity");
            foreach(var root in scene.GetRootGameObjects())
            {
                if(root.GetComponent<Camera>()||root.GetComponent<TeyvatStageBinding>()||root.GetComponent<TeyvatLightingRig>()||root.GetComponent<MapRenderContext>()||root.name=="Osaka Night • Walking Mall"||root.GetComponent<Light>())continue;
                Object.DestroyImmediate(root);
            }
            var camera=Camera.main;var probe=new GameObject("Controlled map performance probe").AddComponent<MapPerformanceProbe>();probe.mapName=name;probe.variant=variant;probe.view=camera;
            var origin=new Vector3(-.16630173f,1.8876038f,-10.101893f);
            probe.poses=new[]{Pose("Planning",origin+new Vector3(0,13.5f,-17),origin+new Vector3(0,1,0),60),Pose("Reverse",origin+new Vector3(0,13.5f,17),origin+new Vector3(0,1,0),60),Pose("Left",origin+new Vector3(0,6,-5),origin+new Vector3(-90,15,-15),68),Pose("Right",origin+new Vector3(0,6,-5),origin+new Vector3(140,14,-25),68),Pose("Rear",origin+new Vector3(0,7,2),origin+new Vector3(0,20,-200),68),Pose("Attack",origin+new Vector3(1.35f,3.9f,-10.4f),origin+new Vector3(0,1.5f,5),58)};
            var chain=new[]{"MapProfile_BattleTeyvat_Before","MapProfile_Battle_Before","MapProfile_BattleTeyvat_After","MapProfile_Battle_After"};var index=Array.IndexOf(chain,scene.name);probe.nextScene=index>=0&&index<chain.Length-1?chain[index+1]:null;
            var binding=Object.FindFirstObjectByType<TeyvatStageBinding>();if(binding){binding.globals.fixedTime=true;binding.globals.fixedSeconds=1;binding.globals.Apply();binding.meadow.data=binding.canonicalMeadowData;}
            camera.useOcclusionCulling=true;EditorSceneManager.SaveScene(scene);if(binding)binding.ApplyBinding();
        }
        static string[] BenchmarkScenes()=>new[]{"MapProfile_BattleTeyvat_Before","MapProfile_Battle_Before","MapProfile_BattleTeyvat_After","MapProfile_Battle_After"}.Select(n=>Scenes+"/"+n+".unity").ToArray();
        static void Profile()
        {
            SessionState.SetBool("MapOptimization.Background",PlayerSettings.runInBackground);SessionState.SetBool("MapOptimization.Profiling",true);SessionState.SetBool("MapOptimization.ProfileComplete",false);
            var list=EditorBuildSettings.scenes.ToList();foreach(var p in BenchmarkScenes())if(!list.Any(s=>s.path==p))list.Add(new EditorBuildSettingsScene(p,true));EditorBuildSettings.scenes=list.ToArray();
            EditorSceneManager.OpenScene(BenchmarkScenes()[0]);EditorApplication.EnterPlaymode();
        }
        static void BuildPlayer(bool legacy=false)
        {
            var path=Evidence+(legacy?"/WindowsBaseline/MapBenchmark.exe":"/Windows/MapBenchmark.exe");Directory.CreateDirectory(Path.GetDirectoryName(path));
            var old=PlayerSettings.enableFrameTimingStats;PlayerSettings.enableFrameTimingStats=true;
            // The build can unload pipeline assets after their temporary removal
            // from settings. Managed Object references alone do not keep those
            // native assets alive, so restore each exact reference by asset path.
            var defaultPipelinePath=AssetDatabase.GetAssetPath(GraphicsSettings.defaultRenderPipeline);var originalTier=QualitySettings.GetQualityLevel();var pipelines=new string[QualitySettings.names.Length];
            for(var i=0;i<pipelines.Length;i++)pipelines[i]=AssetDatabase.GetAssetPath(QualitySettings.GetRenderPipelineAssetAt(i));
            try
            {
                BenchmarkBuild=true;
                if(legacy)
                {
                    GraphicsSettings.defaultRenderPipeline=null;
                    for(var i=0;i<pipelines.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=null;}
                    QualitySettings.SetQualityLevel(originalTier,false);
                    CoreBuildData.instance.Dispose(); // Discard SRP build data cached before the temporary baseline switch.
                }
                var report=UnityEditor.BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=legacy?new[]{Scenes+"/MapProfile_Battle_Before.unity"}:BenchmarkScenes(),locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development|BuildOptions.CompressWithLz4});
                File.WriteAllText(Evidence+(legacy?"/WindowsBaselineBuild.txt":"/WindowsBuild.txt"),report.summary.result+" bytes="+report.summary.totalSize+" duration="+report.summary.totalTime);
            }
            finally{BenchmarkBuild=false;PlayerSettings.enableFrameTimingStats=old;if(legacy){for(var i=0;i<pipelines.Length;i++){QualitySettings.SetQualityLevel(i,false);QualitySettings.renderPipeline=string.IsNullOrEmpty(pipelines[i])?null:AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(pipelines[i]);}GraphicsSettings.defaultRenderPipeline=string.IsNullOrEmpty(defaultPipelinePath)?null:AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(defaultPipelinePath);QualitySettings.SetQualityLevel(originalTier,false);CoreBuildData.instance.Dispose();}AssetDatabase.SaveAssets();}
        }
    }
}
