using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Run;
using FightingAllstar.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using CoreBattleState = FightingAllstar.Core.Combat.BattleState;

namespace FightingAllstar.Presentation.Route
{
    /// <summary>Local route map view. Encounter transitions are exposed to app composition through events.</summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DungeonRouteMapController : MonoBehaviour
    {
        public event Action<RunState> RunChanged;
        public event Action<EncounterProjection> EncounterReady;
        public event Action ReturnToEntryRequested;

        private UIDocument _document;
        private LocalRunStateStore _store;
        [NonSerialized] private RunState _run;
        private VisualElement _rows;
        private VisualElement _hpStrip;
        private VisualElement _choicePanel;
        private Label _title;
        private Label _summary;
        private Label _error;
        private int _requestSequence;
        private bool _confirmAbandon;
        private Button _abandonButton;
        private Button _returnLoadoutButton;
        private IVisualElementScheduledItem _pendingScroll;
        private LabyrinthBoard _board;

        private void OnDisable()
        {
            CancelPendingScroll();
            if (_abandonButton != null) _abandonButton.clicked -= Abandon;
            if (_returnLoadoutButton != null) _returnLoadoutButton.clicked -= ReturnToDungeonSelect;
        }

        private void CancelPendingScroll()
        {
            _pendingScroll?.Pause();
            _pendingScroll = null;
        }

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            _store = new LocalRunStateStore();
            if (_store.TryLoad(out var saved)) _run = saved;
        }

        private void OnEnable()
        {
            if (_document == null || _document.rootVisualElement == null) return;
            var root = _document.rootVisualElement;
            _rows = root.Q<VisualElement>("route-rows");
            _hpStrip = root.Q<VisualElement>("run-hp-strip");
            _choicePanel = root.Q<VisualElement>("node-actions");
            _title = root.Q<Label>("route-title");
            _summary = root.Q<Label>("route-summary");
            _error = root.Q<Label>("route-error");
            _abandonButton = root.Q<Button>("abandon-button");
            if (_abandonButton != null) _abandonButton.clicked += Abandon;
            _returnLoadoutButton = root.Q<Button>("return-dungeon-button") ?? root.Q<Button>("return-loadout-button");
            if (_returnLoadoutButton != null) _returnLoadoutButton.clicked += ReturnToDungeonSelect;
            Refresh();
        }

        public void Bind(RunState run)
        {
            _run = run == null ? null : run.Clone();
            PersistAndNotify();
            Refresh();
        }

        public void SetRun(RunState run)
        {
            _run = run == null ? null : run.Clone();
            PersistAndNotify();
            Refresh();
        }

        public RunState GetRunSnapshot() => _run == null ? null : _run.Clone();

        public bool ApplyBattleResult(CoreBattleState battle, string battleReceiptId, out string error)
        {
            error = null;
            if (_run == null || battle == null || battle.Phase != BattlePhase.Complete)
            { error = "A completed battle and active run are required."; return false; }
            var results = RunBattleBridge.BuildRunResults(_run, battle);
            var won = !battle.IsDraw && battle.Winner == TeamSide.Player;
            if (!DungeonRunEngine.TryApplyBattleResult(_run, battle.MatchId, battleReceiptId, _run.Revision,
                _run.RunId + ":battle-result:" + battleReceiptId, won, results, out var next, out error)) return false;
            _run = next;
            PersistAndNotify();
            Refresh();
            return true;
        }

        private void Refresh()
        {
            CancelPendingScroll();
            if (_run == null || _rows == null) return;
            _rows.Clear();
            _hpStrip.Clear();
            _choicePanel.Clear();
            if (_title != null) _title.text = DungeonFlowContext.GetProfileForId(_run.ProfileId).DisplayName + " · " + _run.Status;
            if (_summary != null) _summary.text = "+" + _run.DifficultyBonusPercent + "% difficulty · Reward " + _run.RewardQuoteDiamonds + " Diamonds · " + _run.SelectedPath.Count + "/9 stages · Seed " + _run.Seed;
            foreach (var fighter in _run.Roster)
            {
                var label = new Label(fighter.DefinitionId.Replace("fighter.", string.Empty) + "  " +
                    (fighter.IsDefeated ? "DEFEATED" : fighter.CurrentHealth + " / " + fighter.Stats.MaxHealth));
                label.AddToClassList(fighter.IsDefeated ? "run-fighter-defeated" : "run-fighter");
                _hpStrip.Add(label);
            }
            var board = new LabyrinthBoard(_run, ChooseNode);
            _board = board;
            _rows.Add(board);
            if (_rows is ScrollView scroll && board.CurrentTile != null)
            {
                // The board owns the callback: removing it also detaches its scheduler.
                // Refresh can replace the board twice in one frame (OnEnable + SetRun).
                _pendingScroll = board.schedule.Execute(() =>
                {
                    if (!isActiveAndEnabled || _board != board) return;
                    TryScrollToCurrentTile(scroll, board);
                });
                _pendingScroll.ExecuteLater(50);
            }
            RenderSelectedNode();
            if (_abandonButton != null)
            {
                _abandonButton.SetEnabled(_run.Status != RunStatus.Completed && _run.Status != RunStatus.Defeated && _run.Status != RunStatus.Abandoned);
                _abandonButton.text = _confirmAbandon ? "Confirm abandon" : "Abandon run";
            }
            if (_returnLoadoutButton != null)
            {
                var runEnded = _run.Status == RunStatus.Completed || _run.Status == RunStatus.Defeated || _run.Status == RunStatus.Abandoned;
                _returnLoadoutButton.style.display = runEnded ? DisplayStyle.Flex : DisplayStyle.None;
                if (runEnded)
                    _returnLoadoutButton.text = "Return to Dungeon Select";
            }
        }

        public static bool TryScrollToCurrentTile(ScrollView scroll, LabyrinthBoard board)
        {
            var tile = board?.CurrentTile;
            if (scroll == null || tile == null || scroll.panel == null ||
                tile.panel != scroll.panel || !scroll.contentContainer.Contains(tile)) return false;
            scroll.ScrollTo(tile);
            return true;
        }

        private void RenderSelectedNode()
        {
            var node = _run.FindNode(_run.CurrentNodeId);
            if (node == null || node.Progress != RouteNodeProgress.Selected) return;
            _choicePanel.Add(new Label("CURRENT: " + node.Type.ToString().ToUpperInvariant()) { name = "choice-title" });
            if (node.Type == RouteNodeType.Rest) RenderRestChoices();
            else if (node.Type == RouteNodeType.Boon || node.Type == RouteNodeType.Elite && _run.Status == RunStatus.InProgress && node.BoonOfferIds.Count > 0)
                RenderBoonChoices(node);
            else if (_run.Status == RunStatus.InBattle)
            {
                var description = new Label("Encounter is committed. Its enemy team snapshot is pinned.");
                description.AddToClassList("stage-description");
                _choicePanel.Add(description);
                var enemyGrid = new VisualElement { name = "enemy-info-grid" };
                enemyGrid.AddToClassList("enemy-info-grid");
                var enemyIcons = new List<VisualElement>();
                foreach (var enemy in node.EnemyTeamSnapshot.OrderBy(f => f.FormationSlot))
                {
                    var isSub = enemy.IsReserve || enemy.FormationSlot == 3;
                    var card = new VisualElement(); card.AddToClassList("enemy-info-card");
                    var icon = CharacterIconView.Create(CharacterIconView.FindCharacter(enemy.DefinitionId), 104, isSub ? "SUB" : null);
                    enemyIcons.Add(icon);
                    card.Add(icon);
                    enemyGrid.Add(card);
                }
                enemyGrid.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    if (enemyIcons.Count == 0 || enemyGrid.contentRect.width <= 0 || enemyGrid.contentRect.height <= 0) return;
                    var slotWidth = (enemyGrid.contentRect.width - 14f * enemyIcons.Count) / enemyIcons.Count;
                    var iconSize = Mathf.Max(1f, Mathf.Min(slotWidth, enemyGrid.contentRect.height - 8f));
                    foreach (var icon in enemyIcons)
                    {
                        if (Mathf.Abs(icon.resolvedStyle.width - iconSize) < 1f) continue;
                        icon.style.width = iconSize;
                        icon.style.height = iconSize;
                    }
                });
                _choicePanel.Add(enemyGrid);
                var enter = new Button(() => BuildEncounter()) { text = "Prepare team →" };
                enter.AddToClassList("primary-button");
                enter.AddToClassList("prepare-team-button");
                _choicePanel.Add(enter);
            }
        }

        private void RenderRestChoices()
        {
            var injured = _run.Roster.Exists(f => !f.IsDefeated && f.CurrentHealth < f.Stats.MaxHealth);
            var defeated = _run.Roster.FindAll(f => f.IsDefeated);
            if (injured)
            {
                var heal = new Button(() => ApplyRest(RestChoice.HealLiving, null)) { text = "Heal living fighters · +40% Max HP" };
                heal.AddToClassList("primary-button");
                _choicePanel.Add(heal);
            }
            foreach (var fighter in defeated)
            {
                var revive = new Button(() => ApplyRest(RestChoice.ReviveOne, fighter.RunFighterId))
                    { text = "Revive " + fighter.DefinitionId.Replace("fighter.", string.Empty) + " · 30% Max HP" };
                revive.AddToClassList("primary-button");
                _choicePanel.Add(revive);
            }
            if (!injured && defeated.Count == 0)
                _choicePanel.Add(new Button(() => ApplyRest(RestChoice.Continue, null)) { text = "Continue without recovery" });
        }

        private void RenderBoonChoices(RouteNodeState node)
        {
            foreach (var boonId in DungeonRunEngine.GetAvailableBoonOffers(_run, node.Id))
            {
                var offer = boonId;
                var boon = _run.BoonDefinitionSnapshot.Find(definition => definition.Id == offer);
                var button = new Button(() => ChooseBoon(offer)) { text = boon == null ? offer : boon.Name + "\n" + boon.Description };
                button.AddToClassList("primary-button");
                _choicePanel.Add(button);
            }
        }

        private void ChooseNode(string nodeId)
        {
            if (DungeonRunEngine.TryChooseNode(_run, nodeId, _run.Revision, NextRequest("node"), out var next, out var error))
            {
                _run = next;
                PersistAndNotify();
                Refresh();
                // Preview the generated team before opening formation.
            }
            else ShowError(error);
        }

        private void ApplyRest(RestChoice choice, string fighterId)
        {
            if (DungeonRunEngine.TryApplyRest(_run, choice, fighterId, _run.Revision, NextRequest("rest"), out var next, out var error))
            {
                _run = next;
                PersistAndNotify();
                Refresh();
            }
            else ShowError(error);
        }

        private void ChooseBoon(string boonId)
        {
            if (DungeonRunEngine.TryChooseBoon(_run, boonId, _run.Revision, NextRequest("boon"), out var next, out var error))
            {
                _run = next;
                PersistAndNotify();
                Refresh();
            }
            else ShowError(error);
        }

        private void BuildEncounter()
        {
            if (_run == null || _run.Status != RunStatus.InBattle) return;
            EncounterReady?.Invoke(new EncounterProjection { BattleId = _run.PendingBattleId, RunRevision = _run.Revision });
        }

        private void Abandon()
        {
            if (_run == null) return;
            if (!_confirmAbandon)
            {
                _confirmAbandon = true;
                ShowError("Choose Abandon again to end this run. Permanent roster ownership is kept.");
                if (_abandonButton != null) _abandonButton.text = "Confirm abandon";
                return;
            }
            if (DungeonRunEngine.TryAbandon(_run, _run.Revision, NextRequest("abandon"), out var next, out var error))
            {
                _confirmAbandon = false;
                _run = null;
                _store.Clear();
                DungeonFlowContext.Clear();
                if (ReturnToEntryRequested != null)
                {
                    ReturnToEntryRequested.Invoke();
                }
                else
                {
                    SceneManager.LoadScene("Combat");
                }
            }
            else { _confirmAbandon = false; ShowError(error); }
        }

        private void ReturnToDungeonSelect()
        {
            _store.Clear();
            _run = null;
            DungeonFlowContext.Clear();
            if (ReturnToEntryRequested != null)
            {
                ReturnToEntryRequested.Invoke();
            }
            else
            {
                SceneManager.LoadScene("Combat");
            }
        }

        private void PersistAndNotify()
        {
            if (_run == null) return;
            if (_run.Status == RunStatus.Defeated || _run.Status == RunStatus.Abandoned)
            {
                _store.Clear();
                DungeonFlowContext.Clear();
            }
            else _store.Save(_run);
            RunChanged?.Invoke(_run.Clone());
        }

        private string NextRequest(string action) => _run.RunId + ":ui:" + action + ":" + _run.Revision + ":" + (++_requestSequence);
        private void ShowError(string error) { if (_error != null) _error.text = error ?? string.Empty; }

        private static string NodeLabel(RouteNodeState node)
        {
            var label = node.Type.ToString();
            if (node.Type == RouteNodeType.Battle || node.Type == RouteNodeType.Elite || node.Type == RouteNodeType.Boss)
            {
                label += "\n";
                foreach (var fighter in node.EnemyTeamSnapshot) label += fighter.DefinitionId.Replace("fighter.", string.Empty) + " ";
            }
            return label + "\n" + node.Progress;
        }
    }
}
