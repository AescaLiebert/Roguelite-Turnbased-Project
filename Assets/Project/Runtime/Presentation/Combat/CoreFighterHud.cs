using System;
using System.Collections;
using System.Collections.Generic;
using FightingAllstar.Core.Combat;
using FightingAllstar.Core.Content;
using TMPro;
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

        [Header("Level Handle & Attribute Visuals")]
        [SerializeField] private Image levelHandleFill;
        [SerializeField] private Image levelHandleBorder;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private bool tintBorderWithAttribute = false;

        [Header("7 Attribute Colors")]
        [SerializeField] private Color blueAttributeColor = new Color(0.145f, 0.388f, 0.922f, 1f);     // #2563EB Blue
        [SerializeField] private Color redAttributeColor = new Color(0.863f, 0.149f, 0.149f, 1f);      // #DC2626 Red
        [SerializeField] private Color greenAttributeColor = new Color(0.086f, 0.639f, 0.290f, 1f);    // #16A34A Green
        [SerializeField] private Color yellowAttributeColor = new Color(0.918f, 0.702f, 0.031f, 1f);   // #EAB308 Yellow
        [SerializeField] private Color darknessAttributeColor = new Color(0.231f, 0.110f, 0.329f, 1f); // #3B1C54 Darkness
        [SerializeField] private Color lightAttributeColor = new Color(0.996f, 0.941f, 0.541f, 1f);    // #FEF08A Light
        [SerializeField] private Color infinityAttributeColor = new Color(0.92f, 0.97f, 1f, 1f);       // White Crystal

        [Header("Status Visuals")]
        [SerializeField] private GameObject statusIconPrefab;

        private Text _levelTextFallback;

        private Image _powerGaugeFill;
        private Image _shieldFill;
        private Slider _shieldSlider;
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
            public int StackIndex;
            public int StackCount;
            public GameObject Root;
            public RectTransform Rect;
            public Image IconImage;
            public TMP_Text StackText;
            public Image CooldownOverlay;
            public int RemainingDuration;
            public int MaxDuration;
            public bool IsPermanent;
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

        public void SetPowerGaugeVisible(bool visible)
        {
            if (powerGaugeSlider != null) powerGaugeSlider.gameObject.SetActive(visible);
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
                ResetStatusFeedbackVisual(entry);
            }
        }

        public void InitializeCore(Transform target, string fighterName, int currentHealth, int maxHealth,
            int shield, int powerGauge) => InitializeCore(target, fighterName, currentHealth, maxHealth, shield, powerGauge, null, 1);

        public void InitializeCore(Transform target, string fighterName, int currentHealth, int maxHealth,
            int shield, int powerGauge, string attributeId, int level = 1)
        {
            if (followScript == null) followScript = GetComponent<WorldBillboardFollower>();
            if (followScript == null) followScript = gameObject.AddComponent<WorldBillboardFollower>();
            followScript.SetTarget(target);
            BindAuthoredUnitUi();
            EnsureSegments();
            SetCoreHealth(currentHealth, maxHealth);
            SetShield(shield, maxHealth);
            SetPowerGauge(powerGauge);
            if (!string.IsNullOrEmpty(attributeId)) SetAttribute(attributeId);
            if (level > 0) SetLevel(level);
        }

        public Color GetAttributeColor(string attributeId)
        {
            if (string.IsNullOrEmpty(attributeId)) return Color.white;
            var lower = attributeId.ToLowerInvariant().Replace("attribute.", string.Empty).Trim();

            if (lower.Contains("blue")) return blueAttributeColor;
            if (lower.Contains("red")) return redAttributeColor;
            if (lower.Contains("green")) return greenAttributeColor;
            if (lower.Contains("yellow") || lower.Contains("gold")) return yellowAttributeColor;
            if (lower.Contains("darkness") || lower.Contains("dark") || lower.Contains("shadow") || lower.Contains("purple") || lower.Contains("void")) return darknessAttributeColor;
            if (lower.Contains("light") || lower.Contains("holy") || lower.Contains("sun")) return lightAttributeColor;
            if (lower.Contains("infinity") || lower.Contains("crystal")) return infinityAttributeColor;

            return Color.white;
        }

        public void SetAttribute(string attributeId) => SetAttributeColor(GetAttributeColor(attributeId));
        public void SetAttribute(FighterAttribute attribute) => SetAttributeColor(GetAttributeColor(attribute.ToString()));

        public void SetAttributeColor(Color color)
        {
            if (levelHandleFill != null)
            {
                levelHandleFill.color = color;
            }

            if (levelHandleBorder != null && tintBorderWithAttribute)
            {
                levelHandleBorder.color = Color.Lerp(color, Color.black, 0.45f);
            }

            if (levelText != null)
            {
                var luminance = 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
                levelText.color = luminance > 0.65f ? Color.black : Color.white;
            }
            else if (_levelTextFallback != null)
            {
                var luminance = 0.299f * color.r + 0.587f * color.g + 0.114f * color.b;
                _levelTextFallback.color = luminance > 0.65f ? Color.black : Color.white;
            }
        }

        public void SetLevel(int level)
        {
            var textVal = level > 0 ? level.ToString() : "1";
            if (levelText != null) levelText.text = textVal;
            if (_levelTextFallback != null) _levelTextFallback.text = textVal;
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

            // Opening/cinematic HUDs can be hidden while playback updates their gauge.
            if (!_pgInitialized || !isActiveAndEnabled)
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
            var safeMaximum = Mathf.Max(1, maxHealth);
            if (_shieldSlider != null)
            {
                _shieldSlider.minValue = 0;
                _shieldSlider.maxValue = safeMaximum;
                _shieldSlider.SetValueWithoutNotify(Mathf.Clamp(shield, 0, safeMaximum));
            }
            if (_shieldFill == null) return;
            _shieldFill.fillAmount = Mathf.Clamp01(shield / (float)safeMaximum);
        }

        public void SetStatuses(IReadOnlyList<StatusInstance> statuses)
        {
            if (_statusPanel == null && _statusTemplate == null)
            {
                BindAuthoredUnitUi();
            }
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
            var seenBaseIds = new HashSet<string>();
            var recipeStackCounts = new Dictionary<string, int>();
            // GridLayoutGroup follows sibling order. Keep each category stable in source order.
            var orderedViews = new List<StatusViewEntry>[4]
            {
                new List<StatusViewEntry>(), new List<StatusViewEntry>(),
                new List<StatusViewEntry>(), new List<StatusViewEntry>()
            };

            for (var i = 0; i < statuses.Count; i++)
            {
                var status = statuses[i];
                if (status == null) continue;

                var recipeId = !string.IsNullOrEmpty(status.RecipeId)
                    ? status.RecipeId
                    : (!string.IsNullOrEmpty(status.InstanceId) ? status.InstanceId : "unknown");

                var maxStacks = (status.Recipe != null && status.Recipe.MaxStacks > 0)
                    ? status.Recipe.MaxStacks
                    : 10;

                recipeStackCounts.TryGetValue(recipeId, out var currentRecipeStacks);
                if (currentRecipeStacks >= maxStacks)
                {
                    continue;
                }

                var isPermanent = status.Recipe != null && status.Recipe.DurationClock == StatusDurationClock.Permanent;
                var remDur = isPermanent ? int.MaxValue : Mathf.Max(0, status.RemainingDuration);
                int maxDur;
                if (isPermanent)
                {
                    maxDur = int.MaxValue;
                }
                else if (status.RemainingDuration > 0)
                {
                    maxDur = status.RemainingDuration;
                }
                else
                {
                    maxDur = status.Recipe?.DefaultDuration > 0 ? status.Recipe.DefaultDuration : 2;
                }

                var isIndependent = status.Recipe != null && status.Recipe.Stacking == StatusStackingPolicy.IndependentStacks;
                var instanceStacks = isIndependent ? 1 : Mathf.Max(1, status.StackCount);
                var allowedStacks = Mathf.Min(instanceStacks, maxStacks - currentRecipeStacks);

                var rawId = !string.IsNullOrEmpty(status.InstanceId) ? status.InstanceId : recipeId;
                var baseId = seenBaseIds.Add(rawId) ? rawId : $"{rawId}_{i}";
                var isGrey = status.Recipe != null && status.Recipe.Color == StatusColor.Grey;
                var isBuff = status.Recipe != null && status.Recipe.Polarity == StatusPolarity.Buff;
                var category = (isGrey ? 0 : 2) + (isBuff ? 0 : 1);

                for (var s = 0; s < allowedStacks; s++)
                {
                    var stackIndex = currentRecipeStacks + s;
                    var stackKey = $"{baseId}_{s}";
                    currentIds.Add(stackKey);

                    if (_statusViews.TryGetValue(stackKey, out var entry) && entry != null && entry.Root != null)
                    {
                        entry.RemainingDuration = remDur;
                        if (remDur > entry.MaxDuration && !isPermanent) entry.MaxDuration = remDur;
                        entry.IsPermanent = isPermanent;
                        UpdateDurationFill(entry, remDur, entry.MaxDuration);
                        orderedViews[category].Add(entry);
                    }
                    else
                    {
                        var newEntry = CreateStatusView(status, stackIndex);
                        if (newEntry != null)
                        {
                            _statusViews[stackKey] = newEntry;
                            orderedViews[category].Add(newEntry);
                            newEntry.Routine = StartCoroutine(AnimateStatusEnter(newEntry.Rect));
                        }
                    }
                }

                recipeStackCounts[recipeId] = currentRecipeStacks + allowedStacks;
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
                    // The exit animation must not occupy a grid cell while survivors close the gap.
                    if (_statusPanel != null)
                        entry.Root.transform.SetParent(_statusPanel.transform.parent, true);
                    StartCoroutine(AnimateStatusExit(entry.Root, entry.Rect));
                }
            }

            if (_statusPanel != null)
            {
                var siblingIndex = 0;
                foreach (var category in orderedViews)
                    foreach (var entry in category)
                        entry.Root.transform.SetSiblingIndex(siblingIndex++);

                var panelRect = _statusPanel.GetComponent<RectTransform>();
                if (panelRect != null)
                {
                    LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
                }
            }
        }

        public RectTransform CreateInspectorStatusIcon(StatusInstance status, Transform parent)
        {
            var entry = CreateStatusView(status, 0);
            if (entry == null) return null;
            entry.Root.transform.SetParent(parent, false);
            var duration = Math.Max(status.RemainingDuration, status.Recipe?.DefaultDuration ?? 1);
            foreach (var live in _statusViews.Values)
                if (!string.IsNullOrEmpty(status.InstanceId) && live.InstanceId == status.InstanceId)
                { duration = live.MaxDuration; break; }
            UpdateDurationFill(entry, status.RemainingDuration, duration);
            return entry.Rect;
        }

        public void PlayStatusRefreshFeedback(string recipeId, bool succeeded)
        {
            if (string.IsNullOrEmpty(recipeId)) return;
            foreach (var entry in _statusViews.Values)
            {
                if (entry == null || entry.Root == null ||
                    !string.Equals(entry.RecipeId, recipeId, StringComparison.OrdinalIgnoreCase)) continue;
                if (entry.Routine != null) StopCoroutine(entry.Routine);
                ResetStatusFeedbackVisual(entry);
                entry.Routine = StartCoroutine(AnimateStatusRefresh(entry, succeeded));
            }
        }

        private static void ResetStatusFeedbackVisual(StatusViewEntry entry)
        {
            if (entry?.Rect != null)
            {
                entry.Rect.localScale = Vector3.one;
                entry.Rect.localRotation = Quaternion.identity;
            }
            if (entry?.IconImage != null) entry.IconImage.color = Color.white;
        }

        private StatusViewEntry CreateStatusView(StatusInstance status, int stackIndex)
        {
            GameObject obj = null;
            if (_statusTemplate != null)
            {
                obj = Instantiate(_statusTemplate, _statusPanel != null ? _statusPanel.transform : transform);
            }
            else if (statusIconPrefab != null)
            {
                obj = Instantiate(statusIconPrefab, _statusPanel != null ? _statusPanel.transform : transform);
            }
            else if (_statusPanel != null)
            {
                obj = new GameObject("StatusIcon", typeof(RectTransform), typeof(CanvasRenderer));
                obj.transform.SetParent(_statusPanel.transform, false);

                var iconObj = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                iconObj.transform.SetParent(obj.transform, false);
                var iconR = iconObj.GetComponent<RectTransform>();
                iconR.anchorMin = Vector2.zero;
                iconR.anchorMax = Vector2.one;
                iconR.sizeDelta = Vector2.zero;

                var cdObj = new GameObject("Cooldown", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                cdObj.transform.SetParent(obj.transform, false);
                var cdR = cdObj.GetComponent<RectTransform>();
                cdR.anchorMin = Vector2.zero;
                cdR.anchorMax = Vector2.one;
                cdR.sizeDelta = Vector2.zero;
                var cdImg = cdObj.GetComponent<Image>();
                cdImg.color = new Color(0.11f, 0.11f, 0.11f, 0.78f);
                cdImg.type = Image.Type.Filled;
                cdImg.fillMethod = Image.FillMethod.Radial360;
                cdImg.fillOrigin = (int)Image.Origin360.Top;
                cdImg.fillClockwise = false;
                cdImg.fillAmount = 0f;
            }
            if (obj == null) return null;

            obj.SetActive(true);
            obj.name = $"Status_{status.RecipeId ?? "Unknown"}_{stackIndex}";

            var rect = obj.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.localScale = Vector3.one;
                if (_statusPanel == null || _statusPanel.GetComponent<LayoutGroup>() == null)
                {
                    if (rect.sizeDelta == Vector2.zero)
                        rect.sizeDelta = new Vector2(25f, 25f);
                }
            }

            var iconTrans = obj.transform.Find("Icon") ?? FindChildDirectOrRecursive(obj.transform, "Icon");
            var iconImage = iconTrans != null ? iconTrans.GetComponent<Image>() : null;
            if (iconImage == null)
            {
                iconImage = obj.GetComponent<Image>();
                if (iconImage == null) iconImage = obj.AddComponent<Image>();
            }

            var cooldownTrans = obj.transform.Find("Cooldown") ?? FindChildDirectOrRecursive(obj.transform, "Cooldown");
            var cooldownImage = cooldownTrans != null ? cooldownTrans.GetComponent<Image>() : null;

            var polarity = status.Recipe?.Polarity ?? StatusPolarity.Debuff;
            if (iconImage != null)
            {
                iconImage.enabled = true;
                iconImage.color = Color.white;
                iconImage.sprite = ResolveStatusSprite(status.RecipeId, polarity);
                if (iconImage.type == Image.Type.Filled)
                {
                    iconImage.fillAmount = 1f;
                }
            }

            var isPermanent = status.Recipe != null && status.Recipe.DurationClock == StatusDurationClock.Permanent;
            int maxDur;
            if (isPermanent)
            {
                maxDur = int.MaxValue;
            }
            else if (status.RemainingDuration > 0)
            {
                maxDur = status.RemainingDuration;
            }
            else
            {
                maxDur = status.Recipe?.DefaultDuration > 0 ? status.Recipe.DefaultDuration : 2;
            }
            var remDur = isPermanent ? int.MaxValue : (status.RemainingDuration > 0 ? status.RemainingDuration : maxDur);

            var entry = new StatusViewEntry
            {
                InstanceId = status.InstanceId,
                RecipeId = status.RecipeId,
                StackIndex = stackIndex,
                StackCount = status.StackCount,
                Root = obj,
                Rect = rect,
                IconImage = iconImage,
                CooldownOverlay = cooldownImage,
                RemainingDuration = remDur,
                MaxDuration = maxDur,
                IsPermanent = isPermanent
            };

            UpdateDurationFill(entry, remDur, maxDur);
            return entry;
        }

        private static void UpdateDurationFill(StatusViewEntry entry, int remaining, int maxDuration)
        {
            if (entry == null) return;

            float ratio = 0f;
            if (entry.IsPermanent)
            {
                ratio = 0f;
            }
            else if (maxDuration > 0)
            {
                ratio = Mathf.Clamp01((float)(maxDuration - remaining) / maxDuration);
            }

            if (entry.CooldownOverlay != null)
            {
                entry.CooldownOverlay.fillAmount = ratio;
            }
            else if (entry.IconImage != null)
            {
                entry.IconImage.type = Image.Type.Filled;
                entry.IconImage.fillMethod = Image.FillMethod.Radial360;
                entry.IconImage.fillOrigin = (int)Image.Origin360.Top;
                entry.IconImage.fillClockwise = true;
                entry.IconImage.fillAmount = Mathf.Clamp01((float)remaining / Mathf.Max(1, maxDuration));
            }
        }

        private IEnumerator AnimateStatusEnter(RectTransform rect)
        {
            if (rect == null) yield break;
            var duration = 0.22f;
            var elapsed = 0f;
            var initialPos = rect.localPosition;
            var startOffset = new Vector3(14f, 4f, 0f);
            var hasLayoutGroup = rect.parent != null && rect.parent.GetComponent<LayoutGroup>() != null;

            rect.localScale = Vector3.one * 0.3f;
            if (!hasLayoutGroup)
            {
                rect.localPosition = initialPos + startOffset;
            }

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);

                if (!hasLayoutGroup)
                {
                    var slideT = Mathf.Sin(t * Mathf.PI * 0.5f);
                    rect.localPosition = Vector3.Lerp(initialPos + startOffset, initialPos, slideT);
                }

                float scale;
                if (t < 0.65f)
                    scale = Mathf.Lerp(0.3f, 1.25f, t / 0.65f);
                else
                    scale = Mathf.Lerp(1.25f, 1f, (t - 0.65f) / 0.35f);
                rect.localScale = Vector3.one * scale;

                yield return null;
            }

            if (!hasLayoutGroup)
            {
                rect.localPosition = initialPos;
            }
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

        private IEnumerator AnimateStatusRefresh(StatusViewEntry entry, bool succeeded)
        {
            if (entry?.Rect == null) yield break;
            var rect = entry.Rect;
            var icon = entry.IconImage;
            var duration = succeeded ? 0.34f : 0.38f;
            var elapsed = 0f;

            rect.localScale = Vector3.one;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var pulse = Mathf.Sin(t * Mathf.PI);
                if (succeeded)
                {
                    rect.localScale = Vector3.one * (1f + pulse * 0.32f);
                    if (icon != null)
                        icon.color = Color.Lerp(Color.white, new Color(0.45f, 1f, 0.58f, 1f), pulse);
                }
                else
                {
                    var shake = Mathf.Sin(t * Mathf.PI * 8f) * (1f - t);
                    rect.localRotation = Quaternion.Euler(0f, 0f, shake * 12f);
                    rect.localScale = Vector3.one * (1f - pulse * 0.12f);
                    if (icon != null)
                        icon.color = Color.Lerp(Color.white, new Color(1f, 0.25f, 0.2f, 1f), pulse);
                }
                yield return null;
            }

            ResetStatusFeedbackVisual(entry);
            entry.Routine = null;
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
            var key = recipeId ?? "";
            if (_statusSpriteCache.TryGetValue(key, out var cached) && cached != null)
                return cached;

            var sprite = StatusVisualData.GetSprite(recipeId, polarity);
            if (sprite == null)
            {
                var fallbackKey = StatusLibrary.GetIconKey(recipeId, polarity);
                sprite = LoadSpriteByName(fallbackKey);
            }

            if (sprite != null)
                _statusSpriteCache[key] = sprite;
            return sprite;
        }

        private static Sprite LoadSpriteByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            string[] subfolders = { "Stats_Icon", "StatusIcon", "" };
#if UNITY_EDITOR
            foreach (var sub in subfolders)
            {
                var path = string.IsNullOrEmpty(sub)
                    ? $"Assets/Project/Art/UI/{name}.png"
                    : $"Assets/Project/Art/UI/{sub}/{name}.png";
                var edSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (edSprite != null) return edSprite;
                var allAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
                if (allAssets != null)
                {
                    for (var i = 0; i < allAssets.Length; i++)
                    {
                        if (allAssets[i] is Sprite spr) return spr;
                    }
                }
            }
#endif
            foreach (var sub in subfolders)
            {
                var resPath = string.IsNullOrEmpty(sub) ? $"UI/{name}" : $"UI/{sub}/{name}";
                var res = Resources.Load<Sprite>(resPath);
                if (res != null) return res;
            }

            foreach (var sub in subfolders)
            {
                var filePath = string.IsNullOrEmpty(sub)
                    ? System.IO.Path.Combine(Application.dataPath, "Project", "Art", "UI", name + ".png")
                    : System.IO.Path.Combine(Application.dataPath, "Project", "Art", "UI", sub, name + ".png");
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
            }
            return null;
        }

        private static Transform FindChildDirectOrRecursive(Transform parent, string targetName)
        {
            if (parent == null) return null;
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (string.Equals(child.name, targetName, System.StringComparison.OrdinalIgnoreCase))
                    return child;
            }
            for (var i = 0; i < parent.childCount; i++)
            {
                var found = FindChildDirectOrRecursive(parent.GetChild(i), targetName);
                if (found != null) return found;
            }
            return null;
        }

        private void BindAuthoredUnitUi()
        {
            var unitUi = FindChildDirectOrRecursive(transform, "UnitUI") ?? transform;
            if (unitUi != null)
            {
                unitUi.gameObject.SetActive(true);
                var h = FindChildDirectOrRecursive(unitUi, "HealthBar");
                if (h != null && healthSlider == null) healthSlider = h.GetComponent<Slider>();

                if (levelHandleFill == null || levelHandleBorder == null || (levelText == null && _levelTextFallback == null))
                {
                    var handleParent = FindChildDirectOrRecursive(h ?? unitUi, "LevelHandle") ?? FindChildDirectOrRecursive(transform, "LevelHandle");
                    if (handleParent != null)
                    {
                        if (levelHandleBorder == null)
                            levelHandleBorder = handleParent.GetComponent<Image>();

                        if (levelHandleFill == null)
                        {
                            var fillTransform = handleParent.Find("LevelHandle") ?? FindChildDirectOrRecursive(handleParent, "LevelHandle");
                            levelHandleFill = fillTransform != null ? fillTransform.GetComponent<Image>() : null;
                            if (levelHandleFill == null)
                                levelHandleFill = levelHandleBorder;
                        }

                        if (levelText == null && _levelTextFallback == null)
                        {
                            var txtTransform = handleParent.Find("Level-Text") ?? FindChildDirectOrRecursive(handleParent, "Level-Text");
                            if (txtTransform != null)
                            {
                                levelText = txtTransform.GetComponent<TMP_Text>();
                                if (levelText == null)
                                    _levelTextFallback = txtTransform.GetComponent<Text>();
                            }
                        }
                    }
                }

                var shield = FindChildDirectOrRecursive(unitUi, "ShieldBar")
                    ?? FindChildDirectOrRecursive(unitUi, "Ex-Healthbar")
                    ?? unitUi.Find("HealthPanel/Ex-Healthbar");
                _shieldRoot = shield == null ? null : shield.gameObject;
                var shieldSliderTransform = shield == null ? null :
                    (FindChildDirectOrRecursive(shield, "ShieldSlider") ?? shield);
                _shieldSlider = shieldSliderTransform?.GetComponent<Slider>()
                    ?? shield?.GetComponentInChildren<Slider>(true);
                _shieldFill = _shieldSlider != null && _shieldSlider.fillRect != null
                    ? _shieldSlider.fillRect.GetComponent<Image>()
                    : shieldSliderTransform?.Find("Fill Area/Fill")?.GetComponent<Image>();

                var panel = FindChildDirectOrRecursive(unitUi, "Buff/DebuffPanel") ?? FindChildDirectOrRecursive(transform, "Buff/DebuffPanel");
                if (panel != null)
                {
                    _statusPanel = panel.gameObject;
                    var t = FindChildDirectOrRecursive(panel, "StatusIcon")
                        ?? panel.Find("StatusIcon")
                        ?? FindChildDirectOrRecursive(panel, "Image")
                        ?? panel.Find("Image");
                    if (t != null)
                    {
                        _statusTemplate = t.gameObject;
                        _statusTemplate.SetActive(false);
                    }
                }

                if (_statusTemplate == null && statusIconPrefab != null)
                {
                    _statusTemplate = statusIconPrefab;
                }
#if UNITY_EDITOR
                if (_statusTemplate == null)
                {
                    _statusTemplate = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Project/Prefabs/StatusIcon.prefab");
                    if (statusIconPrefab == null && _statusTemplate != null)
                    {
                        statusIconPrefab = _statusTemplate;
                    }
                }
#endif
            }

            if (powerGaugeSlider == null)
            {
                var pg = transform.Find("UI/Healthbar/PG")
                    ?? unitUi?.Find("HealthBar/GaugePanel/GaugeSlider")
                    ?? unitUi?.Find("GaugePanel/GaugeSlider");
                powerGaugeSlider = pg?.GetComponent<Slider>() ?? pg?.GetComponentInChildren<Slider>(true);
            }
            _powerGaugeFill = powerGaugeSlider != null && powerGaugeSlider.fillRect != null
                ? powerGaugeSlider.fillRect.GetComponent<Image>() : null;
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

#if UNITY_EDITOR
        [SerializeField] private string previewAttribute = "";
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            try
            {
                BindAuthoredUnitUi();
                if (!string.IsNullOrEmpty(previewAttribute))
                {
                    SetAttribute(previewAttribute);
                }
            }
            catch { }
        }
#endif
    }
}
