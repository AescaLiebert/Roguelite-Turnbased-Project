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
            var profile = policy.AuthoredProfile;
            var explicitTrue = isTrue && profile?.ExplicitTrueDamage == true;
            var legacyTrue = isTrue && !explicitTrue;
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
                var defense = (!policy.BypassDefense && !legacyTrue) ? Math.Max(0, defender.Defense) : 0L;
                var resistanceBp = (policy.BypassResistance || legacyTrue) ? 0L : Math.Max(0L, defender.ResistanceBp);

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
                var trueBonus = explicitTrue ? Math.Max(0, profile.TrueDamageBonusBp) : legacyTrue ? 3000 : 0;
                var conditionalOutgoing = critical ? 0 : Math.Max(0, policy.NonCriticalOutgoingIncreaseBp);
                var outgoingBp = Math.Max(0L, 10000L + policy.OutgoingIncreaseBp - policy.OutgoingDecreaseBp +
                    conditionalOutgoing + trueBonus);
                var finalOffenseNumerator = totalOffenseNumerator * outgoingBp;

                // In-contact with target defense: Target Defense stat subtracted once from finalized attacker's attack
                var scale20 = new BigInteger(10_000_000_000_000_000L) * 10000;
                var defenseNumerator = new BigInteger(defense) * scale20;
                numerator = BigInteger.Max(BigInteger.Zero, finalOffenseNumerator - defenseNumerator);
                denominator = scale20;
                if (legacyTrue || explicitTrue && policy.BypassGenericReduction)
                    Multiply(ref numerator, ref denominator, Math.Max(0, 10000L + policy.IncomingIncreaseBp + (policy.Element == ElementId.None ? policy.PhysicalReceivedIncreaseBp : 0)), 10000);
                else if (!policy.BypassGenericReduction)
                    Multiply(ref numerator, ref denominator,
                        Math.Max(0, 10000L + policy.IncomingIncreaseBp - policy.IncomingDecreaseBp +
                            (policy.Element == ElementId.None ? policy.PhysicalReceivedIncreaseBp : 0)), 10000);

                Multiply(ref numerator, ref denominator, Math.Max(0, packet.VarianceBp), 10000);
                if (!legacyTrue)
                {
                    if (profile?.BypassFinalReduction != true)
                        Multiply(ref numerator, ref denominator, Math.Max(0, 10000 - policy.FinalReductionBp), 10000);
                    if (profile?.BypassFlatReduction != true)
                        numerator -= new BigInteger(Math.Max(0, policy.FlatReduction)) * denominator;
                }
                numerator = BigInteger.Max(BigInteger.Zero, numerator);
            }
            else
            {
                // Additional starts with its own authored percentage of the selected stat.
                // DOT amounts are already snapshotted by their status recipe.
                if (family == DamageFamily.Additional || family == DamageFamily.ElementalReaction)
                    Multiply(ref numerator, ref denominator, Math.Max(0, packet.CoefficientBp), 10000);
                Multiply(ref numerator, ref denominator,
                    Math.Max(0, 10000L + policy.FamilyDealtIncreaseBp - policy.FamilyDealtDecreaseBp), 10000);
                Multiply(ref numerator, ref denominator,
                    Math.Max(0, 10000L + policy.FamilyReceivedIncreaseBp - policy.FamilyReceivedDecreaseBp), 10000);
                Multiply(ref numerator, ref denominator,
                    Math.Max(0, 10000 - policy.FamilySpecificReductionBp), 10000);
            }

            if (!normalFormula && policy.Element == ElementId.None)
                Multiply(ref numerator, ref denominator, Math.Max(0, 10000L + policy.PhysicalReceivedIncreaseBp), 10000);

            // Element stages remain exact until the shared final floor/cap; legacy packets use neutral factors.
            Multiply(ref numerator, ref denominator, Math.Max(0, policy.DotBridgeFactorBp), 10000);
            Multiply(ref numerator, ref denominator, Math.Max(0, policy.ElementFactorBp), 10000);
            Multiply(ref numerator, ref denominator, Math.Max(0, policy.ElementResistanceFactorBp), 10000);
            Multiply(ref numerator, ref denominator, Math.Max(0, policy.AmplificationBp), 10000);
            Multiply(ref numerator, ref denominator, Math.Max(0, policy.ExplosionFactorBp), 10000);
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

            var result = new DamageResult { FormulaDamage = damage, CalculatedDamage = damage,
                WasCritical = critical, WasBlocked = blocked, WasEndured = damage == 0,
                WasCapped = capped, AllocationSurviveAtOne = surviveAtOne };
            Allocate(result, policy, currentHealth, currentShield);
            return result;
        }

        /// <summary>Allocates the already rounded contact; never repeats formula stages or random rolls.</summary>
        public static void Allocate(DamageResult result, DamagePolicy policy, int currentHealth, int currentShield)
        {
            if (result == null || policy == null) throw new ArgumentNullException();
            var damage = Math.Max(0, result.FormulaDamage - result.ConstructAbsorbed);
            var shield = Math.Max(0, currentShield);
            var health = Math.Max(0, currentHealth);
            var efficiency = Math.Max(1, Math.Min(10000, policy.ShieldEfficiencyBp));
            var absorbed = policy.BypassShield ? 0 : (int)Math.Min(damage, (long)shield * efficiency / 10000);
            var shieldLost = policy.BypassShield || damage == 0 ? 0 : absorbed >= (long)shield * efficiency / 10000
                ? shield : (int)Math.Min(shield, ((long)absorbed * 10000 + efficiency - 1) / efficiency);
            shield -= shieldLost;
            var hpDirected = Math.Max(0, damage - absorbed);
            var executes = policy.Family == DamageFamily.Destructive && hpDirected >= health && health > 0;
            var healthLost = executes ? health : Math.Min(health, hpDirected);
            if (result.AllocationSurviveAtOne && !policy.BypassSurviveAtOne && health > 0 && healthLost >= health)
                healthLost = health - 1;
            health -= healthLost;
            if (executes && policy.BypassSurviveAtOne) health = 0;

            result.ShieldAbsorbed = absorbed;
            result.HpDirectedDamage = hpDirected;
            result.ShieldLost = shieldLost;
            result.HealthLost = healthLost;
            result.RemainingHealth = health;
            result.RemainingShield = shield;
            result.Executed = executes && health == 0;
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
