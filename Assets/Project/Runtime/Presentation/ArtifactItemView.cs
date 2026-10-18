using System;
using System.Linq;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Run;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation
{
    /// <summary>Instance-aware artifact tiles and details shared by equipment, materials, backpack and rewards.</summary>
    public static class ArtifactItemView
    {
        private static ArtifactArtSO _art;

        public static void AttachStyle(VisualElement root) => ItemRewardView.AttachStyle(root);
        public static Sprite Icon(ArtifactInstance artifact)
        {
            if (artifact == null) return null;
            if (_art == null) _art = Resources.Load<ArtifactArtSO>("UI/ArtifactArt");
            return _art != null ? _art.Icon(artifact.Set, artifact.Slot) : null;
        }
        public static string Name(ArtifactInstance artifact) => artifact == null ? "Empty slot" : ArtifactRules.Name(artifact);
        public static string StatText(ArtifactStatLine line) => line == null ? "—" : ArtifactRules.StatName(line) + "  +" + ArtifactRules.Format(line);
        public static string RarityStars(ArtifactInstance artifact) => new string('★', Mathf.Clamp((int)(artifact?.Rarity ?? ArtifactRarity.Common), 1, 5));
        public static string RarityClass(ArtifactInstance artifact) => "artifact-rarity-" + Mathf.Clamp((int)(artifact?.Rarity ?? ArtifactRarity.Common), 1, 5);

        public static Button CreateTile(VisualElement root, ArtifactInstance artifact, Action click = null, bool showOwner = true)
        {
            if (artifact == null) return new Button();
            AttachStyle(root);
            var display = artifact.Clone(); ArtifactRules.Normalize(display);
            var subject = PlayerInventoryService.Instance?.SubjectId;
            var owner = EquippedCharacter(display);
            var ownerName = owner != null ? owner.FighterName : "Equipped fighter";
            Button tile = null;
            tile = new Button(() =>
                {
                    if (!string.Equals(PlayerInventoryService.Instance?.SubjectId, subject, StringComparison.Ordinal)) return;
                    if (click != null) click(); else ShowTooltip(root, display, tile);
                })
                { name = "artifact-" + display.InstanceId, tooltip = Name(display) + " · " + RarityStars(display) + " · +" + display.Level + " · " + StatText(display.MainStat) +
                    (showOwner && !string.IsNullOrEmpty(display.EquippedCharacterId) ? " · Equipped: " + ownerName : "") };
            tile.AddToClassList("item-reward-tile"); tile.AddToClassList("artifact-item-tile");
            tile.AddToClassList("item-kind-artifact");
            ItemRewardView.StandardizeTile(tile); tile.Add(CreateFrame(display, showOwner));
            tile.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Escape) ItemRewardView.CloseTooltip(root); });
            return tile;
        }

        internal static VisualElement CreateFrame(ArtifactInstance display, bool showOwner = true)
        {
            var frame = ItemRewardView.CreateFrame((int)display.Rarity);
            var image = new VisualElement { pickingMode = PickingMode.Ignore }; image.AddToClassList("artifact-item-icon"); image.AddToClassList("item-reward-icon");
            ItemRewardView.FullTileIcon(image); var sprite = Icon(display); if (sprite != null) image.style.backgroundImage = new StyleBackground(sprite); frame.Add(image);
            ItemRewardView.AddQuantityFooter(frame, "+" + display.Level);
            var stars = Label(RarityStars(display), "artifact-rarity-stars"); stars.AddToClassList(RarityClass(display)); stars.pickingMode = PickingMode.Ignore; frame.Add(stars);
            if (display.Favourite) frame.Add(FavouriteLock());
            if (showOwner && !string.IsNullOrEmpty(display.EquippedCharacterId)) frame.Add(Avatar(EquippedCharacter(display), "artifact-owner-avatar"));
            return frame;
        }

        public static void FillDetails(VisualElement container, ArtifactInstance artifact, bool showSetBonuses = true, string characterInstanceId = null, bool showMetadata = true, bool showMainStat = true, bool showOwner = true)
        {
            if (container == null || artifact == null) return;
            var display = artifact.Clone(); ArtifactRules.Normalize(display); container.Clear();
            container.AddToClassList("artifact-details");
            var meta = new VisualElement(); meta.AddToClassList("artifact-detail-meta");
            var rarity = Label(RarityStars(display), "artifact-detail-stars"); rarity.AddToClassList(RarityClass(display)); meta.Add(rarity);
            meta.Add(Label(ArtifactRules.SlotName(display.Slot) + " · +" + display.Level, "artifact-detail-slot"));
            if (showMetadata) container.Add(meta);

            var main = new VisualElement(); main.AddToClassList("artifact-main-stat");
            main.Add(Label(ArtifactRules.StatName(display.MainStat), "artifact-stat-label"));
            main.Add(Label("+" + ArtifactRules.Format(display.MainStat), "artifact-main-value")); if (showMainStat) container.Add(main);
            var compact = new VisualElement(); compact.AddToClassList("artifact-substats");
            foreach (var line in display.Substats.Where(s => s != null))
            {
                var row = new VisualElement(); row.AddToClassList("artifact-stat-row");
                row.Add(Label((showMetadata ? "" : "•  ") + ArtifactRules.StatName(line), "artifact-stat-label"));
                row.Add(Label("+" + ArtifactRules.Format(line), "artifact-stat-value")); compact.Add(row);
            }
            if (display.Substats.Count == 0) compact.Add(Label("Common artifacts have no substats.", "artifact-detail-muted"));
            container.Add(compact);
            if (showSetBonuses)
            {
                var context = !string.IsNullOrEmpty(characterInstanceId) ? characterInstanceId : display.EquippedCharacterId;
                var equipped = string.IsNullOrEmpty(context) ? null : PlayerInventoryService.Instance?.GetEquippedArtifacts(context);
                var count = equipped == null ? 0 : ArtifactRules.SetCount(equipped, display.Set);
                FillSetDetails(container, display.Set, count);
            }
            if (showOwner && !string.IsNullOrEmpty(display.EquippedCharacterId))
            {
                var character = EquippedCharacter(display);
                var ownership = new VisualElement(); ownership.AddToClassList("artifact-detail-ownership");
                ownership.Add(Avatar(character, "artifact-detail-avatar"));
                ownership.Add(Label("Equipped: " + (character != null ? character.FighterName : "Owned fighter"), "artifact-detail-equipped")); container.Add(ownership);
            }
        }

        public static void FillSetDetails(VisualElement container, ArtifactSet artifactSet, int count)
        {
            var set = new VisualElement(); set.AddToClassList("artifact-set-details");
            set.Add(Label(ArtifactRules.SetName(artifactSet) + (count > 0 ? "  (" + count + "/5)" : ":"), "artifact-set-name"));
            var stat = artifactSet == ArtifactSet.Striker ? "ATK" : artifactSet == ArtifactSet.Bulwark ? "DEF" : "Max HP";
            var two = Label((count >= 2 ? "✓  " : "○  ") + "2-Piece Set: " + stat + " +20%", "artifact-set-bonus");
            two.EnableInClassList("active", count >= 2); set.Add(two);
            var four = Label((count >= 4 ? "✓  " : "○  ") + "4-Piece Set: " + stat + " +20%", "artifact-set-bonus");
            four.EnableInClassList("active", count >= 4); set.Add(four);
            var conditional = artifactSet == ArtifactSet.Striker ? "Life Steal +5% and Damage Dealt +5%" :
                artifactSet == ArtifactSet.Bulwark ? "Crit Resistance +15% and Crit Defense +15%" : "Regeneration +10% and Recovery +10%";
            var condition = Label("Below 20% HP: " + conditional + ".", "artifact-set-condition");
            condition.EnableInClassList("active", count >= 4); set.Add(condition); container.Add(set);
        }

        public static void ShowTooltip(VisualElement root, ArtifactInstance artifact, VisualElement anchor = null)
        {
            if (root == null || artifact == null) return;
            artifact = artifact.Clone(); ArtifactRules.Normalize(artifact);
            AttachStyle(root); ItemRewardView.CloseTooltip(root);
            var previous = root.panel?.focusController.focusedElement as VisualElement;
            var anchorBounds = anchor?.worldBound;
            var overlay = new VisualElement { name = "item-tooltip-overlay", focusable = true }; overlay.AddToClassList("item-tooltip-overlay");
            var dialog = new VisualElement { name = "item-tooltip-dialog" }; dialog.AddToClassList("item-tooltip-dialog");
            dialog.AddToClassList("artifact-tooltip-dialog"); dialog.AddToClassList("item-frame-" + (int)artifact.Rarity);
            var icon = new VisualElement(); icon.AddToClassList("artifact-tooltip-icon");
            var sprite = Icon(artifact); if (sprite != null) icon.style.backgroundImage = new StyleBackground(sprite);
            var body = ItemRewardView.CreateTooltipPanels(dialog, Name(artifact), ArtifactRules.SlotName(artifact.Slot) + " · Artifact", (int)artifact.Rarity, icon, level: artifact.Level, mainStat: artifact.MainStat);
            var favourite = new Button { name = "artifact-tooltip-favourite" }; favourite.AddToClassList("artifact-favourite-button");
            var message = Label("", "artifact-detail-muted"); message.name = "artifact-tooltip-message";
            var subject = PlayerInventoryService.Instance?.SubjectId;
            var ownedAtOpen = PlayerInventoryService.Instance?.FindArtifact(artifact.InstanceId) != null;
            Action close = () => ScreenMotion.Remove(overlay, () => { if (root.Q("item-tooltip-overlay") == null && previous?.panel != null) previous.Focus(); });
            Action refresh = () =>
            {
                if (!string.Equals(PlayerInventoryService.Instance?.SubjectId, subject, StringComparison.Ordinal)) { close(); return; }
                var current = PlayerInventoryService.Instance?.FindArtifact(artifact.InstanceId);
                if (ownedAtOpen && current == null) { close(); return; }
                var owned = current != null; ownedAtOpen |= owned; current ??= artifact;
                FillDetails(body, current, showMetadata: false, showMainStat: false);
                var levelLabel = dialog.Q<Label>("item-tooltip-level"); if (levelLabel != null) levelLabel.text = "+" + current.Level;
                var mainName = dialog.Q<Label>("item-tooltip-main-stat"); if (mainName != null) mainName.text = ArtifactRules.StatName(current.MainStat);
                var mainValue = dialog.Q<Label>("item-tooltip-main-value"); if (mainValue != null) mainValue.text = ArtifactRules.Format(current.MainStat);
                body.Add(favourite); body.Add(message);
                favourite.text = current.Favourite ? "★ FAVOURITE · PROTECTED" : "☆ MARK AS FAVOURITE";
                favourite.EnableInClassList("selected", current.Favourite);
                favourite.SetEnabled(owned);
                message.text = current.Favourite ? "Protected from all enhancement consumption." : "Favourite an artifact to protect it from consumption.";
            };
            favourite.clicked += () =>
            {
                var service = PlayerInventoryService.Instance; var current = service?.FindArtifact(artifact.InstanceId);
                if (!string.Equals(service?.SubjectId, subject, StringComparison.Ordinal)) { close(); return; }
                if (current == null) return;
                if (!service.TrySetArtifactFavourite(current.InstanceId, !current.Favourite, out var error)) message.text = error;
                else refresh();
            };
            overlay.userData = close;
            var dismiss = new Button(close) { name = "close-item-tooltip", text = "×" }; dismiss.AddToClassList("item-tooltip-close"); dialog.Add(dismiss);
            overlay.Add(dialog); root.Add(overlay); refresh(); ScreenMotion.ShowModal(overlay, dialog);
            PlayerInventoryService.InventoryChanged += refresh;
            overlay.RegisterCallback<DetachFromPanelEvent>(_ => PlayerInventoryService.InventoryChanged -= refresh);
            overlay.RegisterCallback<ClickEvent>(evt => { evt.StopPropagation(); if (evt.target == overlay) close(); });
            overlay.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == KeyCode.Escape) { close(); evt.StopPropagation(); } });
            Action position = () =>
            {
                var width = root.contentRect.width; var height = root.contentRect.height;
                if (width <= 0 || height <= 0) return;
                dialog.style.maxHeight = Mathf.Max(160, height - 24);
                var w = dialog.resolvedStyle.width; var h = dialog.resolvedStyle.height;
                if (float.IsNaN(w) || float.IsNaN(h)) return;
                var bounds = anchor?.panel != null ? anchor.worldBound : anchorBounds;
                var point = !bounds.HasValue ? new Vector2((width - w) / 2, (height - h) / 2) : root.WorldToLocal(new Vector2(bounds.Value.xMin, bounds.Value.yMax + 8));
                if (bounds.HasValue && point.y + h > height - 12) point.y = root.WorldToLocal(bounds.Value.position).y - h - 8;
                dialog.style.left = Mathf.Clamp(point.x, 12, Mathf.Max(12, width - w - 12));
                dialog.style.top = Mathf.Clamp(point.y, 12, Mathf.Max(12, height - h - 12));
            };
            dialog.RegisterCallback<GeometryChangedEvent>(_ => position()); overlay.RegisterCallback<GeometryChangedEvent>(_ => position());
            overlay.schedule.Execute(position); overlay.Focus();
        }

        private static CharacterObject EquippedCharacter(ArtifactInstance artifact)
        {
            if (string.IsNullOrEmpty(artifact?.EquippedCharacterId)) return null;
            var service = PlayerInventoryService.Instance;
            // Tile rendering reads the already-loaded character record; it must not normalize the whole inventory per tile.
            var owned = service?.Snapshot?.FindOwned(artifact.EquippedCharacterId);
            return owned != null ? service.FindDefinition(owned.definitionId) : null;
        }

        private static VisualElement Avatar(CharacterObject character, string css)
        {
            var avatar = new VisualElement { pickingMode = PickingMode.Ignore }; avatar.AddToClassList(css);
            var sprite = character != null ? character.FighterIcon != null ? character.FighterIcon : character.FighterPic : null;
            if (sprite != null) avatar.style.backgroundImage = new StyleBackground(sprite);
            else avatar.Add(Label("•", "artifact-avatar-fallback"));
            return avatar;
        }

        private static VisualElement FavouriteLock()
        {
            var icon = new VisualElement { pickingMode = PickingMode.Ignore, tooltip = "Favourite · protected from consumption" };
            icon.AddToClassList("artifact-favourite-marker");
            icon.generateVisualContent += context =>
            {
                var painter = context.painter2D; var center = icon.contentRect.center;
                var scale = Mathf.Min(icon.contentRect.width, icon.contentRect.height) / 22f;
                painter.strokeColor = new Color(1, .87f, .5f); painter.fillColor = painter.strokeColor; painter.lineWidth = 2 * scale;
                painter.BeginPath(); painter.Arc(center + new Vector2(0, -3) * scale, 4 * scale, 180, 360); painter.Stroke();
                painter.BeginPath(); painter.MoveTo(center + new Vector2(-6, -2) * scale); painter.LineTo(center + new Vector2(6, -2) * scale);
                painter.LineTo(center + new Vector2(6, 7) * scale); painter.LineTo(center + new Vector2(-6, 7) * scale); painter.ClosePath(); painter.Fill();
                painter.fillColor = new Color(.06f, .11f, .17f); painter.BeginPath(); painter.Arc(center + new Vector2(0, 2) * scale, 1.6f * scale, 0, 360); painter.Fill();
            };
            return icon;
        }

        private static Label Label(string text, string css)
        {
            var label = new Label(text); label.AddToClassList(css); return label;
        }
    }
}
