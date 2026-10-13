using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Navigation
{
    internal static class GachaBannerView
    {
        public static void Initialize(VisualElement root)
        {
            var stage = root?.Q("gacha-stage");
            if (stage == null || stage.userData != null) return;
            stage.userData = true;
            var close = root.Q<Button>("details-close");
            if (close != null) close.clicked += () => CloseDetails(root);
            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                var overlay = root.Q("details-overlay");
                if (evt.keyCode != KeyCode.Escape || overlay == null || overlay.style.display.value != DisplayStyle.Flex) return;
                CloseDetails(root); evt.StopPropagation();
            });
        }

        public static void PopulateBanners(VisualElement root, IEnumerable<SummonBannerConfigurationSO> banners,
            SummonBannerConfigurationSO selected, Action<SummonBannerConfigurationSO> select)
        {
            var grid = root?.Q("banner-list-grid");
            if (grid == null) return;
            grid.Clear();
            foreach (var banner in (banners ?? Enumerable.Empty<SummonBannerConfigurationSO>()).Where(b => b != null).Distinct())
            {
                var candidate = banner;
                var card = new Button(() => select?.Invoke(candidate)) { userData = banner };
                card.AddToClassList("banner-card");
                var art = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.ScaleAndCrop };
                art.AddToClassList("banner-card-art");
                if (banner.Thumbnail != null || banner.BannerArtwork != null) art.image = banner.Thumbnail != null ? banner.Thumbnail : banner.BannerArtwork;
                else art.sprite = Featured(banner)?.FighterIcon;
                card.Add(art);
                var title = new Label(banner.BannerName) { pickingMode = PickingMode.Ignore }; title.AddToClassList("banner-card-title"); card.Add(title);
                var active = new Label("SELECTED") { pickingMode = PickingMode.Ignore }; active.AddToClassList("banner-card-selected"); card.Add(active);
                var valid = banner.Validate(out var error);
                card.SetEnabled(valid); card.tooltip = valid ? banner.BannerName : error;
                card.EnableInClassList("selected", banner == selected); grid.Add(card);
            }
            if (grid.childCount == 0)
            {
                var empty = new Label("No banners available."); empty.AddToClassList("banner-list-empty"); grid.Add(empty);
            }
        }

        public static void Refresh(VisualElement root, SummonBannerConfigurationSO banner)
        {
            if (root?.Q("gacha-stage") == null) return;
            var inventory = PlayerInventoryService.Instance;
            var valid = banner != null && banner.Validate(out _);
            foreach (var rarity in new[] { FighterRarity.SSR, FighterRarity.SR, FighterRarity.R })
                Text(root, rarity.ToString().ToLowerInvariant() + "-rate", rarity + "  " +
                    (valid ? (banner.GetRarityRateBasisPoints(rarity) / 100f).ToString("0.##") + "%" : "—"));
            var threshold = PlayerInventoryService.FeaturedSelectorThreshold;
            Text(root, "guarantee", "SSR SELECTOR");
            Text(root, "guarantee-value", (inventory?.GuaranteeProgress ?? 0) + " / " + threshold + " PULLS · " + (inventory?.SelectorEntitlements ?? 0) + " READY");
            var fill = root.Q("guarantee-fill");
            if (fill != null) fill.style.width = Length.Percent(Mathf.Clamp01((float)(inventory?.GuaranteeProgress ?? 0) / threshold) * 100);
            Text(root, "summon-one-label", "1 PULL · " + PlayerInventoryService.SingleSummonPrice.ToString("N0"));
            Text(root, "summon-ten-label", "10 PULLS · " + PlayerInventoryService.TenSummonPrice.ToString("N0"));
            Pull(root.Q<Button>("summon-one"), valid, inventory, PlayerInventoryService.SingleSummonPrice, "Summon one fighter");
            Pull(root.Q<Button>("summon-ten"), valid, inventory, PlayerInventoryService.TenSummonPrice, "Summon ten independent draws");
            Text(root, "banner-title", banner != null ? banner.BannerName : "SUMMON");
            Text(root, "banner-description", banner?.Description);
            var description = root.Q<Label>("banner-description");
            if (description != null) description.style.display = string.IsNullOrWhiteSpace(description.text) ? DisplayStyle.None : DisplayStyle.Flex;
            var artwork = root.Q("banner-artwork");
            if (artwork != null) artwork.style.backgroundImage = banner?.BannerArtwork != null ? new StyleBackground(banner.BannerArtwork) : new StyleBackground(StyleKeyword.Null);
            var hero = root.Q<Image>("hero-art");
            if (hero != null)
            {
                hero.scaleMode = ScaleMode.ScaleToFit;
                var featured = valid ? Featured(banner) : null;
                hero.sprite = featured != null ? featured.FighterPic != null ? featured.FighterPic : featured.FighterIcon : null;
                hero.style.display = banner?.BannerArtwork == null && hero.sprite != null ? DisplayStyle.Flex : DisplayStyle.None;
            }
            var cards = root.Q("banner-list-grid")?.Query<Button>(className: "banner-card").ToList() ?? new List<Button>();
            foreach (var card in cards) card.EnableInClassList("selected", card.userData == banner);
            CharacterGridView.Populate(root.Q("gacha-preview-grid"), valid ? banner.RarityPools.OrderByDescending(p => p.Rarity)
                .SelectMany(p => p.Characters).Where(c => c != null).Distinct().Take(4) : null, iconSize: 64);
            var notice = root.Q<Label>("notice");
            if (notice != null) notice.style.display = string.IsNullOrEmpty(notice.text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private static CharacterObject Featured(SummonBannerConfigurationSO banner) => banner?.RarityPools?.Where(p => p != null)
            .OrderByDescending(p => p.Rarity).SelectMany(p => p.Characters ?? Array.Empty<CharacterObject>()).FirstOrDefault(c => c != null);
        private static void Text(VisualElement root, string name, string value)
        { var label = root.Q<Label>(name); if (label != null) label.text = value ?? string.Empty; }
        private static void Pull(Button button, bool valid, PlayerInventoryService inventory, int cost, string action)
        {
            if (button == null) return;
            button.SetEnabled(valid && inventory != null && inventory.Diamonds >= cost);
            button.tooltip = !valid ? "Banner unavailable" : inventory == null ? "Inventory unavailable" :
                inventory.Diamonds < cost ? "Not enough Diamonds" : action + " for " + cost.ToString("N0") + " Diamonds";
        }
        public static void OpenDetails(VisualElement root, string title, string opener)
        {
            var overlay = root?.Q("details-overlay"); if (overlay == null) return;
            overlay.userData = root.Q<Button>(opener); ScreenMotion.Show(overlay);
            Text(root, "details-title", title); root.Q("banner-screen")?.SetEnabled(false); root.Q<Button>("details-close")?.Focus();
        }
        public static void CloseDetails(VisualElement root)
        {
            var overlay = root?.Q("details-overlay"); if (overlay == null) return;
            ScreenMotion.Hide(overlay);
            var panel = root.Q("details-panel"); if (panel != null) panel.style.display = DisplayStyle.None;
            root.Q("banner-screen")?.SetEnabled(true); (overlay.userData as Button)?.Focus();
        }
    }
}
