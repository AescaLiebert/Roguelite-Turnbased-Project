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
        private VisualElement _readySidebar;
        private readonly Dictionary<string, VisualElement> _readyTags = new Dictionary<string, VisualElement>();


        private void SyncUltimateReady()
        {
            if (_stage == null || _effectsLayer == null) return;
            var state = _isPlayingEvents ? _displayState : _snapshot;
            if (state == null) return;
            if (_readySidebar == null || _readySidebar.parent == null)
            {
                _readyTags.Clear();
                _readySidebar = new VisualElement { name = "ultimate-ready-sidebar", pickingMode = PickingMode.Ignore };
                _readySidebar.AddToClassList("ultimate-ready-sidebar");
                _effectsLayer.Add(_readySidebar);
            }
            foreach (var fighter in state.Player.Fighters)
            {
                // A queued card is reserved, and is only consumed at its CardPlayed event.
                var hasCard = state.Player.Hand.Any(c => c.OwnerFighterId == fighter.Id && c.Kind == CardKind.Ultimate);
                if (_isPlayingEvents && _executionIndex >= 0)
                    hasCard |= _executionEvents.Skip(_executionIndex).Any(e => e.Kind == BattleEventKind.CardPlayed &&
                        e.SourceId == fighter.Id && e.Card?.Kind == CardKind.Ultimate);
                var ready = _hudRevealed && state.Phase != BattlePhase.Complete && fighter.IsAlive && !fighter.IsReserve &&
                    fighter.PowerGauge >= CardRules.UltimateGaugeCost && hasCard;
                _stage.SetUltimateReady(fighter.Id, ready);
                if (!ready)
                {
                    if (_readyTags.TryGetValue(fighter.Id, out var old)) old.RemoveFromHierarchy();
                    _readyTags.Remove(fighter.Id);
                    continue;
                }
                if (_readyTags.ContainsKey(fighter.Id)) continue;
                var tag = new VisualElement { pickingMode = PickingMode.Ignore };
                tag.AddToClassList("ultimate-ready-tag");
                var icon = new Image { sprite = LoadCharacter(fighter.Definition.Id)?.FighterIcon, pickingMode = PickingMode.Ignore };
                icon.AddToClassList("ultimate-ready-icon");
                tag.Add(icon);
                var title = new Label("Ultimate Ready") { pickingMode = PickingMode.Ignore };
                tag.Add(title);
                _readySidebar.Add(tag);
                _readyTags[fighter.Id] = tag;
                StartCoroutine(RevealReadyTag(tag));
            }
        }

        private IEnumerator RevealReadyTag(VisualElement tag)
        {
            _stage.PlayCue(3);
            yield return BattleStagePresenter.Tween(.24f, t =>
            {
                tag.style.translate = new Translate(-230 * (1 - t), 0);
                tag.style.opacity = t;
            });
        }

        private IEnumerator UltimateCutIn(BattleEvent item)
        {
            if (_effectsLayer == null) yield break;
            var fighter = _displayState.Player.FindFighter(item.SourceId) ?? _displayState.Opponent.FindFighter(item.SourceId);
            var character = fighter == null ? null : LoadCharacter(fighter.Definition.Id);
            var overlay = new VisualElement { name = "ultimate-cut-in", pickingMode = PickingMode.Ignore };
            overlay.AddToClassList("ultimate-cut-in");
            var slash = new VisualElement { pickingMode = PickingMode.Ignore };
            slash.AddToClassList("ultimate-slash");
            overlay.Add(slash);
            var portrait = new Image { sprite = character?.FighterIcon, scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            portrait.AddToClassList("ultimate-cut-in-portrait");
            slash.Add(portrait);
            var copy = new VisualElement { pickingMode = PickingMode.Ignore };
            copy.AddToClassList("ultimate-cut-in-copy");
            var kicker = new Label("ULTIMATE") { pickingMode = PickingMode.Ignore };
            kicker.AddToClassList("ultimate-cut-in-kicker");
            copy.Add(kicker);
            var title = new Label(character?.Ultimate?.ultimateName ?? "Ultimate Move") { pickingMode = PickingMode.Ignore };
            title.AddToClassList("ultimate-cut-in-title");
            copy.Add(title);
            slash.Add(copy);
            _effectsLayer.Add(overlay);
            _stage.PlayCue(4);
            yield return BattleStagePresenter.Tween(.18f, t =>
            {
                overlay.style.opacity = t;
                slash.style.translate = new Translate((1 - t) * -900, 0);
                portrait.style.translate = new Translate((1 - t) * -160, 0);
            });
            yield return BattleStagePresenter.Tween(.5f, t => portrait.style.scale = new Scale(Vector3.one * (1 + t * .07f)));
            yield return BattleStagePresenter.Tween(.16f, t =>
            {
                slash.style.translate = new Translate(1000 * t, 0);
                overlay.style.opacity = 1 - t;
            });
            overlay.RemoveFromHierarchy();
        }

        private Dictionary<string, Vector2> CaptureHandPositions()
        {
            var positions = new Dictionary<string, Vector2>();
            foreach (var pair in _handCardViews)
                if (pair.Value != null) positions[pair.Key] = new Vector2(pair.Value.style.left.value.value, 0);
            return positions;
        }

        private IEnumerator ReflowHand(Dictionary<string, Vector2> before, string movedId = null)
        {
            var offsets = new Dictionary<VisualElement, Vector2>();
            foreach (var pair in _handCardViews)
            {
                if (!before.TryGetValue(pair.Key, out var start)) continue;
                offsets[pair.Value] = start - new Vector2(pair.Value.style.left.value.value, 0);
            }
            _stage.PlayCue(0);
            yield return BattleStagePresenter.Tween(.23f, t =>
            {
                foreach (var pair in offsets)
                {
                    var lifted = movedId != null && _handCardViews.TryGetValue(movedId, out var moved) && moved == pair.Key;
                    pair.Key.style.translate = new Translate(pair.Value.x * (1 - t), lifted ? -30 * Mathf.Sin(t * Mathf.PI) : 0);
                    pair.Key.style.scale = new Scale(Vector3.one * (lifted ? 1 + .08f * Mathf.Sin(t * Mathf.PI) : 1));
                }
            });
            foreach (var pair in offsets) { pair.Key.style.translate = new Translate(0, 0); pair.Key.style.scale = new Scale(Vector3.one); }
        }

        private IEnumerator MergeFlash(VisualElement view, int rank, bool upward = true)
        {
            if (_effectsLayer == null) yield break;
            var flash = new VisualElement { pickingMode = PickingMode.Ignore };
            flash.AddToClassList("merge-flash");
            var position = _effectsLayer.WorldToLocal(view.worldBound.center);
            flash.style.left = position.x - 55;
            flash.style.top = position.y - 83;
            _effectsLayer.Add(flash);
            _stage.PlayCue(upward ? 3 : 2);
            StartCoroutine(CardCaption(view, (upward ? "RANK " : "RANK ") + (rank >= 3 ? "III" : rank == 2 ? "II" : "I")));
            yield return BattleStagePresenter.Tween(.3f, t =>
            {
                flash.style.scale = new Scale(new Vector3(1 + t * .5f, 1 + t * .45f, 1));
                flash.style.opacity = (1 - t) * (1 - t);
                view.style.scale = new Scale(Vector3.one * (1 + .14f * Mathf.Sin(t * Mathf.PI)));
            });
            view.style.scale = new Scale(Vector3.one);
            flash.RemoveFromHierarchy();
        }

        private string FeedbackName(BattleEvent item)
        {
            var id = item.StatusRecipeId ?? item.Message;
            var status = item.StatusesAfter?.Find(s => s.RecipeId == id);
            var polarity = status?.Recipe?.Polarity ?? StatusPolarity.Debuff;
            var visual = StatusVisualData.Get(id, polarity);
            if (visual != null) return visual.DisplayName;
            if (status?.Recipe != null && (status.Recipe.Behavior & StatusBehavior.Stance) != 0)
                return string.IsNullOrWhiteSpace(status.Recipe.NameKey) ? "Stance" : status.Recipe.NameKey;
            return StatusVisualData.GetDisplayName(id, polarity);
        }

        private static string FeedbackName(string value)
        {
            return StatusVisualData.GetDisplayName(value);
        }
    }
}
