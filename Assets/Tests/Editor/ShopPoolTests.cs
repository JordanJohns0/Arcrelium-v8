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

        private ShopPool MakePool(params ShopEntry[] entries)
        {
            var pool = ScriptableObject.CreateInstance<ShopPool>();
            pool.stock.AddRange(entries);
            tracked.Add(pool);
            return pool;
        }

        private static ShopEntry Entry(EquipmentData item, int price) => new ShopEntry { item = item, price = price };

        [Test]
        public void Offers_ExcludesOwnedItems()
        {
            var owned = MakeEquipment(EquipmentSlotType.Head, "Owned");
            var fresh = MakeEquipment(EquipmentSlotType.Hand, "Fresh");
            var pool = MakePool(Entry(owned, 10), Entry(fresh, 10));
            ShopPool.SetMain(pool);

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
            var pool = MakePool(Entry(equipped, 10), Entry(fresh, 10));
            ShopPool.SetMain(pool);

            var inventory = MakeInventory();
            inventory.Loadout.Set(EquipmentSlotType.Trinket, 0, equipped);

            var offers = ShopPool.Offers(inventory, 4, new System.Random(1));

            Assert.AreEqual(1, offers.Count);
            Assert.AreSame(fresh, offers[0].Item);
        }

        [Test]
        public void Offers_UsesAuthoredPrice()
        {
            var item = MakeEquipment(EquipmentSlotType.Hand, "Sword");
            var pool = MakePool(Entry(item, 42));
            ShopPool.SetMain(pool);

            var offers = ShopPool.Offers(MakeInventory(), 4, new System.Random(1));

            Assert.AreEqual(1, offers.Count);
            Assert.AreEqual(42, offers[0].Price);
        }

        [Test]
        public void Offers_ZeroPriceEntry_DerivesPrice()
        {
            var item = MakeEquipment(EquipmentSlotType.Hand, "Sword", cardCount: 2);
            var pool = MakePool(Entry(item, 0));
            ShopPool.SetMain(pool);

            var offers = ShopPool.Offers(MakeInventory(), 4, new System.Random(1));

            Assert.AreEqual(1, offers.Count);
            Assert.AreEqual(ShopPricing.PriceFor(item), offers[0].Price);
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

            var shopPool = MakePool(); // no authored stock
            ShopPool.SetMain(shopPool);

            var offers = ShopPool.Offers(MakeInventory(), 4, new System.Random(1));

            CollectionAssert.AreEquivalent(new[] { a, b }, offers.Select(o => o.Item).ToArray());
        }

        [Test]
        public void Offers_LimitedToCount()
        {
            var pool = MakePool(
                Entry(MakeEquipment(EquipmentSlotType.Head, "A"), 10),
                Entry(MakeEquipment(EquipmentSlotType.Hand, "B"), 10),
                Entry(MakeEquipment(EquipmentSlotType.Chest, "C"), 10),
                Entry(MakeEquipment(EquipmentSlotType.Feet, "D"), 10));
            ShopPool.SetMain(pool);

            var offers = ShopPool.Offers(MakeInventory(), 2, new System.Random(1));

            Assert.AreEqual(2, offers.Count);
        }

        [Test]
        public void Offers_SeededRng_IsDeterministic()
        {
            var pool = MakePool(
                Entry(MakeEquipment(EquipmentSlotType.Head, "A"), 10),
                Entry(MakeEquipment(EquipmentSlotType.Hand, "B"), 10),
                Entry(MakeEquipment(EquipmentSlotType.Chest, "C"), 10));
            ShopPool.SetMain(pool);

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
            var pool = MakePool(Entry(a, 10));
            ShopPool.SetMain(pool);

            var inventory = MakeInventory();
            inventory.Unlock(a);

            Assert.AreEqual(0, ShopPool.Offers(inventory, 4, new System.Random(1)).Count);
        }

        [Test]
        public void Offers_SkipsNullEntries()
        {
            var a = MakeEquipment(EquipmentSlotType.Head, "A");
            var pool = MakePool(Entry(null, 10), Entry(a, 10));
            ShopPool.SetMain(pool);

            var offers = ShopPool.Offers(MakeInventory(), 4, new System.Random(1));

            Assert.AreEqual(1, offers.Count);
            Assert.AreSame(a, offers[0].Item);
        }

        [Test]
        public void PriceFor_Null_IsZero()
        {
            Assert.AreEqual(0, ShopPricing.PriceFor(null));
        }

        [Test]
        public void PriceFor_MoreCards_CostsMore()
        {
            var light = MakeEquipment(EquipmentSlotType.Hand, "Light");
            var heavy = MakeEquipment(EquipmentSlotType.Hand, "Heavy", cardCount: 3);
            Assert.Greater(ShopPricing.PriceFor(heavy), ShopPricing.PriceFor(light));
        }
    }
}
