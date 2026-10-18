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
            else CloseModal(overlay, overlay.userData as VisualElement);
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
            Action dismiss = () => { if (overlay.userData is PickerPanel picker) picker.Close(); else CloseModal(overlay, returnFocus); };
            overlay.RegisterCallback<ClickEvent>(evt => { evt.StopPropagation(); if (evt.target == overlay) dismiss(); });
            overlay.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Escape) { dismiss(); evt.StopPropagation(); } });
            ScreenMotion.Enter(overlay);
            return overlay;
        }
        private static void CloseModal(VisualElement overlay, VisualElement focus = null)
        {
            if (overlay == null || overlay.name.EndsWith("-closing", StringComparison.Ordinal)) return;
            var originalName = overlay.name; var parent = overlay.parent;
            overlay.name += "-closing";
            ScreenMotion.Exit(overlay, () => { if (parent?.Q(originalName) == null && focus?.panel != null) focus.Focus(); });
        }
        private static void EnterLookup(VisualElement element, int delayMs = 0, int durationMs = 600)
        {
            element.style.opacity = 0; element.style.translate = new Translate(-28, 0);
            ScreenMotion.Animate(element, durationMs, t =>
            {
                var eased = ScreenMotion.EaseOut(t);
                element.style.opacity = eased; element.style.translate = new Translate(-28 * (1 - eased), 0);
            }, () => { element.style.opacity = 1; element.style.translate = new Translate(0, 0); }, delayMs);
        }

        private sealed class Loadout
        {
            private readonly VisualElement _root, _host, _stage, _details, _sets;
            private readonly Label _message;
            private readonly Button[] _slots = new Button[6];
            private CharacterObject _character;
            private string _subject, _selected;
            private string _inspectionKey, _inspectionOwner;
            private int _inspectionSetCount;
            private ArtifactSlot _slot;
            private EquipmentPicker _equipment;
            private string CharacterId => Inventory?.FindOwnedByDefinition(_character.DefinitionId)?.instanceId;
            private void SetMessage(string text) { if (_message != null) _message.text = text ?? string.Empty; }
            public Loadout(VisualElement root, VisualElement host)
            {
                _root = root; _host = host; host.AddToClassList("artifact-loadout");
                if (host.Q("artifact-character-stage") == null) { host.Clear(); Resources.Load<VisualTreeAsset>("UI/ArtifactLoadout").CloneTree(host); }
                _stage = host.Q("artifact-character-stage");
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
                _character = character; Refresh();
            }
            private void Refresh()
            {
                var equipped = Inventory.GetEquippedArtifacts(CharacterId).ToList();
                for (var i = 0; i < 6; i++)
                {
                    var button = _slots[i]; button.Clear(); button.AddToClassList("item-reward-tile"); button.AddToClassList("artifact-item-tile"); ItemRewardView.StandardizeTile(button);
                    for (var rarity = 1; rarity <= 5; rarity++) button.RemoveFromClassList("item-frame-" + rarity);
                    if (i == 5) { var locked = ItemRewardView.EmptyFrame("LOCKED"); locked.Q<Label>(className: "item-empty-icon").text = "◇"; button.Add(locked); button.tooltip = "Holy Relics are not available yet."; SizeSlot(button); continue; }
                    var slot = (ArtifactSlot)i; var piece = equipped.FirstOrDefault(a => a.Slot == slot); button.EnableInClassList("selected", _slot == slot);
                    button.Add(piece != null ? ArtifactItemView.CreateFrame(piece, showOwner: false) : ItemRewardView.EmptyFrame(ArtifactRules.SlotName(slot).ToUpperInvariant()));
                    button.tooltip = piece == null ? "Choose a " + ArtifactRules.SlotName(slot) : ArtifactItemView.Name(piece);
                    SizeSlot(button);
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
                Inspect(); _equipment?.Refresh();
            }
            private void SizeSlot(VisualElement tile)
            {
                var compact = _root.ClassListContains("compact-layout");
                var side = compact ? 124f : 156f; var footer = compact ? 26f : 32f;
                tile.style.width = tile.style.minWidth = side + 4; tile.style.height = tile.style.minHeight = side + footer + 4;
                var frame = tile.Q(className: "item-reward-frame"); frame.style.width = side; frame.style.height = side + footer;
                var icon = frame.Q(className: "artifact-item-icon"); if (icon != null) icon.style.height = side - 4;
                var amount = frame.Q<Label>(className: "item-reward-amount"); amount.style.height = footer; amount.style.fontSize = compact ? 16 : 19;
                var stars = frame.Q<Label>(className: "artifact-rarity-stars"); if (stars != null) { stars.style.bottom = footer; stars.style.fontSize = compact ? 14 : 17; }
            }
            private void SelectSlot(int index)
            {
                if (index == 5) { SetMessage("Holy Relics are not available yet."); return; }
                SetMessage(""); _slot = (ArtifactSlot)index;
                _selected = Inventory.GetEquippedArtifacts(CharacterId).FirstOrDefault(a => a.Slot == _slot)?.InstanceId;
                Refresh(); if (_selected == null) Picker();
            }
            private void Inspect()
            {
                var details = _details;
                var piece = _selected == null ? null : Inventory.FindArtifact(_selected);
                var presentation = piece?.Clone(); if (presentation != null) presentation.EquippedCharacterId = string.Empty;
                var key = CharacterId + ":" + (presentation == null ? "empty:" + _slot : JsonUtility.ToJson(presentation));
                var rebuild = key != _inspectionKey;
                VisualElement content;
                if (rebuild)
                {
                    details.Clear(); for (var rarity = 1; rarity <= 5; rarity++) details.RemoveFromClassList("item-frame-" + rarity);
                    var icon = new VisualElement(); icon.AddToClassList("artifact-tooltip-icon");
                    if (piece != null) { var sprite = ArtifactItemView.Icon(piece); if (sprite != null) icon.style.backgroundImage = new StyleBackground(sprite); }
                    details.AddToClassList("item-frame-" + (int)(piece?.Rarity ?? ArtifactRarity.Common));
                    content = ItemRewardView.CreateTooltipPanels(details, piece == null ? ArtifactRules.SlotName(_slot) : ArtifactItemView.Name(piece),
                        ArtifactRules.SlotName(_slot) + " · Artifact", (int)(piece?.Rarity ?? ArtifactRarity.Common), icon, level: piece?.Level, mainStat: piece?.MainStat);
                    _inspectionKey = key;
                }
                else content = details.Q(className: "item-tooltip-content");
                var preview = details.Q("item-tooltip-preview-panel");
                var actions = preview.Q("artifact-inspection-actions") ?? Box(preview, "artifact-preview-actions", "artifact-inspection-actions"); actions.Clear();
                Button(actions, "CHANGE", Picker, "artifact-button", "artifact-change");
                if (piece == null)
                {
                    content.Clear();
                    details.Q<Label>(className: "item-tooltip-preview-stars").text = "EMPTY SLOT";
                    Label(content, "Choose an artifact for this slot.", "artifact-empty-copy");
                    Button(actions, "UNEQUIP", () => { }, "artifact-button", "artifact-unequip").SetEnabled(false);
                    Button(actions, "ENHANCE  ›", () => { }, "artifact-primary", "artifact-enhance").SetEnabled(false); return;
                }
                var setCount = ArtifactRules.SetCount(Inventory.GetEquippedArtifacts(CharacterId), piece.Set);
                if (rebuild) ArtifactItemView.FillDetails(content, piece, true, CharacterId, showMetadata: false, showMainStat: false,
                    showOwner: piece.EquippedCharacterId != CharacterId);
                else if (_inspectionOwner != piece.EquippedCharacterId || _inspectionSetCount != setCount)
                {
                    // Keep the same artwork, stat rows and scroll view when equipment changes.
                    // Only the live set/ownership sections and action state need updating.
                    var updated = new VisualElement();
                    ArtifactItemView.FillDetails(updated, piece, true, CharacterId, showMetadata: false, showMainStat: false,
                        showOwner: piece.EquippedCharacterId != CharacterId);
                    foreach (var section in new[] { "artifact-set-details", "artifact-detail-ownership" })
                    {
                        content.Q(className: section)?.RemoveFromHierarchy();
                        var replacement = updated.Q(className: section); if (replacement != null) content.Add(replacement);
                    }
                }
                _inspectionOwner = piece.EquippedCharacterId; _inspectionSetCount = setCount;
                var favourite = preview.Q<Button>("artifact-favourite") ?? Button(preview, piece.Favourite ? "★" : "☆", () =>
                { if (!Inventory.TrySetArtifactFavourite(piece.InstanceId, !piece.Favourite, out var error)) SetMessage(error); Refresh(); }, "artifact-preview-favourite", "artifact-favourite");
                favourite.tooltip = piece.Favourite ? "Favourite · protected" : "Mark as favourite to protect from enhancement consumption";
                var equippedHere = piece.EquippedCharacterId == CharacterId;
                var equippedElsewhere = !string.IsNullOrEmpty(piece.EquippedCharacterId) && !equippedHere;
                if (equippedHere) Button(actions, "UNEQUIP", () => { SetMessage(Inventory.TryUnequipArtifact(CharacterId, piece.Slot, out var error) ? "" : error); Refresh(); }, "artifact-button", "artifact-unequip");
                else
                {
                    var swap = equippedElsewhere && Inventory.GetEquippedArtifacts(CharacterId).Any(a => a.Slot == piece.Slot);
                    var equip = Button(actions, swap ? "SWAP & EQUIP" : "EQUIP", () =>
                    { SetMessage(Inventory.TryEquipArtifact(piece.InstanceId, CharacterId, out var error) ? "" : error); Refresh(); }, "artifact-button", "artifact-equip");
                    equip.tooltip = swap ? "Exchange this slot with the fighter currently wearing this artifact." : "Equip this artifact in its matching slot.";
                }
                Button(actions, "ENHANCE  ›", () => { _equipment?.Close(); new Workshop(_root, piece.InstanceId); }, "artifact-primary", "artifact-enhance").SetEnabled(piece.Level < 20);
                if (piece.Level >= 20 && content.Q(className: "artifact-cap-state") == null) Label(content, "MAX ENHANCEMENT · +20", "artifact-cap-state");
            }
            private void Picker()
            {
                if (_equipment != null) return;
                _equipment = new EquipmentPicker(this);
                _equipment.Refresh();
            }

            private sealed class EquipmentPicker
            {
                private readonly Loadout _loadout;
                private readonly VisualElement _panel, _hero, _portrait, _strip;
                private readonly StyleEnum<DisplayStyle> _portraitDisplay, _stageDisplay;
                private readonly PickerPanel _picker;
                private bool _closed;
                public EquipmentPicker(Loadout loadout)
                {
                    _loadout = loadout;
                    _hero = loadout._root.Q(className: "hub-hero");
                    _portrait = loadout._root.Q("hub-portrait"); _portraitDisplay = _portrait.style.display; _stageDisplay = loadout._stage.style.display;
                    _portrait.style.display = DisplayStyle.None; loadout._stage.style.display = DisplayStyle.None;
                    _hero.AddToClassList("artifact-inventory-open");
                    _panel = Box(_hero, "artifact-equipment-inventory", "artifact-equipment-inventory");
                    var pickerHost = Box(_panel, "artifact-equipment-picker-host");
                    var scroll = new ScrollView(ScrollViewMode.Horizontal) { name = "artifact-equipped-scroll", horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Hidden };
                    scroll.AddToClassList("artifact-equipped-scroll"); scroll.AddToClassList("artifact-character-preview"); _panel.Add(scroll);
                    _strip = Box(scroll, "artifact-equipped-strip", "artifact-equipped-strip");
                    _picker = OpenPicker(loadout._root, "ARTIFACT INVENTORY", Inventory.GetItemInventorySnapshot().Artifacts, piece =>
                    { loadout._selected = piece.InstanceId; loadout._slot = piece.Slot; loadout.SetMessage(""); loadout.Refresh(); },
                        loadout._slots[(int)loadout._slot], dock: pickerHost, restore: Close, keepOpen: true, initialSlot: (int)loadout._slot,
                        isSelected: piece => piece.InstanceId == loadout._selected);
                    ScreenMotion.Enter(_panel);
                    AccountHeaderView.OverrideBack(loadout._root, this, Close, "Back to artifacts");
                    _panel.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Escape) { Close(); evt.StopPropagation(); } });
                    _panel.RegisterCallback<DetachFromPanelEvent>(_ => { AccountHeaderView.ClearBackOverride(loadout._root, this); });
                }
                public void Refresh()
                {
                    if (_closed) return;
                    _strip.Clear();
                    var compact = _loadout._root.ClassListContains("compact-layout");
                    var identity = Box(_strip, "artifact-strip-character"); identity.Add(CharacterIconView.Create(_loadout._character, compact ? 64 : 92));
                    identity.tooltip = _loadout._character.FighterName;
                    var equipped = Inventory.GetEquippedArtifacts(_loadout.CharacterId);
                    for (var i = 0; i < 5; i++)
                    {
                        var slot = (ArtifactSlot)i; var piece = equipped.FirstOrDefault(a => a.Slot == slot);
                        var tile = ItemRewardView.EmptyTile(() =>
                        {
                            _loadout._slot = slot; _loadout._selected = Inventory.GetEquippedArtifacts(_loadout.CharacterId).FirstOrDefault(a => a.Slot == slot)?.InstanceId;
                            _picker.SetSlot?.Invoke((int)slot); _loadout.Refresh();
                        }, ArtifactRules.SlotName(slot).ToUpperInvariant(), "artifact-strip-slot-" + i);
                        if (piece != null) { tile.Clear(); tile.Add(ArtifactItemView.CreateFrame(piece, showOwner: false)); }
                        CompactSlot(tile, compact);
                        tile.EnableInClassList("selected", slot == _loadout._slot); tile.tooltip = ArtifactRules.SlotName(slot) + (piece == null ? " · Empty" : " · " + ArtifactItemView.Name(piece));
                        _strip.Add(tile);
                    }
                    _picker.Rebuild?.Invoke();
                }
                private static void CompactSlot(VisualElement tile, bool compact)
                {
                    var width = compact ? 88 : 116;
                    var height = compact ? 108 : 138;
                    tile.style.width = tile.style.minWidth = width; tile.style.height = tile.style.minHeight = height;
                    tile.style.marginLeft = tile.style.marginRight = compact ? 2 : 6;
                    var frame = tile.Q(className: "item-reward-frame"); frame.style.width = width - 4; frame.style.height = height - 4;
                    var icon = frame.Q(className: "artifact-item-icon"); if (icon != null) icon.style.height = height - 28;
                    var footer = frame.Q(className: "item-reward-amount"); footer.style.height = 20;
                }
                public void Close()
                {
                    if (_closed) return; _closed = true;
                    AccountHeaderView.ClearBackOverride(_loadout._root, this); _picker.Close();
                    ScreenMotion.Exit(_panel, () =>
                    {
                        var artifactsActive = _loadout._root.Q<Button>("hub-tab-artifacts")?.ClassListContains("selected") == true;
                        _hero.RemoveFromClassList("artifact-inventory-open"); _portrait.style.display = _portraitDisplay;
                        _loadout._stage.style.display = artifactsActive ? _stageDisplay : DisplayStyle.None;
                        _loadout._equipment = null;
                        if (artifactsActive) { ScreenMotion.Enter(_loadout._stage); _loadout._slots[(int)_loadout._slot].Focus(); }
                    });
                }
            }
        }

        private sealed class PickerPanel
        {
            private readonly VisualElement _root, _panel, _focus;
            private readonly Action _restore;
            private bool _closed;
            public Action Rebuild;
            public Action<int> SetSlot;
            public PickerPanel(VisualElement root, VisualElement panel, VisualElement focus, Action restore) { _root = root; _panel = panel; _focus = focus; _restore = restore; }
            public void Close()
            {
                if (_closed) return; _closed = true; ItemRewardView.CloseTooltip(_root);
                _panel.name += "-closing";
                ScreenMotion.Exit(_panel, () =>
                {
                    _restore?.Invoke();
                    if (_root.Q("artifact-picker-overlay") == null && _focus?.panel != null) _focus.Focus();
                });
            }
        }

        private static void DismissPicker(VisualElement root)
        {
            var panel = root.Q("artifact-picker-overlay");
            if (panel?.userData is PickerPanel picker) picker.Close(); else panel?.RemoveFromHierarchy();
        }

        private static PickerPanel OpenPicker(VisualElement root, string title, IEnumerable<ArtifactInstance> pieces, Action<ArtifactInstance> select, VisualElement focus, HashSet<string> selected = null, bool multiple = false, VisualElement dock = null, Action restore = null, bool keepOpen = false, int initialSlot = -1, Func<ArtifactInstance, bool> isSelected = null)
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
            var sorting = Box(dialog, "artifact-sorting");
            var sort = new DropdownField(new List<string> { "Base EXP", "1 Star", "2 Stars", "3 Stars", "4 Stars", "5 Stars" }, 0) { name = "artifact-picker-sort" };
            sort.tooltip = "Order by enhancement EXP, or place a star grade first."; sorting.Add(sort);
            var descending = true; var slotFilter = initialSlot;
            var direction = Button(sorting, "↓", null, "artifact-sort-direction", "artifact-picker-direction"); direction.tooltip = "Reverse sort order";
            var count = Label(dialog, "", "artifact-eyebrow", "artifact-picker-count");
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "artifact-picker-scroll", horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Hidden }; scroll.AddToClassList("artifact-picker-scroll"); dialog.Add(scroll);
            var grid = Box(scroll, "artifact-picker-grid", "artifact-picker-grid");
            var empty = Label(dialog, "", "artifact-empty", "artifact-picker-empty");
            var animateGrid = true;
            var renderedOrder = "";
            Action rebuild = () =>
            {
                if (overlay.panel == null && root.panel != null) return;
                if (Inventory.SubjectId != subject) { ItemRewardView.CloseTooltip(root); panel.Close(); return; }
                var focused = root.panel?.focusController.focusedElement as VisualElement;
                var focusedTile = focused != null && grid.Contains(focused) ? focused.name : null;
                var byId = Inventory.GetItemInventorySnapshot().Artifacts.ToDictionary(a => a.InstanceId, StringComparer.Ordinal);
                var currentSource = source.Select(a => byId.TryGetValue(a.InstanceId, out var current) ? current : null).Where(a => a != null && (!multiple || (!a.Favourite && string.IsNullOrEmpty(a.EquippedCharacterId)))).ToList();
                var shown = currentSource.Where(a => slotFilter < 0 || (int)a.Slot == slotFilter)
                    .OrderBy(a => sort.index > 0 && (int)a.Rarity == sort.index ? 0 : 1)
                    .ThenBy(a => descending ? -ArtifactRules.FodderXp(a) : ArtifactRules.FodderXp(a))
                    .ThenBy(a => a.InstanceId, StringComparer.Ordinal).ToList();
                count.text = shown.Count + " ARTIFACTS" + (multiple ? "  ·  " + (selected?.Count ?? 0) + " SELECTED" : "");
                empty.text = currentSource.Count == 0 ? "No eligible artifacts. Win dungeon battles to obtain more." : "No artifacts for this slot."; empty.style.display = shown.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
                var order = string.Join("|", shown.Select(a => a.InstanceId + ":" + a.Level + ":" + a.Favourite + ":" + a.EquippedCharacterId));
                if (order == renderedOrder && grid.childCount == shown.Count)
                {
                    foreach (var piece in shown) { var tile = grid.Q<Button>("picker-" + piece.InstanceId); tile?.EnableInClassList("material-selected", selected?.Contains(piece.InstanceId) ?? false); tile?.EnableInClassList("selected", isSelected?.Invoke(piece) ?? false); }
                    return;
                }
                renderedOrder = order; grid.Clear();
                var index = 0;
                foreach (var piece in shown)
                {
                    var tile = ArtifactItemView.CreateTile(root, piece, () => { select(piece); if (multiple) { grid.Q<Button>("picker-" + piece.InstanceId)?.EnableInClassList("material-selected", selected.Contains(piece.InstanceId)); count.text = shown.Count + " ARTIFACTS  ·  " + selected.Count + " SELECTED"; } else if (!keepOpen) panel.Close(); });
                    tile.name = "picker-" + piece.InstanceId; tile.EnableInClassList("material-selected", selected?.Contains(piece.InstanceId) ?? false);
                    tile.EnableInClassList("selected", isSelected?.Invoke(piece) ?? false);
                    var item = Box(grid, "artifact-picker-item"); item.Add(tile);
                    if (multiple)
                    {
                        Button(item, "INSPECT", () => ArtifactItemView.ShowTooltip(root, piece, tile), "artifact-inspect-link");
                    }
                    if (animateGrid) EnterLookup(item, Math.Min(index++, 12) * 28, 450);
                }
                animateGrid = false;
                if (!string.IsNullOrEmpty(focusedTile)) grid.Q<Button>(focusedTile)?.Focus();
            };
            panel.Rebuild = rebuild;
            panel.SetSlot = slot => { slotFilter = slot; animateGrid = true; rebuild(); };
            sort.RegisterValueChangedCallback(_ => { animateGrid = true; renderedOrder = ""; rebuild(); });
            direction.clicked += () => { descending = !descending; direction.text = descending ? "↓" : "↑"; animateGrid = true; renderedOrder = ""; rebuild(); };
            PlayerInventoryService.InventoryChanged += rebuild;
            overlay.RegisterCallback<DetachFromPanelEvent>(_ => PlayerInventoryService.InventoryChanged -= rebuild);
            rebuild(); EnterLookup(dialog); if (dock == null) Button(dialog, multiple ? "DONE" : "CANCEL", panel.Close, "artifact-button"); sort.Focus(); return panel;
        }

                private sealed class Workshop
        {
            private const int MaterialSlots = 15;
            private readonly VisualElement _root, _overlay, _stats, _materials;
            private readonly Label _title, _level, _levelDelta, _xpText, _cost, _note, _result, _balance;
            private readonly ProgressBar _xp, _xpPreview;
            private readonly Button _enhance;
            private readonly DropdownField _autoRarity;
            private readonly string _target, _subject;
            private readonly HashSet<string> _fodder = new HashSet<string>(StringComparer.Ordinal);
            private readonly VisualElement _previousFocus;
            private ArtifactEnhancementPreview _preview;
            private IVisualElementScheduledItem _upgradeAnimation;
            private string _draftKey;
            private bool _closed, _busy;

            public Workshop(VisualElement root, string target)
            {
                if (root.Q("artifact-enhance-overlay")?.userData is Workshop prior) prior.Close();
                _root = root; _target = target; _subject = Inventory.SubjectId; Attach(root); ItemRewardView.CloseTooltip(root);
                _previousFocus = root.panel?.focusController.focusedElement as VisualElement;
                _overlay = Resources.Load<VisualTreeAsset>("UI/ArtifactWorkshop").CloneTree();
                _overlay.name = "artifact-enhance-overlay"; _overlay.focusable = true; _overlay.AddToClassList("artifact-workshop-overlay"); _overlay.userData = this; root.Add(_overlay);
                _title = _overlay.Q<Label>("enhance-title"); _level = _overlay.Q<Label>("enhance-level"); _levelDelta = _overlay.Q<Label>("enhance-level-delta");
                _balance = _overlay.Q<Label>("enhance-balance"); _xpText = _overlay.Q<Label>("enhance-xp-text");
                _xp = _overlay.Q<ProgressBar>("enhance-xp"); _xpPreview = _overlay.Q<ProgressBar>("enhance-xp-preview");
                _stats = _overlay.Q("enhance-stats"); _materials = _overlay.Q("enhance-materials");
                _cost = _overlay.Q<Label>("enhance-cost"); _note = _overlay.Q<Label>("enhance-note"); _result = _overlay.Q<Label>("enhance-result");
                _autoRarity = _overlay.Q<DropdownField>("enhance-auto-rarity"); _enhance = _overlay.Q<Button>("enhance-commit");
                _autoRarity.choices = new List<string> { "1 Star and below", "2 Stars and below", "3 Stars and below", "4 Stars and below", "5 Stars and below" };
                _autoRarity.index = Mathf.Clamp(Inventory.AutoArtifactRarity - 1, 0, 4);
                _autoRarity.RegisterValueChangedCallback(_ =>
                {
                    if (!Inventory.TrySetArtifactAutoRarity(_autoRarity.index + 1, out var error))
                    { _autoRarity.SetValueWithoutNotify(_autoRarity.choices[Mathf.Clamp(Inventory.AutoArtifactRarity - 1, 0, 4)]); _note.text = error; }
                });
                AccountHeaderView.OverrideBack(root, this, Back, "Back to artifacts");
                _overlay.Q<Button>("enhance-auto").clicked += AutoAdd;
                _overlay.Q<Button>("enhance-clear").clicked += () => { if (_busy) return; _fodder.Clear(); _result.text = ""; Refresh(); };
                _enhance.clicked += Confirm;
                _overlay.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode != KeyCode.Escape || _root.Q("artifact-confirm-overlay") != null) return;
                    if (_root.Q("artifact-picker-overlay") != null) DismissPicker(_root); else Close();
                    evt.StopPropagation();
                });
                _overlay.RegisterCallback<DetachFromPanelEvent>(_ =>
                { _closed = true; _upgradeAnimation?.Pause(); PlayerInventoryService.InventoryChanged -= Changed; AccountHeaderView.ClearBackOverride(_root, this); });
                PlayerInventoryService.InventoryChanged += Changed;
                Refresh(); ScreenMotion.Enter(_overlay); _overlay.Focus();
            }
            private void Back()
            {
                var confirmation = _root.Q("artifact-confirm-overlay");
                if (confirmation != null) { CloseModal(confirmation, _enhance); return; }
                if (_root.Q("artifact-picker-overlay") != null) { DismissPicker(_root); return; }
                if (_root.Q("item-tooltip-overlay") != null) { ItemRewardView.CloseTooltip(_root); return; }
                Close();
            }
            public void Close()
            {
                if (_closed) return; _closed = true; _upgradeAnimation?.Pause();
                AccountHeaderView.ClearBackOverride(_root, this); PlayerInventoryService.InventoryChanged -= Changed;
                DismissPicker(_root); CloseModal(_root.Q("artifact-confirm-overlay")); ItemRewardView.CloseTooltip(_root);
                _overlay.name += "-closing";
                ScreenMotion.Exit(_overlay, () =>
                {
                    if (_root.Q("artifact-enhance-overlay") != null) return;
                    if (_previousFocus?.panel != null) _previousFocus.Focus(); else _root.Q<Button>("artifact-enhance")?.Focus();
                });
            }
            private void Changed()
            {
                if (_closed) return;
                if (Inventory.SubjectId != _subject) { Close(); return; }
                if (_busy) return;
                var confirmation = _root.Q("artifact-confirm-overlay");
                if (confirmation != null)
                { CloseModal(confirmation); _result.EnableInClassList("artifact-failed", true); _result.text = "Inventory changed. Review the updated materials before confirming."; }
                Refresh();
            }
            private void Refresh()
            {
                if (_closed || _busy) return;
                if (Inventory.SubjectId != _subject) { Close(); return; }
                var piece = Inventory.FindArtifact(_target); if (piece == null) { Close(); return; }
                _title.text = ArtifactItemView.Name(piece); if (_balance != null) _balance.text = "COINS  " + Inventory.Coins.ToString("N0");
                var artwork = _overlay.Q<Image>("enhance-art"); artwork.sprite = ArtifactItemView.Icon(piece); artwork.scaleMode = ScaleMode.ScaleToFit;
                _overlay.Q<Label>("enhance-art-meta").text = ArtifactRules.SetName(piece.Set) + "  ·  " + new string('★', (int)piece.Rarity);
                var byId = Inventory.GetItemInventorySnapshot().Artifacts.ToDictionary(a => a.InstanceId, StringComparer.Ordinal);
                _fodder.RemoveWhere(id => !byId.TryGetValue(id, out var a) || !ArtifactRules.CanUseAsFodder(a, _target));
                if (_fodder.Count > 0 || string.IsNullOrEmpty(_result.text)) _overlay.RemoveFromClassList("enhance-upgrade-revealed");
                var valid = Inventory.PreviewArtifactEnhancement(_target, 0, _fodder.OrderBy(id => id, StringComparer.Ordinal), out _preview, out var error);
                var after = _preview?.TargetAfter ?? piece;
                var draftKey = JsonUtility.ToJson(after) + "|" + string.Join("|", _fodder.OrderBy(id => id, StringComparer.Ordinal));
                var animate = _draftKey != null && _draftKey != draftKey;
                _draftKey = draftKey;
                _level.text = "+" + piece.Level;
                _levelDelta.text = after.Level != piece.Level ? "+" + after.Level : "";
                _levelDelta.tooltip = "Draft level after enhancement";
                _levelDelta.style.display = after.Level != piece.Level ? DisplayStyle.Flex : DisplayStyle.None;
                if (animate && after.Level != piece.Level) ScreenMotion.Pulse(_levelDelta, false);
                SetExperience(piece, piece.Experience);
                var required = piece.Level >= 20 ? 1 : ArtifactRules.RequiredXp(piece, piece.Level);
                _xpPreview.lowValue = 0; _xpPreview.highValue = 1; _xpPreview.title = "";
                _xpPreview.value = Mathf.Clamp01((float)(piece.Experience + (_preview?.ExperienceGained ?? 0)) / required);
                _xpPreview.style.display = _fodder.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
                if (_preview != null) _xpText.text += "    +" + _preview.ExperienceGained.ToString("N0") + " EXP";
                RenderStats(piece, after, animate);
                if (_preview?.AffixImprovements.Count > 0)
                    Label(_stats, "✦  RANDOM AFFIX  ·  " + _preview.AffixImprovements.Count + " improvement" + (_preview.AffixImprovements.Count > 1 ? "s" : ""),
                        "artifact-affix-draft", "enhance-affix-draft");
                _result.style.display = string.IsNullOrEmpty(_result.text) ? DisplayStyle.None : DisplayStyle.Flex;
                _materials.Clear();
                foreach (var id in _fodder.OrderByDescending(id => byId[id].Rarity).ThenBy(id => id, StringComparer.Ordinal))
                {
                    var artifact = byId[id];
                    var tile = ArtifactItemView.CreateTile(_root, artifact, () => { if (_busy) return; _fodder.Remove(id); _result.text = ""; Refresh(); });
                    tile.AddToClassList("enhance-material-tile"); _materials.Add(tile);
                }
                for (var i = _fodder.Count; i < MaterialSlots; i++)
                {
                    var add = ItemRewardView.EmptyTile(PickMaterials, "—", "enhance-empty-material-" + i);
                    add.tooltip = "Select enhancement materials · " + _fodder.Count + " / " + MaterialSlots;
                    add.SetEnabled(piece.Level < ArtifactRules.MaxLevel); _materials.Add(add);
                }
                if (_root.ClassListContains("compact-layout"))
                    foreach (var tile in _materials.Children())
                    {
                        tile.style.width = tile.style.minWidth = 96; tile.style.height = tile.style.minHeight = 116;
                        var frame = tile.Q(className: "item-reward-frame"); frame.style.width = 92; frame.style.height = 112;
                        var icon = frame.Q(className: "artifact-item-icon"); if (icon != null) icon.style.height = 88;
                        var footer = frame.Q<Label>(className: "item-reward-amount"); footer.style.height = 20; footer.style.fontSize = 13;
                        var stars = frame.Q<Label>(className: "artifact-rarity-stars"); if (stars != null) { stars.style.bottom = 20; stars.style.fontSize = 12; }
                    }
                _cost.text = "REQUIRED COINS  " + (_preview?.CoinCost ?? 0).ToString("N0");
                _note.text = _fodder.Count == 0 && piece.Level < ArtifactRules.MaxLevel ? "Select an empty slot to choose materials." :
                    error ?? (piece.Level >= ArtifactRules.MaxLevel ? "Maximum enhancement reached." : "Review the draft. Materials are consumed after confirmation.");
                if (_preview != null && _preview.ExcessExperience > 0) _note.text += "  " + _preview.ExcessExperience.ToString("N0") + " excess EXP will be lost.";
                SetControls(true);
                _enhance.SetEnabled(valid && piece.Level < ArtifactRules.MaxLevel && _fodder.Count > 0);
                if (_root.Q("artifact-picker-overlay")?.userData is PickerPanel picker)
                { if (piece.Level >= ArtifactRules.MaxLevel) picker.Close(); else picker.Rebuild?.Invoke(); }
            }
            private void SetExperience(ArtifactInstance piece, long experience)
            {
                var maximum = piece.Level >= ArtifactRules.MaxLevel;
                var required = maximum ? 1 : ArtifactRules.RequiredXp(piece, piece.Level);
                _xp.lowValue = 0; _xp.highValue = 1; _xp.value = maximum ? 1 : (float)experience / required; _xp.title = "";
                _xpText.text = maximum ? "MAX LEVEL" : experience.ToString("N0") + " / " + required.ToString("N0") + " EXP";
            }
            private void RenderStats(ArtifactInstance before, ArtifactInstance after, bool animate)
            {
                _stats.Clear(); Comparison(_stats, before.MainStat, after.MainStat, true, -1, animate);
                for (var i = 0; i < before.Substats.Count; i++)
                    Comparison(_stats, before.Substats[i], after.Substats.ElementAtOrDefault(i) ?? before.Substats[i], false, i, animate);
            }
            private static void Comparison(VisualElement parent, ArtifactStatLine before, ArtifactStatLine after, bool main, int index, bool animate)
            {
                var changed = before.Value != after.Value;
                var row = Box(parent, main ? "artifact-main-row" : "artifact-stat-row", "enhance-stat-row-" + index);
                Label(row, ArtifactRules.StatName(before), "artifact-stat-name");
                var values = Box(row, "artifact-comparison-values");
                if (changed)
                {
                    Label(values, ArtifactRules.Format(before), "artifact-stat-value artifact-stat-before");
                    Label(values, "→", "artifact-stat-separator");
                }
                var value = Label(values, ArtifactRules.Format(after), changed ? "artifact-stat-value artifact-stat-after" : "artifact-stat-value artifact-stat-current", "enhance-stat-value-" + index);
                if (changed)
                {
                    var arrow = Label(values, "↑", "artifact-stat-up-arrow");
                    if (animate)
                    {
                        ScreenMotion.Pulse(value, false);
                        ScreenMotion.Animate(arrow, 700, t =>
                        { arrow.style.opacity = ScreenMotion.EaseOut(t); arrow.style.translate = new Translate(0, 12 * (1 - ScreenMotion.EaseOut(t))); });
                    }
                }
            }
            private void PickMaterials()
            {
                if (_busy || _closed) return;
                if (_root.Q("artifact-picker-overlay") != null) { DismissPicker(_root); return; }
                var hero = _overlay.Q("enhance-hero"); hero.style.display = DisplayStyle.None;
                var host = _overlay.Q("enhance-picker-host"); host.style.display = DisplayStyle.Flex;
                _overlay.AddToClassList("material-picker-open");
                var focus = _materials.Q<Button>(name: "enhance-empty-material-" + _fodder.Count) ?? _enhance;
                OpenPicker(_root, "ENHANCEMENT MATERIALS", Inventory.GetItemInventorySnapshot().Artifacts.Where(a => ArtifactRules.CanUseAsFodder(a, _target)), piece =>
                {
                    if (_busy) return;
                    if (_fodder.Contains(piece.InstanceId)) _fodder.Remove(piece.InstanceId);
                    else if (_fodder.Count < MaterialSlots) _fodder.Add(piece.InstanceId);
                    else { _note.text = "All 15 slots are filled. Remove a material to replace it."; return; }
                    _result.text = ""; Refresh();
                }, focus, _fodder, true, host, () =>
                {
                    if (host.Q("artifact-picker-overlay") != null) return;
                    host.style.display = DisplayStyle.None; hero.style.display = DisplayStyle.Flex;
                    _overlay.RemoveFromClassList("material-picker-open"); ScreenMotion.Enter(hero);
                });
            }
            private void AutoAdd()
            {
                if (_busy) return;
                _fodder.Clear(); _result.text = "";
                var target = Inventory.FindArtifact(_target); if (target == null || target.Level >= ArtifactRules.MaxLevel) { Refresh(); return; }
                var remaining = ArtifactRules.RequiredXp(target, target.Level) - target.Experience;
                var candidates = Inventory.GetItemInventorySnapshot().Artifacts.Where(a => ArtifactRules.CanUseAsFodder(a, _target) && (int)a.Rarity <= _autoRarity.index + 1)
                    .OrderBy(a => a.Rarity).ThenBy(a => a.Level).ThenBy(a => a.InstanceId, StringComparer.Ordinal);
                foreach (var piece in candidates) { if (remaining <= 0 || _fodder.Count >= MaterialSlots) break; _fodder.Add(piece.InstanceId); remaining -= ArtifactRules.FodderXp(piece); }
                Refresh(); if (_fodder.Count == 0) _note.text = "No eligible artifacts at this grade. Favourites and equipped artifacts stay protected.";
            }
            private void Confirm()
            {
                if (_busy || _closed) return;
                if (!Inventory.PreviewArtifactEnhancement(_target, 0, _fodder.OrderBy(id => id, StringComparer.Ordinal), out var preview, out var error))
                { _note.text = error ?? "Select enhancement materials."; return; }
                if (_root.Q("artifact-confirm-overlay") != null) return;
                var overlay = Modal(_root, "artifact-confirm-overlay", _enhance); var dialog = Box(overlay, "artifact-confirm-dialog");
                Label(dialog, "CONFIRM ENHANCEMENT", "artifact-title heavy");
                Label(dialog, ArtifactItemView.Name(preview.TargetBefore) + "  +" + preview.TargetBefore.Level +
                    (preview.TargetAfter.Level > preview.TargetBefore.Level ? " → +" + preview.TargetAfter.Level : "  ·  +" + preview.ExperienceGained.ToString("N0") + " EXP"), "artifact-eyebrow");
                Label(dialog, "Consume " + preview.CoinCost.ToString("N0") + " Coins.");
                Label(dialog, "These artifacts will be permanently consumed:", "artifact-warning");
                var scroll = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Hidden };
                scroll.AddToClassList("artifact-confirm-scroll"); dialog.Add(scroll);
                var byId = Inventory.GetItemInventorySnapshot().Artifacts.ToDictionary(a => a.InstanceId, StringComparer.Ordinal);
                foreach (var id in preview.FodderIds)
                {
                    if (!byId.TryGetValue(id, out var piece)) continue;
                    var row = Box(scroll, "artifact-consume-row"); row.Add(ArtifactItemView.CreateTile(_root, piece));
                    Label(row, ArtifactItemView.Name(piece) + " · " + ArtifactItemView.RarityStars(piece) + " · +" + piece.Level, "artifact-copy");
                }
                if (preview.AffixImprovements.Count > 0) Label(dialog, "✦ " + preview.AffixImprovements.Count + " random affix improvement(s).", "artifact-affix-draft");
                if (preview.ExcessExperience > 0) Label(dialog, preview.ExcessExperience.ToString("N0") + " excess EXP will be lost at +20.", "artifact-warning");
                var actions = Box(dialog, "artifact-action-row");
                var cancel = Button(actions, "CANCEL", () => CloseModal(overlay, _enhance));
                Button(actions, "CONFIRM ENHANCE", () => { CloseModal(overlay); Commit(preview); }, "artifact-primary", "artifact-confirm-commit");
                ScreenMotion.Enter(dialog); cancel.Focus();
            }
            private void SetControls(bool enabled)
            {
                _materials.SetEnabled(enabled); _autoRarity.SetEnabled(enabled);
                _overlay.Q<Button>("enhance-auto").SetEnabled(enabled && Inventory.FindArtifact(_target)?.Level < ArtifactRules.MaxLevel);
                _overlay.Q<Button>("enhance-clear").SetEnabled(enabled);
                _enhance.SetEnabled(enabled);
            }
            private void Commit(ArtifactEnhancementPreview reviewed)
            {
                if (_busy || _closed) return;
                if (Inventory.SubjectId != _subject) { Close(); return; }
                _busy = true; SetControls(false);
                if (!Inventory.TryEnhanceArtifact(reviewed, out var result, out var error))
                {
                    _busy = false; _result.EnableInClassList("artifact-failed", true); _result.text = error; Refresh(); return;
                }
                DismissPicker(_root);
                _overlay.AddToClassList("enhance-upgrade-running");
                _xpPreview.style.display = DisplayStyle.None; _levelDelta.style.display = DisplayStyle.None;
                _result.text = ""; _result.style.display = DisplayStyle.None; _note.text = "Enhancing…";
                var displayed = result.TargetBefore.Clone();
                RenderStats(displayed, displayed, false); SetExperience(displayed, displayed.Experience);
                var gained = result.ExperienceGained - result.ExcessExperience;
                var shownLevel = displayed.Level;
                var duration = Mathf.Clamp(900 + (result.TargetAfter.Level - shownLevel) * 150, 900, 3600);
                _upgradeAnimation = ScreenMotion.Animate(_overlay, duration, t =>
                {
                    if (_closed || Inventory.SubjectId != _subject) { Close(); return; }
                    var pending = result.TargetBefore.Experience + (long)(gained * ScreenMotion.EaseOut(t));
                    var level = result.TargetBefore.Level;
                    while (level < ArtifactRules.MaxLevel && pending >= ArtifactRules.RequiredXp(displayed.Rarity, level))
                    { pending -= ArtifactRules.RequiredXp(displayed.Rarity, level); level++; }
                    displayed.Level = level;
                    if (level != shownLevel)
                    {
                        shownLevel = level; _level.text = "+" + level;
                        displayed.MainStat.Value = ArtifactRules.MainValue(displayed);
                        var main = _stats.Q<Label>("enhance-stat-value--1");
                        main.text = ArtifactRules.Format(displayed.MainStat); ScreenMotion.Pulse(_level, false); ScreenMotion.Pulse(main, false);
                    }
                    SetExperience(displayed, level >= ArtifactRules.MaxLevel ? 0 : pending);
                }, () =>
                {
                    if (_closed) return;
                    displayed.Level = result.TargetAfter.Level; displayed.Experience = result.TargetAfter.Experience;
                    displayed.MainStat = result.TargetAfter.MainStat.Clone(); _level.text = "+" + displayed.Level;
                    SetExperience(displayed, displayed.Experience); RenderStats(displayed, displayed, false);
                    _overlay.AddToClassList("enhance-upgrade-revealed");
                    ScreenMotion.Sparkle(_overlay.Q(className: "artifact-xp-heading"));
                    ScreenMotion.Pulse(_level); Feedback.Play(); RevealAffix(result, displayed, 0);
                });
            }
            private void RevealAffix(ArtifactEnhancementPreview result, ArtifactInstance displayed, int index)
            {
                if (_closed) return;
                if (index >= result.AffixImprovements.Count)
                {
                    _upgradeAnimation = ScreenMotion.Animate(_overlay, 700, _ => { }, () =>
                    {
                        if (_closed) return;
                        _busy = false; _fodder.Clear(); _draftKey = null; _overlay.RemoveFromClassList("enhance-upgrade-running");
                        _result.EnableInClassList("artifact-failed", false);
                        _result.text = result.TargetAfter.Level > result.TargetBefore.Level ? "ENHANCEMENT COMPLETE" : "EXP ADDED";
                        Refresh(); ScreenMotion.Enter(_result); _result.Focus();
                    });
                    return;
                }
                var improvement = result.AffixImprovements[index];
                displayed.Substats[improvement.SubstatIndex].Value = improvement.AfterValue;
                var value = _stats.Q<Label>("enhance-stat-value-" + improvement.SubstatIndex);
                value.text = ArtifactRules.Format(displayed.Substats[improvement.SubstatIndex]); value.AddToClassList("artifact-stat-revealed");
                value.schedule.Execute(() => _overlay.Q<ScrollView>(className: "artifact-workshop-scroll")?.ScrollTo(value)).ExecuteLater(1);
                ScreenMotion.Pulse(value);
                var row = _stats.Q("enhance-stat-row-" + improvement.SubstatIndex);
                var arrow = Label(row, "↑", "artifact-stat-up-arrow artifact-commit-arrow");
                ScreenMotion.Animate(arrow, 850, t =>
                { arrow.style.opacity = 1 - t; arrow.style.translate = new Translate(0, -22 * ScreenMotion.EaseOut(t)); }, arrow.RemoveFromHierarchy);
                _note.text = "✦  +" + improvement.Level + "  ·  " + ArtifactRules.StatName(displayed.Substats[improvement.SubstatIndex]) + " improved";
                _upgradeAnimation = ScreenMotion.Animate(_overlay, 750, _ => { }, () => RevealAffix(result, displayed, index + 1));
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
