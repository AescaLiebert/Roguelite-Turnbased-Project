using System.Collections;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Combat
{
    public sealed partial class CoreBattleSceneController
    {
        private const float EnemyCardWidth = 52f;
        private const float EnemyCardHeight = 80f;
        private VisualElement _enemyDeckField;
        private VisualElement _enemyHandRow;
        private readonly Dictionary<string, VisualElement> _enemyHandCardViews =
            new Dictionary<string, VisualElement>();

        private void BindEnemyHand(VisualElement root)
        {
            BindEnemyPlan(root);
            _enemyDeckField = root?.Q<VisualElement>("enemy-deck-field");
            _enemyHandRow = root?.Q<VisualElement>("enemy-deck-row");
            if (_enemyHandRow == null) return;
            _enemyHandRow.style.position = Position.Relative;
            _enemyHandRow.RegisterCallback<GeometryChangedEvent>(_ => LayoutEnemyHandCards());
        }

        private void SetEnemyHandVisible(bool visible)
        {
            if (_enemyDeckField != null)
                _enemyDeckField.style.visibility = visible ? Visibility.Visible : Visibility.Hidden;
        }

        private void RenderEnemyHand()
        {
            if (_enemyHandRow == null) return;
            if (!_hudRevealed || _snapshot == null)
            {
                ClearEnemyHandViews();
                return;
            }

            var team = _isPlayingEvents || _isAnimatingCards || !_openingSequenceComplete
                ? _displayState?.Opponent : _snapshot.Opponent;
            if (team == null)
            {
                ClearEnemyHandViews();
                return;
            }

            var presentIds = new HashSet<string>();
            for (var index = team.Hand.Count - 1; index >= 0; index--)
            {
                var card = team.Hand[index];
                if (card == null || string.IsNullOrEmpty(card.Id)) continue;
                var owner = team.FindFighter(card.OwnerFighterId);
                presentIds.Add(card.Id);
                if (!_enemyHandCardViews.TryGetValue(card.Id, out var view) || view == null)
                {
                    view = CreateEnemyCardBack(card);
                    _enemyHandCardViews[card.Id] = view;
                    _enemyHandRow.Add(view);
                }
                else UpdateEnemyCardBack(view, card);
                view.EnableInClassList("card-disabled-by-status", IsCardDisabledByStatus(card, owner));
                if (!_isPlayingEvents && !_isAnimatingCards)
                {
                    view.style.translate = new Translate(0, 0);
                    view.style.rotate = new Rotate(new Angle(0, AngleUnit.Degree));
                    view.style.scale = new Scale(Vector3.one);
                    view.style.opacity = 1f;
                    view.RemoveFromClassList("rank-up");
                    view.RemoveFromClassList("rank-down");
                }
            }

            foreach (var id in _enemyHandCardViews.Keys.Where(id => !presentIds.Contains(id)).ToList())
            {
                _enemyHandCardViews[id]?.RemoveFromHierarchy();
                _enemyHandCardViews.Remove(id);
            }

            for (var visualIndex = 0; visualIndex < team.Hand.Count; visualIndex++)
            {
                var logicalIndex = team.Hand.Count - 1 - visualIndex;
                var card = team.Hand[logicalIndex];
                if (card == null || !_enemyHandCardViews.TryGetValue(card.Id, out var view)) continue;
                if (view.parent != _enemyHandRow || _enemyHandRow.IndexOf(view) != visualIndex)
                    _enemyHandRow.Insert(visualIndex, view);
            }
            LayoutEnemyHandCards();
        }

        private void ClearEnemyHandViews()
        {
            foreach (var view in _enemyHandCardViews.Values)
                view?.RemoveFromHierarchy();
            _enemyHandCardViews.Clear();
        }

        private VisualElement CreateEnemyCardBack(CardState card)
        {
            var root = new VisualElement
            {
                name = "Enemy Card",
                pickingMode = PickingMode.Ignore
            };
            root.AddToClassList("enemy-hand-card");
            root.style.position = Position.Absolute;
            root.style.width = EnemyCardWidth;
            root.style.height = EnemyCardHeight;
            root.style.top = 0;
            root.style.opacity = 1f;

            var back = new VisualElement { pickingMode = PickingMode.Ignore };
            back.AddToClassList("enemy-card-back");
            var inner = new VisualElement { pickingMode = PickingMode.Ignore };
            inner.AddToClassList("enemy-card-back-inner");
            var sigil = new Label("◆") { pickingMode = PickingMode.Ignore };
            sigil.AddToClassList("enemy-card-sigil");
            inner.Add(sigil);
            back.Add(inner);

            var frame = new Image { name = "enemy-card-rank-frame", pickingMode = PickingMode.Ignore };
            frame.AddToClassList("enemy-card-rank-frame");
            back.Add(frame);
            var pips = new Label { name = "enemy-card-rank-pips", pickingMode = PickingMode.Ignore };
            pips.AddToClassList("enemy-card-rank-pips");
            back.Add(pips);
            var badge = new Label { name = "enemy-card-rank-badge", pickingMode = PickingMode.Ignore };
            badge.AddToClassList("enemy-card-rank-badge");
            back.Add(badge);
            var disabledOverlay = new VisualElement
            {
                name = "enemy-card-disabled-overlay",
                pickingMode = PickingMode.Ignore
            };
            disabledOverlay.AddToClassList("enemy-card-disabled-overlay");
            back.Add(disabledOverlay);
            back.Insert(0, new CardEnergyElement());
            root.Add(back);

            UpdateEnemyCardBack(root, card);
            return root;
        }

        private void UpdateEnemyCardBack(VisualElement view, CardState card)
        {
            var energy = view.Q<CardEnergyElement>("card-energy");
            if (energy != null) { energy.Ultimate = card?.Kind == CardKind.Ultimate; energy.MarkDirtyRepaint(); }
            var rank = Mathf.Clamp(card?.Rank ?? 1, 1, 3);
            view.EnableInClassList("rank-one", rank == 1);
            view.EnableInClassList("rank-two", rank == 2);
            view.EnableInClassList("rank-three", rank == 3);
            var badge = view.Q<Label>("enemy-card-rank-badge");
            if (badge != null) badge.text = rank == 3 ? "III" : rank == 2 ? "II" : "I";
            var pips = view.Q<Label>("enemy-card-rank-pips");
            if (pips != null) pips.text = new string('◆', rank);
            var frame = view.Q<Image>("enemy-card-rank-frame");
            if (frame != null)
            {
                frame.image = RankFrameTexture(rank);
                frame.scaleMode = ScaleMode.StretchToFill;
                frame.style.display = frame.image == null ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        private void LayoutEnemyHandCards()
        {
            if (_enemyHandRow == null || _enemyHandRow.childCount == 0) return;
            var rowWidth = _enemyHandRow.resolvedStyle.width;
            if (rowWidth <= 0f) return;
            var count = _enemyHandRow.childCount;
            const float leftMargin = 4f;
            const float rightMargin = 4f;
            var capacity = Mathf.Max(count, _displayState?.Opponent.HandCapacity ?? _snapshot?.Opponent.HandCapacity ?? 7);
            var step = HandStep(rowWidth, EnemyCardWidth, capacity, leftMargin + rightMargin);
            var left = Mathf.Max(leftMargin, rowWidth - rightMargin - EnemyCardWidth - (count - 1) * step);
            for (var index = 0; index < count; index++)
            {
                var card = _enemyHandRow[index];
                card.style.left = left + index * step;
                card.style.width = EnemyCardWidth;
                card.style.height = EnemyCardHeight;
            }
        }

        private Dictionary<string, VisualElement> HandCardViews(TeamSide side) =>
            side == TeamSide.Player ? _handCardViews : _enemyHandCardViews;

        private static bool IsCardDisabledByStatus(CardState card, FighterState owner)
        {
            if (card == null || owner == null) return false;
            EffectDefinition effect;
            var hasEffect = card.Kind == CardKind.Skill
                ? CardRules.TryGetSkill(owner.Definition, card.SkillId, card.Rank, out effect)
                : CardRules.TryGetUltimate(owner.Definition, card.UltimateTier, out effect);
            return StatusSystem.IsCardUseBlocked(owner, CardRules.GetEffectCategory(card), card.Rank,
                card.Kind == CardKind.Ultimate, hasEffect && effect.Sequence != null && effect.Sequence.Count > 0);
        }

        private void RefreshCardAvailability(string fighterId)
        {
            if (string.IsNullOrEmpty(fighterId) || _displayState == null) return;
            var fighter = _displayState.Player.FindFighter(fighterId) ?? _displayState.Opponent.FindFighter(fighterId);
            if (fighter == null) return;
            RenderHand(fighter.Side);
        }

        private void RenderHand(TeamSide side)
        {
            if (side == TeamSide.Player) RenderHand();
            else RenderEnemyHand();
        }

        private Dictionary<string, Vector2> CaptureHandPositions(TeamSide side)
        {
            if (side == TeamSide.Player) return CaptureHandPositions();
            var positions = new Dictionary<string, Vector2>();
            foreach (var pair in _enemyHandCardViews)
                if (pair.Value != null) positions[pair.Key] = new Vector2(pair.Value.style.left.value.value + pair.Value.resolvedStyle.translate.x,
                    pair.Value.resolvedStyle.translate.y);
            return positions;
        }

        private IEnumerator ReflowHand(Dictionary<string, Vector2> before, string movedId, TeamSide side)
        {
            if (side == TeamSide.Player)
            {
                yield return ReflowHand(before, movedId);
                yield break;
            }

            var offsets = new Dictionary<VisualElement, Vector2>();
            foreach (var pair in _enemyHandCardViews)
            {
                if (pair.Value == null || !before.TryGetValue(pair.Key, out var start)) continue;
                offsets[pair.Value] = start - new Vector2(pair.Value.style.left.value.value, 0);
            }
            _stage?.PlayCue(0);
            yield return BattleStagePresenter.Tween(.12f, t =>
            {
                foreach (var pair in offsets)
                {
                    var lifted = movedId != null && _enemyHandCardViews.TryGetValue(movedId, out var moved) && moved == pair.Key;
                    pair.Key.style.translate = new Translate(pair.Value.x * (1 - t), pair.Value.y * (1 - t) + (lifted ? 12 * Mathf.Sin(t * Mathf.PI) : 0));
                    pair.Key.style.scale = new Scale(Vector3.one * (lifted ? 1 + .08f * Mathf.Sin(t * Mathf.PI) : 1));
                }
            });
            foreach (var pair in offsets)
            {
                pair.Key.style.translate = new Translate(0, 0);
                pair.Key.style.scale = new Scale(Vector3.one);
            }
        }

        private IEnumerator AnimateEnemyCardUse(BattleEvent item)
        {
            var before = CaptureHandPositions(TeamSide.Opponent);
            _enemyHandCardViews.TryGetValue(item.CardId ?? string.Empty, out var view);
            if (view != null)
            {
                view.BringToFront();
                var visualRoot = battleDocument?.rootVisualElement;
                var destination = visualRoot == null ? new Vector2(0, 130) : visualRoot.worldBound.center - view.worldBound.center;
                _stage?.PlayCue(2);
                yield return BattleStagePresenter.Tween(.24f, t =>
                {
                    view.style.translate = new Translate(destination.x * t, destination.y * t - 18 * Mathf.Sin(t * Mathf.PI));
                    view.style.scale = new Scale(Vector3.one * Mathf.Lerp(1f, .82f, t));
                    view.style.opacity = 1 - t;
                });
            }
            BattlePlaybackState.Apply(_displayState, item);
            RenderEnemyHand();
            yield return ReflowHand(before, null, TeamSide.Opponent);
        }
    }
}
