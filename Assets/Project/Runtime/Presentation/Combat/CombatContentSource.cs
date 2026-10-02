using FightingAllstar.Core.Content;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    [CreateAssetMenu(menuName = "Fighting Allstar/Combat Content Source")]
    public sealed class CombatContentSource : ScriptableObject
    {
        [SerializeField] private TextAsset catalogJson;
        public ContentCatalog LoadCatalog()
        {
            if (catalogJson == null) throw new System.InvalidOperationException("Combat content catalog is not assigned.");
            var catalog = JsonUtility.FromJson<ContentCatalog>(catalogJson.text);
            var errors = ContentValidator.Validate(catalog);
            if (errors.Count > 0) throw new System.InvalidOperationException("Combat content is invalid: " + string.Join("; ", errors));
            return catalog;
        }
    }
}
