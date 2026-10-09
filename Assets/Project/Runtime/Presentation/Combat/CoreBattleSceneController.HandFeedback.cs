using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Combat
{
    public sealed partial class CoreBattleSceneController
    {
        private string _dragCardId;
        private int _dragOriginalIndex = -1;
        private int _dragDestination = -1;
        private readonly Dictionary<string, float> _dragSlots = new Dictionary<string, float>();
        private CardEnergyElement _dragLanding;
        private Label _dragLandingLabel;
        private Dictionary<string, Vector2> _releasedHandPositions;

        private static bool CanMergePair(CardState a, CardState b) => a != null && b != null && a.Id != b.Id &&
            a.Kind == CardKind.Skill && b.Kind == CardKind.Skill && a.Rank < 3 && a.Rank == b.Rank &&
            a.OwnerFighterId == b.OwnerFighterId && a.SkillId == b.SkillId;

        private void SetMergeHint(VisualElement view, bool available, bool ready, int rank = 0)
        {
            var hint = view.Q<CardEnergyElement>("card-merge-hint");
            if (hint != null) { hint.Merge = available; hint.MergeReady = ready; hint.MarkDirtyRepaint(); }
            var badge = view.Q<Label>("card-merge-badge");
            if (badge != null)
            {
                // The held card already has the landing label; keep the partner's rank badge visible.
                badge.style.display = available && !view.ClassListContains("drag-held") ? DisplayStyle.Flex : DisplayStyle.None;
                badge.text = ready ? "RANK " + (rank >= 3 ? "III" : "II") : "MERGE";
                badge.EnableInClassList("merge-ready", ready);
            }
        }

        private void RefreshMergeAvailability(BattleTeamState team)
        {
            if (_dragCardId != null || team == null) return;
            foreach (var pair in _handCardViews)
            {
                var card = team.Hand.Find(item => item.Id == pair.Key);
                SetMergeHint(pair.Value, CanPlan && team.Hand.Any(other => CanMergePair(card, other)), false);
            }
        }

        // Starting values: keep spacing stable against hand capacity, including when cards are used.
        private static float HandStep(float rowWidth, float cardWidth, int capacity, float padding) =>
            Mathf.Max(1f, Mathf.Min(cardWidth, (Mathf.Max(cardWidth, rowWidth - padding) - cardWidth) / Mathf.Max(1, capacity - 1)));

        private void BeginHandDrag(string cardId)
        {
            ClearHandDrag();
            var team = _draft?.Preview ?? _snapshot?.Player;
            if (team == null) return;
            _dragOriginalIndex = team.Hand.FindIndex(card => card.Id == cardId);
            if (_dragOriginalIndex < 0) return;
            _dragCardId = cardId;
            _dragDestination = _dragOriginalIndex;
            foreach (var pair in _handCardViews)
            {
                _dragSlots[pair.Key] = pair.Value.style.left.value.value;
                pair.Value.AddToClassList(pair.Key == cardId ? "drag-held" : "drag-neighbor");
            }
            if (_effectsLayer != null)
            {
                _dragLanding = new CardEnergyElement { Landing = true, name = "drag-landing" };
                var face = _handCardViews[cardId].Q<Button>("card-button");
                _dragLanding.style.width = face.resolvedStyle.width + 28f;
                _dragLanding.style.height = face.resolvedStyle.height + 32f;
                _dragLanding.style.right = _dragLanding.style.bottom = StyleKeyword.Auto;
                _dragLandingLabel = new Label("MOVE") { pickingMode = PickingMode.Ignore };
                _dragLandingLabel.AddToClassList("drag-landing-label");
                _dragLanding.Add(_dragLandingLabel);
                _effectsLayer.Add(_dragLanding);
            }
        }

        private void UpdateHandDrag(string cardId, Vector2 pointer, Vector2 delta)
        {
            if (_dragCardId != cardId) BeginHandDrag(cardId);
            var team = _draft?.Preview ?? _snapshot?.Player;
            if (_dragCardId == null || team == null || _handRow == null) return;
            var pointerX = _dragSlots[cardId] + CardWidth * .5f + delta.x;
            var nearest = float.MaxValue;
            var destination = _dragOriginalIndex;
            for (var i = 0; i < team.Hand.Count; i++)
            {
                if (!_dragSlots.TryGetValue(team.Hand[i].Id, out var left)) continue;
                var distance = Mathf.Abs(pointerX - left - CardWidth * .5f);
                if (distance < nearest) { nearest = distance; destination = i; }
            }
            if (destination != _dragDestination) _stage?.PlayCue(0);
            _dragDestination = destination;
            var preview = team.Clone();
            var held = preview.Hand[_dragOriginalIndex];
            preview.Hand.RemoveAt(_dragOriginalIndex);
            preview.Hand.Insert(destination, held);
            var visualIds = team.Hand.AsEnumerable().Reverse().Select(card => card.Id).ToList();
            var previewIds = preview.Hand.AsEnumerable().Reverse().Select(card => card.Id).ToList();
            var merges = new List<BattleEvent>();
            CardRules.MergeAdjacent(preview, null, merges, false);
            var mergingIds = new HashSet<string>(merges.SelectMany(item => new[] { item.CardId, item.ConsumedCardId }));
            var mergeGroups = new Dictionary<string, HashSet<string>>();
            var mergeRanks = new Dictionary<string, int>();
            foreach (var merge in merges)
            {
                if (!mergeGroups.TryGetValue(merge.CardId, out var group)) group = new HashSet<string> { merge.CardId };
                if (mergeGroups.TryGetValue(merge.ConsumedCardId, out var consumedGroup)) group.UnionWith(consumedGroup);
                else group.Add(merge.ConsumedCardId);
                foreach (var id in group) { mergeGroups[id] = group; mergeRanks[id] = merge.Amount; }
            }
            var hasPartner = team.Hand.Any(candidate => CanMergePair(held, candidate));
            foreach (var pair in _handCardViews)
            {
                var candidate = team.Hand.Find(card => card.Id == pair.Key);
                var compatible = CanMergePair(held, candidate) || (pair.Key == cardId && hasPartner);
                var ready = mergingIds.Contains(pair.Key);
                SetMergeHint(pair.Value, compatible || ready, ready, mergeRanks.TryGetValue(pair.Key, out var rank) ? rank : 0);
                var energy = pair.Value.Q<CardEnergyElement>("card-energy");
                if (energy != null)
                {
                    energy.Merge = compatible || mergingIds.Contains(pair.Key);
                    energy.Burst = mergingIds.Contains(pair.Key) ? .7f : 0;
                    energy.MarkDirtyRepaint();
                }
                if (pair.Key == cardId)
                {
                    pair.Value.style.translate = new Translate(delta.x, -20);
                    pair.Value.style.scale = new Scale(Vector3.one * 1.18f);
                    continue;
                }
                var visualIndex = previewIds.IndexOf(pair.Key);
                if (visualIndex >= 0)
                    pair.Value.style.translate = new Translate(_dragSlots[visualIds[visualIndex]] - _dragSlots[pair.Key], 0);
            }
            if (_dragLanding != null)
            {
                var left = _dragSlots[team.Hand[destination].Id];
                var local = _effectsLayer.WorldToLocal(_handRow.LocalToWorld(new Vector2(left, 0)));
                _dragLanding.style.left = local.x - 14f;
                _dragLanding.style.top = local.y - 16f;
                _dragLandingLabel.text = mergeRanks.TryGetValue(cardId, out var landingRank) ? "MERGE → " + (landingRank >= 3 ? "III" : "II") : destination == _dragOriginalIndex ? "RETURN" : "MOVE";
            }
        }

        private void ClearHandDrag(bool preservePose = false)
        {
            _releasedHandPositions = preservePose ? _handCardViews.ToDictionary(pair => pair.Key,
                pair => new Vector2(pair.Value.style.left.value.value + pair.Value.resolvedStyle.translate.x,
                    pair.Value.resolvedStyle.translate.y)) : null;
            _dragCardId = null;
            _dragDestination = _dragOriginalIndex = -1;
            _dragSlots.Clear();
            _dragLanding?.RemoveFromHierarchy();
            _dragLanding = null;
            _dragLandingLabel = null;
            foreach (var view in _handCardViews.Values)
            {
                view.RemoveFromClassList("drag-held");
                view.RemoveFromClassList("drag-neighbor");
                if (!preservePose) view.style.translate = new Translate(0, 0);
                view.style.scale = new Scale(Vector3.one);
                var energy = view.Q<CardEnergyElement>("card-energy");
                if (energy != null) { energy.Merge = false; energy.Burst = 0; energy.MarkDirtyRepaint(); }
                SetMergeHint(view, false, false);
            }
            if (!preservePose) RefreshMergeAvailability(_draft?.Preview ?? _snapshot?.Player);
        }
    }
}
