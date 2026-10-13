using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

internal static partial class BattlePlaybackChecks
{
    private static void AdditionalDamageChecks()
    {
        var attacker = Fighter("taki-additional", 1000, 20);
        var target = Fighter("taki-additional-target", 100, 10);
        var skill = attacker.Skills[1];
        skill.Category = CardCategory.Attack;
        skill.TargetScope = EffectTargetScope.SelectedEnemy;
        skill.Ranks[0].Effect = new EffectDefinition
        {
            Kind = EffectKind.Damage,
            Scaling = StatScaling.Attack,
            CoefficientBp = 45000,
            Target = EffectTargetScope.SelectedEnemy
        };
        attacker.Passive = new PassiveDefinition { Id = "taki-additional-passive" };
        attacker.Passive.Reactions.Add(new PassiveReactionDefinition
        {
            Id = "skill-2-max-health-hit",
            Trigger = PassiveEventKind.DamageResolved,
            DamageTiming = PassiveDamageTiming.FinalHit,
            ActorRelation = PassiveRelation.Self,
            TargetRelation = PassiveRelation.Enemies,
            CardOriginOnly = true,
            ExcludeUltimate = true,
            FilterCategory = true,
            Category = CardCategory.Attack,
            MinimumRank = 1,
            LimitScope = PassiveLimitScope.RootActionTarget,
            MaximumActivations = 1,
            Commands = new List<PassiveCommandDefinition>
            {
                new PassiveCommandDefinition
                {
                    Kind = PassiveCommandKind.ExecuteEffect,
                    Effect = new CardEffectOperationDefinition
                    {
                        Id = "skill-2-max-health-hit",
                        Window = CardEffectWindow.AfterDamage,
                        Kind = CardEffectOperationKind.Damage,
                        Target = EffectTargetScope.TriggerTarget,
                        Damage = new DamageEffectRecipe
                        {
                            Family = DamageFamily.Additional,
                            Scaling = StatScaling.MaxHealth,
                            StatSource = DamageStatSource.Target,
                            CoefficientBp = 2000,
                            CannotCrit = true,
                            CannotBlock = true
                        }
                    }
                }
            }
        });
        Check(PassiveRuleValidator.Validate(attacker.Passive).Count == 0,
            "Taki's Skill 2 additional-damage reaction must validate.");

        var skillBattle = BattleEngine.Create("taki-additional-overkill", new[] { attacker }, null,
            new[] { target }, null, 4101, TeamSide.Player);
        var skillOwner = skillBattle.Player.Fighters[0];
        var skillTarget = skillBattle.Opponent.Fighters[0];
        skillTarget.Health = 10;
        var skillCard = new CardState
        {
            Id = "taki-skill-2",
            OwnerFighterId = skillOwner.Id,
            SkillId = skill.Id,
            Rank = 1,
            Kind = CardKind.Skill,
            Category = CardCategory.Attack,
            EffectCategory = CardCategory.Attack,
            TargetScope = EffectTargetScope.SelectedEnemy
        };
        skillBattle.Player.Hand.Clear();
        skillBattle.Player.Hand.Add(skillCard);
        var skillPlan = new PlanDraft(skillBattle);
        skillPlan.QueuePlay(skillCard.Id, skillTarget.Id, out _);
        Check(BattleEngine.TryResolvePlan(skillBattle, skillPlan.BuildPlan("taki-skill-2-plan"),
            out var skillResolved, out var skillError), skillError);
        var overkillFollowUp = skillResolved.Events.Find(e => e.Kind == BattleEventKind.DamageApplied &&
            e.CardId == skillCard.Id && e.Message == "Passive additional damage.");
        Check(overkillFollowUp != null && overkillFollowUp.Amount == 20,
            $"Skill 2 must resolve its 20% Max HP hit after the primary hit reduces HP to zero; got {overkillFollowUp?.Amount}.");
        Check(skillResolved.Opponent.Fighters[0].Health == 0 && !skillResolved.Opponent.Fighters[0].IsAlive,
            "The overkill follow-up must leave the target defeated.");

        var ultimateAttacker = Fighter("taki-ultimate", 1000, 20);
        ultimateAttacker.Passive = attacker.Passive.Clone();
        ultimateAttacker.UltimateTiers.Clear();
        ultimateAttacker.UltimateTiers.Add(new UltimateTierDefinition
        {
            Tier = 0,
            Category = CardCategory.Attack,
            Effect = new EffectDefinition
            {
                Kind = EffectKind.Damage,
                Scaling = StatScaling.Attack,
                CoefficientBp = 10000,
                Target = EffectTargetScope.SelectedEnemy
            }
        });
        var ultimateBattle = BattleEngine.Create("taki-ultimate-no-bonus", new[] { ultimateAttacker }, null,
            new[] { target }, null, 4102, TeamSide.Player);
        var ultimateOwner = ultimateBattle.Player.Fighters[0];
        var ultimateTarget = ultimateBattle.Opponent.Fighters[0];
        ultimateOwner.PowerGauge = CardRules.UltimateGaugeCost;
        var ultimateCard = new CardState
        {
            Id = "taki-ultimate",
            OwnerFighterId = ultimateOwner.Id,
            Rank = 1,
            Kind = CardKind.Ultimate,
            Category = CardCategory.Ultimate,
            EffectCategory = CardCategory.Attack,
            TargetScope = EffectTargetScope.SelectedEnemy,
            UltimateTier = 0
        };
        ultimateBattle.Player.Hand.Clear();
        ultimateBattle.Player.Hand.Add(ultimateCard);
        var ultimatePlan = new PlanDraft(ultimateBattle);
        ultimatePlan.QueuePlay(ultimateCard.Id, ultimateTarget.Id, out _);
        Check(BattleEngine.TryResolvePlan(ultimateBattle, ultimatePlan.BuildPlan("taki-ultimate-plan"),
            out var ultimateResolved, out var ultimateError), ultimateError);
        Check(!ultimateResolved.Events.Any(e => e.Kind == BattleEventKind.DamageApplied &&
            e.CardId == ultimateCard.Id && e.Message == "Passive additional damage."),
            "Taki's Skill 2 Max HP follow-up must not trigger on her Ultimate, even when its effect category is Attack.");
    }
}
