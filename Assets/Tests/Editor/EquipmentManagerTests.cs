using NUnit.Framework;
using UnityEngine;
using Equipment;

namespace Tests.Equipment
{
    using Cards = global::Cards;

    [TestFixture]
    public class EquipmentManagerTests
    {
        private EquipmentManager manager;

        [SetUp]
        public void SetUp()
        {
            var go = new GameObject("TestEquipmentManager");
            manager = go.AddComponent<EquipmentManager>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(manager.gameObject);
        }

        private EquipmentData MakeEquip(EquipmentSlotType slot, string name = "Test")
        {
            var data = ScriptableObject.CreateInstance<EquipmentData>();
            data.EquipmentName = name;
            data.SlotType = slot;
            return data;
        }

        [Test]
        public void Equip_ReturnsTrueOnSuccess()
        {
            var data = MakeEquip(EquipmentSlotType.Head);
            Assert.IsTrue(manager.Equip(data));
        }

        [Test]
        public void Equip_Null_ReturnsFalse()
        {
            Assert.IsFalse(manager.Equip(null));
        }

        [Test]
        public void Equip_WrongSlotType_ReturnsFalse()
        {
            var data = MakeEquip(EquipmentSlotType.Head);
            Assert.IsFalse(manager.Equip(data, EquipmentSlotType.Chest));
        }

        [Test]
        public void Equip_Head_SingleSlot()
        {
            var data = MakeEquip(EquipmentSlotType.Head);
            manager.Equip(data);
            var equipped = manager.GetEquipped(EquipmentSlotType.Head);
            Assert.IsNotNull(equipped);
            Assert.AreEqual(data, equipped.Data);
        }

        [Test]
        public void Equip_Head_SecondFails()
        {
            var data1 = MakeEquip(EquipmentSlotType.Head, "Helm 1");
            var data2 = MakeEquip(EquipmentSlotType.Head, "Helm 2");
            manager.Equip(data1);
            Assert.IsFalse(manager.Equip(data2));
        }

        [Test]
        public void Equip_Hand_LeftAndRight()
        {
            var left = MakeEquip(EquipmentSlotType.Hand, "Sword");
            var right = MakeEquip(EquipmentSlotType.Hand, "Shield");

            manager.Equip(left, EquipmentSlotType.Hand, 0);
            manager.Equip(right, EquipmentSlotType.Hand, 1);

            Assert.AreEqual(left, manager.GetEquipped(EquipmentSlotType.Hand, 0).Data);
            Assert.AreEqual(right, manager.GetEquipped(EquipmentSlotType.Hand, 1).Data);
        }

        [Test]
        public void Equip_Hand_OnlyOnePerSlot()
        {
            var left = MakeEquip(EquipmentSlotType.Hand, "Sword");
            manager.Equip(left, EquipmentSlotType.Hand, 0);
            Assert.IsFalse(manager.Equip(left, EquipmentSlotType.Hand, 0));
        }

        [Test]
        public void Equip_Trinket_UpToThree()
        {
            var t1 = MakeEquip(EquipmentSlotType.Trinket, "T1");
            var t2 = MakeEquip(EquipmentSlotType.Trinket, "T2");
            var t3 = MakeEquip(EquipmentSlotType.Trinket, "T3");
            var t4 = MakeEquip(EquipmentSlotType.Trinket, "T4");

            Assert.IsTrue(manager.Equip(t1));
            Assert.IsTrue(manager.Equip(t2));
            Assert.IsTrue(manager.Equip(t3));
            Assert.IsFalse(manager.Equip(t4));
        }

        [Test]
        public void Unequip_Head_ClearsSlot()
        {
            var data = MakeEquip(EquipmentSlotType.Head);
            manager.Equip(data);
            manager.Unequip(EquipmentSlotType.Head);
            Assert.IsNull(manager.GetEquipped(EquipmentSlotType.Head));
        }

        [Test]
        public void Unequip_Empty_ReturnsFalse()
        {
            Assert.IsFalse(manager.Unequip(EquipmentSlotType.Head));
        }

        [Test]
        public void Unequip_Hand_ByIndex()
        {
            var left = MakeEquip(EquipmentSlotType.Hand, "Sword");
            var right = MakeEquip(EquipmentSlotType.Hand, "Shield");
            manager.Equip(left, EquipmentSlotType.Hand, 0);
            manager.Equip(right, EquipmentSlotType.Hand, 1);

            manager.Unequip(EquipmentSlotType.Hand, 0);
            Assert.IsNull(manager.GetEquipped(EquipmentSlotType.Hand, 0));
            Assert.IsNotNull(manager.GetEquipped(EquipmentSlotType.Hand, 1));
        }

        [Test]
        public void Unequip_Trinket_ByIndex()
        {
            var t1 = MakeEquip(EquipmentSlotType.Trinket, "T1");
            var t2 = MakeEquip(EquipmentSlotType.Trinket, "T2");
            manager.Equip(t1);
            manager.Equip(t2);

            manager.Unequip(EquipmentSlotType.Trinket, 0);
            var remaining = manager.GetEquippedInSlot(EquipmentSlotType.Trinket);
            Assert.AreEqual(1, remaining.Count);
        }

        [Test]
        public void GetAllCards_CollectsFromAllSlots()
        {
            var headData = MakeEquip(EquipmentSlotType.Head);
            headData.Cards = new System.Collections.Generic.List<Cards.CardData>
            {
                ScriptableObject.CreateInstance<Cards.CardData>(),
                ScriptableObject.CreateInstance<Cards.CardData>()
            };

            var chestData = MakeEquip(EquipmentSlotType.Chest);
            chestData.Cards = new System.Collections.Generic.List<Cards.CardData>
            {
                ScriptableObject.CreateInstance<Cards.CardData>()
            };

            manager.Equip(headData);
            manager.Equip(chestData);

            var allCards = manager.GetAllCards();
            Assert.AreEqual(3, allCards.Count);
        }

        [Test]
        public void GetAllCards_EmptyWhenNoEquipment()
        {
            Assert.AreEqual(0, manager.GetAllCards().Count);
        }

        [Test]
        public void OnEquipmentChanged_FiresOnEquip()
        {
            bool fired = false;
            manager.OnEquipmentChanged += (inst) => fired = true;

            manager.Equip(MakeEquip(EquipmentSlotType.Head));
            Assert.IsTrue(fired);
        }

        [Test]
        public void OnEquipmentChanged_FiresOnUnequip()
        {
            var data = MakeEquip(EquipmentSlotType.Head);
            manager.Equip(data);

            bool fired = false;
            manager.OnEquipmentChanged += (inst) => fired = true;
            manager.Unequip(EquipmentSlotType.Head);
            Assert.IsTrue(fired);
        }

        [Test]
        public void GetEquippedInSlot_Head_ReturnsSingleItem()
        {
            var data = MakeEquip(EquipmentSlotType.Head);
            manager.Equip(data);

            var items = manager.GetEquippedInSlot(EquipmentSlotType.Head);
            Assert.AreEqual(1, items.Count);
        }

        [Test]
        public void GetEquippedInSlot_EmptySlot_ReturnsEmpty()
        {
            var items = manager.GetEquippedInSlot(EquipmentSlotType.Head);
            Assert.AreEqual(0, items.Count);
        }

        [Test]
        public void CanEquip_Head_FalseWhenOccupied()
        {
            var data = MakeEquip(EquipmentSlotType.Head);
            manager.Equip(data);
            Assert.IsFalse(manager.CanEquip(data, EquipmentSlotType.Head));
        }

        [Test]
        public void CanEquip_Head_TrueWhenEmpty()
        {
            var data = MakeEquip(EquipmentSlotType.Head);
            Assert.IsTrue(manager.CanEquip(data, EquipmentSlotType.Head));
        }

        [Test]
        public void CanEquip_NullData_ReturnsFalse()
        {
            Assert.IsFalse(manager.CanEquip(null, EquipmentSlotType.Head));
        }

        [Test]
        public void Equip_Chest_SingleSlot()
        {
            var data = MakeEquip(EquipmentSlotType.Chest);
            manager.Equip(data);
            Assert.IsNotNull(manager.GetEquipped(EquipmentSlotType.Chest));
        }

        [Test]
        public void Equip_Feet_SingleSlot()
        {
            var data = MakeEquip(EquipmentSlotType.Feet);
            manager.Equip(data);
            Assert.IsNotNull(manager.GetEquipped(EquipmentSlotType.Feet));
        }

        [Test]
        public void GetLoadout_Empty_WhenNothingEquipped()
        {
            var loadout = manager.GetLoadout();
            Assert.IsNotNull(loadout);
            Assert.IsTrue(loadout.IsEmpty);
        }

        [Test]
        public void GetLoadout_SnapshotsAllFixedSlots()
        {
            var head = MakeEquip(EquipmentSlotType.Head, "Helm");
            var chest = MakeEquip(EquipmentSlotType.Chest, "Plate");
            var feet = MakeEquip(EquipmentSlotType.Feet, "Boots");
            var left = MakeEquip(EquipmentSlotType.Hand, "Sword");
            var right = MakeEquip(EquipmentSlotType.Hand, "Shield");
            var t1 = MakeEquip(EquipmentSlotType.Trinket, "Ring 1");
            var t2 = MakeEquip(EquipmentSlotType.Trinket, "Ring 2");
            var t3 = MakeEquip(EquipmentSlotType.Trinket, "Amulet");

            manager.Equip(head, EquipmentSlotType.Head, 0);
            manager.Equip(chest, EquipmentSlotType.Chest, 0);
            manager.Equip(feet, EquipmentSlotType.Feet, 0);
            manager.Equip(left, EquipmentSlotType.Hand, 0);
            manager.Equip(right, EquipmentSlotType.Hand, 1);
            manager.Equip(t1, EquipmentSlotType.Trinket, 0);
            manager.Equip(t2, EquipmentSlotType.Trinket, 1);
            manager.Equip(t3, EquipmentSlotType.Trinket, 2);

            var loadout = manager.GetLoadout();
            Assert.AreEqual(head, loadout.Head);
            Assert.AreEqual(chest, loadout.Chest);
            Assert.AreEqual(feet, loadout.Feet);
            Assert.AreEqual(left, loadout.LeftHand);
            Assert.AreEqual(right, loadout.RightHand);
            Assert.AreEqual(t1, loadout.Trinket1);
            Assert.AreEqual(t2, loadout.Trinket2);
            Assert.AreEqual(t3, loadout.Trinket3);
        }

        [Test]
        public void ApplyLoadout_EquipsEverySlot()
        {
            var loadout = new EquipmentLoadout
            {
                Head = MakeEquip(EquipmentSlotType.Head, "Helm"),
                Chest = MakeEquip(EquipmentSlotType.Chest, "Plate"),
                Feet = MakeEquip(EquipmentSlotType.Feet, "Boots"),
                LeftHand = MakeEquip(EquipmentSlotType.Hand, "Sword"),
                RightHand = MakeEquip(EquipmentSlotType.Hand, "Shield"),
                Trinket1 = MakeEquip(EquipmentSlotType.Trinket, "Ring 1"),
                Trinket2 = MakeEquip(EquipmentSlotType.Trinket, "Ring 2"),
                Trinket3 = MakeEquip(EquipmentSlotType.Trinket, "Amulet")
            };

            manager.ApplyLoadout(loadout);

            Assert.AreEqual("Helm", manager.GetEquipped(EquipmentSlotType.Head).Data.EquipmentName);
            Assert.AreEqual("Plate", manager.GetEquipped(EquipmentSlotType.Chest).Data.EquipmentName);
            Assert.AreEqual("Boots", manager.GetEquipped(EquipmentSlotType.Feet).Data.EquipmentName);
            Assert.AreEqual("Sword", manager.GetEquipped(EquipmentSlotType.Hand, 0).Data.EquipmentName);
            Assert.AreEqual("Shield", manager.GetEquipped(EquipmentSlotType.Hand, 1).Data.EquipmentName);
            Assert.AreEqual("Ring 1", manager.GetEquipped(EquipmentSlotType.Trinket, 0).Data.EquipmentName);
            Assert.AreEqual("Ring 2", manager.GetEquipped(EquipmentSlotType.Trinket, 1).Data.EquipmentName);
            Assert.AreEqual("Amulet", manager.GetEquipped(EquipmentSlotType.Trinket, 2).Data.EquipmentName);
        }

        [Test]
        public void ApplyLoadout_ClearsPreviouslyEquipped()
        {
            var head = MakeEquip(EquipmentSlotType.Head, "Old Helm");
            manager.Equip(head);
            Assert.IsNotNull(manager.GetEquipped(EquipmentSlotType.Head));

            manager.ApplyLoadout(new EquipmentLoadout());
            Assert.IsNull(manager.GetEquipped(EquipmentSlotType.Head));
            Assert.IsTrue(manager.GetLoadout().IsEmpty);
        }

        [Test]
        public void ApplyLoadout_Null_ClearsAllSlots()
        {
            var head = MakeEquip(EquipmentSlotType.Head, "Helm");
            manager.Equip(head);
            manager.ApplyLoadout(null);
            Assert.IsTrue(manager.GetLoadout().IsEmpty);
        }
    }
}
