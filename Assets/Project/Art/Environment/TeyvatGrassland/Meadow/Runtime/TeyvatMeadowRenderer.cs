using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace FightingAllstar.EnvironmentArt
{
    /// <summary>One explicit owner for persisted meadow submissions in URP review scenes.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class TeyvatMeadowRenderer : MonoBehaviour
    {
        public const int InstancesPerSubmission = 500;
        // Starting review values. The gradual shrink ends at 105 m; a hard 100 m
        // cutoff would defeat the transition. Tiny instances are skipped first.
        public const float LODNearMetres = 30;
        public const float LODFarMetres = 50;
        public const float FadeStartMetres = 70;
        public const float FadeEndMetres = 105;

        public TeyvatMeadowData data;
        public TeyvatMaterialGlobals globals;
        [Range(0, 31)] public int renderLayer;
        [Min(0)] public float windBoundsPadding = .8f;
        [Min(0)] public float frustumShadowPadding = 3;

        [NonSerialized] public int visibleInstances;
        [NonSerialized] public int visibleLOD0;
        [NonSerialized] public int visibleLOD1;
        [NonSerialized] public int submittedBatches;
        [NonSerialized] public long submittedTriangles;
        [NonSerialized] public int visibleCells;
        [NonSerialized] public double prepareMilliseconds;
        [NonSerialized] public double drawMilliseconds;

        public sealed class DrawGroup
        {
            public int speciesIndex;
            public int lod;
            public int submeshIndex;
            public Mesh mesh;
            public Material material;
            public Matrix4x4[] matrices;
            public int count;
            public Bounds worldBounds;
        }

        sealed class Chunk
        {
            public readonly Matrix4x4[] matrices = new Matrix4x4[InstancesPerSubmission];
            public readonly List<DrawGroup> drawGroups = new List<DrawGroup>();
            public int count;
            public Bounds bounds;
        }

        sealed class Family
        {
            public readonly List<Chunk> chunks = new List<Chunk>();
            public int count;
        }

        struct CachedPlacement
        {
            public bool valid;
            public bool hasLOD1;
            public int speciesIndex;
            public Matrix4x4 matrix;
            public Vector3 root;
            public float lodThresholdSquared;
            public Bounds boundsLOD0;
            public Bounds boundsLOD1;
        }

        sealed class CachedCell
        {
            public Bounds worldBounds;
            public CachedPlacement[] placements;
        }

        readonly Plane[] frustumPlanes = new Plane[6];
        readonly List<DrawGroup> drawGroups = new List<DrawGroup>();
        Family[] families = Array.Empty<Family>();
        TeyvatMeadowData cachedData;
        TeyvatMeadowData.Species[] cachedSpecies;
        TeyvatMeadowData.Cell[] cachedCells;
        CachedCell[] placementCells = Array.Empty<CachedCell>();
        float cachedWindPadding;
        bool searchedGlobals;
        double nextGlobalsSearch;

        /// <summary>Reused groups from the most recent Prepare. Inspect without mutating.</summary>
        public IReadOnlyList<DrawGroup> DrawGroups { get { return drawGroups; } }

        void OnEnable()
        {
            searchedGlobals = false;
            nextGlobalsSearch = 0;
            InvalidatePlacementCache();
            RenderPipelineManager.beginCameraRendering += BeginCameraRendering;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeginCameraRendering;
            ResetCounters();
            drawGroups.Clear();
        }

        void OnValidate()
        {
            renderLayer = Mathf.Clamp(renderLayer, 0, 31);
            windBoundsPadding = Mathf.Max(0, windBoundsPadding);
            frustumShadowPadding = Mathf.Max(0, frustumShadowPadding);
            searchedGlobals = false;
            nextGlobalsSearch = 0;
            InvalidatePlacementCache();
        }

        void ResetCounters()
        {
            visibleInstances = 0;
            visibleLOD0 = 0;
            visibleLOD1 = 0;
            visibleCells = 0;
            submittedBatches = 0;
            submittedTriangles = 0;
            prepareMilliseconds = 0;
            drawMilliseconds = 0;
        }

        void ResolveGlobals()
        {
            if (globals != null) return;
            // A prefab can enable before the external globals owner exists.
            // Retry at low frequency instead of caching that absence forever.
            if (searchedGlobals && Time.realtimeSinceStartupAsDouble < nextGlobalsSearch) return;
            searchedGlobals = true;
            nextGlobalsSearch = Time.realtimeSinceStartupAsDouble + .5;
            // Search once after enabling/validation. Prefab data does not require
            // the shared scene globals to be serialized into its own asset.
            var candidates = FindObjectsByType<TeyvatMaterialGlobals>(FindObjectsSortMode.None);
            for (var index = 0; index < candidates.Length; index++)
                if (candidates[index].gameObject.scene == gameObject.scene)
                {
                    globals = candidates[index];
                    break;
                }
        }

        /// <summary>Call after editing placement/matrix/mesh bounds in place. New data/array references rebuild automatically.</summary>
        public void InvalidatePlacementCache()
        {
            cachedData = null;
            cachedSpecies = null;
            cachedCells = null;
            placementCells = Array.Empty<CachedCell>();
        }

        void EnsureFamiliesAndPlacementCache()
        {
            if (cachedData == data && cachedSpecies == data.species && cachedCells == data.cells
                && cachedWindPadding == windBoundsPadding && families.Length == data.species.Length * 2) return;
            cachedData = data;
            cachedSpecies = data.species;
            cachedCells = data.cells;
            cachedWindPadding = windBoundsPadding;
            families = new Family[data.species.Length * 2];
            for (var index = 0; index < families.Length; index++) families[index] = new Family();
            var meshBounds = new Bounds[data.species.Length * 2];
            for (var speciesIndex = 0; speciesIndex < data.species.Length; speciesIndex++)
            {
                var species = data.species[speciesIndex];
                if (species == null || species.meshLOD0 == null) continue;
                meshBounds[speciesIndex * 2] = species.meshLOD0.bounds;
                meshBounds[speciesIndex * 2 + 1] = species.meshLOD1 == null ? meshBounds[speciesIndex * 2] : species.meshLOD1.bounds;
            }
            placementCells = new CachedCell[data.cells.Length];
            for (var cellIndex = 0; cellIndex < data.cells.Length; cellIndex++)
            {
                var source = data.cells[cellIndex];
                if (source == null || source.placements == null) continue;
                var cell = new CachedCell { worldBounds = source.worldBounds, placements = new CachedPlacement[source.placements.Length] };
                for (var placementIndex = 0; placementIndex < source.placements.Length; placementIndex++)
                {
                    var sourcePlacement = source.placements[placementIndex];
                    var speciesIndex = sourcePlacement.speciesIndex;
                    if (speciesIndex < 0 || speciesIndex >= data.species.Length) continue;
                    var species = data.species[speciesIndex];
                    if (species == null || species.meshLOD0 == null) continue;
                    var threshold = Mathf.Lerp(LODNearMetres, LODFarMetres, Mathf.Clamp01(sourcePlacement.phase));
                    // Cache full original bounds, deliberately conservative while
                    // distance fading shrinks geometry. The independent world wind
                    // padding remains intact instead of shrinking with the plant.
                    var boundsLOD0 = TransformBounds(meshBounds[speciesIndex * 2], sourcePlacement.matrix);
                    var boundsLOD1 = TransformBounds(meshBounds[speciesIndex * 2 + 1], sourcePlacement.matrix);
                    boundsLOD0.Expand(windBoundsPadding * 2);
                    boundsLOD1.Expand(windBoundsPadding * 2);
                    cell.placements[placementIndex] = new CachedPlacement
                    {
                        valid = true, hasLOD1 = species.meshLOD1 != null, speciesIndex = speciesIndex,
                        matrix = sourcePlacement.matrix,
                        root = new Vector3(sourcePlacement.matrix.m03, sourcePlacement.matrix.m13, sourcePlacement.matrix.m23),
                        lodThresholdSquared = threshold * threshold,
                        boundsLOD0 = boundsLOD0, boundsLOD1 = boundsLOD1,
                    };
                    cell.worldBounds.Encapsulate(boundsLOD0);
                    cell.worldBounds.Encapsulate(boundsLOD1);
                }
                placementCells[cellIndex] = cell;
            }
        }

        bool IsCameraForScene(Camera camera)
        {
            if (camera.gameObject.scene == gameObject.scene) return true;
            // Scene-view cameras belong to Unity's editor scene rather than the
            // authored scene. They still need the meadow for normal authoring.
            return camera.cameraType == CameraType.SceneView && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        /// <summary>Prepare actual culled/LOD/faded batches; no rendering or placement mutation.</summary>
        public void Prepare(Camera camera)
        {
            var started = Stopwatch.GetTimestamp();
            ResetCounters();
            drawGroups.Clear();
            if (GraphicsSettings.currentRenderPipeline == null || camera == null || data == null || data.species == null || data.cells == null
                || !IsCameraForScene(camera) || (camera.cullingMask & (1 << renderLayer)) == 0)
                return;
            ResolveGlobals();
            EnsureFamiliesAndPlacementCache();
            for (var familyIndex = 0; familyIndex < families.Length; familyIndex++)
            {
                var family = families[familyIndex];
                family.count = 0;
                for (var chunkIndex = 0; chunkIndex < family.chunks.Count; chunkIndex++) family.chunks[chunkIndex].count = 0;
            }
            GeometryUtility.CalculateFrustumPlanes(camera, frustumPlanes);
            var cameraPosition = camera.transform.position;
            const float maximumDistanceSquared = FadeEndMetres * FadeEndMetres;
            for (var cellIndex = 0; cellIndex < placementCells.Length; cellIndex++)
            {
                var cell = placementCells[cellIndex];
                if (cell == null || cell.placements == null || cell.worldBounds.SqrDistance(cameraPosition) > maximumDistanceSquared) continue;
                // A small off-frustum margin retains adjacent grass casters for
                // the review's sun angles. It is a starting bound, not a complete
                // shadow-volume solution for arbitrary horizon-height sunlight.
                var cullingBounds = cell.worldBounds;
                cullingBounds.Expand(frustumShadowPadding * 2);
                if (!GeometryUtility.TestPlanesAABB(frustumPlanes, cullingBounds)) continue;
                visibleCells++;
                for (var placementIndex = 0; placementIndex < cell.placements.Length; placementIndex++)
                {
                    ref var placement = ref cell.placements[placementIndex];
                    if (!placement.valid) continue;
                    var distanceSquared = (placement.root - cameraPosition).sqrMagnitude;
                    if (distanceSquared >= maximumDistanceSquared) continue;
                    var fade = 1f;
                    if (distanceSquared > FadeStartMetres * FadeStartMetres)
                    {
                        var fadeT = (Mathf.Sqrt(distanceSquared) - FadeStartMetres) / (FadeEndMetres - FadeStartMetres);
                        fade = 1 - fadeT * fadeT * (3 - 2 * fadeT);
                        if (fade <= .01f) continue;
                    }
                    var lod = distanceSquared >= placement.lodThresholdSquared && placement.hasLOD1 ? 1 : 0;
                    var matrix = placement.matrix;
                    // Scale the basis columns only. The world-space root contact
                    // and its height remain exactly unchanged by distance fade.
                    if (fade < 1)
                    {
                        matrix.m00 *= fade; matrix.m10 *= fade; matrix.m20 *= fade;
                        matrix.m01 *= fade; matrix.m11 *= fade; matrix.m21 *= fade;
                        matrix.m02 *= fade; matrix.m12 *= fade; matrix.m22 *= fade;
                    }
                    var family = families[placement.speciesIndex * 2 + lod];
                    var chunkIndex = family.count / InstancesPerSubmission;
                    if (chunkIndex == family.chunks.Count) family.chunks.Add(new Chunk());
                    var chunk = family.chunks[chunkIndex];
                    chunk.matrices[chunk.count++] = matrix;
                    family.count++;
                    var bounds = lod == 0 ? placement.boundsLOD0 : placement.boundsLOD1;
                    if (chunk.count == 1) chunk.bounds = bounds;
                    else chunk.bounds.Encapsulate(bounds);
                    visibleInstances++;
                    if (lod == 0) visibleLOD0++; else visibleLOD1++;
                }
            }
            for (var speciesIndex = 0; speciesIndex < data.species.Length; speciesIndex++)
            {
                var species = data.species[speciesIndex];
                if (species == null) continue;
                for (var lod = 0; lod < 2; lod++)
                {
                    var mesh = lod == 0 ? species.meshLOD0 : species.meshLOD1;
                    if (mesh == null) continue;
                    var slots = lod == 0 || species.materialsLOD1 == null || species.materialsLOD1.Length == 0 ? species.materialsLOD0 : species.materialsLOD1;
                    if (slots == null) continue;
                    var family = families[speciesIndex * 2 + lod];
                    for (var chunkIndex = 0; chunkIndex < family.chunks.Count; chunkIndex++)
                    {
                        var chunk = family.chunks[chunkIndex];
                        if (chunk.count == 0) continue;
                        for (var submesh = 0; submesh < mesh.subMeshCount && submesh < slots.Length; submesh++)
                        {
                            if (slots[submesh] == null || !slots[submesh].enableInstancing) continue;
                            while (chunk.drawGroups.Count <= submesh) chunk.drawGroups.Add(new DrawGroup());
                            var group = chunk.drawGroups[submesh];
                            group.speciesIndex = speciesIndex; group.lod = lod; group.submeshIndex = submesh;
                            group.mesh = mesh; group.material = slots[submesh]; group.matrices = chunk.matrices;
                            group.count = chunk.count; group.worldBounds = chunk.bounds;
                            drawGroups.Add(group);
                            submittedTriangles += (long)(mesh.GetIndexCount(submesh) / 3) * chunk.count;
                        }
                    }
                }
            }
            prepareMilliseconds = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
        }

        static Bounds TransformBounds(Bounds source, Matrix4x4 matrix)
        {
            var center = matrix.MultiplyPoint3x4(source.center);
            var extent = source.extents;
            var x = matrix.MultiplyVector(new Vector3(extent.x, 0, 0));
            var y = matrix.MultiplyVector(new Vector3(0, extent.y, 0));
            var z = matrix.MultiplyVector(new Vector3(0, 0, extent.z));
            var worldExtent = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            return new Bounds(center, worldExtent * 2);
        }

        void BeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!isActiveAndEnabled || GraphicsSettings.currentRenderPipeline == null || camera == null || !IsCameraForScene(camera)) return;
            Prepare(camera);
            if (globals != null) globals.Apply();
            var viewPosition = camera.transform.position;
            Shader.SetGlobalVector("_TG_MeadowViewPosition", new Vector4(viewPosition.x, viewPosition.y, viewPosition.z, 1));
            var started = Stopwatch.GetTimestamp();
            for (var index = 0; index < drawGroups.Count; index++)
            {
                var group = drawGroups[index];
                var parameters = new RenderParams(group.material)
                {
                    camera = camera, layer = renderLayer, worldBounds = group.worldBounds,
                    shadowCastingMode = ShadowCastingMode.On, receiveShadows = true,
                };
                Graphics.RenderMeshInstanced(parameters, group.mesh, group.submeshIndex, group.matrices, group.count);
                submittedBatches++;
            }
            drawMilliseconds = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
        }
    }
}
