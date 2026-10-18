using System;
using UnityEngine;

namespace FightingAllstar.EnvironmentArt
{
    /// <summary>Persisted, deterministic meadow placement. Matrices and bounds are in world metres.</summary>
    [CreateAssetMenu(menuName = "Teyvat/Meadow placement data", fileName = "TG_MeadowData")]
    public sealed class TeyvatMeadowData : ScriptableObject
    {
        public string revision = "WP04-r1";
        public int seed;
        public Bounds worldBounds;
        public int placementCount;
        public Species[] species = Array.Empty<Species>();
        public Cell[] cells = Array.Empty<Cell>();

        [Serializable]
        public sealed class Species
        {
            public string name;
            public Mesh meshLOD0;
            public Mesh meshLOD1;
            // Each entry matches that mesh's submesh index. A missing LOD1 array
            // uses LOD0's material slots; a missing LOD1 mesh keeps LOD0 geometry.
            public Material[] materialsLOD0 = Array.Empty<Material>();
            public Material[] materialsLOD1 = Array.Empty<Material>();
        }

        [Serializable]
        public sealed class Cell
        {
            public string name;
            // Builder includes full mesh extents and maximum wind displacement.
            public Bounds worldBounds;
            public Placement[] placements = Array.Empty<Placement>();
        }

        [Serializable]
        public struct Placement
        {
            public int speciesIndex;
            // The translation is the root/ground contact; fade must preserve it.
            public Matrix4x4 matrix;
            [Range(0, 1)] public float phase;
        }
    }
}
