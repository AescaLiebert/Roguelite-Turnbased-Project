using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Adapters;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Run;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using CoreBattleState = FightingAllstar.Core.Combat.BattleState;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Renders the Core battle in the existing 3D scene without owning combat rules.</summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class CoreBattleSceneController : MonoBehaviour
    {
        [SerializeField] private string returnScene = "Combat";
        [SerializeField, Range(0f, 1f)] private float eventDelay = 0.25f;
        [SerializeField] private UIDocument battleDocument;
        [SerializeField] private VisualTreeAsset cardTemplate;

        private IBattleSession _session;
        private CoreBattleState _snapshot;
        private PlanDraft _draft;
        private VisualElement _handRow;
        private VisualElement _actionRow;
        private Label _tooltipText;
        private Button _resetButton;
        private readonly List<VisualElement> _handSlots = new List<VisualElement>();
        private readonly List<VisualElement> _handContents = new List<VisualElement>();
        private readonly List<VisualElement> _actionSlots = new List<VisualElement>();
        private readonly List<VisualElement> _actionContents = new List<VisualElement>();
        private readonly List<Label> _actionMarkers = new List<Label>();
        private string _tooltipCardId;
        private string _preferredTargetId;
        private bool _isPlayingEvents;
        private int _requestSequence;
        private readonly Dictionary<string, GameObject> _fighterViews = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, CoreFighterHud> _fighterBillboards = new Dictionary<string, CoreFighterHud>();
        private bool _openingSequenceComplete;
        private long _openingEventCursor;

        private void Awake()
        {
            EnableBattlePresentation();
        }

        private void Start()
        {
            BindBattleHud();
            if (!LocalEncounterContext.TryConsumeEncounter(out var encounter, out var seed))
            {
                Debug.LogWarning("No battle encounter is pending. Open Gacha, set CharacterLoadOut, then enter a Dungeon.", this);
                SetControls(false);
                return;
            }

            try
            {
                var playerCC = encounter.PlayerTeam.Sum(fighter => fighter.Stats == null ? 0 : fighter.Stats.CombatClass);
                var opponentCC = encounter.EnemyTeam.Sum(fighter => fighter.Stats == null ? 0 : fighter.Stats.CombatClass);
                var playerFirst = playerCC >= opponentCC;
                var initialState = RunBattleBridge.CreateLocalBattle(encounter, seed,
                    playerFirst ? TeamSide.Player : TeamSide.Opponent);
                _openingEventCursor = initialState.Events.Count == 0 ? 0 : initialState.Events[initialState.Events.Count - 1].Id;
                _session = new LocalBattleSession(initialState);
                _snapshot = _session.GetSnapshot();
                SpawnFighterViews(_snapshot);
                RefreshView();
                PlayOpeningSequence(playerCC, opponentCC, playerFirst, encounter);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                SetControls(false);
            }
        }

        private static void EnableBattlePresentation()
        {
            SetActive("UnitUICanvas");
            SetActive("MasterUICanvas");
        }

        private static void SetActive(string name)
        {
            var target = GameObject.Find(name);
            if (target != null) target.SetActive(true);
        }

        private void PlayOpeningSequence(int playerCC, int opponentCC, bool playerFirst, EncounterProjection encounter)
        {
            var openingPresenter = FindFirstObjectByType<BattleOpeningPresenter>(FindObjectsInactive.Include);
            if (openingPresenter == null)
            {
                _openingSequenceComplete = true;
                RefreshView();
                PlayOpeningOpponentEvents();
                return;
            }

            openingPresenter.PlayCoreStartSequence(playerCC, opponentCC, playerFirst,
                LoadOpeningIcon(encounter.PlayerTeam), LoadOpeningIcon(encounter.EnemyTeam), () =>
                {
                    _openingSequenceComplete = true;
                    RefreshView();
                    PlayOpeningOpponentEvents();
                });
        }

        private void PlayOpeningOpponentEvents()
        {
            if (_session == null) return;
            var events = _session.GetEventsAfter(_openingEventCursor);
            if (events.Count > 0) StartCoroutine(PlayEvents(events));
        }

        private static Sprite LoadOpeningIcon(IReadOnlyList<EncounterFighterSnapshot> team)
        {
            if (team == null || team.Count == 0) return null;
            var first = team.OrderBy(fighter => fighter.FormationSlot).First();
            return LoadCharacter(first.DefinitionId)?.FighterIcon;
        }

        private void SpawnFighterViews(CoreBattleState state)
        {
            _fighterViews.Clear();
            _fighterBillboards.Clear();
            SpawnTeamViews(state.Player, "Hero");
            SpawnTeamViews(state.Opponent, "Enemy");
        }

        private void SpawnTeamViews(BattleTeamState team, string anchorPrefix)
        {
            foreach (var fighter in team.Fighters)
            {
                var character = LoadCharacter(fighter.Definition.Id);
                if (character == null || character.Fighter3DPrefab == null)
                {
                    Debug.LogWarning("No 3D model is assigned for " + fighter.Definition.Id + ".", this);
                    continue;
                }

                var slot = fighter.IsReserve ? 3 : fighter.FormationSlot;
                var anchorName = anchorPrefix == "Hero" ? "CharHeroPosition" : "EnemyHeroPosition";
                var anchor = GameObject.Find(anchorName + (Mathf.Clamp(slot, 0, 2) + 1));
                var position = anchor == null ? new Vector3(anchorPrefix == "Hero" ? -3f : 3f, 0f, 0f) : anchor.transform.position;
                var rotation = anchor == null ? Quaternion.identity : anchor.transform.rotation;
                var view = Instantiate(character.Fighter3DPrefab, position, rotation);
                view.name = "CoreFighter_" + fighter.Id;
                if (fighter.Side == TeamSide.Opponent) WireOpponentTarget(view, fighter.Id);
                if (fighter.IsReserve) view.SetActive(false);
                _fighterViews[fighter.Id] = view;
                CreateCoreBillboard(fighter, view.transform);
            }
        }

        private void WireOpponentTarget(GameObject view, string fighterId)
        {
            var collider = view.GetComponent<Collider>();
            if (collider == null)
            {
                var box = view.AddComponent<BoxCollider>();
                var renderers = view.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    var bounds = renderers[0].bounds;
                    for (var index = 1; index < renderers.Length; index++) bounds.Encapsulate(renderers[index].bounds);
                    box.center = view.transform.InverseTransformPoint(bounds.center);
                    var localSize = view.transform.InverseTransformVector(bounds.size);
                    box.size = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
                }
                else
                {
                    box.center = new Vector3(0f, 1f, 0f);
                    box.size = new Vector3(1f, 2f, 1f);
                }
            }

            var target = view.GetComponent<CoreFighterTarget>();
            if (target == null) target = view.AddComponent<CoreFighterTarget>();
            target.Initialize(fighterId, SelectTarget);
        }

        private void CreateCoreBillboard(FighterState fighter, Transform target)
        {
            var factory = FindFirstObjectByType<FighterWorldHudFactory>(FindObjectsInactive.Include);
            if (factory == null) return;
            var hud = factory.CreateFighterHud(target, ShortId(fighter.Definition.Id), fighter.Health,
                fighter.Stats.MaxHealth, fighter.Shield, fighter.PowerGauge);
            if (hud != null) _fighterBillboards[fighter.Id] = hud;
        }

        private void RefreshView()
        {
            if (_session == null) return;
            _snapshot = _session.GetSnapshot();
            RefreshWorldViews();
            RenderHand();
            RenderActionField();
            RenderTooltip();

            var playerPlanning = _openingSequenceComplete && !_isPlayingEvents &&
                _snapshot.Phase == BattlePhase.Planning && _snapshot.ActingSide == TeamSide.Player;
            SetControls(playerPlanning);
            if (_resetButton != null) _resetButton.SetEnabled(playerPlanning && _draft != null && _draft.Actions.Count > 0);

            if (_snapshot.Phase == BattlePhase.Complete)
                Debug.Log(_snapshot.IsDraw ? "Battle draw." : _snapshot.Winner == TeamSide.Player ? "Battle victory." : "Battle defeat.", this);
        }

        private void RenderHand()
        {
            if (_handRow == null) return;
            foreach (var contents in _handContents) contents.Clear();
            if (!_openingSequenceComplete || _snapshot == null || _snapshot.ActingSide != TeamSide.Player ||
                _snapshot.Phase != BattlePhase.Planning || _isPlayingEvents) return;

            var team = _draft == null ? _snapshot.Player : _draft.Preview;
            for (var index = 0; index < _handSlots.Count; index++)
            {
                var slot = _handSlots[index];
                slot.style.display = index < team.HandCapacity ? DisplayStyle.Flex : DisplayStyle.None;
                if (index >= team.Hand.Count) continue;
                var card = team.Hand[index];
                var capturedId = card.Id;
                AddCardButton(_handContents[index], card, team.FindFighter(card.OwnerFighterId),
                    () => QueueCard(capturedId), () => ShowTooltip(capturedId), HideTooltip);
            }
        }

        private void RenderActionField()
        {
            if (_actionRow == null) return;
            foreach (var contents in _actionContents) contents.Clear();
            foreach (var marker in _actionMarkers) marker.style.display = DisplayStyle.Flex;
            if (_snapshot == null) return;

            var actions = _draft == null ? new List<PlannedAction>() : _draft.Actions.Where(action => !action.IsMove).ToList();
            var visibleSlotCount = _snapshot.ActingSide == TeamSide.Player && _snapshot.ActionBudget > 0
                ? _snapshot.ActionBudget
                : (_snapshot.Player.LivingActive().Count > 2 ? 3 : 2);
            for (var index = 0; index < _actionSlots.Count; index++)
            {
                var slot = _actionSlots[index];
                slot.style.display = index < visibleSlotCount ? DisplayStyle.Flex : DisplayStyle.None;
                if (index >= visibleSlotCount) continue;
                if (index >= actions.Count)
                {
                    continue;
                }

                var card = _snapshot.Player.Hand.Find(item => item.Id == actions[index].CardId);
                var owner = card == null ? null : _snapshot.Player.FindFighter(card.OwnerFighterId);
                if (card != null)
                {
                    AddCardButton(_actionContents[index], card, owner, null, null, null);
                    _actionMarkers[index].style.display = DisplayStyle.None;
                }
            }
        }

        private void RenderTooltip()
        {
            if (_tooltipText == null) return;
            var card = string.IsNullOrEmpty(_tooltipCardId) || _snapshot == null
                ? null : _snapshot.Player.Hand.Find(item => item.Id == _tooltipCardId);
            if (card == null)
            {
                _tooltipText.style.display = DisplayStyle.None;
                return;
            }

            var owner = _snapshot.Player.FindFighter(card.OwnerFighterId);
            _tooltipText.text = GetTooltip(card, owner);
            _tooltipText.style.display = DisplayStyle.Flex;
        }

        private void ShowTooltip(string id)
        {
            _tooltipCardId = id;
            RenderTooltip();
        }

        private void HideTooltip()
        {
            _tooltipCardId = null;
            RenderTooltip();
        }

        private void QueueCard(string cardId)
        {
            if (_isPlayingEvents || _snapshot == null) return;
            if (_draft == null) _draft = new PlanDraft(_snapshot);
            var target = _snapshot.Opponent.LivingActive().FirstOrDefault(fighter => fighter.Id == _preferredTargetId)
                ?? _snapshot.Opponent.LivingActive().FirstOrDefault();
            if (target == null)
            {
                Debug.LogWarning("No living opponent can receive this card.", this);
                return;
            }
            if (!_draft.QueuePlay(cardId, target.Id, out var error))
            {
                Debug.LogWarning(error, this);
                return;
            }
            _tooltipCardId = null;
            RefreshView();
            if (_draft.Actions.Count >= _snapshot.ActionBudget) StartCoroutine(CommitFilledActionField());
        }

        private void SelectTarget(string targetId)
        {
            if (_snapshot == null || !_snapshot.Opponent.LivingActive().Any(fighter => fighter.Id == targetId)) return;
            _preferredTargetId = targetId;
            Debug.Log("Preferred opponent target changed to " + targetId + ".", this);
        }

        private IEnumerator CommitFilledActionField()
        {
            SetControls(false);
            yield return new WaitForSecondsRealtime(0.15f);
            CommitDraft();
        }

        private void ResetDraft()
        {
            _draft?.Reset();
            _tooltipCardId = null;
            Debug.Log("Action field reset.", this);
            RefreshView();
        }

        private void CommitDraft()
        {
            if (_session == null || _isPlayingEvents) return;
            var previousEventId = _snapshot.Events.Count == 0 ? 0 : _snapshot.Events[_snapshot.Events.Count - 1].Id;
            var actions = _draft == null ? 0 : _draft.Actions.Count;
            var plan = _draft == null
                ? new TurnPlan { RequestId = _snapshot.MatchId + ":request:" + (++_requestSequence), ExpectedRevision = _snapshot.Revision }
                : _draft.BuildPlan(_snapshot.MatchId + ":request:" + (++_requestSequence));
            if (!_session.Submit(plan, out var error))
            {
                Debug.LogWarning(error, this);
                return;
            }

            var events = _session.GetEventsAfter(previousEventId);
            _draft = null;
            _tooltipCardId = null;
            Debug.Log("Turn committed with " + actions + " action(s).", this);
            if (actions == 0 && events.Count == 0) RefreshView();
            else StartCoroutine(PlayEvents(events));
        }

        private IEnumerator PlayEvents(IReadOnlyList<BattleEvent> events)
        {
            _isPlayingEvents = true;
            SetControls(false);
            foreach (var item in events)
            {
                Debug.Log(item.Message ?? item.Kind.ToString(), this);
                yield return AnimateEvent(item);
                if (eventDelay > 0f) yield return new WaitForSeconds(eventDelay);
            }
            _isPlayingEvents = false;
            RefreshView();
            if (_snapshot.Phase == BattlePhase.Complete) FinishBattle();
        }

        private IEnumerator AnimateEvent(BattleEvent item)
        {
            if (item.Kind == BattleEventKind.FighterDefeated &&
                _fighterViews.TryGetValue(item.SourceId ?? string.Empty, out var defeatedView) && defeatedView != null)
                defeatedView.SetActive(false);
            if (item.Kind == BattleEventKind.ReserveEntered &&
                _fighterViews.TryGetValue(item.SourceId ?? string.Empty, out var reserveView) && reserveView != null)
            {
                reserveView.SetActive(true);
                var latest = _session.GetSnapshot();
                var fighter = latest.Player.FindFighter(item.SourceId) ?? latest.Opponent.FindFighter(item.SourceId);
                if (fighter != null) PositionView(fighter, reserveView);
            }
            yield return null;
        }

        private void FinishBattle()
        {
            LocalEncounterContext.StoreResult(_session.GetSnapshot());
            SceneManager.LoadScene(returnScene);
        }

        private void RefreshWorldViews()
        {
            foreach (var fighter in _snapshot.Player.Fighters.Concat(_snapshot.Opponent.Fighters))
            {
                if (!_fighterViews.TryGetValue(fighter.Id, out var view) || view == null) continue;
                var visible = fighter.IsAlive && !fighter.IsReserve;
                view.SetActive(visible);
                if (visible) PositionView(fighter, view);
                if (_fighterBillboards.TryGetValue(fighter.Id, out var billboard) && billboard != null)
                {
                    billboard.gameObject.SetActive(visible);
                    billboard.SetCoreHealth(fighter.Health, fighter.Stats.MaxHealth);
                    billboard.SetShield(fighter.Shield, fighter.Stats.MaxHealth);
                    billboard.SetPowerGauge(fighter.PowerGauge);
                }
            }
        }

        private static void PositionView(FighterState fighter, GameObject view)
        {
            var prefix = fighter.Side == TeamSide.Player ? "CharHeroPosition" : "EnemyHeroPosition";
            var anchor = GameObject.Find(prefix + (Mathf.Clamp(fighter.FormationSlot, 0, 2) + 1));
            if (anchor != null) view.transform.SetPositionAndRotation(anchor.transform.position, anchor.transform.rotation);
        }

        private void BindBattleHud()
        {
            if (battleDocument == null) battleDocument = GetComponent<UIDocument>();
            if (battleDocument == null)
            {
                Debug.LogError("Battle card HUD needs a scene UIDocument with BattleCardHud.uxml assigned.", this);
                return;
            }

            var root = battleDocument.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            _handRow = root.Q<VisualElement>("deck-row");
            _actionRow = root.Q<VisualElement>("action-row");
            _tooltipText = root.Q<Label>("card-tooltip");
            _resetButton = root.Q<Button>("reset-button");
            _handSlots.Clear();
            _handContents.Clear();
            _actionSlots.Clear();
            _actionContents.Clear();
            _actionMarkers.Clear();
            for (var index = 0; index < 7; index++)
            {
                var slot = root.Q<VisualElement>("deck-slot-" + index);
                if (slot != null) _handSlots.Add(slot);
                var contents = root.Q<VisualElement>("deck-content-" + index);
                if (contents != null) _handContents.Add(contents);
            }
            for (var index = 0; index < 3; index++)
            {
                var slot = root.Q<VisualElement>("action-slot-" + index);
                if (slot != null) _actionSlots.Add(slot);
                var contents = root.Q<VisualElement>("action-content-" + index);
                if (contents != null) _actionContents.Add(contents);
                var marker = root.Q<Label>("action-marker-" + index);
                if (marker != null) _actionMarkers.Add(marker);
            }

            if (_resetButton != null) _resetButton.clicked += ResetDraft;
            if (_tooltipText != null) _tooltipText.style.display = DisplayStyle.None;
            if (cardTemplate == null)
                Debug.LogError("Battle card HUD needs the BattleCard.uxml template assigned.", this);
        }

        private void AddCardButton(VisualElement parent, CardState card, FighterState owner,
            System.Action onTap, System.Action onHoldStarted, System.Action onHoldEnded)
        {
            if (cardTemplate == null || parent == null) return;
            var cardTree = cardTemplate.CloneTree();
            cardTree.name = "Card " + card.Id;
            cardTree.style.width = Length.Percent(100);
            cardTree.style.height = Length.Percent(100);
            cardTree.style.flexGrow = 1f;
            var button = cardTree.Q<Button>("card-button");
            var artwork = cardTree.Q<Image>("artwork");
            var rankLabel = cardTree.Q<Label>("card-rank");
            if (button == null || artwork == null || rankLabel == null) return;

            var icon = GetCardIcon(card, owner);
            artwork.sprite = icon;
            button.style.backgroundColor = icon == null ? SlotColor(owner) : new Color(0.09f, 0.10f, 0.12f, 1f);
            artwork.tintColor = Color.white;
            artwork.scaleMode = ScaleMode.ScaleToFit;
            rankLabel.text = card.Kind == CardKind.Ultimate ? "ULT" : "R" + Mathf.Clamp(card.Rank, 1, 3);
            button.SetEnabled(onTap != null);
            if (onTap != null)
            {
                var holding = false;
                IVisualElementScheduledItem holdJob = null;
                button.RegisterCallback<PointerDownEvent>(_ =>
                {
                    holding = false;
                    holdJob?.Pause();
                    holdJob = button.schedule.Execute(() =>
                    {
                        holding = true;
                        onHoldStarted?.Invoke();
                    }).StartingIn(420);
                });
                button.RegisterCallback<PointerUpEvent>(_ =>
                {
                    holdJob?.Pause();
                    if (holding) onHoldEnded?.Invoke();
                });
                button.RegisterCallback<PointerLeaveEvent>(_ =>
                {
                    holdJob?.Pause();
                    if (holding) onHoldEnded?.Invoke();
                    holding = false;
                });
                button.clicked += () =>
                {
                    if (holding)
                    {
                        holding = false;
                        return;
                    }
                    onTap();
                };
            }

            parent.Add(cardTree);
        }

        private static Sprite GetCardIcon(CardState card, FighterState owner)
        {
            if (card == null || owner == null) return null;
            var character = LoadCharacter(owner.Definition.Id);
            if (character == null) return null;
            if (card.Kind == CardKind.Ultimate) return character.Ultimate == null ? null : character.Ultimate.icon;
            var skill = owner.Definition.Skills.Find(item => item != null && item.Id == card.SkillId);
            var asset = skill != null && skill.Slot == 2 ? character.Skill2 : character.Skill1;
            return asset == null ? character.FighterIcon : asset.cardIcon;
        }

        private static string GetTooltip(CardState card, FighterState owner)
        {
            if (card == null || owner == null) return string.Empty;
            var character = LoadCharacter(owner.Definition.Id);
            if (card.Kind == CardKind.Ultimate)
            {
                var data = character?.Ultimate?.GetLevelData(card.UltimateTier);
                return (character?.Ultimate?.ultimateName ?? "Ultimate") + (data == null ? string.Empty : "  •  " + data.description);
            }

            var skill = owner.Definition.Skills.Find(item => item != null && item.Id == card.SkillId);
            var asset = skill != null && skill.Slot == 2 ? character?.Skill2 : character?.Skill1;
            var rank = asset?.GetRankData(card.Rank);
            return (asset?.cardName ?? "Skill") + (rank == null ? string.Empty : "  •  " + rank.description);
        }

        private static Color SlotColor(FighterState owner)
        {
            if (owner == null) return new Color(0.25f, 0.25f, 0.28f, 1f);
            switch (Mathf.Clamp(owner.FormationSlot, 0, 2))
            {
                case 0: return new Color(0.18f, 0.48f, 0.84f, 1f);
                case 1: return new Color(0.78f, 0.34f, 0.18f, 1f);
                default: return new Color(0.24f, 0.64f, 0.30f, 1f);
            }
        }

        private static CharacterObject LoadCharacter(string definitionId) =>
            Resources.Load<CharacterObject>("Character_WIP-Phase/" + ShortId(definitionId));

        private static string ShortId(string id) => (id ?? string.Empty).Replace("fighter.", string.Empty);

        private void SetControls(bool enabled)
        {
            if (_resetButton != null) _resetButton.SetEnabled(enabled && _draft != null && _draft.Actions.Count > 0);
        }
    }
}
