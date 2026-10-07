using FightingAllstar.Core.Content;
using FightingAllstar.Presentation.Content;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    [CreateAssetMenu(menuName = "Fighting Allstar/Combat Content Source")]
    public sealed class CombatContentSource : ScriptableObject
    {
        [Tooltip("Supplies shared combat recipes. Runtime character definitions are built from CharacterObject and its card/passive assets.")]
        [SerializeField] private TextAsset catalogJson;
        public ContentCatalog LoadCatalog()
        {
            var catalog = CharacterObjectCatalogBuilder.Load(catalogJson);
            var errors = ContentValidator.Validate(catalog);
            if (errors.Count > 0) throw new System.InvalidOperationException("Combat content is invalid: " + string.Join("; ", errors));
            return catalog;
        }
    }
}
