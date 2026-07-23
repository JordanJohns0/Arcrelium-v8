using NUnit.Framework;
using UnityEngine;
using Characters;
using Equipment;
using Equipment.Effects;
using HexGrid;

namespace Tests
{
    [TestFixture]
    public class MoveStepDamageEffectTests
    {
        private MoveStepDamageEffect effect;
        private HexGridManager grid;
        private Character owner;
        private Character enemy;

        [SetUp]
        public void SetUp()
        {
            effect = ScriptableObject.CreateInstance<MoveStepDamageEffect>();
            effect.Damage = 2;

            grid = TestHelper.CreateTestGrid(3);
            owner = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
            enemy = TestHelper.CreateCharacterOnGrid(grid, 1, 1, 0);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(effect);
            Object.DestroyImmediate(owner.CurrentCell.gameObject);
            Object.DestroyImmediate(owner.gameObject);
            Object.DestroyImmediate(enemy.CurrentCell.gameObject);
            Object.DestroyImmediate(enemy.gameObject);
            Object.DestroyImmediate(grid.gameObject);
        }

        [Test]
        public void Execute_AdacentEnemy_DealsDamage()
        {
            var cell = grid.GetCell(new HexCoordinates(1, -1));
            var context = new EquipmentContext
            {
                Source = owner,
                Grid = grid,
                TargetCell = cell
            };

            effect.Execute(context);

            Assert.AreEqual(8, enemy.Stats.CurrentHealth);
        }

        [Test]
        public void Execute_NonAdjacentEnemy_NoDamage()
        {
            var farEnemy = TestHelper.CreateCharacterOnGrid(grid, 1, 3, 0);
            var cell = grid.GetCell(new HexCoordinates(1, -1));
            var context = new EquipmentContext
            {
                Source = owner,
                Grid = grid,
                TargetCell = cell
            };

            effect.Execute(context);

            Assert.AreEqual(10, farEnemy.Stats.CurrentHealth);
            Object.DestroyImmediate(farEnemy.CurrentCell.gameObject);
            Object.DestroyImmediate(farEnemy.gameObject);
        }

        [Test]
        public void Execute_FriendlyAdjacent_NoDamage()
        {
            var friendly = TestHelper.CreateCharacterOnGrid(grid, 0, 1, -1);
            var cell = grid.GetCell(new HexCoordinates(1, -1));
            var context = new EquipmentContext
            {
                Source = owner,
                Grid = grid,
                TargetCell = cell
            };

            effect.Execute(context);

            Assert.AreEqual(10, friendly.Stats.CurrentHealth);
            Object.DestroyImmediate(friendly.CurrentCell.gameObject);
            Object.DestroyImmediate(friendly.gameObject);
        }

        [Test]
        public void Execute_Self_NotDamaged()
        {
            var cell = grid.GetCell(new HexCoordinates(1, 0));
            var context = new EquipmentContext
            {
                Source = owner,
                Grid = grid,
                TargetCell = cell
            };

            effect.Execute(context);

            Assert.AreEqual(10, owner.Stats.CurrentHealth);
        }

        [Test]
        public void Execute_MultipleAdjacentEnemies_AllDamaged()
        {
            var enemy2 = TestHelper.CreateCharacterOnGrid(grid, 1, 0, 1);
            var cell = grid.GetCell(new HexCoordinates(0, 0));
            var context = new EquipmentContext
            {
                Source = owner,
                Grid = grid,
                TargetCell = cell
            };

            effect.Execute(context);

            Assert.AreEqual(8, enemy.Stats.CurrentHealth);
            Assert.AreEqual(8, enemy2.Stats.CurrentHealth);
            Object.DestroyImmediate(enemy2.CurrentCell.gameObject);
            Object.DestroyImmediate(enemy2.gameObject);
        }

        [Test]
        public void Execute_NullSource_NoThrow()
        {
            var context = new EquipmentContext { Source = null, Grid = grid };
            Assert.DoesNotThrow(() => effect.Execute(context));
        }

        [Test]
        public void Execute_NullGrid_NoThrow()
        {
            var context = new EquipmentContext { Source = owner, Grid = null, TargetCell = grid.GetCell(new HexCoordinates(0, 0)) };
            Assert.DoesNotThrow(() => effect.Execute(context));
        }
    }
}
