using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Navigation
{
    /// <summary>A display cell for an already granted reward, shared by character and item receipts.</summary>
    public abstract class ObtainRewardCell : VisualElement
    {
        public abstract void SetArrival(float age);
        public virtual void SetTime(float elapsed) { }
    }

    internal sealed class ObtainItemRewardCell : ObtainRewardCell
    {
        private readonly SummonItemLightGraphic _light;

        public ObtainItemRewardCell(VisualElement tile, int index)
        {
            name = "obtain-item-" + index;
            AddToClassList("summon-reward-card"); AddToClassList("obtain-item-card");
            var face = new VisualElement(); face.AddToClassList("obtain-item-face");
            face.Add(tile); Add(face);
            var side = SummonSequenceView.ObtainTileSize;
            var footerHeight = ItemRewardView.FooterHeight * side / ItemRewardView.TileSide;
            tile.style.width = tile.style.minWidth = side + 4;
            tile.style.height = side + footerHeight + 4;
            tile.style.flexDirection = FlexDirection.Column; tile.style.justifyContent = Justify.FlexStart;
            tile.style.backgroundColor = Color.clear;
            var frame = tile.Q(className: "item-reward-frame");
            if (frame != null) { frame.style.width = side; frame.style.height = side + footerHeight; frame.style.flexShrink = 0; }
            var icon = tile.Q(className: "item-reward-icon");
            if (icon != null) icon.style.height = side - 4;
            var amount = tile.Q<Label>(className: "item-reward-amount");
            if (amount != null)
            {
                amount.style.height = footerHeight; amount.style.fontSize = 20; amount.style.color = Color.white;
                amount.style.backgroundColor = new Color(.03f, .08f, .14f, .82f);
            }
            var label = tile.Q<Label>(className: "item-reward-name");
            if (label != null)
            {
                label.style.color = new Color(.10f, .15f, .21f); label.style.fontSize = 12;
                label.style.height = 28; label.style.flexShrink = 0; label.style.marginTop = 2;
                label.style.overflow = Overflow.Hidden;
            }
            _light = new SummonItemLightGraphic(false);
            face.Add(_light);
        }

        public override void SetArrival(float age)
        {
            var progress = Mathf.Clamp01(age / SummonRewardItemView.RiseDuration);
            var eased = progress * progress * (3 - 2 * progress);
            style.scale = new Scale(Vector3.one); style.opacity = eased;
            style.translate = new Translate(0, (1 - eased) * 16);
            _light.WhiteFlash = age >= 0 && age < .12f ? (1 - age / .12f) * .92f : 0;
            _light.SettleGlint = Mathf.Clamp01((age - SummonRewardItemView.RiseDuration) / SummonRewardItemView.GlintDuration);
            _light.GlintVisible = age >= SummonRewardItemView.RiseDuration && age < SummonRewardItemView.RiseDuration + SummonRewardItemView.GlintDuration;
            _light.MarkDirtyRepaint();
        }
    }
}
