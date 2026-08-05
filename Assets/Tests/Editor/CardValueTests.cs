using NUnit.Framework;
using Cards;
using Characters;

namespace Tests.Cards
{
    [TestFixture]
    public class CardValueTests
    {
        private Character source;

        [SetUp]
        public void SetUp()
        {
            source = TestHelper.CreateCharacter(0, 0, 0);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(source.CurrentCell.gameObject);
            UnityEngine.Object.DestroyImmediate(source.gameObject);
        }

        [Test]
        public void Constant_ReturnsSetValue()
        {
            var cv = CardValue.MakeConstant(7);
            Assert.AreEqual(7, cv.Evaluate(source));
        }

        [Test]
        public void CurrentBlock_ReturnsBlockValue()
        {
            source.Stats.GainBlock(5);
            var cv = CardValue.MakeBlock();
            Assert.AreEqual(5, cv.Evaluate(source));
        }

        [Test]
        public void CurrentHealth_ReturnsHealthValue()
        {
            source.Stats.ApplyDamage(3);
            var cv = CardValue.MakeHealth();
            Assert.AreEqual(source.Stats.CurrentHealth, cv.Evaluate(source));
        }

        [Test]
        public void MaxHealth_ReturnsMaxHealth()
        {
            var cv = new CardValue { Type = CardValueType.MaxHealth };
            Assert.AreEqual(source.Stats.MaxHealth, cv.Evaluate(source));
        }

        [Test]
        public void MissingHealth_ReturnsDifference()
        {
            source.Stats.ApplyDamage(4);
            var cv = CardValue.MakeMissingHealth();
            Assert.AreEqual(source.Stats.MaxHealth - source.Stats.CurrentHealth, cv.Evaluate(source));
        }

        [Test]
        public void Evaluate_NullSource_ReturnsConstant()
        {
            var cv = CardValue.MakeConstant(3);
            Assert.AreEqual(3, cv.Evaluate(null));
        }

        [Test]
        public void CurrentBlock_ZeroBlock_ReturnsZero()
        {
            var cv = CardValue.MakeBlock();
            Assert.AreEqual(0, cv.Evaluate(source));
        }

        [Test]
        public void MissingHealth_FullHealth_ReturnsZero()
        {
            var cv = CardValue.MakeMissingHealth();
            Assert.AreEqual(0, cv.Evaluate(source));
        }
    }
}
