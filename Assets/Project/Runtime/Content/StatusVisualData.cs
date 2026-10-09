using System;
using System.Collections.Generic;
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

        [Tooltip("Actor model effect. Automatic reads the status recipe's stat modifiers and stun tag.")]
        [SerializeField] private ActorVisualEffectKind actorVisualEffect = ActorVisualEffectKind.Automatic;

        [Header("Classification")]
        [Tooltip("Polarity of the status: Buff or Debuff")]
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

        public static void Register(StatusVisualData data)
        {
            if (data == null) return;
            if (!AllVisuals.Contains(data)) AllVisuals.Add(data);

            if (!string.IsNullOrEmpty(data.Id))
            {
                Registry[data.Id] = data;
                var shortId = StripStatusPrefix(data.Id);
                if (!string.IsNullOrEmpty(shortId)) Registry[shortId] = data;
                var normId = NormalizeToken(data.Id);
                if (!string.IsNullOrEmpty(normId)) Registry[normId] = data;
            }

            if (!string.IsNullOrEmpty(data.DisplayName))
            {
                Registry[data.DisplayName] = data;
                var shortName = StripStatusPrefix(data.DisplayName);
                if (!string.IsNullOrEmpty(shortName)) Registry[shortName] = data;
                var normName = NormalizeToken(data.DisplayName);
                if (!string.IsNullOrEmpty(normName)) Registry[normName] = data;
            }

            if (data.Keywords != null)
            {
                for (var i = 0; i < data.Keywords.Length; i++)
                {
                    var kw = data.Keywords[i];
                    if (!string.IsNullOrWhiteSpace(kw))
                    {
                        var trimmed = kw.Trim();
                        Registry[trimmed] = data;
                        var normKw = NormalizeToken(trimmed);
                        if (!string.IsNullOrEmpty(normKw)) Registry[normKw] = data;
                    }
                }
            }
        }

        public static void RegisterAlias(string recipeId, StatusVisualData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(recipeId)) return;
            Register(data);
            Registry[recipeId] = data;
            var shortId = StripStatusPrefix(recipeId);
            if (!string.IsNullOrEmpty(shortId)) Registry[shortId] = data;
            var norm = NormalizeToken(recipeId);
            if (!string.IsNullOrEmpty(norm)) Registry[norm] = data;
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
            if (Registry.TryGetValue(clean, out var exact) && exact != null)
                return exact;

            var shortName = StripStatusPrefix(clean);
            if (!string.IsNullOrEmpty(shortName) && Registry.TryGetValue(shortName, out var shortMatch) && shortMatch != null)
                return shortMatch;

            var normTarget = NormalizeToken(clean);
            if (!string.IsNullOrEmpty(normTarget))
            {
                if (Registry.TryGetValue(normTarget, out var normMatch) && normMatch != null)
                    return normMatch;

                for (var i = 0; i < AllVisuals.Count; i++)
                {
                    var v = AllVisuals[i];
                    if (v == null) continue;
                    if (v.Polarity == polarity)
                    {
                        if (NormalizeToken(v.Id) == normTarget || NormalizeToken(v.DisplayName) == normTarget)
                            return v;
                        if (v.Keywords != null)
                        {
                            for (var k = 0; k < v.Keywords.Length; k++)
                            {
                                if (NormalizeToken(v.Keywords[k]) == normTarget)
                                    return v;
                            }
                        }
                    }
                }
            }

            // Substring search
            var lower = clean.ToLowerInvariant();
            for (var i = 0; i < AllVisuals.Count; i++)
            {
                var v = AllVisuals[i];
                if (v == null) continue;
                if (!string.IsNullOrEmpty(v.Id) && lower.Contains(v.Id.ToLowerInvariant()))
                    return v;
                if (!string.IsNullOrEmpty(v.DisplayName) && lower.Contains(v.DisplayName.ToLowerInvariant()))
                    return v;
                if (v.Keywords != null)
                {
                    for (var k = 0; k < v.Keywords.Length; k++)
                    {
                        var kw = v.Keywords[k];
                        if (!string.IsNullOrEmpty(kw) && (lower.Contains(kw.ToLowerInvariant()) || kw.ToLowerInvariant().Contains(lower)))
                            return v;
                    }
                }
            }

            return null;
        }

        public static Sprite GetSprite(string recipeOrStatusId, StatusPolarity polarity = StatusPolarity.Debuff)
        {
            var visual = Get(recipeOrStatusId, polarity);
            if (visual != null && visual.Icon != null)
                return visual.Icon;

            var def = GetDefaultByPolarity(polarity);
            return def != null ? def.Icon : null;
        }

        public static string GetDisplayName(string recipeOrStatusId, StatusPolarity polarity = StatusPolarity.Debuff)
        {
            var visual = Get(recipeOrStatusId, polarity);
            if (visual != null && !string.IsNullOrEmpty(visual.DisplayName))
                return visual.DisplayName;

            return FightingAllstar.Core.Content.StatusLibrary.GetDisplayName(recipeOrStatusId, polarity);
        }

        private static StatusVisualData GetDefaultByPolarity(StatusPolarity polarity)
        {
            var keys = polarity == StatusPolarity.Buff
                ? new[] { "stat_up", "Status_StatUp", "buff_default", "default_buff" }
                : new[] { "stat_down", "Status_StatDown", "debuff_default", "default_debuff" };

            foreach (var key in keys)
            {
                if (Registry.TryGetValue(key, out var match) && match != null) return match;
            }
            return null;
        }

        private static string StripStatusPrefix(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            var s = id;
            if (s.StartsWith("status.debuff.", StringComparison.OrdinalIgnoreCase)) s = s.Substring(14);
            else if (s.StartsWith("status.buff.", StringComparison.OrdinalIgnoreCase)) s = s.Substring(12);
            else if (s.StartsWith("status.", StringComparison.OrdinalIgnoreCase)) s = s.Substring(7);
            else if (s.StartsWith("decrease.", StringComparison.OrdinalIgnoreCase)) s = s.Substring(9);
            else if (s.StartsWith("increase.", StringComparison.OrdinalIgnoreCase)) s = s.Substring(9);
            else if (s.StartsWith("decrease ", StringComparison.OrdinalIgnoreCase)) s = s.Substring(9);
            else if (s.StartsWith("increase ", StringComparison.OrdinalIgnoreCase)) s = s.Substring(9);
            if (s.StartsWith("stat.", StringComparison.OrdinalIgnoreCase)) s = s.Substring(5);
            return s;
        }

        private static string NormalizeToken(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return input.ToLowerInvariant()
                .Replace("status.", "")
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
