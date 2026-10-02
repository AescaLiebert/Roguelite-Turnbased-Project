using System;
using System.Numerics;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Combat
{
    public static class DamageResolver
    {
        /// <summary>Uses exact basis-point fractions and floors once, after all calculation stages.</summary>
        public static DamageResult Resolve(DamagePacket packet, StatBlock attacker, StatBlock defender,
            int currentHealth, int currentShield, int critRollBp, int blockRollBp, bool surviveAtOne = false)
        {
            if (packet == null || packet.Policy == null) throw new ArgumentNullException(nameof(packet));
            if (attacker == null || defender == null) throw new ArgumentNullException("Stats cannot be null.");
            var policy = packet.Policy;
            var family = policy.Family;
            var isTrue = family == DamageFamily.True;
            var normalFormula = family == DamageFamily.Normal || isTrue || family == DamageFamily.Destructive;
            var numerator = new BigInteger(Math.Max(0, packet.BaseAmount));
            var denominator = BigInteger.One;
            var critical = false;
            var blocked = false;

            if (normalFormula)
            {
                var pierce = Math.Max(0, attacker.PierceBp);
                var resistance = policy.BypassResistance || isTrue ? 0L : Math.Max(0, defender.ResistanceBp);
                Multiply(ref numerator, ref denominator, Math.Max(0, packet.CoefficientBp), 10000);
                Multiply(ref numerator, ref denominator, Math.Max(0L, 10000L + pierce - resistance), 10000);

                if (!policy.BypassDefense && !isTrue)
                    numerator -= new BigInteger(Math.Max(0, defender.Defense)) * denominator;
                Multiply(ref numerator, ref denominator, Math.Max(0, packet.KeywordFactorBp), 10000);

                var trueBonus = isTrue ? 3000 : 0;
                Multiply(ref numerator, ref denominator,
                    Math.Max(0, 10000L + policy.OutgoingIncreaseBp - policy.OutgoingDecreaseBp + trueBonus), 10000);
                if (isTrue)
                    Multiply(ref numerator, ref denominator, Math.Max(0, 10000L + policy.IncomingIncreaseBp), 10000);
                else if (!policy.BypassGenericReduction)
                    Multiply(ref numerator, ref denominator,
                        Math.Max(0, 10000L + policy.IncomingIncreaseBp - policy.IncomingDecreaseBp), 10000);
                Multiply(ref numerator, ref denominator, Math.Max(0, policy.AttributeFactorBp), 10000);

                if (family != DamageFamily.Destructive)
                {
                    var critChance = Clamp(attacker.CritChanceBp - defender.CritResistanceBp, 0, 10000);
                    if (!policy.CannotCrit && critRollBp >= 0 && critRollBp < critChance)
                    {
                        Multiply(ref numerator, ref denominator,
                            Math.Max(10000, attacker.CritDamageBp - defender.CritDefenseBp), 10000);
                        critical = true;
                    }
                    else
                    {
                        var blockChance = Clamp(defender.BlockChanceBp - attacker.PierceBp, 0, 10000);
                        if (!policy.CannotBlock && blockRollBp >= 0 && blockRollBp < blockChance)
                        {
                            Multiply(ref numerator, ref denominator, Math.Max(0, 10000 - defender.BlockPowerBp), 10000);
                            blocked = true;
                        }
                    }
                }

                Multiply(ref numerator, ref denominator, Math.Max(0, packet.VarianceBp), 10000);
                if (!isTrue)
                {
                    Multiply(ref numerator, ref denominator, Math.Max(0, 10000 - policy.FinalReductionBp), 10000);
                    numerator -= new BigInteger(Math.Max(0, policy.FlatReduction)) * denominator;
                }
                numerator = BigInteger.Max(BigInteger.Zero, numerator);
            }
            else
            {
                Multiply(ref numerator, ref denominator,
                    Math.Max(0, 10000L + policy.FamilyDealtIncreaseBp - policy.FamilyDealtDecreaseBp), 10000);
                Multiply(ref numerator, ref denominator,
                    Math.Max(0, 10000L + policy.FamilyReceivedIncreaseBp - policy.FamilyReceivedDecreaseBp), 10000);
                Multiply(ref numerator, ref denominator,
                    Math.Max(0, 10000 - policy.FamilySpecificReductionBp), 10000);
            }

            var unbounded = numerator <= 0 ? BigInteger.Zero : BigInteger.Divide(numerator, denominator);
            var familyIgnoresCap = family == DamageFamily.Additional || family == DamageFamily.DamageOverTime;
            var capped = !policy.BypassDamageCap && !familyIgnoresCap && unbounded >= Math.Max(0, policy.DamageCap);
            if (capped) unbounded = BigInteger.Min(unbounded, Math.Max(0, policy.DamageCap));
            var damage = (int)BigInteger.Min(int.MaxValue, unbounded);

            var shield = Math.Max(0, currentShield);
            var health = Math.Max(0, currentHealth);
            var shieldLost = policy.BypassShield ? 0 : Math.Min(shield, damage);
            shield -= shieldLost;
            var hpDirected = Math.Max(0, damage - shieldLost);
            var executes = family == DamageFamily.Destructive && hpDirected >= health && health > 0;
            var healthLost = executes ? health : Math.Min(health, hpDirected);
            if (surviveAtOne && !policy.BypassSurviveAtOne && health > 0 && healthLost >= health)
                healthLost = health - 1;
            health -= healthLost;
            if (executes && policy.BypassSurviveAtOne) health = 0;

            return new DamageResult { CalculatedDamage = damage, ShieldLost = shieldLost, HealthLost = healthLost,
                RemainingHealth = health, RemainingShield = shield, WasCritical = critical, WasBlocked = blocked,
                Executed = executes && health == 0, WasCapped = capped };
        }

        private static void Multiply(ref BigInteger numerator, ref BigInteger denominator, long factor, long scale)
        {
            if (numerator <= 0 || factor <= 0) { numerator = BigInteger.Zero; return; }
            numerator *= factor;
            denominator *= scale;
        }

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
    }
}
