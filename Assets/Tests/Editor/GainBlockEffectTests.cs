using NUnit.Framework;
using UnityEngine;
using Cards;
using Cards.Effects;
using Characters;
using HexGrid;

namespace Tests
{
    [TestFixture]
    public class GainBlockEffectTests
    {
        private GainBlockEffect effect;
        private Character source;

        [SetUp]
        public void SetUp()
        {
            effect = ScriptableObject.CreateInstance<GainBlockEffect>();
            effect.Amount = 3;

            source = TestHelper.CreateCharacter(0, 0, 0);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(source.CurrentCell.gameObject);
            Object.DestroyImmediate(source.gameObject);
        }

        [Test]
        public void Execute_GrantsBlock()
        {
            var ctx = new CardContext { Source = source };
            effect.Execute(ctx);
            Assert.AreEqual(3, source.Stats.Block);
        }

        [Test]
        public void Execute_Additive()
        {
            var ctx = new CardContext { Source = source };
            effect.Execute(ctx);
            effect.Execute(ctx);
            Assert.AreEqual(6, source.Stats.Block);
        }

        [Test]
        public void Execute_BlockAbsorbsDamage()
        {
            var ctx = new CardContext { Source = source };
            effect.Execute(ctx);

            source.Stats.ApplyDamage(2);
            Assert.AreEqual(1, source.Stats.Block);
            Assert.AreEqual(source.Stats.MaxHealth, source.Stats.CurrentHealth);
        }
    }
}
