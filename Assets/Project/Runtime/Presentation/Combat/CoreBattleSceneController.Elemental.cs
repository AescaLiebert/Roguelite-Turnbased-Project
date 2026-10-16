using System.Collections;
using FightingAllstar.Core.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Combat
{
    public sealed partial class CoreBattleSceneController
    {
        private static FightingAllstar.Core.Content.ElementId FeedbackElement(BattleEvent evt)
        {
            if (evt.DamageElement != FightingAllstar.Core.Content.ElementId.None) return evt.DamageElement;
            return evt.StatusRecipeId switch {
                "status.debuff.shock" => FightingAllstar.Core.Content.ElementId.Electro,
                "status.debuff.poison" => FightingAllstar.Core.Content.ElementId.Dendro,
                "status.debuff.frost-bite" => FightingAllstar.Core.Content.ElementId.Cryo,
                _ => FightingAllstar.Core.Content.ElementId.None };
        }
        private IEnumerator PresentReactionFeedback(BattleEvent evt)
        {
            if (_effectsLayer?.panel == null || Camera.main == null ||
                !_fighterViews.TryGetValue(evt.TargetId, out var view) || view == null) yield break;
            var viewport = Camera.main.WorldToViewportPoint(view.transform.position + Vector3.up * 2.75f);
            if (viewport.z <= 0) yield break;
            var group = new VisualElement { pickingMode = PickingMode.Ignore, name = "elemental-contact" };
            group.style.position = Position.Absolute;
            group.style.left = viewport.x * _effectsLayer.resolvedStyle.width - 60;
            group.style.top = (1 - viewport.y) * _effectsLayer.resolvedStyle.height - 25;
            group.style.width = 120;
            group.style.height = 40;
            Label Spark(Color color)
            {
                var label = new Label("●") { pickingMode = PickingMode.Ignore };
                label.style.position = Position.Absolute;
                label.style.fontSize = 22;
                label.style.color = color;
                label.style.unityFontDefinition = FontDefinition.FromFont(GenshinCombatTextStyle.Font);
                label.style.unityTextOutlineColor = new Color(0.05f, .03f, .04f, .65f);
                label.style.unityTextOutlineWidth = 1;
                group.Add(label);
                return label;
            }
            var incoming = Spark(GenshinCombatTextStyle.ElementColor(evt.TriggerElement));
            var setup = Spark(GenshinCombatTextStyle.ElementColor(evt.DamageElement));
            _effectsLayer.Add(group);
            _stage.Pulse(evt.TargetId, GenshinCombatTextStyle.ElementColor(evt.TriggerElement));
            // Starting sequence: two elemental contacts converge in 140 ms, followed by the reaction caption.
            yield return BattleStagePresenter.Tween(.14f, t => {
                var separation = Mathf.Lerp(28, 0, Mathf.SmoothStep(0, 1, t));
                incoming.style.left = 50 - separation;
                setup.style.left = 50 + separation;
                group.style.opacity = Mathf.Lerp(.5f, 1f, t);
            }, false);
            group.RemoveFromHierarchy();
            _stage.PlayCue(10 + (int)evt.ElementalReaction);
            _stage.Pulse(evt.TargetId, GenshinCombatTextStyle.ReactionColor(evt.ElementalReaction));
            FloatText(evt.TargetId, GenshinCombatTextStyle.ReactionName(evt.ElementalReaction) + (evt.ReactionOutputBlocked ? " · Immune" : ""), "reaction",
                reaction: evt.ElementalReaction, styleSourceId: evt.SourceId);
        }
    }
}
