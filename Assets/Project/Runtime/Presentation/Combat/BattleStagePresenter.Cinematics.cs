using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FightingAllstar.Presentation.Combat
{
    public sealed partial class BattleStagePresenter
    {
        // Starting values; see Tests/CombatPresentation/ReferencePresentation.md for tuning checks.
        private readonly Dictionary<string, GameObject> _readyAuras = new Dictionary<string, GameObject>();
        private readonly HashSet<string> _suppressedPersistentEffects = new HashSet<string>();
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
                if (renderer is ParticleSystemRenderer || renderer is LineRenderer || ActorVisualEffect.IsEffectRenderer(renderer)) continue;
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

        private static float FighterRadius(Transform view)
        {
            return TryGetFighterBounds(view, out var bounds)
                ? Mathf.Max(.45f, Mathf.Max(bounds.extents.x, bounds.extents.z))
                : .45f;
        }

        public void SetUltimateReady(string id, bool ready)
        {
            if (_readyAuras.TryGetValue(id, out var existing))
            {
                if (ready && existing != null)
                {
                    existing.SetActive(!_suppressedPersistentEffects.Contains(id));
                    return;
                }
                if (existing != null) ActorVisualEffectGeometry.Release(existing);
                _readyAuras.Remove(id);
            }
            if (!ready || !TryView(id, out var view)) return;
            var aura = CreateEnergy(FighterGroundPosition(view), new Color(1f, .76f, .08f), true, FighterRadius(view) + .25f);
            aura.transform.SetParent(view, true);
            aura.SetActive(!_suppressedPersistentEffects.Contains(id));
            _readyAuras[id] = aura;
        }

        private readonly Dictionary<string, GameObject> _stanceAuras = new Dictionary<string, GameObject>();
        public void SetStance(string id, bool active)
        {
            if (_stanceAuras.TryGetValue(id, out var existing))
            {
                if (active && existing != null)
                {
                    existing.SetActive(!_suppressedPersistentEffects.Contains(id));
                    return;
                }
                if (existing != null) ActorVisualEffectGeometry.Release(existing);
                _stanceAuras.Remove(id);
            }
            if (!active || !TryView(id, out var view)) return;
            var aura = CreateEnergy(FighterGroundPosition(view), new Color(.2f, .8f, 1f), true, FighterRadius(view) + .1f);
            aura.name = "Stance Aura";
            aura.transform.SetParent(view, true);
            aura.SetActive(!_suppressedPersistentEffects.Contains(id));
            _stanceAuras[id] = aura;
        }

        private static float FighterHeight(Transform view)
        {
            return TryGetFighterBounds(view, out var bounds)
                ? Mathf.Clamp(bounds.size.y, 1.7f, 4.2f)
                : 2f;
        }

        private static Vector3 FighterGroundPosition(Transform view)
        {
            if (!TryGetFighterBounds(view, out var bounds)) return view.position;
            return new Vector3(view.position.x, bounds.min.y, view.position.z);
        }

        private static bool TryGetFighterBounds(Transform view, out Bounds bounds)
        {
            bounds = default;
            var foundRenderer = false;
            foreach (var renderer in view.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled || renderer is ParticleSystemRenderer || renderer is LineRenderer ||
                    IsShieldRenderer(renderer) || ActorVisualEffect.IsEffectRenderer(renderer)) continue;
                if (!foundRenderer)
                {
                    bounds = renderer.bounds;
                    foundRenderer = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return foundRenderer;
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
            var burst = CreateEnergy(FighterGroundPosition(view), color, false, FighterRadius(view) + .25f);
            _bursts.Add(burst);
            Destroy(burst, .85f);
            _bursts.RemoveAll(item => item == null);
        }

        private GameObject CreateEnergy(Vector3 position, Color color, bool loop, float radius)
        {
            var root = new GameObject(loop ? "Ultimate Ready Aura" : "Skill Energy");
            root.transform.position = position + Vector3.up * .06f;
            EnsureEnergyMaterial();
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

        private void EnsureEnergyMaterial()
        {
            if (_energyMaterial != null) return;
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader != null) _energyMaterial = new Material(shader) { name = "Runtime Energy Lines" };
        }

        public void ClearAuras()
        {
            ClearActorVisualEffects();
            foreach (var aura in _readyAuras.Values) if (aura != null) ActorVisualEffectGeometry.Release(aura);
            _readyAuras.Clear();
            foreach (var aura in _stanceAuras.Values) if (aura != null) ActorVisualEffectGeometry.Release(aura);
            _stanceAuras.Clear();
            foreach (var aura in _shieldAuras.Values) if (aura != null) DestroyShieldVisual(aura);
            _shieldAuras.Clear();
            _suppressedPersistentEffects.Clear();
            foreach (var burst in _bursts) if (burst != null) DestroyShieldVisual(burst);
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
            if (_energyMaterial != null) ActorVisualEffectGeometry.Release(_energyMaterial);
            if (_shieldSurfaceMaterial != null) ActorVisualEffectGeometry.Release(_shieldSurfaceMaterial);
            if (_shieldNodeMaterial != null) ActorVisualEffectGeometry.Release(_shieldNodeMaterial);
            foreach (var clip in _cues.Values) if (clip != null) ActorVisualEffectGeometry.Release(clip);
        }
    }
}
