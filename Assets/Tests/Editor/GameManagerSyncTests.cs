using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Cards;
using Characters;
using Game;
using Networking;

namespace Tests
{
    [TestFixture]
    public class GameManagerSyncTests
    {
        private GameManager manager;
        private TurnManager turnManager;
        private ActionResolver actionResolver;
        private readonly List<Character> characters = new List<Character>();

        [SetUp]
        public void SetUp()
        {
            var gmGo = new GameObject("GameManager");
            manager = gmGo.AddComponent<GameManager>();

            var tmGo = new GameObject("TurnManager");
            turnManager = tmGo.AddComponent<TurnManager>();

            var arGo = new GameObject("ActionResolver");
            actionResolver = arGo.AddComponent<ActionResolver>();

            typeof(TurnManager).GetField("actionResolver", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(turnManager, actionResolver);

            typeof(GameManager).GetField("turnManager", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(manager, turnManager);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var c in characters)
            {
                if (c == null) continue;
                Object.DestroyImmediate(c.CurrentCell.gameObject);
                Object.DestroyImmediate(c.gameObject);
            }
            characters.Clear();
            Object.DestroyImmediate(turnManager.gameObject);
            Object.DestroyImmediate(actionResolver.gameObject);
            Object.DestroyImmediate(manager.gameObject);
        }

        private Character MakeCharacter(int ownerId, int teamId, string name, int q, int r)
        {
            var c = TestHelper.CreateCharacter(ownerId, q, r, teamId);
            c.SetName(name);
            c.SetPlayerColor(new Color(0.1f, 0.2f, 0.3f));
            characters.Add(c);
            manager.activeCharacters.Add(c);
            return c;
        }

        [Test]
        public void BuildGameStateSync_IncludesAllCharactersAndTurnIndex()
        {
            var player = MakeCharacter(0, 0, "Alice", 0, -2);
            MakeCharacter(1, 0, "Bob", 1, -2);
            MakeCharacter(100, 1, "Enemy", 2, 0);

            player.Hand.AddCard(new CardInstance(TestHelper.MakeCard("Slash")));
            player.Hand.AddCard(new CardInstance(TestHelper.MakeCard("Defend")));

            turnManager.SetTurnOrder(new List<Character>(manager.activeCharacters));
            turnManager.SetTurn(1);

            var sync = manager.BuildGameStateSync();

            Assert.AreEqual(1, sync.currentTurnIndex);
            Assert.AreEqual(3, sync.characterStates.Count);

            var p = sync.characterStates[0];
            Assert.AreEqual(0, p.ownerPlayerId);
            Assert.AreEqual(0, p.teamId);
            Assert.AreEqual(0, p.q);
            Assert.AreEqual(-2, p.r);
            Assert.AreEqual(player.Stats.CurrentHealth, p.currentHp);
            Assert.AreEqual(player.Stats.MaxHealth, p.maxHp);
            Assert.AreEqual(player.Stats.Armor, p.armor);
            Assert.AreEqual("Alice", p.name);
            Assert.AreEqual(0.1f, p.colorR, 0.0001f);
            Assert.AreEqual(0.2f, p.colorG, 0.0001f);
            Assert.AreEqual(0.3f, p.colorB, 0.0001f);
            Assert.AreEqual(2, p.handCardNames.Length);
            Assert.AreEqual("Slash", p.handCardNames[0]);
            Assert.AreEqual("Defend", p.handCardNames[1]);

            var ally = sync.characterStates[1];
            Assert.AreEqual(1, ally.ownerPlayerId);
            Assert.AreEqual(0, ally.teamId);

            var enemy = sync.characterStates[2];
            Assert.AreEqual(100, enemy.ownerPlayerId);
            Assert.AreEqual(1, enemy.teamId);
        }

        [Test]
        public void BuildGameStateSync_NoPlayerColor_DefaultsToWhite()
        {
            var c = TestHelper.CreateCharacter(0, 0, 0);
            characters.Add(c);
            manager.activeCharacters.Add(c);

            var sync = manager.BuildGameStateSync();

            Assert.AreEqual(1f, sync.characterStates[0].colorR);
            Assert.AreEqual(1f, sync.characterStates[0].colorG);
            Assert.AreEqual(1f, sync.characterStates[0].colorB);
        }

        [Test]
        public void BuildGameStateSync_SkipsNullCharacters()
        {
            var c = TestHelper.CreateCharacter(0, 0, 0);
            characters.Add(c);
            manager.activeCharacters.Add(null);
            manager.activeCharacters.Add(c);

            var sync = manager.BuildGameStateSync();

            Assert.AreEqual(1, sync.characterStates.Count);
        }
    }
}
