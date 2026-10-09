using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Navigation
{
    /// <summary>Reference-matched presentation; summon rules remain in PlayerInventoryService.</summary>
    internal static class GachaBannerView
    {
        private static readonly Vector2[] Rectangle = { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        private static readonly Vector2[] Angled = { new Vector2(.17f, 0), new Vector2(1, 0), new Vector2(.83f, 1), new Vector2(0, 1) };
        private static readonly Vector2[] CutCorners = { new Vector2(.025f, 0), new Vector2(1, 0), new Vector2(1, .8f), new Vector2(.965f, 1), new Vector2(0, 1), new Vector2(0, .18f) };
        private static readonly Color Ink = new Color(.025f, .047f, .075f, .95f);
        private static readonly Color Blue = new Color(.5f, .65f, .79f);
        private static readonly Color Gold = new Color(.97f, .78f, .35f);

        public static void Initialize(VisualElement root)
        {
            var viewport = root.Q("gacha-viewport");
            var stage = root.Q("gacha-stage");
            if (viewport == null || stage == null || stage.userData != null) return;
            stage.userData = true;
            Action fit = () =>
            {
                var bounds = viewport.contentRect;
                if (bounds.width <= 0 || bounds.height <= 0) return;
                var safe = Screen.safeArea;
                var width = Screen.width > 0 ? safe.width * bounds.width / Screen.width : bounds.width;
                var height = Screen.height > 0 ? safe.height * bounds.height / Screen.height : bounds.height;
                var x = Screen.width > 0 ? safe.x * bounds.width / Screen.width : 0;
                var y = Screen.height > 0 ? (Screen.height - safe.yMax) * bounds.height / Screen.height : 0;
                var scale = Mathf.Min(width / 1672, height / 941);
                stage.style.scale = new Scale(new Vector3(scale, scale, 1));
                stage.style.left = x + (width - 1672 * scale) / 2;
                stage.style.top = y + (height - 941 * scale) / 2;
            };
            viewport.RegisterCallback<GeometryChangedEvent>(_ => fit());
            viewport.schedule.Execute(fit);

            var atlas = Resources.Load<Texture2D>("UI/GachaBanner/ReferenceAtlas");
            Surface(root, "gacha-header-surface", new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(.97f, 1), new Vector2(0, 1) }, new Color(.025f, .045f, .075f, .93f), new Color(.3f, .4f, .5f, .55f));
            Art(root, "gacha-brand-art", atlas, new Rect(107, 15, 302, 45));
            Art(root, "gacha-wallet-diamond", atlas, new Rect(1454, 18, 42, 38));
            Surface(root, "gacha-featured-surface", CutCorners, new Color(.19f, .14f, .045f, .9f), Gold);
            Art(root, "gacha-featured-art", atlas, new Rect(434, 432, 298, 87));
            Art(root, "gacha-featured-star", atlas, new Rect(84, 438, 81, 70));
            Surface(root, "gacha-rates-surface", new[] { new Vector2(.03f, 0), new Vector2(1, 0), new Vector2(.87f, 1), new Vector2(0, 1), new Vector2(0, .2f) }, Ink, Blue);
            Surface(root, "gacha-caption-surface", new[] { new Vector2(.075f, 0), new Vector2(1, 0), new Vector2(1, .55f), new Vector2(.94f, 1), new Vector2(0, 1), new Vector2(0, .6f) }, Ink, new Color(.91f, .15f, .2f));
            var crops = new[] { new Rect(40, 619, 214, 135), new Rect(229, 619, 214, 135), new Rect(427, 619, 214, 135), new Rect(623, 619, 214, 135) };
            for (var i = 0; i < crops.Length; i++)
            {
                Art(root, "gacha-portrait-" + i, atlas, crops[i], Angled);
                Surface(root, "gacha-portrait-" + i, Angled, Color.clear, i == 0 ? Gold : Blue);
            }
            Surface(root, "gacha-guarantee-surface", CutCorners, Ink, Blue);
            Surface(root, "gacha-details-surface", CutCorners, Ink, Blue);
            Surface(root, "gacha-single-surface", CutCorners, new Color(.94f, .91f, .83f), new Color(.025f, .04f, .055f));
            Surface(root, "gacha-ten-surface", CutCorners, new Color(.79f, .065f, .12f, .97f), new Color(.96f, .27f, .29f));
            Art(root, "gacha-guarantee-icon", atlas, new Rect(59, 801, 67, 73));
            Art(root, "gacha-details-icon", atlas, new Rect(566, 817, 34, 35));
            Art(root, "gacha-single-diamond", atlas, new Rect(894, 817, 44, 39));
            Art(root, "gacha-ten-diamond", atlas, new Rect(1272, 817, 44, 39));
            Accents(root.Q("gacha-single-surface"), new Color(.69f, .67f, .59f, .3f));
            Accents(root.Q("gacha-ten-surface"), new Color(1, .3f, .32f, .4f));

            root.Q<Button>("details-close").clicked += () => CloseDetails(root);
            root.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Escape || root.Q("details-overlay").style.display.value != DisplayStyle.Flex) return;
                CloseDetails(root);
                evt.StopPropagation();
            });
        }

        public static void Refresh(VisualElement root, SummonBannerConfigurationSO banner)
        {
            if (root.Q("gacha-stage") == null) return;
            var inventory = PlayerInventoryService.Instance;
            var valid = banner != null && banner.Validate(out _);
            SetRate(root, "ssr-rate", banner, FighterRarity.SSR, valid);
            SetRate(root, "sr-rate", banner, FighterRarity.SR, valid);
            SetRate(root, "r-rate", banner, FighterRarity.R, valid);
            var threshold = PlayerInventoryService.FeaturedSelectorThreshold;
            var progress = inventory?.GuaranteeProgress ?? 0;
            var selectors = inventory?.SelectorEntitlements ?? 0;
            root.Q<Label>("guarantee").text = "FEATURED GUARANTEE · " + threshold + " PULLS";
            root.Q<Label>("guarantee-value").text = progress + " / " + threshold + "  ·  " + selectors + " SELECTORS";
            root.Q("guarantee-fill").style.width = Length.Percent(Mathf.Clamp01((float)progress / threshold) * 100);
            root.Q<Label>("summon-one-label").text = "1 PULL · " + PlayerInventoryService.SingleSummonPrice.ToString("N0") + " DIAMONDS";
            root.Q<Label>("summon-ten-label").text = "10 PULLS · " + PlayerInventoryService.TenSummonPrice.ToString("N0") + " DIAMONDS";
            var single = root.Q<Button>("summon-one");
            var ten = root.Q<Button>("summon-ten");
            single.SetEnabled(valid && inventory != null && inventory.Diamonds >= PlayerInventoryService.SingleSummonPrice);
            ten.SetEnabled(valid && inventory != null && inventory.Diamonds >= PlayerInventoryService.TenSummonPrice);
            single.tooltip = single.enabledSelf ? "Summon one fighter for " + PlayerInventoryService.SingleSummonPrice + " Diamonds" : valid ? "Not enough Diamonds" : "Banner unavailable";
            ten.tooltip = ten.enabledSelf ? "Summon ten independent draws for " + PlayerInventoryService.TenSummonPrice.ToString("N0") + " Diamonds" : valid ? "Not enough Diamonds" : "Banner unavailable";
            var notice = root.Q<Label>("notice");
            notice.style.display = string.IsNullOrEmpty(notice.text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private static void SetRate(VisualElement root, string name, SummonBannerConfigurationSO banner, FighterRarity rarity, bool valid)
        {
            root.Q<Label>(name).text = rarity + "  " + (valid ? (banner.GetRarityRateBasisPoints(rarity) / 100f).ToString("0.##") + "%" : "—");
        }

        public static void OpenDetails(VisualElement root, string title, string opener)
        {
            var overlay = root.Q("details-overlay");
            if (overlay == null) return;
            overlay.userData = root.Q<Button>(opener);
            overlay.style.display = DisplayStyle.Flex;
            root.Q<Label>("details-title").text = title;
            root.Q("gacha-stage").SetEnabled(false);
            root.Q<Button>("details-close").Focus();
        }

        public static void CloseDetails(VisualElement root)
        {
            var overlay = root.Q("details-overlay");
            if (overlay == null) return;
            overlay.style.display = DisplayStyle.None;
            root.Q("details-panel").style.display = DisplayStyle.None;
            root.Q("gacha-stage").SetEnabled(true);
            (overlay.userData as Button)?.Focus();
        }

        private static void Art(VisualElement root, string name, Texture2D texture, Rect source, Vector2[] polygon = null)
        {
            if (texture != null) root.Q(name)?.Add(new ReferenceMenuGraphic(texture, source, polygon ?? Rectangle, Color.white, Color.clear));
        }

        private static void Surface(VisualElement root, string name, Vector2[] polygon, Color fill, Color border)
        {
            root.Q(name)?.Add(new ReferenceMenuGraphic(null, default, polygon, fill, border));
        }

        private static void Accents(VisualElement holder, Color color)
        {
            holder.Add(new ReferenceMenuGraphic(null, default, new[] { new Vector2(0, .55f), new Vector2(.07f, 0), new Vector2(.11f, 0), new Vector2(0, .95f) }, color, Color.clear));
            holder.Add(new ReferenceMenuGraphic(null, default, new[] { new Vector2(.93f, 1), new Vector2(1, .6f), new Vector2(1, .72f), new Vector2(.95f, 1) }, color, Color.clear));
        }
    }
}
