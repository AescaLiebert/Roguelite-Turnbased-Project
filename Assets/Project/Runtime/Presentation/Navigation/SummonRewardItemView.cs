using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Navigation
{
    public static class SummonAttributeTheme
    {
        public static Color ColorFor(FighterAttribute attribute)
        {
            switch (attribute)
            {
                case FighterAttribute.Yellow: return new Color(1f, .73f, .17f);
                case FighterAttribute.Green: return new Color(.2f, .88f, .47f);
                case FighterAttribute.Blue: return new Color(.19f, .6f, 1f);
                case FighterAttribute.Purple: return new Color(.7f, .35f, 1f);
                default: return new Color(1f, .23f, .17f);
            }
        }

        public static string NameFor(FighterAttribute attribute)
        {
            switch (attribute)
            {
                case FighterAttribute.Yellow: return "yellow";
                case FighterAttribute.Green: return "green";
                case FighterAttribute.Blue: return "blue";
                case FighterAttribute.Purple: return "purple";
                default: return "red";
            }
        }
    }

    /// <summary>The same committed reward cell is used during the sequence and in its final receipt.</summary>
    public sealed class SummonRewardItemView : ObtainRewardCell
    {
        // Starting motion values: a monotonic 240 ms rise, then a 160 ms light sweep; no size changes.
        public const float RiseDuration = .24f, GlintDuration = .16f;
        public SummonDrawPresentation Receipt { get; }
        private readonly SummonItemLightGraphic _light;
        private readonly SummonNewAuraGraphic _aura;
        private IVisualElementScheduledItem _auraTick;

        private SummonRewardItemView(SummonDrawPresentation receipt, int index)
        {
            Receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));
            var fighter = receipt.Character;
            name = "summon-card-" + index;
            userData = receipt;
            pickingMode = PickingMode.Position;
            AddToClassList("summon-reward-card");
            AddToClassList(receipt.IsNew ? "summon-item-new" : "summon-item-duplicate");
            AddToClassList(SummonSequenceView.HasPremiumReveal(fighter.FighterRarity) ? "summon-card-premium" : fighter.FighterRarity == FighterRarity.SR ? "summon-card-sr" : "summon-card-r");
            tooltip = fighter.FighterRarity + " · " + fighter.FighterName + (receipt.IsNew ? " · NEW" : " · Duplicate");
            var face = Box("summon-item-face", "summon-card-face");
            if (receipt.IsNew && SummonSequenceView.HasPremiumReveal(fighter.FighterRarity))
            {
                _aura = new SummonNewAuraGraphic(SummonAttributeTheme.ColorFor(fighter.FighterAttribute)) { name = "summon-new-aura" };
                face.Add(_aura);
            }
            if (receipt.IsNew)
            {
                var icon = CharacterIconView.CreateFilling(fighter);
                var portrait = icon.Q<Image>("char-icon");
                if (portrait != null) portrait.sprite = fighter.FighterIcon;
                face.Add(icon);
                var fresh = new Label("NEW") { name = "summon-new-label", pickingMode = PickingMode.Ignore };
                fresh.AddToClassList("summon-new-label"); face.Add(fresh);
            }
            else face.Add(CreateFragment(receipt));
            _light = new SummonItemLightGraphic(receipt.IsDuplicate) { name = "summon-item-light" };
            face.Add(_light); Add(face);
            var caption = new Label(fighter.FighterName) { pickingMode = PickingMode.Ignore };
            caption.AddToClassList("summon-card-name"); Add(caption);
            RegisterCallback<DetachFromPanelEvent>(_ => { _auraTick?.Pause(); _auraTick = null; });
            SetArrival(10);
        }

        public static SummonRewardItemView Create(SummonDrawPresentation receipt, int index) => new SummonRewardItemView(receipt, index);

        public static void Populate(VisualElement grid, IReadOnlyList<SummonDrawPresentation> receipts)
        {
            if (grid == null) return;
            var sheet = Resources.Load<StyleSheet>("UI/SummonSequence");
            if (sheet != null && !grid.styleSheets.Contains(sheet)) grid.styleSheets.Add(sheet);
            grid.Clear(); grid.AddToClassList("summon-receipt-grid");
            if (receipts == null) return;
            foreach (var receipt in receipts)
            {
                if (receipt?.Character == null) continue;
                var item = Create(receipt, grid.childCount);
                item.style.width = 128; item.style.height = 156;
                grid.Add(item);
                item.StartAura();
            }
        }

        public static void StopEffects(VisualElement grid)
        {
            grid?.Query<SummonRewardItemView>().ForEach(item =>
            {
                item._auraTick?.Pause();
                item._auraTick = null;
            });
        }

        public override void SetArrival(float age)
        {
            var progress = Mathf.Clamp01(age / RiseDuration);
            var eased = progress * progress * (3 - 2 * progress);
            style.scale = new Scale(Vector3.one);
            style.opacity = eased;
            style.translate = new Translate(0, (1 - eased) * 16);
            _light.WhiteFlash = age >= 0 && age < .12f ? (1 - age / .12f) * .92f : 0;
            _light.SettleGlint = Mathf.Clamp01((age - RiseDuration) / GlintDuration);
            _light.GlintVisible = age >= RiseDuration && age < RiseDuration + GlintDuration;
            _light.MarkDirtyRepaint();
        }

        public override void SetTime(float elapsed)
        {
            if (_aura == null) return;
            _aura.TimeValue = elapsed; _aura.MarkDirtyRepaint();
        }

        private void StartAura()
        {
            if (_aura == null) return;
            _auraTick?.Pause();
            _auraTick = schedule.Execute(() => SetTime((float)Time.realtimeSinceStartupAsDouble)).Every(33);
        }

        private static VisualElement CreateFragment(SummonDrawPresentation receipt)
        {
            var fighter = receipt.Character;
            var art = CharacterIconArtSO.Load();
            var rarity = art?.Rarity(fighter.FighterRarity) ?? art?.Rarity(FighterRarity.SSR);
            var fragment = Box("summon-fragment", "summon-fragment");
            fragment.userData = receipt;
            fragment.Add(new SummonFragmentPortrait(fighter.FighterIcon, rarity?.background, fighter.FighterRarity) { name = "fragment-portrait" });
            AddImage(fragment, "fragment-attribute", art?.Attribute(fighter.FighterAttribute), "fragment-attribute");
            AddImage(fragment, "fragment-series", art?.Series(fighter.SeriesId), "fragment-series");
            AddImage(fragment, "fragment-rarity", rarity?.label, "fragment-rarity");
            var strip = Box(null, "fragment-memory-strip");
            var kind = new Label("MEMORY") { name = "fragment-kind", pickingMode = PickingMode.Ignore };
            kind.AddToClassList("fragment-kind"); kind.AddToClassList("condensed-heavy"); strip.Add(kind);
            var value = receipt.FragmentCount > 0 ? receipt.FragmentCount.ToString() : receipt.ConstellationTier >= 0 ? "C" + receipt.ConstellationTier : string.Empty;
            var count = new Label(value) { name = "fragment-count", pickingMode = PickingMode.Ignore };
            count.AddToClassList("fragment-count"); count.AddToClassList("condensed-heavy");
            if (string.IsNullOrEmpty(value)) count.style.display = DisplayStyle.None;
            strip.Add(count); fragment.Add(strip);
            fragment.tooltip = receipt.FragmentCount > 0 ? "Character token ×" + receipt.FragmentCount : receipt.ConstellationTier >= 0 ? "Constellation advanced to C" + receipt.ConstellationTier : "Duplicate character memory";
            return fragment;
        }

        private static void AddImage(VisualElement parent, string name, Sprite sprite, string cls)
        {
            var image = new Image { name = name, sprite = sprite, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            image.AddToClassList(cls); if (sprite == null) image.style.display = DisplayStyle.None;
            parent.Add(image);
        }

        private static VisualElement Box(string name, string cls)
        {
            var box = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            box.AddToClassList(cls); return box;
        }
    }

    /// <summary>A bevelled six-sided memory tile. Portrait/background are clipped by their actual mesh.</summary>
    public sealed class SummonFragmentPortrait : VisualElement
    {
        public Sprite PortraitSprite { get; }
        public Sprite BackgroundSprite { get; }
        public FighterRarity Rarity { get; }
        internal static readonly Vector2[] Shape = { new Vector2(0, .13f), new Vector2(.13f, 0), new Vector2(.74f, 0), new Vector2(1, .26f), new Vector2(1, 1), new Vector2(0, 1) };
        public SummonFragmentPortrait(Sprite portrait, Sprite background, FighterRarity rarity)
        {
            PortraitSprite = portrait; BackgroundSprite = background; Rarity = rarity;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute; style.left = style.right = style.top = 0; style.bottom = Length.Percent(12);
            generateVisualContent += Draw;
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect; if (rect.width <= 0 || rect.height <= 0) return;
            var color = Rarity >= FighterRarity.SSR ? new Color(.28f, .7f, 1f) : Rarity == FighterRarity.SR ? new Color(.7f, .47f, .31f) : new Color(.55f, .7f, .8f);
            var p = context.painter2D;
            Path(p, rect, 0); p.fillColor = Color.Lerp(color, Color.black, .46f); p.Fill();
            DrawSprite(context, BackgroundSprite, rect, .027f);
            DrawSprite(context, PortraitSprite, rect, .027f);
            Path(p, rect, .017f); p.lineWidth = rect.width * .035f; p.strokeColor = color; p.Stroke();
            Path(p, rect, .035f); p.lineWidth = Mathf.Max(.65f, rect.width * .009f); p.strokeColor = new Color(1, 1, 1, .7f); p.Stroke();
        }

        internal static void Path(Painter2D p, Rect rect, float inset)
        {
            p.BeginPath();
            for (var i = 0; i < Shape.Length; i++)
            {
                var point = new Vector2(rect.x + rect.width * (inset + Shape[i].x * (1 - inset * 2)), rect.y + rect.height * (inset + Shape[i].y * (1 - inset * 2)));
                if (i == 0) p.MoveTo(point); else p.LineTo(point);
            }
            p.ClosePath();
        }

        private static void DrawSprite(MeshGenerationContext context, Sprite sprite, Rect rect, float inset)
        {
            if (sprite == null) return;
            var uv = DataUtility.GetOuterUV(sprite);
            var mesh = context.Allocate(Shape.Length, (Shape.Length - 2) * 3, sprite.texture);
            var region = mesh.uvRegion;
            for (var i = 0; i < Shape.Length; i++)
            {
                var point = Shape[i];
                var textureUV = new Vector2(Mathf.Lerp(uv.x, uv.z, point.x), Mathf.Lerp(uv.w, uv.y, point.y));
                mesh.SetNextVertex(new Vertex { position = new Vector3(rect.x + rect.width * (inset + point.x * (1 - inset * 2)), rect.y + rect.height * (inset + point.y * (1 - inset * 2)), Vertex.nearZ), tint = Color.white,
                    uv = new Vector2(region.x + textureUV.x * region.width, region.y + textureUV.y * region.height) });
            }
            for (ushort i = 1; i < Shape.Length - 1; i++)
            { mesh.SetNextIndex(0); mesh.SetNextIndex(i); mesh.SetNextIndex((ushort)(i + 1)); }
        }
    }

    public sealed class SummonItemLightGraphic : VisualElement
    {
        public float WhiteFlash { get; set; }
        public float SettleGlint { get; set; }
        public bool GlintVisible { get; set; }
        private readonly bool _fragment;
        public SummonItemLightGraphic(bool fragment)
        {
            _fragment = fragment; pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute; style.left = style.right = style.top = 0;
            style.bottom = fragment ? Length.Percent(12) : new Length(0);
            generateVisualContent += Draw;
        }
        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect; if (rect.width <= 0 || rect.height <= 0) return;
            var p = context.painter2D;
            if (WhiteFlash > 0)
            {
                if (_fragment) SummonFragmentPortrait.Path(p, rect, .02f);
                else { p.BeginPath(); p.MoveTo(Vector2.zero); p.LineTo(new Vector2(rect.width, 0)); p.LineTo(rect.size); p.LineTo(new Vector2(0, rect.height)); p.ClosePath(); }
                p.fillColor = new Color(1, 1, 1, WhiteFlash); p.Fill();
            }
            if (!GlintVisible) return;
            var alpha = Mathf.Sin(SettleGlint * Mathf.PI);
            var center = new Vector2(rect.width * (.08f + .84f * SettleGlint), rect.height * (.93f - .86f * SettleGlint));
            p.strokeColor = new Color(1, 1, 1, alpha * .95f); p.lineWidth = 1.2f;
            p.BeginPath(); p.MoveTo(center - Vector2.right * rect.width * .16f); p.LineTo(center + Vector2.right * rect.width * .16f); p.Stroke();
            p.BeginPath(); p.MoveTo(center - Vector2.up * rect.height * .09f); p.LineTo(center + Vector2.up * rect.height * .09f); p.Stroke();
            SummonNewAuraGraphic.Glow(context, center, new Vector2(rect.width * .22f, rect.height * .12f), new Color(1, 1, 1, alpha * .72f));
        }
    }

    internal sealed class SummonNewAuraGraphic : VisualElement
    {
        public float TimeValue;
        private readonly Color _accent;
        public Color Accent => _accent;
        public SummonNewAuraGraphic(Color accent)
        {
            _accent = accent; pickingMode = PickingMode.Ignore;
            AddToClassList("summon-new-aura");
            generateVisualContent += Draw;
        }
        private void Draw(MeshGenerationContext context)
        {
            var rect = contentRect; if (rect.width <= 0 || rect.height <= 0) return;
            var center = rect.center;
            var pulse = .88f + .12f * Mathf.Sin(TimeValue * 1.8f);
            Glow(context, center, rect.size * .5f, new Color(_accent.r, _accent.g, _accent.b, .4f * pulse));
            Glow(context, center, rect.size * .36f, new Color(1, .97f, .77f, .45f * pulse));
            var p = context.painter2D;
            for (var i = 0; i < 12; i++)
            {
                var angle = i * Mathf.PI / 6 + TimeValue * .045f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var normal = new Vector2(-direction.y, direction.x);
                var inner = center + direction * rect.width * .18f;
                var outer = center + direction * rect.width * (.42f + .03f * Mathf.Sin(i + TimeValue));
                p.fillColor = new Color(1, .97f, .78f, .13f * pulse);
                p.BeginPath(); p.MoveTo(inner - normal * rect.width * .015f); p.LineTo(outer);
                p.LineTo(inner + normal * rect.width * .015f); p.ClosePath(); p.Fill();
            }
        }
        internal static void Glow(MeshGenerationContext context, Vector2 center, Vector2 radius, Color color)
        {
            const int segments = 40;
            var mesh = context.Allocate(segments + 1, segments * 3);
            mesh.SetNextVertex(new Vertex { position = new Vector3(center.x, center.y, Vertex.nearZ), tint = color });
            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2 / segments;
                mesh.SetNextVertex(new Vertex { position = new Vector3(center.x + Mathf.Cos(angle) * radius.x, center.y + Mathf.Sin(angle) * radius.y, Vertex.nearZ), tint = new Color(color.r, color.g, color.b, 0) });
            }
            for (ushort i = 0; i < segments; i++)
            { mesh.SetNextIndex(0); mesh.SetNextIndex((ushort)(i + 1)); mesh.SetNextIndex((ushort)((i + 1) % segments + 1)); }
        }
    }
}
