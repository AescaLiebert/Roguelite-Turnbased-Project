using System;
using System.Collections.Generic;

namespace FightingAllstar.Core.Content
{
    public enum PassiveExecutionPhase { Before, Normal, After }
    public enum PassiveActionKind { Sequence, Branch, ForEachTarget, Command, SpendResource, TransferResource, ConsumeStatusLayers }
    public enum StatusRemovalReason { Dispelled, Cleansed, Absorbed, Expired, Replaced, Consumed, Broken, OwnerDefeated }

    public sealed partial class PassiveReactionDefinition
    {
        // Zero preserves the shipped roster's frequency/origin behavior. Studio M3 emits version one.
        public int ExecutionVersion;
        public PassiveExecutionPhase Phase = PassiveExecutionPhase.Normal;
        public int Priority;
        public string EventResourceKey;
        public string EventStatusRecipeId;
        public bool FilterRemovalReason;
        public StatusRemovalReason RemovalReason;
        [field: NonSerialized]
        public List<PassiveActionDefinition> Actions { get; set; } = new List<PassiveActionDefinition>();
    }

    /// <summary>Compiled execution tree. Editor storage remains a flat stable-ID node table.</summary>
    [Serializable]
    public sealed class PassiveActionDefinition
    {
        public string Id;
        public PassiveActionKind Kind;
        public List<EffectConditionDefinition> Conditions = new List<EffectConditionDefinition>();
        public List<PassiveCommandDefinition> Commands = new List<PassiveCommandDefinition>();
        public EffectTargetScope Target = EffectTargetScope.Self;
        public int MaximumTargets = 16;
        public TargetQueryDefinition Query;
        public string ResourceKey;
        public int Amount = 1;
        public int ResourceCap = int.MaxValue;
        public string StatusRecipeId;
        public bool StatusOwnerOnly = true;
        public bool RequireFullAmount = true;
        public StatusRemovalReason RemovalReason = StatusRemovalReason.Consumed;
        public string ResultBinding;
        [field: NonSerialized]
        public List<PassiveActionDefinition> Children { get; set; } = new List<PassiveActionDefinition>();
        [field: NonSerialized]
        public List<PassiveActionDefinition> ElseChildren { get; set; } = new List<PassiveActionDefinition>();

        public PassiveActionDefinition Clone()
        {
            var copy = (PassiveActionDefinition)MemberwiseClone();
            copy.Conditions = Conditions?.ConvertAll(c => c?.Clone()) ?? new List<EffectConditionDefinition>();
            copy.Commands = Commands?.ConvertAll(c => c?.Clone()) ?? new List<PassiveCommandDefinition>();
            copy.Query = Query?.Clone();
            copy.Children = Children?.ConvertAll(a => a?.Clone()) ?? new List<PassiveActionDefinition>();
            copy.ElseChildren = ElseChildren?.ConvertAll(a => a?.Clone()) ?? new List<PassiveActionDefinition>();
            return copy;
        }
    }
}
