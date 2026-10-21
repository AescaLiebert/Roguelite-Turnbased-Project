using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;

namespace FightingAllstar.Core.Studio
{
    /// <summary>Semantic import and ownership operations, independent of Unity and view controls.</summary>
    public static class StudioPassiveEditing
    {
        public static void SetAvailability(StudioDocument document, PassivePresence? presence, BattleModeMask? modes)
        {
            if (presence.HasValue && presence != PassivePresence.LivingActive && presence != PassivePresence.LivingReserve)
                throw new ArgumentException("Choose On-Field or Off-Field.", nameof(presence));
            if (modes.HasValue && modes != BattleModeMask.All && modes != BattleModeMask.PvP && modes != BattleModeMask.PvE)
                throw new ArgumentException("Choose ALL, PVP or PVE.", nameof(modes));
            if (modes.HasValue && modes != BattleModeMask.All && document.EditingCharacter?.Passive?.Support?.IsConfigured == true)
                throw new InvalidOperationException("Preserved support hooks do not support a battle-mode restriction.");
            var character = StudioDocumentService.BeginKitEditing(document);
            foreach (var gate in StudioPassiveView.Gates(document).Where(g => g != null))
            { if (presence.HasValue) gate.Presence = presence.Value; if (modes.HasValue) gate.Modes = modes.Value; }
            if (character.PassiveSource == null) character.PassiveSource = new PassiveSourceDefinition();
            if (presence.HasValue)
            {
                character.PassiveSource.SourceType = presence == PassivePresence.LivingReserve ? "Off-Field" : "On-Field";
                if (character.Passive?.Support != null)
                { character.Passive.Support.Presence = presence.Value; character.Passive.Support.SwirlPresence = presence.Value; }
            }
            if (modes.HasValue) character.PassiveSource.Restriction = modes == BattleModeMask.PvP ? "PVP" : modes == BattleModeMask.PvE ? "PVE" : "ALL";
        }

        public static void RemoveSupportClause(StudioDocument document, string mechanicId)
        {
            var support = StudioDocumentService.BeginKitEditing(document).Passive?.Support;
            if (support == null) return;
            switch (mechanicId)
            {
                case "compatibility.support.opening": support.OpeningTeamGauge = 0; break;
                case "compatibility.support.target-bonus": support.BonusAgainstRecipeId = null; support.BonusCritChanceBp = 0; support.BonusDamageBp = 0; break;
                case "compatibility.support.gauge": support.AttackerGaugeAgainstRecipeId = null; support.AttackerGaugeGainBp = 0; break;
                case "compatibility.support.after-attack": support.AfterAttackStatusRecipeId = null; support.RequiresElementalTeam = false; break;
                case "compatibility.support.recovery": support.DamageHealLowestAllyBp = 0; break;
                case "compatibility.support.explosion": support.ExplosionMultiplierBp = 10000; break;
                case "compatibility.support.swirl": support.SwirlStrengthBp = 0; break;
                case "compatibility.support.cadence": support.AttackCadence = 0; support.CadenceDamageBp = 0; support.CadenceDotBp = 0; break;
                default: throw new ArgumentException("Unknown compatibility mechanic: " + mechanicId, nameof(mechanicId));
            }
        }
        public static void Normalize(StudioDocument document)
        {
            foreach (var rule in document.Rules.Where(r => string.IsNullOrEmpty(r.ActionRootId)).ToArray())
            {
                var id = Unique(document, rule.Id + ".opening");
                document.Actions.Add(new StudioActionNode { Id = id, Kind = StudioActionKind.Command,
                    AmountParameterId = rule.AmountParameterId, Command = new PassiveCommandDefinition { Kind = PassiveCommandKind.ExecuteEffect,
                        Effect = new CardEffectOperationDefinition { Kind = CardEffectOperationKind.ChangePowerGauge,
                            Target = rule.Target, Magnitude = document.Parameters.FirstOrDefault(p => p.Id == rule.AmountParameterId)?.Value ?? 0 } } });
                rule.ActionRootId = id; rule.RuntimeVersion = 1;
            }
        }

        public static void ImportReactions(StudioDocument document, DescriptionLibrary library = null)
        {
            var native = document.EditingCharacter?.Passive;
            if (native == null) return;
            foreach (var reaction in document.LegacyCharacter.Passive?.Reactions ?? native.Reactions)
                if (!document.NativeReactionOrder.Contains(reaction.Id)) document.NativeReactionOrder.Add(reaction.Id);
            foreach (var reaction in native.Reactions)
            {
                if (document.ReplacedNativeReactionIds.Contains(reaction.Id) || document.Rules.Any(r => r.Id == reaction.Id)) continue;
                if (!StudioAuthoringCatalog.Triggers.Contains(reaction.Trigger) || reaction.Actions.Count > 0 && reaction.ExecutionVersion == 0 ||
                    reaction.Actions.Any(a => !CanImportAction(a)))
                {
                    document.CompatibilityReasons[reaction.Id] = "The native timing or operation context cannot yet be represented losslessly.";
                    continue;
                }
                var actionCount = document.Actions.Count; var conditionCount = document.Conditions.Count;
                var rule = new StudioRule { Id = reaction.Id, Name = reaction.Id, Imported = true, RuntimeVersion = reaction.ExecutionVersion,
                    Trigger = reaction.Trigger, Gate = reaction.Gate.Clone(), Settings = reaction.Clone() };
                rule.ConditionRootId = Conditions(document, reaction.Conditions, reaction.Id + ".eligibility");
                if (reaction.Actions.Count > 0)
                {
                    var root = new StudioActionNode { Id = Unique(document, reaction.Id + ".program"), Kind = StudioActionKind.Sequence };
                    document.Actions.Add(root);
                    foreach (var action in reaction.Actions) root.Children.Add(Action(document, action, reaction.Id + ".action"));
                    foreach (var command in reaction.Commands) root.Children.Add(Command(document, command, reaction.Id + ".command"));
                    rule.ActionRootId = root.Id;
                }
                else rule.ActionRootId = Commands(document, reaction.Commands, reaction.Id + ".commands");
                if (reaction.ElseCommands.Count > 0) rule.ElseActionRootId = Commands(document, reaction.ElseCommands, reaction.Id + ".else");
                document.Rules.Add(rule); document.ReplacedNativeReactionIds.Add(reaction.Id);
                var candidate = StudioCompiler.Compile(document, library);
                if (!candidate.Success)
                {
                    document.Rules.Remove(rule); document.ReplacedNativeReactionIds.Remove(reaction.Id);
                    document.Actions.RemoveRange(actionCount, document.Actions.Count - actionCount);
                    document.Conditions.RemoveRange(conditionCount, document.Conditions.Count - conditionCount);
                    document.CompatibilityReasons[reaction.Id] = candidate.Diagnostics.First(d => d.Severity == StudioDiagnosticSeverity.Error).Message;
                }
                else document.CompatibilityReasons.Remove(reaction.Id);
            }
        }

        private static bool CanImportAction(PassiveActionDefinition action) => action != null &&
            (action.Kind == PassiveActionKind.Command || action.Commands.Count == 0) &&
            action.Children.All(CanImportAction) && action.ElseChildren.All(CanImportAction);

        private static string Unique(StudioDocument d, string seed)
        {
            var used = new HashSet<string>(d.Rules.Select(r => r.Id).Concat(d.Conditions.Select(c => c.Id)).Concat(d.Actions.Select(a => a.Id))
                .Concat(d.Parameters.Select(p => p.Id)).Concat(d.Resources.Select(r => r.Id)).Concat(d.Statuses.Select(s => s.Id)));
            var id = seed; var suffix = 1; while (used.Contains(id)) id = seed + "." + suffix++; return id;
        }
        private static string Conditions(StudioDocument d, IEnumerable<EffectConditionDefinition> conditions, string seed)
        {
            var root = new StudioConditionNode { Id = Unique(d, seed), Logic = ConditionLogic.All }; d.Conditions.Add(root);
            foreach (var condition in conditions) root.Children.Add(Condition(d, condition, seed + ".condition"));
            if (root.Children.Count == 0) { root.Logic = ConditionLogic.Leaf; root.Kind = EffectConditionKind.Always; }
            return root.Id;
        }
        private static string Condition(StudioDocument d, EffectConditionDefinition c, string seed)
        {
            var node = new StudioConditionNode { Id = Unique(d, seed), Logic = c.Logic, Kind = c.Kind, Subject = c.Subject,
                Negate = c.Negate, Threshold = c.Threshold, RecipeId = c.RecipeId, Tag = c.Tag, StringValue = c.StringValue,
                RosterFilter = c.RosterFilter?.Clone(), InitialRoster = c.InitialRoster };
            d.Conditions.Add(node);
            foreach (var child in c.Children) node.Children.Add(Condition(d, child, seed + ".child"));
            return node.Id;
        }
        private static string Command(StudioDocument d, PassiveCommandDefinition c, string seed)
        { var node = new StudioActionNode { Id = Unique(d, seed), Kind = StudioActionKind.Command, Command = c.Clone() }; d.Actions.Add(node); return node.Id; }
        private static string Commands(StudioDocument d, IEnumerable<PassiveCommandDefinition> commands, string seed)
        {
            var node = new StudioActionNode { Id = Unique(d, seed), Kind = StudioActionKind.Sequence }; d.Actions.Add(node);
            foreach (var command in commands) node.Children.Add(Command(d, command, seed + ".command"));
            return node.Id;
        }
        private static string Action(StudioDocument d, PassiveActionDefinition a, string seed)
        {
            // Command lists share the containing operation context in the runtime. Retain that boundary as a sequence.
            var node = new StudioActionNode { Id = Unique(d, seed), Kind = (StudioActionKind)a.Kind, Target = a.Target, Query = a.Query?.Clone(),
                MaximumTargets = a.MaximumTargets, ResourceKey = a.ResourceKey, Amount = a.Amount, ResourceCap = a.ResourceCap,
                StatusRecipeId = a.StatusRecipeId, StatusOwnerOnly = a.StatusOwnerOnly, RequireFullAmount = a.RequireFullAmount,
                RemovalReason = a.RemovalReason, ResultBinding = a.ResultBinding };
            d.Actions.Add(node);
            if (a.Conditions.Count > 0) node.ConditionRootId = Conditions(d, a.Conditions, seed + ".conditions");
            if (a.Kind == PassiveActionKind.Command)
            {
                if (a.Commands.Count == 1) node.Command = a.Commands[0].Clone();
                else { node.Kind = StudioActionKind.Sequence; foreach (var command in a.Commands) node.Children.Add(Command(d, command, seed + ".command")); }
            }
            else foreach (var command in a.Commands) node.Children.Add(Command(d, command, seed + ".command"));
            foreach (var child in a.Children) node.Children.Add(Action(d, child, seed + ".child"));
            foreach (var child in a.ElseChildren) node.ElseChildren.Add(Action(d, child, seed + ".else"));
            return node.Id;
        }

        public static List<PassiveCommandDefinition> CompileLegacyCommands(StudioDocument document, string root, StudioCompileResult result)
        {
            var output = new List<PassiveCommandDefinition>(); var path = new HashSet<string>();
            void Visit(string id)
            {
                var node = document.Actions.FirstOrDefault(a => a.Id == id);
                if (node == null || !path.Add(id) || !string.IsNullOrEmpty(node.ConditionRootId) || node.ElseChildren.Count > 0 ||
                    node.Kind != StudioActionKind.Command && node.Kind != StudioActionKind.Sequence)
                { result.Diagnostics.Add(new StudioDiagnostic { Severity = StudioDiagnosticSeverity.Error, Code = "rule.legacy-program", SourceId = id,
                    Message = "Imported version-zero receipts require an ordered command list. Nested branches/loops need an explicit verified runtime migration." }); return; }
                if (node.Kind == StudioActionKind.Command)
                {
                    var command = StudioCompiler.CompileAction(node.Id, document.Conditions.ToDictionary(c => c.Id), document.Actions.ToDictionary(a => a.Id),
                        document.Modules.ToDictionary(m => m.Id), document.Parameters.ToDictionary(p => p.Id)).Commands.Single();
                    if (command.Effect != null) command.Effect.Id = node.Command.Effect?.Id;
                    output.Add(command);
                }
                foreach (var child in node.Children) Visit(child);
                path.Remove(id);
            }
            Visit(root); return output;
        }

        public static CharacterDefinition RemoveGenerated(CharacterDefinition current, StudioDocument document)
        {
            var copy = current.Clone();
            var characterRules = new HashSet<string>(document.Rules.Where(r => string.IsNullOrEmpty(r.StatusId)).Select(r => r.Id));
            copy.Passive?.Reactions.RemoveAll(r => characterRules.Contains(r.Id));
            var policies = new HashSet<string>(document.Policies.Select(p => p.Id)); copy.Passive?.Policies.RemoveAll(p => policies.Contains(p.Id));
            foreach (var skill in copy.Skills)
            {
                var variants = new HashSet<string>(document.AbilityVariants.Where(v => v.SkillId == skill.Id).Select(v => v.Definition.Id));
                skill.Variants.RemoveAll(v => variants.Contains(v.Id));
                var overrides = new HashSet<string>(document.AbilityOverrides.Where(v => v.SkillId == skill.Id && string.IsNullOrEmpty(v.VariantId)).Select(v => v.Definition.Id));
                skill.ValueOverrides.RemoveAll(v => overrides.Contains(v.Id));
                foreach (var variant in skill.Variants)
                {
                    var owned = new HashSet<string>(document.AbilityOverrides.Where(v => v.SkillId == skill.Id && v.VariantId == variant.Id).Select(v => v.Definition.Id));
                    variant.Overrides.RemoveAll(v => owned.Contains(v.Id));
                }
            }
            // Status-local declarations are removed only from matching recipe objects, never arbitrary ID-bearing arrays.
            var token = Newtonsoft.Json.Linq.JToken.Parse(SnapshotJson.Serialize(copy));
            foreach (var recipe in ((Newtonsoft.Json.Linq.JContainer)token).Descendants().OfType<Newtonsoft.Json.Linq.JObject>()
                .Where(o => o["Reactions"] is Newtonsoft.Json.Linq.JArray && o["DurationClock"] != null))
            {
                var id = (string)recipe["Id"];
                var owned = new HashSet<string>(document.Rules.Where(r => r.StatusId == id).Select(r => r.Id));
                foreach (var reaction in ((Newtonsoft.Json.Linq.JArray)recipe["Reactions"]).OfType<Newtonsoft.Json.Linq.JObject>().Where(r => owned.Contains((string)r["Id"])).ToArray()) reaction.Remove();
            }
            return SnapshotJson.Deserialize<CharacterDefinition>(token.ToString());
        }
    }
}
