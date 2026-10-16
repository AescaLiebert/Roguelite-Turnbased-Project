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
        private const string LastHeroPreferenceKey = "MainMenu.LastHeroDefinitionId";

        public static void Initialize(VisualElement root)
        {
            var stage = root.Q("menu-stage");
            if (stage == null) return;
            if (stage.userData is Binding existing) { existing.Refresh(); return; }
            InitializeHero(root);
            stage.userData = new Binding(root, stage);
            var atlas = Resources.Load<Texture2D>("UI/MainMenu/ReferenceAtlas");
            Art(root, "summon-icon", atlas, new Rect(1036, 782, 88, 70));
            Art(root, "training-icon", atlas, new Rect(1365, 787, 84, 58));
        }

        private static void InitializeHero(VisualElement root)
        {
            var image = root.Q<Image>("hero-art");
            if (image == null) return;

            var inventory = PlayerInventoryService.Instance;
            var candidates = inventory?.GetOwnedDefinitions()
                .Where(character => character != null &&
                    (character.FighterFull != null || character.FighterPic != null || character.FighterIcon != null))
                .GroupBy(character => character.DefinitionId)
                .Select(group => group.First())
                .ToList();

            if (candidates == null || candidates.Count == 0)
            {
                image.style.display = DisplayStyle.None;
                return;
            }

            var previousId = PlayerPrefs.GetString(LastHeroPreferenceKey, string.Empty);
            if (candidates.Count > 1) candidates.RemoveAll(character => character.DefinitionId == previousId);

            var character = candidates[new System.Random().Next(candidates.Count)];
            image.sprite = character.FighterFull != null ? character.FighterFull :
                character.FighterPic != null ? character.FighterPic : character.FighterIcon;
            image.scaleMode = ScaleMode.ScaleToFit;
            image.tooltip = character.FighterName;
            image.style.display = DisplayStyle.Flex;
            PlayerPrefs.SetString(LastHeroPreferenceKey, character.DefinitionId);
            PlayerPrefs.Save();

            StartHeroMotion(image);
        }

        private static void StartHeroMotion(Image image)
        {
            var startedAt = Time.unscaledTime;
            image.schedule.Execute(() =>
            {
                if (image.panel == null) return;
                var phase = (Time.unscaledTime - startedAt) * (Mathf.PI * 2f / 14f);
                var breath = Mathf.Sin(phase);
                image.style.scale = new Scale(Vector3.one * (1.014f + breath * .006f));
                image.style.translate = new Translate(Mathf.Sin(phase) * 3f, Mathf.Sin(phase + .8f) * 4f);
                image.style.rotate = new Rotate(new Angle(breath * .2f, AngleUnit.Degree));
            }).Every(33);
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
