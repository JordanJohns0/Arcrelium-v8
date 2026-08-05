using NUnit.Framework;
using UnityEngine;
using Cards;
using Cards.Effects;
using Characters;
using HexGrid;

namespace Tests.Cards
{
    [TestFixture]
    public class AreaDamageEffectTests
    {
        private AreaDamageEffect effect;
        private HexGridManager grid;
        private Character source;

        [SetUp]
        public void SetUp()
        {
            effect = ScriptableObject.CreateInstance<AreaDamageEffect>();
            effect.Damage = CardValue.MakeConstant(2);
            effect.Radius = 2;
            effect.Faction = AreaFaction.Enemies;

            grid = TestHelper.CreateTestGrid(5);
            source = TestHelper.CreateCharacterOnGrid(grid, 0, 0, 0);
        }

        [TearDown]
        public void TearDown()
        {
            if (source != null) Object.DestroyImmediate(source.gameObject);
            if (grid != null) Object.DestroyImmediate(grid.gameObject);
        }

        private Character PlaceEnemy(int q, int r)
        {
            return TestHelper.CreateCharacterOnGrid(grid, 1, q, r);
        }

        private Character PlaceAlly(int q, int r)
        {
            return TestHelper.CreateCharacterOnGrid(grid, 0, q, r);
        }

        private CardContext Context(HexCell cell, int range)
        {
            return new CardContext { Source = source, Grid = grid, TargetCell = cell, Range = range };
        }

        [Test]
        public void CanExecute_NullCell_ReturnsFalse()
        {
            Assert.IsFalse(effect.CanExecute(Context(null, 5)));
        }

        [Test]
        public void CanExecute_CellOutOfRange_ReturnsFalse()
        {
            HexCell cell = grid.GetCell(new HexCoordinates(2, 0)); // distance 2 from source
            Assert.IsFalse(effect.CanExecute(Context(cell, 1)));
        }

        [Test]
        public void CanExecute_NoMatchingTargets_ReturnsFalse()
        {
            HexCell cell = grid.GetCell(new HexCoordinates(2, 0));
            Assert.IsFalse(effect.CanExecute(Context(cell, 5)));
        }

        [Test]
        public void CanExecute_EnemyInBlast_ReturnsTrue()
        {
            Character enemy = PlaceEnemy(1, -1); // distance 2 from cell (2,0)
            HexCell cell = grid.GetCell(new HexCoordinates(2, 0));
            Assert.IsTrue(effect.CanExecute(Context(cell, 5)));
            Object.DestroyImmediate(enemy.gameObject);
        }

        [Test]
        public void Execute_DealsDamageToAllEnemiesInRadius()
        {
            Character enemyA = PlaceEnemy(1, -1); // distance 2
            Character enemyB = PlaceEnemy(3, -1); // distance 1
            Character ally = PlaceAlly(2, -2);    // distance 2, but not an enemy

            HexCell cell = grid.GetCell(new HexCoordinates(2, 0));
            effect.Execute(Context(cell, 5));

            Assert.AreEqual(8, enemyA.Stats.CurrentHealth);
            Assert.AreEqual(8, enemyB.Stats.CurrentHealth);
            Assert.AreEqual(10, ally.Stats.CurrentHealth);
            Assert.AreEqual(10, source.Stats.CurrentHealth);
        }

        [Test]
        public void Execute_FactionAllies_HitsAlliesNotEnemies()
        {
            effect.Faction = AreaFaction.Allies;
            Character enemy = PlaceEnemy(1, -1);
            Character ally = PlaceAlly(2, -2);

            HexCell cell = grid.GetCell(new HexCoordinates(2, 0));
            effect.Execute(Context(cell, 5));

            Assert.AreEqual(8, ally.Stats.CurrentHealth);
            Assert.AreEqual(10, enemy.Stats.CurrentHealth);
            Assert.AreEqual(10, source.Stats.CurrentHealth); // source in blast but IncludeSelf=false
        }

        [Test]
        public void Execute_IncludeSelf_HitsSourceInBlast()
        {
            effect.Faction = AreaFaction.All;
            effect.IncludeSelf = true;

            HexCell cell = grid.GetCell(new HexCoordinates(2, 0)); // source is distance 2 away
            effect.Execute(Context(cell, 5));

            Assert.AreEqual(8, source.Stats.CurrentHealth);
        }

        [Test]
        public void Execute_RespectsRadius_IgnoresFarTargets()
        {
            Character enemyA = PlaceEnemy(1, -1);  // distance 2 -> hit
            Character far = PlaceEnemy(4, -4);     // distance 4 -> outside radius 2

            HexCell cell = grid.GetCell(new HexCoordinates(2, 0));
            effect.Execute(Context(cell, 5));

            Assert.AreEqual(8, enemyA.Stats.CurrentHealth);
            Assert.AreEqual(10, far.Stats.CurrentHealth);
        }

        [Test]
        public void Execute_Falloff_ReducesDamageByDistance()
        {
            effect.Damage = CardValue.MakeConstant(5);
            effect.Radius = 3;
            effect.DamagePerHexFalloff = 1;

            Character center = PlaceEnemy(2, 0); // distance 0 -> 5
            Character mid = PlaceEnemy(4, -1);   // distance 2 -> 3

            HexCell cell = grid.GetCell(new HexCoordinates(2, 0));
            effect.Execute(Context(cell, 5));

            Assert.AreEqual(5, center.Stats.CurrentHealth);
            Assert.AreEqual(7, mid.Stats.CurrentHealth);
        }

        [Test]
        public void Execute_Falloff_MinimumDamage_ActsAsFloor()
        {
            effect.Damage = CardValue.MakeConstant(5);
            effect.Radius = 3;
            effect.DamagePerHexFalloff = 2;
            effect.MinimumDamage = 1;

            Character center = PlaceEnemy(2, 0); // distance 0 -> 5
            Character edge = PlaceEnemy(5, 0);   // distance 3 -> 5 - 6 = -1 -> floor 1

            HexCell cell = grid.GetCell(new HexCoordinates(2, 0));
            effect.Execute(Context(cell, 5));

            Assert.AreEqual(5, center.Stats.CurrentHealth);
            Assert.AreEqual(9, edge.Stats.CurrentHealth);
        }

        [Test]
        public void Execute_Falloff_EdgeTakesZeroDamageWhenNoFloor()
        {
            effect.Damage = CardValue.MakeConstant(2);
            effect.Radius = 2;
            effect.DamagePerHexFalloff = 1;

            Character center = PlaceEnemy(2, 0); // distance 0 -> 2
            Character edge = PlaceEnemy(4, 0);   // distance 2 -> 0

            HexCell cell = grid.GetCell(new HexCoordinates(2, 0));
            effect.Execute(Context(cell, 5));

            Assert.AreEqual(8, center.Stats.CurrentHealth);
            Assert.AreEqual(10, edge.Stats.CurrentHealth);
        }

        [Test]
        public void CanExecute_FalloffZeroDamageTarget_IsNotAVictim()
        {
            effect.Damage = CardValue.MakeConstant(2);
            effect.Radius = 2;
            effect.DamagePerHexFalloff = 2;

            Character edge = PlaceEnemy(4, 0); // distance 2 -> 0 damage

            HexCell cell = grid.GetCell(new HexCoordinates(2, 0));
            Assert.IsFalse(effect.CanExecute(Context(cell, 5)));
            Object.DestroyImmediate(edge.gameObject);
        }

        [Test]
        public void Execute_LethalDamage_KillsEnemy()
        {
            effect.Damage = CardValue.MakeConstant(5);
            Character enemy = PlaceEnemy(1, -1);
            enemy.Stats.MaxHealth = 3;
            enemy.Stats.CurrentHealth = 3;

            HexCell cell = grid.GetCell(new HexCoordinates(2, 0));
            effect.Execute(Context(cell, 5));

            Assert.IsFalse(enemy.Stats.IsAlive);
        }
    }
}
