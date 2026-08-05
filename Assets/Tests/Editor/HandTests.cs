using NUnit.Framework;
using UnityEngine;
using Cards;

namespace Tests.Cards
{
    [TestFixture]
    public class HandTests
    {
        private Hand hand;

        [SetUp]
        public void SetUp()
        {
            hand = new Hand();
            hand.MaxCapacity = 6;
        }

        [Test]
        public void NewHand_IsEmpty()
        {
            Assert.AreEqual(0, hand.Cards.Count);
            Assert.IsFalse(hand.IsFull);
        }

        [Test]
        public void AddCard_IncreasesCount()
        {
            var card = new CardInstance(ScriptableObject.CreateInstance<CardData>());

            hand.AddCard(card);

            Assert.AreEqual(1, hand.Cards.Count);
            Assert.IsTrue(hand.Contains(card));
        }

        [Test]
        public void RemoveCard_DecreasesCount()
        {
            var card = new CardInstance(ScriptableObject.CreateInstance<CardData>());
            hand.AddCard(card);

            bool removed = hand.RemoveCard(card);

            Assert.IsTrue(removed);
            Assert.AreEqual(0, hand.Cards.Count);
            Assert.IsFalse(hand.Contains(card));
        }

        [Test]
        public void RemoveCard_NotInHand_ReturnsFalse()
        {
            var card = new CardInstance(ScriptableObject.CreateInstance<CardData>());

            bool removed = hand.RemoveCard(card);

            Assert.IsFalse(removed);
        }

        [Test]
        public void IsFull_TrueAtCapacity()
        {
            hand.MaxCapacity = 2;

            hand.AddCard(new CardInstance(ScriptableObject.CreateInstance<CardData>()));
            Assert.IsFalse(hand.IsFull);

            hand.AddCard(new CardInstance(ScriptableObject.CreateInstance<CardData>()));
            Assert.IsTrue(hand.IsFull);
        }

        [Test]
        public void OnHandChanged_FiresOnAdd()
        {
            bool fired = false;
            hand.OnHandChanged += () => fired = true;

            hand.AddCard(new CardInstance(ScriptableObject.CreateInstance<CardData>()));

            Assert.IsTrue(fired);
        }

        [Test]
        public void OnHandChanged_FiresOnRemove()
        {
            var card = new CardInstance(ScriptableObject.CreateInstance<CardData>());
            hand.AddCard(card);

            bool fired = false;
            hand.OnHandChanged += () => fired = true;

            hand.RemoveCard(card);

            Assert.IsTrue(fired);
        }

        [Test]
        public void OnHandChanged_DoesNotFireOnFailedRemove()
        {
            var card = new CardInstance(ScriptableObject.CreateInstance<CardData>());

            bool fired = false;
            hand.OnHandChanged += () => fired = true;

            hand.RemoveCard(card);

            Assert.IsFalse(fired);
        }

        [Test]
        public void Contains_ReturnsFalseForAbsentCard()
        {
            var card = new CardInstance(ScriptableObject.CreateInstance<CardData>());

            Assert.IsFalse(hand.Contains(card));
        }

        [Test]
        public void MultipleCards_AllTracked()
        {
            var c1 = new CardInstance(ScriptableObject.CreateInstance<CardData>());
            var c2 = new CardInstance(ScriptableObject.CreateInstance<CardData>());
            var c3 = new CardInstance(ScriptableObject.CreateInstance<CardData>());

            hand.AddCard(c1);
            hand.AddCard(c2);
            hand.AddCard(c3);

            Assert.AreEqual(3, hand.Cards.Count);
            Assert.IsTrue(hand.Contains(c1));
            Assert.IsTrue(hand.Contains(c2));
            Assert.IsTrue(hand.Contains(c3));
        }
    }
}
