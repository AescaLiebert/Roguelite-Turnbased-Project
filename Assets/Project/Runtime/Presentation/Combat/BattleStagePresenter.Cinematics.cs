using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    public sealed partial class BattleStagePresenter
    {
        // Starting values; see Tests/CombatPresentation/ReferencePresentation.md for tuning checks.
        private int _executionRank = 1;
        private readonly Dictionary<string, GameObject> _readyAuras = new Dictionary<string, GameObject>();
        private readonly List<GameObject> _bursts = new List<GameObject>();
        private Material _energyMaterial;
        private AudioSource _cueSource;
        private readonly Dictionary<int, AudioClip> _cues = new Dictionary<int, AudioClip>();
        private int _cueVariation;

        public IEnumerator EnterFromSky(string id)
        {
            yield return RecoverAttacker();
            if (!TryView(id, out var view)) yield break;

            var landingPose = new Pose(view.position, view.rotation);
            var landingScale = view.localScale;
            var modelHeight = 1f;
            foreach (var renderer in view.GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer || renderer is LineRenderer) continue;
                modelHeight = Mathf.Max(modelHeight, renderer.bounds.size.y);
            }

            // Starting values: a model-relative drop keeps small and large fighters readable.
            // Validate by checking that all formation slots land clearly without slowing turn flow.
            var dropHeight = Mathf.Clamp(modelHeight * 2.4f, 4.5f, 7.5f);
            view.position = landingPose.position + Vector3.up * dropHeight;
            view.rotation = landingPose.rotation * Quaternion.Euler(-8f, 0f, 0f);
            view.localScale = landingScale * .92f;
            TriggerIfPresent(view, "Fall");

            yield return Tween(.48f, t =>
            {
                var fall = t * t;
                view.position = Vector3.LerpUnclamped(landingPose.position + Vector3.up * dropHeight,
                    landingPose.position, fall);
                view.rotation = Quaternion.Slerp(landingPose.rotation * Quaternion.Euler(-8f, 0f, 0f),
                    landingPose.rotation, fall);
                view.localScale = Vector3.Lerp(landingScale * .92f, landingScale, fall);
            }, smooth: false);

            PlayCue(2);
            Pulse(id, new Color(.35f, .85f, 1f));
            TriggerIfPresent(view, "Land");
            yield return Tween(.1f, t => view.localScale = Vector3.Scale(landingScale,
                new Vector3(1f + .08f * t, 1f - .12f * t, 1f + .08f * t)));
            yield return Tween(.14f, t => view.localScale = Vector3.Lerp(
                Vector3.Scale(landingScale, new Vector3(1.08f, .88f, 1.08f)), landingScale, t));

            view.SetPositionAndRotation(landingPose.position, landingPose.rotation);
            view.localScale = landingScale;
            TriggerIfPresent(view, "Idle");
        }

        public IEnumerator BeginExecution(string sourceId, string targetId, int rank, bool ultimate)
        {
            yield return RecoverAttacker();
            ClearCameraTracking();
            _executionRank = Mathf.Clamp(rank, 1, 3);
            _executionUltimate = ultimate;
            _portraitPrepared = false;
            _animationTiming = null;
            _templateHits = _templateHit = 0;
            _templateTargets.Clear();
            if (!TryView(sourceId, out var source)) yield break;
            _attacker = sourceId;
            if (_executionRank == 1 && !ultimate) yield break;

            // Rank-Card-ex: 7.75s / 14.25s. Cut BEFORE the charge, not after it.
            var portrait = PortraitShot(source, _executionRank >= 3 || ultimate);
            yield return CameraCut(portrait.position, portrait.rotation, portrait.fov);
            _portraitPrepared = true;
            TriggerIfPresent(source, ultimate ? "Ultimate" : "Charge");
            PlayCue(ultimate ? 4 : 3);
            Pulse(sourceId, ultimate ? new Color(1f, .55f, .08f) : new Color(.6f, .8f, 1f));
            var scale = source.localScale;
            var rotation = source.rotation;
            // Starting timings adapted to placeholder clips; reference captures run at x2.
            yield return Tween(ultimate ? .8f : _executionRank >= 3 ? .85f : .65f, t =>
            {
                source.localScale = Vector3.Scale(scale, new Vector3(1 + .035f * Mathf.Sin(t * Mathf.PI), 1 - .05f * Mathf.Sin(t * Mathf.PI), 1));
                source.rotation = rotation * Quaternion.Euler(-7 * Mathf.Sin(t * Mathf.PI), 0, 0);
            });
            source.localScale = scale;
            source.rotation = rotation;
            if (!ultimate) yield break;

            // Ultimates have their own reveal after the portrait/title overlay. This is a
            // model-independent camera fallback, not a replacement for authored skill films.
            var hero = FrameFighters(new[] { source }, Facing(source) + Vector3.up * .18f,
                48f, -8f, 1.35f);
            yield return CameraCut(hero.position, hero.rotation, hero.fov);
            yield return new WaitForSecondsRealtime(.35f);
            var side = FrameFighters(new[] { source }, Quaternion.AngleAxis(55f, Vector3.up) * Facing(source) +
                Vector3.up * .3f, 48f, 5f, 1.4f);
            yield return CameraTo(side.position, side.rotation, side.fov, .55f);
        }

        private static float FighterRadius(Transform view)
        {
            var radius = .45f;
            foreach (var renderer in view.GetComponentsInChildren<Renderer>())
            {
                if (renderer is ParticleSystemRenderer || renderer is LineRenderer) continue;
                radius = Mathf.Max(radius, Mathf.Max(renderer.bounds.extents.x, renderer.bounds.extents.z));
            }
            return radius;
        }

        public void SetUltimateReady(string id, bool ready)
        {
            if (_readyAuras.TryGetValue(id, out var existing))
            {
                if (ready && existing != null) return;
                if (existing != null) Destroy(existing);
                _readyAuras.Remove(id);
            }
            if (!ready || !TryView(id, out var view)) return;
            var aura = CreateEnergy(view.position, new Color(1f, .76f, .08f), true, FighterRadius(view) + .25f);
            aura.transform.SetParent(view, true);
            _readyAuras[id] = aura;
        }

        private readonly Dictionary<string, GameObject> _stanceAuras = new Dictionary<string, GameObject>();
        public void SetStance(string id, bool active)
        {
            if (_stanceAuras.TryGetValue(id, out var existing))
            {
                if (active && existing != null) return;
                if (existing != null) Destroy(existing);
                _stanceAuras.Remove(id);
            }
            if (!active || !TryView(id, out var view)) return;
            var aura = CreateEnergy(view.position, new Color(.2f, .8f, 1f), true, FighterRadius(view) + .1f);
            aura.name = "Stance Aura";
            aura.transform.SetParent(view, true);
            _stanceAuras[id] = aura;
        }

        public IEnumerator WaitForActionEnd(string id)
        {
            if (!TryView(id, out var view)) yield break;
            var animator = view.GetComponentInChildren<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled) yield break;
            yield return null;
            // Starting watchdog; malformed action clips must not lock playback.
            var deadline = Time.realtimeSinceStartup + 8f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var state = animator.GetCurrentAnimatorStateInfo(0);
                if (!animator.IsInTransition(0) && (state.IsName("Idle") || state.IsTag("Idle") ||
                    state.normalizedTime >= 1f)) break;
                yield return null;
            }
            TriggerIfPresent(view, "Idle");
        }

        public void Pulse(string id, Color color)
        {
            if (!TryView(id, out var view)) return;
            var burst = CreateEnergy(view.position, color, false, FighterRadius(view) + .25f);
            _bursts.Add(burst);
            Destroy(burst, .85f);
            _bursts.RemoveAll(item => item == null);
        }

        private GameObject CreateEnergy(Vector3 position, Color color, bool loop, float radius)
        {
            var root = new GameObject(loop ? "Ultimate Ready Aura" : "Skill Energy");
            root.transform.position = position + Vector3.up * .06f;
            if (_energyMaterial == null)
            {
                var shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader != null) _energyMaterial = new Material(shader);
            }
            var ring = root.AddComponent<LineRenderer>();
            ring.sharedMaterial = _energyMaterial;
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 48;
            ring.widthMultiplier = .055f;
            ring.startColor = ring.endColor = color;
            for (var i = 0; i < 48; i++)
            {
                var angle = i * Mathf.PI * 2 / 48;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius));
            }
            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = loop;
            main.duration = .7f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.3f, .7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(.035f, .09f);
            main.startColor = color;
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.rotation = new Vector3(-90, 0, 0);
            shape.randomDirectionAmount = .15f;
            var emission = particles.emission;
            emission.rateOverTime = loop ? 26 : 0;
            if (!loop) emission.SetBursts(new[] { new ParticleSystem.Burst(0, 28) });
            var velocity = particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.y = 1.2f;
            var fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(color, 0), new GradientColorKey(color, 1) },
                new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .12f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _energyMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 3;
            particles.Play();
            return root;
        }

        public void ClearAuras()
        {
            foreach (var aura in _readyAuras.Values) if (aura != null) Destroy(aura);
            _readyAuras.Clear();
            foreach (var aura in _stanceAuras.Values) if (aura != null) Destroy(aura);
            _stanceAuras.Clear();
            foreach (var burst in _bursts) if (burst != null) Destroy(burst);
            _bursts.Clear();
            if (_cueSource != null) _cueSource.Stop();
        }

        public void PlayCue(int kind)
        {
            if (_cueSource == null)
            {
                _cueSource = gameObject.AddComponent<AudioSource>();
                _cueSource.playOnAwake = false;
                _cueSource.spatialBlend = 0;
                _cueSource.volume = .18f;
            }
            if (!_cues.TryGetValue(kind, out var clip))
            {
                const int rate = 22050;
                var samples = new float[(int)(rate * (kind >= 3 ? .3f : .12f))];
                for (var i = 0; i < samples.Length; i++)
                {
                    var t = i / (float)rate;
                    var envelope = Mathf.Sin(Mathf.PI * i / samples.Length) * Mathf.Exp(-t * 12);
                    var frequency = kind == 0 ? 800 : kind >= 3 ? 440 : 140;
                    samples[i] = Mathf.Sin(2 * Mathf.PI * (frequency * t + (kind >= 3 ? 700 : -180) * t * t)) * envelope;
                }
                clip = AudioClip.Create("Presentation cue " + kind, samples.Length, 1, rate, false);
                clip.SetData(samples, 0);
                _cues[kind] = clip;
            }
            _cueSource.pitch = 1 + ((_cueVariation++ % 3) - 1) * .055f;
            _cueSource.PlayOneShot(clip);
        }

        private void OnDestroy()
        {
            ClearAuras();
            if (_energyMaterial != null) Destroy(_energyMaterial);
            foreach (var clip in _cues.Values) if (clip != null) Destroy(clip);
        }
    }
}
