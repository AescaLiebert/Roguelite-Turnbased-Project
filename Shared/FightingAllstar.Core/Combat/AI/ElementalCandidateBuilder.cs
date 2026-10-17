using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat.AI
{
    public static class ElementalCandidateBuilder
    {
        public const int MaximumPairChecks = 512;

        public static List<CandidatePlan> Generate(BattleSituation situation, BattleState state, int maximum)
        {
            var ranked = new List<(CandidatePlan plan, float rank)>();
            if (state?.Team(state.ActingSide).ElementalReactionsEnabled != true || state.ElementalRules == null || situation == null || situation.ActionBudget < 2) return new List<CandidatePlan>();
            var cards = situation.OwnHand.Where(c => c.IsPlayable && !c.IsDisabled).ToList();
            var checks = 0;
            foreach (var first in cards)
            foreach (var second in cards)
            {
                if (first.CardId == second.CardId || !TryEffect(first, out var setup) || !TryEffect(second, out var trigger) ||
                    !CanPrepare(setup, state.ElementalRules) ||
                    trigger.DamageElement == ElementId.None && trigger.ElementalApplication?.IsConfigured != true) continue;
                foreach (var firstTarget in Targets(first, situation))
                foreach (var secondTarget in Targets(second, situation))
                {
                    if (++checks > MaximumPairChecks) return Best(ranked, maximum);
                    var draft = new PlanDraft(state);
                    if (!draft.QueuePlay(first.CardId, firstTarget, out _) || !draft.QueuePlay(second.CardId, secondTarget, out _)) continue;
                    var forecast = draft.Forecast();
                    // Pure setup skills are also candidates; the combat evaluator determines acceptance and payoff.
                    if (forecast.Lines.Count == 0 && setup.Kind != EffectKind.ApplyStatus) continue;
                    var plan = new CandidatePlan { StrategyName = "ElementalOrder", Description = "Prepare/trigger using public setup in queue order." };
                    plan.Actions.AddRange(draft.Actions.Select(a => a.Clone()));
                    var rank = forecast.Lines.Sum(l => (l.Conditional ? .5f : 1f) * (1 + l.AdjacentTargetIds.Count));
                    ranked.Add((plan, rank));
                }
            }
            return Best(ranked, maximum);
        }

        private static List<CandidatePlan> Best(List<(CandidatePlan plan, float rank)> ranked, int maximum) =>
            ranked.OrderByDescending(p => p.rank).Take(Math.Max(0, maximum)).Select(p => p.plan).ToList();

        private static bool TryEffect(CardOption card, out EffectDefinition effect) => card.IsUltimate
            ? CardRules.TryGetUltimate(card.Owner.Definition, card.Card.UltimateTier, out effect)
            : CardRules.TryGetSkill(card.Owner.Definition, card.Card.SkillId, card.Rank, out effect);

        private static bool CanPrepare(EffectDefinition effect, ElementalRuleSet rules) =>
            effect.Kind == EffectKind.Damage && (effect.ElementalApplication?.IsConfigured == true || effect.DamageElement != ElementId.None);

        private static IEnumerable<string> Targets(CardOption card, BattleSituation situation)
        {
            if (card.TargetScope == EffectTargetScope.Self) return new[] { card.Owner.Id };
            if (card.TargetScope == EffectTargetScope.AllAllies) return situation.OwnFighters.Take(1).Select(f => f.FighterId);
            if (card.TargetScope == EffectTargetScope.SelectedAlly) return situation.OwnFighters.Select(f => f.FighterId);
            if (card.TargetScope == EffectTargetScope.AllEnemies) return situation.PlayerFighters.Take(1).Select(f => f.FighterId);
            var taunts = situation.PlayerFighters.Where(f => f.HasTaunt).ToList();
            return (taunts.Count > 0 ? taunts : situation.PlayerFighters).Select(f => f.FighterId);
        }
    }
}
