using System;
using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Core.Content;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>
    /// ScriptableObject defining visual presentation for a combat status effect.
    /// Wires Status ID, Display Name, and Icon together.
    /// </summary>
    [CreateAssetMenu(fileName = "New Status Visual Data", menuName = "Fighting Allstar/Status Visual Data")]
    public sealed class StatusVisualData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Unique status recipe ID or primary ID (e.g. status.debuff.ignite, status.debuff.poison, bleed)")]
        [SerializeField] private string id;

        [Tooltip("Player-facing name shown in floating feedback, badges, and tooltips (e.g. Ignite, Poison, Bleed)")]
        [SerializeField] private string displayName;

        [Header("Visual Asset")]
        [Tooltip("Icon sprite for the status (used in world HUD, card badges, and tooltips)")]
        [SerializeField] private Sprite icon;

        [Tooltip("Actor model effect. Automatic reads stat modifiers, status tags/IDs and card-disable semantics.")]
        [SerializeField] private ActorVisualEffectKind actorVisualEffect = ActorVisualEffectKind.Automatic;

        [Header("Classification")]
        [Tooltip("Polarity of the status: Buff, Neutral (healing/stance), or Debuff")]
        [SerializeField] private StatusPolarity polarity = StatusPolarity.Debuff;

        [Tooltip("Alternative aliases or keywords for fuzzy matching (e.g. 'ignite', 'burn')")]
        [SerializeField] private string[] keywords = Array.Empty<string>();

        [TextArea]
        [SerializeField] private string description;

        public string Id => id;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? id : displayName;
        public Sprite Icon => icon;
        public ActorVisualEffectKind ActorVisualEffect => actorVisualEffect;
        public StatusPolarity Polarity => polarity;
        public string[] Keywords => keywords;
        public string Description => description;

        // Static registry for fast lookup and decoupling presentation from hardcoded string checks
        private static readonly Dictionary<string, StatusVisualData> Registry = new Dictionary<string, StatusVisualData>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<StatusVisualData> AllVisuals = new List<StatusVisualData>();
        private static bool _initialized = false;

        public static void ResetRegistry()
        {
            Registry.Clear();
            AllVisuals.Clear();
            _initialized = false;
        }

        public static void InitializeRegistry()
        {
            var folders = new[] { "StatusData", "StatusVisuals", "Statuses", "" };
            foreach (var folder in folders)
            {
                var loaded = Resources.LoadAll<StatusVisualData>(folder);
                if (loaded != null)
                {
                    for (var i = 0; i < loaded.Length; i++)
                    {
                        Register(loaded[i]);
                    }
                }
            }

            // The authored visuals live under Project/Data/StatusData, outside Resources.
            // This catalog is a Resources asset whose references keep every visual included
            // in player builds and make them discoverable without editor-only AssetDatabase.
            var catalogs = Resources.LoadAll<StatusVisualDataCatalog>("StatusData");
            foreach (var catalog in catalogs)
            {
                if (catalog?.Visuals == null) continue;
                foreach (var visual in catalog.Visuals) Register(visual);
            }

#if UNITY_EDITOR
            // Assets outside Resources are not found by Resources.LoadAll. Always scan
            // the project in the Editor, even when a character/card already registered
            // one visual (for example Brian's barrier). Otherwise that first registration
            // suppresses the scan and leaves every other status using fallback icons.
            var guids = UnityEditor.AssetDatabase.FindAssets("t:StatusVisualData");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<StatusVisualData>(path);
                if (asset != null) Register(asset);
            }
#endif
            _initialized = true;
        }

        private static string RegistryKey(string token, StatusPolarity polarity) => polarity + "|" + token;

        public static void Register(StatusVisualData data)
        {
            if (data == null) return;
            if (!AllVisuals.Contains(data)) AllVisuals.Add(data);
            // Only exact IDs belong in this map. Human aliases can collide and are
            // resolved deterministically by Get instead of last-registration wins.
            if (!string.IsNullOrEmpty(data.Id)) Registry[RegistryKey(data.Id, data.Polarity)] = data;
        }

        public static void RegisterAlias(string recipeId, StatusVisualData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(recipeId)) return;
            Register(data);
            Registry[RegistryKey(recipeId, data.Polarity)] = data;
        }

        public static StatusVisualData Get(string recipeOrStatusId, StatusPolarity polarity = StatusPolarity.Debuff)
        {
            if (!_initialized || AllVisuals.Count == 0)
            {
                InitializeRegistry();
            }

            if (string.IsNullOrWhiteSpace(recipeOrStatusId))
            {
                return GetDefaultByPolarity(polarity);
            }

            var clean = recipeOrStatusId.Trim();
            if (Registry.TryGetValue(RegistryKey(clean, polarity), out var exact) && exact != null)
                return exact;

            // Do not let a normalized alias overwrite an exact mechanic ID. Resolve
            // explicit IDs/names/aliases first, then normalized legacy tokens, all
            // within the requested polarity and with stable ordering.
            var candidates = AllVisuals.Where(v => v != null && v.Polarity == polarity)
                .OrderBy(v => v.Id, StringComparer.OrdinalIgnoreCase).ThenBy(v => v.name, StringComparer.Ordinal);
            foreach (var visual in candidates)
                if (string.Equals(visual.Id, clean, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(visual.DisplayName, clean, StringComparison.OrdinalIgnoreCase) ||
                    visual.Keywords != null && visual.Keywords.Any(k => string.Equals(k, clean, StringComparison.OrdinalIgnoreCase)))
                    return visual;

            var normalized = NormalizeToken(clean);
            if (normalized.Length == 0) return null;
            foreach (var visual in candidates)
                if (NormalizeToken(visual.Id) == normalized || NormalizeToken(visual.DisplayName) == normalized ||
                    visual.Keywords != null && visual.Keywords.Any(k => NormalizeToken(k) == normalized))
                    return visual;
            return null;
        }

        public static Sprite GetSprite(string recipeOrStatusId, StatusPolarity polarity = StatusPolarity.Debuff)
        {
            var visual = Get(recipeOrStatusId, polarity);
            if (visual != null && visual.Icon != null)
                return visual.Icon;

            if (IsElementalStatusId(recipeOrStatusId))
            {
                var debuffFallback = GetDefaultByPolarity(StatusPolarity.Debuff);
                if (debuffFallback != null && debuffFallback.Icon != null)
                    return debuffFallback.Icon;
            }

            var def = GetDefaultByPolarity(polarity);
            return def != null ? def.Icon : null;
        }

        private static bool IsElementalStatusId(string recipeOrStatusId) =>
            !string.IsNullOrWhiteSpace(recipeOrStatusId) &&
            recipeOrStatusId.Trim().StartsWith("status.element.", StringComparison.OrdinalIgnoreCase);

        public static string GetDisplayName(string recipeOrStatusId, StatusPolarity polarity = StatusPolarity.Debuff)
        {
            var visual = Get(recipeOrStatusId, polarity);
            if (visual != null && !string.IsNullOrEmpty(visual.DisplayName))
                return CombatTextCatalog.Normalize(visual.DisplayName);

            return CombatTextCatalog.Normalize(FightingAllstar.Core.Content.StatusLibrary.GetDisplayName(recipeOrStatusId, polarity));
        }

        private static StatusVisualData GetDefaultByPolarity(StatusPolarity polarity)
        {
            var keys = polarity == StatusPolarity.Buff
                ? new[] { "stat_up", "Status_StatUp", "buff_default", "default_buff" }
                : polarity == StatusPolarity.Neutral
                    ? new[] { "neutral_default", "default_neutral" }
                    : new[] { "stat_down", "Status_StatDown", "debuff_default", "default_debuff" };

            foreach (var key in keys)
            {
                if (Registry.TryGetValue(RegistryKey(key, polarity), out var match) && match != null) return match;
            }
            return null;
        }

        private static string NormalizeToken(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return input.ToLowerInvariant()
                .Replace("status.", "").Replace("status_", "")
                .Replace("debuff.", "")
                .Replace("buff.", "")
                .Replace("stat.", "")
                .Replace("decrease.", "")
                .Replace("increase.", "")
                .Replace("decrease ", "")
                .Replace("increase ", "")
                .Replace("decrease", "")
                .Replace("increase", "")
                .Replace("rate", "")
                .Replace(".", "")
                .Replace("_", "")
                .Replace("-", "")
                .Replace(" ", "")
                .Trim();
        }

#if UNITY_EDITOR
        public void SetData(string newId, string newDisplayName, Sprite newIcon, StatusPolarity newPolarity, string[] newKeywords, string newDescription = "")
        {
            id = newId;
            displayName = newDisplayName;
            icon = newIcon;
            polarity = newPolarity;
            keywords = newKeywords;
            description = newDescription;
        }
#endif
    }
}
