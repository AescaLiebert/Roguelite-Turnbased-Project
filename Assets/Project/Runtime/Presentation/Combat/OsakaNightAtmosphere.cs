using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Small Built-in pipeline glow pass. The stage remains fully modeled without it.</summary>
    [ExecuteAlways, RequireComponent(typeof(Camera))]
    public sealed class OsakaNightAtmosphere : MonoBehaviour
    {
        [SerializeField] private Shader glowShader;
        [SerializeField, Range(0f, 1f)] private float glowStrength = .22f;
        [SerializeField, Range(.5f, 3f)] private float threshold = 1.05f;
        private Material _material;

        private void OnEnable() { GetComponent<Camera>().allowHDR = true; }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (glowShader == null || !glowShader.isSupported)
            { Graphics.Blit(source, destination); return; }
            if (_material == null) _material = new Material(glowShader) { hideFlags = HideFlags.HideAndDontSave };
            _material.SetFloat("_Threshold", threshold);
            _material.SetFloat("_Strength", glowStrength);
            var w = Mathf.Max(1, source.width / 4);
            var h = Mathf.Max(1, source.height / 4);
            var a = RenderTexture.GetTemporary(w, h, 0, source.format);
            var b = RenderTexture.GetTemporary(w, h, 0, source.format);
            a.filterMode = b.filterMode = FilterMode.Bilinear;
            try
            {
                Graphics.Blit(source, a, _material, 0);
                for (var i = 0; i < 2; i++)
                {
                    _material.SetVector("_Direction", new Vector4(1f / w, 0, 0, 0));
                    Graphics.Blit(a, b, _material, 1);
                    _material.SetVector("_Direction", new Vector4(0, 1f / h, 0, 0));
                    Graphics.Blit(b, a, _material, 1);
                }
                _material.SetTexture("_Bloom", a);
                Graphics.Blit(source, destination, _material, 2);
            }
            finally { RenderTexture.ReleaseTemporary(a); RenderTexture.ReleaseTemporary(b); }
        }

        private void OnDisable()
        {
            if (_material == null) return;
            if (Application.isPlaying) Destroy(_material); else DestroyImmediate(_material);
            _material = null;
        }
    }
}
