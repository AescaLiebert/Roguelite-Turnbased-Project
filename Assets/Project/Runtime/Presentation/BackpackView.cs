using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation
{
    public static class BackpackView
    {
        public static void Show(VisualElement root)
        {
            if (root == null || root.Q("item-inventory-overlay") != null) return;
            ItemRewardView.AttachStyle(root);
            var tree = Resources.Load<VisualTreeAsset>("UI/Backpack");
            if (tree == null) { Debug.LogError("Backpack UXML is missing from Resources/UI."); return; }
            var overlay = tree.CloneTree(); overlay.name = "item-inventory-overlay";
            overlay.AddToClassList("backpack-overlay"); root.Add(overlay);
            overlay.userData = new Binding(root, overlay);
        }

        private sealed class Binding
        {
            private readonly VisualElement _root, _overlay, _grid;
            private readonly Label _count, _empty, _note;
            private readonly Button[] _tabs;
            private readonly TextField _search;
            private readonly DropdownField _set, _slot, _rarity;
            private readonly Toggle _favourite;
            private readonly VisualElement _previousFocus;
            private int _tab;
            private bool _closed;

            public Binding(VisualElement root, VisualElement overlay)
            {
                _root = root; _overlay = overlay; _grid = overlay.Q("backpack-grid");
                _count = overlay.Q<Label>("backpack-count"); _empty = overlay.Q<Label>("backpack-empty");
                _note = overlay.Q<Label>("backpack-note");
                _search = overlay.Q<TextField>("backpack-search");
                _set = overlay.Q<DropdownField>("backpack-set-filter");
                _slot = overlay.Q<DropdownField>("backpack-slot-filter");
                _rarity = overlay.Q<DropdownField>("backpack-rarity-filter");
                _favourite = overlay.Q<Toggle>("backpack-favourite-filter");
                Configure(_set, new[] { "All sets" }.Concat(Enum.GetValues(typeof(ArtifactSet)).Cast<ArtifactSet>().Select(ArtifactRules.SetName)));
                Configure(_slot, new[] { "All slots" }.Concat(Enum.GetValues(typeof(ArtifactSlot)).Cast<ArtifactSlot>().Select(ArtifactRules.SlotName)));
                Configure(_rarity, new[] { "All rarities", "Common", "Uncommon", "R", "SR", "SSR" });
                _search?.RegisterValueChangedCallback(_ => FilterChanged());
                _set?.RegisterValueChangedCallback(_ => FilterChanged()); _slot?.RegisterValueChangedCallback(_ => FilterChanged());
                _rarity?.RegisterValueChangedCallback(_ => FilterChanged()); _favourite?.RegisterValueChangedCallback(_ => FilterChanged());
                _previousFocus = root.panel?.focusController.focusedElement as VisualElement;
                _tabs = new[] { "artifact", "material", "dupe" }.Select(id => overlay.Q<Button>("backpack-tab-" + id)).ToArray();
                for (var i = 0; i < _tabs.Length; i++) { var tab = i; _tabs[i].clicked += () => Select(tab); }
                overlay.Q<Button>("backpack-close").clicked += Close;
                overlay.RegisterCallback<ClickEvent>(evt => { evt.StopPropagation(); if (evt.target == overlay) Close(); });
                overlay.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Escape) { Close(); evt.StopPropagation(); } });
                overlay.RegisterCallback<DetachFromPanelEvent>(_ => PlayerInventoryService.InventoryChanged -= Refresh);
                PlayerInventoryService.InventoryChanged += Refresh;
                Select(0); _tabs[0].Focus();
            }

            private void Select(int tab)
            {
                ItemRewardView.CloseTooltip(_root); _tab = tab;
                for (var i = 0; i < _tabs.Length; i++) _tabs[i].EnableInClassList("selected", i == tab);
                foreach (var filter in _overlay.Query<VisualElement>(className: "backpack-artifact-filter").ToList())
                    filter.style.display = tab == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                _overlay.Q<ScrollView>("backpack-scroll").scrollOffset = Vector2.zero;
                Refresh();
            }

            private void Refresh()
            {
                if (_closed) return;
                _grid.Clear();
                var inventory = PlayerInventoryService.Instance?.GetItemInventorySnapshot();
                var count = 0;
                if (_tab == 0 && inventory != null)
                {
                    foreach (var artifact in inventory.Artifacts.Where(a => a != null &&
                        Matches(ArtifactItemView.Name(a) + " " + ArtifactRules.SetName(a.Set) + " " + ArtifactRules.SlotName(a.Slot) + " " + ArtifactItemView.StatText(a.MainStat)) &&
                        (_set == null || _set.index <= 0 || ArtifactRules.SetName(a.Set) == _set.value) &&
                        (_slot == null || _slot.index <= 0 || ArtifactRules.SlotName(a.Slot) == _slot.value) &&
                        (_rarity == null || _rarity.index <= 0 || (int)a.Rarity == _rarity.index) &&
                        (_favourite == null || !_favourite.value || a.Favourite))
                        .OrderByDescending(a => a.Favourite).ThenByDescending(a => !string.IsNullOrEmpty(a.EquippedCharacterId))
                        .ThenByDescending(a => a.Rarity).ThenByDescending(a => a.Level).ThenBy(ArtifactItemView.Name, StringComparer.Ordinal))
                    {
                        var tile = ArtifactItemView.CreateTile(_root, artifact);
                        tile.name = "backpack-artifact-" + artifact.InstanceId; _grid.Add(tile); count++;
                    }
                    _note.text = "Select an artifact to view its rolls and set bonuses. Favourite items are locked against enhancement consumption.";
                }
                else if (_tab == 1 && inventory != null)
                {
                    var items = inventory.Materials.Where(i => i.Amount > 0).Select(i => ItemCatalog.Find(i.ItemId))
                        .Where(i => i != null && Matches(i.Name)).GroupBy(i => i.Id).Select(g => g.First()).OrderByDescending(i => i.ValueRank).ThenBy(i => i.Name);
                    foreach (var item in items) { _grid.Add(ItemRewardView.CreateTile(_root, item, inventory.Amount(item.Id), owned: true)); count++; }
                    _note.text = "MATERIAL · Owned materials only. Select an item to view its use and amount.";
                }
                else if (_tab == 2)
                {
                    var service = PlayerInventoryService.Instance;
                    foreach (var stack in service?.GetCharacterDupeTokens() ?? new List<CharacterDupeTokenStack>())
                    {
                        var name = stack.characterDefinitionId;
                        if (name.StartsWith("fighter.", StringComparison.Ordinal)) name = name.Substring("fighter.".Length);
                        if (name.Length > 0) name = char.ToUpperInvariant(name[0]) + name.Substring(1);
                        var item = ItemCatalog.CreateCharacterDupeToken(stack.characterDefinitionId, name);
                        if (item == null || !Matches(item.Name + " " + stack.characterDefinitionId)) continue;
                        _grid.Add(ItemRewardView.CreateTile(_root, item, stack.amount, owned: true)); count++;
                    }
                    _note.text = "CHARACTER DUPE · Each token is bound to one fighter and can only be used for that fighter.";
                }
                _count.text = _tab == 0 ? count.ToString("N0") + " / " + (inventory?.Artifacts.Count ?? 0).ToString("N0") + " ARTIFACTS" : count.ToString("N0") + " STACKS";
                _empty.text = _tab == 0 ? "No artifacts yet. Win dungeon battles for a chance to obtain one." :
                    _tab == 1 ? "No materials yet. Materials are guaranteed after each dungeon battle victory." : "No character-specific dupe tokens yet.";
                if (count == 0 && (!string.IsNullOrWhiteSpace(_search?.value) || _tab == 0 && ((_set?.index ?? 0) > 0 || (_slot?.index ?? 0) > 0 || (_rarity?.index ?? 0) > 0 || _favourite?.value == true)))
                    _empty.text = "No items match these filters. Clear the search or choose All to see more.";
                _empty.style.display = count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                for (var i = count; i < 24; i++) { var slot = new VisualElement { pickingMode = PickingMode.Ignore }; slot.AddToClassList("backpack-empty-slot"); ItemRewardView.StandardizeTile(slot); _grid.Add(slot); }
            }

            private bool Matches(string text) => string.IsNullOrWhiteSpace(_search?.value) ||
                (text ?? "").IndexOf(_search.value.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;

            private void FilterChanged() { ItemRewardView.CloseTooltip(_root); Refresh(); }

            private static void Configure(DropdownField field, IEnumerable<string> choices)
            {
                if (field == null) return; field.choices = choices.ToList(); field.SetValueWithoutNotify(field.choices[0]); field.index = 0;
            }

            private void Close()
            {
                if (_closed) return; _closed = true;
                ItemRewardView.CloseTooltip(_root); PlayerInventoryService.InventoryChanged -= Refresh;
                _overlay.RemoveFromHierarchy();
                if (_previousFocus?.panel != null) _previousFocus.Focus(); else _root.Q<Button>("backpack")?.Focus();
            }
        }
    }
}
