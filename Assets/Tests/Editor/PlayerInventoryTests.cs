using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Equipment;

namespace Tests.Equipment
{
    [TestFixture]
    public class PlayerInventoryTests
    {
        private PlayerInventory inventory;
        private EquipmentData itemA;
        private EquipmentData itemB;

        [SetUp]
        public void SetUp()
        {
            inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            inventory.Clear();

            itemA = ScriptableObject.CreateInstance<EquipmentData>();
            itemA.EquipmentName = "Item A";
            itemB = ScriptableObject.CreateInstance<EquipmentData>();
            itemB.EquipmentName = "Item B";
        }

        [Test]
        public void NewInventory_IsEmpty()
        {
            Assert.AreEqual(0, inventory.UnlockedEquipment.Count);
        }

        [Test]
        public void Unlock_AddsItem()
        {
            inventory.Unlock(itemA);
            Assert.AreEqual(1, inventory.UnlockedEquipment.Count);
            Assert.IsTrue(inventory.IsUnlocked(itemA));
        }

        [Test]
        public void Unlock_DuplicateIgnored()
        {
            inventory.Unlock(itemA);
            inventory.Unlock(itemA);
            Assert.AreEqual(1, inventory.UnlockedEquipment.Count);
        }

        [Test]
        public void Unlock_NullIgnored()
        {
            inventory.Unlock(null);
            Assert.AreEqual(0, inventory.UnlockedEquipment.Count);
        }

        [Test]
        public void Remove_RemovesItem()
        {
            inventory.Unlock(itemA);
            inventory.Remove(itemA);
            Assert.AreEqual(0, inventory.UnlockedEquipment.Count);
            Assert.IsFalse(inventory.IsUnlocked(itemA));
        }

        [Test]
        public void Remove_NotPresent_DoesNotFire()
        {
            bool fired = false;
            inventory.OnInventoryChanged += () => fired = true;

            inventory.Remove(itemA);

            Assert.IsFalse(fired);
        }

        [Test]
        public void IsUnlocked_FalseForAbsentItem()
        {
            Assert.IsFalse(inventory.IsUnlocked(itemA));
        }

        [Test]
        public void IsUnlocked_FalseForNull()
        {
            Assert.IsFalse(inventory.IsUnlocked(null));
        }

        [Test]
        public void Clear_RemovesAll()
        {
            inventory.Unlock(itemA);
            inventory.Unlock(itemB);
            inventory.Clear();
            Assert.AreEqual(0, inventory.UnlockedEquipment.Count);
        }

        [Test]
        public void SetUnlocked_ReplacesList()
        {
            inventory.Unlock(itemA);

            inventory.SetUnlocked(new List<EquipmentData> { itemB });

            Assert.AreEqual(1, inventory.UnlockedEquipment.Count);
            Assert.IsTrue(inventory.IsUnlocked(itemB));
            Assert.IsFalse(inventory.IsUnlocked(itemA));
        }

        [Test]
        public void SetUnlocked_Null_DefaultsEmpty()
        {
            inventory.Unlock(itemA);
            inventory.SetUnlocked(null);
            Assert.AreEqual(0, inventory.UnlockedEquipment.Count);
        }

        [Test]
        public void OnInventoryChanged_FiresOnUnlock()
        {
            bool fired = false;
            inventory.OnInventoryChanged += () => fired = true;
            inventory.Unlock(itemA);
            Assert.IsTrue(fired);
        }

        [Test]
        public void OnInventoryChanged_FiresOnRemove()
        {
            inventory.Unlock(itemA);
            bool fired = false;
            inventory.OnInventoryChanged += () => fired = true;
            inventory.Remove(itemA);
            Assert.IsTrue(fired);
        }

        [Test]
        public void OnInventoryChanged_FiresOnClear()
        {
            bool fired = false;
            inventory.OnInventoryChanged += () => fired = true;
            inventory.Clear();
            Assert.IsTrue(fired);
        }

        [Test]
        public void OnInventoryChanged_FiresOnSetUnlocked()
        {
            bool fired = false;
            inventory.OnInventoryChanged += () => fired = true;
            inventory.SetUnlocked(new List<EquipmentData>());
            Assert.IsTrue(fired);
        }

        [Test]
        public void Clear_RemovesAllUnlocks()
        {
            inventory.Unlock(ScriptableObject.CreateInstance<EquipmentData>());
            inventory.Clear();
            Assert.AreEqual(0, inventory.UnlockedEquipment.Count);
        }
    }
}
