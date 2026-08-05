using NUnit.Framework;
using UnityEngine;
using Equipment;

namespace Tests.Equipment
{
    [TestFixture]
    public class EquipmentInstanceTests
    {
        [Test]
        public void EquipmentInstance_Wraps_EquipmentData()
        {
            EquipmentData data = ScriptableObject.CreateInstance<EquipmentData>();
            data.EquipmentName = "Iron Helm";

            var instance = new EquipmentInstance(data);

            Assert.AreEqual(data, instance.Data);
            Assert.AreEqual("Iron Helm", instance.Data.EquipmentName);
        }

        [Test]
        public void TwoInstances_SameData_AreDistinctReferences()
        {
            EquipmentData data = ScriptableObject.CreateInstance<EquipmentData>();

            var a = new EquipmentInstance(data);
            var b = new EquipmentInstance(data);

            Assert.AreNotSame(a, b);
            Assert.AreSame(a.Data, b.Data);
        }
    }
}
