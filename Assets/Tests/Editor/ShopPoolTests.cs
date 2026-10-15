using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Cards;
using Equipment;

namespace Tests.Equipment
{
    [TestFixture]
    public class ShopPoolTests
    {
        private readonly List<ScriptableObject> tracked = new List<ScriptableObject>();

        [SetUp]
        public void SetUp()
        {
            PlayerInventoryProvider.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            ShopPool.SetMain(null);
            RewardPool.SetMain(null);
            PlayerInventoryProvider.ResetForTests();
            foreach (var so in tracked)
            {
                if (so != null) Object.DestroyImmediate(so);
            }
            tracked.Clear();
        }

        private EquipmentData MakeEquipment(EquipmentSlotType slot, string name, int cardCount = 0)
        {
            var data = ScriptableObject.CreateInstance<EquipmentData>();
            data.EquipmentName = name;
            data.SlotType = slot;
            data.Cards = new List<CardData>();
            for (int i = 0; i < cardCount; i++)
            {
                var card = ScriptableObject.CreateInstance<CardData>();
                card.CardName = name + " Card " + i;
                tracked.Add(card);
                data.Cards.Add(card);
            }
            tracked.Add(data);
            return data;
        }

        private PlayerInventory MakeInventory()
        {
            var inventory = ScriptableObject.CreateInstance<PlayerInventory>();
            tracked.Add(inventory);
            return inventory;
        }

        private ShopPool MakePool(params EquipmentData[] stock)
        {
            var pool = ScriptableObject.CreateInstance<ShopPool>();
            pool.stock.AddRange(stock);
            tracked.Add(pool);
            return pool;
        }

        [Test]
        public void Offers_ExcludesOwnedItems()
        {
            var owned = MakeEquipment(EquipmentSlotType.Head, "Owned");
            var fresh = MakeEquipment(EquipmentSlotType.Hand, "Fresh");
            ShopPool.SetMain(MakePool(owned, fresh));

            var inventory = MakeInventory();
            inventory.Unlock(owned);

            var offers = ShopPool.Offers(inventory, 4, new System.Random(1));

            Assert.AreEqual(1, offers.Count);
            Assert.AreSame(fresh, offers[0].Item);
        }

        [Test]
        public void Offers_ExcludesEquippedItems_EvenWhenNotUnlocked()
        {
            var equipped = MakeEquipment(EquipmentSlotType.Trinket, "Worn");
            var fresh = MakeEquipment(EquipmentSlotType.Head, "Fresh");
            ShopPool.SetMain(MakePool(equipped, fresh));

            var inventory = MakeInventory();
            inventory.Loadout.Set(EquipmentSlotType.Trinket, 0, equipped);

            var offers = ShopPool.Offers(inventory, 4, new System.Random(1));

            Assert.AreEqual(1, offers.Count);
            Assert.AreSame(fresh, offers[0].Item);
        }

        [Test]
        public void Offers_EmptyStock_FallsBackToRewardPool()
        {
            var a = MakeEquipment(EquipmentSlotType.Head, "A");
            var b = MakeEquipment(EquipmentSlotType.Hand, "B");
            var rewardPool = ScriptableObject.CreateInstance<RewardPool>();
            tracked.Add(rewardPool);
            rewardPool.rewards.AddRange(new[] { a, b });
            RewardPool.SetMain(rewardPool);

            ShopPool.SetMain(MakePool()); // no authored stock

            var offers = ShopPool.Offers(MakeInventory(), 4, new System.Random(1));

            CollectionAssert.AreEquivalent(new[] { a, b }, offers.Select(o => o.Item).ToArray());
        }

        [Test]
        public void Offers_LimitedToCount()
        {
            ShopPool.SetMain(MakePool(
                MakeEquipment(EquipmentSlotType.Head, "A"),
                MakeEquipment(EquipmentSlotType.Hand, "B"),
                MakeEquipment(EquipmentSlotType.Chest, "C"),
                MakeEquipment(EquipmentSlotType.Feet, "D")));

            var offers = ShopPool.Offers(MakeInventory(), 3, new System.Random(1));

            Assert.AreEqual(3, offers.Count, "The shop offers exactly three items when enough are available.");
        }

        [Test]
        public void Offers_NeverRepeatsAnItem()
        {
            var a = MakeEquipment(EquipmentSlotType.Head, "A");
            var b = MakeEquipment(EquipmentSlotType.Hand, "B");
            ShopPool.SetMain(MakePool(a, a, b));

            var offers = ShopPool.Offers(MakeInventory(), 3, new System.Random(1));

            Assert.AreEqual(2, offers.Count, "A duplicated stock entry must only be offered once.");
            CollectionAssert.AreEquivalent(new[] { a, b }, offers.Select(o => o.Item).ToArray());
        }

        [Test]
        public void Offers_SeededRng_IsDeterministic()
        {
            ShopPool.SetMain(MakePool(
                MakeEquipment(EquipmentSlotType.Head, "A"),
                MakeEquipment(EquipmentSlotType.Hand, "B"),
                MakeEquipment(EquipmentSlotType.Chest, "C")));

            var first = ShopPool.Offers(MakeInventory(), 3, new System.Random(99));
            var second = ShopPool.Offers(MakeInventory(), 3, new System.Random(99));

            CollectionAssert.AreEqual(
                first.Select(o => o.Item.EquipmentName).ToArray(),
                second.Select(o => o.Item.EquipmentName).ToArray());
        }

        [Test]
        public void Offers_AllOwned_ReturnsEmpty()
        {
            var a = MakeEquipment(EquipmentSlotType.Head, "A");
            ShopPool.SetMain(MakePool(a));

            var inventory = MakeInventory();
            inventory.Unlock(a);

            Assert.AreEqual(0, ShopPool.Offers(inventory, 3, new System.Random(1)).Count);
        }

        [Test]
        public void Offers_SkipsNullEntries()
        {
            var a = MakeEquipment(EquipmentSlotType.Head, "A");
            ShopPool.SetMain(MakePool(null, a));

            var offers = ShopPool.Offers(MakeInventory(), 3, new System.Random(1));

            Assert.AreEqual(1, offers.Count);
            Assert.AreSame(a, offers[0].Item);
        }
    }
}
