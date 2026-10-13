#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEngine;

public static class SpecialStatusVisualChecks
{
    private static readonly ActorVisualEffectKind[] Kinds = { ActorVisualEffectKind.CardDisable,ActorVisualEffectKind.ExtortBuff,
        ActorVisualEffectKind.ExtortDebuff,ActorVisualEffectKind.Explosion,ActorVisualEffectKind.Rejuvenation };

    public static void Run()
    {
        var evidenceDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Evidence"));
        Directory.CreateDirectory(evidenceDir);
        var checks=0;
        Action<bool,string> check=(pass,message)=> { checks++; if(!pass) throw new InvalidOperationException(message); };
        using(var fixture=new Fixture())
        {
            var grid=new Texture2D(1800,900,TextureFormat.RGB24,false);
            for(var i=0;i<Kinds.Length;i++)
            {
                var kind=Kinds[i]; var status=Status(kind);
                check(ActorVisualEffectRules.PersistentStatus(status.Recipe)==kind,"Production visual mapping: "+kind);
                var fighter=new FighterState { Id="fixture",Definition=new CharacterDefinition() };
                fighter.Statuses.Instances.Add(status); fixture.Effect.ClearAll(); fixture.Stage.SyncActorVisualEffects(fighter);
                check(fixture.Effect.ActiveStatusCount==1 && fixture.Effect.ActiveStatusEnterCount==0,"Snapshot idle: "+kind);
                var item=new BattleEvent { Kind=BattleEventKind.StatusApplied,TargetId="fixture",StatusRecipeId=status.RecipeId,
                    StatusInstanceId=status.InstanceId,StatusOutcome=StatusApplyOutcome.Rejected,StatusesAfter=fighter.Statuses.Instances };
                check(fixture.Stage.ActorStatusFeedback(item)==0 && fixture.Effect.ActiveStatusEnterCount==0,"Rejected grant: "+kind);
                item.StatusOutcome=StatusApplyOutcome.Added;
                check(fixture.Stage.ActorStatusFeedback(item)>0 && fixture.Effect.ActiveStatusEnterCount==1,"Accepted grant: "+kind);
                fixture.Effect.SamplePreview(.25f,fixture.Preview.camera);
                check(fixture.Actor.transform.Find("AVE "+kind+" Particles/Enter").GetComponent<ParticleSystem>().particleCount>0,"Rendered entrance: "+kind);
                for(var row=0;row<2;row++)
                {
                    var tile=fixture.Capture(kind,row==0 ? .25f : 2.2f,360,450);
                    grid.SetPixels(i*360,(1-row)*450,360,450,tile.GetPixels()); UnityEngine.Object.DestroyImmediate(tile);
                }
                check(fixture.Effect.ActiveStatusCount==1 && fixture.Effect.ActiveStatusEnterCount==0,"Idle survives entrance: "+kind);
                var particles=0;
                foreach(var ps in fixture.Actor.GetComponentsInChildren<ParticleSystem>()) particles+=ps.particleCount;
                check(particles>0,"Idle particles: "+kind);
                fixture.Stage.SetPersistentEffectsHidden("fixture",true);
                check(fixture.Actor.GetComponentsInChildren<ParticleSystem>().Length==0,"Hide special status: "+kind);
                fixture.Stage.SetPersistentEffectsHidden("fixture",false);
                check(fixture.Effect.ActiveStatusCount==1 && fixture.Effect.ActiveStatusEnterCount==0,"Unhide without entrance: "+kind);
                fighter.Statuses.Instances.Clear(); fixture.Stage.SyncActorVisualEffects(fighter);
                check(fixture.Effect.ActiveStatusCount==0,"Removal: "+kind);
            }
            grid.Apply(); File.WriteAllBytes(Path.Combine(evidenceDir, "special-status-enter-idle.png"),grid.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(grid);
            foreach(var polarity in new[] { StatusPolarity.Buff,StatusPolarity.Debuff })
                check(ActorVisualEffectRules.PersistentStatus(new StatusRecipeDefinition { Id="extort",Polarity=polarity })==
                    (polarity==StatusPolarity.Buff ? ActorVisualEffectKind.ExtortBuff : ActorVisualEffectKind.ExtortDebuff),"Short Extort ID must respect polarity.");
            var partials=new FighterState { Id="fixture",Definition=new CharacterDefinition() };
            partials.Statuses.Instances.Add(Status(ActorVisualEffectKind.CardDisable));
            partials.Statuses.Instances.Add(new StatusInstance { RecipeId="fixture.disable-heal",Recipe=new StatusRecipeDefinition { Id="fixture.disable-heal",
                Behavior=StatusBehavior.Disable,DisableMask=CardCategoryMask.Recovery } });
            fixture.Stage.SyncActorVisualEffects(partials);
            check(fixture.Effect.ActiveStatusCount==1,"Mixed card restrictions must coalesce.");
            partials.Statuses.Instances.RemoveAt(0); fixture.Stage.SyncActorVisualEffects(partials);
            check(fixture.Effect.ActiveStatusCount==1,"Partial card cleanse must retain the other restriction.");
            partials.Statuses.Instances.Clear(); fixture.Stage.SyncActorVisualEffects(partials);
            check(fixture.Effect.ActiveStatusCount==0,"Last card restriction must clear the symbol.");
            check(ActorVisualEffectRules.PersistentStatus(new StatusRecipeDefinition { Id="status.debuff.explosion",Polarity=StatusPolarity.Buff })==ActorVisualEffectKind.Explosion,
                "Existing Andy Explosion ID must resolve despite its authored polarity.");
            foreach(var kind in new[] { ActorVisualEffectKind.ExtortBuff,ActorVisualEffectKind.ExtortDebuff })
            {
                var extort=Status(kind); extort.Recipe.Modifiers.Add(new StatModifierDefinition { Stat=StatId.Attack,Operation=ModifierOperation.PercentOfBase,
                    Amount=kind==ActorVisualEffectKind.ExtortBuff ? 2000 : -2000 });
                check(ActorVisualEffectRules.GrantedEffects(extort).Count==0,"Dedicated Extort aura should communicate the grant without generic icons.");
            }
            var custom=new FighterState { Id="fixture",Definition=new CharacterDefinition() };
            custom.Statuses.Instances.Add(new StatusInstance { RecipeId="fixture.custom-bomb",Recipe=new StatusRecipeDefinition { Id="fixture.custom-bomb",
                Tags=new List<string> { "status.explosion" } } });
            fixture.Stage.SyncActorVisualEffects(custom);
            fixture.Stage.ActorStatusDamageFeedback(new BattleEvent { Kind=BattleEventKind.DamageApplied,TargetId="fixture",StatusRecipeId="fixture.custom-bomb" });
            check(fixture.Effect.IsExploding,"Tagged Explosion recipes must resolve a damage event without a status snapshot.");
            fixture.Effect.ClearAll();
            fixture.Stage.ActorStatusDamageFeedback(new BattleEvent { Kind=BattleEventKind.StatusRemoved,TargetId="fixture",StatusRecipeId="status.debuff.explosion" });
            check(!fixture.Effect.IsExploding,"Cleanse/expiry alone must not explode.");
            fixture.Stage.ActorStatusDamageFeedback(new BattleEvent { Kind=BattleEventKind.DamageApplied,TargetId="fixture",StatusRecipeId="status.debuff.explosion" });
            check(fixture.Effect.IsExploding,"Confirmed Explosion damage must impact.");
            var tileBlast=fixture.Capture(ActorVisualEffectKind.ExplosionDetonation,.2f,640,720);
            File.WriteAllBytes(Path.Combine(evidenceDir, "explosion-detonation.png"),tileBlast.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tileBlast);
            fixture.Effect.SamplePreview(1.2f,fixture.Preview.camera); check(!fixture.Effect.IsExploding,"Detonation self-expiry.");
            var reverseGrid=new Texture2D(1800,450,TextureFormat.RGB24,false);
            fixture.ReverseCamera();
            for(var i=0;i<Kinds.Length;i++)
            {
                var tile=fixture.Capture(Kinds[i],2.2f,360,450);
                reverseGrid.SetPixels(i*360,0,360,450,tile.GetPixels()); UnityEngine.Object.DestroyImmediate(tile);
                var emitter=fixture.Actor.transform.Find("AVE "+Kinds[i]+" Particles/Idle");
                check(emitter.position.z>fixture.Actor.transform.position.z,"Emitter must follow the opposite camera: "+Kinds[i]);
            }
            reverseGrid.Apply(); File.WriteAllBytes(Path.Combine(evidenceDir, "special-status-opposite-camera.png"),reverseGrid.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(reverseGrid);
            fixture.ResetCamera();
            fixture.Effect.ClearAll(); fixture.Effect.SynchronizeStatuses(Kinds);
            foreach(var kind in Kinds) fixture.Effect.EnterStatus(kind);
            fixture.Effect.SamplePreview(2.2f,fixture.Preview.camera);
            check(fixture.Effect.ActiveStatusCount==5,"All special effects must coexist.");
            fixture.Preview.BeginStaticPreview(new Rect(0,0,640,720)); fixture.Preview.Render();
            var all=fixture.Preview.EndStaticPreview(); File.WriteAllBytes(Path.Combine(evidenceDir, "special-status-overlap.png"),all.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(all);
            fixture.Stage.Restore(); check(fixture.Effect.ActiveStatusCount==5,"Skip retains idle effects.");
            var shader=Resources.Load<Shader>("ActorStatusParticles");
            check(shader!=null && !ShaderUtil.ShaderHasError(shader),"Particle shader must compile.");

            // Ultimate Ready Aura verification and capture
            fixture.Effect.ClearAll();
            var auraShader = Resources.Load<Shader>("UltimateReadyAura");
            check(auraShader != null && !ShaderUtil.ShaderHasError(auraShader), "Ultimate Ready Aura shader must compile.");
            fixture.Stage.SetUltimateReady("fixture", true);
            var readyAura = fixture.Actor.GetComponentInChildren<UltimateReadyAura>();
            check(readyAura != null, "Ultimate Ready Aura component must exist on actor.");
            readyAura.Evaluate(0.45f);
            fixture.Preview.BeginStaticPreview(new Rect(0,0,640,720)); fixture.Preview.Render();
            var ultCapture = fixture.Preview.EndStaticPreview();
            File.WriteAllBytes(Path.Combine(evidenceDir, "ultimate-ready-aura.png"), ultCapture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(ultCapture);
            fixture.Stage.ClearAuras();
            check(fixture.Actor.GetComponentInChildren<UltimateReadyAura>() == null, "ClearAuras must destroy UltimateReadyAura.");
        }
        File.WriteAllText(Path.Combine(evidenceDir, "special-checks.txt"),"PASS: "+checks+" special status editor checks\n"+
            "Columns: Disable Card / Extort buff / Extort debuff / Explosion armed / Rejuvenation. Entrance above; idle below.\n"+
            "Ultimate Ready Aura captured.\n");
        Debug.Log("PASS: "+checks+" special status editor checks");
    }

    private static StatusInstance Status(ActorVisualEffectKind kind)
    {
        var buff=kind==ActorVisualEffectKind.ExtortBuff || kind==ActorVisualEffectKind.Rejuvenation;
        var name=kind==ActorVisualEffectKind.CardDisable ? "disable-attack" : kind==ActorVisualEffectKind.ExtortBuff || kind==ActorVisualEffectKind.ExtortDebuff ? "extort" : kind.ToString().ToLowerInvariant();
        var id="status."+(buff ? "buff." : "debuff.")+name;
        return new StatusInstance { InstanceId=id,RecipeId=id,Recipe=new StatusRecipeDefinition { Id=id,Polarity=buff ? StatusPolarity.Buff : StatusPolarity.Debuff,
            Behavior=kind==ActorVisualEffectKind.CardDisable ? StatusBehavior.Disable : StatusBehavior.None,
            DisableMask=kind==ActorVisualEffectKind.CardDisable ? CardCategoryMask.Attack : CardCategoryMask.None } };
    }

    private sealed class Fixture : IDisposable
    {
        public readonly PreviewRenderUtility Preview=new PreviewRenderUtility();
        public readonly GameObject Actor;
        public readonly ActorVisualEffect Effect;
        public readonly BattleStagePresenter Stage;
        private readonly Material _material;
        private readonly Transform _modelTransform;

        public Fixture()
        {
            Preview.camera.clearFlags=CameraClearFlags.SolidColor; Preview.camera.backgroundColor=new Color(.055f,.07f,.12f);
            Preview.camera.transform.position=new Vector3(0,2.3f,-7); Preview.camera.transform.LookAt(new Vector3(0,1.45f,0));
            Preview.camera.fieldOfView=32; Preview.camera.nearClipPlane=.05f; Preview.camera.farClipPlane=100;
            Actor=new GameObject("Special AVE actor"); var model=GameObject.CreatePrimitive(PrimitiveType.Cube); model.name="Model";
            model.transform.SetParent(Actor.transform,false); model.transform.localPosition=Vector3.up*1.35f;
            model.transform.localRotation=Quaternion.Euler(180,180,0);
            model.transform.localScale=new Vector3(1.4f,2.7f,.4f);
            _modelTransform=model.transform;
            _material=new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Texture")) { hideFlags=HideFlags.DontSave };
            _material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Production/Brian.png");
            model.GetComponent<Renderer>().sharedMaterial=_material;
            UnityEngine.Object.DestroyImmediate(model.GetComponent<Collider>());
            Preview.AddSingleGO(Actor);
            var stage=new GameObject("Special AVE stage"); Preview.AddSingleGO(stage); Stage=stage.AddComponent<BattleStagePresenter>();
            Stage.Initialize(new Dictionary<string,GameObject> { ["fixture"]=Actor });
            foreach(var field in new[] { "_camera","_planningPosition","_planningRotation","_homeFov" })
            {
                object value=field=="_camera" ? (object)Preview.camera : field=="_planningPosition" ? (object)Preview.camera.transform.position :
                    field=="_planningRotation" ? (object)Preview.camera.transform.rotation : Preview.camera.fieldOfView;
                typeof(BattleStagePresenter).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(Stage,value);
            }
            Effect=Actor.AddComponent<ActorVisualEffect>(); Effect.Synchronize(false);
        }

        public Texture2D Capture(ActorVisualEffectKind kind,float age,int width,int height)
        {
            Effect.ClearAll();
            if(kind==ActorVisualEffectKind.ExplosionDetonation) Effect.Explode();
            else { Effect.SynchronizeStatuses(new[] { kind }); Effect.EnterStatus(kind); }
            Effect.SamplePreview(age,Preview.camera);
            Preview.BeginStaticPreview(new Rect(0,0,width,height)); Preview.Render(); return Preview.EndStaticPreview();
        }

        public void ReverseCamera()
        {
            Preview.camera.transform.position=new Vector3(0,2.3f,7);
            Preview.camera.transform.LookAt(new Vector3(0,1.45f,0));
            _modelTransform.localRotation=Quaternion.Euler(180,0,0);
        }

        public void ResetCamera()
        {
            Preview.camera.transform.position=new Vector3(0,2.3f,-7);
            Preview.camera.transform.LookAt(new Vector3(0,1.45f,0));
            _modelTransform.localRotation=Quaternion.Euler(180,180,0);
        }

        public void Dispose() { Preview.Cleanup(); UnityEngine.Object.DestroyImmediate(_material); }
    }
}
#endif
