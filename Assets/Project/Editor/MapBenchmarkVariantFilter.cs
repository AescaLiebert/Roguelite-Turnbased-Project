using System.Collections.Generic;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace FightingAllstar.EditorTools
{
    /// <summary>Skips unused GPU-drawer variants in the map-only benchmark, never in shipping builds.</summary>
    public sealed class MapBenchmarkVariantFilter : IPreprocessShaders
    {
        public int callbackOrder => 10000;
        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (!MapOptimizationLab.BenchmarkBuild) return;
            // Unity Terrain uses its native terrain instancing, not MeshRenderer GRD.
            // The benchmark has no MeshRenderers using terrain shaders.
            var terrain = shader.name == "Teyvat/URP/LightingTerrain" || shader.name.Contains("Terrain/Lit");
            var stockForward = shader.name == "Universal Render Pipeline/Lit" && snippet.passName == "ForwardLit";
            if (!terrain && !stockForward) return;
            var dots = new ShaderKeyword("DOTS_INSTANCING_ON");
            var cluster = new ShaderKeyword("_CLUSTER_LIGHT_LOOP");
            for (var index=data.Count-1;index>=0;index--)
                if (data[index].shaderKeywordSet.IsEnabled(dots) &&
                    (terrain || !data[index].shaderKeywordSet.IsEnabled(cluster))) data.RemoveAt(index);
        }
    }
}
