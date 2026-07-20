using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Cards;
using Characters;
using HexGrid;
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

        private static HexCell CreateCell(int q, int r)
        {
            var go = new GameObject($"Cell {q},{r}");
            var cell = go.AddComponent<HexCell>();
            cell.Initialize(new HexCoordinates(q, r));
            return cell;
        }

        private static Character CreateCharacter(int ownerId, int q, int r)
        {
            var cell = CreateCell(q, r);
            var go = new GameObject($"Character {ownerId}");
            var character = go.AddComponent<Character>();
            character.Initialize(ownerId, cell, 0);
            return character;
        }

        [SetUp]
        public void SetUp()
        {
            var tmGo = new GameObject("TurnManager");
            turnManager = tmGo.AddComponent<TurnManager>();

            var arGo = new GameObject("ActionResolver");
            actionResolver = arGo.AddComponent<ActionResolver>();

            typeof(TurnManager).GetField("actionResolver", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(turnManager, actionResolver);

            player = CreateCharacter(0, 0, 0);
            enemy = CreateCharacter(1, 1, 0);
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
            yield return new WaitForSeconds(2f);

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
            yield return new WaitForSeconds(2f);

            Assert.GreaterOrEqual(turnCount, 1);
            Assert.AreEqual(player, turnManager.ActiveCharacter);
        }

        [UnityTest]
        public IEnumerator TurnCycle_AdvancesToNextCharacter()
        {
            turnManager.SetTurnOrder(new List<Character> { player, enemy });
            turnManager.StartMatch();

            var turnStarted = new List<Character>();
            turnManager.OnTurnStarted += (c) => turnStarted.Add(c);

            turnManager.Pass();
            yield return new WaitForSeconds(2f);

            Assert.AreEqual(2, turnStarted.Count);
            Assert.AreEqual(player, turnStarted[0]);
            Assert.AreEqual(enemy, turnStarted[1]);
        }
    }
}
