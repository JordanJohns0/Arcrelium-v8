using NUnit.Framework;
using UnityEngine;
using Cards;

namespace Tests
{
    [TestFixture]
    public class CardInstanceTests
    {
        [Test]
        public void CardInstance_Wraps_CardData()
        {
            CardData data = ScriptableObject.CreateInstance<CardData>();
            data.CardName = "Test Card";

            var instance = new CardInstance(data);

            Assert.AreEqual(data, instance.Data);
            Assert.AreEqual("Test Card", instance.Data.CardName);
        }

        [Test]
        public void TwoInstances_SameData_AreDistinctReferences()
        {
            CardData data = ScriptableObject.CreateInstance<CardData>();

            var a = new CardInstance(data);
            var b = new CardInstance(data);

            Assert.AreNotSame(a, b);
            Assert.AreSame(a.Data, b.Data);
        }

        [Test]
        public void TwoInstances_DifferentData_AreDistinct()
        {
            CardData dataA = ScriptableObject.CreateInstance<CardData>();
            CardData dataB = ScriptableObject.CreateInstance<CardData>();

            var a = new CardInstance(dataA);
            var b = new CardInstance(dataB);

            Assert.AreNotSame(a, b);
            Assert.AreNotSame(a.Data, b.Data);
        }
    }
}
