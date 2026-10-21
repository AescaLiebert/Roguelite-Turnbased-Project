#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public sealed partial class CharacterCreatorStudioWindow : EditorWindow
    {
        [SerializeField] private StudioDraftAsset _draft;
        [SerializeField] private string _nativeCharacterPath;
        [SerializeField] private StudioTestBundleAsset _testBundle;
        [SerializeField] private StudioScenarioAsset _scenario;
        [SerializeField] private string _page = "overview";
        [SerializeField] private string _selectedRule;
        [SerializeField] private string _selectedCondition;
        [SerializeField] private string _locale = "en";
        [SerializeField] private StudioTextFontProfile _textFontProfile = StudioTextFontProfile.EditorStandard;
        [SerializeField] private int _seed = 12345;
        [SerializeField] private int _scenarioLevel = 1;
        [SerializeField] private int _scenarioRank = 1;
        [SerializeField] private int _scenarioTier;
        [SerializeField] private StudioScenarioPreset _scenarioPreset;
        [NonSerialized] private StudioDocument _document;
        [NonSerialized] private DescriptionLibrary _library;
        [NonSerialized] private StudioCompileResult _compiled;
        [NonSerialized] private StudioRunSnapshot _run;
        private ScrollView _host, _outline, _inspector, _problems;
        [SerializeField] private bool _executionEvidenceCollapsed;
        private bool _executionEvidenceSplitApplied;
        private Label _saveState, _status;
        private ObjectField _sourcePicker;
        private StudioPreviewActor _actorPreview;
        private StudioPreviewText _textPreview;
        private bool _queuedRebuild;

        public StudioDraftAsset Draft => _draft;
        public StudioCompileResult Compilation => _compiled;
        public StudioRunSnapshot LastRun => _run;

        [MenuItem("Fighting Allstar/Character Creator Studio")]
        public static CharacterCreatorStudioWindow Open()
        {
            var window = GetWindow<CharacterCreatorStudioWindow>("Character Studio");
            window.minSize = new Vector2(1060, 680);
            window.Show();
            return window;
        }

        private void OnEnable()
        {
            if (_draft == null)
            {
                var characterPath = string.IsNullOrEmpty(_nativeCharacterPath) ? EditorPrefs.GetString(PreferenceKey + ".character", "") : _nativeCharacterPath;
                var character = AssetDatabase.LoadAssetAtPath<CharacterObject>(characterPath);
                if (character != null) _draft = StudioProductionBridge.OpenSource(character);
                var guid = EditorPrefs.GetString(PreferenceKey + ".draft", "");
                if (_draft == null && guid.Length > 0)
                {
                    var old = AssetDatabase.LoadAssetAtPath<StudioDraftAsset>(AssetDatabase.GUIDToAssetPath(guid));
                    if (old?.SourceCharacter != null) _draft = StudioProductionBridge.OpenSource(old.SourceCharacter);
                }
                _page = EditorPrefs.GetString(PreferenceKey + ".page", _page);
                _locale = EditorPrefs.GetString(PreferenceKey + ".locale", _locale);
            }
            Undo.undoRedoPerformed += OnUndoRedo;
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            EditorApplication.playModeStateChanged += RestoreNativeSession;
        }

        public void CreateGUI()
        {
            DisposePreviews();
            rootVisualElement.Clear();
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(StudioDraftRepository.Root + "/UI/CharacterCreatorStudio.uxml");
            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(StudioDraftRepository.Root + "/UI/CharacterCreatorStudio.uss");
            if (layout == null) { rootVisualElement.Add(new HelpBox("The Studio UXML could not be loaded.", HelpBoxMessageType.Error)); return; }
            layout.CloneTree(rootVisualElement);
            if (style != null) rootVisualElement.styleSheets.Add(style);
            ApplyEditorFont();
            _host = rootVisualElement.Q<ScrollView>("studio-document-host");
            _outline = rootVisualElement.Q<ScrollView>("studio-outline");
            _inspector = rootVisualElement.Q<ScrollView>("studio-inspector");
            _problems = rootVisualElement.Q<ScrollView>("studio-problems");
            _saveState = rootVisualElement.Q<Label>("studio-save-state");
            _status = rootVisualElement.Q<Label>("studio-status");
            _executionEvidenceSplitApplied = false;
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnStudioKeyDown, TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<KeyDownEvent>(OnStudioKeyDown, TrickleDown.TrickleDown);
            Bind("studio-collapse-evidence", ToggleExecutionEvidence);
            Bind("studio-save", SaveDraft);
            Bind("studio-undo", Undo.PerformUndo);
            Bind("studio-redo", Undo.PerformRedo);
            Bind("studio-duplicate", () => { if (_draft.IsDirectSession) ShowWorkspace("bundle"); else LoadDraft(StudioDraftRepository.Duplicate(_draft)); });
            Bind("studio-validate", () => { Compile(); Rebuild(); SetStatus("Validation completed for the current draft revision."); });
            Bind("studio-run", () => ShowWorkspace("lab"));
            foreach (var evidence in new[] { "problems", "trace", "state" })
            {
                var tab = evidence;
                Bind("studio-evidence-" + tab, () => { _evidenceTab = tab; RefreshProblems(); ApplyEditorFont(); });
            }
            foreach (var page in new[] { "rules", "descriptions", "scenario", "presentation", "capabilities", "bundle" })
            {
                var selected = page;
                Bind("studio-tab-" + selected, () => ShowWorkspace(selected == "scenario" ? "lab" : selected));
            }
            var bar = rootVisualElement.Q<VisualElement>("studio-document-bar");
            _sourcePicker = new ObjectField("Character Object") { name = "studio-source-picker", objectType = typeof(CharacterObject), value = _draft?.SourceCharacter };
            _sourcePicker.style.minWidth = 310;
            bar.Add(_sourcePicker);
            Button(bar, "Open character", () => Guard(() => OpenNativeCharacter(_sourcePicker.value as CharacterObject))).name = "studio-open-character";
            rootVisualElement.schedule.Execute(() => SetExecutionEvidenceCollapsed(_executionEvidenceCollapsed)).StartingIn(1);
            Guard(() =>
            {
                if (_draft == null)
                {
                    var path = string.IsNullOrEmpty(_nativeCharacterPath) ? EditorPrefs.GetString(PreferenceKey + ".character", "") : _nativeCharacterPath;
                    var source = AssetDatabase.LoadAssetAtPath<CharacterObject>(path) ?? CharacterObjectRegistrySO.LoadAll().First(c => c != null);
                    _draft = StudioProductionBridge.OpenSource(source);
                }
                ReadDraft(); Rebuild();
                SetStatus("Edit the CharacterObject and its four native data assets from the kit outline. Use Save character or Ctrl+S to save; unsaved edits stay in memory.");
            });
        }

        private void ToggleExecutionEvidence()
        {
            SetExecutionEvidenceCollapsed(!_executionEvidenceCollapsed);
        }

        private void SetExecutionEvidenceCollapsed(bool collapsed)
        {
            var split = rootVisualElement.Q<TwoPaneSplitView>("studio-vertical-split");
            var button = rootVisualElement.Q<Button>("studio-collapse-evidence");
            if (button != null) button.text = collapsed ? "Console (Execution Evidence) ▸" : "Console (Execution Evidence) ▾";
            if (split == null) return;

            if (!_executionEvidenceSplitApplied)
            {
                if (collapsed) split.CollapseChild(1);
                _executionEvidenceSplitApplied = true;
            }
            else if (collapsed != _executionEvidenceCollapsed)
            {
                if (collapsed) split.CollapseChild(1);
                else split.UnCollapse();
            }
            _executionEvidenceCollapsed = collapsed;
        }

        public void LoadDraft(StudioDraftAsset asset)
        {
            if (asset?.SourceCharacter != null && asset.IsDirectSession)
            {
                _nativeCharacterPath = AssetDatabase.GetAssetPath(asset.SourceCharacter);
                EditorPrefs.SetString(PreferenceKey + ".character", _nativeCharacterPath);
            }
            _publishPlan = null; _publishUpdate = asset != null && asset.PublishedCharacter != null;
            if (_publishUpdate) { _publishId = asset.PublishedCharacter.DefinitionId; _publishNumericId = asset.PublishedCharacter.ID; _publishName = asset.PublishedCharacter.FighterName; }
            _draft = asset; _run = null;
            _selectedRule = null; _selectedNode = null; _selectedTrace = -1; _selectedModule = null;
            ReadDraft(); Rebuild();
        }

        private void OpenNativeCharacter(CharacterObject source)
        {
            if (source == null) { SetStatus("Select a CharacterObject first."); return; }
            var session = StudioProductionBridge.OpenSource(source);
            LoadDraft(session);
            SetStatus("Editing " + source.FighterName + ". Press Ctrl+S to save changes to the CharacterObject and its four data assets.");
        }

        public void ShowWorkspace(string page)
        {
            if (!Workspaces.Contains(page)) throw new ArgumentException("Unknown Studio workspace.");
            _page = page; Rebuild();
            ResetWorkspaceScroll();
        }

        private void ResetWorkspaceScroll()
        {
            if (_host == null) return;
            _host.scrollOffset = Vector2.zero;
            _inspector.scrollOffset = Vector2.zero;
            // A cleared ScrollView recomputes its range during the next layout.
            _host.schedule.Execute(() => { _host.scrollOffset = Vector2.zero; _inspector.scrollOffset = Vector2.zero; }).StartingIn(1);
        }

        private void ReadDraft()
        {
            if (_draft != null && _draft.IsDirectSession && _draft.SourceCharacter != null)
            {
                _nativeCharacterPath = AssetDatabase.GetAssetPath(_draft.SourceCharacter);
                EditorPrefs.SetString(PreferenceKey + ".character", _nativeCharacterPath);
            }
            if (!StudioDraftRepository.TryRead(_draft, out _document, out _library, out var error))
            { _compiled = null; SetStatus(error); return; }
            Compile();
        }

        private void SaveDraft()
        {
            Guard(() =>
            {
                if (_draft == null) { SetStatus("Open a character before saving."); return; }
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                { SetStatus("Return to Edit Mode and wait for compilation before saving the character."); return; }
                StudioDraftRepository.Save(_draft);
                if (_draft.IsDirectSession && _draft.SourceCharacter != null)
                {
                    _kitTrackedSource = _draft.SourceCharacter;
                    _kitSavedSourceFingerprint = StudioProductionBridge.SourceFingerprint(_draft.SourceCharacter);
                    _kitSourceArtDirty = false;
                }
                ReadDraft(); Rebuild();
                SetStatus(_draft.IsDirectSession ? "CharacterObject and its four data assets saved." : "Compatibility draft saved.");
            });
        }

        private void OnStudioKeyDown(KeyDownEvent evt)
        {
            if (evt == null || evt.keyCode != KeyCode.S || !evt.actionKey) return;
            rootVisualElement.focusController?.IgnoreEvent(evt);
            evt.StopImmediatePropagation();
            SaveDraft();
        }

        private void Edit(string label, Action change, bool rebuild = false)
        {
            Guard(() =>
            {
                change();
                StudioDraftRepository.Edit(_draft, _document, _library, label);
                Compile();
                RefreshSemanticSummaries();
                if (rebuild) QueueRebuild();
                else { RefreshSaveState(); RefreshProblems(); RefreshDescriptionPreview(); RefreshKitSummaries(); }
            });
        }

        private void Compile()
        {
            if (_document == null) return;
            _compiled = StudioCompiler.Compile(_document, _library);
        }

        private void Rebuild()
        {
            if (_host == null) return;
            _sourcePicker?.SetValueWithoutNotify(_draft?.SourceCharacter);
            DisposePreviews();
            _kitAbilityPreviewRefresh = null;
            _sourceField = null;
            _inspector.RemoveFromClassList("studio-ability-inspector");
            _host.Clear(); _outline.Clear(); _inspector.Clear();
            _inspector.style.display = DisplayStyle.Flex;
            foreach (var page in new[] { "rules", "descriptions", "scenario", "presentation", "capabilities", "bundle" })
                rootVisualElement.Q<Button>("studio-tab-" + page)?.EnableInClassList("studio-active", page == _page || page == "scenario" && _page == "lab");
            if (_document == null)
            {
                Body(_host, "The draft could not be read. Its original serialized source was preserved.", "studio-error");
                return;
            }
            BuildKitOutline();
            switch (_page)
            {
                case "overview": BuildKitOverview(); break;
                case "identity": BuildKitIdentity(); break;
                case "skill1": BuildKitSkill(1); break;
                case "skill2": BuildKitSkill(2); break;
                case "ultimate": BuildKitUltimate(); break;
                case "passive": BuildKitPassive(); break;
                case "descriptions": BuildDescriptions(); break;
                case "scenario": BuildScenario(); break;
                case "lab": BuildBattleLab(); break;
                case "presentation": BuildPresentation(); break;
                case "capabilities": BuildCapabilities(); break;
                case "bundle": BuildBundleReview(); break;
                case "resources": BuildResources(); break;
                case "abilities": BuildAbilities(); break;
                case "parameters": BuildParameters(); break;
                case "modules": BuildModules(); break;
                default: BuildRules(); break;
            }
            RefreshSaveState(); RefreshProblems(); ApplyEditorFont();
        }

        private void ApplyEditorFont()
        {
            var font = Resources.Load<Font>(StudioPreviewText.EditorFontResource);
            if (font == null) return;
            var definition = FontDefinition.FromFont(font);
            rootVisualElement.style.unityFontDefinition = definition;
            // Unity's built-in field styles can override inherited font definitions.
            rootVisualElement.Query<TextElement>().ForEach(element => element.style.unityFontDefinition = definition);
        }

        private void QueueRebuild()
        {
            if (_queuedRebuild) return;
            _queuedRebuild = true;
            rootVisualElement.schedule.Execute(() => { _queuedRebuild = false; Rebuild(); });
        }

        private void RefreshSaveState()
        {
            if (_saveState == null || _draft == null) return;
            var current = StudioDraftRepository.Revision(_draft.DocumentJson, _draft.LibraryJson);
            if (_draft.IsDirectSession)
            {
                var state = current == _draft.SavedRevision && !_kitSourceArtDirty ? " · Saved" : " · Unsaved changes · Save character or Ctrl+S";
                _saveState.text = (_document?.EditingCharacter?.DisplayName ?? _document?.Name) + state;
                _saveState.tooltip = "Use Save character or Ctrl+S to commit changes to this CharacterObject and its four data assets.";
            }
            else
            {
                _saveState.text = _document?.Name + (current == _draft.SavedRevision ? " · Saved" : " · Unsaved changes · Save character or Ctrl+S");
                _saveState.tooltip = "Use Save character or Ctrl+S to save these compatibility assets.";
            }
            var save = rootVisualElement.Q<Button>("studio-save");
            if (save != null) save.SetEnabled(!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling);
            var duplicate = rootVisualElement.Q<Button>("studio-duplicate"); if (duplicate != null) duplicate.text = _draft.IsDirectSession ? "Create new character" : "Duplicate draft";
            rootVisualElement.Q<Button>("studio-run")?.SetEnabled(_compiled?.Success == true);
        }

        private void RefreshProblems()
        {
            if (_problems == null) return;
            _problems.Clear();
            foreach (var tab in new[] { "problems", "trace", "state" })
                rootVisualElement.Q<Button>("studio-evidence-" + tab)?.EnableInClassList("studio-active", _evidenceTab == tab);
            if (_page == "lab") { BuildLabEvidence(); return; }
            if (_page == "presentation") { BuildPresentationEvidence(); return; }
            if (_evidenceTab != "problems") { BuildExecutionEvidence(); return; }
            if (_compiled == null) { Body(_problems, "No valid document loaded.", "studio-error"); return; }
            foreach (var diagnostic in _compiled.Diagnostics)
            {
                var captured = diagnostic;
                var row = Button(_problems, diagnostic.ToString(), () => SelectSource(captured.SourceId));
                row.style.whiteSpace = WhiteSpace.Normal;
                row.AddToClassList(diagnostic.Severity == StudioDiagnosticSeverity.Error ? "studio-error" : "studio-warning");
            }
            if (_compiled.Description != null)
                foreach (var diagnostic in _compiled.Description.Diagnostics)
                {
                    var captured = diagnostic;
                    var row = Button(_problems, diagnostic.Code + ": " + diagnostic.Message, () =>
                    { _page = "descriptions"; Rebuild(); _sourceField?.SelectRange(captured.Range.Start, captured.Range.Start + captured.Range.Length); _sourceField?.Focus(); });
                    row.style.whiteSpace = WhiteSpace.Normal;
                }
            if (_compiled.Success) Body(_problems, "Compiled successfully · gameplay " + Short(_compiled.Hashes.Gameplay) + " · text " + Short(_compiled.Hashes.Text), "studio-success");
            if (_run != null) Body(_problems, "A frozen engine run is available in Scenario & actors. Editing the draft preserves that historical run.", "studio-subtle");
        }

        private void SelectSource(string id)
        {
            var rule = _document.Rules.FirstOrDefault(r => r.Id == id || ContainsCondition(r.ConditionRootId, id, new HashSet<string>()) ||
                ContainsAction(r.ActionRootId, id, new HashSet<string>()));
            if (rule != null) _selectedRule = rule.Id;
            _selectedCondition = id;
            _selectedNode = id;
            _page = _document.Policies.Any(p => p.Id == id) || _document.Resources.Any(r => r.Id == id) || _document.Statuses.Any(s => s.Id == id) ? "resources" :
                _document.AbilityVariants.Any(v => v.Definition.Id == id || v.Definition.Ranks.Any(r => r.Effect.Id == id || r.Effect.Sequence.Any(s => s.Effect?.Id == id))) || _document.AbilityOverrides.Any(o => o.Definition.Id == id) ? "abilities" :
                _document.Parameters.Any(p => p.Id == id) ? "parameters" : "rules";
            Rebuild();
            _host.schedule.Execute(() =>
            {
                var selected = _host.Q<VisualElement>("card." + id) ?? _host.Q<VisualElement>(id);
                if (selected != null)
                {
                    for (var ancestor = selected; ancestor != null; ancestor = ancestor.parent)
                        if (ancestor is Foldout fold) fold.value = true;
                    selected.AddToClassList("studio-active");
                    // A tall action can exceed the viewport. Scroll to its header so
                    // its name, guard and cost remain visible when following a trace.
                    selected.schedule.Execute(() =>
                    {
                        var anchor = selected is Foldout selectedFold ? selectedFold.Q<Toggle>() ?? selected : selected;
                        var offset = _host.scrollOffset;
                        offset.y = Mathf.Max(0, offset.y + anchor.worldBound.yMin - _host.contentViewport.worldBound.yMin);
                        _host.scrollOffset = offset;
                    }).StartingIn(1);
                }
            }).StartingIn(1);
            SetStatus("Source: " + id);
        }

        private bool ContainsCondition(string root, string target, HashSet<string> visited)
        {
            if (root == target) return true;
            if (root == null || !visited.Add(root)) return false;
            var node = _document.Conditions.FirstOrDefault(n => n.Id == root);
            return node != null && node.Children.Any(child => ContainsCondition(child, target, visited));
        }

        private bool ContainsAction(string root, string target, HashSet<string> visited)
        {
            if (string.IsNullOrEmpty(root) || !visited.Add(root)) return false;
            if (root == target) return true;
            var node = _document.Actions.FirstOrDefault(a => a.Id == root);
            if (node == null) return false;
            if (ContainsCondition(node.ConditionRootId, target, new HashSet<string>())) return true;
            if (node.Children.Concat(node.ElseChildren).Any(child => ContainsAction(child, target, visited))) return true;
            var module = _document.Modules.FirstOrDefault(m => m.Id == node.ModuleId);
            return node.Kind == StudioActionKind.Module && module != null && ContainsAction(module.RootActionId, target, visited);
        }

        private void BuildCapabilities()
        {
            Heading(_host, "Capabilities backed by the implementation");
            Body(_host, "Enabled options compile into the production M3 dispatcher. Legacy current-contact modifiers remain imported behavior. Each rule uses explicit origin, activation and ordering controls.");
            foreach (var capability in StudioCapabilities.All)
            {
                var row = new VisualElement(); row.AddToClassList("studio-node");
                Body(row, capability.Status + " · " + capability.Label);
                Body(row, capability.Evidence, "studio-subtle"); _host.Add(row);
            }
            Section(_inspector, "TEXT CAPABILITIES");
            foreach (var tag in DescriptionCapabilities.Tags)
            { Body(_inspector, tag.Syntax, "studio-code"); Body(_inspector, StudioDescriptionChecks.VerificationFor(tag.Syntax), "studio-subtle"); }
        }

        private void BuildBundleReview()
        {
            Heading(_host, "Review & publish character");
            Body(_host, "Review the compiled revision, create an optional editor-only test bundle, or publish a separate native runtime package below. Existing legacy sources are preserved.");
            if (_compiled?.Success != true) { Body(_host, "Resolve compilation errors before applying test content.", "studio-error"); return; }
            Body(_host, "Source character: " + _document.SourceCharacterId);
            if (_document.OwnsOpeningGauge) Body(_host, "Legacy opening gauge is replaced by its Studio rule in the compiled definition.");
            foreach (var rule in _document.Rules)
                Body(_host, (rule.Enabled ? "Add reaction " : "Disabled reaction ") + rule.Id + " · " + rule.Trigger +
                    (string.IsNullOrEmpty(rule.ActionRootId) ? " → " + rule.Target + " · " + rule.AmountParameterId : " → action " + rule.ActionRootId));
            foreach (var policy in _document.Policies) Body(_host, "Add policy " + policy.Id + " · " + policy.Definition.Kind);
            foreach (var variant in _document.AbilityVariants) Body(_host, "Add ability variant " + variant.Definition.Id + " → " + variant.SkillId);
            foreach (var value in _document.AbilityOverrides) Body(_host, "Override " + value.Definition.Id + " · " + value.Definition.Field +
                " · level " + value.Definition.MinimumLevel + "–" + value.Definition.MaximumLevel + " / rank " + value.Definition.MinimumRank + "–" + value.Definition.MaximumRank);
            foreach (var parameter in _document.Parameters) Body(_host, parameter.Id + " = " + parameter.Value + " " + parameter.Unit);
            Section(_host, "EXACT COMPILED IDENTITIES");
            Body(_host, "Gameplay: " + _compiled.Hashes.Gameplay, "studio-code");
            Body(_host, "Text: " + _compiled.Hashes.Text, "studio-code");
            Body(_host, "Presentation: " + _compiled.Hashes.Presentation, "studio-code");
            Body(_host, "Bundle: " + _compiled.Hashes.Bundle, "studio-code");
            if (_draft.IsDirectSession)
            {
                Body(_host, "Press Ctrl+S to save edits to the current native asset set. The form below creates a separate character with a new identity.");
                BuildProductionPublish(); return;
            }
            Button(_host, "Apply this revision to a test bundle", () => Guard(() =>
            {
                _testBundle = StudioDraftRepository.ApplyTestBundle(_draft, _compiled);
                Rebuild(); SetStatus("Created " + AssetDatabase.GetAssetPath(_testBundle) + ". Legacy source is preserved for rollback.");
            }));
            if (_testBundle != null)
            {
                Body(_host, "Test content source mode: " + (_testBundle.UseStudioDefinition ? "Studio" : "Legacy"));
                Button(_host, "Return test bundle to legacy source", () => { StudioDraftRepository.RevertTestBundle(_testBundle); Rebuild(); });
            }
            Section(_inspector, "ACTIVATION CONTRACT");
            Body(_inspector, "The revision is recompiled and its bundle hash checked before asset creation. Invalid edits leave the last working test bundle intact.");
            BuildProductionPublish();
        }

        private void OnUndoRedo()
        {
            if (_draft?.IsDirectSession == true && _draft.SourceCharacter != null)
            {
                var fingerprint = StudioProductionBridge.SourceFingerprint(_draft.SourceCharacter);
                var sourceFingerprintChanged = _kitTrackedSource == _draft.SourceCharacter &&
                    !string.Equals(fingerprint, _kitSavedSourceFingerprint, StringComparison.Ordinal);
                _draft.ImportedSourceHash = fingerprint;
                _kitSourceArtDirty = sourceFingerprintChanged;
            }
            ReadDraft(); Rebuild();
        }
        private void RestoreNativeSession(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode && state != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.delayCall += () => {
                if (this == null || _draft != null || string.IsNullOrEmpty(_nativeCharacterPath)) return;
                var source = AssetDatabase.LoadAssetAtPath<CharacterObject>(_nativeCharacterPath); if (source == null) return;
                var session = StudioProductionBridge.OpenSource(source);
                LoadDraft(session); SetStatus("Native editing session reopened from saved assets. Unsaved changes from before the reload were discarded.");
            };
        }
        private void BeforeReload() { DisposePreviews(); }
        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
            EditorApplication.playModeStateChanged -= RestoreNativeSession;
            rootVisualElement.UnregisterCallback<KeyDownEvent>(OnStudioKeyDown, TrickleDown.TrickleDown);
            if (_draft != null && !_draft.IsDirectSession) EditorPrefs.SetString(PreferenceKey + ".draft", AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_draft)));
            if (_draft?.SourceCharacter != null) EditorPrefs.SetString(PreferenceKey + ".character", AssetDatabase.GetAssetPath(_draft.SourceCharacter));
            EditorPrefs.SetString(PreferenceKey + ".page", _page);
            EditorPrefs.SetString(PreferenceKey + ".locale", _locale);
            DisposePreviews();
        }
        private static string PreferenceKey => "FightingAllstar.CharacterStudio." + StudioDraftRepository.Revision(Application.dataPath, "");
        private void DisposePreviews() { _presentationViewport?.Dispose(); _presentationViewport = null; _actorPreview?.Dispose(); _actorPreview = null; _textPreview?.Dispose(); _textPreview = null; _sourceField = null; _richPreview = null; }
        private void SetStatus(string text) { if (_status != null) _status.text = text; }
        private void Guard(Action action) { try { action(); } catch (Exception e) { SetStatus(e.Message); Debug.LogException(e); } }
        private void Bind(string name, Action action) { rootVisualElement.Q<Button>(name).clicked += () => Guard(action); }
        private static string Short(string hash)
        {
            if (string.IsNullOrEmpty(hash)) return "—";
            var digest = hash.Substring(hash.LastIndexOf(':') + 1);
            return digest.Substring(0, Math.Min(12, digest.Length));
        }
        private static Label Body(VisualElement parent, string text, string css = "studio-body") { var label = new Label(text ?? "") { enableRichText = false }; label.AddToClassList(css); parent.Add(label); return label; }
        private static Label Heading(VisualElement parent, string text) => Body(parent, text, "studio-heading");
        private static Label Section(VisualElement parent, string text) => Body(parent, text, "studio-section-label");
        private static Button Button(VisualElement parent, string text, Action action) { var button = new Button(action) { text = text }; parent.Add(button); return button; }
        private static VisualElement Row(VisualElement parent) { var row = new VisualElement(); row.AddToClassList("studio-row"); parent.Add(row); return row; }
    }
}
#endif


