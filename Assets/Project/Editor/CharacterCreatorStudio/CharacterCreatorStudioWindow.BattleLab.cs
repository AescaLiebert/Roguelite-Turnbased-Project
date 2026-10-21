#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using FightingAllstar.Presentation.Combat;
using FightingAllstar.Presentation.Content;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public sealed partial class CharacterCreatorStudioWindow
    {
        [Serializable]
        private sealed class LabSlot
        {
            public CharacterObject Character;
            public GameObject Model;
            public ActorPresentationProfile Profile;
            public float ModelScale = 1, ModelYaw;
            public int Level = 1, Tier, Hp = 100, Gauge = -1, Shield, StatusDamage = -1;
            public string Status = "None";
        }
        [SerializeField] private BattleLabMode _labMode;
        [SerializeField] private BattleLabFirstTurn _labFirst;
        [SerializeField] private int _labSeed = 12345, _labActorCount = 1, _labTargetCount = 1;
        [SerializeField] private bool _labDraft = true;
        [SerializeField] private List<LabSlot> _labActors = new List<LabSlot>(), _labTargets = new List<LabSlot>();
        private Label _labLiveStatus;
        private VisualElement _labLiveControls;
        private ScrollView _labFacts;
        private string _labCard, _labTarget, _labInspect;
        private bool _labWasReady, _labWasPaused;
        private int _labLiveController;
        private long _labLiveRevision = -1;
        [NonSerialized] private BattleState _labAuthoritative, _labDisplayed;
        private void BuildLabEvidence()
        {
            if (_labAuthoritative == null) { Body(_problems, "Launch a 3D lab to inspect committed facts, passive decisions and displayed state."); return; }
            if (_evidenceTab == "trace")
            {
                foreach (var trace in _labAuthoritative.PassiveTrace.AsEnumerable().Reverse().Take(80))
                {
                    var t = trace;
                    Button(_problems, "#" + t.FactSequence + " " + t.Trigger + " · " + t.RuleId + " · " + (t.Outcome ?? t.Reason) + "\n" + t.Reason + (string.IsNullOrEmpty(t.ResourceKey) ? "" : " · " + t.ResourceKey + " " + t.ResourceBefore + " → " + t.ResourceAfter),
                        () => { if (_document.Rules.Any(r => r.Id == t.RuleId)) SelectSource(t.RuleId); else SetStatus(t.RuleId + ": " + t.Reason); });
                }
                if (_labAuthoritative.PassiveTrace.Count == 0) Body(_problems, "No authored passive-rule decisions in this runtime session yet.");
            }
            else if (_evidenceTab == "state")
            {
                Body(_problems, "Authoritative state versus current 3D playback projection. Authority may be ahead while animations play.", "studio-code");
                foreach (var fighter in _labAuthoritative.Player.Fighters.Concat(_labAuthoritative.Opponent.Fighters))
                {
                    var visual = _labDisplayed?.Player.FindFighter(fighter.Id) ?? _labDisplayed?.Opponent.FindFighter(fighter.Id);
                    Body(_problems, fighter.Id + " · HP " + (visual?.Health.ToString() ?? "—") + " displayed / " + fighter.Health + " committed · gauge " + (visual?.PowerGauge.ToString() ?? "—") + " / " + fighter.PowerGauge, "studio-code");
                }
            }
            else Body(_problems, "Runtime session active · " + _labAuthoritative.Phase + " · " + _labAuthoritative.Events.Count + " committed facts. Full FCT, animation, audio, VFX and inspection run in Game View. Setup changes require restart.", "studio-success");
        }
        private void BuildBattleLab()
        {
            Heading(_host, "Battle Lab");
            Body(_host, "Quick 3D simulation in the existing battle scene. Configure actors and targets, launch, and restart freely. No scenario asset or file is required.", "studio-subtle");
            EnsureLabSlots();
            var mode = new EnumField("Battle mode", _labMode); mode.RegisterValueChangedCallback(e => { _labMode = (BattleLabMode)e.newValue; Rebuild(); }); _host.Add(mode);
            Body(_host, _labMode == BattleLabMode.RealBattle ? "REAL BATTLE · runtime HP, resources, RNG, enemy AI, status/DOT clocks, FCT and character inspector. Defeat and turn limits apply."
                : "TRAINING BATTLE · one actor and one idle target · no defeat · all skill ranks and free ultimate · actual animation, hit contacts, authored VFX and SFX.", "studio-code");
            var seed = new IntegerField("RNG seed") { value = _labSeed }; seed.RegisterValueChangedCallback(e => _labSeed = Math.Max(0, e.newValue)); _host.Add(seed);
            Button(_host, "New seed", () => { _labSeed = (int)(BattleEntropy.CreateSeed() & int.MaxValue); seed.SetValueWithoutNotify(_labSeed); });
            var first = new EnumField("First turn", _labFirst); first.SetEnabled(_labMode == BattleLabMode.RealBattle); first.RegisterValueChangedCallback(e => _labFirst = (BattleLabFirstTurn)e.newValue); _host.Add(first);
            var draft = new Toggle("Use current compiled draft for Actor 1") { value = _labDraft }; draft.RegisterValueChangedCallback(e => { _labDraft = e.newValue; Rebuild(); }); _host.Add(draft);
            DrawLabTeam(_host, "Actors", _labActors, true); DrawLabTeam(_host, "Targets", _labTargets, false);
            var controls = Row(_host);
            Button(controls, EditorApplication.isPlaying ? "Apply setup & restart" : "Launch 3D Battle Lab", () => Guard(() => { Compile(); var config = BuildLabConfiguration(); StudioBattleLabBridge.Launch(config, BuildLabVisuals(config)); }));
            Button(controls, "Open Game View", OpenLabGameView);
            Button(controls, "Stop lab", StudioBattleLabBridge.Stop);
            Body(_host, "Setup is frozen at launch. Changes apply on restart. Restart uses the same seed; New seed changes the next run. Play cards in Game View or use the quick controls. Real Battle commits the remaining turn and lets enemy AI respond.", "studio-subtle");
            Button(_host, "Legacy acceptance checks", () => ShowWorkspace("scenario"));
            Section(_inspector, "LIVE BATTLE");
            _labLiveStatus = Body(_inspector, "Launch a lab to inspect the live runtime state.", "studio-code");
            _labLiveControls = new VisualElement(); _inspector.Add(_labLiveControls);
            Section(_inspector, "COMMITTED CONTACTS & STATUS FACTS");
            _labFacts = new ScrollView(); _labFacts.style.maxHeight = 450; _inspector.Add(_labFacts);
            _labLiveRevision = -1; RefreshLabLive(true);
            _host.schedule.Execute(() => { if (_page == "lab") RefreshLabLive(false); }).Every(300);
        }
        private void EnsureLabSlots()
        {
            var ready = CharacterObjectRegistrySO.LoadAll().Where(c => c != null && c.RuntimeReady).ToList();
            while (_labActors.Count < 4) _labActors.Add(new LabSlot { Character = _labActors.Count == 0 ? _draft?.SourceCharacter ?? ready.FirstOrDefault() : ready.FirstOrDefault(c => !_labActors.Any(s => s.Character == c)) });
            while (_labTargets.Count < 4) _labTargets.Add(new LabSlot { Character = _labTargets.Count == 0 ? ready.FirstOrDefault(c => c.DefinitionId == "fighter.kyo98") ?? ready.FirstOrDefault() : ready.FirstOrDefault(c => !_labTargets.Any(s => s.Character == c)) });
        }
        private void DrawLabTeam(VisualElement parent, string label, List<LabSlot> slots, bool actor)
        {
            Section(parent, label.ToUpperInvariant());
            var count = actor ? _labActorCount : _labTargetCount;
            if (_labMode == BattleLabMode.RealBattle)
            {
                var number = new IntegerField(label + " count (1–4)") { value = count };
                number.RegisterValueChangedCallback(e => { if (actor) _labActorCount = Mathf.Clamp(e.newValue, 1, 4); else _labTargetCount = Mathf.Clamp(e.newValue, 1, 4); Rebuild(); }); parent.Add(number);
            }
            else count = 1;
            for (var i = 0; i < count; i++)
            {
                var slot = slots[i]; var index = i;
                var fold = new Foldout { text = (actor ? "Actor " : "Target ") + (i + 1) + (i == 3 ? " · reserve" : " · active") + " · " + (actor && i == 0 && _labDraft ? _document.Name + " (compiled draft)" : slot.Character?.FighterName ?? "Select fighter"), value = i == 0 };
                parent.Add(fold);
                var picker = new ObjectField("Character") { objectType = typeof(CharacterObject), value = slot.Character }; picker.SetEnabled(!(actor && i == 0 && _labDraft)); picker.RegisterValueChangedCallback(e => slot.Character = e.newValue as CharacterObject); fold.Add(picker);
                LabNumber(fold, "Level", slot.Level, v => slot.Level = Mathf.Clamp(v, 1, CharacterProgression.MaxLevel));
                LabNumber(fold, "Tier", slot.Tier, v => slot.Tier = Mathf.Clamp(v, 0, 5));
                var visual = new Foldout { text = "Presentation override (optional)", value = false }; fold.Add(visual);
                var profile = new ObjectField("Actor profile") { objectType = typeof(ActorPresentationProfile), allowSceneObjects = false, value = slot.Profile }; profile.RegisterValueChangedCallback(e => slot.Profile = e.newValue as ActorPresentationProfile); visual.Add(profile);
                var model = new ObjectField("Model / rig prefab") { objectType = typeof(GameObject), allowSceneObjects = false, value = slot.Model }; model.RegisterValueChangedCallback(e => slot.Model = e.newValue as GameObject); visual.Add(model);
                var scale = new FloatField("Model scale") { value = slot.ModelScale }; scale.RegisterValueChangedCallback(e => slot.ModelScale = Mathf.Clamp(e.newValue, .01f, 100)); visual.Add(scale);
                var yaw = new FloatField("Additional model yaw") { value = slot.ModelYaw }; yaw.RegisterValueChangedCallback(e => slot.ModelYaw = e.newValue); visual.Add(yaw);
                Body(visual, "Uses the prefab's existing Animator and the runtime's movement/reaction fallbacks. VFX/SFX use existing runtime profiles. No character or prefab assets are modified.", "studio-subtle");
                var setup = new Foldout { text = "Opening state overrides", value = false }; fold.Add(setup);
                LabNumber(setup, "HP (% max)", slot.Hp, v => slot.Hp = Mathf.Clamp(v, 1, 100));
                LabNumber(setup, "Gauge (-1 = runtime)", slot.Gauge, v => slot.Gauge = Mathf.Clamp(v, -1, 5));
                LabNumber(setup, "Extra shield", slot.Shield, v => slot.Shield = Math.Max(0, v));
                LabNumber(setup, "Status snapshot damage (-1 = source ATK)", slot.StatusDamage, v => slot.StatusDamage = Math.Max(-1, v));
                var options = StandardEffectDatabase.CreateStatusRecipes().Select(s => s.Id).Prepend("None").ToList();
                var status = new PopupField<string>("Starting status", options, options.Contains(slot.Status) ? slot.Status : "None"); status.RegisterValueChangedCallback(e => slot.Status = e.newValue); setup.Add(status);
                Body(setup, "HP applies at battle creation. Gauge/shield/status overrides apply after normal opening rules. Training ignores target gauge. Status source is the opposing lead fighter.", "studio-subtle");
            }
        }
        private static void LabNumber(VisualElement parent, string label, int value, Action<int> assign)
        { var field = new IntegerField(label) { value = value }; field.RegisterValueChangedCallback(e => assign(e.newValue)); parent.Add(field); }
        public BattleLabConfiguration BuildLabConfiguration()
        {
            EnsureLabSlots();
            var config = new BattleLabConfiguration { Mode = _labMode, FirstTurn = _labFirst, Seed = (ulong)Math.Max(0, _labSeed) };
            for (var team = 0; team < 2; team++)
            {
                var slots = team == 0 ? _labActors : _labTargets;
                var count = _labMode == BattleLabMode.TrainingBattle ? 1 : team == 0 ? _labActorCount : _labTargetCount;
                for (var i = 0; i < count; i++)
                {
                    var slot = slots[i]; CharacterDefinition definition;
                    if (team == 0 && i == 0 && _labDraft)
                    {
                        if (_compiled?.Success != true) throw new InvalidOperationException("Repair the current draft before testing it, or disable the draft override to use registry content.");
                        definition = _compiled.Character.Clone();
                    }
                    else
                    {
                        if (slot.Character == null || !slot.Character.RuntimeReady) throw new InvalidOperationException("Select a runtime-ready character for every enabled slot.");
                        definition = CharacterObjectCatalogBuilder.CreateDefinition(slot.Character);
                    }
                    (team == 0 ? config.Actors : config.Targets).Add(new BattleLabFighter { Definition = definition, Level = slot.Level, Tier = slot.Tier, HealthPercent = slot.Hp, Gauge = slot.Gauge, Shield = slot.Shield, StatusDamage = slot.StatusDamage, StatusRecipeId = slot.Status == "None" ? "" : slot.Status });
                }
            }
            return config;
        }
        private static void OpenLabGameView() => EditorApplication.ExecuteMenuItem("Window/General/Game");
        private List<StudioBattleLabBridge.VisualSelection> BuildLabVisuals(BattleLabConfiguration config)
        {
            var result = new List<StudioBattleLabBridge.VisualSelection>();
            for (var team = 0; team < 2; team++)
            {
                var fighters = team == 0 ? config.Actors : config.Targets; var slots = team == 0 ? _labActors : _labTargets;
                for (var i = 0; i < fighters.Count; i++)
                    if (slots[i].Profile != null || slots[i].Model != null || slots[i].ModelScale != 1 || slots[i].ModelYaw != 0)
                        result.Add(new StudioBattleLabBridge.VisualSelection { FighterId = (team == 0 ? TeamSide.Player : TeamSide.Opponent) + ":" + i + ":" + fighters[i].Definition.Id,
                            AssetPath = AssetDatabase.GetAssetPath(slots[i].Model), ProfilePath = AssetDatabase.GetAssetPath(slots[i].Profile), Scale = slots[i].ModelScale, Yaw = slots[i].ModelYaw });
            }
            return result;
        }
        private void RefreshLabLive(bool force)
        {
            if (_labLiveStatus == null || _labLiveStatus.panel == null) return;
            var controller = StudioBattleLabBridge.Controller;
            if (controller == null) { _labLiveControls.Clear(); _labFacts.Clear(); _labLiveRevision = -1; _labAuthoritative = _labDisplayed = null; _labLiveStatus.text = EditorApplication.isPlaying ? "Waiting for Battle Lab scene startup…" : "Lab stopped. Launch a disposable session to inspect state."; RefreshProblems(); return; }
            var state = controller.LabSnapshot;
            if (state == null) { _labLiveStatus.text = "Creating the runtime session…"; return; }
            var displayed = controller.LabDisplaySnapshot;
            _labAuthoritative = state; _labDisplayed = displayed; RefreshProblems();
            _labLiveStatus.text = BattleLabContext.Mode + " · seed " + BattleLabContext.Seed + "\nTurn " + state.TurnNumber + " / revision " + state.Revision + " · " + state.Phase + "\nPresentation: " + controller.PresentationPhase + " · " + controller.LabPresentedFacts.Count + " presented facts\nCombat RNG " + state.RngState + " (" + state.RngDrawCount + " draws)\nCard RNG " + state.CardRngState + " (" + state.CardRngDrawCount + " draws)\n" + string.Join("\n", state.Player.Fighters.Concat(state.Opponent.Fighters).Select(f => f.Id + " · HP " + f.Health + " · shield " + f.Shield + " · gauge " + f.PowerGauge + (f.IsReserve ? " · reserve" : "") + " · " + f.Statuses.Instances.Count + " statuses"));
            var ready = controller.LabCanAct && !EditorApplication.isPaused;
            if (!force && _labLiveRevision == state.Revision && _labWasReady == ready && _labWasPaused == EditorApplication.isPaused && _labLiveController == controller.GetInstanceID() && _labLiveControls.childCount > 0) return;
            _labWasReady = ready; _labWasPaused = EditorApplication.isPaused; _labLiveController = controller.GetInstanceID();
            _labLiveRevision = state.Revision; _labLiveControls.Clear(); _labFacts.Clear();
            var cards = state.Player.Hand.Select(c => c.Id).ToList();
            if (cards.Count > 0)
            {
                if (!cards.Contains(_labCard)) _labCard = cards[0];
                var card = new PopupField<string>("Runtime hand", cards, _labCard);
                Func<string, string> display = id => { var c = state.Player.Hand.First(x => x.Id == id); var f = state.Player.FindFighter(c.OwnerFighterId) ?? state.Opponent.FindFighter(c.OwnerFighterId); return f.Definition.Id + " · " + (c.Kind == CardKind.Ultimate ? "Ultimate" : c.SkillId) + " · rank " + c.Rank; };
                card.formatListItemCallback = display; card.formatSelectedValueCallback = display; card.RegisterValueChangedCallback(e => _labCard = e.newValue); _labLiveControls.Add(card);
                var targets = state.Player.LivingActive().Concat(state.Opponent.LivingActive()).Select(f => f.Id).ToList();
                if (!targets.Contains(_labTarget)) _labTarget = state.Opponent.LivingActive().FirstOrDefault()?.Id ?? targets.FirstOrDefault();
                if (targets.Count > 0) { var target = new PopupField<string>("Action target", targets, _labTarget); target.RegisterValueChangedCallback(e => _labTarget = e.newValue); _labLiveControls.Add(target); }
                Button(_labLiveControls, "Play selected card & commit turn", () => { if (!controller.LabPlayCard(_labCard, _labTarget, out var error)) SetStatus(error); }).SetEnabled(ready);
            }
            Button(_labLiveControls, "Pass turn / advance status clocks", () => { if (!controller.LabPassTurn(out var error)) SetStatus(error); }).SetEnabled(ready);
            Button(_labLiveControls, EditorApplication.isPaused ? "Resume simulation" : "Pause simulation", () => { EditorApplication.isPaused = !EditorApplication.isPaused; RefreshLabLive(true); });
            var fighters = state.Player.Fighters.Concat(state.Opponent.Fighters).Select(f => f.Id).ToList();
            if (!fighters.Contains(_labInspect)) _labInspect = fighters.First();
            var inspect = new PopupField<string>("Inspect fighter", fighters, _labInspect); inspect.RegisterValueChangedCallback(e => _labInspect = e.newValue); _labLiveControls.Add(inspect);
            Button(_labLiveControls, "Open runtime character inspector", () => { if (!controller.LabInspect(_labInspect)) SetStatus("Wait for planning or close the current inspector."); }).SetEnabled(ready);
            foreach (var item in state.Events.Where(e => e.Kind == BattleEventKind.DamageApplied || e.Kind == BattleEventKind.HealApplied || e.Kind == BattleEventKind.StatusApplied || e.Kind == BattleEventKind.StatusRemoved).Reverse().Take(40))
                Body(_labFacts, "#" + item.Id + " " + item.Kind + " · " + item.SourceId + " → " + item.TargetId + "\n" + (item.HitCount > 0 ? "hit " + item.HitIndex + "/" + item.HitCount + " · " : "") + item.DamageOrigin + " · amount " + item.Amount + " · HP after " + item.HealthAfter, "studio-code");
        }
    }
}
#endif


