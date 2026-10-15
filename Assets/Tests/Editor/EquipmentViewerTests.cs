using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Characters;
using Equipment;
using UI;

namespace Tests.UI
{
    [TestFixture]
    public class EquipmentViewerTests
    {
        private readonly List<Object> owned = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in owned)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            owned.Clear();
        }

        private Character MakeCharacterWithEquipment(out EquipmentManager manager)
        {
            var go = new GameObject("Player", typeof(Character));
            owned.Add(go);

            manager = go.AddComponent<EquipmentManager>();
            var character = go.GetComponent<Character>();
            typeof(Character).GetField("equipmentManager", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(character, manager);
            return character;
        }

        [Test]
        public void BuildEquipmentText_NoEquipment_SaysNothingEquipped()
        {
            var character = MakeCharacterWithEquipment(out _);

            Assert.AreEqual("Nothing equipped.", EquipmentViewer.BuildEquipmentText(character));
        }

        [Test]
        public void BuildEquipmentText_ListsItemsAndEnchantmentsAsNameColonDescription()
        {
            var character = MakeCharacterWithEquipment(out EquipmentManager manager);

            var sword = ScriptableObject.CreateInstance<EquipmentData>();
            sword.EquipmentName = "Sword";
            sword.SlotType = EquipmentSlotType.Hand;
            owned.Add(sword);

            var sharpened = ScriptableObject.CreateInstance<GrantCardEnchantmentSO>();
            sharpened.EnchantmentName = "Sharpened";
            sharpened.Description = "Adds damage.";
            owned.Add(sharpened);

            manager.Equip(sword, EquipmentSlotType.Hand, 0, new[] { (EquipmentEnchantmentSO)sharpened });

            string text = EquipmentViewer.BuildEquipmentText(character);

            StringAssert.Contains("Sword", text, "The equipped item must be listed.");
            StringAssert.Contains("Sharpened: Adds damage.", text, "Its enchantment must show as Name: Desc.");
        }
    }
}
