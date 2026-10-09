using System.Collections;
using System.Collections.Generic;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    public sealed partial class BattleStagePresenter
    {
        private sealed class CardExecution
        {
            public string SourceId;
            public ActorCardPresentation Actor;
            public int Rank;
            public bool Ultimate;
            public bool Area;
            public bool SelfTarget;
            public float ActorHeight;
            public float ActorRadius;
            public float ActorGroundOffset;
            public CardCategory Category;
            public bool Portrait;
            public Bounds TargetBounds;
            public Vector3 Direction;
            public CameraShot WindUpShot;
            public CameraShot SupportShot;
            public CameraShot ActionShot;
            public readonly Dictionary<Transform, Quaternion> TargetRotations = new Dictionary<Transform, Quaternion>();
            public bool Support => Category == CardCategory.Buff || Category == CardCategory.Recovery;
        }

        private CardExecution _execution;
        private bool _cutFromSupport;
        private readonly Dictionary<string, TeamSide> _teams = new Dictionary<string, TeamSide>();
        private readonly HashSet<string> _formationMembers = new HashSet<string>();
        private readonly Dictionary<string, Bounds> _formationBounds = new Dictionary<string, Bounds>();
        public ActorCardPhase ExecutionPhase => _execution?.Actor.Phase ?? ActorCardPhase.Rest;

        private void RememberFormation(string id, Transform view)
        {
            if (!view.gameObject.activeInHierarchy) return;
            _formationMembers.Add(id);
            _formationBounds[id] = FighterBounds(view);
        }

        private bool SameTeam(string a, string b)
        {
            if (_teams.TryGetValue(a, out var first) && _teams.TryGetValue(b, out var second)) return first == second;
            // Standalone previews have no BattleState. Infer rows from remembered positions
            // and the arena axis, never from a fighter's current movement or rotation.
            if (!_poses.TryGetValue(a, out var pa) || !_poses.TryGetValue(b, out var pb)) return a == b;
            var axis = _planningRotation * Vector3.forward;
            axis.y = 0;
            return Vector3.Dot(pa.position - _arenaCenter, axis) * Vector3.Dot(pb.position - _arenaCenter, axis) >= 0;
        }

        private Bounds SnapshotTargets(string sourceId, IReadOnlyList<string> targetIds, bool wholeTeam, bool support)
        {
            var selected = ResolveViews(targetIds);
            TryView(sourceId, out var source);
            var bounds = FighterBounds(selected.Count > 0 ? selected[0] : source);
            if (wholeTeam || support)
            {
                var teamId = support ? sourceId : targetIds != null && targetIds.Count > 0 ? targetIds[0] : sourceId;
                var found = false;
                foreach (var id in _formationMembers)
                {
                    if (!SameTeam(teamId, id) || !_formationBounds.TryGetValue(id, out var formation)) continue;
                    // Keep vacant/dead formation slots in AoE framing. Reserves only become
                    // members when RememberPose is called after they enter the battlefield.
                    if (!found) { bounds = formation; found = true; } else bounds.Encapsulate(formation);
                }
                return bounds;
            }
            for (var i = 1; i < selected.Count; i++) bounds.Encapsulate(FighterBounds(selected[i]));
            return bounds;
        }

        private Bounds SnapshotOpposingFormation(string sourceId)
        {
            foreach (var id in _formationMembers)
                if (!SameTeam(sourceId, id))
                    return SnapshotTargets(sourceId, new[] { id }, true, false);
            // Standalone self-cast previews can contain only the actor.
            return SnapshotTargets(sourceId, new[] { sourceId }, false, false);
        }

        public IEnumerator BeginExecution(string sourceId, string targetId, int rank, bool ultimate,
            CardCategory category = CardCategory.Attack, IReadOnlyList<string> targetIds = null, bool area = false, bool? selfTarget = null)
        {
            yield return RecoverAttacker();
            EndExecutionContext();
            if (!TryView(sourceId, out var source) || !source.gameObject.activeInHierarchy) yield break;
            var actor = source.GetComponent<ActorCardPresentation>() ?? source.gameObject.AddComponent<ActorCardPresentation>();
            _execution = new CardExecution { SourceId = sourceId, Actor = actor, Rank = Mathf.Clamp(rank, 1, 3),
                Ultimate = ultimate, Category = category, Area = area,
                SelfTarget = selfTarget ?? (targetIds != null && targetIds.Count > 0
                    ? targetIds.Count == 1 && targetIds[0] == sourceId : targetId == sourceId) };
            var actorBounds = FighterBounds(source);
            _execution.ActorHeight = actorBounds.size.y;
            _execution.ActorRadius = FighterRadius(source);
            _execution.ActorGroundOffset = actorBounds.min.y - source.position.y;
            _execution.TargetBounds = SnapshotTargets(sourceId, targetIds ?? new[] { targetId }, area,
                _execution.Support && !_execution.SelfTarget);
            if (_execution.Support && _execution.SelfTarget)
                _execution.TargetBounds = SnapshotOpposingFormation(sourceId);
            _execution.Direction = _execution.Support || _execution.SelfTarget ? Facing(source) :
                GroundDirection(_execution.TargetBounds.center - source.position, Facing(source));
            _attacker = sourceId;
            _animationTiming = null;
            _templateHits = _templateHit = 0;
            _templateTargets.Clear();
            actor.PhaseChanged += OnActorPhaseChanged;

            if (!_execution.Support && !_execution.SelfTarget)
            {
                source.rotation = Quaternion.LookRotation(_execution.Direction);
                foreach (var target in ResolveViews(targetIds ?? new[] { targetId }))
                {
                    if (target == source) continue;
                    _execution.TargetRotations[target] = target.rotation;
                    target.rotation = Quaternion.LookRotation(GroundDirection(source.position - target.position, -_execution.Direction));
                }
            }
            _attackRoll = 0;
            _execution.WindUpShot = AttackShot(source, _execution.Direction);
            _execution.Portrait = (_execution.Rank >= 2 || ultimate) &&
                (!_execution.Support || _execution.SelfTarget);
            _cutFromSupport = _execution.Support;
            if (_execution.Support)
            {
                _execution.SupportShot = _execution.SelfTarget
                    ? _execution.Portrait ? PortraitShot(source, _execution.Rank >= 3 || ultimate) : _execution.WindUpShot
                    : FrameBounds(_execution.TargetBounds, -Facing(source) + Vector3.up * .45f, 48f, 0f, 1.4f);
                ApplyShot(_execution.SupportShot);
            }

            // Rank 1 support skips anticipation, but its cast shot is already in place.
            if (_execution.Support && _execution.Rank == 1 && !ultimate) yield break;
            var authored = actor.Enter(ActorCardPhase.WindUp);
            if (!_execution.Support && !_execution.Portrait)
                ApplyShot(_execution.WindUpShot);
            if (!authored) TriggerIfPresent(source, ultimate ? "Ultimate" : "Charge");
            PlayCue(ultimate ? 4 : 3);
            if (_execution.Portrait) Pulse(sourceId, ultimate ? new Color(1f, .55f, .08f) : new Color(.6f, .8f, 1f));
            var scale = source.localScale;
            var rotation = source.rotation;
            // Starting feedback timings: retain the ranked anticipation already used by the
            // project; short Rank 1 preparation can be tuned using ActorDrivenExecution.md.
            yield return Tween(ultimate ? .8f : rank >= 3 ? .85f : rank >= 2 ? .65f : .18f, t =>
            {
                if (authored) return;
                var pulse = Mathf.Sin(t * Mathf.PI);
                source.localScale = Vector3.Scale(scale, new Vector3(1 + .035f * pulse, 1 - .05f * pulse, 1));
                source.rotation = rotation * Quaternion.Euler(-7 * pulse, 0, 0);
            });
            source.localScale = scale;
            source.rotation = rotation;
            if (authored) yield return actor.WaitForAnimationEnd();
            if (ultimate)
            {
                yield return UltimateReveal(source);
            }
        }

        private void OnActorPhaseChanged(ActorCardPhase phase)
        {
            if (_execution == null || !TryView(_execution.SourceId, out var source)) return;
            if (_execution.Support)
            {
                _attackRoll = 0;
                if (phase == ActorCardPhase.WindUp || phase == ActorCardPhase.Action)
                    ApplyShot(_execution.SupportShot);
                // Recovery remains in Attack; there is no fourth recovery camera shot.
                return;
            }
            if (phase == ActorCardPhase.WindUp && _execution.Portrait)
                ApplyShot(PortraitShot(source, _execution.Rank >= 3 || _execution.Ultimate));
            else if (phase == ActorCardPhase.Action)
            {
                ApplyShot(_execution.ActionShot);
            }
        }

        private void ApplyShot(CameraShot shot)
        {
            if (_camera == null) return;
            _camera.transform.SetPositionAndRotation(shot.position, shot.rotation);
            _camera.fieldOfView = shot.fov;
        }

        private IEnumerator EnterAction(string sourceId, IReadOnlyList<string> targetIds, CardCategory category, bool area, string trigger,
            bool useAnimationTiming = false, Vector3? attackPosition = null, HitReaction reaction = HitReaction.Hit)
        {
            // Public preview/legacy callers can still enter an action directly.
            if (_execution == null || _execution.SourceId != sourceId)
                yield return BeginExecution(sourceId, targetIds != null && targetIds.Count > 0 ? targetIds[0] : sourceId,
                    1, false, category, targetIds, area);
            if (_execution == null) yield break;
            // Legacy four-argument BeginExecution cannot know skill type/scope until action.
            // Production playback supplies both at entry, before any anticipation shot.
            if (_execution.Category != category || _execution.Area != area)
            {
                _execution.Category = category;
                _execution.Area = area;
                _execution.TargetBounds = SnapshotTargets(sourceId, targetIds, area, _execution.Support && !_execution.SelfTarget);
                if (_execution.Support && _execution.SelfTarget)
                    _execution.TargetBounds = SnapshotOpposingFormation(sourceId);
                if (TryView(sourceId, out var source))
                {
                    _execution.Direction = _execution.Support ? Facing(source) :
                        GroundDirection(_execution.TargetBounds.center - source.position, Facing(source));
                    _execution.WindUpShot = AttackShot(source, _execution.Direction);
                    if (_execution.Support)
                    {
                        _execution.Portrait = _execution.Rank >= 2 && _execution.SelfTarget;
                        _execution.SupportShot = _execution.SelfTarget
                            ? _execution.Portrait ? PortraitShot(source, _execution.Rank >= 3) : _execution.WindUpShot
                            : FrameBounds(_execution.TargetBounds, -Facing(source) + Vector3.up * .45f, 48f, 0f, 1.4f);
                        _cutFromSupport = true;
                    }
                }
            }
            if (useAnimationTiming && TryView(sourceId, out var actionSource)) PrepareAnimationTiming(actionSource);
            if (!_execution.Support && TryView(sourceId, out var shotSource))
            {
                SnapshotReactionPath(sourceId, targetIds, reaction);
                _attackRoll = !_execution.SelfTarget && (_execution.Rank >= 3 || _execution.Ultimate) ? -7f : 0f;
                _execution.ActionShot = AttackShot(shotSource, _execution.Direction, attackPosition);
            }
            _execution.Actor.Enter(ActorCardPhase.Action, trigger);
        }

        public IEnumerator CompleteExecution(bool hasQueuedAction, TeamSide side)
        {
            yield return RecoverAttacker();
            if (!hasQueuedAction) yield return ReturnToPlanning(side);
        }

        private void EndExecutionContext()
        {
            if (_execution == null) return;
            if (_execution.Actor != null)
            {
                _execution.Actor.PhaseChanged -= OnActorPhaseChanged;
                _execution.Actor.Enter(ActorCardPhase.Rest);
            }
            foreach (var target in _execution.TargetRotations)
                if (target.Key != null && target.Key.gameObject.activeInHierarchy) target.Key.rotation = target.Value;
            _execution = null;
        }
    }
}
