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

        [Test]
        public void Execute_WithBonusDamage_AddsToBase()
        {
            var ctx = new CardContext { Source = source, TargetCharacter = target, Range = 5, BonusDamage = 2 };
            effect.Execute(ctx);
            Assert.AreEqual(7, target.Stats.MaxHealth - target.Stats.CurrentHealth);
        }

        [Test]
        public void Execute_NegativeBonusDamage_Ignored()
        {
            var ctx = new CardContext { Source = source, TargetCharacter = target, Range = 5, BonusDamage = -5 };
            effect.Execute(ctx);
            Assert.AreEqual(5, target.Stats.MaxHealth - target.Stats.CurrentHealth,
                "A negative bonus must not reduce (or heal through) card damage.");
        }

        [Test]
        public void Execute_OnHitEffects_AppliedToTheHitEnemyAfterTheDamage()
        {
            var onHit = ScriptableObject.CreateInstance<AttackEffect>();
            onHit.Damage = CardValue.MakeConstant(2);
            effect.OnHitEffects = new List<CardEffectSO> { onHit };

            var ctx = new CardContext { Source = source, TargetCharacter = target, Range = 5 };
            effect.Execute(ctx);

            Assert.AreEqual(7, target.Stats.MaxHealth - target.Stats.CurrentHealth,
                "5 from the attack + 2 from the effect applied to the enemy that was hit.");
        }

        [Test]
        public void Execute_OnHitEffect_SkippedWhenTheHitKills()
        {
            target.Stats.MaxHealth = 3;
            target.Stats.CurrentHealth = 3;

            var onHit = ScriptableObject.CreateInstance<AttackEffect>();
            onHit.Damage = CardValue.MakeConstant(2);
            effect.OnHitEffects = new List<CardEffectSO> { onHit };

            var ctx = new CardContext { Source = source, TargetCharacter = target, Range = 5 };
            effect.Execute(ctx);

            Assert.IsFalse(target.Stats.IsAlive);
            Assert.AreEqual(3, target.Stats.MaxHealth - target.Stats.CurrentHealth,
                "A dead enemy is not 'the enemy that is hit' — the follow-up must not run.");
        }

        [Test]
        public void Execute_OnHitEffect_KeepsSourceAndPicksTheVictimAsTarget()
        {
            var onHit = ScriptableObject.CreateInstance<GainBlockEffect>();
            onHit.Amount = CardValue.MakeConstant(2);
            effect.OnHitEffects = new List<CardEffectSO> { onHit };

            var ctx = new CardContext { Source = source, TargetCharacter = target, Range = 5 };
            effect.Execute(ctx);

            Assert.AreEqual(2, source.Stats.Block,
                "Non-damage follow-ups run too, with the original source and the hit enemy as target.");
            Assert.AreEqual(5, target.Stats.MaxHealth - target.Stats.CurrentHealth,
                "The primary attack still deals its own damage.");
        }
    }
}
