using System.Collections.Generic;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    /// <summary>
    /// Runtime resource catalog that keeps StatusVisualData assets available in player builds.
    /// </summary>
    [CreateAssetMenu(fileName = "StatusVisualDataCatalog", menuName = "Fighting Allstar/Status Visual Catalog")]
    public sealed class StatusVisualDataCatalog : ScriptableObject
    {
        [SerializeField] private List<StatusVisualData> visuals = new List<StatusVisualData>();

        public IReadOnlyList<StatusVisualData> Visuals => visuals;
    }
}
