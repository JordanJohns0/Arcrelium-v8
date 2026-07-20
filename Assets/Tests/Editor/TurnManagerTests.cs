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
    public class TurnManagerTests
    {
        private TurnManager turnManager;
        private ActionResolver actionResolver;
        private Character player;
        private Character enemy;
        private CardData selfCardData;

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

            var selfEffect = ScriptableObject.CreateInstance<Cards.Effects.GainBlockEffect>();
            selfEffect.Amount = 1;
            selfCardData = ScriptableObject.CreateInstance<CardData>();
            selfCardData.CardName = "Defend";
            selfCardData.TargetType = TargetType.Self;
            selfCardData.Cost = 0;
            selfCardData.Effect = selfEffect;
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
        public void StartMatch_SetsActiveCharacter()
        {
            turnManager.SetTurnOrder(new List<Character> { player, enemy });
            turnManager.StartMatch();
            Assert.AreEqual(player, turnManager.ActiveCharacter);
        }

        [Test]
        public void StartMatch_PhaseIsAct()
        {
            turnManager.SetTurnOrder(new List<Character> { player, enemy });
            turnManager.StartMatch();
            Assert.AreEqual(TurnPhase.Act, turnManager.CurrentPhase);
        }

        [Test]
        public void StartMatch_FiresOnTurnStarted()
        {
            bool fired = false;
            turnManager.OnTurnStarted += (c) => fired = true;
            turnManager.SetTurnOrder(new List<Character> { player, enemy });
            turnManager.StartMatch();
            Assert.IsTrue(fired);
        }

        [Test]
        public void PlayCard_InActPhase_Succeeds()
        {
            var card = new CardInstance(selfCardData);
            player.Hand.AddCard(card);

            turnManager.SetTurnOrder(new List<Character> { player, enemy });
            turnManager.StartMatch();

            bool played = turnManager.PlayCard(card);
            Assert.IsTrue(played);
        }

        [Test]
        public void Pass_FiresOnPlayerPassed()
        {
            bool fired = false;
            turnManager.OnPlayerPassed += (c) => fired = true;
            turnManager.SetTurnOrder(new List<Character> { player, enemy });
            turnManager.StartMatch();

            turnManager.Pass();
            Assert.IsTrue(fired);
        }
    }
}
