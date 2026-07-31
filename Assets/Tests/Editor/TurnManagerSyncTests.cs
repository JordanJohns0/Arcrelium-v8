using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Cards;
using Characters;
using HexGrid;
using Game;

namespace Tests
{
    [TestFixture]
    public class TurnManagerSyncTests
    {
        private TurnManager turnManager;
        private ActionResolver actionResolver;
        private Character player;
        private Character enemy;

        [SetUp]
        public void SetUp()
        {
            var tmGo = new GameObject("TurnManager");
            turnManager = tmGo.AddComponent<TurnManager>();

            var arGo = new GameObject("ActionResolver");
            actionResolver = arGo.AddComponent<ActionResolver>();

            typeof(TurnManager).GetField("actionResolver", BindingFlags.NonPublic | BindingFlags.Instance)
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
        }

        [Test]
        public void SetTurn_FiresOnTurnStartedWithActiveCharacter()
        {
            Character turned = null;
            turnManager.OnTurnStarted += (c) => turned = c;

            turnManager.SetTurnOrder(new List<Character> { player, enemy });
            turnManager.SetTurn(0);

            Assert.AreEqual(player, turned);
            Assert.AreEqual(player, turnManager.ActiveCharacter);
            Assert.AreEqual(0, turnManager.CurrentTurnIndex);
        }

        [Test]
        public void SetTurn_WrapsPositiveIndex()
        {
            turnManager.SetTurnOrder(new List<Character> { player, enemy });
            turnManager.SetTurn(3);

            Assert.AreEqual(enemy, turnManager.ActiveCharacter);
            Assert.AreEqual(1, turnManager.CurrentTurnIndex);
        }

        [Test]
        public void SetTurn_WrapsNegativeIndex()
        {
            turnManager.SetTurnOrder(new List<Character> { player, enemy });
            turnManager.SetTurn(-1);

            Assert.AreEqual(enemy, turnManager.ActiveCharacter);
            Assert.AreEqual(1, turnManager.CurrentTurnIndex);
        }

        [Test]
        public void SetTurn_EmptyOrder_DoesNotThrowAndResetsIndex()
        {
            bool fired = false;
            turnManager.OnTurnStarted += (c) => fired = true;

            turnManager.SetTurnOrder(new List<Character>());
            turnManager.SetTurn(2);

            Assert.AreEqual(0, turnManager.CurrentTurnIndex);
            Assert.IsNull(turnManager.ActiveCharacter);
            Assert.IsFalse(fired);
        }

        [Test]
        public void SetTurn_DoesNotDrawCards()
        {
            player.Deck.SetStartingCards(new List<CardData> { TestHelper.MakeCard("Slash") });
            player.Deck.Shuffle();

            turnManager.SetTurnOrder(new List<Character> { player, enemy });
            turnManager.StartMatch();

            int before = player.Hand.Cards.Count;
            Assert.Greater(before, 0);

            turnManager.SetTurn(0);

            Assert.AreEqual(before, player.Hand.Cards.Count);
        }

        [Test]
        public void SetTurn_DoesNotChangePhase()
        {
            turnManager.SetTurnOrder(new List<Character> { player, enemy });
            turnManager.StartMatch();

            turnManager.SetTurn(1);

            Assert.AreEqual(TurnPhase.Act, turnManager.CurrentPhase);
        }
    }
}
