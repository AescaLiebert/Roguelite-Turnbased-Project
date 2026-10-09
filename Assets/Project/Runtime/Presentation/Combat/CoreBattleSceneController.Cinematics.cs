using System.Collections;
using System.Collections.Generic;
using FightingAllstar.Core.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Combat
{
    public sealed partial class CoreBattleSceneController
    {
        private VisualElement _damageTotalElement;
        private Label _damageTotalLabel;
        private bool _damageTotalActionOpen;
        private float _damageTotalLastImpact;
        private VisualElement _ultimateOverlay;

        private static bool HasQueuedExecution(IReadOnlyList<BattleEvent> events, int index)
        {
            for (var i = index + 1; i < events.Count; i++)
            {
                var kind = events[i].Kind;
                if (kind == BattleEventKind.TurnEnded || kind == BattleEventKind.TurnStarted ||
                    kind == BattleEventKind.StatusResolutionStarted || kind == BattleEventKind.BattleCompleted) return false;
                if (kind == BattleEventKind.CardPlayed || kind == BattleEventKind.CounterStarted) return true;
            }
            return false;
        }

        private IEnumerator PresentTurnChange(TeamSide side)
        {
            yield return _stage.RecoverAttacker();
            // Announce the incoming side while the arena orbits to its overhead view.
            var cameraMove = _stage.StartCoroutine(_stage.Turn(side, .75f));
            yield return Banner(side == TeamSide.Player ? "YOUR TURN" : "ENEMY TURN", side == TeamSide.Opponent, .55f);
            yield return cameraMove;
        }

        private void SetUltimateHud(bool cinematic)
        {
            if (_tray != null) _tray.style.opacity = cinematic ? 0 : 1;
            if (_topRightControls != null) _topRightControls.style.opacity = cinematic ? 0 : 1;
            var root = battleDocument?.rootVisualElement;
            foreach (var name in new[] { "enemy-deck-field", "enemy-plan-field", "phase-container", "ultimate-ready-sidebar" })
            {
                var element = root?.Q<VisualElement>(name);
                if (element != null) element.style.opacity = cinematic ? 0 : 1;
            }
        }

        private void ResetDamageTotal(bool actionOpen = false)
        {
            if (_damageTotalHideCoroutine != null) StopCoroutine(_damageTotalHideCoroutine);
            _damageTotalHideCoroutine = null;
            _damageTotalOwnerId = null;
            _damageTotalAmount = 0;
            _damageTotalActionOpen = actionOpen;
            if (damageTotalPanel != null) damageTotalPanel.SetActive(false);
            if (_damageTotalElement != null) _damageTotalElement.style.display = DisplayStyle.None;
        }

        private void EnsureDamageTotal()
        {
            if (_effectsLayer == null || _damageTotalElement != null && _damageTotalElement.parent == _effectsLayer) return;
            _damageTotalElement = new VisualElement { name = "damage-total", pickingMode = PickingMode.Ignore };
            _damageTotalElement.AddToClassList("damage-total");
            var burst = new ImpactBurst { pickingMode = PickingMode.Ignore };
            burst.AddToClassList("damage-total-burst");
            _damageTotalElement.Add(burst);
            var title = new Label("TOTAL DAMAGE") { pickingMode = PickingMode.Ignore };
            title.AddToClassList("damage-total-title");
            _damageTotalElement.Add(title);
            _damageTotalLabel = new Label { name = "damage-total-value", pickingMode = PickingMode.Ignore };
            _damageTotalLabel.AddToClassList("damage-total-value");
            _damageTotalElement.Add(_damageTotalLabel);
            _effectsLayer.Add(_damageTotalElement);
        }

        private sealed class ImpactBurst : VisualElement
        {
            public ImpactBurst()
            {
                generateVisualContent += context =>
                {
                    var painter = context.painter2D;
                    var center = contentRect.center;
                    painter.fillColor = new Color(1f, .24f, .3f, .45f);
                    painter.BeginPath();
                    for (var i = 0; i < 24; i++)
                    {
                        var angle = i * Mathf.PI / 12f - .2f;
                        var radius = i % 2 == 0 ? 1f : .55f;
                        var point = center + new Vector2(Mathf.Cos(angle) * contentRect.width * .5f,
                            Mathf.Sin(angle) * contentRect.height * .5f) * radius;
                        if (i == 0) painter.MoveTo(point); else painter.LineTo(point);
                    }
                    painter.ClosePath(); painter.Fill();
                };
            }
        }

        private void ClearCinematicHud()
        {
            ResetDamageTotal();
            _ultimateOverlay?.RemoveFromHierarchy();
            _ultimateOverlay = null;
            SetUltimateHud(false);
        }
    }
}
