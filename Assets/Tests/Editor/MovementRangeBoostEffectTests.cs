using NUnit.Framework;
using UnityEngine;
using Cards;
using Characters;
using Equipment;
using Equipment.Effects;
using HexGrid;

namespace Tests.Cards
{
    [TestFixture]
    public class MovementRangeBoostEffectTests
    {
        private MovementRangeBoostEffect effect;
        private Character source;

        [SetUp]
        public void SetUp()
        {
            effect = ScriptableObject.CreateInstance<MovementRangeBoostEffect>();
            effect.BonusRange = 2;
            source = TestHelper.CreateCharacter(0, 0, 0);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(effect);
            Object.DestroyImmediate(source.CurrentCell.gameObject);
            Object.DestroyImmediate(source.gameObject);
        }

        [Test]
        public void Execute_IncreasesBonusRange()
        {
            var cardCtx = new CardContext { Source = source, Range = 3 };
            var equipCtx = new EquipmentContext
            {
                Source = source,
                Timing = TriggerTiming.OnBeforeCardPlay,
                CardContext = cardCtx
            };

            effect.Execute(equipCtx);

            Assert.AreEqual(2, cardCtx.BonusRange);
            Assert.AreEqual(5, cardCtx.EffectiveRange);
        }

        [Test]
        public void Execute_CumulativeWithMultipleEffects()
        {
            var effect2 = ScriptableObject.CreateInstance<MovementRangeBoostEffect>();
            effect2.BonusRange = 1;

            var cardCtx = new CardContext { Source = source, Range = 3 };
            var equipCtx = new EquipmentContext
            {
                Source = source,
                Timing = TriggerTiming.OnBeforeCardPlay,
                CardContext = cardCtx
            };

            effect.Execute(equipCtx);
            effect2.Execute(equipCtx);

            Assert.AreEqual(3, cardCtx.BonusRange);
            Assert.AreEqual(6, cardCtx.EffectiveRange);
            Object.DestroyImmediate(effect2);
        }

        [Test]
        public void Execute_ZeroBaseRange_Works()
        {
            effect.BonusRange = 3;
            var cardCtx = new CardContext { Source = source, Range = 0 };
            var equipCtx = new EquipmentContext
            {
                Source = source,
                Timing = TriggerTiming.OnBeforeCardPlay,
                CardContext = cardCtx
            };

            effect.Execute(equipCtx);

            Assert.AreEqual(3, cardCtx.EffectiveRange);
        }

        [Test]
        public void EffectiveRange_BasePlusBonus()
        {
            var cardCtx = new CardContext { Source = source, Range = 2, BonusRange = 1 };
            Assert.AreEqual(3, cardCtx.EffectiveRange);
        }
    }
}
