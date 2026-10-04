using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools
{
    /// <summary>Offscreen evidence capture for batchmode, where ScreenCapture has no Game View backbuffer.</summary>
    internal static class BattlePreviewCapture
    {
        public static void Save(string path, UIDocument document)
        {
            var camera = Camera.main;
            if (camera == null || document?.panelSettings == null) return;
            var panel = document.rootVisualElement.panel;
            if (panel == null) return;
            const int width = 1280;
            const int height = 720;
            var world = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var overlay = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            var oldCameraTarget = camera.targetTexture;
            var oldPanelTarget = document.panelSettings.targetTexture;
            var oldActive = RenderTexture.active;
            Texture2D pixels = null;
            Texture2D uiPixels = null;
            try
            {
                camera.targetTexture = world;
                camera.Render();
                RenderTexture.active = world;
                pixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                RenderTexture.active = overlay;
                GL.Clear(true, true, Color.clear);
                document.panelSettings.targetTexture = overlay;
                typeof(PanelSettings).GetMethod("ApplyPanelSettings", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(document.panelSettings, null);
                panel.GetType().GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null, Type.EmptyTypes, null)?.Invoke(panel, null);
                var repaint = panel.GetType().GetMethod("Repaint", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null, new[] { typeof(Event) }, null);
                if (repaint == null) throw new InvalidOperationException("Runtime panel repaint method unavailable.");
                repaint.Invoke(panel, new object[] { new Event { type = EventType.Repaint } });
                // Unity 6 separates render-chain preparation from drawing the panel.
                panel.GetType().GetMethod("Render", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public,
                    null, Type.EmptyTypes, null)?.Invoke(panel, null);
                Debug.Log("BATTLE UI CAPTURE bounds=" + document.rootVisualElement.worldBound + " panel=" + panel.GetType().Name);
                RenderTexture.active = overlay;
                uiPixels = new Texture2D(width, height, TextureFormat.RGBA32, false);
                uiPixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                uiPixels.Apply();
                var background = pixels.GetPixels();
                var foreground = uiPixels.GetPixels();
                for (var i = 0; i < background.Length; i++)
                    background[i] = foreground[i] + background[i] * (1 - foreground[i].a);
                pixels.SetPixels(background);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldCameraTarget;
                document.panelSettings.targetTexture = oldPanelTarget;
                RenderTexture.active = oldActive;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (uiPixels != null) UnityEngine.Object.DestroyImmediate(uiPixels);
                RenderTexture.ReleaseTemporary(world);
                RenderTexture.ReleaseTemporary(overlay);
            }
        }
    }
}
