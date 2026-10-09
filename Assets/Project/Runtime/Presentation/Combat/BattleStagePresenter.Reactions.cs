using System.Collections;
using System.Collections.Generic;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    public sealed partial class BattleStagePresenter
    {
        [Header("Target Reactions (starting values)")]
        [SerializeField, Min(.05f)] private float reactionDuration = .65f;
        [SerializeField, Min(0f)] private float knockBackDistance = 1.6f;
        [SerializeField, Min(0f)] private float knockBackHoldDuration = 1f;
        [SerializeField, Min(0f)] private float knockUpHeight = 2.4f;
        [SerializeField, Min(0f)] private float knockUpDistance = 1.8f;
        [SerializeField, Min(.05f)] private float knockUpDuration = 2f;

        private sealed class ReactionPose
        {
            public Transform Target;
            public Vector3 Position, Scale, Away, CenterOffset, FootOffset, BodySize;
            public Quaternion Rotation;
            public HitReaction Kind;
            public bool Fell, Recovering, Returning, Holding, Blending;
            public float Duration, GroundCenterHeight, GroundY, Elapsed, BlendElapsed;
            public Vector3 BlendPosition, BlendScale;
            public Quaternion BlendRotation;
        }

        private readonly Dictionary<Transform, ReactionPose> _reactionPoses = new Dictionary<Transform, ReactionPose>();
        public bool TargetsReady => _reactionPoses.Count == 0;

        /// <summary>Release held multi-hit responses, then wait for fall/get-up before an action boundary.</summary>
        public IEnumerator WaitForTargetReactions()
        {
            foreach (var entry in _reactionPoses.Values) entry.Holding = false;
            while (!TargetsReady) yield return null;
        }

        public IEnumerator React(BattleEvent item, float durationScale = 1f)
        {
            if (item == null) yield break;
            yield return ReactMultiple(new[] { item }, durationScale);
        }

        /// <summary>One parallel reaction per recipient; cancellation takes precedence over damage.</summary>
        public IEnumerator ReactMultiple(IReadOnlyList<BattleEvent> impacts, float durationScale = 1f)
        {
            if (impacts == null || impacts.Count == 0) yield break;
            var facts = new Dictionary<string, BattleEvent>();
            foreach (var item in impacts)
            {
                if (item == null || string.IsNullOrEmpty(item.TargetId)) continue;
                if (!facts.TryGetValue(item.TargetId, out var existing) || item.WasStanceCancelled || !existing.WasStanceCancelled)
                    facts[item.TargetId] = item;
            }
            var entries = new List<ReactionPose>();
            var feedbackDuration = .28f * Mathf.Clamp(durationScale, .1f, 1f);
            var anyCritical = false;
            var force = false;
            foreach (var item in facts.Values)
            {
                if (!TryView(item.TargetId, out var target) || !target.gameObject.activeInHierarchy) continue;
                // Legacy recordings/previews retain their hit response, including stance tolerance.
                var kind = item.HasHitReaction ? item.Reaction :
                    _stanceAuras.ContainsKey(item.TargetId) ? HitReaction.None : HitReaction.Hit;
                anyCritical |= item.WasCritical;
                force |= item.WasStanceCancelled;
                var moreHits = item.HitCount > 1 && item.HitIndex > 0 && item.HitIndex < item.HitCount;
                if (_reactionPoses.TryGetValue(target, out var active))
                {
                    // Light recoil/None cannot reset a target already launched or knocked down.
                    // The final packet (even an endured/resisted one) releases that response.
                    if (!moreHits) active.Holding = false;
                    if (kind == active.Kind && kind != HitReaction.Hit)
                    {
                        active.Holding = moreHits && !item.WasStanceCancelled;
                        entries.Add(active);
                        continue;
                    }
                    if (kind == HitReaction.None || kind == HitReaction.Hit)
                    {
                        entries.Add(active);
                        continue;
                    }
                    // A forced override changes direction smoothly without losing the home pose.
                    active.BlendPosition = target.position;
                    active.BlendRotation = target.rotation;
                    active.BlendScale = target.localScale;
                    active.Blending = true;
                    active.BlendElapsed = 0;
                    active.Kind = kind;
                    active.Elapsed = 0;
                    active.Fell = active.Recovering = active.Returning = false;
                    active.Duration = StrongReactionDuration(kind);
                    active.Holding = moreHits && !item.WasStanceCancelled;
                    entries.Add(active);
                    TriggerReaction(target, kind);
                    continue;
                }
                if (kind == HitReaction.None) continue;
                var away = TryView(item.SourceId, out var source) ? target.position - source.position : -target.forward;
                away = GroundDirection(away, -target.forward);
                var bounds = TryGetFighterBounds(target, out var visibleBounds) ? visibleBounds : FighterBounds(target);
                var entry = new ReactionPose { Target = target, Position = target.position, Rotation = target.rotation,
                    Scale = target.localScale, Away = away, Kind = kind,
                    CenterOffset = bounds.center - target.position,
                    FootOffset = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) - target.position,
                    BodySize = bounds.size,
                    GroundY = bounds.min.y,
                    GroundCenterHeight = Mathf.Max(.08f, Mathf.Min(bounds.extents.x, bounds.extents.z)),
                    Duration = kind == HitReaction.Hit ? feedbackDuration : StrongReactionDuration(kind),
                    Holding = kind != HitReaction.Hit && moreHits && !item.WasStanceCancelled };
                _reactionPoses[target] = entry;
                entries.Add(entry);
                TriggerReaction(target, kind);
                StartCoroutine(RunTargetReaction(entry));
            }
            // Damage can still be heard when a stance keeps the recipient motionless.
            PlayCue(anyCritical || force ? 2 : 1);
            if (entries.Count == 0) yield break;
            // Feedback does not move the held phase camera, even on critical impacts.
            yield return new WaitForSecondsRealtime(feedbackDuration);
            // Intermediate hits can continue while a strong response holds. A final hit
            // owns its complete recovery, rather than cutting it to the barrage interval.
            foreach (var entry in entries)
                while (!entry.Holding && entry.Target != null &&
                    _reactionPoses.TryGetValue(entry.Target, out var current) && current == entry)
                    yield return null;
        }

        private static void TriggerReaction(Transform target, HitReaction kind) =>
            TriggerIfPresent(target, kind == HitReaction.KnockBack ? "KnockBack" :
                kind == HitReaction.KnockDown ? "KnockDown" : kind == HitReaction.KnockUp ? "KnockUp" : "Hurt");

        private float StrongReactionDuration(HitReaction kind) =>
            Mathf.Max(.05f, kind == HitReaction.KnockUp ? knockUpDuration :
                kind == HitReaction.KnockBack ? .7f + knockBackHoldDuration : reactionDuration);

        private void SnapshotReactionPath(string sourceId, IReadOnlyList<string> targetIds, HitReaction kind)
        {
            if (_execution == null) return;
            // Reserve displacement before entering Attack. No damage/recovery event can
            // change the camera shot afterwards. Snapshot formation, not live reaction poses.
            foreach (var target in ResolveViews(targetIds))
            {
                if (!TryView(sourceId, out var source) || target == source) continue;
                var bounds = FighterBounds(target);
                var entry = new ReactionPose { Position = target.position,
                    Away = GroundDirection(target.position - source.position, -target.forward),
                    CenterOffset = bounds.center - target.position,
                    FootOffset = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z) - target.position,
                    BodySize = bounds.size, GroundCenterHeight = Mathf.Max(.08f, Mathf.Min(bounds.extents.x, bounds.extents.z)) };
                // Stance cancellation can always force a knockback, so reserve it too.
                _execution.TargetBounds.Encapsulate(new Bounds(bounds.center + entry.Away * knockBackDistance, bounds.size));
                if (kind == HitReaction.KnockUp) EncapsulateKnockUpPath(entry);
            }
        }

        private void EncapsulateKnockUpPath(ReactionPose entry)
        {
            var apex = entry.Position + entry.CenterOffset + entry.Away * (knockUpDistance * .5f) + Vector3.up * knockUpHeight;
            _execution.TargetBounds.Encapsulate(new Bounds(apex, entry.BodySize));
            var side = Vector3.Cross(Vector3.up, entry.Away);
            var width = Mathf.Max(entry.BodySize.x, entry.BodySize.z);
            var groundSize = new Vector3(Mathf.Abs(entry.Away.x) * entry.BodySize.y + Mathf.Abs(side.x) * width,
                entry.GroundCenterHeight * 2, Mathf.Abs(entry.Away.z) * entry.BodySize.y + Mathf.Abs(side.z) * width);
            var landingCenter = entry.Position + entry.FootOffset + entry.Away * (knockUpDistance + entry.BodySize.y * .5f)
                + Vector3.up * entry.GroundCenterHeight;
            _execution.TargetBounds.Encapsulate(new Bounds(landingCenter, groundSize));
        }

        private IEnumerator RunTargetReaction(ReactionPose entry)
        {
            while (entry.Target != null && entry.Target.gameObject.activeInHierarchy && entry.Elapsed < entry.Duration)
            {
                entry.Elapsed += Time.unscaledDeltaTime;
                // Pause before descent/recovery, preserving the original snapshot across hits.
                var holdAt = entry.Kind == HitReaction.KnockUp ? .35f :
                    entry.Kind == HitReaction.KnockBack ? .2f / entry.Duration : .32f;
                if (entry.Holding) entry.Elapsed = Mathf.Min(entry.Elapsed, entry.Duration * holdAt);
                var t = Mathf.Clamp01(entry.Elapsed / entry.Duration);
                AnimateReaction(entry, t);
                if (entry.Blending)
                {
                    // Blend time keeps advancing even while a combo holds its pose.
                    entry.BlendElapsed += Time.unscaledDeltaTime;
                    var blend = Mathf.SmoothStep(0, 1, Mathf.Clamp01(entry.BlendElapsed / .16f));
                    entry.Target.SetPositionAndRotation(Vector3.Lerp(entry.BlendPosition, entry.Target.position, blend),
                        Quaternion.Slerp(entry.BlendRotation, entry.Target.rotation, blend));
                    entry.Target.localScale = Vector3.Lerp(entry.BlendScale, entry.Target.localScale, blend);
                    if (blend >= 1) entry.Blending = false;
                }
                yield return null;
            }
            if (entry.Target != null)
            {
                RestoreReaction(entry);
                if (entry.Kind != HitReaction.Hit) PlayReactionAnimation(entry.Target, "Idle");
            }
            _reactionPoses.Remove(entry.Target);
        }

        private void AnimateReaction(ReactionPose entry, float t)
        {
            var target = entry.Target;
            if (entry.Kind == HitReaction.Hit)
            {
                var pulse = Mathf.Sin(t * Mathf.PI);
                target.position = entry.Position + entry.Away * (.25f * pulse);
                target.rotation = entry.Rotation * Quaternion.Euler(-12f * pulse, 0, 6f * pulse);
                target.localScale = Vector3.Scale(entry.Scale, new Vector3(1 + pulse * .05f, 1 - pulse * .08f, 1));
                return;
            }
            if (entry.Kind == HitReaction.KnockUp)
            {
                AnimateKnockUp(entry, t);
                return;
            }
            if (entry.Kind == HitReaction.KnockBack)
            {
                AnimateKnockBack(entry, t);
                return;
            }
            // Recovery is part of this response, keeping original formation and target facing.
            var recover = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.68f, 1f, t));
            if (!entry.Recovering && t >= .68f)
            {
                entry.Recovering = true;
                TriggerIfPresent(target, "GetUp");
            }
            if (entry.Kind == HitReaction.KnockDown)
            {
                var trip = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t / .3f)) * (1 - recover);
                SetPronePose(entry, 90f * trip, trip);
                return;
            }
        }

        private void AnimateKnockBack(ReactionPose entry, float t)
        {
            var elapsed = t * entry.Duration;
            var pushed = Mathf.Sin(Mathf.Clamp01(elapsed / .2f) * Mathf.PI * .5f);
            var recoverAt = .2f + knockBackHoldDuration;
            var stand = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(recoverAt, recoverAt + .15f, elapsed));
            var run = Mathf.Clamp01(Mathf.InverseLerp(recoverAt + .15f, entry.Duration, elapsed));
            if (!entry.Recovering && elapsed >= recoverAt)
            {
                entry.Recovering = true;
                TriggerIfPresent(entry.Target, "GetUp");
            }
            BeginReactionReturn(entry, run > 0);
            SetFootAnchoredPose(entry, entry.Position + entry.FootOffset + entry.Away * (knockBackDistance * pushed * (1 - run)),
                22f * pushed * (1 - stand), true);
        }

        private static void BeginReactionReturn(ReactionPose entry, bool returning)
        {
            if (!returning || entry.Returning) return;
            entry.Returning = true;
            PlayReactionAnimation(entry.Target, "Run");
        }

        private static void PlayReactionAnimation(Transform target, string name)
        {
            var animator = target.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null) return;
            foreach (var parameter in animator.parameters)
                if (parameter.type == AnimatorControllerParameterType.Trigger && parameter.name == name)
                { animator.SetTrigger(name); return; }
            var hash = Animator.StringToHash(name);
            if (animator.HasState(0, hash)) { animator.CrossFade(hash, .05f, 0, 0); return; }
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
                if (clip.name.EndsWith("_" + name, System.StringComparison.OrdinalIgnoreCase) &&
                    animator.HasState(0, Animator.StringToHash(clip.name)))
                { animator.CrossFade(clip.name, .05f, 0, 0); return; }
        }

        private void AnimateKnockUp(ReactionPose entry, float t)
        {
            var homeFoot = entry.Position + entry.FootOffset;
            if (t < .62f)
            {
                // A decelerating rise, readable apex, then accelerating fall. The visible
                // feet follow the arc; a centre-pivot prefab never orbits its own root.
                var rise = Mathf.Clamp01(t / .3f);
                var drop = Mathf.Clamp01(Mathf.InverseLerp(.4f, .62f, t));
                var lift = knockUpHeight * (1 - (1 - rise) * (1 - rise)) * (1 - drop * drop);
                var travel = t < .3f ? .45f * rise : t < .4f ? Mathf.Lerp(.45f, .55f, (t - .3f) / .1f) : Mathf.Lerp(.55f, 1, drop);
                SetFootAnchoredPose(entry, homeFoot + entry.Away * (knockUpDistance * travel) + Vector3.up * lift,
                    12f * Mathf.Sin(t / .62f * Mathf.PI), false);
                return;
            }
            if (!entry.Fell)
            {
                entry.Fell = true;
                TriggerIfPresent(entry.Target, "KnockFall");
                PlayCue(2);
            }
            if (!entry.Recovering && t >= .76f)
            {
                entry.Recovering = true;
                TriggerIfPresent(entry.Target, "GetUp");
            }
            var fall = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.62f, .72f, t));
            var stand = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.76f, .89f, t));
            // Stand where the actor landed before returning to formation, never drag a
            // prone body home or snap it back as soon as the damage window ends.
            var returnHome = Mathf.Clamp01(Mathf.InverseLerp(.89f, 1f, t));
            BeginReactionReturn(entry, returnHome > 0);
            SetFootAnchoredPose(entry, homeFoot + entry.Away * (knockUpDistance * (1 - returnHome)),
                90f * fall * (1 - stand), true);
        }

        private static void SetFootAnchoredPose(ReactionPose entry, Vector3 foot, float angle, bool grounded)
        {
            var delta = Quaternion.AngleAxis(angle, Vector3.Cross(Vector3.up, entry.Away));
            entry.Target.SetPositionAndRotation(foot - delta * entry.FootOffset, delta * entry.Rotation);
            if (grounded && TryGetFighterBounds(entry.Target, out var bounds))
                entry.Target.position += Vector3.up * (entry.GroundY - bounds.min.y);
        }

        private static void SetPronePose(ReactionPose entry, float angle, float amount)
        {
            var tilt = entry.Rotation * Quaternion.Euler(angle, 0, 0);
            var delta = tilt * Quaternion.Inverse(entry.Rotation);
            // Rotate about the visible body's center, then lower it to the ground. Its
            // horizontal center stays at the same place for a face-down trip or back fall.
            var center = entry.Position + entry.CenterOffset;
            center.y = Mathf.Lerp(center.y, entry.GroundY + entry.GroundCenterHeight, amount);
            entry.Target.SetPositionAndRotation(center - delta * entry.CenterOffset, tilt);
        }

        private static void RestoreReaction(ReactionPose entry)
        {
            if (entry.Target == null) return;
            entry.Target.SetPositionAndRotation(entry.Position, entry.Rotation);
            entry.Target.localScale = entry.Scale;
        }

        private void RestoreTargetReactions()
        {
            foreach (var entry in _reactionPoses.Values)
            {
                RestoreReaction(entry);
                if (entry.Target != null && entry.Kind != HitReaction.Hit) PlayReactionAnimation(entry.Target, "Idle");
            }
            _reactionPoses.Clear();
        }
    }
}
