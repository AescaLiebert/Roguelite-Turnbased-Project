using System.Collections.Generic;
using UnityEngine;

namespace FightingAllstar.Presentation.Combat
{
    public sealed class BattleEntityPresentationCatalog : ScriptableObject
    {
        public List<BattleEntityPresentationProfile> profiles = new List<BattleEntityPresentationProfile>();
        public BattleEntityPresentationProfile Find(string id) => profiles.Find(p => p != null && p.definitionId == id);
    }
}
