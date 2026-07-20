using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Cards;

namespace Tests
{
    [TestFixture]
    public class DeckTests
    {
        private Deck deck;
        private CardData cardDataA;
        private CardData cardDataB;

        [SetUp]
        public void SetUp()
        {
            deck = new Deck();
            cardDataA = ScriptableObject.CreateInstance<CardData>();
            cardDataA.CardName = "Card A";
            cardDataB = ScriptableObject.CreateInstance<CardData>();
            cardDataB.CardName = "Card B";
        }

        [Test]
        public void Shuffle_PopulatesDrawPile()
        {
            deck.SetStartingCards(new List<CardData> { cardDataA, cardDataB });

            deck.Shuffle();

            Assert.AreEqual(2, deck.DrawPile.Count);
            Assert.AreEqual(0, deck.DiscardPile.Count);
        }

        [Test]
        public void Draw_RemovesFromDrawPile()
        {
            deck.SetStartingCards(new List<CardData> { cardDataA });
            deck.Shuffle();

            CardInstance drawn = deck.Draw();

            Assert.IsNotNull(drawn);
            Assert.AreEqual(cardDataA, drawn.Data);
            Assert.AreEqual(0, deck.DrawPile.Count);
        }

        [Test]
        public void Draw_EmptyDrawPile_ReshufflesDiscard()
        {
            deck.SetStartingCards(new List<CardData> { cardDataA });
            deck.Shuffle();

            CardInstance drawn = deck.Draw();
            deck.Discard(drawn);

            Assert.AreEqual(0, deck.DrawPile.Count);
            Assert.AreEqual(1, deck.DiscardPile.Count);

            CardInstance reshuffled = deck.Draw();

            Assert.IsNotNull(reshuffled);
            Assert.AreEqual(0, deck.DiscardPile.Count);
            Assert.AreEqual(1, deck.DrawPile.Count);
        }

        [Test]
        public void Draw_CompletelyEmpty_ReturnsNull()
        {
            deck.SetStartingCards(new List<CardData>());
            deck.Shuffle();

            CardInstance drawn = deck.Draw();

            Assert.IsNull(drawn);
        }

        [Test]
        public void Discard_AddsToDiscardPile()
        {
            deck.SetStartingCards(new List<CardData> { cardDataA });
            deck.Shuffle();
            CardInstance card = deck.Draw();

            deck.Discard(card);

            Assert.AreEqual(1, deck.DiscardPile.Count);
            Assert.AreEqual(card, deck.DiscardPile[0]);
        }

        [Test]
        public void Shuffle_ClearsAllPiles()
        {
            deck.SetStartingCards(new List<CardData> { cardDataA });
            deck.Shuffle();
            deck.Draw();
            deck.Discard(deck.DrawPile.Count > 0 ? deck.DrawPile[0] : null);

            deck.Shuffle();

            Assert.AreEqual(0, deck.DiscardPile.Count);
        }

        [Test]
        public void SetStartingCards_Null_DefaultsEmpty()
        {
            deck.SetStartingCards(null);
            deck.Shuffle();

            Assert.AreEqual(0, deck.DrawPile.Count);
        }

        [Test]
        public void Shuffle_CreatesUniqueInstances()
        {
            deck.SetStartingCards(new List<CardData> { cardDataA, cardDataA });
            deck.Shuffle();

            Assert.AreEqual(2, deck.DrawPile.Count);
            Assert.AreNotSame(deck.DrawPile[0], deck.DrawPile[1]);
            Assert.AreEqual(deck.DrawPile[0].Data, deck.DrawPile[1].Data);
        }

        [Test]
        public void Draw_AllCards_DepletesPile()
        {
            deck.SetStartingCards(new List<CardData> { cardDataA, cardDataB });
            deck.Shuffle();

            deck.Draw();
            deck.Draw();

            Assert.AreEqual(0, deck.DrawPile.Count);
        }
    }
}
