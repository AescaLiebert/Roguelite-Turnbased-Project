using System;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>
    /// Configurable ScriptableObject settings for Floating Combat Text (FCT).
    /// Allows adjusting typography, colors, font sizes, arrows, timing, and float animations
    /// directly from the Unity Inspector without editing code or USS stylesheets.
    /// </summary>
    [CreateAssetMenu(fileName = "FloatingCombatTextSettings", menuName = "Fighting Allstar/Combat/Floating Combat Text Settings")]
    public sealed class FloatingCombatTextSettingsSO : ScriptableObject
    {
        [Header("Typography")]
        [Tooltip("Optional custom Font asset. Leave empty to use default USS font.")]
        [SerializeField] private Font fontAsset;
        [SerializeField] private FontStyle fontStyle = FontStyle.BoldAndItalic;

        [Header("Font Sizes")]
        [SerializeField, Range(12f, 72f)] private float damageFontSize = 40f;
        [SerializeField, Range(12f, 72f)] private float criticalFontSize = 46f;
        [SerializeField, Range(12f, 72f)] private float blockedFontSize = 34f;
        [SerializeField, Range(12f, 72f)] private float healFontSize = 38f;
        [SerializeField, Range(12f, 72f)] private float statusFontSize = 21f;
        [SerializeField, Range(12f, 72f)] private float passiveFontSize = 23f;
        [SerializeField, Range(10f, 36f)] private float kickerFontSize = 18f;

        [Header("Colors (Optional Override)")]
        [Tooltip("Enable to override USS color classes directly from the Inspector.")]
        [SerializeField] private bool overrideColors = false;
        [SerializeField] private Color damageColor = new Color32(255, 251, 235, 255);
        [SerializeField] private Color criticalColor = new Color32(255, 222, 80, 255);
        [SerializeField] private Color criticalOutlineColor = new Color32(93, 45, 9, 255);
        [SerializeField] private Color blockedColor = new Color32(160, 221, 250, 255);
        [SerializeField] private Color healColor = new Color32(135, 250, 64, 255);
        [SerializeField] private Color statusColor = new Color32(244, 199, 255, 255);
        [SerializeField] private Color kickerColor = new Color32(255, 233, 132, 255);
        [SerializeField] private Color kickerOutlineColor = new Color32(50, 26, 10, 255);

        [Header("Attribute Affinity Arrows")]
        [Tooltip("Optional override sprite for Advantage (Red Up Arrow). If null, loads default sprite.")]
        [SerializeField] private Sprite advantageArrowSprite;
        [Tooltip("Optional override sprite for Disadvantage (Blue Down Arrow). If null, loads default sprite.")]
        [SerializeField] private Sprite disadvantageArrowSprite;
        [SerializeField] private Vector2 arrowSizeNormal = new Vector2(26f, 32f);
        [SerializeField] private Vector2 arrowSizeCritical = new Vector2(30f, 38f);
        [SerializeField] private Vector2 arrowSizeBlocked = new Vector2(21f, 26f);
        [SerializeField, Range(0f, 20f)] private float arrowRightMargin = 6f;

        [Header("Timing & Lifetime")]
        [Tooltip("Base duration in seconds for damage/heal numbers.")]
        [SerializeField, Range(0.5f, 3f)] private float baseDurationNumeric = 1.45f;
        [Tooltip("Base duration in seconds for status/passive texts.")]
        [SerializeField, Range(0.5f, 3f)] private float baseDurationStatus = 1.55f;
        [Tooltip("Minimum lifetime scale clamp for high hit-count multi-hits.")]
        [SerializeField, Range(0.2f, 1f)] private float minMultiHitDurationScale = 0.65f;
        [Tooltip("Fraction of duration (0 to 1) that text stays 100% opaque before beginning fade-out.")]
        [SerializeField, Range(0.1f, 0.9f)] private float fadeStartPercent = 0.45f;

        [Header("Motion & Scaling")]
        [Tooltip("Initial snappy vertical rise in pixels.")]
        [SerializeField, Range(5f, 60f)] private float initialRisePixels = 22f;
        [Tooltip("Additional gentle drift rise in pixels over lifetime.")]
        [SerializeField, Range(5f, 80f)] private float driftRisePixels = 26f;
        [Tooltip("Initial scale pop factor on spawn for normal hits.")]
        [SerializeField, Range(1f, 1.6f)] private float popScaleNormal = 1.18f;
        [Tooltip("Initial scale pop factor on spawn for critical hits.")]
        [SerializeField, Range(1f, 1.8f)] private float popScaleCritical = 1.30f;

        public Font FontAsset => fontAsset;
        public FontStyle FontStyle => fontStyle;
        public float DamageFontSize => damageFontSize;
        public float CriticalFontSize => criticalFontSize;
        public float BlockedFontSize => blockedFontSize;
        public float HealFontSize => healFontSize;
        public float StatusFontSize => statusFontSize;
        public float PassiveFontSize => passiveFontSize;
        public float KickerFontSize => kickerFontSize;
        public bool OverrideColors => overrideColors;
        public Color DamageColor => damageColor;
        public Color CriticalColor => criticalColor;
        public Color CriticalOutlineColor => criticalOutlineColor;
        public Color BlockedColor => blockedColor;
        public Color HealColor => healColor;
        public Color StatusColor => statusColor;
        public Color KickerColor => kickerColor;
        public Color KickerOutlineColor => kickerOutlineColor;
        public Sprite AdvantageArrowSprite => advantageArrowSprite;
        public Sprite DisadvantageArrowSprite => disadvantageArrowSprite;
        public Vector2 ArrowSizeNormal => arrowSizeNormal;
        public Vector2 ArrowSizeCritical => arrowSizeCritical;
        public Vector2 ArrowSizeBlocked => arrowSizeBlocked;
        public float ArrowRightMargin => arrowRightMargin;
        public float BaseDurationNumeric => baseDurationNumeric;
        public float BaseDurationStatus => baseDurationStatus;
        public float MinMultiHitDurationScale => minMultiHitDurationScale;
        public float FadeStartPercent => fadeStartPercent;
        public float InitialRisePixels => initialRisePixels;
        public float DriftRisePixels => driftRisePixels;
        public float PopScaleNormal => popScaleNormal;
        public float PopScaleCritical => popScaleCritical;

        public float GetFontSize(string kind) => kind switch
        {
            "critical" => criticalFontSize,
            "blocked" or "evade" or "immunity" => blockedFontSize,
            "heal" => healFontSize,
            "passive" => passiveFontSize,
            "status" => statusFontSize,
            _ => damageFontSize
        };

        public Vector2 GetArrowSize(string kind) => kind switch
        {
            "critical" => arrowSizeCritical,
            "blocked" => arrowSizeBlocked,
            _ => arrowSizeNormal
        };

        private static FloatingCombatTextSettingsSO _cachedDefault;
        public static FloatingCombatTextSettingsSO GetDefault()
        {
            if (_cachedDefault == null)
            {
                _cachedDefault = Resources.Load<FloatingCombatTextSettingsSO>("UI/FloatingCombatTextSettings");
                if (_cachedDefault == null)
                    _cachedDefault = Resources.Load<FloatingCombatTextSettingsSO>("FloatingCombatTextSettings");
                if (_cachedDefault == null)
                    _cachedDefault = CreateInstance<FloatingCombatTextSettingsSO>();
            }
            return _cachedDefault;
        }
    }
}
