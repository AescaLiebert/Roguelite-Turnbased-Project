using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BuildBattleEntityArt
{
    private const string Root = "Assets/Project/Art/Summons/";
    [MenuItem("Tools/Fighting Allstar/Summons/Build Presentation Assets")]
    public static void BuildAll() { foreach (var name in new[] { "Guoba", "Oz", "JadeScreen" }) Build(name); AssetDatabase.SaveAssets(); }

    public static string Build(string entity)
    {
        var slug=entity.ToLowerInvariant();var path=Root+entity+"/";
        var modelPath=path+"Models/"+slug+".fbx";
        if (!File.Exists(modelPath)) throw new FileNotFoundException(modelPath);
        var importer=(ModelImporter)AssetImporter.GetAtPath(modelPath);
        importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation=true;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.importCameras=false;importer.importLights=false;
        importer.isReadable=false;importer.globalScale=1;importer.bakeAxisConversion=true;
        importer.resampleCurves=true;importer.animationCompression=ModelImporterAnimationCompression.Off;
        var clips=importer.defaultClipAnimations;
        foreach (var clip in clips)
        {
            clip.name=clip.takeName.Split('|').Last();clip.loopTime=clip.name=="Idle";clip.loopPose=clip.loopTime;
            clip.lockRootRotation=true;clip.lockRootHeightY=true;clip.lockRootPositionXZ=true;clip.events=Array.Empty<AnimationEvent>();
        }
        importer.clipAnimations=clips;importer.SaveAndReimport();
        foreach (var texturePath in Directory.GetFiles(path+"Textures","*.png"))
        {
            var ti=(TextureImporter)AssetImporter.GetAtPath(texturePath.Replace('\\','/'));
            ti.sRGBTexture=!texturePath.Contains("_masks");ti.alphaSource=TextureImporterAlphaSource.FromInput;
            ti.mipmapEnabled=true;ti.wrapMode=TextureWrapMode.Clamp;ti.maxTextureSize=1024;
            ti.textureCompression=TextureImporterCompression.Uncompressed;ti.SaveAndReimport();
        }
        Directory.CreateDirectory(path+"Materials");Directory.CreateDirectory(path+"Prefabs");Directory.CreateDirectory(path+"Animation");
        var paint=Material(path+"Materials/"+entity+"Paint.mat",path,slug,false);
        var panel=entity=="JadeScreen" ? Material(path+"Materials/JadePanel.mat",path,slug,true) : paint;
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset);
        try
        {
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>(true))
                renderer.sharedMaterials=Enumerable.Repeat(renderer.name.Contains("Translucent") ? panel : paint,renderer.sharedMaterials.Length).ToArray();
            foreach(var animator in instance.GetComponentsInChildren<Animator>()) { animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate; }
            var imported=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            var bounds=new Bounds(Vector3.up,Vector3.zero);
            var baked=new Mesh();
            foreach(var clip in imported)
                for(var f=0;f<=30;f++)
                {
                    clip.SampleAnimation(instance,clip.length*f/30f);
                    foreach(var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        renderer.BakeMesh(baked);
                        foreach(var v in baked.vertices) bounds.Encapsulate(instance.transform.InverseTransformPoint(renderer.transform.TransformPoint(v)));
                    }
                }
            UnityEngine.Object.DestroyImmediate(baked);
            // Restore bind/idle pose before saving the prefab, never an arbitrary final sampled frame.
            var idle=imported.FirstOrDefault(c=>c.name=="Idle");if(idle!=null) idle.SampleAnimation(instance,0);
            foreach(var renderer in instance.GetComponentsInChildren<SkinnedMeshRenderer>())
            { renderer.updateWhenOffscreen=true;renderer.localBounds=new Bounds(renderer.transform.InverseTransformPoint(instance.transform.TransformPoint(bounds.center)),bounds.size*1.2f); }
            var prefab=PrefabUtility.SaveAsPrefabAsset(instance,path+"Prefabs/"+entity+".prefab");
            var profilePath=path+"Animation/"+entity+"Presentation.asset";
            var profile=AssetDatabase.LoadAssetAtPath<BattleEntityPresentationProfile>(profilePath);
            if(profile==null) { profile=ScriptableObject.CreateInstance<BattleEntityPresentationProfile>();AssetDatabase.CreateAsset(profile,profilePath); }
            profile.definitionId=entity=="Guoba" ? "summon.guoba" : entity=="Oz" ? "summon.oz" : "geo-construct.jade-screen";
            profile.prefab=prefab;profile.presentationBounds=bounds;
            profile.modelScale=entity=="Guoba" ? .72f : entity=="Oz" ? .75f : 1.1f;
            profile.teamFront=entity=="JadeScreen";profile.followFormation=entity=="Oz";
            profile.placementOffset=entity=="Guoba" ? new Vector3(1.0f,0,.65f) : entity=="Oz" ? new Vector3(-1.1f,1.2f,.1f) : new Vector3(0,0,1.35f);
            profile.effectColor=entity=="Guoba" ? new Color(1,.27f,.035f) : entity=="Oz" ? new Color(.61f,.3f,1) : new Color(1,.72f,.22f);
            profile.spawn=imported.FirstOrDefault(c=>c.name=="Spawn");profile.idle=idle;profile.attack=imported.FirstOrDefault(c=>c.name=="Attack");
            profile.absorbHit=imported.FirstOrDefault(c=>c.name=="AbsorbHit");profile.capacityBreak=imported.FirstOrDefault(c=>c.name=="Break");profile.despawn=imported.FirstOrDefault(c=>c.name=="Despawn");
            if(profile.spawn==null||profile.idle==null||profile.despawn==null||entity!="JadeScreen"&&profile.attack==null||entity=="JadeScreen"&&(profile.absorbHit==null||profile.capacityBreak==null))
                throw new InvalidOperationException("Missing required imported clip: "+entity+" / "+string.Join(",",imported.Select(c=>c.name)));
            profile.spawnSound=Sound(path,entity,"spawn",.32f,entity=="Oz"?640:330);
            profile.attackSound=Sound(path,entity,"attack",.65f,entity=="Oz"?900:110);
            profile.absorbSound=Sound(path,entity,"absorb",.22f,1400);
            profile.breakSound=Sound(path,entity,"break",.55f,1900);
            profile.despawnSound=Sound(path,entity,"despawn",.30f,440);
            EditorUtility.SetDirty(profile);
            const string catalogPath="Assets/Project/Resources/BattleEntityPresentationCatalog.asset";
            var catalog=AssetDatabase.LoadAssetAtPath<BattleEntityPresentationCatalog>(catalogPath);
            if(catalog==null) { catalog=ScriptableObject.CreateInstance<BattleEntityPresentationCatalog>();AssetDatabase.CreateAsset(catalog,catalogPath); }
            catalog.profiles.RemoveAll(p=>p==null||p.definitionId==profile.definitionId);catalog.profiles.Add(profile);EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            var report=new { entity, rig=importer.animationType.ToString(), clips=imported.Select(c=>new {c.name,c.length,c.isLooping}).ToArray(),boundsCenter=bounds.center.ToString(),boundsSize=bounds.size.ToString(), skinnedMeshes=instance.GetComponentsInChildren<SkinnedMeshRenderer>().Length };
            Directory.CreateDirectory("Artifacts/Summons/Visuals");File.WriteAllText("Artifacts/Summons/Visuals/"+entity+"-Import.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
            return Newtonsoft.Json.JsonConvert.SerializeObject(report);
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); }
    }

    private static Material Material(string path,string folder,string slug,bool transparent)
    {
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null) { material=new Material(Shader.Find("FightingAllstar/SummonPaint"));AssetDatabase.CreateAsset(material,path); }
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"Textures/"+slug+"_basecolor.png"));
        material.SetTexture("_MaskMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"Textures/"+slug+"_masks.png"));
        material.SetFloat("_Emission",slug=="oz" ? 1 : .25f);material.SetFloat("_ZWrite",transparent?0:1);
        material.SetFloat("_SrcBlend",transparent?(int)BlendMode.SrcAlpha:(int)BlendMode.One);
        material.SetFloat("_DstBlend",transparent?(int)BlendMode.OneMinusSrcAlpha:(int)BlendMode.Zero);
        material.renderQueue=transparent?3000:2000;material.SetOverrideTag("RenderType",transparent?"Transparent":"Opaque");EditorUtility.SetDirty(material);return material;
    }

    private static AudioClip Sound(string folder,string entity,string cue,float duration,float frequency)
    {
        var dir=folder+"Audio/";Directory.CreateDirectory(dir);var path=dir+entity+"_"+cue+".wav";
        var count=(int)(22050*duration);var random=new System.Random(271);
        using(var file=new BinaryWriter(File.Create(path)))
        {
            file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));file.Write(36+count*2);file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            file.Write(16);file.Write((short)1);file.Write((short)1);file.Write(22050);file.Write(44100);file.Write((short)2);file.Write((short)16);
            file.Write(System.Text.Encoding.ASCII.GetBytes("data"));file.Write(count*2);
            for(var i=0;i<count;i++)
            {
                var t=i/22050.0;var env=Math.Sin(Math.PI*i/count)*Math.Exp(-t*5);
                var tone=Math.Sin(2*Math.PI*frequency*t*(1-.3*t));var noise=random.NextDouble()*2-1;
                var value=entity=="Guoba"&&cue=="attack" ? noise*.65+tone*.15 : tone*.45+Math.Sin(t*frequency*3.17)*.2+noise*.07;
                file.Write((short)(value*env*12000));
            }
        }
        AssetDatabase.ImportAsset(path);return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
}
