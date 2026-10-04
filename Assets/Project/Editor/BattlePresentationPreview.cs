using System;
using System.IO;
using System.Linq;
using System.Reflection;
using FightingAllstar.Adapters;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;
using FightingAllstar.Presentation.Combat;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools
{
    /// <summary>Disposable battle preview. Never loads or writes the player's inventory/run save.</summary>
    [InitializeOnLoad]
    public static class BattlePresentationPreview
    {
        private const string Pending = "BattlePresentationPreview.Pending";
        private const string Automated = "BattlePresentationPreview.Automated";
        private const string EnemyFirst = "BattlePresentationPreview.EnemyFirst";
        private const string Catalog = "BattlePresentationPreview.Catalog";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static double _started;
        private static double _nextAction;
        private static bool _planningCaptured;
        private static bool _attackCaptured;
        private static bool _openingCaptured;
        private static bool _resultCaptured;
        private static bool _testedDraft;
        private static bool _sawOpening;
        private static bool _sawTurnSwitch;
        private static bool _sawExecution;
        private static int _errors;
        private static bool _skippedOpening;
        private static bool _skippedCombat;

        static BattlePresentationPreview()
        {
            EditorApplication.playModeStateChanged += OnPlayMode;
            EditorApplication.update += Tick;
            Application.logMessageReceived += OnLog;
            // Domain reload clears LocalEncounterContext. Rehydrate before scene Start methods run.
            if (SessionState.GetBool(Pending, false) && EditorApplication.isPlayingOrWillChangePlaymode)
                PrepareEncounter();
        }

        [MenuItem("Fighting Allstar/Preview Battle/Player First")]
        public static void PlayerFirst() => Start(false, false);

        [MenuItem("Fighting Allstar/Preview Battle/Enemy First")]
        public static void OpponentFirst() => Start(true, false);

        public static void RunAutomated() => Start(Environment.GetCommandLineArgs().Contains("-battleEnemyFirst"), true);

        private static void Start(bool enemyFirst, bool automated)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before starting a preview.");
            if (!automated && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            _planningCaptured = _attackCaptured = _openingCaptured = _resultCaptured = _testedDraft = false;
            _sawOpening = _sawTurnSwitch = _sawExecution = _skippedOpening = _skippedCombat = false;
            _errors = 0;
            SessionState.SetBool(EnemyFirst, enemyFirst);
            SessionState.SetBool(Automated, automated);
            SessionState.SetBool(Pending, true);
            var json = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Project/Content/Generated/wip-phase-character-catalog.json");
            var previewCatalog = JsonUtility.FromJson<ContentCatalog>(json.text);
            // Showcase the existing art; some older runtime-ready catalog entries have no portrait assets.
            previewCatalog.Characters = previewCatalog.Characters.Where(c => c.RuntimeReady &&
                AssetDatabase.LoadAssetAtPath<CharacterObject>("Assets/Resources/Character_WIP-Phase/" +
                    c.Id.Replace("fighter.", "") + ".asset")?.FighterIcon != null).Take(4).ToList();
            SessionState.SetString(Catalog, JsonUtility.ToJson(previewCatalog));
            EditorSceneManager.OpenScene("Assets/Project/Scenes/Battle.unity");
            SeedEncounter(); // Also supports Enter Play Mode with domain reload disabled.
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Pending, false))
            {
                PrepareEncounter();
            }
            if (change == PlayModeStateChange.EnteredEditMode) LocalEncounterContext.Clear();
        }

        private static void PrepareEncounter()
        {
            SeedEncounter();
            SessionState.SetBool(Pending, false);
            _started = EditorApplication.timeSinceStartup;
            _nextAction = _started + 1;
        }

        private static void SeedEncounter()
        {
            var catalog = JsonUtility.FromJson<ContentCatalog>(SessionState.GetString(Catalog, ""));
            var roster = catalog.Characters.Where(c => c.RuntimeReady).Take(4).ToList();
            if (roster.Count < 4) throw new InvalidOperationException("Preview requires four runtime-ready fighters.");
            var enemyFirst = SessionState.GetBool(EnemyFirst, false);
            var encounter = new EncounterProjection { BattleId = "presentation-preview" };
            for (var side = 0; side < 2; side++)
                for (var i = 0; i < roster.Count; i++)
                {
                    var definition = roster[i].Clone();
                    var stats = definition.BaseStats.Clone();
                    stats.MaxHealth = 850;
                    stats.Attack = 210;
                    stats.Defense = 25;
                    stats.CombatClass = (side == 1) == enemyFirst ? 12000 : 9000;
                    stats.CritChanceBp = 3500;
                    stats.CritDamageBp = 15000;
                    var fighter = new EncounterFighterSnapshot { FighterId = "preview:" + side + ":" + i,
                        DefinitionId = definition.Id, Definition = definition, Stats = stats, CurrentHealth = stats.MaxHealth,
                        FormationSlot = Math.Min(i, 2), IsReserve = i == 3 };
                    (side == 0 ? encounter.PlayerTeam : encounter.EnemyTeam).Add(fighter);
                }
            LocalEncounterContext.Begin(encounter, 8, previewOnly: true);
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying || !SessionState.GetBool(Automated, false) || _started <= 0) return;
            if (EditorApplication.timeSinceStartup - _started > 180)
            { Finish(false, "Timed out before completing battle."); return; }
            var controller = UnityEngine.Object.FindFirstObjectByType<CoreBattleSceneController>();
            if (controller == null) return;
            var phase = controller.PresentationPhase;
            var document = (UIDocument)typeof(CoreBattleSceneController).GetField("battleDocument", PrivateInstance).GetValue(controller);
            var root = document?.rootVisualElement;
            if (phase == BattlePresentationPhase.ComparingCC && !_openingCaptured &&
                !string.IsNullOrEmpty(root?.Q<Label>("cc-decision")?.text))
            {
                _openingCaptured = true;
                if (root?.Q<VisualElement>("battle-card-tray").resolvedStyle.visibility != Visibility.Hidden)
                    Finish(false, "Card tray visible during CC.");
                Capture("01-cc");
                if (Environment.GetCommandLineArgs().Contains("-battleSkipOpening"))
                {
                    CheckSkip(controller);
                    _skippedOpening = true;
                    return;
                }
            }
            _sawOpening |= phase == BattlePresentationPhase.OpeningDeal;
            _sawTurnSwitch |= phase == BattlePresentationPhase.TurnSwitch;
            _sawExecution |= phase == BattlePresentationPhase.CardExecution;
            if (phase == BattlePresentationPhase.CardExecution && !_attackCaptured && root?.Q<Label>(className: "combat-text") != null)
            {
                _attackCaptured = true;
                Capture("03-execution");
                if (Environment.GetCommandLineArgs().Contains("-battleSkipPlayback"))
                {
                    CheckSkip(controller);
                    _skippedCombat = true;
                    _nextAction = EditorApplication.timeSinceStartup + .75;
                    return;
                }
            }
            if (phase == BattlePresentationPhase.Complete)
            {
                if (!_resultCaptured)
                { _resultCaptured = true; Capture("04-result"); _nextAction = EditorApplication.timeSinceStartup + 2; }
                else if (EditorApplication.timeSinceStartup > _nextAction)
                    Finish(_errors == 0 && (_sawOpening || _skippedOpening) && _sawTurnSwitch && _sawExecution && _testedDraft &&
                        (!Environment.GetCommandLineArgs().Contains("-battleSkipPlayback") || _skippedCombat),
                        "Completed battle; runtime errors=" + _errors + "; opening=" + _sawOpening + "; execution=" + _sawExecution +
                        "; turn switch=" + _sawTurnSwitch + "; skip opening=" + _skippedOpening + "; skip combat=" + _skippedCombat);
                return;
            }
            if (phase != BattlePresentationPhase.Planning || EditorApplication.timeSinceStartup < _nextAction) return;
            var canPlan = (bool)typeof(CoreBattleSceneController).GetProperty("CanPlan", PrivateInstance).GetValue(controller);
            if (!canPlan) return;
            if (!_planningCaptured)
            {
                _planningCaptured = true;
                if (root?.Q<VisualElement>("deck-row")?.Q<Image>("artwork")?.sprite == null)
                    throw new InvalidOperationException("Preview cards must show assigned skill art or their fighter portrait fallback.");
                Capture("02-planning");
            }
            var state = (BattleState)typeof(CoreBattleSceneController).GetField("_snapshot", PrivateInstance).GetValue(controller);
            var draft = (PlanDraft)typeof(CoreBattleSceneController).GetField("_draft", PrivateInstance).GetValue(controller);
            if (!_testedDraft)
            {
                // Exercise a move, then reset without spending authority RNG or PG.
                DragAcrossHand(root);
                var movedDraft = (PlanDraft)typeof(CoreBattleSceneController).GetField("_draft", PrivateInstance).GetValue(controller);
                if (movedDraft == null || movedDraft.Actions.Count != 1 || !movedDraft.Actions[0].IsMove ||
                    movedDraft.Actions[0].DestinationIndex != state.Player.Hand.Count - 1)
                    throw new InvalidOperationException("Pointer drag did not move the card to the opposite end of the hand.");
                _testedDraft = true;
                _nextAction = EditorApplication.timeSinceStartup + 1.5;
                return;
            }
            if (draft != null && draft.Actions.Count == 1 && draft.Actions[0].IsMove)
            {
                typeof(CoreBattleSceneController).GetMethod("ResetDraft", PrivateInstance).Invoke(controller, null);
                _nextAction = EditorApplication.timeSinceStartup + .3;
                return;
            }
            var hand = draft?.Preview.Hand ?? state.Player.Hand;
            if (hand.Count > 0) typeof(CoreBattleSceneController).GetMethod("QueueCard", PrivateInstance).Invoke(controller, new object[] { hand[0].Id });
            else typeof(CoreBattleSceneController).GetMethod("CommitDraft", PrivateInstance).Invoke(controller, null);
            _nextAction = EditorApplication.timeSinceStartup + .65;
        }

        private static void Capture(string name)
        {
            var directory = Path.GetFullPath("Logs/BattlePresentationEvidence");
            Directory.CreateDirectory(directory);
            var controller = UnityEngine.Object.FindFirstObjectByType<CoreBattleSceneController>();
            var document = (UIDocument)typeof(CoreBattleSceneController).GetField("battleDocument", PrivateInstance).GetValue(controller);
            BattlePreviewCapture.Save(Path.Combine(directory, name + ".png"), document);
            Debug.Log("BATTLE PREVIEW CAPTURE " + name);
        }

        private static void CheckSkip(CoreBattleSceneController controller)
        {
            var session = (IBattleSession)typeof(CoreBattleSceneController).GetField("_session", PrivateInstance).GetValue(controller);
            var before = JsonUtility.ToJson(session.GetSnapshot());
            typeof(CoreBattleSceneController).GetMethod("SkipAnimations", PrivateInstance).Invoke(controller, null);
            var shown = (BattleState)typeof(CoreBattleSceneController).GetField("_snapshot", PrivateInstance).GetValue(controller);
            if (before != JsonUtility.ToJson(shown) || before != JsonUtility.ToJson(session.GetSnapshot()))
                throw new InvalidOperationException("Skipping changed the authority or failed to reconcile the display.");
            Debug.Log("BATTLE SKIP STATE PASS");
        }

        private static void DragAcrossHand(VisualElement root)
        {
            var hand = root.Q<VisualElement>("deck-row");
            var source = hand[hand.childCount - 1].Q<Button>("card-button");
            var start = source.worldBound.center;
            var destination = hand[0].worldBound.center;
            using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, mousePosition = start, button = 0 }))
            { down.target = source; source.SendEvent(down); }
            using (var move = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseDrag, mousePosition = destination, button = 0 }))
            { move.target = source; source.SendEvent(move); }
            using (var up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, mousePosition = destination, button = 0 }))
            { up.target = source; source.SendEvent(up); }
        }

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            if (SessionState.GetBool(Automated, false) && EditorApplication.isPlaying &&
                !stackTrace.Contains("UnityEditor.Search.") &&
                (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) _errors++;
        }

        private static void Finish(bool passed, string message)
        {
            SessionState.SetBool(Automated, false);
            Debug.Log((passed ? "BATTLE PREVIEW PASS: " : "BATTLE PREVIEW FAIL: ") + message);
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
            else EditorApplication.ExitPlaymode();
        }
    }
}
