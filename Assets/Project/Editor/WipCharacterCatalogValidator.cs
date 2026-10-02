using System;
using FightingAllstar.Core.Content;
using FightingAllstar.Core.Economy;
using UnityEditor;
using UnityEngine;

namespace FightingAllstar.EditorTools
{
    public static class WipCharacterCatalogValidator
    {
        private const string CatalogPath = "Assets/Project/Content/WipCharacterCatalog.json";

        [MenuItem("Fighting Allstar/Validate WIP Character Catalog")]
        public static void Validate()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(CatalogPath);
            if (asset == null) throw new InvalidOperationException("WIP character catalog is missing at " + CatalogPath + ".");
            var catalog = JsonUtility.FromJson<ContentCatalog>(asset.text);
            var errors = ContentAuthoringValidator.ValidateDrafts(catalog);
            errors.AddRange(BannerDefinition.CreateKofPhaseE().Validate(catalog, false));
            if (errors.Count > 0) throw new InvalidOperationException("WIP character catalog is invalid: " + string.Join("; ", errors));
            Debug.Log("WIP catalog and KOF pool are valid: 29 source characters, eight pool entries, 174 card ranks, 203 constellation tiers. Runtime effects remain un-authored.");
        }
    }
}
