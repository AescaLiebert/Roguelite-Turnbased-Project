using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace FightingAllstar.EditorTools.CharacterCreatorStudio
{
    public enum StudioTextFontProfile { EditorStandard, BattleInspector }

    /// <summary>
    /// The source string is rendered by a real UI Toolkit Label and a real
    /// TextMeshProUGUI canvas. Each selectable font profile uses an owned transient
    /// atlas so previewing never populates a persistent font asset.
    /// </summary>
    public sealed class StudioPreviewText : IDisposable
    {
        public const string BattleFontResource = "UI/BarlowCondensed-SemiBoldItalic SDF";
        public const string EditorFontResource = "UI/zh-cn";
        private readonly IMGUIContainer _viewport;
        private readonly Label _diagnostics = new Label();
        private readonly Label _tmpTitle;
        private readonly DropdownField _fontPicker;
        private PreviewRenderUtility _preview;
        private GameObject _canvasRoot;
        private TMP_FontAsset _ownedFont;
        private string _text = string.Empty;
        private bool _disposed;
        private int _width = 960;
        private int _height = 280;

        public VisualElement Root { get; }
        public Label ToolkitLabel { get; }
        public TextMeshProUGUI TmpText { get; private set; }
        public string Diagnostics => _diagnostics.text;
        public bool HasFixture => _preview != null;
        public string SourceText => _text;
        public StudioTextFontProfile FontProfile { get; private set; }
        public event Action<StudioTextFontProfile> FontProfileChanged;
        public IReadOnlyList<uint> MissingCodepoints => _missing;
        private readonly List<uint> _missing = new List<uint>();

        public StudioPreviewText(StudioTextFontProfile profile = StudioTextFontProfile.EditorStandard)
        {
            FontProfile = profile;
            Root = new VisualElement { name = "studio-text-preview" };
            var font = Resources.Load<Font>(EditorFontResource);
            if (font != null) Root.style.unityFontDefinition = FontDefinition.FromFont(font);
            Root.Add(new Label("UI Toolkit · actual Label") { name = "toolkit-preview-title" });
            ToolkitLabel = new Label { name = "toolkit-description-preview", enableRichText = true };
            ToolkitLabel.style.whiteSpace = WhiteSpace.Normal;
            ToolkitLabel.style.fontSize = 18;
            ToolkitLabel.style.color = new Color(.91f, .95f, 1);
            ToolkitLabel.style.backgroundColor = new Color(.07f, .085f, .12f);
            ToolkitLabel.style.minHeight = 80;
            ToolkitLabel.style.paddingLeft = 12;
            ToolkitLabel.style.paddingRight = 12;
            ToolkitLabel.style.paddingTop = 12;
            ToolkitLabel.style.paddingBottom = 12;
            if (font != null) ToolkitLabel.style.unityFontDefinition = FontDefinition.FromFont(font);
            Root.Add(ToolkitLabel);
            _fontPicker = new DropdownField("TMP font profile", new List<string> { "Editor standard (zh-cn)", "Battle inspector (Barlow)" }, (int)profile);
            _fontPicker.RegisterValueChangedCallback(e => SetFontProfile((StudioTextFontProfile)_fontPicker.index));
            Root.Add(_fontPicker);
            _tmpTitle = new Label { name = "tmp-preview-title" };
            UpdateProfileTitle();
            Root.Add(_tmpTitle);
            _viewport = new IMGUIContainer(Draw) { name = "tmp-description-viewport" };
            _viewport.style.height = 170;
            _viewport.style.minHeight = 100;
            Root.Add(_viewport);
            _diagnostics.style.whiteSpace = WhiteSpace.Normal;
            _diagnostics.style.fontSize = 11;
            Root.Add(_diagnostics);
            Root.RegisterCallback<DetachFromPanelEvent>(_ => ReleaseFixture());
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting += Dispose;
        }

        public void SetFontProfile(StudioTextFontProfile profile)
        {
            ThrowIfDisposed();
            if (!Enum.IsDefined(typeof(StudioTextFontProfile), profile)) throw new ArgumentOutOfRangeException(nameof(profile));
            if (FontProfile == profile) return;
            ReleaseFixture();
            FontProfile = profile;
            _fontPicker.SetValueWithoutNotify(_fontPicker.choices[(int)profile]);
            UpdateProfileTitle();
            SetText(_text);
            FontProfileChanged?.Invoke(profile);
        }

        private void UpdateProfileTitle()
        {
            _tmpTitle.text = FontProfile == StudioTextFontProfile.EditorStandard
                ? "Editor standard · actual TextMeshProUGUI + zh-cn"
                : "Battle inspector · actual TextMeshProUGUI + game font";
        }

        public void SetText(string richText)
        {
            ThrowIfDisposed();
            _text = richText ?? string.Empty;
            ToolkitLabel.text = _text;
            EnsureFixture();
            TmpText.text = _text;
            TmpText.ForceMeshUpdate(true, true);
            UpdateDiagnostics();
            _viewport.MarkDirtyRepaint();
        }

        /// <summary>Set a deterministic logical game surface, independent of editor pixel density.</summary>
        public void SetSurfaceSize(int width, int height)
        {
            ThrowIfDisposed();
            _width = Mathf.Clamp(width, 160, 2048);
            _height = Mathf.Clamp(height, 64, 2048);
            // Give expanded surfaces enough vertical room to retain legible target typography.
            _viewport.style.height = Mathf.Clamp(_height, 170, 600);
            EnsureFixture();
            LayoutCanvas();
            TmpText.ForceMeshUpdate(true, true);
            UpdateDiagnostics();
            _viewport.MarkDirtyRepaint();
        }

        public void SetTypography(float size)
        {
            ToolkitLabel.style.fontSize = size;
            if (TmpText != null) { TmpText.fontSize = size; TmpText.ForceMeshUpdate(true, true); UpdateDiagnostics(); }
        }

        private void EnsureFixture()
        {
            if (_preview != null || _disposed) return;
            _preview = new PreviewRenderUtility();
            try
            {
                var camera = _preview.camera;
                // Unity excludes world-space Canvas rendering from CameraType.Preview.
                // This camera still belongs to the disposable preview scene/texture.
                camera.cameraType = CameraType.Game;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.07f, .085f, .12f);
                camera.orthographic = true;
                camera.nearClipPlane = .01f;
                camera.farClipPlane = 100;
                camera.transform.SetPositionAndRotation(new Vector3(0, 0, -10), Quaternion.identity);
                _canvasRoot = new GameObject("Studio TMP canvas", typeof(RectTransform), typeof(Canvas));
                _canvasRoot.hideFlags = HideFlags.HideAndDontSave;
                var canvas = _canvasRoot.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = camera;
                var textRoot = new GameObject("Studio TMP description", typeof(RectTransform));
                textRoot.hideFlags = HideFlags.HideAndDontSave;
                textRoot.transform.SetParent(_canvasRoot.transform, false);
                TmpText = textRoot.AddComponent<TextMeshProUGUI>();
                var source = FontProfile == StudioTextFontProfile.BattleInspector ? Resources.Load<TMP_FontAsset>(BattleFontResource) : null;
                if (FontProfile == StudioTextFontProfile.BattleInspector)
                {
                    if (source == null || source.sourceFontFile == null)
                        throw new InvalidOperationException("The game description font or its source font is missing: Resources/" + BattleFontResource);
                    // Match the existing game font configuration without populating it.
                    _ownedFont = TMP_FontAsset.CreateFontAsset(source.sourceFontFile, Mathf.Max(24, (int)source.faceInfo.pointSize),
                        source.atlasPadding, source.atlasRenderMode, source.atlasWidth, source.atlasHeight);
                }
                else
                {
                    var editorFont = Resources.Load<Font>(EditorFontResource);
                    if (editorFont == null) throw new InvalidOperationException("The Editor standard font is missing: Resources/" + EditorFontResource);
                    _ownedFont = TMP_FontAsset.CreateFontAsset(editorFont);
                }
                if (_ownedFont == null) throw new InvalidOperationException("Could not create the isolated TMP font atlas.");
                _ownedFont.name = (source != null ? source.name : "zh-cn") + " · Studio transient";
                _ownedFont.hideFlags = HideFlags.HideAndDontSave;
                _ownedFont.fallbackFontAssetTable = new List<TMP_FontAsset>();
                if (_ownedFont.material != null) _ownedFont.material.hideFlags = HideFlags.HideAndDontSave;
                if (_ownedFont.material != null && source != null && source.material != null)
                {
                    var atlas = _ownedFont.material.mainTexture;
                    _ownedFont.material.CopyPropertiesFromMaterial(source.material);
                    _ownedFont.material.mainTexture = atlas;
                    _ownedFont.material.hideFlags = HideFlags.HideAndDontSave;
                }
                TmpText.font = _ownedFont;
                TmpText.richText = true;
                TmpText.fontSize = 26;
                TmpText.color = new Color(.91f, .95f, 1);
                TmpText.raycastTarget = false;
                TmpText.alignment = TextAlignmentOptions.TopLeft;
                TmpText.textWrappingMode = TextWrappingModes.Normal;
                TmpText.overflowMode = TextOverflowModes.Overflow;
                TmpText.margin = new Vector4(20, 16, 20, 16);
                TmpText.text = _text;
                _preview.AddSingleGO(_canvasRoot);
                LayoutCanvas();
                TmpText.ForceMeshUpdate(true, true);
            }
            catch { ReleaseFixture(); throw; }
        }

        private void LayoutCanvas()
        {
            var rect = (RectTransform)_canvasRoot.transform;
            rect.sizeDelta = new Vector2(_width, _height);
            rect.localScale = Vector3.one * .01f;
            var textRect = TmpText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            _preview.camera.orthographicSize = _height * .005f;
        }

        private void UpdateDiagnostics()
        {
            _missing.Clear();
            if (TmpText == null || _ownedFont == null) return;
            var information = TmpText.textInfo;
            for (var i = 0; i < information.characterCount; i++)
            {
                var character = information.characterInfo[i];
                // TMP can put a replacement square into characterInfo.character. Read
                // the requested source codepoint through its source index so missing
                // Thai/combining glyphs remain visible as diagnostics after rendering.
                var requested = character.index >= 0 && character.index < _text.Length ? _text[character.index] : character.character;
                var point = (uint)requested;
                if (char.IsHighSurrogate(requested) && character.index + 1 < _text.Length && char.IsLowSurrogate(_text[character.index + 1]))
                    point = (uint)char.ConvertToUtf32(requested, _text[character.index + 1]);
                if (char.IsWhiteSpace(requested) || char.IsControl(requested)) continue;
                // The source Font.HasCharacter API can report OS-fallback coverage even
                // when the owned TMP face cannot generate that glyph. Check the requested
                // Unicode value in the populated transient TMP atlas, never the square.
                if (!_ownedFont.HasCharacter((int)point) && !_missing.Contains(point)) _missing.Add(point);
            }
            var description = new StringBuilder("Source font: ").Append(_ownedFont.sourceFontFile.name)
                .Append(" · ").Append(information.characterCount).Append(" parsed characters")
                .Append(" · ").Append(_width).Append('×').Append(_height).Append(" logical pixels");
            if (_missing.Count > 0)
            {
                description.Append(" · Missing glyphs:");
                foreach (var point in _missing) description.Append(" U+").Append(point.ToString("X4"));
                description.Append(". This locale needs a font/fallback before release.");
            }
            else description.Append(" · All parsed glyphs available in the selected isolated font.");
            if (TmpText.preferredHeight > _height - 32) description.Append(" · Text exceeds this surface height.");
            _diagnostics.text = description.ToString();
        }

        private void Draw()
        {
            var available = GUILayoutUtility.GetRect(100, 100, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type != EventType.Repaint || available.width <= 1 || available.height <= 1 || _disposed) return;
            EnsureFixture();
            // Preserve the actual game surface's aspect ratio; wider editor panes do
            // not stretch glyphs or claim a different text wrapping configuration.
            var height = Mathf.Min(available.height, available.width * _height / _width);
            var width = height * _width / _height;
            var rect = new Rect(available.x, available.y, width, height);
            _preview.BeginPreview(rect, GUIStyle.none);
            TmpText.ForceMeshUpdate(true, false);
            _preview.Render();
            GUI.DrawTexture(rect, _preview.EndPreview(), ScaleMode.ScaleToFit);
        }

        public void CaptureTmp(string absolutePngPath, int width = 960, int height = 280)
        {
            ThrowIfDisposed();
            SetSurfaceSize(width, height);
            _preview.BeginStaticPreview(new Rect(0, 0, _width, _height));
            TmpText.ForceMeshUpdate(true, false);
            _preview.Render();
            var image = _preview.EndStaticPreview();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(absolutePngPath)));
                File.WriteAllBytes(absolutePngPath, image.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(image); }
        }

        private void ReleaseFixture()
        {
            // Destroy the canvas first, then the transient materials/atlases. No asset
            // loaded through Resources/AssetDatabase is ever destroyed or marked dirty.
            _preview?.Cleanup();
            _preview = null;
            _canvasRoot = null;
            TmpText = null;
            if (_ownedFont != null)
            {
                if (_ownedFont.material != null) Object.DestroyImmediate(_ownedFont.material);
                foreach (var atlas in _ownedFont.atlasTextures)
                    if (atlas != null) Object.DestroyImmediate(atlas);
                Object.DestroyImmediate(_ownedFont);
                _ownedFont = null;
            }
        }

        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(StudioPreviewText)); }

        public void Dispose()
        {
            if (_disposed) return;
            ReleaseFixture();
            _disposed = true;
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            EditorApplication.quitting -= Dispose;
        }
    }
}
