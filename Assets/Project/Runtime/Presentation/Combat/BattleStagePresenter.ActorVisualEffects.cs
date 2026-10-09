using System.Collections;
using System.Collections.Generic;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using UnityEngine;
using UnityEngine.Rendering;

namespace FightingAllstar.Presentation.Combat
{
    public sealed partial class BattleStagePresenter
    {
        private Material _shieldSurfaceMaterial;
        private Material _shieldNodeMaterial;
        private readonly Dictionary<string, ActorVisualEffect> _actorEffects = new Dictionary<string, ActorVisualEffect>();

        private ActorVisualEffect ActorEffects(string id)
        {
            if (!TryView(id, out var view)) return null;
            if (_actorEffects.TryGetValue(id, out var existing) && existing != null && existing.transform == view) return existing;
            var effect = view.GetComponent<ActorVisualEffect>();
            if (effect == null) effect = view.gameObject.AddComponent<ActorVisualEffect>();
            _actorEffects[id] = effect;
            return effect;
        }

        /// <summary>Single state synchronization entry point for persistent actor status visuals.</summary>
        public void SyncActorVisualEffects(FighterState fighter)
        {
            if (fighter == null) return;
            var visible = fighter.IsAlive && !fighter.IsReserve;
            var stance = visible && fighter.Statuses?.Instances.Exists(s =>
                s?.Recipe != null && (s.Recipe.Behavior & StatusBehavior.Stance) != 0) == true;
            SetStance(fighter.Id, stance);
            SetShieldAura(fighter.Id, visible && fighter.Shield > 0);
            var effect = ActorEffects(fighter.Id);
            if (effect == null) return;
            if (!visible) { effect.ClearAll(); return; }
            effect.Synchronize(ActorVisualEffectRules.IsStunned(fighter), _suppressedPersistentEffects.Contains(fighter.Id));
            effect.SynchronizeDebuffs(ActorVisualEffectRules.PersistentDebuffs(fighter));
        }

        /// <returns>How long playback should allow a supported grant effect to settle.</returns>
        public float ActorStatusFeedback(BattleEvent item)
        {
            if (item == null || string.IsNullOrEmpty(item.TargetId)) return 0;
            var applied = item.StatusesAfter?.Find(s => s.InstanceId == item.StatusInstanceId) ??
                item.StatusesAfter?.Find(s => s.RecipeId == item.StatusRecipeId);
            StatusFeedback(item.TargetId, item.Kind == BattleEventKind.StatusRemoved,
                applied?.Recipe?.Polarity == StatusPolarity.Debuff);
            if (item.Kind != BattleEventKind.StatusApplied || applied == null ||
                item.StatusOutcome == StatusApplyOutcome.Rejected || item.StatusOutcome == StatusApplyOutcome.IgnoredWeaker) return 0;
            var effect = ActorEffects(item.TargetId);
            if (effect == null || !effect.isActiveAndEnabled) return 0;
            var kinds = ActorVisualEffectRules.GrantedEffects(applied, item.StatusesAfter);
            foreach (var kind in kinds) effect.Play(kind);
            var debuff = ActorVisualEffectRules.PersistentDebuff(applied.Recipe);
            if (debuff != ActorVisualEffectKind.None) effect.EnterDebuff(debuff);
            if (item.ShieldChanged && item.ShieldAfter > 0) ShieldActivated(item.TargetId);
            if (kinds.Count > 0 || debuff != ActorVisualEffectKind.None) PlayCue(applied.Recipe.Polarity == StatusPolarity.Debuff ? 1 : 3);
            return Mathf.Max(kinds.Count > 0 ? effect.GrantDuration : 0,
                debuff != ActorVisualEffectKind.None ? effect.DebuffEnterDuration : 0);
        }

        public IEnumerator PresentStunnedTurn(BattleState state, TeamSide side)
        { return PresentDisabledTurn(state, side); }

        public IEnumerator PresentDisabledTurn(BattleState state, TeamSide side)
        {
            if (state == null) yield break;
            var duration = 0f;
            foreach (var fighter in state.Team(side).LivingActive())
            {
                SyncActorVisualEffects(fighter);
                if (!ActorVisualEffectRules.IsUnableToAct(fighter)) continue;
                var effect = ActorEffects(fighter.Id);
                effect.Flinch(); duration = Mathf.Max(duration, effect.FlinchDuration);
            }
            if (duration > 0) yield return new WaitForSecondsRealtime(duration);
        }

        public void FlinchIfStunned(string id)
        { FlinchIfDisabled(id); }

        public void FlinchIfDisabled(string id)
        {
            if (_actorEffects.TryGetValue(id, out var effect) && effect != null) effect.Flinch();
        }

        private void ClearActorVisualEffects(bool transientOnly = false)
        {
            foreach (var effect in _actorEffects.Values)
                if (effect != null) { if (transientOnly) effect.ClearTransient(); else effect.ClearAll(); }
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
            if (_actorEffects.TryGetValue(id, out var effect) && effect != null) effect.SetHidden(hidden);
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
                if (collider != null) ActorVisualEffectGeometry.Release(collider);
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
                if (filter.sharedMesh != null && filter.sharedMesh.name == "Procedural Front Shield") ActorVisualEffectGeometry.Release(filter.sharedMesh);
            ActorVisualEffectGeometry.Release(shield);
        }

        private static bool IsShieldRenderer(Renderer renderer)
        {
            for (var current = renderer.transform; current != null; current = current.parent)
                if (current.name.StartsWith("Shield ") || current.name == "Active Shield Aura") return true;
            return false;
        }

            }
}
