using System.Collections;
using System.Collections.Generic;
using FightingAllstar.Core.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Combat
{
    public sealed partial class CoreBattleSceneController
    {
        private VisualElement _enemyPlanField;
        private VisualElement _enemyPlanRow;
        private Label _enemyPlanTitle;
        private readonly List<VisualElement> _enemyPlanSlots = new List<VisualElement>();

        private void BindEnemyPlan(VisualElement root)
        {
            _enemyPlanField = new VisualElement { name = "enemy-plan-field", pickingMode = PickingMode.Ignore };
            _enemyPlanField.AddToClassList("enemy-plan-field");
            _enemyPlanTitle = new Label("ENEMY PLAN") { pickingMode = PickingMode.Ignore };
            _enemyPlanTitle.AddToClassList("enemy-deck-title");
            _enemyPlanRow = new VisualElement { name = "enemy-plan-row", pickingMode = PickingMode.Ignore };
            _enemyPlanRow.AddToClassList("enemy-plan-row");
            _enemyPlanField.Add(_enemyPlanTitle);
            _enemyPlanField.Add(_enemyPlanRow);
            root.Add(_enemyPlanField);
            ClearEnemyPlan();
        }

        private IEnumerator PresentCommittedPlan(BattleEvent item)
        {
            BattlePlaybackState.Apply(_displayState, item);
            var plan = item.Plan;
            if (plan == null) yield break;
            _executionEvents.Clear();
            foreach (var queued in plan.Actions)
                _executionEvents.Add(new BattleEvent { Kind = queued.Action.IsMove ? BattleEventKind.CardMoved : BattleEventKind.CardPlayed,
                    CardId = queued.Action.CardId, SourceId = queued.Card?.OwnerFighterId, Card = queued.Card?.Clone(),
                    TargetId = queued.Action.TargetFighterId, DestinationIndex = queued.Action.DestinationIndex });
            _executionIndex = 0;
            if (plan.Side == TeamSide.Player)
            {
                PopulatePlayerExecutionSlots();
                yield break;
            }

            ClearEnemyPlan();
            SetPhase(BattlePresentationPhase.EnemyPlanning);
            _enemyPlanField.style.display = DisplayStyle.Flex;
            _enemyPlanTitle.text = plan.Actions.Count == 0 ? "ENEMY PLAN · PASS" : "ENEMY PLAN · SELECTING";
            foreach (var queued in plan.Actions)
            {
                var slot = new VisualElement { pickingMode = PickingMode.Ignore };
                slot.AddToClassList("enemy-plan-slot");
                var order = new Label((_enemyPlanSlots.Count + 1).ToString()) { pickingMode = PickingMode.Ignore };
                order.AddToClassList("enemy-plan-order");
                slot.Add(order);
                _enemyPlanRow.Add(slot);
                _enemyPlanSlots.Add(slot);
            }
            // Allow flex layout to resolve before calculating the flight destinations.
            yield return null;
            for (var i = 0; i < plan.Actions.Count; i++)
            {
                var queued = plan.Actions[i];
                var slot = _enemyPlanSlots[i];
                if (queued.Action.IsMove)
                {
                    var move = new Label("MOVE") { pickingMode = PickingMode.Ignore };
                    move.AddToClassList("enemy-plan-move");
                    slot.Add(move);
                }
                else if (queued.Card != null)
                {
                    var back = CreateEnemyCardBack(queued.Card);
                    back.style.left = 0;
                    back.style.top = 0;
                    back.style.opacity = 0;
                    slot.Insert(0, back);
                    if (_enemyHandCardViews.TryGetValue(queued.Action.CardId, out var source))
                    {
                        source.AddToClassList("enemy-card-selected");
                        var ghost = CreateEnemyCardBack(queued.Card);
                        ghost.style.opacity = 1;
                        _effectsLayer?.Add(ghost);
                        var start = _effectsLayer == null ? Vector2.zero : _effectsLayer.WorldToLocal(source.worldBound.position);
                        var end = _effectsLayer == null ? Vector2.zero : _effectsLayer.WorldToLocal(slot.worldBound.position);
                        ghost.style.left = start.x;
                        ghost.style.top = start.y;
                        _stage?.PlayCue(0);
                        yield return BattleStagePresenter.Tween(.22f, t =>
                        {
                            ghost.style.translate = new Translate((end.x - start.x) * t, (end.y - start.y) * t - 24f * Mathf.Sin(t * Mathf.PI));
                        });
                        ghost.RemoveFromHierarchy();
                    }
                    back.style.opacity = 1;
                }
                _enemyPlanTitle.text = "ENEMY PLAN · " + (i + 1) + " / " + plan.Actions.Count;
                yield return new WaitForSecondsRealtime(.1f);
            }
            _enemyPlanTitle.text = "ENEMY PLAN · LOCKED";
            yield return new WaitForSecondsRealtime(.25f);
        }

        private void PopulatePlayerExecutionSlots()
        {
            ClearExecution();
            for (var i = 0; i < _executionEvents.Count && i < _actionSlots.Count; i++)
            {
                var slot = _actionSlots.Count - 1 - i;
                var item = _executionEvents[i];
                _actionSlots[slot].style.display = DisplayStyle.Flex;
                _actionMarkers[slot].text = item.Kind == BattleEventKind.CardMoved ? "MOVE" : (i + 1).ToString();
                if (item.Card != null && item.Kind == BattleEventKind.CardPlayed)
                {
                    var owner = _displayState.Player.FindFighter(item.SourceId) ?? _displayState.Opponent.FindFighter(item.SourceId);
                    AddCardButton(_actionContents[slot], item.Card, owner, null, null, null);
                    _actionMarkers[slot].style.display = DisplayStyle.None;
                }
            }
        }

        private void AdvanceEnemyPlan(BattleEvent item)
        {
            if (_enemyPlanField == null || _enemyPlanField.resolvedStyle.display == DisplayStyle.None) return;
            var index = _executionIndex;
            for (var i = 0; i < _enemyPlanSlots.Count; i++)
            {
                _enemyPlanSlots[i].EnableInClassList("executing", i == index);
                _enemyPlanSlots[i].style.opacity = i < index ? .25f : 1;
            }
            if (index >= 0 && index < _enemyPlanSlots.Count)
            {
                var slot = _enemyPlanSlots[index];
                slot.EnableInClassList("fizzled", item.Kind == BattleEventKind.ActionFizzled);
                var order = slot.Q<Label>(className: "enemy-plan-order");
                if (order != null && item.Kind == BattleEventKind.ActionFizzled) order.text = "SKIP";
            }
            _enemyPlanTitle.text = "ENEMY PLAN · " + (index + 1) + " / " + _enemyPlanSlots.Count;
        }

        private void ClearEnemyPlan()
        {
            _enemyPlanSlots.Clear();
            _enemyPlanRow?.Clear();
            if (_enemyPlanField != null) _enemyPlanField.style.display = DisplayStyle.None;
            foreach (var view in _enemyHandCardViews.Values) view.RemoveFromClassList("enemy-card-selected");
        }
    }
}
