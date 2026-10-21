using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;

namespace CharacterCreatorStudio.Checks
{
    public static class DescriptionChecks
    {
        private static int assertions;
        private static void Check(bool condition, string reason)
        { assertions++; if (!condition) throw new InvalidOperationException("Description: " + reason); }

        public static void Run()
        {
            SourceAndJsonRoundTrip();
            TypedCompilationAndDependencies();
            DiagnosticsAndSafePreview();
            SnippetPinsInputsCyclesAndLimits();
            LocalizationHashingAndRemap();
            Console.WriteLine("PASS: " + assertions + " description assertions (lossless Thai drafts, nested markup, typed bindings, snippets, localization, dependencies and bounded output).");
        }

        private static DescriptionCompileContext Context(string locale = "en") => new DescriptionCompileContext
        {
            Locale = locale,
            Parameters = new Dictionary<string, DescriptionParameterValue>(StringComparer.Ordinal) {
                ["param.opening-gauge"] = new DescriptionParameterValue { Value = 2, Unit = DescriptionParameterUnit.Integer },
                ["param.heal"] = new DescriptionParameterValue { Value = 2000, Unit = DescriptionParameterUnit.BasisPoints },
                ["param.turns"] = new DescriptionParameterValue { Value = 2, Unit = DescriptionParameterUnit.Turns }
            }
        };

        private static DescriptionLibrary Library() => DescriptionLibrary.CreateOpeningGaugeLibrary();
        private static void HasError(DescriptionCompileResult result, string code)
        { Check(!result.Success && result.Diagnostics.Any(x => x.Code == code && x.Severity == DescriptionSeverity.Error), "Expected diagnostic " + code); }

        private static void SourceAndJsonRoundTrip()
        {
            var source = "เมื่อเริ่มต่อสู้ เพิ่ม <b>{{value:param.opening-gauge|integer}}</b> จุด\r\n" +
                "<i>เกจพลัง</i> \\{{literal\\}} \\<b\\> <broken";
            var parsed = DescriptionSource.Parse(source, "paragraph.thai", "description.test");
            Check(parsed.Serialize() == source, "Parser must preserve invalid Thai drafts and exact CRLF/escape text.");
            Check(parsed.Diagnostics.Any(x => x.Range.Start == source.IndexOf("<broken", StringComparison.Ordinal) && x.Range.SourceId == "description.test"), "Invalid source must have a repairable UTF-16 range.");
            var doc = DescriptionDocument.FromSource(source, "th");
            var copy = SnapshotJson.Deserialize<DescriptionDocument>(SnapshotJson.Serialize(doc));
            Check(copy.Locales[0].Blocks[0].Source == source, "JSON source persistence must preserve Thai combining marks and invalid markup.");
            var nested = DescriptionSource.Parse("<b>หนึ่ง <i>สอง</i></b>");
            Check(nested.Diagnostics.Count == 0 && nested.Spans.Single(x => x.Text == "สอง").ParentIndex >= 0, "Flat spans retain nested parent relationships.");
            Check(nested.Spans.Select(x => x.Id).SequenceEqual(DescriptionSource.Parse(nested.Serialize()).Spans.Select(x => x.Id)), "Reparse preserves span identity for unchanged source.");
        }

        private static void TypedCompilationAndDependencies()
        {
            var library = Library();
            var compiled = DescriptionCompiler.Compile(DescriptionLibrary.OpeningGaugeSource, library, Context());
            Check(compiled.Success, string.Join("; ", compiled.Diagnostics.Select(x => x.Message)));
            Check(compiled.PlainText == "At battle start, active allies gain 2 Power Gauge.", "Opening-gauge description must name the active-team recipient and use the shared bound value and keyword.");
            Check(compiled.ToolkitRichText == compiled.TmpRichText && compiled.TmpRichText.Contains("<b>Power Gauge</b>"), "Both adapters use the same balanced portable formatting payload.");
            Check(compiled.Dependencies.Any(x => x.Kind == "parameter" && x.Id == "param.opening-gauge") &&
                compiled.Dependencies.Any(x => x.Kind == "snippet" && x.Revision == 1) && compiled.Dependencies.Any(x => x.Kind == "term"), "Compiled output reports concrete parameter, pinned snippet, and keyword dependencies.");
            var term = compiled.SemanticReferences.Single(x => x.Kind == "term");
            Check(compiled.PlainText.Substring(term.PlainStart, term.PlainLength) == "Power Gauge" && term.Range.SourceId == "snippet:snippet.opening-gauge@1", "Semantic maps link resolved text to exact snippet source.");
            var percent = DescriptionCompiler.Compile("Recover {{value:param.heal|percent}} for {{value:param.turns|turns}} turns.", library, Context());
            Check(percent.Success && percent.PlainText == "Recover 20% for 2 turns.", "2000 basis points must render 20%, with typed turn units.");
            var before = SnapshotJson.Serialize(library);
            DescriptionCompiler.Compile(DescriptionLibrary.OpeningGaugeSource, library, Context());
            Check(SnapshotJson.Serialize(library) == before, "Compilation cannot mutate shared authoring libraries.");
            var overridden = DescriptionCompiler.Compile("<color=#123456>{{term:keyword.power-gauge}}</color>", library, Context());
            Check(overridden.Success && overridden.TmpRichText == "<color=#123456><b>Power Gauge</b></color>", "Explicit span color overrides keyword semantic color without losing emphasis.");
            var adapter = DescriptionCompileContext.FromParameters(new[] { new StudioParameter { Id = "param.opening-gauge", Value = 4, Unit = StudioParameterUnit.Integer } });
            Check(DescriptionCompiler.Compile(DescriptionLibrary.OpeningGaugeSource, library, adapter).PlainText.Contains("gain 4"), "The compiler reads the actual Studio gameplay parameter adapter.");
            var structured = DescriptionDocument.FromSource("<i>Opening</i>");
            structured.Locales[0].Blocks[0].Kind = DescriptionBlockKind.Section;
            structured.Locales[0].Blocks.Add(new DescriptionBlock { Id = "list.first", Kind = DescriptionBlockKind.NumberedItem, Source = "Gain gauge." });
            structured.Locales[0].Blocks.Add(new DescriptionBlock { Id = "list.second", Kind = DescriptionBlockKind.NumberedItem, Source = "Draw a card." });
            var list = DescriptionCompiler.Compile(structured, library, Context());
            Check(list.Success && list.PlainText == "Opening\n\n1. Gain gauge.\n\n2. Draw a card.", "Structured section and ordered-list blocks compile without unsupported HTML list tags.");
        }

        private static void DiagnosticsAndSafePreview()
        {
            HasError(DescriptionCompiler.Compile("{{value:param.heal|integer}}", Library(), Context()), "text.unit-mismatch");
            HasError(DescriptionCompiler.Compile("{{value:param.heal|automatic}}", Library(), Context()), "text.unknown-format");
            HasError(DescriptionCompiler.Compile("{{value:param.unknown|integer}}", Library(), Context()), "text.missing-parameter");
            HasError(DescriptionCompiler.Compile("{{term:keyword.unknown}}", Library(), Context()), "text.missing-term");
            HasError(DescriptionCompiler.Compile("{{text:$unknown}}", Library(), Context()), "text.unbound-input");
            HasError(DescriptionCompiler.Compile("<b><i>text</b></i>", Library(), Context()), "text.tag-nesting");
            HasError(DescriptionCompiler.Compile("<b>unfinished", Library(), Context()), "text.unclosed-tag");
            HasError(DescriptionCompiler.Compile("{{value:param.heal", Library(), Context()), "text.unclosed-token");
            HasError(DescriptionCompiler.Compile("<sprite=0>", Library(), Context()), "text.unsupported-tag");
            var unknown = DescriptionCompiler.Compile("<script>danger</script>", Library(), Context());
            Check(!unknown.Success && unknown.PlainText == "<script>danger</script>" && unknown.TmpRichText.Contains("<noparse><</noparse>script>"), "Unknown markup remains visible but cannot become executable renderer markup.");
            var escaped = DescriptionCompiler.Compile(@"\<b\>literal\</b\> \{{term:fake\}}", Library(), Context());
            Check(escaped.Success && escaped.PlainText == "<b>literal</b> {{term:fake}}", "Escaped brackets and token delimiters remain literal.");
            var immutableSource = "<b>draft";
            var invalid = DescriptionCompiler.Compile(immutableSource, Library(), Context());
            Check(invalid.TmpRichText == "<b>draft</b>" && !invalid.Success, "Invalid drafts get balanced safe previews while remaining ineligible for activation.");
            var library = Library(); library.Styles[0].Color = "red><size=900>";
            HasError(DescriptionCompiler.Compile(DescriptionLibrary.OpeningGaugeSource, library, Context()), "text.style-color");
            library = Library(); library.Terms.Add(library.Terms[0]);
            HasError(DescriptionCompiler.Compile(DescriptionLibrary.OpeningGaugeSource, library, Context()), "text.duplicate-id");
        }

        private static void SnippetPinsInputsCyclesAndLimits()
        {
            HasError(DescriptionCompiler.Compile("{{snippet:snippet.opening-gauge|amount=param.opening-gauge}}", Library(), Context()), "text.unpinned-snippet");
            HasError(DescriptionCompiler.Compile("{{snippet:snippet.opening-gauge@2}}", Library(), Context()), "text.missing-snippet");
            HasError(DescriptionCompiler.Compile("{{snippet:snippet.opening-gauge@1}}", Library(), Context()), "text.missing-input");
            HasError(DescriptionCompiler.Compile(DescriptionLibrary.OpeningGaugeSource.Replace("|amount=", "|unknown="), Library(), Context()), "text.unknown-input");
            var library = Library();
            library.Snippets[0].Templates[0].Text = "{{term:$amount}}";
            HasError(DescriptionCompiler.Compile(DescriptionLibrary.OpeningGaugeSource, library, Context()), "text.input-type");
            library = Library();
            library.Snippets.Add(new DescriptionSnippet { Id = "snippet.a", Templates = new List<DescriptionLocalizedText> {
                new DescriptionLocalizedText { Locale = "en", Text = "{{snippet:snippet.b@1}}" } } });
            library.Snippets.Add(new DescriptionSnippet { Id = "snippet.b", Templates = new List<DescriptionLocalizedText> {
                new DescriptionLocalizedText { Locale = "en", Text = "{{snippet:snippet.a@1}}" } } });
            var cycle = DescriptionCompiler.Compile("{{snippet:snippet.a@1}}", library, Context());
            HasError(cycle, "text.snippet-cycle");
            Check(cycle.Diagnostics.Any(x => x.Range.SourceId == "snippet:snippet.b@1"), "Cycle errors identify the closing source reference.");
            var context = Context(); context.MaxSnippetDepth = 1;
            HasError(DescriptionCompiler.Compile("{{snippet:snippet.a@1}}", library, context), "text.snippet-depth");
            context = Context(); context.MaxExpandedCharacters = 10;
            var bounded = DescriptionCompiler.Compile(DescriptionLibrary.OpeningGaugeSource, Library(), context);
            HasError(bounded, "text.expansion-limit");
            Check(bounded.PlainText.Length <= 10, "Expansion limits must bound generated plain output.");
            library = Library();
            library.Snippets.Add(new DescriptionSnippet { Id = "snippet.literal", Inputs = new List<DescriptionSnippetInput> {
                new DescriptionSnippetInput { Id = "content", Kind = DescriptionInputKind.Text } },
                Templates = new List<DescriptionLocalizedText> { new DescriptionLocalizedText { Locale = "en", Text = "{{text:$content}}" } } });
            var literal = DescriptionCompiler.Compile(@"{{snippet:snippet.literal@1|content=<b>literal</b>\|tail}}", library, Context());
            Check(literal.Success && literal.PlainText == "<b>literal</b>|tail", "Typed text snippet inputs are literal and preserve escaped pipe delimiters.");
        }

        private static void LocalizationHashingAndRemap()
        {
            var library = Library();
            var thai = DescriptionCompiler.Compile(DescriptionLibrary.OpeningGaugeSource, library, Context("th"));
            Check(thai.Success && thai.PlainText == "เมื่อเริ่มต่อสู้ เพิ่มเกจพลัง 2 จุดให้พันธมิตรที่อยู่ในสนามและยังมีชีวิต", "Thai templates name living active allies and reorder semantic references without English fragment concatenation.");
            var french = DescriptionCompiler.Compile(DescriptionLibrary.OpeningGaugeSource, library, Context("fr"));
            Check(french.Success && french.Diagnostics.Any(x => x.Code == "text.locale-fallback"), "Explicit translation fallback remains visible.");
            var documentFallback = DescriptionCompiler.Compile(DescriptionDocument.FromSource(DescriptionLibrary.OpeningGaugeSource), library, Context("th"));
            Check(documentFallback.Success && documentFallback.PlainText == "At battle start, active allies gain 2 Power Gauge.", "Document fallback must resolve all dependent wording in its effective locale.");
            var source = DescriptionLibrary.OpeningGaugeSource;
            var first = DescriptionCompiler.Compile(source, library, Context());
            Check(first.TextHash == DescriptionCompiler.Compile(source, library, Context()).TextHash, "Identical descriptions compile deterministically.");
            var reviewed = DescriptionDocument.FromSource(source); reviewed.Locales[0].ReviewState = "Approved";
            library.Styles[0].Name = "Renamed editor palette label";
            Check(first.TextHash == DescriptionCompiler.Compile(reviewed, library, Context()).TextHash, "Review state and editor palette labels must not change compiled text identity.");
            library.Terms[0].Names[0].Text = "Ultimate Gauge";
            var renamed = DescriptionCompiler.Compile(source, library, Context());
            Check(first.TextHash != renamed.TextHash && renamed.PlainText.Contains("Ultimate Gauge"), "Keyword renaming updates output/hash without changing identity.");
            library = Library();
            library.Snippets.Add(new DescriptionSnippet { Id = "unused", Revision = 1 });
            Check(first.TextHash == DescriptionCompiler.Compile(source, library, Context()).TextHash, "Unused library entries do not invalidate a dependency-scoped text hash.");
            var doc = DescriptionDocument.FromSource(source + " {{value:param.opening-gauge|integer}} \\{{value:param.opening-gauge|integer\\}}");
            doc.OwnerId = "character.old";
            doc.Locales[0].Blocks[0].MechanicIds.Add("rule.old");
            var before = SnapshotJson.Serialize(doc);
            var copy = DescriptionCompiler.RemapLocalReferences(doc, new Dictionary<string, string> {
                ["param.opening-gauge"] = "param.copy", ["character.old"] = "character.copy", ["rule.old"] = "rule.copy" });
            Check(SnapshotJson.Serialize(doc) == before && copy.Id != doc.Id && copy.Locales[0].Blocks[0].Id != doc.Locales[0].Blocks[0].Id, "Duplication must preserve the original and remap description/block identity.");
            var block = copy.Locales[0].Blocks[0];
            Check(copy.OwnerId == "character.copy" && block.MechanicIds[0] == "rule.copy" &&
                block.Source.Contains("amount=param.copy") && block.Source.Contains("{{value:param.copy|integer}}") &&
                block.Source.Contains("resource=keyword.power-gauge") && block.Source.Contains("\\{{value:param.opening-gauge|integer\\}}"), "Duplication remaps local parameter/rule references, preserving escaped prose and external library identity.");
            var escapedInput = @"{{snippet:snippet.literal@1|amount=param.opening-gauge|label=a\|b}}";
            Check(DescriptionSourceCodec.RemapParameterReferences(escapedInput, new Dictionary<string, string> { ["param.opening-gauge"] = "param.copy" }) ==
                @"{{snippet:snippet.literal@1|amount=param.copy|label=a\|b}}", "Reference remapping preserves escaped unrelated snippet inputs byte for byte.");
            var unsupported = DescriptionDocument.FromSource(source); unsupported.SchemaVersion = 99;
            HasError(DescriptionCompiler.Compile(unsupported, Library(), Context()), "text.schema");
        }
    }
}
