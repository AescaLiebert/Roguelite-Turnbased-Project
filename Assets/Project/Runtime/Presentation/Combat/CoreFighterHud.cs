using System.Collections;
using System.Collections.Generic;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using UnityEngine;
using UnityEngine.UI;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>Core adapter for the authored UnitUI prefab.</summary>
    public sealed class CoreFighterHud : MonoBehaviour
    {
        [SerializeField] private Slider healthSlider;
        [SerializeField] private Slider powerGaugeSlider;
        [SerializeField] private WorldBillboardFollower followScript;

        private Image _powerGaugeFill;
        private Image _shieldFill;
        private GameObject _shieldRoot;
        private GameObject _statusPanel;
        private GameObject _statusTemplate;

        private sealed class PGSegment
        {
            public GameObject Root;
            public RectTransform Rect;
            public Image FillImage;
            public Coroutine Routine;
            public bool IsTrue;
            public bool IsDraft;
        }

        private sealed class StatusViewEntry
        {
            public string InstanceId;
            public string RecipeId;
            public GameObject Root;
            public RectTransform Rect;
            public Image IconImage;
            public Text StackText;
            public int StackCount;
            public Coroutine Routine;
        }

        private readonly PGSegment[] _pgSegments = new PGSegment[5];
        private int _currentTruePG;
        private int _currentPreviewPG;
        private bool _pgInitialized;
        private Color _pgBaseColor = new Color(1f, 0.59f, 0f, 1f);
        private readonly Color _pgFlashColor = Color.white;
        private readonly Dictionary<string, StatusViewEntry> _statusViews = new Dictionary<string, StatusViewEntry>();
        private static readonly Dictionary<string, Sprite> _statusSpriteCache = new Dictionary<string, Sprite>();

        private void Awake()
        {
            if (followScript == null) followScript = GetComponent<WorldBillboardFollower>();
            BindAuthoredUnitUi();
            EnsureSegments();
        }

        private void Update()
        {
            UpdateDraftFlicker();
        }

        private void OnDisable()
        {
            for (var i = 0; i < 5; i++)
            {
                if (_pgSegments[i]?.Routine != null)
                {
                    StopCoroutine(_pgSegments[i].Routine);
                    _pgSegments[i].Routine = null;
                }
            }

            foreach (var entry in _statusViews.Values)
            {
                if (entry.Routine != null)
                {
                    StopCoroutine(entry.Routine);
                    entry.Routine = null;
                }
            }
        }

        public void InitializeCore(Transform target, string fighterName, int currentHealth, int maxHealth,
            int shield, int powerGauge)
        {
            if (followScript == null) followScript = GetComponent<WorldBillboardFollower>();
            if (followScript == null) followScript = gameObject.AddComponent<WorldBillboardFollower>();
            followScript.SetTarget(target);
            BindAuthoredUnitUi();
            EnsureSegments();
            SetCoreHealth(currentHealth, maxHealth);
            SetShield(shield, maxHealth);
            SetPowerGauge(powerGauge);
        }

        public void SetCoreHealth(int currentHealth, int maxHealth)
        {
            if (healthSlider == null) return;
            healthSlider.maxValue = Mathf.Max(1, maxHealth);
            healthSlider.value = Mathf.Clamp(currentHealth, 0, healthSlider.maxValue);
        }

        public void SetPowerGauge(int powerGauge) => SetPowerGauge(powerGauge, -1);

        public void SetPowerGauge(int truePowerGauge, int previewPowerGauge)
        {
            truePowerGauge = Mathf.Clamp(truePowerGauge, 0, 5);
            var effectivePreview = previewPowerGauge >= 0 ? Mathf.Clamp(previewPowerGauge, 0, 5) : truePowerGauge;

            if (powerGaugeSlider != null)
            {
                powerGaugeSlider.minValue = 0;
                powerGaugeSlider.maxValue = 5;
                powerGaugeSlider.wholeNumbers = true;
                powerGaugeSlider.SetValueWithoutNotify(0);
                powerGaugeSlider.SetValueWithoutNotify(effectivePreview);
            }

            EnsureSegments();

            if (!_pgInitialized)
            {
                _pgInitialized = true;
                _currentTruePG = truePowerGauge;
                _currentPreviewPG = effectivePreview;
                ApplyImmediateVisuals(truePowerGauge, effectivePreview);
                return;
            }

            var oldTrue = _currentTruePG;
            _currentTruePG = truePowerGauge;
            _currentPreviewPG = effectivePreview;

            // Handle True PG gains (Enter animation: flash white, scale down a bit)
            if (truePowerGauge > oldTrue)
            {
                for (var i = oldTrue; i < truePowerGauge; i++)
                {
                    var seg = _pgSegments[i];
                    if (seg == null) continue;
                    if (seg.Routine != null) StopCoroutine(seg.Routine);
                    seg.Routine = StartCoroutine(AnimateEnter(seg));
                }
            }
            // Handle True PG losses (Exit animation: slowly scale up a bit, flash white, remove)
            else if (truePowerGauge < oldTrue)
            {
                for (var i = truePowerGauge; i < oldTrue; i++)
                {
                    var seg = _pgSegments[i];
                    if (seg == null) continue;
                    if (seg.Routine != null) StopCoroutine(seg.Routine);
                    seg.Routine = StartCoroutine(AnimateExit(seg));
                }
            }

            // Existing True PG segments below min(oldTrue, truePowerGauge) that aren't animating
            for (var i = 0; i < Mathf.Min(oldTrue, truePowerGauge); i++)
            {
                var seg = _pgSegments[i];
                if (seg == null || seg.Routine != null) continue;
                seg.IsTrue = true;
                seg.IsDraft = false;
                seg.Root.SetActive(true);
                seg.Rect.localScale = Vector3.one;
                seg.FillImage.color = _pgBaseColor;
            }

            // Handle draft preview segments (slowly flicker in draft state)
            if (effectivePreview > truePowerGauge)
            {
                for (var i = truePowerGauge; i < effectivePreview; i++)
                {
                    var seg = _pgSegments[i];
                    if (seg == null) continue;
                    if (seg.Routine != null)
                    {
                        StopCoroutine(seg.Routine);
                        seg.Routine = null;
                    }
                    seg.IsTrue = false;
                    seg.IsDraft = true;
                    seg.Root.SetActive(true);
                    seg.Rect.localScale = Vector3.one;
                }
            }
            else if (effectivePreview < truePowerGauge)
            {
                for (var i = effectivePreview; i < truePowerGauge; i++)
                {
                    var seg = _pgSegments[i];
                    if (seg == null || seg.Routine != null) continue;
                    seg.IsDraft = true;
                }
            }

            // Inactive segments (above max of true and preview, not currently animating exit)
            var highestActive = Mathf.Max(truePowerGauge, effectivePreview);
            for (var i = highestActive; i < 5; i++)
            {
                var seg = _pgSegments[i];
                if (seg == null || seg.Routine != null) continue;
                seg.IsTrue = false;
                seg.IsDraft = false;
                seg.Root.SetActive(false);
            }
        }

        private void UpdateDraftFlicker()
        {
            var hasDraft = false;
            for (var i = 0; i < 5; i++)
            {
                var s = _pgSegments[i];
                if (s != null && s.IsDraft)
                {
                    hasDraft = true;
                    break;
                }
            }
            if (!hasDraft) return;

            var wave = (Mathf.Sin(Time.unscaledTime * 4.0f) + 1f) * 0.5f;
            var alpha = Mathf.Lerp(0.05f, 0.95f, wave);
            var tint = Color.Lerp(_pgBaseColor, new Color(1f, 0.95f, 0.8f, 1f), 0.25f);
            var draftColor = new Color(tint.r, tint.g, tint.b, alpha);

            for (var i = 0; i < 5; i++)
            {
                var s = _pgSegments[i];
                if (s != null && s.IsDraft && s.FillImage != null && s.Routine == null)
                {
                    s.FillImage.color = draftColor;
                }
            }
        }

        private IEnumerator AnimateEnter(PGSegment seg)
        {
            seg.IsTrue = true;
            seg.IsDraft = false;
            seg.Root.SetActive(true);
            seg.Root.transform.SetAsLastSibling();
            seg.Rect.localScale = Vector3.one * 1.35f;
            seg.FillImage.color = _pgFlashColor;

            var duration = 0.30f;
            var elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);

                var easeOut = Mathf.Sin(t * Mathf.PI * 0.5f);
                var scale = Mathf.Lerp(1.35f, 1f, easeOut);
                seg.Rect.localScale = Vector3.one * scale;

                Color color;
                if (t < 0.25f)
                {
                    color = _pgFlashColor;
                }
                else
                {
                    var colorT = (t - 0.25f) / 0.75f;
                    color = Color.Lerp(_pgFlashColor, _pgBaseColor, colorT);
                }
                seg.FillImage.color = color;

                yield return null;
            }

            seg.Rect.localScale = Vector3.one;
            seg.FillImage.color = _pgBaseColor;
            seg.Routine = null;
        }

        private IEnumerator AnimateExit(PGSegment seg)
        {
            seg.IsTrue = false;
            seg.IsDraft = false;
            seg.Root.SetActive(true);
            seg.Root.transform.SetAsLastSibling();

            var duration = 0.40f;
            var elapsed = 0f;
            var startColor = seg.FillImage.color;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);

                var scale = Mathf.Lerp(1f, 1.35f, t);
                seg.Rect.localScale = Vector3.one * scale;

                Color color;
                if (t < 0.35f)
                {
                    color = Color.Lerp(startColor, _pgFlashColor, t / 0.35f);
                }
                else
                {
                    var fadeT = (t - 0.35f) / 0.65f;
                    color = new Color(_pgFlashColor.r, _pgFlashColor.g, _pgFlashColor.b, 1f - fadeT);
                }
                seg.FillImage.color = color;

                yield return null;
            }

            seg.Rect.localScale = Vector3.one;
            seg.Root.SetActive(false);
            seg.Routine = null;
        }

        private void ApplyImmediateVisuals(int truePG, int previewPG)
        {
            for (var i = 0; i < 5; i++)
            {
                var seg = _pgSegments[i];
                if (seg == null) continue;
                if (seg.Routine != null)
                {
                    StopCoroutine(seg.Routine);
                    seg.Routine = null;
                }
                seg.Rect.localScale = Vector3.one;
                if (i < truePG)
                {
                    seg.IsTrue = true;
                    seg.IsDraft = false;
                    seg.Root.SetActive(true);
                    seg.FillImage.color = _pgBaseColor;
                }
                else if (i < previewPG)
                {
                    seg.IsTrue = false;
                    seg.IsDraft = true;
                    seg.Root.SetActive(true);
                    seg.FillImage.color = _pgBaseColor;
                }
                else
                {
                    seg.IsTrue = false;
                    seg.IsDraft = false;
                    seg.Root.SetActive(false);
                }
            }
        }

        private void EnsureSegments()
        {
            if (_pgSegments[0] != null) return;
            if (powerGaugeSlider == null && _powerGaugeFill == null) return;

            var parent = _powerGaugeFill != null ? _powerGaugeFill.transform.parent : powerGaugeSlider?.transform;
            if (parent == null) return;

            if (_powerGaugeFill != null)
            {
                _pgBaseColor = _powerGaugeFill.color;
                _powerGaugeFill.enabled = false;
            }

            for (var i = 0; i < 5; i++)
            {
                var segName = $"PGSegment_{i}";
                var existing = parent.Find(segName);
                GameObject segGo;
                if (existing != null)
                {
                    segGo = existing.gameObject;
                }
                else
                {
                    segGo = new GameObject(segName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    segGo.transform.SetParent(parent, false);
                }

                var rect = segGo.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(i / 5f, 0f);
                rect.anchorMax = new Vector2((i + 1) / 5f, 1f);
                rect.offsetMin = new Vector2(1.5f, 0.5f);
                rect.offsetMax = new Vector2(-1.5f, -0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.localScale = Vector3.one;

                var img = segGo.GetComponent<Image>();
                if (_powerGaugeFill != null)
                {
                    img.sprite = _powerGaugeFill.sprite;
                    img.material = _powerGaugeFill.material;
                }
                img.type = Image.Type.Simple;
                img.color = _pgBaseColor;
                img.raycastTarget = false;

                _pgSegments[i] = new PGSegment
                {
                    Root = segGo,
                    Rect = rect,
                    FillImage = img,
                    IsTrue = false,
                    IsDraft = false
                };
                segGo.SetActive(false);
            }
        }

        public void SetShield(int shield, int maxHealth)
        {
            if (_shieldRoot != null) _shieldRoot.SetActive(shield > 0);
            if (_shieldFill == null) return;
            _shieldFill.type = Image.Type.Filled;
            _shieldFill.fillMethod = Image.FillMethod.Horizontal;
            _shieldFill.fillOrigin = 0;
            _shieldFill.fillAmount = Mathf.Clamp01(shield / (float)Mathf.Max(1, maxHealth));
        }

        public void SetStatuses(IReadOnlyList<StatusInstance> statuses)
        {
            if (_statusPanel == null && _statusTemplate == null) return;

            if (statuses == null || statuses.Count == 0)
            {
                foreach (var entry in _statusViews.Values)
                {
                    if (entry.Routine != null) StopCoroutine(entry.Routine);
                    if (entry.Root != null) Destroy(entry.Root);
                }
                _statusViews.Clear();
                if (_statusPanel != null) _statusPanel.SetActive(false);
                return;
            }

            if (_statusPanel != null && !_statusPanel.activeSelf)
                _statusPanel.SetActive(true);

            var currentIds = new HashSet<string>();
            for (var i = 0; i < statuses.Count; i++)
            {
                var status = statuses[i];
                if (status == null || string.IsNullOrEmpty(status.InstanceId)) continue;
                var id = status.InstanceId;
                currentIds.Add(id);

                if (_statusViews.TryGetValue(id, out var entry) && entry != null && entry.Root != null)
                {
                    if (entry.StackCount != status.StackCount)
                    {
                        entry.StackCount = status.StackCount;
                        UpdateStatusStack(entry, status.StackCount);
                        if (entry.Routine != null) StopCoroutine(entry.Routine);
                        entry.Routine = StartCoroutine(AnimateStatusPulse(entry.Rect));
                    }
                }
                else
                {
                    var newEntry = CreateStatusView(status);
                    if (newEntry != null)
                    {
                        _statusViews[id] = newEntry;
                        newEntry.Routine = StartCoroutine(AnimateStatusEnter(newEntry.Rect));
                    }
                }
            }

            var toRemove = new List<string>();
            foreach (var pair in _statusViews)
            {
                if (!currentIds.Contains(pair.Key))
                    toRemove.Add(pair.Key);
            }

            foreach (var id in toRemove)
            {
                var entry = _statusViews[id];
                _statusViews.Remove(id);
                if (entry != null && entry.Root != null)
                {
                    if (entry.Routine != null) StopCoroutine(entry.Routine);
                    StartCoroutine(AnimateStatusExit(entry.Root, entry.Rect));
                }
            }
        }

        private StatusViewEntry CreateStatusView(StatusInstance status)
        {
            GameObject obj = null;
            if (_statusTemplate != null)
            {
                obj = Instantiate(_statusTemplate, _statusPanel != null ? _statusPanel.transform : transform);
            }
            else if (_statusPanel != null)
            {
                obj = new GameObject("StatusIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                obj.transform.SetParent(_statusPanel.transform, false);
            }
            if (obj == null) return null;

            obj.SetActive(true);
            obj.name = "Status_" + (status.RecipeId ?? "Unknown");

            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(20f, 20f);
            rect.localScale = Vector3.one;

            var image = obj.GetComponent<Image>();
            if (image == null) image = obj.AddComponent<Image>();
            image.enabled = true;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var polarity = status.Recipe?.Polarity ?? StatusPolarity.Debuff;
            image.sprite = ResolveStatusSprite(status.RecipeId, polarity);

            var entry = new StatusViewEntry
            {
                InstanceId = status.InstanceId,
                RecipeId = status.RecipeId,
                Root = obj,
                Rect = rect,
                IconImage = image,
                StackCount = status.StackCount
            };

            UpdateStatusStack(entry, status.StackCount);
            return entry;
        }

        private static void UpdateStatusStack(StatusViewEntry entry, int stackCount)
        {
            if (entry == null || entry.Root == null) return;
            if (stackCount > 1)
            {
                if (entry.StackText == null)
                {
                    var textObj = new GameObject("StackCount", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                    textObj.transform.SetParent(entry.Root.transform, false);
                    var tRect = textObj.GetComponent<RectTransform>();
                    tRect.anchorMin = Vector2.zero;
                    tRect.anchorMax = Vector2.one;
                    tRect.offsetMin = new Vector2(2f, -2f);
                    tRect.offsetMax = new Vector2(2f, -2f);
                    tRect.pivot = new Vector2(1f, 0f);

                    var text = textObj.GetComponent<Text>();
                    text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                    text.fontSize = 10;
                    text.fontStyle = FontStyle.Bold;
                    text.alignment = TextAnchor.LowerRight;
                    text.color = new Color(1f, 0.95f, 0.65f, 1f);
                    text.raycastTarget = false;
                    entry.StackText = text;
                }
                entry.StackText.text = stackCount.ToString();
                entry.StackText.gameObject.SetActive(true);
            }
            else if (entry.StackText != null)
            {
                entry.StackText.gameObject.SetActive(false);
            }
        }

        private IEnumerator AnimateStatusEnter(RectTransform rect)
        {
            if (rect == null) yield break;
            var duration = 0.22f;
            var elapsed = 0f;
            var initialPos = rect.localPosition;
            var startOffset = new Vector3(14f, 4f, 0f);

            rect.localScale = Vector3.one * 0.3f;
            rect.localPosition = initialPos + startOffset;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);

                var slideT = Mathf.Sin(t * Mathf.PI * 0.5f);
                rect.localPosition = Vector3.Lerp(initialPos + startOffset, initialPos, slideT);

                float scale;
                if (t < 0.65f)
                    scale = Mathf.Lerp(0.3f, 1.25f, t / 0.65f);
                else
                    scale = Mathf.Lerp(1.25f, 1f, (t - 0.65f) / 0.35f);
                rect.localScale = Vector3.one * scale;

                yield return null;
            }

            rect.localPosition = initialPos;
            rect.localScale = Vector3.one;
        }

        private IEnumerator AnimateStatusPulse(RectTransform rect)
        {
            if (rect == null) yield break;
            var duration = 0.18f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.28f;
                rect.localScale = Vector3.one * scale;
                yield return null;
            }
            rect.localScale = Vector3.one;
        }

        private IEnumerator AnimateStatusExit(GameObject root, RectTransform rect)
        {
            if (root == null) yield break;
            var duration = 0.16f;
            var elapsed = 0f;
            var startScale = rect != null ? rect.localScale : Vector3.one;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                if (rect != null)
                {
                    rect.localScale = Vector3.Lerp(startScale, Vector3.zero, t * t);
                }
                yield return null;
            }
            Destroy(root);
        }

        private static Sprite ResolveStatusSprite(string recipeId, StatusPolarity polarity)
        {
            var lower = (recipeId ?? string.Empty).ToLowerInvariant();
            string key;
            if (lower.Contains("ignite") || lower.Contains("bleed") || lower.Contains("poison") || lower.Contains("shock") || lower.Contains("dot"))
                key = "Cardtype_Debuffatk";
            else if (lower.Contains("attack") && polarity == StatusPolarity.Buff)
                key = "sword";
            else if (lower.Contains("defense") && polarity == StatusPolarity.Buff)
                key = "shield";
            else if ((lower.Contains("hp") || lower.Contains("heal") || lower.Contains("recovery")) && polarity == StatusPolarity.Buff)
                key = "heart";
            else if (polarity == StatusPolarity.Buff)
                key = "Cardtype_buff";
            else
                key = "Cardtype_Debuff";

            if (_statusSpriteCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var sprite = LoadSpriteByName(key);
            if (sprite != null)
                _statusSpriteCache[key] = sprite;
            return sprite;
        }

        private static Sprite LoadSpriteByName(string name)
        {
#if UNITY_EDITOR
            var edSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Project/Art/UI/" + name + ".png");
            if (edSprite != null) return edSprite;
#endif
            var res = Resources.Load<Sprite>("UI/" + name);
            if (res != null) return res;

            var filePath = System.IO.Path.Combine(Application.dataPath, "Project", "Art", "UI", name + ".png");
            if (System.IO.File.Exists(filePath))
            {
                try
                {
                    var bytes = System.IO.File.ReadAllBytes(filePath);
                    var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes))
                    {
                        return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                    }
                }
                catch { }
            }
            return null;
        }

        private void BindAuthoredUnitUi()
        {
            var unitUi = transform.Find("UnitUI");
            if (unitUi != null)
            {
                unitUi.gameObject.SetActive(true);
                if (healthSlider == null) healthSlider = unitUi.Find("HealthBar")?.GetComponent<Slider>();
                var shield = unitUi.Find("HealthPanel/Ex-Healthbar");
                _shieldRoot = shield == null ? null : shield.gameObject;
                _shieldFill = shield?.Find("Fill Area/Fill")?.GetComponent<Image>();

                var panel = unitUi.Find("Buff/DebuffPanel") ?? transform.Find("Buff/DebuffPanel");
                if (panel != null)
                {
                    _statusPanel = panel.gameObject;
                    var t = panel.Find("Image");
                    if (t != null)
                    {
                        _statusTemplate = t.gameObject;
                        _statusTemplate.SetActive(false);
                    }
                }
            }

            if (powerGaugeSlider == null)
            {
                var pg = transform.Find("UI/Healthbar/PG")
                    ?? unitUi?.Find("HealthBar/GaugePanel/GaugeSlider")
                    ?? unitUi?.Find("GaugePanel/GaugeSlider");
                powerGaugeSlider = pg?.GetComponent<Slider>() ?? pg?.GetComponentInChildren<Slider>(true);
            }
            _powerGaugeFill = powerGaugeSlider?.fillRect?.GetComponent<Image>();
            if (_powerGaugeFill == null)
                _powerGaugeFill = powerGaugeSlider?.transform.Find("Fill Area/Fill")?.GetComponent<Image>();
            if (powerGaugeSlider != null)
            {
                powerGaugeSlider.interactable = false;
                if (_powerGaugeFill != null)
                {
                    powerGaugeSlider.fillRect = null;
                    var fillRect = _powerGaugeFill.rectTransform;
                    fillRect.anchorMin = Vector2.zero;
                    fillRect.anchorMax = Vector2.zero;
                    fillRect.offsetMin = Vector2.zero;
                    fillRect.offsetMax = Vector2.zero;
                    _powerGaugeFill.type = Image.Type.Simple;
                }
            }
            if (powerGaugeSlider == null || _powerGaugeFill == null)
                Debug.LogError("Power Gauge Slider is unassigned or has no fill image. Assign the PG Slider in Core Fighter Hud.", this);
        }
    }
}
