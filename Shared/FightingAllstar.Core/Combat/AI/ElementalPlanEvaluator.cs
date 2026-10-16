using System;
using System.Linq;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat.AI
{
    public sealed class ElementalPlanValue
    {
        public bool IsValid;
        public float Damage;
        public float Healing;
        public float Kills;
        public float FutureDamage;
        public float Control;
        public float Protection;
    }

    /// <summary>Expected public outcomes, not prediction of the encounter's future rolls.</summary>
    public static class ElementalPlanEvaluator
    {
        public const int SampleCount = 3;
        public const float RetainedValueDiscount = .6f; // Starting tuning policy; see the G–H task card.
        private static readonly ulong[] Seeds = { 0x9E3779B97F4A7C15UL, 0xD1B54A32D192ED03UL, 0x94D049BB133111EBUL };

        public static ElementalPlanValue Evaluate(CandidatePlan candidate, BattleState observation)
        {
            var value = new ElementalPlanValue();
            if (candidate == null || observation == null) return value;
            // This boundary also protects callers that pass an authority snapshot directly.
            var initial = AiPlanningObservation.Create(observation);
            var side = initial.ActingSide;
            var futureBefore = FutureDamage(initial.OtherTeam(side)) - FutureDamage(initial.Team(side));
            var controlBefore = Control(initial.OtherTeam(side)) - Control(initial.Team(side));
            foreach (var seed in Seeds)
            {
                // TryResolvePlan creates its own working copy; only replace this detached sample's seed.
                var sample = initial;
                sample.RngState = seed;
                var plan = new TurnPlan { RequestId = "elemental-evaluation", ExpectedRevision = sample.Revision,
                    Actions = candidate.Actions.ConvertAll(a => a.Clone()) };
                if (!BattleEngine.TryResolvePlan(sample, plan, out var result, out _)) return value;
                foreach (var before in initial.OtherTeam(side).Fighters)
                {
                    var after = result.OtherTeam(side).FindFighter(before.Id);
                    value.Damage += Math.Max(0, before.Health - after.Health) + Math.Max(0, before.Shield - after.Shield) * .5f;
                    if (before.IsAlive && !after.IsAlive) value.Kills++;
                }
                foreach (var before in initial.Team(side).Fighters)
                {
                    var after = result.Team(side).FindFighter(before.Id);
                    value.Healing += after.Health - before.Health; // Retaliation/self-DOT harm remains a cost.
                    value.Protection += (after.Shield - before.Shield) * .5f;
                    if (before.IsAlive && !after.IsAlive) value.Kills--;
                }
                value.FutureDamage += (FutureDamage(result.OtherTeam(side)) - FutureDamage(result.Team(side)) - futureBefore) * RetainedValueDiscount;
                value.Control += (Control(result.OtherTeam(side)) - Control(result.Team(side)) - controlBefore) * RetainedValueDiscount;
            }
            value.Damage /= SampleCount; value.Healing /= SampleCount; value.Kills /= SampleCount;
            value.FutureDamage /= SampleCount; value.Control /= SampleCount; value.Protection /= SampleCount;
            value.IsValid = true;
            return value;
        }

        private static float FutureDamage(BattleTeamState team)
        {
            double total = 0;
            foreach (var fighter in team.LivingActive())
            {
                if (fighter.Health <= 0 || fighter.CannotDie) continue;
                double future = 0;
                foreach (var timing in new[] { StatusTickTiming.TargetTurnStart, StatusTickTiming.TargetTurnEnd })
                    foreach (var tick in fighter.Statuses.CollectTicks(timing))
                    {
                        var status = fighter.Statuses.Instances.Find(s => s.InstanceId == tick.StatusInstanceId);
                        future += (double)tick.Amount * Math.Min(4, Math.Max(0, status?.RemainingDuration ?? 0));
                    }
                total += Math.Min(fighter.Health + (double)fighter.Shield, future);
            }
            return (float)Math.Min(float.MaxValue, total);
        }

        private static float Control(BattleTeamState team)
        {
            double total = 0;
            foreach (var fighter in team.LivingActive())
            {
                if (fighter.Health <= 0) continue;
                var attack = StatusSystem.GetEffectiveStats(fighter).Attack;
                if (fighter.Statuses.Instances.Any(s =>
                    s.RemainingDuration > 0 && (s.Recipe?.Behavior & StatusBehavior.Disable) != 0 &&
                    s.Recipe?.DisableMask == CardCategoryMask.AllCards))
                    total += attack;
                else
                {
                    var gaugeBp = fighter.Statuses.Instances.Where(s => s.RemainingDuration > 0)
                        .Sum(s => (long)(s.Recipe?.PowerGaugeGainBp ?? 0) * Math.Max(1, s.StackCount));
                    // Starting value: partial gauge denial is worth half the same fraction of a disabled turn.
                    total += attack * Math.Min(10000, Math.Max(0, -gaugeBp)) / 10000.0 * .5;
                }
            }
            return (float)Math.Min(float.MaxValue, total);
        }
    }
}
