using System;
using System.Linq;
using FightingAllstar.Core.Run;
using FightingAllstar.Presentation.Route;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Navigation
{
    public static class MainMenuView
    {
        public static void Initialize(VisualElement root)
        {
            var stage = root.Q("menu-stage");
            if (stage == null) return;
            if (stage.userData is Binding existing) { existing.Refresh(); return; }
            stage.userData = new Binding(root, stage);
            var atlas = Resources.Load<Texture2D>("UI/MainMenu/ReferenceAtlas");
            Art(root, "summon-icon", atlas, new Rect(1036, 782, 88, 70));
            Art(root, "training-icon", atlas, new Rect(1365, 787, 84, 58));
        }

        public static void RefreshDungeon(VisualElement root, RunState run)
        {
            var card = root.Q("route-card"); if (card == null) return;
            var active = run != null && (run.Status == RunStatus.InProgress || run.Status == RunStatus.InBattle);
            card.style.display = active ? DisplayStyle.Flex : DisplayStyle.None; card.SetEnabled(active);
            var enter = root.Q<Button>("battle");
            if (enter != null) { enter.text = active ? "CONTINUE DUNGEON  ›" : "ENTER DUNGEON  ›"; enter.tooltip = active ? "Resume your saved dungeon" : "Choose a dungeon"; }
            if (!active) return;
            var profile = DungeonFlowContext.GetProfileForId(run.ProfileId);
            var node = run.FindNode(run.CurrentNodeId);
            var rows = run.Nodes.Count == 0 ? 0 : run.Nodes.Max(n => n.Row);
            root.Q<Label>("current-dungeon-name").text = profile.DisplayName;
            root.Q<Label>("current-dungeon-stage").text = node == null || node.Type == RouteNodeType.Start ? "READY TO BEGIN · " + rows + " STAGES" :
                "STAGE " + node.Row + " / " + rows + " · " + node.Type.ToString().ToUpperInvariant() +
                (node.Progress == RouteNodeProgress.Completed ? " COMPLETE" : run.Status == RunStatus.InBattle ? " IN PROGRESS" : "");
            root.Q<Label>("current-dungeon-difficulty").text = DungeonDifficulty.Display(DungeonDifficulty.Level(run.DifficultyBonusPercent));
            root.Q<Label>("route-series").text = DungeonProfile.SeriesDisplayName(profile.SeriesId).ToUpperInvariant();
            card.tooltip = "Resume " + profile.DisplayName;
        }

        private sealed class Binding
        {
            private readonly VisualElement _root;
            private bool _subscribed;
            public Binding(VisualElement root, VisualElement stage)
            {
                _root = root;
                root.Q<Button>("backpack").clicked += () => BackpackView.Show(root);
                stage.RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
                stage.RegisterCallback<DetachFromPanelEvent>(_ => { if (_subscribed) PlayerInventoryService.InventoryChanged -= Refresh; _subscribed = false; });
                if (stage.panel != null) Subscribe(); else Refresh();
            }
            private void Subscribe()
            {
                if (!_subscribed) { PlayerInventoryService.InventoryChanged += Refresh; _subscribed = true; }
                Refresh();
            }
            public void Refresh()
            {
                var loaded = new LocalRunStateStore().TryLoad(out var run);
                if (!loaded || run.UserId != PlayerInventoryService.Instance?.SubjectId) run = null;
                RefreshDungeon(_root, run);
            }
        }
        private static void Art(VisualElement root, string name, Texture2D atlas, Rect source)
        {
            if (atlas != null) root.Q(name)?.Add(new ReferenceMenuGraphic(atlas, source,
                new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) }, Color.white, Color.clear));
        }
    }
}