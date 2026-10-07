using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    /// <summary>Transport boundary shared by disposable local practice and future remote sessions.</summary>
    public interface IBattleSession
    {
        BattleState GetSnapshot();
        IReadOnlyList<BattleEvent> GetEventsAfter(long eventId);
        bool Submit(TurnPlan plan, out string error);
    }

    /// <summary>Development-only authority host. It is intentionally not an economy or run-reward service.</summary>
    public sealed class LocalBattleSession : IBattleSession
    {
        private BattleState _state;
        private readonly Dictionary<string, Receipt> _receipts = new Dictionary<string, Receipt>();

        public LocalBattleSession(BattleState initialState)
        {
            _state = initialState?.Clone();
            if (!AdvanceOpponentTurns(out var error))
                throw new System.InvalidOperationException("Local opponent could not resolve its opening turn: " + error);
        }

        public BattleState GetSnapshot() => _state?.Clone();

        public IReadOnlyList<BattleEvent> GetEventsAfter(long eventId)
        {
            var events = new List<BattleEvent>();
            if (_state == null) return events;
            foreach (var e in _state.Events) if (e.Id > eventId) events.Add(e.Clone());
            return events;
        }

        public bool Submit(TurnPlan plan, out string error)
        {
            error = null;
            if (plan != null && _receipts.TryGetValue(plan.RequestId ?? string.Empty, out var receipt))
            {
                if (receipt.PayloadHash == HashPlan(plan)) return true;
                error = "Request id was already used with a different payload.";
                return false;
            }
            if (!BattleEngine.TryResolvePlan(_state, plan, out var next, out error)) return false;
            if (plan != null && !string.IsNullOrEmpty(plan.RequestId))
                _receipts[plan.RequestId] = new Receipt { PayloadHash = HashPlan(plan) };
            _state = next;
            return AdvanceOpponentTurns(out error);
        }

        private bool AdvanceOpponentTurns(out string error)
        {
            error = null;
            while (_state != null && _state.Phase == BattlePhase.Planning && _state.ActingSide == TeamSide.Opponent)
            {
                var aiPlan = AI.EnemyAiPlanner.CreatePlan(_state) ?? LegalAi.CreatePlan(_state);
                if (!BattleEngine.TryResolvePlan(_state, aiPlan, out var next, out error)) return false;
                _state = next;
            }
            return true;
        }

        private static string HashPlan(TurnPlan plan)
        {
            var canonical = new StringBuilder();
            canonical.Append(plan.ExpectedRevision).Append('|').Append(plan.Actions == null ? -1 : plan.Actions.Count);
            if (plan.Actions != null)
                foreach (var action in plan.Actions)
                {
                    canonical.Append('|').Append(action == null ? '0' : action.IsMove ? 'M' : 'P');
                    AppendField(canonical, action?.CardId);
                    AppendField(canonical, action?.TargetFighterId);
                    canonical.Append(action == null ? -1 : action.DestinationIndex).Append('|');
                }
            using (var sha = SHA256.Create())
                return System.BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString())));
        }

        private static void AppendField(StringBuilder builder, string value)
        {
            value = value ?? string.Empty;
            builder.Append(value.Length).Append(':').Append(value).Append('|');
        }

        private sealed class Receipt
        {
            public string PayloadHash;
        }
    }

    internal static class LegalAi
    {
        public static TurnPlan CreatePlan(BattleState state)
        {
            var team = state.Team(state.ActingSide);
            var targets = state.OtherTeam(state.ActingSide).LivingActive();
            targets.Sort((a, b) => a.Health.CompareTo(b.Health));
            var plan = new TurnPlan { RequestId = "local-ai:" + state.MatchId + ":" + state.Revision, ExpectedRevision = state.Revision };
            for (var i = 0; i < team.Hand.Count && targets.Count > 0; i++)
            {
                var card = team.Hand[i];
                var owner = team.FindFighter(card.OwnerFighterId);
                if (owner == null || !owner.IsAlive || owner.IsReserve) continue;
                EffectDefinition effect;
                var hasEffect = card.Kind == CardKind.Skill
                    ? CardRules.TryGetSkill(owner.Definition, card.SkillId, card.Rank, out effect)
                    : CardRules.TryGetUltimate(owner.Definition, card.UltimateTier, out effect);
                var disabled = hasEffect && StatusSystem.IsCardUseBlocked(owner, CardRules.GetEffectCategory(card), card.Rank,
                    card.Kind == CardKind.Ultimate, effect.Sequence != null && effect.Sequence.Count > 0);
                if (card.Kind == CardKind.Ultimate && owner.PowerGauge < CardRules.UltimateGaugeCost && !disabled) continue;
                var target = targets[0];
                if (card.TargetScope == FightingAllstar.Core.Content.EffectTargetScope.Self) target = owner;
                else if (card.TargetScope == FightingAllstar.Core.Content.EffectTargetScope.SelectedAlly ||
                         card.TargetScope == FightingAllstar.Core.Content.EffectTargetScope.AllAllies)
                {
                    var allies = team.LivingActive();
                    allies.Sort((a, b) => ((long)a.Health * b.Stats.MaxHealth).CompareTo((long)b.Health * a.Stats.MaxHealth));
                    target = allies[0];
                }
                else if (card.TargetScope == FightingAllstar.Core.Content.EffectTargetScope.SelectedEnemy)
                    target = targets.Find(f => f.Statuses.Instances.Exists(s => s.Recipe?.HasTaunt == true)) ?? target;
                var draft = new PlanDraft(state);
                if (!draft.QueuePlay(card.Id, target.Id, out _)) continue;
                plan.Actions.Add(new PlannedAction { CardId = card.Id, TargetFighterId = target.Id });
                break;
            }
            return plan;
        }
    }
}
