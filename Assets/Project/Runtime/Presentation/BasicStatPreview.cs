using FightingAllstar.Core.Content;
using UnityEngine.UIElements;
namespace FightingAllstar.Presentation
{
    /// <summary>The same four-stat composition for kit, progression and encounter inspection.</summary>
    public static class BasicStatPreview
    {
        public static void Render(VisualElement container, StatBlock current, StatBlock next = null)
        {
            if (container == null || current == null) return;
            container.Clear(); container.AddToClassList("basic-stat-preview");
            Add(container, "CC", current.CombatClass, next?.CombatClass);
            Add(container, "ATK", current.Attack, next?.Attack);
            Add(container, "DEF", current.Defense, next?.Defense);
            Add(container, "HP", current.MaxHealth, next?.MaxHealth);
        }
        private static void Add(VisualElement container, string stat, int value, int? next)
        {
            var row = new VisualElement(); row.AddToClassList("basic-stat-row");
            AddLabel(row, stat, "basic-stat-name"); AddLabel(row, value.ToString("N0"), "basic-stat-current");
            if (next.HasValue) { AddLabel(row, "→", "basic-stat-arrow"); AddLabel(row, next.Value.ToString("N0") + (next.Value > value ? "  ↑" : ""), "basic-stat-next"); }
            container.Add(row);
        }
        private static void AddLabel(VisualElement row, string text, string css) { var label = new Label(text); label.AddToClassList(css); row.Add(label); }
    }
}
