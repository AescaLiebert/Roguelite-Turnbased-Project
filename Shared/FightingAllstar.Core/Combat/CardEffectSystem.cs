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
                if (!EvaluateCondition(condition, context, target)) return false;
            }
            return true;
        }

        private static bool EvaluateCondition(EffectConditionDefinition condition, CardEffectContext context,
            FighterState target)
        {
            if (condition == null) return true;
            bool passed;
            switch (condition.Logic)
            {
                case ConditionLogic.All:
                    passed = true;
                    if (condition.Children != null)
                        foreach (var child in condition.Children)
                            if (!EvaluateCondition(child, context, target)) { passed = false; break; }
                    break;
                case ConditionLogic.Any:
                    passed = false;
                    if (condition.Children != null)
                        foreach (var child in condition.Children)
                            if (EvaluateCondition(child, context, target)) { passed = true; break; }
                    break;
                case ConditionLogic.Not:
                    passed = condition.Children == null || condition.Children.Count == 0 ||
                        !EvaluateCondition(condition.Children[0], context, target);
                    break;
                default:
                    passed = Evaluate(condition, context, target);
                    break;
            }
            return condition.Negate ? !passed : passed;
        }

        private static bool Evaluate(EffectConditionDefinition condition, CardEffectContext context,
            FighterState target)
        {
            var source = context.Actor;
            var subject = ResolveSubject(condition.Subject, context, target);
            switch (condition.Kind)
            {
                case EffectConditionKind.Always: return true;
                case EffectConditionKind.SourceHasStatusTag:
                    return StatusSystem.Count(source, requiredTag: condition.Tag) >= Math.Max(1, condition.Threshold);
                case EffectConditionKind.TargetHasStatusTag:
                    return StatusSystem.Count(subject, requiredTag: condition.Tag) >= Math.Max(1, condition.Threshold);
                case EffectConditionKind.TargetHasBuff:
                    return StatusSystem.Count(subject, StatusPolarity.Buff) >= Math.Max(1, condition.Threshold);
                case EffectConditionKind.TargetHasDebuff:
                    return StatusSystem.Count(subject, StatusPolarity.Debuff) >= Math.Max(1, condition.Threshold);
                case EffectConditionKind.TargetHasRecipe:
                    return StatusSystem.Count(subject, recipeId: condition.RecipeId) >= Math.Max(1, condition.Threshold);
                case EffectConditionKind.TargetHasRecipeFromEffectOwner:
                    return StatusSystem.HasRecipeFromSource(subject, condition.RecipeId,
                        (context.EffectOwner ?? context.Actor)?.Id);
                case EffectConditionKind.ActorIsEffectOwner:
                    return context.EffectOwner != null && context.Actor.Id == context.EffectOwner.Id;
                case EffectConditionKind.ActorIsAllyOfEffectOwner:
                    return context.EffectOwner != null && context.Actor.Id != context.EffectOwner.Id &&
                        context.Actor.Side == context.EffectOwner.Side;
                case EffectConditionKind.ActorIsEnemyOfEffectOwner:
                    return context.EffectOwner != null && context.Actor.Side != context.EffectOwner.Side;
                case EffectConditionKind.TargetAttributeIs:
                    return MatchesContentId(subject?.Definition?.AttributeId, condition.StringValue, "attribute.");
                case EffectConditionKind.WasCritical: return context.WasCritical;
                case EffectConditionKind.WasBlocked: return context.WasBlocked;
                case EffectConditionKind.SourceGaugeAtLeast: return source.PowerGauge >= condition.Threshold;
                case EffectConditionKind.TargetGaugeAtLeast: return subject != null && subject.PowerGauge >= condition.Threshold;
                case EffectConditionKind.SourceHealthAtMost: return HealthRatioBp(source) <= condition.Threshold;
                case EffectConditionKind.TargetHealthAtMost: return HealthRatioBp(subject) <= condition.Threshold;
                case EffectConditionKind.CounterAtLeast:
                    return CharacterPassiveRuntime.Counter(context.EffectOwner ?? source,
                        FirstValue(condition.StringValue, condition.Tag, condition.RecipeId)) >= condition.Threshold;
                case EffectConditionKind.TargetTraitIs:
                    return ContainsContentId(subject?.Definition?.TraitIds,
                        FirstValue(condition.StringValue, condition.Tag), "trait.");
                case EffectConditionKind.TargetSeriesIs:
                    return MatchesContentId(subject?.Definition?.SeriesId,
                        FirstValue(condition.StringValue, condition.Tag), "series.");
                case EffectConditionKind.RosterCountAtLeast:
                    return RosterCount(context, condition) >= Math.Max(1, condition.Threshold);
                case EffectConditionKind.TargetIsAlive:
                    return subject != null && subject.IsAlive && subject.Health > 0;
                case EffectConditionKind.TargetIsLowestHealthEnemy:
                    return IsLowestHealthEnemy(context?.Battle, source, subject);
                case EffectConditionKind.ActorWasNotDamagedSincePreviousTurnStart:
                    return !WasDamagedSincePreviousTeamTurnStart(context?.Battle, source);
                case EffectConditionKind.ActorWasDamagedDuringPreviousEnemyTurn:
                    return WasDamagedDuringPreviousEnemyTurn(context?.Battle, source);
                case EffectConditionKind.ActorWasNotDamagedDuringPreviousEnemyTurn:
                    return !WasDamagedDuringPreviousEnemyTurn(context?.Battle, source);
                default: return false;
            }
        }

        private static FighterState ResolveSubject(ConditionSubject selection, CardEffectContext context,
            FighterState operationTarget)
        {
            return selection switch
            {
                ConditionSubject.Owner => context?.EffectOwner ?? context?.Actor,
                ConditionSubject.Actor => context?.Actor,
                ConditionSubject.EventTarget => context?.SelectedTarget,
                _ => operationTarget
            };
        }

        private static bool IsLowestHealthEnemy(BattleState battle, FighterState actor, FighterState target)
        {
            if (battle == null || actor == null || target == null || target.Side == actor.Side ||
                !target.IsAlive || target.Health <= 0) return false;
            var enemies = battle.OtherTeam(actor.Side).LivingActive();
            if (enemies == null || enemies.Count == 0) return false;
            var lowestHealth = int.MaxValue;
            foreach (var enemy in enemies)
                if (enemy != null && enemy.IsAlive && enemy.Health > 0)
                    lowestHealth = Math.Min(lowestHealth, enemy.Health);
            return target.Health == lowestHealth;
        }

        private static string FirstValue(params string[] values)
        {
            foreach (var value in values)
                if (!string.IsNullOrWhiteSpace(value)) return value;
            return string.Empty;
        }

        private static bool ContainsContentId(List<string> values, string authored, string prefix)
        {
            if (values == null) return false;
            foreach (var value in values)
                if (MatchesContentId(value, authored, prefix)) return true;
            return false;
        }

        private static bool MatchesContentId(string actual, string authored, string prefix)
        {
            if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(authored)) return false;
            var expected = authored.Trim();
            if (!expected.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) expected = prefix + expected;
            return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        }

        private static int RosterCount(CardEffectContext context, EffectConditionDefinition condition)
        {
            if (context?.Battle == null) return 0;
            var owner = context.EffectOwner ?? context.Actor;
            var filter = condition.RosterFilter ?? new PassiveTargetFilter { Relation = PassiveRelation.Any };
            var count = 0;
            CountRoster(context.Battle.Player, owner, filter, condition.InitialRoster, ref count);
            CountRoster(context.Battle.Opponent, owner, filter, condition.InitialRoster, ref count);
            return count;
        }

        private static void CountRoster(BattleTeamState team, FighterState owner, PassiveTargetFilter filter,
            bool initialRoster, ref int count)
        {
            if (team?.Fighters == null) return;
            foreach (var fighter in team.Fighters)
            {
                if (fighter == null || !initialRoster && (!fighter.IsAlive || fighter.Health <= 0) ||
                    !filter.IncludeReserve && fighter.IsReserve || !filter.IncludeOwner && fighter.Id == owner?.Id)
                    continue;
                var related = filter.Relation == PassiveRelation.Any || owner != null &&
                    (filter.Relation == PassiveRelation.Self ? fighter.Id == owner.Id :
                     filter.Relation == PassiveRelation.Allies ? fighter.Side == owner.Side : fighter.Side != owner.Side);
                if (!related || !string.IsNullOrEmpty(filter.AttributeId) && fighter.Definition?.AttributeId != filter.AttributeId ||
                    !string.IsNullOrEmpty(filter.SeriesId) && fighter.Definition?.SeriesId != filter.SeriesId ||
                    !string.IsNullOrEmpty(filter.TraitId) && fighter.Definition?.TraitIds?.Contains(filter.TraitId) != true)
                    continue;
                count++;
            }
        }

        private static bool WasDamagedSincePreviousTeamTurnStart(BattleState battle, FighterState actor)
        {
            if (battle?.Events == null || actor == null) return false;
            var sideKey = actor.Side.ToString();
            var foundCurrentStart = false;
            for (var i = battle.Events.Count - 1; i >= 0; i--)
            {
                var item = battle.Events[i];
                if (item == null) continue;
                if (item.Kind == BattleEventKind.TurnStarted && item.SourceId == sideKey)
                {
                    if (foundCurrentStart) break;
                    foundCurrentStart = true;
                    continue;
                }
                if (foundCurrentStart && item.Kind == BattleEventKind.DamageApplied && item.TargetId == actor.Id && item.Amount > 0)
                    return true;
            }
            return false;
        }

        private static bool WasDamagedDuringPreviousEnemyTurn(BattleState battle, FighterState actor)
        {
            if (battle?.Events == null || actor == null) return false;
            var enemySideKey = (actor.Side == TeamSide.Player ? TeamSide.Opponent : TeamSide.Player).ToString();
            var endIndex = -1;
            for (var i = battle.Events.Count - 1; i >= 0; i--)
            {
                var item = battle.Events[i];
                if (item?.Kind == BattleEventKind.TurnEnded && item.SourceId == enemySideKey)
                { endIndex = i; break; }
            }
            if (endIndex < 0) return false;
            for (var i = endIndex - 1; i >= 0; i--)
            {
                var item = battle.Events[i];
                if (item?.Kind != BattleEventKind.TurnStarted || item.SourceId != enemySideKey) continue;
                for (var j = i + 1; j < endIndex; j++)
                    if (battle.Events[j]?.Kind == BattleEventKind.DamageApplied &&
                        battle.Events[j].TargetId == actor.Id && battle.Events[j].Amount > 0)
                        return true;
                return false;
            }
            return false;
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
