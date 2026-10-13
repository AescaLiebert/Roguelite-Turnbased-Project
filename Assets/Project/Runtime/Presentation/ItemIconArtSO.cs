using FightingAllstar.Core.Content;
using UnityEngine;

namespace FightingAllstar.Presentation
{
    /// <summary>Authored item sprites used by the backpack and item tooltip lookup.</summary>
    public sealed class ItemIconArtSO : ScriptableObject
    {
        public Sprite diamond;
        public Sprite coin;
        public Sprite artifactDust;
        public Sprite kofTournamentInsignia;
        public Sprite[] phaseCrests = new Sprite[9];
        public Sprite[] subPhaseSigils = new Sprite[5];

        public Sprite Icon(ItemDefinition item)
        {
            if (item == null) return null;
            switch (item.Kind)
            {
                case ItemKind.Diamond: return diamond;
                case ItemKind.Coin: return coin;
                case ItemKind.ArtifactMaterial: return artifactDust;
                case ItemKind.PhaseMaterial: return At(phaseCrests, item.Tier - 1);
                case ItemKind.SubPhaseMaterial: return At(subPhaseSigils, item.Tier);
                case ItemKind.SeriesMaterial: return item.SeriesId == "series.kof" ? kofTournamentInsignia : null;
                default: return null;
            }
        }

        private static Sprite At(Sprite[] sprites, int index) =>
            sprites != null && index >= 0 && index < sprites.Length ? sprites[index] : null;
    }
}
