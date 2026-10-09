using System.Collections;
using FightingAllstar.Core.Combat;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    public sealed partial class BattleStagePresenter
    {
        private GameObject _ultimateReveal;
        public bool UltimateRevealActive => _ultimateReveal != null;
        public bool UltimateLeadInActive { get; private set; }

        public IEnumerator PrepareUltimate(TeamSide side)
        {
            yield return RecoverAttacker();
            EndExecutionContext();
            if (_camera == null) yield break;
            _cutFromSupport = false;
            ApplyShot(PlanningShot(side));
            PlayCue(3);
            UltimateLeadInActive = true;
            try
            {
                // Starting value: let the arena-wide anticipation read before the portrait.
                // Tune with the ultimate lead-in playtest in ActorDrivenExecution.md.
                yield return new WaitForSecondsRealtime(.35f);
            }
            finally { UltimateLeadInActive = false; }
        }

        // Reusable fallback for fighters without an authored ultimate cinematic. The
        // actor drives the reveal; damage and target effects still enter through Action.
        private IEnumerator UltimateReveal(Transform source)
        {
            if (_camera == null) yield break;
            var bounds = FighterBounds(source);
            var height = bounds.size.y;
            var ground = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            var pose = new Pose(source.position, source.rotation);
            _ultimateReveal = new GameObject("Ultimate Reveal Energy");
            var aura = CreateEnergy(ground, new Color(.25f, .9f, 1f), true, height * .7f);
            aura.transform.SetParent(_ultimateReveal.transform, true);
            EnsureEnergyMaterial();
            var spiral = new GameObject("Rising energy spiral").AddComponent<LineRenderer>();
            spiral.transform.SetParent(_ultimateReveal.transform, false);
            spiral.sharedMaterial = _energyMaterial;
            spiral.positionCount = 96;
            spiral.widthMultiplier = height * .035f;
            spiral.startColor = new Color(.2f, .8f, 1f, .8f);
            spiral.endColor = new Color(.8f, 1f, 1f, 0f);
            try
            {
                // Energy reveal plays inside the fixed Wind Up shot, with no extra
                // detail/hero camera states or actor tracking.
                yield return new WaitForSecondsRealtime(.3f);
                yield return Tween(1.15f, t =>
                {
                    source.position = pose.position + Vector3.up * height * .16f * Mathf.Sin(t * Mathf.PI * .5f);
                    for (var i = 0; i < spiral.positionCount; i++)
                    {
                        var progress = i / (float)(spiral.positionCount - 1);
                        var angle = progress * Mathf.PI * 7f - t * Mathf.PI * 5f;
                        var radius = height * (.65f + progress * .5f) * Mathf.Min(1f, t * 3f);
                        spiral.SetPosition(i, ground + new Vector3(Mathf.Cos(angle) * radius,
                            progress * height * 3.2f * t, Mathf.Sin(angle) * radius));
                    }
                    aura.transform.localScale = Vector3.one * (1f + t * .7f);
                });
                Pulse(_execution.SourceId, new Color(.6f, .95f, 1f));
                PlayCue(4);
                yield return Tween(.4f, t =>
                {
                    source.rotation = pose.rotation * Quaternion.Euler(-12f * Mathf.Sin(t * Mathf.PI), 0, 0);
                    spiral.widthMultiplier = height * .035f * (1f + t * 4f);
                });
            }
            finally
            {
                source.SetPositionAndRotation(pose.position, pose.rotation);
                ClearUltimateReveal();
            }
        }

        private void ClearUltimateReveal()
        {
            if (_ultimateReveal != null) Destroy(_ultimateReveal);
            _ultimateReveal = null;
        }
    }
}
