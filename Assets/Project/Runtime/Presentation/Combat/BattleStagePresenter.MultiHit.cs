using System.Collections;
using System.Collections.Generic;
using FightingAllstar.Core.Content;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    public sealed partial class BattleStagePresenter
    {
        private int _templateHits;
        private int _templateHit;
        private AttackRange _templateRange;
        private bool _templateArea;
        private Vector3 _templateAnchor;
        private Vector3 _templateDirection;
        private Quaternion _templateFacing;
        private readonly List<Transform> _templateTargets = new List<Transform>();
        private float TemplateStrikeDuration => .72f / _templateHits;

        // Starting timings and distances. See Tests/CombatPresentation/MultiHit.md.
        public IEnumerator BeginDamageAttack(string sourceId, IReadOnlyList<string> targetIds,
            int hits, AttackRange range, bool area)
        {
            if (_attacker != sourceId) yield return RecoverAttacker();
            if (!TryView(sourceId, out var source)) yield break;
            _templateTargets.Clear();
            _templateTargets.AddRange(ResolveViews(targetIds));
            if (_templateTargets.Count == 0) yield break;
            _attacker = sourceId;
            _templateHits = Mathf.Clamp(hits, 1, 10);
            _templateHit = 0;
            _templateRange = range;
            _templateArea = area;
            // Generated templates own their strike times. Unrelated Animator clip markers
            // must not delay or prematurely resolve these procedural sequences.
            _animationTiming = null;
            _actionMotionDone = true;
            var center = Vector3.zero;
            foreach (var target in _templateTargets) center += target.position;
            center /= _templateTargets.Count;
            _templateDirection = GroundDirection(center - source.position, Facing(source));
            var end = source.position;
            if (range == AttackRange.Close)
            {
                var separation = FighterRadius(source) + .6f;
                foreach (var target in _templateTargets) separation = Mathf.Max(separation, FighterRadius(source) + FighterRadius(target) + .45f);
                // Stop in front of the nearest target plane, so wide AOEs cannot pass
                // through a front-row fighter on their way toward the group center.
                var travel = float.MaxValue;
                foreach (var target in _templateTargets)
                    travel = Mathf.Min(travel, Vector3.Dot(target.position - source.position, _templateDirection) - separation);
                end += _templateDirection * Mathf.Max(0, travel);
            }
            yield return AttackMotion(source, _templateTargets, _templateDirection, end);
            _templateAnchor = source.position;
            _templateFacing = Quaternion.LookRotation(_templateDirection);
            yield return DamageHit(1);
        }

        public IEnumerator DamageHit(int index, IReadOnlyList<string> remainingTargets = null)
        {
            if (_templateHits == 0 || index <= _templateHit || !TryView(_attacker, out var source)) yield break;
            if (remainingTargets != null)
            {
                _templateTargets.Clear();
                _templateTargets.AddRange(ResolveViews(remainingTargets));
            }
            _templateHit = index;
            var heavy = _templateHits == 1;
            var finisher = index == _templateHits;
            var side = Vector3.Cross(Vector3.up, _templateDirection);
            var sign = index % 2 == 1 ? 1f : -1f;
            var close = _templateRange == AttackRange.Close;
            var triple = _templateHits == 3;
            // More barrage hits shorten each beat while keeping their combined strike
            // window fixed. The approach and recovery still run once for the action.
            var duration = heavy ? .42f : _templateHits > 3 ? TemplateStrikeDuration : finisher ? .23f : .2f;
            TriggerIfPresent(source, "Attack");
            PlayCue(0);
            var begin = source.position;
            var rotation = source.rotation;
            yield return Tween(duration, t =>
            {
                var arc = Mathf.Sin(t * Mathf.PI);
                var lift = heavy ? (close ? .95f : .18f) : triple && index == 2 ? .42f : .1f;
                var weave = !heavy && close ? sign * arc * (_templateArea ? .24f : .14f) : 0f;
                source.position = Vector3.Lerp(begin, _templateAnchor, t) + Vector3.up * lift * arc + side * weave;
                var turn = heavy ? 0f : _templateArea && close ? 360f * t : sign * Mathf.Sin(t * Mathf.PI * 1.5f) * (close ? 32 : 12);
                source.rotation = Quaternion.Slerp(rotation, _templateFacing, t) * Quaternion.Euler(
                    heavy ? -24f * arc : (finisher ? 12f : -8f) * arc, turn, close && !heavy ? sign * 9f * arc : 0);
                UpdateAttackCamera();
            });
            source.SetPositionAndRotation(_templateAnchor, _templateFacing);

            var color = close ? new Color(1f, .65f, .2f) : new Color(.3f, .8f, 1f);
            var targets = _templateTargets;
            if (!close)
            {
                // Comet/lance for a single recipient; storm columns or fanned wind blades
                // for an area. Later barrage strikes alternate launch sides and heights.
                var projectiles = new List<(LineRenderer line, Vector3 from, Vector3 to)>();
                foreach (var target in targets)
                {
                    if (target == null || !target.gameObject.activeInHierarchy) continue;
                    var impact = FighterBounds(target).center;
                    var from = _templateArea && (heavy || _templateHits > 3)
                        ? impact + Vector3.up * (heavy ? 3.5f : 2.2f) + side * sign * .55f
                        : FighterBounds(source).center + side * sign * .35f + Vector3.up * (triple ? index * .18f : 0);
                    projectiles.Add((CreateStrikeLine(color, heavy ? .16f : .08f), from, impact));
                }
                var projectileDuration = heavy ? .26f : _templateHits > 3 ? TemplateStrikeDuration : .12f;
                yield return Tween(projectileDuration, t =>
                {
                    foreach (var bolt in projectiles)
                    {
                        var point = Vector3.Lerp(bolt.from, bolt.to, t);
                        var bow = _templateHits == 2 ? side * sign * Mathf.Sin(t * Mathf.PI) * .6f : Vector3.zero;
                        bolt.line.SetPosition(0, Vector3.Lerp(bolt.from, bolt.to, Mathf.Max(0, t - .18f)) + bow);
                        bolt.line.SetPosition(1, point + bow);
                    }
                }, false);
                foreach (var bolt in projectiles) if (bolt.line != null) Destroy(bolt.line.gameObject);
            }
            foreach (var target in targets)
            {
                if (target == null || !target.gameObject.activeInHierarchy) continue;
                var center = FighterBounds(target).center;
                if (heavy || _templateArea && finisher)
                {
                    var burst = CreateEnergy(target.position, color, false, FighterRadius(target) + (heavy ? .5f : .2f));
                    TrackStrike(burst);
                }
                var slash = CreateStrikeLine(color, heavy || finisher ? .11f : .055f);
                var radius = FighterRadius(target) + .35f;
                var screenRight = _camera != null ? _camera.transform.right : side;
                var diagonal = heavy ? Vector3.up : (screenRight * sign + Vector3.up * (triple && index == 2 ? -1 : .65f)).normalized;
                if (_templateArea && !heavy)
                {
                    slash.positionCount = 15;
                    for (var point = 0; point < 15; point++)
                    {
                        var angle = Mathf.Lerp(-135, 135, point / 14f) * Mathf.Deg2Rad;
                        slash.SetPosition(point, center + side * Mathf.Sin(angle) * radius + _templateDirection * Mathf.Cos(angle) * radius + Vector3.up * sign * .15f * Mathf.Sin(angle));
                    }
                }
                else
                {
                    // Draw the slash just in front of the visible body rather than through
                    // its center, where opaque character meshes hide the confirmation.
                    if (_camera != null)
                        center += (_camera.transform.position - center).normalized * (FighterRadius(target) + .12f);
                    slash.SetPosition(0, center - diagonal * radius);
                    slash.SetPosition(1, center + diagonal * radius);
                }
            }
            // Returning is the impact boundary; the next event applies HP and plays audio/recoil.
        }

        private LineRenderer CreateStrikeLine(Color color, float width)
        {
            if (_energyMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader != null) _energyMaterial = new Material(shader);
            }
            var obj = new GameObject("Damage Template Strike");
            var line = obj.AddComponent<LineRenderer>();
            line.sharedMaterial = _energyMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = width;
            line.startColor = color;
            line.endColor = new Color(color.r, color.g, color.b, .2f);
            TrackStrike(obj);
            return line;
        }

        private void TrackStrike(GameObject obj)
        {
            _bursts.RemoveAll(item => item == null);
            _bursts.Add(obj);
            Destroy(obj, .75f);
        }
    }
}
