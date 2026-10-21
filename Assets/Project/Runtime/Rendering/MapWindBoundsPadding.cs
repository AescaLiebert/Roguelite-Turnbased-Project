using System.Collections.Generic;
using UnityEngine;

namespace FightingAllstar.Rendering
{
    /// <summary>Runtime culling bounds for shader wind; source meshes and LOD sizes remain unchanged.</summary>
    [DefaultExecutionOrder(-10980), DisallowMultipleComponent]
    public sealed class MapWindBoundsPadding : MonoBehaviour
    {
        public LODGroup[] sources = System.Array.Empty<LODGroup>();
        public float worldPadding = .8f;
        readonly Dictionary<Renderer, Bounds> originals = new Dictionary<Renderer, Bounds>();
        void OnEnable()
        {
            if (!Application.isPlaying) return;
            foreach (var group in sources)
            {
                if (!group) continue;
                foreach (var level in group.GetLODs()) foreach (var renderer in level.renderers)
                {
                    if (!renderer || originals.ContainsKey(renderer)) continue;
                    var windy = false;
                    foreach (var material in renderer.sharedMaterials)
                        if (material && material.HasProperty("_WindResponse") && material.GetFloat("_WindResponse") > 0) windy = true;
                    if (!windy) continue;
                    var bounds = renderer.localBounds; originals.Add(renderer, bounds);
                    var inverse = renderer.worldToLocalMatrix;
                    var extent = new Vector3(Mathf.Abs(inverse.m00)+Mathf.Abs(inverse.m01)+Mathf.Abs(inverse.m02),
                        Mathf.Abs(inverse.m10)+Mathf.Abs(inverse.m11)+Mathf.Abs(inverse.m12),
                        Mathf.Abs(inverse.m20)+Mathf.Abs(inverse.m21)+Mathf.Abs(inverse.m22));
                    bounds.Expand(extent * (2 * Mathf.Max(0, worldPadding))); renderer.localBounds = bounds;
                }
            }
        }
        void OnDisable()
        {
            foreach (var pair in originals) if (pair.Key) pair.Key.localBounds = pair.Value;
            originals.Clear();
        }
    }
}
