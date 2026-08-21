using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Cards;

namespace Tests.Cards
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
            Assert.AreEqual(0, deck.DrawPile.Count);
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

        [Test]
        public void AddCard_BeforeShuffle_JoinsStartingListOnly()
        {
            deck.SetStartingCards(new List<CardData> { cardDataA });
            deck.AddCard(cardDataB);

            Assert.AreEqual(0, deck.DrawPile.Count, "Pre-shuffle the draw pile does not exist yet.");

            deck.Shuffle();
            Assert.AreEqual(2, deck.DrawPile.Count);
        }

        [Test]
        public void AddCard_AfterShuffle_InjectsIntoDrawPile()
        {
            deck.SetStartingCards(new List<CardData> { cardDataA });
            deck.Shuffle();

            deck.AddCard(cardDataB);

            Assert.AreEqual(2, deck.DrawPile.Count, "Mid-match grants must be drawable immediately.");

            bool found = false;
            foreach (CardInstance card in deck.DrawPile)
                if (card.Data == cardDataB) found = true;
            Assert.IsTrue(found);
        }

        [Test]
        public void AddCard_AfterShuffle_RecordedInStartingList()
        {
            deck.SetStartingCards(new List<CardData> { cardDataA });
            deck.Shuffle();
            deck.AddCard(cardDataB);

            Assert.AreEqual(cardDataB, deck.GetCardDataByName("Card B"),
                "Granted cards must be resolvable by name for hand-sync rebuilds.");
        }

        [Test]
        public void AddCard_Null_DoesNothing()
        {
            deck.SetStartingCards(new List<CardData> { cardDataA });
            deck.Shuffle();

            deck.AddCard(null);

            Assert.AreEqual(1, deck.DrawPile.Count);
        }

        [Test]
        public void SetStartingCards_ResetsShuffleState()
        {
            deck.SetStartingCards(new List<CardData> { cardDataA });
            deck.Shuffle();
            // After a re-Set the deck is considered pre-shuffle again: AddCard must not
            // inject into the stale draw pile. Piles keep their old contents until the
            // next Shuffle rebuilds them from the new starting list.
            deck.SetStartingCards(new List<CardData> { cardDataA });
            deck.AddCard(cardDataB);

            Assert.AreEqual(1, deck.DrawPile.Count);
            Assert.AreEqual(cardDataA, deck.DrawPile[0].Data);

            deck.Shuffle();
            Assert.AreEqual(2, deck.DrawPile.Count);
        }

        [Test]
        public void RemoveCard_RemovesFromDrawPileAndStartingList()
        {
            deck.SetStartingCards(new List<CardData> { cardDataA });
            deck.Shuffle();
            deck.AddCard(cardDataB);

            bool removed = deck.RemoveCard(cardDataB);

            Assert.IsTrue(removed);
            foreach (CardInstance card in deck.DrawPile)
                Assert.AreNotEqual(cardDataB, card.Data);
            Assert.IsNull(deck.GetCardDataByName("Card B"));
        }

        [Test]
        public void RemoveCard_FallsBackToDiscardPile()
        {
            deck.SetStartingCards(new List<CardData> { cardDataA });
            deck.Shuffle();
            CardInstance drawn = deck.Draw();
            deck.Discard(drawn);

            bool removed = deck.RemoveCard(cardDataA);

            Assert.IsTrue(removed);
            Assert.AreEqual(0, deck.DiscardPile.Count);
        }

        [Test]
        public void RemoveCard_NotPresent_ReturnsFalse()
        {
            deck.SetStartingCards(new List<CardData> { cardDataA });
            deck.Shuffle();

            Assert.IsFalse(deck.RemoveCard(cardDataB));
        }

        [Test]
        public void RemoveCard_Null_ReturnsFalse()
        {
            Assert.IsFalse(deck.RemoveCard(null));
        }
    }
}
