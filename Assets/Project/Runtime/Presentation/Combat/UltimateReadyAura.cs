using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>
    /// High-impact 7DS:GC style Ultimate Ready Aura.
    /// Features radiant ground magic pool, multi-layered roaring flame geysers,
    /// asymmetric sweeping flame crests with prismatic lime-gold accents,
    /// high-velocity ascending energy needles, and swirling radiant embers.
    /// Uses view-direction Fresnel falloff to keep the character silhouette clearly
    /// readable while maximizing edge flame intensity and impact.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UltimateReadyAura : MonoBehaviour
    {
        private static Shader _auraShader;
        private readonly List<Mesh> _createdMeshes = new List<Mesh>();
        private readonly List<Material> _createdMaterials = new List<Material>();

        private Transform _groundDiscTransform;
        private Transform _backdropArcTransform;
        private Transform _skirtTransform;
        private Transform _rightWingTransform;
        private Transform _rightInnerWingTransform;
        private Transform _leftWingTransform;
        private Transform _leftInnerWingTransform;
        private Transform _shockwaveTransform;
        private Material _shockwaveMaterial;

        private float _spawnTime;
        private float _baseRadius = 0.75f;
        private float _baseHeight = 2.2f;

        private static Shader AuraShader
        {
            get
            {
                if (_auraShader == null)
                    _auraShader = Resources.Load<Shader>("UltimateReadyAura") ?? Shader.Find("Fighting Allstar/Ultimate Ready Aura");
                if (_auraShader == null)
                    _auraShader = Shader.Find("Sprites/Default");
                return _auraShader;
            }
        }

        public static GameObject Create(Transform parent, Vector3 groundPosition, float radius, float height)
        {
            var root = new GameObject("AVE Ultimate Ready Aura");
            root.transform.position = groundPosition;
            if (parent != null)
                root.transform.SetParent(parent, true);

            var comp = root.AddComponent<UltimateReadyAura>();
            comp.Initialize(radius, height);
            return root;
        }

        public void Initialize(float radius, float height)
        {
            _baseRadius = Mathf.Max(0.45f, radius);
            _baseHeight = Mathf.Clamp(height, 1.8f, 3.8f);
            _spawnTime = Time.unscaledTime;

            // 1. Ground Energy Disc / Light Pool
            var discMesh = CreateDiscMesh(_baseRadius * 1.80f, 32);
            _createdMeshes.Add(discMesh);
            var discMat = CreateMaterial(0, new Color(1.0f, 0.82f, 0.12f), new Color(1.4f, 1.35f, 0.95f), new Color(0.88f, 1.0f, 0.22f), new Color(1.0f, 0.48f, 0.05f), 2.2f);
            var discGo = CreateMeshObject("AVE Ground Pool", discMesh, discMat, transform, Vector3.up * 0.02f, Quaternion.identity);
            _groundDiscTransform = discGo.transform;

            // 2. Entrance Shockwave Ring (expands during first 0.3s)
            var shockMesh = CreateDiscMesh(_baseRadius * 2.2f, 32);
            _createdMeshes.Add(shockMesh);
            _shockwaveMaterial = CreateMaterial(0, new Color(1.0f, 0.85f, 0.15f), new Color(1.4f, 1.4f, 1.0f), new Color(0.9f, 1.0f, 0.3f), new Color(1.0f, 0.5f, 0.05f), 4.0f);
            var shockGo = CreateMeshObject("AVE Shockwave Pulse", shockMesh, _shockwaveMaterial, transform, Vector3.up * 0.025f, Quaternion.identity);
            _shockwaveTransform = shockGo.transform;

            // 3. Backdrop Flame Arc (Behind Character: z > 0, frames fighter without occluding front)
            var backdropMesh = CreateArcMesh(_baseRadius * 1.05f, _baseRadius * 1.48f, _baseHeight * 0.92f, 0.15f * Mathf.PI, 0.85f * Mathf.PI, 18, 6);
            _createdMeshes.Add(backdropMesh);
            var backdropMat = CreateMaterial(1, new Color(1.0f, 0.78f, 0.08f), new Color(1.35f, 1.3f, 0.92f), new Color(0.85f, 1.05f, 0.22f), new Color(1.0f, 0.42f, 0.04f), 2.5f);
            var backdropGo = CreateMeshObject("AVE Backdrop Flame Arc", backdropMesh, backdropMat, transform, new Vector3(0f, 0f, _baseRadius * 0.12f), Quaternion.identity);
            _backdropArcTransform = backdropGo.transform;

            // 4. Low Base Flame Skirt (Around feet/ankles: z=0..0.28 height, grounds aura without covering body)
            var skirtMesh = CreateCylinderMesh(_baseRadius * 0.90f, _baseRadius * 1.25f, _baseHeight * 0.28f, 20, 3);
            _createdMeshes.Add(skirtMesh);
            var skirtMat = CreateMaterial(1, new Color(1.0f, 0.82f, 0.10f), new Color(1.4f, 1.35f, 0.95f), new Color(0.9f, 1.0f, 0.25f), new Color(1.0f, 0.45f, 0.05f), 2.8f);
            skirtMat.SetFloat("_Turbulence", 1.2f);
            var skirtGo = CreateMeshObject("AVE Low Base Skirt", skirtMesh, skirtMat, transform, Vector3.zero, Quaternion.identity);
            _skirtTransform = skirtGo.transform;

            // 5. Sweeping Flame Crest - Right (Golden-Amber Wings flaring outwards to the right)
            var rightWingMesh = CreateRibbonMesh(_baseRadius * 1.35f, _baseHeight * 0.96f, _baseRadius * 0.55f, _baseRadius * 0.35f, new Color(1.0f, 0.15f, 0.0f, 1.0f), 12);
            _createdMeshes.Add(rightWingMesh);
            var rightWingMat = CreateMaterial(2, new Color(1.0f, 0.76f, 0.08f), new Color(1.45f, 1.4f, 0.98f), new Color(1.0f, 0.88f, 0.15f), new Color(1.0f, 0.35f, 0.02f), 2.6f);
            var rightWingGo = CreateMeshObject("AVE Flame Wing Right Outer", rightWingMesh, rightWingMat, transform,
                new Vector3(_baseRadius * 0.62f, 0f, -_baseRadius * 0.06f), Quaternion.Euler(-6f, -18f, -22f));
            _rightWingTransform = rightWingGo.transform;

            var rightInnerMesh = CreateRibbonMesh(_baseRadius * 1.05f, _baseHeight * 0.76f, _baseRadius * 0.38f, _baseRadius * 0.22f, new Color(1.0f, 0.25f, 0.0f, 1.0f), 10);
            _createdMeshes.Add(rightInnerMesh);
            var rightInnerGo = CreateMeshObject("AVE Flame Wing Right Inner", rightInnerMesh, rightWingMat, transform,
                new Vector3(_baseRadius * 0.48f, 0f, _baseRadius * 0.08f), Quaternion.Euler(-4f, -10f, -15f));
            _rightInnerWingTransform = rightInnerGo.transform;

            // 6. Sweeping Flame Crest - Left (Iridescent Lime-Gold Accent Wings flaring outwards to the left)
            var leftWingMesh = CreateRibbonMesh(_baseRadius * 1.30f, _baseHeight * 0.92f, -_baseRadius * 0.52f, -_baseRadius * 0.32f, new Color(0.1f, 1.0f, 0.1f, 1.0f), 12);
            _createdMeshes.Add(leftWingMesh);
            var leftWingMat = CreateMaterial(2, new Color(0.85f, 1.08f, 0.18f), new Color(1.35f, 1.45f, 0.95f), new Color(0.48f, 1.40f, 0.22f), new Color(1.0f, 0.82f, 0.10f), 2.8f);
            var leftWingGo = CreateMeshObject("AVE Flame Wing Left Outer", leftWingMesh, leftWingMat, transform,
                new Vector3(-_baseRadius * 0.62f, 0f, -_baseRadius * 0.06f), Quaternion.Euler(-6f, 18f, 22f));
            _leftWingTransform = leftWingGo.transform;

            var leftInnerMesh = CreateRibbonMesh(_baseRadius * 1.0f, _baseHeight * 0.72f, -_baseRadius * 0.35f, -_baseRadius * 0.20f, new Color(0.2f, 1.0f, 0.2f, 1.0f), 10);
            _createdMeshes.Add(leftInnerMesh);
            var leftInnerGo = CreateMeshObject("AVE Flame Wing Left Inner", leftInnerMesh, leftWingMat, transform,
                new Vector3(-_baseRadius * 0.48f, 0f, _baseRadius * 0.08f), Quaternion.Euler(-4f, 10f, 15f));
            _leftInnerWingTransform = leftInnerGo.transform;

            // 7. Ascending Stretched Energy Streaks (Ki Needles)
            CreateAscendingStreaksEmitter();

            // 8. Swirling Radiant Embers
            CreateEmbersEmitter();
        }

        private void CreateAscendingStreaksEmitter()
        {
            var go = new GameObject("AVE Ascending Streaks");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.06f;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 1.0f;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 65;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.2f, 5.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.075f);
            main.startColor = new Color(1f, 0.96f, 0.75f, 1f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = _baseRadius * 0.90f;
            shape.rotation = new Vector3(-90, 0, 0);

            var emission = ps.emission;
            emission.rateOverTime = 38;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)14) });

            var velocity = ps.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            velocity.y = new ParticleSystem.MinMaxCurve(1.6f, 2.8f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.enabled = true;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1f, 1f, 0.88f), 0f),
                    new GradientColorKey(new Color(1f, 0.78f, 0.08f), 0.5f),
                    new GradientColorKey(new Color(1f, 0.42f, 0.05f), 1f)
                },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.92f, 0.15f),
                    new GradientAlphaKey(0.75f, 0.65f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = grad;

            var streakMat = CreateMaterial(3, new Color(1.0f, 0.78f, 0.08f), new Color(1.3f, 1.25f, 0.9f), new Color(0.85f, 1.0f, 0.22f), new Color(1.0f, 0.45f, 0.05f), 2.0f);
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = streakMat;
            rend.renderMode = ParticleSystemRenderMode.Stretch;
            rend.lengthScale = 4.2f;
            rend.cameraVelocityScale = 0f;

            ps.Play();
        }

        private void CreateEmbersEmitter()
        {
            var go = new GameObject("AVE Swirling Embers");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * 0.1f;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 1.2f;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 40;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.0f, 2.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.startColor = new Color(1f, 0.98f, 0.6f, 1f);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = _baseRadius * 0.85f;
            shape.rotation = new Vector3(-90, 0, 0);

            var emission = ps.emission;
            emission.rateOverTime = 22;

            var velocity = ps.velocityOverLifetime;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            velocity.y = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.25f, 0.25f);
            velocity.space = ParticleSystemSimulationSpace.World;
            velocity.enabled = true;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1f, 1f, 0.8f), 0f),
                    new GradientColorKey(new Color(1f, 0.82f, 0.15f), 0.6f),
                    new GradientColorKey(new Color(0.85f, 1f, 0.22f), 1f)
                },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.85f, 0.25f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = grad;

            var emberMat = CreateMaterial(3, new Color(1.0f, 0.85f, 0.15f), new Color(1.4f, 1.35f, 0.95f), new Color(0.85f, 1.0f, 0.22f), new Color(1.0f, 0.5f, 0.05f), 2.0f);
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = emberMat;
            rend.renderMode = ParticleSystemRenderMode.Billboard;

            ps.Play();
        }

        private void Update()
        {
            var now = Time.unscaledTime;
            Evaluate(now - _spawnTime);
        }

        public void Evaluate(float age)
        {
            // Update custom animation time on all materials
            for (var i = 0; i < _createdMaterials.Count; i++)
            {
                if (_createdMaterials[i] != null)
                    _createdMaterials[i].SetFloat("_CustomTime", age);
            }

            // 1. Slow, energetic ground rotation
            if (_groundDiscTransform != null)
                _groundDiscTransform.localRotation = Quaternion.Euler(0, age * 32f, 0);

            // 2. Breathing scale oscillation for backdrop arc & base skirt
            var breathe = 1.0f + 0.035f * Mathf.Sin(age * 4.0f);
            if (_backdropArcTransform != null)
                _backdropArcTransform.localScale = new Vector3(breathe, 1.0f + 0.02f * Mathf.Sin(age * 5.5f), breathe);
            if (_skirtTransform != null)
                _skirtTransform.localScale = new Vector3(1.0f + 0.03f * Mathf.Cos(age * 4.8f), 1.0f, 1.0f + 0.03f * Mathf.Cos(age * 4.8f));

            // 3. Wing gentle swaying with asymmetric dynamic rhythm
            if (_rightWingTransform != null)
                _rightWingTransform.localRotation = Quaternion.Euler(-6f, -18f + Mathf.Sin(age * 4.5f) * 4f, -24f + Mathf.Cos(age * 3.2f) * 2.5f);
            if (_rightInnerWingTransform != null)
                _rightInnerWingTransform.localRotation = Quaternion.Euler(-4f, -10f + Mathf.Sin(age * 5.2f) * 3f, -16f + Mathf.Cos(age * 3.8f) * 2f);

            if (_leftWingTransform != null)
                _leftWingTransform.localRotation = Quaternion.Euler(-6f, 18f + Mathf.Cos(age * 4.5f) * 4f, 24f - Mathf.Sin(age * 3.2f) * 2.5f);
            if (_leftInnerWingTransform != null)
                _leftInnerWingTransform.localRotation = Quaternion.Euler(-4f, 10f + Mathf.Cos(age * 5.2f) * 3f, 16f - Mathf.Sin(age * 3.8f) * 2f);

            // 4. Initial entrance shockwave burst (0.0s to 0.32s)
            if (_shockwaveTransform != null && _shockwaveMaterial != null)
            {
                if (age < 0.32f)
                {
                    var progress = age / 0.32f;
                    var shockScale = Mathf.Lerp(0.2f, 1.6f, Mathf.Sqrt(progress));
                    _shockwaveTransform.localScale = new Vector3(shockScale, 1f, shockScale);
                    var alpha = (1.0f - progress) * 1.4f;
                    _shockwaveMaterial.SetColor("_TintColor", new Color(1.0f, 0.85f, 0.15f, Mathf.Clamp01(alpha)));
                    _shockwaveTransform.gameObject.SetActive(true);
                }
                else
                {
                    _shockwaveTransform.gameObject.SetActive(false);
                }
            }

            // 5. In edit/preview mode, simulate particle systems so captures render streaks and embers
            if (!Application.isPlaying)
            {
                var systems = GetComponentsInChildren<ParticleSystem>();
                for (var i = 0; i < systems.Length; i++)
                {
                    systems[i].Simulate(age, true, true, false);
                }
            }
        }

        private GameObject CreateMeshObject(string name, Mesh mesh, Material mat, Transform parent, Vector3 localPos, Quaternion localRot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            var rend = go.AddComponent<MeshRenderer>();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = ShadowCastingMode.Off;
            rend.receiveShadows = false;
            rend.lightProbeUsage = LightProbeUsage.Off;
            rend.reflectionProbeUsage = ReflectionProbeUsage.Off;

            return go;
        }

        private Material CreateMaterial(int mode, Color tint, Color core, Color rim, Color accent, float scrollSpeed)
        {
            var mat = new Material(AuraShader)
            {
                name = "AVE Aura Mat Mode" + mode,
                hideFlags = HideFlags.DontSave
            };
            mat.SetFloat("_Mode", mode);
            mat.SetColor("_TintColor", tint);
            mat.SetColor("_CoreColor", core);
            mat.SetColor("_RimColor", rim);
            mat.SetColor("_AccentColor", accent);
            mat.SetFloat("_ScrollSpeed", scrollSpeed);
            mat.SetFloat("_Turbulence", 1.0f);
            _createdMaterials.Add(mat);
            return mat;
        }

        private static Mesh CreateDiscMesh(float radius, int segments = 32)
        {
            var vertices = new Vector3[segments + 1];
            var normals = new Vector3[segments + 1];
            var uvs = new Vector2[segments + 1];
            var colors = new Color[segments + 1];
            var triangles = new int[segments * 3];

            vertices[0] = Vector3.zero;
            normals[0] = Vector3.up;
            uvs[0] = new Vector2(0.5f, 0.5f);
            colors[0] = Color.white;

            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                var cos = Mathf.Cos(angle);
                var sin = Mathf.Sin(angle);
                vertices[i + 1] = new Vector3(cos * radius, 0f, sin * radius);
                normals[i + 1] = Vector3.up;
                uvs[i + 1] = new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f);
                colors[i + 1] = Color.white;

                var next = (i + 1) % segments;
                triangles[i * 3 + 0] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = next + 1;
            }

            var mesh = new Mesh { name = "AVE Aura Disc", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateArcMesh(float baseRadius, float topRadius, float height, float startAngleRad, float endAngleRad, int radialSegments = 18, int heightSegments = 6)
        {
            var vertCount = (radialSegments + 1) * (heightSegments + 1);
            var vertices = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var colors = new Color[vertCount];
            var triangles = new int[radialSegments * heightSegments * 6];

            var triIndex = 0;
            for (var y = 0; y <= heightSegments; y++)
            {
                var v = (float)y / heightSegments;
                var ringY = v * height;
                var r = Mathf.Lerp(baseRadius, topRadius, Mathf.Pow(v, 0.85f));

                for (var x = 0; x <= radialSegments; x++)
                {
                    var u = (float)x / radialSegments;
                    var angle = Mathf.Lerp(startAngleRad, endAngleRad, u);
                    var cos = Mathf.Cos(angle);
                    var sin = Mathf.Sin(angle);
                    var vertIdx = y * (radialSegments + 1) + x;

                    vertices[vertIdx] = new Vector3(cos * r, ringY, sin * r);
                    normals[vertIdx] = new Vector3(cos, 0.1f, sin).normalized;
                    uvs[vertIdx] = new Vector2(u, v);
                    colors[vertIdx] = Color.white;

                    if (y < heightSegments && x < radialSegments)
                    {
                        var cur = vertIdx;
                        var next = cur + 1;
                        var upCur = cur + (radialSegments + 1);
                        var upNext = upCur + 1;

                        triangles[triIndex++] = cur;
                        triangles[triIndex++] = upCur;
                        triangles[triIndex++] = next;

                        triangles[triIndex++] = next;
                        triangles[triIndex++] = upCur;
                        triangles[triIndex++] = upNext;
                    }
                }
            }

            var mesh = new Mesh { name = "AVE Aura Backdrop Arc", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateCylinderMesh(float baseRadius, float topRadius, float height, int radialSegments = 24, int heightSegments = 6)
        {
            var vertCount = (radialSegments + 1) * (heightSegments + 1);
            var vertices = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var colors = new Color[vertCount];
            var triangles = new int[radialSegments * heightSegments * 6];

            var triIndex = 0;
            for (var y = 0; y <= heightSegments; y++)
            {
                var v = (float)y / heightSegments;
                var ringY = v * height;
                var r = Mathf.Lerp(baseRadius, topRadius, Mathf.Pow(v, 0.82f));

                for (var x = 0; x <= radialSegments; x++)
                {
                    var u = (float)x / radialSegments;
                    var angle = u * Mathf.PI * 2f;
                    var cos = Mathf.Cos(angle);
                    var sin = Mathf.Sin(angle);
                    var vertIdx = y * (radialSegments + 1) + x;

                    vertices[vertIdx] = new Vector3(cos * r, ringY, sin * r);
                    normals[vertIdx] = new Vector3(cos, 0.1f, sin).normalized;
                    uvs[vertIdx] = new Vector2(u, v);
                    colors[vertIdx] = Color.white;

                    if (y < heightSegments && x < radialSegments)
                    {
                        var cur = vertIdx;
                        var next = cur + 1;
                        var upCur = cur + (radialSegments + 1);
                        var upNext = upCur + 1;

                        triangles[triIndex++] = cur;
                        triangles[triIndex++] = upCur;
                        triangles[triIndex++] = next;

                        triangles[triIndex++] = next;
                        triangles[triIndex++] = upCur;
                        triangles[triIndex++] = upNext;
                    }
                }
            }

            var mesh = new Mesh { name = "AVE Aura Column", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateRibbonMesh(float width, float height, float flareOut, float curveX, Color tint, int segments = 12)
        {
            var vertCount = (segments + 1) * 2;
            var vertices = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var colors = new Color[vertCount];
            var triangles = new int[segments * 6];

            var triIdx = 0;
            for (var i = 0; i <= segments; i++)
            {
                var v = (float)i / segments;
                var y = v * height;
                var currentFlare = v * flareOut + Mathf.Sin(v * Mathf.PI * 0.75f) * curveX;
                var w = width * Mathf.Lerp(0.85f, 0.12f, Mathf.Pow(v, 1.35f));
                var halfW = w * 0.5f;

                var idxLeft = i * 2;
                var idxRight = i * 2 + 1;

                vertices[idxLeft] = new Vector3(currentFlare - halfW, y, 0f);
                vertices[idxRight] = new Vector3(currentFlare + halfW, y, 0f);

                normals[idxLeft] = Vector3.forward;
                normals[idxRight] = Vector3.forward;

                uvs[idxLeft] = new Vector2(0f, v);
                uvs[idxRight] = new Vector2(1f, v);

                colors[idxLeft] = tint;
                colors[idxRight] = tint;

                if (i < segments)
                {
                    triangles[triIdx++] = idxLeft;
                    triangles[triIdx++] = idxLeft + 2;
                    triangles[triIdx++] = idxRight;

                    triangles[triIdx++] = idxRight;
                    triangles[triIdx++] = idxLeft + 2;
                    triangles[triIdx++] = idxRight + 2;
                }
            }

            var mesh = new Mesh { name = "AVE Aura Crest", hideFlags = HideFlags.DontSave };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private void OnDestroy()
        {
            foreach (var mesh in _createdMeshes)
                ActorVisualEffectGeometry.Release(mesh);
            _createdMeshes.Clear();

            foreach (var mat in _createdMaterials)
                ActorVisualEffectGeometry.Release(mat);
            _createdMaterials.Clear();
        }
    }
}
