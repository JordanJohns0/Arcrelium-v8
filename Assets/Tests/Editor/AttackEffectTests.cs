using NUnit.Framework;
using UnityEngine;
using Cards;
using Cards.Effects;
using Characters;
using HexGrid;

namespace Tests
{
    [TestFixture]
    public class AttackEffectTests
    {
        private AttackEffect effect;
        private Character source;
        private Character target;

        [SetUp]
        public void SetUp()
        {
            effect = ScriptableObject.CreateInstance<AttackEffect>();
            effect.Damage = CardValue.MakeConstant(5);

            source = TestHelper.CreateCharacter(0, 0, 0);
            target = TestHelper.CreateCharacter(1, 1, 0);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(source.CurrentCell.gameObject);
            Object.DestroyImmediate(source.gameObject);
            Object.DestroyImmediate(target.CurrentCell.gameObject);
            Object.DestroyImmediate(target.gameObject);
        }

        [Test]
        public void CanExecute_NullTarget_ReturnsFalse()
        {
            var ctx = new CardContext { Source = source, Range = 5 };
            Assert.IsFalse(effect.CanExecute(ctx));
        }

        [Test]
        public void CanExecute_TargetInRange_ReturnsTrue()
        {
            var ctx = new CardContext { Source = source, TargetCharacter = target, Range = 5 };
            Assert.IsTrue(effect.CanExecute(ctx));
        }

        [Test]
        public void CanExecute_DeadTarget_ReturnsFalse()
        {
            target.Stats.CurrentHealth = 0;
            var ctx = new CardContext { Source = source, TargetCharacter = target, Range = 5 };
            Assert.IsFalse(effect.CanExecute(ctx));
        }

        [Test]
        public void Execute_DealsDamage()
        {
            var ctx = new CardContext { Source = source, TargetCharacter = target, Range = 5 };
            effect.Execute(ctx);
            Assert.AreEqual(5, target.Stats.MaxHealth - target.Stats.CurrentHealth);
        }

        [Test]
        public void Execute_LethalDamage()
        {
            target.Stats.MaxHealth = 3;
            target.Stats.CurrentHealth = 3;
            var ctx = new CardContext { Source = source, TargetCharacter = target, Range = 5 };
            effect.Execute(ctx);
            Assert.IsFalse(target.Stats.IsAlive);
        }
    }
}
