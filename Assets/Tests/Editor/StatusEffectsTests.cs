using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Characters;
using Cards;
using Cards.Effects;
using Game;
using HexGrid;

namespace Tests.Statuses
{
    /// <summary>
    /// The reusable next-turn status system: statuses lie dormant when applied, activate on
    /// their owner's turn, and expire (one status per turn) once that turn ends.
    /// </summary>
    [TestFixture]
    public class StatusEffectsTests
    {
        [Test]
        public void ReducedDraw_Dormant_ThenActive_ThenExpires()
        {
            Character enemy = TestHelper.CreateCharacter(1, 0, 0);
            try
            {
                enemy.AddStatus(new ReducedDrawStatus { Amount = 1 });

                Assert.AreEqual(5, enemy.GetModifiedDrawCount(5), "A just-applied status must be dormant.");
                Assert.IsTrue(enemy.CanMoveThisTurn, "Mesmerize must not block movement");

                enemy.TickStatusesAtTurnStart();
                Assert.AreEqual(4, enemy.GetModifiedDrawCount(5),
                    "An active reduced-draw status must cut the draw count by its amount.");
                Assert.IsTrue(enemy.CanMoveThisTurn);

                enemy.TickStatusesAtTurnEnd(); // the active turn ends -> one status is spent
                enemy.TickStatusesAtTurnStart();
                Assert.AreEqual(5, enemy.GetModifiedDrawCount(5),
                    "A spent status must not linger into the next turn.");
                Assert.IsEmpty(enemy.Statuses, "The spent status should be removed from the character.");
            }
            finally
            {
                Object.DestroyImmediate(enemy.gameObject);
            }
        }

        [Test]
        public void CannotMove_BlocksMovementOnlyWhileActive()
        {
            Character enemy = TestHelper.CreateCharacter(1, 0, 0);
            try
            {
                enemy.AddStatus(new CannotMoveStatus());

                Assert.IsTrue(enemy.CanMoveThisTurn, "A just-applied Entrap must be dormant.");
                Assert.AreEqual(5, enemy.GetModifiedDrawCount(5), "Entrap must not affect draws.");

                enemy.TickStatusesAtTurnStart();
                Assert.IsFalse(enemy.CanMoveThisTurn, "Pinned characters cannot move on their turn.");

                enemy.TickStatusesAtTurnEnd();
                enemy.TickStatusesAtTurnStart();
                Assert.IsTrue(enemy.CanMoveThisTurn, "Movement must be restored the following turn.");
            }
            finally
            {
                Object.DestroyImmediate(enemy.gameObject);
            }
        }

        [Test]
        public void CannotTarget_BlocksOnlyTheProtectedCharacter()
        {
            Character attacker = TestHelper.CreateCharacter(0, 0, 0);
            Character protectedSource = TestHelper.CreateCharacter(0, 1, 1);
            Character enemy = TestHelper.CreateCharacter(1, 2, 0);
            try
            {
                enemy.AddStatus(new CannotTargetStatus { Protected = protectedSource });

                Assert.IsTrue(enemy.CanTarget(protectedSource), "A just-applied Charm must be dormant.");

                enemy.TickStatusesAtTurnStart();
                Assert.IsFalse(enemy.CanTarget(protectedSource),
                    "Charmed enemies cannot target the character that charmed them.");
                Assert.IsTrue(enemy.CanTarget(attacker),
                    "A Charm on the caster must not block targeting anyone else.");
                Assert.AreEqual(5, enemy.GetModifiedDrawCount(5), "Charm must not affect draws.");
                Assert.IsTrue(enemy.CanMoveThisTurn, "Charm must not block movement.");

                enemy.TickStatusesAtTurnEnd();
                enemy.TickStatusesAtTurnStart();
                Assert.IsTrue(enemy.CanTarget(protectedSource),
                    "Targeting must be restored the following turn.");
            }
            finally
            {
                Object.DestroyImmediate(attacker.gameObject);
                Object.DestroyImmediate(protectedSource.gameObject);
                Object.DestroyImmediate(enemy.gameObject);
            }
        }

        [Test]
        public void ReduceDrawNextTurnAsset_AppliesReducedDrawStatus()
        {
            var effect = LoadAsset<ReduceDrawNextTurnEffect>("Assets/CardEffect/Misc/ReduceDrawNextTurn_1.asset");
            Character source = TestHelper.CreateCharacter(0, 0, 0);
            Character enemy = TestHelper.CreateCharacter(1, 1, 0);
            try
            {
                effect.Execute(new CardContext { Source = source, TargetCharacter = enemy });

                Assert.AreEqual(1, enemy.Statuses.Count, "Mesmerize should add exactly one status.");
                var status = enemy.Statuses[0] as ReducedDrawStatus;
                Assert.IsNotNull(status, "Mesmerize should apply a ReducedDrawStatus.");
                Assert.AreEqual(1, status.Amount, "Mesmerize should reduce the draw by exactly 1.");
            }
            finally
            {
                Object.DestroyImmediate(source.gameObject);
                Object.DestroyImmediate(enemy.gameObject);
            }
        }

        [Test]
        public void BlockMovementNextTurnAsset_AppliesCannotMoveStatus()
        {
            var effect = LoadAsset<BlockMovementNextTurnEffect>("Assets/CardEffect/Misc/BlockMovementNextTurn_1.asset");
            Character source = TestHelper.CreateCharacter(0, 0, 0);
            Character enemy = TestHelper.CreateCharacter(1, 1, 0);
            try
            {
                effect.Execute(new CardContext { Source = source, TargetCharacter = enemy });

                Assert.AreEqual(1, enemy.Statuses.Count, "Entrap should add exactly one status.");
                Assert.IsInstanceOf<CannotMoveStatus>(enemy.Statuses[0], "Entrap should apply a CannotMoveStatus.");
            }
            finally
            {
                Object.DestroyImmediate(source.gameObject);
                Object.DestroyImmediate(enemy.gameObject);
            }
        }

        [Test]
        public void BlockTargetingNextTurnAsset_AppliesCannotTargetStatusForCaster()
        {
            var effect = LoadAsset<BlockTargetingNextTurnEffect>("Assets/CardEffect/Misc/BlockTargetingNextTurn_1.asset");
            Character source = TestHelper.CreateCharacter(0, 0, 0);
            Character enemy = TestHelper.CreateCharacter(1, 1, 0);
            try
            {
                effect.Execute(new CardContext { Source = source, TargetCharacter = enemy });

                Assert.AreEqual(1, enemy.Statuses.Count, "Charm should add exactly one status.");
                var status = enemy.Statuses[0] as CannotTargetStatus;
                Assert.IsNotNull(status, "Charm should apply a CannotTargetStatus.");
                Assert.AreSame(source, status.Protected, "The charm protects the caster.");
            }
            finally
            {
                Object.DestroyImmediate(source.gameObject);
                Object.DestroyImmediate(enemy.gameObject);
            }
        }

        /// <summary>
        /// Full axis through TurnManager: applying Mesmerize to an enemy, ending the source's turn,
        /// then starting the enemy's turn draws them one card fewer via the real turn-start flow.
        /// </summary>
        [Test]
        public void MesmerizedEnemy_DrawsOneFewerAtTurnStart()
        {
            var grid = TestHelper.CreateTestGrid(3);
            Character source = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
            Character enemy = TestHelper.CreateCharacterOnGrid(grid, 1, 0, 0);
            var turnGo = new GameObject("TestTurnManager");
            var turnManager = turnGo.AddComponent<TurnManager>();
            try
            {
                var mesmerize = LoadAsset<ReduceDrawNextTurnEffect>("Assets/CardEffect/Misc/ReduceDrawNextTurn_1.asset");
                mesmerize.Execute(new CardContext { Source = source, TargetCharacter = enemy });

                var deck = new List<CardData>();
                for (int i = 0; i < 6; i++) deck.Add(TestHelper.MakeCard("Enemy " + i));
                enemy.Deck.SetStartingCards(deck);
                enemy.Deck.Shuffle();

                turnManager.SetTurnOrder(new List<Character> { enemy });
                turnManager.StartMatch(); // enemy's turn: status activates, then draws to hand limit

                Assert.AreEqual(4, enemy.Hand.Cards.Count,
                    "A mesmerized enemy should draw one card fewer at the start of its turn.");
            }
            finally
            {
                Object.DestroyImmediate(turnGo);
                Object.DestroyImmediate(source.gameObject);
                Object.DestroyImmediate(enemy.gameObject);
                Object.DestroyImmediate(grid.gameObject);
            }
        }

        private static T LoadAsset<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(asset, $"Asset not found at '{path}'.");
            return asset;
        }
    }
}