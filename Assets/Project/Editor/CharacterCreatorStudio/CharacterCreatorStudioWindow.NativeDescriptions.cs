#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Studio;
using FightingAllstar.Presentation.Combat;
using FightingAllstar.Presentation.Navigation;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public sealed partial class CharacterCreatorStudioWindow
    {
        [SerializeField] private string _nativeDescriptionSelection = "Skill 1";
        [SerializeField] private bool _useLegacyDescriptionLibrary;
        [SerializeField] private bool _passivePreviewCollapsed;

        private sealed class WordingTarget
        {
            public string Name, Key;
            public UnityEngine.Object Asset;
            public EffectDefinition Effect;
            public PassiveDefinition Passive;
            public Func<AuthoredDescription> Get;
            public Action<AuthoredDescription> Set;
            public Func<string> Source, Generated;
        }

        private WordingTarget NativeWordingTarget(string selection)
        {
            var c = EditableKit(); var source = _draft?.SourceCharacter;
            if (selection == "Passive")
            {
                if (c.PassiveSource == null) c.PassiveSource = new PassiveSourceDefinition();
                return new WordingTarget { Name = "Passive", Key = "passive", Asset = source, Passive = c.Passive,
                    Get = () => c.PassiveSource.Wording, Set = v => c.PassiveSource.Wording = v,
                    Source = () => c.PassiveSource.Description ?? "",
                    Generated = () => CombatDetailFormatter.HighlightKeywords(PassiveDescriptionText.Format(c.PassiveSource.Description, c.Passive)) };
            }
            if (selection == "Ultimate")
            {
                var tier = c.UltimateTiers.FirstOrDefault(t => t.Tier == _kitTier); if (tier == null) return null;
                return new WordingTarget { Name = "Ultimate · C" + _kitTier, Key = "ultimate." + _kitTier, Asset = source?.Ultimate, Effect = tier.Effect,
                    Get = () => tier.Wording, Set = v => tier.Wording = v, Source = () => tier.Description ?? tier.SourceDescription ?? "",
                    Generated = () => CombatDetailFormatter.HighlightKeywords(CardDescriptionText.Format(tier.Description, tier.Effect)) };
            }
            var slot = selection == "Skill 2" ? 2 : 1;
            var skill = c.Skills.FirstOrDefault(s => s.Slot == slot);
            var rank = skill?.Ranks.FirstOrDefault(r => r.Rank == _kitRank); if (rank == null) return null;
            var effect = CardRules.TryGetSkill(c, skill.Id, rank.Rank, out var sharedEffect) ? sharedEffect : rank.Effect;
            return new WordingTarget { Name = selection + " · R" + _kitRank, Key = "skill" + slot + "." + _kitRank,
                Asset = slot == 1 ? source?.Skill1 : source?.Skill2, Effect = effect,
                Get = () => rank.Wording, Set = v => rank.Wording = v, Source = () => rank.Description ?? rank.SourceDescription ?? "",
                Generated = () => CombatDetailFormatter.HighlightKeywords(CardDescriptionText.Format(rank.Description, effect, rank.Wording)) };
        }

        private void BuildNativeDescriptions()
        {
            Heading(_host, "Description Studio");
            var choices = new List<string> { "Skill 1", "Skill 2", "Ultimate", "Passive" };
            if (!choices.Contains(_nativeDescriptionSelection)) _nativeDescriptionSelection = "Skill 1";
            var selector = new PopupField<string>("Description for", choices, _nativeDescriptionSelection) { name = "studio-description-target" };
            selector.RegisterValueChangedCallback(e => { _nativeDescriptionSelection = e.newValue; Rebuild(); ResetWorkspaceScroll(); }); _host.Add(selector);
            var target = NativeWordingTarget(_nativeDescriptionSelection);
            if (target == null) { Body(_host, "This rank / tier is missing. Create it in its card tab.", "studio-warning"); return; }
            var asset = new ObjectField("Source SO") { objectType = typeof(ScriptableObject), allowSceneObjects = false, value = target.Asset, name = "studio-description-source-asset" };
            asset.RegisterValueChangedCallback(e => {
                var owner = CharacterObjectRegistrySO.LoadAll().FirstOrDefault(c => c == e.newValue || c.Skill1 == e.newValue || c.Skill2 == e.newValue || c.Ultimate == e.newValue || c.PassiveDefinition == e.newValue);
                if (owner == null) { asset.SetValueWithoutNotify(target.Asset); SetStatus("Choose a character, skill, ultimate or passive SO assigned to a registered character."); return; }
                _nativeDescriptionSelection = e.newValue == owner.Skill1 ? "Skill 1" : e.newValue == owner.Skill2 ? "Skill 2" : e.newValue == owner.Ultimate ? "Ultimate" : "Passive";
                LoadDraft(StudioProductionBridge.OpenSource(owner));
            }); _host.Add(asset);
            _inspector.AddToClassList("studio-ability-inspector");
            if (_nativeDescriptionSelection == "Passive") BuildNativePassiveDescriptionPreview(_inspector);
            else
            {
                var preview = new VisualElement { name = "studio-ability-preview" }; preview.AddToClassList("studio-ability-preview"); _inspector.Add(preview);
                BuildAbilityGamePreview(preview, _document.EditingCharacter, _draft?.SourceCharacter, _nativeDescriptionSelection == "Ultimate" ? 0 : _nativeDescriptionSelection == "Skill 2" ? 2 : 1);
            }
            BuildNativeWordingEditor(_host, target);
            Button(_host, "Reusable wording library & localization", () => { _useLegacyDescriptionLibrary = true; Rebuild(); });
        }

        private void BuildNativePassiveDescriptionPreview(VisualElement parent)
        {
            var card = new VisualElement { name = "studio-ability-preview" }; card.AddToClassList("studio-ability-preview"); parent.Add(card);
            var showcase = new VisualElement { name = "studio-native-passive-showcase" };
            showcase.AddToClassList("studio-ability-showcase"); showcase.AddToClassList("studio-native-passive-showcase"); card.Add(showcase);
            var titleBar = new VisualElement(); titleBar.AddToClassList("studio-native-passive-titlebar"); showcase.Add(titleBar);
            Body(titleBar, "PASSIVE", "studio-ability-showcase-title");
            Action refreshCollapse = null;
            var close = Button(titleBar, "×", () => { _passivePreviewCollapsed = !_passivePreviewCollapsed; refreshCollapse(); });
            close.name = "studio-passive-preview-collapse"; close.AddToClassList("studio-native-passive-close");
            var source = _draft != null ? _draft.SourceCharacter : null;
            // Missing serialized Unity references can be managed wrappers: ?? does not
            // use Unity's null comparison and can skip the valid character-icon fallback.
            var passiveArt = source != null && source.FighterPassive != null ? source.FighterPassive : source != null ? source.FighterIcon : null;
            var art = new Image { name = "studio-native-passive-icon", sprite = passiveArt, scaleMode = ScaleMode.ScaleToFit };
            art.tooltip = _draft?.SourceCharacter?.FighterPassive == null ? "No passive art assigned; showing the character icon fallback. Set Passive art in Identity & stats." : "CharacterObject passive icon";
            art.AddToClassList("studio-native-passive-art");
            var artStage = new VisualElement(); artStage.AddToClassList("studio-native-passive-art-stage"); artStage.Add(art); showcase.Add(artStage);
            var copy = new VisualElement(); copy.AddToClassList("studio-ability-game-copy"); card.Add(copy);
            copy.AddToClassList("studio-native-passive-copy");
            var entry = new VisualElement(); entry.AddToClassList("studio-ability-game-header"); entry.AddToClassList("studio-native-passive-entry"); copy.Add(entry);
            var title = new Label { name = "studio-native-passive-title" }; title.AddToClassList("studio-ability-game-title"); entry.Add(title);
            var badges = new VisualElement { name = "studio-native-passive-badges" }; badges.AddToClassList("studio-live-card-badges"); badges.AddToClassList("studio-native-passive-badges"); entry.Add(badges);
            var description = new Label { enableRichText = true, name = "studio-ability-game-description" }; description.AddToClassList("studio-ability-game-description"); copy.Add(description);
            var keywords = new Label { enableRichText = true, name = "studio-ability-game-keywords" }; keywords.AddToClassList("studio-ability-game-keywords"); copy.Add(keywords);
            refreshCollapse = () => {
                artStage.style.display = copy.style.display = _passivePreviewCollapsed ? DisplayStyle.None : DisplayStyle.Flex;
                close.text = _passivePreviewCollapsed ? "+" : "×";
                close.tooltip = _passivePreviewCollapsed ? "Expand passive preview" : "Collapse passive preview";
            }; refreshCollapse();
            var kitArt = Resources.Load<CharacterKitArtSO>("UI/CharacterKitArt");
            _kitAbilityPreviewRefresh = () => {
                var details = CombatDetailFormatter.ExtractPassiveDetails(new FighterState { Definition = _document.EditingCharacter, ConstellationTier = _kitTier }, _draft?.SourceCharacter);
                art.sprite = details.Icon != null ? details.Icon : source?.FighterIcon;
                title.text = details.Title;
                description.text = details.FormattedDescription;
                keywords.text = details.Keywords;
                keywords.style.display = string.IsNullOrWhiteSpace(details.Keywords) ? DisplayStyle.None : DisplayStyle.Flex;
                badges.Clear();
                foreach (var condition in kitArt?.PassiveConditions(details.SourceType, details.Restriction, StudioPassiveView.Gates(_document, true)) ?? Enumerable.Empty<(Sprite icon, string label)>())
                    AddPreviewBadge(badges, condition.icon, condition.label);
                foreach (var status in details.Statuses) AddPreviewBadge(badges, status.Icon, status.Name);
            }; _kitAbilityPreviewRefresh();
        }

        private void BuildNativeWordingEditor(VisualElement parent, WordingTarget target)
        {
            if (target == null) return;
            _nativeDescriptionSelection = target.Key.StartsWith("skill2", StringComparison.Ordinal) ? "Skill 2" : target.Key.StartsWith("skill1", StringComparison.Ordinal) ? "Skill 1" : target.Key.StartsWith("ultimate", StringComparison.Ordinal) ? "Ultimate" : "Passive";
            var box = Node(parent, "DESCRIPTION · " + target.Name.ToUpperInvariant()); box.name = "studio-native-wording-editor";
            AuthoredDescription Ensure() { var value = target.Get(); if (value == null) { value = new AuthoredDescription { Text = target.Generated() }; target.Set(value); } return value; }
            var modes = new List<string> { "Generated from actions", "Designer authored" };
            var mode = new PopupField<string>("Wording mode", modes, target.Get() == null ? modes[0] : modes[1]) { name = "studio-wording-mode" };
            mode.RegisterValueChangedCallback(e => Edit("Change wording mode", () => { if (e.newValue == modes[0]) target.Set(null); else Ensure(); }, true)); box.Add(mode);
            var tools = Row(box);
            Button(tools, "Keyword", () => WrapSelection("<color=#60A5FA><b>", "</b></color>"));
            Button(tools, "Number", () => WrapSelection("<color=#FBBF24><b>", "</b></color>"));
            Button(tools, "Constellation", () => WrapSelection("<color=#FF6577><b>", "</b></color>"));
            Button(tools, "Bold", () => WrapSelection("<b>", "</b>"));
            Button(tools, "Clear formatting", () => {
                var selected = SelectedDescriptionSource();
                if (selected.Length > 0) InsertSource(Regex.Replace(selected, "<[^>]+>", ""));
                else if (_sourceField != null) _sourceField.value = Regex.Replace(_sourceField.value ?? "", "<[^>]+>", "");
            });
            var source = new TextField("Free edit · text and rich text") { name = "studio-description-source", multiline = true, value = target.Get()?.Text ?? target.Generated(), selectAllOnFocus = false };
            ConfigureWrappingTextField(source, 132);
            source.AddToClassList("studio-ability-wording"); box.Add(source); _sourceField = source;
            _sourceFocused = false; _sourceSelectionStart = _sourceSelectionEnd = 0;
            source.RegisterCallback<FocusInEvent>(_ => { if (_sourceField == source) _sourceFocused = true; }, TrickleDown.TrickleDown);
            source.RegisterCallback<FocusOutEvent>(_ => { if (_sourceField != source) return; _sourceSelectionStart = source.cursorIndex; _sourceSelectionEnd = source.selectIndex; _sourceFocused = false; }, TrickleDown.TrickleDown);
            source.RegisterValueChangedCallback(e => { Edit("Write " + target.Name + " description", () => Ensure().Text = e.newValue); mode.SetValueWithoutNotify(modes[1]); });
            if (_page != "descriptions") Button(box, "Open in Description Studio", () => { _useLegacyDescriptionLibrary = false; ShowWorkspace("descriptions"); });
            var advanced = new Foldout { text = "Highlight rules, linked numbers & keyword notes", value = _page == "descriptions" }; box.Add(advanced);
            box = advanced;
            var color = new ColorField("Selection color") { value = new Color(.38f, .65f, .98f), showAlpha = false }; box.Add(color);
            Button(box, "Color selected words", () => WrapSelection("<color=#" + ColorUtility.ToHtmlStringRGB(color.value) + ">", "</color>"));
            if (!target.Key.StartsWith("ultimate", StringComparison.Ordinal))
            {
                var tier = new SliderInt("Preview constellation", 0, 5) { value = _kitTier, showInputField = true };
                tier.RegisterValueChangedCallback(e => { _kitTier = Mathf.Clamp(e.newValue, 0, 5); tier.SetValueWithoutNotify(_kitTier); RefreshKitSummaries(); }); box.Add(tier);
            }
            var snippets = NativeNumberSnippets(target);
            var number = new PopupField<string>("Linked number", snippets, snippets[0], NativeNumberLabel, NativeNumberLabel); box.Add(number);
            Button(box, "Insert linked number", () => InsertSource("{{" + number.value + "}}"));
            var literal = new FloatField("Fixed number") { value = 15 }; box.Add(literal);
            Button(box, "Insert fixed number", () => InsertSource("{{number:" + literal.value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "}}"));
            var highlightTools = Row(box);
            Button(highlightTools, "+ Highlight selected phrase", () => {
                var selected = SelectedDescriptionSource();
                Edit("Add description highlight", () => Ensure().Highlights.Add(new DescriptionHighlight { Text = string.IsNullOrEmpty(selected) ? "keyword" : selected }), true);
            });
            foreach (var rule in target.Get()?.Highlights ?? new List<DescriptionHighlight>())
            {
                var row = Node(box, "HIGHLIGHT");
                TextControl(row, "Word / phrase", rule.Text, v => rule.Text = v);
                var field = new ColorField("Color") { value = ColorUtility.TryParseHtmlString(rule.Color, out var parsed) ? parsed : Color.white, showAlpha = false };
                EnumControl(row, "Role", rule.Role, v => { rule.Role = (DescriptionHighlightRole)v; rule.Color = rule.Role == DescriptionHighlightRole.Constellation ? "#FF6577" : rule.Role == DescriptionHighlightRole.Number ? "#FBBF24" : "#60A5FA"; if (ColorUtility.TryParseHtmlString(rule.Color, out var next)) field.SetValueWithoutNotify(next); });
                field.RegisterValueChangedCallback(e => Edit("Color description phrase", () => rule.Color = "#" + ColorUtility.ToHtmlStringRGB(e.newValue))); row.Add(field);
                ToggleControl(row, "Bold", rule.Bold, v => rule.Bold = v);
                Button(row, "Remove highlight", () => Edit("Remove description highlight", () => Ensure().Highlights.Remove(rule), true));
            }
            var auto = new Toggle("Automatic word highlights") { value = target.Get()?.AutoHighlight ?? false };
            auto.RegisterValueChangedCallback(e => { Edit("Change automatic highlights", () => Ensure().AutoHighlight = e.newValue); mode.SetValueWithoutNotify(modes[1]); }); box.Add(auto);
            var autoKeywords = new Toggle("Automatic keyword explanations") { value = target.Get()?.AutomaticKeywords ?? true };
            autoKeywords.RegisterValueChangedCallback(e => { Edit("Change keyword explanations", () => Ensure().AutomaticKeywords = e.newValue); mode.SetValueWithoutNotify(modes[1]); }); box.Add(autoKeywords);
            var notes = new TextField("Keyword explanations · free edit") { multiline = true, value = target.Get()?.KeywordNotes ?? "", name = "studio-wording-keyword-notes" };
            ConfigureWrappingTextField(notes, 82);
            notes.AddToClassList("studio-ability-wording"); box.Add(notes);
            notes.RegisterValueChangedCallback(e => { Edit("Write keyword explanations", () => { Ensure().KeywordNotes = e.newValue; Ensure().AutomaticKeywords = false; }); autoKeywords.SetValueWithoutNotify(false); mode.SetValueWithoutNotify(modes[1]); });
            var diagnostics = new Label(); diagnostics.AddToClassList("studio-warning"); box.Add(diagnostics);
            Action refresh = () => {
                var rendered = AuthoredCombatText.Render(target.Get(), target.Effect, _kitTier, target.Passive);
                diagnostics.text = string.Join("\n", Regex.Matches(rendered, @"\{\{[^}]+\}\}").Cast<Match>().Select(m => "Unresolved number token: " + m.Value).Distinct());
            }; _kitAbilityPreviewRefresh += refresh; refresh();
            var reference = new Foldout { text = "Source SO wording", value = false }; box.Add(reference);
            var original = new TextField { multiline = true, isReadOnly = true, value = target.Source() }; ConfigureWrappingTextField(original, 90); original.AddToClassList("studio-ability-wording"); reference.Add(original);
            Button(reference, "Use source wording", () => Edit("Import source wording", () => { var value = Ensure(); value.Text = target.Source(); }, true));
            Button(box, "Copy generated wording", () => Edit("Copy generated wording", () => Ensure().Text = target.Generated(), true));
        }

        private static void ConfigureWrappingTextField(TextField field, float minHeight)
        {
            field.multiline = true;
            field.style.flexDirection = FlexDirection.Column;
            field.style.minWidth = 0;
            field.style.minHeight = minHeight;
            field.style.whiteSpace = WhiteSpace.Normal;
            field.style.alignSelf = Align.Stretch;

            var input = field.Q<VisualElement>("unity-text-input");
            if (input != null)
            {
                input.style.whiteSpace = WhiteSpace.Normal;
                input.style.minWidth = 0;
                input.style.minHeight = Mathf.Max(60, minHeight - 22);
                input.style.flexGrow = 1;
                input.style.flexShrink = 1;
                input.style.overflow = Overflow.Hidden;
            }
            field.Query<TextElement>().ForEach(text =>
            {
                text.style.whiteSpace = WhiteSpace.Normal;
                text.style.minWidth = 0;
            });
            field.Query<ScrollView>().ForEach(scroll =>
                scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden);
        }

        private static List<string> NativeNumberSnippets(WordingTarget target)
        {
            var keys = new List<string> { "tier" };
            void Add(EffectOperationDefinition effect, string prefix)
            {
                if (effect == null) return;
                if (effect.Kind == EffectKind.Damage) { keys.Add(prefix + "damage"); keys.Add(prefix + "hits"); }
                if (effect.Kind == EffectKind.ApplyStatus) { keys.Add(prefix + "duration"); keys.Add(prefix + "chance"); keys.Add(prefix + "stacks"); }
                if (effect.Kind == EffectKind.Heal) keys.Add(prefix + "recovery");
                if (effect.Kind == EffectKind.ChangePowerGauge) keys.Add(prefix + "gauge");
            }
            Add(target.Effect, "");
            for (var i = 0; i < (target.Effect?.Sequence?.Count ?? 0); i++) Add(target.Effect.Sequence[i]?.Effect, "step." + (i + 1) + ".");
            for (var i = 0; i < (target.Passive?.Auras?.Count ?? 0); i++)
                for (var j = 0; j < (target.Passive.Auras[i]?.Modifiers?.Count ?? 0); j++) keys.Add("aura." + (i + 1) + ".modifier." + (j + 1));
            return keys;
        }

        private static string NativeNumberLabel(string key)
        {
            if (key == "tier") return "Constellation tier";
            var parts = key.Split('.');
            if (parts[0] == "aura") return "Aura " + parts[1] + " · Modifier " + parts[3];
            var prefix = parts[0] == "step" ? "Action " + parts[1] + " · " : "Main action · ";
            var value = parts[parts.Length - 1];
            return prefix + (value == "duration" ? "Duration (turns)" : value == "damage" ? "Damage" : value == "recovery" ? "Recovery (%)" : value == "chance" ? "Apply chance (%)" : value == "gauge" ? "Power Gauge" : char.ToUpperInvariant(value[0]) + value.Substring(1));
        }
    }
}
#endif
