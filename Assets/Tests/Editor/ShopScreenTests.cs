using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Equipment;
using UI;

namespace Tests.UI
{
    [TestFixture]
    public class ShopScreenTests
    {
        private readonly List<Object> tracked = new List<Object>();
        private PlayerInventory inventory;
        private int savedCount;

        [SetUp]
        public void SetUp()
        {
            savedCount = 0;
            inventory = Track(ScriptableObject.CreateInstance<PlayerInventory>()) as PlayerInventory;
            ShopScreen.LocalInventory = () => inventory;
            ShopScreen.PersistInventory = _ => savedCount++;
            // Pin the offer sources to explicit (empty) pools so no real Resources asset
            // leaks into the tests; MakePool then overrides the shop pool per test.
            ShopPool.SetMain(Track(ScriptableObject.CreateInstance<ShopPool>()) as ShopPool);
            RewardPool.SetMain(Track(ScriptableObject.CreateInstance<RewardPool>()) as RewardPool);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var screen in Object.FindObjectsByType<ShopScreen>(FindObjectsSortMode.None))
            {
                if (screen != null) Object.DestroyImmediate(screen);
            }
            ShopScreen.LocalInventory = PlayerInventoryProvider.GetLocal;
            ShopScreen.PersistInventory = inv =>
                PlayerInventoryProvider.Save(PlayerInventoryProvider.LocalPlayerKey, inv);
            ShopPool.SetMain(null);
            RewardPool.SetMain(null);
            PlayerInventoryProvider.ResetForTests();
            foreach (var obj in tracked)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            tracked.Clear();
        }

        private T Track<T>(T obj) where T : Object
        {
            tracked.Add(obj);
            return obj;
        }

        private EquipmentData MakeEquipment(string name)
        {
            var data = Track(ScriptableObject.CreateInstance<EquipmentData>()) as EquipmentData;
            data.EquipmentName = name;
            return data;
        }

        private ShopPool MakePool(params ShopEntry[] entries)
        {
            var pool = Track(ScriptableObject.CreateInstance<ShopPool>()) as ShopPool;
            pool.stock.AddRange(entries);
            ShopPool.SetMain(pool);
            return pool;
        }

        private static ShopEntry Entry(EquipmentData item, int price) => new ShopEntry { item = item, price = price };

        /// <summary>The pane the shop fills, mirroring the scene's "Special" column.</summary>
        private RectTransform MakeOffersPanel()
        {
            var go = Track(new GameObject("Special", typeof(RectTransform))) as GameObject;
            return go.GetComponent<RectTransform>();
        }

        private TMP_Text MakeGoldLabel()
        {
            var go = Track(new GameObject("Gold Number Display", typeof(TextMeshProUGUI))) as GameObject;
            return go.GetComponent<TMP_Text>();
        }

        /// <summary>A tile shaped like the shared Inventory Slot prefab (icon + script).</summary>
        private GameObject MakeTilePrefab()
        {
            return Track(new GameObject("Inventory Slot", typeof(RectTransform), typeof(Image), typeof(InventorySlotUI))) as GameObject;
        }

        private static ShopScreen Bind(RectTransform panel, TMP_Text gold, GameObject prefab)
            => ShopScreen.Attach(panel, prefab, gold);

        private static Button BuyButton(RectTransform panel)
            => panel.GetChild(0).GetComponentInChildren<Button>();

        [Test]
        public void Attach_Show_PopulatesOffersAndGoldInPanel()
        {
            inventory.AddGold(50);
            MakePool(Entry(MakeEquipment("Sword"), 10));
            var panel = MakeOffersPanel();
            var gold = MakeGoldLabel();
            var prefab = MakeTilePrefab();

            ShopScreen screen = Bind(panel, gold, prefab);
            screen.Show();

            Assert.AreEqual("50", gold.text, "Show must write the player's gold to the display.");
            Assert.AreEqual(1, panel.childCount, "Shop must spawn one tile per offer.");
            var slot = panel.GetChild(0).GetComponent<InventorySlotUI>();
            Assert.IsNotNull(slot, "Tiles must reuse the Inventory Slot prefab.");
            Assert.IsFalse(slot.Interactable, "Shop tiles must not be draggable.");
            Assert.AreEqual("Sword", slot.EquipmentData.EquipmentName, "Tile must show the offered item.");
        }

        [Test]
        public void BuyTile_Affordable_UnlocksDeductsAndRefreshes()
        {
            inventory.AddGold(50);
            var sword = MakeEquipment("Sword");
            MakePool(Entry(sword, 10));
            var panel = MakeOffersPanel();
            var gold = MakeGoldLabel();
            var prefab = MakeTilePrefab();

            Bind(panel, gold, prefab).Show();

            BuyButton(panel).onClick.Invoke();

            Assert.IsTrue(inventory.IsUnlocked(sword), "Buying must unlock the item.");
            Assert.AreEqual(40, inventory.Gold, "Buying must deduct the item's price.");
            Assert.AreEqual(1, savedCount, "Buying must persist the inventory.");
            Assert.AreEqual("40", gold.text, "Gold display must update after a purchase.");
            Assert.AreEqual(0, panel.childCount, "An owned offer must disappear from the shop.");
        }

        [Test]
        public void BuyTile_TooExpensive_DoesNothing()
        {
            inventory.AddGold(5);
            var sword = MakeEquipment("Sword");
            MakePool(Entry(sword, 10));
            var panel = MakeOffersPanel();
            var gold = MakeGoldLabel();
            var prefab = MakeTilePrefab();

            Bind(panel, gold, prefab).Show();

            BuyButton(panel).onClick.Invoke();

            Assert.IsFalse(inventory.IsUnlocked(sword), "An unaffordable item must not be unlocked.");
            Assert.AreEqual(5, inventory.Gold, "An unaffordable purchase must not change the balance.");
            Assert.AreEqual(0, savedCount, "A failed purchase must not persist.");
            Assert.AreEqual(1, panel.childCount, "An unaffordable offer must remain for another try.");
        }

        [Test]
        public void NoOffers_LeavesPanelEmpty()
        {
            var panel = MakeOffersPanel();
            var gold = MakeGoldLabel();
            var prefab = MakeTilePrefab();

            Bind(panel, gold, prefab).Show();

            Assert.AreEqual(0, panel.childCount, "An empty pool must show no shop tiles.");
        }
    }
}