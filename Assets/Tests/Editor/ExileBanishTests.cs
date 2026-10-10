using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Cards;
using Cards.Effects;

namespace Tests.Cards
{
    /// <summary>
    /// The Exile and Banish execution cards, loaded by path so the assertions hold regardless
    /// of their registration state. Both implement "kill an enemy at low health": a single
    /// execution (KillTargetEffect) step at range 2. Exile costs 3, Banish costs 1.
    /// </summary>
    [TestFixture]
    public class ExileBanishTests
    {
        private const string ExilePath = "Assets/CardData/Combat/Magic/Exile.asset";
        private const string BanishPath = "Assets/CardData/Combat/Magic/Banish.asset";

        [Test]
        public void Exile_IsAnExecutionCard_AtRangeTwo_CostingThree()
        {
            AssertExecutionCard(ExilePath, "Exile", expectedCost: 3);
        }

        [Test]
        public void Banish_IsAnExecutionCard_AtRangeTwo_CostingOne()
        {
            AssertExecutionCard(BanishPath, "Banish", expectedCost: 1);
        }

        private static void AssertExecutionCard(string path, string display, int expectedCost)
        {
            var card = AssetDatabase.LoadAssetAtPath<CardData>(path);
            Assert.IsNotNull(card, $"{display} card asset missing at '{path}'.");
            Assert.AreEqual(expectedCost, card.Cost, $"{display} should cost {expectedCost}.");
            Assert.IsNotNull(card.Steps, $"{display} should define steps.");
            Assert.AreEqual(1, card.Steps.Count, $"{display} should be a single-step card.");
            var step = card.Steps[0];
            Assert.IsNotNull(step, $"{display} step should be present.");
            Assert.AreEqual(2, step.Range, $"{display} should reach range 2.");
            Assert.IsInstanceOf<KillTargetEffect>(step.Effect,
                $"{display} should use an execution (kill) effect.");
        }
    }
}