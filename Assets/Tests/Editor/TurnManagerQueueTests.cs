using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Cards;
using Characters;
using HexGrid;
using Game;

namespace Tests.Game
{
    using Cards = global::Cards;

    /// <summary>
    /// Covers the play queue: a player may submit a play while an earlier card is still
    /// resolving, and it resolves afterwards in order. "Still resolving" is simulated by
    /// flagging the character as attacking (IsAnimating), which is exactly what keeps the
    /// manager Busy between frames — no coroutine needs to advance for these assertions.
    /// </summary>
    [TestFixture]
    public class TurnManagerQueueTests
    {
        private TurnManager turnManager;
        private ActionResolver actionResolver;
        private Character player;
        private Character enemy;
        private readonly List<ScriptableObject> tracked = new List<ScriptableObject>();

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

            foreach (var so in tracked)
            {
                if (so != null) Object.DestroyImmediate(so);
            }
            tracked.Clear();
        }

        private CardData BlockCard(string name, int amount, int cost = 0)
        {
            var effect = ScriptableObject.CreateInstance<Cards.Effects.GainBlockEffect>();
            effect.Amount = CardValue.MakeConstant(amount);
            tracked.Add(effect);

            var data = TestHelper.MakeCard(name, cost, TestHelper.Step(effect, TargetType.Self));
            tracked.Add(data);
            return data;
        }

        private void StartMatch()
        {
            turnManager.SetTurnOrder(new List<Character> { player, enemy });
            turnManager.StartMatch();
        }

        /// <summary>Flips the active character's cosmetic "still animating" flag.</summary>
        private void SetAnimating(bool value)
        {
            typeof(Character).GetField("isAttacking", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(player, value);
        }

        [Test]
        public void PlayCard_WhileResolving_QueuesInsteadOfResolving()
        {
            StartMatch();
            var card = new CardInstance(BlockCard("Guard", 3));
            player.Hand.AddCard(card);

            SetAnimating(true);
            bool accepted = turnManager.PlayCard(card);

            Assert.IsTrue(accepted, "A legal play submitted while busy should be accepted and queued.");
            Assert.AreEqual(0, player.Stats.Block, "The queued card must not resolve until the board is idle.");
            Assert.IsTrue(turnManager.HasQueuedPlays);
            Assert.IsTrue(turnManager.Busy);
        }

        [Test]
        public void PlayCard_WhileResolving_RemovesCardFromHandImmediately()
        {
            StartMatch();
            var card = new CardInstance(BlockCard("Guard", 3));
            player.Hand.AddCard(card);

            SetAnimating(true);
            turnManager.PlayCard(card);

            Assert.IsFalse(player.Hand.Contains(card),
                "A queued card leaves the hand the moment it is submitted.");
            Assert.AreEqual(0, player.Stats.Block,
                "Its effect still waits for the earlier card to finish.");
        }

        [Test]
        public void PlayCard_WhileResolving_RemovesCostCardsImmediately()
        {
            StartMatch();
            var guard = new CardInstance(BlockCard("Guard", 3, cost: 1));
            var fuel = new CardInstance(BlockCard("Fuel", 0));
            player.Hand.AddCard(guard);
            player.Hand.AddCard(fuel);

            SetAnimating(true);
            bool accepted = turnManager.PlayCard(guard, new List<CardInstance> { fuel });

            Assert.IsTrue(accepted);
            Assert.IsFalse(player.Hand.Contains(guard));
            Assert.IsFalse(player.Hand.Contains(fuel));

            SetAnimating(false);
            turnManager.PumpPendingPlays();
            Assert.AreEqual(3, player.Stats.Block);
        }

        [Test]
        public void PumpPendingPlays_ResolvesQueuedCardOnceIdle()
        {
            StartMatch();
            var card = new CardInstance(BlockCard("Guard", 3));
            player.Hand.AddCard(card);

            SetAnimating(true);
            turnManager.PlayCard(card);

            SetAnimating(false);
            turnManager.PumpPendingPlays();

            Assert.AreEqual(3, player.Stats.Block);
            Assert.IsFalse(turnManager.HasQueuedPlays);
            Assert.IsFalse(turnManager.Busy);
        }

        [Test]
        public void QueuedPlays_ResolveInSubmissionOrder()
        {
            StartMatch();
            var first = new CardInstance(BlockCard("First", 2));
            var second = new CardInstance(BlockCard("Second", 5));
            player.Hand.AddCard(first);
            player.Hand.AddCard(second);

            SetAnimating(true);
            turnManager.PlayCard(first);
            turnManager.PlayCard(second);

            SetAnimating(false);

            turnManager.PumpPendingPlays();
            Assert.AreEqual(2, player.Stats.Block, "The first submitted play resolves first.");

            turnManager.PumpPendingPlays();
            Assert.AreEqual(7, player.Stats.Block, "The second submitted play resolves next.");
            Assert.IsFalse(turnManager.HasQueuedPlays);
        }

        [Test]
        public void PlayCard_WhileResolving_RejectsIllegalPlayWithoutQueueing()
        {
            StartMatch();
            var card = new CardInstance(BlockCard("Guard", 3));
            // Deliberately not added to the hand.

            SetAnimating(true);
            bool accepted = turnManager.PlayCard(card);

            Assert.IsFalse(accepted);
            Assert.IsFalse(turnManager.HasQueuedPlays);
        }

        [Test]
        public void Pass_IsRejectedWhilePlaysAreQueued()
        {
            StartMatch();
            var card = new CardInstance(BlockCard("Guard", 3));
            player.Hand.AddCard(card);

            SetAnimating(true);
            turnManager.PlayCard(card);

            bool passedFired = false;
            turnManager.OnPlayerPassed += _ => passedFired = true;
            turnManager.Pass();

            Assert.IsFalse(passedFired, "Passing must wait for queued plays to resolve.");
        }

        [Test]
        public void PlayCard_WhenIdle_ResolvesImmediately()
        {
            StartMatch();
            var card = new CardInstance(BlockCard("Guard", 4));
            player.Hand.AddCard(card);

            bool accepted = turnManager.PlayCard(card);

            Assert.IsTrue(accepted);
            Assert.AreEqual(4, player.Stats.Block);
            Assert.IsFalse(turnManager.HasQueuedPlays);
        }
    }
}
