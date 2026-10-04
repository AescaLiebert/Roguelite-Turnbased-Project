using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Adapters;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;
using FightingAllstar.Presentation.Route;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using CoreBattleState = FightingAllstar.Core.Combat.BattleState;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Renders the Core battle in the existing 3D scene without owning combat rules.</summary>
    [DefaultExecutionOrder(-10000)]
    public sealed partial class CoreBattleSceneController : MonoBehaviour
    {
        [SerializeField] private string returnScene = "Combat";
        [SerializeField, Range(0f, 1f)] private float eventDelay = 0.25f;
        [SerializeField] private UIDocument battleDocument;
        [SerializeField] private VisualTreeAsset cardTemplate;
        [SerializeField] private Texture2D rankOneFrameTexture;
        [SerializeField] private Texture2D rankTwoFrameTexture;
        [SerializeField] private Texture2D rankThreeFrameTexture;
        [SerializeField] private GameObject damageTotalPanel;
        [SerializeField] private TMP_Text damageTotalValue;

        private IBattleSession _session;
        private CoreBattleState _snapshot;
        private PlanDraft _draft;
        private VisualElement _handRow;
        private VisualElement _actionRow;
        private Label _tooltipText;
        private Button _resetButton;
        private readonly List<VisualElement> _actionSlots = new List<VisualElement>();
        private readonly List<VisualElement> _actionContents = new List<VisualElement>();
        private readonly List<Label> _actionMarkers = new List<Label>();
        private readonly List<Label> _actionTargetBadges = new List<Label>();
        private readonly Dictionary<string, VisualElement> _handCardViews = new Dictionary<string, VisualElement>();
        private string _tooltipCardId;
        private string _preferredTargetId;
        private TargetReticle _targetReticle;
        private bool _isPlayingEvents;
        private int _requestSequence;
        private readonly Dictionary<string, GameObject> _fighterViews = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, CoreFighterHud> _fighterBillboards = new Dictionary<string, CoreFighterHud>();
        private bool _openingSequenceComplete;
        private string _damageTotalOwnerId;
        private long _damageTotalAmount;
        private Coroutine _damageTotalHideCoroutine;

        private void Awake()
        {
            EnableBattlePresentation();
            EnsureTargetReticle();
        }

        private void Update()
        {
            HandleTargetSelectionInput();
        }

        private void EnsureTargetReticle()
        {
            if (_targetReticle == null)
            {
                _targetReticle = FindFirstObjectByType<TargetReticle>(FindObjectsInactive.Include);
                if (_targetReticle == null)
                {
                    var obj = new GameObject("BattleTargetReticle");
                    _targetReticle = obj.AddComponent<TargetReticle>();
                }
            }
        }

        private void Start()
        {
            BindBattleHud();
            if (damageTotalPanel != null) damageTotalPanel.SetActive(false);
            if (damageTotalValue != null)
            {
                damageTotalValue.enableAutoSizing = true;
                damageTotalValue.fontSizeMin = 18f;
                damageTotalValue.fontSizeMax = Mathf.Max(18f, damageTotalValue.fontSizeMax);
                damageTotalValue.textWrappingMode = TextWrappingModes.NoWrap;
                damageTotalValue.overflowMode = TextOverflowModes.Truncate;
            }
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
                _session = new LocalBattleSession(initialState);
                // LocalBattleSession may already have resolved an enemy-first turn. Display the opening state first.
                _snapshot = initialState.Clone();
                _displayState = BattlePlaybackState.BeforeOpeningDeal(initialState);
                SpawnFighterViews(_snapshot);
                _stage = gameObject.AddComponent<BattleStagePresenter>();
                _stage.Initialize(_fighterViews);
                SetBattleHudVisible(false);
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
            openingPresenter?.HideCoreIntro();
            StartCoroutine(CompareCombatClass(playerCC, opponentCC, playerFirst,
                LoadOpeningIcon(encounter.PlayerTeam), LoadOpeningIcon(encounter.EnemyTeam)));
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
                var slot = fighter.IsReserve ? 3 : fighter.FormationSlot;
                var anchorName = anchorPrefix == "Hero" ? "CharHeroPosition" : "EnemyHeroPosition";
                var anchor = GameObject.Find(anchorName + (Mathf.Clamp(slot, 0, 2) + 1));
                var position = anchor == null ? new Vector3(anchorPrefix == "Hero" ? -3f : 3f, 0f, 0f) : anchor.transform.position;
                var rotation = anchor == null ? Quaternion.identity : anchor.transform.rotation;
                var view = character != null && character.Fighter3DPrefab != null
                    ? Instantiate(character.Fighter3DPrefab, position, rotation)
                    : CreatePlaceholder(fighter, position, rotation);
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
                box.center = new Vector3(0f, 1f, 0f);
                box.size = new Vector3(1.6f, 2.2f, 1.6f);
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
                StatusSystem.GetEffectiveStats(fighter).MaxHealth, fighter.Shield, fighter.PowerGauge);
            if (hud != null) _fighterBillboards[fighter.Id] = hud;
        }

        private void RefreshView()
        {
            if (_session == null) return;
            if (_openingSequenceComplete && !_isPlayingEvents && !_isAnimatingCards)
            {
                _snapshot = _session.GetSnapshot();
                _displayState = _snapshot.Clone();
            }
            RefreshWorldViews();
            RenderHand();
            RenderActionField();
            RenderTooltip();
            UpdateTargetVisual();

            var playerPlanning = _openingSequenceComplete && !_isPlayingEvents && !_isAnimatingCards &&
                _snapshot.Phase == BattlePhase.Planning && _snapshot.ActingSide == TeamSide.Player;
            SetControls(playerPlanning);
            if (_resetButton != null) _resetButton.SetEnabled(playerPlanning && _draft != null && _draft.Actions.Count > 0);

            if (_snapshot.Phase == BattlePhase.Complete)
                Debug.Log(_snapshot.IsDraw ? "Battle draw." : _snapshot.Winner == TeamSide.Player ? "Battle victory." : "Battle defeat.", this);
        }

        private void RenderHand()
        {
            if (_handRow == null) return;
            if (!_hudRevealed || _snapshot == null)
            {
                ClearHandViews();
                return;
            }

            var team = _isPlayingEvents || _isAnimatingCards || !_openingSequenceComplete
                ? _displayState.Player : _draft == null ? _snapshot.Player : _draft.Preview;
            var presentIds = new HashSet<string>();
            for (var index = team.Hand.Count - 1; index >= 0; index--)
            {
                var card = team.Hand[index];
                var capturedId = card.Id;
                presentIds.Add(capturedId);
                var owner = team.FindFighter(card.OwnerFighterId);
                if (_handCardViews.TryGetValue(capturedId, out var view) && view != null)
                {
                    UpdateCardButton(view, card, owner);
                }
                else
                {
                    view = AddCardButton(_handRow, card, owner,
                        () => QueueCard(capturedId), () => ShowTooltip(capturedId), HideTooltip,
                        destination => QueueMoveTo(capturedId, destination));
                    if (view != null) _handCardViews[capturedId] = view;
                }
            }

            var removedIds = _handCardViews.Keys.Where(id => !presentIds.Contains(id)).ToList();
            foreach (var id in removedIds)
            {
                if (_handCardViews[id] != null) _handCardViews[id].RemoveFromHierarchy();
                _handCardViews.Remove(id);
            }
            // Later appended cards appear on the left; existing card views stay stable.
            for (var index = team.Hand.Count - 1; index >= 0; index--)
            {
                var view = _handCardViews[team.Hand[team.Hand.Count - 1 - index].Id];
                if (view.parent != _handRow || _handRow.IndexOf(view) != index) _handRow.Insert(index, view);
            }
            if (!string.IsNullOrEmpty(_tooltipCardId) && !presentIds.Contains(_tooltipCardId)) HideTooltip();
            LayoutHandCards();
        }

        private void ClearHandViews()
        {
            foreach (var view in _handCardViews.Values)
                if (view != null) view.RemoveFromHierarchy();
            _handCardViews.Clear();
        }

        private void LayoutHandCards()
        {
            if (_handRow == null) return;
            var count = _handRow.childCount;
            if (count == 0) return;
            var rowWidth = _handRow.resolvedStyle.width;
            if (rowWidth <= 0f) return;

            // 7DSGC-style closer deck slots:
            // Preferred card width around 88px.
            // For 1-3 cards: slight gap (4px).
            // For 4-8 cards: tighter spacing with negative overlap (-6px to -14px) so cards sit nicely clustered in hand.
            const float preferredCardWidth = 88f;
            var cardWidth = preferredCardWidth;
            var gap = count <= 3 ? 4f : Mathf.Lerp(1f, -14f, Mathf.Clamp01((count - 3) / 5f));

            var totalWidth = cardWidth * count + gap * (count - 1);
            if (totalWidth > rowWidth && rowWidth > 50f)
            {
                cardWidth = Mathf.Max(42f, (rowWidth - gap * (count - 1)) / count);
                totalWidth = cardWidth * count + gap * (count - 1);
            }

            var left = Mathf.Max(0f, (rowWidth - totalWidth) * 0.5f);
            for (var index = 0; index < count; index++)
            {
                var card = _handRow[index];
                card.style.left = left + index * (cardWidth + gap);
                card.style.width = cardWidth;
            }
        }

        private void RenderActionField()
        {
            if (_actionRow == null) return;
            if (_isPlayingEvents) return;
            _actionRow.style.flexDirection = FlexDirection.RowReverse;
            foreach (var contents in _actionContents) contents.Clear();
            foreach (var marker in _actionMarkers) marker.style.display = DisplayStyle.Flex;
            foreach (var badge in _actionTargetBadges) badge.style.display = DisplayStyle.None;
            if (_snapshot == null) return;

            var actions = _draft == null ? new List<PlannedAction>() : _draft.Actions.ToList();
            var visibleSlotCount = _snapshot.ActingSide == TeamSide.Player && _snapshot.ActionBudget > 0
                ? _snapshot.ActionBudget
                : (_snapshot.Player.LivingActive().Count > 2 ? 3 : 2);
            for (var index = 0; index < _actionSlots.Count; index++)
            {
                var slot = _actionSlots[index];
                var actionIndex = _actionSlots.Count - 1 - index;
                slot.style.display = actionIndex < visibleSlotCount ? DisplayStyle.Flex : DisplayStyle.None;
                if (index < _actionMarkers.Count)
                {
                    _actionMarkers[index].text = (actionIndex + 1).ToString();
                    _actionMarkers[index].RemoveFromClassList("move-marker");
                }
                if (index < _actionTargetBadges.Count)
                {
                    _actionTargetBadges[index].style.display = DisplayStyle.None;
                }
                if (actionIndex >= visibleSlotCount) continue;
                if (actionIndex >= actions.Count)
                {
                    continue;
                }

                if (actions[actionIndex].IsMove)
                {
                    if (index < _actionMarkers.Count)
                    {
                        _actionMarkers[index].text = "MOVE";
                        _actionMarkers[index].AddToClassList("move-marker");
                    }
                    continue;
                }

                var card = actionIndex < _draftCards.Count ? _draftCards[actionIndex] :
                    _snapshot.Player.Hand.Find(item => item.Id == actions[actionIndex].CardId);
                var owner = card == null ? null : _snapshot.Player.FindFighter(card.OwnerFighterId);
                if (card != null)
                {
                    AddCardButton(_actionContents[index], card, owner, null, null, null);
                    _actionMarkers[index].style.display = DisplayStyle.None;

                    if (index < _actionTargetBadges.Count && !string.IsNullOrEmpty(actions[actionIndex].TargetFighterId))
                    {
                        var targetFighter = _snapshot.Opponent.FindFighter(actions[actionIndex].TargetFighterId);
                        var posLabel = targetFighter != null ? "Pos " + (targetFighter.FormationSlot + 1) : ShortId(actions[actionIndex].TargetFighterId);
                        _actionTargetBadges[index].text = posLabel;
                        _actionTargetBadges[index].style.display = DisplayStyle.Flex;
                    }
                }
            }
        }

        private void RenderTooltip()
        {
            if (_tooltipText == null) return;
            var hand = _draft == null ? _snapshot?.Player : _draft.Preview;
            var card = string.IsNullOrEmpty(_tooltipCardId) || hand == null
                ? null : hand.Hand.Find(item => item.Id == _tooltipCardId);
            if (card == null)
            {
                _tooltipText.style.display = DisplayStyle.None;
                return;
            }

            var owner = hand.FindFighter(card.OwnerFighterId);
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
            if (!CanPlan) return;
            if (_draft == null) _draft = new PlanDraft(_snapshot);
            var before = _draft.Preview.Clone();
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
            _draftCards.Add(_draft.LastEvents[0].Card.Clone());
            StartCoroutine(AnimateDraft(before, _draft.LastEvents.Select(e => e.Clone()).ToList()));
        }

        private void SelectTarget(string targetId)
        {
            if (!CanPlan) return;
            var fighter = _snapshot.Opponent.LivingActive().FirstOrDefault(item => item.Id == targetId);
            if (fighter == null) return;
            _preferredTargetId = targetId;
            UpdateTargetVisual();
            Debug.Log("Preferred opponent target changed to " + targetId + " (Pos " + (fighter.FormationSlot + 1) + ").", this);
        }

        private void HandleTargetSelectionInput()
        {
            if (!CanPlan) return;
            if (_snapshot.Phase != BattlePhase.Planning || _snapshot.ActingSide != TeamSide.Player) return;

            var livingOpponents = _snapshot.Opponent.LivingActive().ToList();
            if (livingOpponents.Count == 0) return;

            // Ensure a valid target is selected by default during drafting
            if (string.IsNullOrEmpty(_preferredTargetId) || !livingOpponents.Any(f => f.Id == _preferredTargetId))
            {
                SelectTarget(livingOpponents[0].Id);
            }

            var isPointerDown = Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
            if (!isPointerDown) return;

            var pointerPos = Input.touchCount > 0 ? (Vector3)Input.GetTouch(0).position : Input.mousePosition;

            // Ignore input if tapping within the bottom card tray
            if (pointerPos.y < Screen.height * 0.36f) return;

            var mainCam = Camera.main;
            if (mainCam == null) return;

            // 1. Raycast for 3D physics colliders
            var ray = mainCam.ScreenPointToRay(pointerPos);
            if (Physics.Raycast(ray, out var hit, 100f))
            {
                var targetComp = hit.collider.GetComponentInParent<CoreFighterTarget>();
                if (targetComp != null && !string.IsNullOrEmpty(targetComp.FighterId))
                {
                    SelectTarget(targetComp.FighterId);
                    return;
                }
            }

            // 2. Screen-space proximity fallback to allow comfortable taps near enemy models
            float nearestDist = 120f;
            string nearestId = null;
            foreach (var fighter in livingOpponents)
            {
                if (_fighterViews.TryGetValue(fighter.Id, out var view) && view != null && view.activeInHierarchy)
                {
                    var screenPos = mainCam.WorldToScreenPoint(view.transform.position + new Vector3(0f, 1f, 0f));
                    if (screenPos.z > 0f)
                    {
                        var dist = Vector2.Distance(new Vector2(pointerPos.x, pointerPos.y), new Vector2(screenPos.x, screenPos.y));
                        if (dist < nearestDist)
                        {
                            nearestDist = dist;
                            nearestId = fighter.Id;
                        }
                    }
                }
            }

            if (!string.IsNullOrEmpty(nearestId))
            {
                SelectTarget(nearestId);
            }
        }

        private void UpdateTargetVisual()
        {
            if (_targetReticle == null) return;

            var isPlanning = _openingSequenceComplete && !_isPlayingEvents && !_isAnimatingCards &&
                _snapshot != null && _snapshot.Phase == BattlePhase.Planning &&
                _snapshot.ActingSide == TeamSide.Player;

            if (!isPlanning)
            {
                _targetReticle.SetVisible(false);
                return;
            }

            var livingOpponents = _snapshot.Opponent.LivingActive().ToList();
            if (livingOpponents.Count == 0)
            {
                _targetReticle.SetVisible(false);
                return;
            }

            var targetFighter = livingOpponents.FirstOrDefault(f => f.Id == _preferredTargetId) ?? livingOpponents[0];
            _preferredTargetId = targetFighter.Id;

            if (_fighterViews.TryGetValue(_preferredTargetId, out var targetView) && targetView != null && targetView.activeInHierarchy)
            {
                _targetReticle.AttachTo(targetView.transform);
            }
            else
            {
                _targetReticle.SetVisible(false);
            }
        }

        private IEnumerator CommitFilledActionField()
        {
            SetControls(false);
            yield return new WaitForSecondsRealtime(0.15f);
            CommitDraft();
        }

        private void ResetDraft()
        {
            if (!CanPlan) return;
            _draft?.Reset();
            _draftCards.Clear();
            _tooltipCardId = null;
            Debug.Log("Action field reset.", this);
            RefreshView();
            UpdateTargetVisual();
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
                _commitPending = false;
                RefreshView();
                return;
            }

            var events = _session.GetEventsAfter(previousEventId);
            _displayState = _snapshot.Clone();
            if (_draft != null)
            {
                _displayState.Player.Hand = _draft.Preview.Hand.Select(c => c.Clone()).ToList();
            }
            _suppressDraftCardPlayback = true;
            _draft = null;
            _draftCards.Clear();
            _tooltipCardId = null;
            Debug.Log("Turn committed with " + actions + " action(s).", this);
            if (actions == 0 && events.Count == 0) RefreshView();
            else StartCoroutine(PlayEvents(events));
        }

        private IEnumerator PlayEvents(IReadOnlyList<BattleEvent> events)
        {
            _isPlayingEvents = true;
            SetControls(false);
            if (_targetReticle != null) _targetReticle.SetVisible(false);
            _executionIndex = -1;
            for (var index = 0; index < events.Count; index++)
            {
                var item = events[index];
                if (IsExecutionEvent(item) && _executionIndex < 0) PrepareExecution(events, index);
                yield return PresentEvent(item);
            }
            yield return _stage.Turn(TeamSide.Player);
            _openingSequenceComplete = true;
            _isPlayingEvents = false;
            _commitPending = false;
            _suppressDraftCardPlayback = false;
            _executionIndex = -1;
            SetPhase(BattlePresentationPhase.Planning);
            RefreshView();
            UpdateTargetVisual();
            if (_snapshot.Phase == BattlePhase.Complete) ShowBattleResult();
        }

        private void FinishBattle()
        {
            if (ExitPreview()) return;
            LocalEncounterContext.StoreResult(_session.GetSnapshot());
            SceneManager.LoadScene(returnScene);
        }

        private void SurrenderRun()
        {
            if (ExitPreview()) return;
            var store = new LocalRunStateStore();
            store.Clear();
            LocalEncounterContext.Clear();
            DungeonFlowContext.Clear();
            SceneManager.LoadScene(returnScene);
        }

        private bool ExitPreview()
        {
            if (!LocalEncounterContext.IsPreview) return false;
            LocalEncounterContext.Clear();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            SceneManager.LoadScene(returnScene);
#endif
            return true;
        }

        private void RefreshWorldViews()
        {
            var display = _displayState ?? _snapshot;
            foreach (var fighter in display.Player.Fighters.Concat(display.Opponent.Fighters))
            {
                if (!_fighterViews.TryGetValue(fighter.Id, out var view) || view == null) continue;
                var visible = fighter.IsAlive && !fighter.IsReserve;
                view.SetActive(visible);
                if (visible) PositionView(fighter, view);
                if (_fighterBillboards.TryGetValue(fighter.Id, out var billboard) && billboard != null)
                {
                    var isPlayerDraft = fighter.Side == TeamSide.Player && _draft != null && !_isPlayingEvents;
                    var trueFighter = isPlayerDraft ? _snapshot.Player.FindFighter(fighter.Id) : null;
                    var truePG = trueFighter?.PowerGauge ?? fighter.PowerGauge;
                    var draftPG = isPlayerDraft ? (_draft.Preview.FindFighter(fighter.Id)?.PowerGauge ?? truePG) : truePG;

                    billboard.gameObject.SetActive(visible && _hudRevealed);
                    billboard.SetCoreHealth(fighter.Health, StatusSystem.GetEffectiveStats(fighter).MaxHealth);
                    billboard.SetShield(fighter.Shield, StatusSystem.GetEffectiveStats(fighter).MaxHealth);
                    if (isPlayerDraft)
                    {
                        billboard.SetPowerGauge(truePG, draftPG);
                    }
                    else
                    {
                        billboard.SetPowerGauge(fighter.PowerGauge);
                    }
                    billboard.SetStatuses(fighter.Statuses?.Instances);
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
            BindPresentationHud(root);
            _handRow = root.Q<VisualElement>("deck-row");
            if (_handRow != null)
            {
                _handRow.style.position = Position.Relative;
                _handRow.RegisterCallback<GeometryChangedEvent>(_ => LayoutHandCards());
            }
            _actionRow = root.Q<VisualElement>("action-row");
            _tooltipText = root.Q<Label>("card-tooltip");
            _resetButton = root.Q<Button>("reset-button");
            _actionSlots.Clear();
            _actionContents.Clear();
            _actionMarkers.Clear();
            _actionTargetBadges.Clear();
            for (var index = 0; index < 3; index++)
            {
                var slot = root.Q<VisualElement>("action-slot-" + index);
                if (slot != null)
                {
                    _actionSlots.Add(slot);
                    var badge = new Label { name = "action-target-" + index };
                    badge.AddToClassList("action-target-badge");
                    badge.style.display = DisplayStyle.None;
                    badge.pickingMode = PickingMode.Ignore;
                    slot.Add(badge);
                    _actionTargetBadges.Add(badge);
                }
                var contents = root.Q<VisualElement>("action-content-" + index);
                if (contents != null) _actionContents.Add(contents);
                var marker = root.Q<Label>("action-marker-" + index);
                if (marker != null) _actionMarkers.Add(marker);
            }

            if (_resetButton != null) _resetButton.clicked += ResetDraft;
            var surrenderButton = root.Q<Button>("surrender-button");
            if (surrenderButton != null) surrenderButton.clicked += SurrenderRun;
            if (_tooltipText != null) _tooltipText.style.display = DisplayStyle.None;
            if (cardTemplate == null)
                Debug.LogError("Battle card HUD needs the BattleCard.uxml template assigned.", this);
        }

        private VisualElement AddCardButton(VisualElement parent, CardState card, FighterState owner,
            System.Action onTap, System.Action onHoldStarted, System.Action onHoldEnded,
            System.Action<int> onDrop = null)
        {
            if (cardTemplate == null || parent == null) return null;
            var cardTree = cardTemplate.CloneTree();
            cardTree.name = "Card " + card.Id;
            var isHandCard = parent == _handRow;
            if (isHandCard) cardTree.AddToClassList("hand-card");
            if (isHandCard) cardTree.style.width = 92;
            else cardTree.style.width = Length.Percent(100);
            cardTree.style.height = Length.Percent(100);
            if (isHandCard)
            {
                cardTree.style.position = Position.Absolute;
                cardTree.style.top = 0;
                cardTree.style.bottom = 0;
                cardTree.style.opacity = 0f;
                cardTree.schedule.Execute(() =>
                {
                    if (cardTree.panel != null) cardTree.style.opacity = 1f;
                }).StartingIn(20);
            }
            var button = cardTree.Q<Button>("card-button");
            var artwork = cardTree.Q<Image>("artwork");
            var rankFrame = cardTree.Q<Image>("card-rank-frame");
            var skillSlotLabel = cardTree.Q<Label>("card-skill-slot");
            if (button == null || artwork == null || rankFrame == null || skillSlotLabel == null) return null;

            UpdateCardButton(cardTree, card, owner);
            button.SetEnabled(onTap != null);
            if (onTap != null)
            {
                var holding = false;
                var dragging = false;
                var pointerStart = Vector3.zero;
                var pointerId = -1;
                IVisualElementScheduledItem holdJob = null;
                button.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (!CanPlan) return;
                    holding = false;
                    dragging = false;
                    pointerStart = evt.position;
                    pointerId = evt.pointerId;
                    button.CapturePointer(pointerId);
                    holdJob?.Pause();
                    holdJob = button.schedule.Execute(() =>
                    {
                        holding = true;
                        onHoldStarted?.Invoke();
                    }).StartingIn(420);
                    evt.StopPropagation();
                }, TrickleDown.TrickleDown);
                button.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (pointerId != evt.pointerId) return;
                    holdJob?.Pause();
                    if (pointerId >= 0 && button.HasPointerCapture(pointerId)) button.ReleasePointer(pointerId);
                    pointerId = -1;
                    var wasDragging = dragging;
                    var wasHolding = holding;
                    var destination = 0;
                    if (dragging)
                    {
                        var count = _draft == null ? _snapshot?.Player.Hand.Count ?? 0 : _draft.Preview.Hand.Count;
                        var visualDestination = 0;
                        var nearestDistance = float.MaxValue;
                        var pointerX = _handRow == null ? 0f : _handRow.WorldToLocal(new Vector2(evt.position.x, evt.position.y)).x;
                        for (var index = 0; _handRow != null && index < _handRow.childCount; index++)
                        {
                            // Compare layout slots, not the dragged card's translated bounds.
                            var slotCenter = _handRow[index].resolvedStyle.left + _handRow[index].resolvedStyle.width * .5f;
                            var distance = Mathf.Abs(pointerX - slotCenter);
                            if (distance >= nearestDistance) continue;
                            nearestDistance = distance;
                            visualDestination = index;
                        }
                        visualDestination = Mathf.Clamp(visualDestination, 0, Mathf.Max(0, count - 1));
                        destination = count - 1 - visualDestination;
                    }
                    holding = false;
                    dragging = false;
                    cardTree.style.translate = new Translate(0, 0);
                    evt.StopPropagation();
                    if (!CanPlan) return;
                    if (wasDragging) onDrop?.Invoke(destination);
                    else if (wasHolding)
                    {
                        onHoldEnded?.Invoke();
                    }
                    else onTap();
                }, TrickleDown.TrickleDown);
                button.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (pointerId != evt.pointerId || holding || !CanPlan) return;
                    if ((evt.position - pointerStart).sqrMagnitude > 64f)
                    {
                        dragging = true;
                        holdJob?.Pause();
                        cardTree.style.translate = new Translate(evt.position.x - pointerStart.x, -18);
                    }
                });
                button.RegisterCallback<PointerCancelEvent>(evt =>
                {
                    holdJob?.Pause();
                    pointerId = -1;
                    holding = dragging = false;
                    cardTree.style.translate = new Translate(0, 0);
                    onHoldEnded?.Invoke();
                });
            }

            parent.Add(cardTree);
            return cardTree;
        }

        private void UpdateCardButton(VisualElement cardTree, CardState card, FighterState owner)
        {
            var button = cardTree.Q<Button>("card-button");
            var artwork = cardTree.Q<Image>("artwork");
            var rankFrame = cardTree.Q<Image>("card-rank-frame");
            var skillSlotLabel = cardTree.Q<Label>("card-skill-slot");
            if (button == null || artwork == null || rankFrame == null || skillSlotLabel == null) return;
            var icon = GetCardIcon(card, owner);
            artwork.sprite = icon;
            button.style.backgroundColor = icon == null ? SlotColor(owner) : new Color(0.09f, 0.10f, 0.12f, 1f);
            artwork.tintColor = Color.white;
            artwork.scaleMode = ScaleMode.ScaleAndCrop;

            var skillTypeImage = cardTree.Q<Image>("card-skill-type");
            var holoGlow = cardTree.Q<VisualElement>("card-holo-glow");

            rankFrame.image = card.Kind == CardKind.Ultimate ? rankThreeFrameTexture : RankFrameTexture(card.Rank);
            rankFrame.scaleMode = ScaleMode.StretchToFill;
            rankFrame.style.display = rankFrame.image == null ? DisplayStyle.None : DisplayStyle.Flex;

            if (card.Kind == CardKind.Ultimate)
            {
                cardTree.AddToClassList("ultimate-card");
                skillSlotLabel.text = "ULT";
                if (holoGlow != null) holoGlow.style.display = DisplayStyle.Flex;
                if (skillTypeImage != null)
                {
                    skillTypeImage.sprite = LoadSkillTypeSprite(SkillType.Attack);
                    skillTypeImage.style.display = DisplayStyle.Flex;
                }
            }
            else
            {
                cardTree.RemoveFromClassList("ultimate-card");
                if (holoGlow != null) holoGlow.style.display = DisplayStyle.None;
                var skill = owner?.Definition?.Skills?.Find(item => item != null && item.Id == card.SkillId);
                skillSlotLabel.text = skill != null && skill.Slot == 2 ? "2" : "1";
                var type = GetCardSkillType(card, owner);
                var typeSprite = LoadSkillTypeSprite(type);
                if (skillTypeImage != null)
                {
                    skillTypeImage.sprite = typeSprite;
                    skillTypeImage.style.display = typeSprite != null ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
        }

        private static readonly Dictionary<SkillType, Sprite> _skillTypeSprites = new Dictionary<SkillType, Sprite>();

        private static SkillType GetCardSkillType(CardState card, FighterState owner)
        {
            if (card == null || owner == null) return SkillType.Attack;
            if (card.Kind == CardKind.Ultimate) return SkillType.Attack;
            return card.Category switch
            {
                CardCategory.Buff => SkillType.Buff,
                CardCategory.Debuff => SkillType.Debuff,
                CardCategory.AttackDebuff => SkillType.DebuffAtk,
                CardCategory.Recovery => SkillType.Heal,
                CardCategory.Stance => SkillType.Stance,
                _ => SkillType.Attack
            };
        }

        private static Sprite LoadSkillTypeSprite(SkillType type)
        {
            if (_skillTypeSprites.TryGetValue(type, out var cached) && cached != null)
                return cached;

            var fileName = type switch
            {
                SkillType.Attack => "Cardtype_Attack",
                SkillType.Buff => "Cardtype_buff",
                SkillType.Debuff => "Cardtype_Debuff",
                SkillType.DebuffAtk => "Cardtype_Debuffatk",
                SkillType.Heal => "Cardtype_Heal",
                SkillType.Stance => "Cardtype_Stance",
                _ => "Cardtype_Attack"
            };

#if UNITY_EDITOR
            var edSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Art/UI/" + fileName + ".png");
            if (edSprite != null)
            {
                _skillTypeSprites[type] = edSprite;
                return edSprite;
            }
#endif
            var res = Resources.Load<Sprite>("UI/" + fileName);
            if (res != null)
            {
                _skillTypeSprites[type] = res;
                return res;
            }

            var filePath = System.IO.Path.Combine(Application.dataPath, "Project", "Art", "UI", fileName + ".png");
            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    var bytes = System.IO.File.ReadAllBytes(filePath);
                    var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes))
                    {
                        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                        _skillTypeSprites[type] = sprite;
                        return sprite;
                    }
                }
                catch { }
            }
            return null;
        }

        private Texture2D RankFrameTexture(int rank) => Mathf.Clamp(rank, 1, 3) switch
        {
            1 => rankOneFrameTexture,
            2 => rankTwoFrameTexture,
            _ => rankThreeFrameTexture
        };

        private void QueueMoveTo(string cardId, int destination)
        {
            if (!CanPlan) return;
            if (_draft == null) _draft = new PlanDraft(_snapshot);
            var before = _draft.Preview.Clone();
            if (!_draft.QueueMove(cardId, destination, out var error))
            {
                Debug.LogWarning(error, this);
                return;
            }
            _tooltipCardId = null;
            _draftCards.Add(null);
            StartCoroutine(AnimateDraft(before, _draft.LastEvents.Select(e => e.Clone()).ToList()));
        }

        private static Sprite GetCardIcon(CardState card, FighterState owner)
        {
            if (card == null || owner == null) return null;
            var character = LoadCharacter(owner.Definition.Id);
            if (character == null) return null;
            if (card.Kind == CardKind.Ultimate)
                return character.Ultimate != null && character.Ultimate.icon != null ? character.Ultimate.icon : character.FighterIcon;
            var skill = owner.Definition.Skills.Find(item => item != null && item.Id == card.SkillId);
            var asset = skill != null && skill.Slot == 2 ? character.Skill2 : character.Skill1;
            return asset != null && asset.cardIcon != null ? asset.cardIcon : character.FighterIcon;
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
            if (_handRow != null) _handRow.SetEnabled(enabled);
            if (_commitButton != null) _commitButton.SetEnabled(enabled);
        }
    }
}
