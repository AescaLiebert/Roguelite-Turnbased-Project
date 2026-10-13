using System.Collections.Generic;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

internal static partial class BattlePlaybackChecks
{
    private static void SeongPassiveChecks()
    {
        var passive = new PassiveDefinition { Id = "fighter.scii-seong.passive.source" };
        var bonus = new PassiveReactionDefinition { Id = "debuff-bonus", Trigger = PassiveEventKind.BeforeDamage,
            ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies, CardOriginOnly = true,
            FilterDamageFamily = true, DamageFamily = DamageFamily.Normal };
        bonus.Conditions.Add(new EffectConditionDefinition { Kind = EffectConditionKind.TargetHasDebuff });
        bonus.Commands.Add(new PassiveCommandDefinition { Kind = PassiveCommandKind.IncreaseCurrentDamageDealtPercent, Amount = 3000 });
        passive.Reactions.Add(bonus);
        var ignore = new PassiveReactionDefinition { Id = "bleed-crit-defense", Trigger = PassiveEventKind.BeforeDamage,
            ActorRelation = PassiveRelation.Self, TargetRelation = PassiveRelation.Enemies, CardOriginOnly = true,
            FilterDamageFamily = true, DamageFamily = DamageFamily.Normal };
        ignore.Conditions.Add(new EffectConditionDefinition { Kind = EffectConditionKind.TargetHasStatusTag, Tag = CombatTags.Bleed });
        ignore.Commands.Add(new PassiveCommandDefinition { Kind = PassiveCommandKind.IgnoreCurrentTargetCritDefensePercent, Amount = 3000 });
        passive.Reactions.Add(ignore);
        var attack = new PassiveReactionDefinition { Id = "soul-calibur-attack", Trigger = PassiveEventKind.BeforeDamage,
            ActorRelation = PassiveRelation.Self, CardOriginOnly = true, FilterDamageFamily = true, DamageFamily = DamageFamily.Normal };
        attack.Commands.Add(new PassiveCommandDefinition { Kind = PassiveCommandKind.IncreaseCurrentAttackPercent, Amount = 1200 });
        passive.Reactions.Add(attack);
        Check(PassiveRuleValidator.Validate(passive).Count == 0, "Seong's passive rule set should validate.");

        var normalDebuff = new StatusRecipeDefinition { Id = "test.debuff", Polarity = StatusPolarity.Debuff,
            Behavior = StatusBehavior.Stat };
        var bleed = new StatusRecipeDefinition { Id = "status.debuff.bleed", Polarity = StatusPolarity.Debuff,
            Behavior = StatusBehavior.Stat, Tags = new List<string> { CombatTags.Bleed } };
        var noDebuff = PrepareSeongHit(passive, null);
        Check(noDebuff.Attacker.Attack == 112 && noDebuff.Policy.OutgoingIncreaseBp == 0 && noDebuff.Defender.CritDefenseBp == 1000,
            "Soul Calibur ATK bonus should apply for one hit without persistent damage/crit modifiers.");
        var debuffed = PrepareSeongHit(passive, normalDebuff);
        Check(debuffed.Policy.OutgoingIncreaseBp == 3000 && debuffed.Defender.CritDefenseBp == 1000,
            "Seong should deal 30% more damage to any debuffed target without ignoring Crit Defense.");
        var bleeding = PrepareSeongHit(passive, bleed);
        Check(bleeding.Policy.OutgoingIncreaseBp == 3000 && bleeding.Defender.CritDefenseBp == 700,
            "Bleed should activate damage dealt and reduce the target's Crit Defense by exactly 30%.");
    }

    private static AttackEffectCalculation PrepareSeongHit(PassiveDefinition passive, StatusRecipeDefinition targetStatus)
    {
        var seongDefinition = new CharacterDefinition { Id = "fighter.scii-seong", Passive = passive,
            BaseStats = new StatBlock { Attack = 100, MaxHealth = 1000 } };
        var targetDefinition = new CharacterDefinition { Id = "target", BaseStats = new StatBlock {
            Attack = 100, Defense = 100, MaxHealth = 1000, CritDefenseBp = 1000 } };
        var seong = new FighterState { Id = "seong", Side = TeamSide.Player, Definition = seongDefinition, Health = 1000 };
        var target = new FighterState { Id = "target", Side = TeamSide.Opponent, Definition = targetDefinition, Health = 1000 };
        if (targetStatus != null)
            target.Statuses.Instances.Add(new StatusInstance { RecipeId = targetStatus.Id, Recipe = targetStatus,
                SourceFighterId = seong.Id, SourceSide = seong.Side, TargetFighterId = target.Id, RemainingDuration = 2 });
        var battle = new BattleState { Player = new BattleTeamState { Side = TeamSide.Player,
            Fighters = new List<FighterState> { seong } }, Opponent = new BattleTeamState { Side = TeamSide.Opponent,
            Fighters = new List<FighterState> { target } } };
        var card = new CardState { Kind = CardKind.Skill, Category = CardCategory.Attack, EffectCategory = CardCategory.Attack, Rank = 1 };
        return AttackEffectSystem.Prepare(null, battle, seong, target, DamageFamily.Normal, card);
    }
}
