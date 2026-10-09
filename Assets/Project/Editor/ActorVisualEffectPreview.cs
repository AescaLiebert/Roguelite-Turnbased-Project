using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEngine;

namespace FightingAllstar.EditorTools
{
    /// <summary>Disposable preview scene. Does not load a battle or write inventory/run saves.</summary>
    public sealed class ActorVisualEffectPreview : EditorWindow
    {
        private static readonly string[] Labels = { "ATK increase", "DEF increase", "HP increase",
            "ATK decrease", "DEF decrease", "HP decrease", "Stun / unable to act", "Barrier",
            "Paralyze", "Bleed", "Poison", "Shock", "Ignite", "All five debuffs" };
        private static readonly ActorVisualEffectKind[] Debuffs = { ActorVisualEffectKind.Paralyze,
            ActorVisualEffectKind.Bleed, ActorVisualEffectKind.Poison, ActorVisualEffectKind.Shock, ActorVisualEffectKind.Ignite };
        private static readonly ActorVisualEffectKind[] Kinds = { ActorVisualEffectKind.AttackIncrease,
            ActorVisualEffectKind.DefenseIncrease, ActorVisualEffectKind.HealthIncrease,
            ActorVisualEffectKind.AttackDecrease, ActorVisualEffectKind.DefenseDecrease, ActorVisualEffectKind.HealthDecrease };
        private Fixture _fixture;
        private int _selected;
        private bool _animate = true;
        private float _time = .35f;
        private double _started;

        [MenuItem("Fighting Allstar/Preview Actor Visual Effects")]
        public static void Open()
        {
            var window = GetWindow<ActorVisualEffectPreview>("Actor Visual Effects");
            window.minSize = new Vector2(460, 500);
        }

        private void OnEnable() { _started = EditorApplication.timeSinceStartup; EditorApplication.update += Repaint; }
        private void OnDisable() { EditorApplication.update -= Repaint; _fixture?.Dispose(); _fixture = null; }

        private void OnGUI()
        {
            _selected = EditorGUILayout.Popup("Effect", _selected, Labels);
            _animate = EditorGUILayout.Toggle("Animate", _animate);
            _time = EditorGUILayout.Slider("Sample time", _time, 0, 3.5f);
            EditorGUILayout.HelpBox("Stat effects play once. Debuffs enter briefly, then loop until removed. Paralyze and stun flinch on a disabled turn. Tuning: Resources/ActorVisualEffectSettings.", MessageType.Info);
            if (GUILayout.Button("Validate and capture all effects")) RunValidation();
            var rect = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type != EventType.Repaint) return;
            if (_fixture == null) _fixture = new Fixture();
            var time = _animate ? (float)((EditorApplication.timeSinceStartup-_started) % (_selected >= 8 ? 3.5 : 1.4)) : _time;
            _fixture.Pose(_selected, time);
            _fixture.Preview.BeginPreview(rect, GUIStyle.none);
            _fixture.Preview.Render();
            GUI.DrawTexture(rect, _fixture.Preview.EndPreview(), ScaleMode.ScaleToFit);
        }

        [MenuItem("Fighting Allstar/Validation/Actor Visual Effects")]
        public static void RunValidation()
        {
            var checks = 0;
            Action<bool,string> check = (passed,message) => { checks++; if (!passed) throw new InvalidOperationException(message); };
            var folder = Path.GetFullPath("Logs/ActorVisualEffectEvidence");
            Directory.CreateDirectory(folder);
            foreach (var kind in Kinds)
            {
                var status = StatGrant(kind);
                var effects = ActorVisualEffectRules.GrantedEffects(status);
                check(effects.Count == 1 && effects[0] == kind, "Incorrect stat direction: " + kind);
            }
            var bundle = StatGrant(ActorVisualEffectKind.AttackIncrease);
            bundle.Recipe.Modifiers[0] = new StatModifierDefinition { Target = ModifierTarget.StatBundle,
                Bundle = StatBundleKind.BasicStats, Amount = 2000 };
            check(ActorVisualEffectRules.GrantedEffects(bundle).Count == 3, "Basic-stat bundle must present all three stats.");
            bundle.Recipe.Modifiers[0].SerializedBundle = (int)StatBundleKind.HpRelated;
            check(ActorVisualEffectRules.GrantedEffects(bundle)[0] == ActorVisualEffectKind.HealthIncrease, "Serialized bundle mirrors must be respected.");
            var multiplier = StatGrant(ActorVisualEffectKind.AttackDecrease);
            multiplier.Recipe.Modifiers[0].Operation = ModifierOperation.Multiplier;
            multiplier.Recipe.Modifiers[0].Amount = 8000;
            check(ActorVisualEffectRules.GrantedEffects(multiplier)[0] == ActorVisualEffectKind.AttackDecrease,
                "A positive multiplier below 10000 is a decrease.");
            multiplier.Recipe.Modifiers[0].Operation = ModifierOperation.PercentOfBase;
            multiplier.Recipe.Modifiers[0].ScaleByStatusPotency = true;
            multiplier.PotencyBp = -2000;
            check(ActorVisualEffectRules.GrantedEffects(multiplier)[0] == ActorVisualEffectKind.AttackDecrease, "Signed potency must be respected.");
            var stun = new StatusRecipeDefinition { Id = "status.debuff.stun", Behavior = StatusBehavior.Disable,
                Polarity = StatusPolarity.Debuff, DisableMask = CardCategoryMask.AllCards, Tags = new List<string> { CombatTags.Stun } };
            check(ActorVisualEffectRules.IsStun(stun), "Stun must resolve from gameplay semantics.");
            check(!ActorVisualEffectRules.IsStun(new StatusRecipeDefinition { Id = "status.debuff.seal", Behavior = StatusBehavior.Disable,
                DisableMask = CardCategoryMask.AllCards }), "Seal must not be mistaken for stun.");
            foreach (var kind in Debuffs)
                check(ActorVisualEffectRules.PersistentDebuff(DebuffGrant(kind).Recipe) == kind, "Debuff semantics: " + kind);
            check(ActorVisualEffectRules.PersistentDebuff(new StatusRecipeDefinition { Id="status.debuff.posion" }) == ActorVisualEffectKind.Poison,
                "The legacy Poison spelling must work.");
            check(ActorVisualEffectRules.PersistentDebuff(new StatusRecipeDefinition { Id="status.debuff.seal" }) == ActorVisualEffectKind.None,
                "Generic card seals must not look paralyzed.");

            using (var fixture = new Fixture())
            {
                var effect = fixture.Effect;
                var model = fixture.Actor.transform.Find("Model");
                var position = model.localPosition; var rotation = model.localRotation;
                var rootPosition = fixture.Actor.transform.position;
                var cameraPosition = fixture.Preview.camera.transform.position;
                foreach (var kind in Kinds)
                {
                    fixture.Pose(Array.IndexOf(Kinds, kind), .35f);
                    check(effect.ActiveGrantCount == 1, "Grant must produce one short effect.");
                    effect.Play(kind); effect.Play(kind);
                    check(effect.ActiveGrantCount == 1, "Repeated grants must coalesce.");
                    effect.SamplePreview(1.25f, fixture.Preview.camera);
                    check(effect.ActiveGrantCount == 0, "Stat AVE must expire independently of the status clock.");
                }
                var granted = StatGrant(ActorVisualEffectKind.AttackIncrease);
                var applied = new BattleEvent { Kind = BattleEventKind.StatusApplied, TargetId = "fixture",
                    StatusInstanceId = granted.InstanceId, StatusRecipeId = granted.RecipeId,
                    StatusOutcome = StatusApplyOutcome.Added, StatusesAfter = new List<StatusInstance> { granted } };
                check(fixture.Stage.ActorStatusFeedback(applied) > 0 && effect.ActiveGrantCount == 1,
                    "The production event entry point must launch a stat AVE.");
                effect.ClearTransient();
                applied.Kind = BattleEventKind.StatusRemoved;
                check(fixture.Stage.ActorStatusFeedback(applied) == 0 && effect.ActiveGrantCount == 0,
                    "Removal must not launch an inverse stat grant.");
                applied.Kind = BattleEventKind.StatusApplied; applied.StatusOutcome = StatusApplyOutcome.IgnoredWeaker;
                check(fixture.Stage.ActorStatusFeedback(applied) == 0 && effect.ActiveGrantCount == 0,
                    "An ignored weaker grant must not animate.");
                effect.Synchronize(true); effect.Flinch(); effect.SamplePreview(.22f,fixture.Preview.camera);
                check(Quaternion.Angle(rotation,model.localRotation) > 2, "Stun must flinch the model.");
                check(fixture.Actor.transform.position == rootPosition, "Flinch must not displace formation or actor root.");
                check(fixture.Preview.camera.transform.position == cameraPosition, "AVE must not move the battle camera.");
                effect.SetHidden(true);
                check(!fixture.Actor.transform.Find("AVE Stun Stars").gameObject.activeSelf &&
                    Quaternion.Angle(rotation,model.localRotation) < .01f, "Cinematic suppression must restore pose and hide stars.");
                effect.SetHidden(false); effect.SamplePreview(.3f,fixture.Preview.camera);
                check(fixture.Actor.transform.Find("AVE Stun Stars").gameObject.activeSelf, "Unsuppress must restore active stun stars.");
                effect.Synchronize(false);
                check(model.localPosition == position && Quaternion.Angle(rotation,model.localRotation) < .01f,
                    "Cleanse/expiry must restore the exact model pose.");
                effect.Synchronize(true); fixture.Actor.SetActive(false);
                fixture.Stage.SyncActorVisualEffects(new FighterState { Id = "fixture", IsAlive = false });
                check(!effect.IsStunned && effect.ActiveGrantCount == 0, "Death/reserve state synchronization must clear all AVE.");
                fixture.Actor.SetActive(true);
                effect.Play(ActorVisualEffectKind.AttackIncrease); fixture.Stage.Restore();
                check(effect.ActiveGrantCount == 0, "Skip/restore must clear all transient effects.");
                var grid = new Texture2D(1200,900,TextureFormat.RGB24,false);
                for (var i = 0; i < 6; i++)
                {
                    var tile = fixture.Capture(i,.38f,400,450);
                    grid.SetPixels((i%3)*400,(1-i/3)*450,400,450,tile.GetPixels());
                    DestroyImmediate(tile);
                }
                grid.Apply(); File.WriteAllBytes(Path.Combine(folder,"six-stat-effects.png"),grid.EncodeToPNG()); DestroyImmediate(grid);
                foreach (var i in new[] { 6,7 })
                {
                    var tile = fixture.Capture(i,.25f,640,720);
                    File.WriteAllBytes(Path.Combine(folder,i == 6 ? "stun-flinch.png" : "barrier.png"),tile.EncodeToPNG());
                    DestroyImmediate(tile);
                }
                var debuffGrid = new Texture2D(1800,900,TextureFormat.RGB24,false);
                for (var i=0;i<Debuffs.Length;i++)
                {
                    var grantedDebuff=DebuffGrant(Debuffs[i]);
                    var fighter=new FighterState { Id="fixture",Definition=new CharacterDefinition() };
                    fighter.Statuses.Instances.Add(grantedDebuff); fighter.Statuses.Instances.Add(grantedDebuff);
                    effect.ClearAll(); fixture.Stage.SyncActorVisualEffects(fighter);
                    check(effect.ActiveDebuffCount==1 && effect.ActiveDebuffEnterCount==0,"Snapshot/stack sync must start one idle without an entrance.");
                    var debuffEvent=new BattleEvent { Kind=BattleEventKind.StatusApplied,TargetId="fixture",StatusInstanceId=grantedDebuff.InstanceId,
                        StatusRecipeId=grantedDebuff.RecipeId,StatusesAfter=fighter.Statuses.Instances,StatusOutcome=StatusApplyOutcome.Added };
                    check(fixture.Stage.ActorStatusFeedback(debuffEvent)>0 && effect.ActiveDebuffEnterCount==1,"Accepted grants must enter: "+Debuffs[i]);
                    effect.SamplePreview(2.2f,fixture.Preview.camera);
                    check(effect.ActiveDebuffCount==1 && effect.ActiveDebuffEnterCount==0,"Idle must outlive entrance: "+Debuffs[i]);
                    var particles=0; foreach(var ps in fixture.Actor.GetComponentsInChildren<ParticleSystem>()) particles+=ps.particleCount;
                    check(particles>0,"Idle must emit visible particles: "+Debuffs[i]);
                    fighter.Statuses.Instances.RemoveAt(0); fixture.Stage.SyncActorVisualEffects(fighter);
                    check(effect.ActiveDebuffCount==1,"Removing one source must retain the remaining source's visual.");
                    fighter.Statuses.Instances.Clear(); fixture.Stage.SyncActorVisualEffects(fighter);
                    check(effect.ActiveDebuffCount==0 && !effect.IsParalyzed,"Removing the last source must clear the idle.");
                    debuffEvent.StatusOutcome=StatusApplyOutcome.Rejected;
                    check(fixture.Stage.ActorStatusFeedback(debuffEvent)==0 && effect.ActiveDebuffEnterCount==0,"Rejected DOT grants must not enter.");
                    for(var row=0;row<2;row++)
                    {
                        var tile=fixture.Capture(8+i,row==0 ? .25f : 2.2f,360,450);
                        debuffGrid.SetPixels(i*360,(1-row)*450,360,450,tile.GetPixels()); DestroyImmediate(tile);
                    }
                }
                debuffGrid.Apply(); File.WriteAllBytes(Path.Combine(folder,"debuff-enter-idle.png"),debuffGrid.EncodeToPNG()); DestroyImmediate(debuffGrid);
                var allTile=fixture.Capture(13,2.2f,640,720);
                File.WriteAllBytes(Path.Combine(folder,"all-debuffs-idle.png"),allTile.EncodeToPNG()); DestroyImmediate(allTile);
                check(effect.ActiveDebuffCount==5,"All five statuses may coexist.");
                effect.ClearTransient(); check(effect.ActiveDebuffCount==5,"Skip must preserve persistent idle loops.");
                effect.SetHidden(true);
                check(fixture.Actor.GetComponentsInChildren<ParticleSystem>().Length==0,"Cinematics must hide every status emitter.");
                effect.SetHidden(false);
                check(effect.ActiveDebuffCount==5 && effect.ActiveDebuffEnterCount==0,"Cinematic restoration must not replay entrances.");
                effect.Flinch(); effect.SamplePreview(.2f,fixture.Preview.camera);
                check(Quaternion.Angle(rotation,model.localRotation)>5,"Paralyze must flinch without a stun.");
                effect.ClearAll();
                check(model.localPosition==position && Quaternion.Angle(rotation,model.localRotation)<.01f,"Paralyze cleanup must restore the model.");
                // Three simultaneous grants stay bounded and do not alter camera bounds.
                effect.ClearAll(); fixture.Stage.SetShieldAura("fixture",false);
                foreach (var kind in new[] { ActorVisualEffectKind.AttackIncrease,ActorVisualEffectKind.DefenseIncrease,ActorVisualEffectKind.HealthIncrease }) effect.Play(kind);
                effect.SamplePreview(.3f,fixture.Preview.camera);
                check(effect.ActiveGrantCount == 3, "A bundle grant must remain visible simultaneously.");
                var boundsMethod = typeof(BattleStagePresenter).GetMethod("FighterBounds",BindingFlags.Static|BindingFlags.NonPublic);
                var bounds = (Bounds)boundsMethod.Invoke(null,new object[] { fixture.Actor.transform });
                check(bounds.size.y < 3.1f, "AVE geometry must be excluded from actor camera bounds.");
                fixture.Stage.ClearAuras();
                check(effect.ActiveGrantCount == 0 && !effect.IsStunned, "Scene cleanup must clear AVE.");
            }
            File.WriteAllText(Path.Combine(folder,"checks.txt"),"PASS: " + checks + " actor visual effect checks\n" +
                "Stat grid: ATK / DEF / HP increase on top; decreases below.\n" +
                "Debuff grid: Paralyze / Bleed / Poison / Shock / Ignite; entrance above, idle below.\n");
            Debug.Log("PASS: " + checks + " Actor Visual Effect checks. Captures: " + folder);
        }

        private static StatusInstance StatGrant(ActorVisualEffectKind kind)
        {
            var up = kind == ActorVisualEffectKind.AttackIncrease || kind == ActorVisualEffectKind.DefenseIncrease || kind == ActorVisualEffectKind.HealthIncrease;
            var stat = kind == ActorVisualEffectKind.AttackIncrease || kind == ActorVisualEffectKind.AttackDecrease ? StatId.Attack :
                kind == ActorVisualEffectKind.DefenseIncrease || kind == ActorVisualEffectKind.DefenseDecrease ? StatId.Defense : StatId.MaxHealth;
            return new StatusInstance { InstanceId = "grant", RecipeId = "fixture." + kind,
                Recipe = new StatusRecipeDefinition { Id = "fixture." + kind, Polarity = up ? StatusPolarity.Buff : StatusPolarity.Debuff,
                    Behavior = StatusBehavior.Stat, Modifiers = new List<StatModifierDefinition> {
                        new StatModifierDefinition { Stat = stat, Operation = ModifierOperation.PercentOfBase, Amount = up ? 2000 : -2000 } } } };
        }

        private static StatusInstance DebuffGrant(ActorVisualEffectKind kind)
        {
            var id="status.debuff."+kind.ToString().ToLowerInvariant();
            return new StatusInstance { InstanceId=id,RecipeId=id,Recipe=new StatusRecipeDefinition { Id=id,
                Polarity=StatusPolarity.Debuff,Behavior=kind==ActorVisualEffectKind.Paralyze ? StatusBehavior.Disable : StatusBehavior.DamageOverTime } };
        }

        private sealed class Fixture : IDisposable
        {
            public readonly PreviewRenderUtility Preview;
            public readonly GameObject Actor;
            public readonly ActorVisualEffect Effect;
            public readonly BattleStagePresenter Stage;

            public Fixture()
            {
                Preview = new PreviewRenderUtility();
                Preview.camera.clearFlags = CameraClearFlags.SolidColor;
                Preview.camera.backgroundColor = new Color(.055f,.07f,.12f);
                Preview.camera.transform.position = new Vector3(0,2.8f,-8);
                Preview.camera.transform.LookAt(new Vector3(0,2.05f,0));
                Preview.camera.fieldOfView = 36;
                Preview.camera.nearClipPlane = .05f; Preview.camera.farClipPlane = 100;
                Preview.lights[0].intensity = 1.1f; Preview.lights[0].transform.rotation = Quaternion.Euler(35,145,0);
                Preview.lights[1].intensity = .65f; Preview.ambientColor = new Color(.35f,.4f,.5f);
                var character = AssetDatabase.LoadAssetAtPath<CharacterObject>("Assets/Project/Data/Character/WIP_Phase/brian94.asset");
                var prefab = character != null ? character.Fighter3DPrefab : AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/In-Game-CharacterPrefab.prefab");
                Actor = BattleFighterView.Create(prefab,Vector3.up*1.5f,Quaternion.Euler(0,180,0),
                    character != null ? character.FighterModelYawOffset : 0,
                    character?.Fighter3DMesh,character?.Fighter3DMaterial);
                Preview.AddSingleGO(Actor);
                var stageGo = new GameObject("AVE Preview Stage"); Preview.AddSingleGO(stageGo);
                Stage = stageGo.AddComponent<BattleStagePresenter>();
                Stage.Initialize(new Dictionary<string,GameObject> { ["fixture"] = Actor });
                typeof(BattleStagePresenter).GetField("_camera",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(Stage,Preview.camera);
                typeof(BattleStagePresenter).GetField("_planningPosition",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(Stage,Preview.camera.transform.position);
                typeof(BattleStagePresenter).GetField("_planningRotation",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(Stage,Preview.camera.transform.rotation);
                typeof(BattleStagePresenter).GetField("_homeFov",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(Stage,Preview.camera.fieldOfView);
                Effect = Actor.AddComponent<ActorVisualEffect>();
                Effect.Synchronize(false);
            }

            public void Pose(int index, float age)
            {
                Effect.ClearAll(); Stage.SetShieldAura("fixture",index == 7);
                if (index < 6) Effect.Play(Kinds[index]);
                if (index == 6) { Effect.Synchronize(true); Effect.Flinch(); }
                if (index >= 8)
                {
                    var kinds=index==13 ? Debuffs : new[] { Debuffs[index-8] };
                    Effect.SynchronizeDebuffs(kinds);
                    foreach(var kind in kinds) Effect.EnterDebuff(kind);
                    if (index==8) Effect.Flinch();
                }
                Effect.SamplePreview(age,Preview.camera);
            }

            public Texture2D Capture(int index,float age,int width,int height)
            {
                Pose(index,age);
                Preview.BeginStaticPreview(new Rect(0,0,width,height)); Preview.Render();
                return Preview.EndStaticPreview();
            }

            public void Dispose() { Preview.Cleanup(); }
        }
    }
}
