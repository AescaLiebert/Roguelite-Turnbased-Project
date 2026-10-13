using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Navigation
{
    /// <summary>Account collection browsing, independent of dungeon and formation state.</summary>
    public static class CharacterInventoryView
    {
        public static void Initialize(VisualElement root)
        {
            var panel = root.Q("character-inventory");
            if (panel == null || panel.userData is Binding) return;
            panel.userData = new Binding(root, panel);
        }

        public static void Show(VisualElement root)
        {
            Initialize(root);
            if (root.Q("character-inventory")?.userData is Binding binding) binding.Show();
        }

        private sealed class Binding
        {
            private readonly VisualElement _root, _panel, _grid, _home;
            private readonly TextField _search;
            private readonly DropdownField _rarity, _attribute, _sort;
            private readonly ScrollView _scroll;
            private string _selectedId, _previousSection;
            private bool _subscribed, _resetting, _open;

            public Binding(VisualElement root, VisualElement panel)
            {
                _root = root; _panel = panel; _home = root.Q("home-screen");
                _panel.style.display = DisplayStyle.None;
                _grid = panel.Q("inventory-grid"); _scroll = panel.Q<ScrollView>("inventory-scroll");
                _search = panel.Q<TextField>("inventory-search");
                _rarity = panel.Q<DropdownField>("inventory-rarity");
                _attribute = panel.Q<DropdownField>("inventory-attribute");
                _sort = panel.Q<DropdownField>("inventory-sort");
                _rarity.choices = new[] { "All" }.Concat(Enum.GetNames(typeof(FighterRarity)).Reverse()).ToList(); _rarity.index = 0;
                _attribute.choices = new[] { "All" }.Concat(Enum.GetNames(typeof(FighterAttribute))).ToList(); _attribute.index = 0;
                _sort.choices = new List<string> { "Rarity", "Name", "Level", "Constellation" }; _sort.index = 0;
                root.Q<Button>("character-inventory-open").clicked += Show;
                panel.Q<Button>("inventory-reset").clicked += Reset;
                _search.RegisterValueChangedCallback(_ => FilterChanged());
                _rarity.RegisterValueChangedCallback(_ => FilterChanged());
                _attribute.RegisterValueChangedCallback(_ => FilterChanged());
                _sort.RegisterValueChangedCallback(_ => FilterChanged());
                panel.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode != KeyCode.Escape) return;
                    Close(); evt.StopPropagation();
                }, TrickleDown.TrickleDown);
                panel.RegisterCallback<AttachToPanelEvent>(_ => Subscribe());
                panel.RegisterCallback<DetachFromPanelEvent>(_ => Unsubscribe());
                if (panel.panel != null) Subscribe();
            }

            public void Show()
            {
                if (_open) return;
                _open = true;
                var section = _root.Q<Label>("section-title");
                _previousSection = section?.text;
                if (section != null) section.text = "CHARACTERS";
                if (_home != null) _home.style.display = DisplayStyle.None;
                AccountHeaderView.OverrideBack(_root, this, Close, "Back to Home");
                ScreenMotion.Show(_panel); Refresh(); _search.Focus();
            }

            private void Close()
            {
                if (!_open) return;
                _open = false; AccountHeaderView.ClearBackOverride(_root, this);
                ScreenMotion.Hide(_panel);
                if (_home != null) _home.style.display = DisplayStyle.Flex;
                SetText(_root, "section-title", _previousSection ?? "HOME");
                _root.Q<Button>("character-inventory-open")?.Focus();
            }

            private void Subscribe()
            {
                if (_subscribed) return;
                PlayerInventoryService.InventoryChanged += Changed; _subscribed = true;
            }
            private void Unsubscribe()
            {
                AccountHeaderView.ClearBackOverride(_root, this);
                if (!_subscribed) return;
                PlayerInventoryService.InventoryChanged -= Changed; _subscribed = false;
            }
            private void Changed()
            {
                if (_open) Refresh();
            }
            private void FilterChanged()
            {
                if (_resetting) return;
                _scroll.scrollOffset = Vector2.zero; Refresh();
            }
            private void Reset()
            {
                _resetting = true;
                _search.SetValueWithoutNotify(string.Empty);
                _rarity.index = 0; _attribute.index = 0; _sort.index = 0;
                _resetting = false; FilterChanged();
            }

            private void Refresh()
            {
                var inventory = PlayerInventoryService.Instance;
                var owned = inventory?.GetOwnedDefinitions().Where(c => c != null).GroupBy(c => c.DefinitionId)
                    .Select(g => g.First()).ToList() ?? new List<CharacterObject>();
                var search = (_search.value ?? string.Empty).Trim();
                var filtered = owned.Where(c => (search.Length == 0 || (c.FighterName ?? string.Empty).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) &&
                    (_rarity.index == 0 || c.FighterRarity.ToString() == _rarity.value) &&
                    (_attribute.index == 0 || c.FighterAttribute.ToString() == _attribute.value));
                var ordered = _sort.index == 1 ? filtered.OrderBy(c => c.FighterName, StringComparer.OrdinalIgnoreCase) :
                    _sort.index == 2 ? filtered.OrderByDescending(c => inventory.FindOwnedByDefinition(c.DefinitionId)?.level ?? 1) :
                    _sort.index == 3 ? filtered.OrderByDescending(c => inventory.FindOwnedByDefinition(c.DefinitionId)?.constellationTier ?? 0) :
                    filtered.OrderByDescending(c => c.FighterRarity);
                var visible = ordered.ThenBy(c => c.FighterName, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.DefinitionId, StringComparer.Ordinal).ToList();
                SetText(_panel, "inventory-count", visible.Count + " / " + owned.Count + " OWNED FIGHTERS");
                CharacterGridView.Populate(_grid, visible, Select, badge: c =>
                {
                    var record = inventory.FindOwnedByDefinition(c.DefinitionId);
                    return "Lv. " + (record?.level ?? 1) + " · C" + (record?.constellationTier ?? 0);
                }, emptyMessage: owned.Count == 0 ? "Your collection is empty. Summon fighters to add them here." : "No owned fighters match these filters.");

            }

            private void Select(CharacterObject character)
            {
                if (character == null) return;
                _selectedId = character.DefinitionId;
                CharacterHubView.Show(_root, character);
            }

            private static void SetText(VisualElement root, string name, string value)
            {
                var label = root.Q<Label>(name);
                if (label != null) label.text = value;
            }
        }
    }
}
