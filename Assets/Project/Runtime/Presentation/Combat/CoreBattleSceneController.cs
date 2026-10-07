using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
        private const string RecoveryBlockedMessage = "Can't Recovery";
        private const string HealingCardBlockedMessage = "Can't Use Healing Card";

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
        [NonSerialized] private CoreBattleState _snapshot;
        private PlanDraft _draft;
        private VisualElement _handRow;
        private VisualElement _actionRow;
        private VisualElement _tooltipContainer;
        private Image _tooltipCharacterIcon;
        private Image _tooltipTypeIcon;
        private Label _tooltipCardTitle;
        private VisualElement _tooltipStatusGrid;
        private Label _tooltipCardDesc;
        private Label _tooltipKeywordsDesc;
        private Label _tooltipText;
        private Button _resetButton;
        private readonly List<VisualElement> _actionSlots = new List<VisualElement>();
        private readonly List<VisualElement> _actionContents = new List<VisualElement>();
        private readonly List<Label> _actionMarkers = new List<Label>();
        private readonly List<Label> _actionTargetBadges = new List<Label>();
        private readonly Dictionary<string, VisualElement> _handCardViews = new Dictionary<string, VisualElement>();
        private string _tooltipCardId;
        private string _preferredTargetId;
        private bool _targetPointerStarted;
        private Vector2 _targetPointerOrigin;
        private TargetReticle _targetReticle;
        private bool _isPlayingEvents;
        private int _requestSequence;
        private readonly Dictionary<string, GameObject> _fighterViews = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, CoreFighterHud> _fighterBillboards = new Dictionary<string, CoreFighterHud>();
        private bool _openingSequenceComplete;
        private string _damageTotalOwnerId;
        private long _damageTotalAmount;
        private Coroutine _damageTotalHideCoroutine;
        private const float CardWidth = 104f;
        private const float CardHeight = 160f;

        private void Awake()
        {
            EnableBattlePresentation();
            EnsureTargetReticle();
        }

        private void Update()
        {
            HandleInspectorInput();
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
            FightingAllstar.Core.Combat.AI.EnemyAiPlanner.LogCallback = msg => Debug.Log(msg, this);
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
                EnsureFighterAttributes(encounter);
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

        private static void EnsureFighterAttributes(EncounterProjection encounter)
        {
            if (encounter == null) return;
            if (encounter.PlayerTeam != null)
            {
                foreach (var f in encounter.PlayerTeam)
                {
                    if (f?.Definition != null && string.IsNullOrEmpty(f.Definition.AttributeId))
                    {
                        var co = LoadCharacter(f.Definition.Id);
                        if (co != null) f.Definition.AttributeId = "attribute." + co.FighterAttribute.ToString().ToLowerInvariant();
                    }
                }
            }
            if (encounter.EnemyTeam != null)
            {
                foreach (var f in encounter.EnemyTeam)
                {
                    if (f?.Definition != null && string.IsNullOrEmpty(f.Definition.AttributeId))
                    {
                        var co = LoadCharacter(f.Definition.Id);
                        if (co != null) f.Definition.AttributeId = "attribute." + co.FighterAttribute.ToString().ToLowerInvariant();
                    }
                }
            }
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
                if (character != null && string.IsNullOrEmpty(fighter.Definition?.AttributeId))
                    fighter.Definition.AttributeId = "attribute." + character.FighterAttribute.ToString().ToLowerInvariant();
                var slot = fighter.IsReserve ? 3 : fighter.FormationSlot;
                var anchorName = anchorPrefix == "Hero" ? "CharHeroPosition" : "EnemyHeroPosition";
                var anchor = GameObject.Find(anchorName + (Mathf.Clamp(slot, 0, 2) + 1));
                var position = anchor == null ? new Vector3(anchorPrefix == "Hero" ? -3f : 3f, 0f, 0f) : anchor.transform.position;
                var rotation = anchor == null ? Quaternion.identity : anchor.transform.rotation;
                var view = character != null && character.Fighter3DPrefab != null
                    ? Instantiate(character.Fighter3DPrefab, position, rotation)
                    : CreatePlaceholder(fighter, position, rotation);
                view.name = "CoreFighter_" + fighter.Id;
                WireOpponentTarget(view, fighter.Id);
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
            var attrId = fighter.Definition?.AttributeId;
            var level = 1;
            var character = LoadCharacter(fighter.Definition?.Id);
            if (character != null)
            {
                if (character.FighterLevel > 0) level = character.FighterLevel;
                if (string.IsNullOrEmpty(attrId))
                {
                    attrId = "attribute." + character.FighterAttribute.ToString().ToLowerInvariant();
                    if (fighter.Definition != null) fighter.Definition.AttributeId = attrId;
                }
            }
            var hud = factory.CreateFighterHud(target, ShortId(fighter.Definition.Id), fighter.Health,
                StatusSystem.GetEffectiveStats(fighter).MaxHealth, fighter.Shield, fighter.PowerGauge, attrId, level);
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
            SyncUltimateReady();
            RenderHand();
            RenderEnemyHand();
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

            // 7DSGC-style fixed-size cards aligned to the right side of the screen.
            // Card dimensions NEVER shrink or grow regardless of hand count.
            // Overlap adjusts smoothly across available width:
            // 1-3 cards: slight gap (+3px) for side-by-side display matching reference.
            // 4-8 cards: gradual overlap (-10px to -28px) so cards cluster cleanly like 7DSGC hand.
            const float rightMargin = 8f;
            const float minLeft = 12f;
            var availableWidth = Mathf.Max(CardWidth, rowWidth - rightMargin - minLeft);

            float preferredStep;
            if (count <= 1)
            {
                preferredStep = CardWidth;
            }
            else if (count <= 3)
            {
                preferredStep = CardWidth + 3f;
            }
            else
            {
                preferredStep = Mathf.Lerp(CardWidth - 10f, CardWidth - 28f, Mathf.Clamp01((count - 4) / 4f));
            }

            // Cap step so hand never overflows the row, but never shrink card dimensions
            var maxStep = count > 1 ? (availableWidth - CardWidth) / (count - 1) : preferredStep;
            var step = count > 1 ? Mathf.Min(preferredStep, maxStep) : 0f;

            var totalWidth = CardWidth + (count - 1) * step;
            var left = Mathf.Max(minLeft, rowWidth - totalWidth - rightMargin);

            for (var index = 0; index < count; index++)
            {
                var card = _handRow[index];
                card.style.position = Position.Absolute;
                card.style.left = left + index * step;
                card.style.width = CardWidth;
                card.style.height = CardHeight;
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
            var hand = _draft == null ? _snapshot?.Player : _draft.Preview;
            var card = string.IsNullOrEmpty(_tooltipCardId) || hand == null
                ? null : hand.Hand.Find(item => item.Id == _tooltipCardId);
            if (card == null)
            {
                if (_tooltipContainer != null) _tooltipContainer.style.display = DisplayStyle.None;
                if (_tooltipText != null) _tooltipText.style.display = DisplayStyle.None;
                return;
            }

            var owner = hand.FindFighter(card.OwnerFighterId);
            if (_tooltipContainer != null)
            {
                PopulateTooltipView(card, owner);
                _tooltipContainer.style.display = DisplayStyle.Flex;
            }
            else if (_tooltipText != null)
            {
                _tooltipText.text = GetTooltip(card, owner);
                _tooltipText.style.display = DisplayStyle.Flex;
            }
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

        private VisualElement _allyPicker;

        private void QueueCard(string cardId) => QueueCardTarget(cardId, null);

        private void QueueCardTarget(string cardId, string allyId)
        {
            if (!CanPlan) return;
            if (_draft == null) _draft = new PlanDraft(_snapshot);
            var card = _draft.Preview.Hand.Find(c => c.Id == cardId);
            if (card == null) return;
            if (card.TargetScope == EffectTargetScope.SelectedAlly && allyId == null)
            {
                _allyPicker?.RemoveFromHierarchy();
                _allyPicker = new VisualElement { name = "ally-target-picker" };
                _allyPicker.AddToClassList("ally-target-picker");
                _allyPicker.Add(new Label("Choose an ally"));
                foreach (var ally in _snapshot.Player.LivingActive())
                {
                    var id = ally.Id;
                    var button = new Button(() => { _allyPicker?.RemoveFromHierarchy(); _allyPicker = null; QueueCardTarget(cardId, id); });
                    button.AddToClassList("ally-choice");
                    var portrait = new Image { sprite = LoadCharacter(ally.Definition.Id)?.FighterIcon };
                    portrait.style.width = 48; portrait.style.height = 48;
                    button.Add(portrait);
                    var copy = new VisualElement();
                    copy.AddToClassList("ally-choice-copy");
                    copy.Add(new Label(ally.Definition.DisplayName));
                    copy.Add(new Label(ally.Health + " / " + StatusSystem.GetEffectiveStats(ally).MaxHealth + " HP"));
                    button.Add(copy);
                    _allyPicker.Add(button);
                }
                _allyPicker.Add(new Button(() => { _allyPicker?.RemoveFromHierarchy(); _allyPicker = null; }) { text = "Cancel" });
                _effectsLayer.Add(_allyPicker);
                return;
            }
            _allyPicker?.RemoveFromHierarchy(); _allyPicker = null;
            var before = _draft.Preview.Clone();
            var target = card.TargetScope == EffectTargetScope.SelectedAlly ? _snapshot.Player.FindFighter(allyId) :
                card.TargetScope == EffectTargetScope.Self || card.TargetScope == EffectTargetScope.AllAllies ?
                    _snapshot.Player.FindFighter(card.OwnerFighterId) :
                    _snapshot.Opponent.LivingActive().FirstOrDefault(fighter => fighter.Id == _preferredTargetId)
                    ?? _snapshot.Opponent.LivingActive().FirstOrDefault();
            if (card.TargetScope == EffectTargetScope.SelectedEnemy)
            {
                var taunters = _snapshot.Opponent.LivingActive().Where(f => f.Statuses.Instances.Any(s => s.Recipe?.HasTaunt == true)).ToList();
                if (taunters.Count > 0 && !taunters.Contains(target)) target = taunters[0];
            }
            if (target == null)
            {
                Debug.LogWarning("No living opponent can receive this card.", this);
                return;
            }
            if (!_draft.QueuePlay(cardId, target.Id, out var error))
            {
                if (error == HealingCardBlockedMessage)
                    FloatText(card.OwnerFighterId, HealingCardBlockedMessage, "status");
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
            if (!CanPlan || _allyPicker != null) { _targetPointerStarted = false; return; }
            if (_snapshot.Phase != BattlePhase.Planning || _snapshot.ActingSide != TeamSide.Player) return;

            var livingOpponents = _snapshot.Opponent.LivingActive().ToList();
            if (livingOpponents.Count == 0) return;

            // Ensure a valid target is selected by default during drafting
            if (string.IsNullOrEmpty(_preferredTargetId) || !livingOpponents.Any(f => f.Id == _preferredTargetId))
            {
                SelectTarget(livingOpponents[0].Id);
            }

            var pointerPos = Input.touchCount > 0 ? (Vector3)Input.GetTouch(0).position : Input.mousePosition;
            if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                _targetPointerOrigin = pointerPos;
                _targetPointerStarted = pointerPos.y >= Screen.height * .40f;
            }
            if (Input.touchCount > 1 || Vector2.Distance(pointerPos, _targetPointerOrigin) > 24f)
                _targetPointerStarted = false;
            var isPointerUp = Input.GetMouseButtonUp(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended);
            if (!isPointerUp) return;
            var tap = _targetPointerStarted;
            _targetPointerStarted = false;
            if (!tap) return;

            // Ignore input if tapping within the bottom card tray
            if (pointerPos.y < Screen.height * 0.40f) return;

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
            _allyPicker?.RemoveFromHierarchy(); _allyPicker = null;
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
            _allyPicker?.RemoveFromHierarchy();
            _allyPicker = null;
            _isPlayingEvents = true;
            SetControls(false);
            if (_targetReticle != null) _targetReticle.SetVisible(false);
            _executionIndex = -1;
            for (var index = 0; index < events.Count; index++)
            {
                var item = events[index];
                if (IsExecutionEvent(item) && _executionIndex < 0) PrepareExecution(events, index);

                if (IsSimultaneousFeedback(item.Kind))
                {
                    var batch = new List<BattleEvent> { item };
                    while (index + 1 < events.Count && IsSimultaneousFeedback(events[index + 1].Kind))
                        batch.Add(events[++index]);
                    yield return PresentFeedbackBatch(batch);
                    continue;
                }

                yield return PresentEvent(item);
            }
            yield return FinishActionVisual();
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
                _stage.SetStance(fighter.Id, visible && fighter.Statuses.Instances.Any(s =>
                    s.Recipe != null && (s.Recipe.Behavior & StatusBehavior.Stance) != 0));
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
            BindEnemyHand(root);
            _handRow = root.Q<VisualElement>("deck-row");
            if (_handRow != null)
            {
                _handRow.style.position = Position.Relative;
                _handRow.RegisterCallback<GeometryChangedEvent>(_ => LayoutHandCards());
            }
            _actionRow = root.Q<VisualElement>("action-row");
            _tooltipContainer = root.Q<VisualElement>("card-tooltip-container");
            _tooltipCharacterIcon = root.Q<Image>("tooltip-character-icon");
            _tooltipTypeIcon = root.Q<Image>("tooltip-type-icon");
            _tooltipCardTitle = root.Q<Label>("tooltip-card-title");
            _tooltipStatusGrid = root.Q<VisualElement>("tooltip-status-grid");
            _tooltipCardDesc = root.Q<Label>("tooltip-card-desc");
            _tooltipKeywordsDesc = root.Q<Label>("tooltip-keywords-desc");
            _tooltipText = root.Q<Label>("card-tooltip");
            if (_tooltipContainer != null) _tooltipContainer.style.display = DisplayStyle.None;
            if (_tooltipText != null) _tooltipText.style.display = DisplayStyle.None;
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
            if (isHandCard)
            {
                cardTree.style.position = Position.Absolute;
                cardTree.style.width = CardWidth;
                cardTree.style.height = CardHeight;
                cardTree.style.top = 0;
                cardTree.style.opacity = 0f;
                cardTree.schedule.Execute(() =>
                {
                    if (cardTree.panel != null) cardTree.style.opacity = 1f;
                }).StartingIn(20);
            }
            else
            {
                cardTree.style.width = CardWidth;
                cardTree.style.height = CardHeight;
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
                    if (isHandCard) cardTree.BringToFront();
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
                    }).StartingIn(250);
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
                    var destination = -1;
                    var count = _draft == null ? _snapshot?.Player.Hand.Count ?? 0 : _draft.Preview.Hand.Count;
                    var currentSlot = _handRow != null ? _handRow.IndexOf(cardTree) : -1;
                    if (dragging && count > 0)
                    {
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
                        visualDestination = Mathf.Clamp(visualDestination, 0, count - 1);
                        destination = count - 1 - visualDestination;
                    }
                    holding = false;
                    dragging = false;
                    cardTree.style.translate = new Translate(0, 0);
                    evt.StopPropagation();

                    // Dismiss tooltip upon release if it was opened
                    if (wasHolding)
                    {
                        onHoldEnded?.Invoke();
                    }

                    if (!CanPlan) return;

                    var originalLogicalIndex = currentSlot >= 0 ? count - 1 - currentSlot : -1;
                    if (wasDragging && destination >= 0 && destination != originalLogicalIndex)
                    {
                        onDrop?.Invoke(destination);
                    }
                    else if (!wasDragging && !wasHolding)
                    {
                        if (isHandCard) RenderHand();
                        onTap();
                    }
                    else if (isHandCard)
                    {
                        RenderHand();
                    }
                }, TrickleDown.TrickleDown);
                button.RegisterCallback<PointerMoveEvent>(evt =>
                {
                    if (pointerId != evt.pointerId || !CanPlan) return;
                    var delta = evt.position - pointerStart;
                    if (delta.sqrMagnitude > 36f)
                    {
                        dragging = true;
                        cardTree.style.translate = new Translate(delta.x, -18);
                    }
                });
                button.RegisterCallback<PointerCancelEvent>(evt =>
                {
                    holdJob?.Pause();
                    pointerId = -1;
                    var wasHolding = holding;
                    holding = dragging = false;
                    cardTree.style.translate = new Translate(0, 0);
                    if (wasHolding) onHoldEnded?.Invoke();
                    if (isHandCard) RenderHand();
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
            if (cardTree.ClassListContains("hand-card"))
            {
                cardTree.EnableInClassList("card-unavailable", IsHandCardUnavailable(card, owner));
                cardTree.EnableInClassList("card-disabled-by-status", IsCardDisabledByStatus(card, owner));
            }
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
                    skillTypeImage.sprite = LoadSkillTypeSprite(SkillType.Ultimate);
                    skillTypeImage.style.display = skillTypeImage.sprite != null ? DisplayStyle.Flex : DisplayStyle.None;
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

        private bool IsHandCardUnavailable(CardState card, FighterState owner)
        {
            if (IsCardUnavailableByRules(card, owner)) return true;
            if (_draft != null ? _draft.RemainingActions <= 0 : _snapshot == null || _snapshot.ActionBudget <= 0) return true;
            return false;
        }

        private static bool IsCardUnavailableByRules(CardState card, FighterState owner)
        {
            if (card == null || owner == null || !owner.IsAlive || owner.IsReserve) return true;
            EffectDefinition effect;
            var hasEffect = card.Kind == CardKind.Skill
                ? CardRules.TryGetSkill(owner.Definition, card.SkillId, card.Rank, out effect)
                : CardRules.TryGetUltimate(owner.Definition, card.UltimateTier, out effect);
            var disabled = hasEffect && StatusSystem.IsCardUseBlocked(owner, CardRules.GetEffectCategory(card), card.Rank,
                card.Kind == CardKind.Ultimate, effect.Sequence != null && effect.Sequence.Count > 0);
            return card.Kind == CardKind.Ultimate && owner.PowerGauge < CardRules.UltimateGaugeCost && !disabled;
        }

        private static readonly Dictionary<SkillType, Sprite> _skillTypeSprites = new Dictionary<SkillType, Sprite>();

        private static SkillType GetCardSkillType(CardState card, FighterState owner)
        {
            if (card == null || owner == null) return SkillType.Attack;
            if (card.Kind == CardKind.Ultimate) return SkillType.Ultimate;
            return CardRules.GetEffectCategory(card) switch
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
                SkillType.Ultimate => null,
                _ => "Cardtype_Attack"
            };
            if (string.IsNullOrEmpty(fileName)) return null;

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
            var lookup = ResolveCardLookup(card, owner, LoadCharacter(owner.Definition?.Id));
            var character = lookup.Character;
            if (character == null) return null;
            if (card.Kind == CardKind.Ultimate)
                return lookup.UltimateAsset != null && lookup.UltimateAsset.icon != null ? lookup.UltimateAsset.icon : character.FighterIcon;
            return lookup.SkillAsset != null && lookup.SkillAsset.cardIcon != null ? lookup.SkillAsset.cardIcon : character.FighterIcon;
        }

        private static string GetTooltip(CardState card, FighterState owner)
        {
            if (card == null || owner == null) return string.Empty;
            var lookup = ResolveCardLookup(card, owner, LoadCharacter(owner.Definition?.Id));
            var title = GetCardTitle(card, owner, lookup.Character);
            return string.IsNullOrEmpty(lookup.Description) ? title : title + "  •  " + lookup.Description;
        }

        public struct StatusBadgeData
        {
            public string Name;
            public Sprite Icon;
            public bool IsBuff;
        }

        public sealed class InspectorCardTooltipData
        {
            public string Title;
            public string Description;
            public string Keywords;
            public Sprite CharacterIcon;
            public Sprite CardTypeIcon;
            public List<StatusBadgeData> Statuses;
        }

        public static InspectorCardTooltipData BuildInspectorCardTooltip(FighterState owner, int slot, int rank)
        {
            if (owner?.Definition == null) return null;
            var card = new CardState
            {
                OwnerFighterId = owner.Id,
                Kind = slot == 0 ? CardKind.Ultimate : CardKind.Skill,
                Rank = rank,
                UltimateTier = rank,
                SkillId = slot == 0 ? null : owner.Definition.Skills?.FirstOrDefault(skill => skill?.Slot == slot)?.Id
            };
            var character = LoadCharacter(owner.Definition.Id);
            var lookup = ResolveCardLookup(card, owner, character);
            var (description, keywords, statuses) = ExtractCardDetails(card, owner, character);
            var type = GetCardSkillType(card, owner);
            return new InspectorCardTooltipData
            {
                Title = GetCardTitle(card, owner, character),
                Description = description,
                Keywords = keywords,
                Statuses = statuses,
                CharacterIcon = ResolveCharacterIcon(owner, character),
                CardTypeIcon = LoadSkillTypeSprite(type)
            };
        }

        private struct CardLookup
        {
            public CharacterObject Character;
            public SkillCardSO SkillAsset;
            public UltimateCardSO UltimateAsset;
            public CardRankData AssetRank;
            public UltimateLevelData AssetTier;
            public SkillDefinition Skill;
            public SkillRankDefinition SkillRank;
            public UltimateTierDefinition UltimateTier;
            public EffectDefinition Effect;
            public string Description;
        }

        private static CardLookup ResolveCardLookup(CardState card, FighterState owner, CharacterObject character)
        {
            var result = new CardLookup { Character = character };
            if (card == null || owner?.Definition == null) return result;

            if (card.Kind == CardKind.Ultimate)
            {
                result.UltimateAsset = character?.Ultimate;
                result.AssetTier = result.UltimateAsset?.levels?.Find(item => item != null && item.level == card.UltimateTier);
                result.UltimateTier = owner.Definition.UltimateTiers?.Find(item => item != null && item.Tier == card.UltimateTier);
                result.Effect = result.UltimateTier?.Effect ?? result.AssetTier?.runtimeEffect;
                result.Description = result.UltimateTier?.Description;
                if (string.IsNullOrEmpty(result.Description))
                    result.Description = result.AssetTier?.description;
                return result;
            }

            result.Skill = owner.Definition.Skills?.Find(item => item != null && item.Id == card.SkillId);
            if (result.Skill != null)
                result.SkillAsset = result.Skill.Slot == 2 ? character?.Skill2 : character?.Skill1;
            result.AssetRank = result.SkillAsset?.ranks?.Find(item => item != null && item.rankLevel == card.Rank);
            result.SkillRank = result.Skill?.Ranks?.Find(item => item != null && item.Rank == card.Rank);
            result.Effect = result.SkillRank?.Effect ?? result.AssetRank?.runtimeEffect;
            result.Description = result.SkillRank?.Description;
            if (string.IsNullOrEmpty(result.Description))
                result.Description = result.AssetRank?.description;
            return result;
        }

        private static readonly Dictionary<string, Sprite> _characterIconCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static Dictionary<string, CharacterObject> _characterObjectByDefinitionId;
        private static readonly Dictionary<string, Sprite> _statusSpriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> KeywordExplanations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Pierce"] = "3x Pierce Rate Increase.",
            ["Ignite"] = "Increases damage taken by 10% for each stack.",
            ["Poison"] = "Deals additional damage equal to 45% of damage dealt at the end of each turn.",
            ["Bleed"] = "Deals additional damage equal to 33% of damage dealt at the end of each turn.",
            ["Shock"] = "Deals additional damage equal to 36% of damage dealt at the end of each turn.",
            ["Shatter"] = "Ignores enemy Resistance.",
            ["Charge"] = "Ignores enemy Defense.",
            ["Rupture"] = "Deals 2x damage against enemies with Buffs.",
            ["Detonate"] = "Deals 20% additional damage per Power Gauge orb on target.",
            ["Blaze"] = "Deals 25% additional damage per Ignite on target.",
            ["Sever"] = "3x Critical Chance Increase.",
            ["Spike"] = "2x Critical Damage Increase.",
            ["Weakpoint"] = "Deals 3x damage against enemies with Debuffs.",
            ["Weak Point"] = "Deals 3x damage against enemies with Debuffs.",
            ["Amplify"] = "Increases damage dealt by 30% per Buff on self.",
            ["Flood"] = "Increases damage dealt based on remaining HP (up to 80%).",
            ["Despair"] = "Recovers 30% of diminished HP on Critical Hit.",
            ["Cleave"] = "Ignores enemy Critical Defense.",
            ["Quell"] = "Decreases target's Power Gauge.",
            ["Stun"] = "Prevents all actions for the duration.",
            ["Freeze"] = "Prevents actions and takes additional damage when attacked.",
            ["Card Seal"] = "Disables skill card usage for the duration.",
            ["Disable Attack"] = "Prevents using Attack skills.",
            ["Disable Debuff"] = "Prevents using Debuff skills.",
            ["Disable Buff"] = "Prevents using Buff skills.",
            ["Disable Recovery"] = "Prevents all HP recovery.",
            ["Disable Healing Card"] = "Prevents using Recovery cards. Other healing effects still work.",
            ["Disable Stance"] = "Prevents using Stance skills.",
            ["Mark of Concentration"] = "-100% Crit Chance.",
            ["Remove Buff"] = "Removes all Buffs from the target.",
            ["Removes Buff"] = "Removes all Buffs from the target.",
            ["Remove Buffs"] = "Removes all Buffs from the target.",
            ["Cleanse"] = "Removes all Debuffs from allies.",
            ["Rejuvenation"] = "At turn start, heals an additional 60% of HP recovered per stack.",
            ["Recovery"] = "Decreases or increases HP recovery amount.",
            ["Debuff Immunity"] = "Immune to all debuffs for the duration."
        };

        private void PopulateTooltipView(CardState card, FighterState owner)
        {
            if (card == null || owner == null) return;
            var character = LoadCharacter(owner.Definition?.Id);
            var charIcon = ResolveCharacterIcon(owner, character);
            if (_tooltipCharacterIcon != null)
            {
                _tooltipCharacterIcon.sprite = charIcon;
                _tooltipCharacterIcon.scaleMode = ScaleMode.ScaleAndCrop;
            }

            var skillType = GetCardSkillType(card, owner);
            if (_tooltipTypeIcon != null)
            {
                _tooltipTypeIcon.sprite = LoadSkillTypeSprite(skillType);
                _tooltipTypeIcon.style.display = _tooltipTypeIcon.sprite != null ? DisplayStyle.Flex : DisplayStyle.None;
            }

            var title = GetCardTitle(card, owner, character);
            if (_tooltipCardTitle != null) _tooltipCardTitle.text = title;

            var (desc, keywords, statuses) = ExtractCardDetails(card, owner, character);
            if (_tooltipCardDesc != null) _tooltipCardDesc.text = desc;
            if (_tooltipKeywordsDesc != null)
            {
                _tooltipKeywordsDesc.text = keywords;
                _tooltipKeywordsDesc.style.display = string.IsNullOrEmpty(keywords) ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (_tooltipStatusGrid != null)
            {
                _tooltipStatusGrid.Clear();
                foreach (var status in statuses)
                {
                    var badge = CreateStatusBadge(status);
                    _tooltipStatusGrid.Add(badge);
                }
            }
        }

        private static VisualElement CreateStatusBadge(StatusBadgeData status)
        {
            var badge = new VisualElement();
            badge.AddToClassList("tooltip-status-badge");
            badge.AddToClassList(status.IsBuff ? "buff" : "debuff");
            badge.pickingMode = PickingMode.Ignore;

            if (status.Icon != null)
            {
                var icon = new Image();
                icon.sprite = status.Icon;
                icon.AddToClassList("tooltip-status-icon");
                icon.scaleMode = ScaleMode.ScaleToFit;
                icon.pickingMode = PickingMode.Ignore;
                badge.Add(icon);
            }
            else if (!string.IsNullOrEmpty(status.Name))
            {
                var label = new Label(status.Name);
                label.AddToClassList("tooltip-status-label");
                label.pickingMode = PickingMode.Ignore;
                badge.Add(label);
            }

            return badge;
        }

        private static string GetCardTitle(CardState card, FighterState owner, CharacterObject character)
        {
            if (card == null || owner == null) return "\"Skill\"";
            var lookup = ResolveCardLookup(card, owner, character);

            if (card.Kind == CardKind.Ultimate)
            {
                var ultName = lookup.UltimateAsset?.ultimateName ?? lookup.UltimateTier?.SourceLabel;
                if (string.IsNullOrWhiteSpace(ultName))
                    ultName = (owner.Definition?.DisplayName ?? character?.FighterName ?? "Fighter") + " Ultimate";
                return $"\"{ultName}\"";
            }

            var sName = lookup.SkillAsset?.cardName;
            if (string.IsNullOrWhiteSpace(sName))
            {
                sName = $"{owner.Definition?.DisplayName ?? character?.FighterName ?? "Fighter"} Skill {(lookup.Skill?.Slot == 2 ? 2 : 1)}";
            }
            return $"\"{sName}\"";
        }

        private static (string desc, string keywords, List<StatusBadgeData> statuses) ExtractCardDetails(CardState card, FighterState owner, CharacterObject character)
        {
            var lookup = ResolveCardLookup(card, owner, character);
            var rawDesc = lookup.Description ?? (card?.Kind == CardKind.Ultimate ? "Deals massive damage." : "Deals damage.");
            var runtimeEffect = lookup.Effect;
            if (runtimeEffect?.Kind == EffectKind.Damage)
                rawDesc += "\n" + runtimeEffect.DamageHitCount + (runtimeEffect.DamageHitCount == 1 ? " hit" : " hits") +
                    " · " + (runtimeEffect.Attack?.Range == AttackRange.Long ? "Long range" : "Close range") +
                    (runtimeEffect.DamageHitCount > 1 ? " · Damage split across hits" : "");
            var damageKeyword = runtimeEffect?.KeywordId;

            var statuses = new List<StatusBadgeData>();
            var seenStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddBadge(string name, bool isBuff, string iconKey = null)
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                var polarity = isBuff ? StatusPolarity.Buff : StatusPolarity.Debuff;
                var visual = StatusVisualData.Get(iconKey, polarity) ?? StatusVisualData.Get(name, polarity);
                var cleanName = visual != null ? visual.DisplayName : StatusVisualData.GetDisplayName(name, polarity);
                if (cleanName.StartsWith("Increase ", StringComparison.OrdinalIgnoreCase)) cleanName = cleanName.Substring(9);
                if (cleanName.StartsWith("Decrease ", StringComparison.OrdinalIgnoreCase)) cleanName = cleanName.Substring(9);

                var badgeKey = $"{cleanName}:{(isBuff ? "b" : "d")}";
                if (seenStatuses.Add(badgeKey))
                {
                    var sprite = visual?.Icon ?? ResolveStatusSprite(iconKey ?? name, polarity);
                    statuses.Add(new StatusBadgeData { Name = cleanName, Icon = sprite, IsBuff = isBuff });
                }
            }

            AddEffectBadges(runtimeEffect, AddBadge);

            if (!string.IsNullOrEmpty(damageKeyword))
            {
                if (damageKeyword.IndexOf("ignite", StringComparison.OrdinalIgnoreCase) >= 0)
                    AddBadge("Ignite", false, "Status_Ignite");
                else if (damageKeyword.IndexOf("poison", StringComparison.OrdinalIgnoreCase) >= 0)
                    AddBadge("Poison", false, "Status_Poison");
                else if (damageKeyword.IndexOf("bleed", StringComparison.OrdinalIgnoreCase) >= 0)
                    AddBadge("Bleed", false, "Status_Bleed");
                else if (damageKeyword.IndexOf("shock", StringComparison.OrdinalIgnoreCase) >= 0)
                    AddBadge("Shock", false, "Status_Shock");
            }

            if (rawDesc.IndexOf("ignite", StringComparison.OrdinalIgnoreCase) >= 0 && !seenStatuses.Contains("Ignite:d"))
                AddBadge("Ignite", false, "Status_Ignite");
            if (rawDesc.IndexOf("poison", StringComparison.OrdinalIgnoreCase) >= 0 && !seenStatuses.Contains("Poison:d"))
                AddBadge("Poison", false, "Status_Poison");
            if (rawDesc.IndexOf("bleed", StringComparison.OrdinalIgnoreCase) >= 0 && !seenStatuses.Contains("Bleed:d"))
                AddBadge("Bleed", false, "Status_Bleed");
            if (rawDesc.IndexOf("shock", StringComparison.OrdinalIgnoreCase) >= 0 && !seenStatuses.Contains("Shock:d"))
                AddBadge("Shock", false, "Status_Shock");

            var highlightedDesc = HighlightKeywords(rawDesc);
            var keywordsExplanation = BuildKeywordsExplanation(rawDesc, damageKeyword, statuses);

            return (highlightedDesc, keywordsExplanation, statuses);
        }

        private static void AddEffectBadges(EffectDefinition root, Action<string, bool, string> addBadge)
        {
            if (root == null || addBadge == null) return;
            AddEffectBadge(root, addBadge);
            if (root.Sequence == null) return;
            foreach (var step in root.Sequence)
                if (step?.Effect != null) AddEffectBadge(step.Effect, addBadge);
        }

        private static void AddEffectBadge(EffectOperationDefinition effect, Action<string, bool, string> addBadge)
        {
            if (effect == null || effect.Kind != EffectKind.ApplyStatus || effect.StatusRecipe == null) return;
            AddStatusRecipeBadge(effect.StatusRecipe, addBadge);
            if (effect.StatusRecipe.StanceChildren == null) return;
            foreach (var child in effect.StatusRecipe.StanceChildren)
                if (child != null) AddStatusRecipeBadge(child.ToRecipe(), addBadge);
        }

        private static void AddStatusRecipeBadge(StatusRecipeDefinition recipe, Action<string, bool, string> addBadge)
        {
            if (recipe == null || addBadge == null) return;
            var polarity = recipe.Polarity;
            var isBuff = polarity == StatusPolarity.Buff;
            var recipeId = recipe.Id ?? "";
            var lowerId = recipeId.ToLowerInvariant();

            // Prefer authored display names and icons when a recipe has a visual entry.
            var visual = StatusVisualData.Get(recipeId, polarity);
            if (visual != null)
            {
                addBadge(visual.DisplayName, isBuff, recipeId);
                return;
            }

            string iconKey;
            string displayName;

            if (lowerId.Contains("ignite"))
            {
                iconKey = "Status_Ignite";
                displayName = "Ignite";
            }
            else if (lowerId.Contains("poison"))
            {
                iconKey = "Status_Poison";
                displayName = "Poison";
            }
            else if (lowerId.Contains("bleed"))
            {
                iconKey = "Status_Bleed";
                displayName = "Bleed";
            }
            else if (lowerId.Contains("shock"))
            {
                iconKey = "Status_Shock";
                displayName = "Shock";
            }
            else if (lowerId.Contains("freeze"))
            {
                iconKey = "Status_Freeze";
                displayName = "Freeze";
            }
            else if (lowerId.Contains("disable-heal") || lowerId.Contains("disable.healing-card") || lowerId.Contains("disable.healingcard"))
            {
                iconKey = "icon_buff_cc_dis_heal_skill";
                displayName = "Disable Healing Card";
            }
            else if (lowerId.Contains("disable-recovery") || lowerId.Contains("disable.recovery"))
            {
                iconKey = "icon_buff_explosion_debuff_add_per";
                displayName = "Disable Recovery";
            }
            else if (lowerId.Contains("disable-stance") || lowerId.Contains("disable.stance") || (lowerId.Contains("disable") && lowerId.Contains("stance")))
            {
                iconKey = "icon_buff_cc_dis_pose_skill";
                displayName = "Disable Stance";
            }
            else if (lowerId.Contains("stun") || lowerId.Contains("paralyze") || lowerId.Contains("seal") || (recipe.Behavior & StatusBehavior.Disable) != 0)
            {
                iconKey = "Status_Stun";
                displayName = !string.IsNullOrWhiteSpace(recipe.NameKey) ? recipe.NameKey : StatusLibrary.GetDisplayName(recipeId, polarity);
            }
            else if (lowerId.Contains("rejuvenation"))
            {
                iconKey = "icon_buff_heal_dot_heal";
                displayName = "Rejuvenation";
            }
            else if (lowerId.Contains("debuffimmunity") || (recipe.DebuffImmunity && !lowerId.Contains("recovery") && !lowerId.Contains("rejuvenation")))
            {
                iconKey = "icon_buff_number_immune_debuff";
                displayName = "Debuff Immunity";
            }
            else if (lowerId.Contains("recovery"))
            {
                iconKey = isBuff ? "icon_buff_heal_dot_heal" : "icon_buff_explosion_debuff_add_per";
                displayName = isBuff ? "Increase Recovery" : "Decrease Recovery";
            }
            else if ((recipe.Behavior & StatusBehavior.Stat) != 0)
            {
                iconKey = isBuff ? "Status_StatUp" : "Status_StatDown";
                displayName = !string.IsNullOrWhiteSpace(recipe.NameKey) ? recipe.NameKey : StatusLibrary.GetDisplayName(recipeId, polarity);
            }
            else
            {
                iconKey = recipeId;
                displayName = !string.IsNullOrWhiteSpace(recipe.NameKey) ? recipe.NameKey : StatusLibrary.GetDisplayName(recipeId, polarity);
            }

            addBadge(displayName, isBuff, iconKey);
        }

        private static string HighlightKeywords(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";

            text = Regex.Replace(text, @"(\b\d+(\.\d+)?%\b)", "<color=#fbbf24><b>$1</b></color>");
            text = Regex.Replace(text, @"(\b\d+\s*(?:turns?|turn\(s\))\b)", "<color=#fbbf24><b>$1</b></color>", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\b(ATK|Attack|DEF|Defense|HP|Pierce Rate|Crit Chance|Crit Damage)\b", "<color=#60a5fa><b>$1</b></color>", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\b(Removes Buff|Remove Buffs|Remove Buff|Cleanse|Pierce|Ignite|Poison|Bleed|Shock|Stun|Freeze|Shatter|Charge|Rupture|Detonate|Blaze|Sever|Spike|Weakpoint|Weak Point|Amplify|Flood|Mark of Concentration)\b", "<color=#38bdf8><b>$1</b></color>", RegexOptions.IgnoreCase);

            return text;
        }

        private static string BuildKeywordsExplanation(string desc, string damageKeyword, List<StatusBadgeData> statuses)
        {
            var matched = new List<string>();
            var seenKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var combinedText = (desc ?? "") + " " + (damageKeyword ?? "");

            foreach (var status in statuses)
            {
                combinedText += " " + status.Name;
            }

            foreach (var pair in KeywordExplanations)
            {
                if (Regex.IsMatch(combinedText, $@"\b{Regex.Escape(pair.Key)}\b", RegexOptions.IgnoreCase))
                {
                    var unifiedKey = pair.Key.Replace("s", "").Replace(" ", "").ToLowerInvariant();
                    if (seenKeywords.Add(unifiedKey))
                    {
                        matched.Add($"※{pair.Key}: {pair.Value}");
                    }
                }
            }

            if (matched.Count == 0) return "";
            return "<color=#38bdf8>" + string.Join("\n", matched) + "</color>";
        }

        private static Sprite ResolveCharacterIcon(FighterState owner, CharacterObject character)
        {
            if (character != null)
            {
                if (character.FighterIcon != null) return character.FighterIcon;
                if (character.FighterPic != null) return character.FighterPic;
            }

            if (owner?.Definition == null) return null;
            var defId = owner.Definition.Id ?? "";
            if (_characterIconCache.TryGetValue(defId, out var cached) && cached != null)
                return cached;

            var iconName = defId.ToLowerInvariant() switch
            {
                "fighter.kyo94" => "Icon_1_Kyo94",
                "fighter.benimaru94" => "Icon_2_Benimaru94",
                "fighter.goro94" => "Icon_3_Goro94",
                "fighter.shingo97" => "Icon_4_Shingo97",
                "fighter.mai94" => "Icon_17_Mai94",
                "fighter.mai95" => "Icon_5_Mai95",
                "fighter.king94" => "Icon_18_King94",
                "fighter.athena94" => "Icon_16_Athena94",
                "fighter.joe94" => "Icon_9_Joe94",
                "fighter.terry96" => "Icon_11_Terry96",
                "fighter.ryo94" => "Icon_12_Ryo94",
                "fighter.yuri94" => "Icon_13_Yuri94",
                "fighter.robert94" => "Icon_14_Robert94",
                "fighter.chang94" => "Icon_19_Chang94",
                "fighter.choi94" => "Icon_19_Choi94",
                "fighter.brian94" => "Icon_20_Brian94",
                "fighter.lucky94" => "Icon_21_Lucky94",
                "fighter.heavyd94" => "Icon_22_HeavyD!94",
                "fighter.ralf94" => "Icon_23_Ralf94",
                "fighter.clark94" => "Icon_24_Clark94",
                "fighter.leona96" => "Icon_25_Leona96",
                "fighter.iori95" => "Icon_26_Iori95",
                "fighter.chin94" => "Icon_Chin94",
                _ => null
            };

            Sprite sprite = null;
            if (iconName != null)
            {
                sprite = LoadSpriteByName(iconName, "Character/Icon");
            }

            if (sprite == null)
            {
                sprite = Resources.Load<Sprite>("UI/CharPic_Default") ?? LoadSpriteByName("CharPic_Default");
            }

            if (sprite != null)
            {
                _characterIconCache[defId] = sprite;
            }
            return sprite;
        }

        private static Sprite ResolveStatusSprite(string recipeOrStatusId, StatusPolarity polarity)
        {
            if (string.IsNullOrEmpty(recipeOrStatusId))
                return StatusVisualData.GetSprite(null, polarity);

            if (_statusSpriteCache.TryGetValue(recipeOrStatusId, out var cached) && cached != null)
                return cached;

            var sprite = StatusVisualData.GetSprite(recipeOrStatusId, polarity);
            if (sprite == null)
            {
                var fallbackKey = StatusLibrary.GetIconKey(recipeOrStatusId, polarity);
                sprite = LoadSpriteByName(fallbackKey);
            }

            if (sprite != null)
                _statusSpriteCache[recipeOrStatusId] = sprite;

            return sprite;
        }

        private static Sprite LoadSpriteByName(string fileName, string subfolder = "UI")
        {
            if (string.IsNullOrEmpty(fileName)) return null;

            string[] subfolders = subfolder == "UI"
                ? new[] { "UI/Stats_Icon", "UI/StatusIcon", "UI" }
                : new[] { subfolder, "UI/Stats_Icon", "UI/StatusIcon", "UI" };

#if UNITY_EDITOR
            foreach (var sub in subfolders)
            {
                var edPath = "Assets/Project/Art/" + sub + "/" + fileName + ".png";
                var edSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(edPath);
                if (edSprite != null) return edSprite;
            }
#endif
            foreach (var sub in subfolders)
            {
                var res = Resources.Load<Sprite>(sub + "/" + fileName);
                if (res != null) return res;
            }

            foreach (var sub in subfolders)
            {
                var pathParts = ("Project/Art/" + sub + "/" + fileName + ".png").Split('/');
                var filePath = System.IO.Path.Combine(Application.dataPath, System.IO.Path.Combine(pathParts));
                if (System.IO.File.Exists(filePath))
                {
                    try
                    {
                        var bytes = System.IO.File.ReadAllBytes(filePath);
                        var tex = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                        if (tex.LoadImage(bytes))
                        {
                            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                        }
                    }
                    catch { }
                }
            }
            return null;
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

        private static CharacterObject LoadCharacter(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(definitionId)) return null;
            if (_characterObjectByDefinitionId == null)
            {
                _characterObjectByDefinitionId = new Dictionary<string, CharacterObject>(StringComparer.Ordinal);
                foreach (var character in CharacterObjectRegistrySO.LoadAll())
                {
                    if (character == null || string.IsNullOrWhiteSpace(character.DefinitionId)) continue;
                    if (!_characterObjectByDefinitionId.ContainsKey(character.DefinitionId))
                        _characterObjectByDefinitionId.Add(character.DefinitionId, character);
                }
            }

            return _characterObjectByDefinitionId.TryGetValue(definitionId, out var result) ? result : null;
        }

        private static string ShortId(string id) => (id ?? string.Empty).Replace("fighter.", string.Empty);

        private void SetControls(bool enabled)
        {
            if (_resetButton != null)
            {
                _resetButton.style.display = enabled ? DisplayStyle.Flex : DisplayStyle.None;
                _resetButton.SetEnabled(enabled && _draft != null && _draft.Actions.Count > 0);
            }
            if (_handRow != null)
            {
                _handRow.SetEnabled(enabled);
                _handRow.EnableInClassList("input-disabled", !enabled);
            }
            if (_commitButton != null) _commitButton.SetEnabled(enabled);
        }
    }
}
