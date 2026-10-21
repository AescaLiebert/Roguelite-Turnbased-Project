using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FightingAllstar.Core.Studio
{
    public static partial class StudioCompiler
    {
        public static StudioCompileResult Compile(StudioDocument document, DescriptionLibrary library = null)
        {
            var result = new StudioCompileResult();
            if (document == null) { Error(result, "document.missing", "", "Select or import a draft."); return result; }
            if (document.SchemaVersion != StudioDocument.CurrentSchemaVersion)
                Error(result, "schema.unsupported", document.Id, "Unsupported schema. Preserve the source and use an explicit migration.");
            if (string.IsNullOrWhiteSpace(document.Id)) Error(result, "id.missing", "", "Document requires a stable ID.");
            if (document.LegacyCharacter == null) Error(result, "source.missing", document.Id, "A frozen imported character is required.");
            else if (document.SourceCharacterId != document.LegacyCharacter.Id)
                Error(result, "source.mismatch", document.Id, "Source character ID differs from the frozen import.");
            if (document.AuthoredCharacter != null && document.AuthoredCharacter.Id != document.SourceCharacterId)
                Error(result, "authoring.identity", document.Id, "Keep the imported stable identity while editing; choose the published identity in native package review.");
            document = ExpandLibraryInputs(document, result);
            var parameters = Index(document.Parameters, p => p.Id, result, "parameter");
            var nodes = Index(document.Conditions, n => n.Id, result, "condition");
            var rules = Index(document.Rules, r => r.Id, result, "rule");
            var actions = Index(document.Actions, a => a.Id, result, "action");
            var modules = Index(document.Modules, m => m.Id, result, "module");
            var policies = Index(document.Policies, p => p.Id, result, "policy");
            if (document.AbilityVariants == null || document.AbilityOverrides == null)
                Error(result, "ability.table", document.Id, "Ability authoring tables are missing.");
            var allIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var id in parameters.Keys.Concat(nodes.Keys).Concat(rules.Keys).Concat(actions.Keys).Concat(modules.Keys).Concat(policies.Keys))
                if (!allIds.Add(id)) Error(result, "id.duplicate", id, "IDs must be unique across all local authoring tables.");
            foreach (var p in parameters.Values)
                if (!Enum.IsDefined(typeof(StudioParameterUnit), p.Unit)) Error(result, "parameter.unit", p.Id, "Unknown parameter unit.");
            if (nodes.Count > 256) Error(result, "graph.budget", document.Id, "The condition budget is 256 nodes.");
            foreach (var n in nodes.Values) ValidateNode(n, nodes, parameters, result);
            foreach (var n in nodes.Values) CheckCycle(n.Id, nodes, new HashSet<string>(), new HashSet<string>(), result, 0);
            foreach (var r in rules.Values)
            {
                if (!StudioAuthoringCatalog.Triggers.Contains(r.Trigger)) Error(result, "trigger.unsupported", r.Id, "This trigger is not supported by Studio's general action dispatcher.");
                if (string.IsNullOrEmpty(r.ConditionRootId) || !nodes.ContainsKey(r.ConditionRootId))
                    Error(result, "reference.condition", r.Id, "Condition root is missing.");
                if (string.IsNullOrEmpty(r.ActionRootId))
                {
                    if (r.Trigger != PassiveEventKind.BattleStarted) Error(result, "trigger.legacy-slice", r.Id, "Convert this opening-gauge rule to an action sequence before changing its trigger.");
                    if (r.Target != EffectTargetScope.AllAllies) Error(result, "target.unsupported", r.Id, "The original gauge slice targets active allies.");
                    if (!parameters.TryGetValue(r.AmountParameterId ?? "", out var amount))
                        Error(result, "reference.parameter", r.Id, "Gauge amount parameter is missing.");
                    else if (amount.Unit != StudioParameterUnit.Integer || amount.Value < 0 || amount.Value > 5)
                        Error(result, "parameter.gauge", amount.Id, "Gauge amount must be an integer from 0 to 5.");
                }
                else if (!actions.ContainsKey(r.ActionRootId)) Error(result, "reference.action", r.Id, "Rule action root is missing.");
                var gate = r.Gate;
                if (gate == null || gate.MinimumTier < 0 || gate.MaximumTier > 5 || gate.MinimumTier > gate.MaximumTier ||
                    !Enum.IsDefined(typeof(PassivePresence), gate.Presence) || gate.Modes == 0 || (gate.Modes & ~BattleModeMask.All) != 0 || gate.AllowDefeatedOwner)
                    Error(result, "gate.unsupported", r.Id, "Use a living owner, tier 0–5, and a supported presence/mode.");
                if (document.EditingCharacter?.Passive?.Reactions?.Any(old => old?.Id == r.Id && !document.ReplacedNativeReactionIds.Contains(old.Id)) == true ||
                    document.EditingCharacter?.Passive?.Auras?.Any(old => old?.Id == r.Id) == true)
                    Error(result, "id.legacy-collision", r.Id, "Rule ID collides with preserved imported content.");
            }
            if (!document.OwnsOpeningGauge && rules.Values.Any(r => string.IsNullOrEmpty(r.ActionRootId)))
                Error(result, "ownership.missing", document.Id, "This draft has not explicitly imported ownership of opening gauge.");
            // Expansion is safe only after dangling references and cycles have been rejected.
            if (result.Diagnostics.Any(d => d.Severity == StudioDiagnosticSeverity.Error)) return result;
            ValidateM3(document, parameters, nodes, rules, actions, modules, policies, result);
            if (result.Diagnostics.Any(d => d.Severity == StudioDiagnosticSeverity.Error)) return result;
            result.Diagnostics.AddRange(StudioBindingAnalysis.Analyze(document).Diagnostics);
            if (result.Diagnostics.Any(d => d.Severity == StudioDiagnosticSeverity.Error)) return result;
            foreach (var rule in rules.Values)
            {
                var remaining = 1024;
                if (!WithinExpansionBudget(rule.ConditionRootId, nodes, ref remaining))
                    Error(result, "graph.expansion-budget", rule.Id, "Shared condition references expand beyond 1024 runtime nodes.");
            }
            foreach (var policy in policies.Values.Where(p => !string.IsNullOrEmpty(p.ConditionRootId)))
            {
                var remaining = 1024;
                if (!WithinExpansionBudget(policy.ConditionRootId, nodes, ref remaining))
                    Error(result, "graph.expansion-budget", policy.Id, "Policy conditions expand beyond 1024 runtime nodes.");
            }
            if (result.Diagnostics.Any(d => d.Severity == StudioDiagnosticSeverity.Error)) return result;

            var character = document.EditingCharacter.Clone();
            var nativeOrder = (document.NativeReactionOrder.Count > 0 ? document.NativeReactionOrder :
                (document.LegacyCharacter.Passive?.Reactions ?? new List<PassiveReactionDefinition>()).Select(r => r.Id).ToList())
                .Select((id, index) => new { Id = id, Index = index }).GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.First().Index);
            if (character.Passive == null) character.Passive = new PassiveDefinition { Id = character.Id + ".passive" };
            character.Passive.Reactions.RemoveAll(r => document.ReplacedNativeReactionIds.Contains(r.Id));
            if (document.OwnsOpeningGauge && character.Passive.Support != null) character.Passive.Support.OpeningTeamGauge = 0;
            var localStatusRules = new List<KeyValuePair<string, PassiveReactionDefinition>>();
            foreach (var r in rules.Values.Where(r => r.Enabled).OrderBy(r => r.Id, StringComparer.Ordinal))
            {
                var reaction = r.Settings.Clone();
                reaction.Id = r.Id; reaction.Trigger = r.Trigger; reaction.Gate = r.Gate.Clone();
                reaction.Conditions = new List<EffectConditionDefinition>();
                reaction.Commands = new List<PassiveCommandDefinition>(); reaction.ElseCommands = new List<PassiveCommandDefinition>();
                reaction.Actions = new List<PassiveActionDefinition>();
                reaction.Conditions.Add(CompileNode(r.ConditionRootId, nodes, parameters));
                if (!string.IsNullOrEmpty(r.ActionRootId))
                {
                    reaction.ExecutionVersion = r.RuntimeVersion;
                    if (r.RuntimeVersion == 0 || r.Trigger == PassiveEventKind.BeforeDamage)
                        reaction.Commands = StudioPassiveEditing.CompileLegacyCommands(document, r.ActionRootId, result);
                    else reaction.Actions.Add(CompileAction(r.ActionRootId, nodes, actions, modules, parameters));
                }
                else { reaction.ExecutionVersion = 1; reaction.Commands.Add(new PassiveCommandDefinition { Kind = PassiveCommandKind.ExecuteEffect,
                    Effect = new CardEffectOperationDefinition { Id = r.Id + ".gauge", Window = CardEffectWindow.BeforeAction,
                        Kind = CardEffectOperationKind.ChangePowerGauge, Target = r.Target,
                        Magnitude = parameters[r.AmountParameterId].Value } }); }
                if (!string.IsNullOrEmpty(r.ElseActionRootId))
                    reaction.ElseCommands = StudioPassiveEditing.CompileLegacyCommands(document, r.ElseActionRootId, result);
                if (string.IsNullOrEmpty(r.StatusId)) character.Passive.Reactions.Add(reaction);
                else localStatusRules.Add(new KeyValuePair<string, PassiveReactionDefinition>(r.StatusId, reaction));
            }
            // Imported version-zero modifiers retain native composition/rounding and dispatch order,
            // including declarations that remain in compatibility form.
            character.Passive.Reactions = character.Passive.Reactions.OrderBy(r => nativeOrder.TryGetValue(r.Id, out var index) ? index : int.MaxValue).ToList();
            foreach (var policy in policies.Values.OrderBy(p => p.Id, StringComparer.Ordinal))
                character.Passive.Policies.Add(CompilePolicy(policy, nodes, parameters));
            CompileAbilities(document, character, parameters, result);
            // SkillCardSO persists root-level target/choreography as the authority for all ranks.
            // Compile the same canonical payload so preview, gameplay hashes, and native round-trip validation agree.
            foreach (var skill in character.Skills ?? new List<SkillDefinition>())
                CardRules.ApplySharedSkillSettings(skill);
            AttachStatusRules(document, character, localStatusRules, result);
            var candidateCatalog = new ContentCatalog { Characters = new List<CharacterDefinition> { character },
                ElementalRules = ElementalRuleSet.CreateDefault(), StatusRecipes = StandardEffectDatabase.CreateStatusRecipes(),
                AttackEffectRecipes = StandardEffectDatabase.CreateAttackEffects() };
            foreach (var message in ContentValidator.Validate(candidateCatalog, false)) Error(result, "character.validation", document.Id, message);
            foreach (var message in PassiveRuleValidator.Validate(character.Passive)) Error(result, "runtime.validation", document.Id, message);
            foreach (var message in AbilityVariantValidator.Validate(character))
            {
                var source = document.AbilityVariants.Select(v => v.Definition.Id).Concat(document.AbilityOverrides.Select(v => v.Definition.Id))
                    .FirstOrDefault(id => message.StartsWith(id + ":", StringComparison.Ordinal) || message.StartsWith(id + "/", StringComparison.Ordinal));
                Error(result, "ability.validation", source ?? document.Id, message);
            }
            if (result.Diagnostics.Any(d => d.Severity == StudioDiagnosticSeverity.Error)) return result;

            try
            {
                result.Description = DescriptionCompiler.Compile(document.Description, library ?? DescriptionLibrary.CreateOpeningGaugeLibrary(),
                    DescriptionAuthoring.Context(document, character, document.Description.DefaultLocale, 1, 1, 0, ""));
                foreach (var locale in document.Description.Locales.Where(l => l.Locale != document.Description.DefaultLocale))
                {
                    var translated = DescriptionCompiler.Compile(document.Description, library ?? DescriptionLibrary.CreateOpeningGaugeLibrary(),
                        DescriptionAuthoring.Context(document, character, locale.Locale, 1, 1, 0, ""));
                    if (!translated.Success) Error(result, "description.locale-invalid", document.Description.Id, "Translation " + locale.Locale + " contains errors. Open its wording diagnostics.");
                }
            }
            catch (Exception exception) { Error(result, "description.binding", document.Description?.Id, exception.Message); return result; }
            if (!result.Description.Success) Error(result, "description.invalid", document.Description?.Id, "Description has errors. Open its diagnostics before activation.");
            result.Hashes = new StudioHashes {
                Gameplay = StudioHash.Compute("studio-gameplay-v1", GameplayProjection(character)),
                Text = StudioHash.Compute("studio-text-v2", new JObject { ["writer"] = result.Description.TextHash,
                    ["native"] = new JArray(((JContainer)JToken.Parse(SnapshotJson.Serialize(character))).DescendantsAndSelf().OfType<JObject>()
                        .SelectMany(o => o.Properties()).Where(p => NonGameplay.Contains(p.Name))
                        .Select(p => new JObject { ["path"] = p.Path, ["value"] = p.Value.DeepClone() })) }),
                Presentation = StudioHash.Compute("studio-presentation-v1", JToken.FromObject(document.Presentation ?? new StudioPresentationProfile())) };
            result.Hashes.Bundle = StudioHash.Compute("studio-bundle-v1", new JObject {
                ["schema"] = document.SchemaVersion, ["gameplay"] = result.Hashes.Gameplay, ["text"] = result.Hashes.Text,
                ["presentation"] = result.Hashes.Presentation });
            result.Success = !result.Diagnostics.Any(d => d.Severity == StudioDiagnosticSeverity.Error);
            if (result.Success) result.Character = character;
            return result;
        }

        private static void ValidateNode(StudioConditionNode n, Dictionary<string, StudioConditionNode> nodes,
            Dictionary<string, StudioParameter> parameters, StudioCompileResult result)
        {
            if (!Enum.IsDefined(typeof(ConditionLogic), n.Logic)) Error(result, "condition.logic", n.Id, "Unsupported logic.");
            var children = n.Children ?? new List<string>();
            if (n.Logic == ConditionLogic.Leaf && children.Count != 0 || n.Logic == ConditionLogic.Not && children.Count != 1 ||
                (n.Logic == ConditionLogic.All || n.Logic == ConditionLogic.Any) && children.Count == 0)
                Error(result, "condition.arity", n.Id, "Leaf has no children; Not has exactly one; All/Any need at least one.");
            foreach (var id in children)
                if (string.IsNullOrEmpty(id) || !nodes.ContainsKey(id)) Error(result, "reference.child", n.Id, "Missing child: " + id);
            if (n.Logic != ConditionLogic.Leaf) return;
            if (!StudioAuthoringCatalog.Conditions.Contains(n.Kind))
                Error(result, "condition.unsupported", n.Id, "This condition is not enabled for Studio authoring.");
            if (!Enum.IsDefined(typeof(ConditionSubject), n.Subject)) Error(result, "condition.subject", n.Id, "Unknown subject.");
            var value = n.Threshold;
            if (!string.IsNullOrEmpty(n.ThresholdParameterId))
            {
                if (!parameters.TryGetValue(n.ThresholdParameterId, out var p)) Error(result, "reference.threshold", n.Id, "Threshold parameter is missing.");
                else
                {
                    value = p.Value;
                    var unit = n.Kind == EffectConditionKind.SourceHealthAtMost || n.Kind == EffectConditionKind.TargetHealthAtMost ? StudioParameterUnit.BasisPoints : StudioParameterUnit.Integer;
                    if (p.Unit != unit) Error(result, "parameter.unit-mismatch", n.Id, "Threshold parameter has an incompatible unit.");
                }
            }
            if ((n.Kind == EffectConditionKind.SourceGaugeAtLeast || n.Kind == EffectConditionKind.TargetGaugeAtLeast) && (value < 0 || value > 5) ||
                (n.Kind == EffectConditionKind.SourceHealthAtMost || n.Kind == EffectConditionKind.TargetHealthAtMost) && (value < 0 || value > 10000) ||
                n.Kind == EffectConditionKind.RosterCountAtLeast && (value < 1 || value > 16))
                Error(result, "condition.threshold", n.Id, "Threshold is outside the supported domain.");
            if (n.Kind == EffectConditionKind.RosterCountAtLeast &&
                (n.RosterFilter == null || !Enum.IsDefined(typeof(PassiveRelation), n.RosterFilter.Relation)))
                Error(result, "condition.roster", n.Id, "Roster filter is required.");
            if ((n.Kind == EffectConditionKind.CounterAtLeast || n.Kind == EffectConditionKind.TargetAttributeIs ||
                 n.Kind == EffectConditionKind.TargetTraitIs || n.Kind == EffectConditionKind.TargetSeriesIs ||
                 n.Kind == EffectConditionKind.BindingValueAtLeast) && string.IsNullOrWhiteSpace(n.StringValue))
                Error(result, "condition.value", n.Id, "This condition needs its resource key or roster identity.");
            if ((n.Kind == EffectConditionKind.TargetHasRecipe || n.Kind == EffectConditionKind.TargetHasRecipeFromEffectOwner) &&
                string.IsNullOrWhiteSpace(n.RecipeId) && string.IsNullOrWhiteSpace(n.StringValue))
                Error(result, "condition.recipe", n.Id, "This condition needs a status recipe ID.");
            if ((n.Kind == EffectConditionKind.SourceHasStatusTag || n.Kind == EffectConditionKind.TargetHasStatusTag) && string.IsNullOrWhiteSpace(n.Tag))
                Error(result, "condition.tag", n.Id, "This condition needs a status tag.");
            if (n.Kind == EffectConditionKind.OwnerLevelAtLeast && value < 1 ||
                n.Kind == EffectConditionKind.OwnerTierAtLeast && (value < 0 || value > 5) ||
                n.Kind == EffectConditionKind.CardRankAtLeast && (value < 1 || value > 3))
                Error(result, "condition.axis", n.Id, "Level starts at 1; tier is 0–5; card rank is 1–3.");
        }

        private static void CheckCycle(string id, Dictionary<string, StudioConditionNode> nodes, HashSet<string> path,
            HashSet<string> done, StudioCompileResult result, int depth)
        {
            if (!nodes.ContainsKey(id) || done.Contains(id)) return;
            if (depth > 32) { Error(result, "graph.depth", id, "Maximum condition nesting is 32."); return; }
            if (!path.Add(id)) { Error(result, "graph.cycle", id, "Condition cycle detected."); return; }
            foreach (var child in nodes[id].Children ?? new List<string>())
                if (child != null) CheckCycle(child, nodes, path, done, result, depth + 1);
            path.Remove(id); done.Add(id);
        }

        private static bool WithinExpansionBudget(string id, Dictionary<string, StudioConditionNode> nodes, ref int remaining)
        {
            if (--remaining < 0) return false;
            foreach (var child in nodes[id].Children ?? new List<string>())
                if (!WithinExpansionBudget(child, nodes, ref remaining)) return false;
            return true;
        }

        private static EffectConditionDefinition CompileNode(string id, Dictionary<string, StudioConditionNode> nodes,
            Dictionary<string, StudioParameter> parameters)
        {
            var n = nodes[id];
            return new EffectConditionDefinition { StudioNodeId = n.Id, Logic = n.Logic, Kind = n.Kind, Subject = n.Subject,
                Threshold = string.IsNullOrEmpty(n.ThresholdParameterId) ? n.Threshold : parameters[n.ThresholdParameterId].Value, Negate = n.Negate,
                StringValue = n.StringValue, RecipeId = n.RecipeId, Tag = n.Tag, RosterFilter = n.RosterFilter?.Clone(), InitialRoster = n.InitialRoster,
                Children = (n.Children ?? new List<string>()).Select(child => CompileNode(child, nodes, parameters)).ToList() };
        }

        private static Dictionary<string, T> Index<T>(IEnumerable<T> values, Func<T, string> id,
            StudioCompileResult result, string kind) where T : class
        {
            var index = new Dictionary<string, T>(StringComparer.Ordinal);
            if (values == null) { Error(result, "table.missing", kind, "Required table is missing."); return index; }
            foreach (var value in values)
                if (value == null || string.IsNullOrWhiteSpace(id(value))) Error(result, "id.missing", kind, "Every item requires an ID.");
                else if (index.ContainsKey(id(value))) Error(result, "id.duplicate", id(value), "Duplicate " + kind + " ID.");
                else index.Add(id(value), value);
            return index;
        }

        public static JToken GameplayProjection(CharacterDefinition character)
        {
            var token = JToken.Parse(SnapshotJson.Serialize(character));
            StripText(token);
            return token;
        }

        private static readonly HashSet<string> NonGameplay = new HashSet<string>(StringComparer.Ordinal) {
            "DisplayName", "Description", "SourceDescription", "DescriptionKey", "Wording", "NameKey", "ReviewNotes", "SourceStats",
            "SourceTraits", "Provenance", "StatsProvenance", "PassiveSource", "HolyRelicSource", "SourceLabel",
            "SourceTarget", "SourceType", "SourceEffectTags", "SourceRecord", "SourceId", "SourceField", "SourceValue" };
        private static void StripText(JToken token)
        {
            if (token is JObject obj)
                foreach (var property in obj.Properties().ToList())
                    if (NonGameplay.Contains(property.Name)) property.Remove(); else StripText(property.Value);
            else if (token is JArray array) foreach (var child in array) StripText(child);
        }

        private static void Error(StudioCompileResult result, string code, string sourceId, string message) =>
            result.Diagnostics.Add(new StudioDiagnostic { Severity = StudioDiagnosticSeverity.Error, Code = code,
                SourceId = sourceId ?? "", Message = message });
    }

    public static class StudioHash
    {
        public static string Compute(string domain, JToken value)
        {
            using (var algorithm = SHA256.Create())
                return domain + ":" + BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(domain + "\n" +
                    Canonical(value).ToString(Formatting.None)))).Replace("-", "").ToLowerInvariant();
        }
        private static JToken Canonical(JToken value)
        {
            if (value is JObject obj) return new JObject(obj.Properties().OrderBy(p => p.Name, StringComparer.Ordinal)
                .Select(p => new JProperty(p.Name, Canonical(p.Value))));
            if (value is JArray array) return new JArray(array.Select(Canonical));
            return value?.DeepClone() ?? JValue.CreateNull();
        }
    }
}
