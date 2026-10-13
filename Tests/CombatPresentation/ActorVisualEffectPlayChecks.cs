#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ActorVisualEffectPlayChecks
{
    private const string Running = "AVE.PlayChecks.Running";
    private static int _step, _checks, _errors, _editorIndexErrors;
    private static double _at, _deadline;
    private static GameObject _actor;
    private static Transform _model;
    private static ActorVisualEffect _effect;
    private static BattleStagePresenter _stage;
    private static FighterState _fighter;
    private static Vector3 _position;
    private static Quaternion _rotation;
    private static readonly ActorVisualEffectKind[] Debuffs = { ActorVisualEffectKind.Paralyze,ActorVisualEffectKind.Bleed,
        ActorVisualEffectKind.Poison,ActorVisualEffectKind.Shock,ActorVisualEffectKind.Ignite };
    private static readonly ActorVisualEffectKind[] Specials = { ActorVisualEffectKind.CardDisable,ActorVisualEffectKind.ExtortBuff,
        ActorVisualEffectKind.ExtortDebuff,ActorVisualEffectKind.Explosion,ActorVisualEffectKind.Rejuvenation };

    private static StatusInstance Special(ActorVisualEffectKind kind)
    {
        var buff=kind==ActorVisualEffectKind.ExtortBuff || kind==ActorVisualEffectKind.Rejuvenation;
        var name=kind==ActorVisualEffectKind.CardDisable ? "disable-attack" : kind==ActorVisualEffectKind.ExtortBuff || kind==ActorVisualEffectKind.ExtortDebuff ? "extort" : kind.ToString().ToLowerInvariant();
        var id="status."+(buff ? "buff." : "debuff.")+name;
        return new StatusInstance { InstanceId=id,RecipeId=id,Recipe=new StatusRecipeDefinition { Id=id,
            Polarity=buff ? StatusPolarity.Buff : StatusPolarity.Debuff,Behavior=kind==ActorVisualEffectKind.CardDisable ? StatusBehavior.Disable : StatusBehavior.None,
            DisableMask=kind==ActorVisualEffectKind.CardDisable ? CardCategoryMask.Attack : CardCategoryMask.None } };
    }

    private static StatusInstance Debuff(ActorVisualEffectKind kind,string source="one")
    {
        var id="status.debuff."+kind.ToString().ToLowerInvariant();
        return new StatusInstance { InstanceId=id+source,RecipeId=id,Recipe=new StatusRecipeDefinition { Id=id,
            Behavior=kind==ActorVisualEffectKind.Paralyze ? StatusBehavior.Disable : StatusBehavior.DamageOverTime,
            Polarity=StatusPolarity.Debuff,DisableMask=CardCategoryMask.AllCards } };
    }

    private static void Grant(StatusInstance status,StatusApplyOutcome outcome=StatusApplyOutcome.Added)
    {
        _stage.ActorStatusFeedback(new BattleEvent { Kind=BattleEventKind.StatusApplied,TargetId="fighter",
            StatusInstanceId=status.InstanceId,StatusRecipeId=status.RecipeId,StatusOutcome=outcome,
            StatusesAfter=_fighter.Statuses.Instances });
    }

    static ActorVisualEffectPlayChecks()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (type != LogType.Error && type != LogType.Exception) return;
            // Unity 6000.3's SearchDatabase startup can throw in an empty batch project.
            // Record that editor infrastructure error separately; every AVE/runtime error fails.
            if (stack.Contains("UnityEditor.Search.SearchDatabase") && !stack.Contains("FightingAllstar"))
            { _editorIndexErrors++; return; }
            _errors++;
        };
    }

    public static void Run()
    {
        try { SpecialStatusVisualChecks.Run(); }
        catch(Exception error)
        { var ev = Path.GetFullPath(Path.Combine(Application.dataPath, "../Evidence")); Directory.CreateDirectory(ev); File.WriteAllText(Path.Combine(ev, "play-checks.txt"),"FAIL: "+error); Debug.LogException(error); EditorApplication.Exit(1); return; }
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),"Assets/AVEProbe.unity");
        SessionState.SetBool(Running,true);
        EditorApplication.EnterPlaymode();
    }

    private static void Check(bool pass,string message) { _checks++; if (!pass) throw new InvalidOperationException(message); }
    private static void Next(int step,double delay) { _step = step; _at = EditorApplication.timeSinceStartup+delay; }

    private static void Tick()
    {
        if (!SessionState.GetBool(Running,false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        if (_deadline == 0) _deadline = EditorApplication.timeSinceStartup+30;
        if (EditorApplication.timeSinceStartup < _at) return;
        try
        {
            if (EditorApplication.timeSinceStartup > _deadline) throw new InvalidOperationException("AVE play checks timed out.");
            if (_step == 0)
            {
                _actor = new GameObject("AVE Runtime Actor");
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name="Model";
                body.transform.SetParent(_actor.transform,false); body.transform.localPosition=Vector3.up;
                _model = body.transform; _position = _model.localPosition; _rotation = _model.localRotation;
                _stage = new GameObject("AVE Runtime Stage").AddComponent<BattleStagePresenter>();
                _stage.Initialize(new Dictionary<string,GameObject> { ["fighter"] = _actor });
                _fighter = new FighterState { Id="fighter", Definition=new CharacterDefinition() };
                _fighter.Statuses.Instances.Add(new StatusInstance { Recipe = new StatusRecipeDefinition {
                    Id="status.debuff.stun", Behavior=StatusBehavior.Disable, DisableMask=CardCategoryMask.AllCards,
                    Tags=new List<string> { CombatTags.Stun } } });
                _stage.SyncActorVisualEffects(_fighter); _effect = _actor.GetComponent<ActorVisualEffect>();
                var state = new BattleState(); state.Player.Fighters.Add(_fighter);
                _stage.StartCoroutine(_stage.PresentStunnedTurn(state,TeamSide.Player));
                Next(1,.2);
            }
            else if (_step == 1)
            {
                Check(_effect.IsStunned,"Active stun must persist in Play Mode.");
                Check(Quaternion.Angle(_rotation,_model.localRotation)>5,"Entering the stunned turn must visibly flinch.");
                Check(_actor.transform.position==Vector3.zero,"Flinch must preserve formation.");
                var stars=_actor.transform.Find("AVE Stun Stars");
                Check(stars.gameObject.activeInHierarchy && stars.childCount==3,"Three stars must orbit the head.");
                _fighter.Statuses.Instances.Clear(); _stage.SyncActorVisualEffects(_fighter);
                Check(!_effect.IsStunned && _model.localPosition==_position && Quaternion.Angle(_rotation,_model.localRotation)<.01f,
                    "Cleanse must restore the exact resting model pose.");
                foreach (var kind in new[] { ActorVisualEffectKind.AttackIncrease,ActorVisualEffectKind.AttackDecrease,
                    ActorVisualEffectKind.DefenseIncrease,ActorVisualEffectKind.DefenseDecrease,ActorVisualEffectKind.HealthIncrease,ActorVisualEffectKind.HealthDecrease }) _effect.Play(kind);
                Check(_effect.ActiveGrantCount==6,"All six effects must be callable."); Next(2,1.15);
            }
            else if (_step == 2)
            {
                Check(_effect.ActiveGrantCount==0,"All grants must self-expire in about one second.");
                _effect.Play(ActorVisualEffectKind.AttackIncrease); _effect.Play(ActorVisualEffectKind.AttackIncrease);
                Check(_effect.ActiveGrantCount==1,"Repeat grants must reuse the same visual.");
                _stage.Restore(); Check(_effect.ActiveGrantCount==0,"Animation skip must clear transient AVE.");
                _effect.Synchronize(true); _actor.SetActive(false);
                Check(!_effect.IsStunned && _effect.ActiveGrantCount==0,"Unity OnDisable must clear all visuals.");
                Check(_model.localPosition==_position && Quaternion.Angle(_rotation,_model.localRotation)<.01f,"Disable must restore pose.");
                _actor.SetActive(true); _fighter.Shield=250; _stage.SyncActorVisualEffects(_fighter);
                Check(_actor.transform.Find("Active Shield Aura").gameObject.activeInHierarchy,"Generic shield state must reuse Brian's barrier.");
                _stage.SetPersistentEffectsHidden("fighter",true);
                Check(!_actor.transform.Find("Active Shield Aura").gameObject.activeSelf,"Cinematic suppression must hide barrier.");
                _stage.SetPersistentEffectsHidden("fighter",false);
                Check(_actor.transform.Find("Active Shield Aura").gameObject.activeSelf,"Barrier must return after cinematic suppression.");
                _fighter.IsAlive=false; _stage.SyncActorVisualEffects(_fighter); Next(3,.1);
            }
            else if (_step == 3)
            {
                Check(_actor.transform.Find("Active Shield Aura")==null,"Death must remove persistent barrier.");
                _fighter.IsAlive=true; _fighter.Shield=0;
                foreach(var kind in Debuffs) _fighter.Statuses.Instances.Add(Debuff(kind));
                _stage.SyncActorVisualEffects(_fighter);
                Check(_effect.ActiveDebuffCount==5 && _effect.ActiveDebuffEnterCount==0,"Snapshot must start five idle loops without entrances.");
                foreach(var status in _fighter.Statuses.Instances) Grant(status);
                Check(_effect.ActiveDebuffEnterCount==5,"All accepted debuffs must play entrances.");
                var state=new BattleState(); state.Player.Fighters.Add(_fighter);
                _stage.StartCoroutine(_stage.PresentDisabledTurn(state,TeamSide.Player));
                Next(4,.2);
            }
            else if (_step == 4)
            {
                Check(_effect.IsParalyzed && Quaternion.Angle(_rotation,_model.localRotation)>5,"Paralyze turn must visibly flinch.");
                Check(_actor.transform.position==Vector3.zero,"Paralyze must preserve actor formation.");
                foreach(var kind in Debuffs)
                {
                    var enter=_actor.transform.Find("AVE "+kind+" Particles/Enter").GetComponent<ParticleSystem>();
                    Check(enter.particleCount>0,"Entrance must actually emit: "+kind);
                }
                Next(5,1.3);
            }
            else if (_step == 5)
            {
                Check(_effect.ActiveDebuffCount==5 && _effect.ActiveDebuffEnterCount==0,"Idle loops must outlive entrances.");
                foreach(var kind in Debuffs)
                {
                    var root=_actor.transform.Find("AVE "+kind+" Particles");
                    Check(root.Find("Idle").GetComponent<ParticleSystem>().particleCount>0,"Idle must emit: "+kind);
                    Check(root.Find("Enter").GetComponent<ParticleSystem>().particleCount==0,"Entrance particles must expire: "+kind);
                }
                var roots=_actor.transform.childCount;
                var refreshed=_fighter.Statuses.Instances[1]; Grant(refreshed,StatusApplyOutcome.Refreshed);
                Check(_effect.ActiveDebuffEnterCount==1 && _actor.transform.childCount==roots,"Refresh must replay an entrance while reusing the root.");
                _stage.Restore();
                Check(_effect.ActiveDebuffEnterCount==0 && _effect.ActiveDebuffCount==5,"Skip must clear entrances and retain idle loops.");
                Grant(refreshed,StatusApplyOutcome.Rejected);
                Check(_effect.ActiveDebuffEnterCount==0,"Rejected status must not enter.");
                _stage.SetPersistentEffectsHidden("fighter",true);
                Check(_actor.GetComponentsInChildren<ParticleSystem>().Length==0,"Cinematic suppression must hide all emitters.");
                Check(_model.localPosition==_position && Quaternion.Angle(_rotation,_model.localRotation)<.01f,"Cinematic suppression must reset tension pose.");
                _stage.SetPersistentEffectsHidden("fighter",false);
                Check(_effect.ActiveDebuffCount==5 && _effect.ActiveDebuffEnterCount==0,"Unhide must retain idle without replay.");
                _fighter.Statuses.Instances.Add(Debuff(ActorVisualEffectKind.Bleed,"two"));
                _stage.SyncActorVisualEffects(_fighter);
                _fighter.Statuses.Instances.Remove(refreshed); _stage.SyncActorVisualEffects(_fighter);
                Check(_effect.ActiveDebuffCount==5,"Removing one bleed source must retain its idle.");
                _fighter.Statuses.Instances.RemoveAll(s=>s.RecipeId=="status.debuff.bleed"); _stage.SyncActorVisualEffects(_fighter);
                Check(_effect.ActiveDebuffCount==4 && !_actor.transform.Find("AVE Bleed Particles").gameObject.activeSelf,"Final bleed removal must clear only bleed.");
                _actor.SetActive(false);
                Check(_effect.ActiveDebuffCount==0 && !_effect.IsParalyzed,"Disable must clear every persistent status.");
                Check(_model.localPosition==_position && Quaternion.Angle(_rotation,_model.localRotation)<.01f,"Disable must restore paralyzed pose.");
                _actor.SetActive(true); _stage.SyncActorVisualEffects(_fighter);
                Check(_effect.ActiveDebuffCount==4 && _effect.ActiveDebuffEnterCount==0,"Re-enable state reconciliation must restore idle only.");
                _fighter.IsReserve=true; _stage.SyncActorVisualEffects(_fighter);
                Check(_effect.ActiveDebuffCount==0,"Reserve fighters must not show status loops.");
                _fighter.IsReserve=false; _stage.SyncActorVisualEffects(_fighter); _fighter.IsAlive=false; _stage.SyncActorVisualEffects(_fighter);
                Check(_effect.ActiveDebuffCount==0,"Death must clear DOT loops.");
                Next(6,.1);
            }
            else if (_step==6)
            {
                _fighter.IsAlive=true; _fighter.Statuses.Instances.Clear();
                foreach(var kind in Specials)
                {
                    var status=Special(kind); _fighter.Statuses.Instances.Add(status);
                    Check(ActorVisualEffectRules.PersistentStatus(status.Recipe)==kind,"Special status rules: "+kind);
                }
                foreach(var mask in new[] { CardCategoryMask.Attack,CardCategoryMask.Buff,CardCategoryMask.Debuff,CardCategoryMask.Recovery,
                    CardCategoryMask.Stance,CardCategoryMask.Ultimate,CardCategoryMask.RankTwoOrThree,CardCategoryMask.CardEffects,CardCategoryMask.AllCards })
                    Check(ActorVisualEffectRules.PersistentStatus(new StatusRecipeDefinition { Id="fixture.disable",Behavior=StatusBehavior.Disable,DisableMask=mask })==ActorVisualEffectKind.CardDisable,
                        "Every card restriction must share the generic symbol: "+mask);
                Check(ActorVisualEffectRules.PersistentStatus(new StatusRecipeDefinition { Id="status.debuff.disable-recovery",Behavior=StatusBehavior.PreventsRecovery })==ActorVisualEffectKind.None,
                    "Recovery prevention alone must not disable a card.");
                _stage.SyncActorVisualEffects(_fighter);
                Check(_effect.ActiveStatusCount==5 && !_effect.IsUnableToAct,"Card restrictions must not masquerade as stun/paralyze.");
                Check(_effect.ActiveStatusEnterCount==0,"Special snapshot reconciliation must start idle only.");
                foreach(var status in _fighter.Statuses.Instances) Grant(status);
                Check(_effect.ActiveStatusEnterCount==5,"Special grants must enter."); Next(7,.2);
            }
            else if (_step==7)
            {
                foreach(var kind in Specials)
                    Check(_actor.transform.Find("AVE "+kind+" Particles/Enter").GetComponent<ParticleSystem>().particleCount>0,"Special entrance must emit: "+kind);
                var up=_actor.transform.Find("AVE ExtortBuff Particles/Idle").GetComponent<ParticleSystem>().velocityOverLifetime.y;
                var down=_actor.transform.Find("AVE ExtortDebuff Particles/Idle").GetComponent<ParticleSystem>().velocityOverLifetime.y;
                Check(up.constantMin>0 && down.constantMax<0,"Extort auras must visibly move in opposite directions.");
                Next(8,1.6);
            }
            else if (_step==8)
            {
                Check(_effect.ActiveStatusCount==5 && _effect.ActiveStatusEnterCount==0,"Special idle must persist beyond grant duration.");
                foreach(var kind in Specials)
                    Check(_actor.transform.Find("AVE "+kind+" Particles/Idle").GetComponent<ParticleSystem>().particleCount>0,"Special idle must emit: "+kind);
                _stage.SetPersistentEffectsHidden("fighter",true);
                Check(_actor.GetComponentsInChildren<ParticleSystem>().Length==0,"All special idle effects must hide in cinematics.");
                _stage.SetPersistentEffectsHidden("fighter",false);
                Check(_effect.ActiveStatusCount==5 && _effect.ActiveStatusEnterCount==0,"Special idle must restore without another grant.");
                _fighter.Statuses.Instances.Clear(); _stage.SyncActorVisualEffects(_fighter);
                Check(_effect.ActiveStatusCount==0 && !_effect.IsExploding,"Cleanse must not detonate Explosion.");
                _stage.ActorStatusDamageFeedback(new BattleEvent { Kind=BattleEventKind.DamageApplied,TargetId="fighter",StatusRecipeId="status.debuff.bleed" });
                Check(!_effect.IsExploding,"Other DOT damage must not detonate Explosion.");
                _stage.ActorStatusDamageFeedback(new BattleEvent { Kind=BattleEventKind.DamageApplied,TargetId="fighter",StatusRecipeId="status.debuff.explosion" });
                Check(_effect.IsExploding,"Explosion status damage must launch an independent impact.");
                _fighter.IsAlive=false; _stage.SyncActorVisualEffects(_fighter);
                Check(_effect.IsExploding && _effect.ActiveStatusCount==0,"Lethal state sync must retain impact while clearing idle.");
                Next(9,.2);
            }
            else if (_step==9)
            {
                var burst=_actor.transform.Find("AVE ExplosionDetonation Particles/Enter").GetComponent<ParticleSystem>();
                Check(burst.particleCount>0,"Explosion must emit actual impact shards.");
                Next(10,1);
            }
            else if (_step==10)
            {
                Check(!_effect.IsExploding,"Detonation must expire independently of the status clock.");
                _effect.Explode(); _stage.Restore(); Check(!_effect.IsExploding,"Skip must clear detonation.");
                _fighter.IsAlive=true; _fighter.Statuses.Instances.Add(Special(ActorVisualEffectKind.ExtortBuff));
                _fighter.Statuses.Instances.Add(Special(ActorVisualEffectKind.Rejuvenation)); _stage.SyncActorVisualEffects(_fighter);
                _actor.SetActive(false); Check(_effect.ActiveStatusCount==0,"Disable must clear new buffs as well as debuffs.");
                _actor.SetActive(true); _stage.SyncActorVisualEffects(_fighter);
                _effect.Explode(); _stage.BeginDefeat("fighter"); Next(11,.2);
            }
            else if (_step==11)
            {
                Check(_actor.activeInHierarchy && _effect.IsExploding,"Explosion impact must remain visible while the defeated actor falls.");
                Next(12,.6);
            }
            else if (_step==12)
            {
                Check(!_actor.activeInHierarchy && !_effect.IsExploding,"Completed defeat must clear the impact and actor.");
                _actor.SetActive(true); _stage.SyncActorVisualEffects(_fighter);
                UnityEngine.Object.Destroy(_actor); Next(13,.1);
            }
            else
            {
                Check(_actor==null,"Actor destruction must release the status hierarchy.");
                var ev = Path.GetFullPath(Path.Combine(Application.dataPath, "../Evidence"));
                Directory.CreateDirectory(ev); File.WriteAllText(Path.Combine(ev, "play-checks.txt"),"PASS: "+_checks+" AVE Play Mode checks\n"+
                    "Unrelated Unity Editor SearchDatabase startup errors: "+_editorIndexErrors+"\n");
                Debug.Log("PASS: "+_checks+" AVE Play Mode checks"); SessionState.SetBool(Running,false); EditorApplication.Exit(0);
            }
        }
        catch (Exception error)
        {
            var ev = Path.GetFullPath(Path.Combine(Application.dataPath, "../Evidence"));
            Directory.CreateDirectory(ev); File.WriteAllText(Path.Combine(ev, "play-checks.txt"),"FAIL: "+error);
            Debug.LogException(error); SessionState.SetBool(Running,false); EditorApplication.Exit(1);
        }
    }
}
#endif
