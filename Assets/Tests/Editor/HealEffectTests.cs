using NUnit.Framework;
using UnityEngine;
using Cards;
using Cards.Effects;
using Characters;
using HexGrid;

namespace Tests.Cards
{
    [TestFixture]
    public class HealEffectTests
    {
        private HealEffect effect;
        private Character source;

        [SetUp]
        public void SetUp()
        {
            effect = ScriptableObject.CreateInstance<HealEffect>();
            effect.Amount = CardValue.MakeConstant(4);

            source = TestHelper.CreateCharacter(0, 0, 0);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(source.CurrentCell.gameObject);
            Object.DestroyImmediate(source.gameObject);
        }

        [Test]
        public void CanExecute_HealthySource_ReturnsFalse()
        {
            var ctx = new CardContext { Source = source };
            Assert.IsFalse(effect.CanExecute(ctx));
        }

        [Test]
        public void CanExecute_DamagedSource_ReturnsTrue()
        {
            source.Stats.ApplyDamage(5);
            var ctx = new CardContext { Source = source };
            Assert.IsTrue(effect.CanExecute(ctx));
        }

        [Test]
        public void CanExecute_NullSource_ReturnsFalse()
        {
            var ctx = new CardContext { Source = null };
            Assert.IsFalse(effect.CanExecute(ctx));
        }

        [Test]
        public void Execute_HealsSource()
        {
            source.Stats.ApplyDamage(8);
            int healthBefore = source.Stats.CurrentHealth;

            var ctx = new CardContext { Source = source };
            effect.Execute(ctx);

            Assert.AreEqual(healthBefore + 4, source.Stats.CurrentHealth);
        }

        [Test]
        public void Execute_DoesNotExceedMaxHealth()
        {
            source.Stats.ApplyDamage(1);
            var ctx = new CardContext { Source = source };
            effect.Execute(ctx);
            Assert.AreEqual(source.Stats.MaxHealth, source.Stats.CurrentHealth);
        }
    }
}
