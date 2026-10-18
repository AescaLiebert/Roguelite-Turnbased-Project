using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Presentation.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools
{
    /// <summary>Interactive presentation-only preview using authored assets; no scenes or account data are touched.</summary>
    public sealed class SummonSequencePreviewWindow : EditorWindow
    {
        private SummonSequenceView _sequence;
        private SummonDrawPresentation[] _low, _mixed;
        private SummonDrawPresentation[][] _themes;
        private readonly List<CharacterObject> _fixtureClones = new List<CharacterObject>();
        private int _attributeIndex = 1;
        private Label _status;

        [MenuItem("Fighting Allstar/Preview Summon Sequence")]
        public static void Open()
        {
            var window = GetWindow<SummonSequencePreviewWindow>("Summon Preview");
            window.minSize = new Vector2(480, 480);
            window.Show();
        }

        public void CreateGUI()
        {
            _sequence?.Dispose();
            DisposeFixtures();
            var root = rootVisualElement;
            root.Clear();
            var shell = Resources.Load<StyleSheet>("UI/ScreenShell");
            if (shell != null && !root.styleSheets.Contains(shell)) root.styleSheets.Add(shell);
            root.style.backgroundColor = new Color(.025f, .035f, .055f);
            root.style.color = new Color(.96f, .94f, .9f);
            root.style.flexDirection = FlexDirection.Column;

            var stage = new VisualElement { name = "summon-preview-stage" };
            stage.style.flexGrow = 1;
            stage.style.flexBasis = 0;
            stage.style.minHeight = 0;
            stage.style.position = Position.Relative;
            stage.style.overflow = Overflow.Hidden;
            stage.style.alignItems = Align.Center;
            stage.style.justifyContent = Justify.Center;
            var invitation = new Label("Choose a summon sequence below");
            invitation.style.fontSize = 20;
            invitation.style.color = new Color(.76f, .8f, .86f);
            stage.Add(invitation);
            root.Add(stage);
            _sequence = new SummonSequenceView(stage);

            var footer = new VisualElement { name = "summon-preview-controls" };
            footer.style.flexShrink = 0;
            footer.style.paddingLeft = 12;
            footer.style.paddingRight = 12;
            footer.style.paddingTop = 10;
            footer.style.paddingBottom = 10;
            footer.style.backgroundColor = new Color(.07f, .09f, .13f);
            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            footer.Add(actions);
            AddButton(actions, "R / SR + memory", () => Play(_low, "R / SR + memory"));
            var attributes = new DropdownField("SSR", new List<string> { "Red", "Yellow", "Green", "Blue", "Purple" }, _attributeIndex);
            attributes.style.minWidth = 140;
            attributes.style.marginRight = 8;
            attributes.style.minHeight = 34;
            attributes.RegisterValueChangedCallback(evt => _attributeIndex = attributes.choices.IndexOf(evt.newValue));
            actions.Add(attributes);
            AddButton(actions, "SSR / pity reveal", () => Play(_themes == null ? null : _themes[Mathf.Clamp(_attributeIndex, 0, 4)], "SSR / pity reveal"));
            AddButton(actions, "10 NEW / memory", () => Play(_mixed, "10 NEW / memory"));
            AddButton(actions, "Dungeon items", () =>
            {
                _status.text = "Item obtain preview. Inspect a tile; tap the background to close.";
                _sequence.PlayItems(SummonSequencePreview.CreateItemReceipt(false), () => _status.text = "Item preview complete. No rewards changed.");
            });
            AddButton(actions, "Skip", () => _sequence?.Skip());
            _status = new Label("Preview only. No currency is spent and no rewards are granted.");
            _status.style.whiteSpace = WhiteSpace.Normal;
            _status.style.fontSize = 12;
            _status.style.marginTop = 6;
            footer.Add(_status);
            root.Add(footer);
            LoadFixtures();
        }

        private void LoadFixtures()
        {
            var catalog = CharacterObjectRegistrySO.LoadAll().Where(c => c != null).ToArray();
            CharacterObject Pick(FighterRarity rarity) => catalog.Where(c => c.FighterRarity == rarity)
                .OrderByDescending(c => c.FighterFull != null).ThenBy(c => c.DefinitionId, StringComparer.Ordinal).FirstOrDefault();
            var r = Pick(FighterRarity.R);
            var sr = Pick(FighterRarity.SR);
            var ssr = Pick(FighterRarity.SSR);
            if (r == null || sr == null || ssr == null)
            {
                _status.text = "Preview needs authored R, SR, and SSR characters in the registry.";
                _low = _mixed = null;
                _themes = null;
                return;
            }
            _low = new[] { new SummonDrawPresentation(r, true), new SummonDrawPresentation(sr, false, 1, 5) };
            _mixed = SummonDrawPresentation.Classify(new[] { r, sr, ssr, r, ssr, sr, r, sr, r, sr }, new[] { sr.DefinitionId },
                new Dictionary<string, int> { { sr.DefinitionId, 5 } }).ToArray();
            _themes = new[] { FighterAttribute.Red, FighterAttribute.Yellow, FighterAttribute.Green, FighterAttribute.Blue, FighterAttribute.Purple }
                .Select(attribute =>
                {
                    var copy = Instantiate(ssr);
                    copy.FighterAttribute = attribute;
                    _fixtureClones.Add(copy);
                    return new[] { new SummonDrawPresentation(copy, true) };
                }).ToArray();
        }

        private void Play(SummonDrawPresentation[] draws, string label)
        {
            if (_sequence == null || draws == null) return;
            _status.text = label + " playing. Each SSR waits for a tap, Space, or Enter after settling. Escape skips to rewards.";
            _sequence.Play(draws, () =>
            {
                if (_status != null) _status.text = label + " complete. No currency or rewards changed.";
            });
        }

        private static void AddButton(VisualElement parent, string text, Action clicked)
        {
            var button = new Button(clicked) { text = text };
            button.style.minHeight = 34;
            button.style.minWidth = 90;
            button.style.marginRight = 8;
            button.style.paddingLeft = 12;
            button.style.paddingRight = 12;
            parent.Add(button);
        }

        private void OnDisable()
        {
            _sequence?.Dispose();
            _sequence = null;
            DisposeFixtures();
        }

        private void DisposeFixtures()
        {
            foreach (var clone in _fixtureClones) if (clone != null) DestroyImmediate(clone);
            _fixtureClones.Clear();
        }
    }
}
