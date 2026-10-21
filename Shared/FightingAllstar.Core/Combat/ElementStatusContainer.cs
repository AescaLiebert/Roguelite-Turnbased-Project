using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    [Serializable]
    public sealed class ElementalReactionStatusInstance
    {
        public string InstanceId;
        public ElementalReactionId Reaction;
        public string SourceFighterId;
        public int RemainingDuration = 2;
        public int SourceAttackSnapshot;
        public int FixedDamageSnapshot;
        public int DamageCoefficientBp;
        public ElementId DamageElement;
        public int NonElementalDamageTakenBp;
        public bool SkipNextDurationClock;

        public string RecipeId => Reaction == ElementalReactionId.ElectroCharged ? "status.reaction.charged" :
            "status.reaction." + Reaction.ToString().ToLowerInvariant();
        public ElementalReactionStatusInstance Clone() => (ElementalReactionStatusInstance)MemberwiseClone();

        // Presentation adapter only. Reaction conditions remain outside StatusContainer.
        public StatusInstance ToDisplayStatus()
        {
            var displayElement = Reaction == ElementalReactionId.Superconduct ? ElementId.Cryo :
                Reaction == ElementalReactionId.ElectroCharged ? ElementId.Electro : ElementId.Pyro;
            var isBurning = Reaction == ElementalReactionId.Burning;
            return new StatusInstance {
                InstanceId = InstanceId, RecipeId = "status.element." + displayElement.ToString().ToLowerInvariant(),
                SourceFighterId = SourceFighterId, RemainingDuration = RemainingDuration,
                Recipe = new StatusRecipeDefinition { Id = "status.element." + displayElement.ToString().ToLowerInvariant(),
                    NameKey = Reaction.ToString(), Polarity = StatusPolarity.Neutral,
                    DurationClock = StatusDurationClock.TargetTurnEnd, DefaultDuration = ElementStatusContainer.ReactionDuration,
                    MaxStacks = 9,
                    Stacking = isBurning ? StatusStackingPolicy.IndependentStacks : StatusStackingPolicy.IgnoreExisting }
            };
        }
    }

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
                MaxStacks = 9, Stacking = StatusStackingPolicy.IndependentStacks }
        };
    }

    /// <summary>Neutral reaction auras. Unique per element; repeated contacts never refresh the clock.</summary>
    [Serializable]
    public sealed class ElementStatusContainer
    {
        public const int Duration = 3;
        public const int ReactionDuration = 2;
        public const int BurningMaxStacks = 3;
        public List<ElementStatusInstance> Instances = new List<ElementStatusInstance>();
        public List<ElementalReactionStatusInstance> ReactionStatuses = new List<ElementalReactionStatusInstance>();
        public static bool CanApply(ElementId element) => element >= ElementId.Pyro && element <= ElementId.Dendro;
        public bool Has(ElementId element) => Instances.Exists(s => s.Element == element && s.RemainingDuration > 0);
        public bool HasReaction(ElementalReactionId reaction) => ReactionStatuses != null &&
            ReactionStatuses.Exists(s => s.Reaction == reaction && s.RemainingDuration > 0);
        public bool Apply(ElementId element, string sourceId, string instanceId)
        {
            if (!CanApply(element) || Has(element)) return false;
            Instances.Add(new ElementStatusInstance { Element = element, SourceFighterId = sourceId, InstanceId = instanceId });
            return true;
        }
        public bool ApplyReaction(ElementalReactionId reaction, string sourceId, string instanceId,
            int sourceAttackSnapshot, int fixedDamageSnapshot, bool skipNextDurationClock = false)
        {
            if (reaction != ElementalReactionId.Superconduct && reaction != ElementalReactionId.ElectroCharged &&
                reaction != ElementalReactionId.Burning) return false;
            if (ReactionStatuses == null) ReactionStatuses = new List<ElementalReactionStatusInstance>();
            if (ReactionStatuses.Exists(s => s.InstanceId == instanceId)) return false;
            var burning = reaction == ElementalReactionId.Burning;
            if (burning ? ReactionStatuses.FindAll(s => s.Reaction == reaction && s.RemainingDuration > 0).Count >= BurningMaxStacks
                : HasReaction(reaction)) return false;
            ReactionStatuses.Add(new ElementalReactionStatusInstance {
                InstanceId = instanceId, Reaction = reaction, SourceFighterId = sourceId,
                RemainingDuration = ReactionDuration, SourceAttackSnapshot = sourceAttackSnapshot,
                FixedDamageSnapshot = fixedDamageSnapshot,
                DamageCoefficientBp = reaction == ElementalReactionId.Burning ? 30000 :
                    reaction == ElementalReactionId.ElectroCharged ? 15000 : 0,
                DamageElement = reaction == ElementalReactionId.Burning ? ElementId.Pyro :
                    reaction == ElementalReactionId.ElectroCharged ? ElementId.Electro : ElementId.Cryo,
                NonElementalDamageTakenBp = reaction == ElementalReactionId.Superconduct ? 4000 : 0,
                SkipNextDurationClock = skipNextDurationClock
            });
            return true;
        }
        public bool AddLegacyReaction(ElementalReactionStatusInstance status)
        {
            if (status == null || (status.Reaction != ElementalReactionId.Superconduct &&
                status.Reaction != ElementalReactionId.ElectroCharged && status.Reaction != ElementalReactionId.Burning)) return false;
            if (ReactionStatuses == null) ReactionStatuses = new List<ElementalReactionStatusInstance>();
            if (ReactionStatuses.Exists(s => s.InstanceId == status.InstanceId)) return false;
            ReactionStatuses.Add(status.Clone());
            return true;
        }
        public bool RemoveInstance(string id) => Instances.RemoveAll(s => s.InstanceId == id) > 0;
        public bool RemoveReaction(string id) => ReactionStatuses != null && ReactionStatuses.RemoveAll(s => s.InstanceId == id) > 0;
        public bool AdvanceTargetTurnEnd()
        {
            if ((Instances == null || Instances.Count == 0) && (ReactionStatuses == null || ReactionStatuses.Count == 0)) return false;
            if (Instances == null) Instances = new List<ElementStatusInstance>();
            foreach (var status in Instances) status.RemainingDuration--;
            Instances.RemoveAll(s => s.RemainingDuration <= 0);
            if (ReactionStatuses == null) ReactionStatuses = new List<ElementalReactionStatusInstance>();
            foreach (var status in ReactionStatuses)
            {
                if (status.SkipNextDurationClock) { status.SkipNextDurationClock = false; continue; }
                status.RemainingDuration--;
            }
            ReactionStatuses.RemoveAll(s => s.RemainingDuration <= 0);
            return true;
        }
        public ElementStatusContainer Clone() => new ElementStatusContainer {
            Instances = Instances?.ConvertAll(s => s?.Clone()) ?? new List<ElementStatusInstance>(),
            ReactionStatuses = ReactionStatuses?.ConvertAll(s => s?.Clone()) ?? new List<ElementalReactionStatusInstance>()
        };
    }
}
