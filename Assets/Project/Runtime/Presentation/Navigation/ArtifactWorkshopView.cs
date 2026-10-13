using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Navigation
{
    /// <summary>Equipment inspection and an account-backed, preview-first enhancement workshop.</summary>
    public static class ArtifactWorkshopView
    {
        public static void Display(VisualElement root, VisualElement host, CharacterObject character)
        {
            if (host == null || character == null) return;
            Attach(root);
            if (!(host.userData is Loadout)) host.userData = new Loadout(root, host);
            ((Loadout)host.userData).Display(character);
        }

        public static bool CloseOverlays(VisualElement root)
        {
            var overlay = root?.Q("artifact-picker-overlay") ?? root?.Q("artifact-confirm-overlay") ?? root?.Q("artifact-enhance-overlay");
            if (overlay == null) return false;
            if (overlay.userData is Workshop workshop) workshop.Close();
            else if (overlay.userData is PickerPanel picker) picker.Close();
            else { var focus = overlay.userData as VisualElement; overlay.RemoveFromHierarchy(); focus?.Focus(); }
            return true;
        }

        public static void CloseAll(VisualElement root) { while (CloseOverlays(root)) { } }
        private static void Attach(VisualElement root)
        {
            ArtifactItemView.AttachStyle(root);
            var sheet = Resources.Load<StyleSheet>("UI/Artifacts");
            if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
        }
        private static PlayerInventoryService Inventory => PlayerInventoryService.Instance;
        private static Label Label(VisualElement parent, string text, string cls = "artifact-copy", string name = null)
        {
            var label = new Label(text) { name = name }; foreach (var token in cls.Split(' ')) label.AddToClassList(token); parent.Add(label); return label;
        }
        private static VisualElement Box(VisualElement parent, string cls, string name = null)
        {
            var element = new VisualElement { name = name }; element.AddToClassList(cls); parent.Add(element); return element;
        }
        private static Button Button(VisualElement parent, string text, Action action, string cls = "artifact-button", string name = null)
        {
            var button = new Button(action) { text = text, name = name }; button.AddToClassList(cls); parent.Add(button); return button;
        }
        private static Image Art(VisualElement parent, Sprite sprite, string cls)
        {
            var image = new Image { sprite = sprite, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore }; image.AddToClassList(cls); parent.Add(image); return image;
        }
        private static VisualElement Modal(VisualElement root, string name, VisualElement returnFocus)
        {
            Attach(root); var overlay = Box(root, "artifact-modal", name); overlay.userData = returnFocus;
            overlay.RegisterCallback<ClickEvent>(evt => { evt.StopPropagation(); if (evt.target == overlay) { overlay.RemoveFromHierarchy(); returnFocus?.Focus(); } });
            overlay.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Escape) { overlay.RemoveFromHierarchy(); returnFocus?.Focus(); evt.StopPropagation(); } });
            return overlay;
        }

        private sealed class Loadout
        {
            private readonly VisualElement _root, _host, _stage, _details, _sets, _identityIcon;
            private readonly Label _name, _meta, _message;
            private readonly Button[] _slots = new Button[6];
            private CharacterObject _character;
            private string _subject, _selected;
            private ArtifactSlot _slot;
            private string CharacterId => Inventory?.FindOwnedByDefinition(_character.DefinitionId)?.instanceId;
            public Loadout(VisualElement root, VisualElement host)
            {
                _root = root; _host = host; host.AddToClassList("artifact-loadout");
                if (host.Q("artifact-character-stage") == null) { host.Clear(); Resources.Load<VisualTreeAsset>("UI/ArtifactLoadout").CloneTree(host); }
                _name = host.Q<Label>("artifact-fighter-name"); _meta = host.Q<Label>("artifact-fighter-meta");
                _identityIcon = host.Q("artifact-fighter-icon"); _stage = host.Q("artifact-character-stage");
                for (var i = 0; i < 6; i++) { var index = i; _slots[i] = _stage.Q<Button>("artifact-slot-" + i); _slots[i].clicked += () => SelectSlot(index); }
                // Reuse the existing CharacterHub portrait. Only equipment controls join its hero area.
                _stage.RemoveFromHierarchy(); root.Q(className: "hub-hero").Add(_stage);
                root.Q("artifact-equipment-actions")?.RemoveFromHierarchy();
                _sets = host.Q("artifact-active-sets"); _details = host.Q("artifact-detail-panel"); _message = host.Q<Label>("artifact-loadout-message");
                _slot = ArtifactSlot.Flower;
            }
            public void Display(CharacterObject character)
            {
                if (_subject != Inventory.SubjectId) { _subject = Inventory.SubjectId; _selected = null; ArtifactWorkshopView.CloseAll(_root); }
                if (_character != character) { _selected = null; _slot = ArtifactSlot.Flower; }
                _character = character; _name.text = character.FighterName;
                _meta.text = character.FighterRarity + " · " + character.FighterAttribute.ToString().ToUpperInvariant() + "\n" + DungeonProfile.SeriesDisplayName(character.SeriesId);
                _identityIcon.Clear(); _identityIcon.Add(CharacterIconView.Create(character, 84)); Refresh();
            }
            private void Refresh()
            {
                var equipped = Inventory.GetEquippedArtifacts(CharacterId).ToList();
                for (var i = 0; i < 6; i++)
                {
                    var button = _slots[i]; button.Clear(); button.AddToClassList("item-reward-tile"); button.AddToClassList("artifact-item-tile"); ItemRewardView.StandardizeTile(button);
                    for (var rarity = 1; rarity <= 5; rarity++) button.RemoveFromClassList("item-frame-" + rarity);
                    if (i == 5) { var locked = ItemRewardView.EmptyFrame("LOCKED"); locked.Q<Label>(className: "item-empty-icon").text = "◇"; button.Add(locked); button.tooltip = "Holy Relics are not available yet."; continue; }
                    var slot = (ArtifactSlot)i; var piece = equipped.FirstOrDefault(a => a.Slot == slot); button.EnableInClassList("selected", _slot == slot);
                    button.Add(piece != null ? ArtifactItemView.CreateFrame(piece) : ItemRewardView.EmptyFrame(ArtifactRules.SlotName(slot).ToUpperInvariant()));
                    button.tooltip = piece == null ? "Choose a " + ArtifactRules.SlotName(slot) : ArtifactItemView.Name(piece);
                }
                _sets.Clear();
                foreach (ArtifactSet set in Enum.GetValues(typeof(ArtifactSet)))
                {
                    var count = ArtifactRules.SetCount(equipped, set); if (count < 2) continue;
                    var stat = set == ArtifactSet.Striker ? "ATK" : set == ArtifactSet.Bulwark ? "DEF" : "HP";
                    Label(_sets, "✓  " + ArtifactRules.SetName(set) + " · " + stat + " +" + (count >= 4 ? "40" : "20") + "%", "artifact-active-line");
                }
                _sets.style.display = _sets.childCount == 0 ? DisplayStyle.None : DisplayStyle.Flex;
                if (_selected == null) _selected = equipped.FirstOrDefault(a => a.Slot == _slot)?.InstanceId;
                Inspect();
            }
            private void SelectSlot(int index)
            {
                if (index == 5) { _message.text = "Holy Relics are not available yet."; return; }
                _message.text = ""; _slot = (ArtifactSlot)index;
                _selected = Inventory.GetEquippedArtifacts(CharacterId).FirstOrDefault(a => a.Slot == _slot)?.InstanceId;
                Refresh(); if (_selected == null) Picker();
            }
            private void Inspect()
            {
                _details.Clear(); for (var rarity = 1; rarity <= 5; rarity++) _details.RemoveFromClassList("item-frame-" + rarity);
                var piece = _selected == null ? null : Inventory.FindArtifact(_selected);
                var icon = new VisualElement(); icon.AddToClassList("artifact-tooltip-icon");
                if (piece != null) { var sprite = ArtifactItemView.Icon(piece); if (sprite != null) icon.style.backgroundImage = new StyleBackground(sprite); }
                _details.AddToClassList("item-frame-" + (int)(piece?.Rarity ?? ArtifactRarity.Common));
                var content = ItemRewardView.CreateTooltipPanels(_details, piece == null ? ArtifactRules.SlotName(_slot) : ArtifactItemView.Name(piece),
                    ArtifactRules.SlotName(_slot) + " · Artifact", (int)(piece?.Rarity ?? ArtifactRarity.Common), icon, level: piece?.Level, mainStat: piece?.MainStat);
                var preview = _details.Q("item-tooltip-preview-panel"); var actions = Box(preview, "artifact-preview-actions", "artifact-inspection-actions");
                Button(actions, "CHANGE", Picker, "artifact-button", "artifact-change");
                if (piece == null)
                {
                    _details.Q<Label>(className: "item-tooltip-preview-stars").text = "EMPTY SLOT";
                    Label(content, "Choose an artifact for this slot.", "artifact-empty-copy");
                    Button(actions, "UNEQUIP", () => { }, "artifact-button", "artifact-unequip").SetEnabled(false);
                    Button(actions, "ENHANCE  ›", () => { }, "artifact-primary", "artifact-enhance").SetEnabled(false); return;
                }
                ArtifactItemView.FillDetails(content, piece, true, CharacterId, showMetadata: false, showMainStat: false);
                var favourite = Button(preview, piece.Favourite ? "★" : "☆", () =>
                { if (!Inventory.TrySetArtifactFavourite(piece.InstanceId, !piece.Favourite, out var error)) _message.text = error; else Inspect(); }, "artifact-preview-favourite", "artifact-favourite");
                favourite.tooltip = piece.Favourite ? "Favourite · protected" : "Mark as favourite to protect from enhancement consumption";
                var equippedHere = piece.EquippedCharacterId == CharacterId;
                var equippedElsewhere = !string.IsNullOrEmpty(piece.EquippedCharacterId) && !equippedHere;
                if (equippedHere) Button(actions, "UNEQUIP", () => { if (!Inventory.TryUnequipArtifact(CharacterId, piece.Slot, out var error)) _message.text = error; else _selected = null; Refresh(); }, "artifact-button", "artifact-unequip");
                else Button(actions, "EQUIP", () => { if (!Inventory.TryEquipArtifact(piece.InstanceId, CharacterId, out var error)) _message.text = error; Refresh(); }, "artifact-button", "artifact-equip").SetEnabled(!equippedElsewhere);
                Button(actions, "ENHANCE  ›", () => new Workshop(_root, piece.InstanceId), "artifact-primary", "artifact-enhance").SetEnabled(piece.Level < 20);
                if (piece.Level >= 20) Label(content, "MAX ENHANCEMENT · +20", "artifact-cap-state");
            }
            private void Picker()
            {
                OpenPicker(_root, "CHOOSE " + ArtifactRules.SlotName(_slot).ToUpperInvariant(), Inventory.GetItemInventorySnapshot().Artifacts.Where(a => a.Slot == _slot),
                    piece => { _selected = piece.InstanceId; _message.text = ""; Inspect(); }, _slots[(int)_slot]);
            }
        }

        private sealed class PickerPanel
        {
            private readonly VisualElement _root, _panel, _focus;
            private readonly Action _restore;
            public Action Rebuild;
            public PickerPanel(VisualElement root, VisualElement panel, VisualElement focus, Action restore) { _root = root; _panel = panel; _focus = focus; _restore = restore; }
            public void Close() { ItemRewardView.CloseTooltip(_root); _panel.RemoveFromHierarchy(); _restore?.Invoke(); _focus?.Focus(); }
        }

        private static void DismissPicker(VisualElement root)
        {
            var panel = root.Q("artifact-picker-overlay");
            if (panel?.userData is PickerPanel picker) picker.Close(); else panel?.RemoveFromHierarchy();
        }

        private static void OpenPicker(VisualElement root, string title, IEnumerable<ArtifactInstance> pieces, Action<ArtifactInstance> select, VisualElement focus, HashSet<string> selected = null, bool multiple = false, VisualElement dock = null, Action restore = null)
        {
            DismissPicker(root);
            var source = pieces.ToList(); var subject = Inventory.SubjectId;
            var overlay = dock == null ? Modal(root, "artifact-picker-overlay", focus) : Box(dock, "artifact-picker-docked", "artifact-picker-overlay");
            var panel = new PickerPanel(root, overlay, focus, restore); overlay.userData = panel;
            if (dock != null) overlay.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Escape) { panel.Close(); evt.StopPropagation(); } });
            var dialog = Box(overlay, "artifact-picker-dialog");
            var heading = Box(dialog, "artifact-dialog-heading"); Label(heading, title, "artifact-title heavy");
            Button(heading, "✕", panel.Close, "artifact-close", "artifact-picker-close");
            Label(dialog, multiple ? "Select artifacts to consume. Favourites, equipped pieces and the enhancement target are protected." : "Select an artifact to inspect its stats before equipping.");
            var filters = Box(dialog, "artifact-filters");
            var search = new TextField { name = "artifact-picker-search", label = "Search" }; search.AddToClassList("artifact-search"); filters.Add(search);
            search.tooltip = "Search artifact or oath name";
            var sets = new DropdownField(new List<string> { "All oaths", "Striker’s Oath", "Bulwark’s Oath", "Lifebloom’s Oath" }, 0) { name = "artifact-picker-set" }; filters.Add(sets);
            var rarities = new DropdownField(new List<string> { "All rarities", "Common", "Uncommon", "Rare", "SR", "SSR" }, 0) { name = "artifact-picker-rarity" }; filters.Add(rarities);
            var slots = new DropdownField(new List<string> { "All slots", "Flower", "Feather", "Sand Clock", "Goblet", "Crown" }, 0) { name = "artifact-picker-slot" }; filters.Add(slots);
            var count = Label(dialog, "", "artifact-eyebrow", "artifact-picker-count");
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "artifact-picker-scroll", horizontalScrollerVisibility = ScrollerVisibility.Hidden }; scroll.AddToClassList("artifact-picker-scroll"); dialog.Add(scroll);
            var grid = Box(scroll, "artifact-picker-grid", "artifact-picker-grid");
            var empty = Label(dialog, "", "artifact-empty", "artifact-picker-empty");
            Action rebuild = () =>
            {
                if (Inventory.SubjectId != subject) { ItemRewardView.CloseTooltip(root); panel.Close(); return; }
                grid.Clear(); var byId = Inventory.GetItemInventorySnapshot().Artifacts.ToDictionary(a => a.InstanceId, StringComparer.Ordinal);
                var currentSource = source.Select(a => byId.TryGetValue(a.InstanceId, out var current) ? current : null).Where(a => a != null && (!multiple || (!a.Favourite && string.IsNullOrEmpty(a.EquippedCharacterId)))).ToList();
                var shown = currentSource.Where(a => (sets.index == 0 || (int)a.Set == sets.index - 1) && (rarities.index == 0 || (int)a.Rarity == rarities.index) && (slots.index == 0 || (int)a.Slot == slots.index - 1) &&
                    (ArtifactItemView.Name(a) + " " + ArtifactRules.SetName(a.Set)).IndexOf(search.value ?? "", StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderByDescending(a => a.Rarity).ThenByDescending(a => a.Level).ThenBy(a => a.InstanceId, StringComparer.Ordinal).ToList();
                count.text = shown.Count + " ARTIFACTS" + (multiple ? "  ·  " + (selected?.Count ?? 0) + " SELECTED" : "");
                empty.text = currentSource.Count == 0 ? "No eligible artifacts. Win dungeon battles to obtain more." : "No artifacts match these filters."; empty.style.display = shown.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                foreach (var piece in shown)
                {
                    var tile = ArtifactItemView.CreateTile(root, piece, () => { select(piece); if (multiple) { grid.Q<Button>("picker-" + piece.InstanceId)?.EnableInClassList("material-selected", selected.Contains(piece.InstanceId)); count.text = shown.Count + " ARTIFACTS  ·  " + selected.Count + " SELECTED"; } else panel.Close(); });
                    tile.name = "picker-" + piece.InstanceId; tile.EnableInClassList("material-selected", selected?.Contains(piece.InstanceId) ?? false);
                    var item = Box(grid, "artifact-picker-item"); item.Add(tile);
                    if (multiple)
                    {
                        Button(item, "INSPECT", () => ArtifactItemView.ShowTooltip(root, piece, tile), "artifact-inspect-link");
                    }
                }
            };
            panel.Rebuild = rebuild;
            search.RegisterValueChangedCallback(_ => rebuild()); sets.RegisterValueChangedCallback(_ => rebuild()); rarities.RegisterValueChangedCallback(_ => rebuild()); slots.RegisterValueChangedCallback(_ => rebuild());
            PlayerInventoryService.InventoryChanged += rebuild;
            overlay.RegisterCallback<DetachFromPanelEvent>(_ => PlayerInventoryService.InventoryChanged -= rebuild);
            rebuild(); if (dock == null) Button(dialog, multiple ? "DONE" : "CANCEL", panel.Close, "artifact-button"); search.Focus();
        }

        private sealed class Workshop
        {
            private readonly VisualElement _root, _overlay, _stats, _materials;
            private readonly Label _title, _level, _xpText, _cost, _note, _result, _balance;
            private readonly ProgressBar _xp;
            private readonly Button _enhance;
            private readonly DropdownField _autoRarity;
            private readonly string _target, _subject;
            private readonly HashSet<string> _fodder = new HashSet<string>(StringComparer.Ordinal);
            private readonly VisualElement _previousFocus;
            private ArtifactEnhancementPreview _preview;
            private bool _closed, _busy;
            public Workshop(VisualElement root, string target)
            {
                root.Q("artifact-enhance-overlay")?.RemoveFromHierarchy(); _root = root; _target = target; _subject = Inventory.SubjectId; Attach(root); ItemRewardView.CloseTooltip(root);
                _previousFocus = root.panel?.focusController.focusedElement as VisualElement;
                var tree = Resources.Load<VisualTreeAsset>("UI/ArtifactWorkshop");
                _overlay = tree.CloneTree(); _overlay.name = "artifact-enhance-overlay"; _overlay.AddToClassList("artifact-workshop-overlay"); _overlay.userData = this; root.Add(_overlay);
                _title = _overlay.Q<Label>("enhance-title"); _level = _overlay.Q<Label>("enhance-level"); _balance = _overlay.Q<Label>("enhance-balance");
                _xpText = _overlay.Q<Label>("enhance-xp-text"); _xp = _overlay.Q<ProgressBar>("enhance-xp"); _stats = _overlay.Q("enhance-stats"); _materials = _overlay.Q("enhance-materials");
                _cost = _overlay.Q<Label>("enhance-cost"); _note = _overlay.Q<Label>("enhance-note"); _result = _overlay.Q<Label>("enhance-result");
                _autoRarity = _overlay.Q<DropdownField>("enhance-auto-rarity"); _enhance = _overlay.Q<Button>("enhance-commit");
                _autoRarity.choices = new List<string> { "Common and below", "Uncommon and below", "Rare and below", "SR and below", "SSR and below" }; _autoRarity.index = Mathf.Clamp(Inventory.AutoArtifactRarity - 1, 0, 4);
                _autoRarity.RegisterValueChangedCallback(_ => { if (!Inventory.TrySetArtifactAutoRarity(_autoRarity.index + 1, out var error)) { _autoRarity.SetValueWithoutNotify(_autoRarity.choices[Mathf.Clamp(Inventory.AutoArtifactRarity - 1, 0, 4)]); _note.text = error; } });
                AccountHeaderView.OverrideBack(root, this, Back, "Back to artifacts");
                _overlay.Q<Button>("enhance-add").clicked += PickMaterials;
                _overlay.Q<Button>("enhance-auto").clicked += AutoAdd;
                _overlay.Q<Button>("enhance-clear").clicked += () => { _fodder.Clear(); _result.text = ""; Refresh(); };
                _enhance.clicked += Confirm;
                _overlay.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode != KeyCode.Escape || _root.Q("artifact-confirm-overlay") != null) return;
                    if (_root.Q("artifact-picker-overlay") != null) DismissPicker(_root); else Close();
                    evt.StopPropagation();
                });
                _overlay.RegisterCallback<DetachFromPanelEvent>(_ => { PlayerInventoryService.InventoryChanged -= Changed; AccountHeaderView.ClearBackOverride(_root, this); });
                PlayerInventoryService.InventoryChanged += Changed; Refresh(); _root.Q<Button>("account-back")?.Focus();
            }
            private void Back()
            {
                var confirmation = _root.Q("artifact-confirm-overlay");
                if (confirmation != null) { confirmation.RemoveFromHierarchy(); return; }
                if (_root.Q("artifact-picker-overlay") != null) { DismissPicker(_root); return; }
                if (_root.Q("item-tooltip-overlay") != null) { ItemRewardView.CloseTooltip(_root); return; }
                Close();
            }
            public void Close()
            {
                if (_closed) return; _closed = true; AccountHeaderView.ClearBackOverride(_root, this); PlayerInventoryService.InventoryChanged -= Changed;
                DismissPicker(_root); _root.Q("artifact-confirm-overlay")?.RemoveFromHierarchy(); ItemRewardView.CloseTooltip(_root);
                _overlay.RemoveFromHierarchy(); if (_previousFocus?.panel != null) _previousFocus.Focus(); else _root.Q<Button>("artifact-enhance")?.Focus();
            }
            private void Changed()
            {
                if (_closed || _busy) return;
                if (Inventory.SubjectId != _subject) { Close(); return; }
                var confirmation = _root.Q("artifact-confirm-overlay");
                if (confirmation != null) { confirmation.RemoveFromHierarchy(); _result.EnableInClassList("artifact-failed", true); _result.text = "Inventory changed. Review the updated materials before confirming."; }
                Refresh();
            }
            private void Refresh()
            {
                if (Inventory.SubjectId != _subject) { Close(); return; }
                var piece = Inventory.FindArtifact(_target); if (piece == null) { Close(); return; }
                _title.text = ArtifactItemView.Name(piece); _balance.text = "COINS  " + Inventory.Coins.ToString("N0");
                var artwork = _overlay.Q<Image>("enhance-art"); artwork.sprite = ArtifactItemView.Icon(piece); artwork.scaleMode = ScaleMode.ScaleToFit;
                _overlay.Q<Label>("enhance-art-meta").text = ArtifactRules.SetName(piece.Set) + "  ·  " + new string('★', (int)piece.Rarity);
                var byId = Inventory.GetItemInventorySnapshot().Artifacts.ToDictionary(a => a.InstanceId, StringComparer.Ordinal);
                _fodder.RemoveWhere(id => !byId.TryGetValue(id, out var a) || a.Favourite || !string.IsNullOrEmpty(a.EquippedCharacterId) || id == _target);
                var valid = Inventory.PreviewArtifactEnhancement(_target, 0, _fodder.OrderBy(id => id, StringComparer.Ordinal), out _preview, out var error);
                var after = _preview?.TargetAfter ?? piece;
                _level.text = "+" + piece.Level + (after.Level != piece.Level ? "  →  +" + after.Level : "  / 20");
                var required = piece.Level >= 20 ? 0 : ArtifactRules.RequiredXp(piece.Rarity, piece.Level);
                _xp.lowValue = 0; _xp.highValue = Math.Max(1, required); _xp.value = Math.Min(_xp.highValue, piece.Experience + (_preview?.ExperienceGained ?? 0)); _xp.title = "";
                _xpText.text = piece.Level >= 20 ? "MAX LEVEL" : piece.Experience.ToString("N0") + " / " + required.ToString("N0") + " XP" + (_preview != null ? "   + " + _preview.ExperienceGained.ToString("N0") + " SELECTED" : "");
                _stats.Clear(); Comparison(_stats, piece.MainStat, after.MainStat, true);
                for (var i = 0; i < piece.Substats.Count; i++) Comparison(_stats, piece.Substats[i], after.Substats.ElementAtOrDefault(i) ?? piece.Substats[i], false);
                _result.style.display = string.IsNullOrEmpty(_result.text) ? DisplayStyle.None : DisplayStyle.Flex;
                _materials.Clear();
                foreach (var id in _fodder.OrderByDescending(id => byId[id].Rarity).ThenBy(id => id, StringComparer.Ordinal))
                {
                    if (!byId.TryGetValue(id, out var artifact)) continue;
                    var tile = ArtifactItemView.CreateTile(_root, artifact, () => { _fodder.Remove(id); Refresh(); }); tile.AddToClassList("enhance-material-tile"); _materials.Add(tile);
                }
                var add = ItemRewardView.EmptyTile(PickMaterials, "—", "enhance-empty-material"); add.tooltip = "Select enhancement materials"; add.SetEnabled(piece.Level < 20); _materials.Add(add);
                _cost.text = "REQUIRED COINS  " + (_preview?.CoinCost ?? 0).ToString("N0");
                _note.text = _fodder.Count == 0 && piece.Level < 20 ? "Select artifacts to enhance." : error ?? (piece.Level >= 20 ? "Maximum enhancement reached." : "Materials are consumed only after you review and confirm enhancement.");
                if (_preview != null && _preview.ExcessExperience > 0) _note.text += "  " + _preview.ExcessExperience.ToString("N0") + " XP exceeds +20 and will be lost.";
                _enhance.SetEnabled(valid && !_busy && piece.Level < 20 && _fodder.Count > 0);
                _overlay.Q<Button>("enhance-add").SetEnabled(piece.Level < 20); _overlay.Q<Button>("enhance-auto").SetEnabled(piece.Level < 20);
                if (_root.Q("artifact-picker-overlay")?.userData is PickerPanel picker)
                { if (piece.Level >= 20) picker.Close(); else picker.Rebuild?.Invoke(); }
            }
            private static void Comparison(VisualElement parent, ArtifactStatLine before, ArtifactStatLine after, bool main)
            {
                var row = Box(parent, main ? "artifact-main-row" : "artifact-stat-row"); Label(row, ArtifactRules.StatName(before), "artifact-stat-name");
                Label(row, ArtifactRules.Format(before) + (before.Value != after.Value ? "  →  " + ArtifactRules.Format(after) : ""), before.Value != after.Value ? "artifact-stat-gain" : "artifact-stat-value");
            }
            private void PickMaterials()
            {
                if (_root.Q("artifact-picker-overlay") != null) { DismissPicker(_root); return; }
                var hero = _overlay.Q("enhance-hero"); hero.style.display = DisplayStyle.None;
                _overlay.AddToClassList("material-picker-open");
                OpenPicker(_root, "ENHANCEMENT MATERIALS", Inventory.GetItemInventorySnapshot().Artifacts.Where(a => a.InstanceId != _target && !a.Favourite && string.IsNullOrEmpty(a.EquippedCharacterId)), piece =>
                { if (!_fodder.Add(piece.InstanceId)) _fodder.Remove(piece.InstanceId); _result.text = ""; Refresh(); }, _overlay.Q("enhance-add"), _fodder, true, _overlay.Q("enhance-picker-host"), () =>
                { hero.style.display = DisplayStyle.Flex; _overlay.RemoveFromClassList("material-picker-open"); });
            }
            private void AutoAdd()
            {
                _fodder.Clear(); _result.text = "";
                var target = Inventory.FindArtifact(_target); if (target == null || target.Level >= 20) { Refresh(); return; }
                var remaining = ArtifactRules.RequiredXp(target.Rarity, target.Level) - target.Experience;
                var candidates = Inventory.GetItemInventorySnapshot().Artifacts.Where(a => a.InstanceId != _target && !a.Favourite && string.IsNullOrEmpty(a.EquippedCharacterId) && (int)a.Rarity <= _autoRarity.index + 1)
                    .OrderBy(a => a.Rarity).ThenBy(a => a.Level).ThenBy(a => a.InstanceId, StringComparer.Ordinal);
                foreach (var piece in candidates) { if (remaining <= 0) break; _fodder.Add(piece.InstanceId); remaining -= ArtifactRules.FodderXp(piece); }
                Refresh(); if (_fodder.Count == 0) _note.text = "No eligible artifacts at this rarity. Favourites and equipped artifacts stay protected.";
            }
            private void Confirm()
            {
                if (_busy) return;
                if (!Inventory.PreviewArtifactEnhancement(_target, 0, _fodder.OrderBy(id => id, StringComparer.Ordinal), out var preview, out var error)) { _note.text = error ?? "Select enhancement materials."; return; }
                var overlay = Modal(_root, "artifact-confirm-overlay", _enhance); var dialog = Box(overlay, "artifact-confirm-dialog");
                Label(dialog, "CONFIRM ENHANCEMENT", "artifact-title heavy");
                Label(dialog, ArtifactItemView.Name(preview.TargetBefore) + (preview.TargetAfter.Level > preview.TargetBefore.Level ? "  +" + preview.TargetBefore.Level + " → +" + preview.TargetAfter.Level : "  +" + preview.TargetBefore.Level + " · XP " + preview.TargetBefore.Experience + " → " + preview.TargetAfter.Experience + " / " + ArtifactRules.RequiredXp(preview.TargetBefore.Rarity, preview.TargetBefore.Level)), "artifact-eyebrow");
                Label(dialog, "Consume " + preview.CoinCost.ToString("N0") + " Coins.");
                if (_fodder.Count > 0)
                {
                    Label(dialog, "These exact artifacts will be permanently consumed:", "artifact-warning");
                    var scroll = new ScrollView(ScrollViewMode.Vertical); scroll.AddToClassList("artifact-confirm-scroll"); dialog.Add(scroll);
                    var byId = Inventory.GetItemInventorySnapshot().Artifacts.ToDictionary(a => a.InstanceId, StringComparer.Ordinal);
                    foreach (var id in preview.FodderIds)
                    {
                        if (!byId.TryGetValue(id, out var piece)) continue;
                        var row = Box(scroll, "artifact-consume-row"); row.Add(ArtifactItemView.CreateTile(_root, piece));
                        Label(row, ArtifactItemView.Name(piece) + " · " + ArtifactItemView.RarityStars(piece) + " · +" + piece.Level + "\n" + ArtifactRules.StatName(piece.MainStat) + " " + ArtifactRules.Format(piece.MainStat), "artifact-copy");
                    }
                }
                if (preview.ExcessExperience > 0) Label(dialog, preview.ExcessExperience.ToString("N0") + " excess XP will be lost at +20.", "artifact-warning");
                var actions = Box(dialog, "artifact-action-row"); Button(actions, "CANCEL", () => { overlay.RemoveFromHierarchy(); _enhance.Focus(); });
                Button(actions, "CONFIRM ENHANCE", () => { overlay.RemoveFromHierarchy(); Commit(preview); }, "artifact-primary", "artifact-confirm-commit");
            }
            private void Commit(ArtifactEnhancementPreview reviewed)
            {
                if (_busy) return; _busy = true;
                _result.EnableInClassList("artifact-failed", true);
                try
                {
                    if (Inventory.SubjectId != _subject) { Close(); return; }
                    if (!Inventory.PreviewArtifactEnhancement(_target, reviewed.DustAmount, reviewed.FodderIds, out var current, out var previewError))
                    { _result.text = previewError; return; }
                    if (JsonUtility.ToJson(current) != JsonUtility.ToJson(reviewed))
                    { _result.text = "The artifact or selected materials changed. Review enhancement again."; return; }
                    if (Inventory.TryEnhanceArtifact(_target, reviewed.DustAmount, reviewed.FodderIds, out var result, out var error))
                    {
                        _result.EnableInClassList("artifact-failed", false);
                        _fodder.Clear();
                        _result.text = result.TargetAfter.Level > result.TargetBefore.Level ? "ENHANCEMENT COMPLETE  ·  +" + result.TargetBefore.Level + " → +" + result.TargetAfter.Level + "\n" + ArtifactRules.StatName(result.TargetAfter.MainStat) + "  " + ArtifactRules.Format(result.TargetBefore.MainStat) + " → " + ArtifactRules.Format(result.TargetAfter.MainStat) :
                            "XP ADDED  ·  +" + result.ExperienceGained.ToString("N0") + "\n" + result.TargetAfter.Experience.ToString("N0") + " / " + ArtifactRules.RequiredXp(result.TargetAfter.Rarity, result.TargetAfter.Level).ToString("N0") + " XP toward +" + (result.TargetAfter.Level + 1);
                        Feedback.Play();
                    }
                    else _result.text = error;
                }
                finally { _busy = false; Refresh(); _result.Focus(); }
            }
        }

        // Small procedural confirmation cue; no scene or imported audio dependency.
        private static class Feedback
        {
            private static AudioClip _clip;
            public static void Play()
            {
                if (!Application.isPlaying) return;
                if (_clip == null)
                {
                    const int rate = 22050; var samples = new float[6615];
                    for (var i = 0; i < samples.Length; i++) { var t = i / (float)rate; samples[i] = .15f * Mathf.Sin(2 * Mathf.PI * (t < .12f ? 660 : 880) * t) * Mathf.Pow(1 - i / (float)samples.Length, 2); }
                    _clip = AudioClip.Create("Artifact enhanced", samples.Length, 1, rate, false); _clip.SetData(samples, 0);
                }
                var go = new GameObject("Artifact confirmation audio") { hideFlags = HideFlags.HideInHierarchy }; var source = go.AddComponent<AudioSource>(); source.spatialBlend = 0; source.clip = _clip; source.Play(); UnityEngine.Object.Destroy(go, 1);
            }
        }
    }
}
