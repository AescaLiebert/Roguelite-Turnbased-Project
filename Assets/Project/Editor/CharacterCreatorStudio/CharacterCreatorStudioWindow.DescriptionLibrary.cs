#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public sealed partial class CharacterCreatorStudioWindow
    {
        private void DescriptionArea(VisualElement parent, string label, string value, Action<string> setter)
        { var f = new TextField(label) { value = value ?? "", multiline = true }; f.RegisterValueChangedCallback(e => Edit("Edit " + label, () => setter(e.newValue))); parent.Add(f); }
        private void UsageList(VisualElement parent, string id)
        {
            var usages = DescriptionAuthoring.FindUsages(_document.Description, _library, id);
            Body(parent, usages.Count + " uses in this draft and its library", "studio-code");
            foreach (var usage in usages.Take(30))
            { var u = usage; Button(parent, u.ToString(), () => { if (u.SourceId == _document.Description.Id) { _locale = u.Locale; _blockId = u.BlockId; Rebuild(); } else SetStatus(u.ToString()); }); }
        }
        private void BuildKeywordLibrary()
        {
            Section(_inspector, "KEYWORD DATABASE");
            Body(_inspector, "References describe mechanics; inserting a keyword adds no gameplay behavior.", "studio-subtle");
            Button(_inspector, "+ Keyword", () => Edit("Create keyword", () => _library.Terms.Add(new DescriptionTerm { Id = "keyword." + Guid.NewGuid().ToString("N"), Names = new List<DescriptionLocalizedText> { new DescriptionLocalizedText { Locale = _locale, Text = "New keyword" } } }), true));
            Button(_inspector, "Import project vocabulary", () => Edit("Import project wording catalog", SeedProjectVocabulary, true));
            if (!string.IsNullOrEmpty(_openGlossary))
            {
                var term = _library.Terms.FirstOrDefault(t => t.Id == _openGlossary);
                if (term != null)
                {
                    var viewer = Node(_inspector, "GLOSSARY · " + Localized(term.Names, _locale));
                    Body(viewer, Localized(term.Definitions, _locale));
                    if (term.Tooltip != null)
                    {
                        var result = DescriptionCompiler.Compile(term.Tooltip, _library, DescriptionContext());
                        var label = Body(viewer, result.ToolkitRichText, "studio-preview"); label.enableRichText = true;
                        foreach (var reference in result.SemanticReferences.Where(r => r.Kind == "term").Select(r => r.Id).Distinct())
                        { var id = reference; Button(viewer, "Open " + Localized(_library.Terms.First(t => t.Id == id).Names, _locale), () => OpenGlossary(id)); }
                    }
                    Button(viewer, "Back", () => { _openGlossary = _glossaryHistory.Count > 0 ? _glossaryHistory.Pop() : null; RebuildDescriptionInspector(); });
                    Button(viewer, "Close glossary", () => { _openGlossary = null; _glossaryHistory.Clear(); RebuildDescriptionInspector(); });
                }
            }
            foreach (var term in _library.Terms.Where(t => LibraryMatches(t.Id, t.Category, Localized(t.Names, _locale), string.Join(" ", t.Aliases.Select(a => a.Text)))))
            {
                var fold = new Foldout { text = Localized(term.Names, _locale) + (term.Deprecated ? " · deprecated" : ""), value = _library.Terms.Count < 4 }; _inspector.Add(fold);
                Body(fold, term.Id + " · revision " + term.Revision, "studio-code");
                TextControl(fold, "Name (" + _locale + ")", Localized(term.Names, _locale), v => { SetLocalized(term.Names, _locale, v); term.Revision++; });
                TextControl(fold, "Category", term.Category, v => { term.Category = v; term.Revision++; });
                TextControl(fold, "Aliases (semicolon separated)", string.Join("; ", term.Aliases.Where(a => a.Locale == _locale).Select(a => a.Text)), v => {
                    term.Aliases.RemoveAll(a => a.Locale == _locale); term.Aliases.AddRange(v.Split(';').Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => new DescriptionLocalizedText { Locale = _locale, Text = a.Trim() })); term.Revision++;
                });
                DescriptionArea(fold, "Definition", Localized(term.Definitions, _locale), v => { SetLocalized(term.Definitions, _locale, v); term.Revision++; });
                ChoiceControl(fold, "Semantic style", _library.Styles.Select(s => s.Id).Prepend("None").ToList(), string.IsNullOrEmpty(term.StyleId) ? "None" : term.StyleId, v => { term.StyleId = v == "None" ? "" : v; term.Revision++; });
                ChoiceControl(fold, "Gameplay definition link", DescriptionAuthoring.MechanicIds(_document).Concat(StandardEffectDatabase.CreateStatusRecipes().Select(s => s.Id)).Concat(Enum.GetValues(typeof(StatId)).Cast<StatId>().Select(s => "stat." + s)).Distinct().Prepend("None").ToList(), string.IsNullOrEmpty(term.GameplayDefinitionId) ? "None" : term.GameplayDefinitionId, v => { term.GameplayDefinitionId = v == "None" ? "" : v; term.Revision++; });
                ToggleControl(fold, "Deprecated", term.Deprecated, v => { term.Deprecated = v; term.Revision++; });
                var tooltipLocale = term.Tooltip?.Locales.FirstOrDefault(l => l.Locale == _locale);
                DescriptionArea(fold, "Tooltip document source", tooltipLocale?.Blocks.FirstOrDefault()?.Source ?? "", v => {
                    if (term.Tooltip == null) term.Tooltip = new DescriptionDocument { Id = term.Id + ".tooltip", OwnerId = term.Id, DefaultLocale = _locale };
                    var l = term.Tooltip.Locales.FirstOrDefault(x => x.Locale == _locale);
                    if (l == null) { l = new DescriptionLocale { Locale = _locale }; term.Tooltip.Locales.Add(l); }
                    if (l.Blocks.Count == 0) l.Blocks.Add(new DescriptionBlock { Id = term.Id + ".tooltip.paragraph" }); l.Blocks[0].Source = v; term.Revision++;
                });
                Button(fold, "Insert term", () => InsertSource("{{term:" + term.Id + "}}"));
                Button(fold, "Open glossary", () => OpenGlossary(term.Id));
                UsageList(fold, term.Id);
                Button(fold, "Remove unused keyword", () => Edit("Remove keyword", () => _library.Terms.Remove(term), true)).SetEnabled(DescriptionAuthoring.FindUsages(_document.Description, _library, term.Id).Count == 0);
            }
        }
        private void OpenGlossary(string id)
        { if (!string.IsNullOrEmpty(_openGlossary) && _glossaryHistory.Count < 16) _glossaryHistory.Push(_openGlossary); _openGlossary = id; RebuildDescriptionInspector(); }
        private void SeedProjectVocabulary()
        {
            void Add(string id, string name, string definition, string category, string gameplay = "", string[] aliases = null)
            {
                if (_library.Terms.Any(t => t.Id == id)) return;
                if (_library.Terms.Any(t => t.Names.Any(n => n.Locale == "en" && n.Text.Equals(name, StringComparison.OrdinalIgnoreCase)))) name += " (" + id + ")";
                _library.Terms.Add(new DescriptionTerm { Id = id, Category = category, GameplayDefinitionId = gameplay,
                    Names = new List<DescriptionLocalizedText> { new DescriptionLocalizedText { Text = name } },
                    Definitions = new List<DescriptionLocalizedText> { new DescriptionLocalizedText { Text = definition } },
                    Aliases = (aliases ?? new string[0]).Select(a => new DescriptionLocalizedText { Text = a }).ToList() });
            }
            foreach (var term in FightingAllstar.Presentation.Combat.CombatTextCatalog.Terms)
            {
                if (term.Name == "Shatter") continue;
                Add("keyword.attack." + term.Name.ToLowerInvariant().Replace(" ", "-"), term.Name, term.Explanation, "Attack keyword", "", term.Aliases);
            }
            Add("keyword.attack.shatter", "Shatter (attack)", "Ignores the target's Resistance.", "Attack keyword");
            Add("keyword.reaction.shatter", "Shatter (reaction)", "Breaks Frozen and deals reaction damage. Distinct from the attack keyword.", "Elemental reaction");
            foreach (StatId stat in Enum.GetValues(typeof(StatId))) Add("keyword.stat." + stat.ToString().ToLowerInvariant(), stat.ToString(), "Authored stat: " + stat, "Stat", "stat." + stat);
            foreach (var recipe in StandardEffectDatabase.CreateStatusRecipes()) Add("keyword." + recipe.Id, recipe.NameKey ?? recipe.Id, "Standard status " + recipe.Id, "Status", recipe.Id);
            foreach (var resource in _document.Resources) Add("keyword." + resource.Id, resource.Name, "Resource; maximum " + resource.Cap, "Resource", resource.Id);
            foreach (var status in _document.Statuses) Add("keyword." + status.Id, status.Name, "Shared status definition", "Status", status.Id);
            AddWording("snippet.resource-gain", "Resource gain", "Gain {{value:$amount|integer}} {{term:$resource}} (maximum {{value:$cap|integer}}).", "ได้รับ{{term:$resource}} {{value:$amount|integer}} ขั้น (สูงสุด {{value:$cap|integer}} ขั้น)", new[] { "amount", "cap" }, new[] { "resource" });
            AddWording("snippet.protection", "Cleanse then recover", "After removing {{text:$eligible}}, recover {{value:$heal|percent}} of Max HP for {{text:$recipient}}.", "หลังลบ{{text:$eligible}} ฟื้นฟู HP {{value:$heal|percent}} ของ HP สูงสุดให้{{text:$recipient}}", new[] { "heal" }, new string[0], new[] { "eligible", "recipient" });
            AddWording("snippet.layer-scaling", "Per-layer scaling", "Increase damage by {{value:$amount|percent}} per {{term:$mark}} layer.", "เพิ่มดาเมจ {{value:$amount|percent}} ต่อขั้นของ{{term:$mark}}", new[] { "amount" }, new[] { "mark" });
            AddWording("snippet.before-damage", "Before damage", "Before dealing damage, {{text:$action}}. Once per action.", "ก่อนสร้างดาเมจ {{text:$action}} หนึ่งครั้งต่อการกระทำ", new string[0], new string[0], new[] { "action" });
        }
        private void AddWording(string id, string name, string english, string thai, string[] values, string[] terms, string[] text = null)
        {
            if (_library.Snippets.Any(s => s.Id == id)) return;
            _library.Snippets.Add(new DescriptionSnippet { Id = id, Name = name, UsageNotes = "Review timing, recipient and eligibility against linked rules.",
                Inputs = values.Select(v => new DescriptionSnippetInput { Id = v, Kind = DescriptionInputKind.ValueParameter }).Concat(terms.Select(t => new DescriptionSnippetInput { Id = t, Kind = DescriptionInputKind.Term })).Concat((text ?? new string[0]).Select(t => new DescriptionSnippetInput { Id = t, Kind = DescriptionInputKind.Text })).ToList(),
                Templates = new List<DescriptionLocalizedText> { new DescriptionLocalizedText { Text = english }, new DescriptionLocalizedText { Locale = "th", Text = thai } } });
        }
        private void BuildFormattingLibrary()
        {
            Section(_inspector, "APPROVED FORMATTING");
            foreach (var tag in DescriptionCapabilities.Tags.Where(t => LibraryMatches(t.Syntax, t.Description)))
            {
                Body(_inspector, tag.Syntax, "studio-code"); Body(_inspector, tag.Description + " · " + StudioDescriptionChecks.VerificationFor(tag.Syntax), "studio-subtle");
                if (tag.Syntax.StartsWith("<b>")) Button(_inspector, "Wrap bold", () => WrapSelection("<b>", "</b>"));
                else if (tag.Syntax.StartsWith("<i>")) Button(_inspector, "Wrap italic", () => WrapSelection("<i>", "</i>"));
                else if (tag.Syntax.StartsWith("<color")) Button(_inspector, "Wrap approved color", () => WrapSelection("<color=#70D6FF>", "</color>"));
                else Button(_inspector, "Insert literal markup", () => InsertSource(@"\<b\>"));
            }
            Body(_inspector, "Sprites, font/style assets, links, size and layout tags require a registered consumer adapter and remain unsupported. Raw HTML is not accepted.", "studio-warning");
            foreach (var style in _library.Styles.Where(s => LibraryMatches(s.Id, s.Name)))
            {
                var fold = new Foldout { text = style.Name, value = true }; _inspector.Add(fold);
                TextControl(fold, "Style name", style.Name, v => { style.Name = v; style.Revision++; });
                ColorUtility.TryParseHtmlString(style.Color, out var color); var field = new ColorField("Color") { value = color, showAlpha = true };
                field.RegisterValueChangedCallback(e => Edit("Edit semantic color", () => { style.Color = "#" + ColorUtility.ToHtmlStringRGBA(e.newValue); style.Revision++; })); fold.Add(field);
                ToggleControl(fold, "Bold role", style.Bold, v => { style.Bold = v; style.Revision++; }); ToggleControl(fold, "Italic role", style.Italic, v => { style.Italic = v; style.Revision++; });
                Button(fold, "Insert semantic style", () => { var text = SelectedDescriptionSource(); InsertSource("{{style:" + style.Id + "|" + DescriptionAuthoring.Escape(text.Length == 0 ? "text" : text) + "}}"); });
                UsageList(fold, style.Id);
            }
            Button(_inspector, "+ Semantic style", () => Edit("Create semantic style", () => _library.Styles.Add(new DescriptionStyle { Id = "style." + Guid.NewGuid().ToString("N"), Name = "New style" }), true));
        }
        private void BuildDescriptionBindings()
        {
            Section(_inspector, "BOUND AUTHORED VALUES");
            foreach (var p in _document.Parameters.Where(p => LibraryMatches(p.Id, p.Name)))
            {
                Button(_inspector, p.Name + " · " + p.Value + " " + p.Unit, () => InsertSource("{{value:" + p.Id + (p.Unit == StudioParameterUnit.BasisPoints ? "|percent}}" : "|integer}}")));
                Button(_inspector, "Go to " + p.Name, () => SelectSource(p.Id));
            }
            Section(_inspector, "RESOLVED CONFIGURATION BINDINGS");
            foreach (var binding in _document.Description.Bindings)
            {
                var fold = new Foldout { text = binding.Name, value = true }; _inspector.Add(fold); Body(fold, binding.Id, "studio-code");
                TextControl(fold, "Binding name", binding.Name, v => binding.Name = v);
                ChoiceControl(fold, "Bound skill", _document.EditingCharacter.Skills.Select(s => s.Id).ToList(), binding.SkillId, v => binding.SkillId = v);
                TextControl(fold, "Effect ID (blank = primary)", binding.EffectId, v => binding.EffectId = v);
                EnumControl(fold, "Resolved field", binding.Field, v => binding.Field = (DescriptionBindingField)v);
                Button(fold, "Insert resolved value", () => InsertSource("{{value:" + binding.Id + (binding.Field == DescriptionBindingField.Coefficient || binding.Field == DescriptionBindingField.HealCoefficient ? "|percent}}" : binding.Field == DescriptionBindingField.StatusDuration ? "|turns}}" : "|integer}}")));
                UsageList(fold, binding.Id);
                Button(fold, "Remove unused binding", () => Edit("Remove description binding", () => _document.Description.Bindings.Remove(binding), true)).SetEnabled(DescriptionAuthoring.FindUsages(_document.Description, _library, binding.Id).Count == 0);
            }
            Button(_inspector, "+ Resolved skill binding", () => Edit("Add description binding", () => _document.Description.Bindings.Add(new DescriptionValueBinding { SkillId = _document.EditingCharacter.Skills.First().Id }), true));
        }
        private string SelectedDescriptionSource()
        { if (_sourceField == null) return ""; var a = SourceSelectionStart(_sourceField.value.Length); var b = SourceSelectionEnd(_sourceField.value.Length); return _sourceField.value.Substring(a, b - a); }
        private void BuildSnippetLibrary()
        {
            Section(_inspector, "PINNED SNIPPETS & WORDING");
            Body(_inspector, "Published instances pin their template revision. New revisions require explicit adoption. Typed inputs are required.", "studio-subtle");
            Button(_inspector, "+ Snippet", () => Edit("Create snippet", () => { var s = new DescriptionSnippet { Id = "snippet." + Guid.NewGuid().ToString("N"), Templates = new List<DescriptionLocalizedText> { new DescriptionLocalizedText { Locale = _locale, Text = "Write reusable wording here." } } }; _library.Snippets.Add(s); _editableSnippet = s.Id + "@1"; }, true));
            foreach (var snippet in _library.Snippets.Where(s => LibraryMatches(s.Id, s.Name, s.Category, Localized(s.Templates, _locale))).OrderBy(s => s.Id).ThenBy(s => s.Revision))
            {
                var key = snippet.Id + "@" + snippet.Revision; var uses = DescriptionAuthoring.FindUsages(_document.Description, _library, key);
                var editable = _editableSnippet == key && uses.Count == 0;
                var fold = new Foldout { text = key, value = _editableSnippet == key || _library.Snippets.Count < 3 }; _inspector.Add(fold);
                if (editable) { TextControl(fold, "Snippet name", snippet.Name, v => snippet.Name = v); TextControl(fold, "Snippet category", snippet.Category, v => snippet.Category = v); TextControl(fold, "Usage notes", snippet.UsageNotes, v => snippet.UsageNotes = v); }
                else Body(fold, snippet.Name + " · " + snippet.Category + "\n" + snippet.UsageNotes, "studio-subtle");
                var template = new TextField("Template (" + _locale + ")") { multiline = true, value = Localized(snippet.Templates, _locale), isReadOnly = !editable };
                template.RegisterValueChangedCallback(e => Edit("Edit unpublished snippet", () => SetLocalized(snippet.Templates, _locale, e.newValue))); fold.Add(template);
                foreach (var input in snippet.Inputs.ToList())
                {
                    var inputRow = Node(fold, input.Id + " · " + input.Kind);
                    if (editable)
                    {
                        TextControl(inputRow, "Input ID", input.Id, v => input.Id = v);
                        EnumControl(inputRow, "Input type", input.Kind, v => input.Kind = (DescriptionInputKind)v, true);
                        Button(inputRow, "Remove input", () => Edit("Remove snippet input", () => snippet.Inputs.Remove(input), true));
                    }
                    var inputKey = key + ":" + input.Id; _snippetInputs.TryGetValue(inputKey, out var value);
                    if (input.Kind == DescriptionInputKind.Text)
                    { var f = new TextField("Input " + input.Id) { value = value ?? "" }; f.RegisterValueChangedCallback(e => _snippetInputs[inputKey] = e.newValue); inputRow.Add(f); }
                    else
                    {
                        var options = input.Kind == DescriptionInputKind.Term ? _library.Terms.Select(t => t.Id).ToList() : _document.Parameters.Select(p => p.Id).Concat(_document.Description.Bindings.Select(b => b.Id)).ToList();
                        options.Insert(0, "Choose required input");
                        var picker = new PopupField<string>("Input " + input.Id, options, options.Contains(value) ? value : options[0]);
                        picker.RegisterValueChangedCallback(e => _snippetInputs[inputKey] = e.newValue == options[0] ? "" : e.newValue); inputRow.Add(picker);
                    }
                }
                if (editable) Button(fold, "+ Typed input", () => Edit("Add snippet input", () => snippet.Inputs.Add(new DescriptionSnippetInput { Id = "input" + (snippet.Inputs.Count + 1) }), true));
                Button(fold, "Create next editable revision", () => Edit("Version snippet", () => { var copy = SnapshotJson.Deserialize<DescriptionSnippet>(SnapshotJson.Serialize(snippet)); copy.Revision = _library.Snippets.Where(s => s.Id == snippet.Id).Max(s => s.Revision) + 1; _library.Snippets.Add(copy); _editableSnippet = copy.Id + "@" + copy.Revision; }, true));
                Button(fold, "Insert this pinned revision", () => Guard(() => {
                    var inputs = snippet.Inputs.ToDictionary(i => i.Id, i => _snippetInputs.TryGetValue(key + ":" + i.Id, out var v) ? v : "");
                    InsertSource(DescriptionAuthoring.SnippetToken(snippet, inputs)); _editableSnippet = null; RebuildDescriptionInspector();
                }));
                var prior = _library.Snippets.Where(s => s.Id == snippet.Id && s.Revision < snippet.Revision).OrderByDescending(s => s.Revision).FirstOrDefault();
                if (prior != null)
                {
                    Body(fold, "REVISION DIFF\nBefore: " + Localized(prior.Templates, _locale) + "\nAfter: " + Localized(snippet.Templates, _locale), "studio-code");
                    Button(fold, "Adopt revision in selected instance", () => Guard(() => {
                        var token = SelectedDescriptionSource(); var prefix = "{{snippet:" + snippet.Id + "@" + prior.Revision;
                        if (!token.StartsWith(prefix + "|") && token != prefix + "}}") throw new InvalidOperationException("Select one instance of the previous revision first.");
                        var next = "{{snippet:" + key + token.Substring(prefix.Length);
                        var check = DescriptionCompiler.Compile(next, _library, DescriptionContext());
                        if (!check.Success) throw new InvalidOperationException(string.Join("\n", check.Diagnostics.Select(d => d.Message)));
                        InsertSource(next); _editableSnippet = null; RebuildDescriptionInspector();
                    }));
                }
                UsageList(fold, key);
                Button(fold, "Remove unused revision", () => Edit("Remove snippet revision", () => _library.Snippets.Remove(snippet), true)).SetEnabled(uses.Count == 0);
            }
            Button(_inspector, "Detach selected snippet as local wording", () => Guard(() => { var selected = SelectedDescriptionSource(); var detached = DescriptionAuthoring.DetachSnippet(selected, _library, _locale); InsertSource(detached, selected); SetStatus("Detached snippet provenance saved; value and keyword references remain linked."); }));
        }
        private void BuildDescriptionBranches()
        {
            var block = CurrentBlock; if (block == null || CurrentLocale == null) return;
            Section(_inspector, "ORDERED CONFIGURATION WORDING");
            ToggleControl(_inspector, "Explicit fallback", block.HasFallback, v => block.HasFallback = v);
            Body(_inspector, "First matching branch wins. Fallback source is retained. Conditions read authored configuration, never live battle values.", "studio-subtle");
            foreach (var branch in block.Branches.ToList())
            {
                var fold = new Foldout { text = branch.Name, value = true }; _inspector.Add(fold);
                TextControl(fold, "Branch name", branch.Name, v => branch.Name = v);
                IntControl(fold, "Minimum level", branch.MinimumLevel, v => branch.MinimumLevel = v); IntControl(fold, "Maximum level", branch.MaximumLevel, v => branch.MaximumLevel = v);
                IntControl(fold, "Minimum rank", branch.MinimumRank, v => branch.MinimumRank = v); IntControl(fold, "Maximum rank", branch.MaximumRank, v => branch.MaximumRank = v);
                IntControl(fold, "Minimum tier", branch.MinimumTier, v => branch.MinimumTier = v); IntControl(fold, "Maximum tier", branch.MaximumTier, v => branch.MaximumTier = v);
                ChoiceControl(fold, "Branch variant", new[] { "Any", "base" }.Concat(_document.AbilityVariants.Select(v => v.Definition.Id)).ToList(), string.IsNullOrEmpty(branch.VariantId) ? "Any" : branch.VariantId, v => branch.VariantId = v == "Any" ? "" : v);
                Button(fold, "Edit branch wording", () => { _descriptionBranch = branch.Id; Rebuild(); });
                Button(fold, "Move branch up", () => Edit("Reorder wording branches", () => { var i = block.Branches.IndexOf(branch); block.Branches.RemoveAt(i); block.Branches.Insert(i - 1, branch); }, true)).SetEnabled(block.Branches.IndexOf(branch) > 0);
                Button(fold, "Remove branch", () => Edit("Remove wording branch", () => block.Branches.Remove(branch), true));
            }
            Button(_inspector, "+ Configuration branch", () => Edit("Add wording branch", () => { var b = new DescriptionBranch { Source = block.Source }; block.Branches.Add(b); _descriptionBranch = b.Id; }, true));
        }
        private void BuildDescriptionReview()
        {
            Section(_inspector, "WORDING REVIEW");
            var locale = CurrentLocale;
            var hash = _descriptionResult?.TextHash ?? ""; var gameplay = _compiled?.Hashes?.Gameplay ?? "";
            var reviewed = locale != null && locale.ReviewState == "Reviewed" && hash == locale.ReviewedTextHash && gameplay == locale.ReviewedGameplayHash;
            Body(_inspector, DescriptionReviewStatus(), reviewed ? "studio-code" : "studio-warning").name = "studio-wording-review-status";
            Button(_inspector, "Mark selected locale reviewed", () => Edit("Review wording", () => { locale.ReviewState = "Reviewed"; locale.ReviewedTextHash = hash; locale.ReviewedGameplayHash = gameplay; }, true)).SetEnabled(locale != null && _descriptionResult?.Success == true && _compiled?.Success == true);
            var block = CurrentBlock;
            if (block != null && locale != null)
            {
                var choices = DescriptionAuthoring.MechanicIds(_document).ToList();
                foreach (var id in choices)
                { var captured = id; ToggleControl(_inspector, "Covers " + DisplayChoice(id), block.MechanicIds.Contains(id), v => { if (v) block.MechanicIds.Add(captured); else block.MechanicIds.Remove(captured); }); }
            }
            var covered = new HashSet<string>(locale?.Blocks.SelectMany(b => b.MechanicIds) ?? Enumerable.Empty<string>());
            Body(_inspector, "Unlinked mechanics: " + string.Join(", ", DescriptionAuthoring.MechanicIds(_document).Where(id => !covered.Contains(id))) + "\nLinks record review coverage; they do not prove prose is mechanically accurate.", "studio-subtle");
            foreach (var diagnostic in _descriptionResult?.Diagnostics ?? new List<DescriptionDiagnostic>())
            {
                var d = diagnostic; Button(_inspector, d.Code + ": " + d.Message, () => {
                    if (d.Range.SourceId != _document.Description.Id || !locale.Blocks.Any(b => b.Id == d.Range.BlockId)) { SetStatus(d.Range.SourceId + ": " + d.Message); return; }
                    _blockId = d.Range.BlockId; _descriptionBranch = string.IsNullOrEmpty(d.Range.BranchId) ? "Fallback" : d.Range.BranchId; Rebuild(); _sourceField.SelectRange(Math.Min(_sourceField.value.Length, d.Range.Start), Math.Min(_sourceField.value.Length, d.Range.Start + d.Range.Length)); _sourceField.Focus();
                });
            }
            Section(_inspector, "GENERATED MECHANICAL SUMMARY");
            foreach (var rule in _document.Rules) Body(_inspector, rule.Name + " · " + rule.Trigger + " → " + (_document.Actions.FirstOrDefault(a => a.Id == rule.ActionRootId) is StudioActionNode a ? StudioAuthoringCatalog.Summary(a) : "Opening gauge"));
            Section(_inspector, "ORIGINAL DESIGNER SOURCE");
            if (block != null) foreach (var origin in block.DetachedSnippetOrigins) Body(_inspector, "Detached instance: " + origin, "studio-code");
            var original = Body(_inspector, _document.LegacyCharacter.PassiveSource?.Description ?? "No imported passive prose.", "studio-subtle"); original.enableRichText = false;
        }
    }
}
#endif


