using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace FightingAllstar.Rendering
{
    /// <summary>
    /// Releases native drawer resources at Windows player shutdown through the
    /// installed render pipeline's public lifecycle API. No per-frame work.
    /// </summary>
    internal static class MapNativeRendererLifetime
    {
        static bool cleaning, cleaned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            cleaning = cleaned = false;
            Application.quitting -= Cleanup;
            Application.quitting += Cleanup;
#endif
        }

        static void Cleanup()
        {
            if (Application.isEditor || cleaning || cleaned) return;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) return;
            var mode = pipeline.gpuResidentDrawerMode;
            cleaning = true;
            try
            {
                // ReinitializeIfNeeded is Editor-only in the installed Core
                // package. This public interface hook also runs in players and
                // disposes the existing drawer before reading Disabled settings.
                pipeline.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
                IGPUResidentRenderPipeline.ReinitializeGPUResidentDrawer();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                try
                {
                    if (pipeline != null) pipeline.gpuResidentDrawerMode = mode;
                }
                finally
                {
                    cleaning = false;
                    cleaned = true;
                }
            }
        }
    }
}
