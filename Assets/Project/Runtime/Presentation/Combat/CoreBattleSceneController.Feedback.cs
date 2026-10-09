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
            SetUltimateHud(true);
            var fighter = _displayState.Player.FindFighter(item.SourceId) ?? _displayState.Opponent.FindFighter(item.SourceId);
            if (_stage != null)
                yield return _stage.PrepareUltimate(fighter?.Side ?? _displayState.ActingSide);
            if (_effectsLayer == null) yield break;
            var character = fighter == null ? null : LoadCharacter(fighter.Definition.Id);
            var overlay = new VisualElement { name = "ultimate-cut-in", pickingMode = PickingMode.Ignore };
            _ultimateOverlay = overlay;
            overlay.AddToClassList("ultimate-cut-in");
            var streaks = new List<VisualElement>();
            for (var i = 0; i < 9; i++)
            {
                var streak = new VisualElement { pickingMode = PickingMode.Ignore };
                streak.AddToClassList("ultimate-speed-line");
                streak.style.top = Length.Percent(14 + i * 8);
                streak.style.height = i % 3 == 0 ? 10 : 3;
                streak.style.opacity = .25f + i % 3 * .2f;
                overlay.Add(streak); streaks.Add(streak);
            }
            var slash = new VisualElement { pickingMode = PickingMode.Ignore };
            slash.AddToClassList("ultimate-slash");
            overlay.Add(slash);
            var portrait = new Image { sprite = character?.FighterFull != null ? character.FighterFull : character?.FighterIcon,
                scaleMode = ScaleMode.ScaleToFit, pickingMode = PickingMode.Ignore };
            portrait.AddToClassList("ultimate-cut-in-portrait");
            slash.Add(portrait);
            var copy = new VisualElement { pickingMode = PickingMode.Ignore };
            copy.AddToClassList("ultimate-cut-in-copy");
            var kicker = new Label("ULTIMATE") { pickingMode = PickingMode.Ignore };
            kicker.AddToClassList("ultimate-cut-in-kicker"); copy.Add(kicker);
            var title = new Label(character?.Ultimate?.ultimateName ?? "Ultimate Move") { pickingMode = PickingMode.Ignore };
            title.AddToClassList("ultimate-cut-in-title"); copy.Add(title);
            var name = new Label(character?.FighterName ?? fighter?.Definition.DisplayName ?? "") { pickingMode = PickingMode.Ignore };
            name.AddToClassList("ultimate-cut-in-name"); copy.Add(name);
            overlay.Add(copy);
            _effectsLayer.Add(overlay);
            _stage?.PlayCue(4);
            try
            {
                var width = Mathf.Max(600f, _effectsLayer.resolvedStyle.width);
                yield return BattleStagePresenter.Tween(.22f, t =>
                {
                    overlay.style.opacity = Mathf.Clamp01(t * 4f);
                    slash.style.translate = new Translate(-width * (1 - t), 0);
                    portrait.style.translate = new Translate(-width * .2f * (1 - t), 0);
                    copy.style.translate = new Translate(width * .25f * (1 - t), 0);
                });
                yield return BattleStagePresenter.Tween(.65f, t =>
                {
                    portrait.style.scale = new Scale(Vector3.one * (1f + t * .08f));
                    for (var i = 0; i < streaks.Count; i++)
                        streaks[i].style.translate = new Translate((t * (i % 2 == 0 ? 1f : -1f)) * width * .25f, 0);
                });
                yield return BattleStagePresenter.Tween(.2f, t =>
                {
                    slash.style.translate = new Translate(width * t, 0);
                    copy.style.opacity = 1 - t;
                    overlay.style.opacity = 1f - t * t;
                });
            }
            finally
            {
                overlay.RemoveFromHierarchy();
                if (_ultimateOverlay == overlay) _ultimateOverlay = null;
            }
        }

        private Dictionary<string, Vector2> CaptureHandPositions()
        {
            if (_releasedHandPositions != null)
            {
                var released = _releasedHandPositions;
                _releasedHandPositions = null;
                return released;
            }
            var positions = new Dictionary<string, Vector2>();
            foreach (var pair in _handCardViews)
                if (pair.Value != null) positions[pair.Key] = new Vector2(pair.Value.style.left.value.value + pair.Value.resolvedStyle.translate.x,
                    pair.Value.resolvedStyle.translate.y);
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
            yield return BattleStagePresenter.Tween(.12f, t =>
            {
                foreach (var pair in offsets)
                {
                    var lifted = movedId != null && _handCardViews.TryGetValue(movedId, out var moved) && moved == pair.Key;
                    pair.Key.style.translate = new Translate(pair.Value.x * (1 - t), pair.Value.y * (1 - t) + (lifted ? -12 * Mathf.Sin(t * Mathf.PI) : 0));
                    pair.Key.style.scale = new Scale(Vector3.one * (lifted ? 1 + .08f * Mathf.Sin(t * Mathf.PI) : 1));
                }
            });
            foreach (var pair in offsets) { pair.Key.style.translate = new Translate(0, 0); pair.Key.style.scale = new Scale(Vector3.one); }
        }

        private IEnumerator MergeImpact(Vector2 position, float width, float height)
        {
            if (_effectsLayer == null) yield break;
            var flash = new VisualElement { name = "merge-impact", pickingMode = PickingMode.Ignore };
            flash.AddToClassList("merge-flash");
            flash.style.width = width + 8;
            flash.style.height = height + 8;
            flash.style.left = position.x - (width + 8) * .5f;
            flash.style.top = position.y - (height + 8) * .5f;
            _effectsLayer.Add(flash);
            _stage?.PlayCue(3);
            yield return BattleStagePresenter.Tween(.18f, t =>
            {
                flash.style.scale = new Scale(new Vector3(1 + t * .45f, 1 + t * .2f, 1));
                flash.style.opacity = t < .2f ? 1 : Mathf.Pow((1 - t) / .8f, 2);
            }, false);
            flash.RemoveFromHierarchy();
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
