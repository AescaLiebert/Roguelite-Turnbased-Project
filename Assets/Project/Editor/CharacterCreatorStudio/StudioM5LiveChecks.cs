#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using FightingAllstar.Presentation.Combat;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    /// <summary>Real Play Mode acceptance through the editor's widgets; survives domain reload using volatile SessionState.</summary>
    [InitializeOnLoad]
    public static class StudioM5LiveChecks
    {
        private const string Key = "CharacterStudio.M5.LiveChecks";
        [Serializable] private sealed class Trial
        {
            public bool Running = true; public int Stage; public double Deadline;
            public string Error, OriginalScene, SourcePath, SourceHash, SceneHash, Card, Target, TrainingOpeningHash;
            public bool OriginalDirty, SawFct, SawAudio, SawMotion;
            public int ExpectedHits, TargetHp;
            public long PausedRevision;
            public float ActorX, ActorY, ActorZ;
            public Dictionary<string, string> Persistent = new Dictionary<string, string>();
            public List<string> Passed = new List<string>();
        }
        private static Trial trial;
        private static bool waitingPointer;
        static StudioM5LiveChecks()
        {
            var source = SessionState.GetString(Key, "");
            if (!string.IsNullOrEmpty(source)) trial = JsonConvert.DeserializeObject<Trial>(source);
            if (trial?.Running == true) EditorApplication.update += Tick;
        }
        public static object Status => trial == null ? (object)new { Running = false } : new { trial.Running, trial.Stage, trial.Error, Assertions = trial.Passed.Count };
        public static object Start()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start the live checks from Edit Mode.");
            EditorApplication.update -= Tick;
            var w = CharacterCreatorStudioWindow.Open();
            trial = new Trial { OriginalScene = SceneManager.GetActiveScene().path, OriginalDirty = SceneManager.GetActiveScene().isDirty,
                SourcePath = AssetDatabase.GetAssetPath(w.Draft.SourceCharacter), SceneHash = Hash(StudioBattleLabBridge.ScenePath), Persistent = PersistentFiles() };
            trial.SourceHash = Hash(trial.SourcePath); trial.Deadline = EditorApplication.timeSinceStartup + 120;
            Store(); EditorApplication.update += Tick; return Status;
        }
        private static string Hash(string path) { using (var h = SHA256.Create()) return Convert.ToBase64String(h.ComputeHash(File.ReadAllBytes(path))); }
        private static Dictionary<string, string> PersistentFiles() => Directory.Exists(Application.persistentDataPath)
            ? Directory.GetFiles(Application.persistentDataPath, "*.json", SearchOption.AllDirectories).ToDictionary(p => p, Hash) : new Dictionary<string, string>();
        private static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); trial.Passed.Add(message); Store(); }
        private static void Next(int stage) { trial.Stage = stage; trial.Deadline = EditorApplication.timeSinceStartup + 120; Store(); }
        private static CharacterCreatorStudioWindow Lab()
        { var w = CharacterCreatorStudioWindow.Open(); w.ShowWorkspace("lab"); return w; }
        private static void Tick()
        {
            if (trial?.Running != true || EditorApplication.isCompiling || waitingPointer) return;
            try
            {
                if (EditorApplication.timeSinceStartup > trial.Deadline) throw new TimeoutException("Lab acceptance timed out at stage " + trial.Stage);
                var controller = StudioBattleLabBridge.Controller;
                if (controller != null && controller.LabActiveFloatingText > 0) { trial.SawFct = true; Store(); }
                if (controller != null && UnityEngine.Object.FindObjectsByType<SFXPlayer>(FindObjectsSortMode.None).Any(p => p.GetComponent<AudioSource>()?.isPlaying == true)) { trial.SawAudio = true; Store(); }
                switch (trial.Stage)
                {
                    case 0:
                    {
                        var w = Lab(); SetMode(w, BattleLabMode.TrainingBattle);
                        var actor = w.rootVisualElement.Query<Foldout>().ToList().First(f => f.text.StartsWith("Actor 1"));
                        actor.Query<FloatField>().ToList().First(f => f.label == "Model scale").value = 1;
                        actor.Query<UnityEditor.UIElements.ObjectField>().ToList().First(f => f.label == "Model / rig prefab").value = null;
                        var target = w.rootVisualElement.Query<Foldout>().ToList().First(f => f.text.StartsWith("Target 1"));
                        target.Query<PopupField<string>>().ToList().First(f => f.label == "Starting status").value = "None";
                        Next(1); Click(w, "Launch 3D Battle Lab"); break;
                    }
                    case 1:
                    {
                        if (controller?.LabCanAct != true) return;
                        var state = controller.LabSnapshot; var w = Lab();
                        Check(state.IsTraining && state.Player.Hand.Count == 7 && state.Player.Fighters[0].CannotDie, "Training launch uses the real practice state and rank deck.");
                        Check(controller.LabFighterViewCount == 2 && UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Any(r => r.transform.root.name.StartsWith("CoreFighter_")), "Selected runtime actor prefabs and renderers are instantiated.");
                        Check(PlayerInventoryService.Instance == null, "The lab does not bootstrap the account inventory service.");
                        trial.TrainingOpeningHash = StudioSimulation.StateHash(state);
                        var card = state.Player.Hand.First(c => c.Kind == CardKind.Skill && c.SkillId == state.Player.Fighters[0].Definition.Skills.OrderBy(s => s.Slot).First().Id && c.Rank == 1);
                        var owner = state.Player.FindFighter(card.OwnerFighterId); AbilityVariantSystem.TryResolve(owner, card.Clone(), out var effect);
                        trial.ExpectedHits = effect.Attack?.HitCount ?? 1; trial.Card = card.Id; trial.Target = state.Opponent.Fighters[0].Id; trial.TargetHp = state.Opponent.Fighters[0].Health;
                        var actor = GameObject.Find("CoreFighter_" + owner.Id); if (actor != null) { trial.ActorX = actor.transform.position.x; trial.ActorY = actor.transform.position.y; trial.ActorZ = actor.transform.position.z; }
                        Popup(w, "Runtime hand", card.Id); Popup(w, "Action target", trial.Target); Next(2); Click(w, "Play selected card & commit turn"); break;
                    }
                    case 2:
                    {
                        if (controller == null) return;
                        var actor = GameObject.Find("CoreFighter_" + controller.LabSnapshot.Player.Fighters[0].Id);
                        if (actor != null && Vector3.Distance(actor.transform.position, new Vector3(trial.ActorX, trial.ActorY, trial.ActorZ)) > .01f) { trial.SawMotion = true; Store(); }
                        if (!controller.LabCanAct || controller.LabSnapshot.CompletedTurnCount < 2) return;
                        var state = controller.LabSnapshot;
                        var contacts = state.Events.Where(e => e.Kind == BattleEventKind.DamageApplied && e.CardId == trial.Card && e.DamageOrigin == ElementalDamageOrigin.Card).ToList();
                        Check(contacts.Count == trial.ExpectedHits && contacts.Select(e => e.HitIndex).SequenceEqual(Enumerable.Range(1, trial.ExpectedHits)), "Actual damage playback preserves every logical contact and hit index.");
                        Check(controller.LabPresentedFacts.Any(e => e.Kind == BattleEventKind.DamageApplied), "Committed contact facts pass through real 3D presentation.");
                        Check(trial.SawFct, "Real floating combat text becomes active during contact playback.");
                        Check(trial.SawAudio, "The runtime SFX player plays impact audio during the action.");
                        Check(trial.SawMotion, "The selected actor executes actual battle-stage movement.");
                        Check(state.Opponent.Fighters[0].Health == trial.TargetHp && !state.Events.Any(e => e.Kind == BattleEventKind.CardPlayed && e.SourceId == trial.Target), "Training remains repeatable and its target does not attack.");
                        trial.PausedRevision = state.Revision;
                        var w = Lab(); Next(20); Click(w, "Pause simulation"); break;
                    }
                    case 20:
                    {
                        if (!EditorApplication.isPaused || controller == null) return;
                        Check(controller.LabSnapshot.Revision == trial.PausedRevision, "Pause suspends the actual simulation without advancing authority.");
                        var w = Lab(); Next(21); Click(w, "Resume simulation"); break;
                    }
                    case 21:
                    {
                        if (EditorApplication.isPaused || controller?.LabCanAct != true) return;
                        Check(true, "Resume returns the real lab to usable planning controls.");
                        var w = Lab(); Next(3); Click(w, "Open runtime character inspector"); break;
                    }
                    case 3:
                    {
                        if (controller?.LabInspectorOpen != true) return;
                        Check(true, "The actual runtime character inspector opens from the lab.");
                        Directory.CreateDirectory("Artifacts/CharacterCreatorStudio/M5");
                        var document = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).First(d => d.rootVisualElement.Q("battle-lab-controls") != null);
                        BattlePreviewCapture.Save(Path.GetFullPath("Artifacts/CharacterCreatorStudio/M5/training-inspector.png"), document);
                        var inspector = UnityEngine.Object.FindFirstObjectByType<BattleCharacterInspector>();
                        inspector.GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.name == "Close").onClick.Invoke(); Next(4); break;
                    }
                    case 4:
                    {
                        if (controller?.LabCanAct != true) return;
                        var w = Lab();
                        var actor = w.rootVisualElement.Query<Foldout>().ToList().First(f => f.text.StartsWith("Actor 1"));
                        actor.Query<FloatField>().ToList().First(f => f.label == "Model scale").value = 1.25f;
                        actor.Query<UnityEditor.UIElements.ObjectField>().ToList().First(f => f.label == "Model / rig prefab").value = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/In-Game-CharacterPrefab.prefab");
                        Next(5); Click(w, "Apply setup & restart"); break;
                    }
                    case 5:
                    {
                        if (controller?.LabCanAct != true || controller.LabSnapshot.CompletedTurnCount != 0) return;
                        Check(StudioSimulation.StateHash(controller.LabSnapshot) == trial.TrainingOpeningHash, "Restart restores the same opening state and RNG without a scenario file.");
                        var view = GameObject.Find("CoreFighter_" + controller.LabSnapshot.Player.Fighters[0].Id);
                        Check(view != null && Mathf.Abs(view.transform.localScale.x - 1.25f) < .001f, "Transient model/prefab overrides affect presentation while leaving combat state identical.");
                        var w = Lab(); SetMode(w, BattleLabMode.RealBattle);
                        var first = w.rootVisualElement.Query<EnumField>().ToList().First(f => f.label == "First turn"); first.value = BattleLabFirstTurn.Player;
                        var target = w.rootVisualElement.Query<Foldout>().ToList().First(f => f.text.StartsWith("Target 1")); target.value = true;
                        var status = target.Query<PopupField<string>>().ToList().First(f => f.label == "Starting status"); status.value = "status.debuff.bleed";
                        Next(6); Click(w, "Apply setup & restart"); break;
                    }
                    case 6:
                    {
                        if (controller?.LabCanAct != true || controller.LabSnapshot.IsTraining) return;
                        var state = controller.LabSnapshot;
                        Check(!state.Player.Fighters[0].CannotDie && !state.Player.TrainingDeck && state.Opponent.Fighters[0].Statuses.Instances.Any(s => s.RecipeId == "status.debuff.bleed"), "Switching to Real Battle removes practice cheats and applies the selected starting status.");
                        var w = Lab(); Next(7); Click(w, "Pass turn / advance status clocks"); break;
                    }
                    case 7:
                    {
                        if (controller?.LabCanAct != true || controller.LabSnapshot.CompletedTurnCount < 2) return;
                        var state = controller.LabSnapshot;
                        Check(state.Events.Any(e => e.Kind == BattleEventKind.CardPlayed && e.SourceId.StartsWith("Opponent:")), "Real Battle uses enemy AI through the normal local session.");
                        Check(state.Events.Any(e => e.Kind == BattleEventKind.DamageApplied && e.DamageOrigin == ElementalDamageOrigin.DamageOverTime), "Starting DOT resolves through the real turn clock and damage pipeline.");
                        Check(state.RngDrawCount > 0 && state.CardRngDrawCount > 0, "Actual combat and card RNG streams are live.");
                        Check(PlayerInventoryService.Instance == null, "Real Battle also remains isolated from account state.");
                        var doc = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None).First(d => d.rootVisualElement.Q("battle-lab-controls") != null);
                        BattlePreviewCapture.Save(Path.GetFullPath("Artifacts/CharacterCreatorStudio/M5/real-battle.png"), doc);
                        var w = Lab(); Next(8); Click(w, "Stop lab"); break;
                    }
                    case 8:
                    {
                        if (EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool("CharacterStudio.BattleLab.Pending", false)) return;
                        Check(SceneManager.GetActiveScene().path == trial.OriginalScene && SceneManager.GetActiveScene().isDirty == trial.OriginalDirty, "Stopping restores the previous editor scene and dirty state.");
                        Check(!BattleLabContext.IsReady, "Volatile runtime setup is cleared after leaving Play Mode.");
                        Check(Hash(trial.SourcePath) == trial.SourceHash && Hash(StudioBattleLabBridge.ScenePath) == trial.SceneHash, "Source character and Training scene files remain byte-for-byte unchanged.");
                        var now = PersistentFiles();
                        Check(now.Count == trial.Persistent.Count && now.All(p => trial.Persistent.TryGetValue(p.Key, out var hash) && hash == p.Value), "Account/run JSON files are neither created nor changed.");
                        trial.Running = false; Store(); Write(); EditorApplication.update -= Tick; Lab(); break;
                    }
                }
            }
            catch (Exception e) { trial.Error = "Stage " + trial.Stage + ": " + e; trial.Running = false; Store(); Write(); EditorApplication.update -= Tick; if (EditorApplication.isPlaying) StudioBattleLabBridge.Stop(); }
        }
        private static void SetMode(CharacterCreatorStudioWindow w, BattleLabMode mode) => w.rootVisualElement.Query<EnumField>().ToList().First(f => f.label == "Battle mode").value = mode;
        private static void Popup(CharacterCreatorStudioWindow w, string label, string value) => w.rootVisualElement.Query<PopupField<string>>().ToList().First(f => f.label == label).value = value;
        private static void Click(CharacterCreatorStudioWindow w, string label)
        {
            var b = w.rootVisualElement.Query<Button>().ToList().First(x => x.text == label);
            for (var p = b.parent; p != null; p = p.parent) if (p is ScrollView scroll) { scroll.ScrollTo(b); break; }
            if (!b.enabledInHierarchy) throw new InvalidOperationException("Disabled lab button: " + label);
            waitingPointer = true;
            b.schedule.Execute(() => { waitingPointer = false; b.Focus(); var p = b.worldBound.center;
                using (var e = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = p })) b.SendEvent(e);
                using (var e = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = p })) b.SendEvent(e);
            }).StartingIn(80);
        }
        private static void Store() => SessionState.SetString(Key, JsonConvert.SerializeObject(trial));
        private static void Write()
        {
            Directory.CreateDirectory("Artifacts/CharacterCreatorStudio/M5");
            File.WriteAllText("Artifacts/CharacterCreatorStudio/M5/live-evidence.json", JsonConvert.SerializeObject(new { Success = trial.Error == null && trial.Stage == 8, trial.Error,
                Checks = trial.Passed, trial.SawFct, trial.SawAudio, trial.SawMotion, trial.ExpectedHits, Scene = trial.OriginalScene, CapturedUtc = DateTime.UtcNow.ToString("O"),
                RuntimeAssemblyHash = Hash(typeof(BattleLabSimulation).Assembly.Location), EditorAssemblyHash = Hash(typeof(StudioM5LiveChecks).Assembly.Location),
                PresentationAssemblyHash = Hash(typeof(CoreBattleSceneController).Assembly.Location), Scope = "Actual 3D Training and Real Battle Play Mode sessions; editor widget inputs and normal controller/session APIs. No scenario assets." }, Formatting.Indented));
        }
    }
}
#endif


