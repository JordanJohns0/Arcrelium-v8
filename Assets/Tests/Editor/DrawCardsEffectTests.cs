using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Cards;
using Cards.Effects;
using Characters;

namespace Tests.Cards
{
    [TestFixture]
    public class DrawCardsEffectTests
    {
        private DrawCardsEffect effect;
        private Character source;

        [SetUp]
        public void SetUp()
        {
            effect = ScriptableObject.CreateInstance<DrawCardsEffect>();
            effect.Amount = CardValue.MakeConstant(2);

            source = TestHelper.CreateCharacter(0, 0, 0);

            var deck = (Deck)typeof(Character).GetField("deck", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(source);
            var cardA = ScriptableObject.CreateInstance<CardData>();
            cardA.CardName = "A";
            var cardB = ScriptableObject.CreateInstance<CardData>();
            cardB.CardName = "B";
            var cardC = ScriptableObject.CreateInstance<CardData>();
            cardC.CardName = "C";
            deck.SetStartingCards(new List<CardData> { cardA, cardB, cardC });
            deck.Shuffle();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(source.CurrentCell.gameObject);
            Object.DestroyImmediate(source.gameObject);
        }

        [Test]
        public void Execute_DrawsCards()
        {
            int handBefore = source.Hand.Cards.Count;
            var ctx = new CardContext { Source = source };
            effect.Execute(ctx);
            Assert.AreEqual(handBefore + 2, source.Hand.Cards.Count);
        }

        [Test]
        public void Execute_DoesNotExceedHandCapacity()
        {
            source.Hand.MaxCapacity = source.Hand.Cards.Count + 1;
            var ctx = new CardContext { Source = source };
            effect.Execute(ctx);
            Assert.AreEqual(source.Hand.MaxCapacity, source.Hand.Cards.Count);
        }

        [Test]
        public void Execute_StepCardModifiers_AppliedToEveryDrawnCard()
        {
            var setCost = ScriptableObject.CreateInstance<SetCardCostModifier>();
            setCost.Cost = 2;

            var ctx = new CardContext { Source = source, StepModifiers = new List<CardModifierSO> { setCost } };
            effect.Execute(ctx);

            Assert.AreEqual(2, source.Hand.Cards.Count);
            foreach (CardInstance card in source.Hand.Cards)
                Assert.AreEqual(2, card.Cost, "The attached option must touch every drawn card.");
        }

        [Test]
        public void Execute_WithoutCardModifiers_DrawnCostsUnchanged()
        {
            var ctx = new CardContext { Source = source };
            effect.Execute(ctx);

            Assert.AreEqual(2, source.Hand.Cards.Count);
            foreach (CardInstance card in source.Hand.Cards)
                Assert.AreEqual(0, card.Cost, "No option attached means drawn cards keep their base cost.");
        }
    }
}
