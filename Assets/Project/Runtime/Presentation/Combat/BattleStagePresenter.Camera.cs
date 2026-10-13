using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    public sealed partial class BattleStagePresenter
    {
        // Starting framing values, evaluated against the timestamped reference in
        // Tests/CombatPresentation/CameraReferences.md. Leave space for the execution banner.
        private const float CameraSafeHeight = .72f;
        [Header("Camera Phase Moves (starting values)")]
        [SerializeField, Range(.03f, .3f)] private float windUpCameraDuration = .14f;
        [SerializeField, Range(.03f, .3f)] private float attackCameraDuration = .12f;
        [SerializeField, Range(.03f, .3f)] private float restCameraDuration = .16f;
        private Coroutine _cameraTransition;
        private bool _followAttackerCamera;
        public bool CameraTransitionActive { get; private set; }
        private float _attackRoll;
        private CardAnimationTiming _animationTiming;
        private Coroutine _actionMotion;
        private bool _actionMotionDone = true;

        private void LateUpdate()
        {
            // Follow only the offensive caster root. Targets, their geometry and the
            // reaction envelope were captured on entry, never read from a flying victim.
            if (_followAttackerCamera && !CameraTransitionActive && _execution != null &&
                !_execution.Support && _execution.Actor.Phase == ActorCardPhase.Action &&
                TryView(_execution.SourceId, out var source))
                ApplyShot(AttackShot(source, _execution.Direction));
        }

        private void PrepareAnimationTiming(Transform source)
        {
            var animator = source.GetComponentInChildren<Animator>();
            _animationTiming = animator == null ? null : animator.GetComponent<CardAnimationTiming>();
            if (animator != null && _animationTiming == null)
                _animationTiming = animator.gameObject.AddComponent<CardAnimationTiming>();
            _animationTiming?.BeginAction();
        }

        private IEnumerator StartAttackMotion(Transform source, IReadOnlyList<Transform> targets, Vector3 direction, Vector3 end)
        {
            _actionMotionDone = false;
            _actionMotion = StartCoroutine(RunAttackMotion(source, targets, direction, end));
            var deadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (_animationTiming != null && _animationTiming.HasFirstHitEvent)
                { if (_animationTiming.FirstHit && !CameraTransitionActive) break; }
                else if (_actionMotionDone && !CameraTransitionActive) break;
                yield return null;
            }
        }

        private IEnumerator RunAttackMotion(Transform source, IReadOnlyList<Transform> targets, Vector3 direction, Vector3 end)
        {
            yield return AttackMotion(source, targets, direction, end);
            _actionMotionDone = true;
            _actionMotion = null;
        }

        public IEnumerator WaitForLastHit()
        {
            var deadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (_animationTiming != null && _animationTiming.HasLastHitEvent)
                { if (_animationTiming.LastHit && _actionMotionDone) break; }
                else if (_actionMotionDone) break;
                yield return null;
            }
        }

        private struct CameraShot
        {
            public Vector3 position;
            public Quaternion rotation;
            public float fov;
        }

        private static Vector3 GroundDirection(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0;
            if (direction.sqrMagnitude < .001f) direction = Vector3.ProjectOnPlane(fallback, Vector3.up);
            return direction.sqrMagnitude < .001f ? Vector3.forward : direction.normalized;
        }

        private static Vector3 Facing(Transform source) => GroundDirection(source.forward, Vector3.forward);

        private List<Transform> ResolveViews(IReadOnlyList<string> ids)
        {
            var result = new List<Transform>();
            if (ids != null)
                foreach (var id in ids)
                    if (TryView(id, out var view) && view.gameObject.activeInHierarchy && !result.Contains(view))
                        result.Add(view);
            return result;
        }

        private static Bounds FighterBounds(Transform source)
        {
            var bounds = new Bounds(source.position + Vector3.up, new Vector3(1, 2, 1));
            var found = false;
            foreach (var renderer in source.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer is ParticleSystemRenderer || renderer is LineRenderer ||
                    IsShieldRenderer(renderer) || ActorVisualEffect.IsEffectRenderer(renderer)) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            bounds.extents = Vector3.Max(bounds.extents, new Vector3(.35f, .65f, .35f));
            return bounds;
        }

        private CameraShot PortraitShot(Transform source, bool dramatic)
        {
            var bounds = FighterBounds(source);
            // Upper two-thirds composition for every face shot. Ranked/ultimate drama
            // changes the angle/roll, never zooms back into a tight head-only crop.
            // Geometry-based framing also supports fighters without a head bone.
            bounds.center += Vector3.up * bounds.extents.y / 3f;
            bounds.extents = new Vector3(bounds.extents.x, bounds.extents.y * (2f / 3f), bounds.extents.z);
            var front = Quaternion.AngleAxis(dramatic ? -20f : -12f, Vector3.up) * Facing(source);
            return FrameBounds(bounds, front + Vector3.up * (dramatic ? .12f : .06f),
                44f, dramatic ? -9f : 0f, 1.1f);
        }

        private CameraShot FrameFighters(IReadOnlyList<Transform> fighters, Vector3 offset, float fov, float roll, float padding)
        {
            var bounds = FighterBounds(fighters[0]);
            for (var i = 1; i < fighters.Count; i++)
                if (fighters[i] != null) bounds.Encapsulate(FighterBounds(fighters[i]));
            return FrameBounds(bounds, offset, fov, roll, padding);
        }

        private CameraShot FrameBounds(Bounds bounds, Vector3 offset, float fov, float roll, float padding)
        {
            var rotation = Quaternion.LookRotation(-offset.normalized, Vector3.up) * Quaternion.Euler(0, 0, roll);
            var right = rotation * Vector3.right;
            var up = rotation * Vector3.up;
            var forward = rotation * Vector3.forward;
            var halfY = Mathf.Tan(fov * Mathf.Deg2Rad * .5f);
            var halfX = halfY * (_camera != null ? Mathf.Max(.2f, _camera.aspect) : 16f / 9f);
            var distance = 1f;
            var focus = bounds.center;
            // Fit every corner in camera space, including tilted shots and portrait screens.
            for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                    for (var z = -1; z <= 1; z += 2)
                    {
                        var corner = Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                        var depth = Vector3.Dot(corner, forward);
                        distance = Mathf.Max(distance,
                            Mathf.Abs(Vector3.Dot(corner, right)) * padding / (halfX * .88f) - depth,
                            Mathf.Abs(Vector3.Dot(corner, up)) * padding / (halfY * CameraSafeHeight) - depth);
                    }
            return new CameraShot { position = focus - forward * distance, rotation = rotation, fov = fov };
        }

        private CameraShot AttackShot(Transform source, Vector3 direction)
        {
            var sourcePosition = source.position;
            var height = _execution?.ActorHeight ?? FighterBounds(source).size.y;
            if (_execution != null && _execution.SelfTarget && !_execution.Support)
                return FrameBounds(_execution.TargetBounds, -direction + Vector3.up * .25f, 48f, 0f, 1.18f);
            var targets = _execution != null ? _execution.TargetBounds : FighterBounds(source);
            // Shoulder-height rear composition: caster in the lower left foreground, the
            // captured target position ahead. Only the target row needs full-body framing.
            // Its vacant AoE slots remain in these bounds after death or recoil.
            var side = Vector3.Cross(Vector3.up, direction);
            var focus = targets.center + Vector3.up * height * .2f;
            var distanceToTarget = Vector3.ProjectOnPlane(targets.center - sourcePosition, Vector3.up).magnitude;
            var close = 1f - Mathf.InverseLerp(height, height * 3f, distanceToTarget);
            // Open the shoulder angle near contact so the caster cannot hide the victim.
            var shoulderHeight = (_execution?.ActorGroundOffset ?? 0f) + height * 1.3f;
            var clearance = (_execution?.ActorRadius ?? FighterRadius(source)) + height * .06f;
            var rearDistance = height * 1.8f;
            var shot = new CameraShot { fov = 52f };
            var halfY = Mathf.Tan(shot.fov * Mathf.Deg2Rad * .5f);
            var halfX = halfY * (_camera != null ? Mathf.Max(.2f, _camera.aspect) : 16f / 9f);
            for (var attempt = 0; attempt < 80; attempt++)
            {
                // Preserve a clear line to the target even when fitting a wide AoE row
                // pushes the camera farther back. Animation cannot change the cached size.
                var sideOffset = Mathf.Max(height * Mathf.Lerp(.35f, 1.7f, close),
                    clearance * (rearDistance + distanceToTarget) / Mathf.Max(height * .5f, distanceToTarget));
                shot.position = sourcePosition + side * sideOffset + Vector3.up * shoulderHeight - direction * rearDistance;
                shot.rotation = Quaternion.LookRotation(focus - shot.position, Vector3.up) * Quaternion.Euler(0, 0, _attackRoll);
                var inverse = Quaternion.Inverse(shot.rotation);
                var fits = true;
                for (var x = -1; x <= 1; x += 2)
                    for (var y = -1; y <= 1; y += 2)
                        for (var z = -1; z <= 1; z += 2)
                        {
                            var p = inverse * (targets.center + Vector3.Scale(targets.extents, new Vector3(x, y, z)) - shot.position);
                            if (p.z < .1f || Mathf.Abs(p.x) > p.z * halfX * .84f ||
                                p.y < -p.z * halfY * .56f || p.y > p.z * halfY * .76f) fits = false;
                        }
                if (fits) break;
                rearDistance += height * .2f;
            }
            return shot;
        }

        private IEnumerator AttackMotion(Transform source, IReadOnlyList<Transform> targets, Vector3 direction, Vector3 end)
        {
            var start = source.position;
            var rotation = source.rotation;
            yield return Tween(.38f, t =>
            {
                source.position = Vector3.Lerp(start, end, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * .18f;
                source.rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(direction), t);
            });
        }

        private IEnumerator OrbitCamera(Vector3 pivot, CameraShot shot, float duration)
        {
            if (_camera == null) yield break;
            var from = new Pose(_camera.transform.position, _camera.transform.rotation);
            var fov = _camera.fieldOfView;
            var direction = OrbitDirection(from.position - pivot, shot.position - pivot);
            yield return Tween(duration, t =>
            {
                var destination = shot;
                if (_followAttackerCamera && _execution != null && TryView(_execution.SourceId, out var source))
                    destination = AttackShot(source, _execution.Direction);
                ApplyOrbit(from, fov, pivot, pivot, destination, t, direction);
            });
        }

        private void MoveToShot(CameraShot shot, Vector3 pivot, float duration)
        {
            CancelCameraTransition();
            if (_camera == null) return;
            if (Vector3.Distance(_camera.transform.position, shot.position) < .001f &&
                Quaternion.Angle(_camera.transform.rotation, shot.rotation) < .01f &&
                Mathf.Abs(_camera.fieldOfView - shot.fov) < .001f)
            { ApplyShot(shot); return; }
            CameraTransitionActive = true;
            // WindUp/Rest endpoints and pivot are snapshots. Offensive Action alone
            // follows its caster; the target focus never follows a displaced victim.
            _cameraTransition = StartCoroutine(MovePhaseCamera(shot, pivot, Mathf.Clamp(duration, .03f, 1f)));
        }

        private IEnumerator MovePhaseCamera(CameraShot shot, Vector3 pivot, float duration)
        {
            // Preserve the start pose for the phase-entry frame, not an instant first-step cut.
            yield return null;
            yield return OrbitCamera(pivot, shot, duration);
            if (_followAttackerCamera && _execution != null && TryView(_execution.SourceId, out var source))
                shot = AttackShot(source, _execution.Direction);
            ApplyShot(shot);
            CameraTransitionActive = false;
            _cameraTransition = null;
        }

        public IEnumerator WaitForCameraTransition()
        {
            while (CameraTransitionActive) yield return null;
        }

        private void CancelCameraTransition()
        {
            if (_cameraTransition != null) StopCoroutine(_cameraTransition);
            _cameraTransition = null;
            CameraTransitionActive = false;
        }

        private static float OrbitDirection(Vector3 from, Vector3 to)
        {
            var angle = Mathf.DeltaAngle(Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg,
                Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            return Mathf.Abs(angle) < 1f ? 0f : Mathf.Sign(angle);
        }

        private static float OrbitAngle(float from, float to, float t, float direction)
        {
            var delta = Mathf.DeltaAngle(from, to);
            if (direction > 0 && delta < -1f) delta += 360f;
            else if (direction < 0 && delta > 1f) delta -= 360f;
            return from + delta * t;
        }

        private void ApplyOrbit(Pose from, float fromFov, Vector3 oldPivot, Vector3 pivot, CameraShot shot, float t, float direction)
        {
            if (_camera == null) return;
            var a = from.position - oldPivot;
            var b = shot.position - pivot;
            var yawA = Mathf.Atan2(a.x, a.z) * Mathf.Rad2Deg;
            var yawB = Mathf.Atan2(b.x, b.z) * Mathf.Rad2Deg;
            var pitchA = Mathf.Asin(Mathf.Clamp(a.y / Mathf.Max(.001f, a.magnitude), -1, 1));
            var pitchB = Mathf.Asin(Mathf.Clamp(b.y / Mathf.Max(.001f, b.magnitude), -1, 1));
            var yaw = OrbitAngle(yawA, yawB, t, direction) * Mathf.Deg2Rad;
            var pitch = Mathf.Lerp(pitchA, pitchB, t);
            var radius = Mathf.Lerp(a.magnitude, b.magnitude, t);
            var offset = new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch));
            var position = pivot + offset * radius;
            // Rotate with the chosen spatial arc. At a half-turn, an independent
            // quaternion Slerp can choose the opposite yaw and face outside the arena.
            // Interpolate only the remaining pitch/roll/focus correction locally.
            var yawRotation = Quaternion.AngleAxis(yaw * Mathf.Rad2Deg - yawA, Vector3.up);
            var endYawRotation = Quaternion.AngleAxis(OrbitAngle(yawA, yawB, 1f, direction) - yawA, Vector3.up);
            var localEnd = Quaternion.Inverse(endYawRotation * from.rotation) * shot.rotation;
            var orientation = yawRotation * from.rotation * Quaternion.Slerp(Quaternion.identity, localEnd, t);
            _camera.transform.SetPositionAndRotation(position, orientation);
            _camera.fieldOfView = Mathf.Lerp(fromFov, shot.fov, t);
        }

    }
}
