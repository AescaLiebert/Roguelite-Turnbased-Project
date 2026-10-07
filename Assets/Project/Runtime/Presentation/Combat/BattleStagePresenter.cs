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
        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private float _homeFov;
        private Vector3 _planningPosition;
        private Quaternion _planningRotation;
        private Vector3 _arenaCenter;
        private Dictionary<string, GameObject> _views;
        private readonly Dictionary<string, Pose> _poses = new Dictionary<string, Pose>();
        private readonly Dictionary<string, Vector3> _scales = new Dictionary<string, Vector3>();
        private readonly List<Coroutine> _defeatAnimations = new List<Coroutine>();
        private string _attacker;

        public void Initialize(Dictionary<string, GameObject> views)
        {
            _views = views;
            _camera = Camera.main;
            if (_camera != null)
            {
                _homePosition = _camera.transform.position;
                _homeRotation = _camera.transform.rotation;
                _homeFov = _camera.fieldOfView;
                _planningPosition = _homePosition;
                _planningRotation = _homeRotation;
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
            if (_camera == null) yield break;
            var orbit = side == TeamSide.Player ? Quaternion.identity : Quaternion.Euler(0, 180, 0);
            yield return CameraTo(_arenaCenter + orbit * (_planningPosition - _arenaCenter), orbit * _planningRotation, _homeFov, duration);
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
            PrepareAnimationTiming(source);
            TriggerIfPresent(source, "Attack");
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
            var direction = GroundDirection(center - source.position, Facing(source));
            _attacker = sourceId;
            PrepareAnimationTiming(source);
            TriggerIfPresent(source, "Attack");
            yield return StartAttackMotion(source, targets, direction,
                source.position + direction * Mathf.Min(1.2f, Vector3.Distance(source.position, center) * .12f));
        }

        public IEnumerator SupportAction(string sourceId, IReadOnlyList<string> targetIds, CardCategory category)
        {
            if (_attacker != sourceId) yield return RecoverAttacker();
            if (!TryView(sourceId, out var source)) yield break;
            ClearCameraTracking();
            _attacker = sourceId;
            var targets = ResolveViews(targetIds);
            if (targets.Count == 0) targets.Add(source);

            // Debuff card camera frames like an attack card (low rear view framing caster and targets ahead, no zoom into target).
            // Stance on Rank 1 frames from behind/three-quarter side with stance aura clearance (no face shot).
            // Buff / Recovery frames the target recipients.
            CameraShot shot;
            if (category == CardCategory.Debuff)
            {
                var center = Vector3.zero;
                foreach (var target in targets) center += target.position;
                center /= targets.Count;
                var direction = GroundDirection(center - source.position, Facing(source));
                shot = AttackShot(source, targets, direction);
            }
            else if (category == CardCategory.Stance)
            {
                var side = Vector3.Cross(Vector3.up, Facing(source));
                var offset = -Facing(source) * 0.9f + side * 0.35f + Vector3.up * 0.45f;
                shot = FrameFighters(new[] { source }, offset, 48f, 0f, 1.6f);
            }
            else
            {
                shot = FrameFighters(targets, Facing(targets[0]) + Vector3.up * .25f, 48f, 0f, 1.4f);
            }

            // Jump cut to execution shot - do not smoothly move or orbit the camera
            yield return CameraCut(shot.position, shot.rotation, shot.fov);
            _portraitPrepared = false;

            var trigger = category switch
            {
                CardCategory.Recovery => "Heal",
                CardCategory.Debuff => "Debuff",
                CardCategory.Buff => "Buff",
                CardCategory.Stance => "Stance",
                _ => "Skill"
            };
            TriggerIfPresent(source, trigger);
            var scale = source.localScale;
            yield return Tween(.5f, t => source.localScale = scale * (1f + .06f * Mathf.Sin(t * Mathf.PI)));
            source.localScale = scale;
        }

        public IEnumerator Impact(string targetId, bool critical, float durationScale = 1f)
        {
            if (!TryView(targetId, out var target)) yield break;
            PlayCue(critical ? 2 : 1);
            TriggerIfPresent(target, "Hurt");
            var position = target.position;
            var rotation = target.rotation;
            var scale = target.localScale;
            var away = TryView(_attacker, out var attacker) ? (target.position - attacker.position).normalized : -target.forward;
            away.y = 0;
            var cameraPosition = _camera == null ? Vector3.zero : _camera.transform.position;
            yield return Tween(.28f * Mathf.Clamp(durationScale, .1f, 1f), t =>
            {
                var pulse = Mathf.Sin(t * Mathf.PI);
                target.position = position + away * (.25f * pulse);
                target.rotation = rotation * Quaternion.Euler(-12f * pulse, 0, 6f * pulse);
                target.localScale = Vector3.Scale(scale, new Vector3(1 + pulse * .05f, 1 - pulse * .08f, 1));
                SetImpactShake(cameraPosition, Mathf.Sin(t * Mathf.PI * 8f) * (1f - t) * (critical ? .12f : .055f));
            });
            target.SetPositionAndRotation(position, rotation);
            target.localScale = scale;
            SetImpactShake(cameraPosition, 0f);
        }

        public void StatusFeedback(string targetId, bool removed, bool debuff)
        {
            if (!TryView(targetId, out var target)) return;
            TriggerIfPresent(target, removed ? "StatusRemoved" : debuff ? "DebuffReceived" : "BuffReceived");
        }

        public IEnumerator ImpactMultiple(IReadOnlyList<(string targetId, bool critical)> impacts, float durationScale = 1f)
        {
            if (impacts == null || impacts.Count == 0) yield break;
            if (impacts.Count == 1)
            {
                yield return Impact(impacts[0].targetId, impacts[0].critical, durationScale);
                yield break;
            }

            PlayCue(1);
            var entries = new List<(Transform target, Vector3 pos, Quaternion rot, Vector3 scale, Vector3 away, bool critical)>();
            var anyCritical = false;
            foreach (var (targetId, critical) in impacts)
            {
                if (!TryView(targetId, out var target)) continue;
                TriggerIfPresent(target, "Hurt");
                var away = TryView(_attacker, out var attacker) ? (target.position - attacker.position).normalized : -target.forward;
                away.y = 0;
                entries.Add((target, target.position, target.rotation, target.localScale, away, critical));
                if (critical) anyCritical = true;
            }

            if (entries.Count == 0) yield break;

            var cameraPosition = _camera == null ? Vector3.zero : _camera.transform.position;
            yield return Tween(.28f * Mathf.Clamp(durationScale, .1f, 1f), t =>
            {
                var pulse = Mathf.Sin(t * Mathf.PI);
                for (var i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    e.target.position = e.pos + e.away * (.25f * pulse);
                    e.target.rotation = e.rot * Quaternion.Euler(-12f * pulse, 0, 6f * pulse);
                    e.target.localScale = Vector3.Scale(e.scale, new Vector3(1 + pulse * .05f, 1 - pulse * .08f, 1));
                }
                SetImpactShake(cameraPosition, Mathf.Sin(t * Mathf.PI * 8f) * (1f - t) * (anyCritical ? .12f : .055f));
            });

            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                e.target.SetPositionAndRotation(e.pos, e.rot);
                e.target.localScale = e.scale;
            }
            SetImpactShake(cameraPosition, 0f);
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
            while (!_actionMotionDone) yield return null;
            if (!TryView(_attacker, out var view) || !_poses.TryGetValue(_attacker, out var pose))
            { _attacker = null; ClearCameraTracking(); yield break; }
            yield return WaitForActionEnd(_attacker);
            var start = new Pose(view.position, view.rotation);
            var startRoll = _attackRoll;
            yield return Tween(.32f, t =>
            {
                _attackRoll = Mathf.Lerp(startRoll, 0f, t);
                view.SetPositionAndRotation(Vector3.Lerp(start.position, pose.position, t),
                    Quaternion.Slerp(start.rotation, pose.rotation, t));
                UpdateAttackCamera();
            });
            ClearCameraTracking();
            _attacker = null;
        }

        private IEnumerator CameraCut(Vector3 position, Quaternion rotation, float fov)
        {
            if (_camera == null) yield break;
            _camera.transform.SetPositionAndRotation(position, rotation);
            _camera.fieldOfView = fov;
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
            StopAllCoroutines();
            _defeatAnimations.Clear();
            _actionMotion = null;
            _actionMotionDone = true;
            _animationTiming = null;
            _templateHits = _templateHit = 0;
            _templateTargets.Clear();
            ClearCameraTracking();
            _portraitPrepared = false;
            _executionRank = 1;
            _executionUltimate = false;
            foreach (var pair in _poses)
                if (TryView(pair.Key, out var view) && view.gameObject.activeSelf)
                {
                    view.SetPositionAndRotation(pair.Value.position, pair.Value.rotation);
                    view.localScale = _scales[pair.Key];
                }
            if (_camera != null)
            {
                _camera.transform.SetPositionAndRotation(_homePosition, _homeRotation);
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
