using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Complete templates for ordinary card operations. Complex authored rules use the prose adapter.</summary>
    public static class CardEffectText
    {
        public static bool TryFormat(EffectDefinition root, string authored, out string text)
        {
            text = null;
            if (root == null || root.Batches?.Count > 0 || root.AttributeAdvantageDamageBonusBp != 0) return false;
            // These clauses may be implemented by character reactions rather than
            // the card sequence. Keep their authored conditions and extra rules.
            if (Regex.IsMatch(authored ?? "", @"\b(if|when|each time|steal\w*|extort|additional damage|shared chance|on kill)\b", RegexOptions.IgnoreCase)) return false;
            var operations = new List<(EffectOperationDefinition effect, CardEffectTiming timing)> { (root, CardEffectTiming.Damaging) };
            if (root.Sequence != null)
                operations.AddRange(root.Sequence.Where(s => s?.Effect != null).Select(s => (s.Effect, s.Timing)));
            var clauses = new List<(string text, CardEffectTiming timing, bool damage)>();
            foreach (var step in operations)
            {
                if (step.effect.Kind == EffectKind.None) continue;
                // Legacy support roots can be zero-heal coordinators whose real
                // heal is in Sequence. They do not add a player-facing effect.
                if (step.effect.Kind == EffectKind.Heal && IsEmptyHeal(step.effect)) continue;
                if (step.effect.Conditions?.Any(c => c != null && c.Kind != EffectConditionKind.Always) == true) return false;
                var clause = Describe(step.effect);
                if (clause == null) return false;
                if (root.ElementalApplication?.RequiresCritical == true && step.effect.Id == root.ElementalApplication.StatusEffectId)
                    clause = "On a critical hit, " + Lower(clause);
                if (ReferenceEquals(step.effect, root))
                {
                    if (!string.IsNullOrEmpty(root.ConsumeTargetRecipeId)) clause += ", with +" + Percent(root.DamagePerConsumedStackBp) +
                        " damage per target Conductive stack, then consume all of that target's Conductive";
                    if (root.OnKillSelfHealMaxHealthBp > 0) clause += "; if this attack defeats an enemy, heal the hero for " +
                        Percent(root.OnKillSelfHealMaxHealthBp) + " of Max HP once";
                    if (root.Kind == EffectKind.Damage && root.PowerGaugeAmount < 0) clause += ", then remove " +
                        Math.Abs(root.PowerGaugeAmount) + " Power Gauge orbs from the target";
                }
                clauses.Add((clause, step.timing, step.effect.Kind == EffectKind.Damage));
            }
            if (clauses.Count == 0) return false;
            // Render effects in their action timing order: pre-action statuses,
            // damage, then any follow-up effects.
            if (clauses.Any(c => c.damage))
            {
                var pre = clauses.Where(c => c.timing == CardEffectTiming.BeforeAction).Select(c => c.text).ToArray();
                var main = clauses.Where(c => c.timing != CardEffectTiming.BeforeAction).ToArray();
                if (main.Length == 0 || !main[0].damage) return false;
                var ordered = new List<string>();
                if (pre.Length > 0)
                {
                    ordered.Add(pre[0]);
                    ordered.AddRange(pre.Skip(1).Select(Lower));
                    ordered.Add(Lower(main[0].text));
                }
                else ordered.Add(main[0].text);
                ordered.AddRange(main.Skip(1).Select(c => Lower(c.text)));
                text = string.Join(", then ", ordered) + ".";
            }
            else text = clauses[0].text + string.Concat(clauses.Skip(1).Select(c => ", then " + Lower(c.text))) + ".";
            return true;
        }

        private static string Describe(EffectOperationDefinition effect)
        {
            var target = CardDescriptionText.TargetName(effect.Target);
            switch (effect.Kind)
            {
                case EffectKind.Damage: return CardDescriptionText.DamageSentence(effect);
                case EffectKind.Cleanse: case EffectKind.RemoveDebuffs: return "Remove Debuffs from " + target;
                case EffectKind.RemoveBuffs: return "Remove Buffs from " + target;
                case EffectKind.RemoveStance: return "Cancel Stances on " + target;
                case EffectKind.ChangePowerGauge:
                    var gauge = Math.Abs((long)effect.PowerGaugeAmount);
                    return (effect.PowerGaugeAmount >= 0 ? "Increase " : "Decrease ") + Possessive(target) + " Power Gauge by " + gauge + (gauge == 1 ? " orb" : " orbs");
                case EffectKind.ModifyCardRank:
                    var ranks = effect.Magnitude == 0 ? 1 : effect.Magnitude;
                    return (ranks > 0 ? "Increase" : "Decrease") + " the ranks of " + Possessive(target) + " skill cards by " + Math.Abs((long)ranks);
                case EffectKind.Heal: return Heal(effect, target);
                case EffectKind.ApplyStatus: return Status(effect, target);
                default: return null;
            }
        }

        private static string Heal(EffectOperationDefinition effect, string target)
        {
            var value = effect.HealValue?.IsConfigured == true ? effect.HealValue : null;
            if (value == null) return "Heal " + target + " for " + Percent(effect.HealCoefficientBp) + " of the hero's " + CombatTextCatalog.StatName(effect.HealScalingStat);
            if (value.Source == EffectValueSource.Fixed)
                return "Heal " + target + " for " + ((long)value.FixedAmount * value.CoefficientBp / 10000).ToString(CultureInfo.InvariantCulture) + " HP";
            var basis = value.Source switch
            {
                EffectValueSource.SourceAttack => "the hero's Attack",
                EffectValueSource.TargetMaxHealth => "their Max HP",
                EffectValueSource.TargetMissingHealth => "their missing HP",
                EffectValueSource.ActualHealthDamage => "damage dealt",
                EffectValueSource.ActualShieldDamage => "damage dealt to shields", _ => null
            };
            return basis == null ? null : "Heal " + target + " for " + Percent(value.CoefficientBp) + " of " + basis;
        }

        private static bool IsEmptyHeal(EffectOperationDefinition effect) => effect.HealValue?.IsConfigured != true
            ? effect.HealCoefficientBp == 0 : effect.HealValue.CoefficientBp == 0 ||
                effect.HealValue.Source == EffectValueSource.Fixed && effect.HealValue.FixedAmount == 0;

        private static string Status(EffectOperationDefinition effect, string target)
        {
            var recipe = effect.StatusRecipe;
            if (string.IsNullOrWhiteSpace(recipe?.Id)) return null;
            if ((recipe.Behavior & StatusBehavior.Stance) != 0 || recipe.CounterEnabled || recipe.StanceChildren?.Count > 0) return null;
            var duration = effect.StatusDurationOverride > 0 ? effect.StatusDurationOverride : recipe.DefaultDuration;
            var turns = recipe.DurationClock == StatusDurationClock.Permanent ? "" : " for " + duration + (duration == 1 ? " turn" : " turns");
            var isIgnite = recipe.Tags?.Contains(CombatTags.Ignite) == true || recipe.Id == "status.debuff.ignite";
            var clauses = new List<string>();
            if (recipe.Behavior == StatusBehavior.Stat && !isIgnite && recipe.Modifiers?.Count > 0)
            {
                foreach (var modifier in recipe.Modifiers.Where(m => m != null))
                {
                    var amount = modifier.ScaleByStatusPotency ? (long)modifier.PotencyCoefficientBp *
                        (effect.StatusPotencyBp != 0 ? effect.StatusPotencyBp : recipe.DefaultPotencyBp) / 10000 : modifier.Amount;
                    if (modifier.ResolvedOperation == ModifierOperation.Multiplier) amount -= 10000;
                    var number = modifier.ResolvedOperation == ModifierOperation.Flat ? Math.Abs(amount).ToString(CultureInfo.InvariantCulture) : Percent(Math.Abs(amount));
                    var verb = amount >= 0 ? "Increase " : "Decrease ";
                    var label = modifier.ResolvedTarget == ModifierTarget.Stat ? CombatTextCatalog.StatName(modifier.ResolvedStat) :
                        modifier.ResolvedTarget == ModifierTarget.StatBundle ? Bundle(modifier.ResolvedBundle) : null;
                    if (label != null) clauses.Add(verb + Possessive(target) + " " + label + " by " + number + turns);
                    else if (modifier.ResolvedTarget == ModifierTarget.AnyDamageReceived)
                        clauses.Add(verb + "damage taken by " + target + " by " + number + turns);
                    else if (modifier.ResolvedTarget == ModifierTarget.AnyDamageDealt)
                        clauses.Add(verb + "damage dealt by " + target + " by " + number + turns);
                    else return null;
                }
            }
            else
            {
                var name = string.IsNullOrEmpty(recipe.NameKey) || recipe.NameKey.StartsWith("status.", StringComparison.Ordinal)
                    ? CombatTextCatalog.Normalize(StatusVisualData.GetDisplayName(recipe.Id, recipe.Polarity)) : recipe.NameKey;
                if (recipe.BarrierCoefficientBp > 0)
                    clauses.Add("Apply Barrier equal to " + Percent(recipe.BarrierCoefficientBp) + " of the hero's " +
                        (recipe.BarrierSourceStat == BarrierSourceStat.Defense ? "Defense" : recipe.BarrierSourceStat == BarrierSourceStat.MaxHealth ? "Max HP" : "Attack") + " on " + target + turns);
                else if (recipe.DebuffImmunity && string.IsNullOrWhiteSpace(name)) return null;
                else clauses.Add("Apply " + (effect.StatusStackCount > 1 ? effect.StatusStackCount + " stacks of " : "") + name + " on " + target + turns);
                if (recipe.EndTurnPowerGaugeLoss > 0) clauses.Add("Remove " + recipe.EndTurnPowerGaugeLoss + " Power Gauge orbs at each affected enemy's turn end");
            }
            if (clauses.Count == 0) return null;
            var clause = clauses[0] + string.Concat(clauses.Skip(1).Select(c => ", then " + Lower(c)));
            if (effect.StatusApplyChanceBp < 10000) clause = "Has a " + Percent(effect.StatusApplyChanceBp) + " chance to " + Lower(clause);
            return clause;
        }

        private static string Bundle(StatBundleKind bundle) => bundle switch
        {
            StatBundleKind.BasicStats => "Basic Stats", StatBundleKind.AttackRelated => "Attack-related stats",
            StatBundleKind.DefenseRelated => "Defense-related stats", StatBundleKind.HpRelated => "HP-related stats",
            StatBundleKind.AllStats => "All Stats", _ => "Stats"
        };
        private static string Possessive(string target) => target == "the hero" ? "the hero's" : target.EndsWith("s", StringComparison.Ordinal) ? target + "'" : target + "'s";
        private static string Lower(string text) => char.ToLowerInvariant(text[0]) + text.Substring(1);
        private static string Percent(long value) => (value / 100d).ToString("0.##", CultureInfo.InvariantCulture) + "%";
    }
}
