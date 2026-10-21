using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Combat;
using Newtonsoft.Json;

namespace FightingAllstar.Core.Studio
{
    [Serializable]
    public sealed class DescriptionBranch
    {
        public string Id = "branch." + Guid.NewGuid().ToString("N");
        public string Name = "Configuration wording";
        public int MinimumLevel = 1, MaximumLevel = int.MaxValue;
        public int MinimumRank = 1, MaximumRank = 3;
        public int MinimumTier, MaximumTier = 5;
        // Empty matches all configurations; "base" selects the base definition.
        public string VariantId = "";
        public string Source = "";
        public bool Matches(DescriptionCompileContext c) => c.Level >= MinimumLevel && c.Level <= MaximumLevel &&
            c.Rank >= MinimumRank && c.Rank <= MaximumRank && c.Tier >= MinimumTier && c.Tier <= MaximumTier &&
            (string.IsNullOrEmpty(VariantId) || VariantId == (string.IsNullOrEmpty(c.VariantId) ? "base" : c.VariantId));
    }

    public enum DescriptionBindingField { Coefficient, Magnitude, HitCount, StatusLayers, StatusDuration, HealCoefficient }
    [Serializable]
    public sealed class DescriptionValueBinding
    {
        public string Id = "binding." + Guid.NewGuid().ToString("N");
        public string Name = "Resolved skill value";
        public string SkillId = "";
        public string EffectId = "";
        public DescriptionBindingField Field;
    }

    public sealed class DescriptionUsage
    {
        public string SourceId, BlockId, Locale, Kind;
        public int Start, Length;
        public override string ToString() => SourceId + " / " + Locale + " / " + BlockId + " · " + Kind;
    }

    public static class DescriptionAuthoring
    {
        public static DescriptionLibrary RemapLibrary(DescriptionLibrary source, IReadOnlyDictionary<string, string> ids)
        {
            var copy = JsonConvert.DeserializeObject<DescriptionLibrary>(JsonConvert.SerializeObject(source));
            foreach (var term in copy.Terms)
            {
                if (ids.TryGetValue(term.GameplayDefinitionId ?? "", out var mapped)) term.GameplayDefinitionId = mapped;
                if (term.Tooltip != null) term.Tooltip = DescriptionCompiler.RemapLocalReferences(term.Tooltip, ids);
            }
            foreach (var snippet in copy.Snippets) foreach (var template in snippet.Templates)
                template.Text = DescriptionSourceCodec.RemapParameterReferences(template.Text, ids);
            return copy;
        }
        public static IEnumerable<DescriptionBlock> Blocks(DescriptionDocument d) => (d?.Locales ?? new List<DescriptionLocale>())
            .Where(l => l != null).SelectMany(l => l.Blocks ?? new List<DescriptionBlock>()).Where(b => b != null);
        public static IEnumerable<string> Sources(DescriptionBlock b) => new[] { b.Source }.Concat((b.Branches ?? new List<DescriptionBranch>()).Where(x => x != null).Select(x => x.Source));
        public static string Escape(string value) => (value ?? "").Replace("\\", "\\\\").Replace("|", "\\|").Replace("{", "\\{").Replace("}", "\\}").Replace("<", "\\<").Replace(">", "\\>");

        public static string SnippetToken(DescriptionSnippet snippet, IReadOnlyDictionary<string, string> inputs)
        {
            if (snippet == null) throw new ArgumentNullException(nameof(snippet));
            var token = new StringBuilder("{{snippet:").Append(snippet.Id).Append('@').Append(snippet.Revision);
            foreach (var input in snippet.Inputs)
            {
                if (inputs == null || !inputs.TryGetValue(input.Id, out var value) || string.IsNullOrWhiteSpace(value))
                    throw new InvalidOperationException("Required snippet input: " + input.Id);
                token.Append('|').Append(input.Id).Append('=').Append(Escape(value));
            }
            return token.Append("}}").ToString();
        }

        // Detaches one instance to editable source, retaining semantic value/term references.
        public static string DetachSnippet(string token, DescriptionLibrary library, string locale, string fallback = "en")
        {
            var span = DescriptionSource.Parse(token).Spans.SingleOrDefault(s => s.Kind == DescriptionSpanKind.Snippet && s.Range.Length == token.Length);
            if (span == null) throw new InvalidOperationException("Select exactly one linked snippet instance.");
            var parts = DescriptionParser.Split(span.Text, '|');
            var snippet = library.Snippets.FirstOrDefault(s => s.Id + "@" + s.Revision == parts[0]);
            if (snippet == null) throw new InvalidOperationException("Pinned snippet revision is unavailable.");
            var template = snippet.Templates.FirstOrDefault(t => t.Locale == locale) ?? snippet.Templates.FirstOrDefault(t => t.Locale == fallback);
            if (template == null) throw new InvalidOperationException("Snippet translation and fallback are missing.");
            var values = parts.Skip(1).ToDictionary(p => p.Substring(0, p.IndexOf('=')), p => p.Substring(p.IndexOf('=') + 1));
            var output = new StringBuilder(template.Text);
            foreach (var s in DescriptionSource.Parse(template.Text).Spans.AsEnumerable().Reverse())
            {
                var replacement = template.Text.Substring(s.Range.Start, s.Range.Length);
                foreach (var input in snippet.Inputs)
                {
                    if (!values.TryGetValue(input.Id, out var value)) throw new InvalidOperationException("Unbound snippet input " + input.Id);
                    if (s.Kind == DescriptionSpanKind.TextInput && s.Text == "$" + input.Id) replacement = Escape(value);
                    else if (s.Kind == DescriptionSpanKind.Value || s.Kind == DescriptionSpanKind.Term || s.Kind == DescriptionSpanKind.Snippet)
                    {
                        // Replace full placeholder atoms, not similarly named prefixes.
                        replacement = System.Text.RegularExpressions.Regex.Replace(replacement, @"\$" + System.Text.RegularExpressions.Regex.Escape(input.Id) + @"(?=[|}=])", _ => Escape(value));
                    }
                }
                output.Remove(s.Range.Start, s.Range.Length).Insert(s.Range.Start, replacement);
            }
            return output.ToString();
        }

        public static List<DescriptionUsage> FindUsages(DescriptionDocument document, DescriptionLibrary library, string id)
        {
            var result = new List<DescriptionUsage>();
            void Scan(string source, string owner, string block, string locale)
            {
                foreach (var span in DescriptionSource.Parse(source).Spans)
                {
                    var parts = DescriptionParser.Split(span.Text, '|');
                    if ((span.Kind == DescriptionSpanKind.Term || span.Kind == DescriptionSpanKind.Value || span.Kind == DescriptionSpanKind.Style) && parts[0] == id ||
                        span.Kind == DescriptionSpanKind.Snippet && (parts[0] == id || parts[0].StartsWith(id + "@", StringComparison.Ordinal) || parts.Skip(1).Any(p => p.EndsWith("=" + id, StringComparison.Ordinal))))
                        result.Add(new DescriptionUsage { SourceId = owner, BlockId = block, Locale = locale, Kind = span.Kind.ToString(), Start = span.Range.Start, Length = span.Range.Length });
                }
            }
            foreach (var locale in document.Locales)
            foreach (var block in locale.Blocks)
            foreach (var source in Sources(block)) Scan(source, document.Id, block.Id, locale.Locale);
            foreach (var snippet in library.Snippets)
            foreach (var template in snippet.Templates) Scan(template.Text, snippet.Id + "@" + snippet.Revision, "template", template.Locale);
            foreach (var term in library.Terms)
            foreach (var locale in term.Tooltip?.Locales ?? new List<DescriptionLocale>())
            foreach (var block in locale.Blocks)
            foreach (var source in Sources(block)) Scan(source, term.Id, block.Id, locale.Locale);
            return result;
        }

        public static IEnumerable<string> MechanicIds(StudioDocument d) => d.Rules.Select(x => x.Id).Concat(d.Resources.Select(x => x.Id))
            .Concat(d.Statuses.Select(x => x.Id)).Concat(d.Policies.Select(x => x.Id)).Concat(d.AbilityVariants.Select(x => x.Definition.Id))
            .Concat(d.EditingCharacter.Skills.Select(x => x.Id));

        public static DescriptionCompileContext Context(StudioDocument document, CharacterDefinition character, string locale, int level, int rank, int tier, string variantId)
        {
            if (level < 1 || rank < 1 || rank > 3 || tier < 0 || tier > 5) throw new InvalidOperationException("Use level ≥1, rank 1–3 and tier 0–5.");
            var context = DescriptionCompileContext.FromParameters(document.Parameters, locale);
            if (!string.IsNullOrEmpty(variantId) && character?.Skills.Any(s => s.Variants.Any(v => v.Id == variantId)) != true)
                throw new InvalidOperationException("Unknown authored configuration variant " + variantId);
            context.Level = level; context.Rank = rank; context.Tier = tier; context.VariantId = variantId ?? "";
            context.KnownMechanicIds = MechanicIds(document).Concat(StandardEffectDatabase.CreateStatusRecipes().Select(s => s.Id))
                .Concat(Enum.GetValues(typeof(StatId)).Cast<StatId>().Select(s => "stat." + s)).ToList();
            var parameters = context.Parameters.ToDictionary(x => x.Key, x => x.Value);
            foreach (var binding in document.Description.Bindings ?? new List<DescriptionValueBinding>())
            {
                var skill = character?.Skills.Find(s => s.Id == binding.SkillId);
                // A selected variant applies only to the skill which declares it.
                var selectedVariant = skill?.Variants.Any(v => v.Id == variantId) == true ? variantId : "";
                var resolved = AbilityVariantSystem.ResolveAuthored(skill, level, rank, tier, selectedVariant);
                EffectOperationDefinition effect = string.IsNullOrEmpty(binding.EffectId) || binding.EffectId == resolved.Id ? resolved : resolved.Sequence?.FirstOrDefault(s => s.Effect?.Id == binding.EffectId)?.Effect;
                if (effect == null) throw new InvalidOperationException("Binding " + binding.Id + " references an absent effect in the selected configuration.");
                if ((binding.Field == DescriptionBindingField.Coefficient || binding.Field == DescriptionBindingField.HitCount) && effect.Kind != EffectKind.Damage ||
                    binding.Field == DescriptionBindingField.HealCoefficient && effect.Kind != EffectKind.Heal ||
                    (binding.Field == DescriptionBindingField.StatusLayers || binding.Field == DescriptionBindingField.StatusDuration) && effect.Kind != EffectKind.ApplyStatus)
                    throw new InvalidOperationException("Binding " + binding.Id + " requests a field incompatible with the selected effect kind.");
                decimal value;
                switch (binding.Field)
                {
                    case DescriptionBindingField.Coefficient: value = effect.CoefficientBp; break;
                    case DescriptionBindingField.Magnitude: value = effect.Magnitude; break;
                    case DescriptionBindingField.HitCount: value = effect.Attack?.HitCount ?? 1; break;
                    case DescriptionBindingField.StatusLayers: value = effect.StatusStackCount; break;
                    case DescriptionBindingField.StatusDuration:
                        if (effect.StatusRecipe == null || effect.StatusRecipe.DurationClock == StatusDurationClock.Permanent)
                            throw new InvalidOperationException("A duration binding requires a finite status recipe; use permanent wording for a permanent status.");
                        value = effect.StatusDurationOverride > 0 ? effect.StatusDurationOverride : effect.StatusRecipe.DefaultDuration; break;
                    case DescriptionBindingField.HealCoefficient: value = effect.HealValue?.CoefficientBp ?? effect.HealCoefficientBp; break;
                    default: throw new InvalidOperationException("Unknown description binding field.");
                }
                if (parameters.ContainsKey(binding.Id)) throw new InvalidOperationException("Duplicate description binding identity " + binding.Id);
                parameters.Add(binding.Id, new DescriptionParameterValue { Value = value, Unit = binding.Field == DescriptionBindingField.Coefficient || binding.Field == DescriptionBindingField.HealCoefficient ? DescriptionParameterUnit.BasisPoints : binding.Field == DescriptionBindingField.StatusDuration ? DescriptionParameterUnit.Turns : DescriptionParameterUnit.Integer,
                    Clock = binding.Field == DescriptionBindingField.StatusDuration ? effect.StatusRecipe.DurationClock.ToString() : "" });
            }
            context.Parameters = parameters; return context;
        }

        public static List<DescriptionDiagnostic> ValidateLibrary(DescriptionLibrary library)
        {
            var result = new List<DescriptionDiagnostic>();
            void Issue(string code, string owner, string message) => result.Add(new DescriptionDiagnostic { Code = code, Message = message,
                Severity = DescriptionSeverity.Error, Range = new DescriptionRange { SourceId = owner } });
            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var term in library.Terms)
            {
                foreach (var name in term.Names.Concat(term.Aliases).Where(x => !string.IsNullOrWhiteSpace(x.Text)))
                {
                    var key = name.Locale + ":" + name.Text.Trim();
                    if (aliases.TryGetValue(key, out var owner) && owner != term.Id) Issue("text.ambiguous-alias", term.Id, name.Text + " also identifies " + owner + ". Use explicit references.");
                    else aliases[key] = term.Id;
                }
                if (!string.IsNullOrEmpty(term.StyleId) && !library.Styles.Any(s => s.Id == term.StyleId)) Issue("text.missing-style", term.Id, "Keyword style is missing.");
                // Tooltips are bounded documents opened on demand; cross-links never recursively expand.
                foreach (var block in Blocks(term.Tooltip)) foreach (var source in Sources(block))
                {
                    var parsed = DescriptionSource.Parse(source, block.Id, term.Id); result.AddRange(parsed.Diagnostics);
                    foreach (var span in parsed.Spans.Where(s => s.Kind == DescriptionSpanKind.Term))
                        if (!library.Terms.Any(t => t.Id == span.Text)) Issue("text.tooltip-link", term.Id, "Unknown tooltip keyword " + span.Text);
                }
            }
            var visiting = new HashSet<string>(); var visited = new HashSet<string>();
            void Visit(DescriptionSnippet snippet)
            {
                var key = snippet.Id + "@" + snippet.Revision;
                if (visiting.Contains(key)) { Issue("text.snippet-cycle", key, "Snippet dependency cycle."); return; }
                if (!visited.Add(key)) return;
                visiting.Add(key);
                if (visiting.Count > 16) Issue("text.snippet-depth", key, "Snippet dependency depth exceeds 16.");
                else foreach (var template in snippet.Templates)
                {
                    var parsed = DescriptionSource.Parse(template.Text, "template", key); result.AddRange(parsed.Diagnostics);
                    foreach (var span in parsed.Spans.Where(s => s.Kind == DescriptionSpanKind.Snippet))
                    {
                        var childKey = DescriptionParser.Split(span.Text, '|')[0]; var child = library.Snippets.FirstOrDefault(s => s.Id + "@" + s.Revision == childKey);
                        if (child == null) Issue("text.missing-snippet", key, "Unknown pinned dependency " + childKey); else Visit(child);
                    }
                }
                visiting.Remove(key);
            }
            foreach (var snippet in library.Snippets) Visit(snippet);
            foreach (var snippet in library.Snippets)
            {
                if (snippet.Inputs.Any(i => string.IsNullOrWhiteSpace(i.Id) || !Enum.IsDefined(typeof(DescriptionInputKind), i.Kind)) || snippet.Inputs.Select(i => i.Id).Distinct().Count() != snippet.Inputs.Count)
                    Issue("text.snippet-input-definition", snippet.Id, "Input names must be unique, nonempty and typed.");
                if (snippet.Templates.Select(t => t.Locale).Distinct().Count() != snippet.Templates.Count) Issue("text.duplicate-translation", snippet.Id, "Duplicate template locale.");
                foreach (var template in snippet.Templates)
                foreach (var span in DescriptionSource.Parse(template.Text).Spans)
                {
                    DescriptionInputKind? kind = span.Kind == DescriptionSpanKind.Value ? DescriptionInputKind.ValueParameter : span.Kind == DescriptionSpanKind.Term ? DescriptionInputKind.Term : span.Kind == DescriptionSpanKind.TextInput ? DescriptionInputKind.Text : (DescriptionInputKind?)null;
                    var id = DescriptionParser.Split(span.Text, '|')[0];
                    if (kind.HasValue && id.StartsWith("$", StringComparison.Ordinal) && !snippet.Inputs.Any(i => i.Id == id.Substring(1) && i.Kind == kind.Value))
                        Issue("text.snippet-input-definition", snippet.Id, "Template input " + id + " is undeclared or has the wrong type.");
                }
            }
            return result;
        }

        public static List<DescriptionDiagnostic> ValidateBranches(DescriptionDocument document)
        {
            var result = new List<DescriptionDiagnostic>();
            void Error(DescriptionBlock b, string message) => result.Add(new DescriptionDiagnostic { Code = "text.branch-coverage", Severity = DescriptionSeverity.Error, Message = message, Range = new DescriptionRange { BlockId = b.Id, SourceId = document.Id } });
            foreach (var block in Blocks(document))
            {
                var branches = block.Branches ?? new List<DescriptionBranch>();
                if (branches.Count > 32) { Error(block, "A block supports at most 32 configuration branches."); continue; }
                foreach (var b in branches)
                    if (b == null || b.MinimumLevel < 1 || b.MinimumLevel > b.MaximumLevel || b.MinimumRank < 1 || b.MaximumRank > 3 || b.MinimumRank > b.MaximumRank || b.MinimumTier < 0 || b.MaximumTier > 5 || b.MinimumTier > b.MaximumTier)
                        Error(block, "Invalid branch level/rank/tier range.");
                if (branches.Any(b => b == null)) continue;
                if (branches.Select(b => b.Id).Distinct().Count() != branches.Count || branches.Any(b => string.IsNullOrWhiteSpace(b.Id))) Error(block, "Branches require unique stable IDs.");
                if (block.HasFallback) continue;
                var levels = new HashSet<int> { 1 };
                foreach (var b in branches) { levels.Add(b.MinimumLevel); if (b.MaximumLevel < int.MaxValue) levels.Add(b.MaximumLevel + 1); }
                var variants = branches.Select(b => b.VariantId).Where(v => !string.IsNullOrEmpty(v)).Concat(new[] { "base", "__other__" }).Distinct();
                var uncovered = false;
                foreach (var level in levels.Where(v => v >= 1)) foreach (var variant in variants)
                for (var rank = 1; rank <= 3; rank++) for (var tier = 0; tier <= 5; tier++)
                    if (!branches.Any(b => b.Matches(new DescriptionCompileContext { Level = level, Rank = rank, Tier = tier, VariantId = variant }))) uncovered = true;
                if (uncovered) Error(block, "Configuration coverage is incomplete. Add an explicit fallback or cover every level/rank/tier/variant.");
            }
            return result;
        }
    }
}
