using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Characters;
using Cards;
using Cards.Effects;
using HexGrid;

namespace Tests.Cards
{
    /// <summary>
    /// Steal effects: removes the top card of the victim's draw pile into the source's hand.
    /// The victim's deck simply loses the card (never reshuffled back), and the effect cannot
    /// fire against an empty draw pile or a full hand.
    /// </summary>
    [TestFixture]
    public class StealTopCardEffectTests
    {
        private static StealTopCardEffect LoadStealAsset()
        {
            var effect = AssetDatabase.LoadAssetAtPath<StealTopCardEffect>("Assets/CardEffect/Draw Cards/StealTopCardEffect_1.asset");
            Assert.IsNotNull(effect, "StealTopCardEffect_1 asset missing.");
            return effect;
        }

        private static CardContext Context(Character source, Character target, int range = 1)
        {
            return new CardContext { Source = source, TargetCharacter = target, Range = range };
        }

        [Test]
        public void Steal_MovesTopCardFromVictimsDeckIntoSourcesHand()
        {
            var grid = TestHelper.CreateTestGrid(3);
            Character source = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
            Character victim = TestHelper.CreateCharacterOnGrid(grid, 1, 0, 0);
            try
            {
                var victimCards = new List<CardData> { TestHelper.MakeCard("Victim A"), TestHelper.MakeCard("Victim B"), TestHelper.MakeCard("Victim C") };
                victim.Deck.SetStartingCards(victimCards);
                victim.Deck.Shuffle();
                CardData expectedTop = victim.Deck.DrawPile[0].Data;
                int beforeCount = victim.Deck.DrawPile.Count;

                LoadStealAsset().Execute(Context(source, victim));

                Assert.AreEqual(beforeCount - 1, victim.Deck.DrawPile.Count,
                    "The victim's draw pile should lose exactly one card.");
                Assert.AreEqual(1, source.Hand.Cards.Count, "The stolen card should land in the thief's hand.");
                Assert.AreSame(expectedTop, source.Hand.Cards[0].Data,
                    "The hand should contain the exact top card the victim held.");
            }
            finally
            {
                Object.DestroyImmediate(source.gameObject);
                Object.DestroyImmediate(victim.gameObject);
                Object.DestroyImmediate(grid.gameObject);
            }
        }

        [Test]
        public void Steal_NeverReshufflesDiscardIntoTheVictimsDeck()
        {
            var grid = TestHelper.CreateTestGrid(3);
            Character source = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
            Character victim = TestHelper.CreateCharacterOnGrid(grid, 1, 0, 0);
            try
            {
                var victimCards = new List<CardData> { TestHelper.MakeCard("Victim Only") };
                victim.Deck.SetStartingCards(victimCards);
                victim.Deck.Shuffle();

                LoadStealAsset().Execute(Context(source, victim));                    // draw pile: 1 -> 0
                victim.Deck.AddCard(TestHelper.MakeCard("Later"));                   // a card they own arrives late
                LoadStealAsset().Execute(Context(source, victim));                    // steals "Later" -> 0 again

                Assert.AreEqual(0, victim.Deck.DrawPile.Count,
                    "Stealing the last card must not conjure a reshuffle from an empty discard pile.");
                Assert.AreEqual(2, source.Hand.Cards.Count, "Each successful steal adds one card.");
            }
            finally
            {
                Object.DestroyImmediate(source.gameObject);
                Object.DestroyImmediate(victim.gameObject);
                Object.DestroyImmediate(grid.gameObject);
            }
        }

        [Test]
        public void Steal_IsUnplayableAgainstAnEmptyDeck()
        {
            var grid = TestHelper.CreateTestGrid(3);
            Character source = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
            Character victim = TestHelper.CreateCharacterOnGrid(grid, 1, 0, 0);
            try
            {
                var context = Context(source, victim); // victim deck starts empty

                Assert.IsFalse(LoadStealAsset().CanExecute(context),
                    "Steal must be unplayable when the victim's deck has no cards left.");
                LoadStealAsset().Execute(context);
                Assert.AreEqual(0, source.Hand.Cards.Count, "Executing against an empty deck should do nothing.");
            }
            finally
            {
                Object.DestroyImmediate(source.gameObject);
                Object.DestroyImmediate(victim.gameObject);
                Object.DestroyImmediate(grid.gameObject);
            }
        }

        [Test]
        public void Steal_IsUnplayableAgainstAFullHand()
        {
            var grid = TestHelper.CreateTestGrid(3);
            Character source = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
            Character victim = TestHelper.CreateCharacterOnGrid(grid, 1, 0, 0);
            try
            {
                var victimCards = new List<CardData> { TestHelper.MakeCard("Victim A") };
                victim.Deck.SetStartingCards(victimCards);
                victim.Deck.Shuffle();

                for (int i = 0; i < 5; i++)
                    source.Hand.AddCard(new CardInstance(TestHelper.MakeCard("Held " + i)));

                var context = Context(source, victim);
                Assert.IsFalse(LoadStealAsset().CanExecute(context),
                    "Steal must be unplayable when the thief's hand is full.");
                LoadStealAsset().Execute(context);
                Assert.AreEqual(5, source.Hand.Cards.Count, "A full hand must not receive the stolen card.");
                Assert.AreEqual(1, victim.Deck.DrawPile.Count, "The victim's deck must be untouched.");
            }
            finally
            {
                Object.DestroyImmediate(source.gameObject);
                Object.DestroyImmediate(victim.gameObject);
                Object.DestroyImmediate(grid.gameObject);
            }
        }

        [Test]
        public void Steal_RequiresTheTargetToBeInRange()
        {
            var grid = TestHelper.CreateTestGrid(4);
            Character source = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
            Character victim = TestHelper.CreateCharacterOnGrid(grid, 1, 2, 0); // 2 hexes away
            try
            {
                var victimCards = new List<CardData> { TestHelper.MakeCard("Victim A") };
                victim.Deck.SetStartingCards(victimCards);
                victim.Deck.Shuffle();

                var context = Context(source, victim, range: 1);
                Assert.IsFalse(LoadStealAsset().CanExecute(context),
                    "A victim two cells away must be out of range-1 reach.");
            }
            finally
            {
                Object.DestroyImmediate(source.gameObject);
                Object.DestroyImmediate(victim.gameObject);
                Object.DestroyImmediate(grid.gameObject);
            }
        }
    }
}