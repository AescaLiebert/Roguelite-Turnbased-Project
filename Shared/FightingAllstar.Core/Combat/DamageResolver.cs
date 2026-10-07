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
                var baseAmount = Math.Max(0, packet.BaseAmount);
                var coeffBp = Math.Max(0, packet.CoefficientBp);
                var keywordFactorBp = Math.Max(0, packet.KeywordFactorBp);
                var pierceBp = Math.Max(0L, attacker.PierceBp);
                var defense = (!policy.BypassDefense && !isTrue) ? Math.Max(0, defender.Defense) : 0L;
                var resistanceBp = (policy.BypassResistance || isTrue) ? 0L : Math.Max(0L, defender.ResistanceBp);

                // 7DS Grand Cross order:
                // Crit and Block apply to the offensive skill multiplier before defense subtraction
                var critOrBlockBp = 10000L;
                if (family != DamageFamily.Destructive)
                {
                    var critChance = Clamp(attacker.CritChanceBp - defender.CritResistanceBp, 0, 10000);
                    if (!policy.CannotCrit && critRollBp >= 0 && critRollBp < critChance)
                    {
                        critOrBlockBp = Math.Max(10000L, (long)attacker.CritDamageBp - defender.CritDefenseBp);
                        critical = true;
                    }
                    else
                    {
                        var blockChance = Clamp(defender.BlockChanceBp - attacker.PierceBp, 0, 10000);
                        if (!policy.CannotBlock && blockRollBp >= 0 && blockRollBp < blockChance)
                        {
                            critOrBlockBp = Math.Max(0L, 10000L - defender.BlockPowerBp);
                            blocked = true;
                        }
                    }
                }

                var attributeBp = Math.Max(0L, (long)policy.AttributeFactorBp);

                // Skill Offensive Power = ATK * Card% * KeywordFactor * Crit/Block * Attribute
                // Base denominator scale for skill power is 10^16 (10000^4)
                var skillPowerNumerator = new BigInteger(baseAmount)
                    * coeffBp
                    * keywordFactorBp
                    * critOrBlockBp
                    * attributeBp;

                // Pierce Damage = ATK * max(0, Pierce% - Resistance%)
                // Scale is 10,000. Bring to 10^16 scale by multiplying by 10^12 (1_000_000_000_000L)
                var netPierceBp = Math.Max(0L, pierceBp - resistanceBp);
                var piercePowerNumerator = new BigInteger(baseAmount) * netPierceBp * 1_000_000_000_000L;

                // Total Offense = Skill Damage + Pierce Damage
                var totalOffenseNumerator = skillPowerNumerator + piercePowerNumerator;

                // Attacker bonus damage (e.g. Joe95 aura passive, damage dealt buffs) calculates first on attacker's attack
                var trueBonus = isTrue ? 3000 : 0;
                var outgoingBp = Math.Max(0L, 10000L + policy.OutgoingIncreaseBp - policy.OutgoingDecreaseBp + trueBonus);
                var finalOffenseNumerator = totalOffenseNumerator * outgoingBp;

                // In-contact with target defense: Target Defense stat subtracted once from finalized attacker's attack
                var scale20 = new BigInteger(10_000_000_000_000_000L) * 10000;
                var defenseNumerator = new BigInteger(defense) * scale20;
                numerator = BigInteger.Max(BigInteger.Zero, finalOffenseNumerator - defenseNumerator);
                denominator = scale20;
                if (isTrue)
                    Multiply(ref numerator, ref denominator, Math.Max(0, 10000L + policy.IncomingIncreaseBp), 10000);
                else if (!policy.BypassGenericReduction)
                    Multiply(ref numerator, ref denominator,
                        Math.Max(0, 10000L + policy.IncomingIncreaseBp - policy.IncomingDecreaseBp), 10000);

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
            // Split the completed formula, including defense/pierce/cap, rather than charging
            // defense once per slice. Remainder allocation preserves the exact integer total
            // when rolls/stats match. Each packet still owns its independent crit/block roll.
            var hits = Math.Max(1, Math.Min(10, packet.HitCount));
            var hit = Math.Max(1, Math.Min(hits, packet.HitIndex));
            damage = (int)((long)damage * hit / hits - (long)damage * (hit - 1) / hits);

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
                WasEndured = damage == 0, Executed = executes && health == 0, WasCapped = capped };
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
