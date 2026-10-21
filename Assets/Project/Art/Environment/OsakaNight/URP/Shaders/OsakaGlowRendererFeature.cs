using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FightingAllstar.EnvironmentArt
{
    /// <summary>
    /// RenderGraph equivalent of the authored Built-in OsakaNightAtmosphere.
    /// The quarter-resolution extraction, two separable blur iterations and
    /// highlight shoulder retain the sign treatment without a second bloom.
    /// Install only on the Osaka renderer; it does not change scene lighting.
    /// </summary>
    public sealed class OsakaGlowRendererFeature : ScriptableRendererFeature
    {
        public Shader shader;
        [Range(0f, 1f)] public float strength = .22f;
        [Range(.5f, 3f)] public float threshold = 1.05f;
        private Material material;
        private GlowPass pass;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            material = shader != null ? CoreUtils.CreateEngineMaterial(shader) : null;
            pass = new GlowPass { renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var type = renderingData.cameraData.cameraType;
            if (type == CameraType.Preview || type == CameraType.Reflection ||
                renderingData.cameraData.renderType == CameraRenderType.Overlay || shader == null) return;
            if (material == null || material.shader != shader) Create();
            if (material == null || !material.shader.isSupported) return;
            pass.Setup(material, strength, threshold);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }

        private sealed class GlowPass : ScriptableRenderPass
        {
            private static readonly int DirectionId = Shader.PropertyToID("_OsakaGlowDirection");
            private static readonly int ThresholdId = Shader.PropertyToID("_OsakaGlowThreshold");
            private static readonly int StrengthId = Shader.PropertyToID("_OsakaGlowStrength");
            private static readonly int BloomId = Shader.PropertyToID("_OsakaBloom");
            private static readonly Vector4 ScaleBias = new Vector4(1, 1, 0, 0);
            private Material material;
            private float strength, threshold;

            private sealed class PassData
            {
                public TextureHandle source, blurA, blurB, destination;
                public Material material;
                public float strength, threshold;
                public int width, height;
            }

            public GlowPass() { requiresIntermediateTexture = true; }

            public void Setup(Material value, float glowStrength, float glowThreshold)
            { material = value; strength = glowStrength; threshold = glowThreshold; }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || material == null) return;
                var source = resources.activeColorTexture;
                var descriptor = renderGraph.GetTextureDesc(source);
                descriptor.msaaSamples = MSAASamples.None;
                descriptor.depthBufferBits = DepthBits.None;
                descriptor.clearBuffer = false;
                descriptor.name = "Osaka glow composite";
                var destination = renderGraph.CreateTexture(descriptor);
                descriptor.width = Mathf.Max(1, descriptor.width / 4);
                descriptor.height = Mathf.Max(1, descriptor.height / 4);
                descriptor.filterMode = FilterMode.Bilinear;
                descriptor.wrapMode = TextureWrapMode.Clamp;
                descriptor.name = "Osaka glow blur A";
                var blurA = renderGraph.CreateTexture(descriptor);
                descriptor.name = "Osaka glow blur B";
                var blurB = renderGraph.CreateTexture(descriptor);

                // These dependent fullscreen operations cannot merge with scene
                // geometry. One tracked unsafe pass avoids six graph passes and
                // keeps all temporary textures local to this frame and camera.
                using (var builder = renderGraph.AddUnsafePass<PassData>("Osaka authored neon glow", out var data))
                {
                    data.source = source; data.destination = destination;
                    data.blurA = blurA; data.blurB = blurB; data.material = material;
                    data.strength = strength; data.threshold = threshold;
                    data.width = descriptor.width; data.height = descriptor.height;
                    builder.UseTexture(source, AccessFlags.Read);
                    builder.UseTexture(blurA, AccessFlags.ReadWrite);
                    builder.UseTexture(blurB, AccessFlags.ReadWrite);
                    builder.UseTexture(destination, AccessFlags.WriteAll);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
                    {
                        var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        cmd.SetGlobalFloat(ThresholdId, data.threshold);
                        cmd.SetGlobalFloat(StrengthId, data.strength);
                        context.cmd.SetRenderTarget(data.blurA);
                        Blitter.BlitTexture(cmd, (RTHandle)data.source, ScaleBias, data.material, 0);
                        for (var iteration = 0; iteration < 2; iteration++)
                        {
                            cmd.SetGlobalVector(DirectionId, new Vector4(1f / data.width, 0, 0, 0));
                            context.cmd.SetRenderTarget(data.blurB);
                            Blitter.BlitTexture(cmd, (RTHandle)data.blurA, ScaleBias, data.material, 1);
                            cmd.SetGlobalVector(DirectionId, new Vector4(0, 1f / data.height, 0, 0));
                            context.cmd.SetRenderTarget(data.blurA);
                            Blitter.BlitTexture(cmd, (RTHandle)data.blurB, ScaleBias, data.material, 1);
                        }
                        cmd.SetGlobalTexture(BloomId, (RenderTargetIdentifier)data.blurA);
                        context.cmd.SetRenderTarget(data.destination);
                        Blitter.BlitTexture(cmd, (RTHandle)data.source, ScaleBias, data.material, 2);
                    });
                }
                resources.cameraColor = destination;
            }
        }
    }
}
