using System;

namespace FightingAllstar.Core.Content
{
    public enum TargetQuerySort { Formation, LowestHealth, LowestRecipeLayers, HighestHealth }
    public enum TargetQueryAnchor { Actor, Owner, EventTarget }

    /// <summary>Bounded selection over logical battle slots. Bindings contain frozen IDs, never live object references.</summary>
    [Serializable]
    public sealed class TargetQueryDefinition
    {
        public EffectTargetScope BaseScope = EffectTargetScope.AllEnemies;
        public TargetQueryAnchor Anchor = TargetQueryAnchor.Actor;
        public PassiveTargetFilter Filter;
        public bool ExcludeActor;
        public bool ExcludeOwner;
        public bool ExcludeEventTarget;
        public bool AdjacentToEventTarget;
        public string ExcludeBinding;
        public string ReuseBinding;
        public string StoreBinding;
        public TargetQuerySort Sort;
        public string RecipeId;
        public int Limit = 32;
        public int SampleCount;
        public TargetQueryDefinition Clone()
        {
            var copy = (TargetQueryDefinition)MemberwiseClone();
            copy.Filter = Filter?.Clone();
            return copy;
        }
    }
}
