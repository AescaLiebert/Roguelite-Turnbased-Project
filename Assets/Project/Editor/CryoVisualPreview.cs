using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools
{
    /// <summary>Repeatable offscreen production-model checks; leaves the current scene untouched.</summary>
    public static class CryoVisualPreview
    {
        private static readonly ActorVisualEffectKind[] Kinds = { ActorVisualEffectKind.Freeze, ActorVisualEffectKind.FrostBite, ActorVisualEffectKind.GlacialWaltz };
        private static int _checks;
        private static string Output => Path.GetFullPath(Path.Combine(Application.dataPath,"../Artifacts/CryoStatus"));

        [MenuItem("Tools/Fighting Allstar/Preview Cryo Status Effects")]
        public static void Run()
        {
            Directory.CreateDirectory(Output); _checks=0;
            var shader=Resources.Load<Shader>("ActorCryo");
            Check(shader!=null && !ShaderUtil.ShaderHasError(shader),"Cryo shader compiles");
            using(var fixture=new Fixture())
            {
                const int width=480, height=500;
                var grid=new Texture2D(width*3,height*3,TextureFormat.RGB24,false);
                try
                {
                    for(var column=0;column<Kinds.Length;column++)
                    {
                        var kind=Kinds[column];
                        var recipe=Recipe(kind);
                        Check(ActorVisualEffectRules.PersistentStatus(recipe)==kind,"Production mapping: "+kind);
                        fixture.Effect.ClearAll();
                        fixture.Effect.SynchronizeStatuses(new[] { kind });
                        Check(fixture.Effect.ActiveStatusCount==1 && fixture.Effect.ActiveStatusEnterCount==0,"Snapshot starts idle: "+kind);
                        fixture.Effect.EnterStatus(kind);
                        Check(fixture.Effect.ActiveStatusEnterCount==1,"Accepted entrance: "+kind);
                        var enter=fixture.Capture(.24f,width,height);
                        grid.SetPixels(column*width,height*2,width,height,enter.GetPixels()); UnityEngine.Object.DestroyImmediate(enter);
                        var orbit=kind==ActorVisualEffectKind.GlacialWaltz ? fixture.Actor.transform.Find("AVE GlacialWaltz/Orbit icicle 0") : null;
                        var orbitBefore=orbit!=null ? orbit.localPosition : Vector3.zero;
                        var loop=fixture.Capture(1.8f,width,height);
                        grid.SetPixels(column*width,height,width,height,loop.GetPixels()); UnityEngine.Object.DestroyImmediate(loop);
                        var liveParticles=0; foreach(var ps in fixture.Actor.GetComponentsInChildren<ParticleSystem>()) liveParticles+=ps.particleCount;
                        Check(liveParticles>0,"Sustained Cryo particles: "+kind);
                        if(orbit!=null) Check(Vector3.Distance(orbitBefore,orbit.localPosition)>.5f,"Glacial icicles move around the actor");
                        Check(fixture.Effect.ActiveStatusEnterCount==0,"Entrance settles: "+kind);
                        fixture.Effect.SynchronizeStatuses(new[] { kind });
                        Check(fixture.Effect.ActiveStatusEnterCount==0,"Snapshot refresh does not replay entrance: "+kind);
                        if(kind==ActorVisualEffectKind.Freeze)
                        {
                            var fighter=new FighterState { Id="frozen",Definition=new CharacterDefinition() };
                            fighter.Statuses.Instances.Add(new StatusInstance { RecipeId=recipe.Id,Recipe=recipe });
                            Check(ActorVisualEffectRules.IsUnableToAct(fighter) && fixture.Effect.IsUnableToAct,"Frozen turn uses flinch path");
                            Check(fixture.Animator.speed==0,"Freeze pauses model animator");
                            fixture.Effect.Flinch(); fixture.Effect.SamplePreview(.2f,fixture.Preview.camera);
                            Check(Quaternion.Angle(fixture.Model.localRotation,fixture.RestRotation)>.01f,"Frozen turn visibly flinches");
                        }
                        fixture.Effect.SetHidden(true);
                        Check(fixture.Actor.GetComponentsInChildren<ParticleSystem>().Length==0,"Camera suppression hides Cryo: "+kind);
                        if(kind==ActorVisualEffectKind.Freeze) Check(Mathf.Approximately(fixture.Animator.speed,.8f),"Suppression restores animator speed");
                        fixture.Effect.SetHidden(false);
                        Check(fixture.Effect.ActiveStatusCount==1 && fixture.Effect.ActiveStatusEnterCount==0,"Unhide preserves idle: "+kind);
                        fixture.Effect.SynchronizeStatuses(Array.Empty<ActorVisualEffectKind>());
                        Check(fixture.Effect.ActiveStatusCount==0 && fixture.Effect.ExitingCryoCount==1,"Removal starts exit: "+kind);
                        if(kind==ActorVisualEffectKind.Freeze) Check(Mathf.Approximately(fixture.Animator.speed,.8f),"Thaw restores animator speed");
                        var exit=fixture.Capture(.28f,width,height);
                        grid.SetPixels(column*width,0,width,height,exit.GetPixels()); UnityEngine.Object.DestroyImmediate(exit);
                        fixture.Effect.SamplePreview(.8f,fixture.Preview.camera);
                        Check(fixture.Effect.ExitingCryoCount==0,"Exit finishes: "+kind);
                        fixture.Effect.EnterStatus(kind); fixture.Effect.SynchronizeStatuses(Array.Empty<ActorVisualEffectKind>());
                        fixture.Effect.EnterStatus(kind);
                        Check(fixture.Effect.ActiveStatusCount==1 && fixture.Effect.ExitingCryoCount==0,"Reapply interrupts exit: "+kind);
                        fixture.Effect.ClearAll();
                        Check(fixture.Actor.GetComponentsInChildren<ParticleSystem>().Length==0 && fixture.Effect.ExitingCryoCount==0,"Death/reset clears immediately: "+kind);
                    }
                    grid.Apply(); File.WriteAllBytes(Path.Combine(Output,"cryo-enter-loop-exit.png"),grid.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(grid); }
                fixture.Effect.EnterStatus(ActorVisualEffectKind.FrostBite); fixture.Effect.EnterStatus(ActorVisualEffectKind.GlacialWaltz);
                Check(fixture.Effect.ActiveStatusCount==2 && fixture.Effect.ActiveDebuffCount==1,"Glacial Waltz is a buff; Frost Bite is a debuff");
                fixture.Effect.ClearAll();
                CaptureAnimation(fixture);
            }
            StartOverlayCheck();
        }

        private static StatusRecipeDefinition Recipe(ActorVisualEffectKind kind) => new StatusRecipeDefinition {
            Id=kind==ActorVisualEffectKind.Freeze ? StatusSystem.FreezeRecipeId : kind==ActorVisualEffectKind.FrostBite ? "status.debuff.frost-bite" : "status.buff.glacial-waltz",
            Polarity=kind==ActorVisualEffectKind.GlacialWaltz ? StatusPolarity.Buff : StatusPolarity.Debuff };

        private static void CaptureAnimation(Fixture fixture)
        {
            var frames=Path.Combine(Output,"Frames"); Directory.CreateDirectory(frames);
            for(var frame=0;frame<36;frame++)
            {
                var strip=new Texture2D(960,380,TextureFormat.RGB24,false);
                try
                {
                    for(var column=0;column<Kinds.Length;column++)
                    {
                        fixture.Effect.ClearAll(); fixture.Effect.EnterStatus(Kinds[column]);
                        var time=frame/12f;
                        if(time>=2.3f)
                        {
                            fixture.Effect.SamplePreview(2.3f,fixture.Preview.camera);
                            fixture.Effect.SynchronizeStatuses(Array.Empty<ActorVisualEffectKind>());
                            // Keep the sampled orbit phase continuous across the separately sampled exit clock.
                            var cryo=(System.Collections.IDictionary)typeof(ActorVisualEffect).GetField("_cryo",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(fixture.Effect);
                            var visual=cryo[Kinds[column]];
                            visual.GetType().GetField("_started",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(visual,Time.unscaledTime-2.3f);
                            time-=2.3f;
                        }
                        var tile=fixture.Capture(time,320,380);
                        strip.SetPixels(column*320,0,320,380,tile.GetPixels()); UnityEngine.Object.DestroyImmediate(tile);
                    }
                    strip.Apply(); File.WriteAllBytes(Path.Combine(frames,frame.ToString("D3")+".png"),strip.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(strip); }
            }
        }

        private static void StartOverlayCheck()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var go=new GameObject("Cryo overlay verification") { hideFlags=HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);
            var settings=ScriptableObject.CreateInstance<PanelSettings>();
            settings.themeStyleSheet=AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss");
            settings.scaleMode=PanelScaleMode.ConstantPixelSize;
            var texture=new RenderTexture(480,260,24); texture.Create(); settings.targetTexture=texture;
            var document=go.AddComponent<UIDocument>(); document.panelSettings=settings;
            var root=document.rootVisualElement; root.style.width=480; root.style.height=260; root.style.flexDirection=FlexDirection.Row;
            root.style.paddingTop=35; root.style.paddingLeft=45; root.style.backgroundColor=new Color(.08f,.11f,.18f);
            var template=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Project/UI/BattleCard.uxml");
            var cards=new List<VisualElement>();
            for(var i=0;i<2;i++)
            {
                var card=template.CloneTree(); card.AddToClassList("hand-card"); card.style.marginRight=60; root.Add(card); cards.Add(card);
                card.Q<Image>("artwork").sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Art/Character/Icon/icon-gs-kaeya.png");
                card.EnableInClassList("card-disabled-by-status",i==1);
            }
            var ticks=0;
            EditorApplication.CallbackFunction update=null;
            update=()=> {
                if(++ticks<8) return;
                EditorApplication.update-=update;
                try
                {
                    var panel=root.panel;
                    if(panel==null) throw new InvalidOperationException("No runtime UI panel for overlay verification.");
                    var flags=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
                    panel.GetType().GetMethod("Update",flags,null,Type.EmptyTypes,null)?.Invoke(panel,null);
                    Check(cards[0].Q("card-disabled-overlay").resolvedStyle.display==DisplayStyle.None,"Enabled player card has no overlay");
                    var overlay=cards[1].Q("card-disabled-overlay");
                    Check(overlay.resolvedStyle.display==DisplayStyle.Flex && overlay.resolvedStyle.backgroundColor.a>.5f,"Disabled player card resolves black overlay");
                    Check(overlay.worldBound.width>=76 && overlay.worldBound.height>=140,"Overlay covers full player card frame");
                    panel.GetType().GetMethod("Repaint",flags,null,new[] { typeof(Event) },null)?.Invoke(panel,new object[] { new Event { type=EventType.Repaint } });
                    panel.GetType().GetMethod("Render",flags,null,Type.EmptyTypes,null)?.Invoke(panel,null);
                    var old=RenderTexture.active; RenderTexture.active=texture;
                    var image=new Texture2D(480,260,TextureFormat.RGB24,false);
                    try { image.ReadPixels(new Rect(0,0,480,260),0,0); image.Apply(); File.WriteAllBytes(Path.Combine(Output,"player-card-disabled-overlay.png"),image.EncodeToPNG()); }
                    finally { RenderTexture.active=old; UnityEngine.Object.DestroyImmediate(image); }
                    cards[1].EnableInClassList("card-disabled-by-status",false);
                    panel.GetType().GetMethod("Update",flags,null,Type.EmptyTypes,null)?.Invoke(panel,null);
                    Check(overlay.resolvedStyle.display==DisplayStyle.None,"Thawed player card removes black overlay");
                    File.WriteAllText(Path.Combine(Output,"checks.txt"),"PASS: "+_checks+" Cryo visual and player card overlay checks.\nColumns: Freeze / Frost Bite / Glacial Waltz. Rows: Enter / Loop / Exit.\n");
                    Debug.Log("PASS: "+_checks+" Cryo visual and player card overlay checks. "+Output);
                }
                catch(Exception ex) { File.WriteAllText(Path.Combine(Output,"checks.txt"),"FAIL: "+ex); Debug.LogException(ex); }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(settings);
                    texture.Release(); UnityEngine.Object.DestroyImmediate(texture);
                    UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
                }
            };
            EditorApplication.update+=update;
        }

        private static void Check(bool pass,string message) { _checks++; if(!pass) throw new InvalidOperationException(message); }

        private sealed class Fixture : IDisposable
        {
            public readonly PreviewRenderUtility Preview=new PreviewRenderUtility();
            public readonly GameObject Actor;
            public readonly ActorVisualEffect Effect;
            public readonly Animator Animator;
            public readonly Transform Model;
            public readonly Quaternion RestRotation;
            public Fixture()
            {
                Preview.camera.clearFlags=CameraClearFlags.SolidColor; Preview.camera.backgroundColor=new Color(.055f,.08f,.13f);
                Preview.camera.transform.position=new Vector3(0,2.8f,-9.6f); Preview.camera.transform.LookAt(new Vector3(0,1.5f,0));
                Preview.camera.fieldOfView=38; Preview.camera.nearClipPlane=.05f; Preview.camera.farClipPlane=100;
                Preview.lights[0].intensity=1.2f; Preview.lights[0].transform.rotation=Quaternion.Euler(35,25,0);
                Preview.ambientColor=new Color(.55f,.6f,.7f);
                var character=AssetDatabase.LoadAssetAtPath<CharacterObject>("Assets/Project/Data/Character/GenshinImpact/kaeya.asset");
                Actor=BattleFighterView.Create(character.Fighter3DPrefab,Vector3.zero,Quaternion.identity,0,character.Fighter3DMesh,character.Fighter3DMaterial);
                Model=Actor.transform.Find("Model");
                // This shared placeholder uses an inverted portrait UV layout. Match the existing AVE preview orientation.
                Model.localRotation=Quaternion.Euler(180,180,0); RestRotation=Model.localRotation;
                var bounds=Actor.GetComponentInChildren<Renderer>().bounds; Actor.transform.position-=Vector3.up*bounds.min.y;
                Animator=Actor.GetComponentInChildren<Animator>(); if(Animator==null) Animator=Model.gameObject.AddComponent<Animator>();
                Animator.speed=.8f;
                Preview.AddSingleGO(Actor);
                Effect=Actor.AddComponent<ActorVisualEffect>(); Effect.Synchronize(false);
            }
            public Texture2D Capture(float age,int width,int height)
            {
                Effect.SamplePreview(age,Preview.camera);
                Preview.BeginStaticPreview(new Rect(0,0,width,height)); Preview.Render(); return Preview.EndStaticPreview();
            }
            public void Dispose() { Effect.ClearAll(); Preview.Cleanup(); }
        }
    }
}
