using FightingAllstar.Core.Combat;
using FightingAllstar.Presentation.Combat;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace FightingAllstar.EditorTools
{
    [TestFixture]
    public sealed class FloatingCombatTextPoolTests
    {
        [Test]
        public void Initialize_PrewarmsCapacity()
        {
            var pool = new FloatingCombatTextPool();
            var parent = new VisualElement();
            pool.Initialize(parent, 10);

            Assert.AreEqual(10, pool.TotalCount);
            Assert.AreEqual(0, pool.ActiveCount);
            Assert.AreEqual(10, pool.InactiveCount);
            Assert.AreEqual(10, parent.childCount);
        }

        [Test]
        public void Acquire_SetsTextAndStylesCorrectly()
        {
            var pool = new FloatingCombatTextPool();
            var parent = new VisualElement();
            pool.Initialize(parent, 5);

            var item = pool.Acquire("12,345", "damage");
            Assert.AreEqual(1, pool.ActiveCount);
            Assert.AreEqual(4, pool.InactiveCount);
            Assert.AreEqual("12,345", item.Label.text);
            Assert.IsTrue(item.Label.ClassListContains("damage"));
            Assert.AreEqual(DisplayStyle.None, item.Kicker.style.display.value);
            Assert.AreEqual(DisplayStyle.Flex, item.Group.style.display.value);
        }

        [Test]
        public void Acquire_Multiline_PopulatesKicker()
        {
            var pool = new FloatingCombatTextPool();
            var parent = new VisualElement();
            pool.Initialize(parent, 5);

            var item = pool.Acquire("CRITICAL\n99,999", "critical");
            Assert.AreEqual("CRITICAL", item.Kicker.text);
            Assert.AreEqual("99,999", item.Label.text);
            Assert.AreEqual(DisplayStyle.Flex, item.Kicker.style.display.value);
            Assert.IsTrue(item.Kicker.ClassListContains("critical"));
            Assert.IsTrue(item.Label.ClassListContains("critical"));
        }

        [Test]
        public void Release_ReturnsItemToPoolAndHides()
        {
            var pool = new FloatingCombatTextPool();
            var parent = new VisualElement();
            pool.Initialize(parent, 5);

            var item = pool.Acquire("Test", "status");
            Assert.AreEqual(1, pool.ActiveCount);

            pool.Release(item);
            Assert.AreEqual(0, pool.ActiveCount);
            Assert.AreEqual(5, pool.InactiveCount);
            Assert.AreEqual(DisplayStyle.None, item.Group.style.display.value);
        }

        [Test]
        public void Pool_ExpandsWhenCapacityExceeded()
        {
            var pool = new FloatingCombatTextPool();
            var parent = new VisualElement();
            pool.Initialize(parent, 2);

            var item1 = pool.Acquire("Hit 1", "damage");
            var item2 = pool.Acquire("Hit 2", "damage");
            var item3 = pool.Acquire("Hit 3", "damage");

            Assert.AreEqual(3, pool.ActiveCount);
            Assert.AreEqual(3, pool.TotalCount);
            Assert.AreEqual(0, pool.InactiveCount);
            Assert.AreEqual(3, parent.childCount);
        }

        [Test]
        public void Clear_ReleasesAllActiveItems()
        {
            var pool = new FloatingCombatTextPool();
            var parent = new VisualElement();
            pool.Initialize(parent, 5);

            pool.Acquire("Text 1", "heal");
            pool.Acquire("Text 2", "passive");
            pool.Acquire("Text 3", "status");

            Assert.AreEqual(3, pool.ActiveCount);

            pool.Clear();
            Assert.AreEqual(0, pool.ActiveCount);
            Assert.AreEqual(5, pool.InactiveCount);
        }

        [Test]
        public void CalculateFontSize_ClampsAndScales()
        {
            var critSize = FloatingCombatTextPool.CalculateFontSize("36,042", "critical");
            var statusSize = FloatingCombatTextPool.CalculateFontSize("Attack Increase", "status");
            var longTextSize = FloatingCombatTextPool.CalculateFontSize("Extremely Long Combat Status Effect Message That Exceeds Width", "status");

            Assert.AreEqual(46f, critSize);
            Assert.AreEqual(21f, statusSize);
            Assert.IsTrue(longTextSize < statusSize);
            Assert.IsTrue(longTextSize >= 16f);
        }

        [Test]
        public void Acquire_WithAdvantage_ShowsRedArrow()
        {
            var pool = new FloatingCombatTextPool();
            var parent = new VisualElement();
            pool.Initialize(parent, 5);

            var item = pool.Acquire("14,500", "damage", AttributeAffinity.Advantage);
            Assert.AreEqual(DisplayStyle.Flex, item.Arrow.style.display.value);
            Assert.IsTrue(item.Arrow.ClassListContains("advantage"));
            Assert.IsFalse(item.Arrow.ClassListContains("disadvantage"));
            Assert.AreEqual(AttributeAffinity.Advantage, item.CurrentAffinity);
            Assert.AreEqual("14,500", item.Label.text);
        }

        [Test]
        public void Acquire_WithDisadvantage_ShowsBlueArrow()
        {
            var pool = new FloatingCombatTextPool();
            var parent = new VisualElement();
            pool.Initialize(parent, 5);

            var item = pool.Acquire("7,200", "damage", AttributeAffinity.Disadvantage);
            Assert.AreEqual(DisplayStyle.Flex, item.Arrow.style.display.value);
            Assert.IsTrue(item.Arrow.ClassListContains("disadvantage"));
            Assert.IsFalse(item.Arrow.ClassListContains("advantage"));
            Assert.AreEqual(AttributeAffinity.Disadvantage, item.CurrentAffinity);
            Assert.AreEqual("7,200", item.Label.text);
        }

        [Test]
        public void Acquire_WithCriticalAndAdvantage_ShowsKickerAndRedArrow()
        {
            var pool = new FloatingCombatTextPool();
            var parent = new VisualElement();
            pool.Initialize(parent, 5);

            var item = pool.Acquire("CRITICAL\n36,042", "critical", AttributeAffinity.Advantage);
            Assert.AreEqual("CRITICAL", item.Kicker.text);
            Assert.AreEqual(DisplayStyle.Flex, item.Kicker.style.display.value);
            Assert.AreEqual("36,042", item.Label.text);
            Assert.AreEqual(DisplayStyle.Flex, item.Arrow.style.display.value);
            Assert.IsTrue(item.Arrow.ClassListContains("advantage"));
        }

        [Test]
        public void Acquire_WithBlockedAndDisadvantage_ShowsKickerAndBlueArrow()
        {
            var pool = new FloatingCombatTextPool();
            var parent = new VisualElement();
            pool.Initialize(parent, 5);

            var item = pool.Acquire("BLOCK\n3,120", "blocked", AttributeAffinity.Disadvantage);
            Assert.AreEqual("BLOCK", item.Kicker.text);
            Assert.AreEqual(DisplayStyle.Flex, item.Kicker.style.display.value);
            Assert.AreEqual("3,120", item.Label.text);
            Assert.AreEqual(DisplayStyle.Flex, item.Arrow.style.display.value);
            Assert.IsTrue(item.Arrow.ClassListContains("disadvantage"));
        }

        [Test]
        public void Release_ResetsArrowStyle()
        {
            var pool = new FloatingCombatTextPool();
            var parent = new VisualElement();
            pool.Initialize(parent, 5);

            var item = pool.Acquire("25,000", "damage", AttributeAffinity.Advantage);
            Assert.AreEqual(DisplayStyle.Flex, item.Arrow.style.display.value);
            Assert.IsTrue(item.Arrow.ClassListContains("advantage"));

            pool.Release(item);
            Assert.AreEqual(DisplayStyle.None, item.Arrow.style.display.value);
            Assert.IsFalse(item.Arrow.ClassListContains("advantage"));
            Assert.AreEqual(AttributeAffinity.Neutral, item.CurrentAffinity);
        }
    }
}
