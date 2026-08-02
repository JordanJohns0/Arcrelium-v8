using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Cards;
using UI;
using Object = UnityEngine.Object;

namespace Tests
{
    [TestFixture]
    public class DeckListTests
    {
        private readonly List<ScriptableObject> trackedSops = new List<ScriptableObject>();

        private CardData MakeCard(string name, int cost)
        {
            var data = TestHelper.MakeCard(name, TargetType.Self, cost);
            trackedSops.Add(data);
            return data;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var so in trackedSops)
            {
                if (so != null)
                    Object.DestroyImmediate(so);
            }
            trackedSops.Clear();
        }

        [Test]
        public void CondenseDeck_CombinesDuplicateCards()
        {
            var list = new List<CardData>
            {
                MakeCard("Slash", 1),
                MakeCard("Slash", 1),
                MakeCard("Slash", 1),
                MakeCard("Block", 0)
            };

            var result = DeckListUI.CondenseDeck(list);

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual("Block", result[0].Card.CardName);
            Assert.AreEqual(1, result[0].Count);
            Assert.AreEqual("Slash", result[1].Card.CardName);
            Assert.AreEqual(3, result[1].Count);
        }

        [Test]
        public void CondenseDeck_OrdersByCostThenName()
        {
            var list = new List<CardData>
            {
                MakeCard("Zephyr", 3),
                MakeCard("Aegis", 0),
                MakeCard("Bolt", 1),
                MakeCard("Arc", 1)
            };

            var result = DeckListUI.CondenseDeck(list);

            Assert.AreEqual(4, result.Count);
            Assert.AreEqual("Aegis", result[0].Card.CardName);
            Assert.AreEqual("Arc", result[1].Card.CardName);
            Assert.AreEqual("Bolt", result[2].Card.CardName);
            Assert.AreEqual("Zephyr", result[3].Card.CardName);
        }

        [Test]
        public void CondenseDeck_IsCaseInsensitiveAlphabetical()
        {
            var list = new List<CardData>
            {
                MakeCard("banana", 0),
                MakeCard("Apple", 0)
            };

            var result = DeckListUI.CondenseDeck(list);

            Assert.AreEqual("Apple", result[0].Card.CardName);
            Assert.AreEqual("banana", result[1].Card.CardName);
        }

        [Test]
        public void CondenseDeck_DropsNullCards()
        {
            var result = DeckListUI.CondenseDeck(new List<CardData> { MakeCard("Solo", 0), null });

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Solo", result[0].Card.CardName);
        }

        [Test]
        public void CondenseDeck_NullInput_ReturnsEmpty()
        {
            Assert.IsEmpty(DeckListUI.CondenseDeck(null));
        }
    }
}
