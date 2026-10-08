using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FightingAllstar.Presentation.Combat
{
    public sealed partial class BattleStagePresenter
    {
        // Starting values; see Tests/CombatPresentation/ReferencePresentation.md for tuning checks.
        private int _executionRank = 1;
        private readonly Dictionary<string, GameObject> _readyAuras = new Dictionary<string, GameObject>();
        private readonly HashSet<string> _suppressedPersistentEffects = new HashSet<string>();
        private readonly List<GameObject> _bursts = new List<GameObject>();
        private Material _energyMaterial;
        private Material _shieldSurfaceMaterial;
        private Material _shieldNodeMaterial;
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
                if (existing != null) Destroy(existing);
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
                if (existing != null) Destroy(existing);
                _stanceAuras.Remove(id);
            }
            if (!active || !TryView(id, out var view)) return;
            var aura = CreateEnergy(FighterGroundPosition(view), new Color(.2f, .8f, 1f), true, FighterRadius(view) + .1f);
            aura.name = "Stance Aura";
            aura.transform.SetParent(view, true);
            aura.SetActive(!_suppressedPersistentEffects.Contains(id));
            _stanceAuras[id] = aura;
        }

        private readonly Dictionary<string, GameObject> _shieldAuras = new Dictionary<string, GameObject>();

        public void SetShieldAura(string id, bool active)
        {
            if (_shieldAuras.TryGetValue(id, out var existing))
            {
                if (active && existing != null)
                {
                    existing.SetActive(!_suppressedPersistentEffects.Contains(id));
                    return;
                }
                if (existing != null) DestroyShieldVisual(existing);
                _shieldAuras.Remove(id);
            }
            if (!active || !TryView(id, out var view)) return;

            var aura = CreateFrontShield(view, new Color(.12f, .78f, 1f), "Active Shield Aura");
            aura.SetActive(!_suppressedPersistentEffects.Contains(id));
            _shieldAuras[id] = aura;
        }

        public void SetPersistentEffectsHidden(string id, bool hidden)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (hidden) _suppressedPersistentEffects.Add(id);
            else _suppressedPersistentEffects.Remove(id);
            SetAuraActive(_readyAuras, id, !hidden);
            SetAuraActive(_stanceAuras, id, !hidden);
            SetAuraActive(_shieldAuras, id, !hidden);
        }

        private static void SetAuraActive(Dictionary<string, GameObject> auras, string id, bool active)
        {
            if (auras.TryGetValue(id, out var aura) && aura != null) aura.SetActive(active);
        }

        public void ShieldActivated(string id)
        {
            if (!TryView(id, out var view)) return;
            TriggerIfPresent(view, "ShieldActive");
            StartCoroutine(AnimateShieldImpact(id, false, true));
        }

        public void ShieldImpact(string id, bool broken)
        {
            StartCoroutine(AnimateShieldImpact(id, broken, false));
        }

        private IEnumerator AnimateShieldImpact(string id, bool broken, bool activation)
        {
            if (!TryView(id, out var view)) yield break;
            TriggerIfPresent(view, broken ? "ShieldBreak" : "ShieldHit");
            var color = broken ? new Color(.75f, .98f, 1f, .95f) : new Color(.2f, .88f, 1f, .85f);
            var burstName = broken ? "Shield Break Burst" : activation ? "Shield Activation Burst" : "Shield Absorb Burst";
            var burst = CreateFrontShield(view, color, burstName);
            _bursts.Add(burst);
            if (broken) PlayCue(2);

            var duration = broken ? .46f : .3f;
            var baseScale = burst.transform.localScale;
            yield return Tween(duration, t =>
            {
                if (burst == null) return;
                var scale = activation ? Mathf.Lerp(.75f, 1.08f, t) : Mathf.Lerp(1f, broken ? 1.65f : 1.18f, t);
                burst.transform.localScale = baseScale * scale;
                var alpha = 1f - t * t;
                SetShieldVisualOpacity(burst, color, alpha);
            }, false);
            if (burst != null) DestroyShieldVisual(burst);
            _bursts.Remove(burst);
        }

        private GameObject CreateFrontShield(Transform view, Color color, string visualName)
        {
            EnsureShieldMaterials();
            var radius = FighterRadius(view);
            var fighterHeight = FighterHeight(view);
            var shieldDiameter = Mathf.Clamp(Mathf.Max(fighterHeight * 1.08f, radius * 2.4f), 2.15f, 4.4f);
            var shieldHeight = shieldDiameter;
            var shieldWidth = shieldDiameter;
            var facing = Facing(view);
            var centerY = TryGetFighterBounds(view, out var fighterBounds)
                ? fighterBounds.center.y
                : view.position.y;

            var root = new GameObject(visualName);
            root.transform.SetPositionAndRotation(
                new Vector3(view.position.x, centerY, view.position.z) + facing * (radius + .3f),
                Quaternion.LookRotation(facing, Vector3.up));
            root.transform.SetParent(view, true);

            AddShieldSurface(root.transform, shieldWidth, shieldHeight, color);
            AddShieldRim(root.transform, shieldWidth, shieldHeight, color, false);
            AddShieldRim(root.transform, shieldWidth * .91f, shieldHeight * .91f, color, true);
            AddShieldHexCells(root.transform, shieldWidth, shieldHeight, color);
            AddShieldNodes(root.transform, shieldWidth, shieldHeight, color);
            SetShieldVisualOpacity(root, color, 1f);
            return root;
        }

        private void AddShieldSurface(Transform parent, float width, float height, Color color)
        {
            const int columns = 10;
            const int rows = 12;
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var triangles = new int[columns * rows * 12];
            var halfWidth = width * .5f;
            var halfHeight = height * .5f;

            for (var row = 0; row <= rows; row++)
            {
                var y01 = row / (float)rows;
                var normalizedY = y01 * 2f - 1f;
                var rowHalfWidth = halfWidth * Mathf.Sqrt(Mathf.Max(.015f, 1f - normalizedY * normalizedY));
                for (var column = 0; column <= columns; column++)
                {
                    var x01 = column / (float)columns;
                    var x = Mathf.Lerp(-rowHalfWidth, rowHalfWidth, x01);
                    var y = Mathf.Lerp(-halfHeight, halfHeight, y01);
                    vertices[row * (columns + 1) + column] = new Vector3(x, y, ShieldDepth(x, halfWidth));
                }
            }

            var triangle = 0;
            for (var row = 0; row < rows; row++)
            for (var column = 0; column < columns; column++)
            {
                var a = row * (columns + 1) + column;
                var b = a + 1;
                var c = a + columns + 1;
                var d = c + 1;
                triangles[triangle++] = a; triangles[triangle++] = c; triangles[triangle++] = b;
                triangles[triangle++] = b; triangles[triangle++] = c; triangles[triangle++] = d;
                triangles[triangle++] = b; triangles[triangle++] = c; triangles[triangle++] = a;
                triangles[triangle++] = d; triangles[triangle++] = c; triangles[triangle++] = b;
            }

            var mesh = new Mesh { name = "Procedural Front Shield" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            var surface = new GameObject("Shield Surface");
            surface.transform.SetParent(parent, false);
            surface.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = surface.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _shieldSurfaceMaterial;
            SetRendererColor(renderer, new Color(color.r, color.g, color.b, .16f));
        }

        private void AddShieldRim(Transform parent, float width, float height, Color color, bool inner)
        {
            const int segments = 64;
            var lineObject = new GameObject(inner ? "Shield Inner Rim" : "Shield Outer Rim");
            lineObject.transform.SetParent(parent, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = _energyMaterial;
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = segments;
            line.widthMultiplier = inner ? .028f : .075f;
            var halfWidth = width * .5f;
            var halfHeight = height * .5f;
            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                var x = Mathf.Cos(angle) * halfWidth;
                var y = Mathf.Sin(angle) * halfHeight;
                line.SetPosition(i, new Vector3(x, y, ShieldDepth(x, halfWidth) + (inner ? .012f : .02f)));
            }
            var rimColor = color;
            rimColor.a = inner ? .45f : .95f;
            line.startColor = line.endColor = rimColor;
        }

        private void AddShieldHexCells(Transform parent, float width, float height, Color color)
        {
            var halfWidth = width * .5f;
            var halfHeight = height * .5f;
            var hexRadius = Mathf.Clamp(width * .105f, .13f, .25f);
            var horizontalSpacing = Mathf.Sqrt(3f) * hexRadius;
            var verticalSpacing = 1.5f * hexRadius;
            var rowCount = Mathf.CeilToInt(height / verticalSpacing);
            var columnCount = Mathf.CeilToInt(width / horizontalSpacing);

            for (var row = -rowCount; row <= rowCount; row++)
            for (var column = -columnCount; column <= columnCount; column++)
            {
                var centerY = row * verticalSpacing;
                var centerX = (column + ((row & 1) == 0 ? 0f : .5f)) * horizontalSpacing;
                var normalized = centerX * centerX / (halfWidth * halfWidth * .72f) +
                                 centerY * centerY / (halfHeight * halfHeight * .72f);
                if (normalized > 1f) continue;

                var cellObject = new GameObject("Shield Hex Cell");
                cellObject.transform.SetParent(parent, false);
                var cell = cellObject.AddComponent<LineRenderer>();
                cell.sharedMaterial = _energyMaterial;
                cell.useWorldSpace = false;
                cell.loop = true;
                cell.positionCount = 6;
                cell.widthMultiplier = .018f;
                var cellColor = color;
                cellColor.a = .26f;
                cell.startColor = cell.endColor = cellColor;
                for (var corner = 0; corner < 6; corner++)
                {
                    var angle = (60f * corner + 30f) * Mathf.Deg2Rad;
                    var x = centerX + Mathf.Cos(angle) * hexRadius;
                    var y = centerY + Mathf.Sin(angle) * hexRadius;
                    cell.SetPosition(corner, new Vector3(x, y, ShieldDepth(x, halfWidth) + .016f));
                }
            }
        }

        private void AddShieldNodes(Transform parent, float width, float height, Color color)
        {
            var halfWidth = width * .5f;
            var halfHeight = height * .5f;
            var x = -halfWidth * .58f;
            for (var i = -1; i <= 1; i++)
            {
                var y = i * halfHeight * .43f;
                var node = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                node.name = "Shield Energy Node";
                node.transform.SetParent(parent, false);
                node.transform.localPosition = new Vector3(x, y, ShieldDepth(x, halfWidth) + .045f);
                node.transform.localScale = Vector3.one * Mathf.Clamp(width * .095f, .11f, .2f);
                var collider = node.GetComponent<Collider>();
                if (collider != null) Destroy(collider);
                var renderer = node.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = _shieldNodeMaterial;
                SetRendererColor(renderer, new Color(.55f, .96f, 1f, 1f));

                var ringObject = new GameObject("Shield Node Ring");
                ringObject.transform.SetParent(parent, false);
                var ring = ringObject.AddComponent<LineRenderer>();
                ring.sharedMaterial = _energyMaterial;
                ring.useWorldSpace = false;
                ring.loop = true;
                ring.positionCount = 24;
                ring.widthMultiplier = .025f;
                var ringRadius = Mathf.Clamp(width * .09f, .11f, .19f);
                for (var segment = 0; segment < ring.positionCount; segment++)
                {
                    var angle = segment * Mathf.PI * 2f / ring.positionCount;
                    ring.SetPosition(segment, new Vector3(
                        x + Mathf.Cos(angle) * ringRadius,
                        y + Mathf.Sin(angle) * ringRadius,
                        ShieldDepth(x, halfWidth) + .052f));
                }
                var ringColor = color;
                ringColor.a = .85f;
                ring.startColor = ring.endColor = ringColor;
            }
        }

        private static float ShieldDepth(float x, float halfWidth)
        {
            var normalizedX = Mathf.Clamp(x / Mathf.Max(.01f, halfWidth), -1f, 1f);
            return .13f * (1f - normalizedX * normalizedX);
        }

        private void SetShieldVisualOpacity(GameObject shield, Color color, float opacity)
        {
            foreach (var line in shield.GetComponentsInChildren<LineRenderer>())
            {
                var factor = line.name.Contains("Hex") ? .26f : line.name.Contains("Inner") ? .45f : .95f;
                var lineColor = color;
                lineColor.a = opacity * factor;
                line.startColor = line.endColor = lineColor;
            }

            foreach (var renderer in shield.GetComponentsInChildren<MeshRenderer>())
            {
                var alpha = renderer.name.Contains("Surface") ? opacity * .16f : opacity;
                SetRendererColor(renderer, new Color(color.r, color.g, color.b, alpha));
                renderer.enabled = opacity > .015f;
            }
        }

        private static void SetRendererColor(Renderer renderer, Color color)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_Color", color);
            block.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(block);
        }

        private void EnsureShieldMaterials()
        {
            EnsureEnergyMaterial();
            if (_shieldSurfaceMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader != null)
                {
                    _shieldSurfaceMaterial = new Material(shader) { name = "Runtime Shield Surface" };
                    ConfigureTransparentMaterial(_shieldSurfaceMaterial);
                }
            }

            if (_shieldNodeMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    _shieldNodeMaterial = new Material(shader) { name = "Runtime Shield Nodes" };
                    ConfigureTransparentMaterial(_shieldNodeMaterial);
                    if (_shieldNodeMaterial.HasProperty("_BaseColor"))
                        _shieldNodeMaterial.SetColor("_BaseColor", new Color(.45f, .95f, 1f));
                    if (_shieldNodeMaterial.HasProperty("_Color"))
                        _shieldNodeMaterial.SetColor("_Color", new Color(.45f, .95f, 1f));
                }
            }
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private void DestroyShieldVisual(GameObject shield)
        {
            if (shield == null) return;
            foreach (var filter in shield.GetComponentsInChildren<MeshFilter>())
                if (filter.sharedMesh != null && filter.sharedMesh.name == "Procedural Front Shield") Destroy(filter.sharedMesh);
            Destroy(shield);
        }

        private static bool IsShieldRenderer(Renderer renderer)
        {
            for (var current = renderer.transform; current != null; current = current.parent)
                if (current.name.StartsWith("Shield ") || current.name == "Active Shield Aura") return true;
            return false;
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
                if (!renderer.enabled || renderer is ParticleSystemRenderer || renderer is LineRenderer || IsShieldRenderer(renderer)) continue;
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
            foreach (var aura in _readyAuras.Values) if (aura != null) Destroy(aura);
            _readyAuras.Clear();
            foreach (var aura in _stanceAuras.Values) if (aura != null) Destroy(aura);
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
            if (_energyMaterial != null) Destroy(_energyMaterial);
            if (_shieldSurfaceMaterial != null) Destroy(_shieldSurfaceMaterial);
            if (_shieldNodeMaterial != null) Destroy(_shieldNodeMaterial);
            foreach (var clip in _cues.Values) if (clip != null) Destroy(clip);
        }
    }
}
