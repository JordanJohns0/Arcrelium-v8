using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
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
    }
}
