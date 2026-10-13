using System.Collections.Generic;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

internal static partial class BattlePlaybackChecks
{
    private static void SophitiaPassiveChecks()
    {
        var passive = new PassiveDefinition { Id = "fighter.sciii-sophitia.passive.source" };
        var damageBoost = new PassiveReactionDefinition { Id = "crit-chance-on-noncritical-hit",
            Trigger = PassiveEventKind.BeforeDamage, ActorRelation = PassiveRelation.Self,
            CardOriginOnly = true, FilterDamageFamily = true, DamageFamily = DamageFamily.Normal };
        damageBoost.Commands.Add(new PassiveCommandDefinition { Kind = PassiveCommandKind.IncreaseCurrentDamageDealtByBaseStatPercent,
            Amount = 1, ScalingStat = StatId.CritChance });
        passive.Reactions.Add(damageBoost);
        var chanceStack = new PassiveReactionDefinition { Id = "stack-crit-chance-on-taunt-hit",
            Trigger = PassiveEventKind.DamageResolved, DamageTiming = PassiveDamageTiming.FinalHit,
            ActorRelation = PassiveRelation.Enemies, TargetRelation = PassiveRelation.Self,
            CardOriginOnly = true, FilterDamageFamily = true, DamageFamily = DamageFamily.Normal };
        chanceStack.Conditions.Add(new EffectConditionDefinition { Kind = EffectConditionKind.TargetHasStatusTag,
            Tag = "sophitia.stance.rank.1" });
        chanceStack.Commands.Add(StackBuff("status.increase.crit-chance", "Crit Chance", StatId.CritChance, 700));
        chanceStack.Commands.Add(StackBuff("status.increase.crit-damage", "Crit Damage", StatId.CritDamage, 1500));
        passive.Reactions.Add(chanceStack);
        Check(PassiveRuleValidator.Validate(passive).Count == 0, "Sophitia's passive rules should validate.");

        var sophitia = new FighterState { Id = "sophitia", Side = TeamSide.Player, Health = 1000,
            Definition = new CharacterDefinition { Id = "fighter.sciii-sophitia", Passive = passive,
                BaseStats = new StatBlock { Attack = 100, MaxHealth = 1000, CritChanceBp = 3500,
                    CritDamageBp = 15000 } } };
        var enemy = new FighterState { Id = "enemy", Side = TeamSide.Opponent, Health = 1000,
            Definition = new CharacterDefinition { Id = "enemy", BaseStats = new StatBlock { Attack = 100,
                Defense = 0, MaxHealth = 1000 } } };
        var state = State(sophitia, enemy);
        var critBuff = new StatusRecipeDefinition { Id = "test.crit-chance-buff", Polarity = StatusPolarity.Buff,
            Behavior = StatusBehavior.Stat, Modifiers = new List<StatModifierDefinition> {
                new StatModifierDefinition { Target = ModifierTarget.Stat, Stat = StatId.CritChance,
                    Amount = 2000, Operation = ModifierOperation.PercentagePoints } } };
        sophitia.Statuses.Instances.Add(new StatusInstance { RecipeId = critBuff.Id, Recipe = critBuff,
            SourceFighterId = sophitia.Id, SourceSide = sophitia.Side, TargetFighterId = sophitia.Id,
            RemainingDuration = 2 });
        var card = new CardState { Kind = CardKind.Skill, Category = CardCategory.Attack,
            EffectCategory = CardCategory.Attack, Rank = 1 };
        damageBoost.Commands[0].ScalingCoefficientBp = 5000;
        var halfScaledHit = AttackEffectSystem.Prepare(null, state, sophitia, enemy, DamageFamily.Normal, card);
        Check(halfScaledHit.Policy.NonCriticalOutgoingIncreaseBp == 1750,
            "The stat modifier should scale the base Crit Chance damage bonus.");
        damageBoost.Commands[0].ScalingCoefficientBp = 10000;
        var hit = AttackEffectSystem.Prepare(null, state, sophitia, enemy, DamageFamily.Normal, card);
        Check(hit.Attacker.Attack == 100 && hit.Attacker.CritChanceBp == 5500 &&
            hit.Policy.NonCriticalOutgoingIncreaseBp == 3500,
            "The non-critical damage bonus should use base Crit Chance, excluding status bonuses.");
        var nonCritical = DamageResolver.Resolve(new DamagePacket { BaseAmount = 100,
            Policy = hit.Policy }, hit.Attacker, hit.Defender, enemy.Health, 0, 5500, 10000);
        Check(!nonCritical.WasCritical && nonCritical.CalculatedDamage == 135,
            "A non-critical hit should receive the full Crit Chance percentage damage increase.");
        var critical = DamageResolver.Resolve(new DamagePacket { BaseAmount = 100,
            Policy = hit.Policy }, hit.Attacker, hit.Defender, enemy.Health, 0, 0, 10000);
        Check(critical.WasCritical && critical.CalculatedDamage == 150,
            "A critical hit should not receive Sophitia's non-critical damage increase.");

        var stance = new StatusRecipeDefinition { Id = "status.stance.taunt", Polarity = StatusPolarity.Neutral,
            Behavior = StatusBehavior.Stance | StatusBehavior.Taunt,
            Tags = new List<string> { CombatTags.Stance, "sophitia.stance.rank.1" } };
        sophitia.Statuses.Instances.Add(new StatusInstance { RecipeId = stance.Id, Recipe = stance,
            SourceFighterId = sophitia.Id, SourceSide = sophitia.Side, TargetFighterId = sophitia.Id,
            RemainingDuration = 1 });
        CharacterPassiveRuntime.NotifyDamageResolved(state, enemy, sophitia, 10, "enemy-action", true,
            false, DamageFamily.Normal, CardCategory.Attack, 1, false, false, 1, 1);
        var updated = StatusSystem.GetEffectiveStats(sophitia);
        Check(updated.CritChanceBp == 6200 && updated.CritDamageBp == 16500,
            "Being attacked during the rank 1 Taunt stance should stack its rank's Crit Chance and Crit Damage buffs.");
    }

    private static PassiveCommandDefinition StackBuff(string id, string name, StatId stat, int amount) =>
        new PassiveCommandDefinition { Kind = PassiveCommandKind.ExecuteEffect,
            Effect = new CardEffectOperationDefinition { Id = "sophitia.stack." + stat,
                Window = CardEffectWindow.AfterDamage, Kind = CardEffectOperationKind.ApplyStatus,
                Target = EffectTargetScope.TriggerTarget, Status = new StatusApplicationRecipe { StatusRecipeId = id,
                    DurationOverride = 2, InlineRecipe = new StatusRecipeDefinition { Id = id, NameKey = name,
                        Polarity = StatusPolarity.Buff, Behavior = StatusBehavior.Stat, DefaultDuration = 2,
                        DurationClock = StatusDurationClock.TargetTurnEnd, Stacking = StatusStackingPolicy.AddStacks,
                        MaxStacks = int.MaxValue, Modifiers = new List<StatModifierDefinition> {
                            new StatModifierDefinition { Target = ModifierTarget.Stat, Stat = stat, Amount = amount,
                                Operation = ModifierOperation.PercentagePoints } } } } } };

    private static BattleState State(FighterState player, FighterState opponent) => new BattleState {
        Player = new BattleTeamState { Side = TeamSide.Player, Fighters = new List<FighterState> { player } },
        Opponent = new BattleTeamState { Side = TeamSide.Opponent, Fighters = new List<FighterState> { opponent } } };
}
