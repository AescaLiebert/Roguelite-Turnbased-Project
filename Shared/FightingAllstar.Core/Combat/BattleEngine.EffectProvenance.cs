namespace FightingAllstar.Core.Combat
{
    public static partial class BattleEngine
    {
        private static void StampEffectEvents(BattleState state, int start, EffectProvenance source)
        {
            if (source == null) return;
            for (var i = start; i < state.Events.Count; i++)
            {
                var item = state.Events[i];
                if (item.EffectOrigin != EffectOrigin.Unspecified) continue;
                item.EffectOrigin = source.Origin; item.EffectId = source.EffectId;
                item.GrantingAbilityId = source.AbilityId; item.GrantingRank = source.Rank; item.GrantingTier = source.Tier;
                // Keep a newly applied status's own instance ID.
                if (item.Kind == BattleEventKind.PassiveTriggered) item.StatusInstanceId = source.StatusInstanceId;
            }
        }
    }
}
