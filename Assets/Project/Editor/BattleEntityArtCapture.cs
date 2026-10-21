using System;
using System.IO;
using System.Linq;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BattleEntityArtCapture
{
    public static string Capture(string entity, string clipName = "Idle", float phase = 0, float yaw = 0, string suffix = "front")
    {
        var path="Assets/Project/Art/Summons/"+entity+"/";
        var profile=AssetDatabase.LoadAssetAtPath<BattleEntityPresentationProfile>(path+"Animation/"+entity+"Presentation.asset");
        var scene=EditorSceneManager.NewPreviewScene();
        var previous=RenderTexture.active;RenderTexture target=null;Texture2D image=null;
        try
        {
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(profile.prefab,scene);
            obj.transform.rotation=Quaternion.Euler(0,profile.modelYaw+yaw,0);
            foreach(var t in obj.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
            var clip=AssetDatabase.LoadAllAssetsAtPath(path+"Models/"+entity.ToLowerInvariant()+".fbx").OfType<AnimationClip>().FirstOrDefault(c=>c.name==clipName);
            if(clip!=null)clip.SampleAnimation(obj,clip.length*phase);
            var cameraObject=new GameObject("Summon art verification camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera=cameraObject.AddComponent<Camera>();camera.scene=scene;camera.cullingMask=1<<31;
            camera.fieldOfView=30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.06f,.075f,.12f);
            var center=entity=="JadeScreen"?new Vector3(0,1.35f,0):new Vector3(0,.9f,0);
            camera.transform.position=center+new Vector3(entity=="JadeScreen"?.7f:.55f,.38f,entity=="Guoba"?4.6f:entity=="Oz"?6.7f:8.4f);
            camera.transform.LookAt(center);
            target=new RenderTexture(768,768,24);camera.targetTexture=target;camera.Render();RenderTexture.active=target;
            image=new Texture2D(768,768,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,768,768),0,0);image.Apply();
            var output="Artifacts/Summons/Visuals/"+entity+"-"+suffix+".png";File.WriteAllBytes(output,image.EncodeToPNG());
            if(suffix=="front")
            {
                var portraitPath=path+"Textures/"+entity+"Portrait.png";File.WriteAllBytes(portraitPath,image.EncodeToPNG());AssetDatabase.ImportAsset(portraitPath);
                var importer=(TextureImporter)AssetImporter.GetAtPath(portraitPath);importer.textureType=TextureImporterType.Sprite;
                importer.spriteImportMode=SpriteImportMode.Single;importer.maxTextureSize=256;importer.SaveAndReimport();
                profile.portrait=AssetDatabase.LoadAssetAtPath<Sprite>(portraitPath);EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
            }
            return output;
        }
        finally {RenderTexture.active=previous;if(image!=null)UnityEngine.Object.DestroyImmediate(image);if(target!=null)UnityEngine.Object.DestroyImmediate(target);EditorSceneManager.ClosePreviewScene(scene);}
    }
}
