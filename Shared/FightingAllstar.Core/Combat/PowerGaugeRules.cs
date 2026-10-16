using System;

namespace FightingAllstar.Core.Combat
{
    public static class PowerGaugeRules
    {
        public static int GainRateBp(FighterState fighter)
        {
            long rate = 10000;
            if (fighter?.Statuses?.Instances != null) foreach (var status in fighter.Statuses.Instances)
                rate += status?.Recipe?.PowerGaugeGainBp ?? 0;
            return (int)Math.Max(0, Math.Min(int.MaxValue, rate));
        }

        public static int GainForAction(FighterState fighter)
        {
            if (fighter == null || fighter.PowerGaugeDisabled || fighter.PowerGauge >= CardRules.UltimateGaugeCost)
            {
                if (fighter != null) fighter.PowerGaugeRemainderBp = 0;
                return 0;
            }
            var amount = (long)Math.Max(0, fighter.PowerGaugeRemainderBp) + GainRateBp(fighter);
            var previous = fighter.PowerGauge;
            fighter.PowerGauge = (int)Math.Min(CardRules.UltimateGaugeCost, fighter.PowerGauge + amount / 10000);
            fighter.PowerGaugeRemainderBp = fighter.PowerGauge >= CardRules.UltimateGaugeCost ? 0 : (int)(amount % 10000);
            return fighter.PowerGauge - previous;
        }

        public static void SpendUltimate(FighterState fighter)
        {
            fighter.PowerGauge = 0;
            fighter.PowerGaugeRemainderBp = 0;
        }
    }
}
