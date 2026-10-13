using FightingAllstar.Core.Content;
using UnityEngine;

namespace FightingAllstar.Presentation
{
    /// <summary>The authored V3 artifact silhouettes, independent of rarity and level.</summary>
    public sealed class ArtifactArtSO : ScriptableObject
    {
        public Sprite[] striker = new Sprite[5];
        public Sprite[] bulwark = new Sprite[5];
        public Sprite[] lifebloom = new Sprite[5];

        public Sprite Icon(ArtifactSet set, ArtifactSlot slot)
        {
            var sprites = set == ArtifactSet.Striker ? striker : set == ArtifactSet.Bulwark ? bulwark : lifebloom;
            var index = (int)slot;
            return sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
        }
    }
}
