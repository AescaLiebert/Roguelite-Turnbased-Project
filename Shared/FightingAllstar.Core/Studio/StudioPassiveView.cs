using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Studio
{
    public sealed class StudioRuleView
    {
        public string When, Who, Execution, Availability;
    }
    public sealed class StudioPassiveMechanicView
    {
        public string Id, Name, Kind, Summary, Availability, State;
        public bool Enabled = true;
    }
    /// <summary>Pure presentation projections. The same projection drives initial and live summaries.</summary>
    public static class StudioPassiveView
    {
        private static string Words(object value) => Regex.Replace(value?.ToString() ?? "", "([a-z])([A-Z])", "$1 $2");
        public static string Availability(PassiveGate gate) => gate == null ? "Missing availability" :
            (gate.Presence == PassivePresence.LivingActive ? "On-Field" : gate.Presence == PassivePresence.LivingReserve ? "Off-Field" : "On-Field & Off-Field") +
            " · " + (gate.Modes == BattleModeMask.PvP ? "PVP" : gate.Modes == BattleModeMask.PvE ? "PVE" : "ALL") +
            (gate.MinimumTier == 0 && gate.MaximumTier == 5 ? "" : " · C" + gate.MinimumTier + "–C" + gate.MaximumTier);
        public static string AvailabilitySummary(StudioDocument document)
        {
            var states = Mechanics(document).Where(m => m.Enabled).Select(m => m.Availability).Distinct().ToList();
            return states.Count == 0 ? "No executable mechanics" : states.Count == 1 ? states[0] : "Mixed availability · inspect each mechanic";
        }
        public static IEnumerable<PassiveGate> Gates(StudioDocument document, bool enabledOnly = false)
        {
            var passive = document.EditingCharacter?.Passive;
            foreach (var rule in document.Rules.Where(r => string.IsNullOrEmpty(r.StatusId) && (!enabledOnly || r.Enabled))) yield return rule.Gate;
            if (passive == null) yield break;
            foreach (var aura in passive.Auras) yield return aura.Gate;
            foreach (var reaction in passive.Reactions.Where(r => !document.ReplacedNativeReactionIds.Contains(r.Id))) yield return reaction.Gate;
            foreach (var policy in passive.Policies) yield return policy.Gate;
            foreach (var policy in document.Policies) yield return policy.Definition.Gate;
            if (passive.Support?.IsConfigured == true)
            {
                yield return new PassiveGate { Presence = passive.Support.Presence };
                if (passive.Support.SwirlStrengthBp != 0) yield return new PassiveGate { Presence = passive.Support.SwirlPresence };
            }
        }
        public static string Modifier(StatModifierDefinition modifier)
        {
            if (modifier == null) return "Missing modifier";
            var target = modifier.ResolvedTarget;
            var label = target == ModifierTarget.Stat ? Words(modifier.ResolvedStat) :
                target == ModifierTarget.StatBundle ? Words(modifier.ResolvedBundle) + " bundle" :
                target == ModifierTarget.FamilyDamageDealt ? Words(modifier.ResolvedFamily) + " damage dealt" :
                target == ModifierTarget.FamilyDamageReceived ? Words(modifier.ResolvedFamily) + " damage received" : Words(target);
            var flat = target == ModifierTarget.FlatDamageReduction || target == ModifierTarget.Stat && modifier.ResolvedOperation == ModifierOperation.Flat;
            var amount = flat ? modifier.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture) :
                (modifier.Amount / 100m).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%";
            return label + " · " + (target == ModifierTarget.Stat ? Words(modifier.ResolvedOperation) + " · " : "") +
                (modifier.ScaleByStatusPotency ? "status potency × " + (modifier.PotencyCoefficientBp / 100m).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "%" : amount);
        }
        public static StudioRuleView Rule(StudioRule rule)
        {
            var s = rule.Settings;
            var when = Words(rule.Trigger) + (rule.Trigger == PassiveEventKind.BeforeDamage ? " · synchronous calculation modifiers only" : "") +
                (rule.Trigger == PassiveEventKind.DamageResolved ? " · " + Words(s.DamageTiming) : "") +
                (s.RequireCritical ? " · critical required" : "") + (s.RequireBlocked ? " · blocked required" : "") +
                (s.RequireGaugeLoss ? " · gauge loss" : "") + (!string.IsNullOrEmpty(s.EventStatusRecipeId) ? " · " + s.EventStatusRecipeId : "") +
                (!string.IsNullOrEmpty(s.EventResourceKey) ? " · " + s.EventResourceKey : "") + (s.FilterRemovalReason ? " · " + s.RemovalReason : "");
            return new StudioRuleView { When = when, Who = "Event actor: " + Words(s.ActorRelation) + " · event target: " + Words(s.TargetRelation) +
                (s.FilterCategory ? " · " + Words(s.Category) : "") + (s.FilterCategory && s.MinimumRank > 0 ? " · rank ≥ " + s.MinimumRank : "") +
                (s.FilterDamageFamily ? " · " + Words(s.DamageFamily) + " damage" : "") + (s.OwnTeamTurnOnly ? " · own team turn" : "") +
                (s.CardOriginOnly ? " · card origin" : "") + (s.ExcludeUltimate ? " · excludes ultimate" : ""),
                Availability = Availability(rule.Gate), Execution = (rule.Enabled ? "Enabled" : "Disabled") + " · " + Words(s.Phase) + " · priority " + s.Priority + " · " +
                (rule.RuntimeVersion == 0 ? "Native receipts preserved; advanced frequency controls unavailable" :
                (s.LimitScope == PassiveLimitScope.None ? "No activation limit" : s.MaximumActivations + " per " + Words(s.LimitScope)) + " · cooldown " + s.CooldownOwnerTurns + " owner turns") };
        }
        public static List<StudioPassiveMechanicView> Mechanics(StudioDocument document)
        {
            var output = new List<StudioPassiveMechanicView>(); var passive = document.EditingCharacter?.Passive;
            foreach (var rule in document.Rules)
            {
                var view = Rule(rule);
                output.Add(new StudioPassiveMechanicView { Id = rule.Id, Name = rule.Name, Enabled = rule.Enabled, Kind = rule.Trigger == PassiveEventKind.BeforeDamage ? "Calculation rule" : "Event rule", Availability = view.Availability,
                    Summary = view.When + " · " + view.Who, State = (rule.Enabled ? "" : "Disabled · ") + (rule.RuntimeVersion == 0 ? "Editable · native receipt semantics" : "Supported") });
            }
            if (passive == null) return output;
            foreach (var aura in passive.Auras)
                output.Add(new StudioPassiveMechanicView { Id = aura.Id, Name = aura.Modifiers.Count == 0 ? "Continuous modifiers" : Modifier(aura.Modifiers[0]), Kind = "Continuous rule", Availability = Availability(aura.Gate),
                    Summary = "While available → " + Words(aura.Targets.Relation) + " " + aura.Targets.AttributeId + " · " + string.Join(", ", aura.Modifiers.Select(Modifier)), State = "Supported" });
            foreach (var reaction in passive.Reactions.Where(r => !document.ReplacedNativeReactionIds.Contains(r.Id)))
                output.Add(new StudioPassiveMechanicView { Id = reaction.Id, Name = reaction.Id, Kind = "Compatibility event", Availability = Availability(reaction.Gate),
                    Summary = Words(reaction.Trigger), State = "Read-only · " + (document.CompatibilityReasons.TryGetValue(reaction.Id, out var reason) ? reason : "unsupported import timing or program") });
            foreach (var policy in passive.Policies)
                output.Add(new StudioPassiveMechanicView { Id = policy.Id, Name = policy.Id, Kind = "Interception policy", Availability = Availability(policy.Gate), Summary = Words(policy.Kind), State = "Supported · native policy" });
            foreach (var policy in document.Policies)
                output.Add(new StudioPassiveMechanicView { Id = policy.Id, Name = policy.Name, Kind = "Interception policy", Availability = Availability(policy.Definition.Gate), Summary = Words(policy.Definition.Kind), State = "Supported" });
            if (passive.Support?.IsConfigured == true) SupportMechanics(output, passive.Support, document.OwnsOpeningGauge);
            return output;
        }

        private static void SupportMechanics(List<StudioPassiveMechanicView> output, CombatSupportDefinition support, bool openingOwned)
        {
            string Percent(int bp) => (bp / 100m).ToString("0.##") + "%";
            void Add(string id, string name, string summary, PassivePresence? presence = null) => output.Add(new StudioPassiveMechanicView {
                Id = "compatibility.support." + id, Name = name, Kind = "Compatibility", Availability = Availability(new PassiveGate { Presence = presence ?? support.Presence }),
                Summary = summary, State = "Read-only · dedicated runtime / forecast hook" });
            if (support.OpeningTeamGauge != 0 && !openingOwned) Add("opening", "Opening team Gauge", "Battle starts → active allies gain " + support.OpeningTeamGauge + " Gauge.");
            if (!string.IsNullOrEmpty(support.BonusAgainstRecipeId)) Add("target-bonus", "Conditional Crit & damage", "Attack begins → snapshot targets with " + support.BonusAgainstRecipeId +
                "; their hits gain " + Percent(support.BonusCritChanceBp) + " Crit and " + Percent(support.BonusDamageBp) + " damage.");
            if (!string.IsNullOrEmpty(support.AttackerGaugeAgainstRecipeId)) Add("gauge", "Conditional Gauge acceleration", "Attack begins → qualifying " + support.AttackerGaugeAgainstRecipeId +
                " targets grant the attacker " + Percent(support.AttackerGaugeGainBp) + " Gauge acceleration for 1 turn.");
            if (!string.IsNullOrEmpty(support.AfterAttackStatusRecipeId)) Add("after-attack", "After-attack status", "After a damaging attack → surviving contacted targets receive " + support.AfterAttackStatusRecipeId +
                (support.RequiresElementalTeam ? "; requires an elemental team." : "."));
            if (support.DamageHealLowestAllyBp != 0) Add("recovery", "Damage-based ally recovery", "After attack → heal the lowest-HP active ally other than self by " + Percent(support.DamageHealLowestAllyBp) + " of committed damage.");
            if (support.ExplosionMultiplierBp > 10000) Add("explosion", "Team Explosion multiplier", "Team Explosion factor → " + Percent(support.ExplosionMultiplierBp) + "; strongest provider wins.");
            if (support.SwirlStrengthBp != 0) Add("swirl", "Team Swirl strength", "Team Swirl factor gains " + Percent(support.SwirlStrengthBp) + ".", support.SwirlPresence);
            if (support.AttackCadence != 0) Add("cadence", "Attack cadence", "Every " + support.AttackCadence + " attacks → " + Percent(support.CadenceDamageBp) + " damage and " + Percent(support.CadenceDotBp) + " DOT bonus.");
        }
    }
}
