using System.Collections.Generic;
using FightingAllstar.Core.Combat;
using UnityEngine;
using UnityEngine.UIElements;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>
    /// Object pool for UI Toolkit Floating Combat Text (FCT) elements.
    /// Manages reusable visual element groups, kickers, content rows, attribute affinity arrows,
    /// and labels to handle concurrent multi-text display without allocations.
    /// </summary>
    public sealed class FloatingCombatTextPool
    {
        public sealed class Item
        {
            public VisualElement Group { get; }
            public Label Kicker { get; }
            public VisualElement ContentRow { get; }
            public VisualElement Arrow { get; }
            public Label Label { get; }
            public string CurrentKind { get; set; }
            public AttributeAffinity CurrentAffinity { get; set; }

            public Item(VisualElement group, Label kicker, VisualElement contentRow, VisualElement arrow, Label label)
            {
                Group = group;
                Kicker = kicker;
                ContentRow = contentRow;
                Arrow = arrow;
                Label = label;
            }

            public void SetVisible(bool visible)
            {
                Group.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            }

            public void ResetStyle()
            {
                if (!string.IsNullOrEmpty(CurrentKind))
                {
                    Group.RemoveFromClassList(CurrentKind);
                    Kicker.RemoveFromClassList(CurrentKind);
                    Label.RemoveFromClassList(CurrentKind);
                    CurrentKind = null;
                }

                Arrow.RemoveFromClassList("advantage");
                Arrow.RemoveFromClassList("disadvantage");
                Arrow.style.backgroundImage = StyleKeyword.Null;
                Arrow.style.display = DisplayStyle.None;
                CurrentAffinity = AttributeAffinity.Neutral;

                Group.style.opacity = 0f;
                Group.style.scale = new Scale(Vector3.one);
                Kicker.text = string.Empty;
                Kicker.style.display = DisplayStyle.None;
                Label.text = string.Empty;
                Kicker.style.color = StyleKeyword.Null;
                Kicker.style.unityTextOutlineColor = StyleKeyword.Null;
                Kicker.style.unityFont = StyleKeyword.Null;
                Kicker.style.unityFontDefinition = StyleKeyword.Null;
                Kicker.style.unityFontStyleAndWeight = StyleKeyword.Null;
                Kicker.style.fontSize = StyleKeyword.Null;
                Label.style.color = StyleKeyword.Null;
                Label.style.unityTextOutlineColor = StyleKeyword.Null;
                Label.style.unityFont = StyleKeyword.Null;
                Label.style.unityFontDefinition = StyleKeyword.Null;
                Label.style.unityFontStyleAndWeight = StyleKeyword.Null;
                Label.style.fontSize = StyleKeyword.Null;
                SetVisible(false);
            }
        }

        private readonly Stack<Item> _inactive = new Stack<Item>();
        private readonly List<Item> _active = new List<Item>();
        private VisualElement _parentLayer;

        private static Sprite _advantageSprite;
        private static Sprite _disadvantageSprite;
        private static Texture2D _advantageTexture;
        private static Texture2D _disadvantageTexture;
        private static bool _spritesLoaded;

        private static readonly string[] KnownKinds =
        {
            "damage", "critical", "blocked", "heal", "status", "passive", "evade", "immunity"
        };

        public int ActiveCount => _active.Count;
        public int InactiveCount => _inactive.Count;
        public int TotalCount => _active.Count + _inactive.Count;
        public FloatingCombatTextSettingsSO Settings { get; set; }

        public void Initialize(VisualElement parentLayer, int initialCapacity = 24, FloatingCombatTextSettingsSO settings = null)
        {
            _parentLayer = parentLayer;
            if (settings != null) Settings = settings;
            LoadSprites();
            if (_parentLayer == null) return;

            // Ensure existing items belong to new parent
            foreach (var item in _inactive)
            {
                if (item.Group.parent != _parentLayer)
                    _parentLayer.Add(item.Group);
            }
            foreach (var item in _active)
            {
                if (item.Group.parent != _parentLayer)
                    _parentLayer.Add(item.Group);
            }

            while (TotalCount < initialCapacity)
            {
                var item = CreateItem();
                _inactive.Push(item);
            }
        }

        private static void LoadSprites()
        {
            if (_spritesLoaded) return;
            _spritesLoaded = true;

            _advantageSprite = Resources.Load<Sprite>("UI/Arrow_Advantage_Red");
            _disadvantageSprite = Resources.Load<Sprite>("UI/Arrow_Disadvantage_Blue");

            _advantageTexture = Resources.Load<Texture2D>("UI/Arrow_Advantage_Red");
            _disadvantageTexture = Resources.Load<Texture2D>("UI/Arrow_Disadvantage_Blue");

#if UNITY_EDITOR
            if (_advantageSprite == null)
                _advantageSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Art/UI/Arrow_Advantage_Red.png");
            if (_disadvantageSprite == null)
                _disadvantageSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Art/UI/Arrow_Disadvantage_Blue.png");
            if (_advantageTexture == null)
                _advantageTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Project/Art/UI/Arrow_Advantage_Red.png");
            if (_disadvantageTexture == null)
                _disadvantageTexture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Project/Art/UI/Arrow_Disadvantage_Blue.png");
#endif

            if (_advantageTexture == null && _advantageSprite != null)
                _advantageTexture = _advantageSprite.texture;
            if (_disadvantageTexture == null && _disadvantageSprite != null)
                _disadvantageTexture = _disadvantageSprite.texture;

            if (_advantageTexture == null && _advantageSprite == null)
                _advantageTexture = CreateProceduralArrow(true);
            if (_disadvantageTexture == null && _disadvantageSprite == null)
                _disadvantageTexture = CreateProceduralArrow(false);
        }

        private static Texture2D CreateProceduralArrow(bool isUp)
        {
            var width = 32;
            var height = 40;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            var fillColor = isUp ? new Color(0.94f, 0.18f, 0.18f, 1f) : new Color(0.18f, 0.58f, 0.98f, 1f);
            var outlineColor = isUp ? new Color(0.48f, 0.04f, 0.04f, 1f) : new Color(0.06f, 0.20f, 0.48f, 1f);
            var clear = new Color(0f, 0f, 0f, 0f);

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var nx = (x - (width / 2f - 0.5f)) / (width / 2f);
                    var ny = isUp ? ((float)y / height) : (1f - ((float)y / height));

                    var inside = false;
                    var onEdge = false;
                    if (ny >= 0.42f)
                    {
                        var headWidth = (1f - ny) / 0.58f;
                        if (Mathf.Abs(nx) <= headWidth)
                        {
                            inside = true;
                            if (Mathf.Abs(nx) >= headWidth - 0.20f || ny >= 0.92f) onEdge = true;
                        }
                    }
                    else
                    {
                        if (Mathf.Abs(nx) <= 0.40f)
                        {
                            inside = true;
                            if (Mathf.Abs(nx) >= 0.26f || ny <= 0.08f) onEdge = true;
                        }
                    }

                    tex.SetPixel(x, y, inside ? (onEdge ? outlineColor : fillColor) : clear);
                }
            }
            tex.Apply();
            return tex;
        }

        private Item CreateItem()
        {
            var group = new VisualElement { pickingMode = PickingMode.Ignore };
            group.AddToClassList("combat-text-group");
            group.style.display = DisplayStyle.None;

            var kicker = new Label { pickingMode = PickingMode.Ignore };
            kicker.AddToClassList("combat-text-kicker");
            kicker.style.display = DisplayStyle.None;
            group.Add(kicker);

            var contentRow = new VisualElement { pickingMode = PickingMode.Ignore };
            contentRow.AddToClassList("combat-text-content");

            var arrow = new VisualElement { pickingMode = PickingMode.Ignore };
            arrow.AddToClassList("combat-text-arrow");
            arrow.style.display = DisplayStyle.None;
            contentRow.Add(arrow);

            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList("combat-text");
            contentRow.Add(label);

            group.Add(contentRow);

            if (_parentLayer != null)
            {
                _parentLayer.Add(group);
            }

            return new Item(group, kicker, contentRow, arrow, label);
        }

        public Item Acquire(string text, string kind, AttributeAffinity affinity = AttributeAffinity.Neutral)
        {
            Item item;
            if (_inactive.Count > 0)
            {
                item = _inactive.Pop();
                if (_parentLayer != null && item.Group.parent != _parentLayer)
                {
                    _parentLayer.Add(item.Group);
                }
            }
            else
            {
                item = CreateItem();
            }

            _active.Add(item);

            // Clean previous kind classes
            foreach (var k in KnownKinds)
            {
                item.Kicker.RemoveFromClassList(k);
                item.Label.RemoveFromClassList(k);
            }

            // Apply new kind class
            item.CurrentKind = kind;
            if (!string.IsNullOrEmpty(kind))
            {
                item.Kicker.AddToClassList(kind);
                item.Label.AddToClassList(kind);
            }

            // Split into kicker and main body if multiline (e.g. "CRITICAL\n36,042")
            var parts = (text ?? string.Empty).Split('\n');
            if (parts.Length > 1)
            {
                item.Kicker.text = parts[0];
                item.Kicker.style.display = DisplayStyle.Flex;
                item.Label.text = parts[parts.Length - 1];
            }
            else
            {
                item.Kicker.text = string.Empty;
                item.Kicker.style.display = DisplayStyle.None;
                item.Label.text = text ?? string.Empty;
            }

            if (Settings != null && Settings.FontAsset != null)
            {
                var fontDefinition = FontDefinition.FromFont(Settings.FontAsset);
                item.Label.style.unityFontDefinition = fontDefinition;
                item.Kicker.style.unityFontDefinition = fontDefinition;
            }
            if (Settings != null)
            {
                item.Label.style.unityFontStyleAndWeight = Settings.FontStyle;
                item.Kicker.style.unityFontStyleAndWeight = Settings.FontStyle;
            }

            if (Settings != null && Settings.OverrideColors)
            {
                Color textColor;
                Color? outlineColor = null;
                switch (kind)
                {
                    case "critical":
                        textColor = Settings.CriticalColor;
                        outlineColor = Settings.CriticalOutlineColor;
                        break;
                    case "blocked":
                        textColor = Settings.BlockedColor;
                        break;
                    case "heal":
                        textColor = Settings.HealColor;
                        break;
                    case "status":
                        textColor = Settings.StatusColor;
                        break;
                    default:
                        textColor = Settings.DamageColor;
                        break;
                }

                item.Label.style.color = textColor;
                if (outlineColor.HasValue)
                    item.Label.style.unityTextOutlineColor = outlineColor.Value;

                if (parts.Length > 1)
                {
                    item.Kicker.style.color = Settings.KickerColor;
                    item.Kicker.style.unityTextOutlineColor = Settings.KickerOutlineColor;
                }
            }

            // Setup attribute affinity arrow for damage hits
            item.CurrentAffinity = affinity;
            var isDamage = kind == "damage" || kind == "critical" || kind == "blocked";
            var advantageSprite = (Settings != null && Settings.AdvantageArrowSprite != null) ? Settings.AdvantageArrowSprite : _advantageSprite;
            var disadvantageSprite = (Settings != null && Settings.DisadvantageArrowSprite != null) ? Settings.DisadvantageArrowSprite : _disadvantageSprite;

            if (isDamage && affinity == AttributeAffinity.Advantage)
            {
                item.Arrow.RemoveFromClassList("disadvantage");
                item.Arrow.AddToClassList("advantage");
                item.Arrow.style.display = DisplayStyle.Flex;
                if (advantageSprite != null)
                    item.Arrow.style.backgroundImage = new StyleBackground(advantageSprite);
                else if (_advantageTexture != null)
                    item.Arrow.style.backgroundImage = new StyleBackground(_advantageTexture);
            }
            else if (isDamage && affinity == AttributeAffinity.Disadvantage)
            {
                item.Arrow.RemoveFromClassList("advantage");
                item.Arrow.AddToClassList("disadvantage");
                item.Arrow.style.display = DisplayStyle.Flex;
                if (disadvantageSprite != null)
                    item.Arrow.style.backgroundImage = new StyleBackground(disadvantageSprite);
                else if (_disadvantageTexture != null)
                    item.Arrow.style.backgroundImage = new StyleBackground(_disadvantageTexture);
            }
            else
            {
                item.Arrow.RemoveFromClassList("advantage");
                item.Arrow.RemoveFromClassList("disadvantage");
                item.Arrow.style.backgroundImage = StyleKeyword.Null;
                item.Arrow.style.display = DisplayStyle.None;
            }

            var arrowSize = Settings != null ? Settings.GetArrowSize(kind) : new Vector2(kind == "critical" ? 30f : (kind == "blocked" ? 21f : 26f), kind == "critical" ? 38f : (kind == "blocked" ? 26f : 32f));
            item.Arrow.style.width = arrowSize.x;
            item.Arrow.style.height = arrowSize.y;
            item.Arrow.style.marginRight = Settings != null ? Settings.ArrowRightMargin : 6f;

            item.Label.style.fontSize = CalculateFontSize(item.Label.text, kind, Settings);
            if (parts.Length > 1 && Settings != null)
                item.Kicker.style.fontSize = Settings.KickerFontSize;

            item.SetVisible(true);
            return item;
        }

        public void Release(Item item)
        {
            if (item == null) return;
            if (_active.Remove(item))
            {
                item.ResetStyle();
                _inactive.Push(item);
            }
        }

        public void Clear()
        {
            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var item = _active[i];
                item.ResetStyle();
                _inactive.Push(item);
            }
            _active.Clear();
        }

        public static float CalculateFontSize(string text, string kind, FloatingCombatTextSettingsSO settings = null)
        {
            var baseSize = settings != null ? settings.GetFontSize(kind) :
                           kind == "critical" ? 46f :
                           (kind == "blocked" || kind == "evade" || kind == "immunity") ? 34f :
                           kind == "heal" ? 38f :
                           kind == "passive" ? 23f :
                           kind == "status" ? 21f : 40f;
            var longestLine = 1;
            foreach (var line in (text ?? string.Empty).Split('\n'))
                longestLine = Mathf.Max(longestLine, line.Length);
            return Mathf.Clamp(Mathf.Min(baseSize, 320f / (longestLine * .60f)), 16f, baseSize);
        }
    }
}
