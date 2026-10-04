using System;
using System.Collections.Generic;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    /// <summary>Facts available to card and passive conditions. It contains IDs and resolved outcomes, never presentation state.</summary>
    public sealed class CardEffectContext
    {
        public BattleState Battle;
        public FighterState EffectOwner;
        public FighterState Actor;
        public FighterState SelectedTarget;
        public string RootActionId;
        public CardCategory CardCategory;
        public int CardRank;
        public bool IsUltimate;
        public bool WasCritical;
        public bool WasBlocked;
        public int ActualHealthDamage;
        public int ActualShieldDamage;
        public DamageFamily DamageFamily;
    }

    public sealed class ResolvedCardEffect
    {
        public CardEffectOperationDefinition Operation;
        public FighterState Target;
    }

    /// <summary>Resolves ordered recipe operations and ownership-aware conditions without mutating battle state.</summary>
    public static class CardEffectSystem
    {
        public static List<ResolvedCardEffect> ResolveWindow(CardEffectRecipeDefinition recipe,
            CardEffectWindow window, CardEffectContext context)
        {
            var result = new List<ResolvedCardEffect>();
            if (recipe?.Operations == null || context?.Battle == null || context.Actor == null) return result;
            var operations = new List<CardEffectOperationDefinition>();
            foreach (var operation in recipe.Operations)
                if (operation != null && operation.Window == window) operations.Add(operation);
            operations.Sort((left, right) => left.Order != right.Order
                ? left.Order.CompareTo(right.Order) : string.CompareOrdinal(left.Id, right.Id));

            foreach (var operation in operations)
                foreach (var target in ResolveTargets(operation.Target, context))
                    if (AllConditionsPass(operation.Conditions, context, target))
                        result.Add(new ResolvedCardEffect { Operation = operation.Clone(), Target = target });
            return result;
        }

        public static bool ConditionsPass(List<EffectConditionDefinition> conditions,
            CardEffectContext context, FighterState target) => AllConditionsPass(conditions, context, target);

        public static int ResolveValue(EffectValueDefinition value, CardEffectContext context, FighterState target)
        {
            if (value == null) return 0;
            long basis = value.Source switch
            {
                EffectValueSource.ActualHealthDamage => context?.ActualHealthDamage ?? 0,
                EffectValueSource.ActualShieldDamage => context?.ActualShieldDamage ?? 0,
                EffectValueSource.SourceAttack => context?.Actor == null ? 0 : StatusSystem.GetEffectiveStats(context.Actor).Attack,
                EffectValueSource.TargetMaxHealth => target == null ? 0 : StatusSystem.GetEffectiveStats(target).MaxHealth,
                EffectValueSource.TargetMissingHealth => target == null ? 0 :
                    Math.Max(0, StatusSystem.GetEffectiveStats(target).MaxHealth - target.Health),
                _ => value.FixedAmount
            };
            var resolved = Math.Max(0L, basis) * Math.Max(0, value.CoefficientBp) / 10000;
            return (int)Math.Min(Math.Max(0, value.MaximumAmount), Math.Min(int.MaxValue, resolved));
        }

        public static DamagePacket CreateDamagePacket(DamageEffectRecipe recipe, FighterState source,
            FighterState target, AttackEffectRecipeDefinition attackEffect = null, BattleState battle = null)
        {
            if (recipe == null) throw new ArgumentNullException(nameof(recipe));
            var calculation = AttackEffectSystem.Prepare(attackEffect, battle, source, target, recipe.Family);
            var sourceStats = calculation.Attacker;
            var baseAmount = recipe.Scaling switch
            {
                StatScaling.Attack => sourceStats.Attack,
                StatScaling.Defense => sourceStats.Defense,
                StatScaling.MaxHealth => sourceStats.MaxHealth,
                StatScaling.SpecificStat => sourceStats.Get(recipe.ScalingStat),
                _ => recipe.FixedAmount
            };
            var policy = calculation.Policy;
            policy.CannotCrit |= recipe.CannotCrit;
            policy.CannotBlock |= recipe.CannotBlock;
            return new DamagePacket { BaseAmount = Math.Max(0, baseAmount),
                CoefficientBp = Math.Max(0, recipe.CoefficientBp), KeywordFactorBp = Math.Max(0,
                    (int)Math.Min(int.MaxValue, (long)recipe.KeywordFactorBp * calculation.KeywordFactorBp / 10000)),
                Policy = policy };
        }

        private static bool AllConditionsPass(List<EffectConditionDefinition> conditions,
            CardEffectContext context, FighterState target)
        {
            if (conditions == null) return true;
            foreach (var condition in conditions)
            {
                if (condition == null) continue;
                var passed = Evaluate(condition, context, target);
                if (condition.Negate) passed = !passed;
                if (!passed) return false;
            }
            return true;
        }

        private static bool Evaluate(EffectConditionDefinition condition, CardEffectContext context,
            FighterState target)
        {
            var source = context.Actor;
            switch (condition.Kind)
            {
                case EffectConditionKind.Always: return true;
                case EffectConditionKind.SourceHasStatusTag:
                    return StatusSystem.Count(source, requiredTag: condition.Tag) >= Math.Max(1, condition.Threshold);
                case EffectConditionKind.TargetHasStatusTag:
                    return StatusSystem.Count(target, requiredTag: condition.Tag) >= Math.Max(1, condition.Threshold);
                case EffectConditionKind.TargetHasBuff:
                    return StatusSystem.Count(target, StatusPolarity.Buff) >= Math.Max(1, condition.Threshold);
                case EffectConditionKind.TargetHasDebuff:
                    return StatusSystem.Count(target, StatusPolarity.Debuff) >= Math.Max(1, condition.Threshold);
                case EffectConditionKind.TargetHasRecipe:
                    return StatusSystem.Count(target, recipeId: condition.RecipeId) >= Math.Max(1, condition.Threshold);
                case EffectConditionKind.TargetHasRecipeFromEffectOwner:
                    return StatusSystem.HasRecipeFromSource(target, condition.RecipeId,
                        (context.EffectOwner ?? context.Actor)?.Id);
                case EffectConditionKind.ActorIsEffectOwner:
                    return context.EffectOwner != null && context.Actor.Id == context.EffectOwner.Id;
                case EffectConditionKind.ActorIsAllyOfEffectOwner:
                    return context.EffectOwner != null && context.Actor.Id != context.EffectOwner.Id &&
                        context.Actor.Side == context.EffectOwner.Side;
                case EffectConditionKind.ActorIsEnemyOfEffectOwner:
                    return context.EffectOwner != null && context.Actor.Side != context.EffectOwner.Side;
                case EffectConditionKind.TargetAttributeIs:
                    return target?.Definition?.AttributeId == condition.StringValue;
                case EffectConditionKind.WasCritical: return context.WasCritical;
                case EffectConditionKind.WasBlocked: return context.WasBlocked;
                case EffectConditionKind.SourceGaugeAtLeast: return source.PowerGauge >= condition.Threshold;
                case EffectConditionKind.TargetGaugeAtLeast: return target != null && target.PowerGauge >= condition.Threshold;
                case EffectConditionKind.SourceHealthAtMost: return HealthRatioBp(source) <= condition.Threshold;
                case EffectConditionKind.TargetHealthAtMost: return HealthRatioBp(target) <= condition.Threshold;
                default: return false;
            }
        }

        private static List<FighterState> ResolveTargets(EffectTargetScope scope, CardEffectContext context)
        {
            var result = new List<FighterState>();
            var actorTeam = context.Battle.Team(context.Actor.Side);
            var enemyTeam = context.Battle.OtherTeam(context.Actor.Side);
            switch (scope)
            {
                case EffectTargetScope.Self: AddAlive(result, context.Actor); break;
                case EffectTargetScope.SelectedEnemy:
                    if (context.SelectedTarget != null && context.SelectedTarget.Side != context.Actor.Side)
                        AddAlive(result, context.SelectedTarget);
                    break;
                case EffectTargetScope.SelectedAlly:
                    if (context.SelectedTarget != null && context.SelectedTarget.Side == context.Actor.Side)
                        AddAlive(result, context.SelectedTarget);
                    break;
                case EffectTargetScope.AllEnemies: AddAllActive(result, enemyTeam); break;
                case EffectTargetScope.AllAllies: AddAllActive(result, actorTeam); break;
                case EffectTargetScope.TriggerActor: AddAlive(result, context.Actor); break;
                case EffectTargetScope.TriggerTarget: AddAlive(result, context.SelectedTarget); break;
                case EffectTargetScope.StatusOwner: AddAlive(result, context.EffectOwner); break;
            }
            return result;
        }

        private static void AddAllActive(List<FighterState> result, BattleTeamState team)
        {
            if (team == null) return;
            foreach (var fighter in team.LivingActive()) result.Add(fighter);
        }

        private static void AddAlive(List<FighterState> result, FighterState fighter)
        {
            if (fighter != null && fighter.IsAlive) result.Add(fighter);
        }

        private static int HealthRatioBp(FighterState fighter)
        {
            var maxHealth = fighter == null ? 0 : Math.Max(1, StatusSystem.GetEffectiveStats(fighter).MaxHealth);
            return fighter == null ? 0 : (int)Math.Max(0L, Math.Min(10000L, (long)fighter.Health * 10000 / maxHealth));
        }
    }
}
