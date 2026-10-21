#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using FightingAllstar.Core.Studio;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    /// <summary>Passive inventory rendering; semantic ownership and summaries live in the shared services.</summary>
    internal static class StudioPassiveOverviewPresenter
    {
        public static void Build(VisualElement parent, IEnumerable<StudioPassiveMechanicView> mechanics, string expandedId,
            Action<StudioPassiveMechanicView, bool> expand, Action<VisualElement, StudioPassiveMechanicView> editor)
        {
            parent.AddToClassList("studio-passive-grid");
            parent.RegisterCallback<GeometryChangedEvent>(e => parent.EnableInClassList("studio-passive-grid-narrow", e.newRect.width < 640));
            foreach (var item in mechanics)
            {
                var mechanic = item;
                var card = new VisualElement { name = "passive.mechanic." + item.Kind + "." + item.Id };
                card.AddToClassList("studio-node"); card.AddToClassList("studio-passive-card");
                card.EnableInClassList("studio-passive-card-open", expandedId == item.Id); parent.Add(card);
                var kind = new Label(item.Kind.ToUpperInvariant() + " · " + item.State) { name = "passive.state." + item.Id }; kind.AddToClassList("studio-section-label"); card.Add(kind);
                var name = new Label(item.Name) { name = "passive.name." + item.Id, tooltip = item.Id }; name.AddToClassList("studio-passive-card-title"); card.Add(name);
                var summary = new Label(item.Summary) { name = "passive.summary." + item.Id }; summary.AddToClassList("studio-body"); card.Add(summary);
                var availability = new Label(item.Availability) { name = "passive.availability." + item.Id }; availability.AddToClassList("studio-subtle"); card.Add(availability);
                var fold = new Foldout { text = expandedId == item.Id ? "Collapse mechanic" : "Edit mechanic", value = expandedId == item.Id, name = "passive.open." + item.Id };
                fold.AddToClassList("studio-passive-mechanic-fold"); card.Add(fold);
                fold.RegisterValueChangedCallback(e => { if (e.target == fold) expand(mechanic, e.newValue); });
                if (fold.value) editor(fold, mechanic);
            }
        }

        public static void Refresh(VisualElement parent, IEnumerable<StudioPassiveMechanicView> mechanics)
        {
            foreach (var item in mechanics)
            {
                var name = parent.Q<Label>("passive.name." + item.Id); if (name != null) name.text = item.Name;
                var summary = parent.Q<Label>("passive.summary." + item.Id); if (summary != null) summary.text = item.Summary;
                var availability = parent.Q<Label>("passive.availability." + item.Id); if (availability != null) availability.text = item.Availability;
                var state = parent.Q<Label>("passive.state." + item.Id); if (state != null) state.text = item.Kind.ToUpperInvariant() + " · " + item.State;
            }
        }
    }
}
#endif
