using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FightingAllstar.EditorTools
{
    public sealed class SummonBannerConfigurationTests
    {
        [TestCase(FighterRarity.R)]
        [TestCase(FighterRarity.SR)]
        public void SharedPool_UsesRegistryInsteadOfAuthoredList(FighterRarity rarity)
        {
            var banner = ScriptableObject.CreateInstance<SummonBannerConfigurationSO>();
            try
            {
                var serialized = new SerializedObject(banner);
                var pools = serialized.FindProperty("rarityPools");
                pools.arraySize = 1;
                var pool = pools.GetArrayElementAtIndex(0);
                pool.FindPropertyRelative("rarity").intValue = (int)rarity;
                pool.FindPropertyRelative("characters").arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var expected = CharacterObjectRegistrySO.LoadAll().Where(c => c.FighterRarity == rarity).ToArray();
                Assert.IsNotEmpty(expected, "Fixture needs authored characters of this rarity.");
                CollectionAssert.AreEqual(expected, banner.RarityPools[0].Characters);
            }
            finally { Object.DestroyImmediate(banner); }
        }

        [TestCase(FighterRarity.SSR)]
        [TestCase(FighterRarity.FES)]
        [TestCase(FighterRarity.EX)]
        [TestCase(FighterRarity.UE)]
        [TestCase(FighterRarity.LR)]
        public void HigherRarities_UseOnlyBannerListAndValidate(FighterRarity rarity)
        {
            var banner = ScriptableObject.CreateInstance<SummonBannerConfigurationSO>();
            var character = ScriptableObject.CreateInstance<CharacterObject>();
            try
            {
                var fighter = new SerializedObject(character);
                fighter.FindProperty("definitionId").stringValue = "test.banner.fighter";
                fighter.FindProperty("seriesId").stringValue = "series.test";
                fighter.FindProperty("FighterRarity").intValue = (int)rarity;
                fighter.ApplyModifiedPropertiesWithoutUndo();
                var serialized = new SerializedObject(banner);
                serialized.FindProperty("seriesId").stringValue = "series.test";
                var pools = serialized.FindProperty("rarityPools");
                pools.arraySize = 1;
                var pool = pools.GetArrayElementAtIndex(0);
                pool.FindPropertyRelative("rarity").intValue = (int)rarity;
                pool.FindPropertyRelative("rateBasisPoints").intValue = 10000;
                var characters = pool.FindPropertyRelative("characters");
                characters.arraySize = 1;
                characters.GetArrayElementAtIndex(0).objectReferenceValue = character;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                CollectionAssert.AreEqual(new[] { character }, banner.RarityPools[0].Characters);
                Assert.IsTrue(banner.Validate(out var error), error);
                fighter.FindProperty("seriesId").stringValue = "series.other";
                fighter.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsFalse(banner.Validate(out _), "Banner-specific characters must still match its series.");
            }
            finally
            {
                Object.DestroyImmediate(banner);
                Object.DestroyImmediate(character);
            }
        }

        [Test]
        public void SharedPool_ValidatesAcrossSeries()
        {
            var banner = ScriptableObject.CreateInstance<SummonBannerConfigurationSO>();
            try
            {
                var serialized = new SerializedObject(banner);
                serialized.FindProperty("seriesId").stringValue = "series.unrelated";
                var pools = serialized.FindProperty("rarityPools");
                pools.arraySize = 1;
                var pool = pools.GetArrayElementAtIndex(0);
                pool.FindPropertyRelative("rarity").intValue = (int)FighterRarity.R;
                pool.FindPropertyRelative("rateBasisPoints").intValue = 10000;
                pool.FindPropertyRelative("characters").arraySize = 0;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsTrue(banner.Validate(out var error), error);
            }
            finally { Object.DestroyImmediate(banner); }
        }
    }
}
