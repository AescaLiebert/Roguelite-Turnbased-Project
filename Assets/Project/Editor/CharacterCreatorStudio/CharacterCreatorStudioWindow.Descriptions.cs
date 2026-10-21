#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public sealed partial class CharacterCreatorStudioWindow
    {
        [SerializeField] private string _blockId, _descriptionBranch = "Fallback";
        [SerializeField] private string _editableSnippet;
        [SerializeField] private string _descriptionPanel = "Keywords", _librarySearch = "", _previewVariant = "Base";
        [SerializeField] private int _descriptionLevel = 1, _descriptionRank = 1, _descriptionTier, _descriptionWidth = 640;
        [SerializeField] private bool _compactDescription;
        [SerializeField] private string _descriptionSurface = "Studio";
        private TextField _sourceField;
        private Label _richPreview, _descriptionOutput, _descriptionDiagnostics;
        private DescriptionCompileResult _descriptionResult;
        private readonly Dictionary<string, string> _snippetInputs = new Dictionary<string, string>();
        private readonly Stack<string> _glossaryHistory = new Stack<string>();
        private string _openGlossary;
        private int _sourceSelectionStart, _sourceSelectionEnd;
        private bool _sourceFocused;

        private DescriptionLocale CurrentLocale => _document.Description.Locales.FirstOrDefault(l => l.Locale == _locale);
        private DescriptionBlock CurrentBlock => (CurrentLocale ?? _document.Description.Locales.FirstOrDefault(l => l.Locale == _document.Description.DefaultLocale))?.Blocks.FirstOrDefault(b => b.Id == _blockId);
        private DescriptionBranch CurrentBranch => CurrentBlock?.Branches.FirstOrDefault(b => b.Id == _descriptionBranch);

        private void BuildDescriptions()
        {
            if (_useLegacyDescriptionLibrary)
            {
                Button(_host, "← Skill / Ultimate / Passive descriptions", () => { _useLegacyDescriptionLibrary = false; Rebuild(); });
                BuildLegacyDescriptions();
            }
            else BuildNativeDescriptions();
        }

        private void BuildLegacyDescriptions()
        {
            _sourceField = null;
            Heading(_host, "Description Studio");
            Body(_host, "Player wording · typed references · localized configuration previews", "studio-subtle");
            var config = new Foldout { text = "Preview configuration · level " + _descriptionLevel + " / rank " + _descriptionRank + " / tier " + _descriptionTier + " / " + _previewVariant, value = false };
            config.AddToClassList("studio-description-config"); _host.Add(config);
            var locale = new PopupField<string>("Language", _document.Description.Locales.Select(l => l.Locale).Concat(new[] { "en", "th" }).Distinct().ToList(), _locale);
            locale.RegisterValueChangedCallback(e => { _locale = e.newValue; _blockId = null; Rebuild(); }); config.Add(locale);
            PreviewNumber(config, "Preview level", _descriptionLevel, v => _descriptionLevel = Math.Max(1, v));
            PreviewNumber(config, "Preview rank", _descriptionRank, v => _descriptionRank = Mathf.Clamp(v, 1, 3));
            PreviewNumber(config, "Preview tier", _descriptionTier, v => _descriptionTier = Mathf.Clamp(v, 0, 5));
            var variants = new List<string> { "Base" }; variants.AddRange(_document.AbilityVariants.Select(v => v.Definition.Id));
            if (!variants.Contains(_previewVariant)) _previewVariant = "Base";
            var variantPicker = new PopupField<string>("Authored variant", variants, _previewVariant);
            variantPicker.RegisterValueChangedCallback(e => { _previewVariant = e.newValue; RefreshDescriptionPreview(); }); config.Add(variantPicker);
            Body(_host, "Authored configuration preview. Battle resource readiness is inspected in Scenario & actors.", "studio-subtle");
            var authored = CurrentLocale;
            var selected = authored ?? _document.Description.Locales.FirstOrDefault(l => l.Locale == _document.Description.DefaultLocale);
            if (authored == null)
            {
                Body(_host, "Translation missing: visible fallback is " + selected?.Locale + ". Source is read-only until copied for translation.", "studio-warning");
                Button(_host, "Create " + _locale + " translation", () => Edit("Create description translation", () => {
                    var copy = selected == null ? new DescriptionLocale() : SnapshotJson.Deserialize<DescriptionLocale>(SnapshotJson.Serialize(selected));
                    copy.Locale = _locale; copy.ReviewState = "Draft"; copy.ReviewedTextHash = ""; copy.ReviewedGameplayHash = "";
                    _document.Description.Locales.Add(copy);
                }, true));
            }
            if (selected == null) return;
            var block = selected.Blocks.FirstOrDefault(b => b.Id == _blockId) ?? selected.Blocks.FirstOrDefault();
            if (block == null)
            {
                Button(_host, "+ Paragraph", () => Edit("Add description block", () => selected.Blocks.Add(new DescriptionBlock { Id = "paragraph." + Guid.NewGuid().ToString("N") }), true));
                return;
            }
            _blockId = block.Id;
            var blockPicker = new PopupField<string>("Block", selected.Blocks.Select(b => b.Id).ToList(), block.Id);
            blockPicker.formatListItemCallback = id => BlockLabel(selected.Blocks.First(b => b.Id == id));
            blockPicker.formatSelectedValueCallback = blockPicker.formatListItemCallback;
            blockPicker.RegisterValueChangedCallback(e => { _blockId = e.newValue; _descriptionBranch = "Fallback"; Rebuild(); }); _host.Add(blockPicker);
            var blockTools = Row(_host);
            Button(blockTools, "+ Paragraph", () => Edit("Add description block", () => { var b = new DescriptionBlock { Id = "paragraph." + Guid.NewGuid().ToString("N") }; selected.Blocks.Add(b); _blockId = b.Id; }, true)).SetEnabled(authored != null);
            Button(blockTools, "Move block up", () => MoveBlock(-1)).SetEnabled(authored != null && selected.Blocks.IndexOf(block) > 0);
            Button(blockTools, "Move block down", () => MoveBlock(1)).SetEnabled(authored != null && selected.Blocks.IndexOf(block) < selected.Blocks.Count - 1);
            Button(blockTools, "Remove block", () => Edit("Remove description block", () => selected.Blocks.Remove(block), true)).SetEnabled(authored != null && selected.Blocks.Count > 1);
            var shape = new EnumField("Structure", block.Kind); shape.SetEnabled(authored != null);
            shape.RegisterValueChangedCallback(e => Edit("Change block structure", () => block.Kind = (DescriptionBlockKind)e.newValue)); _host.Add(shape);
            var branches = new List<string> { "Fallback" }; branches.AddRange(block.Branches.Select(b => b.Id));
            if (!branches.Contains(_descriptionBranch)) _descriptionBranch = "Fallback";
            var branchPicker = new PopupField<string>("Wording branch", branches, _descriptionBranch);
            branchPicker.formatListItemCallback = id => id == "Fallback" ? "Fallback · default wording" : block.Branches.First(b => b.Id == id).Name;
            branchPicker.formatSelectedValueCallback = branchPicker.formatListItemCallback;
            branchPicker.RegisterValueChangedCallback(e => { _descriptionBranch = e.newValue; Rebuild(); }); _host.Add(branchPicker);
            var toolbar = Row(_host);
            Button(toolbar, "Bold", () => WrapSelection("<b>", "</b>"));
            Button(toolbar, "Italic", () => WrapSelection("<i>", "</i>"));
            foreach (var panel in new[] { "Keywords", "Values", "Snippets", "Formatting", "Review" })
            { var p = panel; Button(toolbar, p, () => { _descriptionPanel = p; RebuildDescriptionInspector(); }); }
            _sourceField = new TextField("SOURCE · approved markup and stable references") { multiline = true, value = CurrentBranch?.Source ?? block.Source, isReadOnly = authored == null, name = "studio-description-source" };
            _sourceField.AddToClassList("studio-source");
            _sourceField.selectAllOnFocus = false;
            _sourceFocused = false; _sourceSelectionStart = _sourceSelectionEnd = 0;
            var editor = _sourceField;
            editor.RegisterCallback<FocusInEvent>(_ => { if (_sourceField == editor) _sourceFocused = true; }, TrickleDown.TrickleDown);
            editor.RegisterCallback<FocusOutEvent>(_ => { if (_sourceField != editor) return; _sourceSelectionStart = editor.cursorIndex; _sourceSelectionEnd = editor.selectIndex; _sourceFocused = false; }, TrickleDown.TrickleDown);
            _sourceField.RegisterValueChangedCallback(e => Edit("Write description", () => { if (CurrentBranch == null) block.Source = e.newValue; else CurrentBranch.Source = e.newValue; })); _host.Add(_sourceField);
            var previewTools = Row(_host);
            var surface = new PopupField<string>("Target surface", StudioDescriptionSurfaces.All.Select(s => s.Name).ToList(), _descriptionSurface);
            surface.RegisterValueChangedCallback(e => { _descriptionSurface = e.newValue; var p = StudioDescriptionSurfaces.All.First(s => s.Name == _descriptionSurface); _descriptionWidth = p.Width; _textPreview.SetFontProfile(p.Font); ResizeDescriptionPreview(); }); _host.Add(surface);
            PreviewNumber(previewTools, "Surface width", _descriptionWidth, v => { _descriptionWidth = Mathf.Clamp(v, 240, 1400); ResizeDescriptionPreview(); });
            var compact = new Toggle("Compact surface") { value = _compactDescription }; compact.RegisterValueChangedCallback(e => { _compactDescription = e.newValue; ResizeDescriptionPreview(); }); previewTools.Add(compact);
            _textPreview = new StudioPreviewText(_textFontProfile);
            _textPreview.FontProfileChanged += profile => _textFontProfile = profile;
            _richPreview = _textPreview.ToolkitLabel; _host.Add(_textPreview.Root);
            _descriptionDiagnostics = Body(_host, "", "studio-warning"); _descriptionDiagnostics.name = "studio-description-diagnostics";
            Section(_host, "PLAIN OUTPUT"); _descriptionOutput = Body(_host, "", "studio-preview"); _descriptionOutput.enableRichText = false;
            RefreshDescriptionPreview(); ResizeDescriptionPreview(); RebuildDescriptionInspector();
        }
        private static string BlockLabel(DescriptionBlock b) => b.Kind + " · " + (b.Source.Length > 55 ? b.Source.Substring(0, 55) + "…" : b.Source);
        private void MoveBlock(int delta) => Edit("Reorder description blocks", () => { var list = CurrentLocale.Blocks; var b = CurrentBlock; var i = list.IndexOf(b); list.RemoveAt(i); list.Insert(i + delta, b); }, true);
        private void PreviewNumber(VisualElement parent, string label, int value, Action<int> set)
        { var f = new IntegerField(label) { value = value }; f.RegisterValueChangedCallback(e => { set(e.newValue); RefreshDescriptionPreview(); }); parent.Add(f); }
        private void ResizeDescriptionPreview()
        {
            if (_textPreview == null) return;
            _textPreview.SetSurfaceSize(_descriptionWidth, _compactDescription ? 180 : 600);
            var surface = StudioDescriptionSurfaces.All.First(s => s.Name == _descriptionSurface);
            _textPreview.SetTypography(surface.Size);
            _richPreview.style.width = _descriptionWidth;
            _richPreview.style.maxWidth = Length.Percent(100);
            _richPreview.style.maxHeight = _compactDescription ? new StyleLength(180) : new StyleLength(StyleKeyword.None);
            _richPreview.style.overflow = _compactDescription ? Overflow.Hidden : Overflow.Visible;
        }
        private DescriptionCompileContext DescriptionContext()
        {
            var character = _compiled?.Character;
            if (character == null)
            {
                var preview = StudioDocumentService.Clone(_document); preview.Description = DescriptionDocument.FromSource("");
                var gameplay = StudioCompiler.Compile(preview, new DescriptionLibrary());
                if (!gameplay.Success) throw new InvalidOperationException("Repair gameplay diagnostics before previewing resolved configuration values.");
                character = gameplay.Character;
            }
            return DescriptionAuthoring.Context(_document, character, _locale, _descriptionLevel, _descriptionRank, _descriptionTier, _previewVariant == "Base" ? "" : _previewVariant);
        }
        private void RefreshDescriptionPreview()
        {
            if (_page != "descriptions" || _textPreview == null || _document == null || _library == null) return;
            try
            {
                _descriptionResult = DescriptionCompiler.Compile(_document.Description, _library, DescriptionContext());
                _textPreview.SetText(_descriptionResult.TmpRichText); _richPreview.text = _descriptionResult.ToolkitRichText;
                if (_descriptionOutput != null) _descriptionOutput.text = _descriptionResult.PlainText;
                if (_descriptionDiagnostics != null) _descriptionDiagnostics.text = string.Join("\n", _descriptionResult.Diagnostics.Select(d => d.Code + ": " + d.Message));
                var review = _inspector?.Q<Label>("studio-wording-review-status"); if (review != null) review.text = DescriptionReviewStatus();
            }
            catch (Exception exception) { _descriptionResult = null; _textPreview.SetText(""); if (_descriptionOutput != null) _descriptionOutput.text = ""; if (_descriptionDiagnostics != null) _descriptionDiagnostics.text = exception.Message; }
        }
        private string DescriptionReviewStatus()
        {
            var locale = CurrentLocale;
            return locale != null && locale.ReviewState == "Reviewed" && _descriptionResult?.TextHash == locale.ReviewedTextHash && _compiled?.Hashes?.Gameplay == locale.ReviewedGameplayHash
                ? "Reviewed for this selected configuration, text dependencies and gameplay revision."
                : "Draft / review required. Free prose requires designer review.";
        }
        private void RebuildDescriptionInspector()
        {
            _inspector.Clear();
            var panels = new List<string> { "Keywords", "Values", "Snippets", "Formatting", "Branches", "Review" };
            var panel = new PopupField<string>("Library", panels, _descriptionPanel); panel.RegisterValueChangedCallback(e => { _descriptionPanel = e.newValue; RebuildDescriptionInspector(); }); _inspector.Add(panel);
            var search = new TextField("Search library") { value = _librarySearch, isDelayed = true }; search.RegisterValueChangedCallback(e => { _librarySearch = e.newValue; RebuildDescriptionInspector(); }); _inspector.Add(search);
            if (_descriptionPanel == "Keywords") BuildKeywordLibrary();
            else if (_descriptionPanel == "Values") BuildDescriptionBindings();
            else if (_descriptionPanel == "Snippets") BuildSnippetLibrary();
            else if (_descriptionPanel == "Formatting") BuildFormattingLibrary();
            else if (_descriptionPanel == "Branches") BuildDescriptionBranches();
            else BuildDescriptionReview();
            ApplyEditorFont();
        }
        private bool LibraryMatches(params string[] values) => string.IsNullOrWhiteSpace(_librarySearch) || values.Any(s => (s ?? "").IndexOf(_librarySearch, StringComparison.OrdinalIgnoreCase) >= 0);
        private void InsertSource(string insertion, string detachedFrom = null)
        {
            if (_sourceField == null || _sourceField.isReadOnly) return;
            var source = _sourceField.value ?? "";
            var start = SourceSelectionStart(source.Length);
            var end = SourceSelectionEnd(source.Length);
            var next = source.Substring(0, start) + insertion + source.Substring(end);
            if (detachedFrom == null) _sourceField.value = next;
            else
            {
                Edit("Detach linked snippet", () => { CurrentBlock.DetachedSnippetOrigins.Add(detachedFrom); if (CurrentBranch == null) CurrentBlock.Source = next; else CurrentBranch.Source = next; });
                _sourceField.SetValueWithoutNotify(next);
            }
            var field = _sourceField; field.Focus(); field.schedule.Execute(() => field.SelectRange(start, start + insertion.Length)).StartingIn(1);
        }
        private void WrapSelection(string open, string close)
        {
            if (_sourceField == null || _sourceField.isReadOnly) return;
            var source = _sourceField.value ?? "";
            var start = SourceSelectionStart(source.Length);
            var end = SourceSelectionEnd(source.Length);
            InsertSource(open + source.Substring(start, end - start) + close);
        }
        private int SourceSelectionStart(int length) => Mathf.Clamp(Math.Min(_sourceFocused ? _sourceField.cursorIndex : _sourceSelectionStart, _sourceFocused ? _sourceField.selectIndex : _sourceSelectionEnd), 0, length);
        private int SourceSelectionEnd(int length) => Mathf.Clamp(Math.Max(_sourceFocused ? _sourceField.cursorIndex : _sourceSelectionStart, _sourceFocused ? _sourceField.selectIndex : _sourceSelectionEnd), SourceSelectionStart(length), length);
        private static string Localized(List<DescriptionLocalizedText> values, string locale) => values.FirstOrDefault(v => v.Locale == locale)?.Text ?? values.FirstOrDefault(v => v.Locale == "en")?.Text ?? "";
        private static void SetLocalized(List<DescriptionLocalizedText> values, string locale, string text)
        { var item = values.FirstOrDefault(v => v.Locale == locale); if (item == null) { item = new DescriptionLocalizedText { Locale = locale }; values.Add(item); } item.Text = text; }
        private string OpeningSnippetSource(DescriptionSnippet snippet) => DescriptionAuthoring.SnippetToken(snippet, snippet.Inputs.ToDictionary(i => i.Id,
            i => i.Kind == DescriptionInputKind.ValueParameter ? _document.Parameters.FirstOrDefault()?.Id ?? "param.required" : i.Kind == DescriptionInputKind.Term ? _library.Terms.FirstOrDefault()?.Id ?? "keyword.required" : "text"));
    }
}
#endif

