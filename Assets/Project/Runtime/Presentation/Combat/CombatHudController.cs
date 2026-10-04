using System.Collections;
using System.Collections.Generic;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;
using UnityEngine;
using UnityEngine.UIElements;
using CoreBattleState = FightingAllstar.Core.Combat.BattleState;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>UIDocument adapter. Displays committed core snapshots/events; no gameplay rules live here.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class CombatHudController : MonoBehaviour
    {
        [SerializeField] private CombatContentSource content;
        [SerializeField] private string matchId = "local-editor-match";
        [SerializeField] private string[] playerDefinitionIds = new string[3];
        [SerializeField] private string[] opponentDefinitionIds = new string[3];
        [SerializeField] private int[] playerConstellationTiers = new int[4];
        [SerializeField] private int[] opponentConstellationTiers = new int[4];
        [SerializeField] private ulong seed = 1;
        [SerializeField] private float eventPlaybackSeconds = 0.28f;

        private UIDocument _document;
        private IBattleSession _session;
        private PlanDraft _draft;
        private CoreBattleState _snapshot;
        private string _selectedCard;
        private string _selectedTarget;
        private bool _playback;
        private int _requestNumber;
        private long _lastEventId;
        private string _reportedCompletedBattleId;

        public event System.Action<CoreBattleState> BattleCompleted;
        private readonly List<VisualElement> _fighterViews = new List<VisualElement>();
        private VisualElement _hand;
        private VisualElement _queue;
        private Label _status;
        private Button _reset;
        private Button _undo;
        private Button _confirm;
        private Button _skip;
        private Button _moveModeButton;
        private bool _moveMode;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            if (content == null) return; // A route-driven scene supplies its pinned encounter through StartLocalEncounter.
            try
            {
                var catalog = content.LoadCatalog();
                var player = ResolveTeam(catalog, playerDefinitionIds, "player");
                var opponent = ResolveTeam(catalog, opponentDefinitionIds, "opponent");
                var initial = BattleEngine.Create(matchId, player, playerConstellationTiers, opponent, opponentConstellationTiers, seed);
                _session = new LocalBattleSession(initial);
                _snapshot = _session.GetSnapshot();
            }
            catch (System.Exception exception)
            {
                Debug.LogError("Combat composition could not start: " + exception.Message, this);
            }
        }

        private void OnEnable()
        {
            if (_document == null || _document.rootVisualElement == null) return;
            Bind(_document.rootVisualElement);
            Refresh();
        }

        public void StartLocalEncounter(EncounterProjection encounter, ulong encounterSeed)
        {
            matchId = encounter.BattleId;
            var state = RunBattleBridge.CreateLocalBattle(encounter, encounterSeed);
            _session = new LocalBattleSession(state);
            _snapshot = _session.GetSnapshot();
            _draft = null;
            _selectedCard = null;
            _selectedTarget = null;
            _requestNumber = 0;
            _lastEventId = 0;
            _reportedCompletedBattleId = null;
            Refresh();
        }

        private void Bind(VisualElement root)
        {
            _hand = root.Q<VisualElement>("hand-tray");
            _queue = root.Q<VisualElement>("action-queue");
            _status = root.Q<Label>("battle-status");
            _reset = root.Q<Button>("reset-button");
            _undo = root.Q<Button>("undo-button");
            _confirm = root.Q<Button>("confirm-button");
            _skip = root.Q<Button>("skip-button");
            _moveModeButton = root.Q<Button>("move-mode-button");
            _fighterViews.Clear();
            _fighterViews.Add(root.Q<VisualElement>("player-formation"));
            _fighterViews.Add(root.Q<VisualElement>("opponent-formation"));
            if (_reset != null) _reset.clicked += ResetDraft;
            if (_undo != null) _undo.clicked += UndoDraft;
            if (_confirm != null) _confirm.clicked += CommitDraft;
            if (_skip != null) _skip.clicked += SkipPlayback;
            if (_moveModeButton != null) _moveModeButton.clicked += ToggleMoveMode;
        }

        private void Refresh()
        {
            if (_snapshot == null || _document == null) return;
            _snapshot = _session.GetSnapshot();
            var root = _document.rootVisualElement;
            if (_status != null)
                _status.text = _playback ? "Resolving committed actions…" : _snapshot.Phase == BattlePhase.Complete
                    ? (_snapshot.IsDraw ? "Draw" : _snapshot.Winner == TeamSide.Player ? "Victory" : "Defeat")
                    : _snapshot.ActingSide + " turn · " + _snapshot.ActionBudget + " actions";
            RefreshFormation(root.Q<VisualElement>("player-formation"), _draft != null ? _draft.Preview : _snapshot.Player, TeamSide.Player);
            RefreshFormation(root.Q<VisualElement>("opponent-formation"), _snapshot.Opponent, TeamSide.Opponent);
            RefreshHand();
            RefreshQueue();
            var planning = !_playback && _snapshot.Phase == BattlePhase.Planning && _snapshot.ActingSide == TeamSide.Player;
            if (_reset != null) _reset.SetEnabled(planning && _draft != null);
            if (_undo != null) _undo.SetEnabled(planning && _draft != null && _draft.Actions.Count > 0);
            if (_confirm != null) _confirm.SetEnabled(planning && _draft != null);
            if (_skip != null) _skip.SetEnabled(_playback);
        }

        private void RefreshFormation(VisualElement container, BattleTeamState team, TeamSide side)
        {
            if (container == null) return;
            container.Clear();
            foreach (var fighter in team.Fighters)
            {
                var card = new VisualElement();
                card.AddToClassList("fighter-card");
                if (!fighter.IsAlive) card.AddToClassList("fighter-defeated");
                if (fighter.IsReserve) card.AddToClassList("fighter-reserve");
                card.Add(new Label(fighter.Definition.Id.Replace("fighter.", string.Empty)) { name = "fighter-name" });
                card.Add(new Label(fighter.IsAlive ? "HP " + fighter.Health + " / " + StatusSystem.GetEffectiveStats(fighter).MaxHealth : "DEFEATED") { name = "fighter-hp" });
                card.Add(new Label("PG " + fighter.PowerGauge + " / 5") { name = "fighter-pg" });
                card.RegisterCallback<ClickEvent>(_ => SelectTarget(fighter, side));
                container.Add(card);
            }
        }

        private void RefreshHand()
        {
            if (_hand == null) return;
            _hand.Clear();
            var team = _draft != null ? _draft.Preview : _snapshot.Player;
            foreach (var card in team.Hand)
            {
                var button = new Button(() => SelectCard(card.Id)) { name = "card-" + card.Id };
                button.AddToClassList("hand-card");
                if (_selectedCard == card.Id) button.AddToClassList("card-selected");
                var owner = team.FindFighter(card.OwnerFighterId);
                var label = card.Kind == CardKind.Ultimate ? "ULT C" + card.UltimateTier : card.SkillId + " · R" + card.Rank;
                button.text = (owner == null ? "?" : owner.Definition.Id.Replace("fighter.", string.Empty)) + "\n" + label;
                button.SetEnabled(!_playback && _snapshot.ActingSide == TeamSide.Player && _snapshot.Phase == BattlePhase.Planning);
                _hand.Add(button);
            }
        }

        private void RefreshQueue()
        {
            if (_queue == null) return;
            _queue.Clear();
            if (_draft == null) return;
            var index = 0;
            foreach (var action in _draft.Actions)
                _queue.Add(new Label((++index) + ". " + (action.IsMove ? "Move " : "Play ") + action.CardId +
                    (action.TargetFighterId == null ? string.Empty : " → " + action.TargetFighterId)));
        }

        private void SelectCard(string cardId)
        {
            if (_playback || _snapshot.ActingSide != TeamSide.Player || _snapshot.Phase != BattlePhase.Planning) return;
            EnsureDraft();
            if (_moveMode && _selectedCard != null && _selectedCard != cardId)
            {
                var destination = _draft.Preview.Hand.FindIndex(card => card.Id == cardId);
                if (!_draft.QueueMove(_selectedCard, destination, out var moveError)) { SetStatus(moveError); return; }
                _selectedCard = null;
                Refresh();
                return;
            }
            _selectedCard = cardId;
            _selectedTarget = null;
            Refresh();
            SetStatus("Choose a living opponent to queue this card.");
        }

        private void SelectTarget(FighterState target, TeamSide side)
        {
            if (_moveMode || _selectedCard == null || side != TeamSide.Opponent || !target.IsAlive || target.IsReserve) return;
            EnsureDraft();
            if (!_draft.QueuePlay(_selectedCard, target.Id, out var reason)) { SetStatus(reason); return; }
            _selectedCard = null;
            _selectedTarget = target.Id;
            Refresh();
        }

        private void EnsureDraft()
        {
            if (_draft == null) _draft = new PlanDraft(_snapshot);
        }

        private void ResetDraft()
        {
            _draft?.Reset();
            _selectedCard = null;
            Refresh();
        }

        private void UndoDraft()
        {
            _draft?.UndoLast();
            Refresh();
        }

        private void CommitDraft()
        {
            if (_draft == null || _playback) return;
            var previousEvent = _snapshot.Events.Count == 0 ? 0 : _snapshot.Events[_snapshot.Events.Count - 1].Id;
            var plan = _draft.BuildPlan(matchId + ":request:" + (++_requestNumber));
            if (!_session.Submit(plan, out var error)) { SetStatus(error); return; }
            var events = _session.GetEventsAfter(previousEvent);
            _draft = null;
            _selectedCard = null;
            StartCoroutine(PlayEvents(events));
        }

        private IEnumerator PlayEvents(IReadOnlyList<BattleEvent> events)
        {
            _playback = true;
            for (var i = 0; i < events.Count; i++)
            {
                var item = events[i];
                _lastEventId = item.Id;
                SetStatus(item.Message ?? item.Kind.ToString());
                if (eventPlaybackSeconds > 0f) yield return new WaitForSeconds(eventPlaybackSeconds);
            }
            _playback = false;
            Refresh();
            ReportCompletionIfReady();
        }

        private void SkipPlayback()
        {
            StopAllCoroutines();
            _playback = false;
            _snapshot = _session.GetSnapshot();
            Refresh();
            ReportCompletionIfReady();
        }

        private void ReportCompletionIfReady()
        {
            if (_snapshot == null || _snapshot.Phase != BattlePhase.Complete || _reportedCompletedBattleId == _snapshot.MatchId) return;
            _reportedCompletedBattleId = _snapshot.MatchId;
            BattleCompleted?.Invoke(_snapshot.Clone());
        }

        private void ToggleMoveMode()
        {
            _moveMode = !_moveMode;
            _selectedCard = null;
            SetStatus(_moveMode ? "Select a regular card, then a destination card." : "Select a card, then a living opponent.");
            Refresh();
        }

        private void SetStatus(string value) { if (_status != null) _status.text = value ?? string.Empty; }

        private static List<CharacterDefinition> ResolveTeam(ContentCatalog catalog, string[] ids, string label)
        {
            if (ids == null || ids.Length < 1 || ids.Length > 4) throw new System.InvalidOperationException(label + " must contain one to four stable character ids.");
            var result = new List<CharacterDefinition>();
            foreach (var id in ids)
            {
                var character = catalog.Characters.Find(c => c.Id == id);
                if (character == null) throw new System.InvalidOperationException(label + " character not found: " + id);
                result.Add(character);
            }
            return result;
        }
    }
}
