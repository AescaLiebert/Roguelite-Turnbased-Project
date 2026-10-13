using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation
{
    /// <summary>Shared character grids, including fitted previews and availability overlays.</summary>
    public static class CharacterGridView
    {
        public static void Populate(VisualElement grid, IEnumerable<CharacterObject> characters,
            Action<CharacterObject> select = null, Func<CharacterObject, bool> enabled = null,
            Func<CharacterObject, string> badge = null, string emptyMessage = "No fighters available.",
            Func<CharacterObject, string> unavailableReason = null, bool fit = false, float iconSize = 128,
            Func<CharacterObject, bool> defeated = null)
        {
            if (grid == null) return;
            grid.Clear(); grid.AddToClassList("character-grid");
            grid.EnableInClassList("fitted-character-grid", fit);
            var visible = characters?.Where(c => c != null).ToList() ?? new List<CharacterObject>();
            foreach (var character in visible)
            {
                var item = character;
                VisualElement card = select == null ? new VisualElement() : new Button(() => select(item));
                card.name = "character-" + item.DefinitionId;
                card.AddToClassList("character-grid-card");
                card.tooltip = item.FighterName + " · " + item.FighterRarity + " · " + item.FighterAttribute;
                var icon = CharacterIconView.Create(item, iconSize, badge?.Invoke(item)); card.Add(icon);
                var reason = unavailableReason?.Invoke(item);
                if (!string.IsNullOrEmpty(reason))
                {
                    card.AddToClassList("character-unavailable"); card.tooltip += " · " + reason;
                    var overlay = new VisualElement { name = "unavailable-overlay", pickingMode = PickingMode.Ignore };
                    overlay.AddToClassList("character-unavailable-overlay"); icon.Add(overlay);
                    if (defeated?.Invoke(item) == true)
                    {
                        card.AddToClassList("character-defeated");
                        var cross = new Label("×") { name = "defeated-cross", pickingMode = PickingMode.Ignore };
                        cross.AddToClassList("character-defeated-cross"); overlay.Add(cross);
                    }
                    if (select != null) card.SetEnabled(false);
                }
                var name = new Label(item.FighterName); name.AddToClassList("character-grid-name"); card.Add(name);
                if (enabled != null) card.SetEnabled(enabled(item) && string.IsNullOrEmpty(reason));
                grid.Add(card);
            }
            if (visible.Count == 0) { var label = new Label(emptyMessage); label.AddToClassList("grid-empty"); grid.Add(label); }
            if (fit)
            {
                if (!(grid.userData is FittedGrid)) grid.userData = new FittedGrid(grid);
                ((FittedGrid)grid.userData).Layout();
                grid.schedule.Execute(((FittedGrid)grid.userData).Layout).ExecuteLater(1);
            }
        }

        private sealed class FittedGrid
        {
            private readonly VisualElement _grid;
            public FittedGrid(VisualElement grid)
            {
                _grid = grid; grid.RegisterCallback<GeometryChangedEvent>(_ => Layout());
            }
            public void Layout()
            {
                var cards = _grid.Children().Where(c => c.ClassListContains("character-grid-card")).ToList();
                var width = _grid.contentRect.width; var height = _grid.contentRect.height;
                if (cards.Count == 0 || width <= 0 || height <= 0) return;
                var names = !_grid.ClassListContains("icon-only-preview");
                var extraHeight = names ? 32 : 14;
                // Fit individual cells into the available panel; never scale the screen root.
                var best = 0f;
                for (var columns = 1; columns <= cards.Count; columns++)
                {
                    var rows = Mathf.CeilToInt(cards.Count / (float)columns);
                    best = Mathf.Max(best, Mathf.Min(128, width / columns - 14, height / rows - extraHeight));
                }
                var size = Mathf.Max(1, Mathf.Floor(best));
                foreach (var card in cards)
                {
                    card.style.width = size + 8; card.style.height = size + extraHeight - 6;
                    var icon = card.Q("character-icon"); icon.style.width = size; icon.style.height = size;
                }
            }
        }
    }
}
