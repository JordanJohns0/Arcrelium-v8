using NUnit.Framework;
using UnityEngine;
using Characters;

namespace Tests
{
    [TestFixture]
    public class CharacterStatsTests
    {
        private CharacterStats stats;

        [SetUp]
        public void SetUp()
        {
            stats = new CharacterStats();
            stats.MaxHealth = 10;
            stats.Initialize();
        }

        [Test]
        public void Initialize_SetsCurrentHealthToMax()
        {
            stats.CurrentHealth = 3;
            stats.Block = 5;

            stats.Initialize();

            Assert.AreEqual(10, stats.CurrentHealth);
            Assert.AreEqual(0, stats.Block);
        }

        [Test]
        public void IsAlive_TrueWhenHealthPositive()
        {
            Assert.IsTrue(stats.IsAlive);
        }

        [Test]
        public void IsAlive_FalseWhenHealthZero()
        {
            stats.CurrentHealth = 0;
            Assert.IsFalse(stats.IsAlive);
        }

        [Test]
        public void ApplyDamage_ReducesHealth()
        {
            stats.ApplyDamage(3);
            Assert.AreEqual(7, stats.CurrentHealth);
        }

        [Test]
        public void ApplyDamage_BlockAbsorbsFirst()
        {
            stats.GainBlock(5);

            stats.ApplyDamage(3);

            Assert.AreEqual(2, stats.Block);
            Assert.AreEqual(10, stats.CurrentHealth);
        }

        [Test]
        public void ApplyDamage_BlockPartialAbsorb()
        {
            stats.GainBlock(2);

            stats.ApplyDamage(5);

            Assert.AreEqual(0, stats.Block);
            Assert.AreEqual(7, stats.CurrentHealth);
        }

        [Test]
        public void ApplyDamage_ArmorMitigatesAfterBlock()
        {
            stats.Armor = 2;
            stats.GainBlock(1);

            stats.ApplyDamage(5);

            Assert.AreEqual(0, stats.Block);
            Assert.AreEqual(8, stats.CurrentHealth);
        }

        [Test]
        public void ApplyDamage_ArmorAlone()
        {
            stats.Armor = 3;

            stats.ApplyDamage(5);

            Assert.AreEqual(8, stats.CurrentHealth);
        }

        [Test]
        public void ApplyDamage_ArmorCannotKill()
        {
            stats.Armor = 10;

            stats.ApplyDamage(5);

            Assert.AreEqual(10, stats.CurrentHealth);
        }

        [Test]
        public void ApplyDamage_LethalDamage()
        {
            stats.ApplyDamage(20);
            Assert.AreEqual(0, stats.CurrentHealth);
            Assert.IsFalse(stats.IsAlive);
        }

        [Test]
        public void ApplyDamage_CannotGoBelowZero()
        {
            stats.ApplyDamage(100);
            Assert.AreEqual(0, stats.CurrentHealth);
        }

        [Test]
        public void Heal_RestoresHealth()
        {
            stats.ApplyDamage(5);
            stats.Heal(3);
            Assert.AreEqual(8, stats.CurrentHealth);
        }

        [Test]
        public void Heal_CannotExceedMaxHealth()
        {
            stats.ApplyDamage(2);
            stats.Heal(100);
            Assert.AreEqual(10, stats.CurrentHealth);
        }

        [Test]
        public void GainBlock_IncreasesBlock()
        {
            stats.GainBlock(5);
            Assert.AreEqual(5, stats.Block);
        }

        [Test]
        public void GainBlock_Additive()
        {
            stats.GainBlock(3);
            stats.GainBlock(2);
            Assert.AreEqual(5, stats.Block);
        }

        [Test]
        public void FullDamagePipeline_BlockArmorHealth()
        {
            stats.Armor = 1;
            stats.GainBlock(2);

            stats.ApplyDamage(6);

            Assert.AreEqual(0, stats.Block);
            Assert.AreEqual(7, stats.CurrentHealth);
        }

        [Test]
        public void ZeroDamage_NoChange()
        {
            stats.ApplyDamage(0);
            Assert.AreEqual(10, stats.CurrentHealth);
        }
    }
}
