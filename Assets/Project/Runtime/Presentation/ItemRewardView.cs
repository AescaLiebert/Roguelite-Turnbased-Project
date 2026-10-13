using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation
{
    /// <summary>Reward and backpack tiles share rarity frames and a compact, dismissible item tooltip.</summary>
    public static class ItemRewardView
    {
        private static ItemIconArtSO _itemArt;

        public static void Populate(VisualElement grid, IEnumerable<ItemDefinition> items, VisualElement root,
            DungeonRewardReceipt received = null, bool showOwned = false, Func<ItemDefinition, string> rule = null,
            ArtifactRarity? previewArtifactRarity = null, bool previewOnly = false, IEnumerable<ArtifactSet> previewArtifactSets = null)
        {
            if (grid == null || root == null) return;
            AttachStyle(root); grid.Clear(); grid.AddToClassList("item-reward-grid");
            grid.EnableInClassList("dungeon-reward-preview", previewOnly);
            var entries = new List<(ItemDefinition item, ArtifactInstance artifact)>();
            foreach (var item in (items ?? Enumerable.Empty<ItemDefinition>()).Where(i => i != null))
            {
                if (received != null && item.Kind == ItemKind.Artifact) continue;
                var display = item;
                if (item.Kind == ItemKind.Artifact && previewArtifactRarity.HasValue)
                    display = new ItemDefinition { Id = item.Id, Name = item.Name, Description = item.Description,
                        Kind = item.Kind, Tier = (int)previewArtifactRarity.Value, IconColor = item.IconColor };
                entries.Add((display, null));
            }
            if (received?.Artifacts != null)
                foreach (var artifact in received.Artifacts.Where(a => a != null))
                {
                    var item = ItemCatalog.Find(artifact.ItemId);
                    if (item != null) entries.Add((item, artifact));
                }
            foreach (var entry in entries.OrderByDescending(e => e.artifact != null ? (int)e.artifact.Rarity : e.item.FrameTier)
                .ThenBy(e => DungeonRewards.RewardOrder(e.item.Kind)).ThenBy(e => e.item.Name, StringComparer.Ordinal)
                .ThenBy(e => e.artifact?.InstanceId, StringComparer.Ordinal))
            {
                if (entry.artifact != null) { grid.Add(ArtifactItemView.CreateTile(root, entry.artifact)); continue; }
                long? amount = showOwned ? PlayerInventoryService.Instance?.GetItemAmount(entry.item.Id) ?? 0 :
                    received == null ? (long?)null : received.Amount(entry.item.Id);
                grid.Add(CreateTile(root, entry.item, amount, rule?.Invoke(entry.item), showOwned, previewOnly, previewArtifactSets));
            }
        }

        public static Button CreateTile(VisualElement root, ItemDefinition item, long? amount = null, string rule = null, bool owned = false, bool previewOnly = false, IEnumerable<ArtifactSet> previewArtifactSets = null, string footerText = null)
        {
            Button tile = null;
            tile = new Button(() => Show(root, item, owned ? null : amount, tile, rule, previewOnly, previewArtifactSets)) { name = "item-" + item.Id };
            tile.AddToClassList("item-reward-tile"); StandardizeTile(tile);
            tile.AddToClassList("item-kind-" + item.Kind.ToString().ToLowerInvariant());
            var frame = Frame(item); frame.Add(Icon(item));
            if (item.Kind == ItemKind.Artifact)
            {
                tile.AddToClassList("artifact-item-tile");
                var preview = new ArtifactInstance { Rarity = (ArtifactRarity)item.Tier };
                var stars = new Label(ArtifactItemView.RarityStars(preview)) { pickingMode = PickingMode.Ignore };
                stars.AddToClassList("artifact-rarity-stars"); stars.AddToClassList(ArtifactItemView.RarityClass(preview)); frame.Add(stars);
                if (previewOnly) { tile.AddToClassList("artifact-preview-tile"); AddQuantityFooter(frame, ""); }
                else AddQuantityFooter(frame, "+0");
                stars.RemoveFromHierarchy(); frame.Add(stars);
            }
            else AddQuantityFooter(frame, footerText ?? (amount.HasValue ? amount.Value.ToString("N0") : ""));
            if (item.Kind != ItemKind.Artifact)
            {
                var tier = new Label(item.Kind == ItemKind.PhaseMaterial ? "P" + item.Tier : item.Kind == ItemKind.SubPhaseMaterial ? "S" + (item.Tier + 1) : "");
                tier.AddToClassList("item-reward-tier"); frame.Add(tier);
            }
            tile.Add(frame);
            if (item.Kind != ItemKind.Artifact) { var label = new Label(item.Name); label.AddToClassList("item-reward-name"); tile.Add(label); }
            // The custom tooltip supplies the same information for mouse, keyboard activation and touch.
            tile.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Escape) CloseTooltip(root); });
            return tile;
        }

        public static Label AddQuantityFooter(VisualElement frame, string text)
        {
            frame.AddToClassList("has-item-footer");
            var label = new Label(text) { pickingMode = PickingMode.Ignore }; label.AddToClassList("item-reward-amount");
            label.style.position = Position.Absolute; label.style.left = label.style.right = label.style.bottom = 0; label.style.height = FooterHeight;
            label.style.marginTop = label.style.marginBottom = label.style.marginLeft = label.style.marginRight = 0;
            frame.Add(label); return label;
        }

        public const int TileSide = 96, FooterHeight = 20;
        internal static void StandardizeTile(VisualElement tile)
        {
            tile.style.width = TileSide + 4; tile.style.height = TileSide + FooterHeight + 4; tile.style.minWidth = TileSide + 4; tile.style.minHeight = TileSide + FooterHeight + 4;
            tile.style.flexBasis = StyleKeyword.Auto; tile.style.flexGrow = 0; tile.style.flexShrink = 0;
            tile.style.borderTopWidth = tile.style.borderBottomWidth = tile.style.borderLeftWidth = tile.style.borderRightWidth = 0;
            tile.style.paddingTop = tile.style.paddingBottom = tile.style.paddingLeft = tile.style.paddingRight = 2;
            tile.style.marginTop = tile.style.marginBottom = tile.style.marginLeft = tile.style.marginRight = 2;
        }
        internal static VisualElement CreateFrame(int rarity)
        {
            var frame = new VisualElement { pickingMode = PickingMode.Ignore }; frame.AddToClassList("item-reward-frame"); frame.AddToClassList("item-frame-" + rarity);
            frame.style.width = TileSide; frame.style.height = TileSide + FooterHeight; return frame;
        }
        internal static void FullTileIcon(VisualElement icon)
        {
            icon.style.position = Position.Absolute; icon.style.left = icon.style.right = icon.style.top = 0; icon.style.bottom = StyleKeyword.Auto;
            icon.style.width = StyleKeyword.Auto; icon.style.height = TileSide - 4;
        }
        public static Button EmptyTile(Action click, string caption = "", string name = null)
        {
            var tile = new Button(click) { name = name }; tile.AddToClassList("item-reward-tile"); StandardizeTile(tile);
            tile.Add(EmptyFrame(caption)); return tile;
        }
        internal static VisualElement EmptyFrame(string caption = "")
        {
            var frame = CreateFrame(1); var plus = new Label("+") { pickingMode = PickingMode.Ignore }; plus.AddToClassList("item-empty-icon"); frame.Add(plus);
            AddQuantityFooter(frame, caption); return frame;
        }
        private static VisualElement Frame(ItemDefinition item)
        {
            var frame = CreateFrame(item.FrameTier); if (item.PremiumFrame) frame.AddToClassList("item-premium-frame"); return frame;
        }

        public static void CloseTooltip(VisualElement root)
        {
            var overlay = root?.Q("item-tooltip-overlay");
            if (overlay?.userData is Action close) close(); else overlay?.RemoveFromHierarchy();
        }

        public static void Show(VisualElement root, ItemDefinition item, long? received = null, VisualElement anchor = null, string rule = null, bool previewOnly = false, IEnumerable<ArtifactSet> previewArtifactSets = null)
        {
            if (root == null || item == null) return;
            AttachStyle(root); CloseTooltip(root);
            var previousFocus = root.panel?.focusController.focusedElement as VisualElement;
            var overlay = new VisualElement { name = "item-tooltip-overlay", focusable = true };
            overlay.AddToClassList("item-tooltip-overlay");
            var dialog = new VisualElement { name = "item-tooltip-dialog" }; dialog.AddToClassList("item-tooltip-dialog");
            dialog.AddToClassList("item-frame-" + item.FrameTier);
            var category = item.Kind == ItemKind.Artifact ? "Artifact Set Basket" :
                item.Kind == ItemKind.PhaseMaterial ? "Phase Upgrade Material" : item.Kind == ItemKind.SubPhaseMaterial ? "Sub-Phase Upgrade Material" :
                item.Kind == ItemKind.SeriesMaterial || item.Kind == ItemKind.SeriesPremiumMaterial ? "Series Material" :
                item.Kind == ItemKind.CharacterDupe ? "Character Duplicate" : item.Kind == ItemKind.ArtifactMaterial ? "Artifact Enhancement Material" : "Currency";
            var body = CreateTooltipPanels(dialog, item.Name, category, item.FrameTier, Icon(item));
            var owned = PlayerInventoryService.Instance?.GetItemAmount(item.Id) ?? 0;
            if (!previewOnly) body.Add(new Label((received.HasValue ? "Received: " + received.Value.ToString("N0") + "  ·  " : "") +
                "Owned: " + owned.ToString("N0")) { name = "item-tooltip-amount" });
            if (item.Kind == ItemKind.Artifact && ArtifactRules.TryIdentity(item.Id, out var set, out _))
            {
                var sets = new[] { set }; // Each visible Flower now represents its own set. Both remain eligible in the basket rule.
                foreach (var candidate in sets) ArtifactItemView.FillSetDetails(body, candidate, 0);
            }
            if (!string.IsNullOrEmpty(rule))
            {
                var rewardRule = new Label(rule) { name = "item-tooltip-rule" }; rewardRule.AddToClassList("item-tooltip-rule"); body.Add(rewardRule);
            }
            var description = new Label(item.Description) { name = "item-tooltip-description" };
            description.AddToClassList("item-tooltip-description"); body.Add(description);
            Action close = () => { overlay.RemoveFromHierarchy(); if (previousFocus?.panel != null) previousFocus.Focus(); };
            var dismiss = new Button(close) { text = "×", name = "close-item-tooltip" }; dismiss.AddToClassList("item-tooltip-close"); dialog.Add(dismiss);
            overlay.Add(dialog); root.Add(overlay);
            overlay.RegisterCallback<ClickEvent>(evt => { evt.StopPropagation(); if (evt.target == overlay) close(); });
            overlay.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Escape) { close(); evt.StopPropagation(); } });
            Action position = () =>
            {
                var width = root.contentRect.width; var height = root.contentRect.height;
                dialog.style.maxHeight = Mathf.Max(160, height - 24);
                var w = dialog.resolvedStyle.width; var h = dialog.resolvedStyle.height;
                if (float.IsNaN(w) || float.IsNaN(h) || width <= 0 || height <= 0) return;
                var point = anchor == null ? new Vector2((width - w) / 2, (height - h) / 2) : root.WorldToLocal(new Vector2(anchor.worldBound.xMin, anchor.worldBound.yMax));
                if (anchor != null && point.y + h + 12 > height) point.y = root.WorldToLocal(anchor.worldBound.position).y - h - 8;
                dialog.style.left = Mathf.Clamp(point.x, 12, Mathf.Max(12, width - w - 12));
                dialog.style.top = Mathf.Clamp(point.y + (anchor == null ? 0 : 8), 12, Mathf.Max(12, height - h - 12));
            };
            dialog.RegisterCallback<GeometryChangedEvent>(_ => position());
            overlay.RegisterCallback<GeometryChangedEvent>(_ => position());
            overlay.schedule.Execute(position); overlay.Focus();
        }

        internal static VisualElement CreateTooltipPanels(VisualElement dialog, string title, string type, int rarity, VisualElement icon, int? level = null, ArtifactStatLine mainStat = null)
        {
            var namePanel = new VisualElement { name = "item-tooltip-name-panel" }; namePanel.AddToClassList("item-tooltip-name-panel");
            var name = new Label(title) { name = "item-tooltip-name" }; name.AddToClassList("item-tooltip-name"); namePanel.Add(name);
            if (level.HasValue) { var badge = new Label("+" + level.Value) { name = "item-tooltip-level" }; badge.AddToClassList("item-tooltip-level"); namePanel.Add(badge); }
            dialog.Add(namePanel);
            var preview = new VisualElement { name = "item-tooltip-preview-panel" }; preview.AddToClassList("item-tooltip-preview-panel");
            var category = new Label(type); category.AddToClassList("item-tooltip-category"); preview.Add(category);
            if (mainStat != null)
            {
                preview.Add(new Label(ArtifactRules.StatName(mainStat)) { name = "item-tooltip-main-stat" });
                preview.Add(new Label(ArtifactRules.Format(mainStat)) { name = "item-tooltip-main-value" });
            }
            icon.style.position = StyleKeyword.Null; icon.style.left = icon.style.right = icon.style.top = icon.style.bottom = StyleKeyword.Null;
            icon.style.width = icon.style.height = StyleKeyword.Null;
            icon.AddToClassList("item-tooltip-preview-icon"); preview.Add(icon);
            var stars = new Label(new string('★', Mathf.Clamp(rarity, 1, 5))); stars.AddToClassList("item-tooltip-preview-stars"); preview.Add(stars); dialog.Add(preview);
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "item-tooltip-content-panel",
                horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Auto };
            scroll.AddToClassList("item-tooltip-content-panel"); dialog.Add(scroll);
            var body = new VisualElement(); body.AddToClassList("item-tooltip-content"); scroll.Add(body); return body;
        }

        public static void ShowInventory(VisualElement root) => BackpackView.Show(root);

        internal static void AttachStyle(VisualElement root)
        {
            if (root == null) return;
            var sheet = Resources.Load<StyleSheet>("UI/ItemRewards");
            if (sheet != null && !root.styleSheets.Contains(sheet)) root.styleSheets.Add(sheet);
        }

        private static VisualElement Icon(ItemDefinition item)
        {
            var icon = new VisualElement { pickingMode = PickingMode.Ignore }; icon.AddToClassList("item-reward-icon"); FullTileIcon(icon);
            if (item.Kind == ItemKind.Artifact && ArtifactRules.TryIdentity(item.Id, out var set, out var slot))
            {
                var sprite = ArtifactItemView.Icon(new ArtifactInstance { Set = set, Slot = slot });
                if (sprite != null) icon.style.backgroundImage = new StyleBackground(sprite);
                icon.AddToClassList("artifact-item-icon"); return icon;
            }
            if (_itemArt == null) _itemArt = Resources.Load<ItemIconArtSO>("UI/ItemIconArt");
            var authoredSprite = _itemArt != null ? _itemArt.Icon(item) : null;
            if (authoredSprite != null)
            {
                icon.style.backgroundImage = new StyleBackground(authoredSprite);
                icon.AddToClassList("authored-item-icon");
                return icon;
            }
            icon.generateVisualContent += context =>
            {
                var p = context.painter2D; var center = icon.contentRect.center;
                var size = Mathf.Min(icon.contentRect.width, icon.contentRect.height) * .31f;
                var tint = item.Kind == ItemKind.Diamond ? new Color(.38f, .86f, .96f) : item.Kind == ItemKind.Coin ? new Color(.98f, .77f, .3f) :
                    item.Kind == ItemKind.SeriesMaterial || item.Kind == ItemKind.SeriesPremiumMaterial ? new Color(.76f, .55f, .98f) :
                    item.Kind == ItemKind.Artifact ? new Color(.94f, .9f, .73f) : new Color(.38f, .71f, .96f);
                if (!string.IsNullOrEmpty(item.IconColor) && ColorUtility.TryParseHtmlString(item.IconColor, out var authoredTint)) tint = authoredTint;
                p.fillColor = tint; p.strokeColor = new Color(1, 1, 1, .85f); p.lineWidth = 2; p.BeginPath();
                if (item.Kind == ItemKind.Coin) p.Arc(center, size, 0, 360);
                else
                {
                    var sides = item.Kind == ItemKind.Artifact || item.Kind == ItemKind.SeriesMaterial || item.Kind == ItemKind.SeriesPremiumMaterial ? 6 : item.Kind == ItemKind.PhaseMaterial ? 5 : 4;
                    for (var i = 0; i < sides; i++)
                    {
                        var angle = -Mathf.PI / 2 + i * Mathf.PI * 2 / sides;
                        var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * size;
                        if (i == 0) p.MoveTo(point); else p.LineTo(point);
                    }
                }
                p.ClosePath(); p.Fill(); p.Stroke();
                if (item.Kind == ItemKind.PhaseMaterial)
                {
                    p.strokeColor = new Color(.08f, .18f, .32f); p.lineWidth = 3;
                    for (var i = 0; i < item.Tier; i++)
                    {
                        var angle = (i * 360f / item.Tier - 90) * Mathf.Deg2Rad;
                        var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        p.BeginPath(); p.MoveTo(center + direction * size * .64f); p.LineTo(center + direction * size * .86f); p.Stroke();
                    }
                    p.fillColor = Color.white; p.BeginPath(); p.Arc(center, size * .24f, 0, 360); p.Fill();
                    return;
                }
                if (item.Kind == ItemKind.SubPhaseMaterial)
                {
                    p.strokeColor = tint; p.lineWidth = 2; p.BeginPath(); p.Arc(center, size * 1.2f, 0, 360); p.Stroke();
                    for (var i = 0; i < item.Tier + 4; i++)
                    {
                        var angle = (i * 360f / (item.Tier + 4)) * Mathf.Deg2Rad;
                        var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                        p.BeginPath(); p.MoveTo(center + direction * size * 1.15f); p.LineTo(center + direction * size * 1.45f); p.Stroke();
                    }
                    p.fillColor = new Color(.08f, .18f, .32f); p.BeginPath(); p.Arc(center, size * .27f, 0, 360); p.Fill();
                    return;
                }
                if (item.Kind == ItemKind.SeriesMaterial || item.Kind == ItemKind.SeriesPremiumMaterial) return;
                p.strokeColor = new Color(.13f, .25f, .34f); p.lineWidth = 3; p.BeginPath();
                p.MoveTo(center + new Vector2(-size * .4f, 0)); p.LineTo(center + new Vector2(size * .4f, 0)); p.Stroke();
                if (item.Kind != ItemKind.Diamond)
                {
                    p.BeginPath(); p.MoveTo(center + new Vector2(0, -size * .4f)); p.LineTo(center + new Vector2(0, size * .4f)); p.Stroke();
                }
            };
            if (item.Kind == ItemKind.SeriesMaterial || item.Kind == ItemKind.SeriesPremiumMaterial)
            {
                var series = DungeonProfile.SeriesDisplayName(item.SeriesId);
                var letters = series == "KOF" ? "KOF" : new string(series.Split(new[] { ' ', '-' }, StringSplitOptions.RemoveEmptyEntries).Select(s => char.ToUpperInvariant(s[0])).Take(3).ToArray());
                var monogram = new Label(letters) { pickingMode = PickingMode.Ignore };
                monogram.style.position = Position.Absolute; monogram.style.left = 0; monogram.style.right = 0;
                monogram.style.top = Length.Percent(30); monogram.style.bottom = Length.Percent(30);
                monogram.style.unityTextAlign = TextAnchor.MiddleCenter; monogram.style.fontSize = 17;
                monogram.style.color = new Color(1, .9f, .64f); monogram.style.unityFontStyleAndWeight = FontStyle.Bold;
                icon.Add(monogram);
            }
            return icon;
        }
    }
}
