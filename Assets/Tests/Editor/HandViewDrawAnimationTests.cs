using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Cards;
using Characters;
using UI;

namespace Tests.UI
{
    [TestFixture]
    public class HandViewDrawAnimationTests
    {
        private readonly List<Object> owned = new List<Object>();
        private Character character;
        private HandView hand;

        [SetUp]
        public void SetUp()
        {
            character = TestHelper.CreateCharacter(0, 0, 0, 0);
            owned.Add(character.gameObject);
            owned.Add(character.CurrentCell.gameObject);

            var prefabGo = new GameObject("CardPrefab", typeof(RectTransform), typeof(Button), typeof(CardView));
            prefabGo.SetActive(false); // template only; never shown
            owned.Add(prefabGo);

            var handGo = new GameObject("HandView", typeof(RectTransform), typeof(HandView));
            owned.Add(handGo);
            hand = handGo.GetComponent<HandView>();

            var containerGo = new GameObject("CardContainer", typeof(RectTransform));
            owned.Add(containerGo);

            typeof(HandView).GetField("cardViewPrefab", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(hand, prefabGo.GetComponent<CardView>());
            typeof(HandView).GetField("cardContainer", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(hand, containerGo.transform);

            hand.SetCharacter(character);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in owned)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            owned.Clear();
        }

        private CardInstance DrawCard(string name)
        {
            var data = ScriptableObject.CreateInstance<CardData>();
            data.CardName = name;
            owned.Add(data);
            return new CardInstance(data);
        }

        [Test]
        public void DrawnCard_StartsADrawAnimation()
        {
            character.Hand.AddCard(DrawCard("A"));

            Assert.AreEqual(1, hand.DrawAnimationsStarted, "A newly drawn card must animate in.");
        }

        [Test]
        public void CardAlreadyInHand_DoesNotAnimateAgain_WhenAnotherIsDrawn()
        {
            character.Hand.AddCard(DrawCard("A"));
            character.Hand.AddCard(DrawCard("B"));

            Assert.AreEqual(2, hand.DrawAnimationsStarted,
                "Only the card that was just drawn animates; the one already in hand does not.");
        }

        [Test]
        public void PlayingACard_DoesNotAnimateTheRemainingHand()
        {
            var first = DrawCard("A");
            var second = DrawCard("B");
            character.Hand.AddCard(first);
            character.Hand.AddCard(second);
            int before = hand.DrawAnimationsStarted;

            character.Hand.RemoveCard(first);

            Assert.AreEqual(before, hand.DrawAnimationsStarted,
                "Removing a card rebuilds the hand but must not replay draw animations.");
        }
    }
}
