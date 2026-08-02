using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Cards;
using Characters;
using Game;

namespace Tests.PlayMode
{
    [TestFixture]
    public class TurnManagerPlayModeTests
    {
        private TurnManager turnManager;
        private ActionResolver actionResolver;
        private Character player;
        private Character enemy;
        private readonly List<ScriptableObject> trackedSops = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            var tmGo = new GameObject("TurnManager");
            turnManager = tmGo.AddComponent<TurnManager>();

            var arGo = new GameObject("ActionResolver");
            actionResolver = arGo.AddComponent<ActionResolver>();

            typeof(TurnManager).GetField("actionResolver", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(turnManager, actionResolver);

            player = TestHelper.CreateCharacter(0, 0, 0);
            enemy = TestHelper.CreateCharacter(1, 1, 0);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(turnManager.gameObject);
            Object.DestroyImmediate(actionResolver.gameObject);
            Object.DestroyImmediate(player.CurrentCell.gameObject);
            Object.DestroyImmediate(player.gameObject);
            Object.DestroyImmediate(enemy.CurrentCell.gameObject);
            Object.DestroyImmediate(enemy.gameObject);

            foreach (var so in trackedSops)
            {
                if (so != null)
                    Object.DestroyImmediate(so);
            }
            trackedSops.Clear();
        }

        [UnityTest]
        public IEnumerator RemoveFromTurnOrder_RemovesCharacter()
        {
            turnManager.SetTurnOrder(new List<Character> { player, enemy });
            turnManager.StartMatch();

            turnManager.RemoveFromTurnOrder(enemy);

            var turnStarted = new List<Character>();
            turnManager.OnTurnStarted += (c) => turnStarted.Add(c);

            turnManager.Pass();
            yield return TestHelper.WaitUntilOrTimeout(() => !turnManager.Busy);
            Assert.IsFalse(turnManager.Busy, "Turn transition did not finish in time");

            Assert.AreEqual(1, turnStarted.Count);
            Assert.AreEqual(player, turnStarted[0]);
        }

        [UnityTest]
        public IEnumerator SinglePlayer_AlwaysActive()
        {
            turnManager.SetTurnOrder(new List<Character> { player });
            turnManager.StartMatch();

            var turnCount = 0;
            turnManager.OnTurnStarted += (c) => turnCount++;

            turnManager.Pass();
            yield return TestHelper.WaitUntilOrTimeout(() => !turnManager.Busy);
            Assert.IsFalse(turnManager.Busy, "Turn transition did not finish in time");

            Assert.GreaterOrEqual(turnCount, 1);
            Assert.AreEqual(player, turnManager.ActiveCharacter);
        }

        [UnityTest]
        public IEnumerator TurnCycle_AdvancesToNextCharacter()
        {
            turnManager.SetTurnOrder(new List<Character> { player, enemy });

            var turnStarted = new List<Character>();
            turnManager.OnTurnStarted += (c) => turnStarted.Add(c);

            turnManager.StartMatch();
            turnManager.Pass();
            yield return TestHelper.WaitUntilOrTimeout(() => !turnManager.Busy);
            Assert.IsFalse(turnManager.Busy, "Turn transition did not finish in time");

            Assert.AreEqual(2, turnStarted.Count);
            Assert.AreEqual(player, turnStarted[0]);
            Assert.AreEqual(enemy, turnStarted[1]);
        }

        [Test]
        public void TurnStart_DrawsUpToMaxHandSize()
        {
            SetMaxHandSize(player, 3);
            GiveDeckCards(player, 8);

            turnManager.SetTurnOrder(new List<Character> { player, enemy });
            turnManager.StartMatch();

            Assert.AreEqual(3, player.Hand.Cards.Count);
        }

        [Test]
        public void TurnStart_StopsWhenDeckRunsOut()
        {
            SetMaxHandSize(player, 5);
            GiveDeckCards(player, 2);

            turnManager.SetTurnOrder(new List<Character> { player, enemy });
            turnManager.StartMatch();

            Assert.AreEqual(2, player.Hand.Cards.Count);
        }

        [UnityTest]
        public IEnumerator Pass_EndOfTurn_DiscardsHand()
        {
            GiveDeckCards(player, 8);

            turnManager.SetTurnOrder(new List<Character> { player, enemy });
            turnManager.StartMatch();
            Assert.AreEqual(player.Hand.MaxCapacity, player.Hand.Cards.Count);

            turnManager.Pass();
            yield return TestHelper.WaitUntilOrTimeout(() => !turnManager.Busy);
            Assert.IsFalse(turnManager.Busy, "Turn transition did not finish in time");

            Assert.AreEqual(0, player.Hand.Cards.Count);
            Assert.AreEqual(player.Hand.MaxCapacity, player.Deck.DiscardPile.Count);
        }

        private static void SetMaxHandSize(Character character, int value)
        {
            typeof(Character).GetField("maxHandSize",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(character, value);
            character.Hand.MaxCapacity = value;
        }

        private void GiveDeckCards(Character character, int count)
        {
            var cards = new List<CardData>();
            for (int i = 0; i < count; i++)
            {
                var data = TestHelper.MakeCard($"Test{i}");
                trackedSops.Add(data);
                cards.Add(data);
            }
            character.Deck.SetStartingCards(cards);
            character.Deck.Shuffle();
        }
    }
}
