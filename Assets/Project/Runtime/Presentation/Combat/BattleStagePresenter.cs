using System.Collections;
using System.Collections.Generic;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Camera and placeholder choreography. Combat outcomes remain entirely Core-owned.</summary>
    public sealed partial class BattleStagePresenter : MonoBehaviour
    {
        private Camera _camera;
        private float _homeFov;
        private Vector3 _planningPosition;
        private Quaternion _planningRotation;
        private Vector3 _arenaCenter;
        private Dictionary<string, GameObject> _views;
        private readonly Dictionary<string, Pose> _poses = new Dictionary<string, Pose>();
        private readonly Dictionary<string, Vector3> _scales = new Dictionary<string, Vector3>();
        private readonly List<Coroutine> _defeatAnimations = new List<Coroutine>();
        private string _attacker;

        public void Initialize(Dictionary<string, GameObject> views, BattleState state = null)
        {
            _views = views;
            _teams.Clear();
            _formationMembers.Clear();
            _formationBounds.Clear();
            if (state != null)
                foreach (var team in new[] { state.Player, state.Opponent })
                    foreach (var fighter in team.Fighters) _teams[fighter.Id] = fighter.Side;
            _camera = Camera.main;
            if (_camera != null)
            {
                _homeFov = _camera.fieldOfView;
                _planningPosition = _camera.transform.position;
                _planningRotation = _camera.transform.rotation;
            }
            foreach (var pair in views) RememberPose(pair.Key);
            var playerAnchor = GameObject.Find("CharHeroPosition2");
            var enemyAnchor = GameObject.Find("EnemyHeroPosition2");
            if (playerAnchor != null && enemyAnchor != null)
            {
                _arenaCenter = (playerAnchor.transform.position + enemyAnchor.transform.position) * .5f;
                var forward = Vector3.ProjectOnPlane(enemyAnchor.transform.position - playerAnchor.transform.position, Vector3.up).normalized;
                if (forward.sqrMagnitude < .01f) forward = Vector3.forward;
                // Center the elevated view behind the acting row; no sideways isometric offset.
                _planningPosition = _arenaCenter - forward * 17f + Vector3.up * 13.5f;
                _planningRotation = Quaternion.LookRotation(_arenaCenter + Vector3.up * 1.3f - _planningPosition);
            }
        }

        public void RememberPose(string id)
        {
            if (!TryView(id, out var view)) return;
            _poses[id] = new Pose(view.position, view.rotation);
            _scales[id] = view.localScale;
            RememberFormation(id, view);
        }

        private bool TryView(string id, out Transform view)
        {
            view = null;
            if (string.IsNullOrEmpty(id) || _views == null || !_views.TryGetValue(id, out var obj) || obj == null) return false;
            view = obj.transform;
            return true;
        }

        public IEnumerator Turn(TeamSide side, float duration = .65f)
        {
            yield return RecoverAttacker();
            EndExecutionContext();
            if (_camera == null) yield break;
            var shot = PlanningShot(side);
            if (_cutFromSupport) ApplyShot(shot);
            else yield return OrbitCamera(_arenaCenter, shot, duration);
            _cutFromSupport = false;
        }

        private CameraShot PlanningShot(TeamSide side)
        {
            var orbit = side == TeamSide.Player ? Quaternion.identity : Quaternion.Euler(0, 180, 0);
            return new CameraShot {
                position = _arenaCenter + orbit * (_planningPosition - _arenaCenter),
                rotation = orbit * _planningRotation, fov = _homeFov };
        }

        public IEnumerator ReturnToPlanning(TeamSide side, float duration = .4f)
        {
            yield return Turn(side, duration);
        }

        public IEnumerator Attack(string sourceId, string targetId, bool attackDebuff = false)
        {
            if (_attacker != sourceId) yield return RecoverAttacker();
            if (!TryView(sourceId, out var source) || !TryView(targetId, out var target)) yield break;
            _attacker = sourceId;
            var direction = GroundDirection(target.position - source.position, Facing(source));
            var separation = Mathf.Max(1.15f, FighterRadius(source) + FighterRadius(target) + .45f);
            var travel = Mathf.Max(0f, Vector3.Distance(source.position, target.position) - separation);
            var end = source.position + direction * travel;
            end.y = source.position.y;
            yield return EnterAction(sourceId, new[] { targetId }, attackDebuff ? CardCategory.AttackDebuff : CardCategory.Attack, false, "Attack", true, end);
            yield return StartAttackMotion(source, new[] { target }, direction, end);
        }

        public IEnumerator AttackArea(string sourceId, IReadOnlyList<string> targetIds, bool attackDebuff = false)
        {
            if (_attacker != sourceId) yield return RecoverAttacker();
            if (!TryView(sourceId, out var source)) yield break;
            var targets = ResolveViews(targetIds);
            if (targets.Count == 0) yield break;
            var center = Vector3.zero;
            foreach (var target in targets) center += target.position;
            center /= targets.Count;
            _attacker = sourceId;
            center = SnapshotTargets(sourceId, targetIds, true, false).center;
            var direction = GroundDirection(center - source.position, Facing(source));
            var end = source.position + direction * Mathf.Min(1.2f, Vector3.Distance(source.position, center) * .12f);
            yield return EnterAction(sourceId, targetIds, attackDebuff ? CardCategory.AttackDebuff : CardCategory.Attack, true, "Attack", true, end);
            yield return StartAttackMotion(source, targets, direction, end);
        }

        public IEnumerator SupportAction(string sourceId, IReadOnlyList<string> targetIds, CardCategory category)
        {
            if (_attacker != sourceId) yield return RecoverAttacker();
            if (!TryView(sourceId, out var source)) yield break;
            var trigger = category switch
            {
                CardCategory.Recovery => "Heal",
                CardCategory.Debuff => "Debuff",
                CardCategory.Buff => "Buff",
                CardCategory.Stance => "Stance",
                _ => "Skill"
            };
            yield return EnterAction(sourceId, targetIds, category, _execution != null && _execution.Area || targetIds != null && targetIds.Count > 1, trigger);
            if (_execution == null) yield break;
            _attacker = sourceId;
            var scale = source.localScale;
            yield return Tween(.5f, t => source.localScale = scale * (1f + .06f * Mathf.Sin(t * Mathf.PI)));
            source.localScale = scale;
        }

        public IEnumerator Impact(string targetId, bool critical, float durationScale = 1f)
        {
            yield return React(new BattleEvent { TargetId = targetId, SourceId = _attacker, WasCritical = critical }, durationScale);
        }

        public void StatusFeedback(string targetId, bool removed, bool debuff)
        {
            if (!TryView(targetId, out var target)) return;
            TriggerIfPresent(target, removed ? "StatusRemoved" : debuff ? "DebuffReceived" : "BuffReceived");
        }

        public IEnumerator ImpactMultiple(IReadOnlyList<(string targetId, bool critical)> impacts, float durationScale = 1f)
        {
            if (impacts == null) yield break;
            var events = new List<BattleEvent>();
            foreach (var impact in impacts)
                events.Add(new BattleEvent { TargetId = impact.targetId, SourceId = _attacker, WasCritical = impact.critical });
            yield return ReactMultiple(events, durationScale);
        }

        public IEnumerator Defeat(string id)
        {
            if (!TryView(id, out var view)) yield break;
            TriggerIfPresent(view, "Death");
            foreach (var collider in view.GetComponentsInChildren<Collider>()) collider.enabled = false;
            var pose = new Pose(view.position, view.rotation);
            var scale = view.localScale;
            yield return Tween(.65f, t =>
            {
                view.rotation = pose.rotation * Quaternion.Euler(0, 0, -82f * t);
                view.position = pose.position + Vector3.down * .35f * t;
                view.localScale = scale * Mathf.Lerp(1, .65f, t * t);
            });
            view.gameObject.SetActive(false);
        }

        public void BeginDefeat(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (_actorEffects.TryGetValue(id, out var effect) && effect != null) effect.ClearAll();
            SetShieldAura(id, false);
            SetStance(id, false);
            _defeatAnimations.Add(StartCoroutine(Defeat(id)));
        }

        public IEnumerator WaitForDefeatAnimations()
        {
            for (var i = 0; i < _defeatAnimations.Count; i++)
                if (_defeatAnimations[i] != null) yield return _defeatAnimations[i];
            _defeatAnimations.Clear();
        }

        public IEnumerator RecoverAttacker()
        {
            yield return WaitForTargetReactions();
            var deadline = Time.realtimeSinceStartup + 8f;
            while (!_actionMotionDone && Time.realtimeSinceStartup < deadline) yield return null;
            if (!_actionMotionDone)
            {
                if (_actionMotion != null) StopCoroutine(_actionMotion);
                _actionMotion = null;
                _actionMotionDone = true;
            }
            if (_execution == null || !TryView(_attacker, out var view) || !_poses.TryGetValue(_attacker, out var pose))
            { _attacker = null; yield break; }
            yield return WaitForActionEnd(_attacker);
            var authoredRecovery = _execution.Actor.Enter(ActorCardPhase.Recovery);
            var start = new Pose(view.position, view.rotation);
            var startScale = view.localScale;
            yield return Tween(.32f, t =>
            {
                view.SetPositionAndRotation(Vector3.Lerp(start.position, pose.position, t),
                    Quaternion.Slerp(start.rotation, pose.rotation, t));
                // Procedural recovery feedback also covers stationary ranged/support casts.
                if (!authoredRecovery) view.localScale = startScale * (1f - .025f * Mathf.Sin(t * Mathf.PI));
            });
            if (authoredRecovery) yield return _execution.Actor.WaitForAnimationEnd();
            view.SetPositionAndRotation(pose.position, pose.rotation);
            view.localScale = _scales[_attacker];
            EndExecutionContext();
            _attacker = null;
        }

        private IEnumerator CameraTo(Vector3 position, Quaternion rotation, float fov, float duration)
        {
            if (_camera == null) yield break;
            var from = new Pose(_camera.transform.position, _camera.transform.rotation);
            var fromFov = _camera.fieldOfView;
            yield return Tween(duration, t =>
            {
                _camera.transform.SetPositionAndRotation(Vector3.Lerp(from.position, position, t), Quaternion.Slerp(from.rotation, rotation, t));
                _camera.fieldOfView = Mathf.Lerp(fromFov, fov, t);
            });
        }

        private static void TriggerIfPresent(Transform view, string name)
        {
            var animator = view.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return;
            foreach (var parameter in animator.parameters)
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == name)
                { animator.SetTrigger(name); break; }
        }

        public static IEnumerator Tween(float duration, System.Action<float> frame, bool smooth = true)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / Mathf.Max(.001f, duration));
                frame(smooth ? t * t * (3 - 2 * t) : t);
                yield return null;
            }
            frame(1f);
        }

        public void Restore()
        {
            _cutFromSupport = false;
            StopAllCoroutines();
            RestoreTargetReactions();
            ClearActorVisualEffects(true);
            ClearUltimateReveal();
            UltimateLeadInActive = false;
            _defeatAnimations.Clear();
            _actionMotion = null;
            _actionMotionDone = true;
            _animationTiming = null;
            _templateHits = _templateHit = 0;
            _templateTargets.Clear();
            EndExecutionContext();
            foreach (var pair in _poses)
                if (TryView(pair.Key, out var view) && view.gameObject.activeSelf)
                {
                    view.SetPositionAndRotation(pair.Value.position, pair.Value.rotation);
                    view.localScale = _scales[pair.Key];
                }
            if (_camera != null)
            {
                _camera.transform.SetPositionAndRotation(_planningPosition, _planningRotation);
                _camera.fieldOfView = _homeFov;
            }
            _attacker = null;
        }

        public void SnapToPlanning()
        {
            Restore();
            if (_camera != null) _camera.transform.SetPositionAndRotation(_planningPosition, _planningRotation);
        }

        private void OnDisable() { Restore(); ClearAuras(); }
    }
}
