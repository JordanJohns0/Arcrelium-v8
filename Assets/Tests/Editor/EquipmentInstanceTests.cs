using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Equipment;

namespace Tests.Equipment
{
    [TestFixture]
    public class EquipmentInstanceTests
    {
        private readonly List<Object> tracked = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in tracked)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            tracked.Clear();
        }

        private EquipmentData MakeEquipment(string name)
        {
            var data = ScriptableObject.CreateInstance<EquipmentData>();
            data.EquipmentName = name;
            tracked.Add(data);
            return data;
        }

        private EquipmentEnchantmentSO MakeEnchantment(string name)
        {
            var so = ScriptableObject.CreateInstance<GrantCardEnchantmentSO>();
            so.EnchantmentName = name;
            tracked.Add(so);
            return so;
        }

        [Test]
        public void EquipmentInstance_Wraps_EquipmentData()
        {
            EquipmentData data = MakeEquipment("Iron Helm");

            var instance = new EquipmentInstance(data);

            Assert.AreEqual(data, instance.Data);
            Assert.AreEqual("Iron Helm", instance.Data.EquipmentName);
        }

        [Test]
        public void TwoInstances_SameData_AreDistinctReferences()
        {
            EquipmentData data = MakeEquipment("Iron Helm");

            var a = new EquipmentInstance(data);
            var b = new EquipmentInstance(data);

            Assert.AreNotSame(a, b);
            Assert.AreSame(a.Data, b.Data);
        }

        [Test]
        public void AllEnchantments_CombinesTemplateAndRunTimeExtras()
        {
            var data = MakeEquipment("Iron Helm");
            var template = MakeEnchantment("Plate Bound");
            data.Enchantments.Add(template);
            var extra = MakeEnchantment("Station Blessing");
            var instance = new EquipmentInstance(data);

            instance.AddEnchantments(new[] { extra });

            var all = new List<EquipmentEnchantmentSO>(instance.AllEnchantments);
            Assert.AreEqual(2, all.Count);
            CollectionAssert.Contains(all, template);
            CollectionAssert.Contains(all, extra);
            CollectionAssert.Contains(new List<EquipmentEnchantmentSO>(instance.ExtraEnchantments), extra);
        }

        [Test]
        public void AddEnchantments_IgnoresNulls()
        {
            var instance = new EquipmentInstance(MakeEquipment("Iron Helm"));

            instance.AddEnchantments(new[] { MakeEnchantment("Real"), null });
            instance.AddEnchantments(null);

            Assert.AreEqual(1, instance.ExtraEnchantments.Count);
        }
    }
}
