using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace FightingAllstar.Core.Studio
{
    public static class DescriptionCompiler
    {
        public static DescriptionDocument RemapLocalReferences(DescriptionDocument document, IReadOnlyDictionary<string, string> ids)
        {
            if (document == null) return null;
            var copy = JsonConvert.DeserializeObject<DescriptionDocument>(JsonConvert.SerializeObject(document));
            var mapping = ids == null ? new Dictionary<string, string>(StringComparer.Ordinal) : ids.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            foreach (var binding in copy.Bindings ?? new List<DescriptionValueBinding>())
                if (!mapping.ContainsKey(binding.Id)) mapping.Add(binding.Id, "binding." + Guid.NewGuid().ToString("N"));
            foreach (var branch in DescriptionAuthoring.Blocks(copy).SelectMany(b => b.Branches ?? new List<DescriptionBranch>()))
                if (!mapping.ContainsKey(branch.Id)) mapping.Add(branch.Id, "branch." + Guid.NewGuid().ToString("N"));
            if (!string.IsNullOrEmpty(copy.Id) && !mapping.ContainsKey(copy.Id)) mapping.Add(copy.Id, "description." + Guid.NewGuid().ToString("N"));
            foreach (var locale in (copy.Locales ?? new List<DescriptionLocale>()).Where(x => x != null))
            foreach (var block in (locale.Blocks ?? new List<DescriptionBlock>()).Where(x => x != null))
                if (!string.IsNullOrEmpty(block.Id) && !mapping.ContainsKey(block.Id)) mapping.Add(block.Id, "paragraph." + Guid.NewGuid().ToString("N"));
            DescriptionSourceCodec.RemapLocalReferences(copy, mapping);
            return copy;
        }

        public static DescriptionCompileResult Compile(string source, DescriptionLibrary library, DescriptionCompileContext context = null) =>
            Compile(DescriptionDocument.FromSource(source, context?.Locale ?? "en"), library, context);

        public static DescriptionCompileResult Compile(DescriptionDocument document, DescriptionLibrary library, DescriptionCompileContext context = null) =>
            new Compilation(document, library ?? new DescriptionLibrary(), context ?? new DescriptionCompileContext()).Run();

        private sealed class Compilation
        {
            private readonly DescriptionDocument document;
            private readonly DescriptionLibrary library;
            private readonly DescriptionCompileContext context;
            private readonly DescriptionCompileResult result = new DescriptionCompileResult();
            private readonly StringBuilder rich = new StringBuilder();
            private readonly StringBuilder plain = new StringBuilder();
            private readonly SortedDictionary<string, string> dependencyContent = new SortedDictionary<string, string>(StringComparer.Ordinal);
            private readonly HashSet<string> reportedFallbacks = new HashSet<string>(StringComparer.Ordinal);
            private readonly List<string> snippetPath = new List<string>();
            private bool sizeReported;
            private long expandedSourceCharacters;
            private string effectiveLocale;

            public Compilation(DescriptionDocument document, DescriptionLibrary library, DescriptionCompileContext context)
            { this.document = document; this.library = library; this.context = context; }

            public DescriptionCompileResult Run()
            {
                var rootRange = new DescriptionRange { SourceId = document?.Id ?? "" };
                if (document == null) { Error("text.no-document", "A description document is required.", rootRange); return Finish(); }
                if (document.SchemaVersion != 1) Error("text.schema", "Unsupported description schema; preserve this source and migrate explicitly.", rootRange);
                if (library.SchemaVersion != 1) Error("text.library-schema", "Unsupported description library schema.", rootRange);
                ValidateIds(library.Terms, x => x?.Id, "keyword", rootRange);
                ValidateIds(library.Styles, x => x?.Id, "style", rootRange);
                ValidateIds(library.Snippets, x => x == null ? null : x.Id + "@" + x.Revision, "snippet revision", rootRange);
                foreach (var snippet in (library.Snippets ?? new List<DescriptionSnippet>()).Where(x => x != null))
                    if (string.IsNullOrWhiteSpace(snippet.Id) || snippet.Revision <= 0)
                        Error("text.snippet-revision", "A snippet requires a stable ID and positive revision.", rootRange);
                foreach (var term in (library.Terms ?? new List<DescriptionTerm>()).Where(x => x != null))
                    if (term.Revision <= 0) Error("text.term-revision", "A keyword revision must be positive.", rootRange);
                ValidateIds(document.Locales, x => x?.Locale, "locale", rootRange);
                foreach (var style in library.Styles ?? new List<DescriptionStyle>())
                    if (style != null && !DescriptionParser.IsColor(style.Color))
                        Error("text.style-color", "Style " + style.Id + " requires a six- or eight-digit hex color.", rootRange);
                result.Diagnostics.AddRange(DescriptionAuthoring.ValidateLibrary(library));
                if (context.KnownMechanicIds != null)
                    foreach (var term in library.Terms.Where(t => !string.IsNullOrEmpty(t.GameplayDefinitionId)))
                        if (!context.KnownMechanicIds.Contains(term.GameplayDefinitionId)) Error("text.gameplay-link", "Keyword " + term.Id + " links to absent gameplay definition " + term.GameplayDefinitionId, new DescriptionRange { SourceId = term.Id });
                result.Diagnostics.AddRange(DescriptionAuthoring.ValidateBranches(document));
                foreach (var hidden in DescriptionAuthoring.Blocks(document).SelectMany(b => b.Branches ?? new List<DescriptionBranch>()).Where(b => b != null))
                {
                    var check = new Compilation(DescriptionDocument.FromSource(hidden.Source, context.Locale), library, context).Run();
                    foreach (var diagnostic in check.Diagnostics.Where(d => d.Severity == DescriptionSeverity.Error))
                    { diagnostic.Range.BlockId = DescriptionAuthoring.Blocks(document).First(b => b.Branches.Contains(hidden)).Id; diagnostic.Range.BranchId = hidden.Id; diagnostic.Range.SourceId = document.Id; result.Diagnostics.Add(diagnostic); }
                }
                var locales = document.Locales ?? new List<DescriptionLocale>();
                var selected = locales.FirstOrDefault(x => x != null && x.Locale == context.Locale);
                if (selected == null)
                {
                    selected = locales.FirstOrDefault(x => x != null && x.Locale == document.DefaultLocale);
                    if (selected != null) Fallback("document:" + document.Id, context.Locale, selected.Locale, rootRange);
                }
                if (selected == null) { Error("text.missing-locale", "No requested locale or explicit default locale exists.", rootRange); return Finish(); }
                effectiveLocale = selected.Locale;
                ValidateIds(selected.Blocks, x => x?.Id, "block", rootRange);
                var index = 0;
                var listIndex = 0;
                foreach (var block in selected.Blocks ?? new List<DescriptionBlock>())
                {
                    if (block == null) continue;
                    if (index > 0) { rich.Append("\n\n"); plain.Append("\n\n"); }
                    index++;
                    var range = new DescriptionRange { BlockId = block.Id, SourceId = document.Id };
                    if (!Enum.IsDefined(typeof(DescriptionBlockKind), block.Kind)) Error("text.block-kind", "Unknown block kind.", range);
                    listIndex = block.Kind == DescriptionBlockKind.NumberedItem ? listIndex + 1 : 0;
                    var prefix = block.Kind == DescriptionBlockKind.Bullet ? "• " : block.Kind == DescriptionBlockKind.NumberedItem ? listIndex + ". " : "";
                    Literal(prefix, range);
                    var closers = OpenStyle(block.StyleId, range, false);
                    if (block.Kind == DescriptionBlockKind.Section) rich.Append("<b>");
                    var matches = (block.Branches ?? new List<DescriptionBranch>()).Where(b => b != null && b.Matches(context)).ToList();
                    if (matches.Count > 1) result.Diagnostics.Add(new DescriptionDiagnostic { Code = "text.branch-overlap", Severity = DescriptionSeverity.Warning,
                        Message = "Overlapping branches; first match wins: " + matches[0].Name, Range = range });
                    if (matches.Count == 0 && !block.HasFallback) Error("text.branch-unmatched", "No matching wording or fallback.", range);
                    if (context.KnownMechanicIds != null)
                        foreach (var id in block.MechanicIds ?? new List<string>()) if (!context.KnownMechanicIds.Contains(id)) Error("text.mechanic-link", "Unknown mechanic " + id, range);
                    CompileSource(matches.FirstOrDefault()?.Source ?? block.Source, block.Id, document.Id, null, false);
                    if (block.Kind == DescriptionBlockKind.Section) rich.Append("</b>");
                    rich.Append(closers);
                }
                return Finish();
            }

            private DescriptionCompileResult Finish()
            {
                result.PlainText = plain.ToString();
                // Both adapters intentionally share only this portable subset. The UI renders actual Label/TMP consumers.
                result.ToolkitRichText = rich.ToString();
                result.TmpRichText = rich.ToString();
                var identity = new StringBuilder();
                // Review workflow state belongs to the draft, not to the compiled text identity.
                HashPart(identity, JsonConvert.SerializeObject(document == null ? null : new {
                    document.SchemaVersion, document.Id, document.OwnerId, document.DefaultLocale,
                    document.Bindings, Locales = (document.Locales ?? new List<DescriptionLocale>()).Where(x => x != null).Select(locale => new {
                        locale.Locale, Blocks = locale.Blocks }) }));
                HashPart(identity, context.Locale);
                HashPart(identity, context.FallbackLocale);
                if (DescriptionAuthoring.Blocks(document).Any(b => b.Branches?.Count > 0) || document?.Bindings?.Count > 0)
                    HashPart(identity, context.Level + ":" + context.Rank + ":" + context.Tier + ":" + context.VariantId);
                foreach (var item in dependencyContent) { HashPart(identity, item.Key); HashPart(identity, item.Value); }
                using (var sha = SHA256.Create()) result.TextHash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity.ToString()))).Replace("-", "").ToLowerInvariant();
                return result;
            }

            private void CompileSource(string source, string blockId, string sourceId, Dictionary<string, BoundInput> inputs, bool inheritedExplicitColor)
            {
                if (plain.Length >= CharacterLimit) { ReportSize(new DescriptionRange { BlockId = blockId, SourceId = sourceId }); return; }
                expandedSourceCharacters += (source ?? "").Length;
                if (expandedSourceCharacters > Math.Max(2048L, CharacterLimit * 16L))
                { ReportSize(new DescriptionRange { BlockId = blockId, SourceId = sourceId }); return; }
                var parsed = DescriptionSource.Parse(source, blockId, sourceId);
                result.Diagnostics.AddRange(parsed.Diagnostics);
                var tags = new Stack<string>();
                foreach (var span in parsed.Spans)
                {
                    if (plain.Length >= CharacterLimit) { ReportSize(span.Range); break; }
                    var start = plain.Length;
                    var explicitColor = inheritedExplicitColor || tags.Any(t => DescriptionParser.TagName(t) == "color");
                    switch (span.Kind)
                    {
                        case DescriptionSpanKind.Literal: Literal(span.Text, span.Range); break;
                        case DescriptionSpanKind.Invalid: Literal(parsed.Source.Substring(span.Range.Start, span.Range.Length), span.Range); break;
                        case DescriptionSpanKind.OpenTag:
                            tags.Push(span.Text.ToLowerInvariant()); rich.Append('<').Append(span.Text.ToLowerInvariant()).Append('>'); break;
                        case DescriptionSpanKind.CloseTag:
                            if (tags.Count > 0) tags.Pop(); rich.Append('<').Append(span.Text.ToLowerInvariant()).Append('>'); break;
                        case DescriptionSpanKind.Term: Term(span.Text, span.Range, inputs, explicitColor); break;
                        case DescriptionSpanKind.Value: Value(span.Text, span.Range, inputs, explicitColor); break;
                        case DescriptionSpanKind.Snippet: Snippet(span.Text, span.Range, inputs, explicitColor); break;
                        case DescriptionSpanKind.TextInput: TextInput(span.Text, span.Range, inputs); break;
                        case DescriptionSpanKind.Style: StyledLiteral(span.Text, span.Range, explicitColor); break;
                    }
                    result.SourceMap.Add(new DescriptionSourceMapping { Range = span.Range, PlainStart = start, PlainLength = plain.Length - start });
                }
                while (tags.Count > 0) rich.Append("</").Append(DescriptionParser.TagName(tags.Pop())).Append('>');
            }

            private void Term(string token, DescriptionRange range, Dictionary<string, BoundInput> inputs, bool explicitColor)
            {
                var id = ResolveInput(token, DescriptionInputKind.Term, inputs, range);
                if (id == null) return;
                var entry = (library.Terms ?? new List<DescriptionTerm>()).FirstOrDefault(x => x != null && x.Id == id);
                if (entry == null) { Error("text.missing-term", "Unknown keyword " + id + ".", range); Placeholder(id, range); return; }
                Depend("term", id, entry.Revision, entry);
                if (entry.Deprecated) result.Diagnostics.Add(new DescriptionDiagnostic { Code = "text.deprecated-term", Severity = DescriptionSeverity.Warning, Message = "Keyword " + id + " is deprecated; migrate its consumers explicitly.", Range = range });
                var label = Localized(entry.Names, "term:" + id, range);
                var start = plain.Length;
                var closers = OpenStyle(entry.StyleId, range, explicitColor);
                Literal(label ?? "[" + id + "]", range);
                rich.Append(closers);
                Reference("term", id, range, start);
            }

            private void Value(string token, DescriptionRange range, Dictionary<string, BoundInput> inputs, bool explicitColor)
            {
                var parts = DescriptionParser.Split(token, '|');
                if (parts.Count > 2) { Error("text.value-syntax", "Use {{value:parameter-id|integer/percent/number/turns}}.", range); return; }
                var id = ResolveInput(parts[0], DescriptionInputKind.ValueParameter, inputs, range);
                if (id == null) return;
                if (context.Parameters == null || !context.Parameters.TryGetValue(id, out var parameter) || parameter == null)
                { Error("text.missing-parameter", "Unknown parameter " + id + ".", range); Placeholder(id, range); return; }
                var format = parts.Count == 2 ? parts[1] : "integer";
                var unit = format == "integer" ? DescriptionParameterUnit.Integer : format == "percent" ? DescriptionParameterUnit.BasisPoints :
                    format == "number" ? DescriptionParameterUnit.Scalar : DescriptionParameterUnit.Turns;
                if (format != "integer" && format != "percent" && format != "number" && format != "turns")
                { Error("text.unknown-format", "Unknown value format " + format + ".", range); return; }
                if (parameter.Unit != unit)
                { Error("text.unit-mismatch", "Parameter " + id + " uses " + parameter.Unit + "; " + format + " expects " + unit + ".", range); Placeholder(id, range); return; }
                if ((unit == DescriptionParameterUnit.Integer || unit == DescriptionParameterUnit.Turns || unit == DescriptionParameterUnit.BasisPoints) && decimal.Truncate(parameter.Value) != parameter.Value)
                { Error("text.nonintegral-value", "Integer-based parameter " + id + " must not contain a fraction.", range); return; }
                Depend("parameter", id, 0, parameter);
                var value = unit == DescriptionParameterUnit.BasisPoints ? parameter.Value / 100m : parameter.Value;
                var rendered = value.ToString("0.############################", CultureInfo.InvariantCulture) + (unit == DescriptionParameterUnit.BasisPoints ? "%" : "");
                var start = plain.Length;
                var closers = OpenStyle((library.Styles ?? new List<DescriptionStyle>()).Any(x => x?.Id == "style.value") ? "style.value" : "", range, explicitColor);
                Literal(rendered, range); rich.Append(closers);
                Reference("parameter", id, range, start);
            }

            private void Snippet(string token, DescriptionRange range, Dictionary<string, BoundInput> parentInputs, bool explicitColor)
            {
                var parts = DescriptionParser.Split(token, '|');
                var at = parts[0].LastIndexOf('@');
                if (at <= 0 || !int.TryParse(parts[0].Substring(at + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var revision) || revision <= 0)
                { Error("text.unpinned-snippet", "A snippet must pin a positive explicit revision: snippet.id@1.", range); return; }
                var id = parts[0].Substring(0, at);
                var entry = (library.Snippets ?? new List<DescriptionSnippet>()).FirstOrDefault(x => x != null && x.Id == id && x.Revision == revision);
                if (entry == null) { Error("text.missing-snippet", "Unknown pinned snippet " + parts[0] + ".", range); Placeholder(parts[0], range); return; }
                if (snippetPath.Contains(parts[0])) { Error("text.snippet-cycle", "Snippet expansion cycle: " + string.Join(" → ", snippetPath.Concat(new[] { parts[0] })) + ".", range); return; }
                if (snippetPath.Count >= Math.Max(1, Math.Min(64, context.MaxSnippetDepth)))
                { Error("text.snippet-depth", "Snippet expansion exceeded the configured depth limit.", range); return; }
                ValidateIds(entry.Inputs, x => x?.Id, "snippet input", range);
                var declared = entry.Inputs ?? new List<DescriptionSnippetInput>();
                var inputs = new Dictionary<string, BoundInput>(StringComparer.Ordinal);
                for (var i = 1; i < parts.Count; i++)
                {
                    var equals = parts[i].IndexOf('=');
                    if (equals <= 0) { Error("text.snippet-input-syntax", "Snippet inputs use name=value.", range); continue; }
                    var name = parts[i].Substring(0, equals);
                    var definition = declared.FirstOrDefault(x => x != null && x.Id == name);
                    if (definition == null) { Error("text.unknown-input", "Snippet " + id + " has no input " + name + ".", range); continue; }
                    var value = ResolveInput(parts[i].Substring(equals + 1), definition.Kind, parentInputs, range);
                    if (inputs.ContainsKey(name)) Error("text.duplicate-input", "Input " + name + " is supplied more than once.", range);
                    else if (value != null) inputs.Add(name, new BoundInput { Kind = definition.Kind, Value = value });
                }
                foreach (var expected in declared.Where(x => x != null))
                {
                    if (string.IsNullOrWhiteSpace(expected.Id)) continue;
                    if (!Enum.IsDefined(typeof(DescriptionInputKind), expected.Kind)) Error("text.input-kind", "Unknown snippet input kind.", range);
                    if (!inputs.ContainsKey(expected.Id)) Error("text.missing-input", "Snippet " + id + " requires " + expected.Id + ".", range);
                    else ValidateInputReference(inputs[expected.Id], range);
                }
                Depend("snippet", id, revision, entry);
                var template = Localized(entry.Templates, "snippet:" + parts[0], range);
                if (template == null) return;
                var start = plain.Length;
                snippetPath.Add(parts[0]);
                CompileSource(template, range.BlockId, "snippet:" + parts[0], inputs, explicitColor);
                snippetPath.RemoveAt(snippetPath.Count - 1);
                Reference("snippet", parts[0], range, start);
            }

            private void TextInput(string token, DescriptionRange range, Dictionary<string, BoundInput> inputs)
            {
                if (!token.StartsWith("$", StringComparison.Ordinal)) { Error("text.text-input", "Text inputs must refer to a typed snippet input: {{text:$name}}.", range); return; }
                var value = ResolveInput(token, DescriptionInputKind.Text, inputs, range);
                if (value != null) Literal(value, range);
            }

            private void StyledLiteral(string token, DescriptionRange range, bool explicitColor)
            {
                var parts = DescriptionParser.Split(token, '|');
                if (parts.Count != 2) { Error("text.style-syntax", "Use {{style:style.id|literal text}}. Nestable markup uses b/i/color tags.", range); return; }
                var closers = OpenStyle(parts[0], range, explicitColor);
                Literal(parts[1], range); rich.Append(closers);
            }

            private string ResolveInput(string value, DescriptionInputKind expected, Dictionary<string, BoundInput> inputs, DescriptionRange range)
            {
                if (!value.StartsWith("$", StringComparison.Ordinal)) return value;
                var name = value.Substring(1);
                if (inputs == null || !inputs.TryGetValue(name, out var bound)) { Error("text.unbound-input", "Unbound snippet input " + name + ".", range); return null; }
                if (bound.Kind != expected) { Error("text.input-type", "Input " + name + " is " + bound.Kind + "; this token requires " + expected + ".", range); return null; }
                return bound.Value;
            }

            private void ValidateInputReference(BoundInput input, DescriptionRange range)
            {
                if (input.Kind == DescriptionInputKind.ValueParameter && (context.Parameters == null || !context.Parameters.ContainsKey(input.Value)))
                    Error("text.missing-parameter", "Unknown parameter " + input.Value + ".", range);
                if (input.Kind == DescriptionInputKind.Term && !(library.Terms ?? new List<DescriptionTerm>()).Any(x => x?.Id == input.Value))
                    Error("text.missing-term", "Unknown keyword " + input.Value + ".", range);
            }

            private string OpenStyle(string id, DescriptionRange range, bool explicitColor)
            {
                if (string.IsNullOrEmpty(id)) return "";
                var style = (library.Styles ?? new List<DescriptionStyle>()).FirstOrDefault(x => x?.Id == id);
                if (style == null) { Error("text.missing-style", "Unknown semantic style " + id + ".", range); return ""; }
                Depend("style", id, style.Revision, style);
                if (!DescriptionParser.IsColor(style.Color)) return "";
                var closing = "";
                if (!explicitColor) { rich.Append("<color=").Append(style.Color).Append('>'); closing = "</color>"; }
                if (style.Bold) { rich.Append("<b>"); closing = "</b>" + closing; }
                if (style.Italic) { rich.Append("<i>"); closing = "</i>" + closing; }
                return closing;
            }

            private string Localized(List<DescriptionLocalizedText> entries, string owner, DescriptionRange range)
            {
                ValidateIds(entries, x => x?.Locale, "localized entry in " + owner, range);
                var requested = effectiveLocale ?? context.Locale;
                var selected = entries?.FirstOrDefault(x => x != null && x.Locale == requested);
                if (selected == null)
                {
                    selected = entries?.FirstOrDefault(x => x != null && x.Locale == context.FallbackLocale);
                    if (selected != null) Fallback(owner, requested, selected.Locale, range);
                }
                if (selected == null) { Error("text.missing-translation", owner + " has no " + requested + " or explicit " + context.FallbackLocale + " fallback.", range); return null; }
                return selected.Text ?? "";
            }

            private void Literal(string value, DescriptionRange range)
            {
                value = value ?? "";
                if (plain.Length + value.Length > CharacterLimit) { value = value.Substring(0, Math.Max(0, CharacterLimit - plain.Length)); ReportSize(range); }
                plain.Append(value);
                // Escaping per character prevents even an authored </noparse> sequence from closing a safety span.
                foreach (var c in value) rich.Append(c == '<' ? "<noparse><</noparse>" : c.ToString());
            }

            private int CharacterLimit => Math.Max(1, Math.Min(1048576, context.MaxExpandedCharacters));
            private void ReportSize(DescriptionRange range) { if (!sizeReported) Error("text.expansion-limit", "Description exceeds the bounded expanded-text limit.", range); sizeReported = true; }
            private void Placeholder(string id, DescriptionRange range) => Literal("[" + id + "]", range);
            private void Error(string code, string message, DescriptionRange range) => result.Diagnostics.Add(new DescriptionDiagnostic { Code = code, Message = message, Range = range, Severity = DescriptionSeverity.Error });
            private void Fallback(string owner, string requested, string actual, DescriptionRange range)
            {
                if (reportedFallbacks.Add(owner)) result.Diagnostics.Add(new DescriptionDiagnostic { Code = "text.locale-fallback", Message = owner + " uses " + actual + " because " + requested + " is absent.", Range = range, Severity = DescriptionSeverity.Warning });
            }
            private void Reference(string kind, string id, DescriptionRange range, int start) => result.SemanticReferences.Add(new DescriptionSemanticReference { Kind = kind, Id = id, Range = range, PlainStart = start, PlainLength = plain.Length - start });
            private void Depend(string kind, string id, int revision, object content)
            {
                var key = kind + ":" + id + "@" + revision;
                if (dependencyContent.ContainsKey(key)) return;
                dependencyContent.Add(key, JsonConvert.SerializeObject(content is DescriptionStyle style ?
                    (object)new { style.Id, style.Revision, style.Color, style.Bold, style.Italic } : content));
                result.Dependencies.Add(new DescriptionDependency { Kind = kind, Id = id, Revision = revision });
            }
            private void ValidateIds<T>(IEnumerable<T> entries, Func<T, string> id, string kind, DescriptionRange range)
            {
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in entries ?? Enumerable.Empty<T>())
                {
                    var value = id(entry);
                    if (string.IsNullOrWhiteSpace(value)) Error("text.missing-id", "A " + kind + " requires a stable ID.", range);
                    else if (!seen.Add(value)) Error("text.duplicate-id", "Duplicate " + kind + " ID " + value + ".", range);
                }
            }
            private static void HashPart(StringBuilder builder, string value) { value = value ?? ""; builder.Append(value.Length).Append(':').Append(value); }
            private sealed class BoundInput { public DescriptionInputKind Kind; public string Value; }
        }
    }
}
