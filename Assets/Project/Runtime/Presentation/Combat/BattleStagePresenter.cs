using System.Collections;
using System.Collections.Generic;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Camera and placeholder choreography. Combat outcomes remain entirely Core-owned.</summary>
    public sealed class BattleStagePresenter : MonoBehaviour
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
            yield return RecoverAttacker();
            if (!TryView(sourceId, out var source) || !TryView(targetId, out var target)) yield break;
            _attacker = sourceId;
            var direction = target.position - source.position;
            direction.y = 0;
            if (direction.sqrMagnitude < .01f) direction = source.forward;
            direction.Normalize();
            var midpoint = (source.position + target.position) * .5f + Vector3.up * 1.2f;
            var cross = Vector3.Cross(Vector3.up, direction);
            var distance = Mathf.Clamp(Vector3.Distance(source.position, target.position) * .65f, 4.5f, 10f);
            var cameraPosition = midpoint - direction * distance + cross * distance * .65f + Vector3.up * 2.3f;
            yield return CameraTo(cameraPosition, Quaternion.LookRotation(midpoint - cameraPosition), Mathf.Min(_homeFov, 48f), .32f);
            TriggerIfPresent(source, attackDebuff ? "AttackDebuff" : "Attack");
            var start = source.position;
            var end = target.position - direction * 1.15f;
            end.y = start.y;
            var rotation = source.rotation;
            yield return Tween(.26f, t =>
            {
                source.position = Vector3.Lerp(start, end, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * .18f;
                source.rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(direction), t);
            });
        }

        public IEnumerator AttackArea(string sourceId, IReadOnlyList<string> targetIds, bool attackDebuff = false)
        {
            yield return RecoverAttacker();
            if (!TryView(sourceId, out var source) || targetIds == null || targetIds.Count == 0) yield break;
            var targets = new List<Transform>();
            var center = Vector3.zero;
            foreach (var id in targetIds)
                if (TryView(id, out var target)) { targets.Add(target); center += target.position; }
            if (targets.Count == 0) yield break;
            center /= targets.Count;
            var direction = center - source.position;
            direction.y = 0;
            if (direction.sqrMagnitude < .01f) direction = source.forward;
            direction.Normalize();
            var spread = 0f;
            foreach (var target in targets) spread = Mathf.Max(spread, Vector3.Distance(center, target.position));
            var midpoint = (source.position + center) * .5f + Vector3.up * 1.25f;
            var cross = Vector3.Cross(Vector3.up, direction);
            var distance = Mathf.Clamp(Vector3.Distance(source.position, center) * .65f + spread * .8f, 6f, 15f);
            var cameraPosition = midpoint - direction * distance + cross * distance * .3f + Vector3.up * (2.5f + spread * .35f);
            yield return CameraTo(cameraPosition, Quaternion.LookRotation(midpoint - cameraPosition),
                Mathf.Min(_homeFov, Mathf.Lerp(50f, 62f, Mathf.Clamp01(spread / 4f))), .38f);
            _attacker = sourceId;
            TriggerIfPresent(source, attackDebuff ? "AttackDebuff" : "Attack");
            var start = source.position;
            var end = start + direction * Mathf.Min(1.2f, Vector3.Distance(source.position, center) * .12f);
            var rotation = source.rotation;
            yield return Tween(.28f, t =>
            {
                source.position = Vector3.Lerp(start, end, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * .22f;
                source.rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(direction), t);
            });
        }

        public IEnumerator SupportAction(string sourceId, IReadOnlyList<string> targetIds, CardCategory category)
        {
            yield return RecoverAttacker();
            if (!TryView(sourceId, out var source)) yield break;
            var center = source.position;
            var count = 1;
            if (targetIds != null)
                foreach (var id in targetIds)
                    if (TryView(id, out var target)) { center += target.position; count++; }
            center /= count;
            var direction = center - source.position;
            direction.y = 0;
            if (direction.sqrMagnitude < .01f) direction = source.forward;
            direction.Normalize();
            var cameraPosition = center - direction * 8f + Vector3.up * 5f - Vector3.Cross(Vector3.up, direction) * 2.5f;
            yield return CameraTo(cameraPosition, Quaternion.LookRotation(center + Vector3.up - cameraPosition),
                Mathf.Min(_homeFov, 56f), .3f);
            _attacker = sourceId;
            var trigger = category switch
            {
                CardCategory.Recovery => "Heal",
                CardCategory.Debuff => "Debuff",
                CardCategory.Buff => "Buff",
                CardCategory.Stance => "Stance",
                _ => "Skill"
            };
            TriggerIfPresent(source, trigger);
            yield return Tween(.2f, t => source.localScale = Vector3.Lerp(_scales[sourceId],
                _scales[sourceId] * 1.08f, Mathf.Sin(t * Mathf.PI)));
        }

        public IEnumerator Impact(string targetId, bool critical)
        {
            if (!TryView(targetId, out var target)) yield break;
            TriggerIfPresent(target, "Hurt");
            var position = target.position;
            var rotation = target.rotation;
            var scale = target.localScale;
            var away = TryView(_attacker, out var attacker) ? (target.position - attacker.position).normalized : -target.forward;
            away.y = 0;
            var cameraPosition = _camera == null ? Vector3.zero : _camera.transform.position;
            yield return Tween(.28f, t =>
            {
                var pulse = Mathf.Sin(t * Mathf.PI);
                target.position = position + away * (.25f * pulse);
                target.rotation = rotation * Quaternion.Euler(-12f * pulse, 0, 6f * pulse);
                target.localScale = Vector3.Scale(scale, new Vector3(1 + pulse * .05f, 1 - pulse * .08f, 1));
                if (_camera != null) _camera.transform.position = cameraPosition + _camera.transform.right *
                    (Mathf.Sin(t * Mathf.PI * 8f) * (1f - t) * (critical ? .12f : .055f));
            });
            target.SetPositionAndRotation(position, rotation);
            target.localScale = scale;
            if (_camera != null) _camera.transform.position = cameraPosition;
            yield return RecoverAttacker();
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

        public IEnumerator RecoverAttacker()
        {
            if (!TryView(_attacker, out var view) || !_poses.TryGetValue(_attacker, out var pose))
            { _attacker = null; yield break; }
            var start = new Pose(view.position, view.rotation);
            yield return Tween(.24f, t => view.SetPositionAndRotation(Vector3.Lerp(start.position, pose.position, t),
                Quaternion.Slerp(start.rotation, pose.rotation, t)));
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

        public static IEnumerator Tween(float duration, System.Action<float> frame)
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / Mathf.Max(.001f, duration));
                frame(t * t * (3 - 2 * t));
                yield return null;
            }
            frame(1f);
        }

        public void Restore()
        {
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

        private void OnDisable() { Restore(); }
    }
}
