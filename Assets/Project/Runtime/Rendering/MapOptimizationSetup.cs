using System;
using UnityEngine;
using UnityEngine.Rendering;
namespace FightingAllstar.Rendering
{
    /// <summary>Applies a reusable policy; preserves native per-renderer culling and all material assets.</summary>
    [DefaultExecutionOrder(-10990),DisallowMultipleComponent]
    public sealed class MapOptimizationSetup : MonoBehaviour
    {
        public MapOptimizationPolicy policy;
        public MeshRenderer[] staticRenderers=Array.Empty<MeshRenderer>();
        public int runtimeStaticBatchCandidates;
        void Awake()
        {
            if(policy==null)return;
            foreach(var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if(camera.gameObject.scene==gameObject.scene)camera.useOcclusionCulling=policy.useBakedOcclusion;
            if(!policy.combineStaticMeshes||!SystemInfo.supports32bitsIndexBuffer)return;
            // Unity retains the original renderer bounds and visibility for native
            // occlusion; it does not collapse the entire map into one giant bound.
            var candidates=new System.Collections.Generic.List<GameObject>();
            foreach(var renderer in staticRenderers)
            {
                if(renderer==null||!renderer.enabled||!renderer.gameObject.activeInHierarchy||renderer.isPartOfStaticBatch)continue;
                var f=renderer.GetComponent<MeshFilter>();
                if(f==null||f.sharedMesh==null||!f.sharedMesh.isReadable)continue;
                candidates.Add(renderer.gameObject);
            }
            runtimeStaticBatchCandidates=candidates.Count;
            if(candidates.Count>1)StaticBatchingUtility.Combine(candidates.ToArray(),gameObject);
        }
    }
}

