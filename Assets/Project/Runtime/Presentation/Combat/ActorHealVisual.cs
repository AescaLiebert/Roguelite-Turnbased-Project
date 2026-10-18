using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>
    /// Upward luminous green particle aura with Genshin-style healing cross symbols,
    /// ascending energy streaks, radiant sparkles, and ground energy ring.
    /// Provides visual feedback for any heal amount from any source (cards, lifesteal, turn recovery).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ActorHealVisual : MonoBehaviour
    {
        private static Shader _healShader;
        private readonly List<Material> _createdMaterials = new List<Material>();

        private ParticleSystem _auraSystem;
        private ParticleSystem _crossSystem;
        private ParticleSystem _needleSystem;
        private ParticleSystem _sparkleSystem;
        private ParticleSystem _groundRingSystem;

        private float _height = 2f;
        private float _radius = 0.6f;
        private float _groundOffset;
        private bool _initialized;
        private bool _hidden;

        public float Height => _height;
        public float Radius => _radius;
        public bool IsPlaying => (_crossSystem != null && _crossSystem.isPlaying) ||
                                 (_auraSystem != null && _auraSystem.isPlaying);

        private static Shader HealShader
        {
            get
            {
                if (_healShader == null)
                    _healShader = Resources.Load<Shader>("ActorHealParticles") ??
                                  Shader.Find("Fighting Allstar/Actor Heal Particles");
                if (_healShader == null)
                    _healShader = Shader.Find("Sprites/Default");
                return _healShader;
            }
        }

        public static ActorHealVisual GetOrCreate(Transform actorView, float height, float radius, float groundOffset)
        {
            if (actorView == null) return null;
            var visual = actorView.GetComponentInChildren<ActorHealVisual>();
            if (visual == null)
            {
                var go = new GameObject("AVE Heal Visual");
                go.transform.SetParent(actorView, false);
                visual = go.AddComponent<ActorHealVisual>();
            }
            visual.Initialize(height, radius, groundOffset);
            return visual;
        }

        public static void PlayOn(Transform actorView, float height, float radius, float groundOffset)
        {
            var visual = GetOrCreate(actorView, height, radius, groundOffset);
            if (visual != null) visual.Play();
        }

        public void Initialize(float height, float radius, float groundOffset)
        {
            _height = Mathf.Clamp(height, 1.4f, 4.2f);
            _radius = Mathf.Clamp(radius, 0.35f, 1.4f);
            _groundOffset = groundOffset;

            if (_initialized) return;
            _initialized = true;

            transform.localPosition = Vector3.up * _groundOffset;
            transform.localRotation = Quaternion.identity;

            // 1. Soft Vertical Aura Column / Luminous Pillar Emitter (Shape 0)
            _auraSystem = CreateAuraEmitter();

            // 2. Genshin Healing Cross (+) Symbols Emitter (Shape 2)
            _crossSystem = CreateCrossEmitter();

            // 3. Ascending High-Velocity Energy Needles / Streaks (Shape 1)
            _needleSystem = CreateNeedleEmitter();

            // 4. Twinkling Diamond Sparkles / Stars (Shape 3)
            _sparkleSystem = CreateSparkleEmitter();

            // 5. Ground Ring Expansion Pulse & Light Pool (Shape 4)
            _groundRingSystem = CreateGroundRingEmitter();
        }

        public void Play()
        {
            if (!_initialized) return;
            if (_hidden) return;

            RestartEmitter(_auraSystem);
            RestartEmitter(_crossSystem);
            RestartEmitter(_needleSystem);
            RestartEmitter(_sparkleSystem);
            RestartEmitter(_groundRingSystem);
        }

        public void Stop()
        {
            if (_auraSystem != null) _auraSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_crossSystem != null) _crossSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_needleSystem != null) _needleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_sparkleSystem != null) _sparkleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_groundRingSystem != null) _groundRingSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        public void SetHidden(bool hidden)
        {
            _hidden = hidden;
            gameObject.SetActive(!hidden);
            if (hidden) Stop();
        }

        private static void RestartEmitter(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play(true);
        }

        private ParticleSystem CreateAuraEmitter()
        {
            var go = new GameObject("Heal Aura Wisps");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.15f;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1.0f;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 30;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.60f, 0.95f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(_radius * 1.3f, _radius * 2.0f);
            main.startSizeY = new ParticleSystem.MinMaxCurve(_height * 0.65f, _height * 1.05f);
            main.startSizeZ = 0.1f;
            main.startColor = new Color(0.40f, 0.95f, 0.30f, 0.62f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = _radius * 0.65f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0.0f, 8),
                new ParticleSystem.Burst(0.10f, 6),
                new ParticleSystem.Burst(0.22f, 4)
            });

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);
            velocity.y = new ParticleSystem.MinMaxCurve(1.8f, 3.0f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.12f, 0.12f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.72f, 1.0f, 0.50f), 0.0f),
                    new GradientColorKey(new Color(0.40f, 0.95f, 0.30f), 0.45f),
                    new GradientColorKey(new Color(0.18f, 0.80f, 0.22f), 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.85f, 0.15f),
                    new GradientAlphaKey(0.55f, 0.65f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            col.color = grad;

            var sizeMod = ps.sizeOverLifetime;
            sizeMod.enabled = true;
            sizeMod.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(
                new Keyframe(0.0f, 0.65f),
                new Keyframe(0.25f, 1.18f),
                new Keyframe(1.0f, 0.80f)
            ));

            var mat = CreateMaterial(0, "Aura");
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = mat;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;

            return ps;
        }

        private ParticleSystem CreateCrossEmitter()
        {
            var go = new GameObject("Heal Cross Symbols");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.2f;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1.05f;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 25;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.68f, 1.02f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f, 2.9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.46f);
            main.startColor = new Color(0.65f, 1.0f, 0.40f, 0.95f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = _radius * 0.85f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0.0f, 6),
                new ParticleSystem.Burst(0.12f, 5),
                new ParticleSystem.Burst(0.25f, 3)
            });

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.20f, 0.20f);
            velocity.y = new ParticleSystem.MinMaxCurve(2.0f, 3.5f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.20f, 0.20f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.88f, 1.0f, 0.68f), 0.0f),
                    new GradientColorKey(new Color(0.58f, 0.98f, 0.38f), 0.5f),
                    new GradientColorKey(new Color(0.28f, 0.88f, 0.24f), 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(1.0f, 0.12f),
                    new GradientAlphaKey(0.88f, 0.65f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            col.color = grad;

            var sizeMod = ps.sizeOverLifetime;
            sizeMod.enabled = true;
            sizeMod.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(
                new Keyframe(0.0f, 0.40f),
                new Keyframe(0.16f, 1.10f),
                new Keyframe(0.70f, 1.00f),
                new Keyframe(1.0f, 0.55f)
            ));

            var mat = CreateMaterial(2, "Cross");
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = mat;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;

            return ps;
        }

        private ParticleSystem CreateNeedleEmitter()
        {
            var go = new GameObject("Heal Ascending Streaks");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.06f;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.90f;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 36;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.12f);
            main.startColor = new Color(0.78f, 1.0f, 0.52f, 0.95f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = _radius * 0.92f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0.0f, 14),
                new ParticleSystem.Burst(0.10f, 10),
                new ParticleSystem.Burst(0.20f, 6)
            });

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
            velocity.y = new ParticleSystem.MinMaxCurve(3.6f, 6.0f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.90f, 1.0f, 0.75f), 0.0f),
                    new GradientColorKey(new Color(0.58f, 0.98f, 0.35f), 0.45f),
                    new GradientColorKey(new Color(0.22f, 0.82f, 0.20f), 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.98f, 0.10f),
                    new GradientAlphaKey(0.75f, 0.55f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            col.color = grad;

            var sizeMod = ps.sizeOverLifetime;
            sizeMod.enabled = true;
            sizeMod.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(
                new Keyframe(0.0f, 0.5f),
                new Keyframe(0.2f, 1.05f),
                new Keyframe(1.0f, 0.4f)
            ));

            var mat = CreateMaterial(1, "Needle");
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = mat;
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.velocityScale = 0.24f;
            rend.lengthScale = 2.6f;
            rend.cameraVelocityScale = 0f;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;

            return ps;
        }

        private ParticleSystem CreateSparkleEmitter()
        {
            var go = new GameObject("Heal Sparkles");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.18f;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 1.0f;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 26;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.50f, 0.88f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.9f, 2.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startColor = new Color(0.88f, 1.0f, 0.65f, 0.90f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = _radius * 0.95f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0.02f, 10),
                new ParticleSystem.Burst(0.14f, 7)
            });

            var velocity = ps.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            velocity.y = new ParticleSystem.MinMaxCurve(1.4f, 2.6f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.96f, 1.0f, 0.80f), 0.0f),
                    new GradientColorKey(new Color(0.68f, 1.0f, 0.48f), 0.55f),
                    new GradientColorKey(new Color(0.32f, 0.90f, 0.28f), 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.95f, 0.18f),
                    new GradientAlphaKey(0.65f, 0.68f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            col.color = grad;

            var sizeMod = ps.sizeOverLifetime;
            sizeMod.enabled = true;
            sizeMod.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(
                new Keyframe(0.0f, 0.35f),
                new Keyframe(0.28f, 1.15f),
                new Keyframe(1.0f, 0.10f)
            ));

            var mat = CreateMaterial(3, "Sparkle");
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = mat;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;

            return ps;
        }

        private ParticleSystem CreateGroundRingEmitter()
        {
            var go = new GameObject("Heal Ground Ring");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.03f;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.55f;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 3;
            main.startLifetime = 0.46f;
            main.startSpeed = 0f;
            main.startSize = _radius * 2.5f;
            main.startColor = new Color(0.50f, 0.98f, 0.40f, 0.90f);

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0.0f, 1),
                new ParticleSystem.Burst(0.12f, 1)
            });

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(0.88f, 1.0f, 0.70f), 0.0f),
                    new GradientColorKey(new Color(0.48f, 0.96f, 0.35f), 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(0.0f, 0.0f),
                    new GradientAlphaKey(0.98f, 0.12f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            col.color = grad;

            var sizeMod = ps.sizeOverLifetime;
            sizeMod.enabled = true;
            sizeMod.size = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(
                new Keyframe(0.0f, 0.35f),
                new Keyframe(1.0f, 1.45f)
            ));

            var mat = CreateMaterial(4, "GroundRing");
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = mat;
            rend.renderMode = ParticleSystemRenderMode.Billboard;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;

            return ps;
        }

        private Material CreateMaterial(int shapeIndex, string debugName)
        {
            var mat = new Material(HealShader)
            {
                name = "AVE Heal Mat " + debugName,
                hideFlags = HideFlags.DontSave
            };
            mat.SetFloat("_Shape", shapeIndex);
            _createdMaterials.Add(mat);
            return mat;
        }

        private void OnDestroy()
        {
            Stop();
            for (var i = 0; i < _createdMaterials.Count; i++)
            {
                if (_createdMaterials[i] != null)
                {
                    if (Application.isPlaying) Destroy(_createdMaterials[i]);
                    else DestroyImmediate(_createdMaterials[i]);
                }
            }
            _createdMaterials.Clear();
        }
    }
}
