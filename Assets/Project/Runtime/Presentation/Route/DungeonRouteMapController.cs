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
using FightingAllstar.Core.Content;

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
        private VisualElement _choicePanel;
        private VisualElement _commitPanel;
        private Label _title;
        private Label _summary;
        private Label _error;
        private int _requestSequence;
        private bool _confirmAbandon;
        private Button _abandonButton;
        private Button _returnLoadoutButton;
        private IVisualElementScheduledItem _pendingScroll;
        private LabyrinthBoard _board;
        private VisualElement _rewardOverlay, _enemyOverlay;
        [NonSerialized] private string _inspectedNodeId;

        private void OnDisable()
        {
            CancelPendingScroll(); CloseEnemyDetails();
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
            AccountHeaderView.Bind(root, ReturnHome);
            _choicePanel = root.Q<VisualElement>("node-actions");
            _commitPanel = root.Q<VisualElement>("node-commit-actions");
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
            if (!won) ShowError("Stage not cleared. Defeated fighters are unavailable for this run. Prepare another team to retry, or abandon when ready.");
            return true;
        }

        private void Refresh()
        {
            CancelPendingScroll();
            if (_run == null || _rows == null) return;
            _rows.Clear();
            _choicePanel.Clear();
            _commitPanel?.Clear();
            if (_title != null) _title.text = DungeonFlowContext.GetProfileForId(_run.ProfileId).DisplayName + " · " + _run.Status;
            if (_summary != null) _summary.text = DungeonDifficulty.Display(DungeonDifficulty.Level(_run.DifficultyBonusPercent)) + " · Rewards after every victory · " + _run.SelectedPath.Count + "/" + (_run.Nodes.Max(n => n.Row) + 1) + " stages";
            var board = new LabyrinthBoard(_run, InspectNode);
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
            ShowPendingRewards();
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
            CloseEnemyDetails(); _choicePanel.Clear();
            _commitPanel?.Clear();
            var node = DungeonRunEngine.InspectNode(_run, _inspectedNodeId ?? _run.CurrentNodeId);
            if (node == null) return;
            var heading = new VisualElement(); heading.AddToClassList("stage-identity"); _choicePanel.Add(heading);
            var emblem = new VisualElement(); emblem.AddToClassList("stage-emblem"); emblem.AddToClassList("stage-" + node.Type.ToString().ToLowerInvariant()); heading.Add(emblem);
            var title = new Label("STAGE " + (node.Row + 1) + " · " + node.Type.ToString().ToUpperInvariant()) { name = "choice-title" }; heading.Add(title);
            var selected = node.Id == _run.CurrentNodeId && node.Progress == RouteNodeProgress.Selected;
            var actionable = DungeonRunEngine.CanEnterNode(_run, node.Id) || selected &&
                (_run.Status == RunStatus.InBattle || _run.Status == RunStatus.InProgress);
            var status = new Label(node.Progress.ToString().ToUpperInvariant()) { name = "inspection-status" }; status.AddToClassList("stage-status");
            status.EnableInClassList("available", actionable); heading.Add(status);
            if (!string.IsNullOrWhiteSpace(node.TacticalTag)) { var tag = new Label(node.TacticalTag); tag.AddToClassList("stage-tactic"); _choicePanel.Add(tag); }
            if (node.Type == RouteNodeType.Start) _choicePanel.Add(new Label("Select a platform to inspect its formation and rewards."));
            if (DungeonRewards.IsBattle(node.Type)) RenderBattleInspection(node);
            if (node.Type == RouteNodeType.Rest) RenderRestChoices();
            else if (node.Type == RouteNodeType.Boon || node.Type == RouteNodeType.Elite && selected && _run.Status == RunStatus.InProgress && _run.Rewards.Exists(r => r.NodeId == node.Id))
                RenderBoonChoices(node);
            else if (DungeonRewards.IsBattle(node.Type) && node.Progress != RouteNodeProgress.Completed)
            {
                var enter = new Button(() => { if (CommitInspectedNode()) { Refresh(); BuildEncounter(); } })
                    { text = selected && _run.Status == RunStatus.InProgress ? "RETRY · PREPARE TEAM →" : selected ? "RESUME · PREPARE TEAM →" : "PREPARE TEAM →", name = "enter-stage-button" };
                enter.AddToClassList("primary-button");
                enter.AddToClassList("prepare-team-button");
                enter.SetEnabled(actionable);
                (_commitPanel ?? _choicePanel).Add(enter);
            }
            foreach (var button in _choicePanel.Query<Button>(className: "route-content-action").ToList()) button.SetEnabled(actionable);
            if (_commitPanel != null)
                foreach (var button in _commitPanel.Query<Button>(className: "route-content-action").ToList()) button.SetEnabled(actionable);
        }

        public void InspectNode(string nodeId)
        {
            if (DungeonRunEngine.InspectNode(_run, nodeId) == null) return;
            _inspectedNodeId = nodeId;
            if (_board != null)
                foreach (var tile in _board.Query<Button>().ToList()) tile.EnableInClassList("tile-inspected", tile.name == "route-node-" + nodeId);
            ShowError(null); RenderSelectedNode();
        }

        private void RenderBattleInspection(RouteNodeState node)
        {
            var formationHeading = new VisualElement(); formationHeading.AddToClassList("stage-formation-heading"); _choicePanel.Add(formationHeading);
            formationHeading.Add(new Label("ENEMY FORMATION") { name = "enemy-formation-title" });
            var cc = new Label("CC  " + node.EnemyTeamSnapshot.Sum(f => (long)(f.Stats?.CombatClass ?? 0)).ToString("N0")); cc.AddToClassList("stage-team-cc"); formationHeading.Add(cc);
            var grid = new VisualElement { name = "enemy-info-grid" }; _choicePanel.Add(grid);
            // UnityEngine.Object overloads ==/!= for missing serialized references. The C# null
            // conditional operator does not use that overload, so a missing targetTexture can
            // still pass through and throw when RenderTexture.width is read.
            var targetTexture = _document.panelSettings == null ? null : _document.panelSettings.targetTexture;
            var width = targetTexture != null ? targetTexture.width : Screen.width;
            CharacterGridView.Populate(grid, node.EnemyTeamSnapshot.OrderBy(f => f.IsReserve).ThenBy(f => f.FormationSlot)
                .Select(f => CharacterIconView.FindCharacter(f.DefinitionId)), select: c => RenderEnemyDetails(node.EnemyTeamSnapshot.Find(f => f.DefinitionId == c.DefinitionId), grid.Q("character-" + c.DefinitionId)),
                badge: c => node.EnemyTeamSnapshot.Any(f => f.DefinitionId == c.DefinitionId && f.IsReserve) ? "SUB" :
                    (node.EnemyTeamSnapshot.Find(f => f.DefinitionId == c.DefinitionId)?.FormationSlot + 1).ToString(), iconSize: width < 1400 ? 104 : 128);
            var rewardHeading = new VisualElement(); rewardHeading.AddToClassList("stage-reward-heading-row"); _choicePanel.Add(rewardHeading);
            rewardHeading.Add(new Label("REWARDS") { name = "stage-reward-heading" });
            var basket = new Label(DungeonRewards.ArtifactBasketLabel(_run.RewardPhase, _run.RewardSubPhase)); basket.AddToClassList("stage-basket-badge"); rewardHeading.Add(basket);
            var rewardScroll = new ScrollView(ScrollViewMode.Horizontal)
            { name = "stage-reward-scroll", horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Hidden };
            rewardScroll.AddToClassList("stage-reward-scroll"); _choicePanel.Add(rewardScroll);
            var rewards = new VisualElement { name = "stage-reward-grid" }; rewardScroll.Add(rewards);
            ItemRewardView.Populate(rewards, DungeonRewards.Preview(_run, node), _document.rootVisualElement,
                rule: item => DungeonRewards.PreviewRule(item, _run.DifficultyBonusPercent, node.Type,
                    new DungeonProfile { Phase = _run.RewardPhase, SubPhase = _run.RewardSubPhase, Mode = _run.Mode }),
                previewArtifactRarity: DungeonRewards.HighestArtifact(_run.DifficultyBonusPercent), previewOnly: true);
        }

        private void CloseEnemyDetails()
        {
            var overlay = _enemyOverlay; _enemyOverlay = null;
            if (overlay?.userData is Action close) close(); else overlay?.RemoveFromHierarchy();
        }

        private void RenderEnemyDetails(EncounterFighterSnapshot fighter, VisualElement anchor = null)
        {
            if (fighter == null) return;
            CloseEnemyDetails(); ItemRewardView.CloseTooltip(_document.rootVisualElement);
            var root = _document.rootVisualElement;
            var focus = anchor ?? root.panel?.focusController.focusedElement as VisualElement;
            var overlay = new VisualElement { name = "enemy-info-overlay", focusable = true }; overlay.AddToClassList("modal-scrim"); overlay.AddToClassList("enemy-info-overlay");
            var dialog = new VisualElement { name = "enemy-info-dialog" }; dialog.AddToClassList("enemy-info-dialog"); overlay.Add(dialog);
            Action close = () => { overlay.RemoveFromHierarchy(); if (_enemyOverlay == overlay) _enemyOverlay = null; if (focus?.panel != null) focus.Focus(); };
            overlay.userData = close; _enemyOverlay = overlay;
            var d = fighter.Definition; var stats = fighter.Stats;
            var header = new VisualElement(); header.AddToClassList("enemy-popup-header"); dialog.Add(header);
            var character = CharacterIconView.FindCharacter(fighter.DefinitionId);
            header.Add(CharacterIconView.Create(character, 104, fighter.IsReserve ? "SUB" : (fighter.FormationSlot + 1).ToString()));
            var identity = new VisualElement(); identity.AddToClassList("enemy-popup-identity"); header.Add(identity);
            var name = new Label(d?.DisplayName ?? fighter.DefinitionId); name.AddToClassList("enemy-popup-name"); identity.Add(name);
            identity.Add(new Label(DungeonProfile.SeriesDisplayName(d?.SeriesId) + " · Lv. " + fighter.Level + " · C" + fighter.ConstellationTier));
            identity.Add(new Label("HP  " + fighter.CurrentHealth.ToString("N0") + " / " + (stats?.MaxHealth ?? 0).ToString("N0")));
            var dismiss = new Button(close) { name = "close-enemy-info", text = "×", tooltip = "Close enemy details" }; dismiss.AddToClassList("enemy-popup-close"); header.Add(dismiss);
            var scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Hidden }; scroll.AddToClassList("enemy-popup-scroll"); dialog.Add(scroll);
            var basic = new VisualElement(); BasicStatPreview.Render(basic, stats); scroll.Add(basic);
            if (stats != null)
            {
                var all = new Foldout { text = "SECONDARY STATS", value = false }; scroll.Add(all);
                foreach (var field in typeof(StatBlock).GetFields().Where(f => f.FieldType == typeof(int) && f.Name.EndsWith("Bp")))
                {
                    var row = new VisualElement(); row.AddToClassList("enemy-secondary-stat");
                    row.Add(new Label(System.Text.RegularExpressions.Regex.Replace(field.Name.Substring(0, field.Name.Length - 2), "(?<!^)([A-Z])", " $1")));
                    row.Add(new Label(((int)field.GetValue(stats) / 100f).ToString("0.##") + "%")); all.Add(row);
                }
            }
            if (d != null)
            {
                var passive = new Foldout { text = "PASSIVE", value = false }; scroll.Add(passive);
                passive.Add(new Label(d.PassiveSource?.Description ?? "No passive description."));
                foreach (var skill in d.Skills)
                {
                    var skillInfo = new Foldout { text = "SKILL " + skill.Slot, value = false }; scroll.Add(skillInfo);
                    foreach (var rank in skill.Ranks) skillInfo.Add(new Label("R" + rank.Rank + " · " + rank.Category + "\n" + (rank.Description ?? rank.SourceDescription ?? "No description.")));
                }
                var ultimate = d.UltimateTiers.Find(t => t.Tier == fighter.ConstellationTier);
                var ultimateInfo = new Foldout { text = "ULTIMATE · C" + fighter.ConstellationTier, value = false }; scroll.Add(ultimateInfo);
                ultimateInfo.Add(new Label(ultimate?.Description ?? ultimate?.SourceDescription ?? "No description."));
            }
            root.Add(overlay);
            overlay.RegisterCallback<ClickEvent>(evt => { evt.StopPropagation(); if (evt.target == overlay) close(); });
            overlay.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Escape) { close(); evt.StopPropagation(); } });
            overlay.Focus();
        }

        private VisualElement SupportCard(string kind, string title, string value, string copy)
        {
            var card = new VisualElement { name = kind + "-preview" }; card.AddToClassList("support-preview"); card.AddToClassList("support-" + kind);
            var header = new VisualElement(); header.AddToClassList("support-heading"); card.Add(header);
            var emblem = new VisualElement(); emblem.AddToClassList("support-emblem"); header.Add(emblem);
            var copyBlock = new VisualElement(); copyBlock.AddToClassList("support-heading-copy"); header.Add(copyBlock);
            var heading = new Label(title); heading.AddToClassList("support-title"); copyBlock.Add(heading);
            var amount = new Label(value); amount.AddToClassList("support-value"); copyBlock.Add(amount);
            var description = new Label(copy); description.AddToClassList("support-copy"); card.Add(description);
            _choicePanel.Add(card); return card;
        }

        private void RenderRestChoices()
        {
            var injured = _run.Roster.Exists(f => !f.IsDefeated && f.CurrentHealth > 0 && f.CurrentHealth < f.Stats.MaxHealth);
            var card = SupportCard("rest", "RECOVERY POINT", "+40% MAX HP", "Restore living fighters. Defeated fighters remain unavailable until this run ends.");
            foreach (var fighter in _run.Roster.OrderBy(f => f.IsDefeated))
            {
                var line = new VisualElement(); line.AddToClassList("recovery-fighter"); card.Add(line);
                line.Add(CharacterIconView.Create(CharacterIconView.FindCharacter(fighter.DefinitionId), 48));
                var recovered = Math.Min(fighter.Stats.MaxHealth, fighter.CurrentHealth + Math.Max(1, fighter.Stats.MaxHealth * 40L / 100));
                var label = new Label((CharacterIconView.FindCharacter(fighter.DefinitionId)?.FighterName ?? fighter.DefinitionId) + "\n" +
                    (fighter.IsDefeated ? "DEFEATED · UNAVAILABLE" : fighter.CurrentHealth.ToString("N0") + " → " + recovered.ToString("N0") + " HP"));
                line.Add(label);
            }
            var heal = new Button(() => ApplyRest(injured ? RestChoice.HealLiving : RestChoice.Continue, null))
                { text = injured ? "Recover fighters →" : "Continue →", name = "rest-confirm" };
            heal.AddToClassList("primary-button"); heal.AddToClassList("route-content-action"); (_commitPanel ?? _choicePanel).Add(heal);
        }

        private void RenderBoonChoices(RouteNodeState node)
        {
            var boost = node.StatBoost ?? DungeonStatBoost.Generate(_run.Seed, node.Id);
            SupportCard("boon", "STAT BLESSING", boost.Description, "One random stat, one random increase. Applies to living fighters and later recruits for this run.");
            var claimed = _run.StatBoosts.Exists(b => b.NodeId == node.Id);
            var button = new Button(() => ChooseBoon("stat-boost:" + node.Id))
                { text = claimed ? "Blessing claimed" : "Claim " + boost.Description + " →", name = "boon-confirm" };
            button.AddToClassList("primary-button"); button.AddToClassList("route-content-action"); (_commitPanel ?? _choicePanel).Add(button);
            if (_run.StatBoosts.Count > 0)
            {
                var summary = new Label("RUN BLESSINGS · " + string.Join(" · ", _run.StatBoosts.Select(b => b.Description)));
                summary.AddToClassList("support-copy"); _choicePanel.Add(summary);
            }
        }

        private void ShowPendingRewards()
        {
            if (_document?.rootVisualElement == null || _run == null || _rewardOverlay?.panel != null) return;
            var receipt = _run.Rewards.FirstOrDefault(r => !r.CompletionBonus && !_run.PresentedRewardIds.Contains(r.Id));
            if (receipt == null) return;
            var root = _document.rootVisualElement.Q("route-root") ?? _document.rootVisualElement;
            var overlay = new VisualElement { name = "victory-reward-overlay", focusable = true };
            _rewardOverlay = overlay; overlay.AddToClassList("modal-scrim"); overlay.AddToClassList("victory-reward-overlay");
            var dialog = new VisualElement { name = "victory-reward-dialog" }; dialog.AddToClassList("modal-dialog"); dialog.AddToClassList("victory-reward-dialog"); overlay.Add(dialog);
            var granted = PlayerInventoryService.Instance?.Snapshot?.items?.ClaimedReceiptIds.Contains(receipt.Id) == true;
            var title = new Label(granted ? "VICTORY · REWARDS GRANTED" : "VICTORY · REWARDS SAVED"); title.AddToClassList("modal-title"); dialog.Add(title);
            var caption = new Label(!granted ? "Your rewards are saved. Inventory delivery will retry automatically." :
                _run.Status == RunStatus.Completed ? "Dungeon cleared. Your rewards are saved." : "Stage cleared. Prepare your team for the next stage."); caption.AddToClassList("body-copy"); dialog.Add(caption);
            var scroll = new ScrollView(ScrollViewMode.Horizontal) { name = "victory-reward-scroll",
                horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.AddToClassList("reward-item-strip"); dialog.Add(scroll);
            var rewards = new VisualElement { name = "victory-reward-items" }; scroll.Add(rewards);
            rewards.style.flexWrap = Wrap.NoWrap;
            ItemRewardView.Populate(rewards, receipt.Items.Select(i => ItemCatalog.Find(i.ItemId))
                .Concat(receipt.Artifacts.Select(a => ItemCatalog.Find(a.ItemId))).Where(i => i != null).GroupBy(i => i.Id).Select(g => g.First()), root, receipt);
            var hint = new Label("Scroll to see all rewards. Select an artifact to inspect its exact stats and set bonuses.");
            hint.AddToClassList("reward-strip-hint"); dialog.Add(hint);
            var continueButton = new Button(() =>
            {
                if (!DungeonRunEngine.TryAcknowledgeReward(_run, receipt.Id, _run.Revision, NextRequest("reward-ack"), out var next, out var error))
                { ShowError(error); return; }
                try { _store.Save(next); }
                catch (Exception exception) { caption.text = "Could not save acknowledgment. Please retry: " + exception.Message; return; }
                _run = next;
                DungeonFlowContext.BeginDungeonFlowForEncounter(DungeonFlowContext.GetProfileForId(_run.ProfileId), _run, null);
                RunChanged?.Invoke(_run.Clone());
                ItemRewardView.CloseTooltip(root); overlay.RemoveFromHierarchy(); _rewardOverlay = null;
                ShowPendingRewards();
            }) { name = "continue-after-reward", text = _run.Status == RunStatus.Completed ? "Continue →" : "Back to route →" };
            continueButton.AddToClassList("primary-button"); dialog.Add(continueButton);
            root.Add(overlay); continueButton.Focus(); ScreenMotion.Show(dialog);
            overlay.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            overlay.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Tab)
                {
                    var controls = dialog.Query<Button>().ToList();
                    var focused = root.panel?.focusController.focusedElement as Button;
                    var index = controls.IndexOf(focused);
                    controls[(index + (evt.shiftKey ? controls.Count - 1 : 1)) % controls.Count].Focus();
                    evt.StopPropagation(); evt.PreventDefault();
                }
            });
        }

        private bool CommitInspectedNode()
        {
            var nodeId = _inspectedNodeId ?? _run.CurrentNodeId;
            if (nodeId == _run.CurrentNodeId && _run.FindNode(nodeId)?.Progress == RouteNodeProgress.Selected)
            {
                var node = _run.FindNode(nodeId);
                if (_run.Status == RunStatus.InProgress && DungeonRewards.IsBattle(node.Type) && !_run.Rewards.Exists(r => r.NodeId == nodeId))
                {
                    if (!DungeonRunEngine.TryRetryBattle(_run, _run.Revision, NextRequest("retry"), out var retry, out var retryError))
                    { ShowError(retryError); return false; }
                    _run = retry; PersistAndNotify();
                }
                return true;
            }
            if (DungeonRunEngine.TryChooseNode(_run, nodeId, _run.Revision, NextRequest("node"), out var next, out var error))
            {
                _run = next;
                PersistAndNotify();
                return true;
            }
            else ShowError(error);
            return false;
        }

        private void ApplyRest(RestChoice choice, string fighterId)
        {
            if (!CommitInspectedNode()) return;
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
            if (!CommitInspectedNode()) return;
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
                _run = next; PersistAndNotify();
                if (!TryDeliverRewards()) { Refresh(); return; }
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
            if (!TryDeliverRewards()) return;
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

        private void ReturnHome()
        {
            // The active run stays persisted so returning from Home resumes the map.
            SceneManager.LoadScene("MainMenu");
        }

        private void PersistAndNotify()
        {
            if (_run == null) return;
            // Persist receipts before wallet writes. A restart safely retries unclaimed victories.
            _store.Save(_run);
            DungeonFlowContext.BeginDungeonFlowForEncounter(DungeonFlowContext.GetProfileForId(_run.ProfileId), _run, null);
            TryDeliverRewards();
            RunChanged?.Invoke(_run.Clone());
        }

        private bool TryDeliverRewards()
        {
            if (_run == null || (_run.Rewards.Count == 0 && _run.Status != RunStatus.Completed)) return true;
            var inventory = PlayerInventoryService.Instance;
            if (inventory == null) { ShowError("Rewards are saved and will be delivered when your inventory is available."); return false; }
            try
            {
                foreach (var reward in _run.Rewards) inventory.GrantDungeonReward(reward);
                if (_run.Status == RunStatus.Completed && !_run.RewardClaimed)
                {
                    _run.RewardClaimed = true;
                }
                _store.Save(_run);
                return true;
            }
            catch (Exception error) { ShowError("Reward delivery will retry: " + error.Message); return false; }
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
