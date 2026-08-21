using NUnit.Framework;
using UnityEngine;
using Cards;
using Cards.Effects;
using Characters;

namespace Tests.Cards
{
    [TestFixture]
    public class SelfDamageEffectTests
    {
        private SelfDamageEffect effect;
        private Character source;

        [SetUp]
        public void SetUp()
        {
            effect = ScriptableObject.CreateInstance<SelfDamageEffect>();
            effect.Damage = CardValue.MakeConstant(3);

            source = TestHelper.CreateCharacter(0, 0, 0);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(source.CurrentCell.gameObject);
            Object.DestroyImmediate(source.gameObject);
        }

        [Test]
        public void CanExecute_NullSource_ReturnsFalse()
        {
            var ctx = new CardContext();
            Assert.IsFalse(effect.CanExecute(ctx));
        }

        [Test]
        public void CanExecute_DeadSource_ReturnsFalse()
        {
            source.Stats.CurrentHealth = 0;
            var ctx = new CardContext { Source = source };
            Assert.IsFalse(effect.CanExecute(ctx));
        }

        [Test]
        public void CanExecute_AliveSource_ReturnsTrue()
        {
            var ctx = new CardContext { Source = source };
            Assert.IsTrue(effect.CanExecute(ctx));
        }

        [Test]
        public void Execute_DamagesSource()
        {
            effect.Execute(new CardContext { Source = source });
            Assert.AreEqual(3, source.Stats.MaxHealth - source.Stats.CurrentHealth);
        }

        [Test]
        public void Execute_BlockAbsorbsDamage()
        {
            source.Stats.GainBlock(5);
            int hpBefore = source.Stats.CurrentHealth;

            effect.Execute(new CardContext { Source = source });

            Assert.AreEqual(hpBefore, source.Stats.CurrentHealth, "Health is untouched while block absorbs.");
            Assert.AreEqual(2, source.Stats.Block, "Block should absorb the self-damage.");
        }

        [Test]
        public void Execute_LethalDamage_KillsSource()
        {
            source.Stats.MaxHealth = 3;
            source.Stats.CurrentHealth = 3;

            effect.Execute(new CardContext { Source = source });

            Assert.IsFalse(source.Stats.IsAlive);
        }

        [Test]
        public void Execute_IgnoresBonusDamage()
        {
            effect.Execute(new CardContext { Source = source, BonusDamage = 10 });

            Assert.AreEqual(3, source.Stats.MaxHealth - source.Stats.CurrentHealth,
                "Self-damage must not scale with enchantment bonus damage.");
        }
    }
}
