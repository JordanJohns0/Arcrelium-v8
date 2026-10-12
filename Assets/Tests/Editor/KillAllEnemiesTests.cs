using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Characters;
using Game;

namespace Tests.Game
{
    [TestFixture]
    public class KillAllEnemiesTests
    {
        private GameManager manager;
        private TurnManager turnManager;
        private ActionResolver actionResolver;
        private readonly List<Character> characters = new List<Character>();

        [SetUp]
        public void SetUp()
        {
            manager = new GameObject("GameManager").AddComponent<GameManager>();
            turnManager = new GameObject("TurnManager").AddComponent<TurnManager>();
            actionResolver = new GameObject("ActionResolver").AddComponent<ActionResolver>();

            typeof(TurnManager).GetField("actionResolver", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(turnManager, actionResolver);
            typeof(GameManager).GetField("turnManager", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(manager, turnManager);
        }

        [TearDown]
        public void TearDown()
        {
            GameState.ActiveMode = GameMode.SinglePlayer;
            foreach (var c in characters)
            {
                if (c == null) continue;
                if (c.CurrentCell != null) Object.DestroyImmediate(c.CurrentCell.gameObject);
                Object.DestroyImmediate(c.gameObject);
            }
            characters.Clear();
            Object.DestroyImmediate(turnManager.gameObject);
            Object.DestroyImmediate(actionResolver.gameObject);
            Object.DestroyImmediate(manager.gameObject);
        }

        private Character MakeCharacter(int ownerId, int teamId, int q, int r)
        {
            var c = TestHelper.CreateCharacter(ownerId, q, r, teamId);
            characters.Add(c);
            manager.activeCharacters.Add(c);
            return c;
        }

        [Test]
        public void KillAllEnemies_DefeatsEveryLivingEnemy()
        {
            var player = MakeCharacter(0, 0, 0, -2);
            var enemyA = MakeCharacter(GameState.EnemyOwnerPlayerId, 1, 2, 0);
            var enemyB = MakeCharacter(GameState.EnemyOwnerPlayerId + 1, 1, -2, 0);

            manager.KillAllEnemies();

            Assert.IsFalse(enemyA.Stats.IsAlive, "Every AI enemy must be defeated.");
            Assert.IsFalse(enemyB.Stats.IsAlive, "Every AI enemy must be defeated.");
            Assert.IsTrue(player.Stats.IsAlive, "Allies must be left alive.");
        }

        [Test]
        public void KillAllEnemies_DefeatsEnemyThroughNormalDeathFlow()
        {
            var enemy = MakeCharacter(GameState.EnemyOwnerPlayerId, 1, 2, 0);
            int defeatedCount = 0;
            enemy.OnDefeated += _ => defeatedCount++;

            manager.KillAllEnemies();

            Assert.AreEqual(1, defeatedCount, "The enemy must be defeated via Character.Kill (OnDefeated fires).");
        }

        [Test]
        public void KillAllEnemies_SkipsBoardStructures()
        {
            var enemy = MakeCharacter(GameState.EnemyOwnerPlayerId, 1, 2, 0);
            var rock = MakeCharacter(GameState.EnemyOwnerPlayerId, -1, -2, 0);
            rock.MarkAsStructure();

            manager.KillAllEnemies();

            Assert.IsFalse(enemy.Stats.IsAlive, "A real enemy is defeated.");
            Assert.IsTrue(rock.Stats.IsAlive, "Neutral structures are not enemies.");
        }

        [Test]
        public void KillAllEnemies_AlreadyDeadEnemy_IsNotKilledAgain()
        {
            var enemy = MakeCharacter(GameState.EnemyOwnerPlayerId, 1, 2, 0);
            enemy.Stats.CurrentHealth = 0;
            int defeatedCount = 0;
            enemy.OnDefeated += _ => defeatedCount++;

            manager.KillAllEnemies();

            Assert.AreEqual(0, defeatedCount, "An already-dead enemy must not be killed again.");
        }

        [Test]
        public void KillAllEnemies_OnClient_DoesNothing()
        {
            GameState.ActiveMode = GameMode.Client;
            var enemy = MakeCharacter(GameState.EnemyOwnerPlayerId, 1, 2, 0);

            manager.KillAllEnemies();

            Assert.IsTrue(enemy.Stats.IsAlive, "Clients aren't authoritative, so the cheat is a no-op.");
        }
    }
}
