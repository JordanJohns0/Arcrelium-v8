using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Cards;
using Cards.Effects;
using Characters;
using HexGrid;

namespace Tests.Cards
{
    [TestFixture]
    public class CompositeEffectTests
    {
        private CompositeEffect composite;
        private Character source;
        private Character target;

        [SetUp]
        public void SetUp()
        {
            composite = ScriptableObject.CreateInstance<CompositeEffect>();
            composite.Effects = new List<CardEffectSO>();

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
        public void Execute_RunsAllEffects()
        {
            var heal = ScriptableObject.CreateInstance<HealEffect>();
            heal.Amount = CardValue.MakeConstant(3);
            var block = ScriptableObject.CreateInstance<GainBlockEffect>();
            block.Amount = CardValue.MakeConstant(2);

            source.Stats.ApplyDamage(5);
            composite.Effects = new List<CardEffectSO> { heal, block };

            var ctx = new CardContext { Source = source, TargetCharacter = target, Range = 5 };
            composite.Execute(ctx);

            Assert.AreEqual(8, source.Stats.CurrentHealth);
            Assert.AreEqual(2, source.Stats.Block);
        }

        [Test]
        public void CanExecute_AllSubEffectsMustPass()
        {
            var attack = ScriptableObject.CreateInstance<AttackEffect>();
            attack.Damage = CardValue.MakeConstant(1);
            composite.Effects = new List<CardEffectSO> { attack };

            var ctx = new CardContext { Source = source, TargetCharacter = null, Range = 5 };
            Assert.IsFalse(composite.CanExecute(ctx));
        }

        [Test]
        public void CanExecute_EmptyList_ReturnsTrue()
        {
            composite.Effects = new List<CardEffectSO>();
            var ctx = new CardContext { Source = source };
            Assert.IsTrue(composite.CanExecute(ctx));
        }

        [Test]
        public void Execute_NullEffectsInList_Skipped()
        {
            composite.Effects = new List<CardEffectSO> { null };
            var ctx = new CardContext { Source = source };
            Assert.DoesNotThrow(() => composite.Execute(ctx));
        }

        [Test]
        public void Execute_OrderRespected()
        {
            var block = ScriptableObject.CreateInstance<GainBlockEffect>();
            block.Amount = CardValue.MakeConstant(5);
            var attack = ScriptableObject.CreateInstance<AttackEffect>();
            attack.Damage = CardValue.MakeConstant(3);

            composite.Effects = new List<CardEffectSO> { block, attack };
            var ctx = new CardContext { Source = source, TargetCharacter = target, Range = 5 };
            composite.Execute(ctx);

            Assert.AreEqual(5, source.Stats.Block);
            Assert.AreEqual(3, target.Stats.MaxHealth - target.Stats.CurrentHealth);
        }
    }
}
