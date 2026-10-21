using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace FightingAllstar.Rendering
{
    /// <summary>
    /// Spatial GPU submissions for explicitly assigned, stationary decorative LOD groups.
    /// Meshes, transforms, material assets and authored LOD transitions remain unchanged.
    /// Unsupported renderers retain their native rendering path. Call Rebuild after moving
    /// sources or changing their meshes, probe lighting, materials or LOD definitions.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class MapLODInstancer : MonoBehaviour
    {
        public const int InstancesPerSubmission = 500;
        public LODGroup[] sources = Array.Empty<LODGroup>();
        public bool playOnly = true;
        public bool instancingEnabled = true;
        [Min(1)] public float cellSize = 32;
        [Min(0)] public float windBoundsPadding = .8f;

        [NonSerialized] public int eligibleGroups;
        [NonSerialized] public int excludedGroups;
        [NonSerialized] public int sourceRenderers;
        [NonSerialized] public int selectedGroups;
        [NonSerialized] public int selectedLOD0;
        [NonSerialized] public int selectedLOD1;
        [NonSerialized] public int selectedLOD2OrHigher;
        [NonSerialized] public int selectedRenderers;
        [NonSerialized] public int submittedBatches;
        [NonSerialized] public long submittedTriangles;
        [NonSerialized] public double prepareMilliseconds;
        [NonSerialized] public double submitMilliseconds;

        struct BucketKey : IEquatable<BucketKey>
        {
            public Mesh mesh;
            public Material material;
            public int submesh, layer, priority;
            public Vector3Int cell;
            public uint renderingLayerMask;
            public ShadowCastingMode shadows;
            public bool receiveShadows;
            public LightProbeUsage probes;
            public ReflectionProbeUsage reflections;
            public MotionVectorGenerationMode motion;

            public bool Equals(BucketKey other)
            {
                return mesh == other.mesh && material == other.material && submesh == other.submesh
                    && layer == other.layer && priority == other.priority && cell == other.cell
                    && renderingLayerMask == other.renderingLayerMask && shadows == other.shadows
                    && receiveShadows == other.receiveShadows && probes == other.probes
                    && reflections == other.reflections && motion == other.motion;
            }

            public override bool Equals(object obj) { return obj is BucketKey other && Equals(other); }
            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = mesh.GetInstanceID();
                    hash = hash * 397 ^ material.GetInstanceID();
                    hash = hash * 397 ^ submesh; hash = hash * 397 ^ layer;
                    hash = hash * 397 ^ priority; hash = hash * 397 ^ cell.GetHashCode();
                    hash = hash * 397 ^ (int)renderingLayerMask; hash = hash * 397 ^ (int)shadows;
                    hash = hash * 397 ^ (receiveShadows ? 1 : 0); hash = hash * 397 ^ (int)probes;
                    hash = hash * 397 ^ (int)reflections; return hash * 397 ^ (int)motion;
                }
            }
        }

        sealed class Chunk
        {
            public readonly Matrix4x4[] matrices;
            public readonly SphericalHarmonicsL2[] probes;
            public readonly Vector4[] occlusion;
            public readonly MaterialPropertyBlock properties;
            public int count;
            public Bounds bounds;

            public Chunk(int capacity, bool copyProbes)
            {
                matrices = new Matrix4x4[capacity];
                if (!copyProbes) return;
                probes = new SphericalHarmonicsL2[capacity];
                occlusion = new Vector4[capacity];
                properties = new MaterialPropertyBlock();
            }
        }

        sealed class Bucket
        {
            public BucketKey key;
            public int capacity, count;
            public Chunk[] chunks;
            public RenderParams parameters;
            public long triangles;

            public void Allocate()
            {
                var count = (capacity + InstancesPerSubmission - 1) / InstancesPerSubmission;
                chunks = new Chunk[count];
                for (var index = 0; index < count; index++)
                    chunks[index] = new Chunk(Mathf.Min(InstancesPerSubmission, capacity - index * InstancesPerSubmission),
                        key.probes == LightProbeUsage.BlendProbes);
                parameters = new RenderParams(key.material)
                {
                    layer = key.layer, renderingLayerMask = key.renderingLayerMask,
                    rendererPriority = key.priority, shadowCastingMode = key.shadows,
                    receiveShadows = key.receiveShadows, reflectionProbeUsage = key.reflections,
                    motionVectorMode = key.motion,
                    lightProbeUsage = key.probes == LightProbeUsage.BlendProbes ? LightProbeUsage.CustomProvided : key.probes,
                };
                triangles = key.mesh.GetTopology(key.submesh) == MeshTopology.Triangles
                    ? key.mesh.GetIndexCount(key.submesh) / 3 : 0;
            }

            public void Add(ref Part part)
            {
                var chunk = chunks[count / InstancesPerSubmission];
                var index = chunk.count++;
                chunk.matrices[index] = part.matrix;
                if (chunk.probes != null)
                {
                    chunk.probes[index] = part.probe;
                    chunk.occlusion[index] = part.occlusion;
                }
                if (index == 0) chunk.bounds = part.bounds;
                else chunk.bounds.Encapsulate(part.bounds);
                count++;
            }
        }

        struct Part
        {
            public Bucket bucket;
            public Matrix4x4 matrix;
            public Bounds bounds;
            public SphericalHarmonicsL2 probe;
            public Vector4 occlusion;
        }

        sealed class Level
        {
            public float height;
            public Part[] parts;
            public int rendererCount;
        }

        sealed class Group
        {
            public LODGroup source;
            public Vector3 referencePoint;
            public float size;
            public Level[] levels;
        }

        struct RendererState
        {
            public Renderer renderer;
            public bool enabled;
        }

        readonly List<Group> groups = new List<Group>();
        readonly List<Bucket> buckets = new List<Bucket>();
        readonly List<RendererState> originalRenderers = new List<RendererState>();
        LODGroup[] cachedSources;
        bool prepared, buildFailed;

        void OnEnable() { RenderPipelineManager.beginCameraRendering += BeginCameraRendering; }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
            Rebuild();
        }
        void OnValidate()
        {
            cellSize = Mathf.Max(1, cellSize);
            windBoundsPadding = Mathf.Max(0, windBoundsPadding);
            Rebuild();
        }
        void LateUpdate()
        {
            // A renderer switch can stop SRP callbacks entirely. Restore the native
            // objects before the next Built-in frame instead of leaving them hidden.
            if (prepared && !CanRun()) Rebuild();
        }

        public void SetInstancingEnabled(bool value)
        {
            if (instancingEnabled == value) return;
            instancingEnabled = value;
            Rebuild();
        }

        /// <summary>Restore native renderers and invalidate all immutable map caches.</summary>
        [ContextMenu("Rebuild stationary LOD cache")]
        public void Rebuild()
        {
            for (var index = 0; index < originalRenderers.Count; index++)
            {
                var state = originalRenderers[index];
                if (state.renderer != null) state.renderer.enabled = state.enabled;
            }
            for (var index = 0; index < groups.Count; index++)
                if (groups[index].source != null) groups[index].source.enabled = true;
            originalRenderers.Clear(); groups.Clear(); buckets.Clear();
            cachedSources = null; prepared = buildFailed = false;
            eligibleGroups = excludedGroups = sourceRenderers = 0;
            ResetFrameCounters();
        }

        bool CanRun()
        {
            return isActiveAndEnabled && instancingEnabled && (!playOnly || Application.isPlaying)
                && SystemInfo.supportsInstancing && GraphicsSettings.currentRenderPipeline != null;
        }

        bool IsCameraForScene(Camera camera)
        {
            return camera != null && (camera.gameObject.scene == gameObject.scene
                || camera.cameraType == CameraType.SceneView && gameObject.scene.IsValid() && gameObject.scene.isLoaded);
        }

        // Eligibility is deliberately conservative. Native paths retain functionality
        // that a static matrix submission cannot faithfully reproduce.
        static bool Supports(Renderer renderer)
        {
            var meshRenderer = renderer as MeshRenderer;
            if (meshRenderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy
                || renderer.forceRenderingOff || renderer.isPartOfStaticBatch || renderer.HasPropertyBlock()
                || meshRenderer.additionalVertexStreams != null
                || renderer.lightProbeUsage != LightProbeUsage.Off && renderer.lightProbeUsage != LightProbeUsage.BlendProbes
                || renderer.lightmapIndex >= 0 && renderer.lightmapIndex < 65534
                || renderer.realtimeLightmapIndex >= 0 && renderer.realtimeLightmapIndex < 65534)
                return false;
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) return false;
            var materials = renderer.sharedMaterials;
            if (materials.Length != filter.sharedMesh.subMeshCount) return false;
            for (var index = 0; index < materials.Length; index++)
                if (materials[index] == null || !materials[index].enableInstancing
                    || materials[index].shader == null || !materials[index].shader.isSupported
                    || materials[index].renderQueue > (int)RenderQueue.GeometryLast) return false;
            return true;
        }

        void Build()
        {
            var lookup = new Dictionary<BucketKey, Bucket>();
            var claimed = new HashSet<Renderer>();
            var seen = new HashSet<LODGroup>();
            var probePositions = new Vector3[1];
            var sampledProbe = new SphericalHarmonicsL2[1];
            var sampledOcclusion = new Vector4[1];
            cachedSources = sources;
            if (sources == null) { prepared = true; return; }
            for (var sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
            {
                var source = sources[sourceIndex];
                if (source == null || !seen.Add(source)) continue;
                if (!source.enabled || !source.gameObject.activeInHierarchy || source.gameObject.scene != gameObject.scene
                    || source.fadeMode != LODFadeMode.None) { excludedGroups++; continue; }
                var lods = source.GetLODs();
                var eligible = lods.Length > 0;
                var members = new HashSet<Renderer>();
                for (var lodIndex = 0; lodIndex < lods.Length && eligible; lodIndex++)
                    for (var rendererIndex = 0; rendererIndex < lods[lodIndex].renderers.Length; rendererIndex++)
                    {
                        var renderer = lods[lodIndex].renderers[rendererIndex];
                        if (renderer == null || claimed.Contains(renderer) || !Supports(renderer)) { eligible = false; break; }
                        members.Add(renderer);
                    }
                if (!eligible || members.Count == 0) { excludedGroups++; continue; }
                var scale = source.transform.lossyScale;
                var group = new Group
                {
                    source = source, referencePoint = source.transform.TransformPoint(source.localReferencePoint),
                    size = source.size * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)),
                    levels = new Level[lods.Length],
                };
                for (var lodIndex = 0; lodIndex < lods.Length; lodIndex++)
                {
                    var lod = lods[lodIndex];
                    var parts = new List<Part>();
                    for (var rendererIndex = 0; rendererIndex < lod.renderers.Length; rendererIndex++)
                    {
                        var renderer = lod.renderers[rendererIndex];
                        var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                        var materials = renderer.sharedMaterials;
                        var matrix = renderer.localToWorldMatrix;
                        var bounds = renderer.bounds;
                        bounds.Expand(windBoundsPadding * 2);
                        var point = renderer.probeAnchor != null ? renderer.probeAnchor.position : renderer.bounds.center;
                        var probe = default(SphericalHarmonicsL2);
                        var occlusion = Vector4.one;
                        if (renderer.lightProbeUsage == LightProbeUsage.BlendProbes)
                        {
                            probePositions[0] = point;
                            LightProbes.CalculateInterpolatedLightAndOcclusionProbes(probePositions, sampledProbe, sampledOcclusion);
                            probe = sampledProbe[0]; occlusion = sampledOcclusion[0];
                        }
                        for (var submesh = 0; submesh < materials.Length; submesh++)
                        {
                            var centre = bounds.center;
                            var key = new BucketKey
                            {
                                mesh = mesh, material = materials[submesh], submesh = submesh,
                                cell = new Vector3Int(Mathf.FloorToInt(centre.x / cellSize), Mathf.FloorToInt(centre.y / cellSize), Mathf.FloorToInt(centre.z / cellSize)),
                                layer = renderer.gameObject.layer, renderingLayerMask = renderer.renderingLayerMask,
                                priority = renderer.rendererPriority, shadows = renderer.shadowCastingMode,
                                receiveShadows = renderer.receiveShadows, probes = renderer.lightProbeUsage,
                                reflections = renderer.reflectionProbeUsage, motion = renderer.motionVectorGenerationMode,
                            };
                            if (!lookup.TryGetValue(key, out var bucket))
                            {
                                bucket = new Bucket { key = key }; lookup.Add(key, bucket); buckets.Add(bucket);
                            }
                            bucket.capacity++;
                            parts.Add(new Part { bucket = bucket, matrix = matrix, bounds = bounds, probe = probe, occlusion = occlusion });
                        }
                    }
                    group.levels[lodIndex] = new Level { height = lod.screenRelativeTransitionHeight, parts = parts.ToArray(), rendererCount = lod.renderers.Length };
                }
                groups.Add(group); eligibleGroups++;
                foreach (var renderer in members)
                {
                    claimed.Add(renderer);
                    originalRenderers.Add(new RendererState { renderer = renderer, enabled = renderer.enabled });
                    sourceRenderers++;
                }
            }
            for (var index = 0; index < buckets.Count; index++) buckets[index].Allocate();
            // Do not change the source renderers until every cache has been built.
            for (var index = 0; index < groups.Count; index++) groups[index].source.enabled = false;
            for (var index = 0; index < originalRenderers.Count; index++) originalRenderers[index].renderer.enabled = false;
            prepared = true;
        }

        void ResetFrameCounters()
        {
            selectedGroups = selectedLOD0 = selectedLOD1 = selectedLOD2OrHigher = selectedRenderers = submittedBatches = 0;
            submittedTriangles = 0; prepareMilliseconds = submitMilliseconds = 0;
        }

        /// <summary>Uses the same screen-height metric as the installed SRP LODRenderingUtils.</summary>
        public static int SelectLOD(float worldSize, Vector3 referencePoint, float[] transitionHeights, Camera camera)
        {
            if (camera == null || transitionHeights == null || transitionHeights.Length == 0) return -1;
            GetCameraMetric(camera, out var position, out var metric, out var orthographic);
            var relative = RelativeHeight(worldSize, referencePoint, position, metric, orthographic);
            var minimum = Mathf.Clamp(QualitySettings.maximumLODLevel, 0, transitionHeights.Length - 1);
            for (var index = minimum; index < transitionHeights.Length; index++)
                if (relative >= transitionHeights[index]) return index;
            return -1;
        }

        static void GetCameraMetric(Camera camera, out Vector3 position, out float metric, out bool orthographic)
        {
            position = camera.transform.position;
            orthographic = camera.orthographic;
            metric = orthographic ? 2 * camera.orthographicSize : 2 * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
            if (camera.TryGetCullingParameters(out var parameters))
            {
                var lod = parameters.lodParameters;
                position = lod.cameraPosition; orthographic = lod.isOrthographic;
                metric = orthographic ? 2 * lod.orthoSize : 2 * Mathf.Tan(lod.fieldOfView * Mathf.Deg2Rad * .5f);
            }
            metric = Mathf.Max(.00001f, metric) / Mathf.Max(.00001f, QualitySettings.lodBias);
        }

        static float RelativeHeight(float size, Vector3 reference, Vector3 cameraPosition, float metric, bool orthographic)
        {
            return size / Mathf.Max(.00001f, orthographic ? metric : metric * Vector3.Distance(reference, cameraPosition));
        }

        public void Prepare(Camera camera)
        {
            var start = Stopwatch.GetTimestamp();
            ResetFrameCounters();
            if (!CanRun() || !IsCameraForScene(camera)) return;
            if (prepared && cachedSources != sources) Rebuild();
            if (buildFailed) return;
            if (!prepared)
            {
                try { Build(); }
                catch (Exception exception)
                {
                    Rebuild(); buildFailed = true; Debug.LogException(exception, this); return;
                }
            }
            for (var bucketIndex = 0; bucketIndex < buckets.Count; bucketIndex++)
            {
                var bucket = buckets[bucketIndex]; bucket.count = 0;
                for (var chunkIndex = 0; chunkIndex < bucket.chunks.Length; chunkIndex++) bucket.chunks[chunkIndex].count = 0;
            }
            GetCameraMetric(camera, out var cameraPosition, out var metric, out var orthographic);
            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = groups[groupIndex];
                if (group.source == null || !group.source.gameObject.activeInHierarchy) continue;
                var height = RelativeHeight(group.size, group.referencePoint, cameraPosition, metric, orthographic);
                var minimum = Mathf.Clamp(QualitySettings.maximumLODLevel, 0, group.levels.Length - 1);
                var selected = -1;
                for (var levelIndex = minimum; levelIndex < group.levels.Length; levelIndex++)
                    if (height >= group.levels[levelIndex].height) { selected = levelIndex; break; }
                if (selected < 0) continue;
                var level = group.levels[selected];
                selectedGroups++; selectedRenderers += level.rendererCount;
                if (selected == 0) selectedLOD0++; else if (selected == 1) selectedLOD1++; else selectedLOD2OrHigher++;
                for (var partIndex = 0; partIndex < level.parts.Length; partIndex++)
                {
                    ref var part = ref level.parts[partIndex];
                    if ((camera.cullingMask & (1 << part.bucket.key.layer)) != 0) part.bucket.Add(ref part);
                }
            }
            prepareMilliseconds = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }

        void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            Prepare(camera);
            if (!prepared || !CanRun() || !IsCameraForScene(camera)) return;
            var start = Stopwatch.GetTimestamp();
            for (var bucketIndex = 0; bucketIndex < buckets.Count; bucketIndex++)
            {
                var bucket = buckets[bucketIndex];
                for (var chunkIndex = 0; chunkIndex < bucket.chunks.Length; chunkIndex++)
                {
                    var chunk = bucket.chunks[chunkIndex];
                    if (chunk.count == 0) continue;
                    var parameters = bucket.parameters;
                    parameters.camera = camera; parameters.worldBounds = chunk.bounds;
                    if (chunk.properties != null)
                    {
                        chunk.properties.CopySHCoefficientArraysFrom(chunk.probes, 0, 0, chunk.count);
                        chunk.properties.CopyProbeOcclusionArrayFrom(chunk.occlusion, 0, 0, chunk.count);
                        parameters.matProps = chunk.properties;
                    }
                    // No camera-frustum rejection here: offscreen trees can cast
                    // visible shadows. Unity culls each spatial draw for its passes.
                    Graphics.RenderMeshInstanced(parameters, bucket.key.mesh, bucket.key.submesh, chunk.matrices, chunk.count);
                    submittedBatches++; submittedTriangles += bucket.triangles * chunk.count;
                }
            }
            submitMilliseconds = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
        }
    }
}
