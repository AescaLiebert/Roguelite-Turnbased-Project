using System.Collections.Generic;
using System.Linq;
using FightingAllstar.Presentation.Navigation;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FightingAllstar.EditorTools
{
    public sealed class SummonDrawPresentationTests
    {
        private readonly List<CharacterObject> _fighters = new List<CharacterObject>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var character in _fighters) Object.DestroyImmediate(character);
            _fighters.Clear();
        }

        [Test]
        public void TenPull_ClassifiesNewAndRepeatedDefinitionsInReceiptOrder()
        {
            var owned = Fighter("fighter.owned");
            var first = Fighter("fighter.first");
            var second = Fighter("fighter.second");
            var draws = new[] { owned, first, second, first, owned, first, second, owned, first, second };
            var receipt = SummonDrawPresentation.Classify(draws, new[] { owned.DefinitionId });
            CollectionAssert.AreEqual(new[] { false, true, true, false, false, false, false, false, false, false },
                receipt.Select(draw => draw.IsNew));
            CollectionAssert.AreEqual(draws, receipt.Select(draw => draw.Character));
            Assert.That(receipt.Where(draw => draw.IsDuplicate).All(draw => draw.FragmentCount == 0),
                "Identity-only input cannot claim that every duplicate granted a token.");
        }

        [Test]
        public void Classification_UsesDefinitionIdentityAcrossDifferentAssetInstances()
        {
            var first = Fighter("fighter.same");
            var second = Fighter("fighter.same");
            var receipt = SummonDrawPresentation.Classify(new[] { first, second }, new string[0]);
            Assert.IsTrue(receipt[0].IsNew);
            Assert.IsTrue(receipt[1].IsDuplicate);
        }

        [Test]
        public void KnownConstellation_CrossingC5GrantsTokensOnlyOnLaterDuplicates()
        {
            var character = Fighter("fighter.c4");
            var tiers = new Dictionary<string, int> { [character.DefinitionId] = 4 };
            var receipt = SummonDrawPresentation.Classify(new[] { character, character, character }, tiers.Keys, tiers);
            CollectionAssert.AreEqual(new[] { 5, 5, 5 }, receipt.Select(draw => draw.ConstellationTier));
            CollectionAssert.AreEqual(new[] { 0, 1, 1 }, receipt.Select(draw => draw.FragmentCount));
            Assert.That(receipt.All(draw => draw.IsDuplicate));
            Assert.AreEqual(4, tiers[character.DefinitionId], "Presentation must not mutate its pre-purchase snapshot.");
        }

        [Test]
        public void NewRepeatedFighter_UnlocksThenAdvancesC1ThroughC5BeforeTokens()
        {
            var character = Fighter("fighter.new");
            var receipt = SummonDrawPresentation.Classify(Enumerable.Repeat(character, 7).ToArray(), new string[0],
                new Dictionary<string, int>());
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3, 4, 5, 5 }, receipt.Select(draw => draw.ConstellationTier));
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 0, 0, 1 }, receipt.Select(draw => draw.FragmentCount));
            Assert.IsTrue(receipt[0].IsNew);
            Assert.That(receipt.Skip(1).All(draw => draw.IsDuplicate));
        }

        [Test]
        public void UnknownOwnedProgression_DoesNotInventConstellationOrTokenPayout()
        {
            var character = Fighter("fighter.unknown");
            var receipt = SummonDrawPresentation.Classify(new[] { character, character }, new[] { character.DefinitionId });
            Assert.That(receipt.All(draw => draw.IsDuplicate && draw.FragmentCount == 0 && draw.ConstellationTier == -1));
        }

        [Test]
        public void Classification_DoesNotAccessAccountOrAdvanceUnityRandomStream()
        {
            var character = Fighter("fighter.preview");
            var randomBefore = Random.state;
            var inventoryBefore = PlayerInventoryService.Instance;
            var receipt = SummonDrawPresentation.Classify(new[] { character }, null);
            Assert.IsTrue(receipt[0].IsNew);
            Assert.AreEqual(randomBefore, Random.state);
            Assert.AreSame(inventoryBefore, PlayerInventoryService.Instance);
        }

        [Test]
        public void MissingDrawsAndNullEntries_AreSafeWithoutChangingOrder()
        {
            var character = Fighter("fighter.valid");
            Assert.IsEmpty(SummonDrawPresentation.Classify(null, null));
            var receipt = SummonDrawPresentation.Classify(new[] { null, character, null, character }, null);
            Assert.AreEqual(2, receipt.Count);
            Assert.IsTrue(receipt[0].IsNew);
            Assert.IsTrue(receipt[1].IsDuplicate);
        }

        private CharacterObject Fighter(string definitionId)
        {
            var character = ScriptableObject.CreateInstance<CharacterObject>();
            _fighters.Add(character);
            var serialized = new SerializedObject(character);
            serialized.FindProperty("definitionId").stringValue = definitionId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return character;
        }
    }
}
