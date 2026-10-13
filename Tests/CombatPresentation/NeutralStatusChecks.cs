using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

internal static partial class BattlePlaybackChecks
{
    private static void NeutralStatusChecks()
    {
        var battle = Create(843);
        var owner = battle.Player.LivingActive()[0];
        var enemy = battle.Opponent.LivingActive()[0];
        var stance = CounterStance(); // Older assets still serialize Buff.
        var parent = StatusSystem.Apply(owner, owner.Id, owner.Side, stance, "neutral-stance", null, 1);
        Check(parent.Accepted && owner.Statuses.Instances.Count == 4 &&
            owner.Statuses.Instances.All(s => s.Recipe.Polarity == StatusPolarity.Neutral),
            "Legacy stance parents and every child must be Neutral at runtime.");
        Check(new StanceChildDefinition { Id = "child" }.ToRecipe().Polarity == StatusPolarity.Neutral,
            "Stance child authoring must produce a Neutral recipe.");
        Check(StatusSystem.Count(owner, StatusPolarity.Buff) == 0 &&
            StatusSystem.Count(owner, StatusPolarity.Debuff) == 0,
            "Stance children must not count as Buffs or Debuffs.");

        var heal = StandardEffectDatabase.CreateStatusRecipes().Find(r => r.Id == "status.buff.rejuvenation");
        Check(heal.Polarity == StatusPolarity.Neutral, "Rejuvenation must be authored Neutral.");
        heal.Polarity = StatusPolarity.Buff; // Migration of older inline healing recipes.
        var healing = StatusSystem.Apply(owner, owner.Id, owner.Side, heal, "neutral-heal", null, 2);
        Check(healing.Accepted && healing.Instance.Recipe.Polarity == StatusPolarity.Neutral,
            "Legacy healing statuses must become Neutral despite a .buff. id.");
        Check(healing.Instance.Recipe.PeriodicHealing?.Scaling == StatusHealScaling.TurnStartRecovery &&
            healing.Instance.Recipe.PeriodicHealing.CoefficientBp == 6000,
            "Neutral classification must preserve Rejuvenation healing.");

        var rupture = StandardEffectDatabase.CreateAttackEffects().Find(r => r.Kind == AttackEffectKind.Rupture);
        var amplify = StandardEffectDatabase.CreateAttackEffects().Find(r => r.Kind == AttackEffectKind.Amplify);
        Check(AttackEffectSystem.Prepare(rupture, battle, enemy, owner, DamageFamily.Normal).KeywordFactorBp == 10000,
            "Rupture must not trigger from Neutral healing, stance parents, or children.");
        Check(AttackEffectSystem.Prepare(amplify, battle, owner, enemy, DamageFamily.Normal).KeywordFactorBp == 10000,
            "Amplify must not count Neutral healing, stance parents, or children.");

        var buff = new StatusRecipeDefinition { Id = "status.buff.real", Polarity = StatusPolarity.Buff };
        StatusSystem.Apply(owner, owner.Id, owner.Side, buff, "real-buff", null, 3);
        Check(AttackEffectSystem.Prepare(rupture, battle, enemy, owner, DamageFamily.Normal).KeywordFactorBp == rupture.PrimaryValueBp,
            "Rupture must still trigger from an actual Buff alongside Neutral statuses.");
        Check(AttackEffectSystem.Prepare(amplify, battle, owner, enemy, DamageFamily.Normal).KeywordFactorBp == 10000 + amplify.PrimaryValueBp,
            "Amplify must count only the actual Buff alongside Neutral statuses.");
        Check(StatusSystem.Remove(owner, StatusPolarity.Buff, true) == 1 && owner.Statuses.Instances.Count == 5,
            "Buff removal must preserve Neutral stance parents, children, and healing.");
        Check(StatusSystem.Remove(owner, StatusPolarity.Debuff, true) == 0,
            "Debuff cleansing must preserve Neutral statuses.");

        var clone = owner.Clone();
        Check(clone.Statuses.Instances.All(s => s.Recipe.Polarity == StatusPolarity.Neutral) &&
            clone.Statuses.Instances.Count(s => s.ParentInstanceId == parent.Instance.InstanceId) == 3,
            "Battle snapshots must preserve Neutral stance children and parent links.");
        owner.Statuses.RemoveInstance(parent.Instance.InstanceId);
        Check(owner.Statuses.Instances.Count == 1 && owner.Statuses.Instances[0].RecipeId == heal.Id,
            "Removing the stance parent must remove its Neutral children and preserve independent healing.");

        foreach (var id in new[] { "status.buff.neutral", "status.debuff.neutral", "status.increase.neutral" })
        {
            var neutral = new StatusRecipeDefinition { Id = id, Polarity = StatusPolarity.Neutral };
            Check(neutral.Clone().Polarity == StatusPolarity.Neutral,
                "Cloning must preserve explicit Neutral classification for " + id);
            Check(StatusSystem.Apply(enemy, enemy.Id, enemy.Side, neutral, id, null, 4).Instance.Recipe.Polarity == StatusPolarity.Neutral,
                "Status application must preserve explicit Neutral classification for " + id);
        }
        Check(StatusLibrary.GetDisplayName("status.buff.custom_heal", StatusPolarity.Neutral) == "Custom Heal" &&
            StatusLibrary.GetIconKey("custom.stance", StatusPolarity.Neutral) == "Cardtype_Stance" &&
            StatusLibrary.GetIconKey("custom.heal", StatusPolarity.Neutral) == "heart",
            "Neutral fallbacks must not label or depict stance/healing as Debuffs.");

        var healingBattle = Create(844);
        var patient = healingBattle.Player.LivingActive()[0];
        patient.Health -= 100;
        patient.Definition.BaseStats.RegenerationBp = 10000;
        patient.Definition.BaseStats.RecoveryBp = 10000;
        StatusSystem.Apply(patient, patient.Id, patient.Side, heal, "healing-check", null, 1);
        Check(BattleEngine.TryResolvePlan(healingBattle, new TurnPlan { RequestId = "neutral-heal-player-pass",
            ExpectedRevision = healingBattle.Revision }, out var opponentTurn, out var error), error);
        Check(BattleEngine.TryResolvePlan(opponentTurn, new TurnPlan { RequestId = "neutral-heal-opponent-pass",
            ExpectedRevision = opponentTurn.Revision }, out var recovered, out error), error);
        Check(recovered.Player.FindFighter(patient.Id).Health == patient.Health + 48 &&
            recovered.Events.Any(e => e.Kind == BattleEventKind.HealApplied && e.TargetId == patient.Id &&
                e.Message == heal.Id && e.Amount == 18),
            "Neutral Rejuvenation must still heal an additional 60% of the turn-start recovery.");
    }
}
