using System;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;

internal static partial class BattlePlaybackChecks
{
    private static void HitReactionChecks()
    {
        BattleState Fixture(bool stance, ulong seed = 6301)
        {
            var state = Create(seed);
            if (stance)
            {
                var target = state.Opponent.Fighters[0];
                Check(StatusSystem.Apply(target, target.Id, target.Side, new StatusRecipeDefinition {
                    Id = "reaction.stance", Polarity = StatusPolarity.Buff, Behavior = StatusBehavior.Stance,
                    DefaultDuration = 5, StanceChildren = { new StanceChildDefinition { Id = "reaction.child", DebuffImmunity = true } }
                }, "stance", "fixture", 1).Accepted, "Reaction stance fixture rejected.");
            }
            return state;
        }
        EffectDefinition Attack(HitReaction kind, HitReactionTiming timing = HitReactionTiming.LastHit) =>
            new EffectDefinition { Scaling = StatScaling.Attack, Attack = new DamageAttackDefinition {
                HitCount = 3, Reaction = kind, ReactionTiming = timing } };

        foreach (HitReaction kind in Enum.GetValues(typeof(HitReaction)))
        foreach (HitReactionTiming timing in Enum.GetValues(typeof(HitReactionTiming)))
        foreach (var stance in new[] { false, true })
        {
            var state = Fixture(stance);
            var next = PlayKind(state, CardCategory.Attack, EffectTargetScope.SelectedEnemy, Attack(kind, timing));
            var hits = next.Events.Skip(state.Events.Count).Where(e => e.Kind == BattleEventKind.DamageApplied && e.HitCount == 3).ToList();
            Check(hits.Count == 3 && hits.Sum(e => e.Amount) == 85, "Reaction changed damage resolution.");
            foreach (var hit in hits)
            {
                var selected = timing == HitReactionTiming.EveryHit || timing == HitReactionTiming.FirstHit && hit.HitIndex == 1 ||
                    timing == HitReactionTiming.LastHit && hit.HitIndex == 3;
                var expected = stance || kind == HitReaction.None ? HitReaction.None : selected ? kind : HitReaction.Hit;
                Check(hit.HasHitReaction && hit.Reaction == expected, "Wrong reaction fact for " + kind + "/" + timing + "/stance=" + stance);
                Check(hit.WasReactionResisted == (stance && kind != HitReaction.None), "Stance tolerance was not recorded.");
            }
            var display = state.Clone();
            foreach (var item in next.Events.Skip(state.Events.Count)) BattlePlaybackState.Apply(display, item);
            EqualDisplay(display, next);
        }

        foreach (var stance in new[] { false, true })
        foreach (var keyword in new[] { "CancelStance", "remove-stance", "attack.cancel-stance" })
        {
            var state = Fixture(stance);
            var effect = Attack(HitReaction.KnockUp);
            effect.KeywordId = keyword;
            var next = PlayKind(state, CardCategory.Attack, EffectTargetScope.SelectedEnemy, effect);
            var events = next.Events.Skip(state.Events.Count).ToList();
            var cancel = events.Where(e => e.WasStanceCancelled).ToList();
            Check(cancel.Count == (stance ? 1 : 0), "Cancellation must force exactly one knockback only when a stance existed.");
            if (stance)
            {
                Check(cancel[0].HasHitReaction && cancel[0].Reaction == HitReaction.KnockBack, "Cancellation did not force knockback.");
                Check(events.Where(e => e.Kind == BattleEventKind.DamageApplied && e.HitCount == 3)
                    .All(e => e.Reaction == HitReaction.None), "Card reaction replaced cancellation's forced knockback.");
                Check(!HitReactionRules.IsInStance(next.Opponent.Fighters[0]), "Cancellation left a stance or child active.");
            }
            else Check(events.Last(e => e.Kind == BattleEventKind.DamageApplied && e.HitCount == 3).Reaction == HitReaction.KnockUp,
                "Cancellation keyword overrode a target that had no stance.");
            var projection = BattleProjectionBuilder.Build(next, TeamSide.Player, "reaction-test");
            Check(projection.Events.Count(e => e.WasStanceCancelled && e.Reaction == "KnockBack") == cancel.Count,
                "Network projection lost the override facts.");
        }

        foreach (CardEffectTiming timing in Enum.GetValues(typeof(CardEffectTiming)))
        {
            var state = Fixture(true);
            var effect = Attack(HitReaction.KnockDown);
            effect.Sequence.Add(new CardEffectStep { Timing = timing, Effect = new EffectDefinition {
                Kind = EffectKind.RemoveStance, Target = EffectTargetScope.SelectedEnemy } });
            var next = PlayKind(state, CardCategory.Attack, EffectTargetScope.SelectedEnemy, effect);
            Check(next.Events.Skip(state.Events.Count).Count(e => e.WasStanceCancelled && e.Reaction == HitReaction.KnockBack) == 1,
                "Utility cancellation failed at " + timing);
            Check(next.Events.Skip(state.Events.Count).Where(e => e.Kind == BattleEventKind.DamageApplied && e.HitCount == 3)
                .All(e => e.Reaction == HitReaction.None), "Stance or its cancellation allowed knockdown at " + timing);
        }

        var utilityState = Fixture(true);
        var utilityNext = PlayKind(utilityState, CardCategory.Debuff, EffectTargetScope.SelectedEnemy,
            new EffectDefinition { Kind = EffectKind.RemoveStance });
        Check(utilityNext.Events.Skip(utilityState.Events.Count).Count(e => e.WasStanceCancelled) == 1,
            "Standalone stance cancel needs its own visual reaction without damage.");

        var aoeState = Fixture(true);
        var aoeNext = PlayKind(aoeState, CardCategory.Attack, EffectTargetScope.AllEnemies, Attack(HitReaction.KnockDown));
        var finalHits = aoeNext.Events.Where(e => e.Kind == BattleEventKind.DamageApplied && e.HitIndex == 3).ToList();
        Check(finalHits.Count(e => e.Reaction == HitReaction.None) == 1 && finalHits.Count(e => e.Reaction == HitReaction.KnockDown) == 2,
            "AOE must respect each recipient's stance independently.");
        var zeroState = Fixture(false);
        var zeroEffect = Attack(HitReaction.KnockUp); zeroEffect.CoefficientBp = 0;
        var zeroNext = PlayKind(zeroState, CardCategory.Attack, EffectTargetScope.SelectedEnemy, zeroEffect);
        Check(zeroNext.Events.Where(e => e.Kind == BattleEventKind.DamageApplied && e.HitCount == 3).All(e => e.Reaction == HitReaction.None),
            "Endured hits should not displace a recipient.");
        var data = Attack(HitReaction.KnockDown); var copy = data.Clone(); copy.Attack.Reaction = HitReaction.KnockUp;
        Check(data.Attack.Reaction == HitReaction.KnockDown, "Reaction authoring data shared across cloned effects.");
        Check(SnapshotJson.Deserialize<EffectDefinition>(SnapshotJson.Serialize(data)).Attack.Reaction == HitReaction.KnockDown,
            "Reaction setting lost during serialization.");
        Console.WriteLine("PASS: target reaction selection, timing, stance tolerance, cancellation overrides and replay/projection.");
    }
}
