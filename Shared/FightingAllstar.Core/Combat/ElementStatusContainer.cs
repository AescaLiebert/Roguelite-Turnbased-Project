using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    [Serializable]
    public sealed class ElementStatusInstance
    {
        public string InstanceId;
        public ElementId Element;
        public string SourceFighterId;
        public int RemainingDuration = ElementStatusContainer.Duration;
        public StatusPolarity Polarity => StatusPolarity.Neutral;
        public ElementStatusInstance Clone() => (ElementStatusInstance)MemberwiseClone();

        // Presentation adapter only. This instance is never inserted into StatusContainer.
        public StatusInstance ToDisplayStatus() => new StatusInstance {
            InstanceId = InstanceId, RecipeId = "status.element." + Element.ToString().ToLowerInvariant(),
            SourceFighterId = SourceFighterId, RemainingDuration = RemainingDuration,
            Recipe = new StatusRecipeDefinition { Id = "status.element." + Element.ToString().ToLowerInvariant(),
                NameKey = Element.ToString(), Polarity = StatusPolarity.Neutral,
                DurationClock = StatusDurationClock.TargetTurnEnd, DefaultDuration = ElementStatusContainer.Duration,
                MaxStacks = 1, Stacking = StatusStackingPolicy.IgnoreExisting }
        };
    }

    /// <summary>Neutral reaction auras. Unique per element; repeated contacts never refresh the clock.</summary>
    [Serializable]
    public sealed class ElementStatusContainer
    {
        public const int Duration = 3;
        public List<ElementStatusInstance> Instances = new List<ElementStatusInstance>();
        public static bool CanApply(ElementId element) => element >= ElementId.Pyro && element <= ElementId.Dendro;
        public bool Has(ElementId element) => Instances.Exists(s => s.Element == element && s.RemainingDuration > 0);
        public bool Apply(ElementId element, string sourceId, string instanceId)
        {
            if (!CanApply(element) || Has(element)) return false;
            Instances.Add(new ElementStatusInstance { Element = element, SourceFighterId = sourceId, InstanceId = instanceId });
            return true;
        }
        public bool RemoveInstance(string id) => Instances.RemoveAll(s => s.InstanceId == id) > 0;
        public bool AdvanceTargetTurnEnd()
        {
            if (Instances.Count == 0) return false;
            foreach (var status in Instances) status.RemainingDuration--;
            Instances.RemoveAll(s => s.RemainingDuration <= 0);
            return true;
        }
        public ElementStatusContainer Clone() => new ElementStatusContainer { Instances = Instances.ConvertAll(s => s.Clone()) };
    }
}
